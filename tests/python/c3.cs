// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

public class Foo{
}

class PrivateFoo{
}

namespace BarNamespace {
    public class Bar {
        public class NestedBar {
        }
    }

    namespace NestedNamespace {
        public class Baz {
        }
    }
}