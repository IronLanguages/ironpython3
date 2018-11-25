// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [PythonType("reversed")]
    public class ReversedEnumerator : IEnumerator, IEnumerable {
        private readonly object _getItemMethod;
        private readonly int _savedIndex;
        private object _current;
        private int _index;

        protected ReversedEnumerator(int length, object getitem) {
            _index = _savedIndex = length;
            _getItemMethod = getitem;
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

        #region IEnumerable implementation

        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion
    }

}
