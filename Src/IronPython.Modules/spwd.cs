// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_NATIVE || NETCOREAPP2_1

using System;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using System.Numerics;

[assembly: PythonModule("spwd", typeof(IronPython.Modules.PythonSpwd), PlatformsAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules {
    
    public static class PythonSpwd {
        public const string __doc__ = @"This module provides access to the Unix shadow password database.
It is available on various Unix versions.

Shadow password database entries are reported as 9-tuples of type struct_spwd,
containing the following items from the password database (see `<shadow.h>'):
sp_namp, sp_pwdp, sp_lstchg, sp_min, sp_max, sp_warn, sp_inact, sp_expire, sp_flag.
The sp_namp and sp_pwdp are strings, the rest are integers.
An exception is raised if the entry asked for cannot be found.
You have to be root to be able to use this module.";

        [StructLayout(LayoutKind.Sequential)]
        private struct spwd {
            [MarshalAs(UnmanagedType.LPStr)]
            public string sp_namp;
            [MarshalAs(UnmanagedType.LPStr)]
            public string sp_pwdp;
            public int sp_lstchg;
            public int sp_min;
            public int sp_max;
            public int sp_warn;
            public int sp_inact;
            public int sp_expire;
            public int sp_flag;
        };        

        [PythonType("struct_spwd")]
        [Documentation(@"spwd.struct_spwd: Results from getsp*() routines.

This object may be accessed either as a 9-tuple of
  (sp_namp,sp_pwdp,sp_lstchg,sp_min,sp_max,sp_warn,sp_inact,sp_expire,sp_flag)
or via the object attributes as named in the above tuple.")]
        public class struct_spwd : PythonTuple {

            private const int LENGTH = 9;

            internal struct_spwd(string sp_nam, string sp_pwd, int sp_lstchg, int sp_min, int sp_max, int sp_warn, int sp_inact, int sp_expire, int sp_flag) :
                base(new object[] { sp_nam ,sp_pwd, sp_lstchg, sp_min, sp_max, sp_warn, sp_inact, sp_expire, sp_flag }) {
            }

            [Documentation("login name")]
            public string sp_nam => (string)_data[0];

            [Documentation("encrypted password")]
            public string sp_pwd => (string)_data[1];

            [Documentation("date of last change")]
            public int sp_lstchg => (int)_data[2];

            [Documentation("min #days between changes")]
            public int sp_min => (int)_data[3];

            [Documentation("max #days between changes")]
            public int sp_max => (int)_data[4];

            [Documentation("#days before pw expires to warn user about it")]
            public int sp_warn => (int)_data[5];

            [Documentation("#days after pw expires until account is disabled")]
            public int sp_inact => (int)_data[6];

            [Documentation("#days since 1970-01-01 when account expires")]
            public int sp_expire => (int)_data[7];

            [Documentation("reserved")]
            public int sp_flag => (int)_data[8];

            public override string/*!*/ __repr__(CodeContext/*!*/ context) {
                return $"spwd.struct_spwd(sp_name='{sp_nam}', sp_pwd='{sp_pwd}', sp_lstchg={sp_lstchg}, sp_min={sp_min}, sp_max={sp_max}, sp_warn={sp_warn}, sp_inact={sp_inact}, sp_expire={sp_expire}, sp_flag={sp_flag})";
            }
        }

        private static struct_spwd Make(IntPtr pwd) {
            spwd p = (spwd)Marshal.PtrToStructure(pwd, typeof(spwd));
            return new struct_spwd(p.sp_namp, p.sp_pwdp, p.sp_lstchg, p.sp_min, p.sp_max, p.sp_warn, p.sp_inact, p.sp_expire, p.sp_flag);
        }

        [Documentation("Return the shadow password database entry for the given user name.")]
        public static struct_spwd getspnam(string name) {
            var pwd = _getspnam(name);
            if(pwd == IntPtr.Zero) {
                throw PythonOps.KeyError($"getspnam(): name not found");
            }

            return Make(pwd);
        }

        [Documentation("Return a list of all available shadow password database entries, in arbitrary order.")]
        public static List getspall() {
            var res = new List();
            setspent();
            IntPtr val = getspent();
            while(val != IntPtr.Zero) {
                res.Add(Make(val));
                val = getspent();
            }
            
            return res;
        }


        #region P/Invoke Declarations

        [DllImport("libc", EntryPoint="getspnam", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr _getspnam([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("libc", CallingConvention=CallingConvention.Cdecl)]
        private static extern void setspent();

        [DllImport("libc", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr getspent();

        #endregion

    }
}
#endif
