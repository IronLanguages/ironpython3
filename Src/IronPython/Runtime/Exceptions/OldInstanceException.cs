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

using System;
using System.Runtime.Serialization;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Exceptions {
    [Serializable]
    class OldInstanceException : Exception, IPythonException {
        private OldInstance _instance;

        public OldInstanceException(OldInstance instance) {
            _instance = instance;
        }
        public OldInstanceException(string msg) : base(msg) { }
        public OldInstanceException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        protected OldInstanceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif

        public OldInstance Instance {
            get {
                return _instance;
            }
        }

        #region IPythonException Members

        public object ToPythonException() {
            return _instance;
        }

        #endregion
    }
}
