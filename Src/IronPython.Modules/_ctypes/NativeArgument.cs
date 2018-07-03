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

#if FEATURE_CTYPES

using System;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Types;

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

            public string __repr__(CodeContext context) {
                return String.Format("<cparam '{0}' ({1})>",
                    _type,
                    IdDispenser.GetId(__obj));// TODO: should be a real address
            }

            #endregion
        }
    }
}

#endif
