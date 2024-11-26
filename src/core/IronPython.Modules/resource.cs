// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (BigInteger)long.MaxValue : BigInteger.MinusOne;

    public static int RLIMIT_CPU
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_CPU : (int)linux__rlimit_resource.RLIMIT_CPU;

    public static int RLIMIT_FSIZE
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_FSIZE : (int)linux__rlimit_resource.RLIMIT_FSIZE;

    public static int RLIMIT_DATA
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_DATA : (int)linux__rlimit_resource.RLIMIT_DATA;

    public static int RLIMIT_STACK
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_STACK : (int)linux__rlimit_resource.RLIMIT_STACK;

    public static int RLIMIT_CORE
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_CORE : (int)linux__rlimit_resource.RLIMIT_CORE;

    public static int RLIMIT_RSS
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_RSS : (int)linux__rlimit_resource.RLIMIT_RSS;

    public static int RLIMIT_AS
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_AS : (int)linux__rlimit_resource.RLIMIT_AS;

    public static int RLIMIT_MEMLOCK
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_MEMLOCK : (int)linux__rlimit_resource.RLIMIT_MEMLOCK;

    public static int RLIMIT_NPROC
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)macos__rlimit_resource.RLIMIT_NPROC : (int)linux__rlimit_resource.RLIMIT_NPROC;

    public static int RLIMIT_NOFILE
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
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
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (int)(macos__rlimit_resource.RLIM_NLIMITS) : (int)(linux__rlimit_resource.RLIM_NLIMITS);

    public static int RUSAGE_SELF => 0;

    public static int RUSAGE_CHILDREN => -1;

    [PythonHidden(PlatformID.MacOSX)]
    public static int RUSAGE_THREAD => 1;

    #endregion

    [LightThrowing]
    public static object getrlimit(int resource) {
        if (resource < 0 || resource >= RLIM_NLIMITS) {
            return LightExceptions.Throw(PythonOps.ValueError("invalid resource specified"));
        }

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
        try {
            int retval = getrlimit(resource, ptr);
            if (retval != 0) return GetPInvokeError();

            rlimit res = Marshal.PtrToStructure<rlimit>(ptr);
            return PythonTuple.MakeTuple(res.rlim_cur.ToPythonInt(), res.rlim_max.ToPythonInt());
        } finally {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [LightThrowing]
    public static object? setrlimit(int resource, [NotNone] object limits) {
        if (resource < 0 || resource >= RLIM_NLIMITS) {
            return LightExceptions.Throw(PythonOps.ValueError("invalid resource specified"));
        }

        rlimit data;
        var cursor = PythonOps.GetEnumerator(limits);
        if (GetLimitValue(cursor) is not long rlim_cur) return LimitsArgError();
        data.rlim_cur = unchecked((ulong)rlim_cur);

        if (GetLimitValue(cursor) is not long rlim_max) return LimitsArgError();
        data.rlim_max = unchecked((ulong)rlim_max);

        if (cursor.MoveNext()) return LimitsArgError();
        if (data.rlim_cur > data.rlim_max) return LightExceptions.Throw(PythonOps.ValueError("current limit exceed maximum limit"));

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
        try {
            Marshal.StructureToPtr(data, ptr, fDeleteOld: false);
            int retval = setrlimit(resource, ptr);
            // TODO: for full CPython compliance, return ValueError iso OSError if non-superuser tries to raise max limit
            if (retval != 0) return GetPInvokeError();
        } finally {
            Marshal.FreeHGlobal(ptr);
        }
        return null;
    }

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    [LightThrowing]
    public static object prlimit(int pid, int resource) {
        if (resource < 0 || resource >= RLIM_NLIMITS) {
            return LightExceptions.Throw(PythonOps.ValueError("invalid resource specified"));
        }

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
        try {
            int retval = prlimit(pid, resource, IntPtr.Zero, ptr);
            if (retval != 0) return GetPInvokeError();

            rlimit res = Marshal.PtrToStructure<rlimit>(ptr);
            return PythonTuple.MakeTuple(res.rlim_cur.ToPythonInt(), res.rlim_max.ToPythonInt());
        } finally {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    [LightThrowing]
    public static object prlimit(int pid, int resource, [NotNone] object limits) {
        if (resource < 0 || resource >= RLIM_NLIMITS) {
            return LightExceptions.Throw(PythonOps.ValueError("invalid resource specified"));
        }

        rlimit data;
        var cursor = PythonOps.GetEnumerator(limits);
        if (GetLimitValue(cursor) is not long rlim_cur) return LimitsArgError();
        data.rlim_cur = unchecked((ulong)rlim_cur);

        if (GetLimitValue(cursor) is not long rlim_max) return LimitsArgError();
        data.rlim_max = unchecked((ulong)rlim_max);

        if (cursor.MoveNext()) return LimitsArgError();
        if (data.rlim_cur > data.rlim_max) return LightExceptions.Throw(PythonOps.ValueError("current limit exceed maximum limit"));

        IntPtr ptr_new = IntPtr.Zero;
        IntPtr ptr_old = IntPtr.Zero;
        try {
            ptr_new = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
            ptr_old = Marshal.AllocHGlobal(Marshal.SizeOf<rlimit>());
            Marshal.StructureToPtr(data, ptr_new, fDeleteOld: false);
            int retval = prlimit(pid, resource, ptr_new, ptr_old);
            // TODO: for full CPython compliance, return ValueError iso OSError if non-superuser tries to raise max limit
            if (retval != 0) return GetPInvokeError();

            rlimit res = Marshal.PtrToStructure<rlimit>(ptr_old);
            return PythonTuple.MakeTuple(res.rlim_cur.ToPythonInt(), res.rlim_max.ToPythonInt());
        } finally {
            if (ptr_new != IntPtr.Zero) Marshal.FreeHGlobal(ptr_new);
            if (ptr_old != IntPtr.Zero) Marshal.FreeHGlobal(ptr_old);
        }
    }

    [LightThrowing]
    public static object getrusage(int who) {
        int maxWho = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0 : 1;
        if (who < -1 || who > maxWho) return LightExceptions.Throw(PythonOps.ValueError("invalid who parameter"));

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<rusage>());
        try {
            int retval = getrusage(who, ptr);
            if (retval != 0) return GetPInvokeError();

            rusage res = Marshal.PtrToStructure<rusage>(ptr);
            return new struct_rusage(res);
        } finally {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static int getpagesize() {
        return Environment.SystemPageSize;
    }

    [PythonType]
    public sealed class struct_rusage : PythonTuple {
        public const int n_fields = 16;
        public const int n_sequence_fields = 16;
        public const int n_unnamed_fields = 0;

        private struct_rusage(object?[] sequence) : base(sequence) {
            if (__len__() != n_sequence_fields)
                throw PythonOps.TypeError("resource.struct_rusage() takes a {0}-sequence ({1}-sequence given)", n_sequence_fields, __len__());
        }

        private struct_rusage(object o) : base(o) {
            if (__len__() != n_sequence_fields)
                throw PythonOps.TypeError("resource.struct_rusage() takes a {0}-sequence ({1}-sequence given)", n_sequence_fields, __len__());
        }

        internal struct_rusage(rusage data)
            : this(
                new object[n_sequence_fields] {
                    data.ru_utime.GetTime(),
                    data.ru_stime.GetTime(),
                    data.ru_maxrss.ToPythonInt(),
                    data.ru_ixrss.ToPythonInt(),
                    data.ru_idrss.ToPythonInt(),
                    data.ru_isrss.ToPythonInt(),
                    data.ru_minflt.ToPythonInt(),
                    data.ru_majflt.ToPythonInt(),
                    data.ru_nswap.ToPythonInt(),
                    data.ru_inblock.ToPythonInt(),
                    data.ru_oublock.ToPythonInt(),
                    data.ru_msgsnd.ToPythonInt(),
                    data.ru_msgrcv.ToPythonInt(),
                    data.ru_nsignals.ToPythonInt(),
                    data.ru_nvcsw.ToPythonInt(),
                    data.ru_nivcsw.ToPythonInt(),
                }
            ) { }

        public static struct_rusage __new__([NotNone] PythonType cls, [NotNone] object sequence, PythonDictionary? dict = null)
            => new struct_rusage(sequence);

        public object? ru_utime => this[0];
        public object? ru_stime => this[1];
        public object? ru_maxrss => this[2];
        public object? ru_ixrss => this[3];
        public object? ru_idrss => this[4];
        public object? ru_isrss => this[5];
        public object? ru_minflt => this[6];
        public object? ru_majflt => this[7];
        public object? ru_nswap => this[8];
        public object? ru_inblock => this[9];
        public object? ru_oublock => this[10];
        public object? ru_msgsnd => this[11];
        public object? ru_msgrcv => this[12];
        public object? ru_nsignals => this[13];
        public object? ru_nvcsw => this[14];
        public object? ru_nivcsw => this[15];

        public override string __repr__(CodeContext/*!*/ context) {
            return string.Format("resource.struct_rusage("
                + "ru_utime={0}, "
                + "ru_stime={1}, "
                + "ru_maxrss={2}, "
                + "ru_ixrss={3}, "
                + "ru_idrss={4}, "
                + "ru_isrss={5}, "
                + "ru_minflt={6}, "
                + "ru_majflt={7}, "
                + "ru_nswap={8}, "
                + "ru_inblock={9}, "
                + "ru_oublock={10}, "
                + "ru_msgsnd={11}, "
                + "ru_msgrcv={12}, "
                + "ru_nsignals={13}, "
                + "ru_nvcsw={14}, "
                + "ru_nivcsw={15})",
                this.Select(v => PythonOps.Repr(context, v)).ToArray());
        }

    }

    private static long? GetLimitValue(System.Collections.IEnumerator cursor) {
        if (!cursor.MoveNext() || !PythonOps.TryToIndex(cursor.Current, out BigInteger lim))
            return null;

        long rlim = checked((long)lim);
        if (rlim < 0 && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            rlim -= long.MinValue;
        return rlim;
    }

    private static object LimitsArgError()
        => LightExceptions.Throw(PythonOps.ValueError("expected a tuple of 2 integers"));

    private static object GetPInvokeError() {
        int errno = Marshal.GetLastWin32Error(); // despite its name, on Posix it retrieves errno set by the last p/Invoke call
        return LightExceptions.Throw(PythonOps.OSError(errno, PythonNT.strerror(errno)));
    }

    private static object ToPythonInt(this ulong value)
        => unchecked((long)value).ToPythonInt();

    private static object ToPythonInt(this long value)
        => value is <= int.MaxValue and >= int.MinValue ? (int)value : (BigInteger)value;

    private struct rlimit {
        // /usr/include/x86_64-linux-gnu/bits/resource.h
        // /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/resource.h

        public ulong rlim_cur;
        public ulong rlim_max;
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    internal struct timeval {
        // /usr/include/x86_64-linux-gnu/bits/types/struct_timeval.h, .../bits/types.h
        // /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/_types/_timeval.h, .../i386/_types.h, .../arm64/_types.h

        public long tv_sec;
        public long tv_usec;    // long on Linux but int on Darwin

        public double GetTime()
            => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                tv_sec + (tv_usec & 0xFFFF_FFFF) * 1e-6 :
                tv_sec + tv_usec * 1e-6;
    }

    internal struct rusage {
        // /usr/include/x86_64-linux-gnu/bits/types/struct_rusage.h
        // /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/resource.h

        public timeval ru_utime;        // user CPU time used
        public timeval ru_stime;        // system CPU time used
        public long    ru_maxrss;       // maximum resident set size
        public long    ru_ixrss;        // integral shared memory size
        public long    ru_idrss;        // integral unshared data size
        public long    ru_isrss;        // integral unshared stack size
        public long    ru_minflt;       // page reclaims (soft page faults)
        public long    ru_majflt;       // page faults (hard page faults)
        public long    ru_nswap;        // swaps
        public long    ru_inblock;      // block input operations
        public long    ru_oublock;      // block output operations
        public long    ru_msgsnd;       // IPC messages sent
        public long    ru_msgrcv;       // IPC messages received
        public long    ru_nsignals;     // signals received
        public long    ru_nvcsw;        // voluntary context switches
        public long    ru_nivcsw;       // involuntary context switches
    }
#pragma warning restore CS0649

    [DllImport("libc", SetLastError = true)]
    private static extern int getrlimit(int resource, /*rlimit*/ IntPtr rlimits);

    [DllImport("libc", SetLastError = true)]
    private static extern int setrlimit(int resource, /*const rlimit*/ IntPtr rlimits);

    [DllImport("libc", SetLastError = true)]
    [SupportedOSPlatform("linux")]
    private static extern int prlimit(int pid, int resource, /*const rlimit*/ IntPtr new_limit, /*rlimit*/ IntPtr old_limit);

    [DllImport("libc", SetLastError = true)]
    private static extern int getrusage(int who, /*rusage*/ IntPtr usage);


    private enum linux__rlimit_resource {
        // /usr/include/x86_64-linux-gnu/sys/resource.h

        RLIMIT_CPU = 0,          // Per-process CPU limit
        RLIMIT_FSIZE = 1,        // Maximum filesize
        RLIMIT_DATA = 2,         // Maximum size of data segment
        RLIMIT_STACK = 3,        // Maximum size of stack segment
        RLIMIT_CORE = 4,         // Maximum size of core file
        RLIMIT_RSS = 5,          // Largest resident set size
        RLIMIT_NPROC = 6,        // Maximum number of processes
        RLIMIT_NOFILE = 7,       // Maximum number of open files
        RLIMIT_MEMLOCK = 8,      // Maximum locked-in-memory address space
        RLIMIT_AS = 9,           // Address space limit
        RLIMIT_LOCKS = 10,       // Maximum number of file locks
        RLIMIT_SIGPENDING = 11,  // Maximum number of pending signals
        RLIMIT_MSGQUEUE = 12,    // Maximum bytes in POSIX message queues
        RLIMIT_NICE = 13,        // Maximum nice prio allowed to raise to (20 added to get a non-negative number)
        RLIMIT_RTPRIO = 14,      // Maximum realtime priority
        RLIMIT_RTTIME = 15,      // Time slice in us

        RLIM_NLIMITS
    };

    private enum macos__rlimit_resource {
        // /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/resource.h

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
}
