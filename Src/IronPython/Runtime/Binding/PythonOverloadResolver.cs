// Licensed to the .NET Foundation under one or more agreements.
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

            if (Converter.IsNumeric(arg.LimitType)) {
                if (Converter.IsFloatingPoint(arg.LimitType)) {
                    if (Converter.IsFloatingPoint(candidateOne.Type)) {
                        if (!Converter.IsFloatingPoint(candidateTwo.Type)) {
                            return Candidate.One;
                        } else if (level != PythonNarrowing.None) { // both params are floating point
                            Candidate preferred = GetWiderCandidate(candidateOne, candidateTwo);
                            if (preferred != Candidate.Ambiguous) {
                                return preferred;
                            }
                        }
                    } else if (Converter.IsFloatingPoint(candidateTwo.Type)) {
                        return Candidate.Two;
                    }
                } else { // arg is integer
                    if (Converter.IsInteger(candidateOne.Type)) {
                        if (!Converter.IsInteger(candidateTwo.Type)) {
                            return Candidate.One;
                        } else if (level != PythonNarrowing.None) { // both params are integer
                            Candidate preferred = GetWiderCandidate(candidateOne, candidateTwo);
                            if (preferred != Candidate.Ambiguous) {
                                return preferred;
                            }
                        }
                    } else if (Converter.IsInteger(candidateTwo.Type)) {
                        return Candidate.Two;
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

        private static Candidate GetWiderCandidate(ParameterWrapper candidateOne, ParameterWrapper candidateTwo) {
            if (GetUnmanagedNumericTypeWidth(candidateOne.Type) is not int candidateOneWidth) return Candidate.Ambiguous;
            if (GetUnmanagedNumericTypeWidth(candidateTwo.Type) is not int candidateTwoWidth) return Candidate.Ambiguous;

            Candidate preferred = Comparer<int>.Default.Compare(candidateOneWidth, candidateTwoWidth) switch {
                 1 => Candidate.One,
                 0 => Candidate.Equivalent,
                -1 => Candidate.Two,
                 _ => throw new InvalidOperationException()
            };

            if (preferred == Candidate.One && Converter.IsUnsignedInt(candidateOne.Type) && !Converter.IsUnsignedInt(candidateTwo.Type)) {
                preferred = Candidate.Ambiguous;
            } else if (preferred == Candidate.Two && Converter.IsUnsignedInt(candidateTwo.Type) && !Converter.IsUnsignedInt(candidateOne.Type)) {
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
                TypeCode.Byte  => sizeof(byte),
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
