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

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Scripting.Utils;
using System.Text;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;

namespace IronPython.Runtime.Types {
    [PythonType("builtin_function_or_method")]
    public class ConstructorFunction : BuiltinFunction {
        private MethodBase[] _ctors;

        internal ConstructorFunction(BuiltinFunction realTarget, IList<MethodBase> constructors)
            : base("__new__", ArrayUtils.ToArray(GetTargetsValidateFunction(realTarget)), realTarget.DeclaringType, FunctionType.Function | FunctionType.AlwaysVisible) {

            Name = realTarget.Name;
            FunctionType = realTarget.FunctionType;
            _ctors = ArrayUtils.ToArray(constructors);
        }

        internal IList<MethodBase> ConstructorTargets {
            get {
                return _ctors;
            }
        }

        public override BuiltinFunctionOverloadMapper Overloads {
            [PythonHidden]
            get {
                return new ConstructorOverloadMapper(this, null);
            }
        }

        private static IList<MethodBase> GetTargetsValidateFunction(BuiltinFunction realTarget) {
            ContractUtils.RequiresNotNull(realTarget, "realTarget");
            return realTarget.Targets;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public new string __name__ {
            get {
                return "__new__";
            }
        }

        public override string __doc__ {
            get {
                StringBuilder sb = new StringBuilder();

                foreach (MethodBase mb in ConstructorTargets) {
                    if (mb != null) sb.AppendLine(DocBuilder.DocOneInfo(mb, "__new__"));
                }
                return sb.ToString();
            }
        }


    }
}
