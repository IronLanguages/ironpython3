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
using System.Diagnostics;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    class TwoObjects {
        internal object obj1;
        internal object obj2;

        public TwoObjects(object obj1, object obj2) {
            this.obj1 = obj1;
            this.obj2 = obj2;
        }
        public override int GetHashCode() {
            throw new NotSupportedException();
        }
        public override bool Equals(object other) {
            TwoObjects o = other as TwoObjects;
            if (o == null) return false;
            return o.obj1 == obj1 && o.obj2 == obj2;
        }
    }

    class CompareUtil {
        [ThreadStatic]
        private static Stack<object> CmpStack;

        internal static bool Check(object o) {
            if (CmpStack == null) {
                return false;
            }
            return CmpStack.Contains(o);
        }

        internal static void Push(object o) {
            Stack<object> infinite = GetInfiniteCmp();
            if (infinite.Contains(o)) {
                throw PythonOps.RuntimeError("maximum recursion depth exceeded in cmp");
            }
            CmpStack.Push(o);
        }

        internal static void Push(object o1, object o2) {
            Stack<object> infinite = GetInfiniteCmp();
            TwoObjects to = new TwoObjects(o1, o2);
            if (infinite.Contains(to)) {
                throw PythonOps.RuntimeError("maximum recursion depth exceeded in cmp");
            }
            CmpStack.Push(to);
        }

        internal static void Pop(object o) {
            Debug.Assert(CmpStack != null && CmpStack.Count > 0);
            Debug.Assert(CmpStack.Peek() == o);
            CmpStack.Pop();
        }

        internal static void Pop(object o1, object o2) {
            Debug.Assert(CmpStack != null && CmpStack.Count > 0);
            Debug.Assert(CmpStack.Peek() is TwoObjects);
            Debug.Assert(((TwoObjects)CmpStack.Peek()).obj1 == o1);
            Debug.Assert(((TwoObjects)CmpStack.Peek()).obj2 == o2);

            CmpStack.Pop();
        }

        private static Stack<object> GetInfiniteCmp() {
            if (CmpStack == null) {
                CmpStack = new Stack<object>();
            }
            return CmpStack;
        }
    }
}
