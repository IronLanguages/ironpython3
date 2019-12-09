// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using IronPython.Runtime;
using System.Collections;
using System.Collections.Generic;

namespace IronPythonTest {
    /// <summary>
    /// Base used for implicit operator tests where the operator is defined
    /// on the "derived" class
    /// </summary>
    public class Base {
        public int value;

        public Base(int value) {
            this.value = value;
        }
    }

    public class Derived {
        public Derived(int value) {
            this.value = value;
        }

        public int value;

        public static implicit operator Base(Derived d) {
            return new Base(d.value);
        }
    }

    /// <summary>
    /// Base class used where the implicit operator is defined on the
    /// "base" class
    /// </summary>
    public class Base2 {
        public int value;

        public Base2(int value) {
            this.value = value;
        }

        public static implicit operator Base2(Derived2 d) {
            return new Base2(d.value);
        }
    }

    public class Derived2 {
        public int value;

        public Derived2(int value) {
            this.value = value;
        }
    }

    public class ConversionStorage {
        public Base Base;
        public Derived Derived;
        public Base2 Base2;
        public Derived2 Derived2;
    }

    public class DoubleToFloat {
        public static float ToFloat(double val) { return (float)val; }
    }

    public class DictConversion {
        public static IList<object> ToIDictionary(IDictionary dict) {
            List<object> res = new List<object>();
            foreach (DictionaryEntry de in dict) {
                res.Add(PythonTuple.MakeTuple(de.Key, de.Value));
            }

            return res;
        }
    }
}
