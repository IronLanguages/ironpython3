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

#if FEATURE_LCG

using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

//[assembly: PythonModule("_ctypes", typeof(IronPython.Modules.CTypes))]
namespace IronPython.Modules {
    internal abstract class MarshalCleanup {
        public abstract void Cleanup(ILGenerator/*!*/ generator);
    }

    class StringCleanup : MarshalCleanup {
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
