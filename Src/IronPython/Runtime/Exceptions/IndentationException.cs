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
using System.Security;
using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// .NET exception thrown when a Python syntax error is related to incorrect indentation.
    /// </summary>
    [Serializable]
    class IndentationException : SyntaxErrorException {
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
            ContractUtils.RequiresNotNull(info, "info");

            base.GetObjectData(info, context);
        }
#endif

    }
}
