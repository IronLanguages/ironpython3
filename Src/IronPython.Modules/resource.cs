// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static System.Environment;

using Microsoft.Scripting.Runtime;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("resource", typeof(IronPython.Modules.PythonResourceModule), PlatformsAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public static class PythonResourceModule {
    public const string __doc__ = "Provides basic mechanisms for measuring and controlling system resources utilized by a program. Unix only.";

    [Obsolete("Deprecated in favor of OSError.")]
    public static PythonType error => PythonExceptions.OSError;

    #region Constants

    public static BigInteger RLIM_INFINITY
        => OSVersion.Platform == PlatformID.MacOSX ?
            (BigInteger)long.MaxValue : BigInteger.MinusOne;

    public static int RLIMIT_CPU
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_CPU : (int)linux__rlimit_resource.RLIMIT_CPU;

    public static int RLIMIT_FSIZE
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_FSIZE : (int)linux__rlimit_resource.RLIMIT_FSIZE;

    public static int RLIMIT_DATA
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_DATA : (int)linux__rlimit_resource.RLIMIT_DATA;

    public static int RLIMIT_STACK
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_STACK : (int)linux__rlimit_resource.RLIMIT_STACK;

    public static int RLIMIT_CORE
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_CORE : (int)linux__rlimit_resource.RLIMIT_CORE;

    public static int RLIMIT_RSS
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_RSS : (int)linux__rlimit_resource.RLIMIT_RSS;

    public static int RLIMIT_AS
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_AS : (int)linux__rlimit_resource.RLIMIT_AS;

    public static int RLIMIT_MEMLOCK
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_MEMLOCK : (int)linux__rlimit_resource.RLIMIT_MEMLOCK;

    public static int RLIMIT_NPROC
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_NPROC : (int)linux__rlimit_resource.RLIMIT_NPROC;

    public static int RLIMIT_NOFILE
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)macos__rlimit_resource.RLIMIT_NOFILE : (int)linux__rlimit_resource.RLIMIT_NOFILE;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_OFILE => RLIMIT_NOFILE;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_LOCKS => (int)linux__rlimit_resource.RLIMIT_LOCKS;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_SIGPENDING => (int)linux__rlimit_resource.RLIMIT_SIGPENDING;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_MSGQUEUE => (int)linux__rlimit_resource.RLIMIT_MSGQUEUE;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_NICE => (int)linux__rlimit_resource.RLIMIT_NICE;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_RTPRIO => (int)linux__rlimit_resource.RLIMIT_RTPRIO;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RLIMIT_RTTIME => (int)linux__rlimit_resource.RLIMIT_RTTIME;

    private static int RLIM_NLIMITS
        => OSVersion.Platform == PlatformID.MacOSX ?
            (int)(macos__rlimit_resource.RLIM_NLIMITS) : (int)(linux__rlimit_resource.RLIM_NLIMITS);

    #endregion

    public static PythonTuple getrlimit(int resource) {
        if (resource < 0 || resource >= RLIM_NLIMITS) {
            throw PythonOps.ValueError("invalid resource specified");
        }

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
        try {
            int err = getrlimit_linux(resource, ptr);
            ThrowIfError(err);
            rlimit res = Marshal.PtrToStructure<rlimit>(ptr);

            return PythonTuple.MakeTuple(res.rlim_cur.ToPythonInt(), res.rlim_max.ToPythonInt());
        } finally {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static void setrlimit(int resource, [NotNone] IEnumerable limits) {
        if (resource < 0 || resource >= RLIM_NLIMITS) {
            throw PythonOps.ValueError("invalid resource specified");
        }

        rlimit data;
        var cursor = limits.GetEnumerator();
        data.rlim_cur = GetLimitValue(cursor);
        data.rlim_max = GetLimitValue(cursor);
        if (cursor.MoveNext()) ThrowValueError();

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
        try {
            Marshal.StructureToPtr(data, ptr, fDeleteOld: false);
            int err = setrlimit_linux(resource, ptr);
            ThrowIfError(err);
        } finally {
            Marshal.FreeHGlobal(ptr);
        }

        static long GetLimitValue(IEnumerator cursor) {
            if (!cursor.MoveNext() || !PythonOps.TryToIndex(cursor.Current, out BigInteger lim))
                ThrowValueError();

            long rlim = checked((long)lim);
            if (rlim < 0 && OSVersion.Platform == PlatformID.MacOSX)
                rlim -= long.MinValue;
            return rlim;
        }

        static void ThrowValueError() => throw PythonOps.ValueError("expected a tuple of 2 integers");
    }

    private static void ThrowIfError(int err) {
        if (err != 0) {
#if NET60_OR_GREATER
            int errno = Marshal.GetLastPInvokeError();
#else
            int errno = Marshal.GetLastWin32Error();
#endif
            throw PythonOps.OSError(errno, PythonNT.strerror(errno));
        }
    }

    private static object ToPythonInt(this long value)
        => value is <= int.MaxValue and >= int.MinValue ? (int)value : (BigInteger)value;

    private struct rlimit {
        public long rlim_cur;
        public long rlim_max;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "getrlimit")]
    private static extern int getrlimit_linux(int __resource, IntPtr __rlimits);

    [DllImport("libc", SetLastError = true, EntryPoint = "setrlimit")]
    private static extern int setrlimit_linux(int __resource, IntPtr __rlimits);

    enum macos__rlimit_resource {
        RLIMIT_CPU = 0,
        RLIMIT_FSIZE = 1,
        RLIMIT_DATA = 2,
        RLIMIT_STACK = 3,
        RLIMIT_CORE = 4,
        RLIMIT_RSS = 5,
        RLIMIT_AS = 5,
        RLIMIT_MEMLOCK = 6,
        RLIMIT_NPROC = 7,
        RLIMIT_NOFILE = 8,

        RLIM_NLIMITS
    }

    enum linux__rlimit_resource {
        // /usr/include/x86_64-linux-gnu/sys/resource.h 

        RLIMIT_CPU = 0, // Per-process CPU limit
        RLIMIT_FSIZE = 1,  // Maximum filesize
        RLIMIT_DATA = 2,  // Maximum size of data segment
        RLIMIT_STACK = 3,  // Maximum size of stack segment
        RLIMIT_CORE = 4,  // Maximum size of core file
        RLIMIT_RSS = 5,  // Largest resident set size
        RLIMIT_NPROC = 6,  // Maximum number of processes
        RLIMIT_NOFILE = 7, // Maximum number of open files
        RLIMIT_MEMLOCK = 8,  // Maximum locked-in-memory address space
        RLIMIT_AS = 9,  // Address space limit
        RLIMIT_LOCKS = 10,  // Maximum number of file locks
        RLIMIT_SIGPENDING = 11,  // Maximum number of pending signals
        RLIMIT_MSGQUEUE = 12,  // Maximum bytes in POSIX message queues
        RLIMIT_NICE = 13,  // Maximum nice prio allowed to raise to (20 added to get a non-negative number)
        RLIMIT_RTPRIO = 14,  // Maximum realtime priority
        RLIMIT_RTTIME = 15,  // Time slice in us

        RLIM_NLIMITS
    };
}
