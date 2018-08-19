// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Dynamic;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;

namespace IronPython.Runtime {
    internal class ThrowingErrorSink : ErrorSink {
        public new static readonly ThrowingErrorSink/*!*/ Default = new ThrowingErrorSink();

        private ThrowingErrorSink() {
        }

        public override void Add(SourceUnit sourceUnit, string message, SourceSpan span, int errorCode, Severity severity) {
            if (severity == Severity.Warning) {
                PythonOps.SyntaxWarning(message, sourceUnit, span, errorCode);
            } else {
                throw PythonOps.SyntaxError(message, sourceUnit, span, errorCode);
            }
        }
    }
}
