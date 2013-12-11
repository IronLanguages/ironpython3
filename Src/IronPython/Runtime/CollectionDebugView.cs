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
using System.Text;
using System.Diagnostics;
using System.Collections;

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
