// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Generation;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;
using System.Collections;

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
                        }
                    } else if (Converter.IsFloatingPoint(candidateTwo.Type)) {
                        return Candidate.Two;
                    }
                } else { // arg is integer
                    if (Converter.IsInteger(candidateOne.Type)) {
                        if (!Converter.IsInteger(candidateTwo.Type)) {
                            return Candidate.One;
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

        private bool IsBytesLikeParameter(ParameterWrapper parameter) {
            return parameter.ParameterInfo?.IsDefined(typeof(BytesLikeAttribute), inherit: false) ?? false;
        }
    }
}
