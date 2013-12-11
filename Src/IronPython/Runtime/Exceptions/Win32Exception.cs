/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/
#if !FEATURE_WIN32EXCEPTION

using System;

namespace IronPython.Runtime.Exceptions {
    // exits for better compatibility w/ Silverlight where this exception isn't available.

    [Serializable]
    public class Win32Exception : Exception {
        public Win32Exception() : base() { }
        public Win32Exception(string msg) : base(msg) { }
        public Win32Exception(string message, Exception innerException)
            : base(message, innerException) {
        }

        public int ErrorCode {
            get {
                return 0;
            }
        }

        public int NativeErrorCode {
            get {
                return 0;
            }
        }
    }
}

#endif