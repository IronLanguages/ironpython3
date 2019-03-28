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
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using System.Threading;
using System.Collections.Generic;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    partial class MetaUserObject : MetaPythonObject, IPythonGetable {
        #region IPythonGetable Members

        public DynamicMetaObject GetMember(PythonGetMemberBinder/*!*/ member, DynamicMetaObject/*!*/ codeContext) {
            return GetMemberWorker(member, codeContext);
        }        

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindGetMember(GetMemberBinder/*!*/ action) {
            return GetMemberWorker(action, PythonContext.GetCodeContextMO(action));
        }

        public override DynamicMetaObject/*!*/ BindSetMember(SetMemberBinder/*!*/ action, DynamicMetaObject/*!*/ value) {
            return new MetaSetBinderHelper(this, value, action).Bind(action.Name);
        }

        public override DynamicMetaObject/*!*/ BindDeleteMember(DeleteMemberBinder/*!*/ action) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "DeleteMember");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "DeleteMember");
            return MakeDeleteMemberRule(
                new DeleteBindingInfo(
                    action,
                    new DynamicMetaObject[] { this },
                    new ConditionalBuilder(action),
                    BindingHelpers.GetValidationInfo(this, PythonType)
                )
            );
        }

        #endregion

        #region Get Member Helpers

        /// <summary>
        /// Provides the lookup logic for resolving a Python object.  Subclasses
        /// provide the actual logic for producing the binding result.  Currently
        /// there are two forms of the binding result: one is the DynamicMetaObject
        /// form used for non-optimized bindings.  The other is the Func of CallSite,
        /// object, CodeContext, object form which is used for fast binding and
        /// pre-compiled rules.
        /// </summary>
        internal abstract class GetOrInvokeBinderHelper<TResult> {
            protected readonly IPythonObject _value;
            protected bool _extensionMethodRestriction;

            public GetOrInvokeBinderHelper(IPythonObject value) {
                _value = value;
            }

            public TResult Bind(CodeContext context, string name) {
                IPythonObject sdo = Value;

                PythonTypeSlot foundSlot;
                if (TryGetGetAttribute(context, sdo.PythonType, out foundSlot)) {
                    return BindGetAttribute(foundSlot);
                }

                // otherwise look the object according to Python rules:
                //  1. 1st search the MRO of the type, and if it's there, and it's a get/set descriptor,
                //      return that value.
                //  2. Look in the instance dictionary.  If it's there return that value, otherwise return
                //      a value found during the MRO search.  If no value was found during the MRO search then
                //      raise an exception.      
                //  3. fall back to __getattr__ if defined.
                //
                // Ultimately we cache the result of the MRO search based upon the type version.  If we have
                // a get/set descriptor we'll only ever use that directly.  Otherwise if we have a get descriptor
                // we'll first check the dictionary and then invoke the get descriptor.  If we have no descriptor
                // at all we'll just check the dictionary.  If both lookups fail we'll raise an exception.

                bool systemTypeResolution, extensionMethodResolution;
                foundSlot = FindSlot(context, name, sdo, out systemTypeResolution, out extensionMethodResolution);
                _extensionMethodRestriction = extensionMethodResolution;

                if (sdo.PythonType.HasDictionary && (foundSlot == null || !foundSlot.IsSetDescriptor(context, sdo.PythonType))) {
                    MakeDictionaryAccess();
                }

                if (foundSlot != null) {
                    MakeSlotAccess(foundSlot, systemTypeResolution);
                }

                if (!IsFinal) {
                    // fall back to __getattr__ if it's defined.
                    // TODO: For InvokeMember we should probably do a fallback w/ an error suggestion
                    if (Value.PythonType.TryResolveSlot(context, "__getattr__", out PythonTypeSlot getattr)) {
                        MakeGetAttrAccess(getattr);
                    }

                    MakeTypeError();
                }


                return FinishRule();
            }

            protected abstract void MakeTypeError();
            protected abstract void MakeGetAttrAccess(PythonTypeSlot getattr);
            protected abstract bool IsFinal { get; }
            protected abstract void MakeSlotAccess(PythonTypeSlot foundSlot, bool systemTypeResolution);
            protected abstract TResult BindGetAttribute(PythonTypeSlot foundSlot);
            protected abstract TResult FinishRule();
            protected abstract void MakeDictionaryAccess();
        
            public IPythonObject Value {
                get {
                    return _value;
                }
            }
        }

        /// <summary>
        /// GetBinder which produces a DynamicMetaObject.  This binder always
        /// successfully produces a DynamicMetaObject which can perform the requested get.
        /// </summary>
        abstract class MetaGetBinderHelper : GetOrInvokeBinderHelper<DynamicMetaObject> {
            private readonly DynamicMetaObject _self;
            private readonly GetBindingInfo _bindingInfo;
            protected readonly MetaUserObject _target;
            private readonly DynamicMetaObjectBinder _binder;
            protected readonly DynamicMetaObject _codeContext;
            private string _resolution = "GetMember ";

            public MetaGetBinderHelper(MetaUserObject target, DynamicMetaObjectBinder binder, DynamicMetaObject codeContext)
                : base(target.Value) {
                _target = target;
                _self = _target.Restrict(Value.GetType());
                _binder = binder;
                _codeContext = codeContext;
                _bindingInfo = new GetBindingInfo(
                    _binder,
                    new DynamicMetaObject[] { _target },
                    Ast.Variable(Expression.Type, "self"),
                    Ast.Variable(typeof(object), "lookupRes"),
                    new ConditionalBuilder(_binder),
                    BindingHelpers.GetValidationInfo(_self, Value.PythonType)
                );
            }

            /// <summary>
            /// Makes a rule which calls a user-defined __getattribute__ function and falls back to __getattr__ if that
            /// raises an AttributeError.
            /// 
            /// slot is the __getattribute__ method to be called.
            /// </summary>
            private DynamicMetaObject/*!*/ MakeGetAttributeRule(GetBindingInfo/*!*/ info, IPythonObject/*!*/ obj, PythonTypeSlot/*!*/ slot, DynamicMetaObject codeContext) {
                // if the type implements IDynamicMetaObjectProvider and we picked up it's __getattribute__ then we want to just 
                // dispatch to the base meta object (or to the default binder). an example of this is:
                //
                // class mc(type):
                //     def __getattr__(self, name):
                //          return 42
                //
                // class nc_ga(object):
                //     __metaclass__ = mc
                //
                // a = nc_ga.x # here we want to dispatch to the type's rule, not call __getattribute__ directly.

                CodeContext context = PythonContext.GetPythonContext(info.Action).SharedContext;
                Type finalType = obj.PythonType.FinalSystemType;
                if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(finalType)) {
                    PythonTypeSlot baseSlot;
                    if (TryGetGetAttribute(context, DynamicHelpers.GetPythonTypeFromType(finalType), out baseSlot) && baseSlot == slot) {
                        return FallbackError();
                    }
                }

                // otherwise generate code into a helper function.  This will do the slot lookup and exception
                // handling for both __getattribute__ as well as __getattr__ if it exists.
                PythonTypeSlot getattr;
                obj.PythonType.TryResolveSlot(context, "__getattr__", out getattr);
                DynamicMetaObject self = _target.Restrict(Value.GetType());
                string methodName = BindingHelpers.IsNoThrow(info.Action) ? "GetAttributeNoThrow" : "GetAttribute";

                return BindingHelpers.AddDynamicTestAndDefer(
                    info.Action,
                    new DynamicMetaObject(
                        Ast.Call(
                            typeof(UserTypeOps).GetMethod(methodName),
                            Ast.Constant(PythonContext.GetPythonContext(info.Action).SharedContext),
                            info.Args[0].Expression,
                            Ast.Constant(GetGetMemberName(info.Action)),
                            Ast.Constant(slot, typeof(PythonTypeSlot)),
                            Ast.Constant(getattr, typeof(PythonTypeSlot)),
                            Ast.Constant(new SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, string, object>>>())
                        ),
                        self.Restrictions
                    ),
                    info.Args,
                    info.Validation
                );
            }
            
            protected abstract DynamicMetaObject FallbackError();
            protected abstract DynamicMetaObject Fallback();

            protected virtual Expression Invoke(Expression res) {
                return Invoke(new DynamicMetaObject(res, BindingRestrictions.Empty)).Expression;
            }

            protected virtual DynamicMetaObject Invoke(DynamicMetaObject res) {
                return res;
            }

            protected override DynamicMetaObject BindGetAttribute(PythonTypeSlot foundSlot) {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "User GetAttribute");
                PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "User GetAttribute");
                return Invoke(MakeGetAttributeRule(_bindingInfo, Value, foundSlot, _codeContext));
            }

            protected override void MakeGetAttrAccess(PythonTypeSlot getattr) {
                _resolution += "GetAttr ";
                MakeGetAttrRule(_bindingInfo, GetWeakSlot(getattr), _codeContext);
            }

            protected override void MakeTypeError() {
                _bindingInfo.Body.FinishCondition(FallbackError().Expression);
            }

            protected override bool IsFinal {
                get { return _bindingInfo.Body.IsFinal; }
            }

            protected override DynamicMetaObject FinishRule() {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, _resolution);
                PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "UserGet");

                DynamicMetaObject res = _bindingInfo.Body.GetMetaObject(_target);
                res = new DynamicMetaObject(
                    Ast.Block(
                        new ParameterExpression[] { _bindingInfo.Self, _bindingInfo.Result },
                        Ast.Assign(_bindingInfo.Self, _self.Expression),
                        res.Expression
                    ),
                    _self.Restrictions.Merge(res.Restrictions)
                );

                if (_extensionMethodRestriction) {
                    res = new DynamicMetaObject(
                        res.Expression,
                        res.Restrictions.Merge(((CodeContext)_codeContext.Value).ModuleContext.ExtensionMethods.GetRestriction(_codeContext.Expression))
                    );
                }

                return BindingHelpers.AddDynamicTestAndDefer(
                    _binder,
                    res,
                    new DynamicMetaObject[] { _target },
                    _bindingInfo.Validation
                );
            }

            private void MakeGetAttrRule(GetBindingInfo/*!*/ info, Expression/*!*/ getattr, DynamicMetaObject codeContext) {
                info.Body.AddCondition(
                    MakeGetAttrTestAndGet(info, getattr),
                    Invoke(MakeGetAttrCall(info, codeContext))
                );
            }

            private Expression/*!*/ MakeGetAttrCall(GetBindingInfo/*!*/ info, DynamicMetaObject codeContext) {
                Expression call = Ast.Dynamic(
                    PythonContext.GetPythonContext(info.Action).InvokeOne,
                    typeof(object),
                    PythonContext.GetCodeContext(info.Action),
                    info.Result,
                    Ast.Constant(GetGetMemberName(info.Action))
                );

                call = MaybeMakeNoThrow(info, call);

                return call;
            }

            private Expression/*!*/ MaybeMakeNoThrow(GetBindingInfo/*!*/ info, Expression/*!*/ expr) {
                if (BindingHelpers.IsNoThrow(info.Action)) {
                    DynamicMetaObject fallback = FallbackError();
                    ParameterExpression tmp = Ast.Variable(typeof(object), "getAttrRes");

                    expr = Ast.Block(
                        new ParameterExpression[] { tmp },
                        Ast.Block(
                            AstUtils.Try(
                                Ast.Assign(tmp, AstUtils.Convert(expr, typeof(object)))
                            ).Catch(
                                typeof(MissingMemberException),
                                Ast.Assign(tmp, AstUtils.Convert(fallback.Expression, typeof(object)))
                            ),
                            tmp
                        )
                    );
                }
                return expr;
            }

            protected override void MakeSlotAccess(PythonTypeSlot foundSlot, bool systemTypeResolution) {
                _resolution += CompilerHelpers.GetType(foundSlot) + " ";

                if (systemTypeResolution) {
                    _bindingInfo.Body.FinishCondition(Fallback().Expression);
                } else {
                    MakeSlotAccess(foundSlot);
                }
            }
                        
            private void MakeSlotAccess(PythonTypeSlot dts) {
                ReflectedSlotProperty rsp = dts as ReflectedSlotProperty;
                if (rsp != null) {
                    // we need to fall back to __getattr__ if the value is not defined, so call it and check the result.
                    _bindingInfo.Body.AddCondition(
                        Ast.NotEqual(
                            Ast.Assign(
                                _bindingInfo.Result,
                                Ast.ArrayAccess(
                                    GetSlots(_target),
                                    Ast.Constant(rsp.Index)
                                )
                            ),
                            Ast.Field(null, typeof(Uninitialized).GetField(nameof(Uninitialized.Instance)))
                        ),
                        Invoke(_bindingInfo.Result)
                    );
                    return;
                }

                PythonTypeUserDescriptorSlot slot = dts as PythonTypeUserDescriptorSlot;
                if (slot != null) {
                    _bindingInfo.Body.FinishCondition(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.GetUserSlotValue)),
                            Ast.Constant(PythonContext.GetPythonContext(_bindingInfo.Action).SharedContext),
                            Ast.Convert(AstUtils.WeakConstant(slot), typeof(PythonTypeUserDescriptorSlot)),
                            _target.Expression,
                            Ast.Property(
                                Ast.Convert(
                                    _bindingInfo.Self,
                                    typeof(IPythonObject)),
                                PythonTypeInfo._IPythonObject.PythonType
                            )
                        )
                    );
                }

                // users can subclass PythonProperty so check the type explicitly 
                // and only in-line the ones we fully understand.
                if (dts.GetType() == typeof(PythonProperty)) {
                    // properties are mutable so we generate code to get the value rather
                    // than burning it into the rule.
                    Expression getter = Ast.Property(
                        Ast.Convert(AstUtils.WeakConstant(dts), typeof(PythonProperty)),
                        "fget"
                    );
                    ParameterExpression tmpGetter = Ast.Variable(typeof(object), "tmpGet");
                    _bindingInfo.Body.AddVariable(tmpGetter);

                    _bindingInfo.Body.FinishCondition(
                        Ast.Block(
                            Ast.Assign(tmpGetter, getter),
                            Ast.Condition(
                                Ast.NotEqual(
                                    tmpGetter,
                                    Ast.Constant(null)
                                ),
                                Invoke(
                                    Ast.Dynamic(
                                        PythonContext.GetPythonContext(_bindingInfo.Action).InvokeOne,
                                        typeof(object),
                                        Ast.Constant(PythonContext.GetPythonContext(_bindingInfo.Action).SharedContext),
                                        tmpGetter,
                                        _bindingInfo.Self
                                    )
                                ),
                                _binder.Throw(Ast.Call(typeof(PythonOps).GetMethod(nameof(PythonOps.UnreadableProperty))), typeof(object))
                            )
                        )
                    );
                    return;
                }

                Expression tryGet = Ast.Call(
                    PythonTypeInfo._PythonOps.SlotTryGetBoundValue,
                    Ast.Constant(PythonContext.GetPythonContext(_bindingInfo.Action).SharedContext),
                    Ast.Convert(AstUtils.WeakConstant(dts), typeof(PythonTypeSlot)),
                    AstUtils.Convert(_bindingInfo.Self, typeof(object)),
                    Ast.Property(
                        Ast.Convert(
                            _bindingInfo.Self,
                            typeof(IPythonObject)),
                        PythonTypeInfo._IPythonObject.PythonType
                    ),
                    _bindingInfo.Result
                );

                Expression value = Invoke(_bindingInfo.Result);
                if (dts.GetAlwaysSucceeds) {
                    _bindingInfo.Body.FinishCondition(
                        Ast.Block(tryGet, value)
                    );
                } else {
                    _bindingInfo.Body.AddCondition(
                        tryGet,
                        value
                    );
                }
            }

            protected override void MakeDictionaryAccess() {
                _resolution += "Dictionary ";

                FieldInfo fi = _target.LimitType.GetField(NewTypeMaker.DictFieldName);
                Expression dict;
                if (fi != null) {
                    dict = Ast.Field(
                        Ast.Convert(_bindingInfo.Self, _target.LimitType),
                        fi
                    );
                } else {
                    dict = Ast.Property(
                        Ast.Convert(_bindingInfo.Self, typeof(IPythonObject)),
                        PythonTypeInfo._IPythonObject.Dict
                    );
                }

                var instanceNames = Value.PythonType.GetOptimizedInstanceNames();
                int instanceIndex;
                if (instanceNames != null && (instanceIndex = instanceNames.IndexOf(GetGetMemberName(_bindingInfo.Action))) != -1) {
                    // optimized instance value access
                    _bindingInfo.Body.AddCondition(
                        Ast.Call(
                            typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.TryGetDictionaryValue)),
                            dict,
                            AstUtils.Constant(GetGetMemberName(_bindingInfo.Action)),
                            Ast.Constant(Value.PythonType.GetOptimizedInstanceVersion()),
                            Ast.Constant(instanceIndex),
                            _bindingInfo.Result
                        ),
                        Invoke(new DynamicMetaObject(_bindingInfo.Result, BindingRestrictions.Empty)).Expression
                    );
                } else {
                    _bindingInfo.Body.AddCondition(
                        Ast.AndAlso(
                            Ast.NotEqual(
                                dict,
                                Ast.Constant(null)
                            ),
                            Ast.Call(
                                dict,
                                PythonTypeInfo._PythonDictionary.TryGetvalue,
                                AstUtils.Constant(GetGetMemberName(_bindingInfo.Action)),
                                _bindingInfo.Result
                            )
                        ),
                        Invoke(new DynamicMetaObject(_bindingInfo.Result, BindingRestrictions.Empty)).Expression
                    );
                }
            }

            public Expression Expression {
                get {
                    return _target.Expression;
                }
            }           
        }

        internal class FastGetBinderHelper : GetOrInvokeBinderHelper<FastGetBase> {
            private readonly int _version;
            private readonly PythonGetMemberBinder/*!*/ _binder;
            private readonly CallSite<Func<CallSite, object, CodeContext, object>> _site;
            private readonly CodeContext _context;
            private bool _dictAccess;
            private PythonTypeSlot _slot;
            private PythonTypeSlot _getattrSlot;

            public FastGetBinderHelper(CodeContext/*!*/ context, CallSite<Func<CallSite, object, CodeContext, object>>/*!*/ site, IPythonObject/*!*/ value, PythonGetMemberBinder/*!*/ binder)
                : base(value) {
                Assert.NotNull(value, binder, context, site);

                _version = value.PythonType.Version;
                _binder = binder;
                _site = site;
                _context = context;
            }

            protected override void MakeTypeError() {
            }

            protected override bool IsFinal {
                get { return _slot != null && _slot.GetAlwaysSucceeds; }
            }

            protected override void MakeSlotAccess(PythonTypeSlot foundSlot, bool systemTypeResolution) {
                if (systemTypeResolution) {
                    if (!_binder.Context.Binder.TryResolveSlot(_context, Value.PythonType, Value.PythonType, _binder.Name, out foundSlot)) {
                        Debug.Assert(false);
                    }

                }
                _slot = foundSlot;
            }
            
            public FastBindResult<Func<CallSite, object, CodeContext, object>> GetBinding(CodeContext context, string name) {
                var cachedGets = GetCachedGets();
                var key = CachedGetKey.Make(name, context.ModuleContext.ExtensionMethods);
                FastGetBase dlg;
                lock (cachedGets) {                    
                    if (!cachedGets.TryGetValue(key, out dlg) || !dlg.IsValid(Value.PythonType)) {
                        var binding = Bind(context, name);
                        if (binding != null) {
                            dlg = binding;

                            if (dlg.ShouldCache) {
                                cachedGets[key] = dlg;
                            }
                        }
                    }
                }

                if (dlg != null && dlg.ShouldUseNonOptimizedSite) {
                    return new FastBindResult<Func<CallSite, object, CodeContext, object>>(dlg._func, dlg.ShouldCache);
                }
                return new FastBindResult<Func<CallSite, object, CodeContext, object>>();
            }

            private Dictionary<CachedGetKey, FastGetBase> GetCachedGets() {
                if (_binder.IsNoThrow) {
                    var cachedGets = Value.PythonType._cachedTryGets;
                    if (cachedGets == null) {
                        Interlocked.CompareExchange(
                            ref Value.PythonType._cachedTryGets,
                            new Dictionary<CachedGetKey, FastGetBase>(),
                            null);

                        cachedGets = Value.PythonType._cachedTryGets;
                    }
                    return cachedGets;
                } else {
                    var cachedGets = Value.PythonType._cachedGets;
                    if (cachedGets == null) {
                        Interlocked.CompareExchange(
                            ref Value.PythonType._cachedGets,
                            new Dictionary<CachedGetKey, FastGetBase>(),
                            null);

                        cachedGets = Value.PythonType._cachedGets;
                    }
                    return cachedGets;
                }
            }

            protected override FastGetBase FinishRule() {
                GetMemberDelegates func;
                ReflectedSlotProperty rsp = _slot as ReflectedSlotProperty;
                if (rsp != null) {
                    Debug.Assert(!_dictAccess); // properties for __slots__ are get/set descriptors so we should never access the dictionary.
                    func = new GetMemberDelegates(OptimizedGetKind.PropertySlot, Value.PythonType, _binder, _binder.Name, _version, _slot, _getattrSlot, rsp.Getter, FallbackError(), _context.ModuleContext.ExtensionMethods);
                } else if (_dictAccess) {
                    if (_slot is PythonTypeUserDescriptorSlot) {
                        func = new GetMemberDelegates(OptimizedGetKind.UserSlotDict, Value.PythonType, _binder, _binder.Name, _version, _slot, _getattrSlot, null, FallbackError(), _context.ModuleContext.ExtensionMethods);
                    } else {
                        func = new GetMemberDelegates(OptimizedGetKind.SlotDict, Value.PythonType, _binder, _binder.Name, _version, _slot, _getattrSlot, null, FallbackError(), _context.ModuleContext.ExtensionMethods);
                    }
                } else {
                    if (_slot is PythonTypeUserDescriptorSlot) {
                        func = new GetMemberDelegates(OptimizedGetKind.UserSlotOnly, Value.PythonType, _binder, _binder.Name, _version, _slot, _getattrSlot, null, FallbackError(), _context.ModuleContext.ExtensionMethods);
                    } else {
                        func = new GetMemberDelegates(OptimizedGetKind.SlotOnly, Value.PythonType, _binder, _binder.Name, _version, _slot, _getattrSlot, null, FallbackError(), _context.ModuleContext.ExtensionMethods);
                    }
                }
                return func;
            }

            private Func<CallSite, object, CodeContext, object> FallbackError() {
                Type finalType = Value.PythonType.FinalSystemType;
                if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(finalType)) {                    
                    return ((IFastGettable)Value).MakeGetBinding(_site, _binder, _context, _binder.Name);
                }

                if (_binder.IsNoThrow) {
                    return (site, self, context) => OperationFailed.Value;
                }

                string name = _binder.Name;
                return (site, self, context) => { throw PythonOps.AttributeErrorForMissingAttribute(((IPythonObject)self).PythonType.Name, name); };
            }

            protected override void MakeDictionaryAccess() {
                _dictAccess = true;
            }

            protected override FastGetBase BindGetAttribute(PythonTypeSlot foundSlot) {
                Type finalType = Value.PythonType.FinalSystemType;
                if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(finalType)) {
                    Debug.Assert(Value is IFastGettable);

                    PythonTypeSlot baseSlot;
                    if (TryGetGetAttribute(_context, DynamicHelpers.GetPythonTypeFromType(finalType), out baseSlot) && 
                        baseSlot == foundSlot) {
                        
                        return new ChainedUserGet(_binder, _version, FallbackError());
                    }
                }

                PythonTypeSlot getattr;
                Value.PythonType.TryResolveSlot(_context, "__getattr__", out getattr);
                return new GetAttributeDelegates(_binder, _binder.Name, _version, foundSlot, getattr);
            }

            protected override void MakeGetAttrAccess(PythonTypeSlot getattr) {
                _getattrSlot = getattr;
            }
        }

        class GetBinderHelper : MetaGetBinderHelper {
            private readonly DynamicMetaObjectBinder _binder;

            public GetBinderHelper(MetaUserObject target, DynamicMetaObjectBinder binder, DynamicMetaObject codeContext)
                : base(target, binder, codeContext) {
                _binder = binder;
            }

            protected override DynamicMetaObject Fallback() {
                return GetMemberFallback(_target, _binder, _codeContext);
            }

            protected override DynamicMetaObject FallbackError() {
                return _target.FallbackGetError(_binder, _codeContext);
            }
        }

        class InvokeBinderHelper : MetaGetBinderHelper {
            private readonly InvokeMemberBinder _binder;
            private readonly DynamicMetaObject[] _args;

            public InvokeBinderHelper(MetaUserObject target, InvokeMemberBinder binder, DynamicMetaObject[] args, DynamicMetaObject codeContext)
                : base(target, binder, codeContext) {
                _binder = binder;
                _args = args;
            }

            protected override DynamicMetaObject Fallback() {
                return _binder.FallbackInvokeMember(_target, _args);
            }

            protected override DynamicMetaObject FallbackError() {
                if (_target._baseMetaObject != null) {
                    return _target._baseMetaObject.BindInvokeMember(_binder, _args);
                }

                return Fallback();
            }

            protected override DynamicMetaObject Invoke(DynamicMetaObject res) {
                return _binder.FallbackInvoke(res, _args, null);
            }
        }

        private DynamicMetaObject GetMemberWorker(DynamicMetaObjectBinder/*!*/ member, DynamicMetaObject codeContext) {
            return new GetBinderHelper(this, member, codeContext).Bind((CodeContext)codeContext.Value, GetGetMemberName(member));
        }

        /// <summary>
        /// Checks to see if this type has __getattribute__ that overrides all other attribute lookup.
        /// 
        /// This is more complex then it needs to be.  The problem is that when we have a 
        /// mixed new-style/old-style class we have a weird __getattribute__ defined.  When
        /// we always dispatch through rules instead of PythonTypes it should be easy to remove
        /// this.
        /// </summary>
        private static bool TryGetGetAttribute(CodeContext/*!*/ context, PythonType/*!*/ type, out PythonTypeSlot dts) {
            if (type.TryResolveSlot(context, "__getattribute__", out dts)) {
                BuiltinMethodDescriptor bmd = dts as BuiltinMethodDescriptor;

                if (bmd == null || bmd.DeclaringType != typeof(object) ||
                    bmd.Template.Targets.Count != 1 ||
                    bmd.Template.Targets[0].DeclaringType != typeof(ObjectOps) ||
                    bmd.Template.Targets[0].Name != "__getattribute__") {
                    return dts != null;
                }
            }
            return false;
        }
        
        private static MethodCallExpression/*!*/ MakeGetAttrTestAndGet(GetBindingInfo/*!*/ info, Expression/*!*/ getattr) {
            return Ast.Call(
                PythonTypeInfo._PythonOps.SlotTryGetBoundValue,
                AstUtils.Constant(PythonContext.GetPythonContext(info.Action).SharedContext),
                AstUtils.Convert(getattr, typeof(PythonTypeSlot)),
                AstUtils.Convert(info.Self, typeof(object)),
                Ast.Convert(
                    Ast.Property(
                        Ast.Convert(
                            info.Self,
                            typeof(IPythonObject)),
                        PythonTypeInfo._IPythonObject.PythonType
                    ),
                    typeof(PythonType)
                ),
                info.Result
            );
        }               
        
        private static Expression/*!*/ GetWeakSlot(PythonTypeSlot slot) {
            return AstUtils.Convert(AstUtils.WeakConstant(slot), typeof(PythonTypeSlot));
        }

        private static Expression/*!*/ MakeTypeError(DynamicMetaObjectBinder binder, string/*!*/ name, PythonType/*!*/ type) {
            return binder.Throw(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.AttributeErrorForMissingAttribute), new Type[] { typeof(string), typeof(string) }),
                    AstUtils.Constant(type.Name),
                    AstUtils.Constant(name)
                ),
                typeof(object)
            );
        }

        #endregion

        #region Set Member Helpers

        internal abstract class SetBinderHelper<TResult> {
            private readonly IPythonObject/*!*/ _instance;
            private readonly object _value;
            protected readonly CodeContext/*!*/ _context;

            public SetBinderHelper(CodeContext/*!*/ context, IPythonObject/*!*/ instance, object value) {
                Assert.NotNull(context, instance);

                _instance = instance;
                _value = value;
                _context = context;
            }

            public TResult Bind(string name) {                
                bool bound = false;

                // call __setattr__ if it exists
                PythonTypeSlot dts;
                if (_instance.PythonType.TryResolveSlot(_context, "__setattr__", out dts) && !IsStandardObjectMethod(dts)) {
                    // skip the fake __setattr__ on mixed new-style/old-style types
                    if (dts != null) {
                        MakeSetAttrTarget(dts);
                        bound = true;
                    }
                }

                if (!bound) {
                    // then see if we have a set descriptor
                    bool systemTypeResolution, extensionMethodResolution;
                    dts = FindSlot(_context, name, _instance, out systemTypeResolution, out extensionMethodResolution);

                    ReflectedSlotProperty rsp = dts as ReflectedSlotProperty;
                    if (rsp != null) {
                        MakeSlotsSetTarget(rsp);
                        bound = true;
                    } else if (dts != null && dts.IsSetDescriptor(_context, _instance.PythonType)) {
                        MakeSlotSetOrFallback(dts, systemTypeResolution);
                        bound = systemTypeResolution || dts.GetType() == typeof(PythonProperty);    // the only slot we currently optimize in MakeSlotSet
                    }
                }

                if (!bound) {
                    // finally if we have a dictionary set the value there.
                    if (_instance.PythonType.HasDictionary) {
                        MakeDictionarySetTarget();
                    } else {
                        MakeFallback();
                    }
                }

                return Finish();
            }

            public IPythonObject Instance {
                get {
                    return _instance;
                }
            }

            public object Value {
                get {
                    return _value;
                }
            }

            protected abstract TResult Finish();
            
            protected abstract void MakeSetAttrTarget(PythonTypeSlot dts);
            protected abstract void MakeSlotsSetTarget(ReflectedSlotProperty prop);
            protected abstract void MakeSlotSetOrFallback(PythonTypeSlot dts, bool systemTypeResolution);
            protected abstract void MakeDictionarySetTarget();
            protected abstract void MakeFallback();
        }

        internal class FastSetBinderHelper<TValue> : SetBinderHelper<SetMemberDelegates<TValue>> {
            private readonly PythonSetMemberBinder _binder;
            private readonly int _version;
            private PythonTypeSlot _setattrSlot;
            private ReflectedSlotProperty _slotProp;
            private bool _unsupported, _dictSet;

            public FastSetBinderHelper(CodeContext context, IPythonObject self, object value, PythonSetMemberBinder binder)
                : base(context, self, value) {
                _binder = binder;
                _version = self.PythonType.Version;
            }

            protected override SetMemberDelegates<TValue> Finish() {
                if (_unsupported) {
                    return new SetMemberDelegates<TValue>(_context, Instance.PythonType, OptimizedSetKind.None, _binder.Name, _version, _setattrSlot, null);
                } else if (_setattrSlot != null) {
                    return new SetMemberDelegates<TValue>(_context, Instance.PythonType, OptimizedSetKind.SetAttr, _binder.Name, _version, _setattrSlot, null);
                } else if (_slotProp != null) {
                    return new SetMemberDelegates<TValue>(_context, Instance.PythonType, OptimizedSetKind.UserSlot, _binder.Name, _version, null, _slotProp.Setter);
                } else if(_dictSet) {
                    return new SetMemberDelegates<TValue>(_context, Instance.PythonType, OptimizedSetKind.SetDict, _binder.Name, _version, null, null);
                } else {
                    return new SetMemberDelegates<TValue>(_context, Instance.PythonType, OptimizedSetKind.Error, _binder.Name, _version, null, null);
                }                
            }
            
            public FastBindResult<Func<CallSite, object, TValue, object>> MakeSet() {
                var cachedSets = GetCachedSets();

                FastSetBase dlg;
                lock (cachedSets) {
                    var kvp = new SetMemberKey(typeof(TValue), _binder.Name);
                    if (!cachedSets.TryGetValue(kvp, out dlg) || dlg._version != Instance.PythonType.Version) {
                        dlg = Bind(_binder.Name);
                        if (dlg != null) {
                            cachedSets[kvp] = dlg;
                        }
                    }
                }

                if (dlg.ShouldUseNonOptimizedSite) {
                    return new FastBindResult<Func<CallSite, object, TValue, object>>((Func<CallSite, object, TValue, object>)(object)dlg._func, false);
                }
                return new FastBindResult<Func<CallSite, object, TValue, object>>();
            }

            private Dictionary<SetMemberKey, FastSetBase> GetCachedSets() {
                var cachedSets = Instance.PythonType._cachedSets;
                if (cachedSets == null) {
                    Interlocked.CompareExchange(
                        ref Instance.PythonType._cachedSets,
                        new Dictionary<SetMemberKey, FastSetBase>(),
                        null);

                    cachedSets = Instance.PythonType._cachedSets;
                }
                return cachedSets;
            }

            protected override void MakeSlotSetOrFallback(PythonTypeSlot dts, bool systemTypeResolution) {
                _unsupported = true;
            }

            protected override void MakeSlotsSetTarget(ReflectedSlotProperty prop) {
                _slotProp = prop;
            }

            protected override void MakeFallback() {
            }

            protected override void MakeSetAttrTarget(PythonTypeSlot dts) {
                _setattrSlot = dts;
            }

            protected override void MakeDictionarySetTarget() {
                _dictSet = true;
            }
        }

        internal class MetaSetBinderHelper : SetBinderHelper<DynamicMetaObject> {
            private readonly MetaUserObject/*!*/ _target;
            private readonly DynamicMetaObject/*!*/ _value;
            private readonly SetBindingInfo _info;
            private DynamicMetaObject _result;
            private string _resolution = "SetMember ";

            public MetaSetBinderHelper(MetaUserObject/*!*/ target, DynamicMetaObject/*!*/ value, SetMemberBinder/*!*/ binder)
                : base(PythonContext.GetPythonContext(binder).SharedContext, target.Value, value.Value) {
                Assert.NotNull(target, value, binder);

                _target = target;
                _value = value;

                _info = new SetBindingInfo(
                    binder,
                    new DynamicMetaObject[] { target, value },
                    new ConditionalBuilder(binder),
                    BindingHelpers.GetValidationInfo(target, Instance.PythonType)
                );
            }

            protected override void MakeSetAttrTarget(PythonTypeSlot dts) {
                ParameterExpression tmp = Ast.Variable(typeof(object), "boundVal");
                _info.Body.AddVariable(tmp);

                _info.Body.AddCondition(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.SlotTryGetValue)),
                        AstUtils.Constant(PythonContext.GetPythonContext(_info.Action).SharedContext),
                        AstUtils.Convert(AstUtils.WeakConstant(dts), typeof(PythonTypeSlot)),
                        AstUtils.Convert(_info.Args[0].Expression, typeof(object)),
                        AstUtils.Convert(AstUtils.WeakConstant(Instance.PythonType), typeof(PythonType)),
                        tmp
                    ),
                    Ast.Dynamic(
                        PythonContext.GetPythonContext(_info.Action).Invoke(
                            new CallSignature(2)
                        ),
                        typeof(object),
                        PythonContext.GetCodeContext(_info.Action),
                        tmp,
                        AstUtils.Constant(_info.Action.Name),
                        _info.Args[1].Expression
                    )
                );

                _info.Body.FinishCondition(
                    FallbackSetError(_info.Action, _info.Args[1]).Expression
                );

                _result = _info.Body.GetMetaObject(_target, _value);
                _resolution += "SetAttr ";
            }

            protected override DynamicMetaObject Finish() {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, _resolution);
                PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "UserSet");
                
                Debug.Assert(_result != null);

                _result = new DynamicMetaObject(
                    _result.Expression,
                    _target.Restrict(Instance.GetType()).Restrictions.Merge(_result.Restrictions)
                );
                
                Debug.Assert(!_result.Expression.Type.IsValueType());

                return BindingHelpers.AddDynamicTestAndDefer(
                    _info.Action,
                    _result,
                    new DynamicMetaObject[] { _target, _value },
                    _info.Validation
                );

            }

            protected override void MakeFallback() {
                _info.Body.FinishCondition(
                    FallbackSetError(_info.Action, _value).Expression
                );

                _result = _info.Body.GetMetaObject(_target, _value);
            }

            protected override void MakeDictionarySetTarget() {
                _resolution += "Dictionary ";
                FieldInfo fi = _info.Args[0].LimitType.GetField(NewTypeMaker.DictFieldName);
                if (fi != null) {
                    FieldInfo classField = _info.Args[0].LimitType.GetField(NewTypeMaker.ClassFieldName);
                    var optInstanceNames = Instance.PythonType.GetOptimizedInstanceNames();
                    int keysIndex;
                    if (classField != null && optInstanceNames != null && (keysIndex = optInstanceNames.IndexOf(_info.Action.Name)) != -1) {
                        // optimized access which can read directly into an object array avoiding a dictionary lookup.
                        // return UserTypeOps.FastSetDictionaryValue(this._class, ref this._dict, name, value, keysVersion, keysIndex);
                        _info.Body.FinishCondition(
                            Ast.Call(
                                typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.FastSetDictionaryValueOptimized)),
                                Ast.Field(
                                    Ast.Convert(_info.Args[0].Expression, _info.Args[0].LimitType),
                                    classField
                                ),
                                Ast.Field(
                                    Ast.Convert(_info.Args[0].Expression, _info.Args[0].LimitType),
                                    fi
                                ),
                                AstUtils.Constant(_info.Action.Name),
                                AstUtils.Convert(_info.Args[1].Expression, typeof(object)),
                                Ast.Constant(Instance.PythonType.GetOptimizedInstanceVersion()),
                                Ast.Constant(keysIndex)
                            )
                        );
                    } else {
                        // return UserTypeOps.FastSetDictionaryValue(ref this._dict, name, value);
                        _info.Body.FinishCondition(
                            Ast.Call(
                                typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.FastSetDictionaryValue)),
                                Ast.Field(
                                    Ast.Convert(_info.Args[0].Expression, _info.Args[0].LimitType),
                                    fi
                                ),
                                AstUtils.Constant(_info.Action.Name),
                                AstUtils.Convert(_info.Args[1].Expression, typeof(object))
                            )
                        );
                    }


                } else {
                    // return UserTypeOps.SetDictionaryValue(rule.Parameters[0], name, value);
                    _info.Body.FinishCondition(
                        Ast.Call(
                            typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.SetDictionaryValue)),
                            Ast.Convert(_info.Args[0].Expression, typeof(IPythonObject)),
                            AstUtils.Constant(_info.Action.Name),
                            AstUtils.Convert(_info.Args[1].Expression, typeof(object))
                        )
                    );
                }

                _result = _info.Body.GetMetaObject(_target, _value);
            }

            protected override void MakeSlotSetOrFallback(PythonTypeSlot dts, bool systemTypeResolution) {
                if (systemTypeResolution) {
                    _result = _target.Fallback(_info.Action, _value);
                } else {
                    _result = MakeSlotSet(_info, dts);
                }
            }
            
            protected override void MakeSlotsSetTarget(ReflectedSlotProperty prop) {
                _resolution += "Slot ";
                MakeSlotsSetTargetHelper(_info, prop, _value.Expression);
                _result = _info.Body.GetMetaObject(_target, _value);
            }

            /// <summary>
            /// Helper for falling back - if we have a base object fallback to it first (which can
            /// then fallback to the calling site), otherwise fallback to the calling site.
            /// </summary>
            private DynamicMetaObject/*!*/ FallbackSetError(SetMemberBinder/*!*/ action, DynamicMetaObject/*!*/ value) {
                if (_target._baseMetaObject != null) {
                    return _target._baseMetaObject.BindSetMember(action, value);
                } else if (action is PythonSetMemberBinder) {
                    return new DynamicMetaObject(
                        MakeTypeError(action, action.Name, Instance.PythonType),
                        BindingRestrictions.Empty
                    );
                }

                return _info.Action.FallbackSetMember(_target.Restrict(_target.GetLimitType()), value);
            }

        }

        private static bool IsStandardObjectMethod(PythonTypeSlot dts) {
            BuiltinMethodDescriptor bmd = dts as BuiltinMethodDescriptor;
            if (bmd == null) return false;
            return bmd.Template.Targets[0].DeclaringType == typeof(ObjectOps);
        }

        private static void MakeSlotsDeleteTarget(MemberBindingInfo/*!*/ info, ReflectedSlotProperty/*!*/ rsp) {
            MakeSlotsSetTargetHelper(info, rsp, Ast.Field(null, typeof(Uninitialized).GetField(nameof(Uninitialized.Instance))));
        }

        private static void MakeSlotsSetTargetHelper(MemberBindingInfo/*!*/ info, ReflectedSlotProperty/*!*/ rsp, Expression/*!*/ value) {
            // type has __slots__ defined for this member, call the setter directly
            ParameterExpression tmp = Ast.Variable(typeof(object), "res");
            info.Body.AddVariable(tmp);

            info.Body.FinishCondition(
                Ast.Block(
                    Ast.Assign(
                        tmp,
                        Ast.Convert(
                            Ast.Assign(
                                Ast.ArrayAccess(
                                    GetSlots(info.Args[0]),
                                    AstUtils.Constant(rsp.Index)
                                ),
                                AstUtils.Convert(value, typeof(object))
                            ),
                            tmp.Type
                        )
                    ),
                    tmp
                )
            );
        }

        private static DynamicMetaObject MakeSlotSet(SetBindingInfo/*!*/ info, PythonTypeSlot/*!*/ dts) {
            ParameterExpression tmp = Ast.Variable(info.Args[1].Expression.Type, "res");
            info.Body.AddVariable(tmp);

            // users can subclass PythonProperty so check the type explicitly 
            // and only in-line the ones we fully understand.
            if (dts.GetType() == typeof(PythonProperty)) {
                // properties are mutable so we generate code to get the value rather
                // than burning it into the rule.
                Expression setter = Ast.Property(
                    Ast.Convert(AstUtils.WeakConstant(dts), typeof(PythonProperty)),
                    "fset"
                );
                ParameterExpression tmpSetter = Ast.Variable(typeof(object), "tmpSet");
                info.Body.AddVariable(tmpSetter);

                info.Body.FinishCondition(
                    Ast.Block(
                        Ast.Assign(tmpSetter, setter),
                        Ast.Condition(
                            Ast.NotEqual(
                                tmpSetter,
                                AstUtils.Constant(null)
                            ),
                            Ast.Block(
                                Ast.Assign(tmp, info.Args[1].Expression),
                                Ast.Dynamic(
                                    PythonContext.GetPythonContext(info.Action).InvokeOne,
                                    typeof(object),
                                    AstUtils.Constant(PythonContext.GetPythonContext(info.Action).SharedContext),
                                    tmpSetter,
                                    info.Args[0].Expression,
                                    AstUtils.Convert(tmp, typeof(object))
                                ),
                                Ast.Convert(
                                    tmp,
                                    typeof(object)
                                )
                            ),
                            info.Action.Throw(Ast.Call(typeof(PythonOps).GetMethod(nameof(PythonOps.UnsetableProperty))), typeof(object))
                        )
                    )
                );
                return info.Body.GetMetaObject();
            }

            CodeContext context = PythonContext.GetPythonContext(info.Action).SharedContext;
            Debug.Assert(context != null);

            info.Body.AddCondition(
                Ast.Block(
                    Ast.Assign(tmp, info.Args[1].Expression),
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.SlotTrySetValue)),
                        AstUtils.Constant(context),
                        AstUtils.Convert(AstUtils.WeakConstant(dts), typeof(PythonTypeSlot)),
                        AstUtils.Convert(info.Args[0].Expression, typeof(object)),
                        Ast.Convert(
                            Ast.Property(
                                Ast.Convert(
                                    info.Args[0].Expression,
                                    typeof(IPythonObject)),
                                PythonTypeInfo._IPythonObject.PythonType
                            ),
                            typeof(PythonType)
                        ),
                        AstUtils.Convert(tmp, typeof(object))
                    )
                ),
                AstUtils.Convert(tmp, typeof(object))
            );
            return null;
        }

        #endregion

        #region Delete Member Helpers

        private DynamicMetaObject/*!*/ MakeDeleteMemberRule(DeleteBindingInfo/*!*/ info) {
            CodeContext context = PythonContext.GetPythonContext(info.Action).SharedContext;
            DynamicMetaObject self = info.Args[0].Restrict(info.Args[0].GetRuntimeType());

            IPythonObject sdo = info.Args[0].Value as IPythonObject;
            if (info.Action.Name == "__class__") {
                return new DynamicMetaObject(
                    info.Action.Throw(
                        Ast.New(
                            typeof(TypeErrorException).GetConstructor(new Type[] { typeof(string) }),
                            AstUtils.Constant("can't delete __class__ attribute")
                        ),
                        typeof(object)
                    ),
                    self.Restrictions
                );
            }

            // call __delattr__ if it exists
            PythonTypeSlot dts;
            if (sdo.PythonType.TryResolveSlot(context, "__delattr__", out dts) && !IsStandardObjectMethod(dts)) {
                MakeDeleteAttrTarget(info, sdo, dts);
            }

            // then see if we have a delete descriptor
            sdo.PythonType.TryResolveSlot(context, info.Action.Name, out dts);
            ReflectedSlotProperty rsp = dts as ReflectedSlotProperty;
            if (rsp != null) {
                MakeSlotsDeleteTarget(info, rsp);
            }
            
            if (!info.Body.IsFinal && dts != null) {
                MakeSlotDelete(info, dts);
            }

            if (!info.Body.IsFinal && sdo.PythonType.HasDictionary) {
                // finally if we have a dictionary set the value there.
                MakeDictionaryDeleteTarget(info);
            }

            if (!info.Body.IsFinal) {
                // otherwise fallback
                info.Body.FinishCondition(
                    FallbackDeleteError(info.Action, info.Args).Expression
                );
            }

            DynamicMetaObject res = info.Body.GetMetaObject(info.Args);

            res = new DynamicMetaObject(
                res.Expression,
                self.Restrictions.Merge(res.Restrictions)
            );

            return BindingHelpers.AddDynamicTestAndDefer(
                info.Action,
                res,
                info.Args,
                info.Validation
            );

        }

        private static DynamicMetaObject MakeSlotDelete(DeleteBindingInfo/*!*/ info, PythonTypeSlot/*!*/ dts) {

            // users can subclass PythonProperty so check the type explicitly 
            // and only in-line the ones we fully understand.
            if (dts.GetType() == typeof(PythonProperty)) {
                // properties are mutable so we generate code to get the value rather
                // than burning it into the rule.
                Expression deleter = Ast.Property(
                    Ast.Convert(AstUtils.WeakConstant(dts), typeof(PythonProperty)),
                    "fdel"
                );
                ParameterExpression tmpDeleter = Ast.Variable(typeof(object), "tmpDel");
                info.Body.AddVariable(tmpDeleter);

                info.Body.FinishCondition(
                    Ast.Block(
                        Ast.Assign(tmpDeleter, deleter),
                        Ast.Condition(
                            Ast.NotEqual(
                                tmpDeleter,
                                AstUtils.Constant(null)
                            ),                            
                            Ast.Dynamic(
                                PythonContext.GetPythonContext(info.Action).InvokeOne,
                                typeof(object),
                                AstUtils.Constant(PythonContext.GetPythonContext(info.Action).SharedContext),
                                tmpDeleter,
                                info.Args[0].Expression
                            ),
                            info.Action.Throw(Ast.Call(typeof(PythonOps).GetMethod(nameof(PythonOps.UndeletableProperty))), typeof(object))
                        )
                    )
                );
                return info.Body.GetMetaObject();
            }

            info.Body.AddCondition(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.SlotTryDeleteValue)),
                    AstUtils.Constant(PythonContext.GetPythonContext(info.Action).SharedContext),
                    AstUtils.Convert(AstUtils.WeakConstant(dts), typeof(PythonTypeSlot)),
                    AstUtils.Convert(info.Args[0].Expression, typeof(object)),
                    Ast.Convert(
                        Ast.Property(
                            Ast.Convert(
                                info.Args[0].Expression,
                                typeof(IPythonObject)),
                            PythonTypeInfo._IPythonObject.PythonType
                        ),
                        typeof(PythonType)
                    )
                ),
                AstUtils.Constant(null)
            );
            return null;
        }

        private static void MakeDeleteAttrTarget(DeleteBindingInfo/*!*/ info, IPythonObject self, PythonTypeSlot dts) {
            ParameterExpression tmp = Ast.Variable(typeof(object), "boundVal");
            info.Body.AddVariable(tmp);

            // call __delattr__
            info.Body.AddCondition(
                Ast.Call(
                    PythonTypeInfo._PythonOps.SlotTryGetBoundValue,
                    AstUtils.Constant(PythonContext.GetPythonContext(info.Action).SharedContext),
                    AstUtils.Convert(AstUtils.WeakConstant(dts), typeof(PythonTypeSlot)),
                    AstUtils.Convert(info.Args[0].Expression, typeof(object)),
                    AstUtils.Convert(AstUtils.WeakConstant(self.PythonType), typeof(PythonType)),
                    tmp
                ),
                DynamicExpression.Dynamic(
                    PythonContext.GetPythonContext(info.Action).InvokeOne,
                    typeof(object),
                    PythonContext.GetCodeContext(info.Action),
                    tmp,
                    AstUtils.Constant(info.Action.Name)
                )
            );
        }

        private static void MakeDictionaryDeleteTarget(DeleteBindingInfo/*!*/ info) {
            info.Body.FinishCondition(
                Ast.Call(
                    typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.RemoveDictionaryValue)),
                    Ast.Convert(info.Args[0].Expression, typeof(IPythonObject)),
                    AstUtils.Constant(info.Action.Name)
                )
            );
        }

        #endregion

        #region Common Helpers

        /// <summary>
        /// Looks up the associated PythonTypeSlot from the object.  Indicates if the result
        /// came from a standard .NET type in which case we will fallback to the sites binder.
        /// </summary>
        private static PythonTypeSlot FindSlot(CodeContext/*!*/ context, string/*!*/ name, IPythonObject/*!*/ sdo, out bool systemTypeResolution, out bool extensionMethodResolution) {
            PythonTypeSlot foundSlot = null;
            systemTypeResolution = false;      // if we pick up the property from a System type we fallback

            foreach (PythonType pt in sdo.PythonType.ResolutionOrder) {
                if (pt.TryLookupSlot(context, name, out foundSlot)) {
                    // use our built-in binding for ClassMethodDescriptors rather than falling back
                    if (!(foundSlot is ClassMethodDescriptor)) {
                        systemTypeResolution = pt.IsSystemType;
                    }
                    break;
                }
            }

            extensionMethodResolution = false;
            if (foundSlot == null) {
                extensionMethodResolution = true;
                var extMethods = context.ModuleContext.ExtensionMethods.GetBinder(context.LanguageContext).GetMember(MemberRequestKind.Get, sdo.PythonType.UnderlyingSystemType, name);

                if (extMethods.Count > 0) {
                    foundSlot = PythonTypeOps.GetSlot(extMethods, name, false);
                }
            }

            return foundSlot;
        }

        #endregion

        #region BindingInfo classes

        class MemberBindingInfo {
            public readonly ConditionalBuilder/*!*/ Body;
            public readonly DynamicMetaObject/*!*/[]/*!*/ Args;
            public readonly ValidationInfo/*!*/ Validation;

            public MemberBindingInfo(DynamicMetaObject/*!*/[]/*!*/ args, ConditionalBuilder/*!*/ body, ValidationInfo/*!*/ validation) {
                Body = body;
                Validation = validation;
                Args = args;
            }
        }

        class DeleteBindingInfo : MemberBindingInfo {
            public readonly DeleteMemberBinder/*!*/ Action;

            public DeleteBindingInfo(DeleteMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args, ConditionalBuilder/*!*/ body, ValidationInfo/*!*/ validation)
                : base(args, body, validation) {
                Action = action;
            }
        }

        class SetBindingInfo : MemberBindingInfo {
            public readonly SetMemberBinder/*!*/ Action;

            public SetBindingInfo(SetMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args, ConditionalBuilder/*!*/ body, ValidationInfo/*!*/ validation)
                : base(args, body, validation) {
                Action = action;
            }
        }

        class GetBindingInfo : MemberBindingInfo {
            public readonly DynamicMetaObjectBinder/*!*/ Action;
            public readonly ParameterExpression/*!*/ Self, Result;

            public GetBindingInfo(DynamicMetaObjectBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args, ParameterExpression/*!*/ self, ParameterExpression/*!*/ result, ConditionalBuilder/*!*/ body, ValidationInfo/*!*/ validationInfo)
                : base(args, body, validationInfo) {
                Action = action;
                Self = self;
                Result = result;
            }
        }

        #endregion

        #region Fallback Helpers

        /// <summary>
        /// Helper for falling back - if we have a base object fallback to it first (which can
        /// then fallback to the calling site), otherwise fallback to the calling site.
        /// </summary>
        private DynamicMetaObject/*!*/ FallbackGetError(DynamicMetaObjectBinder/*!*/ action, DynamicMetaObject codeContext) {
            if (_baseMetaObject != null) {
                return Fallback(action, codeContext);
            } else if (BindingHelpers.IsNoThrow(action)) {
                return new DynamicMetaObject(
                    Ast.Field(null, typeof(OperationFailed).GetField(nameof(OperationFailed.Value))),
                    BindingRestrictions.Empty
                );
            } else if (action is PythonGetMemberBinder) {
                return new DynamicMetaObject(
                    MakeTypeError(action, GetGetMemberName(action), PythonType),
                    BindingRestrictions.Empty
                );
            }

            return GetMemberFallback(this, action, codeContext);
        }

        /// <summary>
        /// Helper for falling back - if we have a base object fallback to it first (which can
        /// then fallback to the calling site), otherwise fallback to the calling site.
        /// </summary>
        private DynamicMetaObject/*!*/ FallbackDeleteError(DeleteMemberBinder/*!*/ action, DynamicMetaObject/*!*/[] args) {
            if (_baseMetaObject != null) {
                return _baseMetaObject.BindDeleteMember(action);
            } else if (action is PythonDeleteMemberBinder) {
                return new DynamicMetaObject(
                    MakeTypeError(action, action.Name, ((IPythonObject)args[0].Value).PythonType),
                    BindingRestrictions.Empty
                );
            }

            return action.FallbackDeleteMember(Restrict(this.GetLimitType()));
        }

        #endregion

        private static Expression/*!*/ GetSlots(DynamicMetaObject/*!*/ self) {
            FieldInfo fi = self.LimitType.GetField(NewTypeMaker.SlotsAndWeakRefFieldName);
            if (fi != null) {
                return Ast.Field(
                    Ast.Convert(self.Expression, self.LimitType),
                    fi
                );
            }
            return Ast.Call(
                Ast.Convert(self.Expression, typeof(IPythonObject)),
                typeof(IPythonObject).GetMethod(nameof(IPythonObject.GetSlots))
            );
        }
    }
}
