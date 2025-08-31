// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

using IronPython.Modules;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    [PythonType("bytearray_iterator")]
    public sealed class ByteArrayIterator : IEnumerable, IEnumerator<int> {
        private IList<byte>? _bytes;
        private int _index;

        internal ByteArrayIterator(ByteArray bytes) {
            Assert.NotNull(bytes);

            _bytes = bytes;
            _index = -1;
        }

        public int __length_hint__()
            => _bytes is null ? 0 : _bytes.Count - _index - 1;

        #region Pickling Protocol

        public PythonTuple __reduce__(CodeContext context) {
            // Using BuiltinModuleInstance rather than GetBuiltinsDict() or TryLookupBuiltin() matches CPython 3.8.2 behaviour
            // Older versions of CPython may have a different behaviour
            object? iter = PythonOps.GetBoundAttr(context, context.LanguageContext.BuiltinModuleInstance, nameof(Builtin.iter));

            if (_bytes is null) {
                return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(PythonTuple.EMPTY)); // CPython 3.7 uses empty tuple
            }
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(_bytes), _index + 1);
        }

        public void __setstate__(int state) {
            if (_bytes is null) return;
            _index = Math.Min(Math.Max(0, state), _bytes.Count) - 1;
        }

        #endregion

        #region IEnumerator<T> Members

        [PythonHidden]
        public int Current => _bytes![_index];

        #endregion

        #region IDisposable Members

        [PythonHidden]
        public void Dispose() { }

        #endregion

        #region IEnumerator Members

        object IEnumerator.Current => ((IEnumerator<int>)this).Current;

        [PythonHidden]
        public bool MoveNext() {
            if (_bytes is null || ++_index >= _bytes.Count) {
                _bytes = null; // free after iteration
                return false;
            }
            return true;
        }

        void IEnumerator.Reset() => throw new NotSupportedException();

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() => this;

        #endregion
    }
}
