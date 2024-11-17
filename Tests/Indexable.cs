// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace IronPythonTest {
    public class Tuple {
        private object[] val;

        public Tuple(object a) {
            val = new object[] { a };
        }

        public Tuple(object a, object b) {
            val = new object[] { a, b };
        }

        public Tuple(object a, object b, object c) {
            val = new object[] { a, b, c };
        }

        public Tuple(object a, object b, object c, object d) {
            val = new object[] { a, b, c, d };
        }

        public Tuple(object a, object b, object c, object d, object e) {
            val = new object[] { a, b, c, d, e };
        }

        private static bool Compare(object x, object y) {
            if (x != null) {
                if (y != null) {
                    return x.Equals(y);
                } else return false;
            } else {
                return y == null;
            }
        }

        public override bool Equals(object obj) {
            Tuple other = obj as Tuple;
            if (other == null) return false;

            if (val != null) {
                if (other.val != null) {
                    if (val.Length != other.val.Length) {
                        return false;
                    }
                    for (int i = 0; i < val.Length; i++) {
                        if (!Compare(val[i], other.val[i])) {
                            return false;
                        }
                    }
                    return true;
                } else return false;
            } else return other.val == null;
        }

        public override int GetHashCode() {
            int hash = 0;
            if (val != null && val.Length > 0) {
                hash = val[0].GetHashCode();

                for (int i = 1; i < val.Length; i++) {
                    hash ^= val[i].GetHashCode();
                }
            }
            return hash;
        }
    }

    public struct StructIndexable {
        private List<int?> _data;

        public int? this[int index] {
            get {
                if (_data == null || _data.Count < index) {
                    return null;
                }

                return _data[index];
            }
            set {
                if (_data == null) {
                    _data = new List<int?>();
                }

                while (_data.Count <= index) {
                    _data.Add(null);
                }

                _data[index] = value;
            }
        }
    }

    public class Indexable {
        private Hashtable ht = new Hashtable();

        public string this[int index] {
            get {
                return (string)ht[index];
            }
            set {
                ht[index] = value;
            }
        }
        public string this[string index] {
            get {
                return (string)ht[index];
            }
            set {
                ht[index] = value;
            }
        }
        public string this[double index] {
            get {
                return (string)ht[index];
            }
            set {
                ht[index] = value;
            }
        }
        public string this[int i, int j] {
            get {
                string ret = (string)ht[new Tuple(i, j)];
                return ret;
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[int i, double j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[int i, string j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[double i, int j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[double i, double j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[double i, string j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[string i, int j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[string i, double j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public string this[string i, string j] {
            get {
                return (string)ht[new Tuple(i, j)];
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
    }

    public class IndexableList : IList {
        private List<object> myList = new List<object>();

        public object this[string index] {
            get {
                return myList[Int32.Parse(index)];
            }
            set {
                myList[Int32.Parse(index)] = value;
            }
        }
        #region IList Members

        public int Add(object value) {
            int res = myList.Count;
            myList.Add(value);
            return res;
        }

        public void Clear() {
            myList.Clear();
        }

        public bool Contains(object value) {
            return myList.Contains(value);
        }

        public int IndexOf(object value) {
            return myList.IndexOf(value);
        }

        public void Insert(int index, object value) {
            myList.Insert(index, value);
        }

        public bool IsFixedSize {
            get { return false; }
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public void Remove(object value) {
            myList.Remove(value);
        }

        public void RemoveAt(int index) {
            myList.RemoveAt(index);
        }

        public object this[int index] {
            get {
                return myList[index];
            }
            set {
                myList[index] = value;
            }
        }

        #endregion

        #region ICollection Members

        public void CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }

        public int Count {
            get { return myList.Count; }
        }

        public bool IsSynchronized {
            get { return false; }
        }

        public object SyncRoot {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator() {
            return myList.GetEnumerator();
        }

        #endregion
    }

    public class PropertyAccessClass {
        public int this[int i] {
            get {
                return i;
            }
            set {
                if (i != value) {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public int this[int i, int j] {
            get {
                return i + j;
            }
            set {
                if (i + j != value) {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public int this[int i, int j, int k] {
            get {
                return i + j + k;
            }
            set {
                if (i + j + k != value) {
                    throw new IndexOutOfRangeException();
                }
            }
        }
    }

    public class MultipleIndexes {
        private Dictionary<object, object> ht = new Dictionary<object, object>();

        // works like Hashtable indexer--returns null instead of throwing if the key isn't found
        private object GetValue(Tuple t) {
            object result;
            ht.TryGetValue(t, out result);
            return result;
        }

        public object this[object i] {
            get {
                return GetValue(new Tuple(i));
            }
            set {
                ht[new Tuple(i)] = value;
            }
        }
        public object this[object i, object j] {
            get {
                return GetValue(new Tuple(i, j));
            }
            set {
                ht[new Tuple(i, j)] = value;
            }
        }
        public object this[object i, object j, object k] {
            get {
                return GetValue(new Tuple(i, j, k));
            }
            set {
                ht[new Tuple(i, j, k)] = value;
            }
        }
        public object this[object i, object j, object k, object l] {
            get {
                return GetValue(new Tuple(i, j, k, l));
            }
            set {
                ht[new Tuple(i, j, k, l)] = value;
            }
        }
        public object this[object i, object j, object k, object l, object m] {
            get {
                return GetValue(new Tuple(i, j, k, l, m));
            }
            set {
                ht[new Tuple(i, j, k, l, m)] = value;
            }
        }
    }

    // TODO: LastIndexOf
    public class UsePythonListAsList {
        private IList<int> list;
        public UsePythonListAsList(IList<int> list) {
            this.list = list;
        }

        public int Inspect() {
            list.Clear();
            for (int i = 0; i < 100; i++) list.Add(i);

            int flag = 0;
            if (list.IndexOf(5) == 5) flag += 1;
            if (list.IndexOf(1000) == -1) flag += 10;
            if (list[5] == 5) flag += 100;
            if (list.Remove(5)) flag += 1000;
            if (list.IndexOf(5) == -1) flag += 10000;
            return flag;
        }

        public void AddRemove() {
            int value = 20;
            list.Insert(0, value);
            list.Insert(3, value);
            list.Insert(5, value);
            list.Insert(list.Count, value);
            list[list.Count / 2] = value;
            list.RemoveAt(2);
            list.RemoveAt(list.Count - 1);
        }

        public int Loop() {
            int sum = 0;
            foreach (int t in list) sum += t;
            return sum;
        }
    }

    public class UsePythonListAsArrayList {
        private ArrayList list;
        public UsePythonListAsArrayList(ArrayList list) {
            this.list = list;
        }

        public void AddRemove() {
            foreach (object o in new object[] { 1, 2L, "string", typeof(int) }) {
                list.Add(o);
            }

            list.Remove(2L);
            list.RemoveAt(0);
            list.RemoveRange(1, 2);
            list.Reverse();

            list.InsertRange(1, new object[] { 100, 30.4, (byte)3 });
            list.SetRange(list.Count - 2, new object[] { (ushort)20, -200 });

            list.Reverse();

            //list.Capacity = list.Count / 2;
        }

        public int Inspect() {
            list.Clear();
            for (int i = 0; i < 10; i++) list.Add(i);
            list.AddRange(new string[] { "a", "bc" });

            int flag = 0;

            if (list.IndexOf("a") == 10
                && list.IndexOf(4, 3) == 4 && list.IndexOf(4, 6) == -1
                && list.IndexOf(3, 1, 4) == 3 && list.IndexOf(4, 1, 10) == 4
                && list.IndexOf(3, 7, 3) == -1
                && list.LastIndexOf("a") == 10 && list.LastIndexOf("bc") == 11
                && list.LastIndexOf(4, 5) == 4
                && list.LastIndexOf(4, 6, 2) == -1 && list.LastIndexOf(4, 6, 3) == 4
                && list.LastIndexOf(1, 6, 5) == -1 && list.LastIndexOf(1, 6, 6) == 1
                && list.LastIndexOf(0, 6, 6) == -1 && list.LastIndexOf(0, 6, 7) == 0
                )
                flag += 1;

            if (list.Count == 12) flag += 10;
            if (list.Contains(8)) flag += 100;
            if (list.Contains("abc") == false) flag += 1000;
            if (list.BinarySearch("5", new MyComparer()) == 5
                && list.BinarySearch(0, 10, "a", new MyComparer()) == -1
                && list.BinarySearch(0, 10, 5, new MyComparer()) == 5
                )
                flag += 10000;
            return flag;
        }

        public int Loop(out string strs) {
            strs = null;
            int sum = 0;
            foreach (object o in list) {
                if (o is int) {
                    sum += (int)o;
                } else if (o is string) {
                    strs += (string)o;
                }
            }
            return sum;
        }

        private class MyComparer : IComparer {
            public int Compare(object x, object y) {
                return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
            }
        }
    }

    public class UsePythonDictAsDictionary {
        private IDictionary<string, int> dict;

        public UsePythonDictAsDictionary(IDictionary<string, int> dict) {
            this.dict = dict;
        }

        public void AddRemove() {
            dict.Add("hello", 1000);
            dict.Add(new KeyValuePair<string, int>("world", 2000));
            dict.Add("python", 3000);
            dict.Remove("python");
        }

        public int Inspect(out string keys, out int values) {
            dict.Clear();
            for (int i = 0; i < 10; i++)
                dict.Add(i.ToString(), i);

            int flag = 0;
            if (dict.ContainsKey("5")) flag += 1;
            if (dict["5"] == 5) flag += 10;
            if (dict.Count == 10) flag += 100;
            int val;
            if (dict.TryGetValue("6", out val)) flag += 1000;
            if (dict.TryGetValue("spam", out val) == false) flag += 10000;

            keys = string.Empty;
            foreach (string s in SortedArray(dict.Keys, null)) {
                keys += s;
            }
            values = 0;
            foreach (int v in dict.Values) {
                values += v;
            }

            return flag;
        }

        private T[] SortedArray<T>(ICollection<T> value, Comparison<T> comparer) {
            T[] array = new T[value.Count];
            value.CopyTo(array, 0);
            if (comparer != null) {
                Array.Sort(array, comparer);
            } else {
                Array.Sort(array);
            }
            return array;
        }

        private static int KeyValueComparer(KeyValuePair<string, int> x, KeyValuePair<string, int> y) {
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
        }

        public void Loop(out string keys, out int values) {
            keys = string.Empty;
            values = 0;
            foreach (KeyValuePair<string, int> pair in SortedArray(dict, KeyValueComparer)) {
                keys += pair.Key;
                values += pair.Value;
            }
        }
    }

    public class CSharpEnumerable {
        public IEnumerable<int> GetEnumerableOfInt() {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        public IEnumerable<object> GetEnumerableOfObject() {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        public IEnumerable GetEnumerable() {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        public IEnumerator<int> GetEnumeratorOfInt() {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        public IEnumerator<object> GetEnumeratorOfObject() {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        public IEnumerator GetEnumerator() {
            yield return 1;
            yield return 2;
            yield return 3;
        }
    }

    public class UsePythonDictAsHashtable {
        private Hashtable table;

        public UsePythonDictAsHashtable(Hashtable table) {
            this.table = table;
        }

        public void AddRemove() {
            table.Add(200, 400);
            table.Add("spam", "spam");

            table.Remove("spam");
        }

        public int Inspect(out int keysum, out int valuesum) {
            table.Clear();
            for (int i = 0; i < 10; i++) {
                table.Add(i, i * i);
            }

            int flag = 0;
            if (table.Contains(0)) flag += 1;
            if (table.Contains("0") == false) flag += 10;
            if (table.ContainsKey(3)) flag += 100;
            if (table.ContainsValue(81)) flag += 1000;
            if ((int)table[8] == 64) flag += 10000;
            table[8] = 89;
            if ((int)table[8] == 89) flag += 100000;
            if (table.Count == 10) flag += 1000000;

            keysum = 0;
            foreach (object o in table.Keys) {
                keysum += (int)o;
            }
            valuesum = 0;
            foreach (object o in table.Values) {
                valuesum += (int)o;
            }
            return flag;
        }

        public int Loop() {
            int sum = 0;
            IDictionaryEnumerator ide = table.GetEnumerator();
            while (ide.MoveNext()) {
                sum += (int)ide.Value;
                sum += (int)ide.Key;
            }
            return sum;
        }
    }
}
