// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Runtime.InteropServices;

using IronPython.Runtime;

[assembly: PythonModule("_overlapped", typeof(IronPython.Modules.PythonOverlapped), PlatformsAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    public static class PythonOverlapped {
        public const int ERROR_NETNAME_DELETED = 64;
        public const int ERROR_SEM_TIMEOUT = 121;
        public const int ERROR_PIPE_BUSY = 231;
        public const int ERROR_IO_PENDING = 997;

        public static readonly BigInteger INVALID_HANDLE_VALUE = IntPtr.Size == 4 ? unchecked((uint)new IntPtr(-1)) : unchecked((ulong)new IntPtr(-1));

        [DllImport("kernel32.dll", EntryPoint = "CreateIoCompletionPort", SetLastError = true)]
        private static extern IntPtr _CreateIoCompletionPort(IntPtr FileHandle, IntPtr ExistingCompletionPort, UIntPtr CompletionKey, uint NumberOfConcurrentThreads);

        public static BigInteger CreateIoCompletionPort(BigInteger handle, BigInteger port, BigInteger key, int concurrency) {
            var res = _CreateIoCompletionPort(checked((IntPtr)(long)handle), checked((IntPtr)(long)port), checked((UIntPtr)(ulong)key), (uint)concurrency);
            if (res == IntPtr.Zero) {
                throw PythonNT.GetLastWin32Error();
            }
            return res.ToInt64();
        }
    }
}
