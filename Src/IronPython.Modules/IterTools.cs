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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

[assembly: PythonModule("itertools", typeof(IronPython.Modules.PythonIterTools))]
namespace IronPython.Modules {
    public static class PythonIterTools {
        public const string __doc__ = "Provides functions and classes for working with iterable objects.";

        public static object tee(object iterable) {
            return tee(iterable, 2);
        }

        public static object tee(object iterable, int n) {
            if (n < 0) throw PythonOps.ValueError("n cannot be negative");

            object[] res = new object[n];
            if (!(iterable is TeeIterator)) {
                IEnumerator iter = PythonOps.GetEnumerator(iterable);
                List dataList = new List();

                for (int i = 0; i < n; i++) {
                    res[i] = new TeeIterator(iter, dataList);
                }

            } else if (n != 0) {
                // if you pass in a tee you get back the original tee
                // and other iterators that share the same data.
                TeeIterator ti = iterable as TeeIterator;
                res[0] = ti;
                for (int i = 1; i < n; i++) {
                    res[1] = new TeeIterator(ti._iter, ti._data);
                }
            }

            return PythonTuple.MakeTuple(res);
        }

        /// <summary>
        /// Base class used for iterator wrappers.
        /// </summary>
        [PythonType, PythonHidden]
        public class IterBase : IEnumerator {
            private IEnumerator _inner;

            internal IEnumerator InnerEnumerator {
                set { _inner = value; }
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get { return _inner.Current; }
            }

            bool IEnumerator.MoveNext() {
                return _inner.MoveNext();
            }

            void IEnumerator.Reset() {
                _inner.Reset();
            }

            public object __iter__() {
                return this;
            }

            #endregion
        }

        [PythonType]
        public class chain : IterBase {
            private chain() { }

            public chain(params object[] iterables) {
                InnerEnumerator = LazyYielder(iterables);
            }

            [ClassMethod]
            public static chain from_iterable(CodeContext/*!*/ context, PythonType cls, object iterables) {
                chain res;
                if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(chain))) {
                    res = new chain();
                    res.InnerEnumerator = LazyYielder(iterables);
                } else {
                    res = (chain)cls.CreateInstance(context);
                    res.InnerEnumerator = LazyYielder(iterables);
                }

                return res;
            }

            private static IEnumerator<object> LazyYielder(object iterables) {
                IEnumerator ie = PythonOps.GetEnumerator(iterables);
                while (ie.MoveNext()) {
                    IEnumerator inner = PythonOps.GetEnumerator(ie.Current);
                    while (inner.MoveNext()) {
                        yield return inner.Current;
                    }
                }
            }
        }

        [PythonType]
        public class compress : IterBase {
            private compress() { }

            public compress(CodeContext/*!*/ context, [NotNull]object data, [NotNull]object selectors) {
                EnsureIterator(context, data);
                EnsureIterator(context, selectors);
                InnerEnumerator = LazyYielder(data, selectors);
            }

            private static void EnsureIterator(CodeContext/*!*/ context, object iter) {
                if (iter is IEnumerable || iter is IEnumerator ||
                    iter is IEnumerable<object> || iter is IEnumerator<object>) {
                    return;
                }
                if (iter == null ||
                    !PythonOps.HasAttr(context, iter, "__iter__") &&
                    !PythonOps.HasAttr(context, iter, "__getitem__")) {
                        if (iter is OldInstance) {
                            throw PythonOps.TypeError("iteration over non-sequence");
                        } else {
                            throw PythonOps.TypeError("'{0}' object is not iterable", PythonTypeOps.GetName(iter));
                        }
                }
            }

            // (d for d, s in zip(data, selectors) if s)
            private static IEnumerator<object> LazyYielder(object data, object selectors) {
                IEnumerator de = PythonOps.GetEnumerator(data);
                IEnumerator se = PythonOps.GetEnumerator(selectors);

                while (de.MoveNext()) {
                    if (!se.MoveNext()) {
                        break;
                    }
                    if (PythonOps.IsTrue(se.Current)) {
                        yield return de.Current;
                    }
                }
            }
        }

        [PythonType]
        public class count : IterBase, ICodeFormattable {
            private int _curInt;
            private object _step, _cur;

            public count() {
                _curInt = 0;
                _step = 1;
                InnerEnumerator = IntYielder(this, 0, 1);
            }

            public count(int start) {
                _curInt = start;
                _step = 1;
                InnerEnumerator = IntYielder(this, start, 1);
            }

            public count(BigInteger start) {
                _cur = start;
                _step = 1;
                InnerEnumerator = BigIntYielder(this, start, 1);
            }

            public count([DefaultParameterValue(0)]int start, [DefaultParameterValue(1)]int step) {
                _curInt = start;
                _step = step;
                InnerEnumerator = IntYielder(this, start, step);
            }

            public count([DefaultParameterValue(0)]int start, BigInteger step) {
                _curInt = start;
                _step = step;
                InnerEnumerator = IntYielder(this, start, step);
            }

            public count(BigInteger start, int step) {
                _cur = start;
                _step = step;
                InnerEnumerator = BigIntYielder(this, start, step);
            }

            public count(BigInteger start, BigInteger step) {
                _cur = start;
                _step = step;
                InnerEnumerator = BigIntYielder(this, start, step);
            }

            public count(CodeContext/*!*/ context, [DefaultParameterValue(0)]object start, [DefaultParameterValue(1)]object step) {
                EnsureNumeric(context, start);
                EnsureNumeric(context, step);
                _cur = start;
                _step = step;
                InnerEnumerator = ObjectYielder(PythonContext.GetContext(context), this, start, step);
            }

            private static void EnsureNumeric(CodeContext/*!*/ context, object num) {
                if (num is int || num is double || num is BigInteger || num is Complex) {
                    return;
                }
                if (num == null ||
                    !PythonOps.HasAttr(context, num, "__int__") &&
                    !PythonOps.HasAttr(context, num, "__float__")) {
                        throw PythonOps.TypeError("a number is required");
                }
            }

            private static IEnumerator<object> IntYielder(count c, int start, int step) {
                int prev;
                for (; ; ) {
                    prev = c._curInt;
                    try {
                        start = checked(start + step);
                    } catch (OverflowException) {
                        break;
                    }

                    c._curInt = start;
                    yield return prev;
                }

                BigInteger startBig = (BigInteger)start + step;
                c._cur = startBig;
                yield return prev;

                for (startBig += step; ; startBig += step) {
                    object prevObj = c._cur;
                    c._cur = startBig;
                    yield return prevObj;
                }
            }

            private static IEnumerator<object> IntYielder(count c, int start, BigInteger step) {
                BigInteger startBig = (BigInteger)start + step;
                c._cur = startBig;
                yield return start;

                for (startBig += step; ; startBig += step) {
                    object prevObj = c._cur;
                    c._cur = startBig;
                    yield return prevObj;
                }
            }

            private static IEnumerator<BigInteger> BigIntYielder(count c, BigInteger start, int step) {
                for (start += step; ; start += step) {
                    BigInteger prev = (BigInteger)c._cur;
                    c._cur = start;
                    yield return prev;
                }
            }

            private static IEnumerator<BigInteger> BigIntYielder(count c, BigInteger start, BigInteger step) {
                for (start += step; ; start += step) {
                    BigInteger prev = (BigInteger)c._cur;
                    c._cur = start;
                    yield return prev;
                }
            }

            private static IEnumerator<object> ObjectYielder(PythonContext context, count c, object start, object step) {
                start = context.Operation(PythonOperationKind.Add, start, step);
                for (; ; start = context.Operation(PythonOperationKind.Add, start, step)) {
                    object prev = c._cur;
                    c._cur = start;
                    yield return prev;
                }
            }

            public PythonTuple __reduce__() {
                PythonTuple args;
                if (StepIsOne()) {
                    args = PythonOps.MakeTuple(_cur == null ? _curInt : _cur);
                } else {
                    args = PythonOps.MakeTuple(_cur == null ? _curInt : _cur, _step);
                }

                return PythonOps.MakeTuple(DynamicHelpers.GetPythonType(this), args);
            }

            public PythonTuple __reduce_ex__([Optional]int protocol) {
                return __reduce__();
            }

            private bool StepIsOne() {
                if (_step is int) {
                    return (int)_step == 1;
                }

                Extensible<int> stepExt;
                if ((stepExt = _step as Extensible<int>) != null) {
                    return stepExt.Value == 1;
                }

                return false;
            }

            #region ICodeFormattable Members

            public string __repr__(CodeContext/*!*/ context) {
                object cur = _cur == null ? _curInt : _cur;

                if (StepIsOne()) {
                    return string.Format("count({0})", PythonOps.Repr(context, cur));
                }

                return string.Format(
                    "count({0}, {1})",
                    PythonOps.Repr(context, cur),
                    PythonOps.Repr(context, _step)
                );
            }

            #endregion
        }

        [PythonType]
        public class cycle : IterBase {
            public cycle(object iterable) {
                InnerEnumerator = Yielder(PythonOps.GetEnumerator(iterable));
            }

            private IEnumerator<object> Yielder(IEnumerator iter) {
                List result = new List();
                while (MoveNextHelper(iter)) {
                    result.AddNoLock(iter.Current);
                    yield return iter.Current;
                }
                if (result.__len__() != 0) {
                    for (; ; ) {
                        for (int i = 0; i < result.__len__(); i++) {
                            yield return result[i];
                        }
                    }
                }
            }
        }

        [PythonType]
        public class dropwhile : IterBase {
            private readonly CodeContext/*!*/ _context;

            public dropwhile(CodeContext/*!*/ context, object predicate, object iterable) {
                _context = context;
                InnerEnumerator = Yielder(predicate, PythonOps.GetEnumerator(iterable));
            }

            private IEnumerator<object> Yielder(object predicate, IEnumerator iter) {
                PythonContext pc = PythonContext.GetContext(_context);

                while (MoveNextHelper(iter)) {
                    if (!Converter.ConvertToBoolean(pc.CallSplat(predicate, iter.Current))) {
                        yield return iter.Current;
                        break;
                    }
                }

                while (MoveNextHelper(iter)) {
                    yield return iter.Current;
                }
            }
        }

        [PythonType]
        public class groupby : IterBase {
            private static readonly object _starterKey = new object();
            private bool _fFinished = false;
            private object _key;
            private readonly CodeContext/*!*/ _context;

            public groupby(CodeContext/*!*/ context, object iterable) {
                InnerEnumerator = Yielder(PythonOps.GetEnumerator(iterable));
                _context = context;
            }

            public groupby(CodeContext/*!*/ context, object iterable, object key) {
                InnerEnumerator = Yielder(PythonOps.GetEnumerator(iterable));
                _context = context;
                if (key != null) {
                    _key = key;
                }
            }

            private IEnumerator<object> Yielder(IEnumerator iter) {
                object curKey = _starterKey;
                if (MoveNextHelper(iter)) {
                    while (!_fFinished) {
                        while (PythonContext.Equal(GetKey(iter.Current), curKey)) {
                            if (!MoveNextHelper(iter)) { 
                                _fFinished = true; 
                                yield break; 
                            }
                        }
                        curKey = GetKey(iter.Current);
                        yield return PythonTuple.MakeTuple(curKey, Grouper(iter, curKey));
                    }
                }
            }

            private IEnumerator<object> Grouper(IEnumerator iter, object curKey) {
                while (PythonContext.Equal(GetKey(iter.Current), curKey)) {
                    yield return iter.Current;
                    if (!MoveNextHelper(iter)) { 
                        _fFinished = true; 
                        yield break; 
                    }
                }
            }

            private object GetKey(object val) {
                if (_key == null) return val;

                return PythonContext.GetContext(_context).CallSplat(_key, val);
            }
        }

        [PythonType]
        public class ifilter : IterBase {
            private readonly CodeContext/*!*/ _context;

            public ifilter(CodeContext/*!*/ context, object predicate, object iterable) {
                _context = context;

                InnerEnumerator = Yielder(predicate, PythonOps.GetEnumerator(iterable));
            }

            private IEnumerator<object> Yielder(object predicate, IEnumerator iter) {
                while (MoveNextHelper(iter)) {
                    if (ShouldYield(predicate, iter.Current)) {
                        yield return iter.Current;
                    }
                }
            }

            private bool ShouldYield(object predicate, object current) {
                if (predicate == null) return PythonOps.IsTrue(current);

                return Converter.ConvertToBoolean(PythonContext.GetContext(_context).CallSplat(predicate, current));
            }
        }

        [PythonType]
        public class ifilterfalse : IterBase {
            private readonly CodeContext/*!*/ _context;

            public ifilterfalse(CodeContext/*!*/ context, object predicate, object iterable) {
                _context = context;
                InnerEnumerator = Yielder(predicate, PythonOps.GetEnumerator(iterable));
            }

            private IEnumerator<object> Yielder(object predicate, IEnumerator iter) {
                while (MoveNextHelper(iter)) {
                    if (ShouldYield(predicate, iter.Current)) {
                        yield return iter.Current;
                    }
                }
            }

            private bool ShouldYield(object predicate, object current) {
                if (predicate == null) return !PythonOps.IsTrue(current);

                return !Converter.ConvertToBoolean(
                    PythonContext.GetContext(_context).CallSplat(predicate, current)
                );
            }
        }

        [PythonType]
        public class imap : IEnumerator {
            private object _function;
            private IEnumerator[] _iterables;
            private readonly CodeContext/*!*/ _context;

            public imap(CodeContext/*!*/ context, object function, params object[] iterables) {
                if (iterables.Length < 1) {
                    throw PythonOps.TypeError("imap() must have at least two arguments");
                }

                _function = function;
                _context = context;                
                _iterables = new IEnumerator[iterables.Length];

                for (int i = 0; i < iterables.Length; i++) {
                    _iterables[i] = PythonOps.GetEnumerator(iterables[i]);
                }
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get {
                    object[] args = new object[_iterables.Length];
                    for (int i = 0; i < args.Length; i++) {
                        args[i] = _iterables[i].Current;
                    }
                    if (_function == null) {
                        return PythonTuple.MakeTuple(args);
                    } else {
                        return PythonContext.GetContext(_context).CallSplat(_function, args);
                    }
                }
            }

            bool IEnumerator.MoveNext() {
                for (int i = 0; i < _iterables.Length; i++) {
                    if (!MoveNextHelper(_iterables[i])) return false;
                }
                return true;
            }

            void IEnumerator.Reset() {
                for (int i = 0; i < _iterables.Length; i++) {
                    _iterables[i].Reset();
                }
            }

            public object __iter__() {
                return this;
            }

            #endregion
        }

        [PythonType]
        public class islice : IterBase {
            public islice(object iterable, object stop)
                : this(iterable, 0, stop, 1) {
            }

            public islice(object iterable, object start, object stop)
                : this(iterable, start, stop, 1) {
            }

            public islice(object iterable, object start, object stop, object step) {
                int startInt = 0, stopInt = -1;

                if (start != null && !Converter.TryConvertToInt32(start, out startInt) || startInt < 0)
                    throw PythonOps.ValueError("start argument must be non-negative integer, ({0})", start);

                if (stop != null) {
                    if (!Converter.TryConvertToInt32(stop, out stopInt) || stopInt < 0)
                        throw PythonOps.ValueError("stop argument must be non-negative integer ({0})", stop);
                }

                int stepInt = 1;
                if (step != null && !Converter.TryConvertToInt32(step, out stepInt) || stepInt <= 0) {
                    throw PythonOps.ValueError("step must be 1 or greater for islice");
                }

                InnerEnumerator = Yielder(PythonOps.GetEnumerator(iterable), startInt, stopInt, stepInt);
            }

            private IEnumerator<object> Yielder(IEnumerator iter, int start, int stop, int step) {
                if (!MoveNextHelper(iter)) yield break;

                int cur = 0;
                while (cur < start) {
                    if (!MoveNextHelper(iter)) yield break;
                    cur++;
                }

                while (cur < stop || stop == -1) {
                    yield return iter.Current;
                    if ((cur + step) < 0) yield break;  // early out if we'll overflow.                    

                    for (int i = 0; i < step; i++) {
                        if ((stop != -1 && ++cur >= stop) || !MoveNextHelper(iter)) {
                            yield break;
                        }
                    }
                }
            }

        }

        [PythonType]
        public class izip : IEnumerator {
            private readonly IEnumerator[]/*!*/ _iters;
            private PythonTuple _current;

            public izip(params object[] iterables) {
                _iters = new IEnumerator[iterables.Length];

                for (int i = 0; i < iterables.Length; i++) {
                    _iters[i] = PythonOps.GetEnumerator(iterables[i]);
                }
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get {
                    return _current;
                }
            }

            bool IEnumerator.MoveNext() {
                if (_iters.Length == 0) return false;

                object[] current = new object[_iters.Length];
                for (int i = 0; i < _iters.Length; i++) {
                    if (!MoveNextHelper(_iters[i])) return false;

                    // values need to be extraced and saved as we move incase
                    // the user passed the same iterable multiple times.
                    current[i] = _iters[i].Current;
                }
                _current = PythonTuple.MakeTuple(current);
                return true;
            }

            void IEnumerator.Reset() {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public object __iter__() {
                return this;
            }

            #endregion
        }

        [PythonType]
        public class izip_longest : IEnumerator {
            private readonly IEnumerator[]/*!*/ _iters;
            private readonly object _fill;
            private PythonTuple _current;

            public izip_longest(params object[] iterables) {
                _iters = new IEnumerator[iterables.Length];

                for (int i = 0; i < iterables.Length; i++) {
                    _iters[i] = PythonOps.GetEnumerator(iterables[i]);
                }
            }

            public izip_longest([ParamDictionary]IDictionary<object, object> paramDict, params object[] iterables) {
                object fill;

                if (paramDict.TryGetValue("fillvalue", out fill)) {
                    _fill = fill;
                    if (paramDict.Count != 1) {
                        paramDict.Remove("fillvalue");
                        throw UnexpectedKeywordArgument(paramDict);
                    }
                } else if (paramDict.Count != 0) {
                    throw UnexpectedKeywordArgument(paramDict);
                }

                _iters = new IEnumerator[iterables.Length];

                for (int i = 0; i < iterables.Length; i++) {
                    _iters[i] = PythonOps.GetEnumerator(iterables[i]);
                }
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get {
                    return _current;
                }
            }

            bool IEnumerator.MoveNext() {
                if (_iters.Length == 0) return false;

                object[] current = new object[_iters.Length];
                bool gotValue = false;
                for (int i = 0; i < _iters.Length; i++) {
                    if (!MoveNextHelper(_iters[i])) {
                        current[i] = _fill;
                    } else {
                        // values need to be extraced and saved as we move incase
                        // the user passed the same iterable multiple times.
                        gotValue = true;
                        current[i] = _iters[i].Current;
                    }
                }
                if (gotValue) {
                    _current = PythonTuple.MakeTuple(current);
                    return true;
                }

                return false;
            }

            void IEnumerator.Reset() {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public object __iter__() {
                return this;
            }

            #endregion
        }

        private static Exception UnexpectedKeywordArgument(IDictionary<object, object> paramDict) {
            foreach (object name in paramDict.Keys) {
                return PythonOps.TypeError("got unexpected keyword argument {0}", name);
            }

            throw new InvalidOperationException();
        }

        [PythonType]
        public class product : IterBase {
            public product(params object[] iterables) {
                InnerEnumerator = Yielder(ArrayUtils.ConvertAll(iterables, x => new List(PythonOps.GetEnumerator(x))));
            }

            public product([ParamDictionary]IDictionary<object, object> paramDict, params object[] iterables) {
                object repeat;
                int iRepeat = 1;
                if (paramDict.TryGetValue("repeat", out repeat)) {
                    if (repeat is int) {
                        iRepeat = (int)repeat;
                    } else {
                        throw PythonOps.TypeError("an integer is required");
                    }

                    if (paramDict.Count != 1) {
                        paramDict.Remove("repeat");
                        throw UnexpectedKeywordArgument(paramDict);
                    }
                } else if (paramDict.Count != 0) {
                    throw UnexpectedKeywordArgument(paramDict);
                }

                List[] finalIterables = new List[iterables.Length * iRepeat];
                for (int i = 0; i < iRepeat; i++) {
                    for (int j = 0; j < iterables.Length; j++) {
                        finalIterables[i * iterables.Length + j] = new List(iterables[j]);
                    }
                }
                InnerEnumerator = Yielder(finalIterables);
            }

            private IEnumerator<object> Yielder(List[] iterables) {
                if (iterables.Length > 0) {
                    IEnumerator[] enums = new IEnumerator[iterables.Length];
                    enums[0] = iterables[0].GetEnumerator();

                    int curDepth = 0;
                    do {
                        if (enums[curDepth].MoveNext()) {
                            if (curDepth == enums.Length - 1) {
                                // create a new array so we don't mutate previous tuples
                                object[] final = new object[enums.Length];
                                for (int j = 0; j < enums.Length; j++) {
                                    final[j] = enums[j].Current;
                                }

                                yield return PythonTuple.MakeTuple(final);
                            } else {
                                // going to the next depth, get a new enumerator
                                curDepth++;
                                enums[curDepth] = iterables[curDepth].GetEnumerator();
                            }
                        } else {
                            // current depth exhausted, go to the previous iterator
                            curDepth--;
                        }
                    } while (curDepth != -1);
                } else {
                    yield return PythonTuple.EMPTY;
                }
            }
        }

        [PythonType]
        public class combinations : IterBase {
            private readonly List _data;

            public combinations(object iterable, object r) {
                _data = new List(iterable);

                InnerEnumerator = Yielder(GetR(r, _data));
            }

            private IEnumerator<object> Yielder(int r) {
                IEnumerator[] enums = new IEnumerator[r];
                if (r > 0) {
                    enums[0] = _data.GetEnumerator();

                    int curDepth = 0;
                    int[] curIndices = new int[enums.Length];
                    do {
                        if (enums[curDepth].MoveNext()) {
                            curIndices[curDepth]++;
                            bool shouldSkip = false;
                            for (int i = 0; i < curDepth; i++) {
                                if (curIndices[i] >= curIndices[curDepth]) {
                                    // skip if we've already seen this index or a higher
                                    // index elsewhere
                                    shouldSkip = true;
                                    break;
                                }
                            }

                            if (!shouldSkip) {
                                if (curDepth == enums.Length - 1) {
                                    // create a new array so we don't mutate previous tuples
                                    object[] final = new object[r];
                                    for (int j = 0; j < enums.Length; j++) {
                                        final[j] = enums[j].Current;
                                    }

                                    yield return PythonTuple.MakeTuple(final);
                                } else {
                                    // going to the next depth, get a new enumerator
                                    curDepth++;
                                    enums[curDepth] = _data.GetEnumerator();
                                    curIndices[curDepth] = 0;
                                }
                            }
                        } else {
                            // current depth exhausted, go to the previous iterator
                            curDepth--;
                        }
                    } while (curDepth != -1);
                } else {
                    yield return PythonTuple.EMPTY;
                }
            }
        }

        [PythonType]
        public class combinations_with_replacement : IterBase {
            private readonly List _data;

            public combinations_with_replacement(object iterable, object r) {
                _data = new List(iterable);

                InnerEnumerator = Yielder(GetR(r, _data));
            }

            private IEnumerator<object> Yielder(int r) {
                IEnumerator[] enums = new IEnumerator[r];
                if (r > 0) {
                    enums[0] = _data.GetEnumerator();

                    int curDepth = 0;
                    int[] curIndices = new int[enums.Length];
                    do {
                        if (enums[curDepth].MoveNext()) {
                            curIndices[curDepth]++;
                            bool shouldSkip = false;
                            for (int i = 0; i < curDepth; i++) {
                                if (curIndices[i] > curIndices[curDepth]) {
                                    // skip if we've already seen a higher index elsewhere
                                    shouldSkip = true;
                                    break;
                                }
                            }

                            if (!shouldSkip) {
                                if (curDepth == enums.Length - 1) {
                                    // create a new array so we don't mutate previous tuples
                                    object[] final = new object[r];
                                    for (int j = 0; j < enums.Length; j++) {
                                        final[j] = enums[j].Current;
                                    }

                                    yield return PythonTuple.MakeTuple(final);
                                } else {
                                    // going to the next depth, get a new enumerator
                                    curDepth++;
                                    enums[curDepth] = _data.GetEnumerator();
                                    curIndices[curDepth] = 0;
                                }
                            }
                        } else {
                            // current depth exhausted, go to the previous iterator
                            curDepth--;
                        }
                    } while (curDepth != -1);
                } else {
                    yield return PythonTuple.EMPTY;
                }
            }
        }

        [PythonType]
        public class permutations : IterBase {
            private readonly List _data;

            public permutations(object iterable) {
                _data = new List(iterable);

                InnerEnumerator = Yielder(_data.Count);
            }

            public permutations(object iterable, object r) {
                _data = new List(iterable);
                
                InnerEnumerator = Yielder(GetR(r, _data));
            }

            private IEnumerator<object> Yielder(int r) {
                if (r > 0) {
                    IEnumerator[] enums = new IEnumerator[r];
                    enums[0] = _data.GetEnumerator();

                    int curDepth = 0;
                    int[] curIndices = new int[enums.Length];
                    do {
                        if (enums[curDepth].MoveNext()) {
                            curIndices[curDepth]++;
                            bool shouldSkip = false;
                            for (int i = 0; i < curDepth; i++) {
                                if (curIndices[i] == curIndices[curDepth]) {
                                    // skip if we're already using this index elsewhere
                                    shouldSkip = true;
                                    break;
                                }
                            }

                            if (!shouldSkip) {
                                if (curDepth == enums.Length - 1) {
                                    // create a new array so we don't mutate previous tuples
                                    object[] final = new object[r];
                                    for (int j = 0; j < enums.Length; j++) {
                                        final[j] = enums[j].Current;
                                    }

                                    yield return PythonTuple.MakeTuple(final);
                                } else {
                                    // going to the next depth, get a new enumerator
                                    curDepth++;
                                    enums[curDepth] = _data.GetEnumerator();
                                    curIndices[curDepth] = 0;
                                }
                            }
                        } else {
                            // current depth exhausted, go to the previous iterator
                            curDepth--;
                        }
                    } while (curDepth != -1);
                } else {
                    yield return PythonTuple.EMPTY;
                }
            }
        }

        private static int GetR(object r, List data) {
            int ri;
            if (r != null) {
                ri = Converter.ConvertToInt32(r);
                if (ri < 0) {
                    throw PythonOps.ValueError("r cannot be negative");
                }
            } else {
                ri = data.Count;
            }
            return ri;
        }

        [PythonType, DontMapICollectionToLen]
        public class repeat : IterBase, ICodeFormattable, ICollection {
            private int _remaining;
            private bool _fInfinite;
            private object _obj;

            public repeat(object @object) {
                _obj = @object;
                InnerEnumerator = Yielder();
                _fInfinite = true;
            }

            public repeat(object @object, int times) {
                _obj = @object;
                InnerEnumerator = Yielder();
                _remaining = times;
            }

            private IEnumerator<object> Yielder() {
                while (_fInfinite || _remaining > 0) {
                    _remaining--;
                    yield return _obj;
                }
            }

            public int __length_hint__() {
                if (_fInfinite) throw PythonOps.TypeError("len() of unsized object");
                return Math.Max(_remaining, 0);
            }

            #region ICodeFormattable Members

            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                if (_fInfinite) {
                    return String.Format("{0}({1})", PythonOps.GetPythonTypeName(this), PythonOps.Repr(context, _obj));
                }
                return String.Format("{0}({1}, {2})", PythonOps.GetPythonTypeName(this), PythonOps.Repr(context, _obj), _remaining);
            }

            #endregion

            #region ICollection Members

            void ICollection.CopyTo(Array array, int index) {
                if (_fInfinite) throw new InvalidOperationException();
                if (_remaining > array.Length - index) {
                    throw new IndexOutOfRangeException();
                }
                for (int i = 0; i < _remaining; i++) {
                    array.SetValue(_obj, index + i);
                }
                _remaining = 0;
            }

            int ICollection.Count {
                get { return __length_hint__(); }
            }

            bool ICollection.IsSynchronized {
                get { return false; }
            }

            object ICollection.SyncRoot {
                get { return this; }
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator() {
                while (_fInfinite || _remaining > 0) {
                    _remaining--;
                    yield return _obj;
                }
            }

            #endregion
        }
        
        [PythonType]
        public class starmap : IterBase {            
            public starmap(CodeContext context, object function, object iterable) {
                InnerEnumerator = Yielder(context, function, PythonOps.GetEnumerator(iterable));
            }

            private IEnumerator<object> Yielder(CodeContext context, object function, IEnumerator iter) {
                PythonContext pc = PythonContext.GetContext(context);

                while (MoveNextHelper(iter)) {
                    PythonTuple args = iter.Current as PythonTuple;
                    object[] objargs;
                    if (args != null) {
                        objargs = new object[args.__len__()];
                        for (int i = 0; i < objargs.Length; i++) {
                            objargs[i] = args[i];
                        }
                    } else {
                        List argsList = new List(PythonOps.GetEnumerator(iter.Current));
                        objargs = ArrayUtils.ToArray(argsList);
                    }

                    yield return pc.CallSplat(function, objargs);
                }
            }
        }

        [PythonType]
        public class takewhile : IterBase {
            private readonly CodeContext/*!*/ _context;

            public takewhile(CodeContext/*!*/ context, object predicate, object iterable) {
                _context = context;

                InnerEnumerator = Yielder(predicate, PythonOps.GetEnumerator(iterable));
            }

            private IEnumerator<object> Yielder(object predicate, IEnumerator iter) {
                while (MoveNextHelper(iter)) {
                    if(!Converter.ConvertToBoolean(
                        PythonContext.GetContext(_context).CallSplat(predicate, iter.Current)
                    )) {
                        break;
                    }

                    yield return iter.Current;
                }
            }
        }

        [PythonHidden]
        public class TeeIterator : IEnumerator, IWeakReferenceable {
            internal IEnumerator _iter;
            internal List _data;
            private int _curIndex = -1;
            private WeakRefTracker _weakRef;

            public TeeIterator(object iterable) {
                TeeIterator other = iterable as TeeIterator;
                if (other != null) {
                    this._iter = other._iter;
                    this._data = other._data;
                } else {
                    this._iter = PythonOps.GetEnumerator(iterable);
                    _data = new List();
                }
            }

            public TeeIterator(IEnumerator iter, List dataList) {
                this._iter = iter;
                this._data = dataList;
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get {
                    return _data[_curIndex];
                }
            }

            bool IEnumerator.MoveNext() {
                lock (_data) {
                    _curIndex++;
                    if (_curIndex >= _data.__len__() && MoveNextHelper(_iter)) {
                        _data.append(_iter.Current);
                    }
                    return _curIndex < _data.__len__();
                }
            }

            void IEnumerator.Reset() {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public object __iter__() {
                return this;
            }

            #endregion

            #region IWeakReferenceable Members

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return (_weakRef);
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _weakRef = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _weakRef = value;
            }

            #endregion
        }

        private static bool MoveNextHelper(IEnumerator move) {
            try { return move.MoveNext(); } catch (IndexOutOfRangeException) { return false; } catch (StopIterationException) { return false; }
        }
    }
}
