// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal partial class MetaPythonType : MetaPythonObject, IPythonInvokable {

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            DynamicMetaObject translated = BuiltinFunction.TranslateArguments(pythonInvoke, codeContext, target, args, false, Value.Name);
            if (translated != null) {
                return translated;
            }

            return InvokeWorker(pythonInvoke, args, codeContext);
        }

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            
            foreach (PythonType pt in Value.ResolutionOrder) {
                PythonTypeSlot dummy;
                if (pt.IsSystemType) {
                    return action.FallbackInvokeMember(this, args);
                } else if (pt.TryResolveSlot(DefaultContext.DefaultCLS, action.Name, out dummy)) {
                    break;
                }
            }

            return BindingHelpers.GenericInvokeMember(action, null, this, args);
        }

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ call, params DynamicMetaObject/*!*/[]/*!*/ args) {
            return InvokeWorker(call, args, PythonContext.GetCodeContext(call));
        }

        #endregion

        #region Invoke Implementation

        private DynamicMetaObject/*!*/ InvokeWorker(DynamicMetaObjectBinder/*!*/ call, DynamicMetaObject/*!*/[]/*!*/ args, Expression/*!*/ codeContext) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Type Invoke " + Value.UnderlyingSystemType.FullName + args.Length);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Type Invoke");
            if (this.NeedsDeferral()) {
                return call.Defer(ArrayUtils.Insert(this, args));
            }

            for (int i = 0; i < args.Length; i++) {
                if (args[i].NeedsDeferral()) {
                    return call.Defer(ArrayUtils.Insert(this, args));
                }
            }

            DynamicMetaObject res;
            if (IsStandardDotNetType(call)) {
                res = MakeStandardDotNetTypeCall(call, codeContext, args);
            } else {
                res = MakePythonTypeCall(call, codeContext, args);
            }

            return BindingHelpers.AddPythonBoxing(res);

        }

        /// <summary>
        /// Creating a standard .NET type is easy - we just call it's constructor with the provided
        /// arguments.
        /// </summary>
        private DynamicMetaObject/*!*/ MakeStandardDotNetTypeCall(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[]/*!*/ args) {
            CallSignature signature = BindingHelpers.GetCallSignature(call);
            PythonContext state = PythonContext.GetPythonContext(call);
            MethodBase[] ctors = PythonTypeOps.GetConstructors(Value.UnderlyingSystemType, state.Binder.PrivateBinding);

            if (ctors.Length > 0) {
                return state.Binder.CallMethod(
                    new PythonOverloadResolver(
                        state.Binder,
                        args,
                        signature,
                        codeContext
                    ),
                    ctors,
                    Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value))
                );
            } else {
                string msg;
                if (Value.UnderlyingSystemType.IsAbstract()) {
                    msg = String.Format("Cannot create instances of {0} because it is abstract", Value.Name);
                }else{
                    msg = String.Format("Cannot create instances of {0} because it has no public constructors", Value.Name);
                }
                return new DynamicMetaObject(
                   call.Throw(
                       Ast.New(
                           typeof(TypeErrorException).GetConstructor(new Type[] { typeof(string) }),
                           AstUtils.Constant(msg)
                       )
                   ),
                   Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value))
                );
            }
        }

        /// <summary>
        /// Creating a Python type involves calling __new__ and __init__.  We resolve them
        /// and generate calls to either the builtin funcions directly or embed sites which
        /// call the slots at runtime.
        /// </summary>
        private DynamicMetaObject/*!*/ MakePythonTypeCall(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[]/*!*/ args) {
            ValidationInfo valInfo = MakeVersionCheck();

            DynamicMetaObject self = new RestrictedMetaObject(
                AstUtils.Convert(Expression, LimitType),
                BindingRestrictionsHelpers.GetRuntimeTypeRestriction(Expression, LimitType),
                Value
            );
            CallSignature sig = BindingHelpers.GetCallSignature(call);
            ArgumentValues ai = new ArgumentValues(sig, self, args);
            NewAdapter newAdapter;
            InitAdapter initAdapter;

            if (TooManyArgsForDefaultNew(call, args)) {
                return MakeIncorrectArgumentsForCallError(call, ai, valInfo);
            } else if (Value.UnderlyingSystemType.IsGenericTypeDefinition()) {
                return MakeGenericTypeDefinitionError(call, ai, valInfo);
            } else if (Value.HasAbstractMethods(PythonContext.GetPythonContext(call).SharedContext)) {
                return MakeAbstractInstantiationError(call, ai, valInfo);
            }

            DynamicMetaObject translated = BuiltinFunction.TranslateArguments(call, codeContext, self, args, false, Value.Name);
            if (translated != null) {
                return translated;
            }

            GetAdapters(ai, call, codeContext, out newAdapter, out initAdapter);
            PythonContext state = PythonContext.GetPythonContext(call);
            
            // get the expression for calling __new__
            DynamicMetaObject createExpr = newAdapter.GetExpression(state.Binder);
            if (createExpr.Expression.Type == typeof(void)) {
                return BindingHelpers.AddDynamicTestAndDefer(
                    call,
                    createExpr,
                    args,                        
                    valInfo
                );                    
            }

            Expression res;
            BindingRestrictions additionalRestrictions = BindingRestrictions.Empty;
            if (!Value.IsSystemType && (!(newAdapter is DefaultNewAdapter) || HasFinalizer(call))) {
                // we need to dynamically check the return value to see if it's a subtype of
                // the type that we are calling.  If it is then we need to call __init__/__del__
                // for the actual returned type.
                res = DynamicExpression.Dynamic(
                    Value.GetLateBoundInitBinder(sig),
                    typeof(object),
                    ArrayUtils.Insert(
                        codeContext,
                        Expression.Convert(createExpr.Expression, typeof(object)),
                        DynamicUtils.GetExpressions(args)
                    )
                );
                additionalRestrictions = createExpr.Restrictions;
            } else {
                // just call the __init__ method, built-in types currently have
                // no wacky return values which don't return the derived type.

                // then get the statement for calling __init__
                ParameterExpression allocatedInst = Ast.Variable(createExpr.GetLimitType(), "newInst");
                Expression tmpRead = allocatedInst;
                DynamicMetaObject initCall = initAdapter.MakeInitCall(
                    state.Binder,
                    new RestrictedMetaObject(
                        AstUtils.Convert(allocatedInst, Value.UnderlyingSystemType),
                        createExpr.Restrictions
                    )
                );

                List<Expression> body = new List<Expression>();
                Debug.Assert(!HasFinalizer(call));

                // add the call to init if we need to
                if (initCall.Expression != tmpRead) {
                    // init can fail but if __new__ returns a different type
                    // no exception is raised.
                    DynamicMetaObject initStmt = initCall;

                    if (body.Count == 0) {
                        body.Add(
                            Ast.Assign(allocatedInst, createExpr.Expression)
                        );
                    }

                    if (!Value.UnderlyingSystemType.IsAssignableFrom(createExpr.Expression.Type)) {
                        // return type of object, we need to check the return type before calling __init__.
                        body.Add(
                            AstUtils.IfThen(
                                Ast.TypeIs(allocatedInst, Value.UnderlyingSystemType),
                                initStmt.Expression
                            )
                        );
                    } else {
                        // just call the __init__ method, no type check necessary (TODO: need null check?)
                        body.Add(initStmt.Expression);
                    }
                }

                // and build the target from everything we have
                if (body.Count == 0) {
                    res = createExpr.Expression;
                } else {
                    body.Add(allocatedInst);
                    res = Ast.Block(body);
                }
                res = Ast.Block(new ParameterExpression[] { allocatedInst }, res);

                additionalRestrictions = initCall.Restrictions;
            }

            return BindingHelpers.AddDynamicTestAndDefer(
                call,
                new DynamicMetaObject(
                    res,
                    self.Restrictions.Merge(additionalRestrictions)
                ),
                ArrayUtils.Insert(this, args),
                valInfo
            );
        }
        
        #endregion

        #region Adapter support

        private void GetAdapters(ArgumentValues/*!*/ ai, DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, out NewAdapter/*!*/ newAdapter, out InitAdapter/*!*/ initAdapter) {
            PythonTypeSlot newInst, init;

            Value.TryResolveSlot(PythonContext.GetPythonContext(call).SharedContext, "__new__", out newInst);
            Value.TryResolveSlot(PythonContext.GetPythonContext(call).SharedContext, "__init__", out init);

            // these are never null because we always resolve to __new__ or __init__ somewhere.
            Assert.NotNull(newInst, init);

            newAdapter = GetNewAdapter(ai, newInst, call, codeContext);
            initAdapter = GetInitAdapter(ai, init, call, codeContext);
        }

        private InitAdapter/*!*/ GetInitAdapter(ArgumentValues/*!*/ ai, PythonTypeSlot/*!*/ init, DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext) {
            PythonContext state = PythonContext.GetPythonContext(call);
            if ((init == InstanceOps.Init && !HasFinalizer(call)) || (Value == TypeCache.PythonType && ai.Arguments.Length == 2)) {
                return new DefaultInitAdapter(ai, state, codeContext);
            } else if (init is BuiltinMethodDescriptor) {
                return new BuiltinInitAdapter(ai, ((BuiltinMethodDescriptor)init).Template, state, codeContext);
            } else if (init is BuiltinFunction) {
                return new BuiltinInitAdapter(ai, (BuiltinFunction)init, state, codeContext);
            } else {
                return new SlotInitAdapter(init, ai, state, codeContext);
            }
        }

        private NewAdapter/*!*/ GetNewAdapter(ArgumentValues/*!*/ ai, PythonTypeSlot/*!*/ newInst, DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext) {
            PythonContext state = PythonContext.GetPythonContext(call);

            if (newInst == InstanceOps.New) {
                return new DefaultNewAdapter(ai, Value, state, codeContext);
            } else if (newInst is ConstructorFunction) {
                return new ConstructorNewAdapter(ai, Value, state, codeContext);
            } else if (newInst is BuiltinFunction) {
                return new BuiltinNewAdapter(ai, Value, ((BuiltinFunction)newInst), state, codeContext);
            }

            return new NewAdapter(ai, state, codeContext);
        }

        private class CallAdapter {
            private readonly ArgumentValues/*!*/ _argInfo;
            private readonly PythonContext/*!*/ _state;
            private readonly Expression/*!*/ _context;

            public CallAdapter(ArgumentValues/*!*/ ai, PythonContext/*!*/ state, Expression/*!*/ codeContext) {
                _argInfo = ai;
                _state = state;
                _context = codeContext;
            }

            protected PythonContext PythonContext {
                get {
                    return _state;
                }
            }

            protected Expression CodeContext {
                get {
                    return _context;
                }
            }

            protected ArgumentValues/*!*/ Arguments {
                get { return _argInfo; }
            }
        }

        private class ArgumentValues {
            public readonly DynamicMetaObject/*!*/ Self;
            public readonly DynamicMetaObject/*!*/[]/*!*/ Arguments;
            public readonly CallSignature Signature;

            public ArgumentValues(CallSignature signature, DynamicMetaObject/*!*/ self, DynamicMetaObject/*!*/[]/*!*/ args) {
                Self = self;
                Signature = signature;
                Arguments = args;
            }
        }

        #endregion

        #region __new__ adapters

        private class NewAdapter : CallAdapter {
            public NewAdapter(ArgumentValues/*!*/ ai, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
            }

            public virtual DynamicMetaObject/*!*/ GetExpression(PythonBinder/*!*/ binder) {
                return MakeDefaultNew(
                    binder,
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.PythonTypeGetMember)),
                        CodeContext,
                        AstUtils.Convert(Arguments.Self.Expression, typeof(PythonType)),
                        AstUtils.Constant(null),
                        AstUtils.Constant("__new__")
                    )
                );
            }

            protected DynamicMetaObject/*!*/ MakeDefaultNew(DefaultBinder/*!*/ binder, Expression/*!*/ function) {
                // calling theType.__new__(theType, args)
                List<Expression> args = new List<Expression>();
                args.Add(CodeContext);
                args.Add(function);

                AppendNewArgs(args);

                return new DynamicMetaObject(
                    DynamicExpression.Dynamic(
                        PythonContext.Invoke(
                            GetDynamicNewSignature()
                        ),
                        typeof(object),
                        args.ToArray()
                    ),
                    Arguments.Self.Restrictions
                );
            }

            private void AppendNewArgs(List<Expression> args) {
                // theType
                args.Add(Arguments.Self.Expression);

                // args
                foreach (DynamicMetaObject mo in Arguments.Arguments) {
                    args.Add(mo.Expression);
                }
            }

            protected CallSignature GetDynamicNewSignature() {
                return Arguments.Signature.InsertArgument(Argument.Simple);
            }
        }

        private class DefaultNewAdapter : NewAdapter {
            private readonly PythonType/*!*/ _creating;

            public DefaultNewAdapter(ArgumentValues/*!*/ ai, PythonType/*!*/ creating, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
                _creating = creating;
            }

            public override DynamicMetaObject/*!*/ GetExpression(PythonBinder/*!*/ binder) {
                PythonOverloadResolver resolver;
                if (_creating.IsSystemType || _creating.HasSystemCtor) {
                    resolver = new PythonOverloadResolver(binder, DynamicMetaObject.EmptyMetaObjects, new CallSignature(0), CodeContext);
                } else {
                    resolver = new PythonOverloadResolver(binder, new[] { Arguments.Self }, new CallSignature(1), CodeContext);
                }

                return binder.CallMethod(resolver, _creating.UnderlyingSystemType.GetConstructors(), BindingRestrictions.Empty, _creating.Name);
            }
        }

        private class ConstructorNewAdapter : NewAdapter {
            private readonly PythonType/*!*/ _creating;

            public ConstructorNewAdapter(ArgumentValues/*!*/ ai, PythonType/*!*/ creating, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
                _creating = creating;
            }

            public override DynamicMetaObject/*!*/ GetExpression(PythonBinder/*!*/ binder) {
                PythonOverloadResolver resolve;

                if (_creating.IsSystemType || _creating.HasSystemCtor) {
                    resolve = new PythonOverloadResolver(
                        binder, 
                        Arguments.Arguments, 
                        Arguments.Signature, 
                        CodeContext
                    );
                } else {
                    resolve = new PythonOverloadResolver(
                        binder, 
                        ArrayUtils.Insert(Arguments.Self, Arguments.Arguments), 
                        GetDynamicNewSignature(), 
                        CodeContext
                    );
                }

                return binder.CallMethod(
                    resolve,
                    _creating.UnderlyingSystemType.GetConstructors(),
                    Arguments.Self.Restrictions,
                    _creating.Name
                );
            }
        }

        private class BuiltinNewAdapter : NewAdapter {
            private readonly PythonType/*!*/ _creating;
            private readonly BuiltinFunction/*!*/ _ctor;

            public BuiltinNewAdapter(ArgumentValues/*!*/ ai, PythonType/*!*/ creating, BuiltinFunction/*!*/ ctor, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
                _creating = creating;
                _ctor = ctor;
            }

            public override DynamicMetaObject/*!*/ GetExpression(PythonBinder/*!*/ binder) {
                return binder.CallMethod(
                    new PythonOverloadResolver(
                        binder,
                        ArrayUtils.Insert(Arguments.Self, Arguments.Arguments),
                        Arguments.Signature.InsertArgument(new Argument(ArgumentType.Simple)),
                        CodeContext
                    ),
                    _ctor.Targets,
                    _creating.Name
                );
            }
        }

        #endregion

        #region __init__ adapters

        private abstract class InitAdapter : CallAdapter {
            protected InitAdapter(ArgumentValues/*!*/ ai, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
            }

            public abstract DynamicMetaObject/*!*/ MakeInitCall(PythonBinder/*!*/ binder, DynamicMetaObject/*!*/ createExpr);

            protected DynamicMetaObject/*!*/ MakeDefaultInit(PythonBinder/*!*/ binder, DynamicMetaObject/*!*/ createExpr, Expression/*!*/ init) {
                List<Expression> args = new List<Expression>();
                args.Add(CodeContext);
                args.Add(Expression.Convert(createExpr.Expression, typeof(object)));
                foreach (DynamicMetaObject mo in Arguments.Arguments) {
                    args.Add(mo.Expression);
                }

                return new DynamicMetaObject(
                    DynamicExpression.Dynamic(
                        ((PythonType)Arguments.Self.Value).GetLateBoundInitBinder(Arguments.Signature),
                        typeof(object),
                        args.ToArray()
                    ),
                    Arguments.Self.Restrictions.Merge(createExpr.Restrictions)
                );                
            }
        }

        private class SlotInitAdapter : InitAdapter {
            private readonly PythonTypeSlot/*!*/ _slot;
            
            public SlotInitAdapter(PythonTypeSlot/*!*/ slot, ArgumentValues/*!*/ ai, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
                _slot = slot;
            }

            public override DynamicMetaObject/*!*/ MakeInitCall(PythonBinder/*!*/ binder, DynamicMetaObject/*!*/ createExpr) {
                Expression init = Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.GetInitSlotMember)),
                    CodeContext,
                    Ast.Convert(Arguments.Self.Expression, typeof(PythonType)),
                    Ast.Convert(AstUtils.WeakConstant(_slot), typeof(PythonTypeSlot)),
                    AstUtils.Convert(createExpr.Expression, typeof(object))
                );

                return MakeDefaultInit(binder, createExpr, init);
            }
        }

        private class DefaultInitAdapter : InitAdapter {
            public DefaultInitAdapter(ArgumentValues/*!*/ ai, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
            }

            public override DynamicMetaObject/*!*/ MakeInitCall(PythonBinder/*!*/ binder, DynamicMetaObject/*!*/ createExpr) {
                // default init, we can just return the value from __new__
                return createExpr;
            }
        }

        private class BuiltinInitAdapter : InitAdapter {
            private readonly BuiltinFunction/*!*/ _method;

            public BuiltinInitAdapter(ArgumentValues/*!*/ ai, BuiltinFunction/*!*/ method, PythonContext/*!*/ state, Expression/*!*/ codeContext)
                : base(ai, state, codeContext) {
                _method = method;
            }

            public override DynamicMetaObject/*!*/ MakeInitCall(PythonBinder/*!*/ binder, DynamicMetaObject/*!*/ createExpr) {
                if (_method == InstanceOps.Init.Template) {
                    // we have a default __init__, don't call it.
                    return createExpr;
                }

                return binder.CallMethod(
                    new PythonOverloadResolver(
                        binder,
                        createExpr,
                        Arguments.Arguments,
                        Arguments.Signature,
                        CodeContext
                    ),
                    _method.Targets,
                    Arguments.Self.Restrictions
                );
            }
        }

        #endregion

        #region Helpers

        private DynamicMetaObject/*!*/ MakeIncorrectArgumentsForCallError(DynamicMetaObjectBinder/*!*/ call, ArgumentValues/*!*/ ai, ValidationInfo/*!*/ valInfo) {
            string message;

            if (Value.IsSystemType) {
                if (Value.UnderlyingSystemType.GetConstructors().Length == 0) {
                    // this is a type we can't create ANY instances of, give the user a half-way decent error message
                    message = "cannot create instances of " + Value.Name;
                } else {
                    message = InstanceOps.ObjectNewNoParameters;
                }
            } else {
                message = InstanceOps.ObjectNewNoParameters;
            }

            return BindingHelpers.AddDynamicTestAndDefer(
                call,
                new DynamicMetaObject(
                    call.Throw(
                        Ast.New(
                            typeof(TypeErrorException).GetConstructor(new Type[] { typeof(string) }),
                            AstUtils.Constant(message)
                        )
                    ),
                    GetErrorRestrictions(ai)
                ), 
                ai.Arguments,                
                valInfo
            );
        }

        private DynamicMetaObject/*!*/ MakeGenericTypeDefinitionError(DynamicMetaObjectBinder/*!*/ call, ArgumentValues/*!*/ ai, ValidationInfo/*!*/ valInfo) {
            Debug.Assert(Value.IsSystemType);
            string message = "cannot create instances of " + Value.Name + " because it is a generic type definition";

            return BindingHelpers.AddDynamicTestAndDefer(
                call,
                new DynamicMetaObject(
                    call.Throw(
                        Ast.New(
                            typeof(TypeErrorException).GetConstructor(new Type[] { typeof(string) }),
                            AstUtils.Constant(message)
                        ),
                        typeof(object)
                    ),
                    GetErrorRestrictions(ai)
                ),
                ai.Arguments,
                valInfo
            );
        }

        private DynamicMetaObject/*!*/ MakeAbstractInstantiationError(DynamicMetaObjectBinder/*!*/ call, ArgumentValues/*!*/ ai, ValidationInfo/*!*/ valInfo) {
            CodeContext context = PythonContext.GetPythonContext(call).SharedContext;
            string message = Value.GetAbstractErrorMessage(context);
            Debug.Assert(message != null);

            return BindingHelpers.AddDynamicTestAndDefer(
                call,
                new DynamicMetaObject(
                    Ast.Throw(
                        Ast.New(
                            typeof(ArgumentTypeException).GetConstructor(new Type[] { typeof(string) }),
                            AstUtils.Constant(message)
                        ),
                        typeof(object)
                    ),
                    GetErrorRestrictions(ai)
                ),
                ai.Arguments,
                valInfo
            );
        }

        private BindingRestrictions/*!*/ GetErrorRestrictions(ArgumentValues/*!*/ ai) {
            BindingRestrictions res = Restrict(this.GetRuntimeType()).Restrictions;
            res = res.Merge(GetInstanceRestriction(ai));

            foreach (DynamicMetaObject mo in ai.Arguments) {
                if (mo.HasValue) {
                    res = res.Merge(mo.Restrict(mo.GetRuntimeType()).Restrictions);
                }
            }

            return res;
        }

        private static BindingRestrictions GetInstanceRestriction(ArgumentValues ai) {
            return BindingRestrictions.GetInstanceRestriction(ai.Self.Expression, ai.Self.Value);
        }
        
        private bool HasFinalizer(DynamicMetaObjectBinder/*!*/ action) {
            // only user types have finalizers...
            if (Value.IsSystemType) return false;

            PythonTypeSlot del;
            bool hasDel = Value.TryResolveSlot(PythonContext.GetPythonContext(action).SharedContext, "__del__", out del);
            return hasDel;
        }

        private bool HasDefaultNew(DynamicMetaObjectBinder/*!*/ action) {
            PythonTypeSlot newInst;
            Value.TryResolveSlot(PythonContext.GetPythonContext(action).SharedContext, "__new__", out newInst);
            return newInst == InstanceOps.New;
        }

        private bool HasDefaultInit(DynamicMetaObjectBinder/*!*/ action) {
            PythonTypeSlot init;
            Value.TryResolveSlot(PythonContext.GetPythonContext(action).SharedContext, "__init__", out init);
            return init == InstanceOps.Init;
        }

        private bool HasDefaultNewAndInit(DynamicMetaObjectBinder/*!*/ action) {
            return HasDefaultNew(action) && HasDefaultInit(action);
        }

        /// <summary>
        /// Checks if we have a default new and init - in this case if we have any
        /// arguments we don't allow the call.
        /// </summary>
        private bool TooManyArgsForDefaultNew(DynamicMetaObjectBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            if (args.Length > 0 && HasDefaultNewAndInit(action)) {
                Argument[] infos = BindingHelpers.GetCallSignature(action).GetArgumentInfos();
                for (int i = 0; i < infos.Length; i++) {
                    Argument curArg = infos[i];

                    switch(curArg.Kind) {
                        case ArgumentType.List:
                            // Deferral?
                            if (((IList<object>)args[i].Value).Count > 0) {
                                return true;
                            }
                            break;
                        case ArgumentType.Dictionary:
                            // Deferral?
                            if (PythonOps.Length(args[i].Value) > 0) {
                                return true;
                            }
                            break;
                        default:
                            return true;
                    }                    
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a test which tests the specific version of the type.
        /// </summary>
        private ValidationInfo/*!*/ MakeVersionCheck() {
            int version = Value.Version;
            return new ValidationInfo(
                Ast.Equal(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.GetTypeVersion)),
                        Ast.Convert(Expression, typeof(PythonType))
                    ),
                    AstUtils.Constant(version)
                )
            );
        }

        private bool IsStandardDotNetType(DynamicMetaObjectBinder/*!*/ action) {
            PythonContext bState = PythonContext.GetPythonContext(action);

            return
                Value.IsSystemType &&
                !Value.IsPythonType &&
                !bState.Binder.HasExtensionTypes(Value.UnderlyingSystemType) &&
                !typeof(Delegate).IsAssignableFrom(Value.UnderlyingSystemType) &&
                !Value.UnderlyingSystemType.IsArray;                                
        }

        #endregion        
    }
}
