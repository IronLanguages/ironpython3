// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
