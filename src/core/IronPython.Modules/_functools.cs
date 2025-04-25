// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("_functools", typeof(IronPython.Modules.FunctionTools))]
namespace IronPython.Modules {
    public static class FunctionTools {
        public const string __doc__ = "provides functionality for manipulating callable objects";

        public static object? reduce(CodeContext/*!*/ context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object?, object?, object?, object?>>> siteData, object? func, object? seq) {
            IEnumerator i = PythonOps.GetEnumerator(context, seq);
            if (!i.MoveNext()) {
                throw PythonOps.TypeError("reduce() of empty sequence with no initial value");
            }
            EnsureReduceData(context, siteData);

            CallSite<Func<CallSite, CodeContext, object?, object?, object?, object?>> site = siteData.Data;

            object? ret = i.Current;
            while (i.MoveNext()) {
                ret = site.Target(site, context, func, ret, i.Current);
            }
            return ret;
        }

        public static object? reduce(CodeContext/*!*/ context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object?, object?, object?, object?>>> siteData, object? func, object? seq, object? initializer) {
            IEnumerator i = PythonOps.GetEnumerator(context, seq);
            EnsureReduceData(context, siteData);

            CallSite<Func<CallSite, CodeContext, object?, object?, object?, object?>> site = siteData.Data;

            object? ret = initializer;
            while (i.MoveNext()) {
                ret = site.Target(site, context, func, ret, i.Current);
            }
            return ret;
        }

        private static void EnsureReduceData(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object?, object?, object?, object?>>> siteData) {
            if (siteData.Data == null) {
                siteData.Data = CallSite<Func<CallSite, CodeContext, object?, object?, object?, object?>>.Create(
                    context.LanguageContext.Invoke(
                        new CallSignature(2)
                    )
                );
            }
        }

        /// <summary>
        /// Returns a new callable object with the provided initial set of arguments
        /// bound to it.  Calling the new function then appends to the additional
        /// user provided arguments.
        /// </summary>
        [PythonType]
        public class partial : IWeakReferenceable {
            private const string _defaultDoc = "partial(func, *args, **keywords) - new function with partial application\n    of the given arguments and keywords.\n";

            private readonly CodeContext/*!*/ _context;                                             // code context from the caller who created us
            private object?[]/*!*/ _args;                                                           // the initially provided arguments
            private IDictionary<object, object?> _keywordArgs;                                      // the initially provided keyword arguments

            private CallSite<Func<CallSite, CodeContext, object, object?[], IDictionary<object, object?>, object>>? _dictSite; // the dictionary call site if ever called w/ keyword args
            private CallSite<Func<CallSite, CodeContext, object, object?[], object>>? _splatSite;   // the position only call site
            private PythonDictionary? _dict;                                                        // dictionary for storing extra attributes
            private WeakRefTracker? _tracker;                                                       // tracker so users can use Python weak references
            private string? _doc;                                                                   // A custom docstring, if used

            #region Constructors

            /// <summary>
            /// Creates a new partial object with the provided positional arguments.
            /// </summary>
            public partial(CodeContext/*!*/ context, object? func, [NotNone] params object[] args)
                : this(context, func, new PythonDictionary(), args) {
            }

            /// <summary>
            /// Creates a new partial object with the provided positional and keyword arguments.
            /// </summary>
            public partial(CodeContext/*!*/ context, object? func, [ParamDictionary] IDictionary<object, object> keywords, [NotNone] params object[] args) {
                if (!PythonOps.IsCallable(context, func)) {
                    throw PythonOps.TypeError("the first argument must be callable");
                }

                this.func = func;
                _keywordArgs = new PythonDictionary(keywords);
                _args = args;
                _context = context;
            }

            #endregion

            #region Public Python API

            [SpecialName, PropertyMethod, WrapperDescriptor]
            public static object Get__doc__(CodeContext context, [NotNone] partial self) {
                return self._doc ?? _defaultDoc;
            }

            [SpecialName, PropertyMethod, WrapperDescriptor]
            public static void Set__doc__([NotNone] partial self, object? value) {
                self._doc = value as string;
            }

            /// <summary>
            /// Gets the function which will be called.
            /// </summary>
            public object func { get; private set; }

            /// <summary>
            /// Gets the initially provided positional arguments.
            /// </summary>
            public object args => PythonTuple.MakeTuple(_args);

            /// <summary>
            /// Gets the initially provided keyword arguments.
            /// </summary>
            public object keywords {
                get {
                    return _keywordArgs;
                }
            }

            /// <summary>
            /// Gets or sets the dictionary used for storing extra attributes on the partial object.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
            public PythonDictionary __dict__ {
                get {
                    return EnsureDict();
                }
                [param: NotNone]
                set {
                    _dict = value;
                }
            }

            [SpecialName, PropertyMethod]
            public void Delete__dict__() {
                throw PythonOps.TypeError("partial's dictionary may not be deleted");
            }

            // This exists for subtypes because we don't yet automap DeleteMember onto __delattr__
            public void __delattr__([NotNone] string name) {
                if (name == "__dict__") Delete__dict__();

                _dict?.Remove(name);
            }

            public PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(
                    DynamicHelpers.GetPythonTypeFromType(typeof(partial)),
                    PythonTuple.MakeTuple(func),
                    PythonTuple.MakeTuple(func, args, keywords, __dict__)
                );
            }

            public void __setstate__(CodeContext context, [NotNone] PythonTuple state) {
                if (state.Count == 4
                        && PythonOps.IsCallable(context, state[0])
                        && state[1] is PythonTuple args
                        && state[2] is PythonDictionary keywords) {
                    func = state[0]!;
                    _args = args._data;
                    _keywordArgs = keywords;
                    if (state[3] is PythonDictionary dict)
                        __dict__.update(context, dict);
                    else if (!(state[3] is null)) {
                        throw PythonOps.TypeError("invalid partial state");
                    }
                } else {
                    throw PythonOps.TypeError("invalid partial state");
                }
            }

            public string __repr__(CodeContext context) {
                var infinite = PythonOps.GetAndCheckInfinite(this);
                if (infinite == null) {
                    return "...";
                }

                int infiniteIndex = infinite.Count;
                infinite.Add(this);
                try {
                    var builder = new StringBuilder();
                    builder.Append("functools.partial(");
                    builder.Append(PythonOps.Repr(context, func));
                    foreach (var x in _args) {
                        builder.Append(", ");
                        builder.Append(PythonOps.Repr(context, x));
                    }
                    foreach (var p in _keywordArgs) {
                        builder.Append(", ");
                        builder.Append(p.Key);
                        builder.Append('=');
                        builder.Append(PythonOps.Repr(context, p.Value));
                    }
                    builder.Append(')');
                    return builder.ToString();
                } finally {
                    System.Diagnostics.Debug.Assert(infiniteIndex == infinite.Count - 1);
                    infinite.RemoveAt(infiniteIndex);
                }
            }

            #endregion

            #region Operator methods

            /// <summary>
            /// Calls func with the previously provided arguments and more positional arguments.
            /// </summary>
            [SpecialName]
            public object? Call(CodeContext/*!*/ context, [NotNone] params object?[] args) {
                if (_keywordArgs == null) {
                    EnsureSplatSite();
                    return _splatSite!.Target(_splatSite, context, func, ArrayUtils.AppendRange(_args, args));
                }

                EnsureDictSplatSite();
                return _dictSite!.Target(_dictSite, context, func, ArrayUtils.AppendRange(_args, args), _keywordArgs);
            }

            /// <summary>
            /// Calls func with the previously provided arguments and more positional arguments and keyword arguments.
            /// </summary>
            [SpecialName]
            public object? Call(CodeContext/*!*/ context, [ParamDictionary] IDictionary<object, object?> dict, [NotNone] params object?[] args) {

                IDictionary<object, object?> finalDict;
                if (_keywordArgs != null) {
                    PythonDictionary pd = new PythonDictionary();
                    pd.update(context, _keywordArgs);
                    pd.update(context, dict);

                    finalDict = pd;
                } else {
                    finalDict = dict;
                }

                EnsureDictSplatSite();
                return _dictSite!.Target(_dictSite, context, func, ArrayUtils.AppendRange(_args, args), finalDict);
            }

            /// <summary>
            /// Operator method to set arbitrary members on the partial object.
            /// </summary>
            [SpecialName]
            public void SetMemberAfter(CodeContext/*!*/ context, [NotNone] string name, object? value) {
                EnsureDict();

                _dict![name] = value;
            }

            /// <summary>
            /// Operator method to get additional arbitrary members defined on the partial object.
            /// </summary>
            [SpecialName]
            public object GetBoundMember(CodeContext/*!*/ context, [NotNone] string name) {
                if (_dict != null && _dict.TryGetValue(name, out object value)) {
                    return value;
                }
                return OperationFailed.Value;
            }

            /// <summary>
            /// Operator method to delete arbitrary members defined in the partial object.
            /// </summary>
            [SpecialName]
            public bool DeleteMember(CodeContext/*!*/ context, [NotNone] string name) {
                switch (name) {
                    case "__dict__":
                        Delete__dict__();
                        break;
                }

                if (_dict == null) return false;

                return _dict.Remove(name);
            }

            #endregion

            #region Internal implementation details

            private void EnsureSplatSite() {
                if (_splatSite == null) {
                    Interlocked.CompareExchange(
                        ref _splatSite,
                        CallSite<Func<CallSite, CodeContext, object, object?[], object>>.Create(
                            Binders.InvokeSplat(_context.LanguageContext)
                        ),
                        null
                    );
                }
            }

            private void EnsureDictSplatSite() {
                if (_dictSite == null) {
                    Interlocked.CompareExchange(
                        ref _dictSite,
                        CallSite<Func<CallSite, CodeContext, object, object?[], IDictionary<object, object?>, object>>.Create(
                            Binders.InvokeKeywords(_context.LanguageContext)
                        ),
                        null
                    );
                }
            }

            private PythonDictionary EnsureDict() {
                if (_dict == null) {
                    _dict = PythonDictionary.MakeSymbolDictionary();
                }
                return _dict;
            }

            #endregion

            #region IWeakReferenceable Members

            WeakRefTracker? IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                return Interlocked.CompareExchange(ref _tracker, value, null) == null;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _tracker = value;
            }

            #endregion
        }
    }
}
