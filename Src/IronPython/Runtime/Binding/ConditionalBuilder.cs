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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Text;
using System.Dynamic;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    /// <summary>
    /// Builds up a series of conditionals when the False clause isn't yet known.  We can
    /// keep appending conditions and if true's.  Each subsequent true branch becomes the
    /// false branch of the previous condition and body.  Finally a non-conditional terminating
    /// branch must be added.
    /// </summary>
    class ConditionalBuilder {
        private readonly DynamicMetaObjectBinder _action;
        private readonly List<Expression/*!*/>/*!*/ _conditions = new List<Expression>();
        private readonly List<Expression/*!*/>/*!*/ _bodies = new List<Expression>();
        private readonly List<ParameterExpression/*!*/>/*!*/ _variables = new List<ParameterExpression>();
        private Expression _body;
        private BindingRestrictions/*!*/ _restrictions = BindingRestrictions.Empty;
        private ParameterExpression _compareRetBool;
        private Type _retType;

        public ConditionalBuilder(DynamicMetaObjectBinder/*!*/ action) {
            _action = action;
        }

        public ConditionalBuilder() {
        }

        /// <summary>
        /// Adds a new conditional and body.  The first call this becomes the top-level
        /// conditional, subsequent calls will have it added as false statement of the
        /// previous conditional.
        /// </summary>
        public void AddCondition(Expression/*!*/ condition, Expression/*!*/ body) {
            _conditions.Add(condition);
            _bodies.Add(body);
        }

        /// <summary>
        /// Adds a new condition to the last added body / condition.
        /// </summary>
        public void AddCondition(Expression condition) {
            if (_body != null) {
                AddCondition(condition, _body);
                _body = null;
            } else {
                _conditions[_conditions.Count - 1] = Ast.AndAlso(
                    _conditions[_conditions.Count - 1],
                    condition);
            }
        }
        
        /// <summary>
        /// Adds the non-conditional terminating node.
        /// </summary>
        public void FinishCondition(Expression/*!*/ body) {
            FinishCondition(body, typeof(object));
        }

        public void FinishCondition(Expression/*!*/ body, Type retType) {
            if (_body != null) throw new InvalidOperationException();

            _body = body;
            _retType = retType;
        }

        public ParameterExpression CompareRetBool {
            get {
                if (_compareRetBool == null) {
                    _compareRetBool = Expression.Variable(typeof(bool), "compareRetBool");
                    AddVariable(_compareRetBool);
                }

                return _compareRetBool;
            }
        }

        public BindingRestrictions Restrictions {
            get {
                return _restrictions;
            }
            set {
                _restrictions = value;
            }
        }

        public DynamicMetaObjectBinder Action {
            get {
                return _action;
            }
        }

        /// <summary>
        /// Returns true if no conditions have been added
        /// </summary>
        public bool NoConditions {
            get {
                return _conditions.Count == 0;
            }
        }

        /// <summary>
        /// Returns true if a final, non-conditional, body has been added.
        /// </summary>
        public bool IsFinal {
            get {
                return _body != null;
            }
        }

        /// <summary>
        /// Gets the resulting meta object for the full body.  FinishCondition
        /// must have been called.
        /// </summary>
        public DynamicMetaObject/*!*/ GetMetaObject(params DynamicMetaObject/*!*/[]/*!*/ types) {
            if (_body == null) {
                throw new InvalidOperationException("FinishCondition not called before GetMetaObject");
            }

            Expression body = _body;
            for (int i = _bodies.Count - 1; i >= 0; i--) {

                body = Ast.Condition(
                    _conditions[i],
                    AstUtils.Convert(_bodies[i], _retType),
                    AstUtils.Convert(body, _retType)
                );
            }

            body = Ast.Block(_variables, body);

            return new DynamicMetaObject(
                body,
                BindingRestrictions.Combine(types)
            );
        }

        /// <summary>
        /// Adds a variable which will be scoped at the level of the final expression.
        /// </summary>
        public void AddVariable(ParameterExpression/*!*/ var) {
            if (_body != null) {
                throw new InvalidOperationException("Variables must be added before calling FinishCondition");
            }

            _variables.Add(var);
        }
    }

}
