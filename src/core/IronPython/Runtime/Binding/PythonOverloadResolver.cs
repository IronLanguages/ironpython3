﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {
    internal sealed class PythonOverloadResolverFactory : OverloadResolverFactory {
        private readonly PythonBinder/*!*/ _binder;
        internal readonly Expression/*!*/ _codeContext;

        public PythonOverloadResolverFactory(PythonBinder/*!*/ binder, Expression/*!*/ codeContext) {
            Assert.NotNull(binder, codeContext);
            _binder = binder;
            _codeContext = codeContext;
        }

        public override DefaultOverloadResolver CreateOverloadResolver(IList<DynamicMetaObject> args, CallSignature signature, CallTypes callType) {
            return new PythonOverloadResolver(_binder, args, signature, callType, _codeContext);
        }
    }

    internal sealed class PythonOverloadResolver : DefaultOverloadResolver {
        private readonly Expression _context;

        public Expression ContextExpression {
            get { return _context; }
        }

        // instance method call:
        public PythonOverloadResolver(PythonBinder binder, DynamicMetaObject instance, IList<DynamicMetaObject> args, CallSignature signature,
            Expression codeContext)
            : base(binder, instance, args, signature) {
            Assert.NotNull(codeContext);
            _context = codeContext;
        }

        // method call:
        public PythonOverloadResolver(PythonBinder binder, IList<DynamicMetaObject> args, CallSignature signature, Expression codeContext)
            : this(binder, args, signature, CallTypes.None, codeContext) {
        }

        // method call:
        public PythonOverloadResolver(PythonBinder binder, IList<DynamicMetaObject> args, CallSignature signature, CallTypes callType, Expression codeContext)
            : base(binder, args, signature, callType) {
            Assert.NotNull(codeContext);
            _context = codeContext;
        }

        private new PythonBinder Binder {
            get {
                return (PythonBinder)base.Binder;
            }
        }

        public override Candidate SelectBestConversionFor(DynamicMetaObject arg, ParameterWrapper candidateOne, ParameterWrapper candidateTwo, NarrowingLevel level) {
            Candidate basePreferred = base.SelectBestConversionFor(arg, candidateOne, candidateTwo, level);
            if (basePreferred == Candidate.One || basePreferred == Candidate.Two) {
                return basePreferred;
            }

            // Work around the choice made in Converter.PreferConvert
            // This cannot be done using NarrowingLevel rules because it would confuse rules for selecting custom operators
            if (level >= PythonNarrowing.IndexOperator && Converter.IsPythonBigInt(arg.LimitType)) {
                TypeCode c1tc = candidateOne.Type.GetTypeCode();
                TypeCode c2tc = candidateTwo.Type.GetTypeCode();
                if (c1tc is TypeCode.UInt32 && c2tc is TypeCode.Int32) return Candidate.Two;
                if (c1tc is TypeCode.Int32 && c2tc is TypeCode.UInt32) return Candidate.One;
                if (c1tc is TypeCode.UInt64 && c2tc is TypeCode.Int64) return Candidate.Two;
                if (c1tc is TypeCode.Int64 && c2tc is TypeCode.UInt64) return Candidate.One;
            }

            // This block codifies the following set of rules for Python numerics (i.e. numeric types and any derived types):
            // 1. Prefer the parameter that is of the matching kind to the argument type (i.e. floating-point to floating-point, or integer to integer).
            // 2. If both parameters are of the matching kind, break the tie by prefering the one that is wider, if any (so the chance of overflow is lower).
            //    "Wider" here means being able to represent all values of the narrower type; e.g. in this sense UInt64 is not wider than SByte.
            // There are the following exceptions to these rules:
            // * Rule 1 is not applied to user-defined subtypes (i.e. subclasses of Extensible<T>). This is to allow such types to bind to parameters
            //   of type object or an interface on Extensible<T> (rule from the DLR). It is because a user-defined type may have user-defined operators
            //   that may need to be inspected by the overload and provide additional functionality above a simple numeric value.
            // * Rule 2 is not applied on narrowing level None, so in a case of a widening cast or a perfect type match, the most efficient (narrowest)
            //   type is selected (rule from the DLR).
            // * If one of the parameters is Boolean and the other is numeric, the Boolean parameter is treated as a 1-bit-wide numeric (thus becomes a case for Rule 2).
            //   This makes the preference for numeric types over Boolean consistent with the one encoded using narrowing levels and conversion sequence.
            // TODO: Increase test coverage for cases involving Complex, Decimal, Half
            if (Converter.IsPythonNumeric(arg.LimitType)) {
                if (Converter.IsPythonFloatingPoint(arg.LimitType)) {
                    if (Converter.IsFloatingPoint(candidateOne.Type)) {
                        if (!Converter.IsFloatingPoint(candidateTwo.Type)) {
                            if (!Converter.IsExtensibleNumeric(arg.LimitType)) {
                                return Candidate.One;
                            }
                        } else if (level > PythonNarrowing.None) { // both params are floating point
                            Candidate preferred = SelectWiderNumericType(candidateOne.Type, candidateTwo.Type);
                            if (preferred != Candidate.Ambiguous) {
                                return preferred;
                            }
                        }
                    } else if (Converter.IsFloatingPoint(candidateTwo.Type)) {
                        if (!Converter.IsExtensibleNumeric(arg.LimitType)) {
                            return Candidate.Two;
                        }
                    }
                } else { // arg is integer
                    if (Converter.IsInteger(candidateOne.Type)) {
                        if (!Converter.IsInteger(candidateTwo.Type)) {
                            if (!Converter.IsExtensibleNumeric(arg.LimitType) || (Converter.IsBoolean(candidateTwo.Type) && level > PythonNarrowing.None)) {
                                return Candidate.One;
                            }
                        } else if (level > PythonNarrowing.None) { // both params are integer
                            Candidate preferred = SelectWiderNumericType(candidateOne.Type, candidateTwo.Type);
                            if (preferred != Candidate.Ambiguous) {
                                return preferred;
                            }
                        }
                    } else if (Converter.IsInteger(candidateTwo.Type)) {
                        if (!Converter.IsExtensibleNumeric(arg.LimitType) || (Converter.IsBoolean(candidateOne.Type) && level > PythonNarrowing.None)) {
                            return Candidate.Two;
                        }
                    }
                }
            }

            return basePreferred;
        }

        public override bool CanConvertFrom(Type fromType, DynamicMetaObject fromArg, ParameterWrapper toParameter, NarrowingLevel level) {
            Type toType = toParameter.Type;

            if (IsBytesLikeParameter(toParameter)) {

                if ((fromType == typeof(PythonList) || fromType.IsSubclassOf(typeof(PythonList)))) {
                    if (toType.IsGenericType &&
                        toType.GetGenericTypeDefinition() == typeof(IList<>)) {
                        return false;
                    }
                }

                if (typeof(IBufferProtocol).IsAssignableFrom(fromType)) {
                    if (toParameter.Type == typeof(IList<byte>) || toParameter.Type == typeof(IReadOnlyList<byte>)) {
                        return true;
                    }
                }
            }

            if (fromType.IsGenericType &&
                fromType.GetGenericTypeDefinition() == typeof(Memory<>) &&
                toType.IsGenericType &&
                toType.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>) &&
                fromType.GetGenericArguments()[0] == toType.GetGenericArguments()[0]) {
                return true;
            }

            if (toType == typeof(IBufferProtocol)) {
                if (fromType == typeof(Memory<byte>) ||
                    fromType == typeof(ReadOnlyMemory<byte>) ||
                    fromType == typeof(byte[]) ||
                    fromType == typeof(ArraySegment<byte>)) {
                    return true;
                }
            }

            if (fromType == typeof(int) && toType.IsAssignableFrom(typeof(BigInteger))) {
                return true; // PEP 237: int/long unification (GH #52)
            }

            return base.CanConvertFrom(fromType, fromArg, toParameter, level);
        }

        protected override BitArray MapSpecialParameters(ParameterMapping/*!*/ mapping) {
            var infos = mapping.Overload.Parameters;
            BitArray special = base.MapSpecialParameters(mapping);

            if (infos.Count > 0) {
                bool normalSeen = false;
                for (int i = 0; i < infos.Count; i++) {
                    bool isSpecial = false;
                    if (infos[i].ParameterType.IsSubclassOf(typeof(SiteLocalStorage))) {
                        mapping.AddBuilder(new SiteLocalStorageBuilder(infos[i]));
                        isSpecial = true;
                    } else if (infos[i].ParameterType == typeof(CodeContext) && !normalSeen) {
                        mapping.AddBuilder(new ContextArgBuilder(infos[i]));
                        isSpecial = true;
                    } else {
                        normalSeen = true;
                    }

                    if (isSpecial) {
                        (special = special ?? new BitArray(infos.Count))[i] = true;
                    }
                }
            }

            return special;
        }

        protected override Expression GetByRefArrayExpression(Expression argumentArrayExpression) {
            return Expression.Call(typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTuple)), argumentArrayExpression);
        }

        protected override bool AllowMemberInitialization(OverloadInfo method) {
            return method.IsInstanceFactory && !method.DeclaringType.IsDefined(typeof(PythonTypeAttribute), true);
        }

        public override Expression Convert(DynamicMetaObject metaObject, Type restrictedType, ParameterInfo info, Type toType) {
            return Binder.ConvertExpression(metaObject.Expression, toType, ConversionResultKind.ExplicitCast, new PythonOverloadResolverFactory(Binder, _context));
        }

        public override Expression GetDynamicConversion(Expression value, Type type) {
            return DynamicExpression.Dynamic(
                Binder.Context.Convert(type, ConversionResultKind.ExplicitCast),
                type,
                value);
        }

        public override Type GetGenericInferenceType(DynamicMetaObject dynamicObject) {
            Type res = PythonTypeOps.GetFinalSystemType(dynamicObject.LimitType);
            if (res == typeof(ExtensibleString) ||
                res == typeof(ExtensibleComplex) ||
                (res.IsGenericType && res.GetGenericTypeDefinition() == typeof(Extensible<>))) {
                return typeof(object);
            }

            return res;
        }

        private static Candidate SelectWiderNumericType(Type candidateOneType, Type candidateTwoType) {
            if (GetUnmanagedNumericTypeWidth(candidateOneType) is not int candidateOneWidth) return Candidate.Ambiguous;
            if (GetUnmanagedNumericTypeWidth(candidateTwoType) is not int candidateTwoWidth) return Candidate.Ambiguous;

            Candidate preferred = Comparer<int>.Default.Compare(candidateOneWidth, candidateTwoWidth) switch {
                 1 => Candidate.One,
                 0 => Candidate.Equivalent,
                -1 => Candidate.Two,
                 _ => throw new InvalidOperationException()
            };

            if (preferred == Candidate.One && Converter.IsUnsignedInt(candidateOneType) && !Converter.IsUnsignedInt(candidateTwoType)) {
                preferred = Candidate.Ambiguous;
            } else if (preferred == Candidate.Two && Converter.IsUnsignedInt(candidateTwoType) && !Converter.IsUnsignedInt(candidateOneType)) {
                preferred = Candidate.Ambiguous;
            }
            return preferred;
        }

        private static int? GetUnmanagedNumericTypeWidth(Type type) {
            return type.GetTypeCode() switch {
                TypeCode.SByte => sizeof(sbyte),
                TypeCode.Int16 => sizeof(short),
                TypeCode.Int32 => sizeof(int),
                TypeCode.Int64 => sizeof(long),
                TypeCode.Byte => sizeof(byte),
                TypeCode.UInt16 => sizeof(ushort),
                TypeCode.UInt32 => sizeof(uint),
                TypeCode.UInt64 => sizeof(ulong),
                TypeCode.Single => sizeof(float),
                TypeCode.Double => sizeof(double),
                _ => null
            };
        }

        private bool IsBytesLikeParameter(ParameterWrapper parameter) {
            return parameter.ParameterInfo?.IsDefined(typeof(BytesLikeAttribute), inherit: false) ?? false;
        }
    }
}
