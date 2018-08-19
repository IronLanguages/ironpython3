// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using IronPython.Runtime.Operations;
using System;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Calculates the method resolution order for a Python class
    /// the rules are:
    ///      If A is a subtype of B, then A has precedence (A > B)
    ///      If C appears before D in the list of bases then C > D
    ///      If E > F in one __mro__ then E > F in all __mro__'s for our subtype
    /// 
    /// class A(object): pass
    /// class B(object): pass
    /// class C(B): pass
    /// class N(A,B,C): pass         # illegal
    ///
    /// This is because:
    ///      C.__mro__ == (C, B, object)
    ///      N.__mro__ == (N, A, B, C, object)
    /// which would conflict, but:
    ///
    /// N(B,A) is ok  (N, B, a, object)
    /// N(C, B, A) is ok (N, C, B, A, object)
    ///
    /// Calculates a C3 MRO as described in "The Python 2.3 Method Resolution Order"
    /// plus support for old-style classes.
    /// 
    /// We build up a list of our base classes MRO's plus our base classes themselves.
    /// We go through the list in order.  Look at the 1st class in the current list, and
    /// if it's not the non-first class in any other list then remove it from all the lists
    /// and append it to the mro.  Otherwise continue to the next list.  If all the classes at
    /// the start are no-good then the MRO is bad and we throw. 
    /// 
    /// For old-style classes if the old-style class is the only one in the list of bases add
    /// it as a depth-first old-style MRO, otherwise compute a new-style mro for all the classes 
    /// and use that.
    /// </summary>
    static class Mro {
        public static List<PythonType> Calculate(PythonType startingType, IList<PythonType> bases) {
            return Calculate(startingType, new List<PythonType>(bases), false);
        }

        /// <summary>
        /// </summary>
        public static List<PythonType> Calculate(PythonType startingType, IList<PythonType> baseTypes, bool forceNewStyle) {
            List<PythonType> bases = new List<PythonType>();
            foreach (PythonType dt in baseTypes) bases.Add(dt);

            if (bases.Contains(startingType)) {
                throw PythonOps.TypeError("a __bases__ item causes an inheritance cycle ({0})", startingType.Name);
            }

            List<PythonType> mro = new List<PythonType>();
            mro.Add(startingType);

            if (bases.Count != 0) {
                List<IList<PythonType>> mroList = new List<IList<PythonType>>();
                // build up the list - it contains the MRO of all our
                // bases as well as the bases themselves in the order in
                // which they appear.

                foreach (PythonType dt in bases) {
                    mroList.Add(TupleToList(dt.ResolutionOrder));
                }

                mroList.Add(TupleToList(bases));

                for (; ; ) {
                    bool removed = false, sawNonZero = false;
                    // now that we have our list, look for good heads
                    PythonType lastHead = null;
                    for (int i = 0; i < mroList.Count; i++) {
                        if (mroList[i].Count == 0) continue;    // we've removed everything from this list.

                        sawNonZero = true;
                        PythonType head = lastHead = mroList[i][0];
                        // see if we're in the tail of any other lists...
                        bool inTail = false;
                        for (int j = 0; j < mroList.Count; j++) {
                            if (mroList[j].Count != 0 && !mroList[j][0].Equals(head) && mroList[j].Contains(head)) {
                                inTail = true;
                                break;
                            }
                        }

                        if (!inTail) {
                            if (mro.Contains(head)) {
                                throw PythonOps.TypeError("a __bases__ item causes an inheritance cycle");
                            }
                            // add it to the linearization, and remove
                            // it from our lists
                            mro.Add(head);

                            for (int j = 0; j < mroList.Count; j++) {
                                mroList[j].Remove(head);
                            }
                            removed = true;
                            break;
                        }
                    }

                    if (!sawNonZero) break;

                    if (!removed) {
                        // we've iterated through the list once w/o removing anything
                        PythonType other = null;
                        string error = String.Format("Cannot create a consistent method resolution\norder (MRO) for bases {0}", lastHead.Name);

                        for (int i = 0; i < mroList.Count; i++) {
                            if (mroList[i].Count != 0 && !mroList[i][0].Equals(lastHead)) {
                                other = mroList[i][0];
                                error += ", ";
                                error += other.Name;
                            }
                        }
                        throw PythonOps.TypeError(error);
                    }
                }
            }

            return mro;
        }

        private static IList<PythonType> TupleToList(IList<PythonType> t) {
            return new List<PythonType>(t);
        }
    }
}
