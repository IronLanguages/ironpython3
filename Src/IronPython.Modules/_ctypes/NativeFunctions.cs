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

#if FEATURE_NATIVE

using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

//[assembly: PythonModule("_ctypes", typeof(IronPython.Modules.CTypes))]
namespace IronPython.Modules {
    /// <summary>
    /// Native functions used for exposing ctypes functionality.
    /// </summary>
    static class NativeFunctions {
        private static SetMemoryDelegate _setMem = MemSet;
        private static MoveMemoryDelegate _moveMem = MoveMemory;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr SetMemoryDelegate(IntPtr dest, byte value, IntPtr length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr MoveMemoryDelegate(IntPtr dest, IntPtr src, IntPtr length);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void SetLastError(int errorCode);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr module, string lpFileName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

        [DllImport("kernel32.dll")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, IntPtr Length);

        // unix entry points, VM needs to map the filenames.
        [DllImport("libdl.so")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        public static IntPtr LoadDLL(string filename, int flags) {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                return dlopen(filename, flags);
            }

            return LoadLibrary(filename);
        }

        public static IntPtr LoadFunction(IntPtr module, string functionName) {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                return dlsym(module, functionName);
            }

            return GetProcAddress(module, functionName);
        }

        public static IntPtr LoadFunction(IntPtr module, IntPtr ordinal) {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                return IntPtr.Zero;
            }

            return GetProcAddress(module, ordinal);

        }

        /// <summary>
        /// Allocates memory that's zero-filled
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public static IntPtr Calloc(IntPtr size) {
            return LocalAlloc(LMEM_ZEROINIT, size);
        }

        public static IntPtr GetMemMoveAddress() {
            return Marshal.GetFunctionPointerForDelegate(_moveMem);
        }

        public static IntPtr GetMemSetAddress() {
            return Marshal.GetFunctionPointerForDelegate(_setMem);
        }

        [DllImport("kernel32.dll"), ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private static extern IntPtr LocalAlloc(uint flags, IntPtr size);

        private const int LMEM_ZEROINIT = 0x0040;

        [DllImport("kernel32.dll")]
        private static extern void RtlMoveMemory(IntPtr Destination, IntPtr src, IntPtr length);

        /// <summary>
        /// Helper function for implementing memset.  Could be more efficient if we 
        /// could P/Invoke or call some otherwise native code to do this.
        /// </summary>
        private static IntPtr MemSet(IntPtr dest, byte value, IntPtr length) {            
            IntPtr end = dest.Add(length.ToInt32());
            for (IntPtr cur = dest; cur != end; cur = new IntPtr(cur.ToInt64() + 1)) {
                Marshal.WriteByte(cur, value);
            }
            return dest;
        }

        private static IntPtr MoveMemory(IntPtr dest, IntPtr src, IntPtr length) {
            RtlMoveMemory(dest, src, length);
            return dest;
        }
    }
}

#endif