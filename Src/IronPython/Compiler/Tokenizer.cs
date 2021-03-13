// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//#define DUMP_TOKENS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

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
        private bool _endContinues;
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
            ContractUtils.RequiresNotNull(errorSink, nameof(errorSink));
            ContractUtils.RequiresNotNull(options, nameof(options));

            _errors = errorSink;
            _verbatim = options.Verbatim;
            _state = new State(null);
            _dontImplyDedent = options.DontImplyDedent;
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
                ContractUtils.RequiresNotNull(value, nameof(value));
                _errors = value;
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
            ContractUtils.RequiresNotNull(sourceUnit, nameof(sourceUnit));

            Initialize(null, sourceUnit.GetReader(), sourceUnit, SourceLocation.MinValue, DefaultBufferCapacity);
        }

        public override void Initialize(object state, TextReader reader, SourceUnit sourceUnit, SourceLocation initialLocation) {
            Initialize(state, reader, sourceUnit, initialLocation, DefaultBufferCapacity);
        }

        public void Initialize(object state, TextReader reader, SourceUnit sourceUnit, SourceLocation initialLocation, int bufferCapacity) {
            Initialize(state, reader, sourceUnit, initialLocation, bufferCapacity, null);
        }

        public void Initialize(object state, TextReader reader, SourceUnit sourceUnit, SourceLocation initialLocation, int bufferCapacity, PythonCompilerOptions compilerOptions) {
            ContractUtils.RequiresNotNull(reader, nameof(reader));

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

            Token token = GetNextToken();
            var sourceSpan = new SourceSpan(IndexToLocation(TokenSpan.Start), IndexToLocation(TokenSpan.End));
            var category = default(TokenCategory);
            var trigger = default(TokenTriggers);

            switch (token.Kind) {
                case TokenKind.EndOfFile:
                    category = TokenCategory.EndOfStream;
                    break;

                case TokenKind.Comment:
                    category = TokenCategory.Comment;
                    break;

                case TokenKind.Name:
                    category = TokenCategory.Identifier;
                    break;

                case TokenKind.Error:
                    if (token is IncompleteStringErrorToken) {
                        category = TokenCategory.StringLiteral;
                    } else {
                        category = TokenCategory.Error;
                    }
                    break;

                case TokenKind.Constant:
                    category = token.Value switch {
                        string => TokenCategory.StringLiteral,
                        bool or null => TokenCategory.Keyword,
                        _ => TokenCategory.NumericLiteral,
                    };
                    break;

                case TokenKind.LeftParenthesis:
                    category = TokenCategory.Grouping;
                    trigger = TokenTriggers.MatchBraces | TokenTriggers.ParameterStart;
                    break;

                case TokenKind.RightParenthesis:
                    category = TokenCategory.Grouping;
                    trigger = TokenTriggers.MatchBraces | TokenTriggers.ParameterEnd;
                    break;

                case TokenKind.LeftBracket:
                case TokenKind.LeftBrace:
                case TokenKind.RightBracket:
                case TokenKind.RightBrace:
                    category = TokenCategory.Grouping;
                    trigger = TokenTriggers.MatchBraces;
                    break;

                case TokenKind.Colon:
                    category = TokenCategory.Delimiter;
                    break;

                case TokenKind.Semicolon:
                    category = TokenCategory.Delimiter;
                    break;

                case TokenKind.Comma:
                    category = TokenCategory.Delimiter;
                    trigger = TokenTriggers.ParameterNext;
                    break;

                case TokenKind.Dot:
                    category = TokenCategory.Operator;
                    trigger = TokenTriggers.MemberSelect;
                    break;

                case TokenKind.NewLine:
                    category = TokenCategory.WhiteSpace;
                    break;

                default:
                    if (token.Kind >= TokenKind.FirstKeyword && token.Kind <= TokenKind.LastKeyword) {
                        category = TokenCategory.Keyword;
                        break;
                    }

                    category = TokenCategory.Operator;
                    break;
            }

            return new TokenInfo(sourceSpan, category, trigger);
        }

        internal bool TryGetTokenString(int len, out string tokenString) {
            if (len != TokenLength) {
                tokenString = null;
                return false;
            }
            tokenString = GetTokenString();
            return true;
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
                        return ReadNameOrString();

                    case 'r':
                    case 'R':
                        _state.LastNewLine = false;
                        return ReadNameOrRawStringOrBytes();
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

                        if (NextChar('.')) {
                            if (NextChar('.')) {
                                MarkTokenEnd();
                                return new ConstantValueToken(PythonOps.Ellipsis);
                            } else {
                                BufferBack();
                            }
                        }

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

        private Token ReadNameOrString() {
            if (NextChar('\"')) return ReadString('\"', false, true, false);
            if (NextChar('\'')) return ReadString('\'', false, true, false);
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

        private Token ReadNameOrRawStringOrBytes() {
            if (NextChar('\"')) return ReadString('\"', true, false, false);
            if (NextChar('\'')) return ReadString('\'', true, false, false);
            if (NextChar('b') || NextChar('B')) {
                if (NextChar('\"')) return ReadString('\"', true, false, true);
                if (NextChar('\'')) return ReadString('\'', true, false, true);
                BufferBack();
            }
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
            try {
                if (!isBytes) {
                    var contents = LiteralParser.ParseString(_buffer, start, length, isRaw, !isRaw, !_disableLineFeedLineSeparator);
                    return new ConstantValueToken(contents);
                } else {
                    List<byte> data = LiteralParser.ParseBytes<char>(_buffer.AsSpan(start, length), isRaw, isAscii: true, !_disableLineFeedLineSeparator);
                    if (data.Count == 0) {
                        return new ConstantValueToken(Bytes.Empty);
                    }

                    return new ConstantValueToken(new Bytes(data));
                }
            } catch (DecoderFallbackException ex) {
                var msg = $"(unicode error) {PythonOps.ToString(PythonExceptions.GetPythonException(ex))}";
                ReportSyntaxError(BufferTokenSpan, msg, ErrorCodes.NoCaret);
                return new ErrorToken(msg);
            } catch (ValueErrorException ex) {
                var msg = $"(value error) {ex.Message}";
                ReportSyntaxError(BufferTokenSpan, msg, ErrorCodes.NoCaret);
                return new ErrorToken(msg);
            } catch (SyntaxErrorException ex) {
                var msg = ex.Message;
                ReportSyntaxError(BufferTokenSpan, msg, ErrorCodes.SyntaxError);
                return new ErrorToken(msg);
            }
        }

        private void UnexpectedEndOfString(bool isTriple, bool isIncomplete) {
            string message = isTriple ? Resources.EofInTripleQuotedString : Resources.EolInSingleQuotedString;
            int error = isIncomplete ? ErrorCodes.SyntaxError | ErrorCodes.IncompleteToken : ErrorCodes.SyntaxError;

            ReportSyntaxError(BufferTokenSpan, message, error);
        }

        private Token ReadNumber(int start) {
            bool isPrefix0 = false;
            if (start == '0') {
                if (NextChar('x') || NextChar('X')) {
                    return ReadHexNumber();
                }
                if (NextChar('b') || NextChar('B')) {
                    return ReadBinaryNumber();
                }
                if (NextChar('o') || NextChar('O')) {
                    return ReadOctalNumber();
                }
                isPrefix0 = true;

                while (NextChar('0')) { } // skip leading zeroes
            }

            bool isFirstChar = true;
            while (true) {
                int ch = NextChar();

                switch (ch) {
                    case '.':
                        return ReadFraction();

                    case 'e':
                    case 'E':
                        var exp = ReadExponent();
                        if (exp != null) return exp;

                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenString(), 10));

                    case 'j':
                    case 'J':
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(LiteralParser.ParseImaginary(GetTokenString()));

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
                        if (isPrefix0 && !isFirstChar) {
                            ReportSyntaxError(BufferTokenSpan, "invalid token", ErrorCodes.SyntaxError);
                        }

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenString(), 10));
                }
                isFirstChar = false;
            }
        }

        private Token ReadBinaryNumber() {
            int bits = 0;
            int iVal = 0;
            bool useBigInt = false;
            BigInteger bigInt = BigInteger.Zero;
            bool first = true;
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
                    default:
                        BufferBack();
                        MarkTokenEnd();

                        if (first) {
                            ReportSyntaxError(
                            new SourceSpan(new SourceLocation(_tokenEndIndex, IndexToLocation(_tokenEndIndex).Line, IndexToLocation(_tokenEndIndex).Column - 1),
                            BufferTokenEnd),
                            Resources.InvalidToken, ErrorCodes.SyntaxError);
                        }

                        return new ConstantValueToken(useBigInt ? bigInt : (object)iVal);
                }
                first = false;
            }
        }

        private Token ReadOctalNumber() {
            bool first = true;
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

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        if (first) {
                            ReportSyntaxError(
                            new SourceSpan(new SourceLocation(_tokenEndIndex, IndexToLocation(_tokenEndIndex).Line, IndexToLocation(_tokenEndIndex).Column - 1),
                            BufferTokenEnd),
                            Resources.InvalidToken, ErrorCodes.SyntaxError);
                        }

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenSubstring(2), 8));
                }
                first = false;
            }
        }

        private Token ReadHexNumber() {
            bool first = true;
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

                    default:
                        BufferBack();
                        MarkTokenEnd();

                        if (first) {
                            ReportSyntaxError(
                            new SourceSpan(new SourceLocation(_tokenEndIndex, IndexToLocation(_tokenEndIndex).Line, IndexToLocation(_tokenEndIndex).Column - 1),
                            BufferTokenEnd),
                            Resources.InvalidToken, ErrorCodes.SyntaxError);
                        }

                        // TODO: parse in place
                        return new ConstantValueToken(ParseInteger(GetTokenSubstring(2), 16));
                }
                first = false;
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
                        var exp = ReadExponent();
                        if (exp != null) return exp;

                        BufferBack();
                        MarkTokenEnd();

                        // TODO: parse in place
                        return new ConstantValueToken(ParseFloat(GetTokenString()));

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
            var idx = CurrentIndex;
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
                        if (idx == CurrentIndex) {
                            return null;
                        }
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
            if (ch == 'F') {
                if (NextChar() == 'a' && NextChar() == 'l' && NextChar() == 's' && NextChar() == 'e' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.FalseToken;
                }
            } else if (ch == 'N') {
                if (NextChar() == 'o' && NextChar() == 'n' && NextChar() == 'e' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.NoneToken;
                }
            } else if (ch == 'T') {
                if (NextChar() == 'r' && NextChar() == 'u' && NextChar() == 'e' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.TrueToken;
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
                    ch = NextChar();
                    if (ch == 's') {
                        if (NextChar() == 'e' && NextChar() == 'r' && NextChar() == 't' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordAssertToken;
                        }
                    } else if (ch == 'y') {
                        if (NextChar() == 'n' && NextChar() == 'c' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordAsyncToken;
                        }
                    }
                }
            } else if (ch == 'b') {
                if (NextChar() == 'r' && NextChar() == 'e' && NextChar() == 'a' && NextChar() == 'k' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordBreakToken;
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
            } else if (ch == 'd') {
                ch = NextChar();
                if (ch == 'e') {
                    ch = NextChar();
                    if (ch == 'f') {
                        if (!IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordDefToken;
                        }
                    } else if (ch == 'l') {
                        if (!IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordDelToken;
                        }
                    }
                }
            } else if (ch == 'e') {
                ch = NextChar();
                if (ch == 'l') {
                    ch = NextChar();
                    if (ch == 'i') {
                        if (NextChar() == 'f' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordElseIfToken;
                        }
                    } else if (ch == 's') {
                        if (NextChar() == 'e' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordElseToken;
                        }
                    }
                } else if (ch == 'x') {
                    if (NextChar() == 'c' && NextChar() == 'e' && NextChar() == 'p' && NextChar() == 't' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordExceptToken;
                    }
                }
            } else if (ch == 'f') {
                ch = NextChar();
                if (ch == 'i') {
                    if (NextChar() == 'n' && NextChar() == 'a' && NextChar() == 'l' && NextChar() == 'l' && NextChar() == 'y' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordFinallyToken;
                    }
                } else if (ch == 'o') {
                    if (NextChar() == 'r' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordForToken;
                    }
                } else if (ch == 'r') {
                    if (NextChar() == 'o' && NextChar() == 'm' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordFromToken;
                    }
                }
            } else if (ch == 'g') {
                if (NextChar() == 'l' && NextChar() == 'o' && NextChar() == 'b' && NextChar() == 'a' && NextChar() == 'l' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordGlobalToken;
                }
            } else if (ch == 'i') {
                ch = NextChar();
                if (ch == 'f') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordIfToken;
                    }
                } else if (ch == 'm') {
                    if (NextChar() == 'p' && NextChar() == 'o' && NextChar() == 'r' && NextChar() == 't' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordImportToken;
                    }
                } else if (ch == 'n') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordInToken;
                    }
                } else if (ch == 's') {
                    if (!IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordIsToken;
                    }
                }
            } else if (ch == 'l') {
                if (NextChar() == 'a' && NextChar() == 'm' && NextChar() == 'b' && NextChar() == 'd' && NextChar() == 'a' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordLambdaToken;
                }
            } else if (ch == 'n') {
                ch = NextChar();
                if (ch == 'o') {
                    ch = NextChar();
                    if (ch == 'n') {
                        if (NextChar() == 'l' && NextChar() == 'o' && NextChar() == 'c' && NextChar() == 'a' && NextChar() == 'l' && !IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordNonlocalToken;
                        }
                    } else if (ch == 't') {
                        if (!IsNamePart(Peek())) {
                            MarkTokenEnd();
                            return Tokens.KeywordNotToken;
                        }
                    }
                }
            } else if (ch == 'o') {
                if (NextChar() == 'r' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordOrToken;
                }
            } else if (ch == 'p') {
                if (NextChar() == 'a' && NextChar() == 's' && NextChar() == 's' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordPassToken;
                }
            } else if (ch == 'r') {
                ch = NextChar();
                if (ch == 'a') {
                    if (NextChar() == 'i' && NextChar() == 's' && NextChar() == 'e' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordRaiseToken;
                    }
                } else if (ch == 'e') {
                    if (NextChar() == 't' && NextChar() == 'u' && NextChar() == 'r' && NextChar() == 'n' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordReturnToken;
                    }
                }
            } else if (ch == 't') {
                if (NextChar() == 'r' && NextChar() == 'y' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordTryToken;
                }
            } else if (ch == 'w') {
                ch = NextChar();
                if (ch == 'h') {
                    if (NextChar() == 'i' && NextChar() == 'l' && NextChar() == 'e' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordWhileToken;
                    }
                } else if (ch == 'i') {
                    if (NextChar() == 't' && NextChar() == 'h' && !IsNamePart(Peek())) {
                        MarkTokenEnd();
                        return Tokens.KeywordWithToken;
                    }
                }
            } else if (ch == 'y') {
                if (NextChar() == 'i' && NextChar() == 'e' && NextChar() == 'l' && NextChar() == 'd' && !IsNamePart(Peek())) {
                    MarkTokenEnd();
                    return Tokens.KeywordYieldToken;
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
        private class TokenEqualityComparer : IEqualityComparer<object> {
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

        internal static string GetEncodingNameFromComment(string line) {
            // PEP 0263 defines the following regex for the encoding line (https://www.python.org/dev/peps/pep-0263/#defining-the-encoding):
            // ^[ \t\f]*#.*?coding[:=][ \t]*([-_.a-zA-Z0-9]+)
            if (line == null || line.Length < 10) return null;

            int startIndex = 0;
            while (startIndex < line.Length) {
                char c = line[startIndex];
                if (c != ' ' && c != '\t' && c != '\f') break;

                startIndex++;
            }

            if (startIndex == line.Length || line[startIndex] != '#') return null;

            // we have magic comment line
            int codingIndex;
            if ((codingIndex = line.IndexOf("coding")) == -1) return null;
            if (line.Length <= (codingIndex + 6)) return null;
            // [:=]
            if (line[codingIndex + 6] != ':' && line[codingIndex + 6] != '=') return null;

            // it contains coding: or coding=
            int encodingStart = codingIndex + 7;
            while (encodingStart < line.Length) {
                char c = line[encodingStart];
                if (c != ' ' && c != '\t') break;

                encodingStart++;
            }

            // line is coding: [all white space]
            if (encodingStart == line.Length) return null;

            int encodingEnd = encodingStart;
            // ([-_.a-zA-Z0-9]+)
            while (encodingEnd < line.Length) {
                char c = line[encodingEnd];
                if (c == '-' || c == '_' || c == '.' || ('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z') || ('0' <= c && c <= '9')) {
                    encodingEnd++;
                } else {
                    break;
                }
            }

            if (encodingEnd == encodingStart) return null;

            // get the encoding string name
            return line.Substring(encodingStart, encodingEnd - encodingStart);
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
        private class IncompleteString : IEquatable<IncompleteString> {
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
        private struct State : IEquatable<State> {
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
                int new_size = Math.Max(System.Math.Max((_end - _start) * 2, _buffer.Length), _position);
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
