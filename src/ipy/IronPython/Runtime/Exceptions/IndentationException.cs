// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;
using System.Security;
using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// .NET exception thrown when a Python syntax error is related to incorrect indentation.
    /// </summary>
    [Serializable]
    internal class IndentationException : SyntaxErrorException {
        public IndentationException(string message) : base(message) { }

        public IndentationException(string message, Exception innerException) : base(message, innerException) { }

        public IndentationException(string message, SourceUnit sourceUnit, SourceSpan span, int errorCode, Severity severity)
            : base(message, sourceUnit, span, errorCode, severity) { }


#if FEATURE_SERIALIZATION
        protected IndentationException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
        }

        [SecurityCritical]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2140:TransparentMethodsMustNotReferenceCriticalCodeFxCopRule")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            ContractUtils.RequiresNotNull(info, nameof(info));

            base.GetObjectData(info, context);
        }
#endif

    }
}
