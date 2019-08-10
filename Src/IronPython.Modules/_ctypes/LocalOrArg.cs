// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_LCG

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

    internal class Local : LocalOrArg {
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

    internal class Arg : LocalOrArg {
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

#endif
