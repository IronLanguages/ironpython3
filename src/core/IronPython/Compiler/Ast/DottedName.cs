// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Scripting;
using System.Text;

namespace IronPython.Compiler.Ast {
    public class DottedName : Node {
        private readonly string[] _names;

        public DottedName(string[] names) {
            _names = names;
        }

        public IList<string> Names => _names;

        public string MakeString() {
            if (_names.Length == 0) return string.Empty;

            StringBuilder ret = new StringBuilder(_names[0]);
            for (int i = 1; i < _names.Length; i++) {
                ret.Append('.');
                ret.Append(_names[i]);
            }
            return ret.ToString();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                ;
            }
            walker.PostWalk(this);
        }
    }
}
