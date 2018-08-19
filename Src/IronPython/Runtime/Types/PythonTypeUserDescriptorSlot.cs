// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    public sealed class PythonTypeUserDescriptorSlot : PythonTypeSlot {
        private object _value;
        private int _descVersion;
        private PythonTypeSlot _desc;

        private const int UserDescriptorFalse = -1;

        internal PythonTypeUserDescriptorSlot(object value) {
            _value = value;
        }
        
        internal PythonTypeUserDescriptorSlot(object value, bool isntDescriptor) {
            _value = value;
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
                return _value;
            } else if (_descVersion != DynamicHelpers.GetPythonType(_value).Version) {
                CalculateDescriptorInfo();
                if (_descVersion == UserDescriptorFalse) {
                    return _value;
                }
            }

            object res;
            Debug.Assert(_desc.GetAlwaysSucceeds);
            _desc.TryGetValue(context, _value, DynamicHelpers.GetPythonType(_value), out res);
            return context.LanguageContext.Call(context, res, instance, owner);
        }

        private void CalculateDescriptorInfo() {
            PythonType pt = DynamicHelpers.GetPythonType(_value);
            if (!pt.IsSystemType) {
                _descVersion = pt.Version;
                if (!pt.TryResolveSlot(pt.Context.SharedClsContext, "__get__", out _desc)) {
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
            object dummy;
            return PythonOps.TryGetBoundAttr(context, Value, "__set__", out dummy);
        }

        internal object Value {
            get {
                return _value;
            }
            set {
                _value = value;
            }
        }
    }
}
