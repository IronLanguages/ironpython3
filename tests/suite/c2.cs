// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace ImportTestNS {
#if TEST1
    public class Foo<T> {
        public string Test(){
            return "Foo<T>";
        }
    }
#endif

#if TEST2
    public class Foo<T,Y> {
        public string Test(){
            return "Foo<T,Y>";
        }
    }
#endif

#if TEST3
    public class Foo<T,Y,Z> {
        public string Test(){
            return "Foo<T,Y,Z>";
        }
    }
#endif


#if TEST4
    public class Foo {
        public string Test(){
            return "Foo";
        }
    }
#endif

#if TEST5
    public class Foo {
        public string Test(){
            return "Foo2";
        }
    }
#endif


#if TEST6
    public class Foo<T> {
        public string Test(){
            return "Foo2<T>";
        }
    }

#endif

#if TEST7
    public class Foo<T,Y> {
        public string Test(){
            return "Foo2<T,Y>";
        }
    }
#endif

#if TEST8
    namespace Foo {
        public class Bar {
            public string Test(){
                return "Bar";
            }
        }
    }
#endif

}

