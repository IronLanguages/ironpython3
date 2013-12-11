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
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    class MetaOldClass : MetaPythonObject, IPythonInvokable, IPythonGetable, IPythonOperable, IPythonConvertible {
        public MetaOldClass(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, OldClass/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {            
            return MakeCallRule(pythonInvoke, codeContext, args);
        }

        #endregion

        #region IPythonGetable Members

        public DynamicMetaObject GetMember(PythonGetMemberBinder member, DynamicMetaObject codeContext) {
            // no codeContext filtering but avoid an extra site by handling this action directly
            return MakeGetMember(member, codeContext);
        }

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            return BindingHelpers.GenericInvokeMember(action, null, this, args);
        }

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ call, params DynamicMetaObject/*!*/[]/*!*/ args) {
            return MakeCallRule(call, AstUtils.Constant(PythonContext.GetPythonContext(call).SharedContext), args);
        }

        public override DynamicMetaObject/*!*/ BindCreateInstance(CreateInstanceBinder/*!*/ create, params DynamicMetaObject/*!*/[]/*!*/ args) {
            return MakeCallRule(create, AstUtils.Constant(PythonContext.GetPythonContext(create).SharedContext), args);
        }

        public override DynamicMetaObject/*!*/ BindGetMember(GetMemberBinder/*!*/ member) {
            return MakeGetMember(member, PythonContext.GetCodeContextMO(member));
        }

        public override DynamicMetaObject/*!*/ BindSetMember(SetMemberBinder/*!*/ member, DynamicMetaObject/*!*/ value) {
            return MakeSetMember(member.Name, value);
        }

        public override DynamicMetaObject/*!*/ BindDeleteMember(DeleteMemberBinder/*!*/ member) {
            return MakeDeleteMember(member);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            return ConvertWorker(conversion, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);
        }

        public DynamicMetaObject BindConvert(PythonConversionBinder binder) {
            return ConvertWorker(binder, binder.Type, binder.ResultKind);
        }

        public DynamicMetaObject ConvertWorker(DynamicMetaObjectBinder binder, Type toType, ConversionResultKind kind) {        
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "OldClass Convert");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "OldClass Convert");
            if (toType.IsSubclassOf(typeof(Delegate))) {
                return MakeDelegateTarget(binder, toType, Restrict(typeof(OldClass)));
            }
            return FallbackConvert(binder);
        }

        public override System.Collections.Generic.IEnumerable<string> GetDynamicMemberNames() {
            foreach (object o in ((IPythonMembersList)Value).GetMemberNames(DefaultContext.Default)) {
                if (o is string) {
                    yield return (string)o;
                }
            }
        }

        #endregion

        #region Calls

        private DynamicMetaObject/*!*/ MakeCallRule(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject[] args) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "OldClass Invoke w/ " + args.Length + " args");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "OldClass Invoke");

            CallSignature signature = BindingHelpers.GetCallSignature(call);
            // TODO: If we know __init__ wasn't present we could construct the OldInstance directly.

            Expression[] exprArgs = new Expression[args.Length];
            for (int i = 0; i < args.Length; i++) {
                exprArgs[i] = args[i].Expression;
            }

            ParameterExpression init = Ast.Variable(typeof(object), "init");
            ParameterExpression instTmp = Ast.Variable(typeof(object), "inst");
            DynamicMetaObject self = Restrict(typeof(OldClass));

            return new DynamicMetaObject(
                Ast.Block(
                    new ParameterExpression[] { init, instTmp },
                    Ast.Assign(
                        instTmp,
                        Ast.New(
                            typeof(OldInstance).GetConstructor(new Type[] { typeof(CodeContext), typeof(OldClass) }),
                            codeContext,
                            self.Expression
                        )
                    ),
                    Ast.Condition(
                        Expression.Not(
                            Expression.TypeIs(
                                Expression.Assign(
                                    init,
                                    Ast.Call(
                                        typeof(PythonOps).GetMethod("OldClassTryLookupInit"),
                                        self.Expression,
                                        instTmp
                                    )
                                ),
                                typeof(OperationFailed)
                            )
                        ),
                        DynamicExpression.Dynamic(
                            PythonContext.GetPythonContext(call).Invoke(
                                signature
                            ),
                            typeof(object),
                            ArrayUtils.Insert<Expression>(codeContext, init, exprArgs)
                        ),
                        NoInitCheckNoArgs(signature, self, args)
                    ),
                    instTmp
                ),
                self.Restrictions.Merge(BindingRestrictions.Combine(args))
            );
        }

        private static Expression NoInitCheckNoArgs(CallSignature signature, DynamicMetaObject self, DynamicMetaObject[] args) {
            int unusedCount = args.Length;

            Expression dictExpr = GetArgumentExpression(signature, ArgumentType.Dictionary, ref unusedCount, args);
            Expression listExpr = GetArgumentExpression(signature, ArgumentType.List, ref unusedCount, args);

            if (signature.IsSimple || unusedCount > 0) {
                if (args.Length > 0) {
                    return Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassMakeCallError"),
                        self.Expression
                    );
                }

                return AstUtils.Constant(null);
            }

            return Ast.Call(
                typeof(PythonOps).GetMethod("OldClassCheckCallError"),
                self.Expression,
                dictExpr,
                listExpr
            );
        }

        private static Expression GetArgumentExpression(CallSignature signature, ArgumentType kind, ref int unusedCount, DynamicMetaObject/*!*/[]/*!*/ args) {
            int index = signature.IndexOf(kind);
            if (index != -1) {
                unusedCount--;
                return args[index].Expression;
            }

            return AstUtils.Constant(null);
        }

        public static object MakeCallError() {
            // Normally, if we have an __init__ method, the method binder detects signature mismatches.
            // This can happen when a class does not define __init__ and therefore does not take any arguments.
            // Beware that calls like F(*(), **{}) have 2 arguments but they're empty and so it should still
            // match against def F(). 
            throw PythonOps.TypeError("this constructor takes no arguments");
        }

        #endregion

        #region Member Access

        private DynamicMetaObject/*!*/ MakeSetMember(string/*!*/ name, DynamicMetaObject/*!*/ value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "OldClass SetMember");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "OldClass SetMember");
            DynamicMetaObject self = Restrict(typeof(OldClass));

            Expression call, valueExpr = AstUtils.Convert(value.Expression, typeof(object));
            switch (name) {
                case "__bases__":
                    call = Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassSetBases"),
                        self.Expression,
                        valueExpr
                    );
                    break;
                case "__name__":
                    call = Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassSetName"),
                        self.Expression,
                        valueExpr
                    );
                    break;
                case "__dict__":
                    call = Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassSetDictionary"),
                        self.Expression,
                        valueExpr
                    );
                    break;
                default:
                    call = Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassSetNameHelper"),
                        self.Expression,
                        AstUtils.Constant(name),
                        valueExpr
                    );
                    break;
            }

            return new DynamicMetaObject(
                call,
                self.Restrictions.Merge(value.Restrictions)
            );
        }

        private DynamicMetaObject/*!*/ MakeDeleteMember(DeleteMemberBinder/*!*/ member) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "OldClass DeleteMember");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "OldClass DeleteMember");
            DynamicMetaObject self = Restrict(typeof(OldClass));

            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod("OldClassDeleteMember"),
                    AstUtils.Constant(PythonContext.GetPythonContext(member).SharedContext),
                    self.Expression,
                    AstUtils.Constant(member.Name)
                ),
                self.Restrictions
            );
        }

        private DynamicMetaObject/*!*/ MakeGetMember(DynamicMetaObjectBinder/*!*/ member, DynamicMetaObject codeContext) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "OldClass GetMember");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "OldClass GetMember");
            DynamicMetaObject self = Restrict(typeof(OldClass));

            Expression target;
            string memberName = GetGetMemberName(member);
            switch (memberName) {
                case "__dict__":
                    target = Ast.Block(
                        Ast.Call(
                            typeof(PythonOps).GetMethod("OldClassDictionaryIsPublic"),
                            self.Expression
                        ),
                        Ast.Call(
                            typeof(PythonOps).GetMethod("OldClassGetDictionary"),
                            self.Expression
                        )
                    );
                    break;
                case "__bases__":
                    target = Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassGetBaseClasses"),
                        self.Expression
                    );
                    break;
                case "__name__":
                    target = Ast.Call(
                        typeof(PythonOps).GetMethod("OldClassGetName"),
                        self.Expression
                    );
                    break;
                default:
                    ParameterExpression tmp = Ast.Variable(typeof(object), "lookupVal");
                    return new DynamicMetaObject(
                        Ast.Block(
                            new ParameterExpression[] { tmp },
                            Ast.Condition(
                                Expression.Not(
                                    Expression.TypeIs(
                                        Expression.Assign(
                                            tmp,
                                            Ast.Call(
                                                typeof(PythonOps).GetMethod("OldClassTryLookupValue"),
                                                AstUtils.Constant(PythonContext.GetPythonContext(member).SharedContext),
                                                self.Expression,
                                                AstUtils.Constant(memberName)
                                            )
                                        ),
                                        typeof(OperationFailed)
                                    )
                                ),
                                tmp,
                                AstUtils.Convert(
                                    GetMemberFallback(this, member, codeContext).Expression,
                                    typeof(object)
                                )
                            )
                        ),
                        self.Restrictions
                    );
            }

            return new DynamicMetaObject(
                target,
                self.Restrictions
            );
        }       

        #endregion

        #region Helpers

        public new OldClass/*!*/ Value {
            get {
                return (OldClass)base.Value;
            }
        }

        #endregion        
    
        #region IPythonOperable Members

        DynamicMetaObject IPythonOperable.BindOperation(PythonOperationBinder action, DynamicMetaObject[] args) {
            if (action.Operation == PythonOperationKind.IsCallable) {
                return new DynamicMetaObject(
                    AstUtils.Constant(true),
                    Restrictions.Merge(BindingRestrictions.GetTypeRestriction(Expression, typeof(OldClass)))
                );
            }

            return null;
        }

        #endregion
    }
}
