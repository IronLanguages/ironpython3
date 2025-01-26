// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        /// <summary>
        /// The enum used for tracking the various ctypes primitive types.
        /// </summary>
        internal enum SimpleTypeKind {
            /// <summary> 'c' </summary>
            Char,
            /// <summary> 'b' </summary>
            SignedByte,
            /// <summary> 'B' </summary>
            UnsignedByte,
            /// <summary> 'h' </summary>
            SignedShort,
            /// <summary> 'H' </summary>
            UnsignedShort,
            /// <summary> 'i' </summary>
            SignedInt,
            /// <summary> 'I' </summary>
            UnsignedInt,
            /// <summary> 'l' </summary>
            SignedLong,
            /// <summary> 'L' </summary>
            UnsignedLong,
            /// <summary> 'f' </summary>
            Single,
            /// <summary> 'd', 'g' </summary>
            Double,
            /// <summary> 'q' </summary>
            SignedLongLong,
            /// <summary> 'Q' </summary>
            UnsignedLongLong,
            /// <summary> 'O' </summary>
            Object,
            /// <summary> 'P' </summary>
            Pointer,
            /// <summary> 'z' </summary>
            CharPointer,
            /// <summary> 'Z' </summary>
            WCharPointer,
            /// <summary> 'u' </summary>
            WChar,
            /// <summary> '?' </summary>
            Boolean,
            /// <summary> 'v' </summary>
            VariantBool,
            /// <summary> 'X' </summary>
            BStr
        }
    }
}
#endif