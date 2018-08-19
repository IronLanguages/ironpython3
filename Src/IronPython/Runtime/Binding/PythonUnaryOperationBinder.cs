// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    class PythonUnaryOperationBinder : UnaryOperationBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonUnaryOperationBinder(PythonContext/*!*/ context, ExpressionType operation)
            : base(operation) {
            _context = context;
        }

        public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject errorSuggestion) {
            return PythonProtocol.Operation(this, target, errorSuggestion);
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            PythonUnaryOperationBinder ob = obj as PythonUnaryOperationBinder;
            if (ob == null) {
                return false;
            }

            return ob._context.Binder == _context.Binder && base.Equals(obj);
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            switch (Operation) {
                case ExpressionType.Negate:
                    if (CompilerHelpers.GetType(args[0]) == typeof(int)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(IntNegate);
                        }
                    }
                    break;
                case ExpressionType.IsFalse:
                    if (args[0] == null) {
                        if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, bool>(NoneIsFalse);
                        }
                    } else if (args[0].GetType() == typeof(string)) {
                        if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, bool>(StringIsFalse);
                        }
                    } else if (args[0].GetType() == typeof(bool)) {
                        if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, bool>(BoolIsFalse);
                        }
                    } else if (args[0].GetType() == typeof(List)) {
                        if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, bool>(ListIsFalse);
                        }
                    } else if (args[0].GetType() == typeof(PythonTuple)) {
                        if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, bool>(TupleIsFalse);
                        }
                    } else if (args[0].GetType() == typeof(int)) {
                        if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                            return (T)(object)new Func<CallSite, object, bool>(IntIsFalse);
                        }
                    }
                    break;
                case ExpressionType.Not:
                    if (args[0] == null) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(NoneNot);
                        }
                    } else if (args[0].GetType() == typeof(string)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(StringNot);
                        }
                    } else if (args[0].GetType() == typeof(bool)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(BoolNot);
                        }
                    } else if (args[0].GetType() == typeof(List)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(ListNot);
                        }
                    } else if (args[0].GetType() == typeof(PythonTuple)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(TupleNot);
                        }
                    } else if (args[0].GetType() == typeof(int)) {
                        if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object>(IntNot);
                        }
                    }
                    break;
            }

            return base.BindDelegate(site, args);
        }

        private object IntNegate(CallSite site, object value) {
            if (value is int) {
                return Int32Ops.Negate((int)value);
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        private bool StringIsFalse(CallSite site, object value) {
            string strVal = value as string;
            if (strVal != null) {
                return strVal.Length == 0;
            } else if (value == null) {
                // improve perf of sites just polymorphic on str & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        private bool ListIsFalse(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(List)) {
                return ((List)value).Count == 0;
            } else if (value == null) {
                // improve perf of sites just polymorphic on list & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        private bool NoneIsFalse(CallSite site, object value) {
            if (value == null) {
                return true;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        private bool IntIsFalse(CallSite site, object value) {
            if (value is int) {
                return (int)value == 0;
            } else if (value == null) {
                // improve perf of sites just polymorphic on int & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        private bool TupleIsFalse(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(PythonTuple)) {
                return ((PythonTuple)value).Count == 0;
            } else if (value == null) {
                // improve perf of sites just polymorphic on tuple & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        private bool BoolIsFalse(CallSite site, object value) {
            if (value is bool) {
                return !(bool)value;
            } else if (value == null) {
                // improve perf of sites just polymorphic on bool & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        private object StringNot(CallSite site, object value) {
            string strVal = value as string;
            if (strVal != null) {
                return strVal.Length == 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            } else if (value == null) {
                // improve perf of sites just polymorphic on str & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        private object ListNot(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(List)) {
                return ((List)value).Count == 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            } else if (value == null) {
                // improve perf of sites just polymorphic on list & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        private object NoneNot(CallSite site, object value) {
            if (value == null) {
                return ScriptingRuntimeHelpers.True;
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        private object TupleNot(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(PythonTuple)) {
                return ((PythonTuple)value).Count == 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            } else if (value == null) {
                // improve perf of sites just polymorphic on tuple & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        private object BoolNot(CallSite site, object value) {
            if (value is bool) {
                return !(bool)value ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            } else if (value == null) {
                // improve perf of sites just polymorphic on bool & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        private object IntNot(CallSite site, object value) {
            if (value is int) {
                return (int)value == 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            } else if (value == null) {
                // improve perf of sites just polymorphic on int & None
                return true;
            }

            return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
        }

        public PythonContext/*!*/ Context {
            get {
                return _context;
            }
        }

        public override string ToString() {
            return "PythonUnary " + Operation;
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeUnaryOperationAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(Operation)
            );
        }

        #endregion
    }
}
