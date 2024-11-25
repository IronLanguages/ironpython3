// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace IronPython.Runtime.Operations {
    public static class ComOps {
        public static string? __str__(object/*!*/ self) {
            return self.ToString();
        }

        public static string/*!*/ __repr__(object/*!*/ self) {
            return $"<{self.ToString()}({TypeDescriptor.GetClassName(self)}) object at {PythonOps.HexId(self)}>";
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00020400-0000-0000-C000-000000000046")]
#pragma warning disable SYSLIB1096 // Convert to 'GeneratedComInterface'
        private interface IDispatch {
#pragma warning restore SYSLIB1096 // Convert to 'GeneratedComInterface'
            int GetTypeInfoCount();
            [return: MarshalAs(UnmanagedType.Interface)]
            ITypeInfo GetTypeInfo([In, MarshalAs(UnmanagedType.U4)] int iTInfo, [In, MarshalAs(UnmanagedType.U4)] int lcid);
            void GetIDsOfNames([In] ref Guid riid, [In, MarshalAs(UnmanagedType.LPArray)] string[] rgszNames, [In, MarshalAs(UnmanagedType.U4)] int cNames, [In, MarshalAs(UnmanagedType.U4)] int lcid, [Out, MarshalAs(UnmanagedType.LPArray)] int[] rgDispId);
        }
    }
}
