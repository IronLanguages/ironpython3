﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    /// <summary>
    /// Abstract base class for all PythonDictionary storage.
    /// </summary>
    /// <remarks>
    /// Defined as a class instead of an interface for performance reasons.  Also not
    /// using IDictionary* for keeping a simple interface.
    /// 
    /// Full locking is defined as being on the DictionaryStorage object itself,
    /// not an internal member.  This enables subclasses to provide their own locking
    /// around large operations and call lock free functions.
    /// </remarks>
    [Serializable]
    internal abstract class DictionaryStorage  {
        public abstract void Add(ref DictionaryStorage storage, object? key, object? value);

        public virtual void AddNoLock(ref DictionaryStorage storage, object? key, object? value) {
            Add(ref storage, key, value);
        }

        public abstract bool Remove(ref DictionaryStorage storage, object? key);

        public virtual bool TryRemoveValue(ref DictionaryStorage storage, object? key, out object? value) {
            if (TryGetValue(key, out value)) {
                return Remove(ref storage, key);

            }

            return false;
        }

        /// <summary>
        /// Convert the storage instance to a mutable type.
        /// </summary>
        /// <remarks>
        /// It has the same effect on the storage as <see cref="Add(ref DictionaryStorage, object?, object?)"/>
        /// or <see cref="Remove(ref DictionaryStorage, object?)"/> except that the contents of the storage
        /// is not modified.
        /// </remarks>
        /// <param name="storage">
        /// Reference to the variable holding the object on which this method is invoked.
        /// </param>
        /// <returns>
        /// <c>this</c> object if it was alreday mutable. Otherwise a reference to a cloned
        /// mutable instance, in which case <paramref name="storage"/> is also updated.
        /// </returns>
        /// <exception cref="InvalidOperationException">The storage is read-only.</exception>
        public abstract DictionaryStorage AsMutable(ref DictionaryStorage storage);

        public abstract void Clear(ref DictionaryStorage storage);

        /// <summary>
        /// Adds items from this dictionary into the other dictionary
        /// </summary>
        public virtual void CopyTo(ref DictionaryStorage/*!*/ into) {
            Debug.Assert(into != null);

            foreach (KeyValuePair<object?, object?> kvp in GetItems()) {
                into!.Add(ref into, kvp.Key, kvp.Value);
            }
        }

        public abstract bool Contains(object? key);

        public abstract bool TryGetValue(object? key, out object? value);

        public abstract int Count { get; }

#if DEBUG
        public bool IsMutable {
            get {
                try {
                    DictionaryStorage testing = this;
                    return ReferenceEquals(this, testing.AsMutable(ref testing));
                } catch (InvalidOperationException) {
                    return false;
                }
            }
        }
#endif

        public virtual bool HasNonStringAttributes() {
            foreach (KeyValuePair<object?, object?> o in GetItems()) {
                if (o.Key is not string && o.Key is not Extensible<string>) {
                    return true;
                }
            }
            return false;
        }

        public abstract List<KeyValuePair<object?, object?>> GetItems();

        public virtual IEnumerable<object?>/*!*/ GetKeys() {
            foreach (var o in GetItems()) {
                yield return o.Key;
            }
        }

        public virtual DictionaryStorage Clone() {
            CommonDictionaryStorage storage = new CommonDictionaryStorage();
            foreach (KeyValuePair<object?, object?> kvp in GetItems()) {
                storage.Add(kvp.Key, kvp.Value);
            }
            return storage;
        }

        public virtual void EnsureCapacityNoLock(int size) {
        }

        public virtual IEnumerator<KeyValuePair<object?, object?>> GetEnumerator() {
            return GetItems().GetEnumerator();
        }
        
        /// <summary>
        /// Provides fast access to the __path__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetPath(out object? value) {
            return TryGetValue("__path__", out value);
        }

        /// <summary>
        /// Provides fast access to the __package__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetPackage(out object? value) {
            return TryGetValue("__package__", out value);
        }

        /// <summary>
        /// Provides fast access to the __builtins__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetBuiltins(out object? value) {
            return TryGetValue("__builtins__", out value);
        }

        /// <summary>
        /// Provides fast access to the __name__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetName(out object? value) {
            return TryGetValue("__name__", out value);
        }

        /// <summary>
        /// Provides fast access to the __import__ attribute if the dictionary storage supports caching it.
        /// </summary>
        public virtual bool TryGetImport(out object? value) {
            return TryGetValue("__import__", out value);
        }
    }

}
