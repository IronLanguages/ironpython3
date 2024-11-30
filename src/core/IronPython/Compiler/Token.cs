// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using Microsoft.Scripting;

namespace IronPython.Compiler {
    internal readonly struct TokenWithSpan {
        public TokenWithSpan(Token token, IndexSpan span) {
            Token = token;
            Span = span;
        }

        public IndexSpan Span { get; }

        public Token Token { get; }

    }

    /// <summary>
    /// Summary description for Token.
    /// </summary>
    public abstract class Token {
        private readonly TokenKind _kind;

        protected Token(TokenKind kind) {
            _kind = kind;
        }

        public TokenKind Kind {
            get { return _kind; }
        }

        public virtual object Value {
            get {
                throw new NotSupportedException(IronPython.Resources.TokenHasNoValue);
            }
        }

        public override string ToString() {
            return base.ToString() + "(" + _kind + ")";
        }

        public abstract String Image {
            get;
        }
    }

    public class ErrorToken : Token {
        private readonly String _message;

        public ErrorToken(String message)
            : base(TokenKind.Error) {
            _message = message;
        }

        public String Message {
            get { return _message; }
        }

        public override String Image {
            get { return _message; }
        }

        public override object Value {
            get { return _message; }
        }
    }

    public class IncompleteStringErrorToken : ErrorToken {
        private readonly string _value;

        public IncompleteStringErrorToken(string message, string value)
            : base(message) {
            _value = value;
        }

        public override string Image {
            get {
                return _value;
            }
        }

        public override object Value {
            get {
                return _value;
            }
        }
    }

    public class ConstantValueToken : Token {
        private readonly object _value;

        public ConstantValueToken(object value)
            : base(TokenKind.Constant) {
            _value = value;
        }

        public object Constant => _value;

        public override object Value => _value;

        public override String Image {
            get {
                return _value == null ? "None" : _value.ToString();
            }
        }
    }

    internal sealed class FormattedStringToken : Token {
        private readonly string _value;

        public FormattedStringToken(string value, bool isRaw)
            : base(TokenKind.Constant) {
            _value = value;
            this.isRaw = isRaw;
        }

        public override string Image => _value;

        public override object Value => _value;

        public bool isRaw { get; }
    }

    public sealed class CommentToken : Token {
        private readonly string _comment;

        public CommentToken(string comment)
            : base(TokenKind.Comment) {
            _comment = comment;
        }

        public string Comment => _comment;

        public override string Image => _comment;

        public override object Value => _comment;
    }

    public class NameToken : Token {
        private readonly string _name;

        public NameToken(string name)
            : base(TokenKind.Name) {
            _name = name;
        }

        public string Name => _name;

        public override object Value => _name;

        public override String Image => _name;
    }

    public class OperatorToken : Token {
        private readonly int _precedence;
        private readonly string _image;

        public OperatorToken(TokenKind kind, string image, int precedence)
            : base(kind) {
            _image = image;
            _precedence = precedence;
        }

        public int Precedence {
            get { return _precedence; }
        }

        public override object Value {
            get { return _image; }
        }

        public override String Image {
            get { return _image; }
        }
    }

    public class SymbolToken : Token {
        private readonly string _image;

        public SymbolToken(TokenKind kind, String image)
            : base(kind) {
            _image = image;
        }

        public String Symbol {
            get { return _image; }
        }

        public override object Value {
            get { return _image; }
        }

        public override String Image {
            get { return _image; }
        }
    }
}
