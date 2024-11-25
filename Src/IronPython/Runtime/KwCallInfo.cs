// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Runtime {
    public sealed class KwCallInfo {
        private readonly object[] _args;
        private readonly string[] _names;

        public KwCallInfo(object[] args, string[] names) {
            _args = args;
            _names = names;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")] // TODO: fix
        public object[] Arguments {
            get {
                return _args;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")] // TODO: fix
        public string[] Names {
            get {
                return _names;
            }
        }
    }
}
