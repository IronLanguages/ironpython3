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
using System.Reflection;

#if FEATURE_REFEMIT

namespace Microsoft.Scripting.Generation {
    internal class ParameterInfoWrapper {
        private readonly ParameterInfo parameterInfo;
        private readonly Type parameterType;
        private readonly string name;

        public ParameterInfoWrapper(ParameterInfo parameterInfo) {
            this.parameterInfo = parameterInfo;
        }

        public ParameterInfoWrapper(Type parameterType, string name = null) {
            this.parameterType = parameterType;
            this.name = name;
        }

        public Type ParameterType {
            get {
                if (parameterInfo != null)
                    return parameterInfo.ParameterType;
                return parameterType;
            }
        }

        public string Name {
            get {
                if (parameterInfo != null)
                    return parameterInfo.Name;
                return name;
            }
        }

        public ParameterAttributes Attributes {
            get {
                if (parameterInfo != null)
                    return parameterInfo.Attributes;
                return default(ParameterAttributes);
            }
        }
    }
}

#endif
