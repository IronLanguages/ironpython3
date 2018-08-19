// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPythonTest.LoadTest {

    public class Values {
        public static int GlobalName1 = 10;
        public static int GlobalName2 = 20;
        public static int NestedName1 = 30;
        public static int NestedName2 = 40;
    }

    public class Name1 {
        public static int Value = Values.GlobalName1;
    }

    public class Nested {
        public class Name1 {
            public static int Value = Values.NestedName1;
        }

        public class Name2 {
            public static int Value = Values.NestedName2;
        }
    }

    public class Name2 {
        public static int Value = Values.GlobalName2;
    }
}

public class NoNamespaceLoadTest {
    public string HelloWorld() {
        return "Hello World";
    }
}