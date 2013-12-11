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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif


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
                if (_defaultValue != null) {
                    _defaultValue.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }

    public class SublistParameter : Parameter {
        private readonly TupleExpression _tuple;

        public SublistParameter(int position, TupleExpression tuple)
            : base("." + position, ParameterKind.Normal) {
            _tuple = tuple;
        }

        public TupleExpression Tuple {
            get { return _tuple; }
        }

        internal override void Init(List<MSAst.Expression> init) {
            init.Add(
                _tuple.TransformSet(Span, ParameterExpression, PythonOperationKind.None)
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_tuple != null) {
                    _tuple.Walk(walker);
                }
                if (_defaultValue != null) {
                    _defaultValue.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
