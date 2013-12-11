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

using System;

namespace IronPythonTest {
    public class CLRException1 : Exception {
        public CLRException1() : base() { }
        public CLRException1(string msg) : base(msg) { }
    }
    public class CLRException2 : Exception {
        public CLRException2() : base() { }
        public CLRException2(string msg) : base(msg) { }
    }
    public class CLRException3 : Exception {
        public CLRException3() : base() { }
        public CLRException3(string msg) : base(msg) { }
    }
    public class CLRException4 : Exception {
        public CLRException4() : base() { }
        public CLRException4(string msg) : base(msg) { }
    }
}
