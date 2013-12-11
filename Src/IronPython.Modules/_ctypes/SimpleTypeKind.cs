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

#if FEATURE_NATIVE

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