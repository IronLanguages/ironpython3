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
using System.Runtime.InteropServices;
using System.Text;

namespace SbsTest {
    public class C {
        //def f1(arg0, arg1, arg2, arg3): print "same##", arg0, arg1, arg2, arg3
        //def f2(arg0, arg1, arg2=6, arg3=7): print "same##", arg0, arg1, arg2, arg3 
        //def f3(arg0, arg1, arg2, *arg3): print "same##", arg0, arg1, arg2, arg3 

        public string M1(int arg0, int arg1, int arg2, int arg3) {
            return String.Format("same## {0} {1} {2} {3}", arg0, arg1, arg2, arg3);
        }

        public string M2(int arg0, int arg1, [DefaultParameterValue(6)]int arg2, [DefaultParameterValue(7)]int arg3) {
            return String.Format("same## {0} {1} {2} {3}", arg0, arg1, arg2, arg3);
        }

        public string M3(int arg0, int arg1, int arg2, params int[] arg3) {
            StringBuilder buf = new StringBuilder();
            buf.Append("(");
            for (int i = 0; i < arg3.Length; i++) {
                if (i > 0) buf.Append(", ");
                buf.Append(arg3[i].ToString());
            }
            if (arg3.Length == 1) buf.Append(",");
            buf.Append(")");

            return String.Format("same## {0} {1} {2} {3}", arg0, arg1, arg2, buf);
        }
    }
}