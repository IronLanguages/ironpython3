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

namespace IronPythonTest {
    public class NullableTest {
        public enum NullableEnums { NE1=1, NE3=3 };
        
        private double? _dvalue;
        private long? _lvalue;
        private bool? _bvalue;
        private NullableEnums? _evalue;

        public double? DProperty {
            get { return _dvalue; }
            set { _dvalue = value; }
        }

        public long? LProperty
        {
            get { return _lvalue; }
            set { _lvalue = value; }
        }

        public bool? BProperty
        {
            get { return _bvalue; }
            set { _bvalue = value; }
        }

        public NullableEnums? EProperty
        {
            get { return _evalue; }
            set { _evalue = value; }
        }

        public double? Method(double? d) {
            return d;
        }
    }
}
