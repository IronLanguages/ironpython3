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
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;
using IronPython.Compiler.Ast;
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

[assembly: PythonModule("builtins", typeof(IronPython.Modules.Builtin))]
namespace IronPython.Modules {
    [Documentation("")]  // Documentation suppresses XML Doc on startup.
    public static partial class Builtin {
        public const string __doc__ = "Provides access to commonly used built-in functions, exception objects, etc...";
        public const object __package__ = null;
        public const string __name__ = "builtins";

        public static object True {
            get {
                return ScriptingRuntimeHelpers.True;
            }
        }

        public static object False {
            get {
                return ScriptingRuntimeHelpers.False;
            }
        }

        // This will always stay null
        public static readonly object None;

        public static IronPython.Runtime.Types.Ellipsis Ellipsis {
            get {
                return IronPython.Runtime.Types.Ellipsis.Value;
            }
        }

        public static NotImplementedType NotImplemented {
            get {
                return NotImplementedType.Value;
            }
        }

        public static object exit {
            get {
                return "Use Ctrl-Z plus Return to exit";
            }
        }

        public static object quit {
            get {
                return "Use Ctrl-Z plus Return to exit";
            }
        }

        [Documentation("__import__(name) -> module\n\nImport a module.")]
        [LightThrowing]
        public static object __import__(CodeContext/*!*/ context, string name) {
            return __import__(context, name, null, null, null, -1);
        }

        [Documentation("__import__(name, globals, locals, fromlist, level) -> module\n\nImport a module.")]
        [LightThrowing]
        public static object __import__(CodeContext/*!*/ context, string name, [DefaultParameterValue(null)]object globals, [DefaultParameterValue(null)]object locals, [DefaultParameterValue(null)]object fromlist, [DefaultParameterValue(-1)]int level) {
            if (fromlist is string || fromlist is Extensible<string>) {
                fromlist = new List<object> { fromlist };
            }

            IList from = fromlist as IList;
            PythonContext pc = PythonContext.GetContext(context);

            object ret = Importer.ImportModule(context, globals, name, from != null && from.Count > 0, level);
            if (ret == null) {
                return LightExceptions.Throw(PythonOps.ImportError("No module named {0}", name));
            }

            PythonModule mod = ret as PythonModule;
            if (mod != null && from != null) {
                string strAttrName;
                for (int i = 0; i < from.Count; i++) {
                    object attrName = from[i];

                    if (pc.TryConvertToString(attrName, out strAttrName) &&
                        !String.IsNullOrEmpty(strAttrName) &&
                        strAttrName != "*") {
                        try {
                            Importer.ImportFrom(context, mod, strAttrName);
                        } catch (ImportException) {
                            continue;
                        }
                    }
                }
            }

            return ret;
        }

        [Documentation("abs(number) -> number\n\nReturn the absolute value of the argument.")]
        public static object abs(CodeContext/*!*/ context, object o) {
            if (o is int) return Int32Ops.Abs((int)o);
            if (o is long) return Int64Ops.Abs((long)o);
            if (o is double) return DoubleOps.Abs((double)o);
            if (o is bool) return (((bool)o) ? 1 : 0);

            if (o is BigInteger) return BigIntegerOps.__abs__((BigInteger)o);
            if (o is Complex) return ComplexOps.Abs((Complex)o);

            object value;
            if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__abs__", out value)) {
                return value;
            }

            throw PythonOps.TypeError("bad operand type for abs(): '{0}'", PythonTypeOps.GetName(o));
        }

        public static bool all(object x) {
            IEnumerator i = PythonOps.GetEnumerator(x);
            while (i.MoveNext()) {
                if (!PythonOps.IsTrue(i.Current)) return false;
            }
            return true;
        }

        public static bool any(object x) {
            IEnumerator i = PythonOps.GetEnumerator(x);
            while (i.MoveNext()) {
                if (PythonOps.IsTrue(i.Current)) return true;
            }
            return false;
        }

        [Documentation("apply(object[, args[, kwargs]]) -> value\n\nDeprecated.\nInstead, use:\n    function(*args, **keywords).")]
        public static object apply(CodeContext/*!*/ context, object func) {
            return PythonOps.CallWithContext(context, func);
        }

        public static object apply(CodeContext/*!*/ context, object func, object args) {
            return PythonOps.CallWithArgsTupleAndContext(context, func, ArrayUtils.EmptyObjects, args);
        }

        public static object apply(CodeContext/*!*/ context, object func, object args, object kws) {
            return context.LanguageContext.CallWithKeywords(func, args, kws);
        }

        public static PythonType basestring {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(string));
            }
        }

        public static string bin(int number) {
            return Int32Ops.ToBinary(number);
        }

        public static string bin(Index number) {
            return Int32Ops.ToBinary(Converter.ConvertToIndex(number));
        }

        public static string bin(BigInteger number) {
            return BigIntegerOps.ToBinary(number);
        }

        public static string bin(double number) {
            throw PythonOps.TypeError("'float' object cannot be interpreted as an index");
        }

        public static PythonType @bool {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(bool));
            }
        }


        public static PythonType buffer {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonBuffer));
            }
        }

        public static PythonType bytes {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(Bytes));
            }
        }

        public static PythonType bytearray {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(ByteArray));
            }
        }

        [Documentation("callable(object) -> bool\n\nReturn whether the object is callable (i.e., some kind of function).")]
        [Python3Warning("callable() is removed in 3.x. instead call hasattr(obj, '__call__')")]
        public static bool callable(CodeContext/*!*/ context, object o) {
            return PythonOps.IsCallable(context, o);
        }

        [Documentation("chr(i) -> character\n\nReturn a string of one character with ordinal i; 0 <= i< 256.")]
        [LightThrowing]
        public static object chr(int value) {
            if (value < 0 || value > 0xFF) {
                return LightExceptions.Throw(PythonOps.ValueError("{0} is not in required range", value));
            }
            return ScriptingRuntimeHelpers.CharToString((char)value);
        }

        internal static object TryCoerce(CodeContext/*!*/ context, object x, object y) {
            PythonTypeSlot pts;
            PythonType xType = DynamicHelpers.GetPythonType(x);

            if (xType.TryResolveSlot(context, "__coerce__", out pts)) {
                object callable;
                if (pts.TryGetValue(context, x, xType, out callable)) {
                    return PythonCalls.Call(context, callable, y);
                }
            }
            return NotImplementedType.Value;
        }

        [Documentation("coerce(x, y) -> (x1, y1)\n\nReturn a tuple consisting of the two numeric arguments converted to\na common type. If coercion is not possible, raise TypeError.")]
        public static object coerce(CodeContext/*!*/ context, object x, object y) {
            object converted;

            if (x == null && y == null) {
                return PythonTuple.MakeTuple(null, null);
            }

            converted = TryCoerce(context, x, y);
            if (converted != null && converted != NotImplementedType.Value) {
                return converted;
            }

            converted = TryCoerce(context, y, x);
            if (converted != null && converted != NotImplementedType.Value) {
                PythonTuple pt = converted as PythonTuple;
                if (pt != null && pt.Count == 2) {
                    return PythonTuple.MakeTuple(pt[1], pt[0]);
                }
            }

            throw PythonOps.TypeError("coercion failed");
        }

        [Documentation("compile a unit of source code.\n\nThe source can be compiled either as exec, eval, or single.\nexec compiles the code as if it were a file\neval compiles the code as if were an expression\nsingle compiles a single statement\n\nsource can either be a string or an AST object")]
        public static object compile(CodeContext/*!*/ context, object source, string filename, string mode, [DefaultParameterValue(null)]object flags, [DefaultParameterValue(null)]object dont_inherit) {

            bool astOnly = false;
            int iflags = flags != null ? Converter.ConvertToInt32(flags) : 0;
            if ((iflags & _ast.PyCF_ONLY_AST) != 0) {
                astOnly = true;
                iflags &= ~_ast.PyCF_ONLY_AST;
            }

            if (mode != "exec" && mode != "eval" && mode != "single") {
                throw PythonOps.ValueError("compile() arg 3 must be 'exec' or 'eval' or 'single'");
            }
            if (source is _ast.AST) {
                if (astOnly) {
                    return source;
                } else {
                    PythonAst ast = _ast.ConvertToPythonAst(context, (_ast.AST) source);
                    ast.Bind();
                    ScriptCode code = ast.ToScriptCode();
                    return ((RunnableScriptCode)code).GetFunctionCode(true);
                }
            }

            string text;
            if (source is string)
                text = (string)source;                        
            else if (source is PythonBuffer)
                text = ((PythonBuffer)source).ToString();
            else if (source is ByteArray)
                text = ((ByteArray)source).ToString();
            else if (source is Bytes)
                text = ((Bytes)source).ToString();
            else 
                // cpython accepts either AST or readable buffer object
                throw PythonOps.TypeError("srouce can be either AST or string, actual argument: {0}", source.GetType());
            
            if (text.IndexOf('\0') != -1) {
                throw PythonOps.TypeError("compile() expected string without null bytes");
            }

            text = RemoveBom(text);

            bool inheritContext = GetCompilerInheritance(dont_inherit);
            CompileFlags cflags = GetCompilerFlags(iflags);
            PythonCompilerOptions opts = GetRuntimeGeneratedCodeCompilerOptions(context, inheritContext, cflags);
            if ((cflags & CompileFlags.CO_DONT_IMPLY_DEDENT) != 0) {
                opts.DontImplyDedent = true;
            }

            SourceUnit sourceUnit = null; 
            switch (mode) {
                case "exec": sourceUnit = context.LanguageContext.CreateSnippet(text, filename, SourceCodeKind.Statements); break;
                case "eval": sourceUnit = context.LanguageContext.CreateSnippet(text, filename, SourceCodeKind.Expression); break;
                case "single": sourceUnit = context.LanguageContext.CreateSnippet(text, filename, SourceCodeKind.InteractiveCode); break;
            }

            return !astOnly ? 
                (object)FunctionCode.FromSourceUnit(sourceUnit, opts, true) :
                (object)_ast.BuildAst(context, sourceUnit, opts, mode);
        }

        private static string RemoveBom(string source) {
            // skip BOM (TODO: this is ugly workaround that is in fact not strictly correct, we need binary strings to handle it correctly)
            if (source.StartsWith("\u00ef\u00bb\u00bf", StringComparison.Ordinal)) {
                source = source.Substring(3, source.Length - 3);
            }
            return source;
        }

        public static PythonType classmethod {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(classmethod));
            }
        }

        public static int cmp(CodeContext/*!*/ context, object x, object y) {
            return PythonOps.Compare(context, x, y);
        }

        // having a cmp overload for double would be nice, but it breaks:
        // x = 1e66666
        // y = x/x
        // cmp(y,y)
        // which returns 0 because id(y) == id(y).  If we added a double overload
        // we lose object identity.

        public static int cmp(CodeContext/*!*/ context, int x, int y) {
            return Int32Ops.Compare(x, y);
        }

        public static int cmp(CodeContext/*!*/ context, [NotNull]BigInteger x, [NotNull]BigInteger y) {
            if ((object)x == (object)y) {
                return 0;
            }
            return BigIntegerOps.Compare(x, y);
        }

        public static int cmp(CodeContext/*!*/ context, double x, [NotNull]BigInteger y) {
            return -BigIntegerOps.Compare(y, x);
        }

        public static int cmp(CodeContext/*!*/ context, [NotNull]BigInteger x, double y) {
            return BigIntegerOps.Compare(x, y);
        }

        public static int cmp(CodeContext/*!*/ context, [NotNull]string x, [NotNull]string y) {
            if ((object)x != (object)y) {
                int res = string.CompareOrdinal(x, y);
                if (res >= 1) {
                    return 1;
                } else if (res <= -1) {
                    return -1;
                }
            }

            return 0;
        }

#if FEATURE_SORTKEY
        public static int cmp(CodeContext/*!*/ context, [NotNull]SortKey x, [NotNull]SortKey y) {
            return SortKey.Compare(x, y);
        }
#endif

        public static int cmp(CodeContext/*!*/ context, [NotNull]PythonTuple x, [NotNull]PythonTuple y) {
            if ((object)x == (object)y) {
                return 0;
            }
            return x.CompareTo(y);
        }

        public static PythonType complex {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(Complex));
            }
        }

        public static void delattr(CodeContext/*!*/ context, object o, string name) {
            PythonOps.DeleteAttr(context, o, name);
        }

        public static PythonType dict {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonDictionary));
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods")]
        public static List dir(CodeContext/*!*/ context) {
            List res = PythonOps.MakeListFromSequence(context.Dict.Keys);

            res.sort(context);
            return res;
        }

        public static List dir(CodeContext/*!*/ context, object o) {
            IList<object> ret = PythonOps.GetAttrNames(context, o);
            List lret = new List(ret);
            lret.sort(context);
            return lret;
        }

        public static object divmod(CodeContext/*!*/ context, object x, object y) {
            Debug.Assert(NotImplementedType.Value != null);

            return PythonContext.GetContext(context).DivMod(x, y);
        }

        public static PythonType enumerate {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(Enumerate));
            }
        }

        public static object eval(CodeContext/*!*/ context, FunctionCode code) {
            Debug.Assert(context != null);
            if (code == null) throw PythonOps.TypeError("eval() argument 1 must be string or code object");

            return eval(context, code, null);
        }

        public static object eval(CodeContext/*!*/ context, FunctionCode code, PythonDictionary globals) {
            Debug.Assert(context != null);
            if (code == null) throw PythonOps.TypeError("eval() argument 1 must be string or code object");

            return eval(context, code, globals, globals);
        }

        public static object eval(CodeContext/*!*/ context, FunctionCode code, PythonDictionary globals, object locals) {
            Debug.Assert(context != null);
            if (code == null) throw PythonOps.TypeError("eval() argument 1 must be string or code object");

            return code.Call(GetExecEvalScopeOptional(context, globals, locals, false));
        }

        internal static PythonDictionary GetAttrLocals(CodeContext/*!*/ context, object locals) {
            PythonDictionary attrLocals = null;
            if (locals == null) {
                if (context.IsTopLevel) {
                    attrLocals = context.Dict;
                }
            } else {
                attrLocals = locals as PythonDictionary ?? new PythonDictionary(new ObjectAttributesAdapter(context, locals));
            }
            return attrLocals;
        }

        [LightThrowing]
        public static object eval(CodeContext/*!*/ context, string expression) {
            Debug.Assert(context != null);
            if (expression == null) throw PythonOps.TypeError("eval() argument 1 must be string or code object");

            return eval(context, expression, globals(context), locals(context));
        }

        [LightThrowing]
        public static object eval(CodeContext/*!*/ context, string expression, PythonDictionary globals) {
            Debug.Assert(context != null);
            if (expression == null) throw PythonOps.TypeError("eval() argument 1 must be string or code object");

            return eval(context, expression, globals, globals);
        }

        [LightThrowing]
        public static object eval(CodeContext/*!*/ context, string expression, PythonDictionary globals, object locals) {
            Debug.Assert(context != null);
            if (expression == null) throw PythonOps.TypeError("eval() argument 1 must be string or code object");

            if (locals != null && PythonOps.IsMappingType(context, locals) == ScriptingRuntimeHelpers.False) {
                throw PythonOps.TypeError("locals must be mapping");
            }

            expression = RemoveBom(expression);
            var scope = GetExecEvalScopeOptional(context, globals, locals, false);
            var pythonContext = PythonContext.GetContext(context);

            // TODO: remove TrimStart
            var sourceUnit = pythonContext.CreateSnippet(expression.TrimStart(' ', '\t'), SourceCodeKind.Expression);
            var compilerOptions = GetRuntimeGeneratedCodeCompilerOptions(context, true, 0);
            compilerOptions.Module |= ModuleOptions.LightThrow;
            compilerOptions.Module &= ~ModuleOptions.ModuleBuiltins;
            var code = FunctionCode.FromSourceUnit(sourceUnit, compilerOptions, false);

            return code.Call(scope);
        }

        public static void execfile(CodeContext/*!*/ context, object/*!*/ filename) {
            execfile(context, filename, null, null);
        }

        public static void execfile(CodeContext/*!*/ context, object/*!*/ filename, object globals) {
            execfile(context, filename, globals, null);
        }

        public static void execfile(CodeContext/*!*/ context, object/*!*/ filename, object globals, object locals) {
            if (filename == null) {
                throw PythonOps.TypeError("execfile() argument 1 must be string, not None");
            }
            
            PythonDictionary g = globals as PythonDictionary;
            if (g == null && globals != null) {
                throw PythonOps.TypeError("execfile() arg 2 must be dictionary");
            }

            PythonDictionary l = locals as PythonDictionary;
            if (l == null && locals != null) {
                throw PythonOps.TypeError("execfile() arg 3 must be dictionary");
            }

            if (l == null) {
                l = g;
            }

            var execScope = GetExecEvalScopeOptional(context, g, l, true);
            string path = Converter.ConvertToString(filename);
            PythonContext pc = PythonContext.GetContext(context);
            if (!pc.DomainManager.Platform.FileExists(path)) {
                throw PythonOps.IOError("execfile: specified file doesn't exist");
            }

            SourceUnit sourceUnit = pc.CreateFileUnit(path, pc.DefaultEncoding, SourceCodeKind.Statements);
            FunctionCode code;

            var options = GetRuntimeGeneratedCodeCompilerOptions(context, true, 0);
            //always generate an unoptimized module since we run these against a dictionary namespace
            options.Module &= ~ModuleOptions.Optimized;

            try {
                code = FunctionCode.FromSourceUnit(sourceUnit, options, false);
            } catch (UnauthorizedAccessException x) {
                throw PythonOps.IOError(x);
            }

            // Do not attempt evaluation mode for execfile
            code.Call(execScope);
        }

        public static PythonType file {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonFile));
            }
        }

        public static string filter(CodeContext/*!*/ context, object function, [NotNull]string list) {
            if (function == null) return list;
            if (list == null) throw PythonOps.TypeError("NoneType is not iterable");

            StringBuilder sb = new StringBuilder();
            foreach (char c in list) {
                if (PythonOps.IsTrue(PythonCalls.Call(context, function, ScriptingRuntimeHelpers.CharToString(c)))) sb.Append(c);
            }

            return sb.ToString();
        }

        public static string filter(CodeContext/*!*/ context, object function, [NotNull]ExtensibleString list) {
            StringBuilder sb = new StringBuilder();
            IEnumerator e = PythonOps.GetEnumerator(list);
            while (e.MoveNext()) {
                object o = e.Current;
                object t = (function != null) ? PythonCalls.Call(context, function, o) : o;

                if (PythonOps.IsTrue(t)) {
                    sb.Append(Converter.ConvertToString(o));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Specialized version because enumerating tuples by Python's definition
        /// doesn't call __getitem__, but filter does!
        /// </summary>
        public static PythonTuple filter(CodeContext/*!*/ context, object function, [NotNull]PythonTuple tuple) {
            List<object> res = new List<object>(tuple.__len__());

            for (int i = 0; i < tuple.__len__(); i++) {
                object obj = tuple[i];
                object t = (function != null) ? PythonCalls.Call(context, function, obj) : obj;

                if (PythonOps.IsTrue(t)) {
                    res.Add(obj);
                }
            }

            return PythonTuple.MakeTuple(res.ToArray());
        }

        public static List filter(CodeContext/*!*/ context, object function, object list) {
            if (list == null) throw PythonOps.TypeError("NoneType is not iterable");
            List ret = new List();

            IEnumerator i = PythonOps.GetEnumerator(list);
            while (i.MoveNext()) {
                if (function == null) {
                    if (PythonOps.IsTrue(i.Current)) ret.AddNoLock(i.Current);
                } else {
                    if (PythonOps.IsTrue(PythonCalls.Call(context, function, i.Current))) ret.AddNoLock(i.Current);
                }
            }

            return ret;
        }

        public static PythonType @float {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(double));
            }
        }

        public static string format(CodeContext/*!*/ context, object argValue, [DefaultParameterValue("")]string formatSpec) {
            object res, formatMethod;
            OldInstance oi = argValue as OldInstance;
            if (oi != null && oi.TryGetBoundCustomMember(context, "__format__", out formatMethod)) {
                res = PythonOps.CallWithContext(context, formatMethod, formatSpec);
            } else {
                // call __format__ with the format spec (__format__ is defined on object, so this always succeeds)
                PythonTypeOps.TryInvokeBinaryOperator(
                    context,
                    argValue,
                    formatSpec,
                    "__format__",
                    out res);
            }

            string strRes = res as string;
            if (strRes == null) {
                throw PythonOps.TypeError("{0}.__format__ must return string or unicode, not {1}", PythonTypeOps.GetName(argValue), PythonTypeOps.GetName(res));
            }

            return strRes;
        }

        public static object getattr(CodeContext/*!*/ context, object o, string name) {
            return PythonOps.GetBoundAttr(context, o, name);
        }

        public static object getattr(CodeContext/*!*/ context, object o, string name, object def) {
            object ret;
            if (PythonOps.TryGetBoundAttr(context, o, name, out ret)) return ret;
            else return def;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods")]
        public static PythonDictionary globals(CodeContext/*!*/ context) {
            return context.ModuleContext.Globals;
        }

        public static bool hasattr(CodeContext/*!*/ context, object o, string name) {
            return PythonOps.HasAttr(context, o, name);
        }

        public static int hash(CodeContext/*!*/ context, object o) {
            return PythonContext.Hash(o);
        }

        public static int hash(CodeContext/*!*/ context, [NotNull]PythonTuple o) {
            return ((IStructuralEquatable)o).GetHashCode(PythonContext.GetContext(context).EqualityComparerNonGeneric);
        }

        // this is necessary because overload resolution selects the int form.
        public static int hash(CodeContext/*!*/ context, char o) {
            return PythonContext.Hash(o);
        }

        public static int hash(CodeContext/*!*/ context, int o) {
            return o;
        }

        public static int hash(CodeContext/*!*/ context, [NotNull]string o) {
            return o.GetHashCode();
        }

        // this is necessary because overload resolution will coerce extensible strings to strings.
        public static int hash(CodeContext/*!*/ context, [NotNull]ExtensibleString o) {
            return hash(context, (object)o);
        }

        public static int hash(CodeContext/*!*/ context, [NotNull]BigInteger o) {
            return BigIntegerOps.__hash__(o);
        }

        public static int hash(CodeContext/*!*/ context, [NotNull]Extensible<BigInteger> o) {
            return hash(context, (object)o);
        }

        public static int hash(CodeContext/*!*/ context, double o) {
            return DoubleOps.__hash__(o);
        }

        public static void help(CodeContext/*!*/ context, object o) {
            StringBuilder doc = new StringBuilder();
            List<object> doced = new List<object>();  // document things only once

            help(context, doced, doc, 0, o);

            if (doc.Length == 0) {
                if (!(o is string)) {
                    help(context, DynamicHelpers.GetPythonType(o));
                    return;
                }
                doc.Append("no documentation found for ");
                doc.Append(PythonOps.Repr(context, o));
            }

            string[] strings = doc.ToString().Split('\n');
            for (int i = 0; i < strings.Length; i++) {
                /* should read only a key, not a line, but we don't seem
                 * to have a way to do that...
                if ((i % Console.WindowHeight) == 0) {
                    Ops.Print(context.SystemState, "-- More --");
                    Ops.ReadLineFromSrc(context.SystemState);
                }*/
                PythonOps.Print(context, strings[i]);
            }
        }

        private static void help(CodeContext/*!*/ context, List<object>/*!*/ doced, StringBuilder/*!*/ doc, int indent, object obj) {
            PythonType type;
            BuiltinFunction builtinFunction;
            PythonFunction function;
            BuiltinMethodDescriptor methodDesc;
            Method method;
            string strVal;
            PythonModule pyModule;
            OldClass oldClass;

            if (doced.Contains(obj)) return;  // document things only once
            doced.Add(obj);

            if ((strVal = obj as string) != null) {
                if (indent != 0) return;

                // try and find things that string could refer to,
                // then call help on them.
                foreach (object module in PythonContext.GetContext(context).SystemStateModules.Values) {
                    IList<object> attrs = PythonOps.GetAttrNames(context, module);
                    List candidates = new List();
                    foreach (string s in attrs) {
                        if (s == strVal) {
                            object modVal;
                            if (!PythonOps.TryGetBoundAttr(context, module, strVal, out modVal))
                                continue;

                            candidates.append(modVal);
                        }
                    }

                    // favor types, then built-in functions, then python functions,
                    // and then only display help for one.
                    type = null;
                    builtinFunction = null;
                    function = null;
                    for (int i = 0; i < candidates.__len__(); i++) {
                        if ((type = candidates[i] as PythonType) != null) {
                            break;
                        }

                        if (builtinFunction == null && (builtinFunction = candidates[i] as BuiltinFunction) != null)
                            continue;

                        if (function == null && (function = candidates[i] as PythonFunction) != null)
                            continue;
                    }

                    if (type != null) help(context, doced, doc, indent, type);
                    else if (builtinFunction != null) help(context, doced, doc, indent, builtinFunction);
                    else if (function != null) help(context, doced, doc, indent, function);
                }
            } else if ((type = obj as PythonType) != null) {
                // find all the functions, and display their 
                // documentation                
                if (indent == 0) {
                    doc.AppendFormat("Help on {0} in module {1}\n\n", type.Name, PythonOps.GetBoundAttr(context, type, "__module__"));
                }

                PythonTypeSlot dts;
                if (type.TryResolveSlot(context, "__doc__", out dts)) {
                    object docText;
                    if (dts.TryGetValue(context, null, type, out docText) && docText != null)
                        AppendMultiLine(doc, docText.ToString() + Environment.NewLine, indent);
                    AppendIndent(doc, indent);
                    doc.AppendLine("Data and other attributes defined here:");
                    AppendIndent(doc, indent);
                    doc.AppendLine();
                }

                List names = type.GetMemberNames(context);
                names.sort(context);

                foreach (string name in names) {
                    if (name == "__class__") continue;

                    PythonTypeSlot value;
                    object val;
                    if (type.TryLookupSlot(context, name, out value) &&
                        value.TryGetValue(context, null, type, out val)) {
                        help(context, doced, doc, indent + 1, val);
                    }
                }
            } else if ((methodDesc = obj as BuiltinMethodDescriptor) != null) {
                if (indent == 0) doc.AppendFormat("Help on method-descriptor {0}\n\n", methodDesc.__name__);
                AppendIndent(doc, indent);
                doc.Append(methodDesc.__name__);
                doc.Append("(...)\n");

                AppendMultiLine(doc, methodDesc.__doc__, indent + 1);
            } else if ((builtinFunction = obj as BuiltinFunction) != null) {
                if (indent == 0) doc.AppendFormat("Help on built-in function {0}\n\n", builtinFunction.Name);
                AppendIndent(doc, indent);
                doc.Append(builtinFunction.Name);
                doc.Append("(...)\n");

                AppendMultiLine(doc, builtinFunction.__doc__, indent + 1);
            } else if ((function = obj as PythonFunction) != null) {
                if (indent == 0) doc.AppendFormat("Help on function {0} in module {1}:\n\n", function.__name__, function.__module__);

                AppendIndent(doc, indent);
                doc.Append(function.GetSignatureString());
                string pfDoc = Converter.ConvertToString(function.__doc__);
                if (!String.IsNullOrEmpty(pfDoc)) {
                    AppendMultiLine(doc, pfDoc, indent);
                }
            } else if ((method = obj as Method) != null && ((function = method.im_func as PythonFunction) != null)) {
                if (indent == 0) doc.AppendFormat("Help on method {0} in module {1}:\n\n", function.__name__, function.__module__);

                AppendIndent(doc, indent);
                doc.Append(function.GetSignatureString());

                if (method.im_self == null) {
                    doc.AppendFormat(" unbound {0} method\n", PythonOps.ToString(method.im_class));
                } else {
                    doc.AppendFormat(" method of {0} instance\n", PythonOps.ToString(method.im_class));
                }

                string pfDoc = Converter.ConvertToString(function.__doc__);
                if (!String.IsNullOrEmpty(pfDoc)) {
                    AppendMultiLine(doc, pfDoc, indent);
                }
            } else if ((pyModule = obj as PythonModule) != null) {
                foreach (string name in pyModule.__dict__.Keys) {
                    if (name == "__class__" || name == "__builtins__") continue;

                    object value;
                    if (pyModule.__dict__.TryGetValue(name, out value)) {
                        help(context, doced, doc, indent + 1, value);
                    }
                }
            } else if ((oldClass = obj as OldClass) != null) {
                if (indent == 0) {
                    doc.AppendFormat("Help on {0} in module {1}\n\n", oldClass.Name, PythonOps.GetBoundAttr(context, oldClass, "__module__"));
                }

                object docText;
                if (oldClass.TryLookupSlot("__doc__", out docText) && docText != null) {
                    AppendMultiLine(doc, docText.ToString() + Environment.NewLine, indent);
                    AppendIndent(doc, indent);
                    doc.AppendLine("Data and other attributes defined here:");
                    AppendIndent(doc, indent);
                    doc.AppendLine();
                }

                IList<object> names = ((IPythonMembersList)oldClass).GetMemberNames(context);
                List sortNames = new List(names);
                sortNames.sort(context);
                names = sortNames;
                foreach (string name in names) {
                    if (name == "__class__") continue;

                    object value;

                    if (oldClass.TryLookupSlot(name, out value))
                        help(context, doced, doc, indent + 1, value);
                }
            }
        }

        private static void AppendMultiLine(StringBuilder doc, string multiline, int indent) {
            string[] docs = multiline.Split('\n');
            for (int i = 0; i < docs.Length; i++) {
                AppendIndent(doc, indent + 1);
                doc.Append(docs[i]);
                doc.Append('\n');
            }
        }

        private static void AppendIndent(StringBuilder doc, int indent) {
            doc.Append(" |  ");
            for (int i = 0; i < indent; i++) doc.Append("    ");
        }

        //??? type this to string
        public static object hex(object o) {
            return PythonOps.Hex(o);
        }

        public static object id(object o) {
            long res = PythonOps.Id(o);
            if (PythonOps.Id(o) <= Int32.MaxValue) {
                return (int)res;
            }
            return (BigInteger)res;
        }

        [LightThrowing]
        public static object input(CodeContext/*!*/ context) {
            return input(context, null);
        }

        [LightThrowing]
        public static object input(CodeContext/*!*/ context, object prompt) {
            return eval(context, raw_input(context, prompt));
        }

        public static PythonType @int {
            get {
                return TypeCache.BigInteger;
            }
        }

        public static string intern(object o) {
            string s = o as string;
            if (s == null) {
                throw PythonOps.TypeError("intern: argument must be string");
            }
            return string.Intern(s);
        }

        public static bool isinstance(object o, [NotNull]PythonType typeinfo) {
            return PythonOps.IsInstance(o, typeinfo);
        }

        public static bool isinstance(CodeContext context, object o, [NotNull]PythonTuple typeinfo) {
            return PythonOps.IsInstance(context, o, typeinfo);
        }

        public static bool isinstance(CodeContext context, object o, object typeinfo) {
            return PythonOps.IsInstance(context, o, typeinfo);
        }

        public static bool issubclass(CodeContext context, [NotNull]OldClass c, object typeinfo) {
            return PythonOps.IsSubClass(context, c.TypeObject, typeinfo);
        }

        public static bool issubclass(CodeContext context, [NotNull]PythonType c, object typeinfo) {
            return PythonOps.IsSubClass(context, c, typeinfo);
        }

        public static bool issubclass(CodeContext context, [NotNull]PythonType c, [NotNull]PythonType typeinfo) {
            return PythonOps.IsSubClass(c, typeinfo);
        }

        [LightThrowing]
        public static object issubclass(CodeContext/*!*/ context, object o, object typeinfo) {
            PythonTuple pt = typeinfo as PythonTuple;
            if (pt != null) {
                // Recursively inspect nested tuple(s)
                foreach (object subTypeInfo in pt) {
                    try {
                        PythonOps.FunctionPushFrame(PythonContext.GetContext(context));
                        var res = issubclass(context, o, subTypeInfo);
                        if (res == ScriptingRuntimeHelpers.True) {
                            return ScriptingRuntimeHelpers.True;
                        } else if (LightExceptions.IsLightException(res)) {
                            return res;
                        }
                    } finally {
                        PythonOps.FunctionPopFrame();
                    }
                }
                return ScriptingRuntimeHelpers.False;
            }

            object bases;
            PythonTuple tupleBases;

            if (!PythonOps.TryGetBoundAttr(o, "__bases__", out bases) || (tupleBases = bases as PythonTuple) == null) {
                return LightExceptions.Throw(PythonOps.TypeError("issubclass() arg 1 must be a class"));
            }

            if (o == typeinfo) {
                return ScriptingRuntimeHelpers.True;
            }
            foreach (object baseCls in tupleBases) {
                PythonType pyType;
                OldClass oc;

                if (baseCls == typeinfo) {
                    return ScriptingRuntimeHelpers.True;
                } else if ((pyType = baseCls as PythonType) != null) {
                    if (issubclass(context, pyType, typeinfo)) {
                        return ScriptingRuntimeHelpers.True;
                    }
                } else if ((oc = baseCls as OldClass) != null) {
                    if (issubclass(context, oc, typeinfo)) {
                        return ScriptingRuntimeHelpers.True;
                    }
                } else if (hasattr(context, baseCls, "__bases__")) {
                    var res = issubclass(context, baseCls, typeinfo);
                    if (res == ScriptingRuntimeHelpers.True) {
                        return ScriptingRuntimeHelpers.True;
                    } else if (LightExceptions.IsLightException(res)) {
                        return res;
                    }
                }
            }

            return ScriptingRuntimeHelpers.False;
        }

        public static object iter(CodeContext/*!*/ context, object o) {
            return PythonOps.GetEnumeratorObject(context, o);
        }

        public static object iter(CodeContext/*!*/ context, object func, object sentinel) {
            if (!PythonOps.IsCallable(context, func)) {
                throw PythonOps.TypeError("iter(v, w): v must be callable");
            }
            return new SentinelIterator(context, func, sentinel);
        }

        public static BigInteger len([NotNull]Range r) {
            return r.__len__();
        }

        public static int len([NotNull]string/*!*/ str) {
            return str.Length;
        }

        public static int len([NotNull]ExtensibleString/*!*/ str) {
            return str.__len__();
        }

        public static int len([NotNull]List/*!*/ list) {
            return list.__len__();
        }

        public static int len([NotNull]PythonTuple/*!*/ tuple) {
            return tuple.__len__();
        }

        public static int len([NotNull]PythonDictionary/*!*/ dict) {
            return dict.__len__();
        }

        public static int len([NotNull]ICollection/*!*/ collection) {
            return collection.Count;
        }

        public static int len(object o) {
            return PythonOps.Length(o);
        }

        public static PythonType set {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(SetCollection));
            }
        }

        public static PythonType frozenset {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(FrozenSetCollection));
            }
        }

        public static PythonType list {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(List));
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods")]
        public static object locals(CodeContext/*!*/ context) {
            PythonDictionary dict = context.Dict;
            ObjectAttributesAdapter adapter = dict._storage as ObjectAttributesAdapter;
            if (adapter != null) {
                // we've wrapped Locals in an PythonDictionary, give the user back the
                // original object.
                return adapter.Backing;
            }

            return context.Dict;
        }

        public static PythonType @long {
            get {
                return TypeCache.BigInteger;
            }
        }

        public static PythonType memoryview {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(MemoryView));
            }
        }

        private static CallSite<Func<CallSite, CodeContext, T, T1, object>> MakeMapSite<T, T1>(CodeContext/*!*/ context) {
            return CallSite<Func<CallSite, CodeContext, T, T1, object>>.Create(
                PythonContext.GetContext(context).InvokeOne
            );
        }

        public static List map(CodeContext/*!*/ context, object func, [NotNull]IEnumerable enumerator) {
            IEnumerator en = PythonOps.GetEnumerator(enumerator);
            
            List ret = new List();            
            CallSite<Func<CallSite, CodeContext, object, object, object>> mapSite = null;

            if (func != null) {
                mapSite = MakeMapSite<object, object>(context);
            }

            while (en.MoveNext()) {
                if (func == null) {
                    ret.AddNoLock(en.Current);
                } else {
                    ret.AddNoLock(mapSite.Target(mapSite, context, func, en.Current));
                }
            }
        
            return ret;
        }

        public static List map(
            CodeContext/*!*/ context,
            SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object, object>>> storage,
            object func,
            [NotNull]string enumerator
        ) {
            CallSite<Func<CallSite, CodeContext, object, object, object>> mapSite;
            if (storage.Data == null && func != null) {
                storage.Data = MakeMapSite<object, object>(context);
            }
            mapSite = storage.Data;

            List ret = new List(enumerator.Length);
            foreach (char o in enumerator) {
                if (func == null) {
                    ret.AddNoLock(ScriptingRuntimeHelpers.CharToString(o));
                } else {
                    ret.AddNoLock(mapSite.Target(mapSite, context, func, ScriptingRuntimeHelpers.CharToString(o)));
                }
            }

            return ret;
        }

        public static List map(
            CodeContext/*!*/ context,
            SiteLocalStorage<CallSite<Func<CallSite, CodeContext, PythonType, object, object>>> storage,
            [NotNull]PythonType/*!*/ func,
            [NotNull]string enumerator
        ) {
            CallSite<Func<CallSite, CodeContext, PythonType, string, object>> mapSite = MakeMapSite<PythonType, string>(context);

            List ret = new List(enumerator.Length);
            foreach (char o in enumerator) {
                ret.AddNoLock(mapSite.Target(mapSite, context, func, ScriptingRuntimeHelpers.CharToString(o)));
            }
            return ret;
        }

        public static List map(
            CodeContext/*!*/ context,
            SiteLocalStorage<CallSite<Func<CallSite, CodeContext, PythonType, object, object>>> storage,
            [NotNull]PythonType/*!*/ func,
            [NotNull]IEnumerable enumerator
        ) {
            CallSite<Func<CallSite, CodeContext, PythonType, object, object>> mapSite;
            if (storage.Data == null) {
                storage.Data = MakeMapSite<PythonType, object>(context);
            }
            mapSite = storage.Data;

            IEnumerator en = PythonOps.GetEnumerator(enumerator);
            List ret = new List();
            while (en.MoveNext()) {
                ret.AddNoLock(mapSite.Target(mapSite, context, func, en.Current));
            }
            return ret;
        }

        public static List map(
            CodeContext/*!*/ context,
            SiteLocalStorage<CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object>>> storage,
            [NotNull]BuiltinFunction/*!*/ func,
            [NotNull]string enumerator
        ) {
            CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object>> mapSite;
            if (storage.Data == null) {
                storage.Data = MakeMapSite<BuiltinFunction, object>(context);
            }
            mapSite = storage.Data;

            List ret = new List(enumerator.Length);
            foreach (char o in enumerator) {
                ret.AddNoLock(mapSite.Target(mapSite, context, func, ScriptingRuntimeHelpers.CharToString(o)));
            }
            return ret;
        }

        public static List map(
            CodeContext/*!*/ context,
            SiteLocalStorage<CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object>>> storage,
            [NotNull]BuiltinFunction/*!*/ func,
            [NotNull]IEnumerable enumerator
        ) {
            CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object>> mapSite;
            if (storage.Data == null) {
                storage.Data = MakeMapSite<BuiltinFunction, object>(context);
            }
            mapSite = storage.Data;

            IEnumerator en = PythonOps.GetEnumerator(enumerator);
            List ret = new List();
            while (en.MoveNext()) {
                ret.AddNoLock(mapSite.Target(mapSite, context, func, en.Current));
            }
            return ret;
        }

        public static List map(
            CodeContext/*!*/ context,
            SiteLocalStorage<CallSite<Func<CallSite, CodeContext, PythonFunction, object, object>>> storage,
            [NotNull]PythonFunction/*!*/ func,
            [NotNull]IList enumerator
        ) {
            CallSite<Func<CallSite, CodeContext, PythonFunction, object, object>> mapSite;
            if (storage.Data == null) {
                storage.Data = MakeMapSite<PythonFunction, object>(context);
            }
            mapSite = storage.Data;

            IEnumerator en = PythonOps.GetEnumerator(enumerator);
            List ret = new List(enumerator.Count);
            while (en.MoveNext()) {
                ret.AddNoLock(mapSite.Target(mapSite, context, func, en.Current));
            }
            return ret;
        }

        public static List map(CodeContext/*!*/ context, params object[] param) {
            if (param == null || param.Length < 2) {
                throw PythonOps.TypeError("at least 2 arguments required to map");
            }
            List ret = new List();
            object func = param[0];

            IEnumerator[] enums = new IEnumerator[param.Length - 1];
            for (int i = 0; i < enums.Length; i++) {
                enums[i] = PythonOps.GetEnumerator(param[i + 1]);
            }

            object[] args = new object[enums.Length];
            while (true) {
                bool done = true;
                for (int i = 0; i < enums.Length; i++) {
                    if (enums[i].MoveNext()) {
                        args[i] = enums[i].Current;
                        done = false;
                    } else {
                        args[i] = null;
                    }
                }
                if (done) {
                    return ret;
                }
                if (func != null) {
                    // splat call w/ args, can't use site here yet...
                    ret.AddNoLock(PythonCalls.Call(context, func, args));
                } else if (args.Length == 1) {
                    ret.AddNoLock(args[0]);
                } else {
                    ret.AddNoLock(PythonTuple.MakeTuple(args));
                    args = new object[enums.Length];    // Tuple does not copy the array, allocate new one.
                }
            }
        }

        public static object max(CodeContext/*!*/ context, object x) {
            IEnumerator i = PythonOps.GetEnumerator(x);
            if (!i.MoveNext())
                throw PythonOps.ValueError("max() arg is an empty sequence");
            object ret = i.Current;
            PythonContext pc = PythonContext.GetContext(context);
            while (i.MoveNext()) {
                if (pc.GreaterThan(i.Current, ret)) ret = i.Current;
            }
            return ret;
        }

        public static object max(CodeContext/*!*/ context, object x, object y) {
            return PythonContext.GetContext(context).GreaterThan(x, y) ? x : y;
        }

        public static object max(CodeContext/*!*/ context, params object[] args) {
            if (args.Length > 0) {
                object ret = args[0];
                if (args.Length == 1) {
                    return max(context, ret);
                }

                PythonContext pc = PythonContext.GetContext(context);
                for (int i = 1; i < args.Length; i++) {
                    if (pc.GreaterThan(args[i], ret)) {
                        ret = args[i];
                    }
                }
                return ret;
            } else {
                throw PythonOps.TypeError("max expecting 1 arguments, got 0");
            }

        }

        public static object max(CodeContext/*!*/ context, object x, [ParamDictionary]IDictionary<object, object> dict) {
            IEnumerator i = PythonOps.GetEnumerator(x);
            if (!i.MoveNext())
                throw PythonOps.ValueError(" max() arg is an empty sequence");
            object method = GetMaxKwArg(dict);
            object ret = i.Current;
            object retValue = PythonCalls.Call(context, method, i.Current);
            PythonContext pc = PythonContext.GetContext(context);
            while (i.MoveNext()) {
                object tmpRetValue = PythonCalls.Call(context, method, i.Current);
                if (pc.GreaterThan(tmpRetValue, retValue)) {
                    ret = i.Current;
                    retValue = tmpRetValue;
                }
            }
            return ret;
        }

        public static object max(CodeContext/*!*/ context, object x, object y, [ParamDictionary] IDictionary<object, object> dict) {
            object method = GetMaxKwArg(dict);
            PythonContext pc = PythonContext.GetContext(context);
            return pc.GreaterThan(PythonCalls.Call(context, method, x), PythonCalls.Call(context, method, y)) ? x : y;
        }

        public static object max(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            if (args.Length > 0) {
                int retIndex = 0;
                if (args.Length == 1) {
                    return max(context, args[retIndex], dict);
                }
                object method = GetMaxKwArg(dict);
                object retValue = PythonCalls.Call(context, method, args[retIndex]);
                PythonContext pc = PythonContext.GetContext(context);
                for (int i = 1; i < args.Length; i++) {
                    object tmpRetValue = PythonCalls.Call(context, method, args[i]);
                    if (pc.GreaterThan(tmpRetValue, retValue)) {
                        retIndex = i;
                        retValue = tmpRetValue;
                    }
                }
                return args[retIndex];
            } else {
                throw PythonOps.TypeError("max expecting 1 arguments, got 0");
            }
        }

        private static object GetMaxKwArg(IDictionary<object, object> dict) {
            if (dict.Count != 1)
                throw PythonOps.TypeError(" max() should have only 1 keyword argument, but got {0} keyword arguments", dict.Count);

            return VerifyKeys("max", dict);
        }

        public static object min(CodeContext/*!*/ context, object x) {
            IEnumerator i = PythonOps.GetEnumerator(x);
            if (!i.MoveNext()) {
                throw PythonOps.ValueError("empty sequence");
            }
            object ret = i.Current;
            PythonContext pc = PythonContext.GetContext(context);
            while (i.MoveNext()) {
                if (pc.LessThan(i.Current, ret)) ret = i.Current;
            }
            return ret;
        }

        public static object min(CodeContext/*!*/ context, object x, object y) {
            return PythonContext.GetContext(context).LessThan(x, y) ? x : y;
        }

        public static object min(CodeContext/*!*/ context, params object[] args) {
            if (args.Length > 0) {
                object ret = args[0];
                if (args.Length == 1) {
                    return min(context, ret);
                }

                PythonContext pc = PythonContext.GetContext(context);
                for (int i = 1; i < args.Length; i++) {
                    if (pc.LessThan(args[i], ret)) ret = args[i];
                }
                return ret;
            } else {
                throw PythonOps.TypeError("min expecting 1 arguments, got 0");
            }
        }

        public static object min(CodeContext/*!*/ context, object x, [ParamDictionary]IDictionary<object, object> dict) {
            IEnumerator i = PythonOps.GetEnumerator(x);
            if (!i.MoveNext())
                throw PythonOps.ValueError(" min() arg is an empty sequence");
            object method = GetMinKwArg(dict);
            object ret = i.Current;
            object retValue = PythonCalls.Call(context, method, i.Current);
            PythonContext pc = PythonContext.GetContext(context);
            while (i.MoveNext()) {
                object tmpRetValue = PythonCalls.Call(context, method, i.Current);

                if (pc.LessThan(tmpRetValue, retValue)) {
                    ret = i.Current;
                    retValue = tmpRetValue;
                }
            }
            return ret;
        }

        public static object min(CodeContext/*!*/ context, object x, object y, [ParamDictionary]IDictionary<object, object> dict) {
            object method = GetMinKwArg(dict);
            return PythonContext.GetContext(context).LessThan(PythonCalls.Call(context, method, x), PythonCalls.Call(context, method, y)) ? x : y;
        }

        public static object min(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            if (args.Length > 0) {
                int retIndex = 0;
                if (args.Length == 1) {
                    return min(context, args[retIndex], dict);
                }
                object method = GetMinKwArg(dict);
                object retValue = PythonCalls.Call(context, method, args[retIndex]);
                PythonContext pc = PythonContext.GetContext(context);

                for (int i = 1; i < args.Length; i++) {
                    object tmpRetValue = PythonCalls.Call(context, method, args[i]);

                    if (pc.LessThan(tmpRetValue, retValue)) {
                        retIndex = i;
                        retValue = tmpRetValue;
                    }
                }
                return args[retIndex];
            } else {
                throw PythonOps.TypeError("min expecting 1 arguments, got 0");
            }
        }

        private static object GetMinKwArg([ParamDictionary]IDictionary<object, object> dict) {
            if (dict.Count != 1)
                throw PythonOps.TypeError(" min() should have only 1 keyword argument, but got {0} keyword arguments", dict.Count);

            return VerifyKeys("min", dict);
        }

        private static object VerifyKeys(string name, IDictionary<object, object> dict) {
            object value;
            if (!dict.TryGetValue("key", out value)) {
                ICollection<object> keys = dict.Keys;
                IEnumerator<object> en = keys.GetEnumerator();
                if (en.MoveNext()) {
                    throw PythonOps.TypeError(" {1}() got an unexpected keyword argument ({0})", en.Current, name);
                }
            }
            return value;
        }

        public static object next(IEnumerator iter) {
            if (iter.MoveNext()) {
                return iter.Current;
            } else {
                throw PythonOps.StopIteration();
            }
        }

        public static object next(IEnumerator iter, object defaultVal) {
            if (iter.MoveNext()) {
                return iter.Current;
            } else {
                return defaultVal;
            }
        }

        [LightThrowing]
        public static object next(PythonGenerator gen) {
            return gen.next();
        }

        [LightThrowing]
        public static object next(PythonGenerator gen, object defaultVal) {
            object res = gen.next();
            Exception exc = LightExceptions.GetLightException(res);
            
            if (exc != null && exc is StopIterationException) {
                return defaultVal;
            }

            return res;
        }

        public static object next(CodeContext/*!*/ context, object iter) {
            return PythonOps.Invoke(context, iter, "next");
        }

        public static object next(CodeContext/*!*/ context, object iter, object defaultVal) {
            try {
                return PythonOps.Invoke(context, iter, "next");
            } catch (StopIterationException) {
                return defaultVal;
            }
        }

        public static PythonType @object {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(object));
            }
        }

        public static object oct(object o) {
            return PythonOps.Oct(o);
        }

        /// <summary>
        /// Opens a file and returns a new file object.
        /// 
        /// name -> the name of the file to open.
        /// mode -> the mode to open the file (r for reading, w for writing, a for appending, default is r).
        /// bufsize -> the size of the buffer to be used (&lt;= 0 indicates to use the default size)
        /// </summary>
        public static PythonFile open(CodeContext context, string name, [DefaultParameterValue("r")]string mode, [DefaultParameterValue(-1)]int buffering) {
            PythonFile res = new PythonFile(context);
            res.__init__(context, name, mode, buffering);
            return res;            
        }

        /// <summary>
        /// Creates a new Python file object from a .NET stream object.
        /// 
        /// stream -> the stream to wrap in a file object.
        /// </summary>
        public static PythonFile open(CodeContext context, [NotNull]Stream stream) {
            PythonFile res = new PythonFile(context);
            res.__init__(context, stream);
            return res;            
        }

        public static int ord(object value) {
            if (value is char) {
                return (char)value;
            } 

            string stringValue = value as string;
            if (stringValue == null) {
                ExtensibleString es = value as ExtensibleString;
                if (es != null) stringValue = es.Value;
            }
            
            if (stringValue != null) {
                if (stringValue.Length != 1) {
                    throw PythonOps.TypeError("expected a character, but string of length {0} found", stringValue.Length);
                }
                return stringValue[0];
            }

            IList<byte> bytes = value as IList<byte>;
            if (bytes != null) {
                if (bytes.Count != 1) {
                    throw PythonOps.TypeError("expected a character, but string of length {0} found", bytes.Count);
                }

                return bytes[0];
            }
                
            throw PythonOps.TypeError("expected a character, but {0} found", PythonTypeOps.GetName(value));
        }

        public static object pow(CodeContext/*!*/ context, object x, object y) {
            return PythonContext.GetContext(context).Operation(PythonOperationKind.Power, x, y);
        }

        public static object pow(CodeContext/*!*/ context, object x, object y, object z) {
            try {
                return PythonOps.PowerMod(context, x, y, z);
            } catch (DivideByZeroException) {
                throw PythonOps.ValueError("3rd argument cannot be 0");
            }
        }

        public static void print(CodeContext/*!*/ context, params object[] args) {
            print(context, " ", "\n", null, args);
        }

        public static void print(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args) {
            object sep = AttrCollectionPop(kwargs, "sep", " ");
            if (sep != null && !(sep is string)) {
                throw PythonOps.TypeError("sep must be None or str, not {0}", PythonTypeOps.GetName(sep));
            }

            object end = AttrCollectionPop(kwargs, "end", "\n");
            if (sep != null && !(sep is string)) {
                throw PythonOps.TypeError("end must be None or str, not {0}", PythonTypeOps.GetName(end));
            }

            object file = AttrCollectionPop(kwargs, "file", null);

            if (kwargs.Count != 0) {
                throw PythonOps.TypeError(
                    "'{0}' is an invalid keyword argument for this function", 
                    new List<object>(kwargs.Keys)[0]
                );
            }

            print(context, (string)sep ?? " ", (string)end ?? "\n", file, args);
        }

        private static object AttrCollectionPop(IDictionary<object, object> kwargs, string name, object defaultValue) {
            object res;
            if (kwargs.TryGetValue(name, out res)) {
                kwargs.Remove(name);
            } else {
                res = defaultValue;
            }
            return res;
        }

        private static void print(CodeContext/*!*/ context, string/*!*/ sep, string/*!*/ end, object file, object[]/*!*/ args) {
            PythonContext pc = PythonContext.GetContext(context);

            if (file == null) {
                file = pc.SystemStandardOut;
            }
            if (file == null) {
                throw PythonOps.RuntimeError("lost sys.std_out");
            }

            if (args == null) {
                // passing None to print passes a null object array
                args = new object[1];
            }

            PythonFile pf = file as PythonFile;

            for (int i = 0; i < args.Length; i++) {
                string text = PythonOps.ToString(context, args[i]);

                if (pf != null) {
                    pf.write(text);
                } else {
                    pc.WriteCallSite.Target(
                        pc.WriteCallSite,
                        context,
                        PythonOps.GetBoundAttr(context, file, "write"),
                        text
                    );
                }

                if (i != args.Length - 1) {
                    if (pf != null) {
                        pf.write(sep);
                    } else {
                        pc.WriteCallSite.Target(
                            pc.WriteCallSite,
                            context,
                            PythonOps.GetBoundAttr(context, file, "write"),
                            sep
                        );
                    }
                }
            }

            if (pf != null) {
                pf.write(end);
            } else {
                pc.WriteCallSite.Target(
                    pc.WriteCallSite,
                    context,
                    PythonOps.GetBoundAttr(context, file, "write"),
                    end
                );
            }
        }

        public static PythonType property {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonProperty));
            }
        }

        public static PythonType range {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(Range));
            }
        }

        public static string raw_input(CodeContext/*!*/ context) {
            return raw_input(context, null);
        }

        public static string raw_input(CodeContext/*!*/ context, object prompt) {
            var pc = PythonContext.GetContext(context);
            var readlineModule = pc.GetModuleByName("readline");
            string line;
            if (readlineModule != null) {
                var rl = readlineModule.GetAttributeNoThrow(context, "rl");
                line = PythonOps.Invoke(context, rl, "readline", new [] {prompt}) as string;
            } else {
                if (prompt != null) {
                    PythonOps.PrintNoNewline(context, prompt);
                }
                line = PythonOps.ReadLineFromSrc(context, PythonContext.GetContext(context).SystemStandardIn) as string;
            }

            if (line != null && line.EndsWith("\n")) return line.Substring(0, line.Length - 1);
            return line;
        }

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

        [ThreadStatic]
        private static List<PythonModule> _reloadStack;

        public static object reload(CodeContext/*!*/ context, PythonModule/*!*/ module) {
            if (module == null) {
                throw PythonOps.TypeError("unexpected type: NoneType");
            }

            if (_reloadStack == null) {
                Interlocked.CompareExchange(ref _reloadStack, new List<PythonModule>(), null);
            }

            // if a module attempts to reload it's self while already reloading it's 
            // self we just return the original module.
            if (_reloadStack.Contains(module)) {
                return module;
            }

            _reloadStack.Add(module);
            try {
                return Importer.ReloadModule(context, module);
            } finally {
                _reloadStack.RemoveAt(_reloadStack.Count - 1);
            }
        }

        public static object repr(CodeContext/*!*/ context, object o) {
            object res = PythonOps.Repr(context, o);

            if (!(res is String) && !(res is ExtensibleString)) {
                throw PythonOps.TypeError("__repr__ returned non-string (type {0})", PythonOps.GetPythonTypeName(o));
            }

            return res;
        }

        public static PythonType reversed {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(ReversedEnumerator));
            }
        }
        
        public static double round(double number) {
            return MathUtils.RoundAwayFromZero(number);
        }

        public static double round(double number, int ndigits) {
            return PythonOps.CheckMath(number, MathUtils.RoundAwayFromZero(number, ndigits));
        }

        public static double round(double number, BigInteger ndigits) {
            int n;
            if (ndigits.AsInt32(out n)) {
                return round(number, n);
            }

            return ndigits > 0 ? number : 0.0;
        }

        public static double round(double number, double ndigits) {
            throw PythonOps.TypeError("'float' object cannot be interpreted as an index");
        }

        public static void setattr(CodeContext/*!*/ context, object o, string name, object val) {
            PythonOps.SetAttr(context, o, name, val);
        }

        public static PythonType slice {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(Slice));
            }
        }

        public static List sorted(CodeContext/*!*/ context, object iterable) {
            return sorted(context, iterable, null, null, false);
        }

        public static List sorted(CodeContext/*!*/ context, object iterable, object cmp) {
            return sorted(context, iterable, cmp, null, false);
        }

        public static List sorted(CodeContext/*!*/ context, object iterable, object cmp, object key) {
            return sorted(context, iterable, cmp, key, false);
        }

        public static List sorted(CodeContext/*!*/ context,
            [DefaultParameterValue(null)] object iterable,
            [DefaultParameterValue(null)]object cmp,
            [DefaultParameterValue(null)]object key,
            [DefaultParameterValue(false)]bool reverse) {

            IEnumerator iter = PythonOps.GetEnumerator(iterable);
            List l = PythonOps.MakeEmptyList(10);
            while (iter.MoveNext()) {
                l.AddNoLock(iter.Current);
            }
            l.sort(context, cmp, key, reverse);
            return l;
        }

        public static PythonType staticmethod {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(staticmethod));
            }
        }

        public static object sum(CodeContext/*!*/ context, object sequence) {
            return sum(context, sequence, 0);
        }

        public static object sum(CodeContext/*!*/ context, [NotNull]List sequence) {
            return sum(context, sequence, 0);
        }

        public static object sum(CodeContext/*!*/ context, [NotNull]PythonTuple sequence) {
            return sum(context, sequence, 0);
        }

        public static object sum(CodeContext/*!*/ context, object sequence, object start) {
            IEnumerator i = PythonOps.GetEnumerator(sequence);

            if (start is string) {
                throw PythonOps.TypeError("Cannot sum strings, use '{0}'.join(seq)", start);
            }

            var sumState = new SumState(context.LanguageContext, start);
            while (i.MoveNext()) {
                SumOne(ref sumState, i.Current);
            }

            return sumState.CurrentValue;
        }

        public static object sum(CodeContext/*!*/ context, [NotNull]List sequence, object start) {
            if (start is string) {
                throw PythonOps.TypeError("Cannot sum strings, use '{0}'.join(seq)", start);
            }

            var sumState = new SumState(context.LanguageContext, start);
            for (int i = 0; i < sequence._size; i++) {
                SumOne(ref sumState, sequence._data[i]);
            }

            return sumState.CurrentValue;
        }

        public static object sum(CodeContext/*!*/ context, [NotNull]PythonTuple sequence, object start) {
            if (start is string) {
                throw PythonOps.TypeError("Cannot sum strings, use '{0}'.join(seq)", start);
            }

            var sumState = new SumState(context.LanguageContext, start);
            var arr = sequence._data;
            for (int i = 0; i < arr.Length; i++) {
                SumOne(ref sumState, arr[i]);
            }

            return sumState.CurrentValue;
        }

        #region Optimized sum

        private static void SumOne(ref SumState state, object current) {
            if (current != null) {
                if (state.CurType == SumVariantType.Int) {
                    if (current.GetType() == typeof(int)) {
                        try {
                            state.IntVal = checked(state.IntVal + ((int)current));
                        } catch (OverflowException) {
                            state.BigIntVal = (BigInteger)state.IntVal + (BigInteger)(int)current;
                            state.CurType = SumVariantType.BigInt;
                        }
                    } else if (current.GetType() == typeof(double)) {
                        state.DoubleVal = state.IntVal + ((double)current);
                        state.CurType = SumVariantType.Double;
                    } else if (current.GetType() == typeof(BigInteger)) {
                        state.BigIntVal = (BigInteger)state.IntVal + (BigInteger)current;
                        state.CurType = SumVariantType.BigInt;
                    } else {
                        SumObject(ref state, state.IntVal, current);
                    }
                } else if (state.CurType == SumVariantType.Double) {
                    if (current.GetType() == typeof(double)) {
                        state.DoubleVal = state.DoubleVal + ((double)current);
                    } else if (current.GetType() == typeof(int)) {
                        state.DoubleVal = state.DoubleVal + ((int)current);
                    } else if (current.GetType() == typeof(BigInteger)) {
                        SumBigIntAndDouble(ref state, (BigInteger)current, state.DoubleVal);
                    } else {
                        SumObject(ref state, state.DoubleVal, current);
                    }
                } else if (state.CurType == SumVariantType.BigInt) {
                    if (current.GetType() == typeof(BigInteger)) {
                        state.BigIntVal = state.BigIntVal + ((BigInteger)current);
                    } else if (current.GetType() == typeof(int)) {
                        state.BigIntVal = state.BigIntVal + ((int)current);
                    } else if (current.GetType() == typeof(double)) {
                        SumBigIntAndDouble(ref state, state.BigIntVal, (double)current);
                    } else {
                        SumObject(ref state, state.BigIntVal, current);
                    }
                } else if (state.CurType == SumVariantType.Object) {
                    state.ObjectVal = state.AddSite.Target(state.AddSite, state.ObjectVal, current);
                }
            } else {
                SumObject(ref state, state.BigIntVal, current);
            }
        }

#if FEATURE_NUMERICS
        private static BigInteger MaxDouble = new BigInteger(Double.MaxValue);
        private static BigInteger MinDouble = new BigInteger(Double.MinValue);
#else
        private static BigInteger MaxDouble = BigInteger.Create(Double.MaxValue);
        private static BigInteger MinDouble = BigInteger.Create(Double.MinValue);
#endif

        private static void SumBigIntAndDouble(ref SumState state, BigInteger bigInt, double dbl) {
            if (bigInt <= MaxDouble && bigInt >= MinDouble) {
                state.DoubleVal = (double)bigInt + dbl;
                state.CurType = SumVariantType.Double;
            } else {
                // fallback to normal add to report error
                SumObject(ref state, dbl, bigInt);
            }
        }

        private static void SumObject(ref SumState state, object value, object current) {
            state.ObjectVal = state.AddSite.Target(state.AddSite, value, current);
            state.CurType = SumVariantType.Object;
        }

        enum SumVariantType {
            Double,
            Int,
            BigInt,
            Object
        }

        struct SumState {
            public double DoubleVal;
            public int IntVal;
            public object ObjectVal;
            public BigInteger BigIntVal;
            public SumVariantType CurType;
            public CallSite<Func<CallSite, object, object, object>> AddSite;

            public SumState(PythonContext context, object start) {
                DoubleVal = 0;
                IntVal = 0;
                ObjectVal = start;
                BigIntVal = BigInteger.Zero;
                AddSite = context.EnsureAddSite();

                if (start != null) {
                    if (start.GetType() == typeof(int)) {
                        CurType = SumVariantType.Int;
                        IntVal = (int)start;
                    } else if (start.GetType() == typeof(double)) {
                        CurType = SumVariantType.Double;
                        DoubleVal = (double)start;
                    } else if (start.GetType() == typeof(BigInteger)) {
                        CurType = SumVariantType.BigInt;
                        BigIntVal = (BigInteger)start;
                    } else {
                        CurType = SumVariantType.Object;
                    }
                } else {
                    CurType = SumVariantType.Object;
                }
            }

            public object CurrentValue {
                get {
                    switch (CurType) {
                        case SumVariantType.BigInt: return BigIntVal;
                        case SumVariantType.Double: return DoubleVal;
                        case SumVariantType.Int: return IntVal;
                        case SumVariantType.Object: return ObjectVal;
                        default: throw Assert.Unreachable;
                    }
                }
            }
        }

        #endregion

        public static PythonType super {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(Super));
            }
        }

        public static PythonType str {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(string));
            }
        }

        public static PythonType tuple {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonTuple));
            }
        }

        public static PythonType type {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonType));
            }
        }

        public static string unichr(int i) {
            if (i < Char.MinValue || i > Char.MaxValue) {
                throw PythonOps.ValueError("{0} is not in required range", i);
            }
            return ScriptingRuntimeHelpers.CharToString((char)i);
        }

        public static PythonType unicode {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(string));
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods")]
        [Documentation("vars([object]) -> dictionary\n\nWithout arguments, equivalent to locals().\nWith an argument, equivalent to object.__dict__.")]
        public static object vars(CodeContext/*!*/ context) {
            return locals(context);
        }

        public static object vars(CodeContext/*!*/ context, object @object) {
            object value;
            if (!PythonOps.TryGetBoundAttr(@object, "__dict__", out value)) {
                throw PythonOps.TypeError("vars() argument must have __dict__ attribute");
            }
            return value;
        }

        public static List zip(object s0, object s1) {
            IEnumerator i0 = PythonOps.GetEnumerator(s0);
            IEnumerator i1 = PythonOps.GetEnumerator(s1);
            List ret = new List();
            while(true) {
                object obj0, obj1;
                if(!i0.MoveNext()) {
                    break;
                }
                obj0 = i0.Current;
                if(!i1.MoveNext()) {
                    break;
                }

                obj1 = i1.Current;
                ret.AddNoLock(PythonTuple.MakeTuple(obj0, obj1));
            }
            return ret;
        }

        //??? should we fastpath the 1,2,3 item cases???
        public static List zip(params object[] seqs) {
            if (seqs == null) throw PythonOps.TypeError("zip argument must support iteration, got None");

            int N = seqs.Length;
            if (N == 2) return zip(seqs[0], seqs[1]);
            if (N == 0) return PythonOps.MakeList();
            IEnumerator[] iters = new IEnumerator[N];
            for (int i = 0; i < N; i++) iters[i] = PythonOps.GetEnumerator(seqs[i]);

            List ret = new List();
            while (true) {
                object[] items = new object[N];
                for (int i = 0; i < N; i++) {
                    // first iterator which is no longer iterable ends the 
                    // loop.
                    if (!iters[i].MoveNext()) return ret;
                    items[i] = iters[i].Current;
                }
                ret.AddNoLock(PythonTuple.MakeTuple(items));
            }
        }

        public static PythonType BaseException {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(PythonExceptions.BaseException));
            }
        }

        /// <summary>
        /// Gets the appropriate LanguageContext to be used for code compiled with Python's compile, eval, execfile, etc...
        /// </summary>
        internal static PythonCompilerOptions GetRuntimeGeneratedCodeCompilerOptions(CodeContext/*!*/ context, bool inheritContext, CompileFlags cflags) {
            PythonCompilerOptions pco;
            if (inheritContext) {
                pco = new PythonCompilerOptions(context.ModuleContext.Features);
            } else {
                pco = DefaultContext.DefaultPythonContext.GetPythonCompilerOptions();
            }
            
            if (((cflags & (CompileFlags.CO_FUTURE_DIVISION | CompileFlags.CO_FUTURE_ABSOLUTE_IMPORT | CompileFlags.CO_FUTURE_WITH_STATEMENT)) != 0)) {
                ModuleOptions langFeat = ModuleOptions.None;
                if ((cflags & CompileFlags.CO_FUTURE_DIVISION) != 0) {
                    langFeat |= ModuleOptions.TrueDivision;
                }
                if ((cflags & CompileFlags.CO_FUTURE_WITH_STATEMENT) != 0) {
                    langFeat |= ModuleOptions.WithStatement;
                }
                if ((cflags & CompileFlags.CO_FUTURE_ABSOLUTE_IMPORT) != 0) {
                    langFeat |= ModuleOptions.AbsoluteImports;
                }
                pco.Module |= langFeat;
            } 

            // The options created this way never creates
            // optimized module (exec, compile)
            pco.Module &= ~(ModuleOptions.Optimized | ModuleOptions.Initialize);
            pco.Module |= ModuleOptions.Interpret | ModuleOptions.ExecOrEvalCode;
            pco.CompilationMode = CompilationMode.Lookup;
            return pco;
        }

        /// <summary> Returns true if we should inherit our callers context (true division, etc...), false otherwise </summary>
        private static bool GetCompilerInheritance(object dontInherit) {
            return dontInherit == null || Converter.ConvertToInt32(dontInherit) == 0;
        }

        /// <summary> Returns the default compiler flags or the flags the user specified. </summary>
        private static CompileFlags GetCompilerFlags(int flags) {
            CompileFlags cflags = (CompileFlags)flags;
            if ((cflags & ~(CompileFlags.CO_NESTED | CompileFlags.CO_GENERATOR_ALLOWED | CompileFlags.CO_FUTURE_DIVISION | CompileFlags.CO_DONT_IMPLY_DEDENT | 
                CompileFlags.CO_FUTURE_ABSOLUTE_IMPORT | CompileFlags.CO_FUTURE_WITH_STATEMENT)) != 0) {
                throw PythonOps.ValueError("unrecognized flags");
            }

            return cflags;
        }

        /// <summary>
        /// Gets a scope used for executing new code in optionally replacing the globals and locals dictionaries.
        /// </summary>
        private static CodeContext/*!*/ GetExecEvalScopeOptional(CodeContext/*!*/ context, PythonDictionary globals, object localsDict, bool copyModule) {
            Assert.NotNull(context);

            if (globals == null) globals = Builtin.globals(context);
            if (localsDict == null) localsDict = locals(context);

            return GetExecEvalScope(context, globals, GetAttrLocals(context, localsDict), copyModule, true);
        }

        internal static CodeContext/*!*/ GetExecEvalScope(CodeContext/*!*/ context, PythonDictionary/*!*/ globals,
            PythonDictionary locals, bool copyModule, bool setBuiltinsToModule) {

            Assert.NotNull(context, globals);
            PythonContext python = PythonContext.GetContext(context);

            // TODO: Need to worry about propagating changes to MC out?
            var mc = new ModuleContext(PythonDictionary.FromIAC(context, globals), context.LanguageContext);
            CodeContext localContext;
            if (locals == null) {
                localContext = mc.GlobalContext;
            } else {
                localContext = new CodeContext(PythonDictionary.FromIAC(context, locals), mc);
            }

            if (!globals.ContainsKey("__builtins__")) {
                if (setBuiltinsToModule) {
                    globals["__builtins__"] = python.SystemStateModules["builtins"];
                } else {
                    globals["__builtins__"] = python.BuiltinModuleDict;
                }
            }
            return localContext;
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext context, PythonDictionary dict) {
            dict["__debug__"] = ScriptingRuntimeHelpers.BooleanToObject(!context.PythonOptions.Optimize);
        }
    }
}
