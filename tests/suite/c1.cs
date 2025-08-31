// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Foo {
    public class Bar {
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public Bar() {
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public string Method() {
#if BAR1
			return "In bar1";
#else
            return "In bar2";
#endif
        }
    }
}