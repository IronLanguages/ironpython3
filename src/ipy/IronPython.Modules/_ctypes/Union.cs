// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using IronPython.Runtime;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {        
        [PythonType("Union")]
        public abstract class _Union : CData {
            public void __init__(CodeContext/*!*/ context) {
                MemHolder = new MemoryHolder(Size);
            }
        }
    }
}

#endif
