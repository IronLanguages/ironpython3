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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

[assembly: PythonModule("copy_reg", typeof(IronPython.Modules.PythonCopyReg))]
namespace IronPython.Modules {
    [Documentation("Provides global reduction-function registration for pickling and copying objects.")]
    public static class PythonCopyReg {
        private static readonly object _dispatchTableKey = new object();
        private static readonly object _extensionRegistryKey = new object();
        private static readonly object _invertedRegistryKey = new object();
        private static readonly object _extensionCacheKey = new object();

        internal static PythonDictionary GetDispatchTable(CodeContext/*!*/ context) {
            EnsureModuleInitialized(context);

            return (PythonDictionary)PythonContext.GetContext(context).GetModuleState(_dispatchTableKey);
        }

        internal static PythonDictionary GetExtensionRegistry(CodeContext/*!*/ context) {
            EnsureModuleInitialized(context);

            return (PythonDictionary)PythonContext.GetContext(context).GetModuleState(_extensionRegistryKey);
        }

        internal static PythonDictionary GetInvertedRegistry(CodeContext/*!*/ context) {
            EnsureModuleInitialized(context);

            return (PythonDictionary)PythonContext.GetContext(context).GetModuleState(_invertedRegistryKey);
        }

        internal static PythonDictionary GetExtensionCache(CodeContext/*!*/ context) {
            EnsureModuleInitialized(context);

            return (PythonDictionary)PythonContext.GetContext(context).GetModuleState(_extensionCacheKey);
        }

        #region Public API

        [Documentation("pickle(type, function[, constructor]) -> None\n\n"
            + "Associate function with type, indicating that function should be used to\n"
            + "\"reduce\" objects of the given type when pickling. function should behave as\n"
            + "specified by the \"Extended __reduce__ API\" section of PEP 307.\n"
            + "\n"
            + "Reduction functions registered by calling pickle() can be retrieved later\n"
            + "through copy_reg.dispatch_table[type].\n"
            + "\n"
            + "Note that calling pickle() will overwrite any previous association for the\n"
            + "given type.\n"
            + "\n"
            + "The constructor argument is ignored, and exists only for backwards\n"
            + "compatibility."
            )]
        public static void pickle(CodeContext/*!*/ context, object type, object function, [DefaultParameterValue(null)] object ctor) {
            EnsureCallable(context, function, "reduction functions must be callable");
            if (ctor != null) constructor(context, ctor);
            GetDispatchTable(context)[type] = function;
        }

        [Documentation("constructor(object) -> None\n\n"
            + "Raise TypeError if object isn't callable. This function exists only for\n"
            + "backwards compatibility; for details, see\n"
            + "http://mail.python.org/pipermail/python-dev/2006-June/066831.html."
            )]
        public static void constructor(CodeContext/*!*/ context, object callable) {
            EnsureCallable(context, callable, "constructors must be callable");
        }

        /// <summary>
        /// Throw TypeError with a specified message if object isn't callable.
        /// </summary>
        private static void EnsureCallable(CodeContext/*!*/ context, object @object, string message) {
            if (!PythonOps.IsCallable(context, @object)) {
                throw PythonOps.TypeError(message);
            }
        }

        [Documentation("pickle_complex(complex_number) -> (<type 'complex'>, (real, imag))\n\n"
            + "Reduction function for pickling complex numbers.")]
        public static PythonTuple pickle_complex(CodeContext context, object complex) {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(Complex)),
                PythonTuple.MakeTuple(
                    PythonOps.GetBoundAttr(context, complex, "real"),
                    PythonOps.GetBoundAttr(context, complex, "imag")
                )
            );
        }

        public static void clear_extension_cache(CodeContext/*!*/ context) {            
            GetExtensionCache(context).clear();
        }

        [Documentation("Register an extension code.")]
        public static void add_extension(CodeContext/*!*/ context, object moduleName, object objectName, object value) {
            PythonTuple key = PythonTuple.MakeTuple(moduleName, objectName);
            int code = GetCode(context, value);

            bool keyExists = GetExtensionRegistry(context).__contains__(key);
            bool codeExists = GetInvertedRegistry(context).__contains__(code);

            if (!keyExists && !codeExists) {
                GetExtensionRegistry(context)[key] = code;
                GetInvertedRegistry(context)[code] = key;
            } else if (keyExists && codeExists &&
                PythonOps.EqualRetBool(context, GetExtensionRegistry(context)[key], code) &&
                PythonOps.EqualRetBool(context, GetInvertedRegistry(context)[code], key)
            ) {
                // nop
            } else {
                if (keyExists) {
                    throw PythonOps.ValueError("key {0} is already registered with code {1}", PythonOps.Repr(context, key), PythonOps.Repr(context, GetExtensionRegistry(context)[key]));
                } else { // codeExists
                    throw PythonOps.ValueError("code {0} is already in use for key {1}", PythonOps.Repr(context, code), PythonOps.Repr(context, GetInvertedRegistry(context)[code]));
                }
            }
        }

        [Documentation("Unregister an extension code. (only for testing)")]
        public static void remove_extension(CodeContext/*!*/ context, object moduleName, object objectName, object value) {
            PythonTuple key = PythonTuple.MakeTuple(moduleName, objectName);
            int code = GetCode(context, value);

            object existingKey;
            object existingCode;

            if (((IDictionary<object, object>)GetExtensionRegistry(context)).TryGetValue(key, out existingCode) &&
                ((IDictionary<object, object>)GetInvertedRegistry(context)).TryGetValue(code, out existingKey) &&
                PythonOps.EqualRetBool(context, existingCode, code) &&
                PythonOps.EqualRetBool(context, existingKey, key)
            ) {
                GetExtensionRegistry(context).__delitem__(key);
                GetInvertedRegistry(context).__delitem__(code);
            } else {
                throw PythonOps.ValueError("key {0} is not registered with code {1}", PythonOps.Repr(context, key), PythonOps.Repr(context, code));
            }
        }

        [Documentation("__newobj__(cls, *args) -> cls.__new__(cls, *args)\n\n"
            + "Helper function for unpickling. Creates a new object of a given class.\n"
            + "See PEP 307 section \"The __newobj__ unpickling function\" for details."
            )]
        public static object __newobj__(CodeContext/*!*/ context, object cls, params object[] args) {
            object[] newArgs = new object[1 + args.Length];
            newArgs[0] = cls;
            for (int i = 0; i < args.Length; i++) newArgs[i + 1] = args[i];
            return PythonOps.Invoke(context, cls, "__new__", newArgs);
        }

        [Documentation("_reconstructor(basetype, objtype, basestate) -> object\n\n"
            + "Helper function for unpickling. Creates and initializes a new object of a given\n"
            + "class. See PEP 307 section \"Case 2: pickling new-style class instances using\n"
            + "protocols 0 or 1\" for details."
            )]
        public static object _reconstructor(CodeContext/*!*/ context, object objType, object baseType, object baseState) {
            object obj;
            if (baseState == null) {
                obj = PythonOps.Invoke(context, baseType, "__new__", objType);
                PythonOps.Invoke(context, baseType, "__init__", obj);
            } else {
                obj = PythonOps.Invoke(context, baseType, "__new__", objType, baseState);
                PythonOps.Invoke(context, baseType, "__init__", obj, baseState);
            }
            return obj;
        }

        #endregion

        #region Private implementation

        /// <summary>
        /// Convert object to ushort, throwing ValueError on overflow.
        /// </summary>
        private static int GetCode(CodeContext/*!*/ context, object value) {
            try {
                int intValue = PythonContext.GetContext(context).ConvertToInt32(value);
                if (intValue > 0) return intValue;
                // fall through and throw below
            } catch (OverflowException) {
                // throw below
            }
            throw PythonOps.ValueError("code out of range");
        }

        #endregion

        private static void EnsureModuleInitialized(CodeContext context) {
            if (!PythonContext.GetContext(context).HasModuleState(_dispatchTableKey)) {
                Importer.ImportBuiltin(context, "copy_reg");
            }
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.NewObject = (BuiltinFunction)dict["__newobj__"];
            context.PythonReconstructor = (BuiltinFunction)dict["_reconstructor"];

            PythonDictionary dispatchTable = new PythonDictionary();
            dispatchTable[TypeCache.Complex] = dict["pickle_complex"];

            context.SetModuleState(_dispatchTableKey, dict["dispatch_table"] = dispatchTable);
            context.SetModuleState(_extensionRegistryKey, dict["_extension_registry"] = new PythonDictionary());
            context.SetModuleState(_invertedRegistryKey, dict["_inverted_registry"] = new PythonDictionary());
            context.SetModuleState(_extensionCacheKey, dict["_extension_cache"] = new PythonDictionary());
        }
    }
}
