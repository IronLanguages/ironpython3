// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace IronPython.Runtime {
    internal class CollectionDebugProxy {
        private readonly ICollection _collection;

        public CollectionDebugProxy(ICollection collection) {
            _collection = collection;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        internal IList Members {
            get {
                List<object> res = new List<object>(_collection.Count);
                foreach (object o in _collection) {
                    res.Add(o);
                }
                return res;
            }
        }
    }

    internal class ObjectCollectionDebugProxy {
        private readonly ICollection<object> _collection;

        public ObjectCollectionDebugProxy(ICollection<object> collection) {
            _collection = collection;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        internal IList<object> Members {
            get {
                return new List<object>(_collection);
            }
        }
    }
}
