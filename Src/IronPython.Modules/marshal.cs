/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

[assembly: PythonModule("marshal", typeof(IronPython.Modules.PythonMarshal))]
namespace IronPython.Modules {
    public static class PythonMarshal {
        public const string __doc__ = "Provides functions for serializing and deserializing primitive data types.";

        #region Public marshal APIs
        public static void dump(object value, PythonFile/*!*/ file) {
            dump(value, file, version);
        }

        public static void dump(object value, PythonFile/*!*/ file, int version) {
            if (file == null) throw PythonOps.TypeError("expected file, found None");

            file.write(dumps(value, version));
        }

        public static object load(PythonFile/*!*/ file) {
            if (file == null) throw PythonOps.TypeError("expected file, found None");

            return MarshalOps.GetObject(FileEnumerator (file));
        }

        public static object dumps(object value) {
            return dumps(value, version);
        }

        public static string dumps(object value, int version) {
            byte[] bytes = MarshalOps.GetBytes(value, version);
            StringBuilder sb = new StringBuilder(bytes.Length);
            for (int i = 0; i < bytes.Length; i++) {
                sb.Append((char)bytes[i]);
            }
            return sb.ToString();
        }

        public static object loads(string @string) {
            return MarshalOps.GetObject(StringEnumerator(@string));
        }

        public const int version = 2;
        #endregion

        #region Implementation details

        private static IEnumerator<byte> FileEnumerator(PythonFile/*!*/ file) {
            for (; ; ) {
                string data = file.read(1);
                if (data.Length == 0) {
                    yield break;
                }

                yield return (byte)data[0];
            }
        }

        private static IEnumerator<byte> StringEnumerator(string/*!*/ str) {
            for (int i = 0; i < str.Length; i++) {
                yield return (byte)str[i];
            }
        }

        #endregion
    }
}
