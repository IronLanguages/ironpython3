// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("sys", typeof(IronPython.Modules.SysModule))]
namespace IronPython.Modules {
    public static class SysModule {
        public const string __doc__ = "Provides access to functions which query or manipulate the Python runtime.";

        public const int api_version = 0;
        // argv is set by PythonContext and only on the initial load
        public static readonly string byteorder = BitConverter.IsLittleEndian ? "little" : "big";
        // builtin_module_names is set by PythonContext and updated on reload
        public const string copyright = "Copyright (c) IronPython Team";

        private static string GetPrefix() {
            string prefix;
#if FEATURE_ASSEMBLY_LOCATION
            try {
                prefix = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            } catch (SecurityException) {
                prefix = String.Empty;
            } catch (ArgumentException) {
                prefix = String.Empty;
            } catch (MethodAccessException) {
                prefix = String.Empty;
            }
#else
            prefix = String.Empty;
#endif
            return prefix;
        }

        /// <summary>
        /// Returns detailed call statistics.  Not implemented in IronPython and always returns None.
        /// </summary>
        public static object callstats() {
            return null;
        }

        /// <summary>
        /// Handles output of the expression statement.
        /// Prints the value and sets the __builtin__._
        /// </summary>
        [PythonHidden]
        [Documentation("displayhook(object) -> None\r\n\r\nPrint an object to sys.stdout and also save it in __builtin__._")]
        public static void displayhookImpl(CodeContext/*!*/ context, object value) {
            // based on the pseudcode: https://docs.python.org/3/library/sys.html#sys.displayhook
            if (value == null) return;
            context.LanguageContext.BuiltinModuleDict["_"] = null;
            var text = PythonOps.Repr(context, value);
            try {
                PythonOps.Print(context, text);
            } catch (EncoderFallbackException) {
                var stdout = context.LanguageContext.SystemStandardOut;
                var encoding = PythonOps.GetBoundAttr(context, stdout, "encoding") as string;
                var bytes = StringOps.encode(context, text, encoding, "backslashreplace");
                // TODO: write the bytes directly to the buffer if possible
                text = bytes.decode(context, encoding, "strict");
                PythonOps.Print(context, text);
            }
            context.LanguageContext.BuiltinModuleDict["_"] = value;
        }

        public static BuiltinFunction displayhook = BuiltinFunction.MakeFunction(
            nameof(displayhook),
            ArrayUtils.ConvertAll(typeof(SysModule).GetMember(nameof(displayhookImpl)), (x) => (MethodBase)x),
            typeof(SysModule)
        );

        public static readonly BuiltinFunction __displayhook__ = displayhook;

        public const int dllhandle = 0;

        [PythonHidden]
        [Documentation(@"excepthook(exctype, value, traceback) -> None

Handle an exception by displaying it with a traceback on sys.stderr._")]
        public static void excepthookImpl(CodeContext/*!*/ context, object exctype, object value, object traceback) {
            PythonContext pc = context.LanguageContext;

            PythonOps.PrintWithDest(
                context,
                pc.SystemStandardError,
                pc.FormatException(PythonExceptions.ToClr(value))
            );
        }

        public static readonly BuiltinFunction excepthook = BuiltinFunction.MakeFunction(
            "excepthook",
            ArrayUtils.ConvertAll(typeof(SysModule).GetMember("excepthookImpl"), (x) => (MethodBase)x),
            typeof(SysModule)
        );

        public static readonly BuiltinFunction __excepthook__ = excepthook;

        public static int getcheckinterval() {
            throw PythonOps.NotImplementedError("IronPython does not support sys.getcheckinterval");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "value")]
        public static void setcheckinterval(int value) {
            throw PythonOps.NotImplementedError("IronPython does not support sys.setcheckinterval");
        }

        public static int getrefcount(CodeContext/*!*/ context, object o) {
            // getrefcount() is used at various places in the CPython test suite, usually to 
            // check that instances are not cloned. Under .NET, we cannot provide that functionality, 
            // but we can at least return a dummy result so that the tests can continue.
            PythonOps.Warn(context, PythonExceptions.RuntimeWarning, "IronPython does not support sys.getrefcount. A dummy result is returned.");
            return 1000000;
        }

        // warnoptions is set by PythonContext and updated on each reload        

        public static PythonTuple exc_info(CodeContext/*!*/ context) {
            return PythonOps.GetExceptionInfo(context);
        }

        // exec_prefix and executable are set by PythonContext and updated on each reload

        public static string intern(object o) {
            string s = o as string;
            if (s == null) {
                throw PythonOps.TypeError("intern: argument must be string");
            }
            return string.Intern(s);
        }

        public static void exit() {
            exit(null);
        }

        public static void exit(object code) {
            if (code == null) {
                throw new PythonExceptions._SystemExit().InitAndGetClrException();
            } else {
                // throw as a python exception here to get the args set.
                throw new PythonExceptions._SystemExit().InitAndGetClrException(code);
            }
        }

        public static string getdefaultencoding(CodeContext/*!*/ context) {
            return context.LanguageContext.GetDefaultEncodingName();
        }

        public static object getfilesystemencoding() {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                return "utf-8";
            return "mbcs";
        }

        [PythonHidden]
        public static TraceBackFrame/*!*/ _getframeImpl(CodeContext/*!*/ context) {
            return _getframeImpl(context, 0);
        }

        [PythonHidden]
        public static TraceBackFrame/*!*/ _getframeImpl(CodeContext/*!*/ context, int depth) {
            return _getframeImpl(context, depth, PythonOps.GetFunctionStack());
        }

        internal static TraceBackFrame/*!*/ _getframeImpl(CodeContext/*!*/ context, int depth, List<FunctionStack> stack) {
            if (depth < stack.Count) {
                TraceBackFrame cur = null;

                for (int i = 0; i < stack.Count - depth; i++) {
                    var elem = stack[i];

                    if (elem.Frame != null) {
                        // we previously handed out a frame here, hand out the same one now
                        cur = elem.Frame;
                    } else {
                        // create a new frame and save it for future calls
                        cur = new TraceBackFrame(
                            context,
                            Builtin.globals(elem.Context),
                            Builtin.locals(elem.Context),
                            elem.Code,
                            cur
                        );

                        stack[i] = new FunctionStack(elem.Context, elem.Code, cur);
                    }
                }
                return cur;
            } 

            throw PythonOps.ValueError("call stack is not deep enough");
        }

        public static int getsizeof(object o) {
            return ObjectOps.__sizeof__(o);
        }

        public static PythonTuple getwindowsversion() {
            var osVer = Environment.OSVersion;
            return new windows_version(
                osVer.Version.Major,
                osVer.Version.Minor,
                osVer.Version.Build,
                (int)osVer.Platform
#if FEATURE_OS_SERVICEPACK
                , osVer.ServicePack
#else
                , ""
#endif      
                );
        }

        [PythonType("sys.getwindowsversion"), PythonHidden]
        public class windows_version : PythonTuple {
            internal windows_version(int major, int minor, int build, int platform, string service_pack)
                : base(new object[] { major, minor, build, platform, service_pack }) {

                this.major = major;
                this.minor = minor;
                this.build = build;
                this.platform = platform;
                this.service_pack = service_pack;
            }

            public readonly int major;
            public readonly int minor;
            public readonly int build;
            public readonly int platform;
            public readonly string service_pack;

            public const int n_fields = 5;
            public const int n_sequence_fields = 5;
            public const int n_unnamed_fields = 0;

            public override string __repr__(CodeContext context) {
                return
                    $"sys.getwindowsversion(major={major}, minor={minor}, build={build}, platform={platform}, service_pack='{service_pack}')";
            }
        }

        // hex_version is set by PythonContext

        public const int maxsize = Int32.MaxValue;
        public const int maxunicode = (int)ushort.MaxValue;

        // modules is set by PythonContext and only on the initial load

        // path is set by PythonContext and only on the initial load

        public static readonly string prefix = GetPrefix();

        // ps1 and ps2 are set by PythonContext and only on the initial load

        public static void setdefaultencoding(CodeContext context, object name) {
            if (name == null) throw PythonOps.TypeError("name cannot be None");
            string strName = name as string;
            if (strName == null) throw PythonOps.TypeError("name must be a string");

            PythonContext pc = context.LanguageContext;
            Encoding enc;
            if (!StringOps.TryGetEncoding(strName, out enc)) {
                throw PythonOps.LookupError("'{0}' does not match any available encodings", strName);
            }

            pc.DefaultEncoding = enc;
        }

#if PROFILE_SUPPORT
        // not enabled because we don't yet support tracing built-in functions.  Doing so is a little
        // difficult because it's hard to flip tracing on/off for them w/o a perf overhead in the 
        // non-profiling case.
        public static void setprofile(CodeContext/*!*/ context, TracebackDelegate o) {
            PythonContext pyContext = context.LanguageContext;
            pyContext.EnsureDebugContext();

            if (o == null) {
                pyContext.UnregisterTracebackHandler();
            } else {
                pyContext.RegisterTracebackHandler();
            }

            // Register the trace func with the listener
            pyContext.TracebackListener.SetProfile(o);
        }
#endif

        public static void settrace(CodeContext/*!*/ context, object o) {
            context.LanguageContext.SetTrace(o);
        }

        public static object call_tracing(CodeContext/*!*/ context, object func, PythonTuple args) {
            return context.LanguageContext.CallTracing(func, args);
        }

        public static object gettrace(CodeContext/*!*/ context) {
            return context.LanguageContext.GetTrace();
        }

        public static void setrecursionlimit(CodeContext/*!*/ context, int limit) {
            context.LanguageContext.RecursionLimit = limit;
        }

        public static int getrecursionlimit(CodeContext/*!*/ context) {
            return context.LanguageContext.RecursionLimit;
        }

        // stdin, stdout, stderr, __stdin__, __stdout__, and __stderr__ added by PythonContext

        // version and version_info are set by PythonContext
        public static PythonTuple subversion = PythonTuple.MakeTuple("IronPython", "", "");

        public const string winver = CurrentVersion.Series;

        #region Special types

        [PythonHidden, PythonType("flags"), DontMapIEnumerableToIter]
        public sealed class SysFlags : IList<object> {
            private const string _className = "sys.flags"; 
            
            internal SysFlags() { }

            private const int INDEX_DEBUG = 0;
            private const int INDEX_PY3K_WARNING = 1;
            private const int INDEX_INSPECT = 2;
            private const int INDEX_INTERACTIVE = 3;
            private const int INDEX_OPTIMIZE = 4;
            private const int INDEX_DONT_WRITE_BYTECODE = 5;
            private const int INDEX_NO_USER_SITE = 6;
            private const int INDEX_NO_SITE = 7;
            private const int INDEX_IGNORE_ENVIRONMENT = 8;
            private const int INDEX_TABCHECK = 9;
            private const int INDEX_VERBOSE = 10;
            private const int INDEX_UNICODE = 11;
            private const int INDEX_BYTES_WARNING = 12;

            public const int n_fields = 13;
            public const int n_sequence_fields = 13;
            public const int n_unnamed_fields = 0;

            private static readonly string[] _keys = new string[] {
                "debug", "py3k_warning", "inspect",
                "interactive", "optimize", "dont_write_bytecode", "no_user_site", "no_site",
                "ignore_environment", "tabcheck", "verbose", "unicode", "bytes_warning"
            };
            private object[] _values = new object[n_fields] {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            };

            private PythonTuple __tuple = null;
            private PythonTuple _tuple {
                get {
                    _Refresh();
                    return __tuple;
                }
            }

            private string __string = null;
            private string _string {
                get {
                    _Refresh();
                    return __string;
                }
            }
            public override string ToString() {
                return _string;
            }
            public string __repr__() {
                return _string;
            }

            private bool _modified = true;
            private void _Refresh() {
                if (_modified) {
                    __tuple = PythonTuple.MakeTuple(_values);

                    StringBuilder sb = new StringBuilder("sys.flags(");
                    for (int i = 0; i < n_fields; i++) {
                        if (_keys[i] == null) {
                            sb.Append(_values[i]);
                        } else {
                            sb.AppendFormat("{0}={1}", _keys[i], _values[i]);
                        }
                        if (i < n_fields - 1) {
                            sb.Append(", ");
                        } else {
                            sb.Append(')');
                        }
                    }
                    __string = sb.ToString();

                    _modified = false;
                }
            }

            private int _GetVal(int index) {
                return (int)_values[index];
            }
            private void _SetVal(int index, int value) {
                if ((int)_values[index] != value) {
                    _modified = true;
                    _values[index] = value;
                }
            }

            #region ICollection<object> Members

            void ICollection<object>.Add(object item) {
                throw new InvalidOperationException(_className + " is readonly");
            }

            void ICollection<object>.Clear() {
                throw new InvalidOperationException(_className + " is readonly");
            }

            [PythonHidden]
            public bool Contains(object item) {
                return _tuple.Contains(item);
            }

            [PythonHidden]
            public void CopyTo(object[] array, int arrayIndex) {
                _tuple.CopyTo(array, arrayIndex);
            }

            public int Count {
                [PythonHidden]
                get {
                    return n_fields;
                }
            }

            bool ICollection<object>.IsReadOnly {
                get { return true; }
            }

            bool ICollection<object>.Remove(object item) {
                throw new InvalidOperationException(_className + " is readonly");
            }

            #endregion

            #region IEnumerable Members

            [PythonHidden]
            public IEnumerator GetEnumerator() {
                return _tuple.GetEnumerator();
            }

            #endregion

            #region IEnumerable<object> Members

            IEnumerator<object> IEnumerable<object>.GetEnumerator() {
                return ((IEnumerable<object>)_tuple).GetEnumerator();
            }

            #endregion

            #region ISequence Members

            public int __len__() {
                return n_fields;
            }

            public object this[int i] {
                get {
                    return _tuple[i];
                }
            }

            public object this[BigInteger i] {
                get {
                    return this[(int)i];
                }
            }

            public object this[Slice s] {
                get {
                    return _tuple[s];
                }
            }

            public object this[object o] {
                get {
                    return this[Converter.ConvertToIndex(o)];
                }
            }

            #endregion

            #region IList<object> Members

            [PythonHidden]
            public int IndexOf(object item) {
                return _tuple.IndexOf(item);
            }

            void IList<object>.Insert(int index, object item) {
                throw new InvalidOperationException(_className + " is readonly");
            }

            void IList<object>.RemoveAt(int index) {
                throw new InvalidOperationException(_className + " is readonly");
            }

            object IList<object>.this[int index] {
                get {
                    return _tuple[index];
                }
                set {
                    throw new InvalidOperationException(_className + " is readonly");
                }
            }

            #endregion

            #region binary ops

            public static PythonTuple operator +([NotNull]SysFlags f, [NotNull]PythonTuple t) {
                return f._tuple + t;
            }

            public static PythonTuple operator *([NotNull]SysFlags f, int n) {
                return f._tuple * n;
            }

            public static PythonTuple operator *(int n, [NotNull]SysFlags f) {
                return f._tuple * n;
            }

            public static object operator *([NotNull]SysFlags f, [NotNull]Index n) {
                return f._tuple * n;
            }

            public static object operator *([NotNull]Index n, [NotNull]SysFlags f) {
                return f._tuple * n;
            }

            public static object operator *([NotNull]SysFlags f, object n) {
                return f._tuple * n;
            }

            public static object operator *(object n, [NotNull]SysFlags f) {
                return f._tuple * n;
            }

            #endregion

            # region comparison and hashing methods

            public static bool operator >(SysFlags f, PythonTuple t) {
                return f._tuple > t;
            }

            public static bool operator <(SysFlags f, PythonTuple t) {
                return f._tuple < t;
            }

            public static bool operator >=(SysFlags f, PythonTuple t) {
                return f._tuple >= t;
            }

            public static bool operator <=(SysFlags f, PythonTuple t) {
                return f._tuple <= t;
            }

            public override bool Equals(object obj) {
                if (obj is SysFlags) {
                    return _tuple.Equals(((SysFlags)obj)._tuple);
                }
                return _tuple.Equals(obj);
            }

            public override int GetHashCode() {
                return _tuple.GetHashCode();
            }

            # endregion

            #region sys.flags API

            public int debug {
                get { return _GetVal(INDEX_DEBUG); }
                internal set { _SetVal(INDEX_DEBUG, value); }
            }

            public int py3k_warning {
                get { return _GetVal(INDEX_PY3K_WARNING); }
                internal set { _SetVal(INDEX_PY3K_WARNING, value); }
            }

            public int inspect {
                get { return _GetVal(INDEX_INSPECT); }
                internal set { _SetVal(INDEX_INSPECT, value); }
            }

            public int interactive {
                get { return _GetVal(INDEX_INTERACTIVE); }
                internal set { _SetVal(INDEX_INTERACTIVE, value); }
            }

            public int optimize {
                get { return _GetVal(INDEX_OPTIMIZE); }
                internal set { _SetVal(INDEX_OPTIMIZE, value); }
            }

            public int dont_write_bytecode {
                get { return _GetVal(INDEX_DONT_WRITE_BYTECODE); }
                internal set { _SetVal(INDEX_DONT_WRITE_BYTECODE, value); }
            }

            public int no_user_site {
                get { return _GetVal(INDEX_NO_USER_SITE); }
                internal set { _SetVal(INDEX_NO_USER_SITE, value); }
            }

            public int no_site {
                get { return _GetVal(INDEX_NO_SITE); }
                internal set { _SetVal(INDEX_NO_SITE, value); }
            }

            public int ignore_environment {
                get { return _GetVal(INDEX_IGNORE_ENVIRONMENT); }
                internal set { _SetVal(INDEX_IGNORE_ENVIRONMENT, value); }
            }

            public int tabcheck {
                get { return _GetVal(INDEX_TABCHECK); }
                internal set { _SetVal(INDEX_TABCHECK, value); }
            }

            public int verbose {
                get { return _GetVal(INDEX_VERBOSE); }
                internal set { _SetVal(INDEX_VERBOSE, value); }
            }

            public int unicode {
                get { return _GetVal(INDEX_UNICODE); }
                internal set { _SetVal(INDEX_UNICODE, value); }
            }

            public int bytes_warning {
                get { return _GetVal(INDEX_BYTES_WARNING); }
                internal set { _SetVal(INDEX_BYTES_WARNING, value); }
            }

            #endregion
        }

        #endregion

        // These values are based on the .NET 2 BigInteger in Microsoft.Scripting.Math
        public static longinfo long_info = new longinfo(32, 4);

        [PythonType("sys.long_info"), PythonHidden]
        public class longinfo : PythonTuple {
            internal longinfo(int bits_per_digit, int sizeof_digit)
                : base(new object[] {bits_per_digit, sizeof_digit}) {

                this.bits_per_digit = bits_per_digit;
                this.sizeof_digit = sizeof_digit;
            }

            public readonly int bits_per_digit;
            public readonly int sizeof_digit;

            public const int n_fields = 2;
            public const int n_sequence_fields = 2;
            public const int n_unnamed_fields = 0;

            public override string __repr__(CodeContext context) {
                return $"sys.long_info(bits_per_digit={bits_per_digit}, sizeof_digit={sizeof_digit})";
            }
        }

        public static floatinfo float_info = new floatinfo(
            Double.MaxValue,    // DBL_MAX
            1024,               // DBL_MAX_EXP
            308,                // DBL_MAX_10_EXP
            // DBL_MIN
            BitConverter.Int64BitsToDouble(BitConverter.IsLittleEndian ? 0x0010000000000000 : 0x0000000000001000),
            -1021,              // DBL_MIN_EXP
            -307,               // DBL_MIN_10_EXP
            15,                 // DBL_DIG
            53,                 // DBL_MANT_DIG
           // DBL_EPSILON
            BitConverter.Int64BitsToDouble(BitConverter.IsLittleEndian ? 0x3cb0000000000000 : 0x000000000000b03c),
            2,                  // FLT_RADIX
            1);                 // FLT_ROUNDS

        [PythonType("sys.float_info"), PythonHidden]
        public class floatinfo : PythonTuple {
            internal floatinfo(double max, int max_exp, int max_10_exp,
                               double min, int min_exp, int min_10_exp,
                               int dig, int mant_dig, double epsilon, int radix, int rounds)
                : base(new object[] { max,  max_exp,  max_10_exp,
                                min,  min_exp,  min_10_exp,
                                dig,  mant_dig,  epsilon,  radix, rounds}) {

                this.max = max;
                this.max_exp = max_exp;
                this.max_10_exp = max_10_exp;
                this.min = min;
                this.min_exp = min_exp;
                this.min_10_exp = min_10_exp;
                this.dig = dig;
                this.mant_dig = mant_dig;
                this.epsilon = epsilon;
                this.radix = radix;
                this.rounds = rounds;
            }

            public readonly double max; 
            public readonly int max_exp;
            public readonly int max_10_exp;
            public readonly double min;
            public readonly int min_exp;
            public readonly int min_10_exp;
            public readonly int dig;
            public readonly int mant_dig;
            public readonly double epsilon;
            public readonly int radix;
            public readonly int rounds; 

            public const int n_fields = 11;
            public const int n_sequence_fields = 11;
            public const int n_unnamed_fields = 0;

            public override string __repr__(CodeContext context) {
                return string.Format("sys.float_info(max={0}, max_exp={1}, max_10_exp={2}, " +
                                     "min={3}, min_exp={4}, min_10_exp={5}, " +
                                     "dig={6}, mant_dig={7}, epsilon={8}, radix={9}, rounds={10})",
                    max, max_exp, max_10_exp,
                    min, min_exp, min_10_exp,
                    dig, mant_dig, epsilon, radix, rounds);
            }
        }


        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            dict["stdin"] = dict["__stdin__"];
            dict["stdout"] = dict["__stdout__"];
            dict["stderr"] = dict["__stderr__"];
            
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                dict["platform"] = "win32";
            } else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                dict["platform"] = "posix";
            } else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                dict["platform"] = "darwin";
            } else {
                dict["platform"] = "cli";
            }

            // !!! These fields do need to be reset on "reload(sys)". However, the initial value is specified by the 
            // engine elsewhere. For now, we initialize them just once to some default value
            dict["warnoptions"] = new List(0);

            PublishBuiltinModuleNames(context, dict);
            context.SetHostVariables(dict);

            dict["meta_path"] = new List(0);
            dict["path_hooks"] = new List(0);

            // add zipimport to the path hooks for importing from zip files.
            try {
                PythonModule zipimport = Importer.ImportModule(
                    context.SharedClsContext, context.SharedClsContext.GlobalDict,
                    "zipimport", false, 0) as PythonModule;
                if (zipimport != null) {
                    object zipimporter = PythonOps.GetBoundAttr(
                        context.SharedClsContext, zipimport, "zipimporter");
                    List path_hooks = dict["path_hooks"] as List;
                    if (path_hooks != null && zipimporter != null) {
                        path_hooks.Add(zipimporter);
                    }
                }
            } catch {
                // this is not a fatal error, so we don't do anything.
            }

            dict["path_importer_cache"] = new PythonDictionary();
        }

        internal static void PublishBuiltinModuleNames(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            object[] keys = new object[context.BuiltinModules.Keys.Count];
            int index = 0;
            foreach (object key in context.BuiltinModules.Keys) {
                keys[index++] = key;
            }
            dict["builtin_module_names"] = PythonTuple.MakeTuple(keys);
        }

    }
}
