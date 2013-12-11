/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System.Runtime.CompilerServices;
using Microsoft.Scripting.Runtime;

[assembly: ExtensionType(typeof(IronPythonTest.ExtendedClass), typeof(IronPythonTest.ExtensionClass))]
namespace IronPythonTest {
    public class OperatorTest {
        public object Value;
        public string Name;

        [SpecialName]
        public object GetBoundMember(string name) {
            Name = name;
            return 42;
        }
        [SpecialName]
        public void SetMember(string name, object value) {
            Name = name;
            Value = value;
        }
    }

    public class ExtendedClass {
        public object Value;
        public string Name;
    }

    public class ExtensionClass {
        [SpecialName]
        public static object GetBoundMember(ExtendedClass self, string name) {
            self.Name = name;
            return 42;
        }
        [SpecialName]
        public static void SetMember(ExtendedClass self, string name, object value) {
            self.Name = name;
            self.Value = value;
        }
    }
}
