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
using dynamic = System.Object;
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Debugging.CompilerServices;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Debugging = Microsoft.Scripting.Debugging;
using PyAst = IronPython.Compiler.Ast;

namespace IronPython.Runtime
{
    public delegate int HashDelegate(object o, ref HashDelegate dlg);

    public sealed partial class PythonContext : LanguageContext {
        internal const string/*!*/ IronPythonDisplayName = CurrentVersion.DisplayName;
        internal const string/*!*/ IronPythonNames = "IronPython;Python;py";
        internal const string/*!*/ IronPythonFileExtensions = ".py";

        private static readonly Guid PythonLanguageGuid = new Guid("03ed4b80-d10b-442f-ad9a-47dae85b2051");
        private static readonly Guid LanguageVendor_Microsoft = new Guid(-1723120188, -6423, 0x11d2, 0x90, 0x3f, 0, 0xc0, 0x4f, 0xa3, 2, 0xa1);

        // fields used during startup
        private readonly IDictionary<object, object>/*!*/ _modulesDict = new PythonDictionary();
        private readonly Dictionary<string, ModuleGlobalCache>/*!*/ _builtinCache = new Dictionary<string, ModuleGlobalCache>(StringComparer.Ordinal);
        private readonly Dictionary<Type, string>/*!*/ _builtinModuleNames = new Dictionary<Type, string>();
        private readonly PythonOptions/*!*/ _options;
        private readonly PythonModule/*!*/ _systemState;
        private readonly Dictionary<string, Type>/*!*/ _builtinModulesDict;
        private readonly PythonOverloadResolverFactory _sharedOverloadResolverFactory;
        private readonly PythonBinder _binder;
#if FEATURE_ASSEMBLY_RESOLVE && FEATURE_FILESYSTEM
        private readonly AssemblyResolveHolder _resolveHolder;
#if !CLR2
        private readonly HashSet<Assembly> _loadedAssemblies = new HashSet<Assembly>();
#endif
#endif
        private Encoding _defaultEncoding = PythonAsciiEncoding.Instance;

        // conditional variables for silverlight/desktop CLR features
        private Hosting.PythonService _pythonService;
        private string _initialExecutable, _initialPrefix = GetInitialPrefix();

        // other fields which might only be conditionally used
        private string _initialVersionString;
        private PythonModule _clrModule;
        private PythonDictionary _builtinDict;
        private PythonModule _builtins;

        private PythonFileManager _fileManager;
        private Dictionary<string, object> _errorHandlers;
        private List<object> _searchFunctions;
        private Dictionary<object, object> _moduleState;
        /// <summary> stored for copyreg module, used for reduce protocol </summary>
        internal BuiltinFunction NewObject;
        /// <summary> stored for copyreg module, used for reduce protocol </summary>
        internal BuiltinFunction PythonReconstructor;
        private Dictionary<Type, object> _genericSiteStorage;

        private CallSite<Func<CallSite, CodeContext, object, object>>[] _newUnarySites;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object>>[] _newTernarySites;

        private CallSite<Func<CallSite, object, object, int>> _compareSite;
        private Dictionary<AttrKey, CallSite<Func<CallSite, object, object, object>>> _setAttrSites;
        private Dictionary<AttrKey, CallSite<Action<CallSite, object>>> _deleteAttrSites;
        private CallSite<Func<CallSite, CodeContext, object, string, PythonTuple, PythonDictionary, object>> _metaClassSite;
        private CallSite<Func<CallSite, CodeContext, object, string, object>> _writeSite;
        private CallSite<Func<CallSite, object, object, object>> _getIndexSite, _equalSite;
        private CallSite<Action<CallSite, object, object>> _delIndexSite;
        private CallSite<Func<CallSite, CodeContext, object, object>> _finalizerSite;
        private CallSite<Func<CallSite, CodeContext, PythonFunction, object>> _functionCallSite;
        private CallSite<Func<CallSite, object, object, bool>> _greaterThanSite, _lessThanSite, _greaterThanEqualSite, _lessThanEqualSite, _containsSite;
        private CallSite<Func<CallSite, CodeContext, object, object[], object>> _callSplatSite;
        private CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>> _callDictSite;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object>> _callDictSiteLooselyTyped;
        private CallSite<Func<CallSite, CodeContext, object, string, PythonDictionary, PythonDictionary, PythonTuple, int, object>> _importSite;
        private CallSite<Func<CallSite, CodeContext, object, string, PythonDictionary, PythonDictionary, PythonTuple, object>> _oldImportSite;
        private CallSite<Func<CallSite, object, bool>> _isCallableSite;
        private CallSite<Func<CallSite, object, IList<string>>> _getSignaturesSite;
        private CallSite<Func<CallSite, object, object, object>> _addSite, _divModSite, _rdivModSite;
        private CallSite<Func<CallSite, object, object, object, object>> _setIndexSite, _delSliceSite;
        private CallSite<Func<CallSite, object, object, object, object, object>> _setSliceSite;
        private CallSite<Func<CallSite, object, string>> _docSite;

        // conversion sites
        private CallSite<Func<CallSite, object, int>> _intSite;
        private CallSite<Func<CallSite, object, string>> _tryStringSite;
        private CallSite<Func<CallSite, object, object>> _tryIntSite;
        private CallSite<Func<CallSite, object, IEnumerable>> _tryIEnumerableSite;
        private Dictionary<Type, CallSite<Func<CallSite, object, object>>> _implicitConvertSites;
        private Dictionary<PythonOperationKind, CallSite<Func<CallSite, object, object, object>>> _binarySites;
        private Dictionary<Type, DefaultPythonComparer> _defaultComparer;
        private CallSite<Func<CallSite, CodeContext, object, object, object, int>> _sharedFunctionCompareSite;
        private CallSite<Func<CallSite, CodeContext, PythonFunction, object, object, int>> _sharedPythonFunctionCompareSite;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object, int>> _sharedBuiltinFunctionCompareSite;
        private CallSite<Func<CallSite, CodeContext, object, int, object>> _getItemCallSite;

        private CallSite<Func<CallSite, CodeContext, object, object, object>> _propGetSite, _propDelSite;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object>> _propSetSite;
        private CompiledLoader _compiledLoader;
        internal bool _importWarningThrows;
        private bool _importedEncodings;
        private Action<Action> _commandDispatcher; // can be null
        private ClrModule.ReferencesList _referencesList;
        private FloatFormat _floatFormat, _doubleFormat;
        private CultureInfo _collateCulture, _ctypeCulture, _timeCulture, _monetaryCulture, _numericCulture;
        private CodeContext _defaultContext, _defaultClsContext;
        private readonly TopNamespaceTracker _topNamespace;
        private readonly IEqualityComparer<object> _equalityComparer;
        private readonly IEqualityComparer _equalityComparerNonGeneric;

        private Dictionary<Type, CallSite<Func<CallSite, object, object, bool>>> _equalSites;

        private Dictionary<Type, PythonSiteCache> _systemSiteCache;
        internal static object _syntaxErrorNoCaret = new object();

        // atomized binders
        private PythonInvokeBinder _invokeNoArgs, _invokeOneArg;
        private Dictionary<CallSignature, PythonInvokeBinder/*!*/> _invokeBinders;
        private Dictionary<string/*!*/, PythonGetMemberBinder/*!*/> _getMemberBinders;
        private Dictionary<string/*!*/, PythonGetMemberBinder/*!*/> _tryGetMemberBinders;
        private Dictionary<string/*!*/, PythonSetMemberBinder/*!*/> _setMemberBinders;
        private Dictionary<string/*!*/, PythonDeleteMemberBinder/*!*/> _deleteMemberBinders;
        private Dictionary<string/*!*/, CompatibilityGetMember/*!*/> _compatGetMember;
        private Dictionary<string/*!*/, CompatibilityGetMember/*!*/> _compatGetMemberNoThrow;
        private Dictionary<PythonOperationKind, PythonOperationBinder/*!*/> _operationBinders;
        private Dictionary<ExpressionType, PythonUnaryOperationBinder/*!*/> _unaryBinders;
        private PythonBinaryOperationBinder[] _binaryBinders;
        private Dictionary<OperationRetTypeKey<ExpressionType>, BinaryRetTypeBinder/*!*/> _binaryRetTypeBinders;
        private Dictionary<OperationRetTypeKey<PythonOperationKind>, BinaryRetTypeBinder/*!*/> _operationRetTypeBinders;
        private Dictionary<Type/*!*/, PythonConversionBinder/*!*/>[] _conversionBinders;
        private Dictionary<Type/*!*/, DynamicMetaObjectBinder/*!*/>[] _convertRetObjectBinders;
        private Dictionary<CallSignature, CreateFallback/*!*/> _createBinders;
        private Dictionary<CallSignature, CompatibilityInvokeBinder/*!*/> _compatInvokeBinders;
        private PythonGetSliceBinder _getSlice;
        private PythonSetSliceBinder _setSlice;
        private PythonDeleteSliceBinder _deleteSlice;
        private PythonGetIndexBinder[] _getIndexBinders;
        private PythonSetIndexBinder[] _setIndexBinders;
        private PythonDeleteIndexBinder[] _deleteIndexBinders;
        private DynamicMetaObjectBinder _invokeTwoConvertToInt;
        private static CultureInfo _CCulture;
        private DynamicDelegateCreator _delegateCreator;

        // tracing / in-proc debugging support
        private DebugContext _debugContext;
        private Debugging.TracePipeline _tracePipeline;
        private readonly Microsoft.Scripting.Utils.ThreadLocal<PythonTracebackListener> _tracebackListeners = new Microsoft.Scripting.Utils.ThreadLocal<PythonTracebackListener>();
        private int _tracebackListenersCount;

        internal FunctionCode.CodeList _allCodes;
        internal readonly object _codeCleanupLock = new object(), _codeUpdateLock = new object();
        internal int _codeCount, _nextCodeCleanup = 200;
        private int _recursionLimit;

        internal readonly List<FunctionStack> _mainThreadFunctionStack;
        private CallSite<Func<CallSite, CodeContext, object, object>> _callSite0LightEh;
        private List<WeakReference> _weakExtensionMethodSets;

        // store the Python types mapping to each .NET type
        private CommonDictionaryStorage _systemPythonTypesWeakRefs = new CommonDictionaryStorage();

#region Generated Python Shared Call Sites Storage

        // *** BEGIN GENERATED CODE ***
        // generated by function: gen_shared_call_sites_storage from: generate_calls.py

        private CallSite<Func<CallSite, CodeContext, object, object>> _callSite0;
        private CallSite<Func<CallSite, CodeContext, object, object, object>> _callSite1;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object>> _callSite2;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object, object>> _callSite3;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object, object, object>> _callSite4;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object, object, object, object>> _callSite5;
        private CallSite<Func<CallSite, CodeContext, object, object, object, object, object, object, object, object>> _callSite6;

        // *** END GENERATED CODE ***

#endregion

        /// <summary>
        /// Creates a new PythonContext not bound to Engine.
        /// </summary>
        public PythonContext(ScriptDomainManager/*!*/ manager, IDictionary<string, object> options)
            : base(manager) {
            _options = new PythonOptions(options);
            _builtinModulesDict = CreateBuiltinTable();

            PythonDictionary defaultScope = new PythonDictionary();
            ModuleContext modContext = new ModuleContext(defaultScope, this);
            _defaultContext = modContext.GlobalContext;

            ModuleDictionaryStorage sysStorage = new ModuleDictionaryStorage(typeof(SysModule));
            PythonDictionary sysDict = new PythonDictionary(sysStorage);
            _systemState = new PythonModule(sysDict);
            _systemState.__dict__["__name__"] = "sys";
            _systemState.__dict__["__package__"] = null;

            PythonBinder binder = new PythonBinder(this, _defaultContext);
            _sharedOverloadResolverFactory = new PythonOverloadResolverFactory(binder, Expression.Constant(_defaultContext));
            _binder = binder;

            CodeContext defaultClsContext = DefaultContext.CreateDefaultCLSContext(this);
            _defaultClsContext = defaultClsContext;

            if (DefaultContext._default == null) {
                DefaultContext.InitializeDefaults(_defaultContext, defaultClsContext);
            }

            InitializeBuiltins();

            InitializeSystemState();
#if SILVERLIGHT
            AddToPath("");
#endif

            // sys.argv always includes at least one empty string.
            SetSystemStateValue("argv", (_options.Arguments.Count == 0) ?
                new List(new object[] { String.Empty }) :
                new List(_options.Arguments)
            );

            if (_options.WarningFilters.Count > 0) {
                _systemState.__dict__["warnoptions"] = new List(_options.WarningFilters);
            }

            if (_options.Frames) {
                var getFrame = BuiltinFunction.MakeFunction(
                    "_getframe", 
                    ArrayUtils.ConvertAll(typeof(SysModule).GetMember("_getframeImpl"), (x) => (MethodBase)x), 
                    typeof(SysModule)
                );
                _systemState.__dict__["_getframe"] = getFrame;
            }

            if (_options.Tracing) {
                EnsureDebugContext();
            }

            List path = new List(_options.SearchPaths);
#if FEATURE_ASSEMBLY_RESOLVE && FEATURE_FILESYSTEM
            _resolveHolder = new AssemblyResolveHolder(this);
            try {
                Assembly entryAssembly = Assembly.GetEntryAssembly();
                // Can be null if called from unmanaged code (VS integration scenario)
                if (entryAssembly != null) {
                    string entry = Path.GetDirectoryName(entryAssembly.Location);
                    string lib = Path.Combine(entry, "Lib");
                    path.append(lib);

                    // add DLLs directory for user-defined extention modules
                    path.append(Path.Combine(entry, "DLLs"));

#if DEBUG
                    // For developer use, add Src/StdLib/Lib
                    string devStdLib = Path.Combine(entry, @"../../Src/StdLib/Lib");
                    if (Directory.Exists(devStdLib))
                        path.append(devStdLib);
#endif
                }
            } catch (SecurityException) {
            }
#endif

            _systemState.__dict__["path"] = path;

            RecursionLimit = _options.RecursionLimit;

#if FEATURE_ASSEMBLY_RESOLVE && FEATURE_FILESYSTEM
            object asmResolve;
            if (options == null ||
                !options.TryGetValue("NoAssemblyResolveHook", out asmResolve) ||
                !System.Convert.ToBoolean(asmResolve)) {
                try {
                    HookAssemblyResolve();
                } catch (System.Security.SecurityException) {
                    // We may not have SecurityPermissionFlag.ControlAppDomain. 
                    // If so, we will not look up sys.path for module loads
                }
            }
#endif
            _equalityComparer = new PythonEqualityComparer(this);
            _equalityComparerNonGeneric = (IEqualityComparer)_equalityComparer;

            InitialHasher = InitialHasherImpl;
            IntHasher = IntHasherImpl;
            DoubleHasher = DoubleHasherImpl;
            StringHasher = StringHasherImpl;
            FallbackHasher = FallbackHasherImpl;

            _topNamespace = new TopNamespaceTracker(manager);
            foreach (Assembly asm in manager.GetLoadedAssemblyList()) {
                _topNamespace.LoadAssembly(asm);
            }
            manager.AssemblyLoaded += new EventHandler<AssemblyLoadedEventArgs>(ManagerAssemblyLoaded);

            _mainThreadFunctionStack = PythonOps.GetFunctionStack();
        }

        void ManagerAssemblyLoaded(object sender, AssemblyLoadedEventArgs e) {
            _topNamespace.LoadAssembly(e.Assembly);
        }

        /// <summary>
        /// Gets or sets the maximum depth of function calls.  Equivalent to sys.getrecursionlimit
        /// and sys.setrecursionlimit.
        /// </summary>
        public int RecursionLimit {
            get {
                return _recursionLimit;
            }
            set {
                if (value < 0) {
                    throw PythonOps.ValueError("recursion limit must be positive");
                }

                lock (_codeUpdateLock) {
                    _recursionLimit = value;

                    if ((_recursionLimit == Int32.MaxValue) != (value == Int32.MaxValue)) {
                        // recursion setting has changed, we need to update all of our
                        // function codes to enforce or un-enforce recursion.
                        FunctionCode.UpdateAllCode(this);
                    }
                }
            }
        }

        internal bool EnableTracing {
            get {
                return PythonOptions.Tracing || _tracebackListenersCount > 0;
            }
        }

        internal TopNamespaceTracker TopNamespace {
            get {
                return _topNamespace;
            }
        }

#if FEATURE_THREAD
        /// <summary>
        /// Gets or sets the main thread which should be interupted by thread.interrupt_main
        /// </summary>
        public Thread MainThread {
            get {
                return _mainThread;
            }
            set {
                _mainThread = value;
            }
        }

        private Thread _mainThread;
#endif

        public IEqualityComparer<object>/*!*/ EqualityComparer {
            get { return _equalityComparer; }
        }

        public IEqualityComparer/*!*/ EqualityComparerNonGeneric {
            get { return _equalityComparerNonGeneric; }
        }

        internal sealed class PythonEqualityComparer : IEqualityComparer, IEqualityComparer<object> {
            public readonly PythonContext/*!*/ Context;

            public PythonEqualityComparer(PythonContext/*!*/ context) {
                Assert.NotNull(context);
                Context = context;
            }

            bool IEqualityComparer.Equals(object x, object y) {
                return PythonOps.EqualRetBool(Context._defaultContext, x, y);
            }

            bool IEqualityComparer<object>.Equals(object x, object y) {
                return PythonOps.EqualRetBool(Context._defaultContext, x, y);
            }

            int IEqualityComparer.GetHashCode(object obj) {
                return PythonContext.Hash(obj);
            }

            int IEqualityComparer<object>.GetHashCode(object obj) {
                return PythonContext.Hash(obj);
            }
        }

#region Specialized Hashers

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        internal readonly HashDelegate InitialHasher;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        internal readonly HashDelegate IntHasher;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        internal readonly HashDelegate DoubleHasher;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        internal readonly HashDelegate StringHasher;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        internal readonly HashDelegate FallbackHasher;

        private int InitialHasherImpl(object o, ref HashDelegate dlg) {
            if (o == null) {
                return NoneTypeOps.NoneHashCode;
            }

            switch (o.GetType().GetTypeCode()) {
                case TypeCode.String:
                    dlg = StringHasher;
                    return StringHasher(o, ref dlg);
                case TypeCode.Int32:
                    dlg = IntHasher;
                    return IntHasher(o, ref dlg);
                case TypeCode.Double:
                    dlg = DoubleHasher;
                    return DoubleHasher(o, ref dlg);
                default:
                    if (o is IPythonObject) {
                        dlg = new OptimizedUserHasher(this, ((IPythonObject)o).PythonType).Hasher;
                    } else {
                        dlg = new OptimizedBuiltinHasher(this, o.GetType()).Hasher;
                    }

                    return dlg(o, ref dlg);
            }
        }

        private int IntHasherImpl(object o, ref HashDelegate dlg) {
            if (o != null && o.GetType() == typeof(int)) {
                return o.GetHashCode();
            }

            dlg = FallbackHasher;
            return FallbackHasher(o, ref dlg);
        }

        private int DoubleHasherImpl(object o, ref HashDelegate dlg) {
            if (o != null && o.GetType() == typeof(double)) {
                return DoubleOps.__hash__((double)o);
            }

            dlg = FallbackHasher;
            return FallbackHasher(o, ref dlg);
        }

        private int StringHasherImpl(object o, ref HashDelegate dlg) {
            if (o != null && o.GetType() == typeof(string)) {
                return o.GetHashCode();
            }

            dlg = FallbackHasher;
            return FallbackHasher(o, ref dlg);
        }

        private int FallbackHasherImpl(object o, ref HashDelegate dlg) {
            return PythonOps.Hash(this.SharedContext, o);
        }

        private sealed class OptimizedUserHasher {
            private readonly PythonContext _context;
            private readonly PythonType _pt;

            public OptimizedUserHasher(PythonContext context, PythonType pt) {
                _context = context;
                _pt = pt;
            }

            public int Hasher(object o, ref HashDelegate dlg) {
                IPythonObject ipo = o as IPythonObject;
                if (ipo != null && ipo.PythonType == _pt) {
                    return _pt.Hash(o);
                }

                dlg = _context.FallbackHasher;
                return _context.FallbackHasher(o, ref dlg);
            }
        }

        private sealed class OptimizedBuiltinHasher {
            private readonly PythonContext _context;
            private readonly Type _type;
            private readonly PythonType _pt;

            public OptimizedBuiltinHasher(PythonContext context, Type type) {
                _context = context;
                _type = type;
                _pt = DynamicHelpers.GetPythonTypeFromType(type);
            }

            public int Hasher(object o, ref HashDelegate dlg) {
                if (o != null && o.GetType() == _type) {
                    return _pt.Hash(o);
                }

                dlg = _context.FallbackHasher;
                return _context.FallbackHasher(o, ref dlg);
            }
        }

#endregion

        public override LanguageOptions/*!*/ Options {
            get { return PythonOptions; }
        }

        /// <summary>
        /// Checks to see if module state has the current value stored already.
        /// </summary>
        public bool HasModuleState(object key) {
            EnsureModuleState();

            lock (_moduleState) {
                return _moduleState.ContainsKey(key);
            }
        }

        private void EnsureModuleState() {
            if (_moduleState == null) {
                Interlocked.CompareExchange(ref _moduleState, new Dictionary<object, object>(), null);
            }
        }

        /// <summary>
        /// Gets per-runtime state used by a module.  The module should have a unique key for
        /// each piece of state it needs to store.
        /// </summary>
        public object GetModuleState(object key) {
            EnsureModuleState();

            lock (_moduleState) {
                Debug.Assert(_moduleState.ContainsKey(key));

                return _moduleState[key];
            }
        }

        /// <summary>
        /// Sets per-runtime state used by a module.  The module should have a unique key for
        /// each piece of state it needs to store.
        /// </summary>
        public void SetModuleState(object key, object value) {
            EnsureModuleState();

            lock (_moduleState) {
                _moduleState[key] = value;
            }
        }

        /// <summary>
        /// Sets per-runtime state used by a module and returns the previous value.  The module
        /// should have a unique key for each piece of state it needs to store.
        /// </summary>
        public object GetSetModuleState(object key, object value) {
            EnsureModuleState();

            lock (_moduleState) {
                object result;
                _moduleState.TryGetValue(key, out result);
                _moduleState[key] = value;
                return result;
            }
        }

        /// <summary>
        /// Sets per-runtime state used by a module and returns the previous value.  The module
        /// should have a unique key for each piece of state it needs to store.
        /// </summary>
        public T GetOrCreateModuleState<T>(object key, Func<T> value) where T : class {
            EnsureModuleState();

            lock (_moduleState) {
                object result;
                if (!_moduleState.TryGetValue(key, out result)) {
                    _moduleState[key] = result = value();
                }
                return (result as T);
            }
        }

        public PythonType EnsureModuleException(object key, PythonDictionary dict, string name, string module) {
            return (PythonType)(dict[name] = GetOrCreateModuleState(
                key,
                () => PythonExceptions.CreateSubType(this, PythonExceptions.Exception, name, module, null, PythonType.DefaultMakeException)
            ));
        }

        public PythonType EnsureModuleException(object key, PythonType baseType, PythonDictionary dict, string name, string module) {
            return (PythonType)(dict[name] = GetOrCreateModuleState(
                key,
                () => PythonExceptions.CreateSubType(this, baseType, name, module, null, PythonType.DefaultMakeException)
            ));
        }

        public PythonType EnsureModuleException(object key, PythonType baseType, Type underlyingType, PythonDictionary dict, string name, string module, Func<string, Exception, Exception> exceptionMaker) {
            return (PythonType)(dict[name] = GetOrCreateModuleState(
                key,
                () => PythonExceptions.CreateSubType(this, baseType, underlyingType, name, module, null, exceptionMaker)
            ));
        }

        public PythonType EnsureModuleException(object key, PythonType[] baseTypes, Type underlyingType, PythonDictionary dict, string name, string module) {
            return (PythonType)(dict[name] = GetOrCreateModuleState(
                key,
                () => PythonExceptions.CreateSubType(this, baseTypes, underlyingType, name, module, null, PythonType.DefaultMakeException)
            ));
        }

        internal PythonOptions/*!*/ PythonOptions {
            get {
                return _options;
            }
        }

        public override Guid VendorGuid {
            get {
                return LanguageVendor_Microsoft;
            }
        }

        public override Guid LanguageGuid {
            get {
                return PythonLanguageGuid;
            }
        }

        public PythonModule/*!*/ SystemState {
            get {
                return _systemState;
            }
        }

        public PythonModule/*!*/ ClrModule {
            get {
                if (_clrModule == null) {
                    Interlocked.CompareExchange(ref _clrModule, CreateBuiltinModule("clr"), null);
                }

                return _clrModule;
            }
        }

        internal bool TryGetSystemPath(out List path) {
            object val;
            if (SystemState.__dict__.TryGetValue("path", out val)) {
                path = val as List;
            } else {
                path = null;
            }

            return path != null;
        }

        internal object SystemStandardOut {
            get {
                return GetSystemStateValue("stdout");
            }
        }

        internal object SystemStandardIn {
            get {
                return GetSystemStateValue("stdin");
            }
        }

        internal object SystemStandardError {
            get {
                return GetSystemStateValue("stderr");
            }
        }

        internal IDictionary<object, object> SystemStateModules {
            get {
                return _modulesDict;
            }
        }

        internal PythonModule GetModuleByName(string/*!*/ name) {
            Assert.NotNull(name);
            object scopeObj;
            PythonModule module;
            if (SystemStateModules.TryGetValue(name, out scopeObj) && (module = scopeObj as PythonModule) != null) {
                return module;
            }
            return null;
        }

        internal PythonModule GetModuleByPath(string/*!*/ path) {
            Assert.NotNull(path);
            foreach (object moduleObj in SystemStateModules.Values) {
                PythonModule module = moduleObj as PythonModule;
                if (module != null) {
                    if (DomainManager.Platform.PathComparer.Compare(module.GetFile(), path) == 0) {
                        return module;
                    }
                }
            }
            return null;
        }

        public override Version LanguageVersion {
            get {
                // Assembly.GetName() can't be called in Silverlight...
                return GetPythonVersion();
            }
        }

        internal static Version GetPythonVersion() {
            return new AssemblyName(typeof(PythonContext).Assembly.FullName).Version;
        }

        internal FloatFormat FloatFormat {
            get {
                return _floatFormat;
            }
            set {
                _floatFormat = value;
            }
        }

        internal FloatFormat DoubleFormat {
            get {
                return _doubleFormat;
            }
            set {
                _doubleFormat = value;
            }
        }

        /// <summary>
        /// Initializes the sys module on startup.  Called both to load and reload sys
        /// </summary>
        private void InitializeSystemState() {
            // These fields do not get reset on "reload(sys)", we populate them once on startup
            SetSystemStateValue("argv", List.FromArrayNoCopy(new object[] { String.Empty }));
            SetSystemStateValue("modules", _modulesDict);
            InitializeSysFlags();

            _modulesDict["sys"] = _systemState;

            SetSystemStateValue("path", new List(3));

            SetSystemStateValue("ps1", ">>> ");
            SetSystemStateValue("ps2", "... ");

            SetStandardIO();

            SysModule.PerformModuleReload(this, _systemState.__dict__);
        }

        internal bool EmitDebugSymbols(SourceUnit sourceUnit) {
            return sourceUnit.EmitDebugSymbols && (PythonOptions.NoDebug == null || !PythonOptions.NoDebug.IsMatch(sourceUnit.Path));
        }

        private void InitializeSysFlags() {
            // sys.flags
            SysModule.SysFlags flags = new SysModule.SysFlags();
            SetSystemStateValue("flags", flags);
            flags.debug = _options.Debug ? 1 : 0;
            flags.py3k_warning = _options.WarnPython30 ? 1 : 0;
            SetSystemStateValue("py3kwarning", _options.WarnPython30);
            flags.inspect = flags.interactive = _options.Inspect ? 1 : 0;
            if (_options.StripDocStrings) {
                flags.optimize = 2;
            } else if (_options.Optimize) {
                flags.optimize = 1;
            }
            flags.dont_write_bytecode = 1;
            SetSystemStateValue("dont_write_bytecode", true);
            flags.no_user_site = _options.NoUserSite ? 1 : 0;
            flags.no_site = _options.NoSite ? 1 : 0;
            flags.ignore_environment = _options.IgnoreEnvironment ? 1 : 0;
            switch (_options.IndentationInconsistencySeverity) {
                case Severity.Warning:
                    flags.tabcheck = 1;
                    break;
                case Severity.Error:
                    flags.tabcheck = 2;
                    break;
            }
            flags.verbose = _options.Verbose ? 1 : 0;
            flags.unicode = 1;
            flags.bytes_warning = _options.BytesWarning ? 1 : 0;
        }

        internal bool ShouldInterpret(PythonCompilerOptions options, SourceUnit source) {
            // We have to turn off adaptive compilation in debug mode to
            // support mangaged debuggers. Also turn off in optimized mode.
            bool adaptiveCompilation = !_options.NoAdaptiveCompilation && !EmitDebugSymbols(source);

            return options.Interpreted || adaptiveCompilation;
        }

        private static PyAst.PythonAst ParseAndBindAst(CompilerContext context) {
            ScriptCodeParseResult properties = ScriptCodeParseResult.Complete;
            bool propertiesSet = false;
            int errorCode = 0;

            PyAst.PythonAst ast;
            using (Parser parser = Parser.CreateParser(context, PythonContext.GetPythonOptions(null))) {
                switch (context.SourceUnit.Kind) {
                    case SourceCodeKind.InteractiveCode:
                        ast = parser.ParseInteractiveCode(out properties);
                        propertiesSet = true;
                        break;

                    case SourceCodeKind.Expression:
                        ast = parser.ParseTopExpression();
                        break;

                    case SourceCodeKind.SingleStatement:
                        ast = parser.ParseSingleStatement();
                        break;

                    case SourceCodeKind.File:
                        ast = parser.ParseFile(true, false);
                        break;

                    case SourceCodeKind.Statements:
                        ast = parser.ParseFile(false, false);
                        break;

                    default:
                    case SourceCodeKind.AutoDetect:
                        ast = parser.ParseFile(true, true);
                        break;
                }

                errorCode = parser.ErrorCode;
            }

            if (!propertiesSet && errorCode != 0) {
                properties = ScriptCodeParseResult.Invalid;
            }

            context.SourceUnit.CodeProperties = properties;

            if (errorCode != 0 || properties == ScriptCodeParseResult.Empty) {
                return null;
            }

            ast.Bind();
            return ast;
        }

        internal static ScriptCode CompilePythonCode(SourceUnit/*!*/ sourceUnit, CompilerOptions/*!*/ options, ErrorSink/*!*/ errorSink) {
            var pythonOptions = (PythonCompilerOptions)options;

            if (sourceUnit.Kind == SourceCodeKind.File) {
                pythonOptions.Module |= ModuleOptions.Initialize;
            }

            CompilerContext context = new CompilerContext(sourceUnit, options, errorSink);

            PyAst.PythonAst ast = ParseAndBindAst(context);
            if (ast == null) {
                return null;
            }
            
            return ast.ToScriptCode();
        }

        public override ScriptCode CompileSourceCode(SourceUnit/*!*/ sourceUnit, CompilerOptions/*!*/ options, ErrorSink/*!*/ errorSink) {            
            ScriptCode res = CompilePythonCode(sourceUnit, options, errorSink);
            if (res != null) {
                Scope scope = res.CreateScope();

                // if this is an optimized module we need to initialize the optimized scope.
                // Optimized scopes come w/ extensions already attached so we use that to know
                // if we're optimized or not.
                PythonScopeExtension scopeExtension = (PythonScopeExtension)scope.GetExtension(ContextId);
                if (scopeExtension != null) {
                    InitializeModule(sourceUnit.Path, scopeExtension.ModuleContext, res, ModuleOptions.None);
                }
            }

            return res;
        }

        public override ScriptCode/*!*/ LoadCompiledCode(Delegate/*!*/ method, string path, string customData) {
            SourceUnit su = new SourceUnit(this, NullTextContentProvider.Null, path, SourceCodeKind.File);
            return new OnDiskScriptCode((LookupCompilationDelegate)method, su, customData);
        }

        public override SourceCodeReader/*!*/ GetSourceReader(Stream/*!*/ stream, Encoding/*!*/ defaultEncoding, string path) {
            ContractUtils.RequiresNotNull(stream, "stream");
            ContractUtils.RequiresNotNull(defaultEncoding, "defaultEncoding");
            ContractUtils.Requires(stream.CanSeek && stream.CanRead, "stream", "The stream must support seeking and reading");

            // Python 3 uses UTF-8 as default
            Encoding encoding = Encoding.UTF8;

            long startPosition = stream.Position;

            StreamReader sr = new StreamReader(stream, PythonAsciiEncoding.SourceEncoding);
            byte[] bomBuffer = new byte[3];
            int bomRead = stream.Read(bomBuffer, 0, 3);
            int bytesRead = 0;
            bool isUtf8 = false;
            if (bomRead == 3 && (bomBuffer[0] == 0xef && bomBuffer[1] == 0xbb && bomBuffer[2] == 0xbf)) {
                isUtf8 = true;
                bytesRead = 3;
            } else {
                stream.Seek(0, SeekOrigin.Begin);
            }

            string line;
            try {
                line = ReadOneLine(sr, ref bytesRead);
            } catch (BadSourceException) {
                throw ReportEncodingError(stream, path);                
            }

            bool gotEncoding = false;
            string encodingName = null;
            // magic encoding must be on line 1 or 2
            if (line != null && !(gotEncoding = Tokenizer.TryGetEncoding(defaultEncoding, line, ref encoding, out encodingName))) {
                try {
                    line = ReadOneLine(sr, ref bytesRead);
                } catch (BadSourceException) {
                    throw ReportEncodingError(stream, path);
                }

                if (line != null) {
                    gotEncoding = Tokenizer.TryGetEncoding(defaultEncoding, line, ref encoding, out encodingName);
                }
            }

            if (gotEncoding && isUtf8 && encodingName != "utf-8") {
                // we have both a BOM & an encoding type, throw an error
                throw new IOException("file has both Unicode marker and PEP-263 file encoding.  You can only use \"utf-8\" as the encoding name when a BOM is present.");
            } else if (encoding == null) {
                throw new IOException("unknown encoding type");
            }

            // if we didn't get an encoding seek back to the beginning...
            if (!gotEncoding || stream.Position != stream.Length) {
                stream.Seek(startPosition, SeekOrigin.Begin);
            }

            // re-read w/ the correct encoding type...
            return new SourceCodeReader(new StreamReader(stream, encoding), encoding);
        }

        internal static Exception ReportEncodingError(Stream stream, string path) {
            stream.Seek(0, SeekOrigin.Begin);
            byte[] buffer = new byte[1024];
            int bytesRead = 0;
            int curLine = 1, curOffset = 1, index = 0;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != -1) {
                for (int i = 0; i < bytesRead; i++) {
                    if (buffer[i] > 0x7f) {
                        return PythonOps.BadSourceError(
                            buffer[i], 
                            new SourceSpan(
                                new SourceLocation(index, curLine, curOffset),
                                new SourceLocation(index, curLine, curOffset)
                            ), 
                            path
                        );
                    } else if (buffer[i] == '\n') {
                        curLine++;
                        curOffset = 1;
                    } else {
                        curOffset++;
                    }

                    index++;
                }
            }

            return new InvalidOperationException();
        }

        /// <summary>
        /// Reads one line keeping track of the # of bytes read
        /// </summary>
        private static string ReadOneLine(StreamReader reader, ref int totalRead) {
            Stream sr = reader.BaseStream;
            byte[] buffer = new byte[256];
            StringBuilder builder = null;
            
            int bytesRead = sr.Read(buffer, 0, buffer.Length);

            while (bytesRead > 0) {
                totalRead += bytesRead;

                bool foundEnd = false;
                for (int i = 0; i < bytesRead; i++) {
                    if (buffer[i] == '\r') {
                        if (i + 1 < bytesRead) {
                            if (buffer[i + 1] == '\n') {
                                totalRead -= (bytesRead - (i + 2));   // skip cr/lf
                                sr.Seek(i + 2, SeekOrigin.Begin);
                                reader.DiscardBufferedData();
                                foundEnd = true;
                            }
                        } else {
                            totalRead -= (bytesRead - (i + 1)); // skip cr
                            sr.Seek(i + 1, SeekOrigin.Begin);
                            reader.DiscardBufferedData();
                            foundEnd = true;
                        }
                    } else if (buffer[i] == '\n') {
                        totalRead -= (bytesRead - (i + 1)); // skip lf
                        sr.Seek(i + 1, SeekOrigin.Begin);
                        reader.DiscardBufferedData();
                        foundEnd = true;
                    }

                    if (foundEnd) {
                        if (builder != null) {
                            builder.Append(buffer.MakeString(), 0, i);
                            return builder.ToString();
                        }
                        return buffer.MakeString().Substring(0, i);
                    }
                }

                if (builder == null) builder = new StringBuilder();
                builder.Append(buffer.MakeString(), 0, bytesRead);
                bytesRead = sr.Read(buffer, 0, buffer.Length);
            }

            // no string
            if (builder == null) {
                return null;
            }

            // no new-line
            return builder.ToString();
        }

#if FEATURE_CODEDOM
        // Convert a CodeDom to source code, and output the generated code and the line number mappings (if any)
        public override SourceUnit/*!*/ GenerateSourceCode(System.CodeDom.CodeObject codeDom, string path, SourceCodeKind kind) {
            return new IronPython.Hosting.PythonCodeDomCodeGen().GenerateCode((System.CodeDom.CodeMemberMethod)codeDom, this, path, kind);
        }
#endif

#region Scopes

        public override Scope GetScope(string/*!*/ path) {
            PythonModule module = GetModuleByPath(path);
            return (module != null) ? module.Scope : null;
        }

        public PythonModule/*!*/ InitializeModule(string fileName, ModuleContext moduleContext, ScriptCode scriptCode, ModuleOptions options) {
            if ((options & ModuleOptions.NoBuiltins) == 0) {
                moduleContext.InitializeBuiltins((options & ModuleOptions.ModuleBuiltins) != 0);
            }

            // If the filename is __init__.py then this is the initialization code
            // for a package and we need to set the __path__ variable appropriately
            if (fileName != null && Path.GetFileName(fileName) == "__init__.py") {
                string dirname = Path.GetDirectoryName(fileName);
                string dir_path = DomainManager.Platform.GetFullPath(dirname);
                moduleContext.Globals["__path__"] = PythonOps.MakeList(dir_path);
            }

            moduleContext.ShowCls = (options & ModuleOptions.ShowClsMethods) != 0;
            moduleContext.Features = options;

            if ((options & ModuleOptions.Initialize) != 0) {
                scriptCode.Run(moduleContext.GlobalScope);

                if (!moduleContext.Globals.ContainsKey("__package__")) {
                    moduleContext.Globals["__package__"] = null;
                }
            }

            return moduleContext.Module;
        }

        public override ScopeExtension CreateScopeExtension(Scope scope) {
            var ret = new PythonScopeExtension(this, scope);
            ret.ModuleContext.InitializeBuiltins(false);

            return ret;
        }

        public PythonModule/*!*/ CompileModule(string fileName, string moduleName, SourceUnit sourceCode, ModuleOptions options) {
            ScriptCode compiledCode;
            return CompileModule(fileName, moduleName, sourceCode, options, out compiledCode);
        }

        public PythonModule/*!*/ CompileModule(string fileName, string moduleName, SourceUnit sourceCode, ModuleOptions options, out ScriptCode scriptCode) {
            ContractUtils.RequiresNotNull(fileName, "fileName");
            ContractUtils.RequiresNotNull(moduleName, "moduleName");
            ContractUtils.RequiresNotNull(sourceCode, "sourceCode");

            scriptCode = GetScriptCode(sourceCode, moduleName, options);
            Scope scope = scriptCode.CreateScope();
            return InitializeModule(fileName, ((PythonScopeExtension)scope.GetExtension(ContextId)).ModuleContext, scriptCode, options);
        }

        internal ScriptCode GetScriptCode(SourceUnit sourceCode, string moduleName, ModuleOptions options) {
            return GetScriptCode(sourceCode, moduleName, options, null);
        }

        internal ScriptCode GetScriptCode(SourceUnit sourceCode, string moduleName, ModuleOptions options, Compiler.CompilationMode mode) {
            PythonCompilerOptions compilerOptions = GetPythonCompilerOptions();

            compilerOptions.SkipFirstLine = (options & ModuleOptions.SkipFirstLine) != 0;
            compilerOptions.ModuleName = moduleName;
            compilerOptions.Module = options;
            compilerOptions.CompilationMode = mode;

            return CompilePythonCode(sourceCode, compilerOptions, ThrowingErrorSink.Default);
        }

        internal PythonModule GetBuiltinModule(string name) {
            lock (this) {
                PythonModule mod = CreateBuiltinModule(name);
                if (mod != null) {
                    PublishModule(name, mod);
                    return mod;
                }

                return null;
            }
        }

        internal PythonModule CreateBuiltinModule(string name) {
            Type type;
            if (BuiltinModules.TryGetValue(name, out type)) {
                // RuntimeHelpers.RunClassConstructor
                // run the type's .cctor before doing any custom reflection on the type.
                // This allows modules to lazily initialize PythonType's to custom values
                // rather than having them get populated w/ the ReflectedType.  W/o this the
                // cctor runs after we've done a bunch of reflection over the type that doesn't
                // force the cctor to run.
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                return CreateBuiltinModule(name, type);
            }

            return null;
        }

        internal PythonModule/*!*/ CreateBuiltinModule(string moduleName, Type type) {
            PythonDictionary dict;
            if (type.IsSubclassOf(typeof(BuiltinPythonModule))) {
                // an optimized Python module.
                var builtinModule = (BuiltinPythonModule)Activator.CreateInstance(type, this);

                var globals = new Dictionary<string, PythonGlobal>();
                var globalStorage = new InstancedModuleDictionaryStorage(builtinModule, globals);
                dict = new PythonDictionary(globalStorage);
                
                var names = builtinModule.GetGlobalVariableNames();
                var codeContext = new ModuleContext(dict, this).GlobalContext;
                foreach (var name in names) {
                    globals[name] = new PythonGlobal(codeContext, name);
                }

                builtinModule.Initialize(codeContext, globals);
            } else {
                dict = new PythonDictionary(new ModuleDictionaryStorage(type));

                if (type == typeof(Builtin)) {
                    Builtin.PerformModuleReload(this, dict);
                } else if (type != typeof(SysModule)) { // will be performed by hand later, see InitializeSystemState
                    MethodInfo reload = type.GetMethod("PerformModuleReload");
                    if (reload != null) {
                        Debug.Assert(reload.IsStatic);

                        reload.Invoke(null, new object[] { this, dict });
                    }

                }
            }

            PythonModule mod = new PythonModule(dict);
            mod.__dict__["__name__"] = moduleName;
            mod.__dict__["__package__"] = null;
            return mod;
        }

        public void PublishModule(string/*!*/ name, PythonModule/*!*/ module) {
            ContractUtils.RequiresNotNull(name, "name");
            ContractUtils.RequiresNotNull(module, "module");
            SystemStateModules[name] = module;
        }

        internal PythonModule GetReloadableModule(PythonModule/*!*/ module) {
            Assert.NotNull(module);

            object name;
            if (!module.__dict__._storage.TryGetName(out name) || !(name is string)) {
                throw PythonOps.SystemError("nameless module");
            }

            if (!SystemStateModules.ContainsKey(name)) {
                throw PythonOps.ImportError("module {0} not in sys.modules", name);
            }

            return module;
        }

#endregion

        public object GetWarningsModule() {
            object warnings = null;
            try {
                if (!_importWarningThrows) {
                    warnings = Importer.ImportModule(SharedContext, new PythonDictionary(), "warnings", false, -1);
                }
            } catch {
                // don't repeatedly import after it fails
                _importWarningThrows = true;
            }
            return warnings;
        }

        public void EnsureEncodings() {
            if (!_importedEncodings) {
                try {
                    Importer.ImportModule(SharedContext, new PythonDictionary(), "encodings", false, -1);
                } catch (ImportException) {
                }
                _importedEncodings = true;
            }
        }

        internal ModuleGlobalCache GetModuleGlobalCache(string name) {
            ModuleGlobalCache res;
            if (!TryGetModuleGlobalCache(name, out res)) {
                res = ModuleGlobalCache.NoCache;
            }

            return res;
        }

#region Assembly Loading
#if FEATURE_ASSEMBLY_RESOLVE && FEATURE_FILESYSTEM

        internal Assembly LoadAssemblyFromFile(string file) {
            // check all files in the path...
            List path;
            if (TryGetSystemPath(out path)) {
                IEnumerator ie = PythonOps.GetEnumerator(path);
                while (ie.MoveNext()) {
                    string str;
                    if (TryConvertToString(ie.Current, out str)) {
                        string fullName = Path.Combine(str, file);
                        Assembly res;

                        if (TryLoadAssemblyFromFileWithPath(fullName, out res)) return res;
                        if (TryLoadAssemblyFromFileWithPath(fullName + ".EXE", out res)) return res;
                        if (TryLoadAssemblyFromFileWithPath(fullName + ".DLL", out res)) return res;
                    }
                }
            }
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFile")]
        internal bool TryLoadAssemblyFromFileWithPath(string path, out Assembly res) {
            if (File.Exists(path) && Path.IsPathRooted(path)) {
                res = Assembly.LoadFile(path);
                if (res != null) {
#if !CLR2
                    _loadedAssemblies.Add(res);
#endif
                    return true;
                }
            }

            res = null;
            return false;
        }

        internal Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
#if !CLR2 && !ANDROID
            if (args.RequestingAssembly != null && !_loadedAssemblies.Contains(args.RequestingAssembly)) {
                return null;
            }
#endif
            AssemblyName an = new AssemblyName(args.Name);
            try {
                return LoadAssemblyFromFile(an.Name);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// We use Assembly.LoadFile to load assemblies from a path specified by the script (in LoadAssemblyFromFileWithPath).
        /// However, when the CLR loader tries to resolve any of assembly references, it will not be able to
        /// find the dependencies, unless we can hook into the CLR loader.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]   // avoid inlining due to LinkDemand from assembly resolve.
        private void HookAssemblyResolve() {
            AppDomain.CurrentDomain.AssemblyResolve += _resolveHolder.AssemblyResolveEvent;
        }

        class AssemblyResolveHolder {
            private readonly WeakReference _context;

            public AssemblyResolveHolder(PythonContext context) {
                _context = new WeakReference(context);
            }

            internal Assembly AssemblyResolveEvent(object sender, ResolveEventArgs args) {
                PythonContext context = (PythonContext)_context.Target;
                if (context != null) {
                    return context.CurrentDomain_AssemblyResolve(sender, args);
                } else {
                    AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolveEvent;
                    return null;
                }
            }
        }

        private void UnhookAssemblyResolve() {
            try {
                AppDomain.CurrentDomain.AssemblyResolve -= _resolveHolder.AssemblyResolveEvent;
            } catch (System.Security.SecurityException) {
                // We may not have SecurityPermissionFlag.ControlAppDomain. 
                // If so, we will not look up sys.path for module loads
            }
        }
#endif
#endregion

        public override ICollection<string> GetSearchPaths() {
            List<string> result = new List<string>();
            List paths;
            if (TryGetSystemPath(out paths)) {
                IEnumerator ie = PythonOps.GetEnumerator(paths);
                while (ie.MoveNext()) {
                    string str;
                    if (TryConvertToString(ie.Current, out str)) {
                        result.Add(str);
                    }
                }
            }
            return result;
        }

        public override void SetSearchPaths(ICollection<string> paths) {
            SetSystemStateValue("path", new List(paths));
        }

        public override void Shutdown() {
            object callable;

#if FEATURE_ASSEMBLY_RESOLVE && FEATURE_FILESYSTEM
            UnhookAssemblyResolve();
#endif

            object threadingMod;
            if (SystemStateModules.TryGetValue("threading", out threadingMod) &&
                (threadingMod is PythonModule) &&
                ((PythonModule)threadingMod).__dict__.TryGetValue("_shutdown", out callable)) {
                try {
                    PythonCalls.Call(SharedContext, callable);
                } catch (Exception e) {
                    PythonOps.PrintWithDest(
                        SharedContext,
                        SystemStandardError,
                        String.Format("Exception {0} ignored", FormatException(e))
                    );
                }
            } 
            
            try {
                if (_systemState.__dict__.TryGetValue("exitfunc", out callable)) {
                    PythonCalls.Call(SharedContext, callable);
                }
            } finally {
#if FEATURE_BASIC_CONSOLE
                if (PythonOptions.PerfStats) {
                    PerfTrack.DumpStats();
                }
#endif
            }
        }

        // TODO: ExceptionFormatter service
#region Stack Traces and Exceptions

        public override string FormatException(Exception exception) {
            ContractUtils.RequiresNotNull(exception, "exception");

            SyntaxErrorException syntax_error = exception as SyntaxErrorException;
            if (syntax_error != null) {
                return FormatPythonSyntaxError(syntax_error);
            }

            StringBuilder result = new StringBuilder();

            object pythonEx = PythonExceptions.ToPython(exception);

            if (exception.InnerException != null) {
                var pythonInnerException = exception.InnerException.GetPythonException() as PythonExceptions.BaseException;
                if (pythonInnerException != null) {
                    // add the nested/chained exception
                    result.AppendLine(FormatException(exception.InnerException));
                    result.AppendLine();

                    // check whether this is implicit or explicit
                    if (!((PythonExceptions.BaseException)pythonEx).IsImplicitException) {
                        result.AppendLine("The above exception was the direct cause of the following exception.");
                    } else {
                        result.AppendLine("During handing of the above exception, another exception occurred:");
                    }
                    result.AppendLine();
                }
            }

            result.Append(FormatStackTraces(exception));
            result.Append(FormatPythonException(pythonEx));

            if (Options.ShowClrExceptions) {
                result.Append(FormatCLSException(exception));
            }

            return result.ToString();
        }

        internal static string FormatPythonSyntaxError(SyntaxErrorException e) {
            string sourceLine = GetSourceLine(e);

            if (e.GetData(_syntaxErrorNoCaret) == null) {
                return String.Format(
                    "  File \"{1}\", line {2}{0}" +
                    "    {3}{0}" +
                    "    {4}^{0}" +
                    "{5}: {6}",
                    Environment.NewLine,
                    e.GetSymbolDocumentName(),
                    e.Line > 0 ? e.Line.ToString() : "?",
                    (sourceLine != null) ? sourceLine.TrimEnd('\n').Replace('\t', ' ') : null,
                    new String(' ', e.Column != 0 ? e.Column - 1 : 0),
                    GetPythonExceptionClassName(PythonExceptions.ToPython(e)), e.Message);
            }

            return String.Format(
                    "  File \"{1}\", line {2}{0}" +
                    "{3}: {4}",
                    Environment.NewLine,
                    e.GetSymbolDocumentName(),
                    new String(' ', e.Column != 0 ? e.Column - 1 : 0),
                    GetPythonExceptionClassName(PythonExceptions.ToPython(e)), e.Message);
        }

        internal static string GetSourceLine(SyntaxErrorException e) {
            if (e.SourceCode == null) {
                return null;
            }
            try {
                using (StringReader reader = new StringReader(e.SourceCode)) {
                    char[] buffer = new char[80];
                    int curLine = 1;
                    StringBuilder line = new StringBuilder();
                    int bytesRead;

                    // we can't use SourceUnit.GetCodeLines because Python includes the new lines
                    // in the syntax error and the codeop standard library depends upon this
                    // being correct
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0 && curLine <= e.Line) {
                        for (int i = 0; i < bytesRead; i++) {
                            if (curLine == e.Line) {
                                line.Append(buffer[i]);
                            }

                            if (buffer[i] == '\n') {
                                curLine++;
                            }

                            if (curLine > e.Line) {
                                break;
                            }
                        }
                    }

                    return line.ToString();
                }
            } catch (IOException) {
                return null;
            }
        }

        private static string FormatCLSException(Exception e) {
            StringBuilder result = new StringBuilder();
            result.AppendLine("CLR Exception: ");
            while (e != null) {
                result.Append("    ");
                result.AppendLine(e.GetType().Name);
                if (!String.IsNullOrEmpty(e.Message)) {
                    result.AppendLine(": ");
                    result.AppendLine(e.Message);
                } else {
                    result.AppendLine();
                }

                e = e.InnerException;
            }

            return result.ToString();
        }

        internal static string FormatPythonException(object pythonException) {
            string result = "";

            // dump the python exception.
            if (pythonException != null) {
                string str = pythonException as string;
                if (str != null) {
                    result += str;
                } else {
                    result += GetPythonExceptionClassName(pythonException);

                    string excepStr = PythonOps.ToString(pythonException);

                    if (!String.IsNullOrEmpty(excepStr)) {
                        result += ": " + excepStr;
                    }
                }
            }

            return result;
        }

        private static string GetPythonExceptionClassName(object pythonException) {
            string className = "";
            object val;
            if (PythonOps.TryGetBoundAttr(pythonException, "__class__", out val)) {
                if (PythonOps.TryGetBoundAttr(val, "__name__", out val)) {
                    className = val.ToString();
                    if (PythonOps.TryGetBoundAttr(pythonException, "__module__", out val)) {
                        string moduleName = val.ToString();
                        if (moduleName != PythonExceptions.DefaultExceptionModule) {
                            className = moduleName + "." + className;
                        }
                    }
                }
            }
            return className;
        }

        public override IList<DynamicStackFrame> GetStackFrames(Exception exception) {
            return PythonOps.GetDynamicStackFrames(exception);
        }

#if !FEATURE_STACK_TRACE
        private string FormatStackTraces(Exception e) {

            StringBuilder result = new StringBuilder();
            result.AppendLine("Traceback (most recent call last):");
            DynamicStackFrame[] dfs = PythonExceptions.GetDynamicStackFrames(e);
            for (int i = 0; i < dfs.Length; ++i) {
                DynamicStackFrame frame = dfs[i];
                result.AppendFormat("  at {0} in {1}, line {2}\n", frame.GetMethodName(), frame.GetFileName(), frame.GetFileLineNumber());
            }

            if (Options.ExceptionDetail) {
                result.AppendLine(e.Message);
            }
            
            return result.ToString();
        }
#else
        private string FormatStackTraces(Exception e) {
            bool printedHeader = false;

            return FormatStackTraces(e, ref printedHeader);
        }

        private string FormatStackTraces(Exception e, ref bool printedHeader) {
            StringBuilder result = new StringBuilder();
            if (Options.ExceptionDetail) {
                if (!printedHeader) {
                    result.AppendLine(e.Message);
                    printedHeader = true;
                }
                IList<System.Diagnostics.StackTrace> traces = ExceptionHelpers.GetExceptionStackTraces(e);

                if (traces != null) {
                    foreach (StackTrace trace in traces) {
                        foreach (StackFrame curFrame in trace.GetFrames()) {
                            result.AppendLine(curFrame.ToString());
                        }
                    }
                }

                if (e.StackTrace != null) result.AppendLine(e.StackTrace.ToString());
                if (e.InnerException != null) result.AppendLine(FormatStackTraces(e.InnerException, ref printedHeader));
            } else {
                result.Append(FormatStackTraceNoDetail(e, ref printedHeader));
            }

            return result.ToString();
        }

        private void PrintHeader(ref StringBuilder result, ref bool printedHeader) {
            if (!printedHeader) {
                result.AppendLine("Traceback (most recent call last):");
                printedHeader = true;
            }
        }

        internal string FormatStackTraceNoDetail(Exception e, ref bool printedHeader) {
            var pythonException = e.GetPythonException() as PythonExceptions.BaseException;
            StringBuilder result = new StringBuilder();

            var traceback = e.GetTraceBack();
            if (traceback != null) {
                PrintHeader(ref result, ref printedHeader);
                result.Append(traceback.Extract());
                return result.ToString();
            }

            var frames = PythonExceptions.GetDynamicStackFrames(e);
            if (frames.Length > 0) PrintHeader(ref result, ref printedHeader);
            for (int i = frames.Length - 1; i >= 0; i--) {
                var frame = frames[i];

                MethodBase method = frame.GetMethod();
                if (CallSiteHelpers.IsInternalFrame(method) ||
                    (method.DeclaringType != null && method.DeclaringType.FullName.StartsWith("IronPython."))) {
                    continue;
                }

                result.AppendLine(FrameToString(frame));
            }
            return result.ToString();
        }

        private static string FrameToString(DynamicStackFrame frame) {
            string methodName = frame.GetMethodName();
            int lineNumber = frame.GetFileLineNumber();

            return String.Format("  File \"{0}\", line {1}, in {2}",
                frame.GetFileName(),
                lineNumber == 0 ? "unknown" : lineNumber.ToString(),
                methodName);
        }

#endif

#endregion
        
        // TODO: Replace all usages with direct access
        internal static PythonContext/*!*/ GetContext(CodeContext/*!*/ context) {
            return context.LanguageContext;
        }
       
        public override TService GetService<TService>(params object[] args) {
            if (typeof(TService) == typeof(TokenizerService)) {
                return (TService)(object)new Tokenizer(ErrorSink.Null, GetPythonCompilerOptions(), true);
            } else if (typeof(TService) == typeof(PythonService)) {
                return (TService)(object)GetPythonService((Microsoft.Scripting.Hosting.ScriptEngine)args[0]);
            } else if (typeof(TService) == typeof(DocumentationProvider)) {
                return (TService)(object)new PythonDocumentationProvider(this);
            }

            return base.GetService<TService>(args);
        }


        /// <summary>
        /// Returns (and creates if necessary) the PythonService that is associated with this PythonContext.
        /// 
        /// The PythonService is used for providing remoted convenience helpers for the DLR hosting APIs.
        /// </summary>
        internal Hosting.PythonService GetPythonService(Microsoft.Scripting.Hosting.ScriptEngine engine) {
            if (_pythonService == null) {
                Interlocked.CompareExchange(ref _pythonService, new Hosting.PythonService(this, engine), null);
            }

            return _pythonService;
        }

        internal static PythonOptions GetPythonOptions(CodeContext context) {
            return DefaultContext.DefaultPythonContext._options;
        }

        internal void InsertIntoPath(int index, string directory) {
            List path;
            if (TryGetSystemPath(out path)) {
                path.insert(index, directory);
            }
        }

        internal void AddToPath(string directory) {
            List path;
            if (TryGetSystemPath(out path)) {
                path.append(directory);
            }
        }

        internal void AddToPath(string directory, int index) {
            List path;
            if (TryGetSystemPath(out path)) {
                path.insert(index, directory);
            }
        }

        internal PythonCompilerOptions GetPythonCompilerOptions() {
            return new PythonCompilerOptions(ModuleOptions.None);
        }

        public override CompilerOptions GetCompilerOptions() {
            return GetPythonCompilerOptions();
        }

        public override CompilerOptions/*!*/ GetCompilerOptions(Scope/*!*/ scope) {
            Assert.NotNull(scope);

            PythonCompilerOptions res = GetPythonCompilerOptions();

            PythonScopeExtension module = (PythonScopeExtension)scope.GetExtension(ContextId);
            if (module != null) {
                res.Module |= module.ModuleContext.Features;
            }

            return res;
        }

        public override void GetExceptionMessage(Exception exception, out string message, out string typeName) {
            object pythonEx = PythonExceptions.ToPython(exception);

            message = FormatPythonException(PythonExceptions.ToPython(exception));
            typeName = GetPythonExceptionClassName(pythonEx);
        }

        /// <summary>
        /// Gets or sets the default encoding for this system state / engine.
        /// </summary>
        public Encoding DefaultEncoding {
            get { return _defaultEncoding; }
            set { _defaultEncoding = value; }
        }

        public string GetDefaultEncodingName() {
            return DefaultEncoding.WebName.ToLower().Replace('-', '_');
        }

        /// <summary>
        /// Dictionary from name to type of all known built-in module names.
        /// </summary>
        internal Dictionary<string, Type> BuiltinModules {
            get {
                return _builtinModulesDict;
            }
        }

        /// <summary>
        /// Dictionary from type to name of all built-in modules.
        /// </summary>
        internal Dictionary<Type, string> BuiltinModuleNames {
            get {
                return _builtinModuleNames;
            }
        }

        private void InitializeBuiltins() {
            // create the __builtin__ module
            BuiltinsDictionaryStorage storage = new BuiltinsDictionaryStorage(BuiltinsChanged);
            PythonDictionary dict = new PythonDictionary(storage);

            Builtin.PerformModuleReload(this, dict);
            _builtinDict = dict;

            _builtins = new PythonModule(dict);

            _modulesDict["builtins"] = _builtins;
        }

        private Dictionary<string, Type> CreateBuiltinTable() {
            Dictionary<string, Type> builtinTable = new Dictionary<string, Type>();

            // We should register builtins, if any, from IronPython.dll
            LoadBuiltins(builtinTable, typeof(PythonContext).Assembly, false);

            // Load builtins from IronPython.Modules
            Assembly ironPythonModules = null;

            try {
                ironPythonModules = DomainManager.Platform.LoadAssembly(GetIronPythonAssembly("IronPython.Modules"));
            } catch (FileNotFoundException) {
                // IronPython.Modules is not available, continue without it...
            }

            if (ironPythonModules != null) {
                LoadBuiltins(builtinTable, ironPythonModules, false);

#if !WIN8
                if (Environment.OSVersion.Platform == PlatformID.Unix) {
                    // we make our nt package show up as a posix package
                    // on unix platforms.  Because we build on top of the 
                    // CLI for all file operations we should be good from
                    // there, but modules that check for the presence of
                    // names (e.g. os) will do the right thing.
                    Debug.Assert(builtinTable.ContainsKey("nt"));
                    builtinTable["posix"] = builtinTable["nt"];
                    builtinTable.Remove("nt");
                }
#endif
            }

            return builtinTable;
        }

        internal void LoadBuiltins(Dictionary<string, Type> builtinTable, Assembly assem, bool updateSys) {
            object[] attrs = assem.GetCustomAttributes(typeof(PythonModuleAttribute), false);
            if (attrs.Length > 0) {
                foreach (PythonModuleAttribute pma in attrs) {
                    if (pma.IsPlatformValid) {
                        builtinTable[pma.Name] = pma.Type;
                        BuiltinModuleNames[pma.Type] = pma.Name;
                    }
                }

                if (updateSys) {
                    SysModule.PublishBuiltinModuleNames(this, _systemState.__dict__);
                }
            }
        }

        public static string GetIronPythonAssembly(string/*!*/ baseName) {
            ContractUtils.RequiresNotNull(baseName, "baseName");
            string fullName = typeof(PythonContext).Assembly.FullName;
            int firstComma = fullName.IndexOf(',');
            return firstComma > 0 ? baseName + fullName.Substring(firstComma) : baseName;
        }

        /// <summary>
        /// TODO: Remove me, or stop caching built-ins.  This is broken if the user changes __builtin__
        /// </summary>
        public PythonModule BuiltinModuleInstance {
            get {
                return _builtins;
            }
        }

        public PythonDictionary BuiltinModuleDict {
            get {
                return _builtinDict;
            }
        }

        private void BuiltinsChanged(object sender, ModuleChangeEventArgs e) {
            ModuleGlobalCache mgc;
            lock (_builtinCache) {
                if (_builtinCache.TryGetValue(e.Name, out mgc)) {
                    switch (e.ChangeType) {
                        case ModuleChangeType.Delete: mgc.Value = Uninitialized.Instance; break;
                        case ModuleChangeType.Set: mgc.Value = e.Value; break;
                    }
                } else {
                    // shouldn't be able to delete before it was set
                    object value = e.ChangeType == ModuleChangeType.Set ? e.Value : Uninitialized.Instance;
                    _builtinCache[e.Name] = new ModuleGlobalCache(value);
                }
            }
        }

        internal bool TryGetModuleGlobalCache(string name, out ModuleGlobalCache cache) {
            lock (_builtinCache) {
                if (!_builtinCache.TryGetValue(name, out cache)) {
                    // only cache values currently in built-ins, everything else will have
                    // no caching policy and will fall back to the LanguageContext.
                    object value;
                    if (BuiltinModuleInstance.__dict__.TryGetValue(name, out value)) {
                        _builtinCache[name] = cache = new ModuleGlobalCache(value);
                    }
                }
            }
            return cache != null;
        }

        internal void SetHostVariables(string prefix, string executable, string versionString) {
            _initialVersionString = !string.IsNullOrEmpty(versionString) ? versionString : GetVersionString();
            _initialExecutable = executable ?? "";
            _initialPrefix = prefix;

#if !SILVERLIGHT
            AddToPath(Path.Combine(prefix, "Lib"), 0);
#endif

            SetHostVariables(SystemState.__dict__);
        }

        internal string InitialPrefix {
            get {
                return _initialPrefix;
            }
        }

        internal void SetHostVariables(PythonDictionary dict) {
            dict["executable"] = _initialExecutable;
            dict["prefix"] = _initialPrefix;
            dict["exec_prefix"] = _initialPrefix;
            dict["base_prefix"] = _initialPrefix;
            dict["base_exec_prefix"] = _initialPrefix;
            SetVersionVariables(dict);
        }

        private void SetVersionVariables(PythonDictionary dict) {
            var implementation = new Implementation();
            dict["implementation"] = implementation;
            dict["version_info"] = implementation.version;
            dict["hexversion"] = implementation.hexversion;
            dict["version"] = implementation.version.GetVersionString(
                    _initialVersionString ?? GetVersionString());
        }

        internal static string GetVersionString() {
            string configuration = BuildInfo.IsDebug ? " DEBUG" : "";
            string platform = Type.GetType("Mono.Runtime") == null ? ".NET" : "Mono";
            string bitness = (IntPtr.Size * 8).ToString();

            return String.Format("{0}{3} ({1}) on {4} {2} ({5}-bit)",
                                PythonContext.IronPythonDisplayName,
                                PythonContext.GetPythonVersion().ToString(),
                                Environment.Version,
                                configuration,
                                platform,
                                bitness
                                );
        }

        private static string GetInitialPrefix() {
#if FEATURE_ASSEMBLY_CODEBASE
            try {
                return typeof(PythonContext).Assembly.CodeBase;
            } catch (SecurityException) {
                // we don't have permissions to get paths...
                return String.Empty;
            } catch (MethodAccessException) {
                return String.Empty;
            }
#else
            return String.Empty;
#endif
        }

        /// <summary>
        /// Gets the member names associated with the object
        /// TODO: Move "GetMemberNames" functionality into MetaObject implementations
        /// </summary>
        public override IList<string> GetMemberNames(object obj) {
            List<string> res = new List<string>();
            foreach (object o in PythonOps.GetAttrNames(SharedContext, obj)) {
                if (o is string) {
                    res.Add((string)o);
                }
            }
            return res;
        }

        public override string/*!*/ FormatObject(DynamicOperations/*!*/ operations, object obj) {
            return PythonOps.Repr(_defaultContext, obj) ?? "None";
        }

        internal object GetSystemStateValue(string name) {
            object val;
            if (SystemState.__dict__.TryGetValue(name, out val)) {
                return val;
            }
            return null;
        }

        internal void SetSystemStateValue(string name, object value) {
            SystemState.__dict__[name] = value;
        }

        internal void DelSystemStateValue(string name) {            
            SystemState.__dict__.Remove(name);
        }

        private void SetStandardIO() {
            SharedIO io = DomainManager.SharedIO;

            var stdin = PythonIOModule.CreateConsole(this, io, ConsoleStreamType.Input, "<stdin>");
            var stdout = PythonIOModule.CreateConsole(this, io, ConsoleStreamType.Output, "<stdout>");
            var stderr = PythonIOModule.CreateConsole(this, io, ConsoleStreamType.ErrorOutput, "<stderr>");

            SetSystemStateValue("__stdin__", stdin);
            SetSystemStateValue("stdin", stdin);

            SetSystemStateValue("__stdout__", stdout);
            SetSystemStateValue("stdout", stdout);

            SetSystemStateValue("__stderr__", stderr);
            SetSystemStateValue("stderr", stderr);
        }

        internal PythonFileManager RawFileManager {
            get {
                return _fileManager;
            }
        }

        internal PythonFileManager/*!*/ FileManager {
            get {
                if (_fileManager == null) {
                    Interlocked.CompareExchange(ref _fileManager, new PythonFileManager(), null);
                }

                return _fileManager;
            }
        }

        public override int ExecuteProgram(SourceUnit/*!*/ program) {
            try {
                PythonCompilerOptions pco = (PythonCompilerOptions)GetCompilerOptions();
                pco.ModuleName = "__main__";
                pco.Module |= ModuleOptions.Initialize;

                program.Execute(pco, ErrorSink.Default);
            } catch (SystemExitException e) {
                object obj;
                return e.GetExitCode(out obj);
            }

            return 0;
        }

        /// <summary> Dictionary of error handlers for string codecs. </summary>
        internal Dictionary<string, object> ErrorHandlers {
            get {
                if (_errorHandlers == null) {
                    Interlocked.CompareExchange(ref _errorHandlers, new Dictionary<string, object>(), null);
                }

                return _errorHandlers;
            }
        }

        /// <summary> Table of functions used for looking for additional codecs. </summary>
        internal List<object> SearchFunctions {
            get {
                if (_searchFunctions == null) {
                    Interlocked.CompareExchange(ref _searchFunctions, new List<object>(), null);
                }

                return _searchFunctions;
            }
        }

        /// <summary>
        /// Gets a SiteLocalStorage when no call site is available.
        /// </summary>
        internal SiteLocalStorage<T> GetGenericSiteStorage<T>() {
            if (_genericSiteStorage == null) {
                Interlocked.CompareExchange(ref _genericSiteStorage, new Dictionary<Type, object>(), null);
            }

            lock (_genericSiteStorage) {
                object res;
                if (!_genericSiteStorage.TryGetValue(typeof(T), out res)) {
                    _genericSiteStorage[typeof(T)] = res = new SiteLocalStorage<T>();
                }
                return (SiteLocalStorage<T>)res;
            }
        }

        internal SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> GetGenericCallSiteStorage() {
            return GetGenericSiteStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>>();

        }

        internal SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object>>> GetGenericCallSiteStorage0() {
            return GetGenericSiteStorage<CallSite<Func<CallSite, CodeContext, object, object>>>();
        }

        internal SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>>> GetGenericKeywordCallSiteStorage() {
            return GetGenericSiteStorage<CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>>>();

        }

#region Object Operations

        public override ConvertBinder/*!*/ CreateConvertBinder(Type/*!*/ toType, bool? explicitCast) {
            if (explicitCast != null) {
                return Convert(toType, (bool)explicitCast ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast).CompatBinder;
            } else {
                return Convert(toType, ConversionResultKind.ImplicitCast).CompatBinder;
            }
        }

        public override DeleteMemberBinder/*!*/ CreateDeleteMemberBinder(string/*!*/ name, bool ignoreCase) {
            if (ignoreCase) {
                return new PythonDeleteMemberBinder(this, name, ignoreCase);
            }
            return DeleteMember(name);
        }

        public override GetMemberBinder/*!*/ CreateGetMemberBinder(string/*!*/ name, bool ignoreCase) {
            if (ignoreCase) {
                return new CompatibilityGetMember(this, name, false);
            }
            return CompatGetMember(name, false);
        }

        public override InvokeBinder/*!*/ CreateInvokeBinder(CallInfo /*!*/ callInfo) {
            return CompatInvoke(callInfo);
        }

        public override BinaryOperationBinder CreateBinaryOperationBinder(ExpressionType operation) {
            return BinaryOperation(operation);
        }

        public override UnaryOperationBinder CreateUnaryOperationBinder(ExpressionType operation) {
            return UnaryOperation(operation);
        }

        public override SetMemberBinder/*!*/ CreateSetMemberBinder(string/*!*/ name, bool ignoreCase) {
            if (ignoreCase) {
                return new PythonSetMemberBinder(this, name, ignoreCase);
            }
            return SetMember(name);
        }

        public override CreateInstanceBinder/*!*/ CreateCreateBinder(CallInfo /*!*/ callInfo) {
            return Create(
                CompatInvoke(callInfo),
                callInfo
            );
        }

#endregion

        internal WeakRefTracker GetSystemPythonTypeWeakRef(PythonType type) {
            object wrt;
            return _systemPythonTypesWeakRefs.TryGetValue(type, out wrt) ?
                (WeakRefTracker)wrt :
                null;
        }

        internal bool SetSystemPythonTypeWeakRef(PythonType type, WeakRefTracker value) {
            lock(_systemPythonTypesWeakRefs) {
                _systemPythonTypesWeakRefs.AddNoLock(type, value);
            }
            return true;
        }

        internal void SetSystemPythonTypeFinalizer(PythonType type, WeakRefTracker value) {
            lock(_systemPythonTypesWeakRefs) {
                _systemPythonTypesWeakRefs.AddNoLock(type, value);
            }
        }

        internal bool TryConvertToWeakReferenceable(object obj, out IWeakReferenceable weakref) {
            IWeakReferenceableByProxy iwrp = obj as IWeakReferenceableByProxy;
            if (iwrp != null) {
                weakref = iwrp.GetWeakRefProxy(this);
                return true;
            }

            IWeakReferenceable iwr = obj as IWeakReferenceable;
            if (iwr != null) {
                weakref = iwr;
                return true;
            }

            weakref = null;
            return false;
        }

        internal IWeakReferenceable ConvertToWeakReferenceable(object obj) {
            IWeakReferenceable iwr;
            if (TryConvertToWeakReferenceable(obj, out iwr)) {
                return iwr;
            } else {
                throw PythonOps.TypeError("cannot create weak reference to '{0}' object", PythonOps.GetPythonTypeName(obj));
            }
        }

#region Per-Runtime Call Sites

        private bool InvokeOperatorWorker(CodeContext/*!*/ context, UnaryOperators oper, object target, out object result) {
            if (_newUnarySites == null) {
                Interlocked.CompareExchange(
                    ref _newUnarySites,
                    new CallSite<Func<CallSite, CodeContext, object, object>>[(int)UnaryOperators.Maximum],
                    null
                );
            }

            if (_newUnarySites[(int)oper] == null) {
                Interlocked.CompareExchange(
                    ref _newUnarySites[(int)oper],
                    CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                        InvokeNone
                    ),
                    null
                );
            }
            CallSite<Func<CallSite, CodeContext, object, object>> site = _newUnarySites[(int)oper];

            var symbol = GetUnarySymbol(oper);
            PythonType pt = DynamicHelpers.GetPythonType(target);
            PythonTypeSlot pts;
            object callable;

            if (pt.TryResolveSlot(context, symbol, out pts) &&
                pts.TryGetValue(context, target, pt, out callable)) {

                result = site.Target(site, context, callable);
                return true;
            }

            result = null;
            return false;
        }

        private static string GetUnarySymbol(UnaryOperators oper) {
            string symbol;
            switch (oper) {
                case UnaryOperators.Repr: symbol = "__repr__"; break;
                case UnaryOperators.Length: symbol = "__len__"; break;
                case UnaryOperators.Hash: symbol = "__hash__"; break;
                case UnaryOperators.String: symbol = "__str__"; break;
                default: throw new ValueErrorException("unknown unary symbol");
            }
            return symbol;
        }

        private bool InvokeOperatorWorker(CodeContext/*!*/ context, TernaryOperators oper, object target, object value1, object value2, out object result) {

            if (_newTernarySites == null) {
                Interlocked.CompareExchange(
                    ref _newTernarySites,
                    new CallSite<Func<CallSite, CodeContext, object, object, object, object>>[(int)TernaryOperators.Maximum],
                    null
                );
            }

            if (_newTernarySites[(int)oper] == null) {
                Interlocked.CompareExchange(
                    ref _newTernarySites[(int)oper],
                    CallSite<Func<CallSite, CodeContext, object, object, object, object>>.Create(
                        Invoke(
                            new CallSignature(2)
                        )
                    ),
                    null
                );
            }
            CallSite<Func<CallSite, CodeContext, object, object, object, object>> site = _newTernarySites[(int)oper];

            string symbol = GetTernarySymbol(oper);
            PythonType pt = DynamicHelpers.GetPythonType(target);
            PythonTypeSlot pts;
            object callable;

            if (pt.TryResolveSlot(context, symbol, out pts) &&
                pts.TryGetValue(context, target, pt, out callable)) {

                result = site.Target(site, context, callable, value1, value2);
                return true;
            }

            result = null;
            return false;
        }

        private static string GetTernarySymbol(TernaryOperators oper) {
            string symbol;
            switch (oper) {
                case TernaryOperators.SetDescriptor: symbol = "__set__"; break;
                case TernaryOperators.GetDescriptor: symbol = "__get__"; break;
                default: throw new ValueErrorException("unknown ternary operator");
            }
            return symbol;
        }

        internal static object InvokeUnaryOperator(CodeContext/*!*/ context, UnaryOperators oper, object target, string errorMsg) {
            object res;
            if (PythonContext.GetContext(context).InvokeOperatorWorker(context, oper, target, out res)) {
                return res;
            }

            throw PythonOps.TypeError(errorMsg);
        }

        internal static object InvokeUnaryOperator(CodeContext/*!*/ context, UnaryOperators oper, object target) {
            object res;
            if (PythonContext.GetContext(context).InvokeOperatorWorker(context, oper, target, out res)) {
                return res;
            }

            throw PythonOps.TypeError(String.Empty);
        }

        internal static bool TryInvokeTernaryOperator(CodeContext/*!*/ context, TernaryOperators oper, object target, object value1, object value2, out object res) {
            return PythonContext.GetContext(context).InvokeOperatorWorker(context, oper, target, value1, value2, out res);
        }

        internal CallSite<Func<CallSite, object, object, int>> CompareSite {
            get {
                if (_compareSite == null) {
                    Interlocked.CompareExchange(ref _compareSite,
                        MakeSortCompareSite(),
                        null
                    );
                }

                return _compareSite;
            }
        }

        internal CallSite<Func<CallSite, object, object, int>> MakeSortCompareSite() {
            return CallSite<Func<CallSite, object, object, int>>.Create(
                Operation(
                    PythonOperationKind.Compare
                )
            );
        }

        internal void SetAttr(CodeContext/*!*/ context, object o, string name, object value) {
            CallSite<Func<CallSite, object, object, object>> site;
            if (_setAttrSites == null) {
                Interlocked.CompareExchange(ref _setAttrSites, new Dictionary<AttrKey, CallSite<Func<CallSite, object, object, object>>>(), null);
            }

            lock (_setAttrSites) {
                AttrKey key = new AttrKey(CompilerHelpers.GetType(o), name);
                if (!_setAttrSites.TryGetValue(key, out site)) {
                    _setAttrSites[key] = site = CallSite<Func<CallSite, object, object, object>>.Create(
                        SetMember(
                            name
                        )
                    );
                }
            }

            site.Target.Invoke(site, o, value);
        }

        internal void DeleteAttr(CodeContext/*!*/ context, object o, string name) {
            AttrKey key = new AttrKey(CompilerHelpers.GetType(o), name);

            if (_deleteAttrSites == null) {
                Interlocked.CompareExchange(ref _deleteAttrSites, new Dictionary<AttrKey, CallSite<Action<CallSite, object>>>(), null);
            }

            CallSite<Action<CallSite, object>> site;
            lock (_deleteAttrSites) {
                if (!_deleteAttrSites.TryGetValue(key, out site)) {
                    _deleteAttrSites[key] = site = CallSite<Action<CallSite, object>>.Create(
                        DeleteMember(name)
                    );
                }
            }

            site.Target(site, o);
        }

        internal CallSite<Func<CallSite, CodeContext, object, string, PythonTuple, object, object>> MetaClassCallSite {
            get {
                if (_metaClassSite == null) {
                    Interlocked.CompareExchange(
                        ref _metaClassSite,
                        CallSite<Func<CallSite, CodeContext, object, string, PythonTuple, object, object>>.Create(
                            Invoke(
                                new CallSignature(3)
                            )
                        ),
                        null
                    );
                }

                return _metaClassSite;
            }
        }

        internal CallSite<Func<CallSite, CodeContext, object, string, object>> WriteCallSite {
            get {
                if (_writeSite == null) {
                    Interlocked.CompareExchange(
                        ref _writeSite,
                        CallSite<Func<CallSite, CodeContext, object, string, object>>.Create(
                            InvokeOne
                        ),
                        null
                    );
                }

                return _writeSite;
            }
        }

        internal CallSite<Func<CallSite, object, object, object>> GetIndexSite {
            get {
                if (_getIndexSite == null) {
                    Interlocked.CompareExchange(
                        ref _getIndexSite,
                        CallSite<Func<CallSite, object, object, object>>.Create(
                            GetIndex(
                                1
                            )
                        ),
                        null
                    );
                }

                return _getIndexSite;
            }
        }

        internal void DelIndex(object target, object index) {
            if (_delIndexSite == null) {
                Interlocked.CompareExchange(
                    ref _delIndexSite,
                    CallSite<Action<CallSite, object, object>>.Create(
                        DeleteIndex(
                            1
                        )
                    ),
                    null
                );
            }


            _delIndexSite.Target(_delIndexSite, target, index);
        }

        internal void DelSlice(object target, object start, object end) {
            if (_delSliceSite == null) {
                Interlocked.CompareExchange(
                    ref _delSliceSite,
                    CallSite<Func<CallSite, object, object, object, object>>.Create(
                        DeleteSlice
                    ),
                    null
                );
            }


            _delSliceSite.Target(_delSliceSite, target, start, end);
        }

        internal void SetIndex(object a, object b, object c) {
            if (_setIndexSite == null) {
                Interlocked.CompareExchange(
                    ref _setIndexSite,
                    CallSite<Func<CallSite, object, object, object, object>>.Create(
                        SetIndex(1)
                    ),
                    null
                );
            }

            _setIndexSite.Target(_setIndexSite, a, b, c);
        }

        internal void SetSlice(object a, object start, object end, object value) {
            if (_setSliceSite == null) {
                Interlocked.CompareExchange(
                    ref _setSliceSite,
                    CallSite<Func<CallSite, object, object, object, object, object>>.Create(
                        SetSliceBinder
                    ),
                    null
                );
            }

            _setSliceSite.Target(_setSliceSite, a, start, end, value);
        }
                
        internal CallSite<Func<CallSite, object, object, object>> EqualSite {
            get {
                if (_equalSite == null) {
                    Interlocked.CompareExchange(
                        ref _equalSite,
                        CallSite<Func<CallSite, object, object, object>>.Create(
                            BinaryOperation(
                                ExpressionType.Equal
                            )
                        ),
                        null
                    );
                }

                return _equalSite;
            }
        }
        
        internal CallSite<Func<CallSite, CodeContext, object, object>> FinalizerSite {
            get {
                if (_finalizerSite == null) {
                    Interlocked.CompareExchange(
                        ref _finalizerSite,
                        CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                            InvokeNone
                        ),
                        null
                    );
                }

                return _finalizerSite;
            }
        }

        internal CallSite<Func<CallSite, CodeContext, PythonFunction, object>> FunctionCallSite {
            get {
                if (_functionCallSite == null) {
                    Interlocked.CompareExchange(
                        ref _functionCallSite,
                        CallSite<Func<CallSite, CodeContext, PythonFunction, object>>.Create(
                            InvokeNone
                        ),
                        null
                    );
                }

                return _functionCallSite;
            }
        }

        class AttrKey : IEquatable<AttrKey> {
            private Type _type;
            private string _name;

            public AttrKey(Type type, string name) {
                _type = type;
                _name = name;
            }

#region IEquatable<AttrKey> Members

            public bool Equals(AttrKey other) {
                if (other == null) return false;

                return _type == other._type && _name == other._name;
            }

#endregion

            public override bool Equals(object obj) {
                return Equals(obj as AttrKey);
            }

            public override int GetHashCode() {
                return _type.GetHashCode() ^ _name.GetHashCode();
            }
        }

        public override string GetDocumentation(object obj) {
            if (_docSite == null) {
                _docSite = CallSite<Func<CallSite, object, string>>.Create(
                    Operation(
                        PythonOperationKind.Documentation
                    )
                );
            }
            return _docSite.Target(_docSite, obj);
        }

        internal PythonSiteCache GetSiteCacheForSystemType(Type type) {
            if (_systemSiteCache == null) {
                Interlocked.CompareExchange(ref _systemSiteCache, new Dictionary<Type, PythonSiteCache>(), null);
            }
            lock (_systemSiteCache) {
                PythonSiteCache result;
                if (!_systemSiteCache.TryGetValue(type, out result)) {
                    _systemSiteCache[type] = result = new PythonSiteCache();
                }
                return result;
            }
        }

#endregion

#region Conversions

        internal Int32 ConvertToInt32(object value) {
            if (_intSite == null) {
                Interlocked.CompareExchange(ref _intSite, MakeExplicitConvertSite<int>(), null);
            }

            return _intSite.Target.Invoke(_intSite, value);
        }

        internal bool TryConvertToString(object str, out string res) {
            if (_tryStringSite == null) {
                Interlocked.CompareExchange(ref _tryStringSite, MakeExplicitTrySite<string>(), null);
            }

            res = _tryStringSite.Target(_tryStringSite, str);
            return res != null;
        }

        internal bool TryConvertToInt32(object val, out int res) {
            if (_tryIntSite == null) {
                Interlocked.CompareExchange(ref _tryIntSite, MakeExplicitStructTrySite<int>(), null);
            }

            object objRes = _tryIntSite.Target(_tryIntSite, val);
            if (objRes != null) {
                res = (int)objRes;
                return true;
            }
            res = 0;
            return false;
        }

        internal bool TryConvertToIEnumerable(object enumerable, out IEnumerable res) {
            if (_tryIEnumerableSite == null) {
                Interlocked.CompareExchange(ref _tryIEnumerableSite, MakeExplicitTrySite<IEnumerable>(), null);
            }

            res = _tryIEnumerableSite.Target(_tryIEnumerableSite, enumerable);
            return res != null;
        }

        private CallSite<Func<CallSite, object, T>> MakeExplicitTrySite<T>() where T : class {
            return MakeTrySite<T, T>(ConversionResultKind.ExplicitTry);
        }

        private CallSite<Func<CallSite, object, object>> MakeExplicitStructTrySite<T>() where T : struct {
            return MakeTrySite<T, object>(ConversionResultKind.ExplicitTry);
        }

        private CallSite<Func<CallSite, object, TRet>> MakeTrySite<T, TRet>(ConversionResultKind kind) {
            return CallSite<Func<CallSite, object, TRet>>.Create(
                Convert(
                    typeof(T),
                    kind
                )
            );
        }

        internal object ImplicitConvertTo<T>(object value) {
            if (_implicitConvertSites == null) {
                Interlocked.CompareExchange(ref _implicitConvertSites, new Dictionary<Type, CallSite<Func<CallSite, object, object>>>(), null);
            }

            CallSite<Func<CallSite, object, object>> site;
            lock (_implicitConvertSites) {
                if (!_implicitConvertSites.TryGetValue(typeof(T), out site)) {
                    _implicitConvertSites[typeof(T)] = site = MakeImplicitConvertSite<T>();
                }
            }

            return site.Target(site, value);
        }


        /*
                public static String ConvertToString(object value) { return _stringSite.Invoke(DefaultContext.Default, value); }
                public static BigInteger ConvertToBigInteger(object value) { return _bigIntSite.Invoke(DefaultContext.Default, value); }
                public static Double ConvertToDouble(object value) { return _doubleSite.Invoke(DefaultContext.Default, value); }
                public static Complex64 ConvertToComplex(object value) { return _complexSite.Invoke(DefaultContext.Default, value); }
                public static Boolean ConvertToBoolean(object value) { return _boolSite.Invoke(DefaultContext.Default, value); }
                public static Int64 ConvertToInt64(object value) { return _int64Site.Invoke(DefaultContext.Default, value); }
                */
        private CallSite<Func<CallSite, object, T>> MakeExplicitConvertSite<T>() {
            return MakeConvertSite<T>(ConversionResultKind.ExplicitCast);
        }

        private CallSite<Func<CallSite, object, object>> MakeImplicitConvertSite<T>() {
            return CallSite<Func<CallSite, object, object>>.Create(
                ConvertRetObject(
                    typeof(T),
                    ConversionResultKind.ImplicitCast
                )
            );
        }

        private CallSite<Func<CallSite, object, T>> MakeConvertSite<T>(ConversionResultKind kind) {
            return CallSite<Func<CallSite, object, T>>.Create(
                Convert(
                    typeof(T),
                    kind
                )
            );
        }

        /// <summary>
        /// Invokes the specified operation on the provided arguments and returns the new resulting value.
        /// 
        /// operation is usually a value from StandardOperators (standard CLR/DLR operator) or 
        /// OperatorStrings (a Python specific operator)
        /// </summary>
        internal object Operation(PythonOperationKind operation, object self, object other) {
            if (_binarySites == null) {
                Interlocked.CompareExchange(
                    ref _binarySites,
                    new Dictionary<PythonOperationKind, CallSite<Func<CallSite, object, object, object>>>(),
                    null
                );
            }

            CallSite<Func<CallSite, object, object, object>> site;
            lock (_binarySites) {
                if (!_binarySites.TryGetValue(operation, out site)) {
                    _binarySites[operation] = site = CallSite<Func<CallSite, object, object, object>>.Create(
                        Binders.BinaryOperationBinder(this, operation)
                    );
                }
            }

            return site.Target(site, self, other);
        }

        internal bool GreaterThan(object self, object other) {
            return Comparison(self, other, ExpressionType.GreaterThan, ref _greaterThanSite);
        }

        internal bool LessThan(object self, object other) {
            return Comparison(self, other, ExpressionType.LessThan, ref _lessThanSite);
        }

        internal bool GreaterThanOrEqual(object self, object other) {
            return Comparison(self, other, ExpressionType.GreaterThanOrEqual, ref _greaterThanEqualSite);
        }

        internal bool LessThanOrEqual(object self, object other) {
            return Comparison(self, other, ExpressionType.LessThanOrEqual, ref _lessThanEqualSite);
        }

        internal bool Contains(object self, object other) {
            return Comparison(self, other, PythonOperationKind.Contains, ref _containsSite);
        }

        internal static bool Equal(object self, object other) {
            return DynamicHelpers.GetPythonType(self).EqualRetBool(self, other);
        }

        internal static bool NotEqual(object self, object other) {
            return !Equal(self, other);
        }

        private bool Comparison(object self, object other, ExpressionType operation, ref CallSite<Func<CallSite, object, object, bool>> comparisonSite) {
            if (comparisonSite == null) {
                Interlocked.CompareExchange(
                    ref comparisonSite,
                    CreateComparisonSite(operation),
                    null
                );
            }

            return comparisonSite.Target(comparisonSite, self, other);
        }

        internal CallSite<Func<CallSite, object, object, bool>> CreateComparisonSite(ExpressionType op) {
            return CallSite<Func<CallSite, object, object, bool>>.Create(
                BinaryOperationRetType(
                    BinaryOperation(op),
                    Convert(typeof(bool), ConversionResultKind.ExplicitCast)
                )
            );
        }

        private bool Comparison(object self, object other, PythonOperationKind operation, ref CallSite<Func<CallSite, object, object, bool>> comparisonSite) {
            if (comparisonSite == null) {
                Interlocked.CompareExchange(
                    ref comparisonSite,
                    CreateComparisonSite(operation),
                    null
                );
            }

            return comparisonSite.Target(comparisonSite, self, other);
        }

        internal CallSite<Func<CallSite, object, object, bool>> CreateComparisonSite(PythonOperationKind op) {
            return CallSite<Func<CallSite, object, object, bool>>.Create(
                OperationRetType(
                    Operation(op),
                    Convert(typeof(bool), ConversionResultKind.ExplicitCast)
                )
            );
        }

        internal object CallSplat(object func, params object[] args) {
            EnsureCallSplatSite();

            return _callSplatSite.Target(_callSplatSite, SharedContext, func, args);
        }

        internal object CallSplat(CodeContext/*!*/ context, object func, params object[] args) {
            EnsureCallSplatSite();

            return _callSplatSite.Target(_callSplatSite, context, func, args);
        }

        internal object CallWithContext(CodeContext/*!*/ context, object func, params object[] args) {
            EnsureCallSplatSite();

            return _callSplatSite.Target(_callSplatSite, context, func, args);
        }

        internal object Call(CodeContext/*!*/ context, object func) {
            EnsureCall0Site();

            return _callSite0.Target(_callSite0, context, func);
        }

        private void EnsureCall0SiteLightEh() {
            if (_callSite0LightEh == null) {
                Interlocked.CompareExchange(
                    ref _callSite0LightEh,
                    CallSite<Func<CallSite, CodeContext, object, object>>.Create(Invoke(new CallSignature(0)).GetLightExceptionBinder()),
                    null
                );
            }
        }

        internal object CallLightEh(CodeContext/*!*/ context, object func) {
            EnsureCall0SiteLightEh();

            return _callSite0LightEh.Target(_callSite0LightEh, context, func);
        }

        internal object Call(CodeContext/*!*/ context, object func, object arg0) {
            EnsureCall1Site();

            return _callSite1.Target(_callSite1, context, func, arg0);
        }

        internal object Call(CodeContext/*!*/ context, object func, object arg0, object arg1) {
            EnsureCall2Site();

            return _callSite2.Target(_callSite2, context, func, arg0, arg1);
        }

        private void EnsureCallSplatSite() {
            if (_callSplatSite == null) {
                Interlocked.CompareExchange(
                    ref _callSplatSite,
                    MakeSplatSite(),
                    null
                );
            }
        }

        internal CallSite<Func<CallSite, CodeContext, object, object[], object>> MakeSplatSite() {
            return CallSite<Func<CallSite, CodeContext, object, object[], object>>.Create(Binders.InvokeSplat(this));
        }
        
        internal object CallWithKeywords(object func, object[] args, IDictionary<object, object> dict) {
            return CallWithKeywordsAndContext(SharedContext, func, args, dict);
        }

        internal object CallWithKeywordsAndContext(CodeContext context, object func, object[] args, IDictionary<object, object> dict) {
            if (_callDictSite == null) {
                Interlocked.CompareExchange(
                    ref _callDictSite,
                    MakeKeywordSplatSite(),
                    null
                );
            }

            return _callDictSite.Target(_callDictSite, context, func, args, dict);
        }

        internal CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>> MakeKeywordSplatSite() {
            return CallSite<Func<CallSite, CodeContext, object, object[], IDictionary<object, object>, object>>.Create(Binders.InvokeKeywords(this));
        }


        internal object CallWithKeywords(object func, object args, object dict) {
            if (_callDictSiteLooselyTyped == null) {
                Interlocked.CompareExchange(
                    ref _callDictSiteLooselyTyped,
                    MakeKeywordSplatSiteLooselyTyped(),
                    null
                );
            }

            return _callDictSiteLooselyTyped.Target(_callDictSiteLooselyTyped, SharedContext, func, args, dict);
        }

        internal CallSite<Func<CallSite, CodeContext, object, object, object, object>> MakeKeywordSplatSiteLooselyTyped() {
            return CallSite<Func<CallSite, CodeContext, object, object, object, object>>.Create(Binders.InvokeKeywords(this));
        }


        internal CallSite<Func<CallSite, CodeContext, object, string, PythonDictionary, PythonDictionary, PythonTuple, int, object>> ImportSite {
            get {
                if (_importSite == null) {
                    Interlocked.CompareExchange(
                        ref _importSite,
                        CallSite<Func<CallSite, CodeContext, object, string, PythonDictionary, PythonDictionary, PythonTuple, int, object>>.Create(
                            Invoke(
                                new CallSignature(5)
                            ).GetLightExceptionBinder()
                        ),
                        null
                    );
                }

                return _importSite;
            }
        }

        internal CallSite<Func<CallSite, CodeContext, object, string, PythonDictionary, PythonDictionary, PythonTuple, object>> OldImportSite {
            get {
                if (_oldImportSite == null) {
                    Interlocked.CompareExchange(
                        ref _oldImportSite,
                        CallSite<Func<CallSite, CodeContext, object, string, PythonDictionary, PythonDictionary, PythonTuple, object>>.Create(
                            Invoke(
                                new CallSignature(4)
                            ).GetLightExceptionBinder()
                        ),
                        null
                    );
                }

                return _oldImportSite;
            }
        }

        public override bool IsCallable(object obj) {
            if (_isCallableSite == null) {
                Interlocked.CompareExchange(
                    ref _isCallableSite,
                    CallSite<Func<CallSite, object, bool>>.Create(
                        Operation(
                            PythonOperationKind.IsCallable
                        )
                    ),
                    null
                );
            }

            return _isCallableSite.Target(_isCallableSite, obj);
        }

        internal static int Hash(object o) {
            if (o != null) {
                switch (o.GetType().GetTypeCode()) {
                    case TypeCode.Int32: return Int32Ops.__hash__((int)o);
                    case TypeCode.String: return ((string)o).GetHashCode();
                    case TypeCode.Double: return DoubleOps.__hash__((double)o);
                    case TypeCode.Int16: return Int16Ops.__hash__((short)o);
                    case TypeCode.Int64: return Int64Ops.__hash__((long)o);
                    case TypeCode.SByte: return SByteOps.__hash__((sbyte)o);
                    case TypeCode.Single: return SingleOps.__hash__((float)o);
                    case TypeCode.UInt16: return UInt16Ops.__hash__((ushort)o);
                    case TypeCode.UInt32: return UInt32Ops.__hash__((uint)o);
                    case TypeCode.UInt64: return UInt64Ops.__hash__((ulong)o);
                    case TypeCode.Decimal: return DecimalOps.__hash__((decimal)o);
                    case TypeCode.DateTime: return ((DateTime)o).GetHashCode();
                    case TypeCode.Boolean: return ((bool)o).GetHashCode();
                    case TypeCode.Byte: return ByteOps.__hash__((byte)o);
                }
            }

            return DynamicHelpers.GetPythonType(o).Hash(o);
        }

        internal static bool IsHashable(object o) {
            if (o == null) {
                return true;
            }
            switch (o.GetType().GetTypeCode()) {
                case TypeCode.Int32:
                case TypeCode.String:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                case TypeCode.Boolean:
                case TypeCode.Byte:
                    return true;
            }
            object hashFunction;
            if (PythonOps.TryGetBoundAttr(o, "__hash__", out hashFunction) && hashFunction != null) {
                 return true;
            }
            if (o is PythonType) {
                return true;
            }
            return false;
        }

        internal object Add(object x, object y) {
            var addSite = EnsureAddSite();

            return addSite.Target(addSite, x, y);
        }

        internal CallSite<Func<CallSite, object, object, object>> EnsureAddSite() {
            if (_addSite == null) {
                Interlocked.CompareExchange(
                    ref _addSite,
                    CallSite<Func<CallSite, object, object, object>>.Create(
                        BinaryOperation(ExpressionType.Add)
                    ),
                    null
                );
            }

            return _addSite;
        }

        internal object DivMod(object x, object y) {
            if (_divModSite == null) {
                Interlocked.CompareExchange(
                    ref _divModSite,
                    CallSite<Func<CallSite, object, object, object>>.Create(
                        Operation(PythonOperationKind.DivMod)
                    ),
                    null
                );
            }

            object ret = _divModSite.Target(_divModSite, x, y);
            if (ret != NotImplementedType.Value) {
                return ret;
            }

            if (_rdivModSite == null) {
                Interlocked.CompareExchange(
                    ref _rdivModSite,
                    CallSite<Func<CallSite, object, object, object>>.Create(
                        Operation(PythonOperationKind.ReverseDivMod)
                    ),
                    null
                );
            }

            ret = _rdivModSite.Target(_rdivModSite, x, y);
            if (ret != NotImplementedType.Value) {
                return ret;
            }

            throw PythonOps.TypeErrorForBinaryOp("divmod", x, y);

        }

#endregion

#region Compiled Code Support

        internal CompiledLoader GetCompiledLoader() {
            if (_compiledLoader == null) {
                if (Interlocked.CompareExchange(ref _compiledLoader, new CompiledLoader(), null) == null) {
                    object path;
                    List lstPath;

                    if (!SystemState.__dict__.TryGetValue("meta_path", out path) || ((lstPath = path as List) == null)) {
                        SystemState.__dict__["meta_path"] = lstPath = new List();
                    }

                    lstPath.append(_compiledLoader);
                }
            }

            return _compiledLoader;
        }

#endregion

        /// <summary>
        /// Returns a shared code context for the current PythonContext.  This shared
        /// context can be used for performing general operations which usually
        /// require a CodeContext.
        /// </summary>
        internal CodeContext SharedContext {
            get {
                return _defaultContext;
            }
        }

        /// <summary>
        /// Returns an overload resolver for the current PythonContext.  The overload
        /// resolver will flow the shared context through as it's CodeContext.
        /// </summary>
        internal PythonOverloadResolverFactory SharedOverloadResolverFactory {
            get {
                return _sharedOverloadResolverFactory;
            }
        }

        /// <summary>
        /// Returns a shared code context for the current PythonContext.  This shared
        /// context can be used for doing lookups which need to occur as if they
        /// happened in a module which has done "import clr".
        /// </summary>
        internal CodeContext SharedClsContext {
            get {
                return _defaultClsContext;
            }
        }

        internal ClrModule.ReferencesList ReferencedAssemblies {
            get {
                if (_referencesList == null) {
                    Interlocked.CompareExchange(ref _referencesList, new ClrModule.ReferencesList(), null);
                }

                return _referencesList;
            }
        }

        internal static CultureInfo CCulture {
            get {
                if (_CCulture == null) {
                    Interlocked.CompareExchange(ref _CCulture, MakeCCulture(), null);
                }

                return _CCulture;
            }
        }

        private static CultureInfo MakeCCulture() {
            CultureInfo res = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            res.NumberFormat.NumberGroupSizes = new int[] { 0 };
            res.NumberFormat.CurrencyGroupSizes = new int[] { 0 };
            return res;
        }

        internal CultureInfo CollateCulture {
            get {
                if (_collateCulture == null) {
                    _collateCulture = CCulture;
                }
                return _collateCulture; 
            }
            set { _collateCulture = value; }
        }

        internal CultureInfo CTypeCulture {
            get {
                if (_ctypeCulture == null) {
                    _ctypeCulture = CCulture;
                }
                return _ctypeCulture; 
            }
            set { _ctypeCulture = value; }
        }

        internal CultureInfo TimeCulture {
            get {
                if (_timeCulture == null) {
                    _timeCulture = CCulture;
                }
                return _timeCulture; 
            }
            set { _timeCulture = value; }
        }

        internal CultureInfo MonetaryCulture {
            get {
                if (_monetaryCulture == null) {
                    _monetaryCulture = CCulture;
                }
                return _monetaryCulture; 
            }
            set { _monetaryCulture = value; }
        }

        internal CultureInfo NumericCulture {
            get {
                if (_numericCulture == null) {
                    _numericCulture = CCulture;
                }
                return _numericCulture; 
            }
            set { _numericCulture = value; }
        }

#region Command Dispatching

        /// <summary>
        /// Sets the current command dispatcher for the Python command line.  The previous dispatcher
        /// is returned.  Null can be passed to remove the current command dispatcher.
        /// 
        /// The command dispatcher will be called with a delegate to be executed.  The command dispatcher
        /// should invoke the target delegate in the desired context.
        /// 
        /// A common use for this is to enable running all REPL commands on the UI thread while the REPL
        /// continues to run on a non-UI thread.
        /// 
        /// The ipy.exe REPL will call into PythonContext.DispatchCommand to dispatch each execution to
        /// the correct thread.  Other REPLs can do the same to support this functionality as well.
        /// </summary>
        public Action<Action> GetSetCommandDispatcher(Action<Action> newDispatcher) {
            return Interlocked.Exchange(ref _commandDispatcher, newDispatcher);
        }

        public Action<Action> GetCommandDispatcher() {
            return _commandDispatcher;
        }

        /// <summary>
        /// Dispatches the command to the current command dispatcher.  If there is no current command
        /// dispatcher the command is executed immediately on the current thread.
        /// </summary>
        public void DispatchCommand(Action command) {
            Action<Action> dispatcher = _commandDispatcher;
            if (dispatcher != null) {
                dispatcher(command);
            } else if (command != null) {
                command();
            }
        }

#endregion

        internal CallSite<Func<CallSite, CodeContext, object, object, object>> PropertyGetSite {
            get {
                if (_propGetSite == null) {
                    Interlocked.CompareExchange(ref _propGetSite,
                        CallSite<Func<CallSite, CodeContext, object, object, object>>.Create(
                            InvokeOne
                        ),
                        null
                    );
                }

                return _propGetSite;
            }
        }

        internal CallSite<Func<CallSite, CodeContext, object, object, object>> PropertyDeleteSite {
            get {
                if (_propDelSite == null) {
                    Interlocked.CompareExchange(ref _propDelSite,
                        CallSite<Func<CallSite, CodeContext, object, object, object>>.Create(
                            InvokeOne
                        ),
                        null
                    );
                }

                return _propDelSite;
            }
        }

        internal CallSite<Func<CallSite, CodeContext, object, object, object, object>> PropertySetSite {
            get {
                if (_propSetSite == null) {
                    Interlocked.CompareExchange(ref _propSetSite,
                        CallSite<Func<CallSite, CodeContext, object, object, object, object>>.Create(
                            Invoke(
                                new CallSignature(2)
                            )
                        ),
                        null
                    );
                }

                return _propSetSite;
            }
        }

        internal PythonBinder Binder {
            get {
                return _binder;
            }
        }

        private class DefaultPythonComparer : IComparer {
            private CallSite<Func<CallSite, object, object, int>> _site;
            public DefaultPythonComparer(PythonContext context) {
                _site = CallSite<Func<CallSite, object, object, int>>.Create(
                    context.Operation(PythonOperationKind.Compare)
                );
            }

            public int Compare(object x, object y) {
                return _site.Target(_site, x, y);
            }
        }

        private class FunctionComparer<T> : IComparer {
            private T _cmpfunc;
            private CallSite<Func<CallSite, CodeContext, T, object, object, int>> _funcSite;
            private CodeContext/*!*/ _context;

            public FunctionComparer(PythonContext/*!*/ context, T cmpfunc)
                : this(context, cmpfunc, MakeCompareSite<T>(context)) {
            }

            public FunctionComparer(PythonContext/*!*/ context, T cmpfunc, CallSite<Func<CallSite, CodeContext, T, object, object, int>> site) {
                _cmpfunc = cmpfunc;
                _context = context.SharedContext;
                _funcSite = site;
            }

            public int Compare(object o1, object o2) {
                return _funcSite.Target(_funcSite, _context, _cmpfunc, o1, o2);
            }
        }

        private static CallSite<Func<CallSite, CodeContext, T, object, object, int>> MakeCompareSite<T>(PythonContext context) {
            return CallSite<Func<CallSite, CodeContext, T, object, object, int>>.Create(
                context.InvokeTwoConvertToInt
            );
        }

        /// <summary>
        /// Gets a function which can be used for comparing two values.  If cmp is not null
        /// then the comparison will use the provided comparison function.  Otherwise
        /// it will use the normal Python semantics.
        /// 
        /// If type is null then a generic comparison function is returned.  If type is 
        /// not null a comparison function is returned that's used for just that type.
        /// </summary>
        internal IComparer GetComparer(object cmp, Type type) {
            if (type == null) {
                // no type information, return the generic version...                
                if (cmp == null) {
                    return new DefaultPythonComparer(this);
                } else if (cmp is PythonFunction) {
                    return new FunctionComparer<PythonFunction>(this, (PythonFunction)cmp);
                } else if (cmp is BuiltinFunction) {
                    return new FunctionComparer<BuiltinFunction>(this, (BuiltinFunction)cmp);
                }

                return new FunctionComparer<object>(this, cmp);
            }

            if (cmp == null) {
                if (_defaultComparer == null) {
                    Interlocked.CompareExchange(
                        ref _defaultComparer,
                        new Dictionary<Type, DefaultPythonComparer>(),
                        null
                    );
                }

                lock (_defaultComparer) {
                    DefaultPythonComparer comparer;
                    if (!_defaultComparer.TryGetValue(type, out comparer)) {
                        _defaultComparer[type] = comparer = new DefaultPythonComparer(this);
                    }
                    return comparer;
                }
            } else if (cmp is PythonFunction) {
                if (_sharedPythonFunctionCompareSite == null) {
                    _sharedPythonFunctionCompareSite = MakeCompareSite<PythonFunction>(this);
                }

                return new FunctionComparer<PythonFunction>(this, (PythonFunction)cmp, _sharedPythonFunctionCompareSite);
            } else if (cmp is BuiltinFunction) {
                if (_sharedBuiltinFunctionCompareSite == null) {
                    _sharedBuiltinFunctionCompareSite = MakeCompareSite<BuiltinFunction>(this);
                }

                return new FunctionComparer<BuiltinFunction>(this, (BuiltinFunction)cmp, _sharedBuiltinFunctionCompareSite);
            }

            if (_sharedFunctionCompareSite == null) {
                _sharedFunctionCompareSite = MakeCompareSite<object>(this);
            }

            return new FunctionComparer<object>(this, cmp, _sharedFunctionCompareSite);

        }

        internal CallSite<Func<CallSite, CodeContext, object, int, object>> GetItemCallSite {
            get {
                if (_getItemCallSite == null) {
                    Interlocked.CompareExchange(
                        ref _getItemCallSite,
                        CallSite<Func<CallSite, CodeContext, object, int, object>>.Create(
                            new PythonInvokeBinder(
                                this,
                                new CallSignature(1)
                            )
                        ),
                        null
                    );
                }

                return _getItemCallSite;
            }
        }

        internal CallSite<Func<CallSite, object, object, bool>> GetEqualSite(Type/*!*/ type) {
            if (_equalSites == null) {
                Interlocked.CompareExchange(ref _equalSites, new Dictionary<Type, CallSite<Func<CallSite, object, object, bool>>>(), null);
            }

            CallSite<Func<CallSite, object, object, bool>> res;
            lock (_equalSites) { 
                if (!_equalSites.TryGetValue(type, out res)) {
                    _equalSites[type] = res = MakeEqualSite();
                }
            }

            return res;
        }

        internal CallSite<Func<CallSite, object, object, bool>> MakeEqualSite() {
            return CreateComparisonSite(ExpressionType.Equal);
        }

        internal static CallSite<Func<CallSite, object, int>> GetHashSite(PythonType/*!*/ type) {
            return type.HashSite;
        }

        internal CallSite<Func<CallSite, object, int>> MakeHashSite() {
            return CallSite<Func<CallSite, object, int>>.Create(
                Operation(
                    PythonOperationKind.Hash
                )
            );
        }

        public override IList<string> GetCallSignatures(object obj) {
            if (_getSignaturesSite == null) {
                Interlocked.CompareExchange(
                    ref _getSignaturesSite,
                    CallSite<Func<CallSite, object, IList<string>>>.Create(
                        Operation(PythonOperationKind.CallSignatures)
                    ),
                    null
                );
            }
            return _getSignaturesSite.Target(_getSignaturesSite, obj);
        }

        /// <summary>
        /// Performs a GC collection including the possibility of freeing weak data structures held onto by the Python runtime.
        /// </summary>
        /// <param name="generation"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.GC.Collect")]
        internal int Collect(int generation) {
            if (generation > GC.MaxGeneration || generation < 0) {
                throw PythonOps.ValueError("invalid generation {0}", generation);
            }

            // now let the CLR do it's normal collection
            long start = GC.GetTotalMemory(false);
            
            for (int i = 0; i < 2; i++) {
#if !SILVERLIGHT // GC.Collect
                GC.Collect(generation);
#else
                GC.Collect();
#endif

                GC.WaitForPendingFinalizers();

                if (generation == GC.MaxGeneration) {
                    // cleanup any weak data structures which we maintain when
                    // we force a collection
                    FunctionCode.CleanFunctionCodes(this, true);
                }
            }

            return (int)Math.Max(start - GC.GetTotalMemory(false), 0);
        }

        public DynamicDelegateCreator DelegateCreator {
            get {
                if (_delegateCreator == null) {
                    Interlocked.CompareExchange(ref _delegateCreator, new DynamicDelegateCreator(this), null);
                }

                return _delegateCreator;
            }
        }

#region Binder Factories

        internal CompatibilityInvokeBinder/*!*/ CompatInvoke(CallInfo /*!*/ callInfo) {
            if (_compatInvokeBinders == null) {
                Interlocked.CompareExchange(
                    ref _compatInvokeBinders,
                    new Dictionary<CallSignature, CompatibilityInvokeBinder>(),
                    null
                );
            }

            lock (_compatInvokeBinders) {
                CallSignature sig = BindingHelpers.CallInfoToSignature(callInfo);
                CompatibilityInvokeBinder res;
                if (!_compatInvokeBinders.TryGetValue(sig, out res)) {
                    _compatInvokeBinders[sig] = res = new CompatibilityInvokeBinder(this, callInfo);
                }

                return res;
            }
        }


        internal PythonConversionBinder/*!*/ Convert(Type/*!*/ type, ConversionResultKind resultKind) {
            if (_conversionBinders == null) {
                Interlocked.CompareExchange(
                    ref _conversionBinders,
                    new Dictionary<Type, PythonConversionBinder>[(int)ConversionResultKind.ExplicitTry + 1], // max conversion result kind
                    null
                );
            }

            if (_conversionBinders[(int)resultKind] == null) {
                Interlocked.CompareExchange(
                    ref _conversionBinders[(int)resultKind],
                    new Dictionary<Type, PythonConversionBinder>(),
                    null
                );
            }

            Dictionary<Type, PythonConversionBinder> dict = _conversionBinders[(int)resultKind];
            lock (dict) {
                PythonConversionBinder res;
                if (!dict.TryGetValue(type, out res)) {
                    dict[type] = res = new PythonConversionBinder(this, type, resultKind);
                }

                return res;
            }
        }

        internal DynamicMetaObjectBinder/*!*/ ConvertRetObject(Type/*!*/ type, ConversionResultKind resultKind) {
            if (_convertRetObjectBinders == null) {
                Interlocked.CompareExchange(
                    ref _convertRetObjectBinders,
                    new Dictionary<Type, DynamicMetaObjectBinder>[(int)ConversionResultKind.ExplicitTry + 1], // max conversion result kind
                    null
                );
            }

            if (_convertRetObjectBinders[(int)resultKind] == null) {
                Interlocked.CompareExchange(
                    ref _convertRetObjectBinders[(int)resultKind],
                    new Dictionary<Type, DynamicMetaObjectBinder>(),
                    null
                );
            }

            Dictionary<Type, DynamicMetaObjectBinder> dict = _convertRetObjectBinders[(int)resultKind];
            lock (dict) {
                DynamicMetaObjectBinder res;
                if (!dict.TryGetValue(type, out res)) {
                    dict[type] = res = new PythonConversionBinder(this, type, resultKind, true);
                }

                return res;
            }
        }

        internal CreateFallback/*!*/ Create(CompatibilityInvokeBinder/*!*/ realFallback, CallInfo /*!*/ callInfo) {
            if (_createBinders == null) {
                Interlocked.CompareExchange(
                    ref _createBinders,
                    new Dictionary<CallSignature, CreateFallback>(),
                    null
                );
            }

            lock (_createBinders) {
                CallSignature sig = BindingHelpers.CallInfoToSignature(callInfo);
                CreateFallback res;
                if (!_createBinders.TryGetValue(sig, out res)) {
                    _createBinders[sig] = res = new CreateFallback(realFallback, callInfo);
                }

                return res;
            }
        }

        internal PythonGetMemberBinder/*!*/ GetMember(string/*!*/ name) {
            return GetMember(name, false);
        }

        internal PythonGetMemberBinder/*!*/ GetMember(string/*!*/ name, bool isNoThrow) {
            Dictionary<string, PythonGetMemberBinder> dict;
            if (isNoThrow) {
                if (_tryGetMemberBinders == null) {
                    Interlocked.CompareExchange(
                        ref _tryGetMemberBinders,
                        new Dictionary<string, PythonGetMemberBinder>(),
                        null
                    );
                }

                dict = _tryGetMemberBinders;
            } else {
                if (_getMemberBinders == null) {
                    Interlocked.CompareExchange(
                        ref _getMemberBinders,
                        new Dictionary<string, PythonGetMemberBinder>(),
                        null
                    );
                }

                dict = _getMemberBinders;
            }

            lock (dict) {
                PythonGetMemberBinder res;
                if (!dict.TryGetValue(name, out res)) {
                    dict[name] = res = new PythonGetMemberBinder(this, name, isNoThrow);
                }

                return res;
            }
        }

        internal CompatibilityGetMember/*!*/ CompatGetMember(string/*!*/ name, bool isNoThrow) {
            Dictionary<string, CompatibilityGetMember> dict;
            if (isNoThrow) {
                if (_compatGetMemberNoThrow == null) {
                    Interlocked.CompareExchange(
                        ref _compatGetMemberNoThrow,
                        new Dictionary<string, CompatibilityGetMember>(),
                        null
                    );
                }
                dict = _compatGetMemberNoThrow;
            } else {
                if (_compatGetMember == null) {
                    Interlocked.CompareExchange(
                        ref _compatGetMember,
                        new Dictionary<string, CompatibilityGetMember>(),
                        null
                    );
                }
                dict = _compatGetMember;
            }

            lock (dict) {
                CompatibilityGetMember res;
                if (!dict.TryGetValue(name, out res)) {
                    dict[name] = res = new CompatibilityGetMember(this, name, isNoThrow);
                }

                return res;
            }
        }

        internal PythonSetMemberBinder/*!*/ SetMember(string/*!*/ name) {
            if (_setMemberBinders == null) {
                Interlocked.CompareExchange(
                    ref _setMemberBinders,
                    new Dictionary<string, PythonSetMemberBinder>(),
                    null
                );
            }

            lock (_setMemberBinders) {
                PythonSetMemberBinder res;
                if (!_setMemberBinders.TryGetValue(name, out res)) {
                    _setMemberBinders[name] = res = new PythonSetMemberBinder(this, name);
                }

                return res;
            }
        }

        internal PythonDeleteMemberBinder/*!*/ DeleteMember(string/*!*/ name) {
            if (_deleteMemberBinders == null) {
                Interlocked.CompareExchange(
                    ref _deleteMemberBinders,
                    new Dictionary<string, PythonDeleteMemberBinder>(),
                    null
                );
            }

            lock (_deleteMemberBinders) {
                PythonDeleteMemberBinder res;
                if (!_deleteMemberBinders.TryGetValue(name, out res)) {
                    _deleteMemberBinders[name] = res = new PythonDeleteMemberBinder(this, name);
                }

                return res;
            }
        }

        internal PythonInvokeBinder/*!*/ Invoke(CallSignature signature) {
            if (_invokeBinders == null) {
                Interlocked.CompareExchange(
                    ref _invokeBinders,
                    new Dictionary<CallSignature, PythonInvokeBinder>(),
                    null
                );
            }

            lock (_invokeBinders) {
                PythonInvokeBinder res;
                if (!_invokeBinders.TryGetValue(signature, out res)) {
                    _invokeBinders[signature] = res = new PythonInvokeBinder(this, signature);
                }

                return res;
            }
        }

        internal PythonInvokeBinder/*!*/ InvokeNone {
            get {
                if (_invokeNoArgs == null) {
                    _invokeNoArgs = Invoke(new CallSignature(0));
                }

                return _invokeNoArgs;
            }
        }

        internal PythonInvokeBinder/*!*/ InvokeOne {
            get {
                if (_invokeOneArg == null) {
                    _invokeOneArg = Invoke(new CallSignature(1));
                }

                return _invokeOneArg;
            }
        }

        internal DynamicMetaObjectBinder/*!*/ InvokeTwoConvertToInt {
            get {
                if (_invokeTwoConvertToInt == null) {
                    // +2 for the target object and CodeContext which InvokeBinder recevies
                    const int argCount = 2;
                    ParameterMappingInfo[] args = new ParameterMappingInfo[argCount + 2];
                    for (int i = 0; i < argCount + 2; i++) {
                        args[i] = ParameterMappingInfo.Parameter(i);
                    }

                    _invokeTwoConvertToInt = new ComboBinder(
                        new BinderMappingInfo(
                            Invoke(new CallSignature(2)),
                            args
                        ),
                        new BinderMappingInfo(
                            Convert(typeof(int), ConversionResultKind.ExplicitCast),
                            ParameterMappingInfo.Action(0)
                        )
                    );
                }

                return _invokeTwoConvertToInt;
            }
        }

        internal PythonOperationBinder/*!*/ Operation(PythonOperationKind operation) {
            if (_operationBinders == null) {
                Interlocked.CompareExchange(
                    ref _operationBinders,
                    new Dictionary<PythonOperationKind, PythonOperationBinder>(),
                    null
                );
            }

            lock (_operationBinders) {
                PythonOperationBinder res;
                if (!_operationBinders.TryGetValue(operation, out res)) {
                    _operationBinders[operation] = res = new PythonOperationBinder(this, operation);
                }

                return res;
            }
        }

        internal PythonUnaryOperationBinder/*!*/ UnaryOperation(ExpressionType operation) {
            if (_unaryBinders == null) {
                Interlocked.CompareExchange(
                    ref _unaryBinders,
                    new Dictionary<ExpressionType, PythonUnaryOperationBinder>(),
                    null
                );
            }

            lock (_unaryBinders) {
                PythonUnaryOperationBinder res;
                if (!_unaryBinders.TryGetValue(operation, out res)) {
                    _unaryBinders[operation] = res = new PythonUnaryOperationBinder(this, operation);
                }

                return res;
            }

        }

        internal PythonBinaryOperationBinder/*!*/ BinaryOperation(ExpressionType operation) {
            if (_binaryBinders == null) {
                Interlocked.CompareExchange(
                    ref _binaryBinders,
                    new PythonBinaryOperationBinder[(int)ExpressionType.IsFalse + 1],
                    null
                );
            }

            var ret = _binaryBinders[(int)operation];
            if (ret != null) {
                return ret;
            }
            var oldValue = Interlocked.CompareExchange(
                ref _binaryBinders[(int)operation],
                ret = new PythonBinaryOperationBinder(this, operation),
                null
            );
            return oldValue ?? ret;
        }

        internal BinaryRetTypeBinder/*!*/ BinaryOperationRetType(PythonBinaryOperationBinder opBinder, PythonConversionBinder convBinder) {
            if (_binaryRetTypeBinders == null) {
                Interlocked.CompareExchange(
                    ref _binaryRetTypeBinders,
                    new Dictionary<OperationRetTypeKey<ExpressionType>, BinaryRetTypeBinder>(),
                    null
                );
            }

            lock (_binaryRetTypeBinders) {
                BinaryRetTypeBinder res;
                OperationRetTypeKey<ExpressionType> key = new OperationRetTypeKey<ExpressionType>(convBinder.Type, opBinder.Operation);
                if (!_binaryRetTypeBinders.TryGetValue(key, out res)) {
                    _binaryRetTypeBinders[key] = res = new BinaryRetTypeBinder(opBinder, convBinder);
                }

                return res;
            }
        }

        internal BinaryRetTypeBinder/*!*/ OperationRetType(PythonOperationBinder opBinder, PythonConversionBinder convBinder) {
            if (_operationRetTypeBinders == null) {
                Interlocked.CompareExchange(
                    ref _operationRetTypeBinders,
                    new Dictionary<OperationRetTypeKey<PythonOperationKind>, BinaryRetTypeBinder>(),
                    null
                );
            }

            lock (_operationRetTypeBinders) {
                BinaryRetTypeBinder res;
                OperationRetTypeKey<PythonOperationKind> key = new OperationRetTypeKey<PythonOperationKind>(convBinder.Type, opBinder.Operation);
                if (!_operationRetTypeBinders.TryGetValue(key, out res)) {
                    _operationRetTypeBinders[key] = res = new BinaryRetTypeBinder(opBinder, convBinder);
                }

                return res;
            }
        }

        internal PythonGetIndexBinder/*!*/ GetIndex(int argCount) {
            if (_getIndexBinders == null) {
                Interlocked.CompareExchange(ref _getIndexBinders, new PythonGetIndexBinder[argCount + 1], null);
            }

            lock (this) {
                if (_getIndexBinders.Length <= argCount) {
                    Array.Resize(ref _getIndexBinders, argCount + 1);
                }

                if (_getIndexBinders[argCount] == null) {
                    _getIndexBinders[argCount] = new PythonGetIndexBinder(this, argCount);
                }

                return _getIndexBinders[argCount];
            }
        }

        internal PythonSetIndexBinder/*!*/ SetIndex(int argCount) {
            if (_setIndexBinders == null) {
                Interlocked.CompareExchange(ref _setIndexBinders, new PythonSetIndexBinder[argCount + 1], null);
            }

            lock (this) {
                if (_setIndexBinders.Length <= argCount) {
                    Array.Resize(ref _setIndexBinders, argCount + 1);
                }

                if (_setIndexBinders[argCount] == null) {
                    _setIndexBinders[argCount] = new PythonSetIndexBinder(this, argCount);
                }

                return _setIndexBinders[argCount];
            }
        }

        internal PythonDeleteIndexBinder/*!*/ DeleteIndex(int argCount) {
            if (_deleteIndexBinders == null) {
                Interlocked.CompareExchange(ref _deleteIndexBinders, new PythonDeleteIndexBinder[argCount + 1], null);
            }

            lock (this) {
                if (_deleteIndexBinders.Length <= argCount) {
                    Array.Resize(ref _deleteIndexBinders, argCount + 1);
                }

                if (_deleteIndexBinders[argCount] == null) {
                    _deleteIndexBinders[argCount] = new PythonDeleteIndexBinder(this, argCount);
                }

                return _deleteIndexBinders[argCount];
            }
        }

        internal PythonGetSliceBinder/*!*/ GetSlice {
            get {
                if (_getSlice == null) {
                    Interlocked.CompareExchange(ref _getSlice, new PythonGetSliceBinder(this), null);
                }

                return _getSlice;
            }
        }

        internal PythonSetSliceBinder/*!*/ SetSliceBinder {
            get {
                if (_setSlice == null) {
                    Interlocked.CompareExchange(ref _setSlice, new PythonSetSliceBinder(this), null);
                }

                return _setSlice;
            }
        }

        internal PythonDeleteSliceBinder/*!*/ DeleteSlice {
            get {
                if (_deleteSlice == null) {
                    Interlocked.CompareExchange(ref _deleteSlice, new PythonDeleteSliceBinder(this), null);
                }

                return _deleteSlice;
            }
        }

        class OperationRetTypeKey<T> : IEquatable<OperationRetTypeKey<T>> {
            public readonly Type ReturnType;
            public readonly T Operation;

            public OperationRetTypeKey(Type retType, T operation) {
                ReturnType = retType;
                Operation = operation;
            }

#region IEquatable<BinaryOperationRetTypeKey> Members

            public bool Equals(OperationRetTypeKey<T> other) {
                return other.ReturnType == ReturnType && other.Operation.Equals(Operation);
            }

#endregion

            public override int GetHashCode() {
                return ReturnType.GetHashCode() ^ Operation.GetHashCode();
            }

            public override bool Equals(object obj) {
                OperationRetTypeKey<T> other = obj as OperationRetTypeKey<T>;
                if (other != null) {
                    return Equals(other);
                }

                return false;
            }
        }

        /// <summary>
        /// Gets a PythonContext given a DynamicMetaObjectBinder.
        /// </summary>
        public static PythonContext/*!*/ GetPythonContext(DynamicMetaObjectBinder/*!*/ action) {
            IPythonSite pySite = action as IPythonSite;
            if (pySite != null) {
                return pySite.Context;
            }

            return DefaultContext.DefaultPythonContext;
        }

        public static Expression/*!*/ GetCodeContext(DynamicMetaObjectBinder/*!*/ action) {
            return Microsoft.Scripting.Ast.Utils.Constant(PythonContext.GetPythonContext(action)._defaultContext);
        }

        public static DynamicMetaObject/*!*/ GetCodeContextMO(DynamicMetaObjectBinder/*!*/ action) {
            return new DynamicMetaObject(
                Microsoft.Scripting.Ast.Utils.Constant(PythonContext.GetPythonContext(action)._defaultContext),
                BindingRestrictions.Empty,
                PythonContext.GetPythonContext(action)._defaultContext
            );
        }

        public static DynamicMetaObject/*!*/ GetCodeContextMOCls(DynamicMetaObjectBinder/*!*/ action) {
            return new DynamicMetaObject(
                Microsoft.Scripting.Ast.Utils.Constant(PythonContext.GetPythonContext(action).SharedClsContext),
                BindingRestrictions.Empty,
                PythonContext.GetPythonContext(action).SharedClsContext
            );
        }

#endregion

#region Scope Access

        public override T ScopeGetVariable<T>(Scope scope, string name) {
            var storage = scope.Storage as ScopeStorage;
            object res;
            if (storage != null && storage.TryGetValue(name, false, out res)) {
                return Operations.ConvertTo<T>(res);
            }

            StringDictionaryExpando dictStorage = scope.Storage as StringDictionaryExpando;
            if (dictStorage != null && dictStorage.Dictionary.TryGetValue(name, out res)) {
                return Operations.ConvertTo<T>(res);
            }

            return base.ScopeGetVariable<T>(scope, name);
        }

        public override dynamic ScopeGetVariable(Scope scope, string name) {
            var storage = scope.Storage as ScopeStorage;
            object res;
            if (storage != null && storage.TryGetValue(name, false, out res)) {
                return res;
            }

            StringDictionaryExpando dictStorage = scope.Storage as StringDictionaryExpando;
            if (dictStorage != null && dictStorage.Dictionary.TryGetValue(name, out res)) {
                return res;
            }

            return base.ScopeGetVariable(scope, name);
        }

        public override void ScopeSetVariable(Scope scope, string name, object value) {
            var storage = scope.Storage as ScopeStorage;
            if (storage != null) {
                storage.SetValue(name, false, value);
                return;
            }

            StringDictionaryExpando dictStorage = scope.Storage as StringDictionaryExpando;
            if (dictStorage != null) {
                dictStorage.Dictionary[name] = value;
                return;
            }

            base.ScopeSetVariable(scope, name, value);
        }

        public override bool ScopeTryGetVariable(Scope scope, string name, out dynamic value) {
            var storage = scope.Storage as ScopeStorage;
            if (storage != null && storage.TryGetValue(name, false, out value)) {
                return true;
            }

            StringDictionaryExpando dictStorage = scope.Storage as StringDictionaryExpando;
            if (dictStorage != null && dictStorage.Dictionary.TryGetValue(name, out value)) {
                return true;
            }

            return base.ScopeTryGetVariable(scope, name, out value);
        }

#endregion

#region Tracing

        internal DebugContext DebugContext {
            get {
                EnsureDebugContext();

                return _debugContext;
            }
        }

        private void EnsureDebugContext() {
            if (_debugContext == null) {
                lock(this) {
                    if (_debugContext == null) {
                        try {
                            _debugContext = DebugContext.CreateInstance();
                            _tracePipeline = Debugging.TracePipeline.CreateInstance(_debugContext);
                            _tracePipeline.TraceCallback = new PythonTracebackListenersDispatcher(this);
                        } catch {
                            _debugContext = null;
                            _tracePipeline = null;
                            throw;
                        }
                    }
                }
            }
        }

        private class PythonTracebackListenersDispatcher : Debugging.ITraceCallback  {
            private readonly PythonContext _parent;

            public PythonTracebackListenersDispatcher(PythonContext parent) {
                _parent = parent;
            }

            void Debugging.ITraceCallback.OnTraceEvent(Debugging.TraceEventKind kind, string name, string sourceFileName, SourceSpan sourceSpan, Func<IDictionary<object, object>> scopeCallback, object payload, object customPayload)
            {
                var listener = _parent._tracebackListeners.Value;

                if (listener == null && _parent.PythonOptions.Tracing) {
                    // If tracing without sys.set_trace() is enabled, we need to register a dummy traceback listener,
                    // because of the FunctionStack handling done there.
                    _parent._tracebackListeners.Value = listener = new PythonTracebackListener(_parent, null);
                }

                if (listener != null)
                    listener.OnTraceEvent(kind, name, sourceFileName, sourceSpan, scopeCallback, payload, customPayload);
            }
        }

        internal Debugging.ITracePipeline TracePipeline {
            get {
                return _tracePipeline;
            }
        }

        internal void SetTrace(object o) {
            if (o == null && _debugContext == null)
                return;

            EnsureDebugContext();

            // thread-local
            var oldTraceListener = _tracebackListeners.Value;
            var newTraceListener = oldTraceListener;

            if (o == null) {
                _tracebackListeners.Value = newTraceListener = null;
            } else {
                // We're following CPython behavior here.
                // If CurrentPythonFrame is not null then we're currently inside a traceback, and
                // enabling trace while inside a traceback is only allowed through sys.call_tracing()
                var pyThread = PythonOps.GetFunctionStackNoCreate();
                if (pyThread == null || (oldTraceListener == null || !oldTraceListener.InTraceBack)) {
                    _tracebackListeners.Value = newTraceListener = new PythonTracebackListener(this, o);
                }
            }

            // global
            lock (_codeUpdateLock)
            {
                var oldEnableTracing = EnableTracing;

                if ((oldTraceListener != null) != (newTraceListener != null)) {
                    _tracebackListenersCount += (newTraceListener != null) ? 1 : -1;
                }

                if (EnableTracing != oldEnableTracing) {
                    // DebugContext invocation has changed, we need to update all of our
                    // function codes.
                    FunctionCode.UpdateAllCode(this);
                }
            }
        }

        internal object CallTracing(object func, PythonTuple args) {
            // The CPython implementation basically only stores/restores the
            // nesting level and the recursion control (e.g. that the trace handler
            // is not called from inside of the trace handler)

            var tblistener = (_debugContext != null) ? _tracebackListeners.Value : null;
            var backupInTraceBack = (tblistener != null) ? tblistener.InTraceBack : false;

            if (tblistener != null && backupInTraceBack)
                tblistener.InTraceBack = false;

            try {
                return PythonCalls.Call(func, args.ToArray());
            } finally {
                if (tblistener != null)
                    tblistener.InTraceBack = backupInTraceBack;
            }
        }

        internal object GetTrace()
        {
            if (_debugContext == null)
                return null;

            var listener = _tracebackListeners.Value;
            if (listener == null)
                return null;

            return listener.TraceObject;
        }

#endregion

        internal ExtensionMethodSet UniqifyExtensions(ExtensionMethodSet newSet) {
            int deadIndex = -1;

            // we shouldn't have tons of different sets so we just run through the list of possible sets...
            if (_weakExtensionMethodSets == null) {
                Interlocked.CompareExchange(ref _weakExtensionMethodSets, new List<WeakReference>(), null);
            }

            lock (_weakExtensionMethodSets) {
                for (int i = 0; i < _weakExtensionMethodSets.Count; i++) {
                    var weakSet = _weakExtensionMethodSets[i];

                    var set = (ExtensionMethodSet)weakSet.Target;
                    if (set != null) {
                        if (set == newSet) {
                            return set;
                        }
                    } else {
                        deadIndex = i;
                    }
                }

                if (deadIndex == -1) {
                    _weakExtensionMethodSets.Add(new WeakReference(newSet));
                } else {
                    _weakExtensionMethodSets[deadIndex].Target = newSet;
                }
                return newSet;
            }
        }
    }

    public static class PythonContextHelpers {
        public static PythonContext GetPythonContext(this CodeContext context) {
            return PythonContext.GetContext(context);
        }
    }

    /// <summary>
    /// List of unary operators which we have sites for to enable fast dispatch that
    /// doesn't collide with other operators.
    /// </summary>
    enum UnaryOperators {
        Repr,
        Length,
        Hash,
        String,

        Maximum
    }

    enum TernaryOperators {
        SetDescriptor,
        GetDescriptor,

        Maximum
    }
}
