// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    internal class RuntimeVariablesDictionaryStorage : CustomDictionaryStorage {
        private readonly MutableTuple _boxes;
        private readonly string[] _args;
        private readonly int _numFreeVars;
        private readonly int _arg0Idx;

        public RuntimeVariablesDictionaryStorage(MutableTuple boxes, string[] args, int numFreeVars, int arg0Idx) {
            Debug.Assert(0 <= numFreeVars && numFreeVars <= args.Length);
            Debug.Assert(arg0Idx == -1 || numFreeVars <= arg0Idx && arg0Idx < args.Length);

            _boxes = boxes;
            _args = args;
            _numFreeVars = numFreeVars;
            _arg0Idx = arg0Idx;
        }

        /// <summary>
        /// Closure tuple.
        /// </summary>
        internal MutableTuple Tuple => _boxes;

        /// <summary>
        /// Names of the variables in the closure.
        /// Cell variables (not accessed in current scope) have null names.
        /// </summary>
        internal string[] Names => _args;

        /// <summary>
        /// Number of free variables in the closure.
        /// If non-zero, cells for free variables are at the beginning of <see cref="Tuple"/>.
        /// </summary>
        internal int NumFreeVars => _numFreeVars;

        /// <summary>
        /// Index of the cell of the first positional parameter of the function call.
        /// Value -1 means that information is not available.
        /// </summary>
        /// <remarks>
        /// This information is intended to be consumed by a parameterless super() call.
        /// For performance reasons, Arg0Idx is only tracked when deemed necessary to support super().
        /// </remarks>
        internal int Arg0Idx => _arg0Idx;

        protected override IEnumerable<KeyValuePair<string, object>> GetExtraItems() {
            for (int i = 0; i < _args.Length; i++) {
                if (GetCell(i).Value != Uninitialized.Instance && _args[i] != null) {
                    yield return new KeyValuePair<string, object>(_args[i], GetCell(i).Value);
                }
            }
        }

        protected override bool TrySetExtraValue(string key, object value) {
            for (int i = 0; i < _args.Length; i++) {
                if (_args[i] == key) {
                    var cell = GetCell(i);

                    cell.Value = value;
                    return true;
                }
            }
            return false;
        }

        protected override bool TryGetExtraValue(string key, out object value) {
            for (int i = 0; i < _args.Length; i++) {
                if (_args[i] == key) {
                    value = GetCell(i).Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        protected override bool? TryRemoveExtraValue(string key) {
            for (int i = 0; i < _args.Length; i++) {
                if (_args[i] == key) {
                    var cell = GetCell(i);

                    if (cell.Value != Uninitialized.Instance) {
                        cell.Value = Uninitialized.Instance;
                        return true;
                    }
                    return false;
                }
            }
            return null;
        }

        internal ClosureCell GetCell(int i) {
            return ((ClosureCell)_boxes.GetNestedValue(_args.Length, i));
        }
    }
}
