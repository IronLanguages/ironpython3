// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Runtime;
using System.Collections;

namespace IronPythonTest.ExtensionMethodTest {
    namespace LinqCollision {
        public static class WhereTest {
            public static int Where(this PythonList list, PythonFunction function) {
                return 42;
            }
        }
    }

    namespace ClassRelationship {
        public static class ClassTests {
            public static int BaseClass(this object foo) {
                return 23;
            }

            public static int Interface(this IList foo) {
                return 23;
            }

            public static int GenericInterface(this IList<object> foo) {
                return 23;
            }

            public static int GenericInterfaceAndMethod<T>(this IList<T> foo) {
                return 23;
            }

            public static int Array(this int[] foo) {
                return 23;
            }

            public static int ArrayAndGenericMethod<T>(this T[] foo) {
                return 23;
            }

            public static int GenericMethod<T>(this T foo) {
                return 23;
            }
        }
    }
}
