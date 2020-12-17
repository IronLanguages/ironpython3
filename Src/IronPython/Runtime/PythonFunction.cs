// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

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
using IronPython.Compiler.Ast;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    using Ast = MSAst.Expression;
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

        internal int _id;                               // ID flag used for testing in rules
        private FunctionCode _code;                     // the Python function code object.  Not currently used for much by us...        
        private string _name;                           // the name of the method
        private object _doc;                            // the current documentation string
        private PythonDictionary _annotations;          // annotations for the function

        private static int[] _depth_fast = new int[20]; // hi-perf thread static data to avoid hitting a real thread static
        [ThreadStatic] private static int DepthSlow;    // current depth stored in a real thread static with fast depth runs out
        [MultiRuntimeAware]
        private static int _CurrentId = 1;              // The current ID for functions which are called in complex ways.

        /// <summary>
        /// Python ctor - maps to function.__new__
        /// 
        /// y = func(x.__code__, globals(), 'foo', None, (a, ))
        /// </summary>
        public PythonFunction(CodeContext context, FunctionCode code, PythonDictionary globals, string name = null, PythonTuple defaults = null, PythonTuple closure = null) {
            if (closure != null && closure.__len__() != 0) {
                throw new NotImplementedException("non empty closure argument is not supported");
            }

            if (globals == context.GlobalDict) {
                _module = context.Module.GetName();
                _context = context;
            } else {
                _module = null;
                _context = new CodeContext(new PythonDictionary(), new ModuleContext(globals, DefaultContext.DefaultPythonContext));
            }

            _defaults = defaults == null ? ArrayUtils.EmptyObjects : defaults.ToArray();
            _code = code;
            _doc = code._initialDoc;
            _name = name ?? code.PythonCode.Name;
            _annotations = new PythonDictionary();

            Closure = null;

            var scopeStatement = _code.PythonCode;
            if (scopeStatement.IsClosure) {
                throw new NotImplementedException("code containing closures is not supported");
            }
            scopeStatement.RewriteBody(FunctionDefinition.ArbitraryGlobalsVisitorInstance);

            FunctionCompatibility = CalculatedCachedCompat();
        }

        internal PythonFunction(CodeContext/*!*/ context, FunctionCode funcInfo, object modName, object[] defaults, PythonDictionary kwdefaults, PythonDictionary annotations, MutableTuple closure) {
            Assert.NotNull(context, funcInfo);

            _context = context;
            _defaults = defaults ?? ArrayUtils.EmptyObjects;
            __kwdefaults__ = kwdefaults;
            _code = funcInfo;
            _doc = funcInfo._initialDoc;
            _name = funcInfo.co_name;
            _annotations = annotations ?? new PythonDictionary();

            Debug.Assert(_defaults.Length <= _code.co_argcount);
            Debug.Assert((__kwdefaults__ ?.Count ?? 0) <= _code.co_kwonlyargcount);
            if (modName != Uninitialized.Instance) {
                _module = modName;
            }

            Closure = closure;
            FunctionCompatibility = CalculatedCachedCompat();
        }

        #region Public APIs

        public object __globals__ {
            get {
                return _context.GlobalDict;
            }
            set {
                throw PythonOps.AttributeError("readonly attribute");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonDictionary __annotations__ {
            get {
                return _annotations;
            }
            set {
                _annotations = value ?? throw PythonOps.TypeError("__annotations__ must be set to a dict object");
            }
        }

        [PropertyMethod, SpecialName, System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void Delete__globals__() {
            throw PythonOps.AttributeError("readonly attribute");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonTuple __defaults__ {
            get {
                if (_defaults.Length == 0) return null;

                return new PythonTuple(_defaults);
            }
            set {
                _defaults = value == null ? ArrayUtils.EmptyObjects : value.ToArray();
                FunctionCompatibility = CalculatedCachedCompat();
            }
        }

        // TODO: update CalculatedCachedCompat?
        public PythonDictionary __kwdefaults__ { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public object __closure__ {
            get {
                if (Context.Dict._storage is RuntimeVariablesDictionaryStorage storage) {
                    object[] res = new object[storage.Names.Length];
                    for (int i = 0; i < res.Length; i++) {
                        res[i] = storage.GetCell(i);
                    }
                    return PythonTuple.MakeTuple(res);
                }

                return null;
            }
            set {
                throw PythonOps.AttributeError("readonly attribute");
            }
        }

        [PropertyMethod, SpecialName, System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void Delete__closure__() {
            throw PythonOps.AttributeError("readonly attribute");
        }

        public string __name__ {
            get { return _name; }
            set {
                _name = value ?? throw PythonOps.TypeError("__name__ must be set to a string object");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PythonDictionary/*!*/ __dict__ {
            get { return EnsureDict(); }
            set {
                _dict = value ?? throw PythonOps.TypeError("__dict__ must be set to a dictionary, not a '{0}'", PythonTypeOps.GetName(value));
            }
        }

        public object __doc__ {
            get { return _doc; }
            set { _doc = value; }
        }

        public object __module__ {
            get { return _module; }
            set { _module = value; }
        }

        public FunctionCode __code__ {
            get {
                return _code; 
            }
            set {
                if (value == null) {
                    throw PythonOps.TypeError("__code__ must be set to a code object");
                }
                _code = value;
                FunctionCompatibility = CalculatedCachedCompat();
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
            get { return __code__.Span; }
        }

        internal string[] ArgNames {
            get { return __code__.ArgNames; }
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
        internal int FunctionCompatibility { get; private set; }

        /// <summary>
        /// Calculates the _compat value which is used for call-compatibility checks
        /// for simple calls.  Whenver any of the dependent values are updated this
        /// must be called again.
        /// 
        /// The dependent values include:
        ///     _nparams - this is readonly, and never requies an update
        ///     _defaults - the user can mutate this (__defaults__) and that forces an update
        ///     __kwdefaults__ - the user can mutate this and that forces an update
        ///     expand dict/list - based on nparams and flags, both read-only
        ///     
        /// Bits are allocated as:
        ///     000000ff - Normal argument count
        ///     0000ff00 - Default count
        ///     007f0000 - Keyword-only argument count
        ///     3f800000 - Keyword-only defaults count
        ///     40000000 - expand list
        ///     80000000 - expand dict
        ///     
        /// Enforce recursion is added at runtime.
        /// </summary>
        private int CalculatedCachedCompat() {
            return (NormalArgumentCount) |
                (Defaults.Length << 8) |
                (KeywordOnlyArgumentCount << 16) |
                ((__kwdefaults__?.Count ?? 0) << 23) |
                ((ExpandListPosition != -1) ? 0x40000000 : 0) |
                ((ExpandDictPosition != -1) ? unchecked((int)0x80000000) : 0);
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
                if (_code.Flags.HasFlag(FunctionAttributes.ArgumentList)) {
                    return _code.co_argcount + _code.co_kwonlyargcount;
                }

                return -1;
            }
        }

        /// <summary>
        /// Gets the position for the expand dictionary argument or -1 if the function doesn't have an expand dictionary parameter.
        /// </summary>
        internal int ExpandDictPosition {
            get {
                if (_code.Flags.HasFlag(FunctionAttributes.KeywordDictionary)) {
                    if (_code.Flags.HasFlag(FunctionAttributes.ArgumentList)) {
                        return _code.co_argcount + _code.co_kwonlyargcount + 1;
                    }
                    return _code.co_argcount + _code.co_kwonlyargcount;
                }
                return -1;
            }
        }

        /// <summary>
        /// Gets the number of normal (not keyword-only, params or kw-params) parameters.
        /// </summary>
        internal int NormalArgumentCount => _code.co_argcount;

        /// <summary>
        /// Gets the number of keyword-only parameters.
        /// </summary>
        internal int KeywordOnlyArgumentCount => _code.co_kwonlyargcount;

        /// <summary>
        /// Gets the number of extra arguments (params or kw-params)
        /// </summary>
        internal int ExtraArguments {
            get {
                if (_code.Flags.HasFlag(FunctionAttributes.ArgumentList)) {
                    if (_code.Flags.HasFlag(FunctionAttributes.KeywordDictionary)) {
                        return 2;
                    }
                    return 1;

                } else if (_code.Flags.HasFlag(FunctionAttributes.KeywordDictionary)) {
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
            PythonList list;
            if (_dict == null) {
                list = new PythonList();
            } else {
                list = new PythonList(_dict);
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
            value = instance == null ? (object)this : new Method(this, instance, owner);
            return true;
        }

        internal override bool GetAlwaysSucceeds => true;

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<function {0} at {1}>", __name__, PythonOps.HexId(this));
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Ast/*!*/ parameter) {
            return new Binding.MetaPythonFunction(parameter, BindingRestrictions.Empty, this);
        }

        #endregion
    }

    [PythonType("cell")]
    public sealed class ClosureCell : ICodeFormattable
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

        public string/*!*/ __repr__(CodeContext/*!*/ context)
            => $"<cell at 0x{IdDispenser.GetId(this):X}: {GetContentsRepr()}>";

        private string GetContentsRepr() {
            if (Value == Uninitialized.Instance) {
                return "empty";
            }

            return $"{PythonTypeOps.GetName(Value)} object at 0x{IdDispenser.GetId(Value):X}";
        }

        #endregion

        public const object __hash__ = null;

        [Python3Warning("cell comparisons not supported in 3.x")]
        public int __cmp__(object other) {
            if (!(other is ClosureCell cc)) throw PythonOps.TypeError("cell.__cmp__(x,y) expected cell, got {0}", PythonTypeOps.GetName(other));

            return PythonOps.Compare(Value, cc.Value);
        }
    }
}
