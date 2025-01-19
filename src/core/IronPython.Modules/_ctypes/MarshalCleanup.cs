// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_LCG

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

//[assembly: PythonModule("_ctypes", typeof(IronPython.Modules.CTypes))]
namespace IronPython.Modules {
    internal abstract class MarshalCleanup {
        public abstract void Cleanup(ILGenerator/*!*/ generator);
    }

    internal class StringCleanup : MarshalCleanup {
        private readonly LocalBuilder/*!*/ _local;

        public StringCleanup(LocalBuilder local) {
            _local = local;
        }

        public override void Cleanup(ILGenerator/*!*/ generator) {
            generator.Emit(OpCodes.Ldloc, _local);
            generator.Emit(OpCodes.Call, typeof(Marshal).GetMethod("FreeHGlobal"));
        }
    }
}

#endif
