// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    public sealed class PythonTypeUserDescriptorSlot : PythonTypeSlot {
        private int _descVersion;
        private PythonTypeSlot _desc;

        private const int UserDescriptorFalse = -1;

        internal PythonTypeUserDescriptorSlot(object value) {
            Value = value;
        }

        internal PythonTypeUserDescriptorSlot(object value, bool isntDescriptor) {
            Value = value;
            if (isntDescriptor) {
                _descVersion = UserDescriptorFalse;
            }
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            try {
                value = PythonOps.GetUserDescriptor(Value, instance, owner);
                return true;
            } catch (MissingMemberException) {
                value = null;
                return false;
            }
        }

        internal object GetValue(CodeContext context, object instance, PythonType owner) {
            if (_descVersion == UserDescriptorFalse) {
                return Value;
            } else if (_descVersion != DynamicHelpers.GetPythonType(Value).Version) {
                CalculateDescriptorInfo();
                if (_descVersion == UserDescriptorFalse) {
                    return Value;
                }
            }

            object res;
            Debug.Assert(_desc.GetAlwaysSucceeds);
            _desc.TryGetValue(context, Value, DynamicHelpers.GetPythonType(Value), out res);
            return context.LanguageContext.Call(context, res, instance, owner);
        }

        private void CalculateDescriptorInfo() {
            PythonType pt = DynamicHelpers.GetPythonType(Value);
            if (!pt.IsSystemType) {
                if (pt.TryResolveSlot(pt.Context.SharedClsContext, "__get__", out _desc)) {
                    _descVersion = pt.Version;
                } else {
                    _descVersion = UserDescriptorFalse;
                }
            } else {
                _descVersion = UserDescriptorFalse;
            }
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            return PythonOps.TryDeleteUserDescriptor(Value, instance);
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            return PythonOps.TrySetUserDescriptor(Value, instance, value);
        }

        internal override bool IsSetDescriptor(CodeContext context, PythonType owner) {
            PythonType pt = DynamicHelpers.GetPythonType(Value);
            if (pt.IsSystemType) return false;
            return pt.TryResolveSlot(context, "__set__", out _);
        }

        internal object Value { get; set; }
    }
}
