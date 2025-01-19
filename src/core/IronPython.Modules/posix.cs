// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Win32.SafeHandles;

using Mono.Unix;
using Mono.Unix.Native;

using IronPython.Runtime;

namespace IronPython.Modules {
    // This file contains exclusively functions used on POSIX systems, heavily dependent on Mono.Unix.
    // Every function in this part of PythonNT must have a platform guard preventing it from being used on Windows.
    // This isolates Mono.Unix from the rest of the code so that we don't try to load the Mono.Unix assembly on Windows.
    // The main implementation of module `nt` still may contain some code that is POSIX-specific, but without using Mono.Unix.
    public static partial class PythonNT {
#if FEATURE_NATIVE

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        internal static Exception GetLastUnixError(string? filename = null, string? filename2 = null)
            // On POSIX, GetLastWin32Error returns the errno value, same as GetLastPInvokeError
            => GetOsError(Marshal.GetLastWin32Error(), filename, filename2);


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static int strerror_r(int code, StringBuilder buffer)
            => Syscall.strerror_r(NativeConvert.ToErrno(code), buffer);


#if FEATURE_PIPES
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static Tuple<int, Stream, int, Stream> CreatePipeStreamsUnix() {
            UnixPipes pipes = UnixPipes.CreatePipes();
            return Tuple.Create<int, Stream, int, Stream>(pipes.Reading.Handle, pipes.Reading, pipes.Writing.Handle, pipes.Writing);
        }
#endif


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static int DuplicateStreamDescriptorUnix(int fd, int targetfd, out Stream? stream) {
            int res = targetfd < 0 ? Syscall.dup(fd) : Syscall.dup2(fd, targetfd);
            if (res < 0) throw GetLastUnixError();

            if (ClrModule.IsMono) {
                // Elaborate workaround on Mono to avoid UnixStream as out
                stream = new UnixStream(res, ownsHandle: false);
                FileAccess fileAccess = stream.CanWrite ? stream.CanRead ? FileAccess.ReadWrite : FileAccess.Write : FileAccess.Read;
                stream.Dispose();
                try {
                    // FileStream on Mono created with a file descriptor might not work: https://github.com/mono/mono/issues/12783
                    // Test if it does, without closing the handle if it doesn't
                    var sfh = new SafeFileHandle((IntPtr)res, ownsHandle: false);
                    stream = new FileStream(sfh, fileAccess);
                    // No exception? Great! We can use FileStream.
                    stream.Dispose();
                    sfh.Dispose();
                    stream = null; // Create outside of try block
                } catch (IOException) {
                    // Fall back to UnixStream
                    stream = new UnixStream(res, ownsHandle: true);
                }
                if (stream is null) {
                    // FileStream is safe
                    var sfh = new SafeFileHandle((IntPtr)res, ownsHandle: true);
                    stream = new FileStream(sfh, fileAccess);
                }
            } else {
                // normal case
                stream = new PosixFileStream(res);
            }
            return res;
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        internal static int dupUnix(int fd, bool closeOnExec) {
            int fd2 = Syscall.dup(fd);
            if (fd2 == -1) throw GetLastUnixError();

            if (closeOnExec) {
                try {
                    // set close-on-exec flag
                    int flags = Syscall.fcntl(fd2, FcntlCommand.F_GETFD);
                    if (flags == -1) throw GetLastUnixError();
    
                    flags |= PythonFcntl.FD_CLOEXEC;
                    flags = Syscall.fcntl(fd2, FcntlCommand.F_SETFD, flags);
                    if (flags == -1) throw GetLastUnixError();
                } catch {
                    Syscall.close(fd2);
                    throw;
                }
            }

            return fd2;
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void chmodUnix(string path, int mode) {
            if (Syscall.chmod(path, NativeConvert.ToFilePermissions((uint)mode)) == 0) return;
            throw GetLastUnixError(path);
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void linkUnix(string src, string dst) {
            if (Syscall.link(src, dst) == 0) return;
            throw GetLastUnixError(src, dst);
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void symlinkUnix(string src, string dst) {
            if (Syscall.symlink(src, dst) == 0) return;
            throw GetLastUnixError(src, dst);
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void renameUnix(string src, string dst) {
            if (Syscall.rename(src, dst) == 0) return;
            throw GetLastUnixError(src, dst);
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static object statUnix(string path) {
            if (Syscall.stat(path, out Stat buf) == 0) {
                return new stat_result(buf);
            }
            return LightExceptions.Throw(GetLastUnixError(path));
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static object fstatUnix(int fd) {
            if (Syscall.fstat(fd, out Stat buf) == 0) {
                return new stat_result(buf);
            }
            return LightExceptions.Throw(GetLastUnixError());
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        internal static void ftruncateUnix(int fd, long length) {
            int result;
            Errno errno;
            do {
                result = Syscall.ftruncate(fd, length);
            } while (UnixMarshal.ShouldRetrySyscall(result, out errno));

            if (errno != 0)
                throw GetOsError(NativeConvert.FromErrno(errno));
        }


        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static uname_result uname() {
            if (Syscall.uname(out Utsname info) == 0) {
                return new uname_result(info.sysname, info.nodename, info.release, info.version, info.machine);
            }
            throw GetLastUnixError();  // rare
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static BigInteger getuid() {
            return Syscall.getuid();
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static BigInteger geteuid() {
            return Syscall.geteuid();
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void utimeUnix(string path, long atime_ns, long utime_ns) {
            var atime = new Timespec();
            atime.tv_sec = atime_ns / 1_000_000_000;
            atime.tv_nsec = atime_ns % 1_000_000_000;
            var utime = new Timespec();
            utime.tv_sec = utime_ns / 1_000_000_000;
            utime.tv_nsec = utime_ns % 1_000_000_000;

            if (Syscall.utimensat(Syscall.AT_FDCWD, path, new[] { atime, utime }, 0) == 0) return;
            throw GetLastUnixError(path);
        }


        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void killUnix(int pid, int sig) {
            if (Syscall.kill(pid, NativeConvert.ToSignum(sig)) == 0) return;
            throw GetLastUnixError();
        }

#endif
    }
}
