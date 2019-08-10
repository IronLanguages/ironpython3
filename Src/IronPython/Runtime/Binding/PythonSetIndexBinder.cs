// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    internal class PythonSetIndexBinder : SetIndexBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonSetIndexBinder(PythonContext/*!*/ context, int argCount)
            : base(new CallInfo(argCount)) {
            _context = context;
        }

        public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject errorSuggestion) {
#if FEATURE_COM
            DynamicMetaObject com;
            if (Microsoft.Scripting.ComInterop.ComBinder.TryBindSetIndex(this, target, BindingHelpers.GetComArguments(indexes), BindingHelpers.GetComArgument(value), out com)) {
                return com;
            }
#endif
            
            DynamicMetaObject[] finalArgs = new DynamicMetaObject[indexes.Length + 2];
            finalArgs[0] = target;
            for (int i = 0; i < indexes.Length; i++) {
                finalArgs[i + 1] = indexes[i];
            }
            finalArgs[finalArgs.Length - 1] = value;

            return PythonProtocol.Index(this, PythonIndexType.SetItem, finalArgs, errorSuggestion);
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            if (args[0] != null && args[0].GetType() == typeof(PythonDictionary)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object, object>(DictAssign);
                }
            }

            return base.BindDelegate(site, args);
        }
        
        private object DictAssign(CallSite site, object dict, object key, object value) {
            if (dict != null && dict.GetType() == typeof(PythonDictionary)) {
                ((PythonDictionary)dict)[key] = value;
                return value;
            }

            return ((CallSite<Func<CallSite, object, object, object, object>>)site).Update(site, dict, key, value);
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            PythonSetIndexBinder ob = obj as PythonSetIndexBinder;
            if (ob == null) {
                return false;
            }

            return ob._context.Binder == _context.Binder && base.Equals(obj);
        }

        #region IPythonSite Members

        public PythonContext/*!*/ Context {
            get { return _context; }
        }

        #endregion

        #region IExpressionSerializable Members

        public Expression/*!*/ CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeSetIndexAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(CallInfo.ArgumentCount)
            );
        }

        #endregion
    }
}
