// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

using IronPython.Runtime.Types;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    /// <summary>
    /// Provides an abstraction for calling something which might be a builtin function or
    /// might be some arbitrary user defined slot.  If the object is a builtin function the
    /// call will go directly to the underlying .NET method.  If the object is an arbitrary
    /// callable object we will setup a nested dynamic site for performing the additional
    /// dispatch.
    /// 
    /// TODO: We could probably do a specific binding to the object if it's another IDyanmicObject.
    /// </summary>
    internal sealed class SlotOrFunction {
        private readonly BindingTarget _function;
        private readonly DynamicMetaObject/*!*/ _target;
        private readonly PythonTypeSlot _slot;
        public static readonly SlotOrFunction/*!*/ Empty = new SlotOrFunction(new DynamicMetaObject(AstUtils.Empty(), BindingRestrictions.Empty));

        private SlotOrFunction() {
        }

        public SlotOrFunction(BindingTarget/*!*/ function, DynamicMetaObject/*!*/ target) {
            _target = target;
            _function = function;
        }

        public SlotOrFunction(DynamicMetaObject/*!*/ target) {
            _target = target;
        }

        public SlotOrFunction(DynamicMetaObject/*!*/ target, PythonTypeSlot slot) {
            _target = target;
            _slot = slot;
        }

        public NarrowingLevel NarrowingLevel {
            get {
                if (_function != null) {
                    return _function.NarrowingLevel;
                }

                return NarrowingLevel.None;
            }
        }

        public Type/*!*/ ReturnType {
            get {
                return _target.GetLimitType();
            }
        }

        public bool MaybeNotImplemented {
            get {
                if (_function != null) {
                    var method = _function.Overload.ReflectionInfo as MethodInfo;
                    return method != null && method.ReturnTypeCustomAttributes.IsDefined(typeof(MaybeNotImplementedAttribute), false);
                }

                return true;
            }
        }

        public bool Success {
            get {
                if (_function != null) {
                    return _function.Success;
                }

                return this != Empty;
            }
        }

        public bool IsNull {
            get {
                if (_slot is PythonTypeUserDescriptorSlot && ((PythonTypeUserDescriptorSlot)_slot).Value == null) {
                    return true;
                }

                return false;
            }
        }

        public DynamicMetaObject/*!*/ Target {
            get {
                return _target;
            }
        }

        /// <summary>
        /// Combines two methods, which came from two different binary types, selecting the method which has the best
        /// set of conversions (the conversions which result in the least narrowing).
        /// </summary>
        public static bool GetCombinedTargets(SlotOrFunction fCand, SlotOrFunction rCand, out SlotOrFunction fTarget, out SlotOrFunction rTarget) {
            fTarget = rTarget = Empty;

            if (fCand.Success) {
                if (rCand.Success) {
                    if (fCand.NarrowingLevel <= rCand.NarrowingLevel) {
                        fTarget = fCand;
                        rTarget = rCand;
                    } else {
                        fTarget = Empty;
                        rTarget = rCand;
                    }
                } else {
                    fTarget = fCand;
                }
            } else if (rCand.Success) {
                rTarget = rCand;
            } else {
                return false;
            }

            return true;
        }

        public bool ShouldWarn(PythonContext context, out WarningInfo info) {
            if (_function != null) {
                return BindingWarnings.ShouldWarn(context, _function.Overload, out info);
            }

            info = null;
            return false;
        }

        public static SlotOrFunction/*!*/ GetSlotOrFunction(PythonContext/*!*/ state, string op, params DynamicMetaObject[] types) {
            PythonTypeSlot slot;
            SlotOrFunction res;
            if (TryGetBinder(state, types, op, null, out res)) {
                if (res != SlotOrFunction.Empty) {
                    return res;
                }
            } else if (MetaUserObject.GetPythonType(types[0]).TryResolveSlot(state.SharedContext, op, out slot)) {
                ParameterExpression tmp = Ast.Variable(typeof(object), "slotVal");

                Expression[] args = new Expression[types.Length - 1];
                for (int i = 1; i < types.Length; i++) {
                    args[i - 1] = types[i].Expression;
                }
                return new SlotOrFunction(
                    new DynamicMetaObject(
                        Ast.Block(
                            new ParameterExpression[] { tmp },
                            MetaPythonObject.MakeTryGetTypeMember(
                                state,
                                slot,
                                tmp,
                                types[0].Expression,
                                Ast.Call(
                                    typeof(DynamicHelpers).GetMethod(nameof(DynamicHelpers.GetPythonType)),
                                    types[0].Expression
                                )
                            ),
                            DynamicExpression.Dynamic(
                                state.Invoke(
                                    new CallSignature(args.Length)
                                ),
                                typeof(object),
                                ArrayUtils.Insert<Expression>(
                                    AstUtils.Constant(state.SharedContext),
                                    tmp,
                                    args
                                )
                            )
                        ),
                        BindingRestrictions.Combine(types).Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(types[0].Expression, types[0].GetLimitType()))
                    ),
                    slot
                );
            }

            return SlotOrFunction.Empty;
        }

        internal static bool TryGetBinder(PythonContext/*!*/ state, DynamicMetaObject/*!*/[]/*!*/ types, string op, string rop, out SlotOrFunction/*!*/ res) {
            PythonType declType;
            return TryGetBinder(state, types, op, rop, out res, out declType);
        }

        /// <summary>
        /// Tries to get a MethodBinder associated with the slot for the specified type.
        /// 
        /// If a method is found the binder is set and true is returned.
        /// If nothing is found binder is null and true is returned.
        /// If something other than a method is found false is returned.
        /// 
        /// TODO: Remove rop
        /// </summary>
        internal static bool TryGetBinder(PythonContext/*!*/ state, DynamicMetaObject/*!*/[]/*!*/ types, string op, string rop, out SlotOrFunction/*!*/ res, out PythonType declaringType) {
            declaringType = null;

            DynamicMetaObject xType = types[0];
            BuiltinFunction xBf;
            if (!BindingHelpers.TryGetStaticFunction(state, op, xType, out xBf)) {
                res = SlotOrFunction.Empty;
                return false;
            }

            xBf = CheckAlwaysNotImplemented(xBf);

            BindingTarget bt;
            DynamicMetaObject binder;
            DynamicMetaObject yType = null;
            BuiltinFunction yBf = null;

            if (types.Length > 1) {
                yType = types[1];
                if (!BindingHelpers.IsSubclassOf(xType, yType) && !BindingHelpers.TryGetStaticFunction(state, rop, yType, out yBf)) {
                    res = SlotOrFunction.Empty;
                    return false;
                }

                yBf = CheckAlwaysNotImplemented(yBf);
            }

            if (yBf == xBf) {
                yBf = null;
            } else if (yBf != null && BindingHelpers.IsSubclassOf(yType, xType)) {
                xBf = null;
            }

            var mc = new PythonOverloadResolver(
                state.Binder,
                types,
                new CallSignature(types.Length),
                AstUtils.Constant(state.SharedContext)
            );

            if (xBf == null) {
                if (yBf == null) {
                    binder = null;
                    bt = null;
                } else {
                    declaringType = DynamicHelpers.GetPythonTypeFromType(yBf.DeclaringType);
                    binder = state.Binder.CallMethod(mc, yBf.Targets, BindingRestrictions.Empty, null, PythonNarrowing.None, PythonNarrowing.BinaryOperator, out bt);
                }
            } else {
                if (yBf == null) {
                    declaringType = DynamicHelpers.GetPythonTypeFromType(xBf.DeclaringType);
                    binder = state.Binder.CallMethod(mc, xBf.Targets, BindingRestrictions.Empty, null, PythonNarrowing.None, PythonNarrowing.BinaryOperator, out bt);
                } else {
                    List<MethodBase> targets = new List<MethodBase>();
                    targets.AddRange(xBf.Targets);
                    foreach (MethodBase mb in yBf.Targets) {
                        if (!ContainsMethodSignature(targets, mb)) targets.Add(mb);
                    }

                    binder = state.Binder.CallMethod(mc, targets.ToArray(), BindingRestrictions.Empty, null, PythonNarrowing.None, PythonNarrowing.BinaryOperator, out bt);

                    foreach (MethodBase mb in yBf.Targets) {
                        if (bt.Overload.ReflectionInfo == mb) {
                            declaringType = DynamicHelpers.GetPythonTypeFromType(yBf.DeclaringType);
                            break;
                        }
                    }

                    if (declaringType == null) {
                        declaringType = DynamicHelpers.GetPythonTypeFromType(xBf.DeclaringType);
                    }
                }
            }

            if (binder != null) {
                res = new SlotOrFunction(bt, binder);
            } else {
                res = SlotOrFunction.Empty;
            }

            Debug.Assert(res != null);
            return true;
        }

        private static BuiltinFunction CheckAlwaysNotImplemented(BuiltinFunction xBf) {
            if (xBf != null) {
                bool returnsValue = false;
                foreach (MethodBase mb in xBf.Targets) {
                    if (mb.GetReturnType() != typeof(NotImplementedType)) {
                        returnsValue = true;
                        break;
                    }
                }

                if (!returnsValue) {
                    xBf = null;
                }
            }
            return xBf;
        }

        private static bool ContainsMethodSignature(IList<MethodBase/*!*/>/*!*/ existing, MethodBase/*!*/ check) {
            ParameterInfo[] pis = check.GetParameters();
            foreach (MethodBase mb in existing) {
                if (MatchesMethodSignature(pis, mb)) return true;
            }
            return false;
        }

        private static bool MatchesMethodSignature(ParameterInfo/*!*/[]/*!*/ pis, MethodBase/*!*/ mb) {
            ParameterInfo[] pis1 = mb.GetParameters();
            if (pis.Length == pis1.Length) {
                for (int i = 0; i < pis.Length; i++) {
                    if (pis[i].ParameterType != pis1[i].ParameterType) return false;
                }
                return true;
            } else {
                return false;
            }
        }
    }
}

