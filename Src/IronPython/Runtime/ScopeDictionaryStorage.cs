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
using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides dictionary based storage which is backed by a Scope object.
    /// </summary>
    internal class ScopeDictionaryStorage : DictionaryStorage {
        private readonly Scope/*!*/ _scope;
        private readonly PythonContext/*!*/ _context;

        public ScopeDictionaryStorage(PythonContext/*!*/ context, Scope/*!*/ scope) {
            Assert.NotNull(context, scope);

            _scope = scope;
            _context = context;
        }

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            string strKey = key as string;
            if (strKey != null) {
                PythonOps.ScopeSetMember(_context.SharedContext, _scope, strKey, value);
            } else {
                PythonScopeExtension ext = (PythonScopeExtension)_context.EnsureScopeExtension(_scope);
                ext.EnsureObjectKeys().Add(key, value);
            }
        }

        public override bool Contains(object key) {
            object dummy;
            return TryGetValue(key, out dummy);
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            return Remove(key);
        }

        private bool Remove(object key) {
            string strKey = key as string;
            if (strKey != null) {
                if (Contains(key)) {
                    return PythonOps.ScopeDeleteMember(_context.SharedContext, Scope, strKey);
                }
            } else {
                PythonScopeExtension ext = (PythonScopeExtension)_context.EnsureScopeExtension(_scope);

                return ext.ObjectKeys != null && ext.ObjectKeys.Remove(key);
            }

            return false;
        }

        public override bool TryGetValue(object key, out object value) {
            string strKey = key as string;
            if (strKey != null) {
                return PythonOps.ScopeTryGetMember(_context.SharedContext, _scope, strKey, out value);
            } else {
                PythonScopeExtension ext = (PythonScopeExtension)_context.EnsureScopeExtension(_scope);
                if (ext.ObjectKeys != null && ext.ObjectKeys.TryGetValue(key, out value)) {
                    return true;
                }
            }

            value = null;
            return false;
        }

        public override int Count {
            get {
                return GetItems().Count;
            }
        }

        public override void Clear(ref DictionaryStorage storage) {
            foreach (var item in GetItems()) {
                Remove(item.Key);
            }
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            List<KeyValuePair<object, object>> res = new List<KeyValuePair<object, object>>();

            foreach (object name in PythonOps.ScopeGetMemberNames(_context.SharedContext, _scope)) {
                object value;
                if (TryGetValue(name, out value)) {
                    res.Add(new KeyValuePair<object, object>(name, value));
                }
            }

            return res;
        }

        internal Scope/*!*/ Scope {
            get {
                return _scope;
            }
        }
    }
}
