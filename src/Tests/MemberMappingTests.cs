// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

using IronPython.Runtime.Operations;

// Includes tests for mapping from .NET interfaces & known types to various Python protocol methods.

namespace IronPythonTest {
    public class GenericCollection : ICollection<int> {
        private List<int> _data = new List<int>();

        #region ICollection<int> Members

        public void Add(int item) {
            _data.Add(item);
        }

        public void Clear() {
            _data.Clear();
        }

        public bool Contains(int item) {
            return _data.Contains(item);
        }

        public void CopyTo(int[] array, int arrayIndex) {
            _data.CopyTo(array, arrayIndex);
        }

        public int Count {
            get { return _data.Count; }
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public bool Remove(int item) {
            return _data.Remove(item);
        }

        #endregion

        #region IEnumerable<int> Members

        public IEnumerator<int> GetEnumerator() {
            return _data.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return ((IEnumerable)_data).GetEnumerator();
        }

        #endregion
    }

    public class IndexableIteration {
        public int this[int index] {
            get {
                if (index > 0) {
                    throw PythonOps.StopIteration();
                }
                return index;
            }
        }
    }
}
