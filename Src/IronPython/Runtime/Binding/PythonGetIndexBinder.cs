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
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Generation;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    class PythonGetIndexBinder : GetIndexBinder, IPythonSite, IExpressionSerializable {
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
                if (CompilerHelpers.GetType(args[0]) == typeof(List)) {
                    if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                        return (T)(object)new Func<CallSite, object, object, object>(ListIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                        return (T)(object)new Func<CallSite, object, int, object>(ListIndex);
                    } else if (typeof(T) == typeof(Func<CallSite, List, object, object>)) {
                        return (T)(object)new Func<CallSite, List, object, object>(ListIndex);
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

        private object ListIndex(CallSite site, List target, object index) {
            if (target != null && index != null && index.GetType() == typeof(int)) {
                return target[(int)index];
            }

            return ((CallSite<Func<CallSite, List, object, object>>)site).Update(site, target, index);
        }

        private object ListIndex(CallSite site, object target, object index) {
            // using as is ok here because [] is virtual and will call the user method if
            // we have a user defined subclass of list.
            List lst = target as List;
            if (lst != null && index != null && index.GetType() == typeof(int)) {
                return lst[(int)index];
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, target, index);
        }

        private object ListIndex(CallSite site, object target, int index) {
            List lst = target as List;
            if (lst != null) {
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
            PythonTuple lst = target as PythonTuple;
            if (lst != null && index != null && index.GetType() == typeof(int)) {
                return lst[(int)index];
            }
            
            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, target, index);
        }

        private object TupleIndex(CallSite site, object target, int index) {
            PythonTuple lst = target as PythonTuple;
            if (lst != null) {
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
            string str = target as string;
            if (str != null && index != null && index.GetType() == typeof(int)) {
                return StringOps.GetItem(str, (int)index);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, target, index);
        }

        private object StringIndex(CallSite site, object target, int index) {
            string str = target as string;
            if (str != null) {
                return StringOps.GetItem(str, index);
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, target, index);
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            PythonGetIndexBinder ob = obj as PythonGetIndexBinder;
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
                typeof(PythonOps).GetMethod("MakeGetIndexAction"),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(CallInfo.ArgumentCount)
            );
        }

        #endregion
    }
}
