// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_NATIVE

using System;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;

using IronPython;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

using System.Numerics;
using System.Diagnostics.CodeAnalysis;

[assembly: PythonModule("pwd", typeof(IronPython.Modules.PythonPwd), PlatformsAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules {
    
    public static class PythonPwd {
        public const string __doc__ = @"This module provides access to the Unix password database.
It is available on all Unix versions.

Password database entries are reported as 7-tuples containing the following
items from the password database (see `<pwd.h>'), in order:
pw_name, pw_passwd, pw_uid, pw_gid, pw_gecos, pw_dir, pw_shell.
The uid and gid items are integers, all others are strings. An
exception is raised if the entry asked for cannot be found.";

        [StructLayout(LayoutKind.Sequential)]
        private struct passwd_linux {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_name;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_passwd;
            public int pw_uid;
            public int pw_gid;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_gecos;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_dir;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_shell;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct passwd_osx {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_name;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_passwd;
            public int pw_uid;
            public int pw_gid;
            public ulong pw_change;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_class;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_gecos;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_dir;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pw_shell;
            public ulong pw_expire;
        };

        [PythonType("struct_passwd")]
        [Documentation(@"pwd.struct_passwd: Results from getpw*() routines.

This object may be accessed either as a tuple of
  (pw_name,pw_passwd,pw_uid,pw_gid,pw_gecos,pw_dir,pw_shell)
or via the object attributes as named in the above tuple.")]
        public class struct_passwd : PythonTuple {

            internal struct_passwd(string pw_name, string pw_passwd, int pw_uid, int pw_gid, string pw_gecos, string pw_dir, string pw_shell) :
                base(new object[] { pw_name, pw_passwd, pw_uid, pw_gid, pw_gecos, pw_dir, pw_shell }) {

            }

            [Documentation("user name")]
            public string pw_name => (string)_data[0]!;

            [Documentation("password")]
            public string pw_passwd => (string)_data[1]!;

            [Documentation("user id")]
            public int pw_uid => (int)_data[2]!;

            [Documentation("group id")]
            public int pw_gid => (int)_data[3]!;

            [Documentation("real name")]
            public string pw_gecos => (string)_data[4]!;

            [Documentation("home directory")]
            public string pw_dir => (string)_data[5]!;

            [Documentation("shell program")]
            public string pw_shell => (string)_data[6]!;

            public override string/*!*/ __repr__(CodeContext/*!*/ context) {
                return $"pwd.struct_passwd(pw_name='{pw_name}', pw_passwd='{pw_passwd}', pw_uid={pw_uid}, pw_gid={pw_gid}, pw_gecos='{pw_gecos}', pw_dir='{pw_dir}', pw_shell='{pw_shell}')";
            }
        }

        private static struct_passwd Make(IntPtr pwd) {
            struct_passwd? res = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                passwd_osx p = Marshal.PtrToStructure<passwd_osx>(pwd);
                res = new struct_passwd(p.pw_name, p.pw_passwd, p.pw_uid, p.pw_gid, p.pw_gecos, p.pw_dir, p.pw_shell);
            } else {
                passwd_linux p = Marshal.PtrToStructure<passwd_linux>(pwd);
                res = new struct_passwd(p.pw_name, p.pw_passwd, p.pw_uid, p.pw_gid, p.pw_gecos, p.pw_dir, p.pw_shell);
            }

            return res;
        }

        [Documentation("Return the password database entry for the given numeric user ID.")]
        public static struct_passwd getpwuid(object? uid) {
            uid = PythonOps.Index(uid);

            if (uid is BigInteger bi) {
                try {
                    uid = (int)bi;
                } catch (OverflowException) {
                    throw PythonOps.KeyError($"getpwuid(): uid not found");
                }
            }

            if (uid is int id) {
                var pwd = _getpwuid(id);
                if(pwd == IntPtr.Zero) {
                    throw PythonOps.KeyError($"getpwuid(): uid not found: {id}");
                }

                return Make(pwd);                
            }

            throw PythonOps.TypeError($"integer argument expected, got {PythonOps.GetPythonTypeName(uid)}");
        }

        [Documentation("Return the password database entry for the given user name.")]
        public static struct_passwd getpwnam([NotNone] string name) {
            var pwd = _getpwnam(name);
            if(pwd == IntPtr.Zero) {
                throw PythonOps.KeyError($"getpwname(): name not found: {name}");
            }

            return Make(pwd);
        }

        [Documentation("Return a list of all available password database entries, in arbitrary order.")]
        public static PythonList getpwall() {
            var res = new PythonList();
            setpwent();
            IntPtr val = getpwent();
            while(val != IntPtr.Zero) {
                res.Add(Make(val));
                val = getpwent();
            }
            
            return res;
        }


        #region P/Invoke Declarations

        [DllImport("libc", EntryPoint="getpwuid", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr _getpwuid(int uid);

        [DllImport("libc", EntryPoint="getpwnam", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr _getpwnam([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("libc", CallingConvention=CallingConvention.Cdecl)]
        private static extern void setpwent();

        [DllImport("libc", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr getpwent();

        #endregion

    }
}
#endif
