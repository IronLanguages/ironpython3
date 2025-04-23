// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using Microsoft.Scripting.Ast;

using System;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;
    using Microsoft.Scripting.Actions;

    internal class PythonBinaryOperationBinder : BinaryOperationBinder, IPythonSite, IExpressionSerializable, ILightExceptionBinder {
        private readonly PythonContext/*!*/ _context;
        private PythonBinaryOperationBinder _lightThrowBinder;

        public PythonBinaryOperationBinder(PythonContext/*!*/ context, ExpressionType operation)
            : base(operation) {
            _context = context;
        }
       
        public override DynamicMetaObject FallbackBinaryOperation(DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject errorSuggestion) {
            return PythonProtocol.Operation(this, target, arg, errorSuggestion);
        }

        //private static Func<CallSite, object, object, object> DoubleAddSite = new Func<CallSite, object, object, object>(DoubleAdd);

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            if (args[0] != null &&
                CompilerHelpers.GetType(args[0]) == CompilerHelpers.GetType(args[1])) {
                switch (Operation) {
                    case ExpressionType.Add:
                    case ExpressionType.AddAssign:
                        return BindAdd<T>(site, args);
                    case ExpressionType.And:
                    case ExpressionType.AndAssign:
                        return BindAnd<T>(site, args);
                    case ExpressionType.Or:
                    case ExpressionType.OrAssign:
                        return BindOr<T>(site, args);
                    case ExpressionType.Subtract:
                    case ExpressionType.SubtractAssign:
                        return BindSubtract<T>(site, args);
                    case ExpressionType.Equal:
                        return BindEqual<T>(site, args);
                    case ExpressionType.NotEqual:
                        return BindNotEqual<T>(site, args);
                    case ExpressionType.GreaterThan:
                        return BindGreaterThan<T>(site, args);
                    case ExpressionType.LessThan:
                        return BindLessThan<T>(site, args);
                    case ExpressionType.LessThanOrEqual:
                        return BindLessThanOrEqual<T>(site, args);
                    case ExpressionType.GreaterThanOrEqual:
                        return BindGreaterThanOrEqual<T>(site, args);
                    case ExpressionType.Multiply:
                    case ExpressionType.MultiplyAssign:
                        return BindMultiply<T>(site, args);
                    case ExpressionType.Divide:
                    case ExpressionType.DivideAssign:
                        return BindDivide<T>(site, args);
                    case ExpressionType.Modulo:
                        return BindModulo<T>(site, args);
                }
            } else {
                switch(Operation) {
                    case ExpressionType.Modulo:
                        return BindModulo<T>(site, args);
                    case ExpressionType.Multiply:
                        return BindMultiplyDifferentTypes<T>(site, args);
                }
            }

            return base.BindDelegate<T>(site, args);
        }

        private T BindModulo<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(string) && !(args[1] is Extensible<string>)) {
                if (typeof(T) == typeof(Func<CallSite, string, PythonDictionary, object>)) {
                    return (T)(object)new Func<CallSite, string, PythonDictionary, object>(StringModulo);
                } else if (typeof(T) == typeof(Func<CallSite, string, PythonTuple, object>)) {
                    return (T)(object)new Func<CallSite, string, PythonTuple, object>(StringModulo);
                } else if (typeof(T) == typeof(Func<CallSite, string, object, object>)) {
                    return (T)(object)new Func<CallSite, string, object, object>(StringModulo);
                } else if (typeof(T) == typeof(Func<CallSite, object, PythonDictionary, object>)) {
                    return (T)(object)new Func<CallSite, object, PythonDictionary, object>(StringModulo);
                } else if (typeof(T) == typeof(Func<CallSite, object, PythonTuple, object>)) {
                    return (T)(object)new Func<CallSite, object, PythonTuple, object>(StringModulo);
                } else if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(StringModulo);
                }

            }
            return base.BindDelegate(site, args);
        }

        private T BindMultiply<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntMultiply);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntMultiply);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntMultiply);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindMultiplyDifferentTypes<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(PythonList) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(ListIntMultiply);
                }
            } else if (CompilerHelpers.GetType(args[0]) == typeof(string) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(StringIntMultiply);
                }
            } else if (CompilerHelpers.GetType(args[0]) == typeof(PythonTuple) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(TupleIntMultiply);
                }
            }


            return base.BindDelegate(site, args);
        }


        private T BindDivide<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntDivide);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntDivide);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntDivide);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindLessThanOrEqual<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntLessThanOrEqual);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntLessThanOrEqual);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntLessThanOrEqual);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindGreaterThanOrEqual<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntGreaterThanOrEqual);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntGreaterThanOrEqual);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntGreaterThanOrEqual);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindGreaterThan<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntGreaterThan);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntGreaterThan);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntGreaterThan);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindLessThan<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntLessThan);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntLessThan);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntLessThan);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindAnd<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntAnd);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntAnd);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntAnd);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindOr<T>(CallSite<T> site, object[] args) where T : class {
            if (CompilerHelpers.GetType(args[0]) == typeof(int) &&
                CompilerHelpers.GetType(args[1]) == typeof(int)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(IntOr);
                } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                    return (T)(object)new Func<CallSite, int, object, object>(IntOr);
                } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)new Func<CallSite, object, int, object>(IntOr);
                }
            }

            return base.BindDelegate(site, args);
        }

        private T BindAdd<T>(CallSite<T> site, object[] args) where T : class {
            Type t = args[0].GetType();
            if (t == typeof(string)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(StringAdd);
                } else if (typeof(T) == typeof(Func<CallSite, object, string, object>)) {
                    return (T)(object)new Func<CallSite, object, string, object>(StringAdd);
                } else if (typeof(T) == typeof(Func<CallSite, string, object, object>)) {
                    return (T)(object)new Func<CallSite, string, object, object>(StringAdd);
                }
            } else if (t == typeof(PythonList)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    if (Operation == ExpressionType.Add) {
                        return (T)(object)new Func<CallSite, object, object, object>(ListAdd);
                    } else {
                        return (T)(object)new Func<CallSite, object, object, object>(ListAddAssign);
                    }
                }
            } else if (t == typeof(PythonTuple)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(TupleAdd);
                }

            } else if (!t.IsEnum) {
                switch (t.GetTypeCode()) {
                    case TypeCode.Double:
                        if(typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object, object>(DoubleAdd);
                        }
                        break;
                    case TypeCode.Int32:
                        if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object, object>(IntAdd);
                        } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                            return (T)(object)new Func<CallSite, object, int, object>(IntAdd);
                        } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                            return (T)(object)new Func<CallSite, int, object, object>(IntAdd);
                        }
                        break;
                }
            }
            return base.BindDelegate(site, args);
        }

        private T BindSubtract<T>(CallSite<T> site, object[] args) where T : class {
            Type t = args[0].GetType();
            if (!t.IsEnum) {
                switch (t.GetTypeCode()) {
                    case TypeCode.Double:
                        if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object, object>(DoubleSubtract);
                        }
                        break;
                    case TypeCode.Int32:
                        if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                            return (T)(object)new Func<CallSite, object, object, object>(IntSubtract);
                        } else if (typeof(T) == typeof(Func<CallSite, object, int, object>)) {
                            return (T)(object)new Func<CallSite, object, int, object>(IntSubtract);
                        } else if (typeof(T) == typeof(Func<CallSite, int, object, object>)) {
                            return (T)(object)new Func<CallSite, int, object, object>(IntSubtract);
                        }
                        break;
                }
            }
            return base.BindDelegate(site, args);
        }

        private T BindEqual<T>(CallSite<T> site, object[] args) where T : class {
            Type t = args[0].GetType();
            if (t == typeof(string)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(StringEqual);
                } else if (typeof(T) == typeof(Func<CallSite, string, object, object>)) {
                    return (T)(object)new Func<CallSite, string, object, object>(StringEqual);
                } else if (typeof(T) == typeof(Func<CallSite, object, string, object>)) {
                    return (T)(object)new Func<CallSite, object, string, object>(StringEqual);
                }
            } else if (!t.IsEnum && typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                switch (t.GetTypeCode()) {
                    case TypeCode.Double:
                        return (T)(object)new Func<CallSite, object, object, object>(DoubleEqual);
                    case TypeCode.Int32:
                        return (T)(object)new Func<CallSite, object, object, object>(IntEqual);
                }
            }
            return base.BindDelegate(site, args);
        }

        private T BindNotEqual<T>(CallSite<T> site, object[] args) where T : class {
            Type t = args[0].GetType();
            if (t == typeof(string)) {
                if (typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)new Func<CallSite, object, object, object>(StringNotEqual);
                } else if (typeof(T) == typeof(Func<CallSite, object, string, object>)) {
                    return (T)(object)new Func<CallSite, object, string, object>(StringNotEqual);
                } else if (typeof(T) == typeof(Func<CallSite, string, object, object>)) {
                    return (T)(object)new Func<CallSite, string, object, object>(StringNotEqual);
                }
            } else if (!t.IsEnum && typeof(T) == typeof(Func<CallSite, object, object, object>)) {
                switch (t.GetTypeCode()) {
                    case TypeCode.Double:
                        return (T)(object)new Func<CallSite, object, object, object>(DoubleNotEqual);
                    case TypeCode.Int32:
                        return (T)(object)new Func<CallSite, object, object, object>(IntNotEqual);
                }
            }
            return base.BindDelegate(site, args);
        }

        private object StringModulo(CallSite site, string self, PythonDictionary other) {
            return StringOps.Mod(Context.SharedContext, self, other);
        }

        private object StringModulo(CallSite site, string self, PythonTuple other) {
            return StringOps.Mod(Context.SharedContext, self, other);
        }

        private object StringModulo(CallSite site, string self, object other) {
            return StringOps.Mod(Context.SharedContext, self, other);
        }

        private object StringModulo(CallSite site, object self, PythonDictionary other) {
            if (self != null && self.GetType() == typeof(string)) {
                return StringOps.Mod(Context.SharedContext, (string)self, other);
            }

            return ((CallSite<Func<CallSite, object, PythonDictionary, object>>)site).Update(site, self, other);
        }

        private object StringModulo(CallSite site, object self, PythonTuple other) {
            if (self != null && self.GetType() == typeof(string)) {
                return StringOps.Mod(Context.SharedContext, (string)self, other);
            }

            return ((CallSite<Func<CallSite, object, PythonTuple, object>>)site).Update(site, self, other);
        }

        private object StringModulo(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(string)) {
                return StringOps.Mod(Context.SharedContext, (string)self, other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object DoubleAdd(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(double) && 
                other != null && other.GetType() == typeof(double)) {
                return (double)self + (double)other;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntAdd(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) && 
                other != null && other.GetType() == typeof(int)) {
                return Int32Ops.Add((int)self, (int)other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntAdd(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return Int32Ops.Add((int)self, other);
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntAdd(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return Int32Ops.Add(self, (int)other);
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object ListIntMultiply(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(PythonList) &&
                other != null && other.GetType() == typeof(int)) {
                return ((PythonList)self) * (int)other;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringIntMultiply(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(string) &&
                other != null && other.GetType() == typeof(int)) {
                return StringOps.Multiply((string)self, (int)other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object TupleIntMultiply(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(PythonTuple) &&
                other != null && other.GetType() == typeof(int)) {
                return ((PythonTuple)self) * (int)other;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntMultiply(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return Int32Ops.Multiply((int)self, (int)other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntMultiply(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return Int32Ops.Multiply((int)self, other);
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntMultiply(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return Int32Ops.Multiply(self, (int)other);
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object IntDivide(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return Int32Ops.TrueDivide((int)self, (int)other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntDivide(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return Int32Ops.TrueDivide((int)self, other);
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntDivide(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return Int32Ops.TrueDivide(self, (int)other);
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object IntAnd(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return ScriptingRuntimeHelpers.Int32ToObject(Int32Ops.BitwiseAnd((int)self, (int)other));
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntAnd(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return ScriptingRuntimeHelpers.Int32ToObject(Int32Ops.BitwiseAnd((int)self, other));
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntAnd(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return ScriptingRuntimeHelpers.Int32ToObject(Int32Ops.BitwiseAnd(self, (int)other));
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object IntOr(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return ScriptingRuntimeHelpers.Int32ToObject(Int32Ops.BitwiseOr((int)self, (int)other));
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntOr(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return ScriptingRuntimeHelpers.Int32ToObject(Int32Ops.BitwiseOr((int)self, other));
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntOr(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return ScriptingRuntimeHelpers.Int32ToObject(Int32Ops.BitwiseOr(self, (int)other));
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object ListAdd(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(PythonList) &&
                other != null && other.GetType() == typeof(PythonList)) {
                return (PythonList)self + (PythonList)other;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object ListAddAssign(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(PythonList) &&
                other != null && other.GetType() == typeof(PythonList)) {
                return ((PythonList)self).InPlaceAdd(DefaultContext.Default, other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object TupleAdd(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(PythonTuple) &&
                other != null && other.GetType() == typeof(PythonTuple)) {
                return (PythonTuple)self + (PythonTuple)other;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringAdd(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(string) && 
                other != null && other.GetType() == typeof(string)) {
                return StringOps.Add((string)self, (string)other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringAdd(CallSite site, string self, object other) {
            if (self != null && 
                other != null && other.GetType() == typeof(string)) {
                return StringOps.Add(self, (string)other);
            }

            return ((CallSite<Func<CallSite, string, object, object>>)site).Update(site, self, other);
        }

        private object StringAdd(CallSite site, object self, string other) {
            if (self != null && self.GetType() == typeof(string) &&
                other != null) {
                return StringOps.Add((string)self, other);
            }

            return ((CallSite<Func<CallSite, object, string, object>>)site).Update(site, self, other);
        }
        
        private object DoubleSubtract(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(double) &&
                other != null && other.GetType() == typeof(double)) {
                return (double)self - (double)other;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntSubtract(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return Int32Ops.Subtract((int)self, (int)other);
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntSubtract(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return Int32Ops.Subtract((int)self, other);
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntSubtract(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return Int32Ops.Subtract(self, (int)other);
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object DoubleEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(double) &&
                other != null && other.GetType() == typeof(double)) {
                return DoubleOps.Equals((double)self, (double)other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return (int)self == (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(string) &&
                other != null && other.GetType() == typeof(string)) {
                return StringOps.Equals((string)self, (string)other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringEqual(CallSite site, string self, object other) {
            if (self != null &&
                other != null && other.GetType() == typeof(string)) {
                return StringOps.Equals(self, (string)other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, string, object, object>>)site).Update(site, self, other);
        }

        private object StringEqual(CallSite site, object self, string other) {
            if (self != null && self.GetType() == typeof(string) &&
                other != null) {
                return StringOps.Equals((string)self, other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, string, object>>)site).Update(site, self, other);
        }
        
        private object DoubleNotEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(double) &&
                other != null && other.GetType() == typeof(double)) {
                return DoubleOps.NotEquals((double)self, (double)other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntNotEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return (int)self != (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringNotEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(string) &&
                other != null && other.GetType() == typeof(string)) {
                return StringOps.NotEquals((string)self, (string)other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object StringNotEqual(CallSite site, string self, object other) {
            if (self != null &&
                other != null && other.GetType() == typeof(string)) {
                return StringOps.NotEquals(self, (string)other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, string, object, object>>)site).Update(site, self, other);
        }

        private object StringNotEqual(CallSite site, object self, string other) {
            if (self != null && self.GetType() == typeof(string) &&
                other != null) {
                return StringOps.NotEquals((string)self, other) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, string, object>>)site).Update(site, self, other);
        }

        private object IntGreaterThan(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return (int)self > (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntGreaterThan(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return (int)self > other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntGreaterThan(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return self > (int)other;
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object IntLessThan(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return (int)self < (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntLessThan(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return (int)self < other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntLessThan(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return self < (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object IntGreaterThanOrEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return (int)self >= (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntGreaterThanOrEqual(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return (int)self >= other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntGreaterThanOrEqual(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return self >= (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        private object IntLessThanOrEqual(CallSite site, object self, object other) {
            if (self != null && self.GetType() == typeof(int) &&
                other != null && other.GetType() == typeof(int)) {
                return (int)self <= (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, object, object>>)site).Update(site, self, other);
        }

        private object IntLessThanOrEqual(CallSite site, object self, int other) {
            if (self != null && self.GetType() == typeof(int)) {
                return (int)self <= other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, object, int, object>>)site).Update(site, self, other);
        }

        private object IntLessThanOrEqual(CallSite site, int self, object other) {
            if (other != null && other.GetType() == typeof(int)) {
                return self <= (int)other ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
            }

            return ((CallSite<Func<CallSite, int, object, object>>)site).Update(site, self, other);
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (!(obj is PythonBinaryOperationBinder ob)) {
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
            return "PythonBinary " + Operation;
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeBinaryOperationAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(Operation)
            );
        }

        #endregion

        #region ILightExceptionBinder Members

        public virtual bool SupportsLightThrow {
            get { return false; }
        }

        public virtual CallSiteBinder GetLightExceptionBinder() {
            if (_lightThrowBinder == null) {
                _lightThrowBinder = new LightThrowBinder(_context, Operation);
            }
            return _lightThrowBinder;
        }

        private class LightThrowBinder : PythonBinaryOperationBinder {
            public LightThrowBinder(PythonContext/*!*/ context, ExpressionType operation)
                : base(context, operation) {
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
