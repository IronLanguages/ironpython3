﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;

using Microsoft.Scripting.Utils;

using IronPython.Modules;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [PythonType("bytes_iterator")]
    public sealed class PythonBytesIterator : IEnumerable, IEnumerator<int> {
        private readonly IList<byte> _bytes;
        private int _index;

        internal PythonBytesIterator(IList<byte> bytes) {
            Assert.NotNull(bytes);

            _bytes = bytes;
            _index = -1;
        }

        public int __length_hint__()
            => _index < _bytes.Count ? _bytes.Count - _index - 1 : 0;

        #region Pickling Protocol

        public PythonTuple __reduce__(CodeContext context) {
            // Using BuiltinModuleInstance rather than GetBuiltinsDict() or TryLookupBuiltin() matches CPython 3.8.2 behaviour
            // Older versions of CPython may have a different hehaviour
            object? iter = PythonOps.GetBoundAttr(context, context.LanguageContext.BuiltinModuleInstance, nameof(Builtin.iter));

            if (_index < _bytes.Count) {
                return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(_bytes), _index + 1);
            } else {
                return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(Bytes.Empty));
            }
        }

        public void __setstate__(int state) {
            if (_index < _bytes.Count) {
                _index = state <= 0 ? -1 : state - 1;
            }
        }

        #endregion

        #region IEnumerator<T> Members

        [PythonHidden]
        public int Current {
            get {
                if (_index < 0) {
                    throw PythonOps.SystemError("Enumeration has not started. Call MoveNext.");
                } else if (_index >= _bytes.Count) {
                    throw PythonOps.SystemError("Enumeration already finished.");
                }
                return _bytes[_index];
            }
        }

        #endregion

        #region IDisposable Members

        [PythonHidden]
        public void Dispose() { }

        #endregion

        #region IEnumerator Members

        object IEnumerator.Current {
            get {
                return ((IEnumerator<int>)this).Current;
            }
        }

        [PythonHidden]
        public bool MoveNext() {
            if (_index >= _bytes.Count) {
                return false;
            }
            _index++;
            return _index != _bytes.Count;
        }

        [PythonHidden]
        public void Reset() {
            _index = -1;
        }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion
    }
}
