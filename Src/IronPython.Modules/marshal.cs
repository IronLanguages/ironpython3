// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("marshal", typeof(IronPython.Modules.PythonMarshal))]
namespace IronPython.Modules {
    public static class PythonMarshal {
        public const string __doc__ = "Provides functions for serializing and deserializing primitive data types.";

        #region Public marshal APIs

        public static void dump(CodeContext/*!*/ context, object value, PythonIOModule._IOBase/*!*/ file, int version = PythonMarshal.version) {
            if (file == null) throw PythonOps.TypeError("expected file, found None");

            file.write(context, dumps(value, version));
        }

        public static object load(CodeContext/*!*/ context, PythonIOModule._IOBase/*!*/ file) {
            if (file == null) throw PythonOps.TypeError("expected file, found None");

            return MarshalOps.GetObject(FileEnumerator(context, file));
        }

        public static Bytes dumps(object value, int version = PythonMarshal.version) {
            return Bytes.Make(MarshalOps.GetBytes(value, version));
        }

        public static object loads([BytesLike]IList<byte> bytes) {
            return MarshalOps.GetObject(bytes.GetEnumerator());
        }

        public const int version = 2;

        #endregion

        #region Implementation details

        private static IEnumerator<byte> FileEnumerator(CodeContext/*!*/ context, PythonIOModule._IOBase/*!*/ file) {
            object bytes = file.read(context, 0);
            if (!(bytes is Bytes)) throw PythonOps.TypeError($"file.read() returned not bytes but {Runtime.Types.DynamicHelpers.GetPythonType(bytes).Name}");

            for (; ; ) {
                Bytes data = (Bytes)file.read(context, 1);
                if (data.Count == 0) {
                    yield break;
                }

                yield return (byte)data[0];
            }
        }

        #endregion
    }
}
