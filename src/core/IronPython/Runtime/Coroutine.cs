// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [PythonType("coroutine")]
    [DontMapIDisposableToContextManager, DontMapIEnumerableToContains]
    public sealed class PythonCoroutine : ICodeFormattable, IWeakReferenceable {
        private readonly PythonGenerator _generator;
        private WeakRefTracker? _tracker;

        internal PythonCoroutine(PythonGenerator generator) {
            _generator = generator;
        }

        [LightThrowing]
        public object send(object? value) {
            return _generator.send(value);
        }

        [LightThrowing]
        public object @throw(object? type) {
            return _generator.@throw(type);
        }

        [LightThrowing]
        public object @throw(object? type, object? value) {
            return _generator.@throw(type, value);
        }

        [LightThrowing]
        public object @throw(object? type, object? value, object? traceback) {
            return _generator.@throw(type, value, traceback);
        }

        [LightThrowing]
        public object? close() {
            return _generator.close();
        }

        public object __await__() {
            return new CoroutineWrapper(this);
        }

        public FunctionCode cr_code => _generator.gi_code;

        public int cr_running => _generator.gi_running;

        public TraceBackFrame cr_frame => _generator.gi_frame;

        public string __name__ => _generator.__name__;

        public string __qualname__ {
            get => _generator.__name__;
        }

        internal PythonGenerator Generator => _generator;

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            return $"<coroutine object {__name__} at {PythonOps.HexId(this)}>";
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker? IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _tracker = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion
    }

    [PythonType("coroutine_wrapper")]
    public sealed class CoroutineWrapper {
        private readonly PythonCoroutine _coroutine;

        internal CoroutineWrapper(PythonCoroutine coroutine) {
            _coroutine = coroutine;
        }

        [LightThrowing]
        public object __next__() {
            return _coroutine.send(null);
        }

        [LightThrowing]
        public object send(object? value) {
            return _coroutine.send(value);
        }

        [LightThrowing]
        public object @throw(object? type) {
            return _coroutine.@throw(type);
        }

        [LightThrowing]
        public object @throw(object? type, object? value) {
            return _coroutine.@throw(type, value);
        }

        [LightThrowing]
        public object @throw(object? type, object? value, object? traceback) {
            return _coroutine.@throw(type, value, traceback);
        }

        public object? close() {
            return _coroutine.close();
        }

        public CoroutineWrapper __iter__() {
            return this;
        }
    }
}
