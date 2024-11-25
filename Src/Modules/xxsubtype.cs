// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;

[assembly: PythonModule("xxsubtype", typeof(IronPython.Modules.xxsubtype))]
namespace IronPython.Modules {
    /// <summary>
    /// Samples on how to subtype built-in types from C#
    /// </summary>
    public static class xxsubtype {
        public const string __doc__ = "Provides samples on how to subtype built-in types from .NET.";
        [PythonType]
        public class spamlist : PythonList {
            public spamlist()
                : base() {
            }

            public spamlist(CodeContext context, object sequence)
                : base(context, sequence) {
            }

            private int _state;

            /// <summary>
            /// an int variable for demonstration purposes
            /// </summary>
            public int state {
                get {
                    return _state;
                }
                set {
                    _state = value;
                }
            }

            public int getstate() {
                return state;
            }

            public void setstate(int value) {
                state = value;
            }

            public static object staticmeth([ParamDictionary] IDictionary<object, object> dict, params object[] args) {
                return PythonTuple.MakeTuple(null, PythonTuple.MakeTuple(args), dict);
            }

            [ClassMethod]
            public static object classmeth(PythonType cls, [ParamDictionary] IDictionary<object, object> dict, params object[] args) {
                return PythonTuple.MakeTuple(cls, PythonTuple.MakeTuple(args), dict);
            }
        }

        [PythonType]
        public class spamdict : PythonDictionary {
            private int _state;

            /// <summary>
            /// an int variable for demonstration purposes
            /// </summary>
            public int state {
                get {
                    return _state;
                }
                set {
                    _state = value;
                }
            }

            public int getstate() {
                return state;
            }

            public void setstate(int value) {
                state = value;
            }
        }
        public static double bench(CodeContext context, object x, string name) {
            double start = PythonTime.clock();

            for (int i = 0; i < 1001; i++) {
                PythonOps.GetBoundAttr(context, x, name);
            }

            return PythonTime.clock() - start;
        }
    }
}
