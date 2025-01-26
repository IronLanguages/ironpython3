// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Wrapper class used when a user defined type (new-style or old-style)
    /// defines __index__.  We provide a conversion from all user defined
    /// types to the Index type so they can be used for determing and method bind
    /// time the most appropriate method to dispatch to.
    /// </summary>
    public class Index {
        public Index(object/*!*/ value) {
            ContractUtils.RequiresNotNull(value, nameof(value));

            Value = value;
        }

        internal object/*!*/ Value { get; }
    }
}
