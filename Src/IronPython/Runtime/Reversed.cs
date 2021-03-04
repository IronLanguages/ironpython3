// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [PythonType("reversed")]
    public class ReversedEnumerator : IEnumerator<object?>, IEnumerable<object?> {
        private readonly int _savedIndex;
        private object? _obj;
        private object? _getItemMethod;
        private int _index;

        protected ReversedEnumerator(int length, object obj, object getitem) {
            _index = _savedIndex = length;
            _obj = obj;
            _getItemMethod = getitem;
        }

        public static object? __new__(CodeContext context, [NotNull] PythonType type, [NotNull] IReversible o) {
            return o.__reversed__();
        }

        public static object? __new__(CodeContext context, [NotNull] PythonType type, object? o) {
            if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__reversed__", out object? res))
                return res;

            object boundFunc;

            PythonTypeSlot getitem;
            PythonType pt = DynamicHelpers.GetPythonType(o);
            if (o is null || o is PythonDictionary
                || !pt.TryResolveSlot(context, "__getitem__", out getitem)
                || !getitem.TryGetValue(context, o, pt, out boundFunc)) {
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

        public int __length_hint__() => _obj is null ? 0 : _index;

        public ReversedEnumerator/*!*/ __iter__() => this;

        public PythonTuple __reduce__() {
            var reversed = DynamicHelpers.GetPythonType(this);
            if (_obj is null)
                return PythonTuple.MakeTuple(reversed, PythonTuple.MakeTuple(PythonTuple.EMPTY));
            return PythonTuple.MakeTuple(reversed, PythonTuple.MakeTuple(_obj), _index - 1);
        }

        public void __setstate__(int position) {
            if (_obj is null) return;
            if (position < 0) position = 0;
            else if (position > _savedIndex) position = _savedIndex;
            _index = position + 1;
        }

        #region IEnumerable<object?> implementation

        [PythonHidden]
        public IEnumerator GetEnumerator() => this;

        IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => this;

        #endregion

        #region IEnumerator<object?> implementation

        [PythonHidden]
        public object? Current { get; private set; }

        [PythonHidden]
        public bool MoveNext() {
            if (_index > 0) {
                _index--;
                Current = PythonCalls.Call(_getItemMethod, _index);
                return true;
            } else {
                _obj = null;
                _getItemMethod = null;
                Current = null;
                return false;
            }
        }

        void IEnumerator.Reset() => throw new NotSupportedException();

        [PythonHidden]
        public void Dispose() { }

        #endregion
    }
}
