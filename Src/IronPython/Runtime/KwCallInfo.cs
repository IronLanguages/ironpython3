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
