// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {

    [PythonType("filter")]
    [Documentation(@"filter(function or None, iterable) -> filter object

Return an iterator yielding those items of iterable for which function(item)
is true. If function is None, return the items that are true.")]
    public class Filter : IEnumerator {
        private readonly CodeContext _context;
        private readonly object? _function;
        private readonly IEnumerator _enumerator;

        public Filter(CodeContext context, object? function, object? iterable) {
            if (!PythonOps.TryGetEnumerator(context, iterable, out IEnumerator? enumerator)) {
                throw PythonOps.TypeErrorForNotIterable(iterable);
            }

            _context = context;
            _function = function;
            _enumerator = enumerator;
        }

        [PythonHidden]
        public object? Current { get; private set; }

        [PythonHidden]
        public bool MoveNext() {
            while (_enumerator.MoveNext()) {
                object? o = _enumerator.Current;
                object? t = (_function != null) ? PythonCalls.Call(_context, _function, o) : o;

                if (PythonOps.IsTrue(t)) {
                    Current = o;
                    return true;
                }
            }
            Current = default;
            return false;
        }

        [PythonHidden]
        public void Reset() { throw new System.NotSupportedException(); }
    }
}
