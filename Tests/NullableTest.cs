// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPythonTest {
    public class NullableTest {
        public enum NullableEnums { NE1 = 1, NE3 = 3 };

        private double? _dvalue;
        private long? _lvalue;
        private bool? _bvalue;
        private NullableEnums? _evalue;

        public double? DProperty {
            get { return _dvalue; }
            set { _dvalue = value; }
        }

        public long? LProperty {
            get { return _lvalue; }
            set { _lvalue = value; }
        }

        public bool? BProperty {
            get { return _bvalue; }
            set { _bvalue = value; }
        }

        public NullableEnums? EProperty {
            get { return _evalue; }
            set { _evalue = value; }
        }

        public double? Method(double? d) {
            return d;
        }
    }
}
