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
using System.Dynamic;
using Microsoft.Scripting;

namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// .NET Exception thrown when a Python syntax error is related to incorrect tabs.
    /// </summary>
    [Serializable]
    sealed class TabException : IndentationException {
        public TabException(string message) : base(message) { }

        public TabException(string message, Exception innerException) : base(message, innerException) { }

        public TabException(string message, SourceUnit sourceUnit, SourceSpan span, int errorCode, Severity severity)
            : base(message, sourceUnit, span, errorCode, severity) { }
    }
}
