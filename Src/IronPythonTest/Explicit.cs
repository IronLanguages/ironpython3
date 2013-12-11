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

namespace IronPythonTest {

    public interface IExplicitTest1 {
        string A();
        string B();
        string C();
        string D();
    }

    public interface IExplicitTest2 {
        string A();
        string B();
    }

    public interface IExplicitTest3 {
        int M();
    }

    public interface IExplicitTest4 {
        int M(int i);
    }

    public interface IExplicitTest5 {
        string A();
    }

    public interface IExplicitTest6 {
        string B();
    }

    public interface IExplicitTest7 {
        string A();
    }

    public class ExplicitTest : IExplicitTest1, IExplicitTest2 {
        #region IExplicitTest1 Members
        string IExplicitTest1.A() {
            return "ExplicitTest.IExplicitTest1.A";
        }
        string IExplicitTest1.B() {
            return "ExplicitTest.IExplicitTest1.B";
        }
        string IExplicitTest1.C() {
            return "ExplicitTest.IExplicitTest1.C";
        }
        public string D() {
            return "ExplicitTest.D";
        }
        #endregion

        #region IExplicitTest2 Members
        string IExplicitTest2.A() {
            return "ExplicitTest.IExplicitTest2.A";
        }
        public string B() {
            return "ExplicitTest.B";
        }
        #endregion
    }

    public class ExplicitTestArg : IExplicitTest3, IExplicitTest4 {
        #region IExplicitTest3 Members
        int IExplicitTest3.M() {
            return 3;
        }
        #endregion

        #region IExplicitTest4 Members
        int IExplicitTest4.M(int i) {
            return 4;
        }
        #endregion
    }

    public class ExplicitTestNoConflict : IExplicitTest5, IExplicitTest6 {
        #region IExplicitTest5 Members

        string IExplicitTest5.A() {
            return "A";
        }

        #endregion

        #region IExplicitTest6 Members

        string IExplicitTest6.B() {
            return "B";
        }

        #endregion
    }

    public class ExplicitTestOneConflict : IExplicitTest5, IExplicitTest6, IExplicitTest7 {
        #region IExplicitTest5 Members

        string IExplicitTest5.A() {
            return "A";
        }

        #endregion

        #region IExplicitTest6 Members

        string IExplicitTest6.B() {
            return "B";
        }

        #endregion

        #region IExplicitTest7 Members

        string IExplicitTest7.A() {
            return "A7";
        }

        #endregion
    }
}
