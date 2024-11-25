// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

using IronPython.Modules;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// .NET exception that is thrown to shutdown the interpreter and exit the system.
    /// </summary>
    [Serializable]
    public sealed class SystemExitException : Exception {
        public SystemExitException() : base() { }
        public SystemExitException(string msg)
            : base(msg) {
        }
        public SystemExitException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        private SystemExitException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
        /// <summary>
        /// Result of sys.exit(n)
        /// </summary>
        /// <param name="otherCode">
        /// null if the script exited using "sys.exit(int_value)"
        /// null if the script exited using "sys.exit(None)"
        /// x    if the script exited using "sys.exit(x)" where isinstance(x, int) == False
        /// </param>
        /// <returns>
        /// int_value if the script exited using "sys.exit(int_value)"
        /// 1 otherwise
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        [PythonHidden]
        public int GetExitCode(out object otherCode) {
            otherCode = null;
            object pyObj = PythonExceptions.ToPython(this);

            object args;
            PythonTuple t;

            if (!PythonOps.TryGetBoundAttr(pyObj, "args", out args) ||
                (t = args as PythonTuple) == null ||
                t.__len__() == 0) {
                return 0;
            } else if (Builtin.isinstance(t[0], TypeCache.Int32)) {
                return Converter.ConvertToInt32(t[0]);
            } else if (Builtin.isinstance(t[0], TypeCache.BigInteger)) {
                var b = Converter.ConvertToBigInteger(t[0]);
                if (b > int.MaxValue || b < int.MinValue) {
                    return -1;
                }
                return (int)b;
            }

            otherCode = t[0];
            return 1;
        }
    }
}
