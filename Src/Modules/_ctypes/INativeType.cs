// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        /// <summary>
        /// Common functionality that all of the meta classes provide which is part of
        /// our implementation.  This is used to implement the serialization/deserialization
        /// of values into/out of memory, emit the marshalling logic for call stubs, and
        /// provide common information (size/alignment) for the types.
        /// </summary>
        internal interface INativeType {
            /// <summary>
            /// Gets the native size of the type
            /// </summary>
            int Size {
                get;
            }

            /// <summary>
            /// Gets the required alignment for the type
            /// </summary>
            int Alignment {
                get;
            }

            /// <summary>
            /// Deserialized the value of this type from the given address at the given
            /// offset.  Any new objects which are created will keep the provided 
            /// MemoryHolder alive.
            /// 
            /// raw determines if the cdata is returned or if the primitive value is
            /// returned.  This is only applicable for subtypes of simple cdata types.
            /// </summary>
            object GetValue(MemoryHolder/*!*/ owner, object readingFrom, int offset, bool raw);

            /// <summary>
            /// Serializes the provided value into the specified address at the given
            /// offset.
            /// </summary>
            object SetValue(MemoryHolder/*!*/ address, int offset, object value);

            /// <summary>
            /// Gets the .NET type which is used when calling or returning the value
            /// from native code.
            /// </summary>
            Type/*!*/ GetNativeType();

            /// <summary>
            /// Gets the .NET type which the native type is converted into when going to Python
            /// code.  This is usually int, BigInt, double, object, or a CData type.
            /// </summary>
            Type/*!*/ GetPythonType();

            /// <summary>
            /// Emits marshalling of an object from Python to native code.  This produces the
            /// native type from the Python type.
            /// </summary>
            MarshalCleanup EmitMarshalling(ILGenerator/*!*/ method, LocalOrArg/*!*/ argIndex, List<object>/*!*/ constantPool, int constantPoolArgument);

            /// <summary>
            /// Emits marshalling from native code to Python code This produces the python type 
            /// from the native type.  This is used for return values and parameters 
            /// to Python callable objects that are passed back out to native code.
            /// </summary>
            void EmitReverseMarshalling(ILGenerator/*!*/ method, LocalOrArg/*!*/ value, List<object>/*!*/ constantPool, int constantPoolArgument);

            /// <summary>
            /// Returns a string which describes the type.  Used for _buffer_info implementation which
            /// only exists for testing purposes.
            /// </summary>
            string TypeFormat {
                get;
            }
        }
    }
}
#endif