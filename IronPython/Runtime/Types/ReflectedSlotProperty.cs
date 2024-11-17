// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {

    /// <summary>
    /// Represents a member of a user-defined type which defines __slots__.  The names listed in
    /// __slots__ have storage allocated for them with the type and provide fast get/set access.
    /// </summary>
    [PythonType("member_descriptor")]
    internal class ReflectedSlotProperty : PythonTypeDataSlot, ICodeFormattable {
        private readonly string/*!*/ _name, _typeName;
        private readonly int/*!*/ _index;

        private static readonly Dictionary<int, SlotValue> _methods = new Dictionary<int, SlotValue>();

        public ReflectedSlotProperty(string/*!*/ name, string/*!*/ typeName, int index) {
            Assert.NotNull(name, typeName);

            _index = index;
            _name = name;
            _typeName = typeName;
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (instance != null) {
                value = Getter(instance);
                PythonOps.CheckInitializedAttribute(value, instance, _name);
                return true;
            }

            value = this;
            return true;
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            if (instance != null) {
                Setter(instance, value);
                return true;
            }

            return false;
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            return TrySetValue(context, instance, owner, Uninitialized.Instance);
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return String.Format("<member '{0}' of '{1}' objects>", _name, _typeName);
        }

        #endregion

        private SlotValue Value {
            get {
                SlotValue res;
                lock (_methods) {
                    if (!_methods.TryGetValue(_index, out res)) {
                        res = _methods[_index] = new SlotValue();
                    }
                }
                return res;
            }
        }

        internal SlotGetValue Getter {
            get {
                SlotValue value = Value;
                lock (value) {
                    EnsureGetter(value);
                    return value.Getter;
                }
            }
        }

        internal SlotSetValue Setter {
            get {
                SlotValue value = Value;
                lock (value) {
                    EnsureSetter(value);
                    return value.Setter;
                }

            }
        }

        /// <summary>
        /// Gets the index into the object array to be used for the slot storage.
        /// </summary>
        internal int Index {
            get {
                return _index;
            }
        }

        private void EnsureGetter(SlotValue value) {
            if (value.Getter == null) {
                value.Getter = (object instance) => ((IPythonObject)instance).GetSlots()[_index];
            }
        }

        private void EnsureSetter(SlotValue value) {
            if (value.Setter == null) {
                value.Setter = (object instance, object setvalue) => ((IPythonObject)instance).GetSlots()[_index] = setvalue;
            }
        }

        private class SlotValue {
            public SlotGetValue Getter;
            public SlotSetValue Setter;
        }
    }

    internal delegate object SlotGetValue(object instance);
    internal delegate void SlotSetValue(object instance, object value);
}
