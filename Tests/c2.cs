/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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

