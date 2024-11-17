// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {

    [PythonType("zip")]
    [Documentation(@"zip(iter1 [,iter2 [...]]) --> zip object

Return a zip object whose .__next__() method returns a tuple where
the i-th element comes from the i-th iterable argument.  The .__next__()
method continues until the shortest iterable in the argument sequence
is exhausted and then it raises StopIteration.")]
    public class Zip : IEnumerator {
        private readonly IEnumerator[] enumerators;
        private object? current;

        public Zip(CodeContext context, [NotNone] params object[] iters) {
            if (iters == null) throw PythonOps.TypeError("zip argument #{0} must support iteration", 1);

            enumerators = new IEnumerator[iters.Length];
            for (var i = 0; i < iters.Length; i++) {
                if (PythonOps.TryGetEnumerator(context, iters[i], out IEnumerator? enumerator))
                    enumerators[i] = enumerator;
                else
                    throw PythonOps.TypeError("zip argument #{0} must support iteration", i + 1);
            }
        }

        [PythonHidden]
        public object Current => current ?? throw new InvalidOperationException();

        [PythonHidden]
        public bool MoveNext() {
            if (enumerators.Length > 0 && enumerators[0].MoveNext()) {
                var res = new object?[enumerators.Length];
                res[0] = enumerators[0].Current;

                for (var i = 1; i < enumerators.Length; i++) {
                    var enumerator = enumerators[i];
                    if (!enumerator.MoveNext()) {
                        current = null;
                        return false;
                    }

                    res[i] = enumerator.Current;
                }

                current = PythonTuple.MakeTuple(res);
                return true;
            }
            current = null;
            return false;
        }

        [PythonHidden]
        public void Reset() { throw new NotSupportedException(); }

        public PythonTuple __reduce__() {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(Zip)),
                PythonTuple.Make(enumerators)
            );
        }
    }
}
