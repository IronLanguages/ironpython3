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
using System.Linq;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using SpecialName = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("gc", typeof(IronPython.Modules.PythonGC))]
namespace IronPython.Modules {
    public static class PythonGC {
        public const string __doc__ = "Provides functions for inspecting, configuring, and forcing garbage collection.";

        public const int DEBUG_STATS = 1;
        public const int DEBUG_COLLECTABLE = 2;
        public const int DEBUG_UNCOLLECTABLE = 4;
        public const int DEBUG_INSTANCES = 8;
        public const int DEBUG_OBJECTS = 16;
        public const int DEBUG_SAVEALL = 32;
        public const int DEBUG_LEAK = (DEBUG_COLLECTABLE | DEBUG_UNCOLLECTABLE | DEBUG_INSTANCES | DEBUG_OBJECTS | DEBUG_SAVEALL);

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
            return PythonContext.GetContext(context).Collect(generation);
        }

        public static int collect(CodeContext context) {
            return collect(context, GC.MaxGeneration);
        }

        public static void set_debug(object o) {
            throw PythonOps.NotImplementedError("gc.set_debug isn't implemented");
        }

        public static object get_debug() {
            return 0;
        }

        public static object[] get_objects() {
            throw PythonOps.NotImplementedError("gc.get_objects isn't implemented");
        }

        public static void set_threshold(CodeContext/*!*/ context, params object[] args) {
            if(args.Length == 0) {
                throw PythonOps.TypeError("set_threshold() takes at least 1 argument (0 given)");
            }

            if(args.Length > 3) {
                throw PythonOps.TypeError("set_threshold() takes at most 3 arguments ({0} given)", args.Length);
            }

            if(args.Any(x => x is double)) {
                throw PythonOps.TypeError("integer argument expected, got float");
            }

            if(!args.All(x => x is int)) {
                throw PythonOps.TypeError("an integer is required");
            }

            PythonTuple current = get_threshold(context);
            object[] threshold = args.Take(args.Length)
                                     .Concat(current.ToArray().Skip(args.Length))
                                     .ToArray();
            SetThresholds(context, PythonTuple.MakeTuple(threshold));
        }

        public static PythonTuple get_threshold(CodeContext/*!*/ context) {
            return GetThresholds(context);
        }

        public static object[] get_referrers(params object[] objs) {
            throw PythonOps.NotImplementedError("gc.get_referrers isn't implemented");
        }

        public static object[] get_referents(params object[] objs) {
            throw PythonOps.NotImplementedError("gc.get_referents isn't implemented");
        }


        public static List garbage {
            get {
                return new List();
            }
        }

        private static PythonTuple GetThresholds(CodeContext/*!*/ context) {
            return (PythonTuple)PythonContext.GetContext(context).GetModuleState(_threadholdKey);
        }

        private static void SetThresholds(CodeContext/*!*/ context, PythonTuple thresholds) {
            PythonContext.GetContext(context).SetModuleState(_threadholdKey, thresholds);
        }
    }
}
