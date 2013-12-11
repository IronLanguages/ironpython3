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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    using Ast = Expression;
    using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

    /// <summary>
    /// Created for a user-defined function.  
    /// </summary>
    [PythonType("function"), DontMapGetMemberNamesToDir, DebuggerDisplay("function {__name__} in {__module__}")]
    public sealed partial class PythonFunction : PythonTypeSlot, IWeakReferenceable, IPythonMembersList, IDynamicMetaObjectProvider, ICodeFormattable, Binding.IFastInvokable {
        private readonly CodeContext/*!*/ _context;     // the creating code context of the function
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        [PythonHidden]
        public readonly MutableTuple Closure;

        private object[]/*!*/ _defaults;                // the default parameters of the method
        internal PythonDictionary _dict;                // a dictionary to story arbitrary members on the function object
        private object _module;                         // the module name

        internal int _id, _compat;                      // ID/Compat flags used for testing in rules
        private FunctionCode _code;                     // the Python function code object.  Not currently used for much by us...        
        private string _name;                           // the name of the method
        private object _doc;                            // the current documentation string

        private static int[] _depth_fast = new int[20]; // hi-perf thread static data to avoid hitting a real thread static
        [ThreadStatic] private static int DepthSlow;    // current depth stored in a real thread static with fast depth runs out
        [MultiRuntimeAware]
        private static int _CurrentId = 1;              // The current ID for functions which are called in complex ways.

        /// <summary>
        /// Python ctor - maps to function.__new__
        /// 
        /// y = func(x.__code__, globals(), 'foo', None, (a, ))
        /// </summary>
        public PythonFunction(CodeContext context, FunctionCode code, PythonDictionary globals, string name, PythonTuple defaults, PythonTuple closure) {
            throw new NotImplementedException();
        }

        internal PythonFunction(CodeContext/*!*/ context, FunctionCode funcInfo, object modName, object[] defaults, MutableTuple closure) {
            Assert.NotNull(context, funcInfo);

            _context = context;
            _defaults = defaults ?? ArrayUtils.EmptyObjects;
            _code = funcInfo;
            _doc = funcInfo._initialDoc;
            _name = funcInfo.co_name;

            Debug.Assert(_defaults.Length <= _code.co_argcount);
            if (modName != Uninitialized.Instance) {
                _module = modName;
            }

            Closure = closure;
            _compat = CalculatedCachedCompat();
        }

        #region Public APIs

        public object __globals__ {
            get {
                return func_globals;
            }
            set {
                throw PythonOps.TypeError("readonly attribute");
            }
        }

        public object func_globals {
            get {
                return _context.GlobalDict;
            }
            set {
                throw PythonOps.TypeError("readonly attribute");
            }
        }

        [PropertyMethod, SpecialName, System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void Deletefunc_globals() {
            throw PythonOps.TypeError("readonly attribute");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonTuple __defaults__ {
            get {
                return func_defaults;
            }
            set {
                func_defaults = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonTuple func_defaults {
            get {
                if (_defaults.Length == 0) return null;

                return new PythonTuple(_defaults);
            }
            set {
                if (value == null) {
                    _defaults = ArrayUtils.EmptyObjects;
                } else {
                    _defaults = value.ToArray();
                }
                _compat = CalculatedCachedCompat();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonTuple __closure__ {
            get {
                return func_closure;
            }
            set {
                func_closure = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonTuple func_closure {
            get {
                var storage = (Context.Dict._storage as RuntimeVariablesDictionaryStorage);
                if (storage != null) {
                    object[] res = new object[storage.Names.Length];
                    for (int i = 0; i < res.Length; i++) {
                        res[i] = storage.GetCell(i);
                    }
                    return PythonTuple.MakeTuple(res);
                }

                return null;
            }
            set {
                throw PythonOps.TypeError("readonly attribute");
            }
        }

        public string __name__ {
            get { return func_name; }
            set { func_name = value; }
        }

        public string func_name {
            get { return _name; }
            set {
                if (value == null) {
                    throw PythonOps.TypeError("func_name must be set to a string object");
                }
                _name = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonDictionary __dict__ {
            get { return func_dict; }
            set { func_dict = value; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonDictionary/*!*/ func_dict {
            get { return EnsureDict(); }
            set {
                if (value == null) throw PythonOps.TypeError("setting function's dictionary to non-dict");

                _dict = value;
            }
        }

        public object __doc__ {
            get { return _doc; }
            set { _doc = value; }
        }

        public object func_doc {
            get { return __doc__; }
            set { __doc__ = value; }
        }

        public object __module__ {
            get { return _module; }
            set { _module = value; }
        }

        public FunctionCode __code__ {
            get {
                return func_code;
            }
            set {
                func_code = value;
            }
        }

        public FunctionCode func_code {
            get {
                return _code; 
            }
            set {
                if (value == null) {
                    throw PythonOps.TypeError("func_code must be set to a code object");
                }
                _code = value;
                _compat = CalculatedCachedCompat();
            }
        }

        public object __call__(CodeContext/*!*/ context, params object[] args) {
            return PythonCalls.Call(context, this, args);
        }

        public object __call__(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            return PythonCalls.CallWithKeywordArgs(context, this, args, dict);
        }

        #endregion

        #region Internal APIs

        internal SourceSpan Span {
            get { return func_code.Span; }
        }

        internal string[] ArgNames {
            get { return func_code.ArgNames; }
        }

        /// <summary>
        /// The parent CodeContext in which this function was declared.
        /// </summary>
        internal CodeContext Context {
            get {
                return _context;
            }
        }

        internal string GetSignatureString() {
            StringBuilder sb = new StringBuilder(__name__);
            sb.Append('(');
            for (int i = 0; i < _code.ArgNames.Length; i++) {
                if (i != 0) sb.Append(", ");

                if (i == ExpandDictPosition) {
                    sb.Append("**");
                } else if (i == ExpandListPosition) {
                    sb.Append("*");
                }

                sb.Append(ArgNames[i]);

                if (i < NormalArgumentCount) {
                    int noDefaults = NormalArgumentCount - Defaults.Length; // number of args w/o defaults
                    if (i - noDefaults >= 0) {
                        sb.Append('=');
                        sb.Append(PythonOps.Repr(Context, Defaults[i - noDefaults]));
                    }
                }
            }
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// Captures the # of args and whether we have kw / arg lists.  This
        /// enables us to share sites for simple calls (calls that don't directly
        /// provide named arguments or the list/dict params).
        /// </summary>
        internal int FunctionCompatibility {
            get {
                return _compat;
            }
        }

        /// <summary>
        /// Calculates the _compat value which is used for call-compatibility checks
        /// for simple calls.  Whenver any of the dependent values are updated this
        /// must be called again.
        /// 
        /// The dependent values include:
        ///     _nparams - this is readonly, and never requies an update
        ///     _defaults - the user can mutate this (func_defaults) and that forces
        ///                 an update
        ///     expand dict/list - based on nparams and flags, both read-only
        ///     
        /// Bits are allocated as:
        ///     00003fff - Normal argument count
        ///     0fffb000 - Default count
        ///     10000000 - unused
        ///     20000000 - expand list
        ///     40000000 - expand dict
        ///     80000000 - unused
        ///     
        /// Enforce recursion is added at runtime.
        /// </summary>
        private int CalculatedCachedCompat() {
            return NormalArgumentCount |
                Defaults.Length << 14 |
                ((ExpandDictPosition != -1) ? 0x40000000 : 0) |
                ((ExpandListPosition != -1) ? 0x20000000 : 0);
        }

        /// <summary>
        /// Generators w/ exception handling need to have some data stored
        /// on them so that we appropriately set/restore the exception state.
        /// </summary>
        internal bool IsGeneratorWithExceptionHandling {
            get {
                return ((_code.Flags & (FunctionAttributes.CanSetSysExcInfo | FunctionAttributes.Generator)) == (FunctionAttributes.CanSetSysExcInfo | FunctionAttributes.Generator));
            }
        }

        /// <summary>
        /// Returns an ID for the function if one has been assigned, or zero if the
        /// function has not yet required the use of an ID.
        /// </summary>
        internal int FunctionID {
            get {
                return _id;
            }
        }

        /// <summary>
        /// Gets the position for the expand list argument or -1 if the function doesn't have an expand list parameter.
        /// </summary>
        internal int ExpandListPosition {
            get {
                if ((_code.Flags & FunctionAttributes.ArgumentList) != 0) {
                    return _code.co_argcount;
                }

                return -1;
            }
        }

        /// <summary>
        /// Gets the position for the expand dictionary argument or -1 if the function doesn't have an expand dictionary parameter.
        /// </summary>
        internal int ExpandDictPosition {
            get {
                if ((_code.Flags & FunctionAttributes.KeywordDictionary) != 0) {
                    if ((_code.Flags & FunctionAttributes.ArgumentList) != 0) {
                        return _code.co_argcount + 1;
                    }
                    return _code.co_argcount;
                }
                return -1;
            }
        }

        /// <summary>
        /// Gets the number of normal (not params or kw-params) parameters.
        /// </summary>
        internal int NormalArgumentCount {
            get {
                return _code.co_argcount;
            }
        }

        /// <summary>
        /// Gets the number of extra arguments (params or kw-params)
        /// </summary>
        internal int ExtraArguments {
            get {
                if ((_code.Flags & FunctionAttributes.ArgumentList) != 0) {
                    if ((_code.Flags & FunctionAttributes.KeywordDictionary) != 0) {
                        return 2;
                    }
                    return 1;

                } else if ((_code.Flags & FunctionAttributes.KeywordDictionary) != 0) {
                    return 1;
                }
                return 0;
            }
        }

        internal FunctionAttributes Flags {
            get {
                return _code.Flags;
            }
        }

        internal object[] Defaults {
            get { return _defaults; }
        }

        internal Exception BadArgumentError(int count) {
            return BinderOps.TypeErrorForIncorrectArgumentCount(__name__, NormalArgumentCount, Defaults.Length, count, ExpandListPosition != -1, false);
        }

        internal Exception BadKeywordArgumentError(int count) {
            return BinderOps.TypeErrorForIncorrectArgumentCount(__name__, NormalArgumentCount, Defaults.Length, count, ExpandListPosition != -1, true);
        }

        #endregion
       
        #region Custom member lookup operators

        IList<string> IMembersList.GetMemberNames() {
            return PythonOps.GetStringMemberList(this);
        }

        IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
            List list;
            if (_dict == null) {
                list = PythonOps.MakeList();
            } else {
                list = PythonOps.MakeListFromSequence(_dict);
            }
            list.AddNoLock("__module__");

            list.extend(TypeCache.Function.GetMemberNames(context, this));
            return list;
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            if (_dict != null) {
                object weakRef;
                if (_dict.TryGetValue("__weakref__", out weakRef)) {
                    return weakRef as WeakRefTracker;
                }
            }
            return null;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            EnsureDict();
            _dict["__weakref__"] = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            ((IWeakReferenceable)this).SetWeakRef(value);
        }

        #endregion

        #region Private APIs

        internal PythonDictionary EnsureDict() {
            if (_dict == null) {
                Interlocked.CompareExchange(ref _dict, PythonDictionary.MakeSymbolDictionary(), null);
            }
            return _dict;
        }
        
        internal static int AddRecursionDepth(int change) {
            // ManagedThreadId starts at 1 and increases as we get more threads.
            // Therefore we keep track of a limited number of threads in an array
            // that only gets created once, and we access each of the elements
            // from only a single thread.
            uint tid = (uint)Thread.CurrentThread.ManagedThreadId;

            if (tid < _depth_fast.Length) {
                return _depth_fast[tid] += change;
            } else {
                return DepthSlow += change;
            }
        }

        internal void EnsureID() {
            if (_id == 0) {
                Interlocked.CompareExchange(ref _id, Interlocked.Increment(ref _CurrentId), 0);
            }
        }

        #endregion

        #region PythonTypeSlot Overrides

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            value = new Method(this, instance, owner);
            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<function {0} at {1}>", func_name, PythonOps.HexId(this));
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter) {
            return new Binding.MetaPythonFunction(parameter, BindingRestrictions.Empty, this);
        }

        #endregion
    }

    [PythonType("cell")]
    public sealed class ClosureCell : ICodeFormattable
#if CLR2
        , IValueEquality
#endif
    {
        [PythonHidden]
        public object Value;

        internal ClosureCell(object value) {
            Value = value;
        }

        public object cell_contents {
            get {
                if (Value == Uninitialized.Instance) {
                    throw PythonOps.ValueError("cell is empty");
                }

                return Value;
            }
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return String.Format("<cell at {0}: {1}>",
                IdDispenser.GetId(this),
                GetContentsRepr()
                );
        }

        private string GetContentsRepr() {
            if (Value == Uninitialized.Instance) {
                return "empty";
            }

            return String.Format("{0} object at {1}",
                PythonTypeOps.GetName(Value),
                IdDispenser.GetId(Value));
        }

        #endregion

        #region IValueEquality Members

        public const object __hash__ = null;

#if CLR2
        int IValueEquality.GetValueHashCode() {
            throw PythonOps.TypeError("unhashable type: cell");
        }

        bool IValueEquality.ValueEquals(object other) {
            return __cmp__(other) == 0;
        }
#endif
        #endregion

        [Python3Warning("cell comparisons not supported in 3.x")]
        public int __cmp__(object other) {
            ClosureCell cc = other as ClosureCell;
            if (cc == null) throw PythonOps.TypeError("cell.__cmp__(x,y) expected cell, got {0}", PythonTypeOps.GetName(other));

            return PythonOps.Compare(Value, cc.Value);
        }
    }
}
