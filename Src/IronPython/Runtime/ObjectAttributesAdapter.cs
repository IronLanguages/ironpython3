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

using System.Collections.Generic;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    class ObjectAttributesAdapter  : DictionaryStorage {
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

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            _context.LanguageContext.SetIndex(_backing, key, value);
        }

        public override bool Contains(object key) {
            object dummy;
            return TryGetValue(key, out dummy);
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            try {
                _context.LanguageContext.DelIndex(_backing, key);
                return true;
            } catch (KeyNotFoundException) {
                return false;
            }
        }

        public override bool TryGetValue(object key, out object value) {
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

        public override List<KeyValuePair<object, object>> GetItems() {
            List<KeyValuePair<object, object>> res = new List<KeyValuePair<object, object>>();
            foreach (object o in Keys) {
                object val;
                TryGetValue(o, out val);

                res.Add(new KeyValuePair<object, object>(o, val));            
            }
            return res;
        }

        private ICollection<object> Keys {
            get { return (ICollection<object>)Converter.Convert(PythonOps.Invoke(_context, _backing, "keys"), typeof(ICollection<object>)); }
        }
    }
}
