// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Operations;

using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal class PythonGetIndexBinder : GetIndexBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonGetIndexBinder(PythonContext/*!*/ context, int argCount)
            : base(new CallInfo(argCount)) {
            _context = context;
        }

        public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion) {
#if FEATURE_COM
            DynamicMetaObject com;
            if (Microsoft.Scripting.ComInterop.ComBinder.TryBindGetIndex(this, target, BindingHelpers.GetComArguments(indexes), out com)) {
                return com;
            }
#endif
            return PythonProtocol.Index(this, PythonIndexType.GetItem, ArrayUtils.Insert(target, indexes), errorSuggestion);
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            if (CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (CompilerHelpers.GetType(args[0]) == typeof(PythonList)) {
                    if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                        return (T)(object)new Func<CallSite, object, object, object>(ListIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                        return (T)(object)new Func<CallSite, object, int, object>(ListIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, PythonList, object, object>)) {
                        return (T)(object)new Func<CallSite, PythonList, object, object>(ListIndex);
                    }
                } else if (CompilerHelpers.GetType(args[0]) == typeof(PythonTuple)) {
                    if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                        return (T)(object)new Func<CallSite, object, object, object>(TupleIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                        return (T)(object)new Func<CallSite, object, int, object>(TupleIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, PythonTuple, object, object>)) {
                        return (T)(object)new Func<CallSite, PythonTuple, object, object>(TupleIndex);
                    }
                } else if (CompilerHelpers.GetType(args[0]) == typeof(string)) {
                    if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                        return (T)(object)new Func<CallSite, object, object, object>(StringIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                        return (T)(object)new Func<CallSite, object, int, object>(StringIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, string, object, object>)) {
                        return (T)(object)new Func<CallSite, string, object, object>(StringIndex);
                    }
                }
            }

            return base.BindDelegate<T>(site, args);
        }

        private object ListIndex(CallSite site, PythonList target, object index) {
            if (target != null && index != null && index.GetType() == typeof(int)) {
                return target[(int)index];
            }

            return ((CallSite<Func<CallSite, PythonList, object, object>>)site).Update(site, target, index);
        }

        private object ListIndex(CallSite site, object target, object index) {
            // using as is ok here because [] is virtual and will call the user method if
            // we have a user defined subclass of list.
            if (target is PythonList lst && index != null && index.GetType() == typeof(int)) {
                return lst[(int)index];
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, target, index);
        }

        private object ListIndex(CallSite site, object target, int index) {
            if (target is PythonList lst) {
                return lst[index];
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, target, index);
        }

        private object TupleIndex(CallSite site, PythonTuple target, object index) {
            if (target != null && index != null && index.GetType() == typeof(int)) {
                return target[(int)index];
            }

            return ((CallSite<Func<CallSite, PythonTuple, object, object>>)site).Update(site, target, index);
        }

        private object TupleIndex(CallSite site, object target, object index) {
            if (target is PythonTuple lst && index != null && index.GetType() == typeof(int)) {
                return lst[(int)index];
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, target, index);
        }

        private object TupleIndex(CallSite site, object target, int index) {
            if (target is PythonTuple lst) {
                return lst[index];
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, target, index);
        }

        private object StringIndex(CallSite site, string target, object index) {
            if (target != null && index != null && index.GetType() == typeof(int)) {
                return StringOps.GetItem(target, (int)index);
            }

            return ((CallSite<Func<CallSite, string, object, object>>)site).Update(site, target, index);
        }

        private object StringIndex(CallSite site, object target, object index) {
            if (target is string str && index != null && index.GetType() == typeof(int)) {
                return StringOps.GetItem(str, (int)index);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, target, index);
        }

        private object StringIndex(CallSite site, object target, int index) {
            if (target is string str) {
                return StringOps.GetItem(str, index);
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, target, index);
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (!(obj is PythonGetIndexBinder ob)) {
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
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeGetIndexAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(CallInfo.ArgumentCount)
            );
        }

        #endregion
    }
}
