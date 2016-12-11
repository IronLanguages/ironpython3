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
//#define DUMP_TOKENS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Compiler {

    /// <summary>
    /// IronPython tokenizer
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Tokenizer")]
    public sealed partial class Tokenizer : TokenizerService {
        private State _state;
        private readonly bool _verbatim;
        internal bool _dontImplyDedent;
        private bool _disableLineFeedLineSeparator;
        private SourceUnit _sourceUnit;
        private ErrorSink _errors;
        private Severity _indentationInconsistencySeverity;
        private bool _endContinues, _unicodeLiterals;
        private List<int> _newLineLocations;
        private SourceLocation _initialLocation;
        private TextReader _reader;
        private char[] _buffer;
        private bool _multiEolns;
        private int _position, _end, _tokenEnd, _start, _tokenStartIndex, _tokenEndIndex;
        private bool _bufferResized;

        private const int EOF = -1;
        private const int MaxIndent = 80;
        private const int DefaultBufferCapacity = 1024;

        private Dictionary<object, NameToken> _names;
        private static object _currentName = new object();

        public Tokenizer() {
            _errors = ErrorSink.Null;
            _verbatim = true;
            _state = new State(null);
            _names = new Dictionary<object, NameToken>(128, new TokenEqualityComparer(this));
        }

        public Tokenizer(ErrorSink errorSink) {
            _errors = errorSink;
            _state = new State(null);
            _names = new Dictionary<object, NameToken>(128, new TokenEqualityComparer(this));
        }

        public Tokenizer(ErrorSink errorSink, PythonCompilerOptions options) {
            ContractUtils.RequiresNotNull(errorSink, "errorSink");
            ContractUtils.RequiresNotNull(options, "options");

            _errors = errorSink;
            _verbatim = options.Verbatim;
            _state = new State(null);
            _dontImplyDedent = options.DontImplyDedent;
            _unicodeLiterals = options.UnicodeLiterals;
            _names = new Dictionary<object, NameToken>(128, new TokenEqualityComparer(this));
        }

        /// <summary>
        /// Used to create tokenizer for hosting API.
        /// </summary>
        internal Tokenizer(ErrorSink errorSink, PythonCompilerOptions options, bool verbatim)
            : this(errorSink, options) {

            _verbatim = verbatim || options.Verbatim;
        }

        public override bool IsRestartable {
            get { return true; }
        }

        public override object CurrentState {
            get {
                return _state;
            }
        }

        public override SourceLocation CurrentPosition {
            get { 
                return IndexToLocation(CurrentIndex);
            }
        }

        public SourceLocation IndexToLocation(int index) {            
            int match = _newLineLocations.BinarySearch(index);
            if (match < 0) {
                // If our index = -1, it means we're on the first line.
                if (match == -1) {
                    return new SourceLocation(index + _initialLocation.Index, _initialLocation.Line, checked(index + _initialLocation.Column));
                }
                // If we couldn't find an exact match for this line number, get the nearest
                // matching line number less than this one
                match = ~match - 1;
            }
            
            return new SourceLocation(index + _initialLocation.Index, _sourceUnit.MapLine(match + 2) + _initialLocation.Line - 1, index - _newLineLocations[match] + _initialLocation.Column);
        }

        public SourceUnit SourceUnit {
            get {
                return _sourceUnit;
            }
        }

        public override ErrorSink ErrorSink {
            get { return _errors; }
            set {
                ContractUtils.RequiresNotNull(value, "value");
                _errors = value;
            }
        }

        public Severity IndentationInconsistencySeverity {
            get { return _indentationInconsistencySeverity; }
            set {
                _indentationInconsistencySeverity = value;

                if (value != Severity.Ignore && _state.IndentFormat == null) {
                    _state.IndentFormat = new StringBuilder[MaxIndent];
                }
            }
        }

        public bool IsEndOfFile {
            get {
                return Peek() == EOF;
            }
        }

        public IndexSpan TokenSpan {
            get {
                return new IndexSpan(_tokenStartIndex, _tokenEndIndex - _tokenStartIndex);
            }
        }

        public void Initialize(SourceUnit sourceUnit) {
            ContractUtils.RequiresNotNull(sourceUnit, "sourceUnit");

            Initialize(null, sourceUnit.GetReader(), sourceUnit, SourceLocation.MinValue, DefaultBufferCapacity);
        }

        public override void Initialize(object state, TextReader reader, SourceUnit sourceUnit, SourceLocation initialLocation) {
            Initialize(state, reader, sourceUnit, initialLocation, DefaultBufferCapacity);
        }

        public void Initialize(object state, TextReader reader, SourceUnit sourceUnit, SourceLocation initialLocation, int bufferCapacity) {
            Initialize(state, reader, sourceUnit, initialLocation, bufferCapacity, null);
        }

        public void Initialize(object state, TextReader reader, SourceUnit sourceUnit, SourceLocation initialLocation, int bufferCapacity, PythonCompilerOptions compilerOptions) {
            ContractUtils.RequiresNotNull(reader, "reader");

            if (state != null) {
                if (!(state is State)) throw new ValueErrorException("bad state provided");
                _state = new State((State)state);
            } else {
                _state = new State(null);
            }

            if (compilerOptions != null && compilerOptions.InitialIndent != null) {
                _state.Indent = (int[])compilerOptions.InitialIndent.Clone();
            }

            _sourceUnit = sourceUnit;
            _disableLineFeedLineSeparator = reader is NoLineFeedSourceContentProvider.Reader;

            _reader = reader;
            
            if (_buffer == null || _buffer.Length < bufferCapacity) {
                _buffer = new char[bufferCapacity];
            }
            
            _newLineLocations = new List<int>();
            _tokenEnd = -1;
            _multiEolns = !_disableLineFeedLineSeparator;
            _initialLocation = initialLocation;

            _tokenEndIndex = -1;
            _tokenStartIndex = 0;

            _start = _end = 0;
            _position = 0;

            DumpBeginningOfUnit();
        }

        public override TokenInfo ReadToken() {
            if (_buffer == null) {
                throw new InvalidOperationException("Uninitialized");
            }

            TokenInfo result = new TokenInfo();
            Token token = GetNextToken();
            result.SourceSpan = new SourceSpan(IndexToLocation(TokenSpan.Start), IndexToLocation(TokenSpan.End));

            switch (token.Kind) {
                case TokenKind.EndOfFile:
                    result.Category = TokenCategory.EndOfStream;
                    break;

                case TokenKind.Comment:
                    result.Category = TokenCategory.Comment;
                    break;

                case TokenKind.Name:
                    result.Category = TokenCategory.Identifier;
                    break;

                case TokenKind.Error:
                    if (token is IncompleteStringErrorToken) {
                        result.Category = TokenCategory.StringLiteral;
                    } else {
                        result.Category = TokenCategory.Error;
                    }
                    break;

                case TokenKind.Constant:
                    result.Category = (token.Value is string) ? TokenCategory.StringLiteral : TokenCategory.NumericLiteral;
                    break;

                case TokenKind.LeftParenthesis:
                    result.Category = TokenCategory.Grouping;
                    result.Trigger = TokenTriggers.MatchBraces | TokenTriggers.ParameterStart;
                    break;

                case TokenKind.RightParenthesis:
                    result.Category = TokenCategory.Grouping;
                    result.Trigger = TokenTriggers.MatchBraces | TokenTriggers.ParameterEnd;
                    break;

                case TokenKind.LeftBracket:
                case TokenKind.LeftBrace:
                case TokenKind.RightBracket:
                case TokenKind.RightBrace:
                    result.Category = TokenCategory.Grouping;
                    result.Trigger = TokenTriggers.MatchBraces;
                    break;

                case TokenKind.Colon:
                    result.Category = TokenCategory.Delimiter;
                    break;

                case TokenKind.Semicolon:
                    result.Category = TokenCategory.Delimiter;
                    break;

                case TokenKind.Comma:
                    result.Category = TokenCategory.Delimiter;
                    result.Trigger = TokenTriggers.ParameterNext;
                    break;

                case TokenKind.Dot:
                    result.Category = TokenCategory.Operator;
                    result.Trigger = TokenTriggers.MemberSelect;
                    break;

                case TokenKind.NewLine:
                    result.Category = TokenCategory.WhiteSpace;
                    break;

                default:
                    if (token.Kind >= TokenKind.FirstKeyword && token.Kind <= TokenKind.LastKeyword) {
                        result.Category = TokenCategory.Keyword;
                        break;
                    }

                    result.Category = TokenCategory.Operator;
                    break;
            }

            return result;
        }

        internal bool TryGetTokenString(int len, out string tokenString) {
            if (len != TokenLength) {
                tokenString = null;
                return false;
            }
            tokenString = GetTokenString();
            return true;
        }

        internal bool UnicodeLiterals {
            get {
                return _unicodeLiterals;
            }
            set {
                _unicodeLiterals = value;
            }
        }

        public Token GetNextToken() {
            Token result;

            if (_state.PendingDedents != 0) {
                if (_state.PendingDedents == -1) {
                    _state.PendingDedents = 0;
                    result = Tokens.IndentToken;
                } else {
                    _state.PendingDedents--;
                    result = Tokens.DedentToken;
                }
            } else {
                result = Next();
            }

            DumpToken(result);
            return result;
        }

        private Token Next() {
            bool at_beginning = AtBeginning;

            if (_state.IncompleteString != null && Peek() != EOF) {
                IncompleteString prev = _state.IncompleteString;
                _state.IncompleteString = null;
                return ContinueString(prev.IsSingleTickQuote ? '\'' : '"', prev.IsRaw, prev.IsUnicode, false, prev.IsTripleQuoted, 0);
            }

            DiscardToken();

            int ch = NextChar();

            while (true) {
                switch (ch) {
                    case EOF:
                        return ReadEof();
                    case '\f':
                        // Ignore form feeds
                        DiscardToken();
                        ch = NextChar();
                        break;
                    case ' ':
                    case '\t':
                        ch = SkipWhiteSpace(at_beginning);
                        break;

                    case '#':
                        if (_verbatim)
                            return ReadSingleLineComment();

                        ch = SkipSingleLineComment();
                        break;

                    case '\\':
                        if (ReadEolnOpt(NextChar()) > 0) {
                            _newLineLocations.Add(CurrentIndex);
                            // discard token '\\<eoln>':
                            DiscardToken();

                            ch = NextChar();
                            if (ch == -1) {
                                _endContinues = true;
                            }
                            break;

                        } else {
                            BufferBack();
                            goto default;
                        }

                    case '\"':
                    case '\'':
                        _state.LastNewLine = false;
                        return ReadString((char)ch, false, false, false);

                    case 'u':
                    case 'U':
                        _state.LastNewLine = false;
                        return ReadNameOrUnicodeString();

                    case 'r':
                    case 'R':
                        _state.LastNewLine = false;
                        return ReadNameOrRawString();
                    case 'b':
                    case 'B':
                        _state.LastNewLine = false;
                        return ReadNameOrBytes();

                    case '_':
                        _state.LastNewLine = false;
                        return ReadName();

                    case '.':
                        _state.LastNewLine = false;
                        ch = Peek();
                        if (ch >= '0' && ch <= '9')
                            return ReadFraction();

                        MarkTokenEnd();
                        return Tokens.DotToken;

                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        _state.LastNewLine = false;
                        return ReadNumber(ch);

                    default:

                        if (ReadEolnOpt(ch) > 0) {
                            _newLineLocations.Add(CurrentIndex);
                            // token marked by the callee:
                            if (ReadNewline()) {
                                if (_state.LastNewLine) {
                                    return Tokens.NLToken;
                                } else {
                                    _state.LastNewLine = true;
                                    return Tokens.NewLineToken;
                                }
                            }

                            // we're in a grouping, white space is ignored
                            DiscardToken();
                            ch = NextChar();
                            break;
                        }

                        _state.LastNewLine = false;
                        Token res = NextOperator(ch);
                        if (res != null) {
                            MarkTokenEnd();
                            return res;
                        }

                        if (IsNameStart(ch)) return ReadName();

                        MarkTokenEnd();
                        return BadChar(ch);
                }
            }
        }

        private int SkipWhiteSpace(bool atBeginning) {
            int ch;
            do { ch = NextChar(); } while (ch == ' ' || ch == '\t');

            BufferBack();

            if (atBeginning && ch != '#' && ch != '\f' && ch != EOF && !IsEoln(ch)) {
                MarkTokenEnd();
                ReportSyntaxError(BufferTokenSpan, Resources.InvalidSyntax, ErrorCodes.SyntaxError);
            }

            DiscardToken();
            SeekRelative(+1);
            return ch;
        }

        private int SkipSingleLineComment() {
            // do single-line comment:
            int ch = ReadLine();
            MarkTokenEnd();

            // discard token '# ...':
            DiscardToken();
            SeekRelative(+1);

            return ch;
        }

        private Token ReadSingleLineComment() {
            // do single-line comment:
            ReadLine();
            MarkTokenEnd();

            return new CommentToken(GetTokenString());
        }

        private Token ReadNameOrUnicodeString() {
            if (NextChar('\"')) return ReadString('\"', false, true, false);
            if (NextChar('\'')) return ReadString('\'', false, true, false);
            if (NextChar('r') || NextChar('R')) {
                if (NextChar('\"')) return ReadString('\"', true, true, false);
                if (NextChar('\'')) return ReadString('\'', true, true, false);
                BufferBack();
            }
            return ReadName();
        }

        private Token ReadNameOrBytes() {
            if (NextChar('\"')) return ReadString('\"', false, false, true);
            if (NextChar('\'')) return ReadString('\'', false, false, true);
            if (NextChar('r') || NextChar('R')) {
                if (NextChar('\"')) return ReadString('\"', true, false, true);
                if (NextChar('\'')) return ReadString('\'', true, false, true);
                BufferBack();
            }
            return ReadName();
        }

        private Token ReadNameOrRawString() {
            if (NextChar('\"')) return ReadString('\"', true, false, false);
            if (NextChar('\'')) return ReadString('\'', true, false, false);
            return ReadName();
        }

        private Token ReadEof() {
            MarkTokenEnd();

            if (!_dontImplyDedent && _state.IndentLevel > 0) {
                // before we imply dedents we need to make sure the last thing we returned was
                // a new line.
                if (!_state.LastNewLine) {
                    _state.LastNewLine = true;
                    return Tokens.NewLineToken;
                }

                // and then go ahead and imply the dedents.
                SetIndent(0, null);
                _state.PendingDedents--;
                return Tokens.DedentToken;
            }

            return Tokens.EndOfFileToken;
        }

        private static ErrorToken BadChar(int ch) {
            return new ErrorToken(StringUtils.AddSlashes(((char)ch).ToString()));
        }

        private static bool IsNameStart(int ch) {
            return Char.IsLetter(unchecked((char)ch)) || ch == '_';
        }

        private static bool IsNamePart(int ch) {
            return Char.IsLetterOrDigit(unchecked((char)ch)) || ch == '_';
        }

        private Token ReadString(char quote, bool isRaw, bool isUni, bool isBytes) {
            int sadd = 0;
            bool isTriple = false;

            if (NextChar(quote)) {
                if (NextChar(quote)) {
                    isTriple = true; sadd += 3;
                } else {
                    BufferBack();
                    sadd++;
                }
            } else {
                sadd++;
            }

            if (isRaw) sadd++;
            if (isUni) sadd++;
            if (isBytes) sadd++;

            return ContinueString(quote, isRaw, isUni, isBytes, isTriple, sadd);
        }

        private Token ContinueString(char quote, bool isRaw, bool isUnicode, bool isBytes, bool isTriple, int startAdd) {
            // PERF: Might be nice to have this not need to get the whole token (which requires a buffer >= in size to the
            // length of the string) and instead build up the string via pieces.  Currently on files w/ large doc strings we
            // are forced to grow our buffer.

            int end_add = 0;
            int eol_size = 0;

            for (; ; ) {
                int ch = NextChar();

                if (ch == EOF) {
                    BufferBack();

                    if (isTriple) {
                        // CPython reports the multi-line string error as if it is a single line
                        // ending at the last char in the file.

                        MarkTokenEnd();

                        var errorEnd = new SourceLocation(BufferTokenEnd.Index - 1, BufferTokenEnd.Line, IndexToLocation(_tokenStartIndex).Column + _tokenEndIndex - _tokenStartIndex - 1);
                        ReportSyntaxError(new SourceSpan(errorEnd, errorEnd), Resources.EofInTripleQuotedString, ErrorCodes.SyntaxError | ErrorCodes.IncompleteToken);
                    } else {
                        MarkTokenEnd();
                    }
                    
                    UnexpectedEndOfString(isTriple, isTriple);
                    string incompleteContents = GetTokenSubstring(startAdd, TokenLength - startAdd - end_add);
                    
                    _state.IncompleteString = new IncompleteString(quote == '\'', isRaw, isUnicode, isTriple);
                    return new IncompleteStringErrorToken(Resources.EofInString, incompleteContents);
                } else if (ch == quote) {

                    if (isTriple) {
                        if (NextChar(quote) && NextChar(quote)) {
                            end_add += 3;
                            break;
                        }
                    } else {
                        end_add++;
                        break;
                    }

                } else if (ch == '\\') {

                    ch = NextChar();

                    if (ch == EOF) {
                        BufferBack();

                        MarkTokenEnd();
                        UnexpectedEndOfString(isTriple, isTriple);

                        string incompleteContents = GetTokenSubstring(startAdd, TokenLength - startAdd - end_add - 1);
                        
                        _state.IncompleteString = new IncompleteString(quote == '\'', isRaw, isUnicode, isTriple);

                        return new IncompleteStringErrorToken(Resources.EofInString, incompleteContents);
                    } else if ((eol_size = ReadEolnOpt(ch)) > 0) {
                        _newLineLocations.Add(CurrentIndex);

                        // skip \<eoln> unless followed by EOF:
                        if (Peek() == EOF) {

                            // backup over the eoln:
                            SeekRelative(-eol_size);
                            MarkTokenEnd();

                            // incomplete string in the form "abc\

                            string incompleteContents = GetTokenSubstring(startAdd, TokenLength - startAdd - end_add - 1);                            

                            UnexpectedEndOfString(isTriple, true);
                            return new IncompleteStringErrorToken(Resources.EofInString, incompleteContents);
                        }

                    } else if (ch != quote && ch != '\\') {
                        BufferBack();
                    }

                } else if ((eol_size = ReadEolnOpt(ch)) > 0) {
                    _newLineLocations.Add(CurrentIndex);
                    if (!isTriple) {
                        // backup over the eoln:
                        SeekRelative(-eol_size);

                        MarkTokenEnd();
                        UnexpectedEndOfString(isTriple, false);

                        string incompleteContents = GetTokenSubstring(startAdd, TokenLength - startAdd - end_add);
                        
                        return new IncompleteStringErrorToken((quote == '"') ? Resources.NewLineInDoubleQuotedString : Resources.NewLineInSingleQuotedString, incompleteContents);
                    }
                }
            }

            MarkTokenEnd();

            return MakeStringToken(quote, isRaw, isUnicode, isBytes, isTriple, _start + startAdd, TokenLength - startAdd - end_add);
        }

        private Token MakeStringToken(char quote, bool isRaw, bool isUnicode, bool isBytes, bool isTriple, int start, int length) {
            if (!isBytes) {
                string contents = LiteralParser.ParseString(_buffer, start, length, isRaw, isUnicode || UnicodeLiterals, !_disableLineFeedLineSeparator);
                if (isUnicode) {
                    return new UnicodeStringToken(contents);
                }
                return new ConstantValueToken(contents);
            } else {
                List<byte> data = LiteralParser.ParseBytes(_buffer, start, length, isRaw, !_disableLineFeedLineSeparator);
                if (data.Count == 0) {
                    return new ConstantValueToken(Bytes.Empty);
                }

                return new ConstantValueToken(new Bytes(data));
            }
        }

        private void UnexpectedEndOfString(bool isTriple, bool isIncomplete) {
            string message = isTriple ? Resources.EofInTripleQuotedString : Resources.EolInSingleQuotedString;
            int error = isIncomplete ? ErrorCodes.SyntaxError | ErrorCodes.IncompleteToken : ErrorCodes.SyntaxError;

            ReportSyntaxError(BufferTokenSpan, message, error);
        }

        private Token ReadNumber(int start) {
            int b = 10;
            if (start == '0') {
                if (NextChar('x') || NextChar('X')) {
                    return ReadHexNumber();
                } else {
                    if (NextChar('b') || NextChar('B')) {
                        return ReadBinaryNumber();
                    } else if (NextChar('o') || NextChar('O')) {
                        return ReadOctalNumber();
                    }
                }
                b = 8;
            }

            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case '.':
                        return ReadFraction();

                    case 'e':
                    case 'E':
                        return ReadExponent();

                    case 'j':
                    case 'J':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseImaginary(GetTokenString()));

                    case 'l':
                    case 'L':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseBigInteger(GetTokenString(), b));

                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        break;

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenString(), b));
                }
            }
        }

        private Token ReadBinaryNumber() {
            int bits = 0;
            int iVal = 0;
            bool useBigInt = false;
            BigInteger bigInt = BigInteger.Zero;
            while (true) {
                int ch = NextChar();
                switch (ch) {
                    case '0':
                        if (iVal != 0) {
                            // ignore leading 0's...
                            goto case '1';
                        }
                        break;
                    case '1':
                        bits++;
                        if (bits == 32) {
                            useBigInt = true;
                            bigInt = (BigInteger)iVal;
                        }

                        if (bits >= 32) {
                            bigInt = (bigInt << 1) | (ch - '0');
                        } else {
                            iVal = iVal << 1 | (ch - '0');
                        }
                        break;
                    case 'l':
                    case 'L':
                        MarkTokenEnd();

                        return new ConstantValueToken(useBigInt ? bigInt : (BigInteger)iVal);
                    default:
                        BufferBack();
                        MarkTokenEnd();

                        return new ConstantValueToken(useBigInt ? (object)bigInt : (object)iVal);
                }
            }
        }

        private Token ReadOctalNumber() {
            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                        break;

                    case 'l':
                    case 'L':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseBigInteger(GetTokenSubstring(2, TokenLength - 2), 8));

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenSubstring(2), 8));
                }
            }
        }

        private Token ReadHexNumber() {
            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                        break;

                    case 'l':
                    case 'L':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseBigInteger(GetTokenSubstring(2, TokenLength - 3), 16));

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenSubstring(2), 16));
                }
            }
        }

        private Token ReadFraction() {
            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        break;

                    case 'e':
                    case 'E':
                        return ReadExponent();

                    case 'j':
                    case 'J':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseImaginary(GetTokenString()));

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseFloat(GetTokenString()));
                }
            }
        }

        private Token ReadExponent() {
            int ch = NextChar();

            if (ch == '-' || ch == '+') {
                ch = NextChar();
            }

            while (true) {
                switch (ch) {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        ch = NextChar();
                        break;

                    case 'j':
                    case 'J':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseImaginary(GetTokenString()));

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseFloat(GetTokenString()));
                }
            }
        }

        private Token ReadName() {
            #region Generated Python Keyword Lookup

            // *** BEGIN GENERATED CODE ***
            // generated by function: keyword_lookup_generator from: generate_ops.py

            int ch;
            BufferBack();
            ch = NextChar();
            if (ch == 'i') {
                ch = NextChar();
                if (ch == 'n') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordInToken;
                    }
                } else if (ch == 'm') {
                    if (NextChar() == 'p' && NextChar() == 'o' && NextChar() == 'r' && NextChar() == 't' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordImportToken;
                    }
                } else if (ch == 's') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordIsToken;
                    }
                } else if (ch == 'f') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordIfToken;
                    }
                }
            } else if (ch == 'w') {
                ch = NextChar();
                if (ch == 'i') {
                    if (NextChar() == 't' && NextChar() == 'h' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordWithToken;
                    }
                } else if (ch == 'h') {
                    if (NextChar() == 'i' && NextChar() == 'l' && NextChar() == 'e' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordWhileToken;
                    }
                }
            } else if (ch == 't') {
                if (NextChar() == 'r' && NextChar() == 'y' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordTryToken;
                }
            } else if (ch == 'r') {
                ch = NextChar();
                if (ch == 'e') {
                    if (NextChar() == 't' && NextChar() == 'u' && NextChar() == 'r' && NextChar() == 'n' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordReturnToken;
                    }
                } else if (ch == 'a') {
                    if (NextChar() == 'i' && NextChar() == 's' && NextChar() == 'e' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordRaiseToken;
                    }
                }
            } else if (ch == 'p') {
                if (NextChar() == 'a' && NextChar() == 's' && NextChar() == 's' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordPassToken;
                }
            } else if (ch == 'g') {
                if (NextChar() == 'l' && NextChar() == 'o' && NextChar() == 'b' && NextChar() == 'a' && NextChar() == 'l' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordGlobalToken;
                }
            } else if (ch == 'f') {
                ch = NextChar();
                if (ch == 'r') {
                    if (NextChar() == 'o' && NextChar() == 'm' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordFromToken;
                    }
                } else if (ch == 'i') {
                    if (NextChar() == 'n' && NextChar() == 'a' && NextChar() == 'l' && NextChar() == 'l' && NextChar() == 'y' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordFinallyToken;
                    }
                } else if (ch == 'o') {
                    if (NextChar() == 'r' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordForToken;
                    }
                }
            } else if (ch == 'e') {
                ch = NextChar();
                if (ch == 'x') {
                    ch = NextChar();
                    if (ch == 'c') {
                        if (NextChar() == 'e' && NextChar() == 'p' && NextChar() == 't' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordExceptToken;
                        }
                    }
                } else if (ch == 'l') {
                    ch = NextChar();
                    if (ch == 's') {
                        if (NextChar() == 'e' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordElseToken;
                        }
                    } else if (ch == 'i') {
                        if (NextChar() == 'f' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordElseIfToken;
                        }
                    }
                }
            } else if (ch == 'd') {
                ch = NextChar();
                if (ch == 'e') {
                    ch = NextChar();
                    if (ch == 'l') {
                        if (!IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordDelToken;
                        }
                    } else if (ch == 'f') {
                        if (!IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordDefToken;
                        }
                    }
                }
            } else if (ch == 'c') {
                ch = NextChar();
                if (ch == 'l') {
                    if (NextChar() == 'a' && NextChar() == 's' && NextChar() == 's' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordClassToken;
                    }
                } else if (ch == 'o') {
                    if (NextChar() == 'n' && NextChar() == 't' && NextChar() == 'i' && NextChar() == 'n' && NextChar() == 'u' && NextChar() == 'e' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordContinueToken;
                    }
                }
            } else if (ch == 'b') {
                if (NextChar() == 'r' && NextChar() == 'e' && NextChar() == 'a' && NextChar() == 'k' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordBreakToken;
                }
            } else if (ch == 'a') {
                ch = NextChar();
                if (ch == 'n') {
                    if (NextChar() == 'd' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordAndToken;
                    }
                } else if (ch == 's') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordAsToken;
                    }
                    if (NextChar() == 's' && NextChar() == 'e' && NextChar() == 'r' && NextChar() == 't' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordAssertToken;
                    }
                }
            } else if (ch == 'y') {
                if (NextChar() == 'i' && NextChar() == 'e' && NextChar() == 'l' && NextChar() == 'd' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordYieldToken;
                }
            } else if (ch == 'o') {
                if (NextChar() == 'r' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordOrToken;
                }
            } else if (ch == 'n') {
                if (NextChar() == 'o' && NextChar() == 't' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordNotToken;
                }
            } else if (ch == 'N') {
                if (NextChar() == 'o' && NextChar() == 'n' && NextChar() == 'e' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.NoneToken;
                }
            } else if (ch == 'l') {
                if (NextChar() == 'a' && NextChar() == 'm' && NextChar() == 'b' && NextChar() == 'd' && NextChar() == 'a' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordLambdaToken;
                }
            }

            // *** END GENERATED CODE ***

            #endregion


            BufferBack();
            ch = NextChar();

            while (IsNamePart(ch)) {
                ch = NextChar();
            }
            BufferBack();

            MarkTokenEnd();
            NameToken token;
            if (!_names.TryGetValue(_currentName, out token)) {
                string name = GetTokenString();
                token = _names[name] = new NameToken(name);
            }
            
            return token;
        }

        /// <summary>
        /// Equality comparer that can compare strings to our current token w/o creating a new string first.
        /// </summary>
        class TokenEqualityComparer : IEqualityComparer<object> {
            private readonly Tokenizer _tokenizer;

            public TokenEqualityComparer(Tokenizer tokenizer) {
                _tokenizer = tokenizer;
            }

            #region IEqualityComparer<object> Members

            bool IEqualityComparer<object>.Equals(object x, object y) {
                if (x == _currentName) {
                    if (y == _currentName) {
                        return true;
                    }

                    return Equals((string)y);
                } else if (y == _currentName) {
                    return Equals((string)x);
                } else {
                    return (string)x == (string)y;
                }
            }

            public int GetHashCode(object obj) {
                int result = 5381;
                if (obj == _currentName) {
                    char[] buffer = _tokenizer._buffer;
                    int start = _tokenizer._start, end = _tokenizer._tokenEnd;                    
                    for (int i = start; i < end; i++) {
                        int c = buffer[i];
                        result = unchecked(((result << 5) + result) ^ c);
                    }
                } else {
                    string str = (string)obj;
                    for (int i = 0; i < str.Length; i++) {
                        int c = str[i];
                        result = unchecked(((result << 5) + result) ^ c);
                    }
                }
                return result;
            }

            private bool Equals(string value) {
                int len = _tokenizer._tokenEnd - _tokenizer._start;
                if (len != value.Length) {
                    return false;
                }

                var buffer = _tokenizer._buffer;
                for (int i = 0, bufferIndex = _tokenizer._start; i < value.Length; i++, bufferIndex++) {
                    if (value[i] != buffer[bufferIndex]) {
                        return false;
                    }
                }

                return true;
            }

            #endregion
        }

        public int GroupingLevel {
            get {
                return _state.ParenLevel + _state.BraceLevel + _state.BracketLevel;
            }
        }

        /// <summary>
        /// True if the last characters in the buffer are a backslash followed by a new line indicating
        /// that their is an incompletement statement which needs further input to complete.
        /// </summary>
        public bool EndContinues {
            get {
                return _endContinues;
            }
        }

        /// <summary>
        /// Returns whether the 
        /// </summary>
        private bool ReadNewline() {
            // Check whether we're currently scanning for inconsistent use of identation characters. If
            // we are we'll switch to using a slower version of this method with the extra checks embedded.
            if (IndentationInconsistencySeverity != Severity.Ignore)
                return ReadNewlineWithChecks();

            int spaces = 0;
            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case ' ': spaces += 1; break;
                    case '\t': spaces += 8 - (spaces % 8); break;
                    case '\f': spaces = 0; break;

                    case '#':
                        if (_verbatim) {
                            BufferBack();
                            MarkTokenEnd();
                            return true;
                        } else {
                            ch = ReadLine();
                            break;
                        }
                    default:
                        BufferBack();

                        if (GroupingLevel > 0) {
                            return false;
                        }

                        MarkTokenEnd();

                        // if there's a blank line then we don't want to mess w/ the
                        // indentation level - Python says that blank lines are ignored.
                        // And if we're the last blank line in a file we don't want to
                        // increase the new indentation level.
                        if (ch == EOF) {
                            if (spaces < _state.Indent[_state.IndentLevel]) {
                                if (_sourceUnit.Kind == SourceCodeKind.InteractiveCode ||
                                    _sourceUnit.Kind == SourceCodeKind.Statements) {
                                    SetIndent(spaces, null);
                                } else {
                                    DoDedent(spaces, _state.Indent[_state.IndentLevel]);
                                }
                            }
                        } else if (ch != '\n' && ch != '\r') {
                            SetIndent(spaces, null);
                        }

                        return true;
                }
            }
        }

        // This is another version of ReadNewline with nearly identical semantics. The difference is
        // that checks are made to see that indentation is used consistently. This logic is in a
        // duplicate method to avoid inflicting the overhead of the extra logic when we're not making
        // the checks.
        private bool ReadNewlineWithChecks() {
            // Keep track of the indentation format for the current line
            StringBuilder sb = new StringBuilder(80);

            int spaces = 0;
            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case ' ': spaces += 1; sb.Append(' '); break;
                    case '\t': spaces += 8 - (spaces % 8); sb.Append('\t'); break;
                    case '\f': spaces = 0; sb.Append('\f'); break;

                    case '#':
                        if (_verbatim) {
                            BufferBack();
                            MarkTokenEnd();
                            return true;
                        } else {
                            ch = ReadLine();
                            break;
                        }

                    default:
                        if (ReadEolnOpt(ch) > 0) {
                            _newLineLocations.Add(CurrentIndex);
                            spaces = 0;
                            sb.Length = 0;
                            break;
                        }

                        BufferBack();

                        if (GroupingLevel > 0) {
                            return false;
                        }

                        MarkTokenEnd();

                        // We've captured a line of significant identation (i.e. not pure whitespace).
                        // Check that any of this indentation that's in common with the current indent
                        // level is constructed in exactly the same way (i.e. has the same mix of spaces
                        // and tabs etc.).
                        CheckIndent(sb);

                        // if there's a blank line then we don't want to mess w/ the
                        // indentation level - Python says that blank lines are ignored.
                        // And if we're the last blank line in a file we don't want to
                        // increase the new indentation level.
                        if (ch == EOF) {
                            if (spaces < _state.Indent[_state.IndentLevel]) {
                                if (_sourceUnit.Kind == SourceCodeKind.InteractiveCode ||
                                    _sourceUnit.Kind == SourceCodeKind.Statements) {
                                    SetIndent(spaces, sb);
                                } else {
                                    DoDedent(spaces, _state.Indent[_state.IndentLevel]);
                                }
                            }
                        } else if (ch != '\n' && ch != '\r') {
                            SetIndent(spaces, sb);
                        }


                        return true;
                }
            }
        }

        private void CheckIndent(StringBuilder sb) {
            if (_state.Indent[_state.IndentLevel] > 0) {
                StringBuilder previousIndent = _state.IndentFormat[_state.IndentLevel];
                int checkLength = previousIndent.Length < sb.Length ? previousIndent.Length : sb.Length;
                for (int i = 0; i < checkLength; i++) {
                    if (sb[i] != previousIndent[i]) {

                        SourceLocation eoln_token_end = BufferTokenEnd;

                        // We've hit a difference in the way we're indenting, report it.
                        _errors.Add(_sourceUnit, Resources.InconsistentWhitespace,
                            new SourceSpan(eoln_token_end, eoln_token_end), // TODO: we can report better span - starting at the beginning of the line
                            ErrorCodes.TabError, _indentationInconsistencySeverity
                        );

                        // We only report problems once per module, so switch back to the fast algorithm.
                        _indentationInconsistencySeverity = Severity.Ignore;
                    }
                }
            }
        }

        private void SetIndent(int spaces, StringBuilder chars) {
            int current = _state.Indent[_state.IndentLevel];
            if (spaces == current) {
                return;
            } else if (spaces > current) {
                _state.Indent[++_state.IndentLevel] = spaces;
                if (_state.IndentFormat != null)
                    _state.IndentFormat[_state.IndentLevel] = chars;
                _state.PendingDedents = -1;
                return;
            } else {
                current = DoDedent(spaces, current);

                if (spaces != current) {
                    ReportSyntaxError(
                        new SourceSpan(new SourceLocation(_tokenEndIndex, IndexToLocation(_tokenEndIndex).Line, IndexToLocation(_tokenEndIndex).Column - 1),
                            BufferTokenEnd),
                        Resources.IndentationMismatch, ErrorCodes.IndentationError);
                }
            }
        }

        private int DoDedent(int spaces, int current) {
            while (spaces < current) {
                _state.IndentLevel -= 1;
                _state.PendingDedents += 1;
                current = _state.Indent[_state.IndentLevel];
            }
            return current;
        }

        private object ParseInteger(string s, int radix) {
            try {
                return LiteralParser.ParseInteger(s, radix);
            } catch (ArgumentException e) {
                ReportSyntaxError(BufferTokenSpan, e.Message, ErrorCodes.SyntaxError);
            }
            return ScriptingRuntimeHelpers.Int32ToObject(0);
        }

        private object ParseFloat(string s) {
            try {
                return LiteralParser.ParseFloat(s);
            } catch (Exception e) {
                ReportSyntaxError(BufferTokenSpan, e.Message, ErrorCodes.SyntaxError);
                return 0.0;
            }
        }

        internal static bool TryGetEncoding(Encoding defaultEncoding, string line, ref Encoding enc, out string encName) {
            // PEP 0263 defines the following regex for the encoding line
            // coding[:=]\s*([-\w.]+)
            encName = null;
            int startIndex = 0;
            if (line.Length < 10) return false;
            while (startIndex < line.Length) {
                if (!Char.IsWhiteSpace (line[startIndex])) break;
                
                startIndex++;
            }

            if (startIndex == line.Length || line[startIndex] != '#') return false;

            // we have magic comment line
            int codingIndex;
            if ((codingIndex = line.IndexOf("coding")) == -1) return false;
            if (line.Length <= (codingIndex + 6)) return false;
            // [:=]
            if (line[codingIndex + 6] != ':' && line[codingIndex + 6] != '=') return false;

            // it contains coding: or coding=
            int encodingStart = codingIndex + 7;
            while (encodingStart < line.Length) {
                if (!Char.IsWhiteSpace(line[encodingStart])) break;

                encodingStart++;
            }

            // line is coding: [all white space]
            if (encodingStart == line.Length) return false;

            int encodingEnd = encodingStart;
            // ([-\w.]+)
            while (encodingEnd < line.Length) {
                if (line[encodingEnd] != '-' && line[encodingEnd] != '.' && !Char.IsLetterOrDigit(line[encodingEnd])) break;

                encodingEnd++;
            }

            // get the encoding string name
            encName = line.Substring(encodingStart, encodingEnd - encodingStart);

            // and we have the magic ending as well...
            if (StringOps.TryGetEncoding(encName, out enc)) {
#if FEATURE_ENCODING
                enc.DecoderFallback = new NonStrictDecoderFallback();
#endif
                return true;
            }
            return false;
        }

        private void ReportSyntaxError(SourceSpan span, string message, int errorCode) {
            _errors.Add(_sourceUnit, message, span, errorCode, Severity.FatalError);
        }

        private void ReportSyntaxError(IndexSpan span, string message, int errorCode) {
            _errors.Add(_sourceUnit, message, new SourceSpan(IndexToLocation(span.Start), IndexToLocation(span.End)), errorCode, Severity.FatalError);
        }


        [Conditional("DUMP_TOKENS")]
        private void DumpBeginningOfUnit() {
            Console.WriteLine("--- Source unit: '{0}' ---", _sourceUnit.Path);
        }

        [Conditional("DUMP_TOKENS")]
        private static void DumpToken(Token token) {
            Console.WriteLine("{0} `{1}`", token.Kind, token.Image.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"));
        }

        public int[] GetLineLocations() {
            return _newLineLocations.ToArray();
        }

        [Serializable]
        class IncompleteString : IEquatable<IncompleteString> {
            public readonly bool IsRaw, IsUnicode, IsTripleQuoted, IsSingleTickQuote;

            public IncompleteString(bool isSingleTickQuote, bool isRaw, bool isUnicode, bool isTriple) {
                IsRaw = isRaw;
                IsUnicode = isUnicode;
                IsTripleQuoted = isTriple;
                IsSingleTickQuote = isSingleTickQuote;
            }

            public override bool Equals(object obj) {
                IncompleteString oth = obj as IncompleteString;
                if (oth != null) {
                    return Equals(oth);
                }
                return false;
            }

            public override int GetHashCode() {
                return (IsRaw ? 0x01 : 0) |
                    (IsUnicode ? 0x02 : 0) |
                    (IsTripleQuoted ? 0x04 : 0) |
                    (IsSingleTickQuote ? 0x08 : 0);
            }

            public static bool operator ==(IncompleteString left, IncompleteString right) {
                if ((object)left == null) return (object)right == null;

                return left.Equals(right);
            }

            public static bool operator !=(IncompleteString left, IncompleteString right) {
                return !(left == right);
            }

            #region IEquatable<State> Members

            public bool Equals(IncompleteString other) {
                if (other == null) {
                    return false;
                }

                return IsRaw == other.IsRaw &&
                    IsUnicode == other.IsUnicode &&
                    IsTripleQuoted== other.IsTripleQuoted &&
                    IsSingleTickQuote == other.IsSingleTickQuote;
            }

            #endregion
        }

        [Serializable]
        struct State : IEquatable<State> {
            // indentation state
            public int[] Indent;
            public int IndentLevel;
            public int PendingDedents;
            public bool LastNewLine;        // true if the last token we emitted was a new line.
            public IncompleteString IncompleteString;

            // Indentation state used only when we're reporting on inconsistent identation format.
            public StringBuilder[] IndentFormat;

            // grouping state
            public int ParenLevel, BraceLevel, BracketLevel;

            public State(State state) {
                Indent = (int[])state.Indent.Clone();
                LastNewLine = state.LastNewLine;
                BracketLevel = state.BraceLevel;
                ParenLevel = state.ParenLevel;
                BraceLevel = state.BraceLevel;
                PendingDedents = state.PendingDedents;
                IndentLevel = state.IndentLevel;
                IndentFormat = (state.IndentFormat != null) ? (StringBuilder[])state.IndentFormat.Clone() : null;
                IncompleteString = state.IncompleteString;
            }

            public State(object dummy) {
                Indent = new int[MaxIndent]; // TODO
                LastNewLine = false;
                BracketLevel = ParenLevel = BraceLevel = PendingDedents = IndentLevel = 0;
                IndentFormat = null;
                IncompleteString = null;
            }

            public override bool Equals(object obj) {
                if (obj is State) {
                    State other = (State)obj;
                    return other == this;
                } else {
                    return false;
                }
            }

            public override int GetHashCode() {
                return base.GetHashCode();
            }

            public static bool operator ==(State left, State right) {
                return left.BraceLevel == right.BraceLevel &&
                       left.BracketLevel == right.BracketLevel &&
                       left.IndentLevel == right.IndentLevel &&
                       left.ParenLevel == right.ParenLevel &&
                       left.PendingDedents == right.PendingDedents &&
                       left.LastNewLine == right.LastNewLine &&
                       left.IncompleteString == right.IncompleteString;
            }

            public static bool operator !=(State left, State right) {
                return !(left == right);
            }

            #region IEquatable<State> Members

            public bool Equals(State other) {
                return this.Equals(other);
            }

            #endregion
        }

        #region Buffer Access
        
        private string GetTokenSubstring(int offset) {
            return GetTokenSubstring(offset, _tokenEnd - _start - offset);
        }

        private string GetTokenSubstring(int offset, int length) {
            Debug.Assert(_tokenEnd != -1, "Token end not marked");
            Debug.Assert(offset >= 0 && offset <= _tokenEnd - _start && length >= 0 && length <= _tokenEnd - _start - offset);

            return new String(_buffer, _start + offset, length);
        }

        [Conditional("DEBUG")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private void CheckInvariants() {
            Debug.Assert(_buffer.Length >= 1);

            // _start == _end when discarding token and at beginning, when == 0
            Debug.Assert(_start >= 0 && _start <= _end); 

            Debug.Assert(_end >= 0 && _end <= _buffer.Length);
            
            // position beyond _end means we are reading EOFs:
            Debug.Assert(_position >= _start);
            Debug.Assert(_tokenEnd >= -1 && _tokenEnd <= _end);
        }

        private int Peek() {
            if (_position >= _end) {
                RefillBuffer();
                
                // eof:
                if (_position >= _end) {
                    return EOF;
                }
            }

            Debug.Assert(_position < _end);
            
            return _buffer[_position];
        }

        private int ReadLine() {
            int ch;
            do { ch = NextChar(); } while (ch != EOF && !IsEoln(ch));
            BufferBack();
            return ch;
        }

        private void MarkTokenEnd() {
            CheckInvariants();

            _tokenEnd = System.Math.Min(_position, _end);
            int token_length = _tokenEnd - _start;

            _tokenEndIndex = _tokenStartIndex + token_length;

            DumpToken();

            CheckInvariants();
        }

        [Conditional("DUMP_TOKENS")]
        private void DumpToken() {
            Console.WriteLine("--> `{0}` {1}", GetTokenString().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"), TokenSpan);
        }

        private void BufferBack() {
            SeekRelative(-1);
        }

        internal string GetTokenString() {
            return new String(_buffer, _start, _tokenEnd - _start);
        }

        private int TokenLength {
            get {
                return _tokenEnd - _start;
            }
        }

        private void SeekRelative(int disp) {
            CheckInvariants();
            Debug.Assert(disp >= _start - _position);
            // no upper limit, we can seek beyond end in which case we are reading EOFs

            _position += disp;

            CheckInvariants();
        }

        private SourceLocation BufferTokenEnd {
            get {
                return IndexToLocation(_tokenEndIndex);
            }
        }

        private IndexSpan BufferTokenSpan {
            get {
                return new IndexSpan(_tokenStartIndex, _tokenEndIndex - _tokenStartIndex);
            }
        }

        private bool NextChar(int ch) { 
            CheckInvariants();
            if (Peek() == ch) {
                _position++;
                CheckInvariants();
                return true;
            } else {
                return false;
            }
        }

        private int NextChar() {
            int result = Peek();
            _position++;
            return result;
        }

        private bool AtBeginning {
            get {
                return _position == 0 && !_bufferResized;
            }
        }

        private int CurrentIndex {
            get {
                return _tokenStartIndex + Math.Min(_position, _end) - _start;
            }
        }

        private void DiscardToken() {
            CheckInvariants();

            // no token marked => mark it now:
            if (_tokenEnd == -1) MarkTokenEnd();

            // the current token's end is the next token's start:
            _start = _tokenEnd;
            _tokenStartIndex = _tokenEndIndex;
            _tokenEnd = -1;
#if DEBUG
            _tokenEndIndex = -1;
#endif
            CheckInvariants();
        }

        private int ReadEolnOpt(int current) {
            if (current == '\n') return 1;

            if (current == '\r' && _multiEolns) {

                if (Peek() == '\n') {
                    SeekRelative(+1);
                    return 2;
                }

                return 1;
            }

            return 0;
        }

        private bool IsEoln(int current) {
            if (current == '\n') return true;

            if (current == '\r' && _multiEolns) {
                if (Peek() == '\n') {
                    return true;
                }

                return true;
            }

            return false;
        }

        private void RefillBuffer() {
            if (_end == _buffer.Length) {
                int new_size = System.Math.Max(System.Math.Max((_end - _start) * 2, _buffer.Length), _position);
                ResizeInternal(ref _buffer, new_size, _start, _end - _start);
                _end -= _start;
                _position -= _start;
                _start = 0;
                _bufferResized = true;
            }

            // make the buffer full:
            int count = _reader.Read(_buffer, _end, _buffer.Length - _end);
            _end += count;

            ClearInvalidChars();
        }

        /// <summary>
        /// Resizes an array to a speficied new size and copies a portion of the original array into its beginning.
        /// </summary>
        private static void ResizeInternal(ref char[] array, int newSize, int start, int count) {
            Debug.Assert(array != null && newSize > 0 && count >= 0 && newSize >= count && start >= 0);

            char[] result = (newSize != array.Length) ? new char[newSize] : array;

            Buffer.BlockCopy(array, start * sizeof(char), result, 0, count * sizeof(char));

            array = result;
        }

        [Conditional("DEBUG")]
        private void ClearInvalidChars() {
            for (int i = 0; i < _start; i++) _buffer[i] = '\0';
            for (int i = _end; i < _buffer.Length; i++) _buffer[i] = '\0';
        }

        #endregion
    }
}
