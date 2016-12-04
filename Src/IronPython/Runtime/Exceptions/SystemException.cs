#if WIN8 || NETSTANDARD

using System;

namespace System {
    [Serializable]
    public class SystemException : Exception {
        public SystemException()
            : base("System exception") {
        }

        public SystemException(string message)
            : base(message) {
        }
        
        public SystemException(string message, Exception innerException)
            : base(message, innerException) {
        }
    }
}

#endif