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

using MSAst = System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Generation;
using Utils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Copy on write constant dictionary storage used for dictionaries created with constant items.
    /// </summary>
    [Serializable]
    class ConstantDictionaryStorage : DictionaryStorage, IExpressionSerializable {
        private readonly CommonDictionaryStorage _storage;

        public ConstantDictionaryStorage(CommonDictionaryStorage storage) {
            _storage = storage;
        }

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            lock (this) {
                if (storage == this) {
                    var newStore = new CommonDictionaryStorage();
                    _storage.CopyTo(newStore);
                    newStore.AddNoLock(key, value);
                    storage = newStore;
                    return;
                }
            }
            
            // race, try again...
            storage.Add(ref storage, key, value);
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            if (_storage.Contains(key)) {
                lock (this) {
                    if (storage == this) {
                        var newStore = new CommonDictionaryStorage();
                        _storage.CopyTo(newStore);
                        newStore.Remove(key);
                        storage = newStore;
                        return true;
                    }
                }

                // race, try again
                return storage.Remove(ref storage, key);
            }
            
            return false;
        }

        public override void Clear(ref DictionaryStorage storage) {
            lock (this) {
                if (storage == this) {
                    storage = EmptyDictionaryStorage.Instance;
                    return;
                }
            }

            // race, try again
            storage.Clear(ref storage);
        }

        public override bool Contains(object key) {
            return _storage.Contains(key);
        }

        public override bool TryGetValue(object key, out object value) {
            return _storage.TryGetValue(key, out value);
        }

        public override int Count {
            get { return _storage.Count; }
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            return _storage.GetItems();
        }

        public override DictionaryStorage Clone() {
            return _storage.Clone();
        }

        public override bool HasNonStringAttributes() {
            return _storage.HasNonStringAttributes();
        }

        #region IExpressionSerializable Members

        public MSAst.Expression CreateExpression() {
            MSAst.Expression[] items = new MSAst.Expression[Count * 2];
            int index = 0;
            foreach (var item in GetItems()) {
                items[index++] = Utils.Convert(Utils.Constant(item.Value), typeof(object));
                items[index++] = Utils.Convert(Utils.Constant(item.Key), typeof(object));
            }

            return MSAst.Expression.Call(
                typeof(PythonOps).GetMethod("MakeConstantDictStorage"),
                MSAst.Expression.NewArrayInit(
                    typeof(object),
                    items
                )
            );
        }

        #endregion
    }
}
