// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace IronPythonTest {
    [TestFixture(Category = "IronPython")]
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
