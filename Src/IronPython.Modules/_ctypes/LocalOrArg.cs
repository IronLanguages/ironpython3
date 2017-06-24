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
using System.Reflection.Emit;

namespace IronPython.Modules {
    /// <summary>
    /// Wrapper class for emitting locals/variables during marshalling code gen.
    /// </summary>
    internal abstract class LocalOrArg {
        public abstract void Emit(ILGenerator ilgen);
        public abstract Type Type {
            get;
        }
    }

    class Local : LocalOrArg {
        private readonly LocalBuilder _local;

        public Local(LocalBuilder local) {
            _local = local;
        }

        public override void Emit(ILGenerator ilgen) {
            ilgen.Emit(OpCodes.Ldloc, _local);
        }

        public override Type Type {
            get { return _local.LocalType; }
        }
    }

    class Arg : LocalOrArg {
        private readonly int _index;
        private readonly Type _type;

        public Arg(int index, Type type) {
            _index = index;
            _type = type;
        }

        public override void Emit(ILGenerator ilgen) {
            ilgen.Emit(OpCodes.Ldarg, _index);
        }

        public override Type Type {
            get { return _type; }
        }
    }
}