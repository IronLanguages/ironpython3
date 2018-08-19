// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    struct FastBindResult<T> where T : class {
        public readonly T Target;
        public readonly bool ShouldCache;

        public FastBindResult(T target, bool shouldCache) {
            Target = target;
            ShouldCache = shouldCache;
        }

        public FastBindResult(T target) {
            Target = target;
            ShouldCache = false;
        }
    }
}
