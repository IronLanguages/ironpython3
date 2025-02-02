// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using SpecialName = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("gc", typeof(IronPython.Modules.PythonGC))]
namespace IronPython.Modules {
    public static class PythonGC {
        public const string __doc__ = "Provides functions for inspecting, configuring, and forcing garbage collection.";

        public const int DEBUG_STATS = 1;
        public const int DEBUG_COLLECTABLE = 2;
        public const int DEBUG_UNCOLLECTABLE = 4;
        public const int DEBUG_SAVEALL = 32;
        public const int DEBUG_LEAK = (DEBUG_COLLECTABLE | DEBUG_UNCOLLECTABLE | DEBUG_SAVEALL);

        private static readonly object _threadholdKey = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.SetModuleState(_threadholdKey, PythonTuple.MakeTuple(64 * 1024, 256 * 1024, 1024 * 1024));
        }

        public static void enable() {
        }

        public static void disable(CodeContext/*!*/ context) {
            PythonOps.Warn(context, PythonExceptions.RuntimeWarning, "IronPython has no support for disabling the GC");
        }

        public static object isenabled() {
            return ScriptingRuntimeHelpers.True;
        }

        public static int collect(CodeContext context, int generation) {
            return context.LanguageContext.Collect(generation);
        }

        public static int collect(CodeContext context) {
            return collect(context, GC.MaxGeneration);
        }

        public static void set_debug(object? o) {
            throw PythonOps.NotImplementedError("gc.set_debug isn't implemented");
        }

        public static object get_debug() {
            return 0;
        }

        public static object[] get_objects() {
            throw PythonOps.NotImplementedError("gc.get_objects isn't implemented");
        }

        public static void set_threshold(CodeContext/*!*/ context, [NotNone] params object[] args) {
            if (args.Length == 0) {
                throw PythonOps.TypeError("set_threshold() takes at least 1 argument (0 given)");
            }

            if (args.Length > 3) {
                throw PythonOps.TypeError("set_threshold() takes at most 3 arguments ({0} given)", args.Length);
            }

            if (args.Any(x => x is double)) {
                throw PythonOps.TypeError("integer argument expected, got float");
            }

            if (!args.All(x => x is int)) {
                throw PythonOps.TypeError("an integer is required");
            }

            PythonTuple current = get_threshold(context);
            object?[] threshold = args.Take(args.Length)
                                     .Concat(current.ToArray().Skip(args.Length))
                                     .ToArray();
            SetThresholds(context, PythonTuple.MakeTuple(threshold));
        }

        public static PythonTuple get_threshold(CodeContext/*!*/ context) {
            return GetThresholds(context);
        }

        public static object[] get_referrers([NotNone] params object[] objs) {
            throw PythonOps.NotImplementedError("gc.get_referrers isn't implemented");
        }

        public static object[] get_referents([NotNone] params object[] objs) {
            throw PythonOps.NotImplementedError("gc.get_referents isn't implemented");
        }


        public static PythonList garbage {
            get {
                return new PythonList();
            }
        }

        private static PythonTuple GetThresholds(CodeContext/*!*/ context) {
            return (PythonTuple)context.LanguageContext.GetModuleState(_threadholdKey);
        }

        private static void SetThresholds(CodeContext/*!*/ context, PythonTuple thresholds) {
            context.LanguageContext.SetModuleState(_threadholdKey, thresholds);
        }
    }
}
