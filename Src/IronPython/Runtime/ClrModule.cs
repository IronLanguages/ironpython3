// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_SERIALIZATION
using System.Runtime.Serialization.Formatters.Binary;
#endif

#if !FEATURE_REMOTING
using MarshalByRefObject = System.Object;
#endif

#if FEATURE_COM
using ComTypeLibInfo = Microsoft.Scripting.ComInterop.ComTypeLibInfo;
using ComTypeLibDesc = Microsoft.Scripting.ComInterop.ComTypeLibDesc;
#endif

#if FEATURE_FILESYSTEM && FEATURE_REFEMIT
using System.Reflection.Emit;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("clr", typeof(IronPython.Runtime.ClrModule))]
namespace IronPython.Runtime {
    /// <summary>
    /// this class contains objecs and static methods used for
    /// .NET/CLS interop with Python.  
    /// </summary>
    public static class ClrModule {

#if NETCOREAPP
        public static readonly bool IsNetCoreApp = true;
#elif NETFRAMEWORK
        public static readonly bool IsNetCoreApp = false;
#else
        public static readonly bool IsNetCoreApp = FrameworkDescription.StartsWith(".NET", StringComparison.Ordinal) && !FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal);
#endif

        public static string TargetFramework => typeof(ClrModule).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        internal static string FileVersion => typeof(ClrModule).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

#if DEBUG
        public static readonly bool IsDebug = true;
#else
        public static readonly bool IsDebug = false;
#endif

        public static string FrameworkDescription {
            get {
#if FEATURE_RUNTIMEINFORMATION
                var frameworkDescription = RuntimeInformation.FrameworkDescription;
                if (frameworkDescription.StartsWith(".NET Core 4.6.", StringComparison.OrdinalIgnoreCase)) {
                    return $".NET Core 2.x ({frameworkDescription.Substring(10)})";
                }
                return frameworkDescription;
#else
                // try reflection since we're probably running on a newer runtime anyway
                if (typeof(void).Assembly.GetType("System.Runtime.InteropServices.RuntimeInformation")?.GetProperty("FrameworkDescription")?.GetValue(null) is string frameworkDescription) {
                    return frameworkDescription;
                }
                return (IsMono ? "Mono " : ".NET Framework ") + Environment.Version.ToString();
#endif
            }
        }

        private static int _isMono = -1;
        public static bool IsMono {
            get {
                if (_isMono == -1) {
                    _isMono = Type.GetType("Mono.Runtime") != null ? 1 : 0;
                }
                return _isMono == 1;
            }
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            if (!dict.ContainsKey("References")) {
                dict["References"] = context.ReferencedAssemblies;
            }
        }

        #region Public methods

        /// <summary>
        /// Gets the current ScriptDomainManager that IronPython is loaded into.  The
        /// ScriptDomainManager can then be used to work with the language portion of the
        /// DLR hosting APIs.
        /// </summary>
        public static ScriptDomainManager/*!*/ GetCurrentRuntime(CodeContext/*!*/ context) {
            return context.LanguageContext.DomainManager;
        }

        [Documentation(@"Adds a reference to a .NET assembly.  Parameters can be an already loaded
Assembly object, a full assembly name, or a partial assembly name. After the
load the assemblies namespaces and top-level types will be available via 
import Namespace.")]
        public static void AddReference(CodeContext/*!*/ context, params object[] references) {
            if (references == null) throw new TypeErrorException("Expected string or Assembly, got NoneType");
            if (references.Length == 0) throw new ValueErrorException("Expected at least one name, got none");
            ContractUtils.RequiresNotNull(context, nameof(context));

            foreach (object reference in references) {
                AddReference(context, reference);
            }
        }

        [Documentation(@"Adds a reference to a .NET assembly.  One or more assembly names can
be provided.  The assembly is searched for in the directories specified in 
sys.path and dependencies will be loaded from sys.path as well.  The assembly 
name should be the filename on disk without a directory specifier and 
optionally including the .EXE or .DLL extension. After the load the assemblies 
namespaces and top-level types will be available via import Namespace.")]
        public static void AddReferenceToFile(CodeContext/*!*/ context, params string[] files) {
            if (files == null) throw new TypeErrorException("Expected string, got NoneType");
            if (files.Length == 0) throw new ValueErrorException("Expected at least one name, got none");
            ContractUtils.RequiresNotNull(context, nameof(context));

            foreach (string file in files) {
                AddReferenceToFile(context, file);
            }
        }

        [Documentation(@"Adds a reference to a .NET assembly.  Parameters are an assembly name. 
After the load the assemblies namespaces and top-level types will be available via 
import Namespace.")]
        public static void AddReferenceByName(CodeContext/*!*/ context, params string[] names) {
            if (names == null) throw new TypeErrorException("Expected string, got NoneType");
            if (names.Length == 0) throw new ValueErrorException("Expected at least one name, got none");
            ContractUtils.RequiresNotNull(context, nameof(context));

            foreach (string name in names) {
                AddReferenceByName(context, name);
            }
        }

        [Documentation(@"Adds a reference to a .NET assembly.  Parameters are a partial assembly name. 
After the load the assemblies namespaces and top-level types will be available via 
import Namespace.")]
        public static void AddReferenceByPartialName(CodeContext/*!*/ context, params string[] names) {
            if (names == null) throw new TypeErrorException("Expected string, got NoneType");
            if (names.Length == 0) throw new ValueErrorException("Expected at least one name, got none");
            ContractUtils.RequiresNotNull(context, nameof(context));

            foreach (string name in names) {
                AddReferenceByPartialName(context, name);
            }
        }

#if FEATURE_FILESYSTEM
        [Documentation(@"Adds a reference to a .NET assembly.  Parameters are a full path to an. 
assembly on disk. After the load the assemblies namespaces and top-level types 
will be available via import Namespace.")]
        public static Assembly/*!*/ LoadAssemblyFromFileWithPath(CodeContext/*!*/ context, string/*!*/ file) {
            if (file == null) throw new TypeErrorException("LoadAssemblyFromFileWithPath: arg 1 must be a string.");
            
            Assembly res;
            if (!context.LanguageContext.TryLoadAssemblyFromFileWithPath(file, out res)) {
                if (!Path.IsPathRooted(file)) {
                    throw new ValueErrorException("LoadAssemblyFromFileWithPath: path must be rooted");
                } else if (!File.Exists(file)) {
                    throw new ValueErrorException("LoadAssemblyFromFileWithPath: file not found");
                } else {
                    throw new ValueErrorException("LoadAssemblyFromFileWithPath: error loading assembly");
                }
            }
            return res;
        }

        [Documentation(@"Loads an assembly from the specified filename and returns the assembly
object.  Namespaces or types in the assembly can be accessed directly from 
the assembly object.")]
        public static Assembly/*!*/ LoadAssemblyFromFile(CodeContext/*!*/ context, string/*!*/ file) {
            if (file == null) throw new TypeErrorException("Expected string, got NoneType");
            if (file.Length == 0) throw new ValueErrorException("assembly name must not be empty string");
            ContractUtils.RequiresNotNull(context, nameof(context));

            if (file.IndexOf(Path.DirectorySeparatorChar) != -1) {
                throw new ValueErrorException("filenames must not contain full paths, first add the path to sys.path");
            }

            return context.LanguageContext.LoadAssemblyFromFile(file);
        }
#endif

#if FEATURE_LOADWITHPARTIALNAME
        [Documentation(@"Loads an assembly from the specified partial assembly name and returns the 
assembly object.  Namespaces or types in the assembly can be accessed directly 
from the assembly object.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName")]
        public static Assembly/*!*/ LoadAssemblyByPartialName(string/*!*/ name) {
            if (name == null) {
                throw new TypeErrorException("LoadAssemblyByPartialName: arg 1 must be a string");
            }

#pragma warning disable 618, 612 // csc
            return Assembly.LoadWithPartialName(name);
#pragma warning restore 618, 612
        }
#endif
        [Documentation(@"Loads an assembly from the specified assembly name and returns the assembly
object.  Namespaces or types in the assembly can be accessed directly from 
the assembly object.")]
        public static Assembly/*!*/ LoadAssemblyByName(CodeContext/*!*/ context, string/*!*/ name) {
            if (name == null) {
                throw new TypeErrorException("LoadAssemblyByName: arg 1 must be a string");
            }

            return context.LanguageContext.DomainManager.Platform.LoadAssembly(name);
        }

        /// <summary>
        /// Use(name) -> module
        /// 
        /// Attempts to load the specified module searching all languages in the loaded ScriptRuntime.
        /// </summary>
        public static object Use(CodeContext/*!*/ context, string/*!*/ name) {
            ContractUtils.RequiresNotNull(context, nameof(context));

            if (name == null) {
                throw new TypeErrorException("Use: arg 1 must be a string");
            }

            var scope = Importer.TryImportSourceFile(context.LanguageContext, name);
            if (scope == null) {
                throw new ValueErrorException(String.Format("couldn't find module {0} to use", name));
            }
            return scope;
        }

        [Documentation("Converts an array of bytes to a string.")]
        public static string GetString(byte[] bytes) {
            return GetString(bytes, bytes.Length);
        }

        [Documentation("Converts maxCount of an array of bytes to a string")]
        public static string GetString(byte[] bytes, int maxCount) {
            int bytesToCopy = Math.Min(bytes.Length, maxCount);            
            return Encoding.GetEncoding("iso-8859-1").GetString(bytes, 0, bytesToCopy);
        }

        [Documentation("Converts a string to an array of bytes")]
        public static byte[] GetBytes(string s) {
            return GetBytes(s, s.Length);
        }

        [Documentation("Converts maxCount of a string to an array of bytes")]
        public static byte[] GetBytes(string s, int maxCount) {
            int charsToCopy = Math.Min(s.Length, maxCount);
            byte[] ret = new byte[charsToCopy];
            Encoding.GetEncoding("iso-8859-1").GetBytes(s, 0, charsToCopy, ret, 0);
            return ret;
        }

        /// <summary>
        /// Use(path, language) -> module
        /// 
        /// Attempts to load the specified module belonging to a specific language loaded into the
        /// current ScriptRuntime.
        /// </summary>
        public static object/*!*/ Use(CodeContext/*!*/ context, string/*!*/ path, string/*!*/ language) {
            ContractUtils.RequiresNotNull(context, nameof(context));

            if (path == null) {
                throw new TypeErrorException("Use: arg 1 must be a string");
            }

            if (language == null) {
                throw new TypeErrorException("Use: arg 2 must be a string");
            }

            var manager = context.LanguageContext.DomainManager;
            if (!manager.Platform.FileExists(path)) {
                throw new ValueErrorException(String.Format("couldn't load module at path '{0}' in language '{1}'", path, language));
            }

            var sourceUnit = manager.GetLanguageByName(language).CreateFileUnit(path);
            return Importer.ExecuteSourceUnit(context.LanguageContext, sourceUnit);
        }

        /// <summary>
        /// SetCommandDispatcher(commandDispatcher)
        /// 
        /// Sets the current command dispatcher for the Python command line.  
        /// 
        /// The command dispatcher will be called with a delegate to be executed.  The command dispatcher
        /// should invoke the target delegate in the desired context.
        /// 
        /// A common use for this is to enable running all REPL commands on the UI thread while the REPL
        /// continues to run on a non-UI thread.
        /// </summary>
        public static Action<Action> SetCommandDispatcher(CodeContext/*!*/ context, Action<Action> dispatcher) {
            ContractUtils.RequiresNotNull(context, nameof(context));

            return ((PythonContext)context.LanguageContext).GetSetCommandDispatcher(dispatcher);
        }

        public static void ImportExtensions(CodeContext/*!*/ context, PythonType type) {
            if (type == null) {
                throw PythonOps.TypeError("type must not be None");
            } else if (!type.IsSystemType) {
                throw PythonOps.ValueError("type must be .NET type");
            }

            lock (context.ModuleContext) {
                context.ModuleContext.ExtensionMethods = ExtensionMethodSet.AddType(context.LanguageContext, context.ModuleContext.ExtensionMethods, type);
            }
        }

        public static void ImportExtensions(CodeContext/*!*/ context, [NotNone] NamespaceTracker @namespace) {
            lock (context.ModuleContext) {
                context.ModuleContext.ExtensionMethods = ExtensionMethodSet.AddNamespace(context.LanguageContext, context.ModuleContext.ExtensionMethods, @namespace);
            }
        }

#if FEATURE_COM
        /// <summary>
        /// LoadTypeLibrary(rcw) -> type lib desc
        /// 
        /// Gets an ITypeLib object from OLE Automation compatible RCW ,
        /// reads definitions of CoClass'es and Enum's from this library
        /// and creates an object that allows to instantiate coclasses
        /// and get actual values for the enums.
        /// </summary>
        public static ComTypeLibInfo LoadTypeLibrary(CodeContext/*!*/ context, object rcw) {
            return ComTypeLibDesc.CreateFromObject(rcw);
        }

        /// <summary>
        /// LoadTypeLibrary(guid) -> type lib desc
        /// 
        /// Reads the latest registered type library for the corresponding GUID,
        /// reads definitions of CoClass'es and Enum's from this library
        /// and creates a IDynamicMetaObjectProvider that allows to instantiate coclasses
        /// and get actual values for the enums.
        /// </summary>
        public static ComTypeLibInfo LoadTypeLibrary(CodeContext/*!*/ context, Guid typeLibGuid) {
            return ComTypeLibDesc.CreateFromGuid(typeLibGuid);
        }

        /// <summary>
        /// AddReferenceToTypeLibrary(rcw) -> None
        /// 
        /// Makes the type lib desc available for importing. See also LoadTypeLibrary.
        /// </summary>
        public static void AddReferenceToTypeLibrary(CodeContext/*!*/ context, object rcw) {
            ComTypeLibInfo typeLibInfo = ComTypeLibDesc.CreateFromObject(rcw);
            PublishTypeLibDesc(context, typeLibInfo.TypeLibDesc);
        }

        /// <summary>
        /// AddReferenceToTypeLibrary(guid) -> None
        /// 
        /// Makes the type lib desc available for importing.  See also LoadTypeLibrary.
        /// </summary>
        public static void AddReferenceToTypeLibrary(CodeContext/*!*/ context, Guid typeLibGuid) {
            ComTypeLibInfo typeLibInfo = ComTypeLibDesc.CreateFromGuid(typeLibGuid);
            PublishTypeLibDesc(context, typeLibInfo.TypeLibDesc);
        }

        private static void PublishTypeLibDesc(CodeContext context, ComTypeLibDesc typeLibDesc) {
            PythonOps.ScopeSetMember(context, context.LanguageContext.DomainManager.Globals, typeLibDesc.Name, typeLibDesc);
        }
#endif

        #endregion

        #region Private implementation methods

        private static void AddReference(CodeContext/*!*/ context, object reference) {
            Assembly asmRef = reference as Assembly;
            if (asmRef != null) {
                AddReference(context, asmRef);
                return;
            }

            if (reference is string strRef) {
                AddReference(context, strRef);
                return;
            }

            throw new TypeErrorException(String.Format("Invalid assembly type. Expected string or Assembly, got {0}.", reference));
        }

        private static void AddReference(CodeContext/*!*/ context, Assembly assembly) {
            context.LanguageContext.DomainManager.LoadAssembly(assembly);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")] // TODO: fix
        private static void AddReference(CodeContext/*!*/ context, string name) {
            if (name == null) throw new TypeErrorException("Expected string, got NoneType");

            Assembly asm = null;

            try {
                asm = LoadAssemblyByName(context, name);
            } catch { }

            // note we don't explicit call to get the file version
            // here because the assembly resolve event will do it for us.

#if FEATURE_LOADWITHPARTIALNAME
            if (asm == null) {
                asm = LoadAssemblyByPartialName(name);
            }
#endif

            if (asm == null) {
                throw new IOException(String.Format("Could not add reference to assembly {0}", name));
            }
            AddReference(context, asm);
        }

        private static void AddReferenceToFile(CodeContext/*!*/ context, string file) {
            if (file == null) throw new TypeErrorException("Expected string, got NoneType");

#if !FEATURE_FILESYSTEM
            Assembly asm = context.LanguageContext.DomainManager.Platform.LoadAssemblyFromPath(file);
#else
            Assembly asm = LoadAssemblyFromFile(context, file);
#endif
            if (asm == null) {
                throw new IOException(String.Format("Could not add reference to assembly {0}", file));
            }

            AddReference(context, asm);
        }

#if FEATURE_LOADWITHPARTIALNAME
        private static void AddReferenceByPartialName(CodeContext/*!*/ context, string name) {
            if (name == null) throw new TypeErrorException("Expected string, got NoneType");
            ContractUtils.RequiresNotNull(context, nameof(context));

            Assembly asm = LoadAssemblyByPartialName(name);
            if (asm == null) {
                throw new IOException(String.Format("Could not add reference to assembly {0}", name));
            }

            AddReference(context, asm);
        }
#endif
        private static void AddReferenceByName(CodeContext/*!*/ context, string name) {
            if (name == null) throw new TypeErrorException("Expected string, got NoneType");

            Assembly asm = LoadAssemblyByName(context, name);

            if (asm == null) {
                throw new IOException(String.Format("Could not add reference to assembly {0}", name));
            }

            AddReference(context, asm);
        }

        #endregion

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")] // TODO: fix
        public sealed class ReferencesList : List<Assembly>, ICodeFormattable {

            public new void Add(Assembly other) {
                base.Add(other);
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists"), SpecialName]
            public ClrModule.ReferencesList Add(object other) {
                IEnumerator ie = PythonOps.GetEnumerator(other);
                while (ie.MoveNext()) {
                    Assembly cur = ie.Current as Assembly;
                    if (cur == null) throw PythonOps.TypeError("non-assembly added to references list");

                    base.Add(cur);
                }
                return this;
            }

            public string/*!*/ __repr__(CodeContext/*!*/ context) {
                StringBuilder res = new StringBuilder("(");
                string comma = "";
                foreach (Assembly asm in this) {
                    res.Append(comma);
                    res.Append('<');
                    res.Append(asm.FullName);
                    res.Append('>');
                    comma = "," + Environment.NewLine;
                }

                res.AppendLine(")");
                return res.ToString();
            }
        }

        private static PythonType _strongBoxType;

        #region Runtime Type Checking support

#if FEATURE_FILESYSTEM
        [Documentation(@"Adds a reference to a .NET assembly.  One or more assembly names can
be provided which are fully qualified names to the file on disk.  The 
directory is added to sys.path and AddReferenceToFile is then called. After the 
load the assemblies namespaces and top-level types will be available via 
import Namespace.")]
        public static void AddReferenceToFileAndPath(CodeContext/*!*/ context, params string[] files) {
            if (files == null) throw new TypeErrorException("Expected string, got NoneType");
            ContractUtils.RequiresNotNull(context, nameof(context));

            foreach (string file in files) {
                AddReferenceToFileAndPath(context, file);
            }
        }

        private static void AddReferenceToFileAndPath(CodeContext/*!*/ context, string file) {
            if (file == null) throw PythonOps.TypeError("Expected string, got NoneType");

            // update our path w/ the path of this file...
            string path = System.IO.Path.GetDirectoryName(Path.GetFullPath(file));
            PythonList list;

            PythonContext pc = context.LanguageContext;
            if (!pc.TryGetSystemPath(out list)) {
                throw PythonOps.TypeError("cannot update path, it is not a list");
            }

            list.append(path);

            Assembly asm = pc.LoadAssemblyFromFile(file);
            if (asm == null) throw PythonOps.IOError("file does not exist: {0}", file);
            AddReference(context, asm);
        }

#endif

        /// <summary>
        /// Gets the CLR Type object from a given Python type object.
        /// </summary>
        public static Type GetClrType(Type type) {
            return type;
        }

        /// <summary>
        /// Gets the Python type object from a given CLR Type object.
        /// </summary>
        public static PythonType GetPythonType(Type t) {
            return DynamicHelpers.GetPythonTypeFromType(t);
        }

        /// <summary>
        /// OBSOLETE: Gets the Python type object from a given CLR Type object.
        /// 
        /// Use clr.GetPythonType instead.
        /// </summary>
        [Obsolete("Call clr.GetPythonType instead")]
        public static PythonType GetDynamicType(Type t) {
            return DynamicHelpers.GetPythonTypeFromType(t);
        }

        public static PythonType Reference {
            get {
                return StrongBox;
            }
        }


        public static PythonType StrongBox {
            get {
                if (_strongBoxType == null) {
                    _strongBoxType = DynamicHelpers.GetPythonTypeFromType(typeof(StrongBox<>));
                }
                return _strongBoxType;
            }
        }

        /// <summary>
        /// accepts(*types) -> ArgChecker
        /// 
        /// Decorator that returns a new callable object which will validate the arguments are of the specified types.
        /// </summary>
        /// <param name="types"></param>
        /// <returns></returns>
        public static object accepts([NotNone] params object[] types) {
            return new ArgChecker(types);
        }

        /// <summary>
        /// returns(type) -> ReturnChecker
        /// 
        /// Returns a new callable object which will validate the return type is of the specified type.
        /// </summary>
        public static object returns(object type) {
            return new ReturnChecker(type);
        }

        public static object Self() {
            return null;
        }

        #endregion

        /// <summary>
        /// Decorator for verifying the arguments to a function are of a specified type.
        /// </summary>
        public class ArgChecker {
            private readonly PythonType[] expected;

            public ArgChecker([NotNone] object[] types) {
                expected = types.Select(t => t.ToPythonType()).ToArray();
            }

            #region ICallableWithCodeContext Members

            [SpecialName]
            public object Call(CodeContext context, object func) {
                // expect only to receive the function we'll call here.

                return new RuntimeArgChecker(func, expected);
            }

            #endregion
        }

        /// <summary>
        /// Returned value when using clr.accepts/ArgChecker.  Validates the argument types and
        /// then calls the original function.
        /// </summary>
        public class RuntimeArgChecker : PythonTypeSlot {
            private readonly PythonType[] _expected;
            private readonly object _func;
            private readonly object _inst;

            public RuntimeArgChecker([NotNone] object function, [NotNone] PythonType[] expectedArgs) {
                _expected = expectedArgs;
                _func = function;
            }

            public RuntimeArgChecker(object instance, [NotNone] object function, [NotNone] PythonType[] expectedArgs)
                : this(function, expectedArgs) {
                _inst = instance;
            }

            private void ValidateArgs(object[] args) {
                int start = 0;

                if (_inst != null) {
                    start = 1;
                }

                // no need to validate self... the method should handle it.
                for (int i = start; i < args.Length + start; i++) {
                    object arg = args[i - start];
                    PythonType expct = _expected[i];
                    if (!IsInstanceOf(arg, expct)) {
                        throw PythonOps.AssertionError(
                            "argument {0} has bad value (expected {1}, got {2})",
                            i, expct.Name, PythonOps.GetPythonTypeName(arg));
                    }
                }
            }

            #region ICallableWithCodeContext Members
            [SpecialName]
            public object Call(CodeContext context, params object[] args) {
                ValidateArgs(args);

                if (_inst != null) {
                    return PythonOps.CallWithContext(context, _func, ArrayUtils.Insert(_inst, args));
                } else {
                    return PythonOps.CallWithContext(context, _func, args);
                }
            }

            #endregion

            internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
                value = new RuntimeArgChecker(instance, _func, _expected);
                return true;
            }

            internal override bool GetAlwaysSucceeds {
                get {
                    return true;
                }
            }

            #region IFancyCallable Members
            [SpecialName]
            public object Call(CodeContext context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
                ValidateArgs(args);

                if (_inst != null) {
                    return PythonCalls.CallWithKeywordArgs(context, _func, ArrayUtils.Insert(_inst, args), dict);
                } else {
                    return PythonCalls.CallWithKeywordArgs(context, _func, args, dict);
                }
            }

            #endregion
        }

        /// <summary>
        /// Decorator for verifying the return type of functions.
        /// </summary>
        public class ReturnChecker {
            public PythonType retType;

            public ReturnChecker(object returnType) {
                retType = returnType.ToPythonType();
            }

            #region ICallableWithCodeContext Members
            [SpecialName]
            public object Call(CodeContext context, object func) {
                // expect only to receive the function we'll call here.
                return new RuntimeReturnChecker(func, retType);
            }

            #endregion
        }

        /// <summary>
        /// Returned value when using clr.returns/ReturnChecker.  Calls the original function and
        /// validates the return type is of a specified type.
        /// </summary>
        public class RuntimeReturnChecker : PythonTypeSlot {
            private readonly PythonType _retType;
            private readonly object _func;
            private readonly object _inst;

            public RuntimeReturnChecker([NotNone] object function, [NotNone] PythonType expectedReturn) {
                _retType = expectedReturn;
                _func = function;
            }

            public RuntimeReturnChecker(object instance, [NotNone] object function, [NotNone] PythonType expectedReturn)
                : this(function, expectedReturn) {
                _inst = instance;
            }

            private void ValidateReturn(object ret) {
                if (!IsInstanceOf(ret, _retType)) {
                    throw PythonOps.AssertionError(
                        "bad return value returned (expected {0}, got {1})",
                        _retType.Name, PythonOps.GetPythonTypeName(ret));
                }
            }

            #region ICallableWithCodeContext Members
            [SpecialName]
            public object Call(CodeContext context, params object[] args) {
                object ret = PythonOps.CallWithContext(
                    context,
                    _func,
                    _inst != null ? ArrayUtils.Insert(_inst, args) : args);
                ValidateReturn(ret);
                return ret;
            }

            #endregion

            #region IDescriptor Members

            public object GetAttribute(object instance, object owner) {
                return new RuntimeReturnChecker(instance, _func, _retType);
            }


            #endregion

            internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
                value = GetAttribute(instance, owner);
                return true;
            }

            internal override bool GetAlwaysSucceeds {
                get {
                    return true;
                }
            }

            #region IFancyCallable Members
            [SpecialName]
            public object Call(CodeContext context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
                object ret;
                if (_inst != null) {
                    ret = PythonCalls.CallWithKeywordArgs(context, _func, ArrayUtils.Insert(_inst, args), dict);
                } else {
                    return PythonCalls.CallWithKeywordArgs(context, _func, args, dict);
                }
                ValidateReturn(ret);
                return ret;
            }

            #endregion
        }

        private static PythonType ToPythonType(this object obj) {
            return obj switch {
                PythonType pt => pt,
                Type t => DynamicHelpers.GetPythonTypeFromType(t),
                null => TypeCache.Null,
                _ => throw PythonOps.TypeErrorForTypeMismatch("type", obj)
            };
        }

        private static bool IsInstanceOf(object obj, PythonType pt) {
            // See also PythonOps.IsInstance
            var objType = DynamicHelpers.GetPythonType(obj);

            if (objType == pt) {
                return true;
            }

            // PEP 237: int/long unification
            // https://github.com/IronLanguages/ironpython3/issues/52
            if (pt == TypeCache.BigInteger && obj is int) {
                return true;
            }

            return pt.__subclasscheck__(objType);
        }

        /// <summary>
        /// returns the result of dir(o) as-if "import clr" has not been performed.
        /// </summary>
        public static PythonList Dir(CodeContext context, object o) {
            IList<object> ret = PythonOps.GetAttrNames(DefaultContext.Default, o);
            PythonList lret = new PythonList(context, ret);
            lret.Sort(DefaultContext.Default);
            return lret;
        }

        /// <summary>
        /// Returns the result of dir(o) as-if "import clr" has been performed.
        /// </summary>
        public static PythonList DirClr(CodeContext context, object o) {
            IList<object> ret = PythonOps.GetAttrNames(DefaultContext.DefaultCLS, o);
            PythonList lret = new PythonList(context, ret);
            lret.Sort(DefaultContext.DefaultCLS);
            return lret;
        }

        /// <summary>
        /// Attempts to convert the provided object to the specified type.  Conversions that 
        /// will be attempted include standard Python conversions as well as .NET implicit
        /// and explicit conversions.
        /// 
        /// If the conversion cannot be performed a TypeError will be raised.
        /// </summary>
        public static object Convert(CodeContext/*!*/ context, object o, Type toType) {
            return Converter.Convert(o, toType);
        }

#if FEATURE_FILESYSTEM && FEATURE_REFEMIT
        /// <summary>
        /// Provides a helper for compiling a group of modules into a single assembly.  The assembly can later be
        /// reloaded using the clr.AddReference API.
        /// </summary>
        public static void CompileModules(CodeContext/*!*/ context, string/*!*/ assemblyName, [ParamDictionary] IDictionary<string, object> kwArgs, params string/*!*/[]/*!*/ filenames) {
            ContractUtils.RequiresNotNull(assemblyName, nameof(assemblyName));
            ContractUtils.RequiresNotNullItems(filenames, nameof(filenames));

            PythonContext pc = context.LanguageContext;

            for (int i = 0; i < filenames.Length; i++) {
                filenames[i] = Path.GetFullPath(filenames[i]);
            }

            Dictionary<string, string> packageMap = BuildPackageMap(filenames);

            List<SavableScriptCode> code = new List<SavableScriptCode>();
            foreach (string filename in filenames) {
                if (!pc.DomainManager.Platform.FileExists(filename)) {
                    throw PythonOps.IOError($"Couldn't find file for compilation: {filename}");
                }

                ScriptCode sc;

                string modName;
                string dname = Path.GetDirectoryName(filename);
                string outFilename = "";
                if (Path.GetFileName(filename) == "__init__.py") {
                    // remove __init__.py to get package name
                    dname = Path.GetDirectoryName(dname);
                    if (String.IsNullOrEmpty(dname)) {
                        modName = Path.GetDirectoryName(filename);
                    } else {
                        modName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(filename));
                    }
                    outFilename = Path.DirectorySeparatorChar + "__init__.py";
                } else {
                    modName = Path.GetFileNameWithoutExtension(filename);
                }

                // see if we have a parent package, if so incorporate it into
                // our name
                if (packageMap.TryGetValue(dname, out string parentPackage)) {
                    modName = parentPackage + "." + modName;
                }

                outFilename = modName.Replace('.', Path.DirectorySeparatorChar) + outFilename;

                SourceUnit su = pc.CreateSourceUnit(
                    new FileStreamContentProvider(
                        context.LanguageContext.DomainManager.Platform,
                        filename
                    ),
                    outFilename,
                    pc.DefaultEncoding,
                    SourceCodeKind.File
                );

                sc = context.LanguageContext.GetScriptCode(su, modName, ModuleOptions.Initialize, Compiler.CompilationMode.ToDisk);

                code.Add((SavableScriptCode)sc);
            }

            if (kwArgs != null && kwArgs.TryGetValue("mainModule", out object mainModule)) {
                if (mainModule is string strModule) {
                    if (!pc.DomainManager.Platform.FileExists(strModule)) {
                        throw PythonOps.IOError("Couldn't find main file for compilation: {0}", strModule);
                    }

                    SourceUnit su = pc.CreateFileUnit(strModule, pc.DefaultEncoding, SourceCodeKind.File);
                    code.Add((SavableScriptCode)context.LanguageContext.GetScriptCode(su, "__main__", ModuleOptions.Initialize, Compiler.CompilationMode.ToDisk));
                }
            }

            SavableScriptCode.SaveToAssembly(assemblyName, kwArgs, code.ToArray());
        }

        public static AssemblyGen CreateAssemblyGen(CodeContext/*!*/ context, string/*!*/ assemblyName, [ParamDictionary]IDictionary<string, object> kwArgs, params string/*!*/[]/*!*/ filenames) {
            ContractUtils.RequiresNotNull(assemblyName, nameof(assemblyName));
            ContractUtils.RequiresNotNullItems(filenames, nameof(filenames));

            PythonContext pc = context.LanguageContext;

            for (int i = 0; i < filenames.Length; i++) {
                filenames[i] = Path.GetFullPath(filenames[i]);
            }

            Dictionary<string, string> packageMap = BuildPackageMap(filenames);

            List<SavableScriptCode> code = new List<SavableScriptCode>();
            foreach (string filename in filenames) {
                if (!pc.DomainManager.Platform.FileExists(filename)) {
                    throw PythonOps.IOError($"Couldn't find file for compilation: {filename}");
                }

                ScriptCode sc;

                string modName;
                string dname = Path.GetDirectoryName(filename);
                string outFilename = "";
                if (Path.GetFileName(filename) == "__init__.py") {
                    // remove __init__.py to get package name
                    dname = Path.GetDirectoryName(dname);
                    if (String.IsNullOrEmpty(dname)) {
                        modName = Path.GetDirectoryName(filename);
                    } else {
                        modName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(filename));
                    }
                    outFilename = Path.DirectorySeparatorChar + "__init__.py";
                } else {
                    modName = Path.GetFileNameWithoutExtension(filename);
                }

                // see if we have a parent package, if so incorporate it into
                // our name
                if (packageMap.TryGetValue(dname, out string parentPackage)) {
                    modName = parentPackage + "." + modName;
                }

                outFilename = modName.Replace('.', Path.DirectorySeparatorChar) + outFilename;

                SourceUnit su = pc.CreateSourceUnit(
                    new FileStreamContentProvider(
                        context.LanguageContext.DomainManager.Platform,
                        filename
                    ),
                    outFilename,
                    pc.DefaultEncoding,
                    SourceCodeKind.File
                );

                sc = context.LanguageContext.GetScriptCode(su, modName, ModuleOptions.Initialize, Compiler.CompilationMode.ToDisk);

                code.Add((SavableScriptCode)sc);
            }

            if (kwArgs != null && kwArgs.TryGetValue("mainModule", out object mainModule)) {
                if (mainModule is string strModule) {
                    if (!pc.DomainManager.Platform.FileExists(strModule)) {
                        throw PythonOps.IOError("Couldn't find main file for compilation: {0}", strModule);
                    }

                    SourceUnit su = pc.CreateFileUnit(strModule, pc.DefaultEncoding, SourceCodeKind.File);
                    code.Add((SavableScriptCode)context.LanguageContext.GetScriptCode(su, "__main__", ModuleOptions.Initialize, Compiler.CompilationMode.ToDisk));
                }
            }

            return CreateAssemblyGen(assemblyName, kwArgs, code.ToArray());
        }

        private class CodeInfo {
            public readonly MethodBuilder Builder;
            public readonly ScriptCode Code;
            public readonly Type DelegateType;

            public CodeInfo(MethodBuilder builder, ScriptCode code, Type delegateType) {
                Builder = builder;
                Code = code;
                DelegateType = delegateType;
            }
        }

        private static AssemblyGen CreateAssemblyGen(string assemblyName, IDictionary<string, object> assemblyAttributes, params SavableScriptCode[] codes) {
            ContractUtils.RequiresNotNull(assemblyName, nameof(assemblyName));
            ContractUtils.RequiresNotNullItems(codes, nameof(codes));

            // break the assemblyName into it's dir/name/extension
            string dir = Path.GetDirectoryName(assemblyName);
            if (string.IsNullOrEmpty(dir)) {
                dir = Environment.CurrentDirectory;
            }

            string name = Path.GetFileNameWithoutExtension(assemblyName);
            string ext = Path.GetExtension(assemblyName);

            // build the assembly & type gen that all the script codes will live in...
            AssemblyGen ag = new AssemblyGen(new AssemblyName(name), dir, ext, /*emitSymbols*/false, assemblyAttributes);
            TypeBuilder tb = ag.DefinePublicType("DLRCachedCode", typeof(object), true);
            TypeGen tg = new TypeGen(ag, tb);
            // then compile all of the code

            MethodInfo compileForSave =
                typeof(SavableScriptCode).GetMethod(
                    "CompileForSave",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    Type.DefaultBinder,
                    new[] { typeof(TypeGen) },
                    null);

            Dictionary<Type, List<CodeInfo>> langCtxBuilders = new Dictionary<Type, List<CodeInfo>>();
            foreach (SavableScriptCode sc in codes) {
                if (!langCtxBuilders.TryGetValue(sc.LanguageContext.GetType(), out List<CodeInfo> builders)) {
                    langCtxBuilders[sc.LanguageContext.GetType()] = builders = new List<CodeInfo>();
                }
                KeyValuePair<MethodBuilder, Type> compInfo = (KeyValuePair<MethodBuilder, Type>)compileForSave.Invoke(sc, new[] { tg });

                builders.Add(new CodeInfo(compInfo.Key, sc, compInfo.Value));
            }

            MethodBuilder mb = tb.DefineMethod(
                "GetScriptCodeInfo",
                MethodAttributes.SpecialName | MethodAttributes.Public | MethodAttributes.Static,
                typeof(MutableTuple<Type[], Delegate[][], string[][], string[][]>),
                ReflectionUtils.EmptyTypes);

            ILGen ilgen = new ILGen(mb.GetILGenerator());

            var langsWithBuilders = langCtxBuilders.ToArray();

            // lang ctx array
            ilgen.EmitArray(typeof(Type), langsWithBuilders.Length, (index) => {
                ilgen.Emit(OpCodes.Ldtoken, langsWithBuilders[index].Key);
                ilgen.EmitCall(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) }));
            });

            // builders array of array
            ilgen.EmitArray(typeof(Delegate[]), langsWithBuilders.Length, (index) => {
                List<CodeInfo> builders = langsWithBuilders[index].Value;

                ilgen.EmitArray(typeof(Delegate), builders.Count, (innerIndex) => {
                    ilgen.EmitNull();
                    ilgen.Emit(OpCodes.Ldftn, builders[innerIndex].Builder);
                    ilgen.EmitNew(
                        builders[innerIndex].DelegateType,
                        new[] { typeof(object), typeof(IntPtr) }
                    );
                });
            });

            // paths array of array
            ilgen.EmitArray(typeof(string[]), langsWithBuilders.Length, (index) => {
                List<CodeInfo> builders = langsWithBuilders[index].Value;

                ilgen.EmitArray(typeof(string), builders.Count, (innerIndex) => {
                    ilgen.EmitString(builders[innerIndex].Code.SourceUnit.Path);
                });
            });

            // 4th element in tuple - custom per-language data
            ilgen.EmitArray(typeof(string[]), langsWithBuilders.Length, (index) => {
                List<CodeInfo> builders = langsWithBuilders[index].Value;

                ilgen.EmitArray(typeof(string), builders.Count, (innerIndex) => {
                    ICustomScriptCodeData data = builders[innerIndex].Code as ICustomScriptCodeData;
                    if (data != null) {
                        ilgen.EmitString(data.GetCustomScriptCodeData());
                    } else {
                        ilgen.Emit(OpCodes.Ldnull);
                    }
                });
            });

            ilgen.EmitNew(
                typeof(MutableTuple<Type[], Delegate[][], string[][], string[][]>),
                new[] { typeof(Type[]), typeof(Delegate[][]), typeof(string[][]), typeof(string[][]) }
            );
            ilgen.Emit(OpCodes.Ret);

            mb.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(DlrCachedCodeAttribute).GetConstructor(ReflectionUtils.EmptyTypes),
                ArrayUtils.EmptyObjects
            ));

            tg.FinishType();
            return ag;
        }
#endif

#if FEATURE_REFEMIT
        /// <summary>
        /// clr.CompileSubclassTypes(assemblyName, *typeDescription)
        /// 
        /// Provides a helper for creating an assembly which contains pre-generated .NET 
        /// base types for new-style types.
        /// 
        /// This assembly can then be AddReferenced or put sys.prefix\DLLs and the cached 
        /// types will be used instead of generating the types at runtime.
        /// 
        /// This function takes the name of the assembly to save to and then an arbitrary 
        /// number of parameters describing the types to be created.  Each of those
        /// parameter can either be a plain type or a sequence of base types.
        /// 
        /// clr.CompileSubclassTypes(object) -> create a base type for object
        /// clr.CompileSubclassTypes(object, str, System.Collections.ArrayList) -> create 
        ///     base  types for both object and ArrayList.
        ///     
        /// clr.CompileSubclassTypes(object, (object, IComparable)) -> create base types for 
        ///     object and an object which implements IComparable.
        /// 
        /// </summary>
        public static void CompileSubclassTypes(string/*!*/ assemblyName, params object[] newTypes) {
            if (assemblyName == null) {
                throw PythonOps.TypeError("CompileTypes expected str for assemblyName, got NoneType");
            }

            var typesToCreate = new List<PythonTuple>();
            foreach (object o in newTypes) {
                if (o is PythonType) {
                    typesToCreate.Add(PythonTuple.MakeTuple(o));
                } else {
                    typesToCreate.Add(PythonTuple.Make(o));
                }
            }

            NewTypeMaker.SaveNewTypes(assemblyName, typesToCreate);
        }
#endif

        /// <summary>
        /// clr.GetSubclassedTypes() -> tuple
        /// 
        /// Returns a tuple of information about the types which have been subclassed. 
        /// 
        /// This tuple can be passed to clr.CompileSubclassTypes to cache these
        /// types on disk such as:
        /// 
        /// clr.CompileSubclassTypes('assembly', *clr.GetSubclassedTypes())
        /// </summary>
        public static PythonTuple GetSubclassedTypes() {
            List<object> res = new List<object>();
            
            foreach (NewTypeInfo info in NewTypeMaker._newTypes.Keys) {
                Type clrBaseType = info.BaseType;
                Type tempType = clrBaseType;
                while (tempType != null) {
                    if (tempType.IsGenericType && tempType.GetGenericTypeDefinition() == typeof(Extensible<>)) {
                        clrBaseType = tempType.GetGenericArguments()[0];
                        break;
                    }
                    tempType = tempType.BaseType;
                }

                PythonType baseType = DynamicHelpers.GetPythonTypeFromType(clrBaseType);
                if (info.InterfaceTypes.Count == 0) {
                    res.Add(baseType);
                } else if (info.InterfaceTypes.Count > 0) {
                    PythonType[] types = new PythonType[info.InterfaceTypes.Count + 1];
                    types[0] = baseType;
                    for (int i = 0; i < info.InterfaceTypes.Count; i++) {
                        types[i + 1] = DynamicHelpers.GetPythonTypeFromType(info.InterfaceTypes[i]);
                    }
                    res.Add(PythonTuple.MakeTuple(types));
                }
            }

            return PythonTuple.MakeTuple(res.ToArray());
        }

        /// <summary>
        /// Provides a StreamContentProvider for a stream of content backed by a file on disk.
        /// </summary>
        [Serializable]
        internal sealed class FileStreamContentProvider : StreamContentProvider {
            private readonly string _path;
            private readonly PALHolder _pal;

            internal string Path {
                get { return _path; }
            }

            #region Construction

            internal FileStreamContentProvider(PlatformAdaptationLayer manager, string path) {
                ContractUtils.RequiresNotNull(path, nameof(path));

                _path = path;
                _pal = new PALHolder(manager);
            }

            #endregion

            public override Stream GetStream() {
                return _pal.GetStream(Path);
            }

            [Serializable]
            private class PALHolder : MarshalByRefObject {
                [NonSerialized]
                private readonly PlatformAdaptationLayer _pal;

                internal PALHolder(PlatformAdaptationLayer pal) {
                    _pal = pal;
                }

                internal Stream GetStream(string path) {
                    return _pal.OpenInputFileStream(path);
                }
            }
        }

        /// <summary>
        /// Goes through the list of files identifying the relationship between packages
        /// and subpackages.  Returns a dictionary with all of the package filenames (minus __init__.py)
        /// mapping to their full name.  For example given a structure:
        /// 
        /// C:\
        ///     someDir\
        ///         package\
        ///             __init__.py
        ///             a.py
        ///             b\
        ///                 __init.py
        ///                 c.py
        ///         
        /// Returns:
        ///     {r'C:\somedir\package' : 'package', r'C:\somedir\package\b', 'package.b'}
        ///     
        /// This can then be used for calculating the full module name of individual files
        /// and packages.  For example a's full name is "package.a" and c's full name is
        /// "package.b.c".
        /// </summary>
        private static Dictionary<string/*!*/, string/*!*/>/*!*/ BuildPackageMap(string/*!*/[]/*!*/ filenames) {
            // modules which are the children of packages should have the __name__
            // package.subpackage.modulename, not just modulename.  So first
            // we need to get a list of all the modules...
            List<string> modules = new List<string>();
            foreach (string filename in filenames) {
                if (filename.EndsWith("__init__.py", StringComparison.Ordinal)) {
                    // this is a package
                    modules.Add(filename);
                }
            }

            // next we need to understand the relationship between the packages so
            // if we have package.subpackage1 and package.subpackage2 we know
            // both of these are children of the package.  So sort the module names,
            // shortest name first...
            SortModules(modules);

            // finally build up the package names for the dirs...
            Dictionary<string, string> packageMap = new Dictionary<string, string>();
            foreach (string packageName in modules) {
                string dirName = Path.GetDirectoryName(packageName);    // remove __init__.py
                string fullName = Path.GetFileName(Path.GetDirectoryName(packageName));

                if (packageMap.TryGetValue(Path.GetDirectoryName(dirName), out string pkgName)) {   // remove directory name
                    fullName = pkgName + "." + fullName;
                }

                packageMap[Path.GetDirectoryName(packageName)] = fullName;
            }
            return packageMap;
        }

        private static void SortModules(List<string> modules) {
            modules.Sort((string x, string y) => x.Length - y.Length);
        }

        /// <summary>
        /// Returns a list of profile data. The values are tuples of Profiler.Data objects
        /// 
        /// All times are expressed in the same unit of measure as DateTime.Ticks
        /// </summary>
        public static PythonTuple GetProfilerData(CodeContext/*!*/ context, bool includeUnused=false) {
            return new PythonTuple(Profiler.GetProfiler(context.LanguageContext).GetProfile(includeUnused));
        }

        /// <summary>
        /// Resets all profiler counters back to zero
        /// </summary>
        public static void ClearProfilerData(CodeContext/*!*/ context) {
            Profiler.GetProfiler(context.LanguageContext).Reset();
        }

        /// <summary>
        /// Enable or disable profiling for the current ScriptEngine.  This will only affect code
        /// that is compiled after the setting is changed; previously-compiled code will retain
        /// whatever setting was active when the code was originally compiled.
        /// 
        /// The easiest way to recompile a module is to reload() it.
        /// </summary>
        public static void EnableProfiler(CodeContext/*!*/ context, bool enable) {
            var pc = context.LanguageContext;
            var po = pc.Options as PythonOptions;
            po.EnableProfiler = enable;
        }

#if FEATURE_SERIALIZATION
        /// <summary>
        /// Serializes data using the .NET serialization formatter for complex
        /// types.  Returns a tuple identifying the serialization format and the serialized 
        /// data which can be fed back into clr.Deserialize.
        /// 
        /// Current serialization formats include custom formats for primitive .NET
        /// types which aren't already recognized as tuples.  None is used to indicate
        /// that the Binary .NET formatter is used.
        /// </summary>
        public static PythonTuple/*!*/ Serialize(object self) {
            if (self == null) {
                return PythonTuple.MakeTuple(null, String.Empty);
            }

            string data, format;
            Type type = CompilerHelpers.GetType(self);
            if ((type.IsPrimitive && type.GetTypeCode() != TypeCode.Object)
                || type == typeof(decimal) || type == typeof(DBNull)) {
                // For select primitive types just do a simple serialization.
                data = self.ToString();
                format = type.FullName;
            } else {
                // something more complex, let the binary formatter handle it                    
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream stream = new MemoryStream();
#pragma warning disable SYSLIB0011 // BinaryFormatter serialization methods are obsolete in .NET 5.0
                bf.Serialize(stream, self);
#pragma warning restore SYSLIB0011
                data = stream.ToArray().MakeString();
                format = null;
            }

            return PythonTuple.MakeTuple(format, data);
        }

        /// <summary>
        /// Deserializes the result of a Serialize call.  This can be used to perform serialization
        /// for .NET types which are serializable.  This method is the callable object provided
        /// from __reduce_ex__ for .serializable .NET types.
        /// 
        /// The first parameter indicates the serialization format and is the first tuple element
        /// returned from the Serialize call.  
        /// 
        /// The second parameter is the serialized data.
        /// </summary>
        public static object Deserialize(string serializationFormat, [NotNone] string/*!*/ data) {
            if (serializationFormat != null) {
                switch (serializationFormat) {
                    case "System.Boolean": return Boolean.Parse(data);
                    case "System.Byte": return Byte.Parse(data);
                    case "System.Char": return Char.Parse(data);
                    case "System.DBNull": return DBNull.Value;
                    case "System.Decimal": return Decimal.Parse(data);
                    case "System.Double": return Double.Parse(data);
                    case "System.Int16": return Int16.Parse(data);
                    case "System.Int32": return Int32.Parse(data);
                    case "System.Int64": return Int64.Parse(data);
                    case "System.SByte": return SByte.Parse(data);
                    case "System.Single": return Single.Parse(data);
                    case "System.UInt16": return UInt16.Parse(data);
                    case "System.UInt32": return UInt32.Parse(data);
                    case "System.UInt64": return UInt64.Parse(data);
                    default:
                        throw PythonOps.ValueError("unknown serialization format: {0}", serializationFormat);
                }
            } else if (String.IsNullOrEmpty(data)) {
                return null;
            }

            MemoryStream stream = new MemoryStream(data.MakeByteArray());
            BinaryFormatter bf = new BinaryFormatter();
#pragma warning disable SYSLIB0011 // BinaryFormatter serialization methods are obsolete in .NET 5.0
            return bf.Deserialize(stream);
#pragma warning restore SYSLIB0011
        }
#endif
    }
}
