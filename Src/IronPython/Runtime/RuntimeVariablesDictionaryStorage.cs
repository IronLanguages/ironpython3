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
        private readonly int _numPosArgs;

        public RuntimeVariablesDictionaryStorage(MutableTuple boxes, string[] args, int numFreevars, int numPosArgs) {
            Debug.Assert(numFreevars >= 0 && numPosArgs >= 0);
            Debug.Assert(numFreevars + numPosArgs <= args.Length);

            _boxes = boxes;
            _args = args;
            _numFreeVars = numFreevars;
            _numPosArgs = numPosArgs;
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
        /// Number of positional parameter variables of the lambda.
        /// Thus no keyword-only, *args, nor **kwargs.
        /// If non-zero, cells for parameters are after the free variables in <see cref="Tuple"/>.
        /// </summary>
        /// <remarks>
        /// A zero value does not mean that the function doesn't have any positional arguments,
        /// or that none of its argumets are lifted to a closure cell.
        /// For performance reasons, NumPosArgs is only tracked when deemed necessary;
        /// to ensure it is tracked in all cases, run IronPython with option FullFrames.
        /// </remarks>
        internal int NumPosArgs => _numPosArgs;

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
