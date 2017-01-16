// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;


namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public enum ParameterKind {
        Normal,
        List,
        Dictionary,
        KeywordOnly,
    };

    /// <summary>
    /// Parameter base class
    /// </summary>
    public class Parameter : Node {
        protected readonly ParameterKind _kind;
        protected Expression _defaultValue;

        private PythonVariable _variable;
        private MSAst.ParameterExpression _parameter;

        public Parameter(string name)
            : this(name, ParameterKind.Normal) {
        }

        public Parameter(string name, ParameterKind kind) {
            Name = name;
            _kind = kind;
        }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; }

        public Expression Annotation { get; set; }

        public Expression DefaultValue {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public bool IsList => _kind == ParameterKind.List;

        public bool IsDictionary => _kind == ParameterKind.Dictionary;

        internal bool IsKeywordOnly => _kind == ParameterKind.KeywordOnly;

        internal ParameterKind Kind => _kind;

        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }

        internal MSAst.Expression FinishBind(bool needsLocalsDictionary) {
            if (_variable.AccessedInNestedScope || needsLocalsDictionary) {
                _parameter = Expression.Parameter(typeof(object), Name);
                var cell = Ast.Parameter(typeof(ClosureCell), Name);
                return new ClosureExpression(_variable, cell, _parameter);
            } else {
                return _parameter = Ast.Parameter(typeof(object), Name);
            }
        }

        internal MSAst.ParameterExpression ParameterExpression {
            get {
                return _parameter;
            }
        }

        internal virtual void Init(List<MSAst.Expression> init) {
            // Regular parameter has no initialization
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _defaultValue?.Walk(walker);
                Annotation?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
