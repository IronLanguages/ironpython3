// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

internal static partial class Interop {
    internal static partial class Ucrtbase {
        [DllImport(Libraries.Ucrtbase, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        [SupportedOSPlatform("windows")]
        private static extern int strerror_s([Out] StringBuilder buffer, nuint sizeInBytes, int errnum);

        [SupportedOSPlatform("windows")]
        internal static int strerror(int errnum, StringBuilder buf) {
            return strerror_s(buf, (nuint)buf.Capacity + 1, errnum);
        }
    }
}
