// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Linq;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {

    [PythonType("map")]
    [Documentation(@"map(func, *iterables) -> map object

Make an iterator that computes the function using arguments from
each of the iterables.  Stops when the shortest iterable is exhausted.")]
    public class Map : IEnumerator {
        private readonly CodeContext _context;
        private readonly object? _func;
        private readonly IEnumerator[] _enumerators;

        public Map(CodeContext context, object? func, params object[] iterables) {
            if (iterables.Length == 0) {
                throw PythonOps.TypeError("map() must have at least two arguments.");
            }

            _enumerators = new IEnumerator[iterables.Length];
            for (var i = 0; i < iterables.Length; i++) {
                var iter = iterables[i];
                if (!PythonOps.TryGetEnumerator(context, iter, out var enumerator)) {
                    throw PythonOps.TypeErrorForNotIterable(iter);
                }
                _enumerators[i] = enumerator;
            }

            _context = context;
            _func = func;
        }

        [PythonHidden]
        public object? Current { get; private set; }

        [PythonHidden]
        public bool MoveNext() {
            if (_enumerators.Length > 0 && _enumerators.All(x => x.MoveNext())) {
                Current = PythonOps.CallWithContext(_context, _func, _enumerators.Select(x => x.Current).ToArray());
                return true;
            }
            Current = default;
            return false;
        }

        [PythonHidden]
        public void Reset() { throw new NotSupportedException(); }

        public PythonTuple __reduce__() {
            var args = new object?[_enumerators.Length + 1];
            args[0] = _func;
            Array.Copy(_enumerators, 0, args, 1, _enumerators.Length);
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(Map)),
                PythonTuple.MakeTuple(args)
            );
        }
    }
}
