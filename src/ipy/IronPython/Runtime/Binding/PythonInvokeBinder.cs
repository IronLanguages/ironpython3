// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using Microsoft.Scripting.Ast;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {

    /// <summary>
    /// The Action used for Python call sites.  This supports both splatting of position and keyword arguments.
    /// 
    /// When a foreign object is encountered the arguments are expanded into normal position/keyword arguments.
    /// </summary>
    internal class PythonInvokeBinder : DynamicMetaObjectBinder, IPythonSite, IExpressionSerializable, ILightExceptionBinder {
        private readonly PythonContext/*!*/ _context;
        private readonly CallSignature _signature;
        private LightThrowBinder _lightThrowBinder;

        public PythonInvokeBinder(PythonContext/*!*/ context, CallSignature signature) {
            _context = context;
            _signature = signature;
        }

        #region DynamicMetaObjectBinder overrides

        /// <summary>
        /// Python's Invoke is a non-standard action.  Here we first try to bind through a Python
        /// internal interface (IPythonInvokable) which supports CallSignatures.  If that fails
        /// and we have an IDO then we translate to the DLR protocol through a nested dynamic site -
        /// this includes unsplatting any keyword / position arguments.  Finally if it's just a plain
        /// old .NET type we use the default binder which supports CallSignatures.
        /// </summary>
        public override DynamicMetaObject/*!*/ Bind(DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            Debug.Assert(args.Length > 0);

            DynamicMetaObject cc = target;
            DynamicMetaObject actualTarget = args[0];
            args = ArrayUtils.RemoveFirst(args);

            Debug.Assert(cc.GetLimitType() == typeof(CodeContext));

            return BindWorker(cc, actualTarget, args);
        }

        private DynamicMetaObject BindWorker(DynamicMetaObject/*!*/ context, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            // we don't have CodeContext if an IDO falls back to us when we ask them to produce the Call

            if (target is IPythonInvokable icc) {
                // call it and provide the context
                return icc.Invoke(
                    this,
                    context.Expression,
                    target,
                    args
                );
            } else if (target.Value is IDynamicMetaObjectProvider) {
                return InvokeForeignObject(target, args);
            }
#if FEATURE_COM
            else if (Microsoft.Scripting.ComInterop.ComBinder.CanComBind(target.Value)) {
                return InvokeForeignObject(target, args);
            }
#endif

            return Fallback(context.Expression, target, args);
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            if (args[1] is IFastInvokable ifi) {
                FastBindResult<T> res = ifi.MakeInvokeBinding(site, this, (CodeContext)args[0], ArrayUtils.ShiftLeft(args, 2));
                if (res.Target != null) {
                    if (res.ShouldCache) {
                        CacheTarget(res.Target);
                    }

                    return res.Target;
                }
            }

            if (args[1] is Types.PythonType) {
                PerfTrack.NoteEvent(PerfTrack.Categories.BindingSlow, "InvokeNoFast " + ((Types.PythonType)args[1]).Name);
            } else {
                PerfTrack.NoteEvent(PerfTrack.Categories.BindingSlow, "InvokeNoFast " + CompilerHelpers.GetType(args[1]));
            }

            var target = this.LightBind<T>(args, Context.Options.CompilationThreshold);
            CacheTarget(target);
            return target;
        }

        /// <summary>
        /// Fallback - performs the default binding operation if the object isn't recognized
        /// as being invokable.
        /// </summary>
        internal DynamicMetaObject/*!*/ Fallback(Expression codeContext, DynamicMetaObject target, DynamicMetaObject/*!*/[]/*!*/ args) {
            if (target.NeedsDeferral()) {
                return Defer(args);
            }

            return PythonProtocol.Call(this, target, args) ??
                Context.Binder.Create(Signature, target, args, codeContext) ??
                Context.Binder.Call(Signature, new PythonOverloadResolverFactory(Context.Binder, codeContext), target, args);
        }

        #endregion

        #region Object Overrides

        public override int GetHashCode() {
            return _signature.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (!(obj is PythonInvokeBinder ob)) {
                return false;
            }

            return ob._context.Binder == _context.Binder &&
                _signature == ob._signature;
        }

        public override string ToString() {
            return "Python Invoke " + Signature.ToString();
        }

        #endregion

        #region Public API Surface

        /// <summary>
        /// Gets the CallSignature for this invocation which describes how the MetaObject array
        /// is to be mapped.
        /// </summary>
        public CallSignature Signature {
            get {
                return _signature;
            }
        }

        #endregion

        #region Implementation Details

        /// <summary>
        /// Creates a nested dynamic site which uses the unpacked arguments.
        /// </summary>
        internal DynamicMetaObject InvokeForeignObject(DynamicMetaObject target, DynamicMetaObject[] args) {
            // need to unpack any dict / list arguments...
            CallInfo callInfo;
            List<Expression> metaArgs;
            Expression test;
            BindingRestrictions restrictions;

            TranslateArguments(target, args, out callInfo, out metaArgs, out test, out restrictions);

            Debug.Assert(metaArgs.Count > 0);

            return BindingHelpers.AddDynamicTestAndDefer(
                this,
                new DynamicMetaObject(
                    DynamicExpression.Dynamic(
                        _context.CompatInvoke(callInfo),
                        typeof(object),
                        metaArgs.ToArray()
                    ),
                    restrictions.Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target.Expression, target.GetLimitType()))
                ),
                args,
                new ValidationInfo(test)
            );
        }

        /// <summary>
        /// Translates our CallSignature into a DLR Argument list and gives the simple MetaObject's which are extracted
        /// from the tuple or dictionary parameters being splatted.
        /// </summary>
        private void TranslateArguments(DynamicMetaObject target, DynamicMetaObject/*!*/[]/*!*/ args, out CallInfo /*!*/ callInfo, out List<Expression/*!*/>/*!*/ metaArgs, out Expression test, out BindingRestrictions restrictions) {
            Argument[] argInfo = _signature.GetArgumentInfos();

            List<string> namedArgNames = new List<string>();
            metaArgs = new List<Expression>();
            metaArgs.Add(target.Expression);
            Expression splatArgTest = null;
            Expression splatKwArgTest = null;
            restrictions = BindingRestrictions.Empty;

            for (int i = 0; i < argInfo.Length; i++) {
                Argument ai = argInfo[i];

                switch (ai.Kind) {
                    case ArgumentType.Dictionary:
                        PythonDictionary iac = (PythonDictionary)args[i].Value;
                        List<string> argNames = new List<string>();

                        foreach (KeyValuePair<object, object> kvp in iac) {
                            string key = (string)kvp.Key;
                            namedArgNames.Add(key);
                            argNames.Add(key);

                            metaArgs.Add(
                                Expression.Call(
                                    AstUtils.Convert(args[i].Expression, typeof(PythonDictionary)),
                                    typeof(PythonDictionary).GetMethod("get_Item", new[] { typeof(object) }),
                                    AstUtils.Constant(key)
                                )
                            );
                        }

                        restrictions = restrictions.Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(args[i].Expression, args[i].GetLimitType()));
                        splatKwArgTest = Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.CheckDictionaryMembers)),
                            AstUtils.Convert(args[i].Expression, typeof(PythonDictionary)),
                            AstUtils.Constant(argNames.ToArray())
                        );
                        break;
                    case ArgumentType.List:
                        IList<object> splattedArgs = (IList<object>)args[i].Value;
                        splatArgTest = Expression.Equal(
                            Expression.Property(AstUtils.Convert(args[i].Expression, args[i].GetLimitType()), typeof(ICollection<object>).GetProperty("Count")),
                            AstUtils.Constant(splattedArgs.Count)
                        );

                        for (int splattedArg = 0; splattedArg < splattedArgs.Count; splattedArg++) {
                            metaArgs.Add(
                                Expression.Call(
                                    AstUtils.Convert(args[i].Expression, typeof(IList<object>)),
                                    typeof(IList<object>).GetMethod("get_Item"),
                                    AstUtils.Constant(splattedArg)
                                )
                            );
                        }

                        restrictions = restrictions.Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(args[i].Expression, args[i].GetLimitType()));
                        break;
                    case ArgumentType.Named:
                        namedArgNames.Add(ai.Name);
                        metaArgs.Add(args[i].Expression);
                        break;
                    case ArgumentType.Simple:
                        metaArgs.Add(args[i].Expression);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            
            callInfo = new CallInfo(metaArgs.Count - 1, namedArgNames.ToArray());

            test = splatArgTest;
            if (splatKwArgTest != null) {
                test = test != null ? Expression.AndAlso(test, splatKwArgTest) : splatKwArgTest;
            }
        }

        #endregion

        #region IPythonSite Members

        public PythonContext Context {
            get { return _context; }
        }

        #endregion

        #region IExpressionSerializable Members

        public virtual Expression CreateExpression() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeInvokeAction)),
                BindingHelpers.CreateBinderStateExpression(),
                Signature.CreateExpression()
            );
        }

        #endregion

        #region ILightExceptionBinder Members

        public virtual bool SupportsLightThrow {
            get { return false; }
        }

        public virtual CallSiteBinder GetLightExceptionBinder() {            
            if (_lightThrowBinder == null) {
                _lightThrowBinder = new LightThrowBinder(_context, _signature);
            }
            return _lightThrowBinder;
        }

        public CallSiteBinder GetLightExceptionBinder(bool really) {
            if (really) {
                return GetLightExceptionBinder();
            }
            return this;
        }

        private class LightThrowBinder : PythonInvokeBinder {
            public LightThrowBinder(PythonContext/*!*/ context, CallSignature signature)
                : base(context, signature) {
            }

            public override bool SupportsLightThrow {
                get {
                    return true;
                }
            }

            public override CallSiteBinder GetLightExceptionBinder() {
                return this;
            }
        }

        #endregion
    }

}
