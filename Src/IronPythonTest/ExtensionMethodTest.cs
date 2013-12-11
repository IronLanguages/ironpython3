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
using System.Linq;
using System.Text;
using IronPython.Runtime;
using System.Collections;

namespace IronPythonTest.ExtensionMethodTest {
    namespace LinqCollision {
        public static class WhereTest {
            public static int Where(this List list, PythonFunction function) {
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
