// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;

using Microsoft.Scripting.Utils;

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
