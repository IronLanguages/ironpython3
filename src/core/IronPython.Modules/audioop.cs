// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Runtime.CompilerServices;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

[assembly: PythonModule("audioop", typeof(IronPython.Modules.PythonAudioOp))]
namespace IronPython.Modules {
    public static class PythonAudioOp {
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException("audiooperror", dict, "error", "audioop");
        }

        private static PythonType error(CodeContext/*!*/ context) => (PythonType)context.LanguageContext.GetModuleState("audiooperror");

        public static Bytes byteswap(CodeContext/*!*/ context, [NotNone] IBufferProtocol fragment, int width) {
            if (width < 1 || width > 4) {
                throw PythonExceptions.CreateThrowable(error(context), "Size should be 1, 2, 3 or 4");
            }

            using var buffer = fragment.GetBuffer();
            if (buffer.NumBytes() % width != 0) {
                throw PythonExceptions.CreateThrowable(error(context), "not a whole number of frames");
            }

            var array = buffer.ToArray();
            if (width == 2) {
                for (var i = 0; i < array.Length; i += width) {
                    array.ByteSwap(i, i + 1);
                }
            } else if (width == 3) {
                for (var i = 0; i < array.Length; i += width) {
                    array.ByteSwap(i, i + 2);
                }
            } else if (width == 4) {
                for (var i = 0; i < array.Length; i += width) {
                    array.ByteSwap(i, i + 3);
                    array.ByteSwap(i + 1, i + 2);
                }
            }
            return Bytes.Make(array);
        }

        private static void ByteSwap(this byte[] array, int i, int j) {
            var tmp = array[i];
            array[i] = array[j];
            array[j] = tmp;
        }
    }
}
