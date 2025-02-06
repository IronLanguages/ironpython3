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
using System.Text;

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

        const int maxArgSize = 1024;  // 1 KiB
        int argSize = arg.Count;
        if (argSize > maxArgSize) {
            throw PythonOps.ValueError("fcntl bytes arg too long");
        }

        if (!NativeConvert.TryToFcntlCommand(cmd, out FcntlCommand fcntlCommand)) {
            throw PythonOps.OSError(PythonErrno.EINVAL, "unsupported fcntl command");
        }

        var buf = new byte[maxArgSize];
        Array.Copy(arg.UnsafeByteArray, buf, argSize);

        int result;
        Errno errno;
        unsafe {
            fixed (byte* ptr = buf) {
                do {
                    result = Syscall.fcntl(fd, fcntlCommand, (IntPtr)ptr);
                } while (UnixMarshal.ShouldRetrySyscall(result, out errno));
            }
        }

        if (result == -1) {
            return LightExceptions.Throw(PythonNT.GetOsError(NativeConvert.FromErrno(errno)));
        }
        byte[] response = new byte[argSize];
        Array.Copy(buf, response, argSize);
        return Bytes.Make(response);
    }


    [LightThrowing]
    public static object fcntl(int fd, int cmd, [Optional] object? arg) {
        CheckFileDescriptor(fd);

        if (!TryGetInt64(arg, out long data)) {
            return arg switch {
                Bytes bytes           => fcntl(fd, cmd, bytes),
                string s              => fcntl(fd, cmd, Bytes.Make(Encoding.UTF8.GetBytes(s))),
                Extensible<string> es => fcntl(fd, cmd, Bytes.Make(Encoding.UTF8.GetBytes(es.Value))),
                _                     => throw PythonOps.TypeErrorForBadInstance("integer or bytes argument expected, got {0}", arg)
            };
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
    public static object fcntl(CodeContext context, object? fd, int cmd, [Optional] object? arg)
        => fcntl(GetFileDescriptor(context, fd), cmd, arg);

    #endregion


    #region  ioctl

    // The actual signature of ioctl is
    //
    //      int ioctl(int, unsigned long, ...)
    //
    // but .NET, as of Jan 2025, still does not support varargs in P/Invoke [1]
    // so as a workaround, nonvararg prototypes are defined for each architecture.
    // [1]: https://github.com/dotnet/runtime/issues/48796

#if NET10_0_OR_GREATER
#error Check if this version of .NET supports P/Invoke of variadic functions; if not, change the condition to recheck at next major .NET version
#endif

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern unsafe int _ioctl(int fd, ulong request, void* arg);
    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int _ioctl(int fd, ulong request, long arg);

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern unsafe int _ioctl_arm64(int fd, ulong request,
        // pad register arguments (first 8) to force vararg on stack
        // ARM: https://github.com/ARM-software/abi-aa/blob/main/aapcs64/aapcs64.rst#appendix-variable-argument-lists
        // Apple: https://developer.apple.com/documentation/xcode/writing-arm64-code-for-apple-platforms
        nint r2, nint r3, nint r4, nint r5, nint r6, nint r7,
        void* arg);
    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int _ioctl_arm64(int fd, ulong request,
        nint r2, nint r3, nint r4, nint r5, nint r6, nint r7,
        long arg);


    // request will be int, uint or BigInteger, and in Python is limited to values that can fit in 32 bits (unchecked)
    // long should capture all allowed request values
    // return value is int, bytes, or LightException
    [LightThrowing]
    public static object ioctl(int fd, long request, [NotNone] IBufferProtocol arg, bool mutate_flag = true) {
        CheckFileDescriptor(fd);

        ulong cmd = unchecked((ulong)request);

        const int defaultBufSize = 1024;
        int bufSize;
        IPythonBuffer? buf = null;

        if (mutate_flag) {
            buf = arg.GetBufferNoThrow(BufferFlags.Writable);
        }
        if (buf is not null) {
            bufSize = buf.AsSpan().Length;  // check early if buf is indeed writable
        } else {
            buf = arg.GetBuffer(BufferFlags.Simple);
            bufSize = buf.AsReadOnlySpan().Length;
            if (bufSize > defaultBufSize) {
                buf.Dispose();
                throw PythonOps.ValueError("ioctl bytes arg too long");
            }
            mutate_flag = false;  // return a buffer, not integer
        }
        bool in_place = bufSize > defaultBufSize;  // only large buffers are mutated in place

        try {
            Debug.Assert(!in_place || mutate_flag);    // in_place implies mutate_flag

            Span<byte> workSpan;
            if (in_place) {
                workSpan = buf.AsSpan();
            } else {
                workSpan = new byte[defaultBufSize + 1];  // +1 for extra NUL byte
                Debug.Assert(bufSize <= defaultBufSize);
                buf.AsReadOnlySpan().CopyTo(workSpan);
            }
            int result;
            Errno errno;
            unsafe {
                fixed (byte* ptr = workSpan) {
                    do {
                        if (IsArchitecutreArm64()) {
                            // workaround for Arm64 vararg calling convention (but not for ARM64EC on Windows)
                            result = _ioctl_arm64(fd, cmd, 0, 0, 0, 0, 0, 0, ptr);
                        } else {
                            result = _ioctl(fd, cmd, ptr);
                        }
                    } while (UnixMarshal.ShouldRetrySyscall(result, out errno));
                }
            }

            if (result == -1) {
                return LightExceptions.Throw(PythonNT.GetOsError(NativeConvert.FromErrno(errno)));
            }
            if (mutate_flag) {
                if (!in_place) {
                    workSpan.Slice(0, bufSize).CopyTo(buf.AsSpan());
                }
                return ScriptingRuntimeHelpers.Int32ToObject(result);
            } else {
                Debug.Assert(!in_place);
                byte[] response = new byte[bufSize];
                workSpan.Slice(0, bufSize).CopyTo(response);
                return Bytes.Make(response);
            }
        } finally {
            buf.Dispose();
        }
    }


    [LightThrowing]
    public static object ioctl(int fd, long request, [Optional] object? arg, bool mutate_flag = true) {
        CheckFileDescriptor(fd);

        if (!TryGetInt64(arg, out long data)) {
            return arg switch {
                IBufferProtocol bp    => ioctl(fd, request, bp),
                string s              => ioctl(fd, request, Bytes.Make(Encoding.UTF8.GetBytes(s))),
                Extensible<string> es => ioctl(fd, request, Bytes.Make(Encoding.UTF8.GetBytes(es.Value))),
                _                     => throw PythonOps.TypeErrorForBadInstance("integer or a bytes-like argument expected, got {0}", arg)
            };
        };

        ulong cmd = unchecked((ulong)request);

        int result;
        Errno errno;
        do {
            if (IsArchitecutreArm64()) {
                // workaround for Arm64 vararg calling convention (but not for ARM64EC on Windows)
                result = _ioctl_arm64(fd, cmd, 0, 0, 0, 0, 0, 0, data);
            } else {
                result = _ioctl(fd, cmd, data);
            }
        } while (UnixMarshal.ShouldRetrySyscall(result, out errno));

        if (result == -1) {
            return LightExceptions.Throw(PythonNT.GetOsError(NativeConvert.FromErrno(errno)));
        }
        return ScriptingRuntimeHelpers.Int32ToObject(result);
    }


    [LightThrowing]
    public static object ioctl(CodeContext context, object? fd, long request, [Optional] object? arg, bool mutate_flag = true)
        => ioctl(GetFileDescriptor(context, fd), request, arg, mutate_flag);

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


    #region Helper Methods

    internal static int GetFileDescriptor(CodeContext context, object? obj) {
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

    private static bool IsArchitecutreArm64() {
#if NETCOREAPP
        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
#else
        if (Syscall.uname(out Utsname info) == 0) {
            return info.machine is "arm64" or "aarch64";
        }
        return false;
#endif
    }

    private static bool TryGetInt64(object? obj, out long value) {
        value = default;
        if (obj is Missing) {
            return true;
        }
        if (PythonOps.TryToIndex(obj, out BigInteger bi)) {
            value = (long)bi;
            return true;
        }
        return false;
    }

    #endregion


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