/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

[assembly: PythonModule("_random", typeof(IronPython.Modules.PythonRandom))]
namespace IronPython.Modules {
    public static class PythonRandom {
        public const string __doc__ = "implements a random number generator";

        [PythonType]
        public class Random {
            private System.Random _rnd;

            public Random() {
                seed();
            }

            public Random(object seed) {
                this.seed(seed);
            }

            #region Public API surface

            public object getrandbits(int bits) {
                if (bits <= 0) {
                    throw PythonOps.ValueError("number of bits must be greater than zero");
                }

                lock (this) {
                    return _rnd.GetRandBits(bits);
                }
            }

            public object getstate() {
                return _rnd;
            }

            public void jumpahead(int count) {
                lock (this) {
                    _rnd.NextBytes(new byte[4096]);
                }
            }

            public void jumpahead(double count) {
                throw PythonOps.TypeError("jumpahead requires an integer, not 'float'");
            }

            public object random() {
                lock (this) {
                    return _rnd.NextDouble();
                }
            }

            public void seed() {
                seed(DateTime.Now);
            }

            public void seed(object s) {
                if (s == null) {
                    seed();
                    return;
                }

                int newSeed;
                if (s is int) {
                    newSeed = (int)s;
                } else {
                    newSeed = s.GetHashCode();
                }

                lock (this) {
                    _rnd = new System.Random(newSeed);
                }
            }

            public void setstate(object state) {
                System.Random random = state as System.Random;

                lock (this) {
                    if (random != null) {
                        _rnd = random;
                    } else {
                        throw IronPython.Runtime.Operations.PythonOps.TypeError("setstate: argument must be value returned from getstate()");
                    }
                }
            }

            #endregion
        }
    }
}
