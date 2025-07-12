// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    internal class CompareUtil {
        [ThreadStatic]
        private static Stack<object>? CmpStack;

        internal static bool Check(object o) => CmpStack?.Contains(o) ?? false;

        internal static void Push(object o) {
            var stack = EnsureStack();
            if (stack.Contains(o)) {
                throw PythonOps.RecursionError("maximum recursion depth exceeded");
            }
            stack.Push(o);
        }

        internal static void Push(object o1, object o2) => Push(new TwoObjects(o1, o2));

        internal static void Pop(object o) {
            Debug.Assert(CmpStack != null && CmpStack.Count > 0);
            Debug.Assert(CmpStack!.Peek() == o);
            CmpStack.Pop();
        }

        internal static void Pop(object o1, object o2) {
            Debug.Assert(CmpStack != null && CmpStack.Count > 0);
            Debug.Assert(CmpStack!.Peek() is TwoObjects t && t.Equals(new TwoObjects(o1, o2)));
            CmpStack.Pop();
        }

        private static Stack<object> EnsureStack() {
            var stack = CmpStack;
            if (stack == null) {
                CmpStack = stack = new Stack<object>();
            }
            return stack;
        }

        private class TwoObjects {
            private readonly object obj1;
            private readonly object obj2;

            public TwoObjects(object obj1, object obj2) {
                this.obj1 = obj1;
                this.obj2 = obj2;
            }
            public override int GetHashCode() => throw new NotSupportedException();

            public override bool Equals(object? other) => other is TwoObjects o && o.obj1 == obj1 && o.obj2 == obj2;
        }
    }
}
