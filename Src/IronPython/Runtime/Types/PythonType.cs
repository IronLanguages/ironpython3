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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Dynamic;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

namespace IronPython.Runtime.Types {

    /// <summary>
    /// Represents a PythonType.  Instances of PythonType are created via PythonTypeBuilder.  
    /// </summary>
    [DebuggerDisplay("PythonType: {Name}"), DebuggerTypeProxy(typeof(PythonType.DebugProxy))]
    [PythonType("type")]
    [Documentation(@"type(object) -> gets the type of the object
type(name, bases, dict) -> creates a new type instance with the given name, base classes, and members from the dictionary")]
    public partial class PythonType : IPythonMembersList, IDynamicMetaObjectProvider, IWeakReferenceable, IWeakReferenceableByProxy, ICodeFormattable, IFastGettable, IFastSettable, IFastInvokable {
        private Type/*!*/ _underlyingSystemType;            // the underlying CLI system type for this type
        private string _name;                               // the name of the type
        private Dictionary<string, PythonTypeSlot> _dict;   // type-level slots & attributes
        private PythonTypeAttributes _attrs;                // attributes of the type
        private int _flags;                                 // CPython-like flags on the type
        private int _version = GetNextVersion();            // version of the type
        private List<WeakReference> _subtypes;              // all of the subtypes of the PythonType
        private PythonContext _pythonContext;               // the context the type was created from, or null for system types.
        private bool? _objectNew, _objectInit;              // true if the type doesn't override __new__ / __init__ from object.
        internal Dictionary<CachedGetKey, FastGetBase> _cachedGets; // cached gets on user defined type instances
        internal Dictionary<CachedGetKey, FastGetBase> _cachedTryGets; // cached try gets on used defined type instances
        internal Dictionary<SetMemberKey, FastSetBase> _cachedSets; // cached sets on user defined instances
        internal Dictionary<string, TypeGetBase> _cachedTypeGets; // cached gets on types (system and user types)
        internal Dictionary<string, TypeGetBase> _cachedTypeTryGets; // cached gets on types (system and user types)

        // commonly calculatable
        private List<PythonType> _resolutionOrder;          // the search order for methods in the type
        private PythonType/*!*/[]/*!*/ _bases;              // the base classes of the type
        private BuiltinFunction _ctor;                      // the built-in function which allocates an instance - a .NET ctor
        private Type _finalSystemType;                      // base .NET type if we're inherited from another Python-like type.

        // fields that frequently remain null
        private WeakRefTracker _weakrefTracker;             // storage for Python style weak references
        private WeakReference _weakRef;                     // single weak ref instance used for all user PythonTypes.
        private string[] _slots;                            // the slots when the class was created
        private OldClass _oldClass;                         // the associated OldClass or null for new-style types  
        private int _originalSlotCount;                     // the number of slots when the type was created
        private InstanceCreator _instanceCtor;              // creates instances
        private CallSite<Func<CallSite, object, int>> _hashSite;
        private CallSite<Func<CallSite, object, object, bool>> _eqSite;
        private CallSite<Func<CallSite, object, object, int>> _compareSite;
        private Dictionary<CallSignature, LateBoundInitBinder> _lateBoundInitBinders;
        private string[] _optimizedInstanceNames;           // optimized names stored in a custom dictionary
        private int _optimizedInstanceVersion;
        private Dictionary<string, List<MethodInfo>> _extensionMethods;         // extension methods defined on the type

        private PythonSiteCache _siteCache = new PythonSiteCache();

        private PythonTypeSlot _lenSlot;                    // cached length slot, cleared when the type is mutated

        internal Func<string, Exception> _makeException = DefaultMakeException;

        [MultiRuntimeAware]
        private static int MasterVersion = 1;
        private static readonly CommonDictionaryStorage _pythonTypes = new CommonDictionaryStorage();
        internal static readonly PythonType _pythonTypeType = DynamicHelpers.GetPythonTypeFromType(typeof(PythonType));
        private static readonly WeakReference[] _emptyWeakRef = new WeakReference[0];
        private static object _subtypesLock = new object();
        internal static readonly Func<string, Exception> DefaultMakeException = (message) => new Exception(message);

        /// <summary>
        /// Provides delegates that will invoke a parameterless type ctor.  The first key provides
        /// the dictionary for a specific type, the 2nd key provides the delegate for a specific
        /// call site type used in conjunction w/ our IFastInvokable implementation.
        /// </summary>
        private static Dictionary<Type, Dictionary<Type, Delegate>> _fastBindCtors = new Dictionary<Type, Dictionary<Type, Delegate>>();

        /// <summary>
        /// Shared built-in functions for creating instances of user defined types.  Because all
        /// types w/ the same UnderlyingSystemType share the same constructors these can be
        /// shared across multiple types.
        /// </summary>
        private static Dictionary<Type, BuiltinFunction> _userTypeCtors = new Dictionary<Type, BuiltinFunction>();


        /// <summary>
        /// Creates a new type for a user defined type.  The name, base classes (a tuple of type
        /// objects), and a dictionary of members is provided.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public PythonType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary dict)
            : this(context, name, bases, dict, String.Empty) {
        }

        /// <summary>
        /// Creates a new type for a user defined type.  The name, base classes (a tuple of type
        /// objects), and a dictionary of members is provided.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        internal PythonType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary dict, string selfNames) {
            InitializeUserType(context, name, bases, dict, selfNames);
        }

        internal PythonType() {
        }

        /// <summary>
        /// Creates a new PythonType object which is backed by the specified .NET type for
        /// storage.  The type is considered a system type which can not be modified
        /// by the user.
        /// </summary>
        /// <param name="underlyingSystemType"></param>
        internal PythonType(Type underlyingSystemType) {
            _underlyingSystemType = underlyingSystemType;

            InitializeSystemType();
        }

        /// <summary>
        /// Creates a new PythonType which is a subclass of the specified PythonType.
        /// 
        /// Used for runtime defined new-style classes which require multiple inheritance.  The
        /// primary example of this is the exception system.
        /// </summary>
        internal PythonType(PythonType baseType, string name, Func<string, Exception> exceptionMaker) {
            _underlyingSystemType = baseType.UnderlyingSystemType;

            IsSystemType = baseType.IsSystemType;
            IsPythonType = baseType.IsPythonType;
            Name = name;
            _bases = new PythonType[] { baseType };
            ResolutionOrder = Mro.Calculate(this, _bases);
            _attrs |= PythonTypeAttributes.HasDictionary;
            _makeException = exceptionMaker;
        }

        /// <summary>
        /// Creates a new PythonType which is a subclass of the specified PythonTypes.
        /// 
        /// Used for runtime defined new-style classes which require multiple inheritance.  The
        /// primary example of this is the exception system.
        /// </summary>
        internal PythonType(PythonType[] baseTypes, string name) {
            bool isSystemType = false;
            bool isPythonType = false;
            foreach (PythonType baseType in baseTypes) {
                isSystemType |= baseType.IsSystemType;
                isPythonType |= baseType.IsPythonType;
            }
            IsSystemType = isSystemType;
            IsPythonType = isPythonType;
            Name = name;
            _bases = baseTypes;
            ResolutionOrder = Mro.Calculate(this, _bases);
            _attrs |= PythonTypeAttributes.HasDictionary;
        }

        /// <summary>
        /// Creates a new PythonType which is a subclass of the specified PythonTypes.
        /// 
        /// Used for runtime defined new-style classes which require multiple inheritance.  The
        /// primary example of this is the exception system.
        /// </summary>
        internal PythonType(PythonType[] baseTypes, Type underlyingType, string name, Func<string, Exception> exceptionMaker)
            : this(baseTypes, name) {
            _underlyingSystemType = underlyingType;
            _makeException = exceptionMaker;
        }

        /// <summary>
        /// Creates a new PythonType which is a subclass of the specified PythonType.
        /// 
        /// Used for runtime defined new-style classes which require multiple inheritance.  The
        /// primary example of this is the exception system.
        /// </summary>
        internal PythonType(PythonContext context, PythonType baseType, string name, string module, string doc, Func<string, Exception> exceptionMaker)
            : this(baseType, name, exceptionMaker) {
            EnsureDict();

            _dict["__doc__"] = new PythonTypeUserDescriptorSlot(doc, true);
            _dict["__module__"] = new PythonTypeUserDescriptorSlot(module, true);
            IsSystemType = false;
            IsPythonType = false;
            _pythonContext = context;
            _attrs |= PythonTypeAttributes.HasDictionary;
        }

        /// <summary>
        /// Creates a new PythonType which is a subclass of the specified PythonTypes.
        /// 
        /// Used for runtime defined new-style classes which require multiple inheritance.  The
        /// primary example of this is the exception system.
        /// </summary>
        internal PythonType(PythonContext context, PythonType[] baseTypes, string name, string module, string doc)
            : this(baseTypes, name) {
            EnsureDict();

            _dict["__doc__"] = new PythonTypeUserDescriptorSlot(doc, true);
            _dict["__module__"] = new PythonTypeUserDescriptorSlot(module, true);
            _pythonContext = context;
            _attrs |= PythonTypeAttributes.HasDictionary;
        }

        /// <summary>
        /// Creates a new PythonType which is a subclass of the specified PythonTypes.
        /// 
        /// Used for runtime defined new-style classes which require multiple inheritance.  The
        /// primary example of this is the exception system.
        /// </summary>
        internal PythonType(PythonContext context, PythonType[] baseTypes, Type underlyingType, string name, string module, string doc, Func<string, Exception> exceptionMaker)
            : this(baseTypes, underlyingType, name, exceptionMaker) {
            EnsureDict();

            _dict["__doc__"] = new PythonTypeUserDescriptorSlot(doc, true);
            _dict["__module__"] = new PythonTypeUserDescriptorSlot(module, true);
            IsSystemType = false;
            IsPythonType = false;
            _pythonContext = context;
            _attrs |= PythonTypeAttributes.HasDictionary;
        }


        /// <summary>
        /// Creates a new PythonType object which represents an Old-style class.
        /// </summary>
        internal PythonType(OldClass oc) {
            EnsureDict();

            _underlyingSystemType = typeof(OldInstance);
            Name = oc.Name;
            OldClass = oc;

            List<PythonType> ocs = new List<PythonType>(oc.BaseClasses.Count);
            foreach (OldClass klass in oc.BaseClasses) {
                ocs.Add(klass.TypeObject);
            }

            List<PythonType> mro = new List<PythonType>();
            mro.Add(this);

            _bases = ocs.ToArray(); 
            _resolutionOrder = mro;
            AddSlot("__class__", new PythonTypeUserDescriptorSlot(this, true));
        }

        internal BuiltinFunction Ctor {
            get {
                EnsureConstructor();

                return _ctor;
            }
        }

        #region Public API

        public static object __new__(CodeContext/*!*/ context, PythonType cls, string name, PythonTuple bases, PythonDictionary dict) {
            return __new__(context, cls, name, bases, dict, String.Empty);
        }

        internal static object __new__(CodeContext/*!*/ context, PythonType cls, string name, PythonTuple bases, PythonDictionary dict, string selfNames) {
            if (name == null) {
                throw PythonOps.TypeError("type() argument 1 must be string, not None");
            }
            if (bases == null) {
                throw PythonOps.TypeError("type() argument 2 must be tuple, not None");
            }
            if (dict == null) {
                throw PythonOps.TypeError("TypeError: type() argument 3 must be dict, not None");
            }

            EnsureModule(context, dict);

            PythonType meta = FindMetaClass(cls, bases);

            if (meta != TypeCache.OldInstance && meta != TypeCache.PythonType) {
                if (meta != cls) {
                    // the user has a custom __new__ which picked the wrong meta class, call the correct metaclass
                    return PythonCalls.Call(context, meta, name, bases, dict);
                }

                // we have the right user __new__, call our ctor method which will do the actual
                // creation.                   
                return meta.CreateInstance(context, name, bases, dict);
            }

            // no custom user type for __new__
            return new PythonType(context, name, bases, dict, selfNames);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void __init__(string name, PythonTuple bases, PythonDictionary dict) {
        }

        internal static PythonType FindMetaClass(PythonType cls, PythonTuple bases) {
            PythonType meta = cls;
            foreach (object dt in bases) {
                PythonType metaCls = DynamicHelpers.GetPythonType(dt);

                if (metaCls == TypeCache.OldClass) continue;

                if (meta.IsSubclassOf(metaCls)) continue;

                if (metaCls.IsSubclassOf(meta)) {
                    meta = metaCls;
                    continue;
                }
                throw PythonOps.TypeError("metaclass conflict {0} and {1}", metaCls.Name, meta.Name);
            }
            return meta;
        }

        public static object __new__(CodeContext/*!*/ context, object cls, object o) {
            return DynamicHelpers.GetPythonType(o);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void __init__(object o) {
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static PythonTuple Get__bases__(CodeContext/*!*/ context, PythonType/*!*/ type) {
            return type.GetBasesTuple();
        }

        private PythonTuple GetBasesTuple() {
            object[] res = new object[BaseTypes.Count];
            IList<PythonType> bases = BaseTypes;
            for (int i = 0; i < bases.Count; i++) {
                PythonType baseType = bases[i];

                if (baseType.IsOldClass) {
                    res[i] = baseType.OldClass;
                } else {
                    res[i] = baseType;
                }
            }

            return PythonTuple.MakeTuple(res);
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static PythonType Get__base__(CodeContext/*!*/ context, PythonType/*!*/ type) {
            foreach (object typeObj in Get__bases__(context, type)) {
                PythonType pt = typeObj as PythonType;
                if (pt != null) {
                    return pt;
                }
            }
            return null;
        }

        /// <summary>
        /// Used in copy_reg which is the only consumer of __flags__ in the standard library.
        /// 
        /// Set if the type is user defined
        /// </summary>
        private const int TypeFlagHeapType = 0x00000200;

        /// <summary>
        /// Set if the type has __abstractmethods__ defined
        /// </summary>
        private const int TypeFlagAbstractMethodsDefined = 0x00080000;
        private const int TypeFlagAbstractMethodsNonEmpty = 0x00100000;

        private bool SetAbstractMethodFlags(string name, object value) {
            if (name != "__abstractmethods__") {
                return false;
            }

            int res = _flags | TypeFlagAbstractMethodsDefined;

            IEnumerator enumerator;
            if (value == null ||
                !PythonOps.TryGetEnumerator(DefaultContext.Default, value, out enumerator) ||
                !enumerator.MoveNext()) {
                // CPython treats None, non-iterables, and empty iterables as empty sets
                // of abstract methods, and sets this flag accordingly
                res &= ~(TypeFlagAbstractMethodsNonEmpty);
            } else {
                res |= TypeFlagAbstractMethodsNonEmpty;
            }

            _flags = res;
            return true;
        }

        private void ClearAbstractMethodFlags(string name) {
            if (name == "__abstractmethods__") {
                _flags &= ~(TypeFlagAbstractMethodsDefined | TypeFlagAbstractMethodsNonEmpty);
            }
        }

        internal bool HasAbstractMethods(CodeContext/*!*/ context) {
            object abstractMethods;
            IEnumerator en;
            return (_flags & TypeFlagAbstractMethodsNonEmpty) != 0 &&
                TryGetBoundCustomMember(context, "__abstractmethods__", out abstractMethods) &&
                PythonOps.TryGetEnumerator(context, abstractMethods, out en) &&
                en.MoveNext();
        }

        internal string GetAbstractErrorMessage(CodeContext/*!*/ context) {
            if ((_flags & TypeFlagAbstractMethodsNonEmpty) == 0) {
                return null;
            }

            object abstractMethods;
            IEnumerator en;
            if (!TryGetBoundCustomMember(context, "__abstractmethods__", out abstractMethods) ||
                !PythonOps.TryGetEnumerator(context, abstractMethods, out en) ||
                !en.MoveNext()) {
                return null;
            }

            string comma = "";
            StringBuilder error = new StringBuilder("Can't instantiate abstract class ");
            error.Append(Name);
            error.Append(" with abstract methods ");

            int i = 0;
            do {
                string s = en.Current as string;

                if (s == null) {
                    Extensible<string> es = en.Current as Extensible<string>;
                    if (es != null) {
                        s = es.Value;
                    }
                }

                if (s == null) {
                    return string.Format(
                        "sequence item {0}: expected string, {1} found",
                        i, PythonTypeOps.GetName(en.Current)
                    );
                }

                error.Append(comma);
                error.Append(en.Current);

                comma = ", ";
                i++;
            } while (en.MoveNext());

            return error.ToString();
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static int Get__flags__(CodeContext/*!*/ context, PythonType/*!*/ type) {
            int res = type._flags;

            if (type.IsSystemType) {
                res |= TypeFlagHeapType;
            }
            return res;
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static void Set__bases__(CodeContext/*!*/ context, PythonType/*!*/ type, object value) {
            // validate we got a tuple...           
            PythonTuple t = value as PythonTuple;
            if (t == null) throw PythonOps.TypeError("expected tuple of types or old-classes, got '{0}'", PythonTypeOps.GetName(value));

            List<PythonType> ldt = new List<PythonType>();

            foreach (object o in t) {
                // gather all the type objects...
                PythonType adt = o as PythonType;
                if (adt == null) {
                    OldClass oc = o as OldClass;
                    if (oc == null) {
                        throw PythonOps.TypeError("expected tuple of types, got '{0}'", PythonTypeOps.GetName(o));
                    }

                    adt = oc.TypeObject;
                }

                ldt.Add(adt);
            }

#if FEATURE_REFEMIT
            // Ensure that we are not switching the CLI type
            Type newType = NewTypeMaker.GetNewType(type.Name, t);
            if (type.UnderlyingSystemType != newType)
                throw PythonOps.TypeErrorForIncompatibleObjectLayout("__bases__ assignment", type, newType);
#endif

            // set bases & the new resolution order
            List<PythonType> mro = CalculateMro(type, ldt);

            type.BaseTypes = ldt;
            type._resolutionOrder = mro;
        }

        private static List<PythonType> CalculateMro(PythonType type, IList<PythonType> ldt) {
            return Mro.Calculate(type, ldt);
        }

        private static bool TryReplaceExtensibleWithBase(Type curType, out Type newType) {
            if (curType.IsGenericType &&
                curType.GetGenericTypeDefinition() == typeof(Extensible<>)) {
                newType = curType.GetGenericArguments()[0];
                return true;
            }
            newType = null;
            return false;
        }

        public object __call__(CodeContext context, params object[] args) {
            return PythonTypeOps.CallParams(context, this, args);
        }

        public object __call__(CodeContext context, [ParamDictionary]IDictionary<string, object> kwArgs, params object[] args) {
            return PythonTypeOps.CallWorker(context, this, kwArgs, args);
        }

        public int __cmp__([NotNull]PythonType other) {
            if (other != this) {
                int res = Name.CompareTo(other.Name);

                if (res == 0) {
                    long thisId = IdDispenser.GetId(this);
                    long otherId = IdDispenser.GetId(other);
                    if (thisId > otherId) {
                        return 1;
                    } else {
                        return -1;
                    }
                }
                return res;
            }
            return 0;
        }

        // Do not override == and != because it causes lots of spurious warnings
        // TODO Replace those warnings with .ReferenceEquals calls & overload ==/!=
        public bool __eq__([NotNull]PythonType other) {
            return this.__cmp__(other) == 0;
        }

        public bool __ne__([NotNull]PythonType other) {
            return this.__cmp__(other) != 0;
        }

        [Python3Warning("type inequality comparisons not supported in 3.x")]
        public static bool operator >(PythonType self, PythonType other) {
            return self.__cmp__(other) > 0;
        }

        [Python3Warning("type inequality comparisons not supported in 3.x")]
        public static bool operator <(PythonType self, PythonType other) {
            return self.__cmp__(other) < 0;
        }

        [Python3Warning("type inequality comparisons not supported in 3.x")]
        public static bool operator >=(PythonType self, PythonType other) {
            return self.__cmp__(other) >= 0;
        }

        [Python3Warning("type inequality comparisons not supported in 3.x")]
        public static bool operator <=(PythonType self, PythonType other) {
            return self.__cmp__(other) <= 0;
        }

        public void __delattr__(CodeContext/*!*/ context, string name) {
            DeleteCustomMember(context, name);
        }

        [SlotField]
        public static PythonTypeSlot __dict__ = new PythonTypeDictSlot(_pythonTypeType);

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static object Get__doc__(CodeContext/*!*/ context, PythonType self) {
            PythonTypeSlot pts;
            object res;
            if (self.TryLookupSlot(context, "__doc__", out pts) &&
                pts.TryGetValue(context, null, self, out res)) {
                return res;
            } else if (self.IsSystemType) {
                return PythonTypeOps.GetDocumentation(self.UnderlyingSystemType);
            }

            return null;
        }

        public object __getattribute__(CodeContext/*!*/ context, string name) {
            object value;
            if (TryGetBoundCustomMember(context, name, out value)) {
                return value;
            }

            throw PythonOps.AttributeError("type object '{0}' has no attribute '{1}'", Name, name);
        }

        public PythonType this[params Type[] args] {
            get {
                if (UnderlyingSystemType == typeof(Array)) {
                    if (args.Length == 1) {
                        return DynamicHelpers.GetPythonTypeFromType(args[0].MakeArrayType());
                    }
                    throw PythonOps.TypeError("expected one argument to make array type, got {0}", args.Length);
                }

                if (!UnderlyingSystemType.IsGenericTypeDefinition()) {
                    throw new InvalidOperationException("MakeGenericType on non-generic type");
                }

                return DynamicHelpers.GetPythonTypeFromType(UnderlyingSystemType.MakeGenericType(args));
            }
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static object Get__module__(CodeContext/*!*/ context, PythonType self) {
            PythonTypeSlot pts;
            object res;
            if (self._dict != null && 
                self._dict.TryGetValue("__module__", out pts) && 
                pts.TryGetValue(context, self, DynamicHelpers.GetPythonType(self), out res)) {
                return res;
            }
            return PythonTypeOps.GetModuleName(context, self.UnderlyingSystemType);
        }

        [SpecialName, PropertyMethod, WrapperDescriptor, PythonHidden]
        public static string Get__clr_assembly__(PythonType self) {
            return self.UnderlyingSystemType.Namespace + " in " + self.UnderlyingSystemType.GetTypeInfo().Assembly.FullName;
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static void Set__module__(CodeContext/*!*/ context, PythonType self, object value) {
            if (self.IsSystemType) {
                throw PythonOps.TypeError("can't set {0}.__module__", self.Name);
            }

            Debug.Assert(self._dict != null);
            self._dict["__module__"] = new PythonTypeUserDescriptorSlot(value);
            self.UpdateVersion();
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static void Delete__module__(CodeContext/*!*/ context, PythonType self) {
            throw PythonOps.TypeError("can't delete {0}.__module__", self.Name);
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static PythonTuple Get__mro__(PythonType type) {
            return PythonTypeOps.MroToPython(type.ResolutionOrder);
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static string Get__name__(PythonType type) {
            return type.Name;
        }

        [SpecialName, PropertyMethod, WrapperDescriptor]
        public static void Set__name__(PythonType type, string name) {
            if (type.IsSystemType) {
                throw PythonOps.TypeError("can't set attributes of built-in/extension type '{0}'", type.Name);
            }

            type.Name = name;
        }

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            string name = Name;

            if (IsSystemType) {
                if (PythonTypeOps.IsRuntimeAssembly(UnderlyingSystemType.GetTypeInfo().Assembly) || IsPythonType) {
                    object module = Get__module__(context, this);
                    if (!module.Equals("builtins")) {
                        return string.Format("<type '{0}.{1}'>", module, Name);
                    }
                }
                return string.Format("<type '{0}'>", Name);
            } else {
                PythonTypeSlot dts;
                string module = "unknown";
                object modObj;
                if (TryLookupSlot(context, "__module__", out dts) &&
                    dts.TryGetValue(context, this, this, out modObj)) {
                    module = modObj as string;
                }
                return string.Format("<class '{0}.{1}'>", module, name);
            }
        }

        internal string/*!*/ GetTypeDebuggerDisplay() {
            PythonTypeSlot dts;
            string module = "unknown";
            object modObj;
            if (TryLookupSlot(Context.SharedContext, "__module__", out dts) &&
                dts.TryGetValue(Context.SharedContext, this, this, out modObj)) {
                module = modObj as string;
            }
            return string.Format("{0}.{1} instance", module, Name);
        }

        public void __setattr__(CodeContext/*!*/ context, string name, object value) {
            SetCustomMember(context, name, value);
        }

        public List __subclasses__(CodeContext/*!*/ context) {
            List ret = new List();
            IList<WeakReference> subtypes = SubTypes;

            if (subtypes != null) {
                PythonContext pc = PythonContext.GetContext(context);

                foreach (WeakReference wr in subtypes) {
                    if (wr.IsAlive) {
                        PythonType pt = (PythonType)wr.Target;

                        if (pt.PythonContext == null || pt.PythonContext == pc) {
                            ret.AddNoLock(wr.Target);
                        }
                    }
                }
            }

            return ret;
        }

        public virtual List mro() {
            return new List(Get__mro__(this));
        }

        /// <summary>
        /// Returns true if the specified object is an instance of this type.
        /// </summary>
        public virtual bool __instancecheck__(object instance) {
            return SubclassImpl(DynamicHelpers.GetPythonType(instance));
        }

        public virtual bool __subclasscheck__(PythonType sub) {
            return SubclassImpl(sub);
        }

        private bool SubclassImpl(PythonType sub) {
            if (UnderlyingSystemType.IsInterface()) {
                // interfaces aren't in bases, and therefore IsSubclassOf doesn't do this check.
                if (UnderlyingSystemType.IsAssignableFrom(sub.UnderlyingSystemType)) {
                    return true;
                }
            }

            return sub.IsSubclassOf(this);
        }

        public virtual bool __subclasscheck__(OldClass sub) {
            return IsSubclassOf(sub.TypeObject);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates")]
        public static implicit operator Type(PythonType self) {
            return self.UnderlyingSystemType;
        }

        public static implicit operator TypeTracker(PythonType self) {
            return TypeTracker.GetTypeTracker(self.UnderlyingSystemType);
        }

        #endregion

        #region Internal API

        internal bool IsMixedNewStyleOldStyle() {
            if (!IsOldClass) {
                foreach (PythonType baseType in ResolutionOrder) {
                    if (baseType.IsOldClass) {
                        // mixed new-style/old-style class, we can't handle
                        // __init__ in an old-style class yet (it doesn't show
                        // up in a slot).
                        return true;
                    }
                }
            }
            return false;
        }

        internal int SlotCount {
            get {
                return _originalSlotCount;
            }
        }

        /// <summary>
        /// Gets the name of the dynamic type
        /// </summary>
        internal string Name {
            get {
                return _name;
            }
            set {
                _name = value;
            }
        }

        internal int Version {
            get {
                return _version;
            }
        }

        internal bool IsNull {
            get {
                return UnderlyingSystemType == typeof(DynamicNull);
            }
        }

        /// <summary>
        /// Gets the resolution order used for attribute lookup
        /// </summary>
        internal IList<PythonType> ResolutionOrder {
            get {
                return _resolutionOrder;
            }
            set {
                lock (SyncRoot) {
                    _resolutionOrder = new List<PythonType>(value);
                }
            }
        }

        /// <summary>
        /// Gets the dynamic type that corresponds with the provided static type. 
        /// 
        /// Returns null if no type is available.  TODO: In the future this will
        /// always return a PythonType created by the DLR.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static PythonType/*!*/ GetPythonType(Type type) {
            object res;
            
            if (!_pythonTypes.TryGetValue(type, out res)) {
                lock (_pythonTypes) {
                    if (!_pythonTypes.TryGetValue(type, out res)) {
                        res = new PythonType(type);

                        _pythonTypes.Add(type, res);
                    }
                }
            }

            return (PythonType)res;
        }

        /// <summary>
        /// Sets the python type that corresponds with the provided static type.
        /// 
        /// This is used for built-in types which have a metaclass.  Currently
        /// only used by ctypes.
        /// </summary>
        internal static PythonType SetPythonType(Type type, PythonType pyType) {
            lock (_pythonTypes) {
                Debug.Assert(!_pythonTypes.Contains(type));
                Debug.Assert(pyType.GetType() != typeof(PythonType));

                _pythonTypes.Add(type, pyType);
            }
            return pyType;
        }

        /// <summary>
        /// Allocates the storage for the instance running the .NET constructor.  This provides
        /// the creation functionality for __new__ implementations.
        /// </summary>
        internal object CreateInstance(CodeContext/*!*/ context) {
            EnsureInstanceCtor();

            return _instanceCtor.CreateInstance(context);
        }

        /// <summary>
        /// Allocates the storage for the instance running the .NET constructor.  This provides
        /// the creation functionality for __new__ implementations.
        /// </summary>
        internal object CreateInstance(CodeContext/*!*/ context, object arg0) {
            EnsureInstanceCtor();

            return _instanceCtor.CreateInstance(context, arg0);
        }

        /// <summary>
        /// Allocates the storage for the instance running the .NET constructor.  This provides
        /// the creation functionality for __new__ implementations.
        /// </summary>
        internal object CreateInstance(CodeContext/*!*/ context, object arg0, object arg1) {
            EnsureInstanceCtor();

            return _instanceCtor.CreateInstance(context, arg0, arg1);
        }

        /// <summary>
        /// Allocates the storage for the instance running the .NET constructor.  This provides
        /// the creation functionality for __new__ implementations.
        /// </summary>
        internal object CreateInstance(CodeContext/*!*/ context, object arg0, object arg1, object arg2) {
            EnsureInstanceCtor();

            return _instanceCtor.CreateInstance(context, arg0, arg1, arg2);
        }

        /// <summary>
        /// Allocates the storage for the instance running the .NET constructor.  This provides
        /// the creation functionality for __new__ implementations.
        /// </summary>
        internal object CreateInstance(CodeContext context, params object[] args) {
            Assert.NotNull(args);
            EnsureInstanceCtor();

            // unpack args for common cases so we don't generate code to do it...
            switch (args.Length) {
                case 0: return _instanceCtor.CreateInstance(context);
                case 1: return _instanceCtor.CreateInstance(context, args[0]);
                case 2: return _instanceCtor.CreateInstance(context, args[0], args[1]);
                case 3: return _instanceCtor.CreateInstance(context, args[0], args[1], args[2]);
                default: 
                    return _instanceCtor.CreateInstance(context, args);
            }
        }

        /// <summary>
        /// Allocates the storage for the instance running the .NET constructor.  This provides
        /// the creation functionality for __new__ implementations.
        /// </summary>
        internal object CreateInstance(CodeContext context, object[] args, string[] names) {
            Assert.NotNull(args, "args");
            Assert.NotNull(names, "names");

            EnsureInstanceCtor();

            return _instanceCtor.CreateInstance(context, args, names);
        }

        internal int Hash(object o) {
            EnsureHashSite();

            return _hashSite.Target(_hashSite, o);
        }

        internal bool TryGetLength(CodeContext context, object o, out int length) {
            CallSite<Func<CallSite, CodeContext, object, object>> lenSite;
            if (IsSystemType) {
                lenSite = PythonContext.GetContext(context).GetSiteCacheForSystemType(UnderlyingSystemType).GetLenSite(context);
            } else {
                lenSite = _siteCache.GetLenSite(context);
            }

            PythonTypeSlot lenSlot = _lenSlot;
            if (lenSlot == null && !PythonOps.TryResolveTypeSlot(context, this, "__len__", out lenSlot)) {
                length = 0;
                return false;                
            }

            object func;
            if (!lenSlot.TryGetValue(context, o, this, out func)) {
                length = 0;
                return false;
            }

            object res = lenSite.Target(lenSite, context, func);
            if (!(res is int)) {
                throw PythonOps.ValueError("__len__ must return int");
            }

            length = (int)res;
            return true;
        }

        internal bool EqualRetBool(object self, object other) {
            if (_eqSite == null) {
                Interlocked.CompareExchange(
                    ref _eqSite,
                    Context.CreateComparisonSite(PythonOperationKind.Equal),
                    null
                );
            }

            return _eqSite.Target(_eqSite, self, other);
        }

        internal int Compare(object self, object other) {
            if (_compareSite == null) {
                Interlocked.CompareExchange(
                    ref _compareSite,
                    Context.MakeSortCompareSite(),
                    null
                );
            }

            return _compareSite.Target(_compareSite, self, other);
        }

        internal bool TryGetBoundAttr(CodeContext context, object o, string name, out object ret) {
            CallSite<Func<CallSite, object, CodeContext, object>> site;
            if (IsSystemType) {
                site = PythonContext.GetContext(context).GetSiteCacheForSystemType(UnderlyingSystemType).GetTryGetMemberSite(context, name);
            } else {
                site = _siteCache.GetTryGetMemberSite(context, name);
            }

            try {
                ret = site.Target(site, o, context);
                return ret != OperationFailed.Value;
            } catch (MissingMemberException) {
                ret = null;
                return false;
            }
        }

        internal CallSite<Func<CallSite, object, int>> HashSite {
            get {
                EnsureHashSite();

                return _hashSite;
            }
        }

        private void EnsureHashSite() {
            if(_hashSite == null) {
                Interlocked.CompareExchange(
                    ref _hashSite,
                    CallSite<Func<CallSite, object, int>>.Create(
                        Context.Operation(
                            PythonOperationKind.Hash
                        )
                    ),
                    null
                );
            }
        }

        /// <summary>
        /// Gets the underlying system type that is backing this type.  All instances of this
        /// type are an instance of the underlying system type.
        /// </summary>
        internal Type/*!*/ UnderlyingSystemType {
            get {
                return _underlyingSystemType;
            }
        }

        internal Type/*!*/ FinalSystemType {
            get {
                return _finalSystemType ?? (_finalSystemType = PythonTypeOps.GetFinalSystemType(_underlyingSystemType));
            }
        }

        /// <summary>
        /// Gets the extension type for this type.  The extension type provides
        /// a .NET type which can be inherited from to extend sealed classes
        /// or value types which Python allows inheritance from.
        /// </summary>
        internal Type/*!*/ ExtensionType {
            get {
                if (!_underlyingSystemType.IsEnum()) {
                    switch (_underlyingSystemType.GetTypeCode()) {
                        case TypeCode.String: return typeof(ExtensibleString);
                        case TypeCode.Int32: return typeof(Extensible<int>);
                        case TypeCode.Double: return typeof(Extensible<double>);
                        case TypeCode.Object:
                            if (_underlyingSystemType == typeof(BigInteger)) {
                                return typeof(Extensible<BigInteger>);
                            } else if (_underlyingSystemType == typeof(Complex)) {
                                return typeof(ExtensibleComplex);
                            }
                            break;
                    }
                }
                return _underlyingSystemType;
            }
        }

        /// <summary>
        /// Gets the base types from which this type inherits.
        /// </summary>
        internal IList<PythonType>/*!*/ BaseTypes {
            get {
                return _bases;
            }
            set {
                // validate input...
                foreach (PythonType pt in value) {
                    if (pt == null) throw new ArgumentNullException("value", "a PythonType was null while assigning base classes");
                }

                // first update our sub-type list

                lock (_bases) {
                    foreach (PythonType dt in _bases) {
                        dt.RemoveSubType(this);
                    }

                    // set the new bases
                    List<PythonType> newBases = new List<PythonType>(value);

                    // add us as subtypes of our new bases
                    foreach (PythonType dt in newBases) {
                        dt.AddSubType(this);
                    }

                    UpdateVersion();
                    _bases = newBases.ToArray();
                }
            }
        }

        /// <summary>
        /// Returns true if this type is a subclass of other
        /// </summary>
        internal bool IsSubclassOf(PythonType other) {
            // check for a type match
            if (other == this) {
                return true;
            }

            //Python doesn't have value types inheriting from ValueType, but we fake this for interop
            if (other.UnderlyingSystemType == typeof(ValueType) && UnderlyingSystemType.IsValueType()) {
                return true;
            }

            return IsSubclassWorker(other);
        }

        private bool IsSubclassWorker(PythonType other) {
            for (int i = 0; i < _bases.Length; i++) {
                PythonType baseClass = _bases[i];

                if (baseClass == other || baseClass.IsSubclassWorker(other)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True if the type is a system type.  A system type is a type which represents an
        /// underlying .NET type and not a subtype of one of these types.
        /// </summary>
        internal bool IsSystemType {
            get {
                return (_attrs & PythonTypeAttributes.SystemType) != 0;
            }
            set {
                if (value) _attrs |= PythonTypeAttributes.SystemType;
                else _attrs &= (~PythonTypeAttributes.SystemType);
            }
        }

        internal bool IsWeakReferencable {
            get {
                return (_attrs & PythonTypeAttributes.WeakReferencable) != 0;
            }
            set {
                if (value) _attrs |= PythonTypeAttributes.WeakReferencable;
                else _attrs &= (~PythonTypeAttributes.WeakReferencable);
            }
        }

        internal bool HasDictionary {
            get {
                return (_attrs & PythonTypeAttributes.HasDictionary) != 0;
            }
            set {
                if (value) _attrs |= PythonTypeAttributes.HasDictionary;
                else _attrs &= (~PythonTypeAttributes.HasDictionary);
            }
        }

        internal bool HasSystemCtor {
            get {
                return (_attrs & PythonTypeAttributes.SystemCtor) != 0;
            }
        }

        internal void SetConstructor(BuiltinFunction ctor) {
            _ctor = ctor;
        }

        internal bool IsPythonType {
            get {
                return (_attrs & PythonTypeAttributes.IsPythonType) != 0;
            }
            set {
                if (value) {
                    _attrs |= PythonTypeAttributes.IsPythonType;
                } else {
                    _attrs &= ~PythonTypeAttributes.IsPythonType;
                }
            }
        }

        internal OldClass OldClass {
            get {
                return _oldClass;
            }
            set {
                _oldClass = value;
            }
        }

        internal bool IsOldClass {
            get {
                return _oldClass != null;
            }
        }

        internal PythonContext PythonContext {
            get {
                return _pythonContext;
            }
        }

        internal PythonContext/*!*/ Context {
            get {
                return _pythonContext ?? DefaultContext.DefaultPythonContext;
            }
        }

        internal object SyncRoot {
            get {
                // TODO: This is un-ideal, we should lock on something private.
                return this;
            }
        }

        internal bool IsHiddenMember(string name) {
            PythonTypeSlot dummySlot;
            return !TryResolveSlot(DefaultContext.Default, name, out dummySlot) &&
                    TryResolveSlot(DefaultContext.DefaultCLS, name, out dummySlot);
        }
        
        internal LateBoundInitBinder GetLateBoundInitBinder(CallSignature signature) {
            Debug.Assert(!IsSystemType); // going to hold onto a PythonContext, shouldn't ever be a system type
            Debug.Assert(_pythonContext != null);

            if (_lateBoundInitBinders == null) {
                Interlocked.CompareExchange(ref _lateBoundInitBinders, new Dictionary<CallSignature, LateBoundInitBinder>(), null);
            }

            lock(_lateBoundInitBinders) {
                LateBoundInitBinder res;
                if (!_lateBoundInitBinders.TryGetValue(signature, out res)) {
                    _lateBoundInitBinders[signature] = res = new LateBoundInitBinder(this, signature);
                }

                return res;
            }
        }

        internal Dictionary<string, List<MethodInfo>> ExtensionMethods {
            get {
                if (_extensionMethods == null) {
                    Dictionary<string, List<MethodInfo>> extMethods = new Dictionary<string, List<MethodInfo>>();

                    foreach (var method in UnderlyingSystemType.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
                        if (method.IsExtension()) {
                            List<MethodInfo> methods;
                            if (!extMethods.TryGetValue(method.Name, out methods)) {
                                extMethods[method.Name] = methods = new List<MethodInfo>();
                            }
                            methods.Add(method);
                        }
                    }

                    _extensionMethods = extMethods;
                }
                return _extensionMethods;
            }
        }

        #endregion

        #region Type member access

        /// <summary>
        /// Looks up a slot on the dynamic type
        /// </summary>
        internal bool TryLookupSlot(CodeContext context, string name, out PythonTypeSlot slot) {
            if (IsSystemType) {
                return PythonBinder.GetBinder(context).TryLookupSlot(context, this, name, out slot);
            }

            return _dict.TryGetValue(name, out slot);
        }

        /// <summary>
        /// Searches the resolution order for a slot matching by name
        /// </summary>
        internal bool TryResolveSlot(CodeContext context, string name, out PythonTypeSlot slot) {
            for (int i = 0; i < _resolutionOrder.Count; i++) {
                PythonType dt = _resolutionOrder[i];

                // don't look at interfaces - users can inherit from them, but we resolve members
                // via methods implemented on types and defined by Python.
                if (dt.IsSystemType && !dt.UnderlyingSystemType.IsInterface()) {
                    return PythonBinder.GetBinder(context).TryResolveSlot(context, dt, this, name, out slot);
                }

                if (dt.TryLookupSlot(context, name, out slot)) {
                    return true;
                }
            }

            if (UnderlyingSystemType.IsInterface()) {
                return TypeCache.Object.TryResolveSlot(context, name, out slot);
            }
            

            slot = null;
            return false;
        }

        /// <summary>
        /// Searches the resolution order for a slot matching by name.
        /// 
        /// Includes searching for methods in old-style classes
        /// </summary>
        internal bool TryResolveMixedSlot(CodeContext context, string name, out PythonTypeSlot slot) {
            for (int i = 0; i < _resolutionOrder.Count; i++) {
                PythonType dt = _resolutionOrder[i];

                if (dt.TryLookupSlot(context, name, out slot)) {
                    return true;
                }

                if (dt.OldClass != null) {
                    object ret;
                    if (dt.OldClass.TryLookupSlot(name, out ret)) {
                        slot = ToTypeSlot(ret);
                        return true;
                    }
                }
            }

            slot = null;
            return false;
        }

        /// <summary>
        /// Internal helper to add a new slot to the type
        /// </summary>
        /// <param name="name"></param>
        /// <param name="slot"></param>
        internal void AddSlot(string name, PythonTypeSlot slot) {
            Debug.Assert(!IsSystemType);

            _dict[name] = slot;
            if (name == "__new__") {
                _objectNew = null;
                ClearObjectNewInSubclasses(this);
            } else if (name == "__init__") {
                _objectInit = null;
                ClearObjectInitInSubclasses(this);
            }
        }

        private void ClearObjectNewInSubclasses(PythonType pt) {
            lock (_subtypesLock) {
                if (pt._subtypes != null) {
                    foreach (WeakReference wr in pt._subtypes) {
                        PythonType type = wr.Target as PythonType;
                        if (type != null) {
                            type._objectNew = null;

                            ClearObjectNewInSubclasses(type);
                        }
                    }
                }
            }
        }

        private void ClearObjectInitInSubclasses(PythonType pt) {
            lock (_subtypesLock) {
                if (pt._subtypes != null) {
                    foreach (WeakReference wr in pt._subtypes) {
                        PythonType type = wr.Target as PythonType;
                        if (type != null) {
                            type._objectInit = null;

                            ClearObjectInitInSubclasses(type);
                        }
                    }
                }
            }
        }

        internal bool TryGetCustomSetAttr(CodeContext context, out PythonTypeSlot pts) {
            PythonContext pc = PythonContext.GetContext(context);
            return pc.Binder.TryResolveSlot(
                    context,
                    DynamicHelpers.GetPythonType(this),
                    this,
                    "__setattr__",
                    out pts) &&
                    pts is BuiltinMethodDescriptor &&
                    ((BuiltinMethodDescriptor)pts).DeclaringType != typeof(PythonType);
        }

        internal void SetCustomMember(CodeContext/*!*/ context, string name, object value) {
            Debug.Assert(context != null);

            PythonTypeSlot dts;
            if (TryResolveSlot(context, name, out dts)) {
                if (dts.TrySetValue(context, null, this, value)) {
                    return;
                }
            }

            if (PythonType._pythonTypeType.TryResolveSlot(context, name, out dts)) {
                if (dts.TrySetValue(context, this, PythonType._pythonTypeType, value)) {
                    return;
                }
            }

            if (IsSystemType) {
                throw new MissingMemberException(String.Format("'{0}' object has no attribute '{1}'", Name, name));
            }

            PythonTypeSlot curSlot;
            if (!(value is PythonTypeSlot) && _dict.TryGetValue(name, out curSlot) && curSlot is PythonTypeUserDescriptorSlot) {
                if (SetAbstractMethodFlags(name, value)) {
                    UpdateVersion();
                }
                ((PythonTypeUserDescriptorSlot)curSlot).Value = value;
            } else {
                SetAbstractMethodFlags(name, value);
                AddSlot(name, ToTypeSlot(value));
                UpdateVersion();
            }
        }

        internal static PythonTypeSlot ToTypeSlot(object value) {
            PythonTypeSlot pts = value as PythonTypeSlot;
            if (pts != null) {
                return pts;
            }

            // We could do more checks for things which aren't descriptors
            if (value != null) { 
                return new PythonTypeUserDescriptorSlot(value);
            }

            return new PythonTypeUserDescriptorSlot(value, true);
        }


        internal bool DeleteCustomMember(CodeContext/*!*/ context, string name) {
            Debug.Assert(context != null);

            PythonTypeSlot dts;
            if (TryResolveSlot(context, name, out dts)) {
                if (dts.TryDeleteValue(context, null, this))
                    return true;
            }

            if (IsSystemType) {
                throw new MissingMemberException(String.Format("can't delete attributes of built-in/extension type '{0}'", Name));
            }

            if (!_dict.Remove(name)) {
                throw new MissingMemberException(String.Format(CultureInfo.CurrentCulture,
                    IronPython.Resources.MemberDoesNotExist,
                    name.ToString()));
            }

            // match CPython's buggy behavior, there's a test in test_class for this.
            /*
            if (name == "__new__") {
                _objectNew = null;
                ClearObjectNewInSubclasses(this);
            } else if (name == "__init__") {
                _objectInit = null;
                ClearObjectInitInSubclasses(this);
            }*/

            ClearAbstractMethodFlags(name);
            UpdateVersion();
            return true;
        }

        internal bool TryGetBoundCustomMember(CodeContext context, string name, out object value) {
            PythonTypeSlot dts;
            if (TryResolveSlot(context, name, out dts) &&
                dts.TryGetValue(context, null, this, out value)) {
                return true;
            }

            // search the type
            PythonType myType = DynamicHelpers.GetPythonType(this);
            if (myType.TryResolveSlot(context, name, out dts) &&
                dts.TryGetValue(context, this, myType, out value)) {
                return true;
            }

            value = null;
            return false;
        }

        #region IFastGettable Members

        T IFastGettable.MakeGetBinding<T>(CallSite<T> site, PythonGetMemberBinder/*!*/ binder, CodeContext context, string name) {
            return (T)(object)new MetaPythonType.FastGetBinderHelper(this, context, binder).GetBinding();
        }

        #endregion

        #endregion

        #region Instance member access

        internal object GetMember(CodeContext context, object instance, string name) {
            object res;
            if (TryGetMember(context, instance, name, out res)) {
                return res;
            }

            throw new MissingMemberException(String.Format(CultureInfo.CurrentCulture,
                IronPython.Resources.CantFindMember,
                name));
        }

        internal void SetMember(CodeContext context, object instance, string name, object value) {
            if (TrySetMember(context, instance, name, value)) {
                return;
            }

            throw new MissingMemberException(
                String.Format(CultureInfo.CurrentCulture,
                    IronPython.Resources.Slot_CantSet,
                    name));
        }

        internal void DeleteMember(CodeContext context, object instance, string name) {
            if (TryDeleteMember(context, instance, name)) {
                return;
            }

            throw new MissingMemberException(String.Format(CultureInfo.CurrentCulture, "couldn't delete member {0}", name));
        }

        /// <summary>
        /// Gets a value from a dynamic type and any sub-types.  Values are stored in slots (which serve as a level of 
        /// indirection).  This searches the types resolution order and returns the first slot that
        /// contains the value.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        internal bool TryGetMember(CodeContext context, object instance, string name, out object value) {
            if (TryGetNonCustomMember(context, instance, name, out value)) {
                return true;
            }

            try {
                if (PythonTypeOps.TryInvokeBinaryOperator(context, instance, name, "__getattr__", out value)) {
                    return true;
                }
            } catch (MissingMemberException) {
                //!!! when do we let the user see this exception?
            }

            return false;
        }

        /// <summary>
        /// Attempts to lookup a member w/o using the customizer.  Equivelent to object.__getattribute__
        /// but it doens't throw an exception.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        internal bool TryGetNonCustomMember(CodeContext context, object instance, string name, out object value) {
            PythonType pt;
            IPythonObject sdo;
            bool hasValue = false;
            value = null;

            // first see if we have the value in the instance dictionary...
            // TODO: Instance checks should also work on functions, 
            if ((pt = instance as PythonType) != null) {
                PythonTypeSlot pts;
                if (pt.TryLookupSlot(context, name, out pts)) {
                    hasValue = pts.TryGetValue(context, null, this, out value);
                }
            } else if ((sdo = instance as IPythonObject) != null) {
                PythonDictionary dict = sdo.Dict;

                hasValue = dict != null && dict.TryGetValue(name, out value);
            } 

            // then check through all the descriptors.  If we have a data
            // descriptor it takes priority over the value we found in the
            // dictionary.  Otherwise only run a get descriptor if we don't
            // already have a value.
            for (int i = 0; i < _resolutionOrder.Count; i++) {
                PythonType dt = _resolutionOrder[i];

                PythonTypeSlot slot;
                object newValue;
                if (dt.TryLookupSlot(context, name, out slot)) {
                    if (!hasValue || slot.IsSetDescriptor(context, this)) {
                        if (slot.TryGetValue(context, instance, this, out newValue))
                            value = newValue;
                            return true;
                    }
                }
            }

            return hasValue;
        }

        /// <summary>
        /// Gets a value from a dynamic type and any sub-types.  Values are stored in slots (which serve as a level of 
        /// indirection).  This searches the types resolution order and returns the first slot that
        /// contains the value.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        internal bool TryGetBoundMember(CodeContext context, object instance, string name, out object value) {
            object getattr;
            if (TryResolveNonObjectSlot(context, instance, "__getattribute__", out getattr)) {
                value = InvokeGetAttributeMethod(context, name, getattr);
                return true;
            }

            return TryGetNonCustomBoundMember(context, instance, name, out value);
        }

        private object InvokeGetAttributeMethod(CodeContext context, string name, object getattr) {
            CallSite<Func<CallSite, CodeContext, object, string, object>> getAttributeSite;
            if (IsSystemType) {
                getAttributeSite = PythonContext.GetContext(context).GetSiteCacheForSystemType(UnderlyingSystemType).GetGetAttributeSite(context);
            } else {
                getAttributeSite = _siteCache.GetGetAttributeSite(context);
            }

            return getAttributeSite.Target(getAttributeSite, context, getattr, name);
        }

        /// <summary>
        /// Attempts to lookup a member w/o using the customizer.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        internal bool TryGetNonCustomBoundMember(CodeContext context, object instance, string name, out object value) {
            IPythonObject sdo = instance as IPythonObject;
            if (sdo != null) {
                PythonDictionary iac = sdo.Dict;
                if (iac != null && iac.TryGetValue(name, out value)) {
                    return true;
                }
            }

            if (TryResolveSlot(context, instance, name, out value)) {
                return true;
            }

            try {
                object getattr;
                if (TryResolveNonObjectSlot(context, instance, "__getattr__", out getattr)) {
                    value = InvokeGetAttributeMethod(context, name, getattr);
                    return true;
                }
            } catch (MissingMemberException) {
                //!!! when do we let the user see this exception?
            }

            value = null;
            return false;
        }

        private bool TryResolveSlot(CodeContext context, object instance, string name, out object value) {
            for (int i = 0; i < _resolutionOrder.Count; i++) {
                PythonType dt = _resolutionOrder[i];

                PythonTypeSlot slot;
                if (dt.TryLookupSlot(context, name, out slot)) {
                    if (slot.TryGetValue(context, instance, this, out value))
                        return true;
                }
            }

            value = null;
            return false;
        }

        private bool TryResolveNonObjectSlot(CodeContext context, object instance, string name, out object value) {
            for (int i = 0; i < _resolutionOrder.Count; i++) {
                PythonType dt = _resolutionOrder[i];

                if (dt == TypeCache.Object) break;

                PythonTypeSlot slot;
                if (dt.TryLookupSlot(context, name, out slot)) {
                    if (slot.TryGetValue(context, instance, this, out value))
                        return true;
                }
            }

            value = null;
            return false;
        }


        /// <summary>
        /// Sets a value on an instance.  If a slot is available in the most derived type the slot
        /// is set there, otherwise the value is stored directly in the instance.
        /// </summary>
        internal bool TrySetMember(CodeContext context, object instance, string name, object value) {
            object setattr;
            if (TryResolveNonObjectSlot(context, instance, "__setattr__", out setattr)) {
                CallSite<Func<CallSite, CodeContext, object, object, string, object, object>> setAttrSite;
                if (IsSystemType) {
                    setAttrSite = PythonContext.GetContext(context).GetSiteCacheForSystemType(UnderlyingSystemType).GetSetAttrSite(context);
                } else {
                    setAttrSite = _siteCache.GetSetAttrSite(context);
                }

                setAttrSite.Target(setAttrSite, context, setattr, instance, name, value);
                return true;                              
            }

            return TrySetNonCustomMember(context, instance, name, value);
        }

        /// <summary>
        /// Attempst to set a value w/o going through the customizer.
        /// 
        /// This enables languages to provide the "base" implementation for setting attributes
        /// so that the customizer can call back here.
        /// </summary>
        internal bool TrySetNonCustomMember(CodeContext context, object instance, string name, object value) {
            PythonTypeSlot slot;
            if (TryResolveSlot(context, name, out slot)) {
                if (slot.TrySetValue(context, instance, this, value)) {
                    return true;
                }
            }

            // set the attribute on the instance
            IPythonObject sdo = instance as IPythonObject;
            if (sdo != null) {
                PythonDictionary iac = sdo.Dict;
                if (iac == null && sdo.PythonType.HasDictionary) {
                    iac = MakeDictionary();

                    if ((iac = sdo.SetDict(iac)) == null) {
                        return false;
                    }
                }

                iac[name] = value;
                return true;
            }

            return false;
        }

        internal bool TryDeleteMember(CodeContext context, object instance, string name) {
            try {
                object delattr;
                if (TryResolveNonObjectSlot(context, instance, "__delattr__", out delattr)) {
                    InvokeGetAttributeMethod(context, name, delattr);
                    return true;
                }
            } catch (MissingMemberException) {
                //!!! when do we let the user see this exception?
            }

            return TryDeleteNonCustomMember(context, instance, name);
        }

        internal bool TryDeleteNonCustomMember(CodeContext context, object instance, string name) {
            PythonTypeSlot slot;
            if (TryResolveSlot(context, name, out slot)) {
                if (slot.TryDeleteValue(context, instance, this)) {
                    return true;
                }
            }

            // set the attribute on the instance
            IPythonObject sdo = instance as IPythonObject;
            if (sdo != null) {
                PythonDictionary dict = sdo.Dict;
                if (dict == null && sdo.PythonType.HasDictionary) {
                    dict = MakeDictionary();

                    if ((dict = sdo.SetDict(dict)) == null) {
                        return false;
                    }
                }

                return dict.Remove(name);
            }

            return false;
        }

        #endregion

        #region Member lists

        /// <summary>
        /// Returns a list of all slot names for the type and any subtypes.
        /// </summary>
        /// <param name="context">The context that is doing the inquiry of InvariantContext.Instance.</param>
        internal List GetMemberNames(CodeContext context) {
            return GetMemberNames(context, null);
        }

        /// <summary>
        /// Returns a list of all slot names for the type, any subtypes, and the instance.
        /// </summary>
        /// <param name="context">The context that is doing the inquiry of InvariantContext.Instance.</param>
        /// <param name="self">the instance to get instance members from, or null.</param>
        internal List GetMemberNames(CodeContext context, object self) {
            List res = TryGetCustomDir(context, self);
            if (res != null) {
                return res;
            }

            Dictionary<string, string> keys = new Dictionary<string, string>();
            res = new List();

            for (int i = 0; i < _resolutionOrder.Count; i++) {
                PythonType dt = _resolutionOrder[i];

                if (dt.IsSystemType) {
                    PythonBinder.GetBinder(context).ResolveMemberNames(context, dt, this, keys);
                } else {
                    AddUserTypeMembers(context, keys, dt, res);
                }
            }

            var extMethods = context.ModuleContext.ExtensionMethods;
            if (extMethods != ExtensionMethodSet.Empty) {
                // add any available extension methods.
                foreach (var method in extMethods.GetExtensionMethods(this)) {
                    keys[method.Name] = method.Name;
                }
            }

            return AddInstanceMembers(self, keys, res);
        }

        private List TryGetCustomDir(CodeContext context, object self) {
            if (self != null) {
                object dir;
                if (TryResolveNonObjectSlot(context, self, "__dir__", out dir)) {
                    CallSite<Func<CallSite, CodeContext, object, object>> dirSite;
                    if (IsSystemType) {
                        dirSite = PythonContext.GetContext(context).GetSiteCacheForSystemType(UnderlyingSystemType).GetDirSite(context);
                    } else {
                        dirSite = _siteCache.GetDirSite(context);
                    }

                    return new List(dirSite.Target(dirSite, context, dir));
                }
            }

            return null;
        }

        /// <summary>
        /// Adds members from a user defined type.
        /// </summary>
        private static void AddUserTypeMembers(CodeContext context, Dictionary<string, string> keys, PythonType dt, List res) {
            if (dt.OldClass != null) {
                foreach (KeyValuePair<object, object> kvp in dt.OldClass._dict) {
                    AddOneMember(keys, res, kvp.Key);
                }
            } else {
                foreach (KeyValuePair<string, PythonTypeSlot> kvp in dt._dict) {
                    if (keys.ContainsKey(kvp.Key)) continue;

                    keys[kvp.Key] = kvp.Key;
                }
            }
        }

        private static void AddOneMember(Dictionary<string, string> keys, List res, object name) {
            string strKey = name as string;
            if (strKey != null) {
                keys[strKey] = strKey;
            } else {
                res.Add(name);
            }
        }

        /// <summary>
        /// Adds members from a user defined type instance
        /// </summary>
        private static List AddInstanceMembers(object self, Dictionary<string, string> keys, List res) {
            IPythonObject dyno = self as IPythonObject;
            if (dyno != null) {
                PythonDictionary dict = dyno.Dict;
                if (dict != null) {
                    lock (dict) {
                        foreach (object name in dict.Keys) {
                            AddOneMember(keys, res, name);
                        }
                    }
                }
            }

            List<string> strKeys = new List<string>(keys.Keys);
            strKeys.Sort();
            res.extend(strKeys);

            return res;
        }

        internal PythonDictionary GetMemberDictionary(CodeContext context) {
            return GetMemberDictionary(context, true);
        }

        internal PythonDictionary GetMemberDictionary(CodeContext context, bool excludeDict) {
            PythonDictionary dict = PythonDictionary.MakeSymbolDictionary();
            if (IsSystemType) {
                PythonBinder.GetBinder(context).LookupMembers(context, this, dict);
            } else {
                foreach (var item in _dict) {
                    if (excludeDict && item.Key == "__dict__") {
                        continue;
                    }

                    PythonTypeUserDescriptorSlot dts = item.Value as PythonTypeUserDescriptorSlot;
                    object val = dts == null ? item.Value : dts.Value;
                    dict[item.Key] = val;
                }
            }
            return dict;
        }

        #endregion

        #region User type initialization

        private void InitializeUserType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary vars, string selfNames) {
            // we don't support overriding __mro__
            if (vars.ContainsKey("__mro__"))
                throw new NotImplementedException("Overriding __mro__ of built-in types is not implemented");

            // cannot override mro when inheriting from type
            if (vars.ContainsKey("mro")) {
                foreach (object o in bases) {
                    PythonType dt = o as PythonType;
                    if (dt != null && dt.IsSubclassOf(TypeCache.PythonType)) {
                        throw new NotImplementedException("Overriding type.mro is not implemented");
                    }
                }
            }

            bases = ValidateBases(bases);

            _name = name;
            _bases = GetBasesAsList(bases).ToArray();
            _pythonContext = PythonContext.GetContext(context);
            _resolutionOrder = CalculateMro(this, _bases);

            bool hasSlots = false;
            foreach (PythonType pt in _bases) {
                // if we directly inherit from 2 types with slots then the indexes would
                // conflict so inheritance isn't allowed.
                int slotCount = pt.GetUsedSlotCount();
                
                if (slotCount != 0) {
                    if (hasSlots) {
                        throw PythonOps.TypeError("multiple bases have instance lay-out conflict");
                    }
                    hasSlots = true;
                }
                
                pt.AddSubType(this);
            }

            HashSet<string> optimizedInstanceNames = null;
            foreach (PythonType pt in _resolutionOrder) {
                // we need to calculate the number of slots from resolution
                // order to deal with multiple bases having __slots__ that
                // directly inherit from each other.
                _originalSlotCount += pt.GetUsedSlotCount();
                if (pt._optimizedInstanceNames != null) {
                    if (optimizedInstanceNames == null) {
                        optimizedInstanceNames = new HashSet<string>();
                    }

                    optimizedInstanceNames.UnionWith(pt._optimizedInstanceNames);
                }
            }
            if (!String.IsNullOrEmpty(selfNames)) {
                if (optimizedInstanceNames == null) {
                    optimizedInstanceNames = new HashSet<string>();
                }

                optimizedInstanceNames.UnionWith(selfNames.Split(','));
            }

            if (optimizedInstanceNames != null) {
                _optimizedInstanceVersion = CustomInstanceDictionaryStorage.AllocateVersion();
                _optimizedInstanceNames = new List<string>(optimizedInstanceNames).ToArray();
            }


            EnsureDict();

            PopulateDictionary(context, name, bases, vars);

#if FEATURE_REFEMIT
            // calculate the .NET type once so it can be used for things like super calls
            _underlyingSystemType = NewTypeMaker.GetNewType(name, bases);

            // then let the user intercept and rewrite the type - the user can't create
            // instances of this type yet.
            _underlyingSystemType = __clrtype__();
            if (_underlyingSystemType == null) {
                throw PythonOps.ValueError("__clrtype__ must return a type, not None");
            }
#else
            _underlyingSystemType = typeof(IronPython.NewTypes.System.Object_1_1);
#endif

            // finally assign the ctors from the real type the user provided

            lock (_userTypeCtors) {
                if (!_userTypeCtors.TryGetValue(_underlyingSystemType, out _ctor)) {
                    ConstructorInfo[] ctors = _underlyingSystemType.GetConstructors();

                    bool isPythonType = false;
                    foreach (ConstructorInfo ci in ctors) {
                        ParameterInfo[] pis = ci.GetParameters();
                        if((pis.Length > 1 && pis[0].ParameterType == typeof(CodeContext) && pis[1].ParameterType == typeof(PythonType)) ||
                            (pis.Length > 0 && pis[0].ParameterType == typeof(PythonType))) {
                            isPythonType = true;
                            break;
                        }
                    }

                    _ctor = BuiltinFunction.MakeFunction(Name, ctors, _underlyingSystemType);

                    if (isPythonType) {
                        _userTypeCtors[_underlyingSystemType] = _ctor;
                    } else {
                        // __clrtype__ returned a type w/o any PythonType parameters, force this to
                        // be created like a normal .NET type.  Presumably the user is planning on storing
                        // the Python type in a static field or something and passing the Type object to
                        // some .NET API which wants to Activator.CreateInstance on it w/o providing a 
                        // PythonType object.
                        _instanceCtor = new SystemInstanceCreator(this);
                        _attrs |= PythonTypeAttributes.SystemCtor;
                    }
                }
            }

            UpdateObjectNewAndInit(context);
        }

        internal PythonDictionary MakeDictionary() {
            if (_optimizedInstanceNames != null) {
                return new PythonDictionary(new CustomInstanceDictionaryStorage(_optimizedInstanceNames, _optimizedInstanceVersion));
            }

            return PythonDictionary.MakeSymbolDictionary();
        }

        internal IList<string> GetOptimizedInstanceNames() {
            return _optimizedInstanceNames;
        }

        internal int GetOptimizedInstanceVersion() {
            return _optimizedInstanceVersion;
        }

        internal IList<string> GetTypeSlots() {
            PythonTypeSlot pts;
            if(_dict != null && _dict.TryGetValue("__slots__", out pts) && pts is PythonTypeUserDescriptorSlot) {
                return SlotsToList(((PythonTypeUserDescriptorSlot)pts).Value);
            }
            return ArrayUtils.EmptyStrings; 
        }

        internal static List<string> GetSlots(PythonDictionary dict) {
            List<string> res = null;
            object slots;
            if (dict != null && dict.TryGetValue("__slots__", out slots)) {
                res = SlotsToList(slots);
            }

            return res;
        }

        internal static List<string> SlotsToList(object slots) {
            List<string> res = new List<string>();
            IList<object> seq = slots as IList<object>;
            if (seq != null) {
                res = new List<string>(seq.Count);
                for (int i = 0; i < seq.Count; i++) {
                    res.Add(GetSlotName(seq[i]));
                }

                res.Sort();
            } else {
                res = new List<string>(1);
                res.Add(GetSlotName(slots));
            }
            return res;
        }

        internal bool HasObjectNew(CodeContext context) {
            if (!_objectNew.HasValue) {
                UpdateObjectNewAndInit(context);
            }

            Debug.Assert(_objectNew.HasValue);
            return _objectNew.Value;
        }

        internal bool HasObjectInit(CodeContext context) {
            if (!_objectInit.HasValue) {
                UpdateObjectNewAndInit(context);
            }

            Debug.Assert(_objectInit.HasValue);
            return _objectInit.Value;
        }

        private void UpdateObjectNewAndInit(CodeContext context) {
            PythonTypeSlot slot;
            object funcObj;

            foreach (PythonType pt in _bases) {
                if (pt == TypeCache.Object) {
                    continue;
                }

                if (pt._objectNew == null || pt._objectInit == null) {
                    pt.UpdateObjectNewAndInit(context);
                }

                Debug.Assert(pt._objectInit != null && pt._objectNew != null);

                if (!pt._objectNew.Value) {
                    _objectNew = false;                    
                }

                if (!pt._objectInit.Value) {
                    _objectInit = false;
                }
            }

            if (_objectInit == null) {
                _objectInit = TryResolveSlot(context, "__init__", out slot) && slot.TryGetValue(context, null, this, out funcObj) && funcObj == InstanceOps.Init;
            }

            if (_objectNew == null) {
                _objectNew = TryResolveSlot(context, "__new__", out slot) && slot.TryGetValue(context, null, this, out funcObj) && funcObj == InstanceOps.New;
            }
        }

        private static string GetSlotName(object o) {
            string value;
            if (!Converter.TryConvertToString(o, out value) || String.IsNullOrEmpty(value))
                throw PythonOps.TypeError("slots must be one string or a list of strings");

            for (int i = 0; i < value.Length; i++) {
                if ((value[i] >= 'a' && value[i] <= 'z') ||
                    (value[i] >= 'A' && value[i] <= 'Z') ||
                    (i != 0 && value[i] >= '0' && value[i] <= '9') ||
                    value[i] == '_') {
                    continue;
                }
                throw PythonOps.TypeError("__slots__ must be valid identifiers");
            }

            return value;
        }

        private int GetUsedSlotCount() {
            int slotCount = 0;
            if (_slots != null) {
                slotCount = _slots.Length;

                if (Array.IndexOf(_slots, "__weakref__") != -1) {
                    slotCount--;
                }

                if (Array.IndexOf(_slots, "__dict__") != -1) {
                    slotCount--;
                }
            }
            return slotCount;
        }

        private void PopulateDictionary(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary vars) {
            PopulateSlot("__doc__", null);

            List<string> slots = GetSlots(vars);
            if (slots != null) {
                _slots = slots.ToArray();
                
                int index = _originalSlotCount;

                string typeName = IronPython.Compiler.Parser.GetPrivatePrefix(name);
                for (int i = 0; i < slots.Count; i++) {
                    string slotName = slots[i];
                    if (slotName.StartsWith("__") && !slotName.EndsWith("__")) {
                        slotName = "_" + typeName + slotName;
                    }

                    AddSlot(slotName, new ReflectedSlotProperty(slotName, name, i + index));
                }

                _originalSlotCount += slots.Count;
            }

            // check the slots to see if we're weak refable
            if (CheckForSlotWithDefault(context, bases, slots, "__weakref__")) {
                _attrs |= PythonTypeAttributes.WeakReferencable;
                AddSlot("__weakref__", new PythonTypeWeakRefSlot(this));
            }

            if (CheckForSlotWithDefault(context, bases, slots, "__dict__")) {
                _attrs |= PythonTypeAttributes.HasDictionary;
                PythonTypeSlot pts;
                bool inheritsDict = false;
                for(int i = 1; i<_resolutionOrder.Count; i++) {
                    PythonType pt = _resolutionOrder[i];
                    if (pt.TryResolveSlot(context, "__dict__", out pts)) {
                        inheritsDict = true;
                        break;
                    }
                }

                if (!inheritsDict) {
                    AddSlot("__dict__", new PythonTypeDictSlot(this));
                }
            }

            object modName;
            if (context.TryGetVariable("__name__", out modName)) {
                PopulateSlot("__module__", modName);
            }

            foreach (var kvp in vars) {
                if (kvp.Key is string) {
                    PopulateSlot((string)kvp.Key, kvp.Value);
                }
            }

            PythonTypeSlot val;
            if (_dict.TryGetValue("__new__", out val) && val is PythonFunction) {
                AddSlot("__new__", new staticmethod(val));
            }
        }

        private static bool CheckForSlotWithDefault(CodeContext context, PythonTuple bases, List<string> slots, string name) {
            bool hasSlot = true;
            if (slots != null && !slots.Contains(name)) {
                hasSlot = false;
                foreach (object pt in bases) {
                    PythonType dt = pt as PythonType;
                    PythonTypeSlot dummy;
                    if (dt != null && dt.TryLookupSlot(context, name, out dummy)) {
                        hasSlot = true;
                    }
                }
            } else if (slots != null) {
                // check and see if we have 2
                if(bases.Count > 0) {
                    PythonType dt = bases[0] as PythonType;
                    PythonTypeSlot dummy;
                    if (dt != null && dt.TryLookupSlot(context, name, out dummy)) {
                        throw PythonOps.TypeError(name + " slot disallowed: we already got one");
                    }
                }
            }
            return hasSlot;
        }

        /// <summary>
        /// Gets the .NET type which is used for instances of the Python type.
        /// 
        /// When overridden by a metaclass enables a customization of the .NET type which
        /// is used for instances of the Python type.  Meta-classes can construct custom
        /// types at runtime which include new .NET methods, fields, custom attributes or
        /// other features to better interoperate with .NET.
        /// </summary>
        [PythonHidden]
        public virtual Type __clrtype__() {
            return _underlyingSystemType;
        }

        private void PopulateSlot(string key, object value) {
            AddSlot(key, ToTypeSlot(value));
        }

        private static List<PythonType> GetBasesAsList(PythonTuple bases) {
            List<PythonType> newbs = new List<PythonType>();
            foreach (object typeObj in bases) {
                PythonType dt = typeObj as PythonType;
                if (dt == null) {
                    dt = ((OldClass)typeObj).TypeObject;
                }

                newbs.Add(dt);
            }

            return newbs;
        }

        private static PythonTuple ValidateBases(PythonTuple bases) {
            PythonTuple newBases = PythonTypeOps.EnsureBaseType(bases);
            for (int i = 0; i < newBases.__len__(); i++) {

#if !FEATURE_REFEMIT
                PythonType pt = newBases[i] as PythonType;
                if (pt != null && pt.IsSystemType && pt != TypeCache.Object) {
                    // The only supported CLR base class w/o refemit is object
                    throw new NotSupportedException(string.Format("{0} is not a valid CLR base class. Only object is supported w/o refemit.", pt.UnderlyingSystemType.FullName));
                }
#endif

                for (int j = 0; j < newBases.__len__(); j++) {
                    if (i != j && newBases[i] == newBases[j]) {
                        OldClass oc = newBases[i] as OldClass;
                        if (oc != null) {
                            throw PythonOps.TypeError("duplicate base class {0}", oc.Name);
                        } else {
                            throw PythonOps.TypeError("duplicate base class {0}", ((PythonType)newBases[i]).Name);
                        }
                    }
                }
            }
            return newBases;
        }

        private static void EnsureModule(CodeContext context, PythonDictionary dict) {
            if (!dict.ContainsKey("__module__")) {
                object modName;
                if (context.TryGetVariable("__name__", out modName)) {
                    dict["__module__"] = modName;
                }
            }
        }
        
        #endregion

        #region System type initialization

        /// <summary>
        /// Initializes a PythonType that represents a standard .NET type.  The same .NET type
        /// can be shared with the Python type system.  For example object, string, int,
        /// etc... are all the same types.  
        /// </summary>
        private void InitializeSystemType() {
            IsSystemType = true;
            IsPythonType = PythonBinder.IsPythonType(_underlyingSystemType);
            _name = NameConverter.GetTypeName(_underlyingSystemType);
            AddSystemBases();
        }

        private void AddSystemBases() {
            List<PythonType> mro = new List<PythonType>();
            mro.Add(this);

            if (_underlyingSystemType.GetBaseType() != null) {
                Type baseType;
                if (_underlyingSystemType == typeof(bool)) {
                    // bool inherits from int in python
                    baseType = typeof(int);
                } else if (_underlyingSystemType.GetBaseType() == typeof(ValueType)) {
                    // hide ValueType, it doesn't exist in Python
                    baseType = typeof(object);
                } else {
                    baseType = _underlyingSystemType.GetBaseType();
                }

                while (baseType.GetTypeInfo().IsDefined(typeof(PythonHiddenBaseClassAttribute), false)) {
                    baseType = baseType.GetBaseType();
                }

                _bases = new PythonType[] { GetPythonType(baseType) };

                Type curType = baseType;
                while (curType != null) {
                    Type newType;
                    if (TryReplaceExtensibleWithBase(curType, out newType)) {
                        mro.Add(DynamicHelpers.GetPythonTypeFromType(newType));
                    } else if(!curType.GetTypeInfo().IsDefined(typeof(PythonHiddenBaseClassAttribute), false)) {
                        mro.Add(DynamicHelpers.GetPythonTypeFromType(curType));
                    }
                    curType = curType.GetBaseType();
                }

                if (!IsPythonType) {
                    AddSystemInterfaces(mro);
                }
            } else if (_underlyingSystemType.IsInterface()) {
                // add interfaces to MRO & create bases list
                Type[] interfaces = _underlyingSystemType.GetInterfaces();
                PythonType[] bases = new PythonType[interfaces.Length];

                for (int i = 0; i < interfaces.Length; i++) {
                    Type iface = interfaces[i];
                    PythonType it = DynamicHelpers.GetPythonTypeFromType(iface);

                    mro.Add(it);
                    bases[i] = it;
                }
                _bases = bases;
            } else {
                _bases = new PythonType[0];
            }

            _resolutionOrder = mro;
        }

        private void AddSystemInterfaces(List<PythonType> mro) {
            if (_underlyingSystemType.IsArray) {
                // include the standard array interfaces in the array MRO.  We pick the
                // non-strongly typed versions which are also in Array.__mro__
                mro.Add(DynamicHelpers.GetPythonTypeFromType(typeof(IList)));
                mro.Add(DynamicHelpers.GetPythonTypeFromType(typeof(ICollection)));
                mro.Add(DynamicHelpers.GetPythonTypeFromType(typeof(IEnumerable)));
                return;
            } 

            Type[] interfaces = _underlyingSystemType.GetInterfaces();
            Dictionary<string, Type> methodMap = new Dictionary<string, Type>();
            bool hasExplicitIface = false;
            List<Type> nonCollidingInterfaces = new List<Type>(interfaces);
            
            foreach (Type iface in interfaces) {
                InterfaceMapping mapping = _underlyingSystemType.GetInterfaceMap(iface);
                
                // grab all the interface methods which would hide other members
                for (int i = 0; i < mapping.TargetMethods.Length; i++) {
                    MethodInfo target = mapping.TargetMethods[i];
                    
                    if (target == null) {
                        continue;
                    }

                    if (!target.IsPrivate) {
                        methodMap[target.Name] = null;
                    } else {
                        hasExplicitIface = true;
                    }
                }

                if (hasExplicitIface) {
                    for (int i = 0; i < mapping.TargetMethods.Length; i++) {
                        MethodInfo target = mapping.TargetMethods[i];
                        MethodInfo iTarget = mapping.InterfaceMethods[i];

                        // any methods which aren't explicit are picked up at the appropriate
                        // time earlier in the MRO so they can be ignored
                        if (target != null && target.IsPrivate) {
                            hasExplicitIface = true;

                            Type existing;
                            if (methodMap.TryGetValue(iTarget.Name, out existing)) {
                                if (existing != null) {
                                    // collision, multiple interfaces implement the same name, and
                                    // we're not hidden by another method.  remove both interfaces, 
                                    // but leave so future interfaces get removed
                                    nonCollidingInterfaces.Remove(iface);
                                    nonCollidingInterfaces.Remove(methodMap[iTarget.Name]);
                                    break;
                                }
                            } else {
                                // no collisions so far...
                                methodMap[iTarget.Name] = iface;
                            }
                        } 
                    }
                }
            }

            if (hasExplicitIface) {
                // add any non-colliding interfaces into the MRO
                foreach (Type t in nonCollidingInterfaces) {
                    Debug.Assert(t.IsInterface());

                    mro.Add(DynamicHelpers.GetPythonTypeFromType(t));
                }
            }
        }

        /// <summary>
        /// Creates a __new__ method for the type.  If the type defines interesting constructors
        /// then the __new__ method will call that.  Otherwise if it has only a single argless
        /// </summary>
        private void AddSystemConstructors() {
            if (typeof(Delegate).IsAssignableFrom(_underlyingSystemType)) {
                SetConstructor(
                    BuiltinFunction.MakeFunction(
                        _underlyingSystemType.Name,
                        new[] { typeof(DelegateOps).GetMethod("__new__") },
                        _underlyingSystemType
                    )
                );
            } else if (!_underlyingSystemType.IsAbstract()) {
                BuiltinFunction reflectedCtors = GetConstructors();
                if (reflectedCtors == null) {
                    return; // no ctors, no __new__
                }

                SetConstructor(reflectedCtors);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        private BuiltinFunction GetConstructors() {
            Type type = _underlyingSystemType;
            string name = Name;

            return PythonTypeOps.GetConstructorFunction(type, name);
        }

        private void EnsureConstructor() {
            if (_ctor == null) {
                AddSystemConstructors();
                if (_ctor == null) {
                    throw PythonOps.TypeError(_underlyingSystemType.FullName + " does not define any public constructors.");
                }
            }
        }

        private void EnsureInstanceCtor() {
            if (_instanceCtor == null) {
                _instanceCtor = InstanceCreator.Make(this);
            }
        }

        #endregion

        #region Private implementation details

        private void UpdateVersion() {
            foreach (WeakReference wr in SubTypes) {
                if (wr.IsAlive) {
                    ((PythonType)wr.Target).UpdateVersion();
                }
            }

            _lenSlot = null;
            _version = GetNextVersion();
        }

        /// <summary>
        /// This will return a unique integer for every version of every type in the system.
        /// This means that DynamicSite code can generate a check to see if it has the correct
        /// PythonType and version with a single integer compare.
        /// 
        /// TODO - This method and related code should fail gracefully on overflow.
        /// </summary>
        private static int GetNextVersion() {
            if (MasterVersion < 0) {
                throw new InvalidOperationException(IronPython.Resources.TooManyVersions);
            }
            return Interlocked.Increment(ref MasterVersion);
        }

        private void EnsureDict() {
            if (_dict == null) {
                Interlocked.CompareExchange<Dictionary<string, PythonTypeSlot>>(
                    ref _dict,
                    new Dictionary<string, PythonTypeSlot>(StringComparer.Ordinal),
                    null);
            }
        }
      
        /// <summary>
        /// Internal helper function to add a subtype
        /// </summary>
        private void AddSubType(PythonType subtype) {
            if (_subtypes == null) {
                Interlocked.CompareExchange<List<WeakReference>>(ref _subtypes, new List<WeakReference>(), null);
            }

            lock (_subtypesLock) {
                _subtypes.Add(new WeakReference(subtype));
            }
        }

        private void RemoveSubType(PythonType subtype) {
            int i = 0;
            if (_subtypes != null) {
                lock (_subtypesLock) {
                    while (i < _subtypes.Count) {
                        if (!_subtypes[i].IsAlive || _subtypes[i].Target == subtype) {
                            _subtypes.RemoveAt(i);
                            continue;
                        }
                        i++;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a list of weak references to all the subtypes of this class.  May return null
        /// if there are no subtypes of the class.
        /// </summary>
        private IList<WeakReference> SubTypes {
            get {
                if (_subtypes == null) return _emptyWeakRef;

                lock (_subtypesLock) {
                    return _subtypes.ToArray();
                }
            }
        }

        [Flags]
        private enum PythonTypeAttributes {
            None = 0x00,
            Immutable = 0x01,
            SystemType = 0x02,
            IsPythonType = 0x04,
            WeakReferencable = 0x08,
            HasDictionary = 0x10,

            /// <summary>
            /// The type has a ctor which does not accept PythonTypes.  This is used
            /// for user defined types which implement __clrtype__
            /// </summary>
            SystemCtor    = 0x20
        }

        #endregion

        #region IMembersList Members

        IList<string> IMembersList.GetMemberNames() {
            return PythonOps.GetStringMemberList(this);
        }

        IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
            IList<object> res = GetMemberNames(context);

            object[] arr = new object[res.Count];
            res.CopyTo(arr, 0);

            Array.Sort(arr);
            return arr;
        }

        #endregion        

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _weakrefTracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            if (!IsSystemType) {
                return Interlocked.CompareExchange<WeakRefTracker>(ref _weakrefTracker, value, null) == null;
            } else {
                return false;
            }
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            if (!IsSystemType) {
                _weakrefTracker = value;
            } else {
                // do nothing, system types are never collected
            }
        }

        IWeakReferenceable IWeakReferenceableByProxy.GetWeakRefProxy(PythonContext context) {
            return new WeakReferenceProxy(context, this);
        }

        class WeakReferenceProxy : IWeakReferenceable {
            private readonly PythonContext context;
            private readonly PythonType type;

            internal WeakReferenceProxy(PythonContext context, PythonType type) {
                this.type = type;
                this.context = context;
            }

            public WeakRefTracker GetWeakRef() {
                if(type.IsSystemType) {
                    return this.context.GetSystemPythonTypeWeakRef(type);
                } else {
                    IWeakReferenceable weakref = (IWeakReferenceable)type;
                    return weakref.GetWeakRef();
                }
            }

            public void SetFinalizer(WeakRefTracker value) {
                if(type.IsSystemType) {
                    this.context.SetSystemPythonTypeFinalizer(type, value);
                } else {
                    IWeakReferenceable weakref = (IWeakReferenceable)type;
                    weakref.SetFinalizer(value);
                }
            }

            public bool SetWeakRef(WeakRefTracker value) {
                if(type.IsSystemType) {
                    return this.context.SetSystemPythonTypeWeakRef(type, value);
                } else {
                    IWeakReferenceable weakref = (IWeakReferenceable)type;
                    return weakref.SetWeakRef(value);
                }
            }
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        [PythonHidden]
        public DynamicMetaObject/*!*/ GetMetaObject(Expression/*!*/ parameter) {
            return new Binding.MetaPythonType(parameter, BindingRestrictions.Empty, this);
        }

        #endregion

        /// <summary>
        /// Returns a CLR WeakReference object to this PythonType that can be shared
        /// between anyone who needs a weak reference to the type.
        /// </summary>
        internal WeakReference/*!*/ GetSharedWeakReference() {
            if (_weakRef == null) {
                _weakRef = new WeakReference(this);                
            }

            return _weakRef;
        }

        #region IFastSettable Members

        T IFastSettable.MakeSetBinding<T>(CallSite<T> site, PythonSetMemberBinder binder) {
            PythonTypeSlot pts;
            // if our meta class has a custom __setattr__ then we don't handle it in the
            // fast path.  Usually this is handled by a user defined type (UserTypeOps) IDynamicMetaObjectProvider
            // class.  But w/ _ctypes support we can have a built-in meta class which doesn't
            // get this treatment.
            if (!IsSystemType && !TryGetCustomSetAttr(Context.SharedContext, out pts)) {
                CodeContext context = PythonContext.GetPythonContext(binder).SharedContext;
                string name = binder.Name;
                
                // optimized versions for possible literals that can show up in code.
                Type setType = typeof(T);
                if (setType == typeof(Func<CallSite, object, object, object>)) {
                    return (T)(object)MakeFastSet<object>(context, name);
                } else if (setType == typeof(Func<CallSite, object, string, object>)) {
                    return (T)(object)MakeFastSet<string>(context, name);
                } else if (setType == typeof(Func<CallSite, object, int, object>)) {
                    return (T)(object)MakeFastSet<int>(context, name);
                } else if (setType == typeof(Func<CallSite, object, double, object>)) {
                    return (T)(object)MakeFastSet<double>(context, name);
                } else if (setType == typeof(Func<CallSite, object, List, object>)) {
                    return (T)(object)MakeFastSet<List>(context, name);
                } else if (setType == typeof(Func<CallSite, object, PythonTuple, object>)) {
                    return (T)(object)MakeFastSet<PythonTuple>(context, name);
                } else if (setType == typeof(Func<CallSite, object, PythonDictionary, object>)) {
                    return (T)(object)MakeFastSet<PythonDictionary>(context, name);
                } else if (setType == typeof(Func<CallSite, object, SetCollection, object>)) {
                    return (T)(object)MakeFastSet<SetCollection>(context, name);
                } else if (setType == typeof(Func<CallSite, object, FrozenSetCollection, object>)) {
                    return (T)(object)MakeFastSet<FrozenSetCollection>(context, name);
                }
            }

            return null;
        }

        private static Func<CallSite, object, T, object> MakeFastSet<T>(CodeContext/*!*/ context, string name) {
            return new Setter<T>(context, name).Target;
        }

        class Setter<T> : FastSetBase<T> {
            private readonly CodeContext/*!*/ _context;
            private readonly string _name;

            public Setter(CodeContext/*!*/ context, string name)
                : base(-1) {
                _context = context;
                _name = name;
            }

            public object Target(CallSite site, object self, T value) {
                PythonType type = self as PythonType;
                if (type != null && !type.IsSystemType) {
                    type.SetCustomMember(_context, _name, value);
                    return value;
                }

                return Update(site, self, value);
            }
        }

        #endregion

        internal class DebugProxy {
            private readonly PythonType _type;

            public DebugProxy(PythonType type) {
                _type = type;
            }
            
            public PythonType[] __bases__ {
                get {
                    return ArrayUtils.ToArray(_type.BaseTypes);
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public List<ObjectDebugView> Members {
                get {
                    var res = new List<ObjectDebugView>();
                    if (_type._dict != null) {
                        foreach (var v in _type._dict) {
                            if (v.Value is PythonTypeUserDescriptorSlot) {
                                res.Add(new ObjectDebugView(v.Key, ((PythonTypeUserDescriptorSlot)v.Value).Value));
                            } else {
                                res.Add(new ObjectDebugView(v.Key, v.Value));
                            }
                        }
                    }
                    return res;
                }
            }
        }
    }

    enum OptimizedGetKind {
        None,
        SlotDict,
        SlotOnly,
        PropertySlot,
        UserSlotDict,
        UserSlotOnly,
    }

    class UserGetBase : FastGetBase {
        internal readonly int _version;

        public UserGetBase(PythonGetMemberBinder binder, int version) {
            _version = version;
        }

        public override bool IsValid(PythonType type) {
            return _version == type.Version;
        }
    }

    class ChainedUserGet : UserGetBase {
        public ChainedUserGet(PythonGetMemberBinder binder, int version, Func<CallSite, object, CodeContext, object> func)
            : base(binder, version) {
            _func = func;
        }

        internal override bool ShouldCache {
            get {
                return false;
            }
        }
    }

    class GetAttributeDelegates : UserGetBase {
        private readonly string _name;
        private readonly PythonTypeSlot _getAttributeSlot;
        private readonly PythonTypeSlot _getAttrSlot;
        private readonly SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, string, object>>>/*!*/ _storage;
        private readonly bool _isNoThrow;

        public GetAttributeDelegates(PythonGetMemberBinder/*!*/ binder, string/*!*/ name, int version, PythonTypeSlot/*!*/ getAttributeSlot, PythonTypeSlot/*!*/ getAttrSlot)
            : base(binder, version) {
            Assert.NotNull(binder, getAttributeSlot);

            _storage = new SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, string, object>>>();
            _getAttributeSlot = getAttributeSlot;
            _getAttrSlot = getAttrSlot;
            _name = name;
            _func = GetAttribute;
            _isNoThrow = binder.IsNoThrow;
        }

        public object GetAttribute(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version) {
                if (_isNoThrow) {
                    return UserTypeOps.GetAttributeNoThrow(context, self, _name, _getAttributeSlot, _getAttrSlot, _storage);
                }

                return UserTypeOps.GetAttribute(context, self, _name, _getAttributeSlot, _getAttrSlot, _storage);
            }
            return Update(site, self, context);
        }
    }

    class GetMemberDelegates : UserGetBase {
        private readonly string _name;
        private readonly bool _isNoThrow;
        private readonly PythonTypeSlot _slot, _getattrSlot;
        private readonly SlotGetValue _slotFunc;
        private readonly Func<CallSite, object, CodeContext, object> _fallback;
        private readonly int _dictVersion, _dictIndex;
        private readonly ExtensionMethodSet _extMethods;

        public GetMemberDelegates(OptimizedGetKind getKind, PythonType type, PythonGetMemberBinder binder, string name, int version, PythonTypeSlot slot, PythonTypeSlot getattrSlot, SlotGetValue slotFunc, Func<CallSite, object, CodeContext, object> fallback, ExtensionMethodSet extMethods)
            : base(binder, version) {
            _slot = slot;
            _name = name;
            _getattrSlot = getattrSlot;
            _slotFunc = slotFunc;
            _fallback = fallback;
            _isNoThrow = binder.IsNoThrow;
            _extMethods = extMethods;
            var optNames = type.GetOptimizedInstanceNames(); 
            
            switch (getKind) {
                case OptimizedGetKind.SlotDict:
                    if (optNames != null) {
                        _dictIndex = optNames.IndexOf(name);
                    }

                    if (optNames != null && _dictIndex != -1) {
                        _func = SlotDictOptimized;
                        _dictVersion = type.GetOptimizedInstanceVersion();                        
                    } else {
                        _func = SlotDict;
                    }
                    break;
                case OptimizedGetKind.SlotOnly: _func = SlotOnly; break;
                case OptimizedGetKind.PropertySlot: _func = UserSlot; break;
                case OptimizedGetKind.UserSlotDict:
                    if (optNames != null) {
                        _dictIndex = optNames.IndexOf(name);
                    }

                    if (optNames != null && _dictIndex != -1) {
                        _dictVersion = type.GetOptimizedInstanceVersion();
                        if (_getattrSlot != null) {
                            _func = UserSlotDictGetAttrOptimized;
                        } else {
                            _func = UserSlotDictOptimized;
                        }
                    } else if (_getattrSlot != null) {
                        _func = UserSlotDictGetAttr;
                    } else {
                        _func = UserSlotDict;
                    } 
                    break;
                case OptimizedGetKind.UserSlotOnly:
                    if (_getattrSlot != null) {
                        _func = UserSlotOnlyGetAttr;
                    } else {
                        _func = UserSlotOnly; 
                    }
                    break;
                default: throw new InvalidOperationException();
            }
        }

        public object SlotDict(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                _hitCount++;

                object res;
                if (ipo.Dict != null && ipo.Dict.TryGetValue(_name, out res)) {
                    return res;
                }

                if (_slot != null && _slot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return res;
                }

                if (_getattrSlot != null && _getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }

        public object SlotDictOptimized(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                _hitCount++;

                object res;
                PythonDictionary dict = ipo.Dict;

                if (UserTypeOps.TryGetDictionaryValue(dict, _name, _dictVersion, _dictIndex, out res)) {
                    return res;
                }

                if (_slot != null && _slot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return res;
                }

                if (_getattrSlot != null && _getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }


        public object SlotOnly(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                _hitCount++;

                object res;
                if (_slot != null && _slot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return res;
                }

                if (_getattrSlot != null && _getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }

        public object UserSlotDict(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                object res;
                if (ipo.Dict != null && ipo.Dict.TryGetValue(_name, out res)) {
                    return res;
                }

                return ((PythonTypeUserDescriptorSlot)_slot).GetValue(context, self, ipo.PythonType);
            }

            return Update(site, self, context);
        }

        public object UserSlotDictOptimized(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                object res;
                PythonDictionary dict = ipo.Dict;

                if (UserTypeOps.TryGetDictionaryValue(dict, _name, _dictVersion, _dictIndex, out res)) {
                    return res;
                }

                return ((PythonTypeUserDescriptorSlot)_slot).GetValue(context, self, ipo.PythonType);
            }

            return Update(site, self, context);
        }

        public object UserSlotOnly(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                return ((PythonTypeUserDescriptorSlot)_slot).GetValue(context, self, ipo.PythonType);
            }

            return Update(site, self, context);
        }

        public object UserSlotDictGetAttr(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                object res;
                if (ipo.Dict != null && ipo.Dict.TryGetValue(_name, out res)) {
                    return res;
                }

                try {
                    return ((PythonTypeUserDescriptorSlot)_slot).GetValue(context, self, ipo.PythonType);
                } catch (MissingMemberException) {
                }

                if (_getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }

        public object UserSlotDictGetAttrOptimized(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                object res;
                PythonDictionary dict = ipo.Dict;

                if (UserTypeOps.TryGetDictionaryValue(dict, _name, _dictVersion, _dictIndex, out res)) {
                    return res;
                }

                try {
                    return ((PythonTypeUserDescriptorSlot)_slot).GetValue(context, self, ipo.PythonType);
                } catch (MissingMemberException) {
                }

                if (_getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }

        public object UserSlotOnlyGetAttr(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                try {
                    return ((PythonTypeUserDescriptorSlot)_slot).GetValue(context, self, ipo.PythonType);
                } catch (MissingMemberException) {
                }

                object res;
                if (_getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }

        public object UserSlot(CallSite site, object self, CodeContext context) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite && (object)context.ModuleContext.ExtensionMethods == (object)_extMethods) {
                object res = _slotFunc(self);
                if (res != Uninitialized.Instance) {
                    return res;
                }

                if (_getattrSlot != null && _getattrSlot.TryGetValue(context, self, ipo.PythonType, out res)) {
                    return GetAttr(context, res);
                }

                return TypeError(site, ipo, context);
            }

            return Update(site, self, context);
        }

        private object GetAttr(CodeContext context, object res) {
            if (_isNoThrow) {
                try {
                    return PythonContext.GetContext(context).Call(context, res, _name);
                } catch (MissingMemberException) {
                    return OperationFailed.Value;
                }
            } else {
                return PythonContext.GetContext(context).Call(context, res, _name);
            }
        }

        private object TypeError(CallSite site, IPythonObject ipo, CodeContext context) {
            return _fallback(site, ipo, context);
        }
    }

    enum OptimizedSetKind {
        None,
        SetAttr,
        UserSlot,
        SetDict,
        Error
    }
    
    class SetMemberDelegates<TValue> : FastSetBase<TValue> {
        private readonly string _name;
        private readonly PythonTypeSlot _slot;
        private readonly SlotSetValue _slotFunc;
        private readonly CodeContext _context;
        private readonly int _index, _keysVersion;

        public SetMemberDelegates(CodeContext context, PythonType type, OptimizedSetKind kind, string name, int version, PythonTypeSlot slot, SlotSetValue slotFunc) 
            : base(version) {
            _slot = slot;
            _name = name;
            _slotFunc = slotFunc;
            _context = context;
            switch (kind) {
                case OptimizedSetKind.SetAttr: _func = new Func<CallSite, object, TValue, object>(SetAttr); break;
                case OptimizedSetKind.UserSlot: _func = new Func<CallSite, object, TValue, object>(UserSlot); break;
                case OptimizedSetKind.SetDict:
                    var optNames = type.GetOptimizedInstanceNames();
                    int index;
                    if (optNames != null && (index = optNames.IndexOf(name)) != -1) {
                        _index = index;
                        _keysVersion = type.GetOptimizedInstanceVersion();
                        _func = new Func<CallSite, object, TValue, object>(SetDictOptimized);
                    } else {
                        _func = new Func<CallSite, object, TValue, object>(SetDict);
                    }
                    break;
                case OptimizedSetKind.Error: _func = new Func<CallSite, object, TValue, object>(Error); break;
            }
        }

        public object SetAttr(CallSite site, object self, TValue value) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite) {
                _hitCount++;

                object res;
                if (_slot.TryGetValue(_context, self, ipo.PythonType, out res)) {
                    return PythonOps.CallWithContext(_context, res, _name, value);
                }

                return TypeError(ipo);
            }

            return Update(site, self, value);
        }

        public object SetDictOptimized(CallSite site, object self, TValue value) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite) {
                _hitCount++;

                return UserTypeOps.SetDictionaryValueOptimized(ipo, _name, value, _keysVersion, _index);
            }

            return Update(site, self, value);
        }

        
        public object SetDict(CallSite site, object self, TValue value) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite) {
                _hitCount++;

                UserTypeOps.SetDictionaryValue(ipo, _name, value);
                return null;
            }

            return Update(site, self, value);
        }

        public object Error(CallSite site, object self, TValue value) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version) {
                return TypeError(ipo);
            }

            return Update(site, self, value);
        }

        public object UserSlot(CallSite site, object self, TValue value) {
            IPythonObject ipo = self as IPythonObject;
            if (ipo != null && ipo.PythonType.Version == _version && ShouldUseNonOptimizedSite) {
                _hitCount++;

                _slotFunc(self, value);
                return null;
            }

            return Update(site, self, value);
        }

        private object TypeError(IPythonObject ipo) {
            throw PythonOps.AttributeErrorForMissingAttribute(ipo.PythonType.Name, _name);
        }
    }

    class SetMemberKey : IEquatable<SetMemberKey> {
        public readonly Type Type;
        public readonly string Name;

        public SetMemberKey(Type type, string name) {
            Type = type;
            Name = name;
        }

        #region IEquatable<SetMemberKey> Members

        public bool Equals(SetMemberKey other) {
            return Type == other.Type && Name == other.Name;
        }

        #endregion

        public override bool Equals(object obj) {
            SetMemberKey other = obj as SetMemberKey;
            if (other == null) {
                return false;
            }

            return Equals(other);
        }

        public override int GetHashCode() {
            return Type.GetHashCode() ^ Name.GetHashCode();
        }
    }

    abstract class TypeGetBase : FastGetBase {
        private readonly FastGetDelegate[] _delegates;

        public TypeGetBase(PythonGetMemberBinder binder, FastGetDelegate[] delegates) {
            _delegates = delegates;
        }

        protected object RunDelegates(object self, CodeContext context) {
            _hitCount++;
            for (int i = 0; i < _delegates.Length; i++) {
                object res;
                if (_delegates[i](context, self, out res)) {                    
                    return res;
                }
            }

            // last delegate should always throw or succeed, this should be unreachable
            throw new InvalidOperationException();
        }

        protected object RunDelegatesNoOptimize(object self, CodeContext context) {
            for (int i = 0; i < _delegates.Length; i++) {
                object res;
                if (_delegates[i](context, self, out res)) {
                    return res;
                }
            }

            // last delegate should always throw or succeed, this should be unreachable
            throw new InvalidOperationException();
        }
    }

    delegate bool FastGetDelegate(CodeContext context, object self, out object result);

    class TypeGet : TypeGetBase {
        private int _version;

        public TypeGet(PythonGetMemberBinder binder, FastGetDelegate[] delegates, int version, bool isMeta, bool canOptimize)
            : base(binder, delegates) {
            _version = version;
            if (canOptimize) {
                if (isMeta) {
                    _func = MetaOnlyTargetOptimizing;
                } else {
                    _func = TargetOptimizing;
                }
            } else {
                if (isMeta) {
                    _func = MetaOnlyTarget;
                } else {
                    _func = Target;
                }
            }
        }

        public object Target(CallSite site, object self, CodeContext context) {
            PythonType pt = self as PythonType;

            if (pt != null && pt.Version == _version) {
                return RunDelegatesNoOptimize(self, context);
            }

            return Update(site, self, context);
        }

        public object MetaOnlyTarget(CallSite site, object self, CodeContext context) {
            PythonType pt = self as PythonType;
            if (pt != null && PythonOps.CheckTypeVersion(pt, _version)) {
                return RunDelegatesNoOptimize(self, context);
            }

            return Update(site, self, context);
        }

        public object TargetOptimizing(CallSite site, object self, CodeContext context) {
            PythonType pt = self as PythonType;

            if (pt != null && pt.Version == _version && ShouldUseNonOptimizedSite) {
                return RunDelegates(self, context);
            }

            return Update(site, self, context);
        }

        public object MetaOnlyTargetOptimizing(CallSite site, object self, CodeContext context) {
            PythonType pt = self as PythonType;
            if (pt != null && PythonOps.CheckTypeVersion(pt, _version) && ShouldUseNonOptimizedSite) {
                return RunDelegates(self, context);
            }

            return Update(site, self, context);
        }

        public override bool IsValid(PythonType type) {
            if (_func == MetaOnlyTarget || _func == MetaOnlyTargetOptimizing) {
                return PythonOps.CheckTypeVersion(type, _version);
            }

            return type.Version == _version;
        }
    }

    class SystemTypeGet : TypeGetBase {
        private readonly PythonType _self;

        public SystemTypeGet(PythonGetMemberBinder binder, FastGetDelegate[] delegates, PythonType type, bool isMeta, bool optimizing)
            : base(binder, delegates) {
            _self = type;
            if (optimizing) {
                if (isMeta) {
                    _func = MetaOnlyTargetOptimizing;
                } else {
                    _func = TargetOptimizing;
                }
            } else {
                if (isMeta) {
                    _func = MetaOnlyTarget;
                } else {
                    _func = Target;
                }
            }
        }

        public object Target(CallSite site, object self, CodeContext context) {
            if (self == _self) {
                return RunDelegatesNoOptimize(self, context);
            }

            return Update(site, self, context);
        }

        public object MetaOnlyTarget(CallSite site, object self, CodeContext context) {
            if (self is PythonType) {
                return RunDelegatesNoOptimize(self, context);
            }

            return Update(site, self, context);
        }

        public object TargetOptimizing(CallSite site, object self, CodeContext context) {
            if (self == _self && ShouldUseNonOptimizedSite) {
                return RunDelegates(self, context);
            }

            return Update(site, self, context);
        }

        public object MetaOnlyTargetOptimizing(CallSite site, object self, CodeContext context) {
            if (self is PythonType && ShouldUseNonOptimizedSite) {
                return RunDelegates(self, context);
            }

            return Update(site, self, context);
        }

        public override bool IsValid(PythonType type) {
            if (_func == MetaOnlyTarget || _func == MetaOnlyTargetOptimizing) {
                return true;
            }

            return type == _self;
        }
    }

    internal abstract class CachedGetKey : IEquatable<CachedGetKey> {
        public readonly string Name;

        public CachedGetKey(string name) {
            Name = name;
        }

        public static CachedGetKey Make(string name, ExtensionMethodSet set) {
            if (set.Id != ExtensionMethodSet.OutOfIds) {
                return new CachedGetIdIntExtensionMethod(name, set.Id);
            }

            return new CachedGetIdWeakRefExtensionMethod(name, new WeakReference(set));
        }

        public override bool Equals(object obj) {
            CachedGetKey cachedKey = obj as CachedGetKey;
            if (cachedKey == null) {
                return false;
            }

            return this.Equals(cachedKey);
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }

        public abstract bool Equals(CachedGetKey key);
    }

    internal sealed class CachedGetIdIntExtensionMethod : CachedGetKey {
        private readonly int _id;

        public CachedGetIdIntExtensionMethod(string name, int id)
            : base(name) {
            _id = id;
        }

        public override int GetHashCode() {
            return Name.GetHashCode() ^ _id;
        }

        #region IEquatable<CachedGetKey> Members

        public override bool Equals(CachedGetKey other) {
            var obj = other as CachedGetIdIntExtensionMethod;
            if (obj == null) {
                return false;
            }
            return obj._id == _id && obj.Name == Name;
        }

        #endregion
    }

    internal sealed class CachedGetIdWeakRefExtensionMethod : CachedGetKey {
        private readonly WeakReference _extMethodSet;

        public CachedGetIdWeakRefExtensionMethod(string name, WeakReference weakReference)
            : base(name) {
            _extMethodSet = weakReference;
        }

        #region IEquatable<CachedGetKey> Members

        public override bool Equals(CachedGetKey other) {
            var obj = other as CachedGetIdWeakRefExtensionMethod;
            if (obj == null) {
                return false;
            }
            return obj._extMethodSet.Target == _extMethodSet.Target && 
                obj.Name == Name;
        }

        #endregion
    }
}
