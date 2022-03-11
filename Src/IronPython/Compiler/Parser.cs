// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler.Ast;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Compiler {
    // this file is your friend (check the python version that is being developed against)
    // https://raw.githubusercontent.com/python/cpython/v3.4.10/Grammar/Grammar
    // the parser itself should match the grammar rules in this file. the closer it is, the easier it is to add new
    // features and fix bugs since the code flow can be read alongside the rules in the file.
    //
    // it's not entirely clear whether there is a name for the syntax used in this file, however i found this helpful
    // http://matt.might.net/articles/grammars-bnf-ebnf/
    //   '|' is or rather than '/'
    //   * means repetition of 0 or more
    //   # starts a comment
    // that's probably about all you need to know to get stuck in.

    public class Parser : IDisposable { // TODO: remove IDisposable
        // immutable properties:
        private readonly Tokenizer _tokenizer;

        // mutable properties:
        private ErrorSink _errors;
        private ParserSink _sink;

        // resettable properties:
        private SourceUnit _sourceUnit;

        /// <summary>
        /// Language features initialized on parser construction and possibly updated during parsing. 
        /// The code can set the language features (e.g. "from __future__ import division").
        /// </summary>
        private ModuleOptions _languageFeatures;

        // state:
        private TokenWithSpan _token;
        private TokenWithSpan _lookahead;
        private Stack<FunctionDefinition> _functions;
        private Stack<ClassDefinition> _classes;
        private bool _fromFutureAllowed;
        private string _privatePrefix;
        private bool _parsingStarted, _allowIncomplete;
        private bool _inLoop, _inFinally, _inFinallyLoop;
        private SourceCodeReader _sourceReader;
        private int _errorCode;
        private readonly CompilerContext _context;
        private PythonAst _globalParent;

        private static readonly char[] newLineChar = new char[] { '\n' };
        private static readonly char[] whiteSpace = { ' ', '\t' };

        #region Construction

        private Parser(CompilerContext context, Tokenizer tokenizer, ErrorSink errorSink, ParserSink parserSink, ModuleOptions languageFeatures) {
            ContractUtils.RequiresNotNull(tokenizer, nameof(tokenizer));
            ContractUtils.RequiresNotNull(errorSink, nameof(errorSink));
            ContractUtils.RequiresNotNull(parserSink, nameof(parserSink));

            tokenizer.ErrorSink = new TokenizerErrorSink(this);

            _tokenizer = tokenizer;
            ErrorSink = errorSink;
            ParserSink = parserSink;
            _context = context;

            Reset(tokenizer.SourceUnit, languageFeatures);
        }

        public static Parser CreateParser(CompilerContext context, PythonOptions options) {
            return CreateParserWorker(context, options, false);
        }

        [Obsolete("pass verbatim via PythonCompilerOptions in PythonOptions")]
        public static Parser CreateParser(CompilerContext context, PythonOptions options, bool verbatim) {
            return CreateParserWorker(context, options, verbatim);
        }

        private static Parser CreateParserWorker(CompilerContext context, PythonOptions options, bool verbatim) {
            ContractUtils.RequiresNotNull(context, nameof(context));
            ContractUtils.RequiresNotNull(options, nameof(options));

            PythonCompilerOptions compilerOptions = context.Options as PythonCompilerOptions;
            if (options == null) {
                throw new ValueErrorException(Resources.PythonContextRequired);
            }

            SourceCodeReader reader;

            try {
                reader = context.SourceUnit.GetReader();

                if (compilerOptions.SkipFirstLine) {
                    reader.ReadLine();
                }
            } catch (IOException e) {
                context.Errors.Add(context.SourceUnit, e.Message, SourceSpan.Invalid, 0, Severity.Error);
                throw;
            }

            Tokenizer tokenizer = new Tokenizer(context.Errors, compilerOptions, verbatim);

            tokenizer.Initialize(null, reader, context.SourceUnit, SourceLocation.MinValue);

            Parser result = new Parser(context, tokenizer, context.Errors, context.ParserSink, compilerOptions.Module);
            result._sourceReader = reader;
            return result;
        }

        #endregion

        #region Public parser interface

        public PythonAst ParseFile(bool makeModule) {
            return ParseFile(makeModule, false);
        }

        //single_input: Newline | simple_stmt | compound_stmt Newline
        //eval_input: testlist Newline* ENDMARKER
        //file_input: (Newline | stmt)* ENDMARKER
        public PythonAst ParseFile(bool makeModule, bool returnValue) {
            try {
                return ParseFileWorker(makeModule, returnValue);
            } catch (DecoderFallbackException dfe) {
                throw BadSourceError(dfe);
            }
        }

#nullable enable

        internal Expression? ParseFString(string expression) {
            var sourceUnit = DefaultContext.DefaultPythonContext.CreateSnippet(expression, "<string>", SourceCodeKind.Expression);
            var context = new CompilerContext(sourceUnit, _context.Options, _context.Errors, _context.ParserSink);
            using var parser = CreateParser(context, new PythonOptions());
            var ast = parser.ParseSingleStatement();
            if (parser.ErrorCode != 0) return null;
            var body = ast.Body as ExpressionStatement;
            return body?.Expression;
        }

#nullable restore

        //[stmt_list] Newline | compound_stmt Newline
        //stmt_list ::= simple_stmt (";" simple_stmt)* [";"]
        //compound_stmt: if_stmt | while_stmt | for_stmt | try_stmt | funcdef | classdef
        //Returns a simple or coumpound_stmt or null if input is incomplete
        /// <summary>
        /// Parse one or more lines of interactive input
        /// </summary>
        /// <returns>null if input is not yet valid but could be with more lines</returns>
        public PythonAst ParseInteractiveCode(out ScriptCodeParseResult properties) {
            bool parsingMultiLineCmpdStmt;
            bool isEmptyStmt = false;

            properties = ScriptCodeParseResult.Complete;

            _globalParent = new PythonAst(false, _languageFeatures, true, _context);
            StartParsing();
            Statement ret = InternalParseInteractiveInput(out parsingMultiLineCmpdStmt, out isEmptyStmt);

            if (_errorCode == 0) {
                if (isEmptyStmt) {
                    properties = ScriptCodeParseResult.Empty;
                } else if (parsingMultiLineCmpdStmt) {
                    properties = ScriptCodeParseResult.IncompleteStatement;
                }

                if (isEmptyStmt) {
                    return null;
                }

                return FinishParsing(ret);
            } else {
                if ((_errorCode & ErrorCodes.IncompleteMask) != 0) {
                    if ((_errorCode & ErrorCodes.IncompleteToken) != 0) {
                        properties = ScriptCodeParseResult.IncompleteToken;
                        return null;
                    }

                    if ((_errorCode & ErrorCodes.IncompleteStatement) != 0) {
                        if (parsingMultiLineCmpdStmt) {
                            properties = ScriptCodeParseResult.IncompleteStatement;
                        } else {
                            properties = ScriptCodeParseResult.IncompleteToken;
                        }
                        return null;
                    }
                }

                properties = ScriptCodeParseResult.Invalid;
                return null;
            }
        }

        private PythonAst FinishParsing(Statement ret) {
            var res = _globalParent;
            _globalParent = null;
            var lineLocs = _tokenizer.GetLineLocations();
            // update line mapping
            if (_sourceUnit.HasLineMapping) {
                List<int> newLineMapping = new List<int>();
                int last = 0;
                for (int i = 0; i < lineLocs.Length; i++) {
                    while (newLineMapping.Count < i) {
                        newLineMapping.Add(last);
                    }
                    last = lineLocs[i] + 1;
                    newLineMapping.Add(lineLocs[i]);
                }

                lineLocs = newLineMapping.ToArray();
            }
            res.ParsingFinished(lineLocs, ret, _languageFeatures);

            return res;
        }

        public PythonAst ParseSingleStatement() {
            try {
                _globalParent = new PythonAst(false, _languageFeatures, true, _context);
                StartParsing();

                MaybeEatNewLine();
                Statement statement = ParseStmt();
                EatEndOfInput();
                return FinishParsing(statement);
            } catch (DecoderFallbackException dfe) {
                throw BadSourceError(dfe);
            }
        }

        public PythonAst ParseTopExpression() {
            try {
                // TODO: move from source unit  .TrimStart(' ', '\t')
                _globalParent = new PythonAst(false, _languageFeatures, false, _context);
                ReturnStatement ret = new ReturnStatement(ParseTestListAsExpression());
                ret.SetLoc(_globalParent, 0, 0);
                return FinishParsing(ret);
            } catch (DecoderFallbackException dfe) {
                throw BadSourceError(dfe);
            }
        }

        /// <summary>
        /// Given the interactive text input for a compound statement, calculate what the
        /// indentation level of the next line should be
        /// </summary>
        public static int GetNextAutoIndentSize(string text, int autoIndentTabWidth) {
            ContractUtils.RequiresNotNull(text, nameof(text));

            Debug.Assert(text[text.Length - 1] == '\n');
            string[] lines = text.Split(newLineChar);
            if (lines.Length <= 1) return 0;
            string lastLine = lines[lines.Length - 2];

            // Figure out the number of white-spaces at the start of the last line
            int startingSpaces = 0;
            while (startingSpaces < lastLine.Length && lastLine[startingSpaces] == ' ')
                startingSpaces++;

            // Assume the same indent as the previous line
            int autoIndentSize = startingSpaces;
            // Increase the indent if this looks like the start of a compounds statement.
            // Ideally, we would ask the parser to tell us the exact indentation level
            if (lastLine.TrimEnd(whiteSpace).EndsWith(":", StringComparison.Ordinal))
                autoIndentSize += autoIndentTabWidth;

            return autoIndentSize;
        }

#nullable enable

        public ErrorSink ErrorSink {
            get {
                return _errors;
            }
            set {
                ContractUtils.RequiresNotNull(value, nameof(value));
                _errors = value;
            }
        }

        public ParserSink? ParserSink {
            get {
                return _sink;
            }
            set {
                _sink = value == ParserSink.Null ? null : value;
            }
        }

#nullable restore

        public int ErrorCode {
            get { return _errorCode; }
        }

        public void Reset(SourceUnit sourceUnit, ModuleOptions languageFeatures) {
            ContractUtils.RequiresNotNull(sourceUnit, nameof(sourceUnit));

            _sourceUnit = sourceUnit;
            _languageFeatures = languageFeatures;
            _token = new TokenWithSpan();
            _lookahead = new TokenWithSpan();
            _fromFutureAllowed = true;
            _functions = null;
            _privatePrefix = null;

            _parsingStarted = false;
            _errorCode = 0;
        }

        public void Reset() {
            Reset(_sourceUnit, _languageFeatures);
        }

        #endregion

        #region Error Reporting

        private void ReportSyntaxError(TokenWithSpan t, int errorCode = ErrorCodes.SyntaxError) {
            ReportSyntaxError(t.Token, t.Span, errorCode, true);
        }

        private void ReportSyntaxError(Token t, IndexSpan span, int errorCode, bool allowIncomplete) {
            var start = span.Start;
            var end = span.End;

            if (allowIncomplete && (t.Kind == TokenKind.EndOfFile || (_tokenizer.IsEndOfFile && (t.Kind == TokenKind.Dedent || t.Kind == TokenKind.NLToken)))) {
                errorCode |= ErrorCodes.IncompleteStatement;
            }

            string msg = string.Format(System.Globalization.CultureInfo.InvariantCulture, GetErrorMessage(t, errorCode), t.Image);

            ReportSyntaxError(start, end, msg, errorCode);
        }

        private static string GetErrorMessage(Token t, int errorCode) {
            string msg;
            if ((errorCode & ~ErrorCodes.IncompleteMask) == ErrorCodes.IndentationError) {
                msg = Resources.ExpectedIndentation;
            } else if (t.Kind == TokenKind.EndOfFile) {
                msg = "unexpected EOF while parsing";
            } else {
                msg = "invalid syntax";
            }

            return msg;
        }

        private void ReportSyntaxError(string message) {
            ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, message);
        }

        internal void ReportSyntaxError(int start, int end, string message, int errorCode = ErrorCodes.SyntaxError) {
            // save the first one, the next error codes may be induced errors:
            if (_errorCode == 0) {
                _errorCode = errorCode;
            }
            ErrorSink.Add(_sourceUnit,
                message,
                new SourceSpan(_tokenizer.IndexToLocation(start), _tokenizer.IndexToLocation(end)),
                errorCode,
                Severity.FatalError);
        }

        #endregion        

        #region LL(1) Parsing

        private static bool IsPrivateName(string name) {
            return name.StartsWith("__", StringComparison.Ordinal) && !name.EndsWith("__", StringComparison.Ordinal);
        }

        private string FixName(string name) {
            if (_privatePrefix != null && IsPrivateName(name)) {
                name = "_" + _privatePrefix + name;
            }

            return name;
        }

        private string ReadName() {
            if (PeekToken() is NameToken n) {
                NextToken();
                return FixName(n.Name);
            }

            ReportSyntaxError(_lookahead);
            return null;
        }

        //stmt: simple_stmt | compound_stmt
        //compound_stmt: if_stmt | while_stmt | for_stmt | try_stmt | with_stmt | funcdef | classdef | decorated | async_stmt        
        private Statement ParseStmt() {
            switch (PeekToken().Kind) {
                case TokenKind.KeywordIf:
                    return ParseIfStmt();
                case TokenKind.KeywordWhile:
                    return ParseWhileStmt();
                case TokenKind.KeywordFor:
                    return ParseForStmt();
                case TokenKind.KeywordTry:
                    return ParseTryStatement();
                case TokenKind.At:
                    return ParseDecorated();
                case TokenKind.KeywordDef:
                    return ParseFuncDef();
                case TokenKind.KeywordClass:
                    return ParseClassDef();
                case TokenKind.KeywordWith:
                    return ParseWithStmt();
                case TokenKind.KeywordAsync:
                    return ParseAsyncStmt();
                default:
                    return ParseSimpleStmt();
            }
        }

        //simple_stmt: small_stmt (';' small_stmt)* [';'] Newline
        private Statement ParseSimpleStmt() {
            Statement s = ParseSmallStmt();
            if (MaybeEat(TokenKind.Semicolon)) {
                var start = s.StartIndex;
                List<Statement> l = new List<Statement>();
                l.Add(s);
                while (true) {
                    if (MaybeEatNewLine() || MaybeEat(TokenKind.EndOfFile)) {
                        break;
                    }

                    l.Add(ParseSmallStmt());

                    if (MaybeEat(TokenKind.EndOfFile)) {
                        // implies a new line
                        break;
                    } else if (!MaybeEat(TokenKind.Semicolon)) {
                        EatNewLine();
                        break;
                    }
                }
                Statement[] stmts = l.ToArray();

                SuiteStatement ret = new SuiteStatement(stmts);
                ret.SetLoc(_globalParent, start, stmts[stmts.Length - 1].EndIndex);
                return ret;
            } else if (!MaybeEat(TokenKind.EndOfFile) && !EatNewLine()) {
                // error handling, make sure we're making forward progress
                NextToken();
            }
            return s;
        }

        /*
        small_stmt: expr_stmt | del_stmt | pass_stmt | flow_stmt | import_stmt | global_stmt | nonlocal_stmt | assert_stmt
        
        del_stmt: 'del' exprlist
        pass_stmt: 'pass'
        flow_stmt: break_stmt | continue_stmt | return_stmt | raise_stmt | yield_stmt
        break_stmt: 'break'
        continue_stmt: 'continue'
        return_stmt: 'return' [testlist]
        yield_stmt: yield_expr
        */
        private Statement ParseSmallStmt() {
            switch (PeekToken().Kind) {
                case TokenKind.KeywordPass:
                    return FinishSmallStmt(new EmptyStatement());
                case TokenKind.KeywordBreak:
                    if (!_inLoop) {
                        ReportSyntaxError("'break' outside loop");
                    }
                    return FinishSmallStmt(new BreakStatement());
                case TokenKind.KeywordContinue:
                    if (!_inLoop) {
                        ReportSyntaxError("'continue' not properly in loop");
                    } else if (_inFinally && !_inFinallyLoop) {
                        ReportSyntaxError("'continue' not supported inside 'finally' clause");
                    }
                    return FinishSmallStmt(new ContinueStatement());
                case TokenKind.KeywordReturn:
                    return ParseReturnStmt();
                case TokenKind.KeywordFrom:
                    return ParseFromImportStmt();
                case TokenKind.KeywordImport:
                    return ParseImportStmt();
                case TokenKind.KeywordGlobal:
                    return ParseGlobalStmt();
                case TokenKind.KeywordNonlocal:
                    return ParseNonLocalStmt();
                case TokenKind.KeywordRaise:
                    return ParseRaiseStmt();
                case TokenKind.KeywordAssert:
                    return ParseAssertStmt();
                case TokenKind.KeywordDel:
                    return ParseDelStmt();
                case TokenKind.KeywordYield:
                    return ParseYieldStmt();
                default:
                    return ParseExprStmt();
            }
        }

        // del_stmt: "del" exprlist
        //  for error reporting reasons we allow any expression and then report the bad
        //  delete node when it fails
        private Statement ParseDelStmt() {
            NextToken();
            var start = GetStart();
            List<Expression> l = ParseExprList(out _);
            foreach (Expression e in l) {
                string delError = e.CheckDelete();
                if (delError != null) {
                    ReportSyntaxError(e.StartIndex, e.EndIndex, delError, ErrorCodes.SyntaxError);
                }
            }

            DelStatement ret = new DelStatement(l.ToArray());
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // return_stmt: 'return' [testlist]
        private Statement ParseReturnStmt() {
            if (CurrentFunction == null) {
                ReportSyntaxError(IronPython.Resources.MisplacedReturn);
            }
            NextToken();
            Expression expr = null;
            var start = GetStart();
            if (!NeverTestToken(PeekToken())) {
                expr = ParseTestList();
            }

            ReturnStatement ret = new ReturnStatement(expr);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Statement FinishSmallStmt(Statement stmt) {
            NextToken();
            stmt.SetLoc(_globalParent, GetStart(), GetEnd());
            return stmt;
        }

        // yield_stmt: yield_expr
        private Statement ParseYieldStmt() {
            // For yield statements, continue to enforce that it's currently in a function. 
            // This gives us better syntax error reporting for yield-statements than for yield-expressions.
            FunctionDefinition current = CurrentFunction;
            if (current == null) {
                ReportSyntaxError(IronPython.Resources.MisplacedYield);
            }

            Eat(TokenKind.KeywordYield);

            // See Pep 342: a yield statement is now just an expression statement around a yield expression.
            Expression e = ParseYieldExpression();
            Debug.Assert(e != null); // caller already verified we have a yield.

            Statement s = new ExpressionStatement(e);
            s.SetLoc(_globalParent, e.IndexSpan);
            return s;
        }

        /// <summary>
        /// Peek if the next token is a 'yield' and parse a yield expression. Else return null.
        /// 
        /// Called w/ yield already eaten.
        /// </summary>
        /// <returns>A yield expression if present, else null. </returns>

        // yield_expr: 'yield' [yield_arg]
        // yield_arg: 'from' test | testlist

        private Expression ParseYieldExpression() {
            // Mark that this function is actually a generator.
            // If we're in a generator expression, then we don't have a function yet.
            //    g=((yield i) for i in range(5))
            // In that acse, the genexp will mark IsGenerator. 
            FunctionDefinition current = CurrentFunction;
            if (current != null) {
                current.IsGenerator = true;
                current.GeneratorStop = GeneratorStop;
            }

            var start = GetStart();

            Expression yieldExpression;
            if (MaybeEat(TokenKind.KeywordFrom)) {
                Expression yieldResult = ParseTest();
                yieldResult.SetLoc(_globalParent, start, GetEnd());
                yieldExpression = new YieldFromExpression(yieldResult);
            } else {
                // Parse expression list after yield. This can be:
                // 1) empty
                // 2) a single expression
                // 3) multiple expression, in which case it's wrapped in a tuple.
                Expression yieldResult;

                bool trailingComma;
                List<Expression> l = ParseTestList(out trailingComma);
                if (l.Count == 0) {
                    yieldResult = null;
                } else if (l.Count != 1) {
                    // make a tuple
                    yieldResult = MakeTupleOrExpr(l, trailingComma);
                } else {
                    // just take the single expression
                    yieldResult = l[0];
                }
                yieldExpression = new YieldExpression(yieldResult);
            }

            yieldExpression.SetLoc(_globalParent, start, GetEnd());
            return yieldExpression;
        }

        private Statement FinishAssignments(Expression right) {
            List<Expression> left = null;
            Expression singleLeft = null;

            while (MaybeEat(TokenKind.Assign)) {
                if (right.CheckAssign() is { } assignError) {
                    ReportSyntaxError(right.StartIndex, right.EndIndex, assignError, ErrorCodes.SyntaxError | ErrorCodes.NoCaret);
                }

                if (singleLeft == null) {
                    singleLeft = right;
                } else {
                    if (left == null) {
                        left = new List<Expression> { singleLeft };
                    }

                    left.Add(right);
                }

                right = MaybeEat(TokenKind.KeywordYield) ? ParseYieldExpression() : ParseTestListStarExpr();
            }

            var target = left?.ToArray() ?? new[] { singleLeft };

            Debug.Assert(target.Length > 0);
            Debug.Assert(target[0] != null);

            var assign = new AssignmentStatement(target, right);
            assign.SetLoc(_globalParent, target[0].StartIndex, right.EndIndex);
            return assign;
        }

        // expr_stmt: testlist_star_expr (augassign (yield_expr|testlist) | ('=' (yield_expr|testlist_star_expr))*)
        // augassign: ('+=' | '-=' | '*=' | '@=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<=' | '>>=' | '**=' | '//=')
        private Statement ParseExprStmt() {
            Expression ret = ParseTestListStarExpr();
            if (ret is ErrorExpression) {
                NextToken();
            }

            if (PeekToken(TokenKind.Assign)) {
                return FinishAssignments(ret);
            }

            PythonOperator op = GetAssignOperator(PeekToken());
            if (op != PythonOperator.None) {
                NextToken();
                Expression rhs;

                if (MaybeEat(TokenKind.KeywordYield)) {
                    rhs = ParseYieldExpression();
                } else {
                    rhs = ParseTestList();
                }

                string assignError = ret.CheckAugmentedAssign();
                if (assignError != null) {
                    ReportSyntaxError(assignError);
                }

                AugmentedAssignStatement aug = new AugmentedAssignStatement(op, ret, rhs);
                aug.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                return aug;
            }

            Statement stmt = new ExpressionStatement(ret);
            stmt.SetLoc(_globalParent, ret.IndexSpan);
            return stmt;
        }

        private PythonOperator GetAssignOperator(Token t) {
            switch (t.Kind) {
                case TokenKind.AddEqual: return PythonOperator.Add;
                case TokenKind.SubtractEqual: return PythonOperator.Subtract;
                case TokenKind.MultiplyEqual: return PythonOperator.Multiply;
                case TokenKind.MatMultEqual: return PythonOperator.MatMult;
                case TokenKind.FloorDivideEqual: return PythonOperator.FloorDivide;
                case TokenKind.TrueDivideEqual: return PythonOperator.TrueDivide;
                case TokenKind.ModEqual: return PythonOperator.Mod;
                case TokenKind.BitwiseAndEqual: return PythonOperator.BitwiseAnd;
                case TokenKind.BitwiseOrEqual: return PythonOperator.BitwiseOr;
                case TokenKind.ExclusiveOrEqual: return PythonOperator.Xor;
                case TokenKind.LeftShiftEqual: return PythonOperator.LeftShift;
                case TokenKind.RightShiftEqual: return PythonOperator.RightShift;
                case TokenKind.PowerEqual: return PythonOperator.Power;
                default: return PythonOperator.None;
            }
        }

        private PythonOperator GetBinaryOperator(OperatorToken token) {
            switch (token.Kind) {
                case TokenKind.Add: return PythonOperator.Add;
                case TokenKind.Subtract: return PythonOperator.Subtract;
                case TokenKind.Multiply: return PythonOperator.Multiply;
                case TokenKind.MatMult: return PythonOperator.MatMult;
                case TokenKind.FloorDivide: return PythonOperator.FloorDivide;
                case TokenKind.TrueDivide: return PythonOperator.TrueDivide;
                case TokenKind.Mod: return PythonOperator.Mod;
                case TokenKind.BitwiseAnd: return PythonOperator.BitwiseAnd;
                case TokenKind.BitwiseOr: return PythonOperator.BitwiseOr;
                case TokenKind.ExclusiveOr: return PythonOperator.Xor;
                case TokenKind.LeftShift: return PythonOperator.LeftShift;
                case TokenKind.RightShift: return PythonOperator.RightShift;
                case TokenKind.Power: return PythonOperator.Power;
                default:
                    string message = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Resources.UnexpectedToken,
                        token.Kind);
                    Debug.Assert(false, message);
                    throw new ValueErrorException(message);
            }
        }

        // import_stmt: 'import' module ['as' name"] (',' module ['as' name])*        
        // name: identifier
        private ImportStatement ParseImportStmt() {
            Eat(TokenKind.KeywordImport);
            var start = GetStart();

            List<ModuleName> l = new List<ModuleName>();
            List<string> las = new List<string>();
            l.Add(ParseModuleName());
            las.Add(MaybeParseAsName());
            while (MaybeEat(TokenKind.Comma)) {
                l.Add(ParseModuleName());
                las.Add(MaybeParseAsName());
            }
            ModuleName[] names = l.ToArray();
            var asNames = las.ToArray();

            ImportStatement ret = new ImportStatement(names, asNames);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // module: (identifier '.')* identifier
        private ModuleName ParseModuleName() {
            var start = GetStart();
            ModuleName ret = new ModuleName(ReadNames());
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // relative_module: "."* module | "."+
        private ModuleName ParseRelativeModuleName() {
            var start = GetStart();

            int dotCount = 0;
            while (PeekToken(TokenKind.Constant) && PeekToken().Value == Ellipsis.Value) {
                dotCount += 3;
                NextToken();
            }
            while (MaybeEat(TokenKind.Dot)) {
                dotCount++;
            }

            string[] names = ArrayUtils.EmptyStrings;
            if (PeekToken() is NameToken) {
                names = ReadNames();
            }

            ModuleName ret;
            if (dotCount > 0) {
                ret = new RelativeModuleName(names, dotCount);
            } else {
                if (names.Length == 0) {
                    ReportSyntaxError("invalid syntax");
                }
                ret = new ModuleName(names);
            }

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private string[] ReadNames() {
            List<string> l = new List<string>();
            l.Add(ReadName());
            while (MaybeEat(TokenKind.Dot)) {
                l.Add(ReadName());
            }
            return l.ToArray();
        }


        // 'from' relative_module 'import' identifier ['as' name] (',' identifier ['as' name]) *
        // 'from' relative_module 'import' '(' identifier ['as' name] (',' identifier ['as' name])* [','] ')'        
        // 'from' module 'import' "*"                                        
        private FromImportStatement ParseFromImportStmt() {
            Eat(TokenKind.KeywordFrom);
            var start = GetStart();
            ModuleName dname = ParseRelativeModuleName();

            Eat(TokenKind.KeywordImport);

            bool ateParen = MaybeEat(TokenKind.LeftParenthesis);

            string[] names;
            string[] asNames;
            bool fromFuture = false;

            if (MaybeEat(TokenKind.Multiply)) {
                names = (string[])FromImportStatement.Star;
                asNames = null;
            } else {
                List<string> l = new List<string>();
                List<string> las = new List<string>();

                if (MaybeEat(TokenKind.LeftParenthesis)) {
                    ParseAsNameList(l, las);
                    Eat(TokenKind.RightParenthesis);
                } else {
                    ParseAsNameList(l, las);
                }
                names = l.ToArray();
                asNames = las.ToArray();
            }

            // Process from __future__ statement

            if (dname.Names.Count == 1 && dname.Names[0] == "__future__") {
                if (!_fromFutureAllowed) {
                    ReportSyntaxError(Resources.MisplacedFuture);
                }
                if (names == FromImportStatement.Star) {
                    ReportSyntaxError(Resources.NoFutureStar);
                }
                fromFuture = true;
                foreach (string name in names) {
                    if (name == "division") {
                        // Ignored in Python 3
                    } else if (name == "with_statement") {
                        // Ignored in Python 2.7
                    } else if (name == "absolute_import") {
                        // Ignored in Python 3
                    } else if (name == "print_function") {
                        // Ignored in Python 3
                    } else if (name == "unicode_literals") {
                        // Ignored in Python 3
                    } else if (name == "generator_stop") {
                        // New in 3.5, mandatory in 3.7
                        _languageFeatures |= ModuleOptions.GeneratorStop;
                    } else if (name == "nested_scopes") {
                    } else if (name == "generators") {
                    } else {
                        string strName = name;
                        fromFuture = false;

                        if (strName != "braces") {
                            ReportSyntaxError(Resources.UnknownFutureFeature + strName);
                        } else {
                            // match CPython error message
                            ReportSyntaxError(Resources.NotAChance);
                        }
                    }
                }
            }

            if (ateParen) {
                Eat(TokenKind.RightParenthesis);
            }

            FromImportStatement ret = new FromImportStatement(dname, (string[])names, asNames, fromFuture);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // import_as_name (',' import_as_name)*
        private void ParseAsNameList(List<string> l, List<string> las) {
            l.Add(ReadName());
            las.Add(MaybeParseAsName());
            while (MaybeEat(TokenKind.Comma)) {
                if (PeekToken(TokenKind.RightParenthesis)) return;  // the list is allowed to end with a ,
                l.Add(ReadName());
                las.Add(MaybeParseAsName());
            }
        }

        //import_as_name: NAME [NAME NAME]
        //dotted_as_name: dotted_name [NAME NAME]
        private string MaybeParseAsName() {
            if (MaybeEat(TokenKind.KeywordAs)) {
                return ReadName();
            }
            return null;
        }

        //nonlocal_stmt: 'nonlocal' NAME (',' NAME)*
        private NonlocalStatement ParseNonLocalStmt() {
            Eat(TokenKind.KeywordNonlocal);
            var start = GetStart();
            var l = new List<string>();
            l.Add(ReadName());
            while (MaybeEat(TokenKind.Comma)) {
                l.Add(ReadName());
            }
            string[] names = l.ToArray();
            NonlocalStatement ret = new NonlocalStatement(names);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //global_stmt: 'global' NAME (',' NAME)*
        private GlobalStatement ParseGlobalStmt() {
            Eat(TokenKind.KeywordGlobal);
            var start = GetStart();
            List<string> l = new List<string>();
            l.Add(ReadName());
            while (MaybeEat(TokenKind.Comma)) {
                l.Add(ReadName());
            }
            string[] names = l.ToArray();
            GlobalStatement ret = new GlobalStatement(names);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //raise_stmt: 'raise' [test ['from' test]]
        private RaiseStatement ParseRaiseStmt() {
            Eat(TokenKind.KeywordRaise);
            var start = GetStart();
            Expression exception = null, cause = null;

            if (!NeverTestToken(PeekToken())) {
                exception = ParseTest();
                if (MaybeEat(TokenKind.KeywordFrom)) {
                    cause = ParseTest();
                }
            }
            RaiseStatement ret = new RaiseStatement(exception, cause);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //assert_stmt: 'assert' expression [',' expression]
        private AssertStatement ParseAssertStmt() {
            Eat(TokenKind.KeywordAssert);
            var start = GetStart();
            Expression expr = ParseTest();
            Expression message = null;
            if (MaybeEat(TokenKind.Comma)) {
                message = ParseTest();
            }
            AssertStatement ret = new AssertStatement(expr, message);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private string SetPrivatePrefix(string name) {
            string oldPrefix = _privatePrefix;

            _privatePrefix = GetPrivatePrefix(name);

            return oldPrefix;
        }

        internal static string GetPrivatePrefix(string name) {
            // Remove any leading underscores before saving the prefix
            if (name != null) {
                for (int i = 0; i < name.Length; i++) {
                    if (name[i] != '_') {
                        return name.Substring(i);
                    }
                }
            }
            // Name consists of '_'s only, no private prefix mapping
            return null;
        }

        private ErrorExpression Error() {
            var res = new ErrorExpression();
            res.SetLoc(_globalParent, GetStart(), GetEnd());
            return res;
        }

        private ExpressionStatement ErrorStmt() {
            return new ExpressionStatement(Error());
        }

        //classdef: 'class' NAME ['(' arglist ')'] ':' suite
        private ClassDefinition ParseClassDef() {
            Eat(TokenKind.KeywordClass);

            var start = GetStart();
            string name = ReadName();
            if (name == null) {
                // no name, assume there's no class.
                return new ClassDefinition(string.Empty, null, null, ErrorStmt());
            }

            List<Expression> bases = null;
            List<Keyword> keywords = null;
            if (MaybeEat(TokenKind.LeftParenthesis)) {
                IReadOnlyList<Node> args = FinishArgumentList(null);
                SplitAndValidateArguments(args, out bases, out keywords);
            }
            var mid = GetEnd();

            // Save private prefix
            string savedPrefix = SetPrivatePrefix(name);

            var ret = new ClassDefinition(name, bases, keywords);
            PushClass(ret);

            // Parse the class body
            Statement body = ParseClassOrFuncBody();

            ClassDefinition ret2 = PopClass();
            Debug.Assert(ret == ret2);

            // Restore the private prefix
            _privatePrefix = savedPrefix;

            ret.Body = body;
            ret.HeaderIndex = mid;
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //  decorators ::=
        //      decorator+
        //  decorator ::=
        //      "@" dotted_name ["(" [argument_list [","]] ")"] NEWLINE
        private List<Expression> ParseDecorators() {
            List<Expression> decorators = new List<Expression>();

            while (MaybeEat(TokenKind.At)) {
                var start = GetStart();
                Expression decorator = new NameExpression(ReadName());
                decorator.SetLoc(_globalParent, start, GetEnd());
                while (MaybeEat(TokenKind.Dot)) {
                    string name = ReadName();
                    decorator = new MemberExpression(decorator, name);
                    decorator.SetLoc(_globalParent, GetStart(), GetEnd());
                }
                decorator.SetLoc(_globalParent, start, GetEnd());

                if (MaybeEat(TokenKind.LeftParenthesis)) {
                    ParserSink?.StartParameters(GetSourceSpan());
                    IReadOnlyList<Node> args = FinishArgumentList(null);
                    decorator = FinishCallExpr(decorator, args);
                }
                decorator.SetLoc(_globalParent, start, GetEnd());
                EatNewLine();

                decorators.Add(decorator);
            }

            return decorators;
        }

        // 'def' NAME parameters ['->' test] ':' suite

        //  decorated: decorators(classdef | funcdef | async_funcdef)
        // this gets called with "@" look-ahead
        private Statement ParseDecorated() {
            List<Expression> decorators = ParseDecorators();

            Statement res;

            if (PeekToken() == Tokens.KeywordDefToken) {
                FunctionDefinition fnc = ParseFuncDef();
                fnc.Decorators = decorators.ToArray();
                res = fnc;
            } else if (PeekToken() == Tokens.KeywordClassToken) {
                ClassDefinition cls = ParseClassDef();
                cls.Decorators = decorators.ToArray();
                res = cls;
            } else if (PeekToken() == Tokens.KeywordAsyncToken) {
                Eat(TokenKind.KeywordAsync);
                FunctionDefinition fnc = ParseFuncDef(true);
                fnc.Decorators = decorators.ToArray();
                res = fnc;
            } else {
                res = new EmptyStatement();
                ReportSyntaxError(_lookahead);
            }

            return res;
        }

        // funcdef: [decorators] 'def' NAME parameters ':' suite
        // parameters: '(' [typedargslist] ')'
        // this gets called with "def" as the look-ahead
        private FunctionDefinition ParseFuncDef(bool isAsync = false) {
            Eat(TokenKind.KeywordDef);
            var start = GetStart();
            string name = ReadName();

            Eat(TokenKind.LeftParenthesis);

            var lStart = GetStart();
            var lEnd = GetEnd();
            int grouping = _tokenizer.GroupingLevel;

            Parameter[] parameters = ParseParameterList(TokenKind.RightParenthesis, allowAnnotations: true);
            FunctionDefinition ret;
            if (parameters == null) {
                // error in parameters
                ret = new FunctionDefinition(name, Array.Empty<Parameter>(), isAsync);
                ret.SetLoc(_globalParent, start, lEnd);
                return ret;
            }

            Expression annotation = null;
            if (MaybeEat(TokenKind.ReturnAnnotation)) {
                annotation = ParseTest();
            }

            var rStart = GetStart();
            var rEnd = GetEnd();

            ret = new FunctionDefinition(name, parameters, isAsync);
            PushFunction(ret);

            Statement body = ParseClassOrFuncBody();
            FunctionDefinition ret2 = PopFunction();
            Debug.Assert(ret == ret2);

            ret.Body = body;
            ret.HeaderIndex = rEnd;
            ret.ReturnAnnotation = annotation;

            ParserSink?.MatchPair(
                new SourceSpan(_tokenizer.IndexToLocation(lStart), _tokenizer.IndexToLocation(lEnd)),
                new SourceSpan(_tokenizer.IndexToLocation(rStart), _tokenizer.IndexToLocation(rEnd)),
                grouping);

            ret.SetLoc(_globalParent, start, body.EndIndex);

            return ret;
        }

        private void CheckUniqueParameter(HashSet<string> names, string name) {
            if (names.Contains(name)) {
                ReportSyntaxError(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Resources.DuplicateArgumentInFuncDef,
                    name));
            }
            names.Add(name);
        }

        // parameter_list ::=
        //     (defparameter ",")*
        //     ( "*" [parameter] ("," defparameter)* ["," "**" parameter]
        //         | "**" parameter
        //         | defparameter[","] )
        private Parameter[] ParseParameterList(TokenKind terminator, bool allowAnnotations) {
            List<Parameter> pl = new List<Parameter>();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            bool needDefault = false;
            bool readMultiply = false;
            bool hasKeywordOnlyParameter = false;
            // we want these to be the last two parameters
            Parameter listParameter = null;
            Parameter dictParameter = null;
            for (int position = 0; ; position++) {
                if (MaybeEat(terminator)) break;

                Parameter parameter;

                if (MaybeEat(TokenKind.Power)) {
                    dictParameter = ParseParameter(names, ParameterKind.Dictionary, allowAnnotations);
                    if (dictParameter == null) {
                        // no parameter name, syntax error
                        return null;
                    }
                    Eat(terminator);
                    break;
                }

                if (MaybeEat(TokenKind.Multiply)) {
                    if (readMultiply) {
                        ReportSyntaxError(_lookahead);
                        return null;
                    }

                    if (PeekToken(TokenKind.Comma)) {
                        // "*"
                    } else {
                        listParameter = ParseParameter(names, ParameterKind.List, allowAnnotations);
                        if (listParameter == null) {
                            // no parameter name, syntax error
                            return null;
                        }
                    }

                    readMultiply = true;
                } else {
                    // If a parameter has a default value, all following parameters up until the "*" must also have a default value
                    if (readMultiply) {
                        bool dontCare = false;
                        parameter = ParseDefParameter(names, ParameterKind.KeywordOnly, allowAnnotations, ref dontCare);
                        hasKeywordOnlyParameter = true;
                    } else {
                        parameter = ParseDefParameter(names, ParameterKind.Normal, allowAnnotations, ref needDefault);
                    }
                    if (parameter == null) {
                        // no parameter, syntax error
                        return null;
                    }

                    pl.Add(parameter);
                }

                if (!MaybeEat(TokenKind.Comma)) {
                    Eat(terminator);
                    break;
                }
            }

            if (readMultiply && listParameter == null && dictParameter != null && !hasKeywordOnlyParameter) {
                // TODO: this should not throw right away
                ReportSyntaxError("named arguments must follow bare *");
            }

            if (listParameter != null) pl.Add(listParameter);
            if (dictParameter != null) pl.Add(dictParameter);

            return pl.ToArray();
        }

        // defparameter ::=
        //     parameter ["=" expression]
        private Parameter ParseDefParameter(HashSet<string> names, ParameterKind parameterKind, bool allowAnnotations, ref bool needDefault) {
            Parameter parameter = ParseParameter(names, parameterKind, allowAnnotations);
            if (parameter != null) {
                if (MaybeEat(TokenKind.Assign)) {
                    needDefault = true;
                    parameter.DefaultValue = ParseTest();
                } else if (needDefault) {
                    ReportSyntaxError(IronPython.Resources.DefaultRequired);
                }
            }
            return parameter;
        }

        // parameter ::=
        //     identifier [":" expression]
        private Parameter ParseParameter(HashSet<string> names, ParameterKind parameterKind, bool allowAnnotations) {
            if (!PeekToken(TokenKind.Name)) {
                ReportSyntaxError(_lookahead);
                return null;
            }

            Token t = NextToken();
            string name = FixName((string)t.Value);
            Parameter parameter = new Parameter(name, parameterKind);
            CompleteParameterName(parameter, name, names);

            // expression
            if (allowAnnotations && MaybeEat(TokenKind.Colon)) {
                parameter.Annotation = ParseTest();
            }

            return parameter;
        }

        private void CompleteParameterName(Node node, string name, HashSet<string> names) {
            ParserSink?.StartName(GetSourceSpan(), name);
            CheckUniqueParameter(names, name);
            node.SetLoc(_globalParent, GetStart(), GetEnd());
        }

        // lambdef_nocond: 'lambda' [varargslist] ':' test_nocond
        private Expression FinishLambdefNoCond() {
            FunctionDefinition func = ParseLambdaHelperStart(null);
            Expression expr = ParseTestNoCond();
            return ParseLambdaHelperEnd(func, expr);
        }

        // lambdef: 'lambda' [varargslist] ':' test
        private Expression FinishLambdef() {
            FunctionDefinition func = ParseLambdaHelperStart(null);
            Expression expr = ParseTest();
            return ParseLambdaHelperEnd(func, expr);
        }

        // Helpers for parsing lambda expressions. 
        // Usage
        //   FunctionDefinition f = ParseLambdaHelperStart(string);
        //   Expression expr = ParseXYZ();
        //   return ParseLambdaHelperEnd(f, expr);
        private FunctionDefinition ParseLambdaHelperStart(string name) {
            var start = GetStart();
            Parameter[] parameters = ParseParameterList(TokenKind.Colon, allowAnnotations: false);
            var mid = GetEnd();

            FunctionDefinition func = new FunctionDefinition(name, parameters ?? Array.Empty<Parameter>()); // new Parameter[0] for error handling of incomplete lambda
            func.HeaderIndex = mid;
            func.StartIndex = start;

            // Push the lambda function on the stack so that it's available for any yield expressions to mark it as a generator.
            PushFunction(func);

            return func;
        }

        private Expression ParseLambdaHelperEnd(FunctionDefinition func, Expression expr) {
            // Pep 342 in Python 2.5 allows Yield Expressions, which can occur inside a Lambda body. 
            // In this case, the lambda is a generator and will yield it's final result instead of just return it.
            Statement body;
            if (func.IsGenerator) {
                YieldExpression y = new YieldExpression(expr);
                y.SetLoc(_globalParent, expr.IndexSpan);
                body = new ExpressionStatement(y);
            } else {
                body = new ReturnStatement(expr);
            }
            body.SetLoc(_globalParent, expr.StartIndex, expr.EndIndex);

            FunctionDefinition func2 = PopFunction();
            System.Diagnostics.Debug.Assert(func == func2);

            func.Body = body;
            func.EndIndex = GetEnd();

            LambdaExpression ret = new LambdaExpression(func);
            func.SetLoc(_globalParent, func.IndexSpan);
            ret.SetLoc(_globalParent, func.IndexSpan);
            return ret;
        }

        //while_stmt: 'while' expression ':' suite ['else' ':' suite]
        private WhileStatement ParseWhileStmt() {
            Eat(TokenKind.KeywordWhile);
            var start = GetStart();
            Expression expr = ParseTest();
            var mid = GetEnd();
            Statement body = ParseLoopSuite();
            Statement else_ = null;
            if (MaybeEat(TokenKind.KeywordElse)) {
                else_ = ParseSuite();
            }
            WhileStatement ret = new WhileStatement(expr, body, else_);
            ret.SetLoc(_globalParent, start, mid, GetEnd());
            return ret;
        }

        private readonly struct WithItem {
            public readonly int Start;
            public readonly Expression ContextManager;
            public readonly Expression Variable;

            public WithItem(int start, Expression contextManager, Expression variable) {
                Start = start;
                ContextManager = contextManager;
                Variable = variable;
            }
        }

        //with_stmt: 'with' with_item (',' with_item)* ':' suite
        //with_item: test ['as' expr]
        private WithStatement ParseWithStmt() {
            Eat(TokenKind.KeywordWith);

            var withItem = ParseWithItem();
            List<WithItem> items = null;
            while (MaybeEat(TokenKind.Comma)) {
                if (items == null) {
                    items = new List<WithItem>();
                }

                items.Add(ParseWithItem());
            }


            var header = GetEnd();
            Statement body = ParseSuite();
            if (items != null) {
                for (int i = items.Count - 1; i >= 0; i--) {
                    var curItem = items[i];
                    var innerWith = new WithStatement(curItem.ContextManager, curItem.Variable, body);
                    innerWith.HeaderIndex = header;
                    innerWith.SetLoc(_globalParent, withItem.Start, GetEnd());
                    body = innerWith;
                    header = GetEnd();
                }
            }

            WithStatement ret = new WithStatement(withItem.ContextManager, withItem.Variable, body);
            ret.HeaderIndex = header;
            ret.SetLoc(_globalParent, withItem.Start, GetEnd());
            return ret;
        }

        private WithItem ParseWithItem() {
            var start = GetStart();
            Expression contextManager = ParseTest();
            Expression var = null;
            if (MaybeEat(TokenKind.KeywordAs)) {
                var = ParseTest();
            }

            return new WithItem(start, contextManager, var);
        }

        // async_stmt: 'async' (funcdef | with_stmt | for_stmt)
        private Statement ParseAsyncStmt() {
            Eat(TokenKind.KeywordAsync);
            ReportSyntaxError("invalid syntax");

            if (PeekToken().Kind == TokenKind.KeywordDef) {
                FunctionDefinition def = ParseFuncDef(true);
                return def;
            }

            return null;
        }

        // for_stmt: 'for' exprlist 'in' testlist ':' suite ['else' ':' suite]
        private ForStatement ParseForStmt() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();

            bool trailingComma;
            List<Expression> l = ParseExprList(out trailingComma);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);
            Expression list = ParseTestList();
            var header = GetEnd();
            Statement body = ParseLoopSuite();
            Statement else_ = null;
            if (MaybeEat(TokenKind.KeywordElse)) {
                else_ = ParseSuite();
            }
            ForStatement ret = new ForStatement(lhs, list, body, else_);
            ret.HeaderIndex = header;
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Statement ParseLoopSuite() {
            Statement body;
            bool inLoop = _inLoop, inFinallyLoop = _inFinallyLoop;
            try {
                _inLoop = true;
                _inFinallyLoop = _inFinally;
                body = ParseSuite();
            } finally {
                _inLoop = inLoop;
                _inFinallyLoop = inFinallyLoop;
            }
            return body;
        }

        private Statement ParseClassOrFuncBody() {
            Statement body;
            bool inLoop = _inLoop,
                 inFinally = _inFinally,
                 inFinallyLoop = _inFinallyLoop;
            try {
                _inLoop = false;
                _inFinally = false;
                _inFinallyLoop = false;
                body = ParseSuite();
            } finally {
                _inLoop = inLoop;
                _inFinally = inFinally;
                _inFinallyLoop = inFinallyLoop;
            }
            return body;
        }

        // if_stmt: 'if' expression ':' suite ('elif' expression ':' suite)* ['else' ':' suite]
        private IfStatement ParseIfStmt() {
            Eat(TokenKind.KeywordIf);
            var start = GetStart();
            List<IfStatementTest> l = new List<IfStatementTest>();
            l.Add(ParseIfStmtTest());

            while (MaybeEat(TokenKind.KeywordElseIf)) {
                l.Add(ParseIfStmtTest());
            }

            Statement else_ = null;
            if (MaybeEat(TokenKind.KeywordElse)) {
                else_ = ParseSuite();
            }

            IfStatementTest[] tests = l.ToArray();
            IfStatement ret = new IfStatement(tests, else_);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private IfStatementTest ParseIfStmtTest() {
            var start = GetStart();
            Expression expr = ParseTest();
            var header = GetEnd();
            Statement suite = ParseSuite();
            IfStatementTest ret = new IfStatementTest(expr, suite);
            ret.SetLoc(_globalParent, start, suite.EndIndex);
            ret.HeaderIndex = header;
            return ret;
        }

        //try_stmt: ('try' ':' suite (except_clause ':' suite)+
        //    ['else' ':' suite] | 'try' ':' suite 'finally' ':' suite)
        //# NB compile.c makes sure that the default except clause is last

        // Python 2.5 grammar
        //try_stmt: 'try' ':' suite
        //          (
        //            (except_clause ':' suite)+
        //            ['else' ':' suite]
        //            ['finally' ':' suite]
        //          |
        //            'finally' : suite
        //          )


        private Statement ParseTryStatement() {
            Eat(TokenKind.KeywordTry);
            var start = GetStart();
            var mid = GetEnd();
            Statement body = ParseSuite();
            Statement finallySuite = null;
            Statement elseSuite = null;
            Statement ret;
            int end;

            // If this function has a try block, then it can set the current exception.
            FunctionDefinition current = CurrentFunction;
            if (current != null) {
                current.CanSetSysExcInfo = true;
            }

            if (MaybeEat(TokenKind.KeywordFinally)) {
                finallySuite = ParseFinallySuite(finallySuite);
                end = finallySuite.EndIndex;
                TryStatement tfs = new TryStatement(body, null, elseSuite, finallySuite);
                tfs.HeaderIndex = mid;
                ret = tfs;
            } else {
                List<TryStatementHandler> handlers = new List<TryStatementHandler>();
                TryStatementHandler dh = null;
                do {
                    TryStatementHandler handler = ParseTryStmtHandler();
                    end = handler.EndIndex;
                    handlers.Add(handler);

                    if (dh != null) {
                        ReportSyntaxError(dh.StartIndex, dh.EndIndex, "default 'except' must be last");
                    }
                    if (handler.Test == null) {
                        dh = handler;
                    }
                } while (PeekToken().Kind == TokenKind.KeywordExcept);

                if (MaybeEat(TokenKind.KeywordElse)) {
                    elseSuite = ParseSuite();
                    end = elseSuite.EndIndex;
                }

                if (MaybeEat(TokenKind.KeywordFinally)) {
                    // If this function has an except block, then it can set the current exception.
                    finallySuite = ParseFinallySuite(finallySuite);
                    end = finallySuite.EndIndex;
                }

                TryStatement ts = new TryStatement(body, handlers.ToArray(), elseSuite, finallySuite);
                ts.HeaderIndex = mid;
                ret = ts;
            }
            ret.SetLoc(_globalParent, start, end);
            return ret;
        }

        private Statement ParseFinallySuite(Statement finallySuite) {
            MarkFunctionContainsFinally();
            bool inFinally = _inFinally, inFinallyLoop = _inFinallyLoop;
            try {
                _inFinally = true;
                _inFinallyLoop = false;
                finallySuite = ParseSuite();
            } finally {
                _inFinally = inFinally;
                _inFinallyLoop = inFinallyLoop;
            }
            return finallySuite;
        }

        private void MarkFunctionContainsFinally() {
            FunctionDefinition current = CurrentFunction;
            if (current != null) {
                current.ContainsTryFinally = true;
            }
        }

        // except_clause: 'except' [expression ['as' identifier]]
        private TryStatementHandler ParseTryStmtHandler() {
            Eat(TokenKind.KeywordExcept);

            var start = GetStart();
            Expression test = null, target = null;
            if (PeekToken().Kind != TokenKind.Colon) {
                test = ParseTest();
                if (MaybeEat(TokenKind.KeywordAs)) {
                    string name = ReadName();
                    target = new NameExpression(FixName(name));
                    target.SetLoc(_globalParent, GetStart(), GetEnd());
                }
            }
            var mid = GetEnd();
            Statement body = ParseSuite();
            TryStatementHandler ret = new TryStatementHandler(test, target, body);
            ret.HeaderIndex = mid;
            ret.SetLoc(_globalParent, start, body.EndIndex);
            return ret;
        }

        //suite: simple_stmt NEWLINE | Newline INDENT stmt+ DEDENT
        private Statement ParseSuite() {
            if (!EatNoEof(TokenKind.Colon)) {
                // improve error handling...
                return ErrorStmt();
            }

            TokenWithSpan cur = _lookahead;
            List<Statement> l = new List<Statement>();

            // we only read a real NewLine here because we need to adjust error reporting
            // for the interpreter.
            if (MaybeEat(TokenKind.NewLine)) {
                CheckSuiteEofError(cur);

                // for error reporting we track the NL tokens and report the error on
                // the last one.  This matches CPython.
                cur = _lookahead;
                while (PeekToken(TokenKind.NLToken)) {
                    cur = _lookahead;
                    NextToken();
                }

                if (!MaybeEat(TokenKind.Indent)) {
                    // no indent?  report the indentation error.
                    if (cur.Token.Kind == TokenKind.Dedent) {
                        ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, Resources.ExpectedIndentation, ErrorCodes.IndentationError | ErrorCodes.IncompleteStatement);
                    } else {
                        ReportSyntaxError(cur, ErrorCodes.IndentationError);
                    }
                    return ErrorStmt();
                }

                while (true) {
                    Statement s = ParseStmt();

                    l.Add(s);

                    if (MaybeEat(TokenKind.Dedent)) break;
                    if (PeekToken().Kind == TokenKind.EndOfFile) {
                        ReportSyntaxError("unexpected end of file");
                        break; // error handling
                    }
                }

                Statement[] stmts = l.ToArray();
                SuiteStatement ret = new SuiteStatement(stmts);
                ret.SetLoc(_globalParent, stmts[0].StartIndex, stmts[stmts.Length - 1].EndIndex);
                return ret;
            } else {
                //  simple_stmt NEWLINE
                //  ParseSimpleStmt takes care of the NEWLINE
                Statement s = ParseSimpleStmt();
                return s;
            }
        }

        private void CheckSuiteEofError(TokenWithSpan cur) {
            if (MaybeEat(TokenKind.EndOfFile)) {
                // for interactive parsing we allow the user to continue in this case
                ReportSyntaxError(_lookahead.Token, cur.Span, ErrorCodes.SyntaxError, true);
            }
        }

        // test_nocond: or_test | lambdef_nocond
        private Expression ParseTestNoCond() {
            if (MaybeEat(TokenKind.KeywordLambda)) {
                return FinishLambdefNoCond();
            }
            return ParseOrTest();
        }

        // test: or_test ['if' or_test 'else' test] | lambdef
        private Expression ParseTest() {
            if (MaybeEat(TokenKind.KeywordLambda)) {
                return FinishLambdef();
            }

            Expression ret = ParseOrTest();
            if (MaybeEat(TokenKind.KeywordIf)) {
                var start = ret.StartIndex;
                ret = ParseConditionalTest(ret);
                ret.SetLoc(_globalParent, start, GetEnd());
            }

            return ret;
        }

        // or_test: and_test ('or' and_test)*
        private Expression ParseOrTest() {
            Expression ret = ParseAndTest();
            while (MaybeEat(TokenKind.KeywordOr)) {
                var start = ret.StartIndex;
                ret = new OrExpression(ret, ParseAndTest());
                ret.SetLoc(_globalParent, start, GetEnd());
            }
            return ret;
        }

        private Expression ParseConditionalTest(Expression trueExpr) {
            Expression expr = ParseOrTest();
            Eat(TokenKind.KeywordElse);
            Expression falseExpr = ParseTest();
            return new ConditionalExpression(expr, trueExpr, falseExpr);
        }

        // and_test: not_test ('and' not_test)*
        private Expression ParseAndTest() {
            Expression ret = ParseNotTest();
            while (MaybeEat(TokenKind.KeywordAnd)) {
                var start = ret.StartIndex;
                ret = new AndExpression(ret, ParseAndTest());
                ret.SetLoc(_globalParent, start, GetEnd());
            }
            return ret;
        }

        // not_test: 'not' not_test | comparison
        private Expression ParseNotTest() {
            if (MaybeEat(TokenKind.KeywordNot)) {
                var start = GetStart();
                Expression ret = new UnaryExpression(PythonOperator.Not, ParseNotTest());
                ret.SetLoc(_globalParent, start, GetEnd());
                return ret;
            } else {
                return ParseComparison();
            }
        }

        // comparison: expr (comp_op expr)*
        // comp_op: '<'|'>'|'=='|'>='|'<='|'!='|'in'|'not' 'in'|'is'|'is' 'not'
        private Expression ParseComparison() {
            Expression ret = ParseExpr();
            while (true) {
                PythonOperator op;
                switch (PeekToken().Kind) {
                    case TokenKind.LessThan: NextToken(); op = PythonOperator.LessThan; break;
                    case TokenKind.LessThanOrEqual: NextToken(); op = PythonOperator.LessThanOrEqual; break;
                    case TokenKind.GreaterThan: NextToken(); op = PythonOperator.GreaterThan; break;
                    case TokenKind.GreaterThanOrEqual: NextToken(); op = PythonOperator.GreaterThanOrEqual; break;
                    case TokenKind.Equals: NextToken(); op = PythonOperator.Equal; break;
                    case TokenKind.NotEquals: NextToken(); op = PythonOperator.NotEqual; break;

                    case TokenKind.KeywordIn: NextToken(); op = PythonOperator.In; break;
                    case TokenKind.KeywordNot: NextToken(); Eat(TokenKind.KeywordIn); op = PythonOperator.NotIn; break;

                    case TokenKind.KeywordIs:
                        NextToken();
                        if (MaybeEat(TokenKind.KeywordNot)) {
                            op = PythonOperator.IsNot;
                        } else {
                            op = PythonOperator.Is;
                        }
                        break;
                    default:
                        return ret;
                }
                Expression rhs = ParseComparison();
                BinaryExpression be = new BinaryExpression(op, ret, rhs);
                be.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                ret = be;
            }
        }

        // expr: xor_expr ('|' xor_expr)*
        // xor_expr: and_expr ('^' and_expr)*
        // and_expr: shift_expr ('&' shift_expr)*
        // shift_expr: arith_expr (('<<'|'>>') arith_expr)*
        // arith_expr: term (('+'|'-') term)*
        // term: factor (('*'|'/'|'%'|'//') factor)*
        private Expression ParseExpr(int precedence = 0) {
            Expression ret = ParseFactor();
            while (true) {
                Token t = PeekToken();
                if (!(t is OperatorToken ot)) return ret;

                int prec = ot.Precedence;
                if (prec >= precedence) {
                    NextToken();
                    Expression right = ParseExpr(prec + 1);
                    var start = ret.StartIndex;
                    ret = new BinaryExpression(GetBinaryOperator(ot), ret, right);
                    ret.SetLoc(_globalParent, start, GetEnd());
                } else {
                    return ret;
                }
            }
        }

        // factor: ('+'|'-'|'~') factor | power
        private Expression ParseFactor() {
            var start = _lookahead.Span.Start;
            Expression ret;
            switch (PeekToken().Kind) {
                case TokenKind.Add:
                    NextToken();
                    ret = new UnaryExpression(PythonOperator.Pos, ParseFactor());
                    break;
                case TokenKind.Subtract:
                    NextToken();
                    ret = FinishUnaryNegate();
                    break;
                case TokenKind.Twiddle:
                    NextToken();
                    ret = new UnaryExpression(PythonOperator.Invert, ParseFactor());
                    break;
                default:
                    return ParsePower();
            }
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Expression FinishUnaryNegate() {
            // Special case to ensure that System.Int32.MinValue is an int and not a BigInteger
            if (PeekToken().Kind == TokenKind.Constant) {
                Token t = PeekToken();

                if (t.Value is BigInteger bi) {
                    if (bi.AsUInt32(out uint iVal) && iVal == 0x80000000) {
                        string tokenString = _tokenizer.GetTokenString();
                        Debug.Assert(tokenString.Length > 0);
                        NextToken();
                        return new ConstantExpression(int.MinValue);
                    }
                }
            }

            return new UnaryExpression(PythonOperator.Negate, ParseFactor());
        }

        // power: atom trailer* ['**' factor]
        private Expression ParsePower() {
            Expression ret = ParseAtom();
            ret = AddTrailers(ret);
            if (MaybeEat(TokenKind.Power)) {
                var start = ret.StartIndex;
                ret = new BinaryExpression(PythonOperator.Power, ret, ParseFactor());
                ret.SetLoc(_globalParent, start, GetEnd());
            }
            return ret;
        }

        //atom: ('(' [yield_expr|testlist_comp] ')' |
        //  '[' [testlist_comp] ']' |
        //  '{' [dictorsetmaker] '}' |
        //  NAME | NUMBER | STRING+ | '...' | 'None' | 'True' | 'False')
        //        ^^^^^^^^^^^^^^^^^^^^ CONSTANT ^^^^^^^^^^^^^^^^^^^^^^^
        private Expression ParseAtom() {
            Token t = PeekToken();
            Expression ret;
            switch (t.Kind) {
                case TokenKind.LeftParenthesis: // parenth_form, generator_expression, yield_atom
                    NextToken();
                    return FinishTupleOrGenExp();
                case TokenKind.LeftBracket:     // list_display
                    NextToken();
                    return FinishListValue();
                case TokenKind.LeftBrace:       // dict_display
                    NextToken();
                    return FinishDictOrSetValue();
                case TokenKind.Name:            // identifier
                    NextToken();
                    string name = (string)t.Value;
                    ParserSink?.StartName(GetSourceSpan(), name);
                    ret = new NameExpression(FixName(name));
                    ret.SetLoc(_globalParent, GetStart(), GetEnd());
                    return ret;
                case TokenKind.Constant:        // literal
                    NextToken();
                    var start = GetStart();
                    object cv = t.Value;
                    if (cv is string) {
                        ret = FinishJoinedString(t);
                    } else if (cv is Bytes bytes) {
                        ret = new ConstantExpression(FinishBytesPlus(bytes));
                    } else {
                        ret = new ConstantExpression(cv);
                    }
                    if (ret is JoinedStringExpression jse) {
                        // TODO: better locations
                        foreach (var expr in jse.Values) {
                            expr.SetLoc(_globalParent, start, GetEnd());
                        }
                    }
                    ret.SetLoc(_globalParent, start, GetEnd());
                    return ret;
                default:
                    ReportSyntaxError(_lookahead.Token, _lookahead.Span, ErrorCodes.SyntaxError, _allowIncomplete || _tokenizer.EndContinues);

                    // error node
                    ret = new ErrorExpression();
                    ret.SetLoc(_globalParent, _lookahead.Span.Start, _lookahead.Span.End);
                    return ret;
            }
        }

#nullable enable

        private string ParseFormattedString(FormattedStringToken t, string? s, List<Expression> expressions) {
            if (!string.IsNullOrEmpty(s)) {
                expressions.Add(new ConstantExpression(s));
            }

            expressions.AddRange(LiteralParser.DoParseFString(t.Image.AsSpan(), t.isRaw, !t.isRaw, normalizeLineEndings: true, isFormatted: true, this).Values);

            return string.Empty;
        }

        private Expression FinishJoinedString(Token t) {
            string s;
            List<Expression> expressions;

            // process the last token
            if (t is FormattedStringToken) {
                expressions = new List<Expression>();
                s = ParseFormattedString((FormattedStringToken)t, null, expressions);
            } else {
                Debug.Assert(t is ConstantValueToken);
                s = FinishStringPlus((string)t.Value);
                if (PeekToken() is not FormattedStringToken) {
                    return new ConstantExpression(s);
                }
                expressions = new List<Expression>();
            }

            // process the rest of the tokens
            t = PeekToken();
            while (t is FormattedStringToken fst) {
                s = ParseFormattedString(fst, s, expressions);
                NextToken();
                t = PeekToken();
            }

            while (t is ConstantValueToken) {
                bool stop = false;
                while (t is ConstantValueToken) {
                    if (t.Value is string cvs) {
                        s += cvs;
                        NextToken();
                        t = PeekToken();
                    } else {
                        ReportSyntaxError(t.Value is Bytes ? "cannot mix bytes and nonbytes literals" : "invalid syntax");
                        stop = true;
                        break;
                    }
                }
                if (stop) break;

                while (t is FormattedStringToken fst) {
                    s = ParseFormattedString(fst, s, expressions);
                    NextToken();
                    t = PeekToken();
                }
            }

            if (!string.IsNullOrEmpty(s)) {
                expressions.Add(new ConstantExpression(s));
            }

            return new JoinedStringExpression(expressions);
        }

        private string FinishStringPlus(string s) {
            Token t = PeekToken();
            while (t is ConstantValueToken) {
                if (t.Value is string cvs) {
                    s += cvs;
                    NextToken();
                    t = PeekToken();
                } else {
                    ReportSyntaxError(t.Value is Bytes ? "cannot mix bytes and nonbytes literals" : "invalid syntax");
                    break;
                }
            }
            return s;
        }

        private Bytes FinishBytesPlus(Bytes s) {
            Token t = PeekToken();
            while (t is ConstantValueToken) {
                if (t.Value is Bytes cvs) {
                    s += cvs;
                    NextToken();
                    t = PeekToken();
                } else {
                    ReportSyntaxError(t.Value is string ? "cannot mix bytes and nonbytes literals" : "invalid syntax");
                    break;
                }
            }
            if (t is FormattedStringToken) {
                ReportSyntaxError("cannot mix bytes and nonbytes literals");
            }
            return s;
        }

#nullable restore

        private Expression AddTrailers(Expression ret) {
            return AddTrailers(ret, true);
        }

        // trailer: '(' [ arglist_genexpr ] ')' | '[' subscriptlist ']' | '.' NAME
        private Expression AddTrailers(Expression ret, bool allowGeneratorExpression) {
            bool prevAllow = _allowIncomplete;
            try {
                _allowIncomplete = true;
                while (true) {
                    switch (PeekToken().Kind) {
                        case TokenKind.LeftParenthesis:
                            if (!allowGeneratorExpression) return ret;

                            NextToken();
                            IReadOnlyList<Node> args = FinishArgListOrGenExpr();
                            CallExpression call = FinishCallExpr(ret, args);
                            call.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                            ret = call;
                            break;
                        case TokenKind.LeftBracket:
                            NextToken();
                            Expression index = ParseSubscriptList();
                            IndexExpression ie = new IndexExpression(ret, index);
                            ie.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                            ret = ie;
                            break;
                        case TokenKind.Dot:
                            NextToken();
                            string name = ReadName();
                            MemberExpression fe = new MemberExpression(ret, name);
                            fe.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                            ret = fe;
                            break;
                        case TokenKind.Constant:
                            // abc.1, abc"", abc 1L, abc 0j
                            ReportSyntaxError("invalid syntax");
                            return Error();
                        default:
                            return ret;
                    }
                }
            } finally {
                _allowIncomplete = prevAllow;
            }
        }

        //subscriptlist: subscript (',' subscript)* [',']
        //subscript: '.' '.' '.' | expression | [expression] ':' [expression] [sliceop]
        //sliceop: ':' [expression]
        private Expression ParseSubscriptList() {
            const TokenKind terminator = TokenKind.RightBracket;
            var start0 = GetStart();
            bool trailingComma = false;

            List<Expression> l = new List<Expression>();
            while (true) {
                Expression e;
                if (MaybeEat(TokenKind.Dot)) {
                    var start = GetStart();
                    Eat(TokenKind.Dot); Eat(TokenKind.Dot);
                    e = new ConstantExpression(Ellipsis.Value);
                    e.SetLoc(_globalParent, start, GetEnd());
                } else if (MaybeEat(TokenKind.Colon)) {
                    e = FinishSlice(null, GetStart());
                } else {
                    e = ParseTest();
                    if (MaybeEat(TokenKind.Colon)) {
                        e = FinishSlice(e, e.StartIndex);
                    }
                }

                l.Add(e);
                if (!MaybeEat(TokenKind.Comma)) {
                    Eat(terminator);
                    trailingComma = false;
                    break;
                }

                trailingComma = true;
                if (MaybeEat(terminator)) {
                    break;
                }
            }
            Expression ret = MakeTupleOrExpr(l, trailingComma, true);
            ret.SetLoc(_globalParent, start0, GetEnd());
            return ret;
        }

        private Expression ParseSliceEnd() {
            Expression e2 = null;
            switch (PeekToken().Kind) {
                case TokenKind.Comma:
                case TokenKind.RightBracket:
                    break;
                default:
                    e2 = ParseTest();
                    break;
            }
            return e2;
        }

        private Expression FinishSlice(Expression e0, int start) {
            Expression e1 = null;
            Expression e2 = null;

            switch (PeekToken().Kind) {
                case TokenKind.Comma:
                case TokenKind.RightBracket:
                    break;
                case TokenKind.Colon:
                    // x[?::?]
                    NextToken();
                    e2 = ParseSliceEnd();
                    break;
                default:
                    // x[?:val:?]
                    e1 = ParseTest();
                    if (MaybeEat(TokenKind.Colon)) {
                        e2 = ParseSliceEnd();
                    }
                    break;
            }
            var ret = new SliceExpression(e0, e1, e2);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // exprlist: (expr|star_expr) (',' (expr|star_expr))* [',']
        private List<Expression> ParseExprList(out bool trailingComma) {
            var expressions = new List<Expression>();
            while (true) {
                expressions.Add(PeekToken(TokenKind.Multiply) ? ParseStarExpr() : ParseExpr());

                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }

                if (NeverTestToken(PeekToken()) && !PeekToken(TokenKind.Multiply)) {
                    trailingComma = true;
                    break;
                }
            }

            return expressions;
        }

        // arglist:
        //             expression                     rest_of_arguments
        //             expression "=" expression      rest_of_arguments
        //             expression "for" gen_expr_rest
        //
        private IReadOnlyList<Node> FinishArgListOrGenExpr() {
            Node a = null;

            ParserSink?.StartParameters(GetSourceSpan());

            Token t = PeekToken();
            if (t.Kind != TokenKind.RightParenthesis && t.Kind != TokenKind.Multiply && t.Kind != TokenKind.Power) {
                var start = GetStart();
                Expression e = ParseTest();
                if (e is ErrorExpression) {
                    return null;
                }

                if (MaybeEat(TokenKind.Assign)) {               //  Keyword argument
                    a = FinishKeywordArgument(e);
                } else if (PeekToken(Tokens.KeywordForToken)) {    //  Generator expression
                    a = ParseGeneratorExpression(e);
                    Eat(TokenKind.RightParenthesis);
                    a.SetLoc(_globalParent, start, GetEnd());
                    ParserSink?.EndParameters(GetSourceSpan());
                    return new Node[1] { a };       //  Generator expression is the argument
                } else {
                    a = e;
                }

                //  Was this all?
                //
                if (MaybeEat(TokenKind.Comma)) {
                    ParserSink?.NextParameter(GetSourceSpan());
                } else {
                    Eat(TokenKind.RightParenthesis);
                    ParserSink?.EndParameters(GetSourceSpan());
                    return new Node[1] { a };
                }
            }

            return FinishArgumentList(a);
        }

        private Keyword FinishKeywordArgument(Expression t) {
            if (t is NameExpression n) {
                Expression val = ParseTest();
                var arg = new Keyword(n.Name, val);
                arg.SetLoc(_globalParent, n.StartIndex, val.EndIndex);
                return arg;
            } else {
                ReportSyntaxError(IronPython.Resources.ExpectedName);
                var arg = new Keyword(null, t);
                arg.SetLoc(_globalParent, t.StartIndex, t.EndIndex);
                return arg;
            }
        }

        private void CheckUniqueArgument(List<Node> names, Keyword arg) {
            if (arg.Name != null) {
                for (int i = 0; i < names.Count; i++) {
                    if (names[i] is Keyword k && k.Name == arg.Name) {
                        ReportSyntaxError(IronPython.Resources.DuplicateKeywordArg);
                    }
                }
            }
        }

        //arglist: (argument ',')* (argument [',']| '*' expression [',' '**' expression] | '**' expression)
        //argument: [expression '='] expression    # Really [keyword '='] expression
        private IReadOnlyList<Node> FinishArgumentList(Node first) {
            const TokenKind terminator = TokenKind.RightParenthesis;
            List<Node> l = new List<Node>();

            if (first != null) {
                l.Add(first);
            }

            // Parse remaining arguments
            while (true) {
                if (MaybeEat(terminator)) {
                    break;
                }
                Node a;
                if (MaybeEat(TokenKind.Multiply)) {
                    var start = GetStart();
                    a = new StarredExpression(ParseTest());
                    a.SetLoc(_globalParent, start, GetEnd());
                } else if (MaybeEat(TokenKind.Power)) {
                    var e = ParseTest();
                    a = new Keyword(null, e);
                    a.SetLoc(_globalParent, e.StartIndex, e.EndIndex);
                } else {
                    var e = ParseTest();
                    if (MaybeEat(TokenKind.Assign)) {
                        a = FinishKeywordArgument(e);
                        CheckUniqueArgument(l, (Keyword)a);
                    } else {
                        a = e;
                    }
                }
                l.Add(a);
                if (MaybeEat(TokenKind.Comma)) {
                    ParserSink?.NextParameter(GetSourceSpan());
                } else {
                    Eat(terminator);
                    break;
                }
            }

            ParserSink?.EndParameters(GetSourceSpan());

            return l;
        }

        // testlist: test (',' test)* [',']
        private List<Expression> ParseTestList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            trailingComma = false;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseTest());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }
            return l;
        }

        // testlist: test (',' test)* [',']
        private Expression ParseTestList() {
            if (!NeverTestToken(PeekToken())) {
                var expr = ParseTest();
                if (!MaybeEat(TokenKind.Comma)) {
                    return expr;
                }

                return ParseTestList(expr);
            } else {
                return ParseTestListError();
            }

            Expression ParseTestList(Expression expr) {
                List<Expression> l = new List<Expression>();
                l.Add(expr);

                bool trailingComma = true;
                while (true) {
                    if (NeverTestToken(PeekToken())) break;
                    l.Add(ParseTest());
                    if (!MaybeEat(TokenKind.Comma)) {
                        trailingComma = false;
                        break;
                    }
                }
                return MakeTupleOrExpr(l, trailingComma);
            }
        }

        // star_expr: ' * ' expr
        private Expression ParseStarExpr() {
            Eat(TokenKind.Multiply);
            var start = GetStart();
            var expr = new StarredExpression(ParseExpr());
            expr.SetLoc(_globalParent, start, GetEnd());
            return expr;
        }

        // testlist_star_expr: (test|star_expr) (',' (test|star_expr))* [',']
        private Expression ParseTestListStarExpr() {
            Expression expr;

            // (test|star_expr)
            if (PeekToken(TokenKind.Multiply)) {
                expr = ParseStarExpr();
            } else if (!NeverTestToken(PeekToken())) {
                expr = ParseTest();
            } else {
                // instructed to parse a testlist_star_expr, but it contains no elements, when it must contain at
                // least 1.
                return ParseTestListError();
            }

            if (!MaybeEat(TokenKind.Comma)) {
                // there is no comma so that is the end of the list.
                return expr;
            }

            // the presence of a comma indicates that the list may contain more elements
            var expressions = new List<Expression> { expr };

            var trailingComma = true;
            while (true) {
                if (PeekToken(TokenKind.Multiply)) {
                    expressions.Add(ParseStarExpr());
                } else if (!NeverTestToken(PeekToken())) {
                    expressions.Add(ParseTest());
                } else {
                    // no element
                    break;
                }

                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
            }

            return MakeTupleOrExpr(expressions, trailingComma);
        }

        private Expression ParseTestListError() {
            if (MaybeEat(TokenKind.Indent)) {
                // the error is on the next token which has a useful location, unlike the indent - note we don't have an
                // indent if we're at an EOF.  It'a also an indentation error instead of a syntax error.
                NextToken();
                ReportSyntaxError(GetStart(), GetEnd(), "unexpected indent", ErrorCodes.IndentationError);
            } else {
                ReportSyntaxError(_lookahead);
            }

            return new ErrorExpression();
        }

        private Expression FinishExpressionListAsExpr(Expression expr) {
            var start = GetStart();
            bool trailingComma = true;
            var expressions = new List<Expression> { expr };

            while (true) {
                if (PeekToken(TokenKind.Multiply)) {
                    expr = ParseStarExpr();
                } else if (!NeverTestToken(PeekToken())) {
                    expr = ParseTest();
                } else {
                    break;
                }

                expressions.Add(expr);
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
            }

            Expression ret = MakeTupleOrExpr(expressions, trailingComma);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //
        //  testlist_gexp: expression ( genexpr_for | (',' expression)* [','] )
        //
        private Expression FinishTupleOrGenExp() {
            var lStart = GetStart();
            var lEnd = GetEnd();
            int grouping = _tokenizer.GroupingLevel;
            bool hasRightParenthesis;

            Expression ret;
            //  Empty tuple
            if (MaybeEat(TokenKind.RightParenthesis)) {
                ret = MakeTupleOrExpr(new List<Expression>(), false);
                hasRightParenthesis = true;
            } else if (MaybeEat(TokenKind.KeywordYield)) {
                ret = ParseYieldExpression();
                Eat(TokenKind.RightParenthesis);
                hasRightParenthesis = true;
            } else {
                bool prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;

                    var expr = PeekToken(TokenKind.Multiply) ? ParseStarExpr() : ParseTest();
                    if (MaybeEat(TokenKind.Comma)) {
                        // "(" expression "," ...
                        ret = FinishExpressionListAsExpr(expr);
                    } else if (PeekToken(Tokens.KeywordForToken)) {
                        // "(" expression "for" ...
                        if (expr is StarredExpression) ReportSyntaxError(expr.StartIndex, expr.EndIndex, "iterable unpacking cannot be used in comprehension");
                        ret = ParseGeneratorExpression(expr);
                    } else {
                        // "(" expression ")"
                        ret = expr is ParenthesisExpression ? expr : new ParenthesisExpression(expr);
                    }
                    hasRightParenthesis = Eat(TokenKind.RightParenthesis);
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }

            var rStart = GetStart();
            var rEnd = GetEnd();

            if (hasRightParenthesis) {
                ParserSink?.MatchPair(new SourceSpan(_tokenizer.IndexToLocation(lStart), _tokenizer.IndexToLocation(lEnd)),
                    new SourceSpan(_tokenizer.IndexToLocation(rStart), _tokenizer.IndexToLocation(rEnd)), grouping);
            }

            ret.SetLoc(_globalParent, lStart, rEnd);
            return ret;
        }

        //  genexpr_for  ::= "for" target_list "in" or_test [genexpr_iter]
        //  genexpr_iter ::= (genexpr_for | genexpr_if) *
        //
        //  "for" has NOT been eaten before entering this method
        private Expression ParseGeneratorExpression(Expression expr) {
            ForStatement root = ParseGenExprFor();
            Statement current = root;

            for (; ; ) {
                if (PeekToken(Tokens.KeywordForToken)) {
                    current = NestGenExpr(current, ParseGenExprFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    current = NestGenExpr(current, ParseGenExprIf());
                } else {
                    // Generator Expressions have an implicit function definition and yield around their expression.
                    //  (x for i in R)
                    // becomes:
                    //   def f(): 
                    //     for i in R: yield (x)
                    ExpressionStatement ys = new ExpressionStatement(new YieldExpression(expr));
                    ys.Expression.SetLoc(_globalParent, expr.IndexSpan);
                    ys.SetLoc(_globalParent, expr.IndexSpan);
                    NestGenExpr(current, ys);
                    break;
                }
            }

            // We pass the outermost iterable in as a parameter because Python semantics
            // say that this one piece is computed at definition time rather than iteration time
            const string fname = "<genexpr>";
            Parameter parameter = new Parameter("__gen_$_parm__", 0);
            FunctionDefinition func = new FunctionDefinition(fname, new Parameter[] { parameter }, root);
            func.IsGenerator = true;
            func.GeneratorStop = GeneratorStop;
            func.SetLoc(_globalParent, root.StartIndex, GetEnd());
            func.HeaderIndex = root.EndIndex;

            //  Transform the root "for" statement
            Expression outermost = root.List;
            NameExpression ne = new NameExpression("__gen_$_parm__");
            ne.SetLoc(_globalParent, outermost.IndexSpan);
            root.List = ne;

            GeneratorExpression ret = new GeneratorExpression(func, outermost);
            ret.SetLoc(_globalParent, expr.StartIndex, GetEnd());
            return ret;
        }

        private static Statement NestGenExpr(Statement current, Statement nested) {
            if (current is ForStatement fes) {
                fes.Body = nested;
            } else if (current is IfStatement ifs) {
                ifs.Tests[0].Body = nested;
            }
            return nested;
        }

        // "for" exprlist "in" or_test
        private ForStatement ParseGenExprFor() {
            var start = GetStart();
            Eat(TokenKind.KeywordFor);
            bool trailingComma;
            List<Expression> l = ParseExprList(out trailingComma);
            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression expr = null;
            expr = ParseOrTest();

            ForStatement gef = new ForStatement(lhs, expr, null, null);
            var end = GetEnd();
            gef.SetLoc(_globalParent, start, end);
            gef.HeaderIndex = end;
            return gef;
        }

        //  genexpr_if: "if" test_nocond
        private IfStatement ParseGenExprIf() {
            var start = GetStart();
            Eat(TokenKind.KeywordIf);
            Expression expr = ParseTestNoCond();
            IfStatementTest ist = new IfStatementTest(expr, null);
            var end = GetEnd();
            ist.HeaderIndex = end;
            ist.SetLoc(_globalParent, start, end);
            IfStatement gei = new IfStatement(new IfStatementTest[] { ist }, null);
            gei.SetLoc(_globalParent, start, end);
            return gei;
        }

        // dict_display: '{' [dictorsetmaker] '}'
        // dictorsetmaker: ( ((test ':' test | '**' expr)
        //                    (comp_for | (',' (test ':' test | '**' expr))* [','])) |
        //                   ((test | star_expr)
        //                    (comp_for | (',' (test | star_expr))* [','])) )
        private Expression FinishDictOrSetValue() {
            var oStart = GetStart();
            var oEnd = GetEnd();

            List<SliceExpression> dictMembers = null;
            List<Expression> setMembers = null;
            bool prevAllow = _allowIncomplete;
            try {
                _allowIncomplete = true;
                while (true) {
                    if (MaybeEat(TokenKind.RightBrace)) { // empty dict literal
                        break;
                    }
                    bool first = false;
                    if (PeekToken(TokenKind.Power)) {
                        if (setMembers is not null) ReportSyntaxError("invalid syntax");
                        else if (dictMembers is null) {
                            dictMembers = new();
                            first = true;
                        }
                        NextToken();
                        var expr = ParseExpr();

                        var se = new SliceExpression(null, expr, null);
                        se.SetLoc(_globalParent, expr.StartIndex, expr.EndIndex);
                        dictMembers?.Add(se);
                    } else if (PeekToken(TokenKind.Multiply)) {
                        if (dictMembers is not null) ReportSyntaxError("invalid syntax");
                        else if (setMembers is null) {
                            setMembers = new();
                            first = true;
                        }
                        var expr = ParseStarExpr();

                        if (PeekToken(Tokens.KeywordForToken)) {
                            if (!first) ReportSyntaxError("invalid syntax");
                        }

                        setMembers?.Add(expr);
                    } else {
                        Expression e1 = ParseTest();
                        if (MaybeEat(TokenKind.Colon)) { // dict literal
                            if (setMembers != null) {
                                ReportSyntaxError("invalid syntax");
                            } else if (dictMembers == null) {
                                dictMembers = new List<SliceExpression>();
                                first = true;
                            }
                            Expression e2 = ParseTest();

                            if (PeekToken(Tokens.KeywordForToken)) {
                                if (!first) {
                                    ReportSyntaxError("invalid syntax");
                                }
                                return FinishDictComp(e1, e2, oStart, oEnd);
                            }

                            SliceExpression se = new SliceExpression(e1, e2, null);
                            se.SetLoc(_globalParent, e1.StartIndex, e2.EndIndex);
                            dictMembers.Add(se);
                        } else { // set literal
                            if (dictMembers != null) {
                                ReportSyntaxError("invalid syntax");
                            } else if (setMembers == null) {
                                setMembers = new List<Expression>();
                                first = true;
                            }

                            if (PeekToken(Tokens.KeywordForToken)) {
                                if (!first) {
                                    ReportSyntaxError("invalid syntax");
                                }
                                return FinishSetComp(e1, oStart, oEnd);
                            }

                            // error recovery
                            setMembers?.Add(e1);
                        }
                    }

                    if (!MaybeEat(TokenKind.Comma)) {
                        Eat(TokenKind.RightBrace);
                        break;
                    }
                }
            } finally {
                _allowIncomplete = prevAllow;
            }

            var cStart = GetStart();
            var cEnd = GetEnd();

            ParserSink?.MatchPair(
                new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)),
                new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)),
                1);

            if (dictMembers != null || setMembers == null) {
                SliceExpression[] exprs;
                if (dictMembers != null) {
                    exprs = dictMembers.ToArray();
                } else {
                    exprs = Array.Empty<SliceExpression>();
                }
                DictionaryExpression ret = new DictionaryExpression(exprs);
                ret.SetLoc(_globalParent, oStart, cEnd);
                return ret;
            } else {
                SetExpression ret = new SetExpression(setMembers.ToArray());
                ret.SetLoc(_globalParent, oStart, cEnd);
                return ret;
            }
        }

        // comp_iter '}'
        private SetComprehension FinishSetComp(Expression item, int oStart, int oEnd) {
            ComprehensionIterator[] iters = ParseCompIter();
            Eat(TokenKind.RightBrace);

            var cStart = GetStart();
            var cEnd = GetEnd();
            ParserSink?.MatchPair(
                new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)),
                new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)),
                1);

            if (item is StarredExpression) ReportSyntaxError(item.StartIndex, item.EndIndex, "iterable unpacking cannot be used in comprehension");
            var ret = new SetComprehension(item, iters);
            ret.SetLoc(_globalParent, oStart, cEnd);
            return ret;
        }

        // comp_iter '}'
        private DictionaryComprehension FinishDictComp(Expression key, Expression value, int oStart, int oEnd) {
            ComprehensionIterator[] iters = ParseCompIter();
            Eat(TokenKind.RightBrace);

            var cStart = GetStart();
            var cEnd = GetEnd();

            ParserSink?.MatchPair(
                new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)),
                new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)),
                1);
            var ret = new DictionaryComprehension(key, value, iters);
            ret.SetLoc(_globalParent, oStart, cEnd);
            return ret;
        }

        // comp_iter: comp_for | comp_if
        private ComprehensionIterator[] ParseCompIter() {
            List<ComprehensionIterator> iters = new List<ComprehensionIterator>();
            ComprehensionFor firstFor = ParseCompFor();
            iters.Add(firstFor);

            while (true) {
                if (PeekToken(Tokens.KeywordForToken)) {
                    iters.Add(ParseCompFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    iters.Add(ParseCompIf());
                } else {
                    break;
                }
            }

            return iters.ToArray();
        }

        // comp_for: 'for' exprlist 'in' or_test [comp_iter]
        private ComprehensionFor ParseCompFor() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();
            bool trailingComma;
            List<Expression> l = ParseExprList(out trailingComma);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression list = ParseOrTest();

            ComprehensionFor ret = new ComprehensionFor(lhs, list);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // testlist_comp: (test|star_expr) ( comp_for | (',' (test|star_expr))* [','] )
        private Expression FinishListValue() {
            var oStart = GetStart();
            var oEnd = GetEnd();
            var grouping = _tokenizer.GroupingLevel;

            Expression ret;
            if (MaybeEat(TokenKind.RightBracket)) {
                ret = new ListExpression();
            } else {
                var prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;

                    // (test|star_expr)
                    var expr = PeekToken(TokenKind.Multiply) ? ParseStarExpr() : ParseTest();

                    // (comp_for | (',' (test|star_expr))* [','] )

                    // comp_for
                    if (PeekToken(Tokens.KeywordForToken)) {
                        // although it's calling ParseCompIter(), because the peek token is a FOR it is going to
                        // do the right thing.
                        if (expr is StarredExpression) ReportSyntaxError(expr.StartIndex, expr.EndIndex, "iterable unpacking cannot be used in comprehension");
                        ret = new ListComprehension(expr, ParseCompIter());
                    } else {
                        // (',' (test|star_expr))* [',']
                        var items = new List<Expression> { expr };
                        while (MaybeEat(TokenKind.Comma)) {
                            if (PeekToken(TokenKind.Multiply)) {
                                items.Add(ParseStarExpr());
                            } else if (!NeverTestToken(PeekToken())) {
                                items.Add(ParseTest());
                            } else {
                                // prevents the case of ','*
                                break;
                            }
                        }

                        ret = new ListExpression(items.ToArray());
                    }

                    Eat(TokenKind.RightBracket);
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }

            var cStart = GetStart();
            var cEnd = GetEnd();

            ParserSink?.MatchPair(
                new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)),
                new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)),
                grouping
            );

            ret.SetLoc(_globalParent, oStart, cEnd);
            return ret;
        }

        // comp_if: 'if' test_nocond [comp_iter]
        private ComprehensionIf ParseCompIf() {
            Eat(TokenKind.KeywordIf);
            var start = GetStart();
            Expression expr = ParseTestNoCond();
            ComprehensionIf ret = new ComprehensionIf(expr);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Expression MakeTupleOrExpr(List<Expression> l, bool trailingComma) {
            return MakeTupleOrExpr(l, trailingComma, false);
        }

        private Expression MakeTupleOrExpr(List<Expression> l, bool trailingComma, bool expandable) {
            if (l.Count == 1 && !trailingComma) return l[0];

            Expression[] exprs = l.ToArray();
            TupleExpression te = new TupleExpression(expandable && !trailingComma, exprs);
            if (exprs.Length > 0) {
                te.SetLoc(_globalParent, exprs[0].StartIndex, exprs[exprs.Length - 1].EndIndex);
            }
            return te;
        }

        private static bool NeverTestToken(Token t) {
            switch (t.Kind) {
                case TokenKind.AddEqual:
                case TokenKind.SubtractEqual:
                case TokenKind.MultiplyEqual:
                case TokenKind.MatMultEqual:
                case TokenKind.FloorDivideEqual:
                case TokenKind.TrueDivideEqual:
                case TokenKind.ModEqual:
                case TokenKind.BitwiseAndEqual:
                case TokenKind.BitwiseOrEqual:
                case TokenKind.ExclusiveOrEqual:
                case TokenKind.LeftShiftEqual:
                case TokenKind.RightShiftEqual:
                case TokenKind.PowerEqual:

                case TokenKind.Indent:
                case TokenKind.Dedent:
                case TokenKind.NewLine:
                case TokenKind.EndOfFile:
                case TokenKind.Semicolon:

                case TokenKind.Assign:
                case TokenKind.RightBrace:
                case TokenKind.RightBracket:
                case TokenKind.RightParenthesis:

                case TokenKind.Comma:

                case TokenKind.KeywordFor:
                case TokenKind.KeywordIn:
                case TokenKind.KeywordIf:
                    return true;

                default: return false;
            }
        }

        private ClassDefinition CurrentClass {
            get {
                if (_classes != null && _classes.Count > 0) {
                    return _classes.Peek();
                }
                return null;
            }
        }

        private ClassDefinition PopClass() {
            if (_classes != null && _classes.Count > 0) {
                return _classes.Pop();
            }
            return null;
        }

        private void PushClass(ClassDefinition clazz) {
            if (_classes == null) {
                _classes = new Stack<ClassDefinition>();
            }
            _classes.Push(clazz);
        }

        private FunctionDefinition CurrentFunction {
            get {
                if (_functions != null && _functions.Count > 0) {
                    return _functions.Peek();
                }
                return null;
            }
        }

        private FunctionDefinition PopFunction() {
            if (_functions != null && _functions.Count > 0) {
                return _functions.Pop();
            }
            return null;
        }

        private void PushFunction(FunctionDefinition function) {
            if (_functions == null) {
                _functions = new Stack<FunctionDefinition>();
            }
            _functions.Push(function);
        }

        private CallExpression FinishCallExpr(Expression target, IEnumerable<Node> args) {
            List<Expression> posargs = null;
            List<Keyword> kwargs = null;

            if (args is not null) {
                SplitAndValidateArguments(args, out posargs, out kwargs);
            }

            return new CallExpression(target, posargs, kwargs);
        }

        private void SplitAndValidateArguments(IEnumerable<Node> args, out List<Expression> posargs, out List<Keyword> kwargs) {
            bool hasKeyword = false;
            bool hasKeywordUnpacking = false;

            posargs = null;
            kwargs = null;

            foreach (Node arg in args) {
                if (arg is Keyword keyword) {
                    if (keyword.Name is null) {
                        hasKeywordUnpacking = true;
                    } else {
                        hasKeyword = true;
                    }
                    kwargs ??= new List<Keyword>();
                    kwargs.Add(keyword);
                } else if (arg is StarredExpression starredExpression) {
                    if (hasKeywordUnpacking) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "iterable argument unpacking follows keyword argument unpacking");
                    }
                    posargs ??= new List<Expression>();
                    posargs.Add(starredExpression);
                } else {
                    var expr = (Expression)arg;
                    if (hasKeywordUnpacking) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "positional argument follows keyword argument unpacking");
                    } else if (hasKeyword) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "positional argument follows keyword argument");
                    }
                    posargs ??= new List<Expression>();
                    posargs.Add(expr);

                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            _sourceReader?.Dispose();
        }

        #endregion

        #region Implementation Details

        private PythonAst ParseFileWorker(bool makeModule, bool returnValue) {
            _globalParent = new PythonAst(makeModule, _languageFeatures, false, _context);
            StartParsing();

            List<Statement> l = new List<Statement>();

            //
            // A future statement must appear near the top of the module. 
            // The only lines that can appear before a future statement are: 
            // - the module docstring (if any), 
            // - comments, 
            // - blank lines, and 
            // - other future statements. 
            // 

            MaybeEatNewLine();

            if (PeekToken(TokenKind.Constant)) {
                Statement s = ParseStmt();
                l.Add(s);
                _fromFutureAllowed = false;
                if (s is ExpressionStatement es) {
                    if (es.Expression is ConstantExpression ce && ce.Value is string) {
                        // doc string
                        _fromFutureAllowed = true;
                    }
                }
            }

            MaybeEatNewLine();

            // from __future__
            if (_fromFutureAllowed) {
                while (PeekToken(Tokens.KeywordFromToken)) {
                    Statement s = ParseStmt();
                    l.Add(s);
                    if (s is FromImportStatement fis && !fis.IsFromFuture) {
                        // end of from __future__
                        break;
                    }
                }
            }

            // the end of from __future__ sequence
            _fromFutureAllowed = false;

            while (true) {
                if (MaybeEat(TokenKind.EndOfFile)) break;
                if (MaybeEatNewLine()) continue;

                Statement s = ParseStmt();
                l.Add(s);
            }

            Statement[] stmts = l.ToArray();

            if (returnValue && stmts.Length > 0) {
                if (stmts[stmts.Length - 1] is ExpressionStatement exprStmt) {
                    var retStmt = new ReturnStatement(exprStmt.Expression);
                    stmts[stmts.Length - 1] = retStmt;
                    retStmt.SetLoc(_globalParent, exprStmt.Expression.IndexSpan);
                }
            }

            SuiteStatement ret = new SuiteStatement(stmts);
            ret.SetLoc(_globalParent, 0, GetEnd());
            return FinishParsing(ret);
        }

        private Statement InternalParseInteractiveInput(out bool parsingMultiLineCmpdStmt, out bool isEmptyStmt) {
            try {
                Statement s;
                isEmptyStmt = false;
                parsingMultiLineCmpdStmt = false;

                switch (PeekToken().Kind) {
                    case TokenKind.NewLine:
                        MaybeEatNewLine();
                        Eat(TokenKind.EndOfFile);
                        if (_tokenizer.EndContinues) {
                            parsingMultiLineCmpdStmt = true;
                            _errorCode = ErrorCodes.IncompleteStatement;
                        } else {
                            isEmptyStmt = true;
                        }
                        return null;

                    case TokenKind.KeywordIf:
                    case TokenKind.KeywordWhile:
                    case TokenKind.KeywordFor:
                    case TokenKind.KeywordTry:
                    case TokenKind.At:
                    case TokenKind.KeywordDef:
                    case TokenKind.KeywordClass:
                    case TokenKind.KeywordWith:
                        parsingMultiLineCmpdStmt = true;
                        s = ParseStmt();
                        EatEndOfInput();
                        break;

                    default:
                        //  parseSimpleStmt takes care of one or more simple_stmts and the Newline
                        s = ParseSimpleStmt();
                        MaybeEatNewLine();
                        Eat(TokenKind.EndOfFile);
                        break;
                }
                return s;
            } catch (DecoderFallbackException dfe) {
                throw BadSourceError(dfe);
            }
        }

        private Expression ParseTestListAsExpression() {
            StartParsing();

            Expression expression = ParseTestList();
            EatEndOfInput();
            return expression;
        }

        /// <summary>
        /// Maybe eats a new line token returning true if the token was
        /// eaten.
        /// 
        /// Python always tokenizes to have only 1  new line character in a 
        /// row.  But we also craete NLToken's and ignore them except for 
        /// error reporting purposes.  This gives us the same errors as 
        /// CPython and also matches the behavior of the standard library 
        /// tokenize module.  This function eats any present NL tokens and throws
        /// them away.
        /// </summary>
        private bool MaybeEatNewLine() {
            if (MaybeEat(TokenKind.NewLine)) {
                while (MaybeEat(TokenKind.NLToken)) ;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Eats a new line token throwing if the next token isn't a new line.  
        /// 
        /// Python always tokenizes to have only 1  new line character in a 
        /// row.  But we also craete NLToken's and ignore them except for 
        /// error reporting purposes.  This gives us the same errors as 
        /// CPython and also matches the behavior of the standard library 
        /// tokenize module.  This function eats any present NL tokens and throws
        /// them away.
        /// </summary>
        private bool EatNewLine() {
            bool res = Eat(TokenKind.NewLine);
            while (MaybeEat(TokenKind.NLToken)) ;
            return res;
        }

        private Token EatEndOfInput() {
            while (MaybeEatNewLine() || MaybeEat(TokenKind.Dedent)) {
                ;
            }

            Token t = NextToken();
            if (t.Kind != TokenKind.EndOfFile) {
                ReportSyntaxError(_token);
            }
            return t;
        }

        private Exception/*!*/ BadSourceError(DecoderFallbackException dfe) {
            if (_sourceReader.BaseReader is StreamReader sr && sr.BaseStream.CanSeek) {
                // TODO: Convert exception index to proper SourceLocation
            }
            // BUG: We have some weird stream and we can't accurately track the 
            // position where the exception came from.  There are too many levels
            // of buffering below us to re-wind and calculate the actual line number, so
            // we'll give the last line number the tokenizer was at.
            int lineNum = _tokenizer.CurrentPosition.Line;

            string message;

            if (_sourceReader.Encoding == null) {
                // BUG: source reader reads from a text source so no DecoderFallbackException can originate from there
                message = "encoding problem";

            } else if (dfe.BytesUnknown != null && dfe.BytesUnknown.Length > 0) {

                if (_sourceUnit.LanguageContext is PythonContext pc && ReferenceEquals(_sourceReader.Encoding, pc.DefaultEncoding)) {
                    // more specific error message if default encoding is used
                    message = string.Format("Non-UTF-8 code starting with '\\x{0:x2}' in file {1} on line {2}, but no encoding declared; see http://python.org/dev/peps/pep-0263/ for details",
                        dfe.BytesUnknown[0],
                        _sourceUnit.Path,
                        lineNum
                    );
                } else {
                    // standard message
                    int cp = _sourceReader.Encoding.CodePage;
                    message = string.Format("{0}'{1}' codec can't decode byte 0x{2:x2} in position {3}: {4}",
                        cp == 65001 || cp == 1200 || cp == 1201 || cp == 12000 || cp == 12001 ? "(unicode error) " : string.Empty,
                        StringOps.GetEncodingName(_sourceReader.Encoding, normalize: false),
                        dfe.BytesUnknown[0],
                        dfe.Index,
                        dfe.Message
                    );
                }

            } else if (!string.IsNullOrEmpty(dfe.Message)) {
                message = string.Format("encoding problem: {0}: {1}",
                    StringOps.GetEncodingName(_sourceReader.Encoding),
                    dfe.Message
                );

            } else {
                message = string.Format("encoding problem: {0}",
                    StringOps.GetEncodingName(_sourceReader.Encoding)
                );
            }

            return PythonOps.BadSourceEncodingError(message, lineNum, _sourceUnit.Path);
        }

        private bool GeneratorStop => (_languageFeatures & ModuleOptions.GeneratorStop) == ModuleOptions.GeneratorStop;

        private void StartParsing() {
            if (_parsingStarted)
                throw new InvalidOperationException("Parsing already started. Use Restart to start again.");

            _parsingStarted = true;

            FetchLookahead();
        }

        private int GetEnd() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return _token.Span.End;
        }

        private int GetStart() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return _token.Span.Start;
        }

        private SourceSpan GetSourceSpan() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return new SourceSpan(
                _tokenizer.IndexToLocation(GetStart()),
                _tokenizer.IndexToLocation(GetEnd())
            );
        }

        private Token NextToken() {
            _token = _lookahead;
            FetchLookahead();
            return _token.Token;
        }

        private Token PeekToken() {
            return _lookahead.Token;
        }

        private void FetchLookahead() {
            _lookahead = new TokenWithSpan(_tokenizer.GetNextToken(), _tokenizer.TokenSpan);
        }

        private bool PeekToken(TokenKind kind) {
            return PeekToken().Kind == kind;
        }

        private bool PeekToken(Token check) {
            return PeekToken() == check;
        }

        private bool Eat(TokenKind kind) {
            Token next = PeekToken();
            if (next.Kind != kind) {
                ReportSyntaxError(_lookahead);
                return false;
            } else {
                NextToken();
                return true;
            }
        }

        private bool EatNoEof(TokenKind kind) {
            Token next = PeekToken();
            if (next.Kind != kind) {
                ReportSyntaxError(_lookahead.Token, _lookahead.Span, ErrorCodes.SyntaxError, false);
                return false;
            }
            NextToken();
            return true;
        }

        private bool MaybeEat(TokenKind kind) {
            if (PeekToken().Kind == kind) {
                NextToken();
                return true;
            } else {
                return false;
            }
        }

        private class TokenizerErrorSink : ErrorSink {
            private readonly Parser _parser;

            public TokenizerErrorSink(Parser parser) {
                Assert.NotNull(parser);
                _parser = parser;
            }

            public override void Add(SourceUnit sourceUnit, string message, SourceSpan span, int errorCode, Severity severity) {
                if (_parser._errorCode == 0 && (severity == Severity.Error || severity == Severity.FatalError)) {
                    _parser._errorCode = errorCode;
                }
                _parser.ErrorSink.Add(sourceUnit, message, span, errorCode, severity);
            }
        }

        #endregion
    }
}
