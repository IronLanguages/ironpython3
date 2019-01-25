// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Linq;

using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    [PythonType("map")]
    [Documentation(@"map(func, *iterables) -> map object

Make an iterator that computes the function using arguments from
each of the iterables.  Stops when the shortest iterable is exhausted.")]
    public class Map : IEnumerable {
        private CodeContext _context;
        private object _func;
        private object[] _iters;

        public Map(CodeContext context, object func,  params object[] iters){
            _context = context;
            _func = func;
            _iters = iters;

            if(iters.Length == 0) {
                throw PythonOps.TypeError("map() must have at least two arguments.");
            }

            if(!PythonOps.IsCallable(context, func)) {
                throw PythonOps.TypeError("'{0}' object is not callable", PythonOps.GetPythonTypeName(func));
            }

            foreach(object o in iters) {
                IEnumerator e;
                if(!PythonOps.TryGetEnumerator(context, o, out e)) {
                    throw PythonOps.TypeErrorForNotIterable(o);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            IEnumerator[] enumerators = _iters.Select(x => PythonOps.GetEnumerator(x)).ToArray();
            while (enumerators.All(x => x.MoveNext())) {
                yield return PythonOps.CallWithContext(_context, _func, enumerators.Select(x => x.Current).ToArray());
            }
        }
    }
}
