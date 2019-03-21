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
        private readonly int _savedIndex;
        private readonly object _obj;
        private readonly object _getItemMethod;
        private object _current;
        private int _index;

        protected ReversedEnumerator(int length, object obj, object getitem) {
            _index = _savedIndex = length;
            _obj = obj;
            _getItemMethod = getitem;
        }

        public static object __new__(CodeContext context, PythonType type, [NotNull]IReversible o) {
            return o.__reversed__();
        }

        public static object __new__(CodeContext context, PythonType type, object o) {
            if (PythonOps.TryGetBoundAttr(context, o, "__reversed__", out object reversed)) {
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
                return new ReversedEnumerator(length, o, boundFunc);
            }

            return type.CreateInstance(context, length, getitem);
        }

        public int __length_hint__() { return _index; }

        public ReversedEnumerator/*!*/ __iter__() {
            return this;
        }

        public PythonTuple __reduce__() {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(_obj),
                _index - 1
            );
        }

        public void __setstate__(int position) {
            if (position < 0) position = 0;
            else if (position > _savedIndex) position = _savedIndex;
            _index = position + 1;
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
