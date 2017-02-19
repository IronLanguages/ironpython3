/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler.Ast;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Compiler {

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
            ContractUtils.RequiresNotNull(tokenizer, "tokenizer");
            ContractUtils.RequiresNotNull(errorSink, "errorSink");
            ContractUtils.RequiresNotNull(parserSink, "parserSink");

            tokenizer.ErrorSink = new TokenizerErrorSink(this);

            _tokenizer = tokenizer;
            _errors = errorSink;
            if (parserSink != ParserSink.Null) {
                _sink = parserSink;
            }
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
            ContractUtils.RequiresNotNull(context, "context");
            ContractUtils.RequiresNotNull(options, "options");

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
            tokenizer.IndentationInconsistencySeverity = options.IndentationInconsistencySeverity;

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
            } catch (BadSourceException bse) {
                throw BadSourceError(bse);
            }
        }
        
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
            } catch (BadSourceException bse) {
                throw BadSourceError(bse);
            }
        }

        public PythonAst ParseTopExpression() {
            try {
                // TODO: move from source unit  .TrimStart(' ', '\t')
                _globalParent = new PythonAst(false, _languageFeatures, false, _context);
                ReturnStatement ret = new ReturnStatement(ParseTestListAsExpression());
                ret.SetLoc(_globalParent, 0, 0);
                return FinishParsing(ret);
            } catch (BadSourceException bse) {
                throw BadSourceError(bse);
            }
        }

        /// <summary>
        /// Given the interactive text input for a compound statement, calculate what the
        /// indentation level of the next line should be
        /// </summary>
        public static int GetNextAutoIndentSize(string text, int autoIndentTabWidth) {
            ContractUtils.RequiresNotNull(text, "text");

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
            if (lastLine.TrimEnd(whiteSpace).EndsWith(":"))
                autoIndentSize += autoIndentTabWidth;

            return autoIndentSize;
        }

        public ErrorSink ErrorSink {
            get {
                return _errors;
            }
            set {
                ContractUtils.RequiresNotNull(value, "value");
                _errors = value;
            }
        }

        public ParserSink ParserSink {
            get {
                return _sink;
            }
            set {
                if (_sink == ParserSink.Null) {
                    _sink = null;
                } else {
                    _sink = value;
                }
            }
        }

        public int ErrorCode {
            get { return _errorCode; }
        }

        public void Reset(SourceUnit sourceUnit, ModuleOptions languageFeatures) {
            ContractUtils.RequiresNotNull(sourceUnit, "sourceUnit");

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

        private void ReportSyntaxError(TokenWithSpan t) {
            ReportSyntaxError(t, ErrorCodes.SyntaxError);
        }

        private void ReportSyntaxError(TokenWithSpan t, int errorCode) {
            ReportSyntaxError(t.Token, t.Span, errorCode, true);
        }

        private void ReportSyntaxError(Token t, IndexSpan span, int errorCode, bool allowIncomplete) {
            var start = span.Start;
            var end = span.End;

            if (allowIncomplete && (t.Kind == TokenKind.EndOfFile || (_tokenizer.IsEndOfFile && (t.Kind == TokenKind.Dedent || t.Kind == TokenKind.NLToken)))) {
                errorCode |= ErrorCodes.IncompleteStatement;
            }

            string msg = String.Format(System.Globalization.CultureInfo.InvariantCulture, GetErrorMessage(t, errorCode), t.Image);

            ReportSyntaxError(start, end, msg, errorCode);
        }

        private static string GetErrorMessage(Token t, int errorCode) {
            string msg;
            if ((errorCode & ~ErrorCodes.IncompleteMask) == ErrorCodes.IndentationError) {
                msg = Resources.ExpectedIndentation;
            } else if (t.Kind != TokenKind.EndOfFile) {
                msg = "invalid syntax";
            } else {
                msg = "unexpected EOF while parsing";
            }
            
            return msg;
        }

        private void ReportSyntaxError(string message) {
            ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, message);
        }

        internal void ReportSyntaxError(int start, int end, string message) {
            ReportSyntaxError(start, end, message, ErrorCodes.SyntaxError);
        }

        internal void ReportSyntaxError(int start, int end, string message, int errorCode) {
            // save the first one, the next error codes may be induced errors:
            if (_errorCode == 0) {
                _errorCode = errorCode;
            }
            _errors.Add(_sourceUnit, 
                message,
                new SourceSpan(_tokenizer.IndexToLocation(start), _tokenizer.IndexToLocation(end)), 
                errorCode, 
                Severity.FatalError);
        }

        #endregion        

        #region LL(1) Parsing

        private static bool IsPrivateName(string name) {
            return name.StartsWith("__") && !name.EndsWith("__");
        }

        private string FixName(string name) {
            if (_privatePrefix != null && IsPrivateName(name)) {
                name = "_" + _privatePrefix + name;
            }

            return name;
        }

        private string ReadName() {
            NameToken n = PeekToken() as NameToken;
            if (n == null) {
                ReportSyntaxError(_lookahead);
                return null;
            }
            NextToken();
            return FixName(n.Name);
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
        //  delete node when it fails.  This is the reason we don't call ParseTargetList.
        private Statement ParseDelStmt() {
            NextToken();
            var start = GetStart();
            List<Expression> l = ParseExprList();
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

        // 'return' [testlist]
        private Statement ParseReturnStmt() {
            if (CurrentFunction == null) {
                ReportSyntaxError(IronPython.Resources.MisplacedReturn);
            }
            NextToken();
            Expression expr = null;
            var start = GetStart();
            if (!NeverTestToken(PeekToken())) {
                expr = ParseTestListAsExpr();
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
            }

            var start = GetStart();

            // Parse expression list after yield. This can be:
            // 1) empty, in which case it becomes 'yield None'
            // 2) a single expression
            // 3) multiple expression, in which case it's wrapped in a tuple.
            Expression yieldResult;

            if (MaybeEat(TokenKind.KeywordFrom)) {
                yieldResult = ParseExpression();
				yieldResult.SetLoc(_globalParent, start, GetEnd());
            } else {
                bool trailingComma;
                List<Expression> l = ParseExpressionList(out trailingComma);
                if (l.Count == 0) {
                    // Check empty expression and convert to 'none'
                    yieldResult = new ConstantExpression(null);
                    // location set to match yield location (consistent with cpython)
                    yieldResult.SetLoc(_globalParent, start, GetEnd());
                } else if (l.Count != 1) {
                    // make a tuple
                    yieldResult = MakeTupleOrExpr(l, trailingComma);
                } else {
                    // just take the single expression
                    yieldResult = l[0];
                }
            }
            Expression yieldExpression = new YieldExpression(yieldResult);

            yieldExpression.SetLoc(_globalParent, start, GetEnd());
            return yieldExpression;

        }

        private Statement FinishAssignments(Expression right) {
            List<Expression> left = null;
            Expression singleLeft = null;

            while (MaybeEat(TokenKind.Assign)) {
                string assignError = right.CheckAssign();
                if (assignError != null) {
                    ReportSyntaxError(right.StartIndex, right.EndIndex, assignError, ErrorCodes.SyntaxError | ErrorCodes.NoCaret);
                }

                if (singleLeft == null) {
                    singleLeft = right;
                } else {
                    if (left == null) {
                        left = new List<Expression>();
                        left.Add(singleLeft);
                    }
                    left.Add(right);
                }

                if (MaybeEat(TokenKind.KeywordYield)) {
                    right = ParseYieldExpression();
                } else {
                    right = ParseTestListStarExpr();
                }
            }

            if (left != null) {
                Debug.Assert(left.Count > 0);

                AssignmentStatement assign = new AssignmentStatement(left.ToArray(), right);
                assign.SetLoc(_globalParent, left[0].StartIndex, right.EndIndex);
                return assign;
            } else {
                Debug.Assert(singleLeft != null);

                AssignmentStatement assign = new AssignmentStatement(new[] { singleLeft }, right);
                assign.SetLoc(_globalParent, singleLeft.StartIndex, right.EndIndex);
                return assign;
            }
        }

        // expr_stmt: testlist_star_expr (augassign (yield_expr|testlist) | ('=' (yield_expr|testlist_star_expr))*)
        // expr_stmt: testlist (augassign (yield_expr|testlist) | ('=' (yield_expr|testlist))*)
        // testlist_star_expr: (test|star_expr) (',' (test|star_expr))* [',']
        // augassign: ('+=' | '-=' | '*=' | '@=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<=' | '>>=' | '**=' | '//=')
        // testlist: test (',' test)* [',']
        // star_expr: '*' expr
        private Statement ParseExprStmt() {
            Expression ret = ParseTestListStarExpr();
            if (ret is ErrorExpression) {
                NextToken();
            }

            if (PeekToken(TokenKind.Assign)) {
                return FinishAssignments(ret);
            } else {
                PythonOperator op = GetAssignOperator(PeekToken());
                if (op != PythonOperator.None) {
                    NextToken();
                    Expression rhs;

                    if (MaybeEat(TokenKind.KeywordYield)) {
                        rhs = ParseYieldExpression();
                    } else {
                        rhs = ParseTestListAsExpr();
                    }

                    string assignError = ret.CheckAugmentedAssign();
                    if (assignError != null) {
                        ReportSyntaxError(assignError);
                    }

                    AugmentedAssignStatement aug = new AugmentedAssignStatement(op, ret, rhs);
                    aug.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                    return aug;
                } else {
                    Statement stmt = new ExpressionStatement(ret);
                    stmt.SetLoc(_globalParent, ret.IndexSpan);
                    return stmt;
                }
            }
        }

        private PythonOperator GetAssignOperator(Token t) {
            switch (t.Kind) {
                case TokenKind.AddEqual: return PythonOperator.Add;
                case TokenKind.SubtractEqual: return PythonOperator.Subtract;
                case TokenKind.MultiplyEqual: return PythonOperator.Multiply;
                case TokenKind.TrueDivideEqual: return PythonOperator.TrueDivide;
                case TokenKind.ModEqual: return PythonOperator.Mod;
                case TokenKind.BitwiseAndEqual: return PythonOperator.BitwiseAnd;
                case TokenKind.BitwiseOrEqual: return PythonOperator.BitwiseOr;
                case TokenKind.ExclusiveOrEqual: return PythonOperator.Xor;
                case TokenKind.LeftShiftEqual: return PythonOperator.LeftShift;
                case TokenKind.RightShiftEqual: return PythonOperator.RightShift;
                case TokenKind.PowerEqual: return PythonOperator.Power;
                case TokenKind.FloorDivideEqual: return PythonOperator.FloorDivide;
                default: return PythonOperator.None;
            }
        }


        private PythonOperator GetBinaryOperator(OperatorToken token) {
            switch (token.Kind) {
                case TokenKind.Add: return PythonOperator.Add;
                case TokenKind.Subtract: return PythonOperator.Subtract;
                case TokenKind.Multiply: return PythonOperator.Multiply;
                case TokenKind.TrueDivide: return PythonOperator.TrueDivide;
                case TokenKind.Mod: return PythonOperator.Mod;
                case TokenKind.BitwiseAnd: return PythonOperator.BitwiseAnd;
                case TokenKind.BitwiseOr: return PythonOperator.BitwiseOr;
                case TokenKind.ExclusiveOr: return PythonOperator.Xor;
                case TokenKind.LeftShift: return PythonOperator.LeftShift;
                case TokenKind.RightShift: return PythonOperator.RightShift;
                case TokenKind.Power: return PythonOperator.Power;
                case TokenKind.FloorDivide: return PythonOperator.FloorDivide;
                default:
                    string message = String.Format(
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

            ImportStatement ret = new ImportStatement(names, asNames, AbsoluteImports);
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
                    ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, "invalid syntax");
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
                    ReportSyntaxError(IronPython.Resources.MisplacedFuture);
                }
                if (names == FromImportStatement.Star) {
                    ReportSyntaxError(IronPython.Resources.NoFutureStar);
                }
                fromFuture = true;
                foreach (string name in names) {
                    if (name == "division") {
                        // Ignored in Python 3
                    } else if (name == "with_statement") {
                        _languageFeatures |= ModuleOptions.WithStatement;
                    } else if (name == "absolute_import") {
                        _languageFeatures |= ModuleOptions.AbsoluteImports;
                    } else if (name == "print_function") {
                        // Ignored in Python 3
                    } else if (name == "unicode_literals") {
                        _tokenizer.UnicodeLiterals = true;
                        _languageFeatures |= ModuleOptions.UnicodeLiterals;
                    } else if (name == "nested_scopes") {
                    } else if (name == "generators") {
                    } else {
                        string strName = name;
                        fromFuture = false;

                        if (strName != "braces") {
                            ReportSyntaxError(IronPython.Resources.UnknownFutureFeature + strName);
                        } else {
                            // match CPython error message
                            ReportSyntaxError(IronPython.Resources.NotAChance);
                        }
                    }
                }
            }

            if (ateParen) {
                Eat(TokenKind.RightParenthesis);
            }

            FromImportStatement ret = new FromImportStatement(dname, (string[])names, asNames, fromFuture, AbsoluteImports);
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
            while(MaybeEat(TokenKind.Comma)) {
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
            Expression type = null, _value = null, traceback = null, cause = null;

            if (!NeverTestToken(PeekToken())) {
                type = ParseExpression();
                if (MaybeEat(TokenKind.Comma)) {
                    _value = ParseExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        traceback = ParseExpression();
                    }
                } else if (MaybeEat(TokenKind.KeywordFrom)) {
                    cause = ParseExpression();
                }
            }
            RaiseStatement ret = new RaiseStatement(type, _value, traceback, cause);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //assert_stmt: 'assert' expression [',' expression]
        private AssertStatement ParseAssertStmt() {
            Eat(TokenKind.KeywordAssert);
            var start = GetStart();
            Expression expr = ParseExpression();
            Expression message = null;
            if (MaybeEat(TokenKind.Comma)) {
                message = ParseExpression();
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
                return new ClassDefinition(null, new Expression[0], new Expression[0], ErrorStmt());
            }

            Expression metaclass = null;
            var bases = new List<Expression>();
            var keywords = new List<Expression>();
            if (MaybeEat(TokenKind.LeftParenthesis)) {
                foreach (var arg in FinishArgumentList(null)) {
                    var info = arg.GetArgumentInfo();
                    if (info.Kind == Microsoft.Scripting.Actions.ArgumentType.Simple) {
                        bases.Add(arg.Expression);
                    }
                    else if(info.Kind == Microsoft.Scripting.Actions.ArgumentType.Named) {
                        keywords.Add(arg.Expression);
                        if (arg.Name == "metaclass") {
                            metaclass = arg.Expression;
                        }
                    }
                }
            }
            var mid = GetEnd();

            // Save private prefix
            string savedPrefix = SetPrivatePrefix(name);

            // Parse the class body
            Statement body = ParseClassOrFuncBody(metaclass);

            // Restore the private prefix
            _privatePrefix = savedPrefix;

            ClassDefinition ret = new ClassDefinition(name, bases.ToArray(), keywords.ToArray(), body);
            ret.HeaderIndex =  mid;
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
                    if (_sink != null) {
                        _sink.StartParameters(GetSourceSpan());
                    }
                    Arg[] args = FinishArgumentList(null);
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
        private FunctionDefinition ParseFuncDef(bool isAsync=false) {
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
                ret = new FunctionDefinition(name, new Parameter[0], isAsync);
                ret.SetLoc(_globalParent, start, lEnd);
                return ret;
            }

            var rStart = GetStart();
            var rEnd = GetEnd();

            ret = new FunctionDefinition(name, parameters, isAsync);
            PushFunction(ret);


            Statement body = ParseClassOrFuncBody();
            FunctionDefinition ret2 = PopFunction();
            System.Diagnostics.Debug.Assert(ret == ret2);

            ret.Body = body;
            ret.HeaderIndex = rEnd;

            if (_sink != null) {
                _sink.MatchPair(
                    new SourceSpan(_tokenizer.IndexToLocation(lStart), _tokenizer.IndexToLocation(lEnd)), 
                    new SourceSpan(_tokenizer.IndexToLocation(rStart), _tokenizer.IndexToLocation(rEnd)), 
                    grouping
                );
            }

            ret.SetLoc(_globalParent, start, body.EndIndex);

            return ret;
        }

        private void CheckUniqueParameter(HashSet<string> names, string name) {
            if (names.Contains(name)) {
                ReportSyntaxError(String.Format(
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
            for (int position = 0; ; position++) {
                if (MaybeEat(terminator)) break;

                Parameter parameter;

                if (MaybeEat(TokenKind.Power)) {
                    parameter = ParseParameter(names, ParameterKind.Dictionary, allowAnnotations);
                    if (parameter == null) {
                        // no parameter name, syntax error
                        return null;
                    }
                    pl.Add(parameter);
                    Eat(terminator);
                    break;
                }

                if (MaybeEat(TokenKind.Multiply)) {
                    if (readMultiply) {
                        ReportSyntaxError(_lookahead);
                        return null;
                    }

                    if (PeekToken(TokenKind.Comma)) {
                        // TODO: "*"
                    }
                    else {
                        parameter = ParseParameter(names, ParameterKind.List, allowAnnotations);
                        if (parameter == null) {
                            // no parameter name, syntax error
                            return null;
                        }
                        pl.Add(parameter);
                    }

                    readMultiply = true;
                }
                else {
                    // If a parameter has a default value, all following parameters up until the "*" must also have a default value
                    if (readMultiply) {
                        bool dontCare = false;
                        parameter = ParseDefParameter(names, ParameterKind.Normal, allowAnnotations, ref dontCare);
                    }
                    else {
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

            return pl.ToArray();
        }

        // defparameter ::=
        //     parameter ["=" expression]
        private Parameter ParseDefParameter(HashSet<string> names, ParameterKind parameterKind, bool allowAnnotations, ref bool needDefault)
        {
            Parameter parameter = ParseParameter(names, parameterKind, allowAnnotations);
            if (parameter != null) {
                if (MaybeEat(TokenKind.Assign)) {
                    needDefault = true;
                    parameter.DefaultValue = ParseExpression();
                }
                else if (needDefault) {
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
                parameter.Annotation = ParseExpression();
            }

            return parameter;
        }

        private void CompleteParameterName(Node node, string name, HashSet<string> names) {
            if (_sink != null) {
                _sink.StartName(GetSourceSpan(), name);
            }
            CheckUniqueParameter(names, name);
            node.SetLoc(_globalParent, GetStart(), GetEnd());
        }

        //Python2.5 -> old_lambdef: 'lambda' [varargslist] ':' old_expression
        private Expression FinishOldLambdef() {
            FunctionDefinition func = ParseLambdaHelperStart(null);
            Expression expr = ParseOldExpression();
            return ParseLambdaHelperEnd(func, expr);
        }

        //lambdef: 'lambda' [varargslist] ':' expression
        private Expression FinishLambdef() {
            FunctionDefinition func = ParseLambdaHelperStart(null);
            Expression expr = ParseExpression();
            return ParseLambdaHelperEnd(func, expr);
        }


        // Helpers for parsing lambda expressions. 
        // Usage
        //   FunctionDefinition f = ParseLambdaHelperStart(string);
        //   Expression expr = ParseXYZ();
        //   return ParseLambdaHelperEnd(f, expr);
        private FunctionDefinition ParseLambdaHelperStart(string name) {
            var start = GetStart();
            Parameter[] parameters;
            parameters = ParseParameterList(TokenKind.Colon, allowAnnotations: false);
            var mid = GetEnd();

            FunctionDefinition func = new FunctionDefinition(name, parameters ?? new Parameter[0]); // new Parameter[0] for error handling of incomplete lambda
            func.HeaderIndex =  mid;
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
            Expression expr = ParseExpression();
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
        struct WithItem {
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
            Expression contextManager = ParseExpression();
            Expression var = null;
            if (MaybeEat(TokenKind.KeywordAs)) {
                var = ParseExpression();
            }

            return new WithItem(start, contextManager, var);
        }

        // async_stmt: 'async' (funcdef | with_stmt | for_stmt)
        private Statement ParseAsyncStmt() {
            Eat(TokenKind.KeywordAsync);

            if(PeekToken().Kind == TokenKind.KeywordDef) {
                FunctionDefinition def = ParseFuncDef(true);
                return def;
            }

            return null;
        }

        //for_stmt: 'for' target_list 'in' expression_list ':' suite ['else' ':' suite]
        private ForStatement ParseForStmt() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();

            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);
            Expression list = ParseTestListAsExpr();
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

        private Statement ParseClassOrFuncBody(Expression metaclass = null) {
            Statement body;
            bool inLoop = _inLoop,
                 inFinally = _inFinally,
                 inFinallyLoop = _inFinallyLoop;
            try {
                _inLoop = false;
                _inFinally = false;
                _inFinallyLoop = false;
                body = ParseSuite(metaclass);
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
            Expression expr = ParseExpression();
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

            // If this function has an except block, then it can set the current exception.
            FunctionDefinition current = CurrentFunction;
            if (current != null) {
                current.CanSetSysExcInfo = true;
            }

            var start = GetStart();
            Expression test = null, target = null;
            if (PeekToken().Kind != TokenKind.Colon) {
                test = ParseExpression();
                if (MaybeEat(TokenKind.KeywordAs)) {
                    string name = ReadName();
                    target = new NameExpression(FixName(name));
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
        private Statement ParseSuite(Expression metaclass = null) {
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

                if (metaclass != null) {
                    l.Add(new AssignmentStatement(new[] { new NameExpression("__metaclass__") }, metaclass));
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

        // Python 2.5 -> old_test: or_test | old_lambdef
        private Expression ParseOldExpression() {
            if (MaybeEat(TokenKind.KeywordLambda)) {
                return FinishOldLambdef();
            }
            return ParseOrTest();
        }

        // expression: conditional_expression | lambda_form
        // conditional_expression: or_test ['if' or_test 'else' expression]
        // lambda_form: "lambda" [parameter_list] : expression
        private Expression ParseExpression() {
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
            Expression falseExpr = ParseExpression();
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

        //not_test: 'not' not_test | comparison
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
        //comparison: expr (comp_op expr)*
        //comp_op: '<'|'>'|'=='|'>='|'<='|'!='|'in'|'not' 'in'|'is'|'is' 'not'
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

        /*
        expr: xor_expr ('|' xor_expr)*
        xor_expr: and_expr ('^' and_expr)*
        and_expr: shift_expr ('&' shift_expr)*
        shift_expr: arith_expr (('<<'|'>>') arith_expr)*
        arith_expr: term (('+'|'-') term)*
        term: factor (('*'|'/'|'%'|'//') factor)*
        */
        private Expression ParseExpr() {
            return ParseExpr(0);
        }

        private Expression ParseExpr(int precedence) {
            Expression ret = ParseFactor();
            while (true) {
                Token t = PeekToken();
                OperatorToken ot = t as OperatorToken;
                if (ot == null) return ret;

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

                if (t.Value is BigInteger) {
                    BigInteger bi = (BigInteger)t.Value;
                    uint iVal;
                    if (bi.AsUInt32(out iVal) && iVal == 0x80000000) {
                        string tokenString = _tokenizer.GetTokenString(); ;
                        Debug.Assert(tokenString.Length > 0);

                        if (tokenString[tokenString.Length - 1] != 'L' &&
                            tokenString[tokenString.Length - 1] != 'l') {
                            NextToken();
                            return new ConstantExpression(-2147483648);

                        }
                    }
                }
            }

            return new UnaryExpression(PythonOperator.Negate, ParseFactor());
        }

        // power: atom trailer* ['**' factor]
        private Expression ParsePower() {
            Expression ret = ParsePrimary();
            ret = AddTrailers(ret);
            if (MaybeEat(TokenKind.Power)) {
                var start = ret.StartIndex;
                ret = new BinaryExpression(PythonOperator.Power, ret, ParseFactor());
                ret.SetLoc(_globalParent, start, GetEnd());
            }
            return ret;
        }


        // primary: atom | attributeref | subscription | slicing | call 
        // atom:    identifier | literal | enclosure 
        // enclosure: 
        //      parenth_form | 
        //      list_display | 
        //      generator_expression | 
        //      dict_display | 
        //      string_conversion | 
        //      yield_atom 
        private Expression ParsePrimary() {
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
                    if (_sink != null) {
                        _sink.StartName(GetSourceSpan(), name);
                    }
                    ret = new NameExpression(FixName(name));
                    ret.SetLoc(_globalParent, GetStart(), GetEnd());
                    return ret;
                case TokenKind.Constant:        // literal
                    NextToken();
                    var start = GetStart();
                    object cv = t.Value;
                    string cvs = cv as string;
                    if (cvs != null) {
                        cv = FinishStringPlus(cvs);
                    } else {
                        Bytes bytes = cv as Bytes;
                        if (bytes != null) {
                            cv = FinishBytesPlus(bytes);
                        }
                    }

                    ret = new ConstantExpression(cv);
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

        private string FinishStringPlus(string s) {
            Token t = PeekToken();
            while (true) {
                if (t is ConstantValueToken) {
                    string cvs;
                    if ((cvs = t.Value as String) != null) {
                        s += cvs;
                        NextToken();
                        t = PeekToken();
                        continue;
                    } else {
                        ReportSyntaxError("invalid syntax");
                    }
                }
                break;
            }
            return s;
        }

        private Bytes FinishBytesPlus(Bytes s) {
            Token t = PeekToken();
            while (true) {
                if (t is ConstantValueToken) {
                    Bytes cvs;
                    if ((cvs = t.Value as Bytes) != null) {
                        s = s + cvs;
                        NextToken();
                        t = PeekToken();
                        continue;
                    } else {
                        ReportSyntaxError("invalid syntax");
                    }
                }
                break;
            }
            return s;
        }

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
                            Arg[] args = FinishArgListOrGenExpr();
                            CallExpression call;
                            if (args != null) {
                                call = FinishCallExpr(ret, args);
                            } else {
                                call = new CallExpression(ret, new Arg[0]);
                            }

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
                    e = ParseExpression();
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
                    e2 = ParseExpression();
                    break;
            }
            return e2;
        }

        private Expression FinishSlice(Expression e0, int start) {
            Expression e1 = null;
            Expression e2 = null;
            bool stepProvided = false;

            switch (PeekToken().Kind) {
                case TokenKind.Comma:
                case TokenKind.RightBracket:
                    break;
                case TokenKind.Colon:
                    // x[?::?]
                    stepProvided = true;
                    NextToken();
                    e2 = ParseSliceEnd();
                    break;
                default:
                    // x[?:val:?]
                    e1 = ParseExpression();
                    if (MaybeEat(TokenKind.Colon)) {
                        stepProvided = true;
                        e2 = ParseSliceEnd();
                    }
                    break;
            }
            SliceExpression ret = new SliceExpression(e0, e1, e2, stepProvided);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }


        //exprlist: (expr|star_expr) (',' (expr|star_expr))* [',']
        private List<Expression> ParseExprList() {
            List<Expression> l = new List<Expression>();
            while (true) {
                Expression e = ParseExpr();
                l.Add(e);
                if (!MaybeEat(TokenKind.Comma)) {
                    break;
                }
                if (NeverTestToken(PeekToken())) {
                    break;
                }
            }
            return l;
        }

        // arglist:
        //             expression                     rest_of_arguments
        //             expression "=" expression      rest_of_arguments
        //             expression "for" gen_expr_rest
        //
        private Arg[] FinishArgListOrGenExpr() {
            Arg a = null;
            
            if (_sink != null) {
                _sink.StartParameters(GetSourceSpan());
            }

            Token t = PeekToken();
            if (t.Kind != TokenKind.RightParenthesis && t.Kind != TokenKind.Multiply && t.Kind != TokenKind.Power) {
                var start = GetStart();
                Expression e = ParseExpression();
                if (e is ErrorExpression) {
                    return null;
                }

                if (MaybeEat(TokenKind.Assign)) {               //  Keyword argument
                    a = FinishKeywordArgument(e);

                    if (a == null) {                            // Error recovery
                        a = new Arg(e);
                        a.SetLoc(_globalParent, e.StartIndex, GetEnd());
                    }
                } else if (PeekToken(Tokens.KeywordForToken)) {    //  Generator expression
                    a = new Arg(ParseGeneratorExpression(e));
                    Eat(TokenKind.RightParenthesis);
                    a.SetLoc(_globalParent, start, GetEnd());
                    if (_sink != null) {
                        _sink.EndParameters(GetSourceSpan());
                    }
                    return new Arg[1] { a };       //  Generator expression is the argument
                } else {
                    a = new Arg(e);
                    a.SetLoc(_globalParent, e.StartIndex, e.EndIndex);
                }

                //  Was this all?
                //
                if (MaybeEat(TokenKind.Comma)) {
                    if (_sink != null) {
                        _sink.NextParameter(GetSourceSpan());
                    }
                } else {
                    Eat(TokenKind.RightParenthesis);
                    a.SetLoc(_globalParent, start, GetEnd());
                    if (_sink != null) {
                        _sink.EndParameters(GetSourceSpan());
                    }
                    return new Arg[1] { a };
                }
            }

            return FinishArgumentList(a);
        }

        private Arg FinishKeywordArgument(Expression t) {
            NameExpression n = t as NameExpression;
            if (n == null) {
                ReportSyntaxError(IronPython.Resources.ExpectedName);
                Arg arg = new Arg(null, t);
                arg.SetLoc(_globalParent, t.StartIndex, t.EndIndex);
                return arg;
            } else {
                Expression val = ParseExpression();
                Arg arg = new Arg(n.Name, val);
                arg.SetLoc(_globalParent, n.StartIndex, val.EndIndex);
                return arg;
            }
        }

        private void CheckUniqueArgument(List<Arg> names, Arg arg) {
            if (arg != null && arg.Name != null) {
                for (int i = 0; i < names.Count; i++) {
                    if (names[i].Name == arg.Name) {
                        ReportSyntaxError(IronPython.Resources.DuplicateKeywordArg);
                    }
                }
            }
        }

        //arglist: (argument ',')* (argument [',']| '*' expression [',' '**' expression] | '**' expression)
        //argument: [expression '='] expression    # Really [keyword '='] expression
        private Arg[] FinishArgumentList(Arg first) {
            const TokenKind terminator = TokenKind.RightParenthesis;
            List<Arg> l = new List<Arg>();

            if (first != null) {
                l.Add(first);
            }

            // Parse remaining arguments
            while (true) {
                if (MaybeEat(terminator)) {
                    break;
                }
                var start = GetStart();
                Arg a;
                if (MaybeEat(TokenKind.Multiply)) {
                    Expression t = ParseExpression();
                    a = new Arg("*", t);
                } else if (MaybeEat(TokenKind.Power)) {
                    Expression t = ParseExpression();
                    a = new Arg("**", t);
                } else {
                    Expression e = ParseExpression();
                    if (MaybeEat(TokenKind.Assign)) {
                        a = FinishKeywordArgument(e);
                        CheckUniqueArgument(l, a);
                    } else {
                        a = new Arg(e);
                    }
                }
                a.SetLoc(_globalParent, start, GetEnd());
                l.Add(a);
                if (MaybeEat(TokenKind.Comma)) {
                    if (_sink != null) {
                        _sink.NextParameter(GetSourceSpan());
                    }
                } else {
                    Eat(terminator);
                    break;
                }
            }

            if (_sink != null) {
                _sink.EndParameters(GetSourceSpan());
            }

            Arg[] ret = l.ToArray();
            return ret;
        }

        private List<Expression> ParseTestList() {
            bool tmp;
            return ParseExpressionList(out tmp);
        }

        private Expression ParseOldExpressionListAsExpr() {
            bool trailingComma;
            List<Expression> l = ParseOldExpressionList(out trailingComma);
            //  the case when no expression was parsed e.g. when we have an empty expression list
            if (l.Count == 0 && !trailingComma) {
                ReportSyntaxError("invalid syntax");
            }
            return MakeTupleOrExpr(l, trailingComma);
        }

        // old_expression_list: old_expression [(',' old_expression)+ [',']]
        private List<Expression> ParseOldExpressionList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            trailingComma = false;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseOldExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }
            return l;
        }

        // target_list: target ("," target)* [","] 
        private List<Expression> ParseTargetList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            while (true) {
                l.Add(ParseTarget());

                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }

                trailingComma = true;

                if (NeverTestToken(PeekToken())) break;
            }

            return l;
        }

        // target: identifier | "(" target_list ")"  | "[" target_list "]"  | attributeref  | subscription  | slicing 
        private Expression ParseTarget() {
            Token t = PeekToken();
            switch (t.Kind) {
                case TokenKind.LeftParenthesis: // parenth_form or generator_expression
                case TokenKind.LeftBracket:     // list_display
                    Eat(t.Kind);

                    bool trailingComma;
                    Expression res = MakeTupleOrExpr(ParseTargetList(out trailingComma), trailingComma);

                    if (t.Kind == TokenKind.LeftParenthesis) {
                        Eat(TokenKind.RightParenthesis);
                    } else {
                        Eat(TokenKind.RightBracket);
                    }

                    return res;
                default:        // identifier, attribute ref, subscription, slicing
                    return AddTrailers(ParsePrimary(), false);
            }
        }
        
        // expression_list: expression (',' expression)* [',']
        private List<Expression> ParseExpressionList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            trailingComma = false;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }
            return l;
        }

        private Expression ParseTestListAsExpr() {
            if (!NeverTestToken(PeekToken())) {
                var expr = ParseExpression();
                if (!MaybeEat(TokenKind.Comma)) {
                    return expr;
                }

                return ParseTestListAsExpr(expr);
            } else {
                return ParseTestListAsExprError();
            }
        }

        private Expression ParseTestListAsExpr(Expression expr) {
            List<Expression> l = new List<Expression>();
            l.Add(expr);

            bool trailingComma = true;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
            }
            return MakeTupleOrExpr(l, trailingComma);
        }


        private Expression ParseTestListStarExpr() {
            if (MaybeEat(TokenKind.Multiply)) {
                var start = GetStart();
                var expr = new StarredExpression(ParseExpression());
                expr.SetLoc(_globalParent, start, GetEnd());
                if (!MaybeEat(TokenKind.Comma)) {
                    return expr;
                }

                return ParseTestListStarExpr(expr);
            }
            if (!NeverTestToken(PeekToken())) {
                var expr = ParseExpression();
                if (!MaybeEat(TokenKind.Comma)) {
                    return expr;
                }

                return ParseTestListStarExpr(expr);
            } else {
                return ParseTestListAsExprError();
            }
        }

        private Expression ParseTestListStarExpr(Expression expr) {
            List<Expression> l = new List<Expression>();
            l.Add(expr);

            bool trailingComma = true;
            while (true) {
                if (MaybeEat(TokenKind.Multiply)) {
                    var start = GetStart();
                    var sExpr = new StarredExpression(ParseExpression());
                    sExpr.SetLoc(_globalParent, start, GetEnd());
                    l.Add(sExpr);
                }
                else {
                    if (NeverTestToken(PeekToken())) break;
                    l.Add(ParseExpression());
                }
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
            }
            return MakeTupleOrExpr(l, trailingComma);
        }


        private Expression ParseTestListAsExprError() {
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
            List<Expression> l = new List<Expression>();
            l.Add(expr);

            while (true) {
                if (NeverTestToken(PeekToken())) break;
                expr = ParseExpression();
                l.Add(expr);
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }

            Expression ret = MakeTupleOrExpr(l, trailingComma);
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

                    Expression expr = ParseExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        // "(" expression "," ...
                        ret = FinishExpressionListAsExpr(expr);
                    } else if (PeekToken(Tokens.KeywordForToken)) {
                        // "(" expression "for" ...
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

            if (hasRightParenthesis && _sink != null) {
                _sink.MatchPair(new SourceSpan(_tokenizer.IndexToLocation(lStart), _tokenizer.IndexToLocation(lEnd)), new SourceSpan(_tokenizer.IndexToLocation(rStart), _tokenizer.IndexToLocation(rEnd)), grouping);
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
            ForStatement fes = current as ForStatement;
            IfStatement ifs;
            if (fes != null) {
                fes.Body = nested;
            } else if ((ifs = current as IfStatement) != null) {
                ifs.Tests[0].Body = nested;
            }
            return nested;
        }

        // "for" target_list "in" or_test
        private ForStatement ParseGenExprFor() {
            var start = GetStart();
            Eat(TokenKind.KeywordFor);
            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);
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

        //  genexpr_if: "if" old_test
        private IfStatement ParseGenExprIf() {
            var start = GetStart();
            Eat(TokenKind.KeywordIf);
            Expression expr = ParseOldExpression();
            IfStatementTest ist = new IfStatementTest(expr, null);
            var end = GetEnd();
            ist.HeaderIndex = end;
            ist.SetLoc(_globalParent, start, end);
            IfStatement gei = new IfStatement(new IfStatementTest[] { ist }, null);
            gei.SetLoc(_globalParent, start, end);
            return gei;
        }


        // dict_display: '{' [dictorsetmaker] '}'
        // dictorsetmaker: ( (test ':' test (comp_for | (',' test ':' test)* [','])) |
        //                   (test (comp_for | (',' test)* [','])) )


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
                    Expression e1 = ParseExpression();
                    if (MaybeEat(TokenKind.Colon)) { // dict literal
                        if (setMembers != null) {
                            ReportSyntaxError("invalid syntax");
                        } else if (dictMembers == null) {
                            dictMembers = new List<SliceExpression>();
                            first = true;
                        }
                        Expression e2 = ParseExpression();

                        if (PeekToken(Tokens.KeywordForToken)) {
                            if (!first) {
                                ReportSyntaxError("invalid syntax");
                            }
                            return FinishDictComp(e1, e2, oStart, oEnd);
                        }

                        SliceExpression se = new SliceExpression(e1, e2, null, false);
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
                        if (setMembers != null) {
                            setMembers.Add(e1);
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

            if (_sink != null) {
                _sink.MatchPair(
                    new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)), 
                    new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)), 
                    1
                );
            }

            if (dictMembers != null || setMembers == null) {
                SliceExpression[] exprs;
                if (dictMembers != null) {
                    exprs = dictMembers.ToArray();
                } else {
                    exprs = new SliceExpression[0];
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
            if (_sink != null) {
                _sink.MatchPair(
                    new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)),
                    new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)),
                    1
                );
            }

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
            
            if (_sink != null) {
                _sink.MatchPair(
                    new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)),
                    new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)),
                    1
                );
            }
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

        // comp_for: 'for target_list 'in' or_test [comp_iter]
        private ComprehensionFor ParseCompFor() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();
            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);

            // expr list is something like:
            //  ()
            // a
            // a,b
            // a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression list = ParseOrTest();

            ComprehensionFor ret = new ComprehensionFor(lhs, list);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // listmaker: expression ( list_for | (',' expression)* [','] )
        private Expression FinishListValue() {
            var oStart = GetStart();
            var oEnd = GetEnd();
            int grouping = _tokenizer.GroupingLevel;

            Expression ret;
            if (MaybeEat(TokenKind.RightBracket)) {
                ret = new ListExpression();
            } else {
                bool prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;
                    Expression t0 = ParseExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        List<Expression> l = ParseTestList();
                        Eat(TokenKind.RightBracket);
                        l.Insert(0, t0);
                        ret = new ListExpression(l.ToArray());
                    } else if (PeekToken(Tokens.KeywordForToken)) {
                        ret = FinishListComp(t0);
                    } else {
                        Eat(TokenKind.RightBracket);
                        ret = new ListExpression(t0);
                    }
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }

            var cStart = GetStart();
            var cEnd = GetEnd();

            if (_sink != null) {
                _sink.MatchPair(
                    new SourceSpan(_tokenizer.IndexToLocation(oStart), _tokenizer.IndexToLocation(oEnd)), 
                    new SourceSpan(_tokenizer.IndexToLocation(cStart), _tokenizer.IndexToLocation(cEnd)), 
                    grouping
                );
            }

            ret.SetLoc(_globalParent, oStart, cEnd);
            return ret;
        }

        // list_iter ']'
        private ListComprehension FinishListComp(Expression item) {
            ComprehensionIterator[] iters = ParseListCompIter();
            Eat(TokenKind.RightBracket);
            return new ListComprehension(item, iters);
        }

        // list_iter: list_for | list_if
        private ComprehensionIterator[] ParseListCompIter() {
            List<ComprehensionIterator> iters = new List<ComprehensionIterator>();
            ComprehensionFor firstFor = ParseListCompFor();
            iters.Add(firstFor);

            while (true) {
                if (PeekToken(Tokens.KeywordForToken)) {
                    iters.Add(ParseListCompFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    iters.Add(ParseCompIf());
                } else {
                    break;
                }
            }

            return iters.ToArray();
        }

        // list_for: 'for' target_list 'in' old_expression_list [list_iter]
        private ComprehensionFor ParseListCompFor() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();
            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression list = ParseOldExpressionListAsExpr();

            ComprehensionFor ret = new ComprehensionFor(lhs, list);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // list_if: 'if' old_test [list_iter]
        // comp_if: 'if' old_test [comp_iter]
        private ComprehensionIf ParseCompIf() {
            Eat(TokenKind.KeywordIf);
            var start = GetStart();
            Expression expr = ParseOldExpression();
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
                case TokenKind.TrueDivideEqual:
                case TokenKind.ModEqual:
                case TokenKind.BitwiseAndEqual:
                case TokenKind.BitwiseOrEqual:
                case TokenKind.ExclusiveOrEqual:
                case TokenKind.LeftShiftEqual:
                case TokenKind.RightShiftEqual:
                case TokenKind.PowerEqual:
                case TokenKind.FloorDivideEqual:

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

        private CallExpression FinishCallExpr(Expression target, params Arg[] args) {
            bool hasArgsTuple = false;
            bool hasKeywordDict = false;
            int keywordCount = 0;
            int extraArgs = 0;

            foreach (Arg arg in args) {
                if (arg.Name == null) {
                    if (hasArgsTuple || hasKeywordDict || keywordCount > 0) {
                        ReportSyntaxError(IronPython.Resources.NonKeywordAfterKeywordArg);
                    }
                } else if (arg.Name == "*") {
                    if (hasArgsTuple || hasKeywordDict) {
                        ReportSyntaxError(IronPython.Resources.OneListArgOnly);
                    }
                    hasArgsTuple = true; extraArgs++;
                } else if (arg.Name == "**") {
                    if (hasKeywordDict) {
                        ReportSyntaxError(IronPython.Resources.OneKeywordArgOnly);
                    }
                    hasKeywordDict = true; extraArgs++;
                } else {
                    if (hasKeywordDict) {
                        ReportSyntaxError(IronPython.Resources.KeywordOutOfSequence);
                    }
                    keywordCount++;
                }
            }

            return new CallExpression(target, args);
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            if (_sourceReader != null) {
                _sourceReader.Dispose();
            }
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
                ExpressionStatement es = s as ExpressionStatement;
                if (es != null) {
                    ConstantExpression ce = es.Expression as ConstantExpression;
                    if (ce != null && ce.Value is string) {
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
                    FromImportStatement fis = s as FromImportStatement;
                    if (fis != null && !fis.IsFromFuture) {
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
                ExpressionStatement exprStmt = stmts[stmts.Length - 1] as ExpressionStatement;
                if (exprStmt != null) {
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
            } catch (BadSourceException bse) {
                throw BadSourceError(bse);
            }
        }



        private Expression ParseTestListAsExpression() {
            StartParsing();

            Expression expression = ParseTestListAsExpr();
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

        private Exception/*!*/ BadSourceError(BadSourceException bse) {
            StreamReader sr = _sourceReader.BaseReader as StreamReader;
            if (sr != null && sr.BaseStream.CanSeek) {
                return PythonContext.ReportEncodingError(sr.BaseStream, _sourceUnit.Path);

            }
            // BUG: We have some weird stream and we can't accurately track the 
            // position where the exception came from.  There are too many levels
            // of buffering below us to re-wind and calculate the actual line number, so
            // we'll give the last line number the tokenizer was at.
            return IronPython.Runtime.Operations.PythonOps.BadSourceError(
                bse._badByte,
                new SourceSpan(_tokenizer.CurrentPosition, _tokenizer.CurrentPosition),
                _sourceUnit.Path
            );
        }

        private bool AbsoluteImports {
            get { return (_languageFeatures & ModuleOptions.AbsoluteImports) == ModuleOptions.AbsoluteImports; }
        }

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
