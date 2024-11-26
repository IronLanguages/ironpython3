// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Scripting.Utils;
using System.Collections.Generic;

namespace Microsoft.Scripting.Generation {
    /// <summary>
    /// This helper type lets us build a fake ParameterInfo object with a specific type and name
    /// to pass along to methods that expect ParameterInfos.  This is currently found useful
    /// for the NewTypeMaker code and may be useful in other situations as well.
    /// </summary>
    public class ParameterInfoWrapper : ParameterInfo {
        private Type _type;
        private string _name;

        public ParameterInfoWrapper(Type parameterType) {
            _type = parameterType;
        }

        public ParameterInfoWrapper(Type parameterType, string parameterName) {
            _type = parameterType;
            _name = parameterName;
        }

        public override Type ParameterType {
            get {
                return _type;
            }
        }

        public override string Name {
            get {
                if (_name != null) return _name;

                return base.Name;
            }
        }

        public override object[] GetCustomAttributes(bool inherit) {
            return ArrayUtils.EmptyObjects;
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) {
            return ArrayUtils.EmptyObjects;
        }
    }
}
