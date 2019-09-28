// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    [PythonType("field#")]
    public sealed class ReflectedField : PythonTypeSlot, ICodeFormattable {
        private readonly NameType _nameType;
        internal readonly FieldInfo/*!*/ _info;
        internal const string UpdateValueTypeFieldWarning = "Setting field {0} on value type {1} may result in updating a copy.  Use {1}.{0}.SetValue(instance, value) if this is safe.  For more information help({1}.{0}.SetValue).";

        public ReflectedField(FieldInfo/*!*/ info, NameType nameType) {
            Debug.Assert(info != null);

            _nameType = nameType;
            _info = info;
        }

        public ReflectedField(FieldInfo/*!*/ info)
            : this(info, NameType.PythonField) {
        }

        #region Public Python APIs

        public FieldInfo Info {
            [PythonHidden]
            get { 
                return _info; 
            }
        }

        /// <summary>
        /// Convenience function for users to call directly
        /// </summary>
        public object GetValue(CodeContext context, object instance) {
            object value;
            if (TryGetValue(context, instance, DynamicHelpers.GetPythonType(instance), out value)) {
                return value;
            }
            throw new InvalidOperationException("cannot get field");
        }

        /// <summary>
        /// This function can be used to set a field on a value type without emitting a warning. Otherwise it is provided only to have symmetry with properties which have GetValue/SetValue for supporting explicitly implemented interfaces.
        /// 
        /// Setting fields on value types usually warns because it can silently fail to update the value you expect.  For example consider this example where Point is a value type with the public fields X and Y:
        /// 
        /// arr = System.Array.CreateInstance(Point, 10)
        /// arr[0].X = 42
        /// print arr[0].X
        /// 
        /// prints 0.  This is because reading the value from the array creates a copy of the value.  Setting the value then mutates the copy and the array does not get updated.  The same problem exists when accessing members of a class.
        /// </summary>
        public void SetValue(CodeContext context, object instance, object value) {
            if (!TrySetValueWorker(context, instance, DynamicHelpers.GetPythonType(instance), value, true)) {
                throw new InvalidOperationException("cannot set field");
            }            
        }

        public void __set__(CodeContext/*!*/ context, object instance, object value) {
            if (instance == null && _info.IsStatic) {
                DoSet(context, null, value, false);
            } else if (!_info.IsStatic) {
                DoSet(context, instance, value, false);
            } else {
                throw PythonOps.AttributeErrorForReadonlyAttribute(_info.DeclaringType.Name, _info.Name);
            }
        }

        [SpecialName]
        public void __delete__(object instance) {
            throw PythonOps.AttributeErrorForBuiltinAttributeDeletion(_info.DeclaringType.Name, _info.Name);
        }

        public string __doc__ {
            get {
                return DocBuilder.DocOneInfo(_info);
            }
        }

        public PythonType FieldType {
            [PythonHidden]
            get {
                return DynamicHelpers.GetPythonTypeFromType(_info.FieldType);
            }
        }

        #endregion

        #region Internal APIs

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Fields, this);
            if (instance == null) {
                value = _info.IsStatic ? _info.GetValue(null) : this;
            } else {
                value = _info.GetValue(context.LanguageContext.Binder.Convert(instance, _info.DeclaringType));
            }

            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        internal override bool CanOptimizeGets {
            get {
                return !_info.IsLiteral;
            }
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            return TrySetValueWorker(context, instance, owner, value, false);
        }

        private bool TrySetValueWorker(CodeContext context, object instance, PythonType owner, object value, bool suppressWarning) {
            if (ShouldSetOrDelete(owner)) {
                DoSet(context, instance, value, suppressWarning);
                return true;
            }

            return false;
        }

        internal override bool IsSetDescriptor(CodeContext context, PythonType owner) {
            // field is settable if it is not readonly
            return (_info.Attributes & FieldAttributes.InitOnly) == 0 && !_info.IsLiteral;
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            if (ShouldSetOrDelete(owner)) {
                throw PythonOps.AttributeErrorForBuiltinAttributeDeletion(_info.DeclaringType.Name, _info.Name);
            }
            return false;
        }

        internal override bool IsAlwaysVisible {
            get {
                return _nameType == NameType.PythonField;
            }
        }

        internal override void MakeGetExpression(PythonBinder/*!*/ binder, Expression/*!*/ codeContext, DynamicMetaObject instance, DynamicMetaObject/*!*/ owner, ConditionalBuilder/*!*/ builder) {
            if (!_info.IsPublic || _info.DeclaringType.ContainsGenericParameters) {
                // fallback to reflection
                base.MakeGetExpression(binder, codeContext, instance, owner, builder);
            } else if (instance == null) {
                if (_info.IsStatic) {
                    builder.FinishCondition(AstUtils.Convert(Ast.Field(null, _info), typeof(object)));
                } else {
                    builder.FinishCondition(Ast.Constant(this));
                }
            } else {
                builder.FinishCondition(
                    AstUtils.Convert(
                        Ast.Field(
                            binder.ConvertExpression(
                                instance.Expression,
                                _info.DeclaringType,
                                ConversionResultKind.ExplicitCast,
                                new PythonOverloadResolverFactory(binder, codeContext)
                            ),
                            _info
                        ),
                        typeof(object)
                    )
                );
            }
        }

        #endregion

        #region Private helpers

        private void DoSet(CodeContext context, object instance, object val, bool suppressWarning) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Fields, this);
            if (_info.IsInitOnly || _info.IsLiteral) {
                throw PythonOps.AttributeErrorForReadonlyAttribute(_info.DeclaringType.Name, _info.Name);
            } else if (!suppressWarning && instance != null && instance.GetType().IsValueType) {
                PythonOps.Warn(context, PythonExceptions.RuntimeWarning, UpdateValueTypeFieldWarning, _info.Name, _info.DeclaringType.Name);
            }

            _info.SetValue(instance, context.LanguageContext.Binder.Convert(val, _info.FieldType));
        }

        private bool ShouldSetOrDelete(PythonType type) {
            PythonType dt = type as PythonType;

            // statics must be assigned through their type, not a derived type.  Non-statics can
            // be assigned through their instances.
            return (dt != null && _info.DeclaringType == dt.UnderlyingSystemType) || !_info.IsStatic || _info.IsLiteral || _info.IsInitOnly;
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<field# {0} on {1}>", _info.Name, _info.DeclaringType.Name);
        }

        #endregion
    }
}
