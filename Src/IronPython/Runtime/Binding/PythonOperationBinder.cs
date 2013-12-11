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
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    class PythonOperationBinder : DynamicMetaObjectBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;
        private readonly PythonOperationKind _operation;

        public PythonOperationBinder(PythonContext/*!*/ context, PythonOperationKind/*!*/ operation) {
            _context = context;
            _operation = operation;
        }

        public override DynamicMetaObject Bind(DynamicMetaObject target, DynamicMetaObject[] args) {
            IPythonOperable op = target as IPythonOperable;
            if (op != null) {
                DynamicMetaObject res = op.BindOperation(this, ArrayUtils.Insert(target, args));
                if (res != null) {
                    return res;
                }
            }

            return PythonProtocol.Operation(this, ArrayUtils.Insert(target, args));
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            switch(_operation) {
                case PythonOperationKind.Hash:
                    if (CompilerHelpers.GetType(args[0]) == typeof(PythonType)) {
                        if (typeof(T) == typeof(Func<CallSite, object, int>)) {
                            return (T)(object)new Func<CallSite, object, int>(HashPythonType);
                        }
                    } else if (args[0] is OldClass) {
                        if (typeof(T) == typeof(Func<CallSite, object, int>)) {
                            return (T)(object)new Func<CallSite, object, int>(HashOldClass);
                        }
                    }
                    break;
                case PythonOperationKind.Compare:
                    if (CompilerHelpers.GetType(args[0]) == typeof(string) &&
                        CompilerHelpers.GetType(args[1]) == typeof(string)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object, int>)) {
                            return (T)(object)new Func<CallSite, object, object, int>(CompareStrings);
                        }
                    }
                    break;
                case PythonOperationKind.GetEnumeratorForIteration:
                    if (CompilerHelpers.GetType(args[0]) == typeof(List)) {
                        if (typeof(T) == typeof(Func<CallSite, List, KeyValuePair<IEnumerator, IDisposable>>)) {
                            return (T)(object)new Func<CallSite, List, KeyValuePair<IEnumerator, IDisposable>>(GetListEnumerator);
                        }
                        return (T)(object)new Func<CallSite, object, KeyValuePair<IEnumerator, IDisposable>>(GetListEnumerator);
                    } else if (CompilerHelpers.GetType(args[0]) == typeof(PythonTuple)) {
                        if (typeof(T) == typeof(Func<CallSite, PythonTuple, KeyValuePair<IEnumerator, IDisposable>>)) {
                            return (T)(object)new Func<CallSite, PythonTuple, KeyValuePair<IEnumerator, IDisposable>>(GetTupleEnumerator);
                        }
                        return (T)(object)new Func<CallSite, object, KeyValuePair<IEnumerator, IDisposable>>(GetTupleEnumerator);

                    }
                    break;
                case PythonOperationKind.Contains:
                    if (CompilerHelpers.GetType(args[1]) == typeof(List)) {
                        Type tType = typeof(T);
                        if (tType == typeof(Func<CallSite, object, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, object, bool>(ListContains<object>);
                        } else if (tType == typeof(Func<CallSite, object, List, bool>)) {
                            return (T)(object)new Func<CallSite, object, List, bool>(ListContains);
                        } else if (tType == typeof(Func<CallSite, int, object, bool>)) {
                            return (T)(object)new Func<CallSite, int, object, bool>(ListContains<int>);
                        } else if (tType == typeof(Func<CallSite, string, object, bool>)) {
                            return (T)(object)new Func<CallSite, string, object, bool>(ListContains<string>);
                        } else if (tType == typeof(Func<CallSite, double, object, bool>)) {
                            return (T)(object)new Func<CallSite, double, object, bool>(ListContains<double>);
                        } else if (tType == typeof(Func<CallSite, PythonTuple, object, bool>)) {
                            return (T)(object)new Func<CallSite, PythonTuple, object, bool>(ListContains<PythonTuple>);
                        }
                    } else if (CompilerHelpers.GetType(args[1]) == typeof(PythonTuple)) {
                        Type tType = typeof(T);
                        if (tType == typeof(Func<CallSite, object, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, object, bool>(TupleContains<object>);
                        } else if (tType == typeof(Func<CallSite, object, PythonTuple, bool>)) {
                            return (T)(object)new Func<CallSite, object, PythonTuple, bool>(TupleContains);
                        } else if (tType == typeof(Func<CallSite, int, object, bool>)) {
                            return (T)(object)new Func<CallSite, int, object, bool>(TupleContains<int>);
                        } else if (tType == typeof(Func<CallSite, string, object, bool>)) {
                            return (T)(object)new Func<CallSite, string, object, bool>(TupleContains<string>);
                        } else if (tType == typeof(Func<CallSite, double, object, bool>)) {
                            return (T)(object)new Func<CallSite, double, object, bool>(TupleContains<double>);
                        } else if (tType == typeof(Func<CallSite, PythonTuple, object, bool>)) {
                            return (T)(object)new Func<CallSite, PythonTuple, object, bool>(TupleContains<PythonTuple>);
                        }
                    } else if (CompilerHelpers.GetType(args[0]) == typeof(string) && CompilerHelpers.GetType(args[1]) == typeof(string)) {
                        Type tType = typeof(T);
                        if (tType == typeof(Func<CallSite, object, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, object, bool>(StringContains);
                        } else if(tType == typeof(Func<CallSite, string, object, bool>)) {
                            return (T)(object)new Func<CallSite, string, object, bool>(StringContains);
                        } else if (tType == typeof(Func<CallSite, object, string, bool>)) {
                            return (T)(object)new Func<CallSite, object, string, bool>(StringContains);
                        } else if (tType == typeof(Func<CallSite, string, string, bool>)) {
                            return (T)(object)new Func<CallSite, string, string, bool>(StringContains);
                        }
                    } else if (CompilerHelpers.GetType(args[1]) == typeof(SetCollection)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, object, bool>(SetContains);
                        }
                    }
                    break;
                
            }
            return base.BindDelegate<T>(site, args);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetListEnumerator(CallSite site, List value) {
            return new KeyValuePair<IEnumerator,IDisposable>(new ListIterator(value), null);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetListEnumerator(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(List)) {
                return new KeyValuePair<IEnumerator,IDisposable>(new ListIterator((List)value), null);
            }

            return ((CallSite<Func<CallSite, object, KeyValuePair<IEnumerator, IDisposable>>>)site).Update(site, value);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetTupleEnumerator(CallSite site, PythonTuple value) {
            return new KeyValuePair<IEnumerator,IDisposable>(new TupleEnumerator(value), null);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetTupleEnumerator(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(PythonTuple)) {
                return new KeyValuePair<IEnumerator, IDisposable>(new TupleEnumerator((PythonTuple)value), null);
            }

            return ((CallSite<Func<CallSite, object, KeyValuePair<IEnumerator, IDisposable>>>)site).Update(site, value);
        }

        private bool ListContains(CallSite site, object other, List value) {
            return value.ContainsWorker(other);
        }

        private bool ListContains<TOther>(CallSite site, TOther other, object value) {
            if (value != null && value.GetType() == typeof(List)) {
                return ((List)value).ContainsWorker(other);
            }

            return ((CallSite<Func<CallSite, TOther, object, bool>>)site).Update(site, other, value);
        }

        private bool TupleContains(CallSite site, object other, PythonTuple value) {
            return value.Contains(other);
        }

        private bool TupleContains<TOther>(CallSite site, TOther other, object value) {
            if (value != null && value.GetType() == typeof(PythonTuple)) {
                return ((PythonTuple)value).Contains(other);
            }

            return ((CallSite<Func<CallSite, TOther, object, bool>>)site).Update(site, other, value);
        }

        private bool StringContains(CallSite site, string other, string value) {
            if (other != null && value != null) {
                return StringOps.__contains__(value, other);
            }

            return ((CallSite<Func<CallSite, string, string, bool>>)site).Update(site, other, value);
        }

        private bool StringContains(CallSite site, object other, string value) {
            if (other is string && value != null) {
                return StringOps.__contains__(value, (string)other);
            }

            return ((CallSite<Func<CallSite, object, string, bool>>)site).Update(site, other, value);
        }

        private bool StringContains(CallSite site, string other, object value) {
            if (value is string && other != null) {
                return StringOps.__contains__((string)value, other);
            }

            return ((CallSite<Func<CallSite, string, object, bool>>)site).Update(site, other, value);
        }

        private bool StringContains(CallSite site, object other, object value) {
            if (value is string && other is string) {
                return StringOps.__contains__((string)value, (string)other);
            }

            return ((CallSite<Func<CallSite, object, object, bool>>)site).Update(site, other, value);
        }

        private bool SetContains(CallSite site, object other, object value) {
            if (value != null && value.GetType() == typeof(SetCollection)) {
                return ((SetCollection)value).__contains__(other);
            }

            return ((CallSite<Func<CallSite, object, object, bool>>)site).Update(site, other, value);
        }

        private int HashPythonType(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(PythonType)) {
                return value.GetHashCode();
            }

            return ((CallSite<Func<CallSite, object, int>>)site).Update(site, value);
        }

        private int HashOldClass(CallSite site, object value) {
            // OldClass is sealed, an is check is good enough.
            if (value is OldClass) {
                return value.GetHashCode();
            }

            return ((CallSite<Func<CallSite, object, int>>)site).Update(site, value);
        }

        private int CompareStrings(CallSite site, object arg0, object arg1) {
            if (arg0 != null && arg0.GetType() == typeof(string) &&
                arg1 != null && arg1.GetType() == typeof(string)) {
                return StringOps.Compare((string)arg0, (string)arg1);
            }

            return ((CallSite<Func<CallSite, object, object, int>>)site).Update(site, arg0, arg1);
        }

        public PythonOperationKind Operation {
            get {
                return _operation;
            }
        }

        /// <summary>
        /// The result type of the operation.
        /// </summary>
        public override Type ReturnType {
            get {
                switch (Operation & (~PythonOperationKind.DisableCoerce)) {
                    case PythonOperationKind.Compare: return typeof(int);
                    case PythonOperationKind.IsCallable: return typeof(bool);
                    case PythonOperationKind.Hash: return typeof(int);
                    case PythonOperationKind.Contains: return typeof(bool);
                    case PythonOperationKind.GetEnumeratorForIteration: return typeof(KeyValuePair<IEnumerator, IDisposable>);
                    case PythonOperationKind.CallSignatures: return typeof(IList<string>);
                    case PythonOperationKind.Documentation: return typeof(string);
                }
                return typeof(object); 
            }
        }


        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode() ^ _operation.GetHashCode();
        }

        public override bool Equals(object obj) {
            PythonOperationBinder ob = obj as PythonOperationBinder;
            if (ob == null) {
                return false;
            }

            return ob._context.Binder == _context.Binder && base.Equals(obj);
        }

        public PythonContext/*!*/ Context {
            get {
                return _context;
            }
        }

        public override string ToString() {
            return "Python " + Operation;
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod("MakeOperationAction"),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant((int)Operation)
            );
        }

        #endregion
    }
}
