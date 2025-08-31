// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

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

    internal class PythonOperationBinder : DynamicMetaObjectBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;
        private readonly PythonOperationKind _operation;

        public PythonOperationBinder(PythonContext/*!*/ context, PythonOperationKind/*!*/ operation) {
            _context = context;
            _operation = operation;
        }

        public override DynamicMetaObject Bind(DynamicMetaObject target, DynamicMetaObject[] args) {
            if (target is IPythonOperable op) {
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
                    }
                    break;
                case PythonOperationKind.GetEnumeratorForIteration:
                    if (CompilerHelpers.GetType(args[0]) == typeof(PythonList)) {
                        if (typeof(T) == typeof(Func<CallSite, PythonList, KeyValuePair<IEnumerator, IDisposable>>)) {
                            return (T)(object)new Func<CallSite, PythonList, KeyValuePair<IEnumerator, IDisposable>>(GetListEnumerator);
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
                    if (CompilerHelpers.GetType(args[1]) == typeof(PythonList)) {
                        Type tType = typeof(T);
                        if (tType == typeof(Func<CallSite, object, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, object, bool>(ListContains<object>);
                        } else if (tType == typeof(Func<CallSite, object, PythonList, bool>)) {
                            return (T)(object)new Func<CallSite, object, PythonList, bool>(ListContains);
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

        private KeyValuePair<IEnumerator, IDisposable> GetListEnumerator(CallSite site, PythonList value) {
            return new KeyValuePair<IEnumerator,IDisposable>(new PythonListIterator(value), null);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetListEnumerator(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(PythonList)) {
                return new KeyValuePair<IEnumerator,IDisposable>(new PythonListIterator((PythonList)value), null);
            }

            return ((CallSite<Func<CallSite, object, KeyValuePair<IEnumerator, IDisposable>>>)site).Update(site, value);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetTupleEnumerator(CallSite site, PythonTuple value) {
            return new KeyValuePair<IEnumerator,IDisposable>(new PythonTupleEnumerator(value), null);
        }

        private KeyValuePair<IEnumerator, IDisposable> GetTupleEnumerator(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(PythonTuple)) {
                return new KeyValuePair<IEnumerator, IDisposable>(new PythonTupleEnumerator((PythonTuple)value), null);
            }

            return ((CallSite<Func<CallSite, object, KeyValuePair<IEnumerator, IDisposable>>>)site).Update(site, value);
        }

        private bool ListContains(CallSite site, object other, PythonList value) {
            return value.ContainsWorker(other);
        }

        private bool ListContains<TOther>(CallSite site, TOther other, object value) {
            if (value != null && value.GetType() == typeof(PythonList)) {
                return ((PythonList)value).ContainsWorker(other);
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
                switch (Operation) {
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
            if (!(obj is PythonOperationBinder ob)) {
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
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeOperationAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant((int)Operation)
            );
        }

        #endregion
    }
}
