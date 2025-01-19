// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 license.
// See the LICENSE file in the project root for more information.

#if !FEATURE_SERIALIZATION

using System;
using System.Diagnostics;

namespace IronPython {
    [Conditional("STUB")]
    internal class SerializableAttribute : Attribute {
    }

    [Conditional("STUB")]
    internal class NonSerializedAttribute : Attribute {
    }

    namespace Runtime {
        internal interface ISerializable {
        }

        internal interface IDeserializationCallback {
        }
    }

    internal class SerializationException : Exception {
    }
}

#endif
