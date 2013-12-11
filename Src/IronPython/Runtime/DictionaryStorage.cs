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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Scripting;

namespace IronPython.Runtime {
    /// <summary>
    /// Abstract base class for all PythonDictionary storage.
    /// 
    /// Defined as a class instead of an interface for performance reasons.  Also not
    /// using IDictionary* for keeping a simple interface.
    /// 
    /// Full locking is defined as being on the DictionaryStorage object it's self,
    /// not an internal member.  This enables subclasses to provide their own locking
    /// aruond large operations and call lock free functions.
    /// </summary>
    [Serializable]
    internal abstract class DictionaryStorage  {
        public abstract void Add(ref DictionaryStorage storage, object key, object value);

        public virtual void AddNoLock(ref DictionaryStorage storage, object key, object value) {
            Add(ref storage, key, value);
        }

        public abstract bool Remove(ref DictionaryStorage storage, object key);

        public virtual bool TryRemoveValue(ref DictionaryStorage storage, object key, out object value) {
            if (TryGetValue(key, out value)) {
                return Remove(ref storage, key);

            }

            return false;
        }
        public abstract void Clear(ref DictionaryStorage storage);

        /// <summary>
        /// Adds items from this dictionary into the other dictionary
        /// </summary>
        public virtual void CopyTo(ref DictionaryStorage/*!*/ into) {
            Debug.Assert(into != null);

            foreach (KeyValuePair<object, object> kvp in GetItems()) {
                into.Add(ref into, kvp.Key, kvp.Value);
            }
        }

        public abstract bool Contains(object key);
        
        public abstract bool TryGetValue(object key, out object value);

        public abstract int Count { get; }
        public virtual bool HasNonStringAttributes() {
            foreach (KeyValuePair<object, object> o in GetItems()) {
                if (!(o.Key is string)) {
                    return true;
                }
            }
            return false;
        }

        public abstract List<KeyValuePair<object, object>> GetItems();

        public virtual IEnumerable<object>/*!*/ GetKeys() {
            foreach (var o in GetItems()) {
                yield return o.Key;
            }
        }

        public virtual DictionaryStorage Clone() {
            CommonDictionaryStorage storage = new CommonDictionaryStorage();
            foreach (KeyValuePair<object, object> kvp in GetItems()) {
                storage.Add(kvp.Key, kvp.Value);
            }
            return storage;
        }

        public virtual void EnsureCapacityNoLock(int size) {
        }

        public virtual IEnumerator<KeyValuePair<object, object>> GetEnumerator() {
            return GetItems().GetEnumerator();
        }
        
        /// <summary>
        /// Provides fast access to the __path__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetPath(out object value) {
            return TryGetValue("__path__", out value);
        }

        /// <summary>
        /// Provides fast access to the __package__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetPackage(out object value) {
            return TryGetValue("__package__", out value);
        }

        /// <summary>
        /// Provides fast access to the __builtins__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetBuiltins(out object value) {
            return TryGetValue("__builtins__", out value);
        }

        /// <summary>
        /// Provides fast access to the __name__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetName(out object value) {
            return TryGetValue("__name__", out value);
        }

        /// <summary>
        /// Provides fast access to the __import__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetImport(out object value) {
            return TryGetValue("__import__", out value);
        }
    }

}
