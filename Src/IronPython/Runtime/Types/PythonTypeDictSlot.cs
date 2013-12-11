/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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
                value = new DictProxy(owner);
                return true;
            }

            PythonType pt = instance as PythonType;
            if (pt != null) {
                value = new DictProxy(pt);
                return true;
            }

            IPythonObject sdo = instance as IPythonObject;
            if (sdo != null && sdo.PythonType.HasDictionary) {
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
            IPythonObject sdo = instance as IPythonObject;
            if (sdo != null) {
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
            IPythonObject sdo = instance as IPythonObject;
            if (sdo != null) {
                if(!sdo.PythonType.HasDictionary) {
                    return false;
                }

                sdo.ReplaceDict(null);
                return true;
            }

            if (instance == null) throw PythonOps.TypeError("'__dict__' of '{0}' objects is not writable", owner.Name);
            return false;
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return String.Format("<attribute '__dict__' of '{0}' objects>", _type.Name);
        }

        #endregion
    }

}
