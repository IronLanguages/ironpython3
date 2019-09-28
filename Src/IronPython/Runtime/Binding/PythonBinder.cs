// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using System.Numerics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal partial class PythonBinder : DefaultBinder {
        private PythonContext/*!*/ _context;
        private SlotCache/*!*/ _typeMembers = new SlotCache();
        private SlotCache/*!*/ _resolvedMembers = new SlotCache();
        private Dictionary<Type/*!*/, IList<Type/*!*/>/*!*/>/*!*/ _dlrExtensionTypes;
        private bool _registeredInterfaceExtensions;    // true if someone has registered extensions for interfaces

        [MultiRuntimeAware]
        private static readonly Dictionary<Type/*!*/, ExtensionTypeInfo/*!*/>/*!*/ _sysTypes = MakeSystemTypes();

        public PythonBinder(PythonContext/*!*/ pythonContext, CodeContext context) {
            ContractUtils.RequiresNotNull(pythonContext, nameof(pythonContext));

            _dlrExtensionTypes = MakeExtensionTypes();
            _context = pythonContext;           
            if (context != null) {
                context.LanguageContext.DomainManager.AssemblyLoaded += new EventHandler<AssemblyLoadedEventArgs>(DomainManager_AssemblyLoaded);

                foreach (Assembly asm in pythonContext.DomainManager.GetLoadedAssemblyList()) {
                    DomainManager_AssemblyLoaded(this, new AssemblyLoadedEventArgs(asm));
                }
            }
        }

        public PythonBinder(PythonBinder binder) {
            _context = binder._context;
            _typeMembers = binder._typeMembers;
            _resolvedMembers = binder._resolvedMembers;
            _dlrExtensionTypes = binder._dlrExtensionTypes;
            _registeredInterfaceExtensions = binder._registeredInterfaceExtensions;
        }

        public override Expression/*!*/ ConvertExpression(Expression/*!*/ expr, Type/*!*/ toType, ConversionResultKind kind, OverloadResolverFactory factory) {
            ContractUtils.RequiresNotNull(expr, nameof(expr));
            ContractUtils.RequiresNotNull(toType, nameof(toType));

            Type exprType = expr.Type;

            if (toType == typeof(object)) {
                if (exprType.IsValueType) {
                    return AstUtils.Convert(expr, toType);
                } else {
                    return expr;
                }
            }

            if (toType.IsAssignableFrom(exprType)) {
                // remove the unboxing expression if we're going be boxing again
                if (exprType.IsValueType && !toType.IsValueType && expr.NodeType == ExpressionType.Unbox) {
                    return ((UnaryExpression)expr).Operand;
                }
                return expr;
            }
            
            Type visType = Context.Binder.PrivateBinding ? toType : CompilerHelpers.GetVisibleType(toType);

            if (exprType == typeof(PythonType) && visType == typeof(Type)) {
                return AstUtils.Convert(expr, visType); // use the implicit conversion
            }

            return Binders.Convert(
                ((PythonOverloadResolverFactory)factory)._codeContext,
                _context,
                visType,
                visType == typeof(char) ? ConversionResultKind.ImplicitCast : kind,
                expr
            );
        }

        internal static MethodInfo GetGenericConvertMethod(Type toType) {
            if (toType.IsValueType) {
                if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                    return typeof(Converter).GetMethod(nameof(Converter.ConvertToNullableType));
                } else {
                    return typeof(Converter).GetMethod(nameof(Converter.ConvertToValueType));
                }
            } else {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToReferenceType));
            }
        }


        internal static MethodInfo GetFastConvertMethod(Type toType) {
            if (toType == typeof(char)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToChar));
            } else if (toType == typeof(int)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToInt32));
            } else if (toType == typeof(string)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToString));
            } else if (toType == typeof(long)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToInt64));
            } else if (toType == typeof(double)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToDouble));
            } else if (toType == typeof(bool)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToBoolean));
            } else if (toType == typeof(BigInteger)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToBigInteger));
            } else if (toType == typeof(Complex)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToComplex));
            } else if (toType == typeof(IEnumerable)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToIEnumerable));
            } else if (toType == typeof(float)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToSingle));
            } else if (toType == typeof(byte)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToByte));
            } else if (toType == typeof(sbyte)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToSByte));
            } else if (toType == typeof(short)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToInt16));
            } else if (toType == typeof(uint)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToUInt32));
            } else if (toType == typeof(ulong)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToUInt64));
            } else if (toType == typeof(ushort)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToUInt16));
            } else if (toType == typeof(Type)) {
                return typeof(Converter).GetMethod(nameof(Converter.ConvertToType));
            } else {
                return null;
            }
        }

        public override object Convert(object obj, Type toType) {
            return Converter.Convert(obj, toType);
        }

        public override bool CanConvertFrom(Type fromType, Type toType, bool toNotNullable, NarrowingLevel level) {
            return Converter.CanConvertFrom(fromType, toType, level);
        }

        public override Candidate PreferConvert(Type t1, Type t2) {
            return Converter.PreferConvert(t1, t2);
        }

        public override bool PrivateBinding {
            get {
                return _context.DomainManager.Configuration.PrivateBinding;
            }
        }

        public override ErrorInfo MakeSetValueTypeFieldError(FieldTracker field, DynamicMetaObject instance, DynamicMetaObject value) {
            // allow the set but emit a warning
            return ErrorInfo.FromValueNoError(
                Expression.Block(
                    Expression.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.Warn)),
                        Expression.Constant(_context.SharedContext),
                        Expression.Constant(PythonExceptions.RuntimeWarning),
                        Expression.Constant(ReflectedField.UpdateValueTypeFieldWarning),
                        Expression.Constant(
                            new object[] { field.Name, field.DeclaringType.Name }
                        )
                    ),
                    Expression.Assign(
                        Expression.Field(
                            AstUtils.Convert(
                                instance.Expression, 
                                field.DeclaringType
                            ), 
                            field.Field
                        ),
                        ConvertExpression(
                            value.Expression, 
                            field.FieldType, 
                            ConversionResultKind.ExplicitCast, 
                            new PythonOverloadResolverFactory(
                                this, 
                                Expression.Constant(_context.SharedContext)
                            )
                        )
                    )
                )
            );
        }

        public override ErrorInfo MakeConversionError(Type toType, Expression value) {
            return ErrorInfo.FromException(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.TypeErrorForTypeMismatch)),
                    AstUtils.Constant(DynamicHelpers.GetPythonTypeFromType(toType).Name),
                    AstUtils.Convert(value, typeof(object))
               )
            );
        }

        public override ErrorInfo/*!*/ MakeNonPublicMemberGetError(OverloadResolverFactory resolverFactory, MemberTracker member, Type type, DynamicMetaObject instance) {
            if (PrivateBinding) {
                return base.MakeNonPublicMemberGetError(resolverFactory, member, type, instance);
            }

            return ErrorInfo.FromValue(
                BindingHelpers.TypeErrorForProtectedMember(type, member.Name)
            );
        }

        public override ErrorInfo/*!*/ MakeStaticAssignFromDerivedTypeError(Type accessingType, DynamicMetaObject instance, MemberTracker info, DynamicMetaObject assignedValue, OverloadResolverFactory factory) {
            return MakeMissingMemberError(accessingType, instance, info.Name);
        }

        public override ErrorInfo/*!*/ MakeStaticPropertyInstanceAccessError(PropertyTracker/*!*/ tracker, bool isAssignment, IList<DynamicMetaObject>/*!*/ parameters) {            
            ContractUtils.RequiresNotNull(tracker, nameof(tracker));
            ContractUtils.RequiresNotNull(parameters, nameof(parameters));

            if (isAssignment) {
                return ErrorInfo.FromException(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.StaticAssignmentFromInstanceError)),
                        AstUtils.Constant(tracker),
                        AstUtils.Constant(isAssignment)
                    )
                );
            }

            return ErrorInfo.FromValue(
                Ast.Property(
                    null,
                    tracker.GetGetMethod(DomainManager.Configuration.PrivateBinding)
                )
            );
        }

        #region .NET member binding

        public override string GetTypeName(Type t) {
            return DynamicHelpers.GetPythonTypeFromType(t).Name;
        }

        public override MemberGroup/*!*/ GetMember(MemberRequestKind actionKind, Type type, string name) {
            MemberGroup mg;
            if (!_resolvedMembers.TryGetCachedMember(type, name, actionKind == MemberRequestKind.Get, out mg)) {
                mg = PythonTypeInfo.GetMemberAll(
                    this,
                    actionKind,
                    type,
                    name);

                _resolvedMembers.CacheSlot(type, actionKind == MemberRequestKind.Get, name, PythonTypeOps.GetSlot(mg, name, PrivateBinding), mg);
            }

            return mg ?? MemberGroup.EmptyGroup;
        }


        public override ErrorInfo/*!*/ MakeEventValidation(MemberGroup/*!*/ members, DynamicMetaObject eventObject, DynamicMetaObject/*!*/ value, OverloadResolverFactory/*!*/ factory) {            
            EventTracker ev = (EventTracker)members[0];

            return ErrorInfo.FromValueNoError(
               Ast.Block(
                   Ast.Call(
                       typeof(PythonOps).GetMethod(nameof(PythonOps.SlotTrySetValue)),
                       ((PythonOverloadResolverFactory)factory)._codeContext,
                       AstUtils.Constant(PythonTypeOps.GetReflectedEvent(ev)),
                       eventObject != null ? AstUtils.Convert(eventObject.Expression, typeof(object)) : AstUtils.Constant(null),
                       AstUtils.Constant(null, typeof(PythonType)),
                       AstUtils.Convert(value.Expression, typeof(object))
                   ),
                   Ast.Constant(null)
               )
            );
        }

        public override ErrorInfo MakeMissingMemberError(Type type, DynamicMetaObject self, string name) {
            string typeName;
            if (typeof(TypeTracker).IsAssignableFrom(type)) {
                typeName = "type";
            } else {
                typeName = NameConverter.GetTypeName(type);
            }

            return ErrorInfo.FromException(
                Ast.New(
                    typeof(MissingMemberException).GetConstructor(new Type[] { typeof(string) }),
                    AstUtils.Constant(String.Format("'{0}' object has no attribute '{1}'", typeName, name))
                )
            );
        }

        public override ErrorInfo MakeMissingMemberErrorForAssign(Type type, DynamicMetaObject self, string name) {
            if (self != null) {
                return MakeMissingMemberError(type, self, name);
            }

            return ErrorInfo.FromException(
                Ast.New(
                    typeof(TypeErrorException).GetConstructor(new Type[] { typeof(string) }),
                    AstUtils.Constant(String.Format("can't set attributes of built-in/extension type '{0}'", NameConverter.GetTypeName(type)))
                )
            );
        }

        public override ErrorInfo MakeMissingMemberErrorForAssignReadOnlyProperty(Type type, DynamicMetaObject self, string name) {
            return ErrorInfo.FromException(
                Ast.New(
                    typeof(MissingMemberException).GetConstructor(new Type[] { typeof(string) }),
                    AstUtils.Constant(String.Format("can't assign to read-only property {0} of type '{1}'", name, NameConverter.GetTypeName(type)))
                )
            );
        }

        public override ErrorInfo MakeMissingMemberErrorForDelete(Type type, DynamicMetaObject self, string name) {
            return MakeMissingMemberErrorForAssign(type, self, name);
        }

        /// <summary>
        /// Provides a way for the binder to provide a custom error message when lookup fails.  Just
        /// doing this for the time being until we get a more robust error return mechanism.
        /// </summary>
        public override ErrorInfo MakeReadOnlyMemberError(Type type, string name) {
            return ErrorInfo.FromException(
                Ast.New(
                    typeof(MissingMemberException).GetConstructor(new Type[] { typeof(string) }),
                    AstUtils.Constant(
                        String.Format("attribute '{0}' of '{1}' object is read-only",
                            name,
                            NameConverter.GetTypeName(type)
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Provides a way for the binder to provide a custom error message when lookup fails.  Just
        /// doing this for the time being until we get a more robust error return mechanism.
        /// </summary>
        public override ErrorInfo MakeUndeletableMemberError(Type type, string name) {
            return ErrorInfo.FromException(
                Ast.New(
                    typeof(MissingMemberException).GetConstructor(new Type[] { typeof(string) }),
                    AstUtils.Constant(
                        String.Format("cannot delete attribute '{0}' of builtin type '{1}'",
                            name,
                            NameConverter.GetTypeName(type)
                        )
                    )
                )
            );
        }

        #endregion

        internal IList<Type> GetExtensionTypesInternal(Type t) {
            List<Type> res = new List<Type>(base.GetExtensionTypes(t));

            AddExtensionTypes(t, res);

            return res.ToArray();
        }

        public override bool IncludeExtensionMember(MemberInfo member) {
            return !member.DeclaringType.IsDefined(typeof(PythonHiddenBaseClassAttribute), false);
        }

        public override IList<Type> GetExtensionTypes(Type t) {
            List<Type> list = new List<Type>();

            // Python includes the types themselves so we can use extension properties w/ CodeContext
            list.Add(t);

            list.AddRange(base.GetExtensionTypes(t));

            AddExtensionTypes(t, list);

            return list;
        }
        
        private void AddExtensionTypes(Type t, List<Type> list) {
            ExtensionTypeInfo extType;
            if (_sysTypes.TryGetValue(t, out extType)) {
                list.Add(extType.ExtensionType);
            }

            IList<Type> userExtensions;
            lock (_dlrExtensionTypes) {
                if (_dlrExtensionTypes.TryGetValue(t, out userExtensions)) {
                    list.AddRange(userExtensions);
                }

                if (_registeredInterfaceExtensions) {
                    foreach (Type ifaceType in t.GetInterfaces()) {
                        IList<Type> extTypes;
                        if (_dlrExtensionTypes.TryGetValue(ifaceType, out extTypes)) {
                            list.AddRange(extTypes);
                        }
                    }
                }

                if (t.IsGenericType) {
                    // search for generic extensions, e.g. ListOfTOps<T> for List<T>,
                    // we then make a new generic type out of the extension type.
                    Type typeDef = t.GetGenericTypeDefinition();
                    Type[] args = t.GetGenericArguments();

                    if (_dlrExtensionTypes.TryGetValue(typeDef, out userExtensions)) {
                        foreach (Type genExtType in userExtensions) {
                            list.Add(genExtType.MakeGenericType(args));
                        }
                    }
                }
            }
        }

        public bool HasExtensionTypes(Type t) {
            return _dlrExtensionTypes.ContainsKey(t);
        }

        public override DynamicMetaObject ReturnMemberTracker(Type type, MemberTracker memberTracker) {
            var res = ReturnMemberTracker(type, memberTracker, PrivateBinding);

            return res ?? base.ReturnMemberTracker(type, memberTracker);
        }

        private static DynamicMetaObject ReturnMemberTracker(Type type, MemberTracker memberTracker, bool privateBinding) {
            switch (memberTracker.MemberType) {
                case TrackerTypes.TypeGroup:
                    return new DynamicMetaObject(AstUtils.Constant(memberTracker), BindingRestrictions.Empty, memberTracker);
                case TrackerTypes.Type:
                    return ReturnTypeTracker((TypeTracker)memberTracker);
                case TrackerTypes.Bound:
                    return new DynamicMetaObject(ReturnBoundTracker((BoundMemberTracker)memberTracker, privateBinding), BindingRestrictions.Empty);
                case TrackerTypes.Property:
                    return new DynamicMetaObject(ReturnPropertyTracker((PropertyTracker)memberTracker, privateBinding), BindingRestrictions.Empty);;
                case TrackerTypes.Event:
                    return new DynamicMetaObject(Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.MakeBoundEvent)),
                        AstUtils.Constant(PythonTypeOps.GetReflectedEvent((EventTracker)memberTracker)),
                        AstUtils.Constant(null),
                        AstUtils.Constant(type)
                    ), BindingRestrictions.Empty);;
                case TrackerTypes.Field:
                    return new DynamicMetaObject(ReturnFieldTracker((FieldTracker)memberTracker), BindingRestrictions.Empty);;
                case TrackerTypes.MethodGroup:
                    return new DynamicMetaObject(ReturnMethodGroup((MethodGroup)memberTracker), BindingRestrictions.Empty); ;
                case TrackerTypes.Constructor:
                    MethodBase[] ctors = PythonTypeOps.GetConstructors(type, privateBinding, true);
                    object val;
                    if (PythonTypeOps.IsDefaultNew(ctors)) {
                        if (IsPythonType(type)) {
                            val = InstanceOps.New;
                        } else {
                            val = InstanceOps.NewCls;
                        }
                    } else {
                        val = PythonTypeOps.GetConstructor(type, InstanceOps.NonDefaultNewInst, ctors);
                    }

                    return new DynamicMetaObject(AstUtils.Constant(val), BindingRestrictions.Empty, val);
                case TrackerTypes.Custom:
                    return new DynamicMetaObject(
                        AstUtils.Constant(((PythonCustomTracker)memberTracker).GetSlot(), typeof(PythonTypeSlot)),
                        BindingRestrictions.Empty,
                        ((PythonCustomTracker)memberTracker).GetSlot()
                    );
            }
            return null;
        }

        /// <summary>
        /// Gets the PythonBinder associated with tihs CodeContext
        /// </summary>
        public static PythonBinder/*!*/ GetBinder(CodeContext/*!*/ context) {
            return (PythonBinder)context.LanguageContext.Binder;
        }

        /// <summary>
        /// Performs .NET member resolution.  This looks within the given type and also
        /// includes any extension members.  Base classes and their extension members are 
        /// not searched.
        /// </summary>
        public bool TryLookupSlot(CodeContext/*!*/ context, PythonType/*!*/ type, string name, out PythonTypeSlot slot) {
            Debug.Assert(type.IsSystemType);

            return TryLookupProtectedSlot(context, type, name, out slot);
        }

        /// <summary>
        /// Performs .NET member resolution.  This looks within the given type and also
        /// includes any extension members.  Base classes and their extension members are 
        /// not searched.
        /// 
        /// This version allows PythonType's for protected member resolution.  It shouldn't
        /// be called externally for other purposes.
        /// </summary>
        internal bool TryLookupProtectedSlot(CodeContext/*!*/ context, PythonType/*!*/ type, string name, out PythonTypeSlot slot) {
            Type curType = type.UnderlyingSystemType;

            if (!_typeMembers.TryGetCachedSlot(curType, true, name, out slot)) {
                MemberGroup mg = PythonTypeInfo.GetMember(
                    this,
                    MemberRequestKind.Get,
                    curType,
                    name);

                slot = PythonTypeOps.GetSlot(mg, name, PrivateBinding);

                _typeMembers.CacheSlot(curType, true, name, slot, mg);
            }

            if (slot != null && (slot.IsAlwaysVisible || PythonOps.IsClsVisible(context))) {
                return true;
            }

            slot = null;
            return false;
        }

        /// <summary>
        /// Performs .NET member resolution.  This looks the type and any base types
        /// for members.  It also searches for extension members in the type and any base types.
        /// </summary>
        public bool TryResolveSlot(CodeContext/*!*/ context, PythonType/*!*/ type, PythonType/*!*/ owner, string name, out PythonTypeSlot slot) {
            Type curType = type.UnderlyingSystemType;

            if (!_resolvedMembers.TryGetCachedSlot(curType, true, name, out slot)) {
                MemberGroup mg = PythonTypeInfo.GetMemberAll(
                    this,
                    MemberRequestKind.Get,
                    curType,
                    name);

                slot = PythonTypeOps.GetSlot(mg, name, PrivateBinding);

                _resolvedMembers.CacheSlot(curType, true, name, slot, mg);
            }

            if (slot != null && (slot.IsAlwaysVisible || PythonOps.IsClsVisible(context))) {
                return true;
            }

            slot = null;
            return false;
        }

        /// <summary>
        /// Gets the member names which are defined in this type and any extension members.
        /// 
        /// This search does not include members in any subtypes or their extension members.
        /// </summary>
        public void LookupMembers(CodeContext/*!*/ context, PythonType/*!*/ type, PythonDictionary/*!*/ memberNames) {
            if (!_typeMembers.IsFullyCached(type.UnderlyingSystemType, true)) {
                Dictionary<string, KeyValuePair<PythonTypeSlot, MemberGroup>> members = new Dictionary<string, KeyValuePair<PythonTypeSlot, MemberGroup>>();

                foreach (ResolvedMember rm in PythonTypeInfo.GetMembers(
                    this,
                    MemberRequestKind.Get,
                    type.UnderlyingSystemType)) {

                    if (!members.ContainsKey(rm.Name)) {
                        members[rm.Name] = new KeyValuePair<PythonTypeSlot, MemberGroup>(
                            PythonTypeOps.GetSlot(rm.Member, rm.Name, PrivateBinding), 
                            rm.Member
                        );
                    }
                }

                _typeMembers.CacheAll(type.UnderlyingSystemType, true, members);
            }

            foreach (KeyValuePair<string, PythonTypeSlot> kvp in _typeMembers.GetAllMembers(type.UnderlyingSystemType, true)) {
                PythonTypeSlot slot = kvp.Value;
                string name = kvp.Key;

                if (slot.IsAlwaysVisible || PythonOps.IsClsVisible(context)) {
                    memberNames[name] = slot;
                }
            }
        }

        /// <summary>
        /// Gets the member names which are defined in the type and any subtypes.  
        /// 
        /// This search includes members in the type and any subtypes as well as extension
        /// types of the type and its subtypes.
        /// </summary>
        public void ResolveMemberNames(CodeContext/*!*/ context, PythonType/*!*/ type, PythonType/*!*/ owner, Dictionary<string, string>/*!*/ memberNames) {
            if (!_resolvedMembers.IsFullyCached(type.UnderlyingSystemType, true)) {
                Dictionary<string, KeyValuePair<PythonTypeSlot, MemberGroup>> members = new Dictionary<string, KeyValuePair<PythonTypeSlot, MemberGroup>>();

                foreach (ResolvedMember rm in PythonTypeInfo.GetMembersAll(
                    this,
                    MemberRequestKind.Get,
                    type.UnderlyingSystemType)) {

                    if (!members.ContainsKey(rm.Name)) {
                        members[rm.Name] = new KeyValuePair<PythonTypeSlot, MemberGroup>(
                            PythonTypeOps.GetSlot(rm.Member, rm.Name, PrivateBinding), 
                            rm.Member
                        );
                    }
                }

                _resolvedMembers.CacheAll(type.UnderlyingSystemType, true, members);
            }

            foreach (KeyValuePair<string, PythonTypeSlot> kvp in _resolvedMembers.GetAllMembers(type.UnderlyingSystemType, true)) {
                PythonTypeSlot slot = kvp.Value;
                string name = kvp.Key;

                if (slot.IsAlwaysVisible || PythonOps.IsClsVisible(context)) {
                    memberNames[name] = name;
                }
            }
        }

        private static Expression ReturnFieldTracker(FieldTracker fieldTracker) {
            return AstUtils.Constant(PythonTypeOps.GetReflectedField(fieldTracker.Field));
        }

        private static Expression ReturnMethodGroup(MethodGroup methodGroup) {
            return AstUtils.Constant(PythonTypeOps.GetFinalSlotForFunction(GetBuiltinFunction(methodGroup)));
        }

        private static Expression ReturnBoundTracker(BoundMemberTracker boundMemberTracker, bool privateBinding) {
            MemberTracker boundTo = boundMemberTracker.BoundTo;
            switch (boundTo.MemberType) {
                case TrackerTypes.Property:
                    PropertyTracker pt = (PropertyTracker)boundTo;
                    Debug.Assert(pt.GetIndexParameters().Length > 0);
                    return Ast.New(
                        typeof(ReflectedIndexer).GetConstructor(new Type[] { typeof(ReflectedIndexer), typeof(object) }),
                        AstUtils.Constant(new ReflectedIndexer(((ReflectedPropertyTracker)pt).Property, NameType.Property, privateBinding)),
                        boundMemberTracker.Instance.Expression
                    );
                case TrackerTypes.Event:
                    return Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.MakeBoundEvent)),
                        AstUtils.Constant(PythonTypeOps.GetReflectedEvent((EventTracker)boundMemberTracker.BoundTo)),
                        boundMemberTracker.Instance.Expression,
                        AstUtils.Constant(boundMemberTracker.DeclaringType)
                    );
                case TrackerTypes.MethodGroup:
                    return Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.MakeBoundBuiltinFunction)),
                        AstUtils.Constant(GetBuiltinFunction((MethodGroup)boundTo)),
                        AstUtils.Convert(
                            boundMemberTracker.Instance.Expression,
                            typeof(object)
                        )
                    );
            }
            throw new NotImplementedException();
        }

        private static BuiltinFunction GetBuiltinFunction(MethodGroup mg) {
            MethodBase[] methods = new MethodBase[mg.Methods.Count];
            for (int i = 0; i < mg.Methods.Count; i++) {
                methods[i] = mg.Methods[i].Method;
            }
            return PythonTypeOps.GetBuiltinFunction(
                mg.DeclaringType,
                mg.Methods[0].Name,
                (PythonTypeOps.GetMethodFunctionType(mg.DeclaringType, methods) & (~FunctionType.FunctionMethodMask)) |
                    (mg.ContainsInstance ? FunctionType.Method : FunctionType.None) |
                    (mg.ContainsStatic ? FunctionType.Function : FunctionType.None),
                mg.GetMethodBases()
            );
        }

        private static Expression ReturnPropertyTracker(PropertyTracker propertyTracker, bool privateBinding) {
            return AstUtils.Constant(PythonTypeOps.GetReflectedProperty(propertyTracker, new MemberGroup(propertyTracker), privateBinding));
        }

        private static DynamicMetaObject ReturnTypeTracker(TypeTracker memberTracker) {
            // all non-group types get exposed as PythonType's
            object value = DynamicHelpers.GetPythonTypeFromType(memberTracker.Type);
            return new DynamicMetaObject(Ast.Constant(value), BindingRestrictions.Empty, value);
        }

        internal ScriptDomainManager/*!*/ DomainManager {
            get {
                return _context.DomainManager;
            }
        }

        private class ExtensionTypeInfo {
            public Type ExtensionType;
            public string PythonName;

            public ExtensionTypeInfo(Type extensionType, string pythonName) {
                ExtensionType = extensionType;
                PythonName = pythonName;
            }
        }

        internal static void AssertNotExtensionType(Type t) {
            foreach (ExtensionTypeInfo typeinfo in _sysTypes.Values) {
                Debug.Assert(typeinfo.ExtensionType != t);
            }

            Debug.Assert(t != typeof(InstanceOps));
        }

        /// <summary>
        /// Creates the initial table of extension types.  These are standard extension that we apply
        /// to well known .NET types to make working with them better.  Being added to this table does
        /// not make a type a Python type though so that it's members are generally accessible w/o an
        /// import clr and their type is not re-named.
        /// </summary>
        private static Dictionary<Type/*!*/, IList<Type/*!*/>/*!*/>/*!*/ MakeExtensionTypes() {
            Dictionary<Type, IList<Type>> res = new Dictionary<Type, IList<Type>>();

#if FEATURE_DBNULL
            res[typeof(DBNull)] = new Type[] { typeof(DBNullOps) };
#endif
            res[typeof(List<>)] = new Type[] { typeof(ListOfTOps<>) };
            res[typeof(Dictionary<,>)] = new Type[] { typeof(DictionaryOfTOps<,>) };
            res[typeof(Array)] = new Type[] { typeof(ArrayOps) };
            res[typeof(Assembly)] = new Type[] { typeof(PythonAssemblyOps) };
            res[typeof(Enum)] = new Type[] { typeof(EnumOps) };
            res[typeof(Delegate)] = new Type[] { typeof(DelegateOps) };
            res[typeof(Byte)] = new Type[] { typeof(ByteOps) };
            res[typeof(SByte)] = new Type[] { typeof(SByteOps) };
            res[typeof(Int16)] = new Type[] { typeof(Int16Ops) };
            res[typeof(UInt16)] = new Type[] { typeof(UInt16Ops) };
            res[typeof(UInt32)] = new Type[] { typeof(UInt32Ops) };
            res[typeof(Int64)] = new Type[] { typeof(Int64Ops) };
            res[typeof(UInt64)] = new Type[] { typeof(UInt64Ops) };
            res[typeof(char)] = new Type[] { typeof(CharOps) };
            res[typeof(decimal)] = new Type[] { typeof(DecimalOps) };
            res[typeof(float)] = new Type[] { typeof(SingleOps) };
            
            return res;
        }

        /// <summary>
        /// Creates a table of standard .NET types which are also standard Python types.  These types have a standard
        /// set of extension types which are shared between all runtimes.
        /// </summary>
        private static Dictionary<Type/*!*/, ExtensionTypeInfo/*!*/>/*!*/ MakeSystemTypes() {
            Dictionary<Type/*!*/, ExtensionTypeInfo/*!*/> res = new Dictionary<Type, ExtensionTypeInfo>();

            // Native CLR types
            res[typeof(object)] = new ExtensionTypeInfo(typeof(ObjectOps), "object");
            res[typeof(string)] = new ExtensionTypeInfo(typeof(StringOps), "str");
            res[typeof(int)] = new ExtensionTypeInfo(typeof(Int32Ops), "int");
            res[typeof(bool)] = new ExtensionTypeInfo(typeof(BoolOps), "bool");
            res[typeof(double)] = new ExtensionTypeInfo(typeof(DoubleOps), "float");
            res[typeof(ValueType)] = new ExtensionTypeInfo(typeof(ValueType), "ValueType");   // just hiding it's methods in the inheritance hierarchy

            // MS.Math types
            res[typeof(BigInteger)] = new ExtensionTypeInfo(typeof(BigIntegerOps), "long");
            res[typeof(Complex)] = new ExtensionTypeInfo(typeof(ComplexOps), "complex");

            // DLR types
            res[typeof(DynamicNull)] = new ExtensionTypeInfo(typeof(NoneTypeOps), "NoneType");
            res[typeof(IDictionary<object, object>)] = new ExtensionTypeInfo(typeof(DictionaryOps), "dict");
            res[typeof(NamespaceTracker)] = new ExtensionTypeInfo(typeof(NamespaceTrackerOps), "namespace#");
            res[typeof(TypeGroup)] = new ExtensionTypeInfo(typeof(TypeGroupOps), "type-collision");
            res[typeof(TypeTracker)] = new ExtensionTypeInfo(typeof(TypeTrackerOps), "type-collision");

            return res;
        }

        internal static string GetTypeNameInternal(Type t) {
            ExtensionTypeInfo extInfo;
            if (_sysTypes.TryGetValue(t, out extInfo)) {
                return extInfo.PythonName;
            }

            var attr = t.GetCustomAttributes<PythonTypeAttribute>(false).FirstOrDefault();
            if (attr != null && attr.Name != null) {
                return attr.Name;
            }

            return t.Name;
        }

        public static bool IsExtendedType(Type/*!*/ t) {
            Debug.Assert(t != null);

            return _sysTypes.ContainsKey(t);
        }

        public static bool IsPythonType(Type/*!*/ t) {
            Debug.Assert(t != null);

            return _sysTypes.ContainsKey(t) || t.IsDefined(typeof(PythonTypeAttribute), false);
        }

        /// <summary>
        /// Event handler for when our domain manager has an assembly loaded by the user hosting the script
        /// runtime.  Here we can gather any information regarding extension methods.  
        /// 
        /// Currently DLR-style extension methods become immediately available w/o an explicit import step.
        /// </summary>
        private void DomainManager_AssemblyLoaded(object sender, AssemblyLoadedEventArgs e) {
            Assembly asm = e.Assembly;

            var attrs = asm.GetCustomAttributes<ExtensionTypeAttribute>();

            if (attrs.Any()) {
                lock (_dlrExtensionTypes) {
                    foreach (ExtensionTypeAttribute attr in attrs) {
                        if (attr.Extends.IsInterface) {
                            _registeredInterfaceExtensions = true;
                        }

                        IList<Type> typeList;
                        if (!_dlrExtensionTypes.TryGetValue(attr.Extends, out typeList)) {
                            _dlrExtensionTypes[attr.Extends] = typeList = new List<Type>();
                        } else if (typeList.IsReadOnly) {
                            _dlrExtensionTypes[attr.Extends] = typeList = new List<Type>(typeList);
                        }

                        // don't add extension types twice even if we receive multiple assembly loads
                        if (!typeList.Contains(attr.ExtensionType)) {
                            typeList.Add(attr.ExtensionType);
                        }
                    }
                }
            }

            TopNamespaceTracker.PublishComTypes(asm);

            // Add it to the references tuple if we
            // loaded a new assembly.
            ClrModule.ReferencesList rl = _context.ReferencedAssemblies;
            lock (rl) {
                rl.Add(asm);
            }

#if FEATURE_REFEMIT
            // load any compiled code that has been cached...
            LoadScriptCode(_context, asm);
#endif

            // load any Python modules
            _context.LoadBuiltins(_context.BuiltinModules, asm, true);

            // load any cached new types
            NewTypeMaker.LoadNewTypes(asm);
        }

#if FEATURE_REFEMIT
        private static void LoadScriptCode(PythonContext/*!*/ pc, Assembly/*!*/ asm) {
            ScriptCode[] codes = SavableScriptCode.LoadFromAssembly(pc.DomainManager, asm);

            foreach (ScriptCode sc in codes) {
                pc.GetCompiledLoader().AddScriptCode(sc);
            }
        }
#endif

        internal PythonContext/*!*/ Context {
            get {
                return _context;
            }
        }

        /// <summary>
        /// Provides a cache from Type/name -> PythonTypeSlot and also allows access to
        /// all members (and remembering whether all members are cached).
        /// </summary>
        private class SlotCache {
            private Dictionary<CachedInfoKey/*!*/, SlotCacheInfo/*!*/> _cachedInfos;

            /// <summary>
            /// Writes to a cache the result of a type lookup.  Null values are allowed for the slots and they indicate that
            /// the value does not exist.
            /// </summary>
            public void CacheSlot(Type/*!*/ type, bool isGetMember, string/*!*/ name, PythonTypeSlot slot, MemberGroup/*!*/ memberGroup) {
                Debug.Assert(type != null); Debug.Assert(name != null);

                EnsureInfo();

                lock (_cachedInfos) {
                    SlotCacheInfo slots = GetSlotForType(type, isGetMember);

                    if (slots.ResolvedAll && slot == null && memberGroup.Count == 0) {
                        // nothing to cache, and we know we don't need to cache non-hits.
                        return;
                    }

                    slots.Members[name] = new KeyValuePair<PythonTypeSlot, MemberGroup>(slot, memberGroup);
                }
            }

            /// <summary>
            /// Looks up a cached type slot for the specified member and type.  This may return true and return a null slot - that indicates
            /// that a cached result for a member which doesn't exist has been stored.  Otherwise it returns true if a slot is found or
            /// false if it is not.
            /// </summary>
            public bool TryGetCachedSlot(Type/*!*/ type, bool isGetMember, string/*!*/ name, out PythonTypeSlot slot) {
                Debug.Assert(type != null); Debug.Assert(name != null);

                if (_cachedInfos != null) {
                    lock (_cachedInfos) {
                        SlotCacheInfo slots;
                        if (_cachedInfos.TryGetValue(new CachedInfoKey(type, isGetMember), out slots) &&
                            (slots.TryGetSlot(name, out slot) || slots.ResolvedAll)) {
                            return true;
                        }
                    }
                }

                slot = null;
                return false;
            }

            /// <summary>
            /// Looks up a cached member group for the specified member and type.  This may return true and return a null group - that indicates
            /// that a cached result for a member which doesn't exist has been stored.  Otherwise it returns true if a group is found or
            /// false if it is not.
            /// </summary>
            public bool TryGetCachedMember(Type/*!*/ type, string/*!*/ name, bool getMemberAction, out MemberGroup/*!*/ group) {
                Debug.Assert(type != null); Debug.Assert(name != null);

                if (_cachedInfos != null) {
                    lock (_cachedInfos) {
                        SlotCacheInfo slots;
                        if (_cachedInfos.TryGetValue(new CachedInfoKey(type, getMemberAction), out slots) &&
                            (slots.TryGetMember(name, out group) || (getMemberAction && slots.ResolvedAll))) {
                            return true;
                        }
                    }
                }

                group = MemberGroup.EmptyGroup;
                return false;
            }

            /// <summary>
            /// Checks to see if all members have been populated for the provided type.
            /// </summary>
            public bool IsFullyCached(Type/*!*/ type, bool isGetMember) {
                if (_cachedInfos != null) {
                    lock (_cachedInfos) {
                        SlotCacheInfo info;
                        if (_cachedInfos.TryGetValue(new CachedInfoKey(type, isGetMember), out info)) {
                            return info.ResolvedAll;
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// Populates the type with all the provided members and marks the type 
            /// as being fully cached.
            /// 
            /// The dictionary is used for the internal storage and should not be modified after
            /// providing it to the cache.
            /// </summary>
            public void CacheAll(Type/*!*/ type, bool isGetMember, Dictionary<string/*!*/, KeyValuePair<PythonTypeSlot/*!*/, MemberGroup/*!*/>> members) {
                Debug.Assert(type != null);

                EnsureInfo();

                lock (_cachedInfos) {
                    SlotCacheInfo slots = GetSlotForType(type, isGetMember);

                    slots.Members = members;
                    slots.ResolvedAll = true;
                }
            }

            /// <summary>
            /// Returns an enumerable object which provides access to all the members of the provided type.
            /// 
            /// The caller must check that the type is fully cached and populate the cache if it isn't before
            /// calling this method.
            /// </summary>
            public IEnumerable<KeyValuePair<string/*!*/, PythonTypeSlot/*!*/>>/*!*/ GetAllMembers(Type/*!*/ type, bool isGetMember) {
                Debug.Assert(type != null);

                SlotCacheInfo info = GetSlotForType(type, isGetMember);
                Debug.Assert(info.ResolvedAll);

                foreach (KeyValuePair<string, PythonTypeSlot> slot in info.GetAllSlots()) {
                    if (slot.Value != null) {
                        yield return slot;
                    }
                }
            }

            private SlotCacheInfo/*!*/ GetSlotForType(Type/*!*/ type, bool isGetMember) {
                SlotCacheInfo slots;
                var key = new CachedInfoKey(type, isGetMember);
                if (!_cachedInfos.TryGetValue(key, out slots)) {
                    _cachedInfos[key] = slots = new SlotCacheInfo();
                }
                return slots;
            }

            private class CachedInfoKey : IEquatable<CachedInfoKey> {
                public readonly Type Type;
                public readonly bool IsGetMember;

                public CachedInfoKey(Type type, bool isGetMember) {
                    Type = type;
                    IsGetMember = isGetMember;
                }

                #region IEquatable<CachedInfoKey> Members

                public bool Equals(CachedInfoKey other) {
                    return other.Type == Type && other.IsGetMember == IsGetMember;
                }

                #endregion

                public override bool Equals(object obj) {
                    CachedInfoKey other = obj as CachedInfoKey;
                    if (other != null) {
                        return Equals(other);
                    }

                    return false;
                }

                public override int GetHashCode() {
                    return Type.GetHashCode() ^ (IsGetMember ? -1 : 0);
                }
            }
            private void EnsureInfo() {
                if (_cachedInfos == null) {
                    Interlocked.CompareExchange(ref _cachedInfos, new Dictionary<CachedInfoKey/*!*/, SlotCacheInfo>(), null);
                }
            }

            private class SlotCacheInfo {
                public SlotCacheInfo() {
                    Members = new Dictionary<string/*!*/, KeyValuePair<PythonTypeSlot, MemberGroup/*!*/>>(StringComparer.Ordinal);
                }               

                public bool TryGetSlot(string/*!*/ name, out PythonTypeSlot slot) {
                    Debug.Assert(name != null);

                    KeyValuePair<PythonTypeSlot, MemberGroup> kvp;
                    if (Members.TryGetValue(name, out kvp)) {
                        slot = kvp.Key;
                        return true;
                    }

                    slot = null;
                    return false;
                }

                public bool TryGetMember(string/*!*/ name, out MemberGroup/*!*/ group) {
                    Debug.Assert(name != null);

                    KeyValuePair<PythonTypeSlot, MemberGroup> kvp;
                    if (Members.TryGetValue(name, out kvp)) {
                        group = kvp.Value;
                        return true;
                    }

                    group = MemberGroup.EmptyGroup;
                    return false;
                }

                public IEnumerable<KeyValuePair<string/*!*/, PythonTypeSlot>>/*!*/ GetAllSlots() {
                    foreach (KeyValuePair<string, KeyValuePair<PythonTypeSlot, MemberGroup>> kvp in Members) {
                        yield return new KeyValuePair<string, PythonTypeSlot>(kvp.Key, kvp.Value.Key);
                    }
                }

                public Dictionary<string/*!*/, KeyValuePair<PythonTypeSlot, MemberGroup/*!*/>>/*!*/ Members;
                public bool ResolvedAll;
            }
        }
    }
}
