// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPythonTest {
    public class ExceptionsTest {
        // a call  that takes the fast path
        public void CallVirtual() {
            VirtualFunc();
        }

        public virtual void VirtualFunc() {
            // a virtual function we can override for throwing from Python code
        }

        // overloads forced to take the slow path...
        public void CallVirtualOverloaded(int bar) {
            VirtualFunc();
        }

        public void CallVirtualOverloaded(string s) {
            VirtualFunc();
        }

        public void CallVirtualOverloaded(object foo) {
            VirtualFunc();
        }

        public void ThrowException() {
            throw new IndexOutOfRangeException("Index out of range!");
        }

        public object CallVirtCatch() {
            try {
                CallVirtual();
            } catch (Exception e) {
                return e;
            }
            return null;
        }

        public object CatchAndRethrow() {
            try {
                CallVirtual();
            } catch (Exception e) {
                throw e;
            }
            return null;
        }
        public object CatchAndRethrow2() {
            try {
                CallVirtual();
            } catch (Exception) {
                throw;
            }
            return null;
        }
    }
}