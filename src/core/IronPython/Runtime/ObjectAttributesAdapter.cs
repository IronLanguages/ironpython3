// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    internal class ObjectAttributesAdapter  : DictionaryStorage {
        private readonly object _backing;
        private readonly CodeContext/*!*/ _context;

        public ObjectAttributesAdapter(CodeContext/*!*/ context, object backing) {
            _backing = backing;
            _context = context;
        }

        internal object Backing {
            get {
                return _backing;
            }
        }

        public override void Add(ref DictionaryStorage storage, object? key, object? value) {
            _context.LanguageContext.SetIndex(_backing, key, value);
        }

        public override bool Contains(object? key) {
            return TryGetValue(key, out _);
        }

        public override bool Remove(ref DictionaryStorage storage, object? key) {
            try {
                _context.LanguageContext.DelIndex(_backing, key);
                return true;
            } catch (KeyNotFoundException) {
                return false;
            }
        }

        public override DictionaryStorage AsMutable(ref DictionaryStorage storage) => this;

        public override bool TryGetValue(object? key, out object? value) {
            try {
                value = PythonOps.GetIndex(_context, _backing, key);
                return true;
            } catch (KeyNotFoundException) {
                // return false
            }
            value = null;
            return false;
        }

        public override int Count {
            get { return PythonOps.Length(_backing);  }
        }

        public override void Clear(ref DictionaryStorage storage) {
            PythonOps.Invoke(_context, _backing, "clear");
        }

        public override List<KeyValuePair<object?, object?>> GetItems() {
            var res = new List<KeyValuePair<object?, object?>>();
            IEnumerator keys = KeysEnumerator;
            while (keys.MoveNext()) {
                object? key = keys.Current;
                TryGetValue(key, out object? val);

                res.Add(new KeyValuePair<object?, object?>(key, val));
            }
            return res;
        }

        private IEnumerator KeysEnumerator
            => PythonOps.GetEnumerator(_context, PythonOps.Invoke(_context, _backing, "keys"));
    }
}
