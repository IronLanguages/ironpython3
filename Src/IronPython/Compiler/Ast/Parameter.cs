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
    };

    /// <summary>
    /// Parameter base class
    /// </summary>
    public class Parameter : Node {
        /// <summary>
        /// Position of the parameter: 0-based index
        /// </summary>
        private readonly string _name;
        protected readonly ParameterKind _kind;
        protected Expression _defaultValue;

        private PythonVariable _variable;
        private MSAst.ParameterExpression _parameter;

        public Parameter(string name)
            : this(name, ParameterKind.Normal) {
        }

        public Parameter(string name, ParameterKind kind) {
            _name = name;
            _kind = kind;
        }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name {
            get { return _name; }
        }

        public Expression Annotation { get; set; }

        public Expression DefaultValue {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public bool IsList {
            get {
                return _kind == ParameterKind.List;
            }
        }

        public bool IsDictionary {
            get {
                return _kind == ParameterKind.Dictionary;
            }
        }

        internal ParameterKind Kind {
            get {
                return _kind;
            }
        }

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
