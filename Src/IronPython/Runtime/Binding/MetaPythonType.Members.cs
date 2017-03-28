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
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    partial class MetaPythonType : MetaPythonObject, IPythonGetable {

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindGetMember(GetMemberBinder/*!*/ member) {
            return GetMemberWorker(member, PythonContext.GetCodeContext(member));
        }

        private ValidationInfo GetTypeTest() {
            int version = Value.Version;

            return new ValidationInfo(
                Ast.Call(
                    typeof(PythonOps).GetMethod("CheckSpecificTypeVersion"),
                    AstUtils.Convert(Expression, typeof(PythonType)),
                    AstUtils.Constant(version)
                )
            );
        }

        public override DynamicMetaObject/*!*/ BindSetMember(SetMemberBinder/*!*/ member, DynamicMetaObject/*!*/ value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Type SetMember " + Value.UnderlyingSystemType.FullName);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Type SetMember");
            PythonContext state = PythonContext.GetPythonContext(member);

            if (Value.IsSystemType) {
                MemberTracker tt = MemberTracker.FromMemberInfo(Value.UnderlyingSystemType.GetTypeInfo());
                MemberGroup mg = state.Binder.GetMember(MemberRequestKind.Set, Value.UnderlyingSystemType, member.Name);

                // filter protected member access against .NET types, these can only be accessed from derived types...
                foreach (MemberTracker mt in mg) {
                    if (IsProtectedSetter(mt)) {
                        return new DynamicMetaObject(
                            BindingHelpers.TypeErrorForProtectedMember(Value.UnderlyingSystemType, member.Name),
                            Restrictions.Merge(value.Restrictions).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value))
                        );
                    }
                }

                // have the default binder perform it's operation against a TypeTracker and then
                // replace the test w/ our own.
                return new DynamicMetaObject(
                    state.Binder.SetMember(
                        member.Name,
                        new DynamicMetaObject(
                            AstUtils.Constant(tt),
                            BindingRestrictions.Empty,
                            tt
                        ),
                        value,
                        new PythonOverloadResolverFactory(state.Binder, AstUtils.Constant(state.SharedContext))
                    ).Expression,
                    Restrictions.Merge(value.Restrictions).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value))
                );
            }

            return MakeSetMember(member, value);
        }

        public override DynamicMetaObject/*!*/ BindDeleteMember(DeleteMemberBinder/*!*/ member) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Type DeleteMember " + Value.UnderlyingSystemType.FullName);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Type DeleteMember");
            if (Value.IsSystemType) {
                PythonContext state = PythonContext.GetPythonContext(member);

                MemberTracker tt = MemberTracker.FromMemberInfo(Value.UnderlyingSystemType.GetTypeInfo());

                // have the default binder perform it's operation against a TypeTracker and then
                // replace the test w/ our own.
                return new DynamicMetaObject(
                    state.Binder.DeleteMember(
                        member.Name,
                        new DynamicMetaObject(
                            AstUtils.Constant(tt),
                            BindingRestrictions.Empty,
                            tt
                        ),
                        state.SharedOverloadResolverFactory
                    ).Expression,
                    BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Restrictions)
                );
            }

            return MakeDeleteMember(member);
        }

        #endregion

        #region IPythonGetable Members

        public DynamicMetaObject/*!*/ GetMember(PythonGetMemberBinder/*!*/ member, DynamicMetaObject/*!*/ codeContext) {
            return GetMemberWorker(member, codeContext.Expression);
        }

        #endregion

        #region Gets

        private DynamicMetaObject/*!*/ GetMemberWorker(DynamicMetaObjectBinder/*!*/ member, Expression codeContext) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Type GetMember " + Value.UnderlyingSystemType.FullName);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Type GetMember");

            return new MetaGetBinderHelper(this, member, codeContext, GetTypeTest(), MakeMetaTypeTest(Restrict(this.GetRuntimeType()).Expression)).MakeTypeGetMember();
        }

        private ValidationInfo MakeMetaTypeTest(Expression self) {

            PythonType metaType = DynamicHelpers.GetPythonType(Value);
            if (!metaType.IsSystemType) {
                int version = metaType.Version;

                return new ValidationInfo(
                    Ast.Call(
                        typeof(PythonOps).GetMethod("CheckTypeVersion"),
                        self,
                        AstUtils.Constant(version)
                    )
                );
            }

            return ValidationInfo.Empty;
        }

        /// <summary>
        /// Base class for performing member binding.  Derived classes override Add methods
        /// to produce the actual final result based upon what the GetBinderHelper resolves.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        public abstract class GetBinderHelper<TResult> {
            private readonly PythonType _value;
            private readonly string _name;
            internal readonly CodeContext/*!*/ _context;

            public GetBinderHelper(PythonType value, CodeContext/*!*/ context, string name) {
                _value = value;
                _name = name;
                _context = context;
            }

            #region Abstract members

            protected abstract TResult Finish(bool metaOnly);

            protected abstract void AddError();

            protected abstract void AddMetaGetAttribute(PythonType metaType, PythonTypeSlot pts);

            protected abstract bool AddMetaSlotAccess(PythonType pt, PythonTypeSlot pts);

            protected abstract bool AddSlotAccess(PythonType pt, PythonTypeSlot pts);

            #endregion

            #region Common Get Code

            public TResult MakeTypeGetMember() {
                PythonTypeSlot pts;

                bool isFinal = false, metaOnly = false;
                CodeContext lookupContext = PythonContext.GetContext(_context).SharedClsContext;

                // first look in the meta-class to see if we have a get/set descriptor
                PythonType metaType = DynamicHelpers.GetPythonType(Value);
                foreach (PythonType pt in metaType.ResolutionOrder) {
                    if (pt.TryLookupSlot(lookupContext, _name, out pts) && pts.IsSetDescriptor(lookupContext, metaType)) {
                        if (AddMetaSlotAccess(metaType, pts)) {
                            metaOnly = isFinal = true;
                            break;
                        }
                    }
                }

                if (!isFinal) {
                    // then search the MRO to see if we have the value
                    foreach (PythonType pt in Value.ResolutionOrder) {
                        if (pt.TryLookupSlot(lookupContext, _name, out pts)) {
                            if (AddSlotAccess(pt, pts)) {
                                isFinal = true;
                                break;
                            }
                        }
                    }
                }

                if (!isFinal) {
                    // then go back to the meta class to see if we have a normal attribute
                    foreach (PythonType pt in metaType.ResolutionOrder) {
                        if (pt.TryLookupSlot(lookupContext, _name, out pts)) {
                            if (AddMetaSlotAccess(metaType, pts)) {
                                isFinal = true;
                                break;
                            }
                        }
                    }
                }

                if (!isFinal) {
                    // the member doesn't exist anywhere in the type hierarchy, see if
                    // we define __getattr__ on our meta type.
                    if (metaType.TryResolveSlot(_context, "__getattr__", out pts) && 
                        !pts.IsSetDescriptor(lookupContext, metaType)) { // we tried get/set descriptors initially

                        AddMetaGetAttribute(metaType, pts);
                        isFinal = pts.GetAlwaysSucceeds;
                    }
                }

                if (!isFinal) {
                    AddError();
                }

                return Finish(metaOnly);
            }

            #endregion

            protected PythonType Value {
                get {
                    return _value;
                }
            }
        }

        /// <summary>
        /// Provides the normal meta binder binding.
        /// </summary>
        class MetaGetBinderHelper : GetBinderHelper<DynamicMetaObject> {
            private readonly DynamicMetaObjectBinder _member;
            private readonly MetaPythonType _type;
            private readonly Expression _codeContext;
            private readonly DynamicMetaObject _restrictedSelf;
            private readonly ConditionalBuilder _cb;
            private readonly string _symName;
            private readonly PythonContext _state;
            private readonly ValidationInfo _valInfo, _metaValInfo;
            private ParameterExpression _tmp;

            public MetaGetBinderHelper(MetaPythonType type, DynamicMetaObjectBinder member, Expression codeContext, ValidationInfo validationInfo, ValidationInfo metaValidation)
                : base(type.Value, PythonContext.GetPythonContext(member).SharedContext, GetGetMemberName(member)) {
                _member = member;
                _codeContext = codeContext;
                _type = type;
                _cb = new ConditionalBuilder(member);
                _symName = GetGetMemberName(member);
                _restrictedSelf = new DynamicMetaObject(
                    AstUtils.Convert(Expression, Value.GetType()),
                    Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)),
                    Value
                );
                _state = PythonContext.GetPythonContext(member);
                _valInfo = validationInfo;
                _metaValInfo = metaValidation;
            }            

            private void EnsureTmp() {
                if (_tmp == null) {
                    _tmp = Ast.Variable(typeof(object), "tmp");
                    _cb.AddVariable(_tmp);
                }
            }

            protected override bool AddSlotAccess(PythonType pt, PythonTypeSlot pts) {
                pts.MakeGetExpression(
                        _state.Binder,
                        _codeContext,
                        null,
                        new DynamicMetaObject(
                            AstUtils.Convert(AstUtils.WeakConstant(Value), typeof(PythonType)),
                            BindingRestrictions.Empty,
                            Value
                        ),
                        _cb
                    );

                if (!pts.IsAlwaysVisible) {
                    _cb.ExtendLastCondition(Ast.Call(typeof(PythonOps).GetMethod("IsClsVisible"), _codeContext));
                    return false;
                }

                return pts.GetAlwaysSucceeds;
            }


            protected override void AddError() {
                // TODO: We should preserve restrictions from the error
                _cb.FinishCondition(GetFallbackError(_member).Expression);
            }

            protected override void AddMetaGetAttribute(PythonType metaType, PythonTypeSlot pts) {
                EnsureTmp();

                // implementation similar to PythonTypeSlot.MakeGetExpression()

                Expression getExpr = Ast.Call(
                    typeof(PythonOps).GetMethod("SlotTryGetBoundValue"),
                    _codeContext,
                    AstUtils.Constant(pts, typeof(PythonTypeSlot)),
                    Expression,
                    AstUtils.Constant(metaType),
                    _tmp
                );
                DynamicExpression invokeExpr = DynamicExpression.Dynamic(
                    _state.InvokeOne,
                    typeof(object),
                    _codeContext,
                    _tmp,
                    AstUtils.Constant(GetGetMemberName(_member))
                );

                if (!pts.GetAlwaysSucceeds) {
                    _cb.AddCondition(getExpr, invokeExpr);
                } else {
                    _cb.FinishCondition(Ast.Block(getExpr, invokeExpr));
                }
            }

            protected override bool AddMetaSlotAccess(PythonType metaType, PythonTypeSlot pts) {
                pts.MakeGetExpression(
                    _state.Binder,
                    _codeContext,
                    _type,
                    new DynamicMetaObject(
                        AstUtils.Constant(metaType),
                        BindingRestrictions.Empty,
                        metaType
                    ),
                    _cb
                );

                if (!pts.IsAlwaysVisible) {
                    _cb.ExtendLastCondition(Ast.Call(typeof(PythonOps).GetMethod("IsClsVisible"), _codeContext));
                    return false;
                }

                return pts.GetAlwaysSucceeds;
            }


            protected override DynamicMetaObject/*!*/ Finish(bool metaOnly) {
                DynamicMetaObject res = _cb.GetMetaObject(_restrictedSelf);

                if (metaOnly) {
                    res = BindingHelpers.AddDynamicTestAndDefer(
                        _member,
                        res,
                        new DynamicMetaObject[] { _type },
                        _metaValInfo
                    );
                } else if (!Value.IsSystemType) {
                    res = BindingHelpers.AddDynamicTestAndDefer(
                        _member,
                        res,
                        new DynamicMetaObject[] { _type },
                        _valInfo
                    );
                }

                return res;
            }

            private DynamicMetaObject/*!*/ GetFallbackError(DynamicMetaObjectBinder/*!*/ member) {
                if (member is PythonGetMemberBinder) {
                    // accessing from Python, produce our error
                    PythonGetMemberBinder pb = member as PythonGetMemberBinder;
                    if (pb.IsNoThrow) {
                        return new DynamicMetaObject(
                            Expression.Constant(OperationFailed.Value),
                            BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Restrictions)
                        );
                    } else {
                        return new DynamicMetaObject(
                            member.Throw(
                                Ast.Call(
                                    typeof(PythonOps).GetMethod(
                                        "AttributeErrorForMissingAttribute",
                                        new Type[] { typeof(string), typeof(string) }
                                    ),
                                    AstUtils.Constant(DynamicHelpers.GetPythonType(Value).Name),
                                    AstUtils.Constant(pb.Name)
                                ),
                                typeof(object)
                            ),
                            BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Restrictions)
                        );
                    }
                }

                // let the calling language bind the .NET members
                return ((GetMemberBinder)member).FallbackGetMember(_type);
            }

            private Expression/*!*/ Expression {
                get {
                    return _type.Expression;
                }
            }

            private BindingRestrictions Restrictions {
                get {
                    return _type.Restrictions;
                }
            }


        }

        /// <summary>
        /// Provides delegate based fast binding.
        /// </summary>
        internal class FastGetBinderHelper : GetBinderHelper<TypeGetBase> {
            private readonly PythonGetMemberBinder _binder;
            private readonly int _version;
            private readonly int _metaVersion;
            private bool _canOptimize;
            private List<FastGetDelegate> _gets = new List<FastGetDelegate>();

            public FastGetBinderHelper(PythonType type, CodeContext context, PythonGetMemberBinder binder)
                : base(type, context, binder.Name) {
                // capture these before we start producing the result
                _version = type.Version;
                _metaVersion = DynamicHelpers.GetPythonType(type).Version;
                _binder = binder;
            }

            public Func<CallSite, object, CodeContext, object> GetBinding() {
                Dictionary<string, TypeGetBase> cachedGets = GetCachedGets();

                TypeGetBase dlg;
                lock (cachedGets) {
                    if (!cachedGets.TryGetValue(_binder.Name, out dlg) || !dlg.IsValid(Value)) {
                        var binding = MakeTypeGetMember();
                        if (binding != null) {
                            dlg = cachedGets[_binder.Name] = binding;
                        }
                    }
                }

                if (dlg != null && dlg.ShouldUseNonOptimizedSite) {
                    return dlg._func;
                }
                return null;
            }

            private Dictionary<string, TypeGetBase> GetCachedGets() {
                if (_binder.IsNoThrow) {
                    Dictionary<string, TypeGetBase> cachedGets = Value._cachedTypeTryGets;
                    if (cachedGets == null) {
                        Interlocked.CompareExchange(
                            ref Value._cachedTypeTryGets,
                            new Dictionary<string, TypeGetBase>(),
                            null);

                        cachedGets = Value._cachedTypeTryGets;
                    }
                    return cachedGets;
                } else {
                    Dictionary<string, TypeGetBase> cachedGets = Value._cachedTypeGets;
                    if (cachedGets == null) {
                        Interlocked.CompareExchange(
                            ref Value._cachedTypeGets,
                            new Dictionary<string, TypeGetBase>(),
                            null);

                        cachedGets = Value._cachedTypeGets;
                    }
                    return cachedGets;
                }
            }

            protected override bool AddSlotAccess(PythonType pt, PythonTypeSlot pts) {
                if (pts.CanOptimizeGets) {
                    _canOptimize = true;
                }

                if (pts.IsAlwaysVisible) {
                    _gets.Add(new SlotAccessDelegate(pts, Value).Target);
                    return pts.GetAlwaysSucceeds;
                } else {
                    _gets.Add(new SlotAccessDelegate(pts, Value).TargetCheckCls);
                    return false;
                }
            }

            class SlotAccessDelegate {
                private readonly PythonTypeSlot _slot;
                private readonly PythonType _owner;
                private readonly WeakReference _weakOwner;
                private readonly WeakReference _weakSlot;

                public SlotAccessDelegate(PythonTypeSlot slot, PythonType owner) {
                    if (owner.IsSystemType) {
                        _owner = owner;
                        _slot = slot;
                    } else {
                        _weakOwner = owner.GetSharedWeakReference();
                        _weakSlot = new WeakReference(slot);
                    }
                }

                public bool TargetCheckCls(CodeContext context, object self, out object result) {
                    if (PythonOps.IsClsVisible(context)) {
                        return Slot.TryGetValue(context, null, Type, out result);
                    }

                    result = null;
                    return false;
                }

                public bool Target(CodeContext context, object self, out object result) {
                    return Slot.TryGetValue(context, null, Type, out result);
                }

                public bool MetaTargetCheckCls(CodeContext context, object self, out object result) {
                    if (PythonOps.IsClsVisible(context)) {
                        return Slot.TryGetValue(context, self, Type, out result);
                    }

                    result = null;
                    return false;
                }

                public bool MetaTarget(CodeContext context, object self, out object result) {
                    return Slot.TryGetValue(context, self, Type, out result);
                }

                private PythonType Type {
                    get {
                        return _owner ?? (PythonType)_weakOwner.Target;
                    }
                }

                private PythonTypeSlot Slot {
                    get {
                        return _slot ?? (PythonTypeSlot)_weakSlot.Target;
                    }
                }
            }

            protected override void AddError() {
                if (_binder.IsNoThrow) {
                    _gets.Add(new ErrorBinder(_binder.Name).TargetNoThrow);
                } else {
                    _gets.Add(new ErrorBinder(_binder.Name).Target);
                }
            }

            protected override void AddMetaGetAttribute(PythonType metaType, PythonTypeSlot pts) {
                _gets.Add(new MetaGetAttributeDelegate(_context, pts, metaType, _binder.Name).Target);
            }

            class MetaGetAttributeDelegate {
                private readonly string _name;
                private readonly PythonType _metaType;
                private readonly WeakReference _weakMetaType;
                private readonly PythonTypeSlot _slot;
                private readonly WeakReference _weakSlot;
                private readonly CallSite<Func<CallSite, CodeContext, object, string, object>> _invokeSite;

                public MetaGetAttributeDelegate(CodeContext context, PythonTypeSlot slot, PythonType metaType, string name) {
                    _name = name;

                    if (metaType.IsSystemType) {
                        _metaType = metaType;
                        _slot = slot;
                    } else {
                        _weakMetaType = metaType.GetSharedWeakReference();
                        _weakSlot = new WeakReference(slot);
                    }
                    _invokeSite = CallSite<Func<CallSite, CodeContext, object, string, object>>.Create(PythonContext.GetContext(context).InvokeOne);
                }

                public bool Target(CodeContext context, object self, out object result) {
                    object value;

                    if (Slot.TryGetValue(context, self, MetaType, out value)) {
                        result = _invokeSite.Target(_invokeSite, context, value, _name);
                        return true;
                    }

                    result = null;
                    return false;
                }

                private PythonType MetaType {
                    get {
                        return _metaType ?? (PythonType)_weakMetaType.Target;
                    }
                }

                private PythonTypeSlot Slot {
                    get {
                        return _slot ?? (PythonTypeSlot)_weakSlot.Target;
                    }
                }
            }

            protected override bool AddMetaSlotAccess(PythonType metaType, PythonTypeSlot pts) {
                if (pts.CanOptimizeGets) {
                    _canOptimize = true;
                }

                if (pts.IsAlwaysVisible) {
                    _gets.Add(new SlotAccessDelegate(pts, metaType).MetaTarget);
                    return pts.GetAlwaysSucceeds;
                } else {
                    _gets.Add(new SlotAccessDelegate(pts, metaType).MetaTargetCheckCls);
                    return false;
                }
            }


            protected override TypeGetBase/*!*/ Finish(bool metaOnly) {
                if (metaOnly) {
                    if (DynamicHelpers.GetPythonType(Value).IsSystemType) {
                        return new SystemTypeGet(_binder, _gets.ToArray(), Value, metaOnly, _canOptimize);
                    } else {
                        return new TypeGet(_binder, _gets.ToArray(), metaOnly ? _metaVersion : _version, metaOnly, _canOptimize);
                    }
                } else {
                    if (Value.IsSystemType) {
                        return new SystemTypeGet(_binder, _gets.ToArray(), Value, metaOnly, _canOptimize);
                    }
                    return new TypeGet(_binder, _gets.ToArray(), metaOnly ? _metaVersion : _version, metaOnly, _canOptimize);
                }
            }

            class ErrorBinder {
                private readonly string _name;

                public ErrorBinder(string name) {
                    _name = name;
                }

                public bool TargetNoThrow(CodeContext context, object self, out object result) {
                    result = OperationFailed.Value;
                    return true;
                }

                public bool Target(CodeContext context, object self, out object result) {
                    throw PythonOps.AttributeErrorForObjectMissingAttribute(self, _name);
                }
            }
        }

        #endregion

        #region Sets

        private DynamicMetaObject/*!*/ MakeSetMember(SetMemberBinder/*!*/ member, DynamicMetaObject/*!*/ value) {
            PythonContext state = PythonContext.GetPythonContext(member);
            DynamicMetaObject self = Restrict(Value.GetType());

            if (Value.GetType() != typeof(PythonType) && DynamicHelpers.GetPythonType(Value).IsSystemType) {
                // built-in subclass of .NET type.  Usually __setattr__ is handled by MetaUserObject
                // but we can have a built-in subtype that's not a user type.
                PythonTypeSlot pts;
                if (Value.TryGetCustomSetAttr(state.SharedContext, out pts)) {

                    Debug.Assert(pts.GetAlwaysSucceeds);

                    ParameterExpression tmp = Ast.Variable(typeof(object), "boundVal");

                    return BindingHelpers.AddDynamicTestAndDefer(
                        member,
                        new DynamicMetaObject(
                            Ast.Block(
                                new[] { tmp },
                                DynamicExpression.Dynamic(
                                    state.Invoke(new CallSignature(2)),
                                    typeof(object),
                                    AstUtils.Constant(state.SharedContext),
                                    Ast.Block(
                                        Ast.Call(
                                            typeof(PythonOps).GetMethod("SlotTryGetValue"),
                                            AstUtils.Constant(state.SharedContext),
                                            AstUtils.Convert(AstUtils.WeakConstant(pts), typeof(PythonTypeSlot)),
                                            AstUtils.Convert(Expression, typeof(object)),
                                            AstUtils.Convert(AstUtils.WeakConstant(DynamicHelpers.GetPythonType(Value)), typeof(PythonType)),
                                            tmp
                                        ),
                                        tmp
                                    ),
                                    Ast.Constant(member.Name),
                                    value.Expression
                                )
                            ),
                            self.Restrictions
                        ),
                        new DynamicMetaObject[] { this, value },
                        TestUserType()
                    );
                }
            }

            return BindingHelpers.AddDynamicTestAndDefer(
                member,
                new DynamicMetaObject(
                    Ast.Call(
                        typeof(PythonOps).GetMethod("PythonTypeSetCustomMember"),
                        AstUtils.Constant(PythonContext.GetPythonContext(member).SharedContext),
                        self.Expression,
                        AstUtils.Constant(member.Name),
                        AstUtils.Convert(
                            value.Expression,
                            typeof(object)
                        )
                    ),
                    self.Restrictions.Merge(value.Restrictions)
                ),
                new DynamicMetaObject[] { this, value },
                TestUserType()
            );
        }

        private static bool IsProtectedSetter(MemberTracker mt) {
            PropertyTracker pt = mt as PropertyTracker;
            if (pt != null) {
                MethodInfo mi = pt.GetSetMethod(true);
                if (mi != null && mi.IsProtected()) {
                    return true;
                }
            }

            FieldTracker ft = mt as FieldTracker;
            if (ft != null) {
                return ft.Field.IsProtected();
            }

            return false;
        }

        #endregion

        #region Deletes

        private DynamicMetaObject/*!*/ MakeDeleteMember(DeleteMemberBinder/*!*/ member) {
            DynamicMetaObject self = Restrict(Value.GetType());
            return BindingHelpers.AddDynamicTestAndDefer(
                member,
                new DynamicMetaObject(
                    Ast.Call(
                        typeof(PythonOps).GetMethod("PythonTypeDeleteCustomMember"),
                        AstUtils.Constant(PythonContext.GetPythonContext(member).SharedContext),
                        self.Expression,
                        AstUtils.Constant(member.Name)
                    ),
                    self.Restrictions
                ),
                new DynamicMetaObject[] { this },
                TestUserType()
            );
        }

        #endregion

        #region Helpers

        private ValidationInfo/*!*/ TestUserType() {
            return new ValidationInfo(
                Ast.Not(
                    Ast.Call(
                        typeof(PythonOps).GetMethod("IsPythonType"),
                        AstUtils.Convert(
                            Expression,
                            typeof(PythonType)
                        )
                    )
                )
            );
        }

        #endregion
    }
}
