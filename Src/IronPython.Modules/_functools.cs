﻿/* ****************************************************************************
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
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using System.Collections;
using Microsoft.Scripting.Actions;

[assembly: PythonModule("_functools", typeof(IronPython.Modules.FunctionTools))]
namespace IronPython.Modules {
    public static class FunctionTools {
        public const string __doc__ = "provides functionality for manipulating callable objects";

        public static object reduce(CodeContext/*!*/ context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object, object, object>>> siteData, object func, object seq) {
            IEnumerator i = PythonOps.GetEnumerator(seq);
            if (!i.MoveNext()) {
                throw PythonOps.TypeError("reduce() of empty sequence with no initial value");
            }
            EnsureReduceData(context, siteData);

            CallSite<Func<CallSite, CodeContext, object, object, object, object>> site = siteData.Data;

            object ret = i.Current;
            while (i.MoveNext()) {
                ret = site.Target(site, context, func, ret, i.Current);
            }
            return ret;
        }

        public static object reduce(CodeContext/*!*/ context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object, object, object>>> siteData, object func, object seq, object initializer) {
            IEnumerator i = PythonOps.GetEnumerator(seq);
            EnsureReduceData(context, siteData);

            CallSite<Func<CallSite, CodeContext, object, object, object, object>> site = siteData.Data;

            object ret = initializer;
            while (i.MoveNext()) {
                ret = site.Target(site, context, func, ret, i.Current);
            }
            return ret;
        }

        private static void EnsureReduceData(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object, object, object>>> siteData) {
            if (siteData.Data == null) {
                siteData.Data = CallSite<Func<CallSite, CodeContext, object, object, object, object>>.Create(
                    PythonContext.GetContext(context).Invoke(
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

            private object/*!*/ _function;                                                  // the callable function to dispatch to
            private object[]/*!*/ _args;                                                    // the initially provided arguments
            private IDictionary<object, object> _keywordArgs;                               // the initially provided keyword arguments or null
            private CodeContext/*!*/ _context;                                              // code context from the caller who created us
            private CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>> _dictSite; // the dictionary call site if ever called w/ keyword args
            private CallSite<Func<CallSite, CodeContext, object, object[], object>> _splatSite;      // the position only call site
            private PythonDictionary _dict;                                                 // dictionary for storing extra attributes
            private WeakRefTracker _tracker;                                                // tracker so users can use Python weak references
            private string _doc;                                                            // A custom docstring, if used

            #region Constructors

            /// <summary>
            /// Creates a new partial object with the provided positional arguments.
            /// </summary>
            public partial(CodeContext/*!*/ context, object func, [NotNull]params object[]/*!*/ args)
                : this(context, func, null, args) {
            }

            /// <summary>
            /// Creates a new partial object with the provided positional and keyword arguments.
            /// </summary>
            public partial(CodeContext/*!*/ context, object func, [ParamDictionary]IDictionary<object, object> keywords, [NotNull]params object[]/*!*/ args) {
                if (!PythonOps.IsCallable(context, func)) {
                    throw PythonOps.TypeError("the first argument must be callable");
                }

                _function = func;
                _keywordArgs = keywords;
                _args = args;
                _context = context;
            }

            #endregion

            #region Public Python API

            [SpecialName, PropertyMethod, WrapperDescriptor]
            public static object Get__doc__(CodeContext context, partial self) {
                return self._doc ?? _defaultDoc;
            }

            [SpecialName, PropertyMethod, WrapperDescriptor]
            public static void Set__doc__(partial self, object value) {
                self._doc = value as string;
            }

            /// <summary>
            /// Gets the function which will be called
            /// </summary>
            public object func {
                get {
                    return _function;
                }
            }

            /// <summary>
            /// Gets the initially provided positional arguments.
            /// </summary>
            public object args {
                get {
                    return PythonTuple.MakeTuple(_args);
                }
            }

            /// <summary>
            /// Gets the initially provided keyword arguments or None.
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
                set {
                    _dict = value;
                }
            }

            [SpecialName, PropertyMethod]
            public void Delete__dict__() {
                throw PythonOps.TypeError("partial's dictionary may not be deleted");
            }

            // This exists for subtypes because we don't yet automap DeleteMember onto __delattr__
            public void __delattr__(string name) {
                if (name == "__dict__") Delete__dict__();

                if (_dict != null) {
                    _dict.Remove(name);
                }
            }

            #endregion

            #region Operator methods

            /// <summary>
            /// Calls func with the previously provided arguments and more positional arguments.
            /// </summary>
            [SpecialName]
            public object Call(CodeContext/*!*/ context, params object[] args) {
                if (_keywordArgs == null) {
                    EnsureSplatSite();
                    return _splatSite.Target(_splatSite, context, _function, ArrayUtils.AppendRange(_args, args));
                }

                EnsureDictSplatSite();
                return _dictSite.Target(_dictSite, context, _function, ArrayUtils.AppendRange(_args, args), _keywordArgs);
            }

            /// <summary>
            /// Calls func with the previously provided arguments and more positional arguments and keyword arguments.
            /// </summary>
            [SpecialName]
            public object Call(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {

                IDictionary<object, object> finalDict;
                if (_keywordArgs != null) {
                    PythonDictionary pd = new PythonDictionary();
                    pd.update(context, _keywordArgs);
                    pd.update(context, dict);

                    finalDict = pd;
                } else {
                    finalDict = dict;
                }

                EnsureDictSplatSite();
                return _dictSite.Target(_dictSite, context, _function, ArrayUtils.AppendRange(_args, args), finalDict);
            }

            /// <summary>
            /// Operator method to set arbitrary members on the partial object.
            /// </summary>
            [SpecialName]
            public void SetMemberAfter(CodeContext/*!*/ context, string name, object value) {
                EnsureDict();

                _dict[name] = value;
            }

            /// <summary>
            /// Operator method to get additional arbitrary members defined on the partial object.
            /// </summary>
            [SpecialName]
            public object GetBoundMember(CodeContext/*!*/ context, string name) {
                object value;
                if (_dict != null && _dict.TryGetValue(name, out value)) {
                    return value;
                }
                return OperationFailed.Value;
            }

            /// <summary>
            /// Operator method to delete arbitrary members defined in the partial object.
            /// </summary>
            [SpecialName]
            public bool DeleteMember(CodeContext/*!*/ context, string name) {
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
                        CallSite<Func<CallSite, CodeContext, object, object[], object>>.Create(
                            Binders.InvokeSplat(PythonContext.GetContext(_context))
                        ),
                        null
                    );
                }
            }

            private void EnsureDictSplatSite() {
                if (_dictSite == null) {
                    Interlocked.CompareExchange(
                        ref _dictSite,
                        CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>>.Create(
                            Binders.InvokeKeywords(PythonContext.GetContext(_context))
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

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                return Interlocked.CompareExchange<WeakRefTracker>(ref _tracker, value, null) == null;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _tracker = value;
            }

            #endregion
        }
    }
}
