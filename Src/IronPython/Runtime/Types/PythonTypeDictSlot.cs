// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Provides a slot object for the dictionary to allow setting of the dictionary.
    /// </summary>
    [PythonType("getset_descriptor")]
    public sealed class PythonTypeDictSlot : PythonTypeSlot, ICodeFormattable {
        private PythonType/*!*/ _type;

        public PythonTypeDictSlot(PythonType type) {
            _type = type;
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (instance == null) {
                value = new MappingProxy(context, owner);
                return true;
            }

            if (instance is PythonType pt) {
                value = new MappingProxy(context, pt);
                return true;
            }

            if (instance is IPythonObject sdo && sdo.PythonType.HasDictionary) {
                PythonDictionary res = sdo.Dict;
                if (res != null || (res = sdo.SetDict(sdo.PythonType.MakeDictionary())) != null) {
                    value = res;
                    return true;
                }
            }

            value = null;
            return false;
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            if (instance is IPythonObject sdo) {
                if (!(value is PythonDictionary))
                    throw PythonOps.TypeError("__dict__ must be set to a dictionary, not '{0}'", owner.Name);

                if (!sdo.PythonType.HasDictionary) {
                    return false;
                }

                sdo.ReplaceDict((PythonDictionary)value);
                return true;
            }

            if (instance == null) throw PythonOps.AttributeError("'__dict__' of '{0}' objects is not writable", owner.Name);
            return false;
        }

        internal override bool IsSetDescriptor(CodeContext context, PythonType owner) {
            return true;
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            if (instance is IPythonObject sdo) {
                if(!sdo.PythonType.HasDictionary) {
                    return false;
                }

                sdo.ReplaceDict(null);
                return true;
            }

            if (instance == null) throw PythonOps.TypeError("'__dict__' of '{0}' objects is not writable", owner.Name);
            return false;
        }

        public void __set__(CodeContext context, object instance, object value) {
            TrySetValue(context, instance, DynamicHelpers.GetPythonType(instance), value);
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return String.Format("<attribute '__dict__' of '{0}' objects>", _type.Name);
        }

        #endregion
    }

}
