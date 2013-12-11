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
using System.Collections.Generic;

using Microsoft.Scripting;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("xxsubtype", typeof(IronPython.Modules.xxsubtype))]
namespace IronPython.Modules {    
    /// <summary>
    /// Samples on how to subtype built-in types from C#
    /// </summary>
    public static class xxsubtype {
        public const string __doc__ = "Provides samples on how to subtype built-in types from .NET.";
        [PythonType]
        public class spamlist : List {
            public spamlist()
                : base() {
            }

            public spamlist(object sequence)
                : base(sequence) {
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

            public static object staticmeth([ParamDictionary]IDictionary<object, object> dict, params object[] args) {
                return PythonTuple.MakeTuple(null, PythonTuple.MakeTuple(args), dict);
            }

            [ClassMethod]
            public static object classmeth(PythonType cls, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
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
