// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    // note: any changes in how this iterator works should also be applied in the
    //       optimized overloads of Builtins.map()
    [PythonType("str_iterator")]
    public sealed class PythonStrIterator : IEnumerable, IEnumerator<string> {
        private readonly string/*!*/ _s;
        private int _index;

        internal PythonStrIterator(string s) {
            Assert.NotNull(s);

            _index = -1;
            _s = s;
        }

        public PythonTuple __reduce__(CodeContext/*!*/ context) {
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            return PythonTuple.MakeTuple(
                iter,
                PythonTuple.MakeTuple(_s),
                _index + 1
            );
        }

        public void __setstate__(int index) {
            _index = index - 1;
        }

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        #region IEnumerator<string> Members

        [PythonHidden]
        public string Current {
            get {
                if (_index < 0) {
                    throw PythonOps.SystemError("Enumeration has not started. Call MoveNext.");
                } else if (_index >= _s.Length) {
                    throw PythonOps.SystemError("Enumeration already finished.");
                }
                return ScriptingRuntimeHelpers.CharToString(_s[_index]);
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
                return ((IEnumerator<string>)this).Current;
            }
        }

        [PythonHidden]
        public bool MoveNext() {
            if (_index >= _s.Length) {
                return false;
            }
            _index++;
            return _index != _s.Length;
        }

        [PythonHidden]
        public void Reset() {
            _index = -1;
        }

        #endregion
    }
}
