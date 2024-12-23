// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using IronPython.Runtime;
using IronPython.Runtime.Types;
using IronPython.Runtime.Operations;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {        
        // returned from from_param, byref, seemingly called "cparam", but type() says CArgObject in CPython
        [PythonType, PythonHidden]
        public sealed class NativeArgument : ICodeFormattable {
            private readonly CData __obj;
            private readonly string _type;

            internal NativeArgument(CData value, string type) {
                __obj = value;
                _type = type;
            }

            public CData _obj {
                get {
                    return __obj;
                }
            }

            #region ICodeFormattable Members

            public string __repr__(CodeContext context)
                => $"<cparam '{_type}' ({PythonOps.Repr(context, __obj)})>";

            #endregion
        }
    }
}

#endif
