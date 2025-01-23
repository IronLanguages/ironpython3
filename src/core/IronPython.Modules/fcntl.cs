// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Mono.Unix;
using Mono.Unix.Native;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;


[assembly: PythonModule("fcntl", typeof(IronPython.Modules.PythonFcntl), PlatformsAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public static class PythonFcntl {

    public const string __doc__ = """
        This module performs file control and I/O control on file descriptors.
        It is an interface to the fcntl() and ioctl() Unix routines.
        File descriptors can be obtained with the fileno() method of
        a file object.
        """;


    #region  fcntl

    [LightThrowing]
    public static object fcntl(int fd, int cmd, [NotNone] Bytes arg) {
        CheckFileDescriptor(fd);

        const int maxArgSize = 1024;
        if (arg.Count > maxArgSize) {
            throw PythonOps.ValueError("fcntl bytes arg too long");
        }

        if (!NativeConvert.TryToFcntlCommand(cmd, out FcntlCommand fcntlCommand)) {
            throw PythonOps.OSError(PythonErrno.EINVAL, "unsupported fcntl command");
        }

        int argSize = arg.Count;
        IntPtr ptr = Marshal.AllocHGlobal(argSize);
        try {
            Marshal.Copy(arg.UnsafeByteArray, 0, ptr, argSize);
            int result;
            Errno errno;
            do {
                result = Syscall.fcntl(fd, fcntlCommand, ptr);
            } while (UnixMarshal.ShouldRetrySyscall(result, out errno));

            if (result == -1) {
                return LightExceptions.Throw(PythonNT.GetOsError(NativeConvert.FromErrno(errno)));
            }
            byte[] response = new byte[argSize];
            Marshal.Copy(ptr, response, 0, argSize);
            return Bytes.Make(response);
        } finally {
            Marshal.FreeHGlobal(ptr);
        }
    }


    [LightThrowing]
    public static object fcntl(int fd, int cmd, [Optional] object? arg) {
        CheckFileDescriptor(fd);

        long data = arg switch {
            Missing => 0,
            int i => i,
            uint ui => ui,
            long l => l,
            ulong ul => (long)ul,
            BigInteger bi => (long)bi,
            Extensible<BigInteger> ebi => (long)ebi.Value,
            _ => throw PythonOps.TypeErrorForBadInstance("integer argument expected, got {0}", arg)
        };

        if (!NativeConvert.TryToFcntlCommand(cmd, out FcntlCommand fcntlCommand)) {
            throw PythonOps.OSError(PythonErrno.EINVAL, "unsupported fcntl command");
        }

        int result;
        Errno errno;
        do {
            result = Syscall.fcntl(fd, fcntlCommand, data);
        } while (UnixMarshal.ShouldRetrySyscall(result, out errno));

        if (result == -1) {
            return LightExceptions.Throw(PythonNT.GetOsError(NativeConvert.FromErrno(errno)));
        }
        return ScriptingRuntimeHelpers.Int32ToObject(result);
    }


    [LightThrowing]
    public static object fcntl(CodeContext context, object? fd, int cmd, object? arg = null) {
        int fileno = GetFileDescriptor(context, fd);

        if (arg is Bytes bytes) {
            return fcntl(fileno, cmd, bytes);
        }

        return fcntl(fileno, cmd, arg);
    }

    #endregion


    #region  flock

    [DllImport("libc", SetLastError = true, EntryPoint = "flock")]
    private static extern int _flock(int fd, int op);

    [LightThrowing]
    public static object? flock(int fd, int operation) {
        CheckFileDescriptor(fd);

        int result;
        int errno = 0;
        do {
            result = _flock(fd, operation);
        } while (result == -1 && (errno = Marshal.GetLastWin32Error()) == PythonErrno.EINTR);

        if (result == -1) {
            return LightExceptions.Throw(PythonNT.GetOsError(errno));
        }
        return null;
    }


    [LightThrowing]
    public static object? flock(CodeContext context, object? fd, int operation)
        => flock(GetFileDescriptor(context, fd), operation);

    #endregion


    #region  lockf

    [LightThrowing]
    public static object? lockf(int fd, int cmd, long len = 0, long start = 0, int whence = 0) {
        CheckFileDescriptor(fd);

        Flock flock = new() {
            l_whence = (SeekFlags)whence,
            l_start = start,
            l_len = len
        };
        if (cmd == LOCK_UN) {
            flock.l_type = LockType.F_UNLCK;
        } else if ((cmd & LOCK_SH) != 0) {
            flock.l_type = LockType.F_RDLCK;
        } else if ((cmd & LOCK_EX) != 0) {
            flock.l_type = LockType.F_WRLCK;
        } else {
            throw PythonOps.ValueError("unrecognized lockf argument");
        }

        int result;
        Errno errno;
        do {
            result = Syscall.fcntl(fd, (cmd & LOCK_NB) != 0 ? FcntlCommand.F_SETLK : FcntlCommand.F_SETLKW, ref flock);
        } while (UnixMarshal.ShouldRetrySyscall(result, out errno));

        if (result == -1) {
            return LightExceptions.Throw(PythonNT.GetOsError(NativeConvert.FromErrno(errno)));
        }
        return null;
    }


    [LightThrowing]
    public static object? lockf(CodeContext context, object? fd, int cmd, long len = 0, long start = 0, int whence = 0)
        => lockf(GetFileDescriptor(context, fd), cmd, len, start, whence);

    #endregion

    #region Private Methods

    private static int GetFileDescriptor(CodeContext context, object? obj) {
        if (!PythonOps.TryGetBoundAttr(context, obj, "fileno", out object? filenoMeth)) {
            throw PythonOps.TypeError("argument must be an int, or have a fileno() method.");
        }
        return PythonCalls.Call(context, filenoMeth) switch {
            int i => i,
            uint ui => (int)ui,
            BigInteger bi => (int)bi,
            Extensible<BigInteger> ebi => (int)ebi.Value,
            _ => throw PythonOps.TypeError("fileno() returned a non-integer")
        };
    }


    private static void CheckFileDescriptor(int fd) {
        if (fd < 0) {
            throw PythonOps.ValueError("file descriptor cannot be a negative integer ({0})", fd);
        }
    }

    #endregion


    // supporting fcntl.ioctl(fileno, termios.TIOCGWINSZ, buf)
    // where buf = array.array('h', [0, 0, 0, 0])
    public static object ioctl(CodeContext context, int fd, int cmd, [NotNone] IBufferProtocol arg, int mutate_flag = 1) {
        if (cmd == PythonTermios.TIOCGWINSZ) {
            using IPythonBuffer buf = arg.GetBuffer();

            Span<short> winsize = stackalloc short[4];
            winsize[0] = (short)Console.WindowHeight;
            winsize[1] = (short)Console.WindowWidth;
            winsize[2] = (short)Console.BufferHeight;  // buffer height and width are not accurate on macOS
            winsize[3] = (short)Console.BufferWidth;
            Span<byte> payload = MemoryMarshal.Cast<short, byte>(winsize);

            if (buf.IsReadOnly || mutate_flag == 0) {
                byte[] res = buf.ToArray();
                payload.Slice(0, Math.Min(payload.Length, res.Length)).CopyTo(res);
                return Bytes.Make(res);
            } else {
                var res = buf.AsSpan();
                payload.Slice(0, Math.Min(payload.Length, res.Length)).CopyTo(res);
                return 0;
            }
        }
        throw new NotImplementedException($"ioctl: unsupported command {cmd}");
    }


    // FD Flags
    public static int FD_CLOEXEC = 1;

    // O_* flags under F* name
    public static int FASYNC => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x0040 : 0x2000;  // O_ASYNC


    #region Generated FD Commands

    // *** BEGIN GENERATED CODE ***
    // generated by function: generate_FD_commands from: generate_os_codes.py


    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_ADD_SEALS => 1033;

    public static int F_DUPFD => 0;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_EXLCK => 4;

    public static int F_GETFD => 1;

    public static int F_GETFL => 3;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GETLEASE => 1025;

    public static int F_GETLK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 7 : 5;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GETLK64 => 5;

    public static int F_GETOWN => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 5 : 9;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GETSIG => 11;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GET_SEALS => 1034;

    [PythonHidden(PlatformID.Unix)]
    [SupportedOSPlatform("macos")]
    public static int F_NOCACHE => 48;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_NOTIFY => 1026;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_OFD_GETLK => 36;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_OFD_SETLK => 37;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_OFD_SETLKW => 38;

    public static int F_RDLCK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 1 : 0;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_GROW => 4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_SEAL => 1;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_SHRINK => 2;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_WRITE => 8;

    public static int F_SETFD => 2;

    public static int F_SETFL => 4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETLEASE => 1024;

    public static int F_SETLK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 6;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETLK64 => 6;

    public static int F_SETLKW => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 9 : 7;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETLKW64 => 7;

    public static int F_SETOWN => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 6 : 8;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETSIG => 10;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SHLCK => 8;

    public static int F_UNLCK => 2;

    public static int F_WRLCK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 3 : 1;

    // *** END GENERATED CODE ***

    #endregion


    #region Generated LOCK Flags

    // *** BEGIN GENERATED CODE ***
    // generated by function: generate_LOCK_flags from: generate_os_codes.py


    public static int LOCK_EX => 0x2;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_MAND => 0x20;

    public static int LOCK_NB => 0x4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_READ => 0x40;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_RW => 0xc0;

    public static int LOCK_SH => 0x1;

    public static int LOCK_UN => 0x8;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_WRITE => 0x80;

    // *** END GENERATED CODE ***

    #endregion


    // Linux only
    #region Generated Directory Notify Flags

    // *** BEGIN GENERATED CODE ***
    // generated by function: generate_DN_flags from: generate_os_codes.py


    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_ACCESS => 0x1;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_ATTRIB => 0x20;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_CREATE => 0x4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_DELETE => 0x8;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_MODIFY => 0x2;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static long DN_MULTISHOT => 0x80000000;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_RENAME => 0x10;

    // *** END GENERATED CODE ***

    #endregion
}