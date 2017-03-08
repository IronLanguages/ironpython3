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

#if FEATURE_SERIALIZATION
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
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
#if FEATURE_SERIALIZATION
                MemoryStream stream = new MemoryStream();
                IFormatter formatter = new BinaryFormatter();
                lock (this) {
                    formatter.Serialize(stream, _rnd);
                }
                return PythonTuple.MakeTuple(stream.GetBuffer().Select(x => (int)x).Cast<object>().ToArray());
#else
                return _rnd;
#endif
            }

            public object random() {
                lock (this) {
                    byte[] randA = new byte[sizeof(uint)];
                    byte[] randB = new byte[sizeof(uint)];
                    _rnd.NextBytes(randA);
                    _rnd.NextBytes(randB);

                    // this is pulled from _randommodule.c from CPython
                    uint a = BitConverter.ToUInt32(randA, 0) >> 5;
                    uint b = BitConverter.ToUInt32(randB, 0) >> 6;
                    return (a*67108864.0+b)*(1.0/9007199254740992.0);
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
                    if (!PythonContext.IsHashable(s)) {
                        throw PythonOps.TypeError("unhashable type: '{0}'", PythonOps.GetPythonTypeName(s));
                    }
                    newSeed = s.GetHashCode();
                }

                lock (this) {
                    _rnd = new System.Random(newSeed);
                }
            }

            public void setstate(object state) {
#if FEATURE_SERIALIZATION
                PythonTuple s = state as PythonTuple;
                if(s == null) {
                    throw PythonOps.TypeError("state vector must be a tuple");
                }

                try {
                    object[] arr = s.ToArray();
                    byte[] b = new byte[arr.Length];
                    for (int i = 0; i < arr.Length; i++) {
                        if (arr[i] is int) {
                            b[i] = (byte)(int)(arr[i]);
                        } else {
                            throw PythonOps.TypeError("state vector of unexpected type: {0}", PythonOps.GetPythonTypeName(arr[i]));
                        }
                    }
                    MemoryStream stream = new MemoryStream(b);
                    IFormatter formatter = new BinaryFormatter();
                    lock (this) {
                        _rnd = (System.Random)formatter.Deserialize(stream);
                    }
                } catch (SerializationException ex) {
                    throw PythonOps.SystemError("state vector invalid: {0}", ex.Message);
                }
#else
                System.Random random = state as System.Random;

                lock (this) {
                    if (random != null) {
                        _rnd = random;
                    } else {
                        throw IronPython.Runtime.Operations.PythonOps.TypeError("setstate: argument must be value returned from getstate()");
                    }
                }
#endif
}

            #endregion
        }
    }
}
