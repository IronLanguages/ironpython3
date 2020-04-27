// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Numerics;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal class PythonConversionBinder : DynamicMetaObjectBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;
        private readonly ConversionResultKind/*!*/ _kind;
        private readonly Type _type;
        private readonly bool _retObject;
        private CompatConversionBinder _compatConvert;

        public PythonConversionBinder(PythonContext/*!*/ context, Type/*!*/ type, ConversionResultKind resultKind) {
            Assert.NotNull(context, type);

            _context = context;
            _kind = resultKind;
            _type = type;
        }

        public PythonConversionBinder(PythonContext/*!*/ context, Type/*!*/ type, ConversionResultKind resultKind, bool retObject) {
            Assert.NotNull(context, type);

            _context = context;
            _kind = resultKind;
            _type = type;
            _retObject = retObject;
        }

        public Type Type {
            get {
                return _type;
            }
        }

        public ConversionResultKind ResultKind {
            get {
                return _kind;
            }
        }

        public override DynamicMetaObject Bind(DynamicMetaObject target, DynamicMetaObject[] args) {
            DynamicMetaObject self = target;

            DynamicMetaObject res = null;
            if (self.NeedsDeferral()) {
                return MyDefer(self);
            }


            if (target is IPythonConvertible convertible) {
                res = convertible.BindConvert(this);
            } else if (res == null) {
                res = BindConvert(self);
            }

            if (_retObject) {
                res = new DynamicMetaObject(
                    AstUtils.Convert(res.Expression, typeof(object)),
                    res.Restrictions
                );
            }

            return res;
        }

        public override Type ReturnType {
            get {
                if (_retObject) {
                    return typeof(object);
                }

                return (_kind == ConversionResultKind.ExplicitCast || _kind == ConversionResultKind.ImplicitCast) ?
                    Type :
                    _type.IsValueType ?
                        typeof(object) :
                        _type;
            }
        }

        private DynamicMetaObject MyDefer(DynamicMetaObject self) {
            return new DynamicMetaObject(
                DynamicExpression.Dynamic(
                    this,
                    ReturnType,
                    self.Expression
                ),
                self.Restrictions
            );
        }

        private DynamicMetaObject BindConvert(DynamicMetaObject self) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Convert " + Type.FullName + " " + self.LimitType);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Conversion");

            DynamicMetaObject res;
#if FEATURE_COM
            DynamicMetaObject comConvert;
            if (Microsoft.Scripting.ComInterop.ComBinder.TryConvert(CompatBinder, self, out comConvert)) {
                res = comConvert;
            } else
#endif
            {
                res = self.BindConvert(CompatBinder);
            }

            // if we return object and the interop binder had to put on an extra conversion
            // to the strong type go ahead and remove it now.
            if (ReturnType == typeof(object) &&
                res.Expression.Type != typeof(object) &&
                res.Expression.NodeType == ExpressionType.Convert) {
                res = new DynamicMetaObject(
                    ((UnaryExpression)res.Expression).Operand,
                    res.Restrictions
                );
            }

            return res;
        }

        internal CompatConversionBinder CompatBinder {
            get {
                if (_compatConvert == null) {
                    _compatConvert = new CompatConversionBinder(this, Type, _kind == ConversionResultKind.ExplicitCast || _kind == ConversionResultKind.ExplicitTry);
                }
                return _compatConvert;
            }
        }

        internal DynamicMetaObject FallbackConvert(Type returnType, DynamicMetaObject self, DynamicMetaObject errorSuggestion) {
            Type type = Type;
            DynamicMetaObject res = null;
            switch (type.GetTypeCode()) {
                case TypeCode.Boolean:
                    res = MakeToBoolConversion(self);
                    break;
                case TypeCode.Char:
                    res = TryToCharConversion(self);
                    break;
                case TypeCode.String:
                    break;
                case TypeCode.Object:
                    // !!! Deferral?
                    if (type.IsArray && self.Value is PythonTuple && type.GetArrayRank() == 1) {
                        res = MakeToArrayConversion(self, type);
                    } else if (type.IsGenericType && !type.IsAssignableFrom(CompilerHelpers.GetType(self.Value))) {
                        Type genTo = type.GetGenericTypeDefinition();

                        // Interface conversion helpers...
                        if (type == typeof(ReadOnlyMemory<byte>) && self.Value is IBufferProtocol) {
                            res = ConvertFromBufferProtocolToMemory(self.Restrict(self.GetLimitType()), typeof(ReadOnlyMemory<byte>));
                        } else if (type == typeof(IReadOnlyList<byte>) && self.Value is IBufferProtocol) {
                            res = ConvertFromBufferProtocolToByteList(self.Restrict(self.GetLimitType()), typeof(IReadOnlyList<byte>));
                        } else if (type == typeof(IList<byte>) && self.Value is IBufferProtocol) {
                            res = ConvertFromBufferProtocolToByteList(self.Restrict(self.GetLimitType()), typeof(IList<byte>));
                        } else if (genTo == typeof(IList<>)) {
                            res = TryToGenericInterfaceConversion(self, type, typeof(IList<object>), typeof(ListGenericWrapper<>));
                        } else if (genTo == typeof(IDictionary<,>)) {
                            res = TryToGenericInterfaceConversion(self, type, typeof(IDictionary<object, object>), typeof(DictionaryGenericWrapper<,>));
                        } else if (genTo == typeof(IEnumerable<>)) {
                            res = TryToGenericInterfaceConversion(self, type, typeof(IEnumerable), typeof(IEnumerableOfTWrapper<>));
                        }
                    } else if (type == typeof(IEnumerable)) {
                        if (!typeof(IEnumerable).IsAssignableFrom(self.GetLimitType())) {
                            res = ConvertToIEnumerable(this, self.Restrict(self.GetLimitType()));
                        }
                    } else if (type == typeof(IEnumerator)) {
                        if (!typeof(IEnumerator).IsAssignableFrom(self.GetLimitType()) &&
                            !typeof(IEnumerable).IsAssignableFrom(self.GetLimitType())) {
                            res = ConvertToIEnumerator(this, self.Restrict(self.GetLimitType()));
                        }
                    }
                    break;
            }

            if (type.IsEnum && Enum.GetUnderlyingType(type) == self.GetLimitType()) {
                // numeric type to enum, this is ok if the value is zero
                object value = Activator.CreateInstance(type);

                return new DynamicMetaObject(
                    Ast.Condition(
                        Ast.Equal(
                            AstUtils.Convert(self.Expression, Enum.GetUnderlyingType(type)),
                            AstUtils.Constant(Activator.CreateInstance(self.GetLimitType()))
                        ),
                        AstUtils.Constant(value),
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.TypeErrorForBadEnumConversion)).MakeGenericMethod(type),
                            AstUtils.Convert(self.Expression, typeof(object))
                        )
                    ),
                    self.Restrictions.Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(self.Expression, self.GetLimitType())),
                    value
                );
            }

            return res ?? EnsureReturnType(returnType, Context.Binder.ConvertTo(Type, ResultKind, self, _context.SharedOverloadResolverFactory, errorSuggestion));
        }

        private static DynamicMetaObject EnsureReturnType(Type returnType, DynamicMetaObject dynamicMetaObject) {
            if (dynamicMetaObject.Expression.Type != returnType) {
                dynamicMetaObject = new DynamicMetaObject(
                    AstUtils.Convert(
                        dynamicMetaObject.Expression,
                        returnType
                    ),
                    dynamicMetaObject.Restrictions
                );
            }

            return dynamicMetaObject;
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            //Debug.Assert(typeof(T).GetMethod("Invoke").ReturnType == Type);

            object target = args[0];
            T res = null;
            if (typeof(T) == typeof(Func<CallSite, object, string>) && target is string) {
                res = (T)(object)new Func<CallSite, object, string>(StringConversion);
            } else if (typeof(T) == typeof(Func<CallSite, object, int>)) {
                if (target is int) {
                    res = (T)(object)new Func<CallSite, object, int>(IntConversion);
                } else if (target is bool) {
                    res = (T)(object)new Func<CallSite, object, int>(BoolToIntConversion);
                }
            } else if (typeof(T) == typeof(Func<CallSite, bool, int>)) {
                res = (T)(object)new Func<CallSite, bool, int>(BoolToIntConversion);
            } else if (typeof(T) == typeof(Func<CallSite, object, bool>)) {
                if (target is bool) {
                    res = (T)(object)new Func<CallSite, object, bool>(BoolConversion);
                } else if (target is string) {
                    res = (T)(object)new Func<CallSite, object, bool>(StringToBoolConversion);
                } else if (target is int) {
                    res = (T)(object)new Func<CallSite, object, bool>(IntToBoolConversion);
                } else if (target == null) {
                    res = (T)(object)new Func<CallSite, object, bool>(NullToBoolConversion);
                } else if (target.GetType() == typeof(object)) {
                    res = (T)(object)new Func<CallSite, object, bool>(ObjectToBoolConversion);
                } else if (target.GetType() == typeof(PythonList)) {
                    res = (T)(object)new Func<CallSite, object, bool>(ListToBoolConversion);
                } else if (target.GetType() == typeof(PythonTuple)) {
                    res = (T)(object)new Func<CallSite, object, bool>(TupleToBoolConversion);
                }
            } else if (target != null) {
                // Special cases:
                //  - string or bytes to IEnumerable or IEnumerator
                //  - CLR 4 only: BigInteger -> Complex
                if (target is BigInteger) {
                    if (typeof(T) == typeof(Func<CallSite, BigInteger, Complex>)) {
                        res = (T)(object)new Func<CallSite, BigInteger, Complex>(BigIntegerToComplexConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, object, Complex>)) {
                        res = (T)(object)new Func<CallSite, object, Complex>(BigIntegerObjectToComplexConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, BigInteger, object>)) {
                        res = (T)(object)new Func<CallSite, BigInteger, object>(BigIntegerToComplexObjectConversion);
                    }
                } else if (target is string) {
                    if (typeof(T) == typeof(Func<CallSite, string, IEnumerable>)) {
                        res = (T)(object)new Func<CallSite, string, IEnumerable>(StringToIEnumerableConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, string, IEnumerator>)) {
                        res = (T)(object)new Func<CallSite, string, IEnumerator>(StringToIEnumeratorConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, object, IEnumerable>)) {
                        res = (T)(object)new Func<CallSite, object, IEnumerable>(ObjectToIEnumerableConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, object, IEnumerator>)) {
                        res = (T)(object)new Func<CallSite, object, IEnumerator>(ObjectToIEnumeratorConversion);
                    }
                } else if (target.GetType() == typeof(Bytes)) {
                    if (typeof(T) == typeof(Func<CallSite, Bytes, IEnumerable>)) {
                        res = (T)(object)new Func<CallSite, Bytes, IEnumerable>(BytesToIEnumerableConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, Bytes, IEnumerator>)) {
                        res = (T)(object)new Func<CallSite, Bytes, IEnumerator>(BytesToIEnumeratorConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, object, IEnumerable>)) {
                        res = (T)(object)new Func<CallSite, object, IEnumerable>(ObjectToIEnumerableConversion);
                    } else if (typeof(T) == typeof(Func<CallSite, object, IEnumerator>)) {
                        res = (T)(object)new Func<CallSite, object, IEnumerator>(ObjectToIEnumeratorConversion);
                    }
                }

                if (res == null && (target.GetType() == Type || Type.IsAssignableFrom(target.GetType()))) {
                    if (typeof(T) == typeof(Func<CallSite, object, object>)) {
                        // called via a helper call site in the runtime (e.g. Converter.Convert)
                        res = (T)(object)new Func<CallSite, object, object>(new IdentityConversion(target.GetType()).Convert);
                    } else {
                        // called via an embedded call site
                        Debug.Assert(typeof(T).GetMethod("Invoke").ReturnType == Type);
                        if (typeof(T).GetMethod("Invoke").GetParameters()[1].ParameterType == typeof(object)) {
                            object identityConversion = Activator.CreateInstance(typeof(IdentityConversion<>).MakeGenericType(Type), target.GetType());
                            res = (T)(object)identityConversion.GetType().GetMethod("Convert").CreateDelegate(typeof(T), identityConversion);
                        }
                    }
                }
            }

            if (res != null) {
                CacheTarget(res);
                return res;
            }

            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Convert " + Type.FullName + " " + CompilerHelpers.GetType(args[0]) + " " + typeof(T));
            return base.BindDelegate(site, args);
        }

        public string StringConversion(CallSite site, object value) {
            if (value is string str) {
                return str;
            }

            return ((CallSite<Func<CallSite, object, string>>)site).Update(site, value);
        }

        public int IntConversion(CallSite site, object value) {
            if (value is int) {
                return (int)value;
            }

            return ((CallSite<Func<CallSite, object, int>>)site).Update(site, value);
        }

        public int BoolToIntConversion(CallSite site, object value) {
            if (value is bool) {
                return (bool)value ? 1 : 0;
            }

            return ((CallSite<Func<CallSite, object, int>>)site).Update(site, value);
        }

        public int BoolToIntConversion(CallSite site, bool value) {
            return (bool)value ? 1 : 0;
        }

        public bool BoolConversion(CallSite site, object value) {
            if (value is bool) {
                return (bool)value;
            } else if (value == null) {
                // improve perf of sites just polymorphic on bool & None
                return false;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public bool IntToBoolConversion(CallSite site, object value) {
            if (value is int) {
                return (int)value != 0;
            } else if (value == null) {
                // improve perf of sites just polymorphic on int & None
                return false;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public bool StringToBoolConversion(CallSite site, object value) {
            if (value is string) {
                return ((string)value).Length > 0;
            } else if (value == null) {
                // improve perf of sites just polymorphic on str & None
                return false;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public bool NullToBoolConversion(CallSite site, object value) {
            if (value == null) {
                return false;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public bool ObjectToBoolConversion(CallSite site, object value) {
            if (value != null && value.GetType() == typeof(Object)) {
                return true;
            } else if (value == null) {
                // improve perf of sites just polymorphic on object & None
                return false;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public bool ListToBoolConversion(CallSite site, object value) {
            if (value == null) {
                return false;
            } else if (value.GetType() == typeof(PythonList)) {
                return ((PythonList)value).Count != 0;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public bool TupleToBoolConversion(CallSite site, object value) {
            if (value == null) {
                return false;
            } else if (value.GetType() == typeof(PythonTuple)) {
                return ((PythonTuple)value).Count != 0;
            }

            return ((CallSite<Func<CallSite, object, bool>>)site).Update(site, value);
        }

        public IEnumerable StringToIEnumerableConversion(CallSite site, string value) {
            if (value == null) {
                return ((CallSite<Func<CallSite, string, IEnumerable>>)site).Update(site, value);
            }

            return PythonOps.StringEnumerable(value);
        }

        public IEnumerator StringToIEnumeratorConversion(CallSite site, string value) {
            if (value == null) {
                return ((CallSite<Func<CallSite, string, IEnumerator>>)site).Update(site, value);
            }

            return PythonOps.StringEnumerator(value).Key;
        }

        public IEnumerable BytesToIEnumerableConversion(CallSite site, Bytes value) {
            if (value == null) {
                return ((CallSite<Func<CallSite, Bytes, IEnumerable>>)site).Update(site, value);
            }

            return PythonOps.BytesEnumerable(value);
        }

        public IEnumerator BytesToIEnumeratorConversion(CallSite site, Bytes value) {
            if (value == null) {
                return ((CallSite<Func<CallSite, Bytes, IEnumerator>>)site).Update(site, value);
            }

            return PythonOps.BytesEnumerator(value).Key;
        }

        public IEnumerable ObjectToIEnumerableConversion(CallSite site, object value) {
            if (value != null) {
                if (value is string) {
                    return PythonOps.StringEnumerable((string)value);
                } else if (value.GetType() == typeof(Bytes)) {
                    return PythonOps.BytesEnumerable((Bytes)value);
                }
            }

            return ((CallSite<Func<CallSite, object, IEnumerable>>)site).Update(site, value);
        }

        public IEnumerator ObjectToIEnumeratorConversion(CallSite site, object value) {
            if (value != null) {
                if (value is string) {
                    return PythonOps.StringEnumerator((string)value).Key;
                } else if (value.GetType() == typeof(Bytes)) {
                    return PythonOps.BytesEnumerator((Bytes)value).Key;
                }
            }

            return ((CallSite<Func<CallSite, object, IEnumerator>>)site).Update(site, value);
        }

        public Complex BigIntegerToComplexConversion(CallSite site, BigInteger value) {
            return BigIntegerOps.ConvertToComplex(value);
        }

        public Complex BigIntegerObjectToComplexConversion(CallSite site, object value) {
            if (value is BigInteger) {
                return BigIntegerOps.ConvertToComplex((BigInteger)value);
            }

            return ((CallSite<Func<CallSite, object, Complex>>)site).Update(site, value);
        }

        public object BigIntegerToComplexObjectConversion(CallSite site, BigInteger value) {
            return (object)BigIntegerOps.ConvertToComplex((BigInteger)value);
        }

        private class IdentityConversion {
            private readonly Type _type;

            public IdentityConversion(Type type) {
                _type = type;
            }
            public object Convert(CallSite site, object value) {
                if (value != null && value.GetType() == _type) {
                    return value;
                }

                return ((CallSite<Func<CallSite, object, object>>)site).Update(site, value);
            }
        }

        private class IdentityConversion<T> {
            private readonly Type _type;

            public IdentityConversion(Type type) {
                _type = type;
            }

            public T Convert(CallSite site, object value) {
                if (value != null && value.GetType() == _type) {
                    return (T)value;
                }

                return ((CallSite<Func<CallSite, object, T>>)site).Update(site, value);
            }
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode() ^ _kind.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (!(obj is PythonConversionBinder ob)) {
                return false;
            }

            return ob._context.Binder == _context.Binder && 
                _kind == ob._kind && base.Equals(obj) &&
                _retObject == ob._retObject;
        }

        public PythonContext/*!*/ Context {
            get {
                return _context;
            }
        }

        #region Conversion Logic

        private static DynamicMetaObject TryToGenericInterfaceConversion(DynamicMetaObject/*!*/ self, Type/*!*/ toType, Type/*!*/ fromType, Type/*!*/ wrapperType) {
            if (fromType.IsAssignableFrom(CompilerHelpers.GetType(self.Value))) {
                Type making = wrapperType.MakeGenericType(toType.GetGenericArguments());

                self = self.Restrict(CompilerHelpers.GetType(self.Value));

                return new DynamicMetaObject(
                    Ast.New(
                        making.GetConstructor(new Type[] { fromType }),
                        AstUtils.Convert(
                            self.Expression,
                            fromType
                        )
                    ),
                    self.Restrictions
                );
            }
            return null;
        }

        private static DynamicMetaObject/*!*/ MakeToArrayConversion(DynamicMetaObject/*!*/ self, Type/*!*/ toType) {
            self = self.Restrict(typeof(PythonTuple));

            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.ConvertTupleToArray)).MakeGenericMethod(toType.GetElementType()),
                    self.Expression
                ),
                self.Restrictions
            );
        }

        private DynamicMetaObject TryToCharConversion(DynamicMetaObject/*!*/ self) {
            DynamicMetaObject res;
            // we have an implicit conversion to char if the
            // string length == 1, but we can only represent
            // this is implicit via a rule.
            string strVal = self.Value as string;
            Expression strExpr = self.Expression;
            if (strVal == null) {
                if (self.Value is Extensible<string> extstr) {
                    strVal = extstr.Value;
                    strExpr =
                        Ast.Property(
                            AstUtils.Convert(
                                strExpr,
                                typeof(Extensible<string>)
                            ),
                            typeof(Extensible<string>).GetProperty(nameof(Extensible<string>.Value))
                        );
                }
            }

            // we can only produce a conversion if we have a string value...
            if (strVal != null) {
                self = self.Restrict(self.GetRuntimeType());

                Expression getLen = Ast.Property(
                    AstUtils.Convert(
                        strExpr,
                        typeof(string)
                    ),
                    typeof(string).GetProperty("Length")
                );

                if (strVal.Length == 1) {
                    res = new DynamicMetaObject(
                        Ast.Call(
                            AstUtils.Convert(strExpr, typeof(string)),
                            typeof(string).GetMethod("get_Chars", new[] { typeof(int) }),
                            AstUtils.Constant(0)
                        ),
                        self.Restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Ast.Equal(getLen, AstUtils.Constant(1))))
                    );
                } else {
                    res = new DynamicMetaObject(
                        this.Throw(
                            Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.TypeError)),
                                AstUtils.Constant("expected string of length 1 when converting to char, got '{0}'"),
                                Ast.NewArrayInit(typeof(object), self.Expression)
                            ),
                            ReturnType
                        ),
                        self.Restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Ast.NotEqual(getLen, AstUtils.Constant(1))))
                    );
                }
            } else {
                // let the base class produce the rule
                res = null;
            }

            return res;
        }

        private DynamicMetaObject/*!*/ MakeToBoolConversion(DynamicMetaObject/*!*/ self) {
            DynamicMetaObject res;
            if (self.HasValue) {
                self = self.Restrict(self.GetRuntimeType());
            }

            // Optimization: if we already boxed it to a bool, and now
            // we're unboxing it, remove the unnecessary box.
            if (self.Expression.NodeType == ExpressionType.Convert && self.Expression.Type == typeof(object)) {
                var convert = (UnaryExpression)self.Expression;
                if (convert.Operand.Type == typeof(bool)) {
                    return new DynamicMetaObject(convert.Operand, self.Restrictions);
                }
            }

            if (self.GetLimitType() == typeof(DynamicNull)) {
                // None has no __bool__ and no __len__ but it's always false
                res = MakeNoneToBoolConversion(self);
            } else if (self.GetLimitType() == typeof(bool)) {
                // nothing special to convert from bool to bool
                res = self;
            } else if (typeof(IStrongBox).IsAssignableFrom(self.GetLimitType())) {
                // Explictly block conversion of References to bool
                res = MakeStrongBoxToBoolConversionError(self);
            } else if (self.GetLimitType().IsPrimitive || self.GetLimitType().IsEnum) {
                // optimization - rather than doing a method call for primitives and enums generate
                // the comparison to zero directly.
                res = MakePrimitiveToBoolComparison(self);
            } else {
                // anything non-null that doesn't fall under one of the above rules is true.  So we
                // fallback to the base Python conversion which will check for __bool__ and
                // __len__.  The fallback is handled by our ConvertTo site binder.
                return
                    PythonProtocol.ConvertToBool(this, self) ??
                    new DynamicMetaObject(
                        AstUtils.Constant(true),
                        self.Restrictions
                    );
            }

            return res;
        }

        private static DynamicMetaObject/*!*/ MakeNoneToBoolConversion(DynamicMetaObject/*!*/ self) {
            // null is never true
            return new DynamicMetaObject(
                AstUtils.Constant(false),
                self.Restrictions
            );
        }

        private static DynamicMetaObject/*!*/ MakePrimitiveToBoolComparison(DynamicMetaObject/*!*/ self) {
            object zeroVal = Activator.CreateInstance(self.GetLimitType());

            return new DynamicMetaObject(
                Ast.NotEqual(
                    AstUtils.Constant(zeroVal),
                    self.Expression
                ),
                self.Restrictions
            );
        }

        private DynamicMetaObject/*!*/ MakeStrongBoxToBoolConversionError(DynamicMetaObject/*!*/ self) {
            return new DynamicMetaObject(
                this.Throw(
                    Ast.Call(
                        typeof(ScriptingRuntimeHelpers).GetMethod(nameof(ScriptingRuntimeHelpers.SimpleTypeError)),
                        AstUtils.Constant("Can't convert a Reference<> instance to a bool")
                    ),
                    ReturnType
                ),
                self.Restrictions
            );
        }

        private DynamicMetaObject ConvertFromBufferProtocolToMemory(DynamicMetaObject self, Type toType) {
            return new DynamicMetaObject(
                AstUtils.Convert(
                    Ast.Call(
                        Ast.Call(
                            AstUtils.Convert(self.Expression, typeof(IBufferProtocol)),
                            typeof(IBufferProtocol).GetMethod(nameof(IBufferProtocol.GetBuffer)),
                            AstUtils.Constant(BufferFlags.Simple, typeof(BufferFlags))
                        ),
                        typeof(IPythonBuffer).GetMethod(nameof(IPythonBuffer.ToMemory))
                    ),
                    toType
                ),
                self.Restrictions
            );
        }

        private DynamicMetaObject ConvertFromBufferProtocolToByteList(DynamicMetaObject self, Type toType) {
            return new DynamicMetaObject(
                AstUtils.Convert(
                    Ast.Call(
                        Ast.Call(
                            AstUtils.Convert(self.Expression, typeof(IBufferProtocol)),
                            typeof(IBufferProtocol).GetMethod(nameof(IBufferProtocol.GetBuffer)),
                            AstUtils.Constant(BufferFlags.Simple, typeof(BufferFlags))
                        ),
                        typeof(IPythonBuffer).GetMethod(nameof(IPythonBuffer.ToBytes)),
                        AstUtils.Constant(0, typeof(int)),
                        AstUtils.Constant(null, typeof(Nullable<int>))
                    ),
                    toType
                ),
                self.Restrictions
            );
        }

        internal static DynamicMetaObject ConvertToIEnumerable(DynamicMetaObjectBinder/*!*/ conversion, DynamicMetaObject/*!*/ metaUserObject) {
            PythonType pt = MetaPythonObject.GetPythonType(metaUserObject);
            PythonContext pyContext = PythonContext.GetPythonContext(conversion);
            CodeContext context = pyContext.SharedContext;
            PythonTypeSlot pts;

            if (pt.TryResolveSlot(context, "__iter__", out pts)) {
                return MakeIterRule(metaUserObject, nameof(PythonOps.CreatePythonEnumerable));
            } else if (pt.TryResolveSlot(context, "__getitem__", out pts)) {
                return MakeGetItemIterable(metaUserObject, pyContext, pts, nameof(PythonOps.CreateItemEnumerable));
            }

            return null;
        }

        internal static DynamicMetaObject ConvertToIEnumerator(DynamicMetaObjectBinder/*!*/ conversion, DynamicMetaObject/*!*/ metaUserObject) {
            PythonType pt = MetaPythonObject.GetPythonType(metaUserObject);
            PythonContext state = PythonContext.GetPythonContext(conversion);
            CodeContext context = state.SharedContext;
            PythonTypeSlot pts;


            if (pt.TryResolveSlot(context, "__iter__", out pts)) {
                ParameterExpression tmp = Ast.Parameter(typeof(object), "iterVal");

                return new DynamicMetaObject(
                    Expression.Block(
                        new[] { tmp },
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.CreatePythonEnumerator)),
                            Ast.Block(
                                MetaPythonObject.MakeTryGetTypeMember(
                                    state,
                                    pts,
                                    metaUserObject.Expression,
                                    tmp
                                ),
                                DynamicExpression.Dynamic(
                                    new PythonInvokeBinder(
                                        state,
                                        new CallSignature(0)
                                    ),
                                    typeof(object),
                                    AstUtils.Constant(context),
                                    tmp
                                )
                            )
                        )
                    ),
                    metaUserObject.Restrictions
                );
            } else if (pt.TryResolveSlot(context, "__getitem__", out pts)) {
                return MakeGetItemIterable(metaUserObject, state, pts, nameof(PythonOps.CreateItemEnumerator));
            }

            return null;
        }

        private static DynamicMetaObject MakeGetItemIterable(DynamicMetaObject metaUserObject, PythonContext state, PythonTypeSlot pts, string method) {
            ParameterExpression tmp = Ast.Parameter(typeof(object), "getitemVal");
            return new DynamicMetaObject(
                Expression.Block(
                    new[] { tmp },
                    Expression.Call(
                        typeof(PythonOps).GetMethod(method),
                        Ast.Block(
                            MetaPythonObject.MakeTryGetTypeMember(
                                state,
                                pts,
                                tmp,
                                metaUserObject.Expression,
                                Ast.Call(
                                    typeof(DynamicHelpers).GetMethod(nameof(DynamicHelpers.GetPythonType)),
                                    AstUtils.Convert(
                                        metaUserObject.Expression,
                                        typeof(object)
                                    )
                                )
                            ),
                            tmp
                        ),
                        AstUtils.Constant(
                            CallSite<Func<CallSite, CodeContext, object, int, object>>.Create(
                                new PythonInvokeBinder(state, new CallSignature(1))
                            )
                        )
                    )
                ),
                metaUserObject.Restrictions
            );
        }

        private static DynamicMetaObject/*!*/ MakeIterRule(DynamicMetaObject/*!*/ self, string methodName) {
            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod(methodName),
                    AstUtils.Convert(self.Expression, typeof(object))
                ),
                self.Restrictions
            );
        }

        #endregion

        public override string ToString() {
            return String.Format("Python Convert {0} {1}", Type, ResultKind);
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeConversionAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(Type),
                AstUtils.Constant(ResultKind)
            );
        }

        #endregion
    }

    internal class CompatConversionBinder : ConvertBinder {
        private readonly PythonConversionBinder _binder;

        public CompatConversionBinder(PythonConversionBinder binder, Type toType, bool isExplicit)
            : base(toType, isExplicit) {
            _binder = binder;
        }

        public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion) {
            return _binder.FallbackConvert(ReturnType, target, errorSuggestion);
        }
    }
}
