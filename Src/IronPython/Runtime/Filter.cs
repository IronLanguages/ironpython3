// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    [PythonType("filter")]
    [Documentation(@"filter(function or None, iterable) -> filter object

Return an iterator yielding those items of iterable for which function(item)
is true. If function is None, return the items that are true.")]
    public class Filter : IEnumerable {
        private readonly CodeContext _context;
        private readonly object _function;
        private readonly object _iterable;

        public Filter(CodeContext context, object function, object iterable) {
            _context = context;
            _function = function;
            _iterable = iterable;

            if (!PythonOps.TryGetEnumerator(context, iterable, out _)) {
                throw PythonOps.TypeErrorForNotIterable(iterable);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            if (_function != null && !PythonOps.IsCallable(_context, _function)) {
                throw PythonOps.UncallableError(_function);
            }

            IEnumerator e = PythonOps.GetEnumerator(_context, _iterable);
            while (e.MoveNext()) {
                object o = e.Current;
                object t = (_function != null) ? PythonCalls.Call(_context, _function, o) : o;

                if (PythonOps.IsTrue(t)) {
                    yield return o;
                }
            }
        }
    }
}
