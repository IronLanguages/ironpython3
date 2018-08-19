// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Exceptions {
    [Serializable]
    sealed class ObjectException : Exception, IPythonException {
        private object _instance;
        private PythonType _type;

        public ObjectException(PythonType type, object instance) {
            _instance = instance;
            _type = type;
        }

        public ObjectException(string msg) : base(msg) { }
        public ObjectException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        private ObjectException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif

        public object Instance {
            get {
                return _instance;
            }
        }

        public PythonType Type {
            get {
                return _type;
            }
        }

        #region IPythonException Members

        public object ToPythonException() {
            return this;
        }

        #endregion
    }
}
