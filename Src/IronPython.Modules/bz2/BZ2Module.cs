// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright 2012 Jeff Hardy
//

using System.Collections.Generic;
using System.Linq;

using IronPython.Runtime;

[assembly: PythonModule("_bz2", typeof(IronPython.Modules.Bz2.Bz2Module))]

namespace IronPython.Modules.Bz2 {
    public static partial class Bz2Module {
        internal const int DEFAULT_COMPRESSLEVEL = 9;

        /// <summary>
        /// Try to convert IList(Of byte) to byte[] without copying, if possible.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static byte[] ToArrayNoCopy(this IList<byte> bytes) {
            byte[] bytesA = bytes as byte[];
            if (bytesA != null) {
                return bytesA;
            }

            Bytes bytesP = bytes as Bytes;
            if (bytesP != null) {
                return bytesP.UnsafeByteArray;
            }

            return bytes.ToArray();
        }
    }
}
