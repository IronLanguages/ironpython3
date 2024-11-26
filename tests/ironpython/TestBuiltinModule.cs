// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Runtime;
using IronPython.Compiler;

[assembly: PythonModule("test_new_module", typeof(IronPythonTest.TestBuiltinModule))]
namespace IronPythonTest {
    public class TestBuiltinModule : BuiltinPythonModule {
        private PythonGlobal _test_attr, _test_min;
        private int _value;

        public TestBuiltinModule(PythonContext context)
            : base(context) {
        }

        protected override void Initialize(CodeContext codeContext, Dictionary<string, PythonGlobal> globals) {
            _test_attr = globals["test_attr"];
            _test_min = globals["min"];
            base.Initialize(codeContext, globals);
        }

        protected override IEnumerable<string> GetGlobalVariableNames() {
            return new string[] { "test_attr", "test_overlap_method", "test_overlap_type", "min" };
        }

        public object test_overlap_method() {
            return 42;
        }

        public class test_overlap_type {
        }

        public void inc_value() {
            _value++;
        }

        public int get_value() {
            return _value;
        }

        public object test_method() {
            return 42;
        }

        public object get_test_attr() {
            return _test_attr.CurrentValue;
        }

        public object get_min() {
            return _test_min.CurrentValue;
        }

        // statics
        public static int StaticMethod() {
            return 42;
        }

        public static readonly int StaticField = 42;
        public static int StaticProperty {
            get {
                return 42;
            }
        }
    }
}
