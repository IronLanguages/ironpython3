﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

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
        [PythonHidden]
        public int GetExitCode(out object? otherCode) {
            otherCode = null;
            var pyObj = PythonExceptions.ToPython(this);

            if (PythonOps.TryGetBoundAttr(pyObj, "code", out object? code)) {
                if (code is null) {
                    return 0;
                } else if (Builtin.isinstance(code, TypeCache.Int32)) {
                    return Converter.ConvertToInt32(code);
                } else if (Builtin.isinstance(code, TypeCache.BigInteger)) {
                    var b = Converter.ConvertToBigInteger(code);
                    if (b > int.MaxValue || b < int.MinValue) {
                        return -1;
                    }
                    return (int)b;
                } else {
                    otherCode = code;
                }
            }
            return 1;
        }
    }
}
