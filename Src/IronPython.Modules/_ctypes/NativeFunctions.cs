// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

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

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        public static extern void SetLastError(int errorCode);

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr module, string lpFileName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

        [DllImport("kernel32.dll")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, IntPtr Length);

        // unix entry points, VM needs to map the filenames.
        [DllImport("libdl")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        private const int RTLD_NOW = 2;

        public static IntPtr LoadDLL(string filename, int flags) {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                if(flags == 0)
                    flags = RTLD_NOW;
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
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                return calloc((IntPtr)1, size);
            }

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

        [DllImport("libc")]
        private static extern IntPtr calloc(IntPtr num, IntPtr size);

        private const int LMEM_ZEROINIT = 0x0040;

        [DllImport("kernel32.dll")]
        private static extern void RtlMoveMemory(IntPtr Destination, IntPtr src, IntPtr length);

        [DllImport("libc")]
        private static extern IntPtr memmove(IntPtr dst, IntPtr src, IntPtr length);

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
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                memmove(dest, src, length);
            } else {
                RtlMoveMemory(dest, src, length);
            }
            return dest;
        }
    }
}

#endif