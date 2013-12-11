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
using System.Collections.Generic;
using System.Text;
using System.Collections;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [PythonType("reversed")]
    public class ReversedEnumerator : IEnumerator {
        private readonly object _getItemMethod;
        private readonly int _savedIndex;
        private object _current;
        private int _index;

        protected ReversedEnumerator(int length, object getitem) {
            this._index = this._savedIndex = length;
            this._getItemMethod = getitem;
        }

        public static object __new__(CodeContext context, PythonType type, [NotNull]IReversible o) {
            return o.__reversed__();
        }

        public static object __new__(CodeContext context, PythonType type, object o) {
            object reversed;
            if (PythonOps.TryGetBoundAttr(context, o, "__reversed__", out reversed)) {
                return PythonCalls.Call(context, reversed);
            }

            object boundFunc;

            PythonTypeSlot getitem;
            PythonType pt = DynamicHelpers.GetPythonType(o);
            if(!pt.TryResolveSlot(context, "__getitem__", out getitem) ||
                !getitem.TryGetValue(context, o, pt, out boundFunc)
                || o is PythonDictionary) {
                throw PythonOps.TypeError("argument to reversed() must be a sequence");
            }

            int length;
            if (!DynamicHelpers.GetPythonType(o).TryGetLength(context, o, out length)) {
                throw PythonOps.TypeError("object of type '{0}' has no len()", DynamicHelpers.GetPythonType(o).Name);
            }

            if (type.UnderlyingSystemType == typeof(ReversedEnumerator)) {
                return new ReversedEnumerator((int)length, boundFunc);
            }

            return type.CreateInstance(context, length, getitem);
        }

        public int __length_hint__() { return _savedIndex; }

        public ReversedEnumerator/*!*/ __iter__() {
            return this;
        }

        #region IEnumerator implementation

        object IEnumerator.Current {
            get {
                return _current;
            }
        }

        bool IEnumerator.MoveNext() {
            if (_index > 0) {
                _index--;
                _current = PythonCalls.Call(_getItemMethod, _index);
                return true;
            } else return false;
        }

        void IEnumerator.Reset() {
            _index = _savedIndex;
        }

        #endregion
    }

}
