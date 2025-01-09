// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
#define SPAN_OVERRIDE  // Stream has Span<T>-based virtual methods
#endif

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Mono.Unix;
using Mono.Unix.Native;

using IronPython.Runtime.Operations;
using System.Diagnostics;
using IronPython.Runtime.Exceptions;

#nullable enable

namespace IronPython.Runtime;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal class PosixFileStream : Stream
{
    private readonly int _fd;
    private readonly bool _canSeek;
    private readonly bool _canRead;
    private readonly bool _canWrite;

    private bool _disposed;


    public PosixFileStream(int fileDescriptor) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("This stream only works on POSIX systems");

        if (fileDescriptor < 0)
            throw PythonOps.OSError(PythonFileManager.EBADF, "Bad file descriptor");

        _fd = fileDescriptor;

        _canSeek = Syscall.lseek(fileDescriptor, 0, SeekFlags.SEEK_CUR) >= 0;
        _canRead = Syscall.read(fileDescriptor, IntPtr.Zero, 0) == 0;
        _canWrite = Syscall.write(fileDescriptor, IntPtr.Zero, 0) == 0;
    }


    public static Stream Open(string name, int flags, uint mode, out int fd) {
        OpenFlags openFlags = NativeConvert.ToOpenFlags(flags);
        FilePermissions permissions = NativeConvert.ToFilePermissions(mode);
        Errno errno;

        do {
            fd = Syscall.open(name, openFlags, permissions);
        } while (UnixMarshal.ShouldRetrySyscall(fd, out errno));

        if (fd < 0) {
            Debug.Assert(errno != 0);
            throw CreateExceptionForLastError(errno, name);
        }
        return new PosixFileStream(fd);
    }


    public int Handle => _fd;

    public override bool CanSeek => _canSeek;
    public override bool CanRead => _canRead;
    public override bool CanWrite => _canWrite;


    public override long Length {
        get {
            ThrowIfDisposed();
            int res = Syscall.fstat(_fd, out Stat stat);
            ThrowIfError(res);

            return stat.st_size;
        }
    }


    public override long Position {
        get => Seek(0, SeekOrigin.Current);
        set => Seek(value, SeekOrigin.Begin);
    }


    public override long Seek(long offset, SeekOrigin origin) {
        ThrowIfDisposed();
        SeekFlags whence = origin switch
        {
            SeekOrigin.Begin => SeekFlags.SEEK_SET,
            SeekOrigin.Current => SeekFlags.SEEK_CUR,
            SeekOrigin.End => SeekFlags.SEEK_END,
            _ => throw PythonOps.OSError(PythonFileManager.EINVAL, "Invalid argument")
        };

        long result = Syscall.lseek(_fd, offset, whence);
        ThrowIfError(result);

        return result;
    }


    public override void SetLength(long value) {
        ThrowIfDisposed();
        int result;
        Errno errno;
        do {
            result = Syscall.ftruncate(_fd, value);
        } while (UnixMarshal.ShouldRetrySyscall(result, out errno));
        ThrowIfError(errno);
    }


#if SPAN_OVERRIDE
#pragma warning disable IDE0036  // Modifiers are not ordered
    override
#pragma warning restore IDE0036
#endif
    public int Read(Span<byte> buffer) {
        ThrowIfDisposed();
        if (!CanRead)
            throw PythonOps.OSError(PythonFileManager.EBADF, "Bad file descriptor");


        if (buffer.Length == 0)
            return 0;

        int bytesRead;
        Errno errno;
        unsafe {
            fixed (byte* buf = buffer) {
                do {
                    bytesRead = (int)Syscall.read(_fd, buf, (ulong)buffer.Length);
                } while (UnixMarshal.ShouldRetrySyscall(bytesRead, out errno));
            }
        }
        ThrowIfError(errno);

        return bytesRead;
    }


    // If offset == 0 and count == 0, buffer is allowed to be null.
    public override int Read(byte[] buffer, int offset, int count) {
        ThrowIfDisposed();
        return Read(buffer.AsSpan(offset, count));
    }


    public override int ReadByte() {
        Span<byte> buffer = stackalloc byte[1];
        int bytesRead = Read(buffer);
        return bytesRead == 0 ? -1 : buffer[0];
    }


#if SPAN_OVERRIDE
#pragma warning disable IDE0036  // Modifiers are not ordered
    override
#pragma warning restore IDE0036
#endif
    public void Write(ReadOnlySpan<byte> buffer) {
        ThrowIfDisposed();
        if (!CanWrite)
            throw PythonOps.OSError(PythonFileManager.EBADF, "Bad file descriptor");

        if (buffer.Length == 0)
            return;

        int bytesWritten;
        Errno errno;
        unsafe {
            fixed (byte* buf = buffer) {
                do {
                    bytesWritten = (int)Syscall.write(_fd, buf, (ulong)buffer.Length);
                } while (UnixMarshal.ShouldRetrySyscall(bytesWritten, out errno));
            }
        }
        ThrowIfError(errno);
    }

    // If offset == 0 and count == 0, buffer is allowed to be null.
    public override void Write(byte[] buffer, int offset, int count) {
        ThrowIfDisposed();
        Write(buffer.AsSpan(offset, count));
    }


    public override void WriteByte(byte value) {
        Span<byte> buffer = stackalloc byte[] { value };
        Write(buffer);
    }


    public override void Flush() {
        ThrowIfDisposed();
        int result;
        Errno errno;
        do {
            result = Syscall.fsync(_fd);
        } while (UnixMarshal.ShouldRetrySyscall(result, out errno));
        ThrowIfError(errno);
    }

    protected override void Dispose(bool disposing) {
        if (!_disposed) {
            int result = Syscall.close(_fd);
            WarnIfError(result, "Error closing file descriptor {0}: {1}: {2}");
            _disposed = true;
        }
        base.Dispose(disposing);
    }


    #region Private Methods

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }


    private static void ThrowIfError(long result) {
        if (result < 0)
            throw CreateExceptionForLastError();
    }


    private static void ThrowIfError(Errno errno) {
        if (errno != 0)
            throw CreateExceptionForLastError(errno);
    }


    private static Exception CreateExceptionForLastError(string? filename = null) {
        Errno errno = Stdlib.GetLastError();
        return CreateExceptionForLastError(errno, filename);
    }


    private static Exception CreateExceptionForLastError(Errno errno, string? filename = null) {
        if (errno == 0) return new InvalidOperationException("Unknown error");

        string msg = UnixMarshal.GetErrorDescription(errno);
        int error = NativeConvert.FromErrno(errno);
        return PythonOps.OSError(error, msg, filename);
    }

    private void WarnIfError(int result, string msgTmpl) {
        if (result < 0) {
            Errno errno = Stdlib.GetLastError();
            int error = NativeConvert.FromErrno(errno);
            PythonOps.Warn(DefaultContext.Default,
                PythonExceptions.RuntimeWarning,
                msgTmpl, _fd, error, UnixMarshal.GetErrorDescription(errno));
        }
    }

    #endregion
}
