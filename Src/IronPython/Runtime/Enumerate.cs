// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /*
     * Enumerators exposed to Python code directly
     *
     */

    [PythonType("enumerate")]
    [Documentation("enumerate(iterable) -> iterator for index, value of iterable")]
    [DontMapIDisposableToContextManager, DontMapIEnumerableToContains]
    public class Enumerate : IEnumerator, IEnumerator<object> {
        private readonly IEnumerator _iter;
        private object _index;

        public Enumerate(object iter) {
            _iter = PythonOps.GetEnumerator(iter);
            _index = ScriptingRuntimeHelpers.Int32ToObject(-1);
        }

        public Enumerate(CodeContext context, object iter, object start) {
            object index = PythonOps.Index(start);
            _iter = PythonOps.GetEnumerator(iter);
            _index = context.LanguageContext.Operation(Binding.PythonOperationKind.Subtract, index, ScriptingRuntimeHelpers.Int32ToObject(1));
        }

        public object __iter__() {
            return this;
        }

        public PythonTuple __reduce__() {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(_iter, AddOneTo(_index))
            );
        }

        private static object AddOneTo(object _index) {
            if (_index is int index) {
                if (index != int.MaxValue) {
                    return ScriptingRuntimeHelpers.Int32ToObject(index + 1);
                } else {
                    return new BigInteger(int.MaxValue) + 1;
                }
            } else {
                Debug.Assert(_index is BigInteger);
                return (BigInteger)_index + 1;
            }
        }

        #region IEnumerator Members

        void IEnumerator.Reset() {
            throw new NotImplementedException();
        }

        object IEnumerator.Current {
            get {
                return PythonTuple.MakeTuple(_index, _iter.Current);
            }
        }

        object IEnumerator<object>.Current {
            get {
                return ((IEnumerator)this).Current;
            }
        }

        bool IEnumerator.MoveNext() {
            _index = AddOneTo(_index);
            return _iter.MoveNext();
        }

        #endregion

        #region IDisposable Members

        void IDisposable.Dispose() {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        [PythonHidden]
        protected virtual void Dispose(bool notFinalizing) {
        }

        #endregion
    }

    [PythonType("callable_iterator")]
    public sealed class SentinelIterator : IEnumerator, IEnumerator<object> {
        private readonly object _target;
        private readonly object _sentinel;
        private readonly CodeContext/*!*/ _context;
        private readonly CallSite<Func<CallSite, CodeContext, object, object>> _site;
        private object _current;
        private bool _sinkState;

        public SentinelIterator(CodeContext/*!*/ context, object target, object sentinel) {
            _target = target;
            _sentinel = sentinel;
            _context = context;
            _site = CallSite<Func<CallSite, CodeContext, object, object>>.Create(_context.LanguageContext.InvokeNone);
        }

        public object __iter__() {
            return this;
        }

        public object __next__() {
            if (((IEnumerator)this).MoveNext()) {
                return ((IEnumerator)this).Current;
            } else {
                throw PythonOps.StopIteration();
            }
        }

        #region IEnumerator implementation

        object IEnumerator.Current {
            get {
                return _current;
            }
        }

        object IEnumerator<object>.Current {
            get {
                return _current;
            }
        }

        bool IEnumerator.MoveNext() {
            if (_sinkState) return false;

            _current = _site.Target(_site, _context, _target);

            bool hit = _sentinel == _current || PythonOps.EqualRetBool(_context, _sentinel, _current);
            if (hit) _sinkState = true;

            return !hit;
        }

        void IEnumerator.Reset() {
            throw new NotImplementedException();
        }

        #endregion

        #region IDisposable Members

        void IDisposable.Dispose() {
        }

        #endregion
    }

    /*
     * Enumerators exposed to .NET code
     */

    [PythonType("enumerator")]
    public class PythonEnumerator : IEnumerator {
        private readonly object _baseObject;
        private object _current;
        public static bool TryCastIEnumer(object baseObject, out IEnumerator enumerator) {
            if (baseObject is IEnumerator) {
                enumerator = (IEnumerator)baseObject;
                return true;
            }

            if (baseObject is IEnumerable) {
                enumerator = ((IEnumerable)baseObject).GetEnumerator();
                return true;
            }

            enumerator = null;
            return false;
        }

        public static bool TryCreate(object baseObject, out IEnumerator enumerator) {
            if (TryCastIEnumer(baseObject, out enumerator)) {
                return true;
            }

            if (PythonOps.TryGetBoundAttr(baseObject, "__iter__", out object iter)) {
                object iterator = PythonCalls.Call(iter);
                // don't re-wrap if we don't need to (common case is PythonGenerator).
                if (TryCastIEnumer(iterator, out enumerator)) {
                    return true;
                }
                enumerator = new PythonEnumerator(iterator);
                return true;
            } else {
                enumerator = null;
                return false;
            }
        }

        public static IEnumerator Create(object baseObject) {
            IEnumerator res;
            if (!TryCreate(baseObject, out res)) {
                throw PythonOps.TypeError("cannot convert {0} to IEnumerator", PythonOps.GetPythonTypeName(baseObject));
            }
            return res;
        }

        internal PythonEnumerator(object iter) {
            Debug.Assert(!(iter is PythonGenerator));

            _baseObject = iter;
        }


        #region IEnumerator Members

        public void Reset() {
            throw new NotImplementedException();
        }

        public object Current {
            get {
                return _current;
            }
        }

        /// <summary>
        /// Move to the next item in this iterable
        /// </summary>
        /// <returns>True if moving was successfull</returns>
        public bool MoveNext() {
            PythonTypeOps.TryGetOperator(DefaultContext.Default, _baseObject, "__next__", out object nextMethod);

            if (nextMethod == null) {
                throw PythonOps.TypeErrorForNotAnIterator(_baseObject);
            }

            try {
                _current = DefaultContext.Default.LanguageContext.CallLightEh(DefaultContext.Default, nextMethod);
                Exception lightEh = LightExceptions.GetLightException(_current);
                if (lightEh != null) {
                    if (lightEh is StopIterationException) {
                        return false;
                    }

                    throw lightEh;
                }
                return true;
            } catch (StopIterationException) {
                return false;
            }
        }

        #endregion

        public object __iter__() {
            return this;
        }
    }

    [PythonType("enumerable")]
    public class PythonEnumerable : IEnumerable {
        private readonly object _iterator;

        public static bool TryCreate(CodeContext context, object baseEnumerator, out IEnumerable enumerator) {
            Debug.Assert(!(baseEnumerator is IEnumerable) || baseEnumerator is IPythonObject);   // we shouldn't re-wrap things that don't need it

            if (PythonOps.TryGetBoundAttr(context, baseEnumerator, "__iter__", out object iter)) {
                object iterator = PythonCalls.Call(context, iter);
                if (iterator is IEnumerable en) {
                    enumerator = en;
                } else {
                    if (!PythonOps.TryGetBoundAttr(context, iterator, "__next__", out _)) {
                        enumerator = null;
                        return false;
                    }
                    enumerator = new PythonEnumerable(iterator);
                }
                return true;
            } else {
                enumerator = null;
                return false;
            }
        }

        public static IEnumerable Create(CodeContext context, object baseObject) {
            IEnumerable res;
            if (!TryCreate(context, baseObject, out res)) {
                throw PythonOps.TypeError("cannot convert {0} to IEnumerable", PythonOps.GetPythonTypeName(baseObject));
            }
            return res;
        }

        private PythonEnumerable(object iterator) {
            this._iterator = iterator;
        }

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return _iterator as IEnumerator ?? new PythonEnumerator(_iterator);
        }

        #endregion
    }

    [PythonType("iterator")]
    public sealed class ItemEnumerator : IEnumerator {
        // The actual object on which we are calling __getitem__()
        private object _source;
        private object _getItemMethod;
        private CallSite<Func<CallSite, CodeContext, object, int, object>> _site;
        private object _current;
        private int _index;

        internal ItemEnumerator(object source, object getItemMethod, CallSite<Func<CallSite, CodeContext, object, int, object>> site) {
            _source = source;
            _getItemMethod = getItemMethod;
            _site = site;
        }

        #region Pickling

        public PythonTuple __reduce__(CodeContext context) {
            context.TryLookupBuiltin("iter", out object iter);
            if (_index < 0) {
                return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(PythonTuple.EMPTY));
            }
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(_source), _index);
        }

        public void __setstate__(int index) {
            // If our iterator has already gone through all of the elements,
            // then it cannot be reset as it has "released"
            if (_index < 0) {
                return;
            }

            if (index < 0) {
                _index = 0;
            } else {
                _index = index;
            }
        }

        #endregion

        #region IEnumerator members

        object IEnumerator.Current {
            get {
                return _current;
            }
        }

        bool IEnumerator.MoveNext() {
            if (_index < 0) {
                return false;
            }

            try {
                _current = _site.Target(_site, DefaultContext.Default, _getItemMethod, _index);
                _index++;
                return true;
            } catch (IndexOutOfRangeException) {
                _current = null;
                _site = null;
                _source = null;
                _getItemMethod = null;
                _index = -1;     // this is the end
                return false;
            } catch (StopIterationException) {
                _current = null;
                _site = null;
                _source = null;
                _getItemMethod = null;
                _index = -1;     // this is the end
                return false;
            }
        }

        void IEnumerator.Reset() {
            _index = 0;
            _current = null;
        }

        #endregion
    }

    [PythonType("iterable")]
    public sealed class ItemEnumerable : IEnumerable {
        private readonly object _source;
        private readonly object _getitem;
        private readonly CallSite<Func<CallSite, CodeContext, object, int, object>> _site;

        internal ItemEnumerable(object source, object getitem, CallSite<Func<CallSite, CodeContext, object, int, object>> site) {
            _source = source;
            _getitem = getitem;
            _site = site;
        }

        public IEnumerator __iter__() {
            return ((IEnumerable)this).GetEnumerator();
        }

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new ItemEnumerator(_source, _getitem, _site);
        }

        #endregion
    }
}
