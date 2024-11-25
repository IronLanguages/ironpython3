// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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