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
using System.Collections;
using System.Runtime.CompilerServices;

namespace IronPythonTest {
    public class DisposableTest : IDisposable {
        public bool Called;

        #region IDisposable Members

        public void Dispose() {
            Called = true;
        }

        #endregion
    }

    public class EnumerableTest : IEnumerable {
        private readonly StrongBox<bool> _disposed;

        public EnumerableTest(StrongBox<bool> disposed) {
            _disposed = disposed;
        }

        #region IEnumerable Members

        public IEnumerator GetEnumerator() {
            return new MyEnumerator(_disposed);
        }

        #endregion        
    }

    public class MyEnumerator : IEnumerator, IDisposable {
        private readonly StrongBox<bool> _disposed;
        private int _count;

        public MyEnumerator(StrongBox<bool> disposed) {
            _disposed = disposed;
        }

        #region IEnumerator Members

        public object Current {
            get { return 42; }
        }

        public bool MoveNext() {
            return _count++ < 2;
        }

        public void Reset() {
            throw new NotImplementedException();
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            _disposed.Value = true;
        }

        #endregion
    }
}
