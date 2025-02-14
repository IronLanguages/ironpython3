// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES
#pragma warning disable SYSLIB0004 // The Constrained Execution Region (CER) feature is not supported in .NET 5.0.

using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

//[assembly: PythonModule("_ctypes", typeof(IronPython.Modules.CTypes))]
namespace IronPython.Modules {
    /// <summary>
    /// Native functions used for exposing ctypes functionality.
    /// </summary>
    internal static class NativeFunctions {
        private static SetMemoryDelegate _setMem = MemSet;
        private static MoveMemoryDelegate _moveMem = MoveMemory;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr SetMemoryDelegate(IntPtr dest, byte value, nuint length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr MoveMemoryDelegate(IntPtr dest, IntPtr src, nuint length);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        public static extern void SetLastError(int errorCode);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        public static extern int GetLastError();
        [SupportedOSPlatform("windows")]

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr module, string lpFileName);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern unsafe void memcpy(void* dst, void* src, nuint length);

        [SupportedOSPlatform("macos")]
        [DllImport("libSystem.dylib", EntryPoint = "memcpy")]
        private static extern unsafe void memcpy_darwin(void* dst, void* src, nuint length);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", ExactSpelling = true)]
        private static extern unsafe void CopyMemory(void* destination, void* source, nuint length);

        public static unsafe void MemCopy(IntPtr destination, IntPtr source, nuint length) {
            void* dst = (void*)destination;
            void* src = (void*)source;
#if NET7_0_OR_GREATER
            NativeMemory.Copy(source: src, destination: dst, length);
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                memcpy(dst, src, length);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                memcpy_darwin(dst, src, length);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                CopyMemory(dst, src, length);
            } else {
                throw new PlatformNotSupportedException();
            }
#endif
        }

        // unix entry points, VM needs to map the filenames.
        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern IntPtr dlopen(string filename, int flags);

        [SupportedOSPlatform("linux")]
        [DllImport("libdl", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen_dl(string filename, int flags);

        [SupportedOSPlatform("macos")]
        [DllImport("libSystem.dylib", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen_darwin(string filename, int flags);

        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern unsafe void* dlsym(IntPtr handle, string symbol);

        [SupportedOSPlatform("linux")]
        [DllImport("libdl", EntryPoint = "dlsym")]
        private static extern unsafe void* dlsym_dl(IntPtr handle, string symbol);

        [SupportedOSPlatform("macos")]
        [DllImport("libSystem.dylib", EntryPoint = "dlsym")]
        private static extern unsafe void* dlsym_darwin(IntPtr handle, string symbol);

        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern IntPtr gnu_get_libc_version();

        [SupportedOSPlatform("linux")]
        private static bool GetGNULibCVersion(out int major, out int minor) {
            major = minor = 0;
            try {
                string ver = Marshal.PtrToStringAnsi(gnu_get_libc_version());
                int dot = ver.IndexOf('.');
                if (dot < 0) dot = ver.Length;
                if (!int.TryParse(ver.Substring(0, dot), out major)) return false;
                if (dot + 1 < ver.Length) {
                    if (!int.TryParse(ver.Substring(dot + 1), out minor)) return false;
                }
            } catch {
                return false;
            }
            return true;
        }

        private const int RTLD_NOW = 2;

        [SupportedOSPlatform("linux")]
        private static bool UseLibDL() {
            if (!_useLibDL.HasValue) {
                bool success = GetGNULibCVersion(out int major, out int minor);
                _useLibDL = !success || major < 2 || (major == 2 && minor < 34);
            }
            return _useLibDL.Value;
        }
        private static bool? _useLibDL;

        public static IntPtr LoadDLL(string filename, int flags) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return LoadLibrary(filename);
            }

            if (flags == 0) flags = RTLD_NOW;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return UseLibDL() ? dlopen_dl(filename, flags) : dlopen(filename, flags);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return dlopen_darwin(filename, flags);
            } else {
                throw new PlatformNotSupportedException();
            }
        }

        public static unsafe IntPtr LoadFunction(IntPtr module, string functionName) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return GetProcAddress(module, functionName);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return (IntPtr)(UseLibDL() ? dlsym_dl(module, functionName) : dlsym(module, functionName));
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return (IntPtr)dlsym_darwin(module, functionName);
            } else {
                throw new PlatformNotSupportedException();
            }
        }

        public static IntPtr LoadFunction(IntPtr module, IntPtr ordinal) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return GetProcAddress(module, ordinal);
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Allocates memory that's zero-filled
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public static unsafe IntPtr Calloc(nuint size) {
#if NET7_0_OR_GREATER
            return new IntPtr(NativeMemory.AllocZeroed(size));
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return (IntPtr)calloc(1, size);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return (IntPtr)calloc_darwin(1, size);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                const uint LMEM_ZEROINIT = 0x0040;
                return (IntPtr)LocalAlloc(LMEM_ZEROINIT, size);
            } else {
                throw new PlatformNotSupportedException();
            }
#endif
        }

        public static IntPtr GetMemMoveAddress() {
            return Marshal.GetFunctionPointerForDelegate(_moveMem);
        }

        public static IntPtr GetMemSetAddress() {
            return Marshal.GetFunctionPointerForDelegate(_setMem);
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll"), ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private static extern unsafe void* LocalAlloc(uint flags, nuint size);

        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern unsafe void* calloc(nuint num, nuint size);

        
        [SupportedOSPlatform("macos")]
        [DllImport("libSystem.dylib", EntryPoint = "calloc")]
        private static extern unsafe void* calloc_darwin(nuint num, nuint size);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        private static extern unsafe void RtlMoveMemory(void* dest, void* src, nuint length);

        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern unsafe void* memmove(void* dest, void* src, nuint length);
    
        [SupportedOSPlatform("macos")]
        [DllImport("libSystem.dylib", EntryPoint = "memmove")]
        private static extern unsafe void* memmove_darwin(void* dest, void* src, nuint count);

        /// <summary>
        /// Helper function for implementing memset.
        /// </summary>
        private static unsafe IntPtr MemSet(IntPtr dest, byte value, nuint length) {
#if NET7_0_OR_GREATER
            NativeMemory.Fill((void*)dest, length, value);
#else
            const int blockSize = 1 << 30;  // 1 GiB
            byte* cur = (byte*)dest;
            while (length > 0) {
                int to_fill = length < blockSize ? (int)length : blockSize;
                new Span<byte>(cur, to_fill).Fill(value);
                length -= (nuint)to_fill;
                cur += to_fill;
            }
#endif
            return dest;
        }

        private static unsafe IntPtr MoveMemory(IntPtr destination, IntPtr source, nuint length) {
            void* dst = (void*)destination;
            void* src = (void*)source;
#if NET7_0_OR_GREATER
            NativeMemory.Copy((void*)src, (void*)dst, length);
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                memmove(dst, src, length);
            } else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                memmove_darwin(dst, src, length);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                RtlMoveMemory(dst, src, length);
            } else {
                throw new PlatformNotSupportedException();
            }
#endif
            return destination;
        }
    }
}

#endif
