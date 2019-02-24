// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

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

        public static Bytes dumps(object value, int version) {
            return Bytes.Make(MarshalOps.GetBytes(value, version));
        }

        public static object loads([BytesConversion]IList<byte> bytes) {
            return MarshalOps.GetObject(bytes.GetEnumerator());
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

        #endregion
    }
}
