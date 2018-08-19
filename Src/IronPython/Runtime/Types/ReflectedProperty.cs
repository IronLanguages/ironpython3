// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Types {
    [PythonType("getset_descriptor")]
    public class ReflectedProperty : ReflectedGetterSetter, ICodeFormattable {
        private readonly PropertyInfo/*!*/ _info;

        public ReflectedProperty(PropertyInfo info, MethodInfo getter, MethodInfo setter, NameType nt)
            : base(new MethodInfo[] { getter }, new MethodInfo[] { setter }, nt) {
            Debug.Assert(info != null);

            _info = info;
        }

        /// <summary>
        /// True if generating code for gets can result in more optimal accesses.
        /// </summary>
        internal override bool CanOptimizeGets {
            get {
                return true;
            }
        }

        public ReflectedProperty(PropertyInfo info, MethodInfo[] getters, MethodInfo[] setters, NameType nt)
            : base(getters, setters, nt) {
            Debug.Assert(info != null);

            _info = info;
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            if (Setter.Length == 0) {
                return false;
            }

            if (instance == null) {
                foreach (MethodInfo mi in Setter) {
                    if(mi.IsStatic && DeclaringType != owner.UnderlyingSystemType) {
                        return false;
                    } else if (mi.IsProtected()) {
                        throw PythonOps.TypeErrorForProtectedMember(owner.UnderlyingSystemType, _info.Name);
                    }
                }
            } else if (instance != null) {
                foreach (MethodInfo mi in Setter) {
                    if (mi.IsStatic) {
                        return false;
                    }
                }
            }

            return CallSetter(context, context.LanguageContext.GetGenericCallSiteStorage(), instance, ArrayUtils.EmptyObjects, value);
        }

        internal override Type DeclaringType {
            get { return _info.DeclaringType; }
        }

        public override string __name__ {
            get { return _info.Name; }
        }

        public PropertyInfo Info {
            [PythonHidden]
            get {
                return _info;
            }
        }

        public override PythonType PropertyType {
            [PythonHidden]
            get {
                return DynamicHelpers.GetPythonTypeFromType(_info.PropertyType);
            }
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Properties, this);

            value = CallGetter(context, owner, context.LanguageContext.GetGenericCallSiteStorage0(), instance);
            return true;
        }

        private object CallGetter(CodeContext context, PythonType owner, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object>>> storage, object instance) {
            if (NeedToReturnProperty(instance, Getter)) {
                return this;
            }

            if (Getter.Length == 0) {
                throw new MissingMemberException("unreadable property");
            }

            if (owner == null) {
                owner = DynamicHelpers.GetPythonType(instance);
            }

            // this matches the logic in the default binder when it does a property get.  We 
            // need to duplicate it here to be consistent for all gets.
            MethodInfo[] members = Getter;

            Type type = owner.UnderlyingSystemType;
            if (Getter.Length > 1) {
                // if we were given multiple members pick the member closest to the type...                
                Type bestMemberDeclaringType = Getter[0].DeclaringType;
                MethodInfo bestMember = Getter[0];

                for (int i = 1; i < Getter.Length; i++) {
                    MethodInfo mt = Getter[i];
                    if (!IsApplicableForType(type, mt)) {
                        continue;
                    }

                    if (Getter[i].DeclaringType.IsSubclassOf(bestMemberDeclaringType) ||
                        !IsApplicableForType(type, bestMember)) {
                        bestMember = Getter[i];
                        bestMemberDeclaringType = Getter[i].DeclaringType;
                    }
                }
                members = new MethodInfo[] { bestMember };
            }

            BuiltinFunction target = PythonTypeOps.GetBuiltinFunction(type, __name__, members);

            return target.Call0(context, storage, instance);
        }

        private static bool IsApplicableForType(Type type, MethodInfo mt) {
            return mt.DeclaringType == type || type.IsSubclassOf(mt.DeclaringType);
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            __delete__(instance);
            return true;
        }

        internal override void MakeGetExpression(PythonBinder/*!*/ binder, Expression/*!*/ codeContext, DynamicMetaObject instance, DynamicMetaObject/*!*/ owner, ConditionalBuilder/*!*/ builder) {
            if (Getter.Length != 0 && !Getter[0].IsPublic) {
                // fallback to runtime call
                base.MakeGetExpression(binder, codeContext, instance, owner, builder);
            } else if (NeedToReturnProperty(instance, Getter)) {
                builder.FinishCondition(AstUtils.Constant(this));
            } else if (Getter[0].ContainsGenericParameters) {
                builder.FinishCondition(
                    DefaultBinder.MakeError(
                        binder.MakeContainsGenericParametersError(
                            MemberTracker.FromMemberInfo(_info)
                        ),
                        typeof(object)
                    ).Expression
                );
            } else if (instance != null) {
                builder.FinishCondition(
                    AstUtils.Convert(
                        binder.MakeCallExpression(
                            new PythonOverloadResolverFactory(binder, codeContext),
                            Getter[0],
                            instance
                        ).Expression,
                        typeof(object)
                    )
                );
            } else {
                builder.FinishCondition(
                    AstUtils.Convert(
                        binder.MakeCallExpression(
                            new PythonOverloadResolverFactory(binder, codeContext),
                            Getter[0]
                        ).Expression,
                        typeof(object)
                    )
                );                
            }
        }

        internal override bool IsAlwaysVisible {
            get {
                return NameType == NameType.PythonProperty;
            }
        }

        internal override bool IsSetDescriptor(CodeContext context, PythonType owner) {
            return Setter.Length != 0;
        }

        #region Public Python APIs

        /// <summary>
        /// Convenience function for users to call directly
        /// </summary>
        [PythonHidden]
        public object GetValue(CodeContext context, object instance) {
            object value;
            if (TryGetValue(context, instance, DynamicHelpers.GetPythonType(instance), out value)) {
                return value;
            }
            throw new InvalidOperationException("cannot get property");
        }

        /// <summary>
        /// Convenience function for users to call directly
        /// </summary>
        [PythonHidden]
        public void SetValue(CodeContext context, object instance, object value) {
            if (!TrySetValue(context, instance, DynamicHelpers.GetPythonType(instance), value)) {
                throw new InvalidOperationException("cannot set property");
            }
        }

        public void __set__(CodeContext context, object instance, object value) {
            // TODO: Throw?  currently we have a test that verifies we never throw when this is called directly.
            TrySetValue(context, instance, DynamicHelpers.GetPythonType(instance), value);
        }

        public void __delete__(object instance) {
            if (Setter.Length != 0)
                throw PythonOps.AttributeErrorForReadonlyAttribute(
                    DynamicHelpers.GetPythonTypeFromType(DeclaringType).Name,
                    __name__);
            else
                throw PythonOps.AttributeErrorForBuiltinAttributeDeletion(
                    DynamicHelpers.GetPythonTypeFromType(DeclaringType).Name,
                    __name__);
        }

        public string __doc__ {
            get {
                return DocBuilder.DocOneInfo(Info);
            }
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<property# {0} on {1}>",
                __name__,
                DynamicHelpers.GetPythonTypeFromType(DeclaringType).Name);
        }

        #endregion
    }
}
