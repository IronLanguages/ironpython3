// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Dynamic;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    /// <summary>
    /// BuiltinFunction represents any standard CLR function exposed to Python.
    /// This is used for both methods on standard Python types such as list or tuple
    /// and for methods from arbitrary .NET assemblies.
    /// 
    /// All calls are made through the optimizedTarget which is created lazily.
    /// 
    /// TODO: Back BuiltinFunction's by MethodGroup's.
    /// </summary>    
    [PythonType("builtin_function_or_method"), DontMapGetMemberNamesToDir]
    public partial class BuiltinFunction : PythonTypeSlot, ICodeFormattable, IDynamicMetaObjectProvider, IDelegateConvertible, IFastInvokable  {
        internal readonly BuiltinFunctionData/*!*/ _data;            // information describing the BuiltinFunction
        internal readonly object _instance;                          // the bound instance or null if unbound
        private static readonly object _noInstance = new object();  

        #region Static factories

        /// <summary>
        /// Creates a new builtin function for a static .NET function.  This is used for module methods
        /// and well-known __new__ methods.
        /// </summary>
        internal static BuiltinFunction/*!*/ MakeFunction(string name, MethodBase[] infos, Type declaringType) {
#if DEBUG
            foreach (MethodBase mi in infos) {
                Debug.Assert(!mi.ContainsGenericParameters);
            }
#endif

            return new BuiltinFunction(name, infos, declaringType, FunctionType.AlwaysVisible | FunctionType.Function);
        }

        /// <summary>
        /// Creates a built-in function for a .NET method declared on a type.
        /// </summary>
        internal static BuiltinFunction/*!*/ MakeMethod(string name, MethodBase[] infos, Type declaringType, FunctionType ft) {
            foreach (MethodBase mi in infos) {
                if (mi.ContainsGenericParameters) {
                    return new GenericBuiltinFunction(name, infos, declaringType, ft);
                }
            }

            return new BuiltinFunction(name, infos, declaringType, ft);
        }

        internal virtual BuiltinFunction/*!*/ BindToInstance(object instance) {
            return new BuiltinFunction(instance, _data);
        }

        #endregion

        #region Constructors

        internal BuiltinFunction(string/*!*/ name, MethodBase/*!*/[]/*!*/ originalTargets, Type/*!*/ declaringType, FunctionType functionType) {
            Assert.NotNull(name);
            Assert.NotNull(declaringType);
            Assert.NotNullItems(originalTargets);

            _data = new BuiltinFunctionData(name, originalTargets, declaringType, functionType);
            _instance = _noInstance;
        }

        /// <summary>
        /// Creates a bound built-in function.  The instance may be null for built-in functions
        /// accessed for None.
        /// </summary>
        internal BuiltinFunction(object instance, BuiltinFunctionData/*!*/ data) {
            Assert.NotNull(data);

            _instance = instance;
            _data = data;
        }

        #endregion

        #region Internal API Surface

        internal void AddMethod(MethodInfo mi) {
            _data.AddMethod(mi);
        }

        internal bool TestData(object data) {
            return _data == data;
        }

        internal bool IsUnbound {
            get {
                return _instance == _noInstance;
            }
        }

        internal string Name {
            get {
                return _data.Name;
            }
            set {
                _data.Name = value;
            }
        }

        internal object Call(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, object instance, object[] args) {
            storage = GetInitializedStorage(context, storage);
            
            object callable;
            if (!GetDescriptor().TryGetValue(context, instance, DynamicHelpers.GetPythonTypeFromType(DeclaringType), out callable)) {
                callable = this;
            }

            return storage.Data.Target(storage.Data, context, callable, args);
        }

        internal object Call0(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object>>> storage, object instance) {
            storage = GetInitializedStorage(context, storage);

            object callable;
            if (!GetDescriptor().TryGetValue(context, instance, DynamicHelpers.GetPythonTypeFromType(DeclaringType), out callable)) {
                callable = this;
            }
            
            return storage.Data.Target(storage.Data, context, callable);
        }

        private static SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> GetInitializedStorage(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage) {
            if (storage == null) {
                storage = context.LanguageContext.GetGenericCallSiteStorage();
            }

            if (storage.Data == null) {
                storage.Data = context.LanguageContext.MakeSplatSite();
            }
            return storage;
        }

        private static SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object>>> GetInitializedStorage(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object>>> storage) {
            if (storage.Data == null) {
                storage.Data = CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                    context.LanguageContext.InvokeNone
                );
            }
            return storage;
        }

        internal object Call(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>>> storage, object instance, object[] args, IDictionary<object, object> keywordArgs) {
            if (storage == null) {
                storage = context.LanguageContext.GetGenericKeywordCallSiteStorage();
            }

            if (storage.Data == null) {
                storage.Data = context.LanguageContext.MakeKeywordSplatSite();
            }

            if (instance != null) {
                return storage.Data.Target(storage.Data, context, this, ArrayUtils.Insert(instance, args), keywordArgs);
            }

            return storage.Data.Target(storage.Data, context, this, args, keywordArgs);
        }
        
        /// <summary>
        /// Returns a BuiltinFunction bound to the provided type arguments.  Returns null if the binding
        /// cannot be performed.
        /// </summary>
        internal BuiltinFunction MakeGenericMethod(Type[] types) {
            TypeList tl = new TypeList(types);

            // check for cached method first...
            BuiltinFunction bf;
            if (_data.BoundGenerics != null) {
                lock (_data.BoundGenerics) {
                    if (_data.BoundGenerics.TryGetValue(tl, out bf)) {
                        return bf;
                    }
                }
            }

            // Search for generic targets with the correct arity (number of type parameters).
            // Compatible targets must be MethodInfos by definition (constructors never take
            // type arguments).
            List<MethodBase> targets = new List<MethodBase>(Targets.Count);
            foreach (MethodBase mb in Targets) {
                MethodInfo mi = mb as MethodInfo;
                if (mi == null)
                    continue;
                if (mi.ContainsGenericParameters && mi.GetGenericArguments().Length == types.Length)
                    targets.Add(mi.MakeGenericMethod(types));
            }

            if (targets.Count == 0) {
                return null;
            }

            // Build a new ReflectedMethod that will contain targets with bound type arguments & cache it.
            bf = new BuiltinFunction(Name, targets.ToArray(), DeclaringType, FunctionType);

            EnsureBoundGenericDict();

            lock (_data.BoundGenerics) {
                _data.BoundGenerics[tl] = bf;
            }

            return bf;
        }

        /// <summary>
        /// Returns a descriptor for the built-in function if one is
        /// neededed
        /// </summary>
        internal PythonTypeSlot/*!*/ GetDescriptor() {
            if ((FunctionType & FunctionType.Method) != 0) {
                return new BuiltinMethodDescriptor(this);
            }
            return this;
        }

        public Type DeclaringType {
            [PythonHidden]
            get {
                return _data.DeclaringType;
            }
        }

        /// <summary>
        /// Gets the target methods that we'll be calling.  
        /// </summary>
        public IList<MethodBase> Targets {
            [PythonHidden]
            get {
                return _data.Targets;                
            }
        }

        /// <summary>
        /// True if the method should be visible to non-CLS opt-in callers
        /// </summary>
        internal override bool IsAlwaysVisible {
            get {
                return (_data.Type & FunctionType.AlwaysVisible) != 0;
            }
        }

        internal bool IsReversedOperator {
            get {
                return (FunctionType & FunctionType.ReversedOperator) != 0;
            }
        }

        internal bool IsBinaryOperator {
            get {
                return (FunctionType & FunctionType.BinaryOperator) != 0;
            }
        }

        internal FunctionType FunctionType {
            get {
                return _data.Type;
            }
            set {
                _data.Type = value;
            }
        }

        /// <summary>
        /// Makes a test for the built-in function against the private _data 
        /// which is unique per built-in function.
        /// </summary>
        internal Expression/*!*/ MakeBoundFunctionTest(Expression/*!*/ functionTarget) {
            Debug.Assert(functionTarget.Type == typeof(BuiltinFunction));

            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.TestBoundBuiltinFunction)),
                functionTarget,
                AstUtils.Constant(_data, typeof(object))
            );
        }
        
        #endregion

        #region PythonTypeSlot Overrides

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            value = this;
            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        internal override void MakeGetExpression(PythonBinder/*!*/ binder, Expression/*!*/ codeContext, DynamicMetaObject instance, DynamicMetaObject/*!*/ owner, ConditionalBuilder/*!*/ builder) {
            builder.FinishCondition(Ast.Constant(this));
        }

        #endregion                

        #region ICodeFormattable members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            if (IsUnbound || IsBuiltinModuleMethod) {
                return string.Format("<built-in function {0}>", Name);
            }

            return string.Format("<built-in method {0} of {1} object at {2}>",
                __name__,
                PythonOps.GetPythonTypeName(__self__),
                PythonOps.HexId(__self__));
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter) {
            return new Binding.MetaBuiltinFunction(parameter, BindingRestrictions.Empty, this);
        }

        internal class BindingResult {
            public readonly BindingTarget Target;
            public readonly DynamicMetaObject MetaObject;

            public BindingResult(BindingTarget target, DynamicMetaObject meta) {
                Target = target;
                MetaObject = meta;
            }
        }

        /// <summary>
        /// Helper for generating the call to a builtin function.  This is used for calls from built-in method
        /// descriptors and built-in functions w/ and w/o a bound instance.  
        /// 
        /// This provides all sorts of common checks on top of the call while the caller provides a delegate
        /// to do the actual call.  The common checks include:
        ///     check for generic-only methods
        ///     reversed operator support
        ///     transforming arguments so the default binder can understand them (currently user defined mapping types to PythonDictionary)
        ///     returning NotImplemented from binary operators
        ///     Warning when calling certain built-in functions
        ///     
        /// </summary>
        /// <param name="call">The call binder we're doing the call for</param>
        /// <param name="codeContext">An expression which points to the code context</param>
        /// <param name="function">the meta object for the built in function</param>
        /// <param name="hasSelf">true if we're calling with an instance</param>
        /// <param name="args">The arguments being passed to the function</param>
        /// <param name="functionRestriction">A restriction for the built-in function, method desc, etc...</param>
        /// <param name="bind">A delegate to perform the actual call to the method.</param>
        internal DynamicMetaObject/*!*/ MakeBuiltinFunctionCall(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ function, DynamicMetaObject/*!*/[] args, bool hasSelf, BindingRestrictions/*!*/ functionRestriction, Func<DynamicMetaObject/*!*/[]/*!*/, BindingResult/*!*/> bind) {
            DynamicMetaObject res = null;

            // if we have a user defined operator for **args then transform it into a PythonDictionary
            DynamicMetaObject translated = TranslateArguments(call, codeContext, new DynamicMetaObject(function.Expression, functionRestriction, function.Value), args, hasSelf, Name);
            if (translated != null) {
                return translated;
            }

            // swap the arguments if we have a reversed operator
            if (IsReversedOperator) {
                ArrayUtils.SwapLastTwo(args);
            }

            // do the appropriate calling logic
            BindingResult result = bind(args);

            // validate the result
            BindingTarget target = result.Target;
            res = result.MetaObject;

            if (target.Overload != null && target.Overload.IsProtected) {
                // report an error when calling a protected member
                res = new DynamicMetaObject(
                    BindingHelpers.TypeErrorForProtectedMember(
                        target.Overload.DeclaringType,
                        target.Overload.Name
                    ),
                    res.Restrictions
                );
            } else if (IsBinaryOperator && args.Length == 2 && IsThrowException(res.Expression)) {
                // Binary Operators return NotImplemented on failure.
                res = new DynamicMetaObject(
                    Ast.Property(null, typeof(PythonOps), "NotImplemented"),
                    res.Restrictions
                );
            } else if (target.Overload != null) {
                // Add profiling information for this builtin function, if applicable
                IPythonSite pythonSite = (call as IPythonSite);
                if (pythonSite != null) {
                    var pc = pythonSite.Context;
                    var po = pc.Options as PythonOptions;
                    if (po != null && po.EnableProfiler) {
                        Profiler profiler = Profiler.GetProfiler(pc);
                        res = new DynamicMetaObject(
                            profiler.AddProfiling(res.Expression, target.Overload.ReflectionInfo),
                            res.Restrictions
                        );
                    }
                }
            }

            // add any warnings that are applicable for calling this function
            WarningInfo info;

            if (target.Overload != null && BindingWarnings.ShouldWarn(PythonContext.GetPythonContext(call), target.Overload, out info)) {
                res = info.AddWarning(codeContext, res);
            }            

            // finally add the restrictions for the built-in function and return the result.
            res = new DynamicMetaObject(
                res.Expression,
                functionRestriction.Merge(res.Restrictions)
            );

            // The function can return something typed to boolean or int.
            // If that happens, we need to apply Python's boxing rules.
            if (res.Expression.Type.IsValueType()) {
                res = BindingHelpers.AddPythonBoxing(res);
            } else if (res.Expression.Type == typeof(void)) {
                res = new DynamicMetaObject(
                    Expression.Block(
                        res.Expression,
                        Expression.Constant(null)
                    ),
                    res.Restrictions
                );
            }

            return res;
        }

        internal static DynamicMetaObject TranslateArguments(DynamicMetaObjectBinder call, Expression codeContext, DynamicMetaObject function, DynamicMetaObject/*!*/[] args, bool hasSelf, string name) {
            if (hasSelf) {
                args = ArrayUtils.RemoveFirst(args);
            }

            CallSignature sig = BindingHelpers.GetCallSignature(call);
            if (sig.HasDictionaryArgument()) {
                int index = sig.IndexOf(ArgumentType.Dictionary);

                DynamicMetaObject dict = args[index];

                if (!(dict.Value is IDictionary) && dict.Value != null) {
                    // The DefaultBinder only handles types that implement IDictionary.  Here we have an
                    // arbitrary user-defined mapping type.  We'll convert it into a PythonDictionary
                    // and then have an embedded dynamic site pass that dictionary through to the default
                    // binder.
                    DynamicMetaObject[] dynamicArgs = ArrayUtils.Insert(function, args);

                    dynamicArgs[index + 1] = new DynamicMetaObject(
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.UserMappingToPythonDictionary)),
                            codeContext,
                            args[index].Expression,
                            AstUtils.Constant(name)
                        ),
                        BindingRestrictionsHelpers.GetRuntimeTypeRestriction(dict.Expression, dict.GetLimitType()),
                        PythonOps.UserMappingToPythonDictionary(PythonContext.GetPythonContext(call).SharedContext, dict.Value, name)
                    );

                    if (call is IPythonSite) {
                        dynamicArgs = ArrayUtils.Insert(
                            new DynamicMetaObject(codeContext, BindingRestrictions.Empty),
                            dynamicArgs
                        );
                    }

                    return new DynamicMetaObject(
                        DynamicExpression.Dynamic(
                            call,
                            typeof(object),
                            DynamicUtils.GetExpressions(dynamicArgs)
                        ),
                        BindingRestrictions.Combine(dynamicArgs).Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(dict.Expression, dict.GetLimitType()))
                    );
                }
            }

            if (sig.HasListArgument()) {
                int index = sig.IndexOf(ArgumentType.List);
                DynamicMetaObject str = args[index];

                 // TODO: ANything w/ __iter__ that's not an IList<object>
                if (!(str.Value is IList<object>) && str.Value is IEnumerable) {
                    // The DefaultBinder only handles types that implement IList<object>.  Here we have a
                    // string.  We'll convert it into a tuple
                    // and then have an embedded dynamic site pass that tuple through to the default
                    // binder.
                    DynamicMetaObject[] dynamicArgs = ArrayUtils.Insert(function, args);

                    dynamicArgs[index + 1] = new DynamicMetaObject(
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTupleFromSequence)),
                            Expression.Convert(args[index].Expression, typeof(object))
                        ),
                        BindingRestrictions.Empty
                    );

                    if (call is IPythonSite) {
                        dynamicArgs = ArrayUtils.Insert(
                            new DynamicMetaObject(codeContext, BindingRestrictions.Empty),
                            dynamicArgs
                        );
                    }

                    return new DynamicMetaObject(
                        DynamicExpression.Dynamic(
                            call,
                            typeof(object),
                            DynamicUtils.GetExpressions(dynamicArgs)
                        ),
                        function.Restrictions.Merge(
                            BindingRestrictions.Combine(args).Merge(BindingRestrictionsHelpers.GetRuntimeTypeRestriction(str.Expression, str.GetLimitType()))
                        )
                    );
                }

            }
            return null;
        }

        private static bool IsThrowException(Expression expr) {
            if (expr.NodeType == ExpressionType.Throw) {
                return true;
            } else if (expr.NodeType == ExpressionType.Convert) {
                return IsThrowException(((UnaryExpression)expr).Operand);
            } else if (expr.NodeType == ExpressionType.Block) {
                foreach (Expression e in ((BlockExpression)expr).Expressions) {
                    if (IsThrowException(e)) {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
                        
        #region Public Python APIs

        public int __cmp__(CodeContext/*!*/ context, [NotNull]BuiltinFunction/*!*/  other) {
            if (other == this) {
                return 0;
            }

            if (!IsUnbound && !other.IsUnbound) {
                int result = PythonOps.Compare(__self__, other.__self__);
                if (result != 0) {
                    return result;
                }

                if (_data == other._data) {
                    return 0;
                }
            }

            int res = String.CompareOrdinal(__name__, other.__name__);
            if (res != 0) {
                return res;
            }

            res = String.CompareOrdinal(Get__module__(context), other.Get__module__(context));
            if (res != 0) {
                return res;
            }
            
            long lres = IdDispenser.GetId(this) - IdDispenser.GetId(other);
            return lres > 0 ? 1 : -1;
        }

        // these are present in CPython but always return NotImplemented.
        [return: MaybeNotImplemented]
        [Python3Warning("builtin_function_or_method order comparisons not supported in 3.x")]
        public static NotImplementedType operator >(BuiltinFunction self, BuiltinFunction other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("builtin_function_or_method order comparisons not supported in 3.x")]
        public static NotImplementedType operator <(BuiltinFunction self, BuiltinFunction other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("builtin_function_or_method order comparisons not supported in 3.x")]
        public static NotImplementedType operator >=(BuiltinFunction self, BuiltinFunction other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("builtin_function_or_method order comparisons not supported in 3.x")]
        public static NotImplementedType operator <=(BuiltinFunction self, BuiltinFunction other) {
            return PythonOps.NotImplemented;
        }

        public int __hash__(CodeContext/*!*/ context) {
            return PythonOps.Hash(context, _instance) ^ PythonOps.Hash(context, _data);
        }

        [SpecialName, PropertyMethod]
        public string Get__module__(CodeContext/*!*/ context) {
            if (Targets.Count > 0) {
                PythonType declaringType = DynamicHelpers.GetPythonTypeFromType(DeclaringType);

                string res = PythonTypeOps.GetModuleName(context, declaringType.UnderlyingSystemType);
                if (res != "builtins" || DeclaringType == typeof(IronPython.Modules.Builtin)) {
                    return res;
                }
            }
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), SpecialName, PropertyMethod]
        public void Set__module__(string value) {
            // Do nothing but don't return an error
        }

        /// <summary>
        /// Provides (for reflected methods) a mapping from a signature to the exact target
        /// which takes this signature.
        /// signature with syntax like the following:
        ///    someClass.SomeMethod.Overloads[str, int]("Foo", 123)
        /// </summary>
        public virtual BuiltinFunctionOverloadMapper Overloads {
            [PythonHidden]
            get {
                // The mapping is actually provided by a class rather than a dictionary
                // since it's hard to generate all the keys of the signature mapping when
                // two type systems are involved.  

                return new BuiltinFunctionOverloadMapper(this, IsUnbound ? null : _instance);
            }
        }

        /// <summary>
        /// Gets the overload dictionary for the logical function.  These overloads
        /// are never bound to an instance.
        /// </summary>
        internal Dictionary<TypeList, BuiltinFunction> OverloadDictionary {
            get {
                if (_data.OverloadDictionary == null) {
                    Interlocked.CompareExchange(
                        ref _data.OverloadDictionary,
                        new Dictionary<TypeList, BuiltinFunction>(),
                        null);
                }

                return _data.OverloadDictionary;
            }
        }

        public string __name__ {
            get {
                return Name;
            }
        }

        public virtual string __doc__ {
            get {
                StringBuilder sb = new StringBuilder();
                IList<MethodBase> targets = Targets;
                for (int i = 0; i < targets.Count; i++) {
                    if (targets[i] != null) {
                        if (IsBuiltinModuleMethod) {
                            sb.Append(DocBuilder.DocOneInfo(targets[i], Name, false));
                        } else {
                            sb.Append(DocBuilder.DocOneInfo(targets[i], Name));
                        }
                    }
                }
                return sb.ToString();
            }
        }

        public object __self__ {
            get {
                if (IsUnbound || IsBuiltinModuleMethod) {
                    return null;
                }

                return _instance;
            }
        }

        /// <summary>
        /// Returns the instance used for binding.  This differs on module functions implemented
        /// using instance methods so the built-in functions there don't expose the instance.
        /// </summary>
        internal object BindingSelf {
            get {
                if (IsUnbound) {
                    return null;
                }

                return _instance;
            }
        }

        private bool IsBuiltinModuleMethod {
            get {
                return (FunctionType & FunctionType.ModuleMethod) != 0;
            }
        }

        public object __call__(CodeContext/*!*/ context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>>> storage, [ParamDictionary]IDictionary<object, object> dictArgs, params object[] args) {
            return Call(context, storage, null, args, dictArgs);
        }

        
        internal virtual bool IsOnlyGeneric {
            get {
                return false;
            }
        }

        #endregion

        #region Private members

        private BinderType BinderType {
            get {
                return IsBinaryOperator ? BinderType.BinaryOperator : BinderType.Normal;
            }
        }

        private void EnsureBoundGenericDict() {
            if (_data.BoundGenerics == null) {
                Interlocked.CompareExchange<Dictionary<TypeList, BuiltinFunction>>(
                    ref _data.BoundGenerics,
                    new Dictionary<TypeList, BuiltinFunction>(1),
                    null);
            }
        }

        internal class TypeList {
            private Type[] _types;

            public TypeList(Type[] types) {
                Debug.Assert(types != null);
                _types = types;
            }

            public override bool Equals(object obj) {
                TypeList tl = obj as TypeList;
                if (tl == null || _types.Length != tl._types.Length) return false;

                for (int i = 0; i < _types.Length; i++) {
                    if (_types[i] != tl._types[i]) return false;
                }
                return true;
            }

            public override int GetHashCode() {
                int hc = 6551;
                foreach (Type t in _types) {
                    hc = (hc << 5) ^ t.GetHashCode();
                }
                return hc;
            }
        }

        #endregion

        #region IDelegateConvertible Members

        Delegate IDelegateConvertible.ConvertToDelegate(Type type) {
            // see if we have any functions which are compatible with the delegate type...
            ParameterInfo[] delegateParams = type.GetMethod("Invoke").GetParameters();

            // if we have overloads then we need to do the overload resolution at runtime
            if (Targets.Count == 1) {
                MethodInfo mi = Targets[0] as MethodInfo;
                if (mi != null) {
                    ParameterInfo[] methodParams = mi.GetParameters();
                    if (methodParams.Length == delegateParams.Length) {
                        bool match = true;
                        for (int i = 0; i < methodParams.Length; i++) {
                            if (delegateParams[i].ParameterType != methodParams[i].ParameterType) {
                                match = false;
                                break;
                            }
                        }

                        if (match) {
                            if (IsUnbound) {
                                return mi.CreateDelegate(type);
                            } else {
                                return mi.CreateDelegate(type, _instance);
                            }
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region BuiltinFunctionData

        internal sealed class BuiltinFunctionData {
            public string/*!*/ Name;
            public MethodBase/*!*/[]/*!*/ Targets;
            public readonly Type/*!*/ DeclaringType;
            public FunctionType Type;
            public Dictionary<TypeList, BuiltinFunction> BoundGenerics;
            public Dictionary<BuiltinFunction.TypeList, BuiltinFunction> OverloadDictionary;

            public BuiltinFunctionData(string name, MethodBase[] targets, Type declType, FunctionType functionType) {
                Name = name;
                Targets = targets;
                DeclaringType = declType;
                Type = functionType;
            }

            internal void AddMethod(MethodBase/*!*/ info) {
                Assert.NotNull(info);

                MethodBase[] ni = new MethodBase[Targets.Length + 1];
                Targets.CopyTo(ni, 0);
                ni[Targets.Length] = info;
                Targets = ni;
            }
        }

        #endregion

        #region IFastInvokable Members

        FastBindResult<T> IFastInvokable.MakeInvokeBinding<T>(CallSite<T> site, PythonInvokeBinder binder, CodeContext state, object[] args) {
            return new FastBindResult<T>(
                binder.LightBind<T>(ArrayUtils.Insert(state, this, args), 100),
                true
            );
        }

        #endregion
    }

    
    /// <summary>
    /// A custom built-in function which supports indexing 
    /// </summary>
    public class GenericBuiltinFunction : BuiltinFunction {
        internal GenericBuiltinFunction(string/*!*/ name, MethodBase/*!*/[]/*!*/ originalTargets, Type/*!*/ declaringType, FunctionType functionType)
            : base(name, originalTargets, declaringType, functionType) {
        }

        public BuiltinFunction/*!*/ this[PythonTuple tuple] {
            get {
                return this[tuple._data];
            }
        }

        internal GenericBuiltinFunction(object instance, BuiltinFunctionData/*!*/ data) : base(instance, data) {
        }


        internal override BuiltinFunction BindToInstance(object instance) {
            return new GenericBuiltinFunction(instance, _data);
        }

        /// <summary>
        /// Use indexing on generic methods to provide a new reflected method with targets bound with
        /// the supplied type arguments.
        /// </summary>
        public BuiltinFunction/*!*/ this[params object[] key] {
            get {
                // Retrieve the list of type arguments from the index.
                Type[] types = new Type[key.Length];
                for (int i = 0; i < types.Length; i++) {
                    types[i] = Converter.ConvertToType(key[i]);
                }

                BuiltinFunction res = MakeGenericMethod(types);
                if (res == null) {
                    bool hasGenerics = false;
                    foreach (MethodBase mb in Targets) {
                        MethodInfo mi = mb as MethodInfo;
                        if (mi != null && mi.ContainsGenericParameters) {
                            hasGenerics = true;
                        }
                    }

                    if (hasGenerics) {
                        throw PythonOps.TypeError(string.Format("bad type args to this generic method {0}", Name));
                    } else {
                        throw PythonOps.TypeError(string.Format("{0} is not a generic method and is unsubscriptable", Name));
                    }
                }

                if (IsUnbound) {
                    return res;
                }

                return new BuiltinFunction(_instance, res._data);
            }
        }

        internal override bool IsOnlyGeneric {
            get {
                foreach (MethodBase mb in Targets) {
                    if (!mb.IsGenericMethod || !mb.ContainsGenericParameters) {
                        return false;
                    }
                }

                return true;
            }
        }

    }
}

