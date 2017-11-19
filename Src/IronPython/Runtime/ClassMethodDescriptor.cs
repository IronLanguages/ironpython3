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
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [PythonType("method_descriptor"), DontMapGetMemberNamesToDir]
    public class ClassMethodDescriptor : PythonTypeSlot, ICodeFormattable {
        internal readonly BuiltinFunction _func;

        internal ClassMethodDescriptor(BuiltinFunction func) {
            _func = func;
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            owner = CheckGetArgs(context, instance, owner);
            value = new Method(_func, owner, DynamicHelpers.GetPythonType(owner));
            return true;
        }

        #region Descriptor Protocol
        
        private PythonType CheckGetArgs(CodeContext context, object instance, PythonType owner) {
            if (owner == null) {
                if (instance == null) throw PythonOps.TypeError("__get__(None, None) is invalid");
                owner = DynamicHelpers.GetPythonType(instance);
            } else {
                if (!owner.IsSubclassOf(DynamicHelpers.GetPythonTypeFromType(_func.DeclaringType))) {
                    throw PythonOps.TypeError("descriptor {0} for type {1} doesn't apply to type {2}",
                        PythonOps.Repr(context, _func.Name),
                        PythonOps.Repr(context, DynamicHelpers.GetPythonTypeFromType(_func.DeclaringType).Name),
                        PythonOps.Repr(context, owner.Name));
                }
            }
            if (instance != null)
                BuiltinMethodDescriptor.CheckSelfWorker(context, instance, _func);

            return owner;
        }

        #endregion

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            BuiltinFunction bf = _func as BuiltinFunction;
            if (bf != null) {
                return String.Format("<method {0} of {1} objects>",
                    PythonOps.Repr(context, bf.Name),
                    PythonOps.Repr(context, DynamicHelpers.GetPythonTypeFromType(bf.DeclaringType).Name));
            }

            return String.Format("<classmethod object at {0}>",
                IdDispenser.GetId(this));
        }

        #endregion

        public override bool Equals(object obj) {
            ClassMethodDescriptor cmd = obj as ClassMethodDescriptor;
            if (cmd == null) return false;

            return cmd._func == _func;
        }

        public override int GetHashCode() {
            return ~_func.GetHashCode();
        }
    } 
}
