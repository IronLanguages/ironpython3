// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Interface used to mark objects which contain a dictionary of custom attributes that shadow
    /// their existing attributes in a dynamic fashion.  <seealso cref="T:MetaExpandable"/>
    /// </summary>
    public interface IPythonExpandable {
        /// <summary>
        /// Ensures that a non-null IDictionary instance is created for CustomAttributes and
        /// returns it.
        /// </summary>
        IDictionary<string, object> EnsureCustomAttributes();

        IDictionary<string, object> CustomAttributes {
            get;
        }

        CodeContext/*!*/ Context {
            get;
        }
    }

    /// <summary>
    /// Meta-object which allows IPythonExpandable objects to behave like Python objects in their
    /// ability to dynamically add and remove new or existing custom attributes, generally shadowing
    /// existing built-in members.  <seealso cref="T:IPythonExpandable"/>
    /// 
    /// Getting: Member accesses first consult the object's CustomAttributes dictionary, then fall
    ///     through to the underlying object.
    /// 
    /// Setting: Values can be bound to any member name, shadowing any existing attributes except
    ///     public non-PythonHidden fields and properties, which will bypass the dictionary.  Thus,
    ///     it is possible for SetMember to fail, for example if the property is read-only or of
    ///     the wrong type.
    /// 
    /// Deleting: Any member represented in the dictionary can be deleted, re-exposing the
    ///     underlying member if it exists.  Any other deletions will fail.
    /// </summary>
    public sealed class MetaExpandable<T> : DynamicMetaObject where T : IPythonExpandable {
        private static readonly object _getFailed = new object();

        public MetaExpandable(Expression parameter, IPythonExpandable value)
            : base(parameter, BindingRestrictions.Empty, value) {
        }

        public new T Value {
            get { return (T)base.Value; }
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
            return DynamicTryGetMember(
                binder.Name,
                binder.FallbackGetMember(this).Expression,
                res => res
            );
        }

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args) {
            return DynamicTryGetMember(
                binder.Name,
                binder.FallbackInvokeMember(this, args).Expression,
                res => binder.FallbackInvoke(new DynamicMetaObject(res, BindingRestrictions.Empty), args, null).Expression
            );
        }

        private DynamicMetaObject DynamicTryGetMember(string name, Expression fallback, Func<Expression, Expression> transform) {
            var temp = Expression.Parameter(typeof(object));
            return new DynamicMetaObject(
                Expression.Block(
                    new[] { temp },
                    Expression.Condition(
                        Expression.NotEqual(
                            Expression.Assign(
                                temp,
                                Expression.Invoke(
                                    Expression.Constant(new Func<T, string, object>(TryGetMember)),
                                    Convert(Expression, typeof(T)),
                                    Expression.Constant(name)
                                )
                            ),
                            Expression.Constant(_getFailed)
                        ),
                        AstUtils.Convert(transform(temp), typeof(object)),
                        AstUtils.Convert(fallback, typeof(object))
                    )
                ),
                GetRestrictions()
            );
        }

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
            return new DynamicMetaObject(
                Expression.Block(
                    Expression.Condition(
                        Convert(
                            Expression.Invoke(
                                Expression.Constant(new Func<T, string, object, bool>(TrySetMember)),
                                Convert(Expression, typeof(T)),
                                Expression.Constant(binder.Name),
                                Convert(value.Expression, typeof(object))
                            ),
                            typeof(bool)
                        ),
                        Expression.Empty(),
                        binder.FallbackSetMember(this, value).Expression,
                        typeof(void)
                    ),
                    value.Expression
                ),
                GetRestrictions()
            );
        }

        public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) {
            return new DynamicMetaObject(
                Expression.Condition(
                    Convert(
                        Expression.Invoke(
                            Expression.Constant(new Func<T, string, bool>(TryDeleteMember)),
                            Convert(Expression, typeof(T)),
                            Expression.Constant(binder.Name)
                        ),
                        typeof(bool)
                    ),
                    Expression.Empty(),
                    binder.FallbackDeleteMember(this).Expression,
                    typeof(void)
                ),
                GetRestrictions()
            );
        }

        public override IEnumerable<string> GetDynamicMemberNames() {
            var dict = Value.CustomAttributes;
            if (dict == null) {
                return base.GetDynamicMemberNames();
            }

            IList<object> names = DynamicHelpers.GetPythonType(Value).GetMemberNames(Value.Context);
            List<string> res = new List<string>(dict.Keys.Count + names.Count);
            res.AddRange(dict.Keys);
            for (int i = 0; i < names.Count; i++) {
                string name = names[i] as string;

                if (name == null) {
                    Extensible<string> es = names[i] as Extensible<string>;
                    if (es == null) {
                        continue;
                    }

                    name = es.Value;
                }

                res.Add(name);
            }

            return res;
        }

        private BindingRestrictions GetRestrictions() {
            return BindingRestrictions.GetTypeRestriction(Expression, typeof(T));
        }

        private static Expression Convert(Expression expression, Type type) {
            return (expression.Type != type) ? Expression.Convert(expression, type) : expression;
        }

        private static object TryGetMember(T target, string name) {
            var dict = target.CustomAttributes;
            object res;
            if (dict != null && dict.TryGetValue(name, out res)) {
                return res;
            }
            return _getFailed;
        }

        private static bool TrySetMember(T target, string name, object value) {
            MemberInfo member =
                (MemberInfo)typeof(T).GetProperty(name) ??
                typeof(T).GetField(name);

            if (member != null && !PythonHiddenAttribute.IsHidden(member)) {
                return false;
            }

            target.EnsureCustomAttributes()[name] = value;
            return true;
        }

        private static bool TryDeleteMember(T target, string name) {
            var dict = target.CustomAttributes;
            return dict != null && dict.Remove(name);
        }
    }
}
