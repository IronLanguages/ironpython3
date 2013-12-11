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
using System.Numerics;
#else
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Math;
#endif

using System;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;
    
    /// <summary>
    /// Common helpers used by the various binding logic.
    /// </summary>
    static class BindingHelpers {
        /// <summary>
        /// Tries to get the BuiltinFunction for the given name on the type of the provided MetaObject.  
        /// 
        /// Succeeds if the MetaObject is a BuiltinFunction or BuiltinMethodDescriptor.
        /// </summary>
        internal static bool TryGetStaticFunction(PythonContext/*!*/ state, string op, DynamicMetaObject/*!*/ mo, out BuiltinFunction function) {
            PythonType type = MetaPythonObject.GetPythonType(mo);
            function = null;
            if (!String.IsNullOrEmpty(op)) {
                PythonTypeSlot xSlot;
                object val;
                if (type.TryResolveSlot(state.SharedContext, op, out xSlot) &&
                    xSlot.TryGetValue(state.SharedContext, null, type, out val)) {
                    function = TryConvertToBuiltinFunction(val);
                    if (function == null) return false;
                }
            }
            return true;
        }

        internal static bool IsNoThrow(DynamicMetaObjectBinder action) {
            PythonGetMemberBinder gmb = action as PythonGetMemberBinder;
            if (gmb != null) {
                return gmb.IsNoThrow;
            }

            return false;
        }

        internal static DynamicMetaObject/*!*/ FilterShowCls(DynamicMetaObject/*!*/ codeContext, DynamicMetaObjectBinder/*!*/ action, DynamicMetaObject/*!*/ res, Expression/*!*/ failure) {
            if (action is IPythonSite) {
                return new DynamicMetaObject(
                    Ast.Condition(
                        Ast.Call(
                            typeof(PythonOps).GetMethod("IsClsVisible"),
                            codeContext.Expression
                        ),
                        AstUtils.Convert(res.Expression, typeof(object)),
                        AstUtils.Convert(failure, typeof(object))

                    ),
                    res.Restrictions
                );
            }

            return res;
        }

        /// <summary>
        /// Gets the best CallSignature from a MetaAction.
        /// 
        /// The MetaAction should be either a Python InvokeBinder, or a DLR InvokeAction or 
        /// CreateAction.  For Python we can use a full-fidelity 
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        internal static CallSignature GetCallSignature(DynamicMetaObjectBinder/*!*/ action) {
            // Python'so own InvokeBinder which has a real sig
            PythonInvokeBinder pib = action as PythonInvokeBinder;
            if (pib != null) {
                return pib.Signature;
            }

            // DLR Invoke which has a argument array
            InvokeBinder iac = action as InvokeBinder;
            if (iac != null) {
                return CallInfoToSignature(iac.CallInfo);
            }

            InvokeMemberBinder cla = action as InvokeMemberBinder;
            if (cla != null) {
                return CallInfoToSignature(cla.CallInfo);
            }
            
            // DLR Create action which we hand off to our call code, also
            // has an argument array.
            CreateInstanceBinder ca = action as CreateInstanceBinder;
            Debug.Assert(ca != null);

            return CallInfoToSignature(ca.CallInfo);
        }

        public static Expression/*!*/ Invoke(Expression codeContext, PythonContext/*!*/ binder, Type/*!*/ resultType, CallSignature signature, params Expression/*!*/[]/*!*/ args) {
            return DynamicExpression.Dynamic(
                binder.Invoke(
                    signature
                ),
                resultType,
                ArrayUtils.Insert(codeContext, args)
            );
        }

        /// <summary>
        /// Transforms an invoke member into a Python GetMember/Invoke.  The caller should
        /// verify that the given attribute is not resolved against a normal .NET class
        /// before calling this.  If it is a normal .NET member then a fallback InvokeMember
        /// is preferred.
        /// </summary>
        internal static DynamicMetaObject/*!*/ GenericInvokeMember(InvokeMemberBinder/*!*/ action, ValidationInfo valInfo, DynamicMetaObject target, DynamicMetaObject/*!*/[]/*!*/ args) {
            if (target.NeedsDeferral()) {
                return action.Defer(args);
            }

            return AddDynamicTestAndDefer(action, 
                action.FallbackInvoke(
                    new DynamicMetaObject(
                        Binders.Get(
                            PythonContext.GetCodeContext(action),
                            PythonContext.GetPythonContext(action),
                            typeof(object),
                            action.Name,
                            target.Expression
                        ),
                        BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                    ),
                    args,
                    null
                ),
                args,
                valInfo
            );
        }

        internal static bool NeedsDeferral(DynamicMetaObject[] args) {
            foreach (DynamicMetaObject mo in args) {
                if (mo.NeedsDeferral()) {
                    return true;
                }
            }
            return false;
        }

        internal static CallSignature CallInfoToSignature(CallInfo callInfo) {
            Argument[] ai = new Argument[callInfo.ArgumentCount];
            int positionalArgNum = callInfo.ArgumentCount - callInfo.ArgumentNames.Count;

            int i;
            for (i = 0; i < positionalArgNum; i++) {
                ai[i] = new Argument(ArgumentType.Simple);
            }

            foreach (var name in callInfo.ArgumentNames) {
                ai[i++] = new Argument(
                    ArgumentType.Named,
                    name
                );
            }
            return new CallSignature(ai);
        }

        internal static Type/*!*/ GetCompatibleType(/*!*/Type t, Type/*!*/ otherType) {
            if (t != otherType) {
                if (t.IsAssignableFrom(otherType)) {
                    // subclass
                    t = otherType;
                } else if (otherType.IsAssignableFrom(t)) {
                    // keep t
                } else {
                    // incompatible, both go to object
                    t = typeof(object);
                }
            }
            return t;
        }

        /// <summary>
        /// Determines if the type associated with the first MetaObject is a subclass of the
        /// type associated with the second MetaObject.
        /// </summary>
        internal static bool IsSubclassOf(DynamicMetaObject/*!*/ xType, DynamicMetaObject/*!*/ yType) {
            PythonType x = MetaPythonObject.GetPythonType(xType);
            PythonType y = MetaPythonObject.GetPythonType(yType);
            return x.IsSubclassOf(y);
        }
        
        private static BuiltinFunction TryConvertToBuiltinFunction(object o) {
            BuiltinMethodDescriptor md = o as BuiltinMethodDescriptor;

            if (md != null) {
                return md.Template;
            }

            return o as BuiltinFunction;
        }

        internal static DynamicMetaObject/*!*/ AddDynamicTestAndDefer(DynamicMetaObjectBinder/*!*/ operation, DynamicMetaObject/*!*/ res, DynamicMetaObject/*!*/[] args, ValidationInfo typeTest, params ParameterExpression[] temps) {
            return AddDynamicTestAndDefer(operation, res, args, typeTest, null, temps);
        }

        internal static DynamicMetaObject/*!*/ AddDynamicTestAndDefer(DynamicMetaObjectBinder/*!*/ operation, DynamicMetaObject/*!*/ res, DynamicMetaObject/*!*/[] args, ValidationInfo typeTest, Type deferType, params ParameterExpression[] temps) {
            if (typeTest != null) {
                if (typeTest.Test != null) {
                    // add the test and a validator if persent
                    Expression defer = operation.GetUpdateExpression(deferType ?? typeof(object));

                    Type bestType = BindingHelpers.GetCompatibleType(defer.Type, res.Expression.Type);

                    res = new DynamicMetaObject(
                        Ast.Condition(
                            typeTest.Test,
                            AstUtils.Convert(res.Expression, bestType),
                            AstUtils.Convert(defer, bestType)
                        ),
                        res.Restrictions 
                    );
                }
            } 
            
            if (temps.Length > 0) {
                // finally add the scoped variables
                res = new DynamicMetaObject(
                    Ast.Block(temps, res.Expression),
                    res.Restrictions,
                    null
                );
            }

            return res;
        }
        
        internal static ValidationInfo/*!*/ GetValidationInfo(DynamicMetaObject/*!*/ tested, PythonType type) {
            return new ValidationInfo(
                Ast.AndAlso(
                    Ast.TypeEqual(tested.Expression, type.UnderlyingSystemType),
                    CheckTypeVersion(
                        AstUtils.Convert(tested.Expression, type.UnderlyingSystemType), 
                        type.Version
                    )
                )
            );
        }

        internal static ValidationInfo/*!*/ GetValidationInfo(params DynamicMetaObject/*!*/[]/*!*/ args) {
            Expression typeTest = null;
            for (int i = 0; i < args.Length; i++) {
                if (args[i].HasValue) {
                    IPythonObject val = args[i].Value as IPythonObject;
                    if (val != null) {
                        Expression test = BindingHelpers.CheckTypeVersion(
                            AstUtils.Convert(args[i].Expression, val.GetType()),
                            val.PythonType.Version
                        );

                        test = Ast.AndAlso(
                            Ast.TypeEqual(args[i].Expression, val.GetType()),
                            test
                        );

                        if (typeTest != null) {
                            typeTest = Ast.AndAlso(typeTest, test);
                        } else {
                            typeTest = test;
                        }
                    }
                }
            }

            return new ValidationInfo(typeTest);
        }

        internal static MethodCallExpression/*!*/ CheckTypeVersion(Expression/*!*/ tested, int version) {
#if FEATURE_REFEMIT
            FieldInfo fi = tested.Type.GetField(NewTypeMaker.ClassFieldName);
#else
            FieldInfo fi = null;
#endif
            if (fi == null) {
                return Ast.Call(
                    typeof(PythonOps).GetMethod("CheckTypeVersion"),
                    AstUtils.Convert(tested, typeof(object)),
                    AstUtils.Constant(version)
                );
            }

            Debug.Assert(tested.Type != typeof(object));
            return Ast.Call(
                typeof(PythonOps).GetMethod("CheckSpecificTypeVersion"),
                Ast.Field(
                    tested,
                    fi
                ),
                AstUtils.Constant(version)
            );
        }

        /// <summary>
        /// Adds a try/finally which enforces recursion limits around the target method.
        /// </summary>
        internal static Expression AddRecursionCheck(PythonContext pyContext, Expression expr) {
            ParameterExpression tmp = Ast.Variable(expr.Type, "callres");

            expr = 
                Ast.Block(
                    new [] { tmp },
                    AstUtils.Try(
                        Ast.Call(typeof(PythonOps).GetMethod("FunctionPushFrame"), Ast.Constant(pyContext)),
                        Ast.Assign(tmp, expr)
                    ).Finally(
                        Ast.Call(typeof(PythonOps).GetMethod("FunctionPopFrame"))
                    ),
                    tmp
                );
            return expr;
        }

        internal static Expression CreateBinderStateExpression() {
            return Compiler.Ast.PythonAst._globalContext;
        }

        /// <summary>
        /// Helper to do fallback for Invoke's so we can handle both StandardAction and Python's 
        /// InvokeBinder.
        /// </summary>
        internal static DynamicMetaObject/*!*/ InvokeFallback(DynamicMetaObjectBinder/*!*/ action, Expression codeContext, DynamicMetaObject target, DynamicMetaObject/*!*/[]/*!*/ args) {
            InvokeBinder act = action as InvokeBinder;
            if (act != null) {
                return act.FallbackInvoke(target, args);
            }

            PythonInvokeBinder invoke = action as PythonInvokeBinder;
            if (invoke != null) {
                return invoke.Fallback(codeContext, target, args);
            }

            // unreachable, we always have one of these binders
            throw new InvalidOperationException();
        }

        internal static Expression/*!*/ TypeErrorForProtectedMember(Type/*!*/ type, string/*!*/ name) {
            Debug.Assert(!typeof(IPythonObject).IsAssignableFrom(type));

            return Ast.Throw(
                Ast.Call(
                    typeof(PythonOps).GetMethod("TypeErrorForProtectedMember"),
                    AstUtils.Constant(type),
                    AstUtils.Constant(name)
                ),
                typeof(object)
            );
        }

        internal static DynamicMetaObject/*!*/ TypeErrorGenericMethod(Type/*!*/ type, string/*!*/ name, BindingRestrictions/*!*/ restrictions) {
            return new DynamicMetaObject(
                Ast.Throw(
                    Ast.Call(
                        typeof(PythonOps).GetMethod("TypeErrorForGenericMethod"),
                        AstUtils.Constant(type),
                        AstUtils.Constant(name)
                    ),
                    typeof(object)
                ),
                restrictions
            );
        }

        internal static bool IsDataMember(object p) {
            if (p is PythonFunction || p is BuiltinFunction || p is PythonType || p is BuiltinMethodDescriptor || p is OldClass || p is staticmethod || p is classmethod || p is Method || p is Delegate) {
                return false;
            }

            return true;
        }

        internal static DynamicMetaObject AddPythonBoxing(DynamicMetaObject res) {
            if (res.Expression.Type.IsValueType()) {
                // Use Python boxing rules if we're return a value type
                res = new DynamicMetaObject(
                    AddPythonBoxing(res.Expression),
                    res.Restrictions
                );
            }
            return res;
        }

        internal static Expression AddPythonBoxing(Expression res) {
            return AstUtils.Convert(res, typeof(object));
        }


        /// <summary>
        /// Converts arguments into a form which can be used for COM interop.
        /// 
        /// The argument is only converted if we have an IronPython specific
        /// conversion when calling COM methods.
        /// </summary>
        internal static DynamicMetaObject[] GetComArguments(DynamicMetaObject[] args) {
            DynamicMetaObject[] res = null;
            for (int i = 0; i < args.Length; i++) {
                DynamicMetaObject converted = GetComArgument(args[i]);
                if (!ReferenceEquals(converted, args[i])) {
                    if (res == null) {
                        res = new DynamicMetaObject[args.Length];
                        for (int j = 0; j < i; j++) {
                            res[j] = args[j];
                        }
                    }

                    res[i] = converted;
                } else if (res != null) {
                    res[i] = args[i];
                }
            }

            return res ?? args;
        }

        /// <summary>
        /// Converts a single argument into a form which can be used for COM 
        /// interop.  
        /// 
        /// The argument is only converted if we have an IronPython specific
        /// conversion when calling COM methods.
        /// </summary>
        internal static DynamicMetaObject GetComArgument(DynamicMetaObject arg) {
            IComConvertible comConv = arg as IComConvertible;
            if (comConv != null) {
                return comConv.GetComMetaObject();
            }

            if (arg.Value != null) {
                Type type = arg.Value.GetType();
                if (type == typeof(BigInteger)) {
                    return new DynamicMetaObject(
                        Ast.Convert(AstUtils.Convert(arg.Expression, typeof(BigInteger)), typeof(double)),
                        BindingRestrictions.GetTypeRestriction(arg.Expression, type)
                    );
                }
            }

            return arg;
        }

        internal static BuiltinFunction.BindingResult CheckLightThrow(DynamicMetaObjectBinder call, DynamicMetaObject res, BindingTarget target) {
            return new BuiltinFunction.BindingResult(target, CheckLightThrowMO(call, res, target));
        }

        internal static DynamicMetaObject CheckLightThrowMO(DynamicMetaObjectBinder call, DynamicMetaObject res, BindingTarget target) {
            if (target.Success && target.Overload.ReflectionInfo.IsDefined(typeof(LightThrowingAttribute), false)) {
                if (!call.SupportsLightThrow()) {
                    res = new DynamicMetaObject(
                        LightExceptions.CheckAndThrow(res.Expression),
                        res.Restrictions
                    );
                }
            }
            return res;
        }
    }

    internal class ValidationInfo {
        public readonly Expression Test;
        public static readonly ValidationInfo Empty = new ValidationInfo(null);

        public ValidationInfo(Expression test) {
            Test = test;
        }
    }
}
