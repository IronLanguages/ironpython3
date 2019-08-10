// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class Python3WarningAttribute : Attribute  {
        private readonly string/*!*/ _message;
        
        public Python3WarningAttribute(string/*!*/ message) {
            ContractUtils.RequiresNotNull(message, nameof(message));

            _message = message;
        }

        public string/*!*/ Message {
            get {
                return _message;
            }
        }
    }
}
