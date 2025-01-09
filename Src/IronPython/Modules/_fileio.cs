// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

#nullable enable


namespace IronPython.Modules {
    public static partial class PythonIOModule {
        [Documentation("""
            FileIO(name, mode='r', closefd=True, opener=None) -> file IO object

            Open a file.

            The mode can be 'r' (default), 'w', 'x' or 'a' for reading,
            writing, exclusive creation or appending.  The file will be created if it
            doesn't exist when opened for writing or appending; it will be truncated when
            opened for writing.  A FileExistsError will be raised if it already
            exists when opened for creating. Opening a file for creating implies
            writing so this mode behaves in a similar way to 'w'.
            Add a '+' to the mode to allow simultaneous reading and writing.

            A custom opener can be used by passing a callable as `opener`.
            The underlying file descriptor for the file object is then obtained
            by calling opener with (`name`, `flags`).
            `opener` must return an open file descriptor (passing os.open as `opener`
            results in functionality similar to passing None).
            """)]
        [PythonType, DontMapIDisposableToContextManager]
        public class FileIO : _RawIOBase, IDisposable, IWeakReferenceable, ICodeFormattable, IDynamicMetaObjectProvider {
            #region Fields and constructors

            private static readonly int DEFAULT_BUF_SIZE = 32;

            private readonly StreamBox _streams;
            private bool _closed, _closefd;
            private WeakRefTracker? _tracker;
            private readonly PythonContext _context;

            public object? name;


            internal FileIO(CodeContext/*!*/ context, Stream stream)
                : this(context, new StreamBox(stream)) {
            }


            internal FileIO(CodeContext/*!*/ context, StreamBox streams)
                : base(context) {
                _context = context.LanguageContext;

                this.mode = streams.WriteStream.CanWrite ? streams.ReadStream.CanRead ? "w+" : "w" : "r";
                _streams = streams;
                _closefd = !streams.IsConsoleStream();
            }


            public FileIO(CodeContext/*!*/ context, int fd, [NotNone] string mode = "r", bool closefd = true, object? opener = null)
                : base(context) {
                if (fd < 0) {
                    throw PythonOps.ValueError("fd must be >= 0");
                }

                _context = context.LanguageContext;
                this.mode = NormalizeMode(mode, out _);

                _streams = _context.FileManager.GetStreams(fd); // OSError here if no such fd

                name = fd;

                _closefd = closefd && !_streams.IsConsoleStream();
            }


            public FileIO(CodeContext/*!*/ context, [NotNone] string name, [NotNone] string mode = "r", bool closefd = true, object? opener = null)
                : base(context) {
                if (name.Contains('\0')) {
                    throw PythonOps.ValueError("embedded null character");
                }
                if (!closefd) {
                    throw PythonOps.ValueError("Cannot use closefd=False with file name");
                }

                this.name = name;
                PlatformAdaptationLayer pal = context.LanguageContext.DomainManager.Platform;

                _context = context.LanguageContext;
                this.mode = NormalizeMode(mode, out int flags);

                if (opener is null) {
                    if ((RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) && !ClrModule.IsMono) {
                        // Use PosixFileStream to operate on fd directly
                        // On Mono, we must use FileStream due to limitations in MemoryMappedFile
                        var stream = PosixFileStream.Open(name, flags, 0b_110_110_110, out int fd);  // mode: rw-rw-rw-
                        if ((flags & O_APPEND) != 0) {
                            stream.Seek(0L, SeekOrigin.End);
                        }
                        _streams = new(stream);
                        _context.FileManager.Add(fd, _streams);
                    } else {
                        switch (this.mode) {
                            case "rb":
                                _streams = new(OpenFile(context, pal, name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                                break;
                            case "wb":
                                _streams = new(OpenFile(context, pal, name, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                                break;
                            case "xb":
                                _streams = new(OpenFile(context, pal, name, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite));
                                break;
                            case "ab":
                                _streams = new(OpenFile(context, pal, name, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                                _streams.WriteStream.Seek(0L, SeekOrigin.End);
                                break;
                            case "rb+":
                                _streams = new(OpenFile(context, pal, name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
                                break;
                            case "wb+":
                                _streams = new(OpenFile(context, pal, name, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite));
                                break;
                            case "xb+":
                                _streams = new(OpenFile(context, pal, name, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite));
                                break;
                            case "ab+":
                                // Opening writeStream before readStream will create the file if it does not exist
                                var writeStream = OpenFile(context, pal, name, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                                var readStream = OpenFile(context, pal, name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                readStream.Seek(0L, SeekOrigin.End);
                                writeStream.Seek(0L, SeekOrigin.End);
                                _streams = new(readStream, writeStream);
                                break;
                            default:
                                throw new InvalidOperationException();
                        }
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                            // On POSIX, register the file descriptor with the file manager right after file opening
                            // This branch is needed for Mono, the .NET case is already handled above before `switch`
                            _context.FileManager.GetOrAssignId(_streams);
                            // according to [documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.safefilehandle?view=net-9.0#remarks)
                            // accessing SafeFileHandle sets the current stream position to 0
                            // in practice it doesn't seem to be the case, but better to be sure
                            if (this.mode[0] == 'a') {
                                _streams.WriteStream.Seek(0L, SeekOrigin.End);
                            }
                            if (!_streams.IsSingleStream) {
                                _streams.ReadStream.Seek(_streams.WriteStream.Position, SeekOrigin.Begin);
                            }
                        }
                    }
                } else {  // opener is not null
                    object? fdobj = PythonOps.CallWithContext(context, opener, name, flags);
                    if (fdobj is int fd) {
                        if (fd < 0) {
                            throw PythonOps.ValueError("opener returned {0}", fd);
                        }

                        if (_context.FileManager.TryGetStreams(fd, out StreamBox? streams)) {
                            _streams = streams;
                        } else {
                            // TODO: This is not necessarily an error on Posix.
                            // The descriptor could have been opened by a different means than os.open.
                            // In such case:
                            // _streams = new(new UnixStream(fd, ownsHandle: true))
                            // _context.FileManager.Add(fd, _streams);
                            throw PythonOps.OSError(PythonFileManager.EBADF, "Bad file descriptor");
                        }
                    } else {
                        throw PythonOps.TypeError("expected integer from opener");
                    }
                }

                _closefd = true;
            }


            private static string NormalizeMode(string mode, out int flags) {
                flags = 0;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    flags |= O_NOINHERIT | O_BINARY;
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    flags |= O_CLOEXEC;
                }
                switch (StandardizeMode(mode)) {
                    case "r":
                        flags |= O_RDONLY;
                        return "rb";
                    case "w":
                        flags |= O_CREAT | O_TRUNC | O_WRONLY;
                        return "wb";
                    case "a":
                        flags |= O_APPEND | O_CREAT | O_WRONLY;
                        return "ab";
                    case "x":
                        flags |= O_CREAT | O_EXCL | O_WRONLY;
                        return "xb";
                    case "r+":
                    case "+r":
                        flags |= O_RDWR;
                        return "rb+";
                    case "w+":
                    case "+w":
                        flags |= O_CREAT | O_TRUNC | O_RDWR;
                        return "wb+";
                    case "a+":
                    case "+a":
                        flags |= O_APPEND | O_CREAT | O_RDWR;
                        return "ab+";
                    case "x+":
                    case "+x":
                        flags |= O_CREAT | O_EXCL | O_RDWR;
                        return "xb+";
                    default:
                        throw BadMode(mode);
                }

                // remove all 'b's from mode string to simplify parsing
                static string StandardizeMode(string mode) {
                    int index = mode.IndexOf('b');
                    if (index == -1) return mode;

                    if (index == mode.Length - 1) {
                        mode = mode.Substring(0, index);
                    } else if (index >= 0) {
                        StringBuilder sb = new StringBuilder(mode.Substring(0, index), mode.Length - 1);
                        for (int pos = index + 1; pos < mode.Length; pos++) {
                            if (mode[pos] != 'b') {
                                sb.Append(mode[pos]);
                            }
                        }
                        mode = sb.ToString();
                    }

                    return mode;
                }

                static Exception BadMode(string mode) {
                    bool foundMode = false, foundPlus = false;
                    foreach (char c in mode) {
                        switch (c) {
                            case 'r':
                            case 'w':
                            case 'a':
                            case 'x':
                                if (foundMode) {
                                    return BadModeException();
                                } else {
                                    foundMode = true;
                                    continue;
                                }
                            case '+':
                                if (foundPlus) {
                                    return BadModeException();
                                } else {
                                    foundPlus = true;
                                    continue;
                                }
                            case 'b':
                                // any number of 'b's is acceptable
                                continue;
                            default:
                                return PythonOps.ValueError("invalid mode: {0}", mode);
                        }
                    }

                    return BadModeException();

                    static Exception BadModeException() => PythonOps.ValueError("Must have exactly one of create/read/write/append mode and at most one plus");
                }
            }

            #endregion

            #region Public API

            [Documentation("""
                close() -> None

                Flush and close the file.

                A closed file cannot be used for further I/O operations.
                close() may be called more than once without error.
                """)]
            public override void close(CodeContext/*!*/ context) {
                if (_closed) {
                    return;
                }

                try {
                    flush(context);
                } catch (IOException) { /* ignore */ } catch (OSException) { /* ignore */ }
                // flushing can fail, esp. if the other half of a pipe is closed
                // ignore it because we're closing anyway

                _closed = true;

                if (_closefd) {
                    _streams.CloseStreams(_context.RawFileManager);
                }
            }


            [Documentation("True if the file is closed")]
            public override bool closed => _closed;


            public bool closefd => _closefd;


            [Documentation("Return underlying file descriptor if one exists.")]
            public override int fileno(CodeContext/*!*/ context) {
                _checkClosed();

                return _context.FileManager.GetOrAssignId(_streams);
            }


            [Documentation("Flush write buffers, if applicable.\n\n"
                + "This is not implemented for read-only and non-blocking streams.\n"
                )]
            public override void flush(CodeContext/*!*/ context) {
                _checkClosed();

                _streams.Flush();
            }


            [Documentation("isatty() -> bool\n\nTrue if the file is connected to a tty device.")]
            public override bool isatty(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.IsConsoleStream();
            }


            [Documentation("String giving the file mode")]
            public string mode { get; }


            [Documentation("""
                read(size: int) -> bytes

                Read at most size bytes, returned as bytes.

                Only makes one system call, so less data may be returned than requested.
                In non-blocking mode, returns None if no data is available.
                On end-of-file, returns b''.
                """)]
            public override object read(CodeContext/*!*/ context, object? size = null) {
                int sizeInt = GetInt(size, -1);
                if (sizeInt < 0) {
                    return readall();
                }
                EnsureReadable();

                return Bytes.Make(_streams.Read(sizeInt));
            }


            [Documentation("readable() -> bool\n\nTrue if file was opened in a read mode.")]
            public override bool readable(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.ReadStream.CanRead;
            }


            [Documentation("""
                readall() -> bytes

                Read all data from the file, returned as bytes.

                In non-blocking mode, returns as much as is immediately available,
                or None if no data is available.  On end-of-file, returns b''.
                """)]
            public Bytes readall() {
                EnsureReadable();

                int bufSize = DEFAULT_BUF_SIZE;
                byte[] buffer = new byte[bufSize];
                int totalBytes = 0;

                for (var bytesRead = -1; bytesRead != 0;) {
                    bytesRead = _streams.ReadStream.Read(buffer, totalBytes, bufSize - totalBytes);
                    totalBytes += bytesRead;
                    if (totalBytes >= bufSize) {
                        bufSize = bufSize * 2;
                        Array.Resize(ref buffer, bufSize);
                    }
                }

                Array.Resize(ref buffer, totalBytes);
                return Bytes.Make(buffer);
            }


            [Documentation("readinto() -> Same as RawIOBase.readinto().")]
            public BigInteger readinto([NotNone] IBufferProtocol buffer) {
                EnsureReadable();

                using var pythonBuffer = buffer.GetBufferNoThrow(BufferFlags.Writable)
                    ?? throw PythonOps.TypeError("readinto() argument must be read-write bytes-like object, not {0}", PythonOps.GetPythonTypeName(buffer));

                _checkClosed();

                return _streams.ReadInto(pythonBuffer);
            }


            public override BigInteger readinto(CodeContext/*!*/ context, [NotNone] object buf) {
                var bufferProtocol = Converter.Convert<IBufferProtocol>(buf);
                return readinto(bufferProtocol);
            }


            [Documentation("""
                seek(offset: int[, whence: int]) -> int.

                Change stream position.

                Argument offset is a byte count.  Optional argument whence defaults to
                0 or `os.SEEK_SET` (offset from start of file, offset should be >= 0);
                other values are 1 or `os.SEEK_CUR` (move relative to current position,
                positive or negative), and 2 or `os.SEEK_END` (move relative to end of
                file, usually negative, although many platforms allow seeking beyond
                the end of a file, by adding zeros to enlarge the file).

                Return the new absolute position.

                Note that not all file objects are seekable.
                """)]
            public override BigInteger seek(CodeContext/*!*/ context, BigInteger offset, [Optional, NotNone] object whence) {
                _checkClosed();

                var origin = (SeekOrigin)GetInt(whence);
                if (origin < SeekOrigin.Begin || origin > SeekOrigin.End)
                    throw PythonOps.OSError(PythonFileManager.EINVAL, "Invalid argument");

                long ofs = checked((long)offset);

                if (ofs < 0 && ClrModule.IsMono && origin == SeekOrigin.Current) {
                    // Mono does not support negative offsets with SeekOrigin.Current
                    // so we need to calculate the absolute offset
                    ofs += _streams.ReadStream.Position;
                    origin = SeekOrigin.Begin;
                }

                return _streams.ReadStream.Seek(ofs, origin);
            }


            [Documentation("seekable() -> bool\n\nTrue if file supports random-access.")]
            public override bool seekable(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.ReadStream.CanSeek;
            }


            [Documentation("tell() -> int\n\nCurrent file position.")]
            public override BigInteger tell(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.ReadStream.Position;
            }


            public BigInteger truncate(BigInteger size) {
                EnsureWritable();

                return _streams.Truncate((long)size);
            }


            [Documentation("""
                truncate([size: int]) -> int

                Truncate the file to at most size bytes.

                Size defaults to the current file position, as returned by tell().
                The current file position is changed to the value of size.
                """)]
            public override BigInteger truncate(CodeContext/*!*/ context, object? pos = null) {
                if (pos == null) {
                    return truncate(tell(context));
                }

                if (TryGetBigInt(pos, out BigInteger bi)) {
                    return truncate(bi);
                }

                EnsureWritable();
                throw PythonOps.TypeError("an integer is required");
            }


            [Documentation("writable() -> bool\n\nTrue if file was opened in a write mode.")]
            public override bool writable(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.WriteStream.CanWrite;
            }


            [Documentation("""
                write(buf: bytes) -> int

                Write buffer buf to file, return number written.

                Return the number of bytes witten, which is always
                the length of b in bytes.
                """)]
            public override BigInteger write(CodeContext/*!*/ context, [NotNone] object buf) {
                var bufferProtocol = Converter.Convert<IBufferProtocol>(buf);
                using var buffer = bufferProtocol.GetBuffer();

                EnsureWritable();
                return _streams.Write(buffer);
            }

            #endregion

            #region ICodeFormattable Members

            public string __repr__(CodeContext/*!*/ context) {
                return string.Format("<_io.FileIO name={0} mode='{1}'>", PythonOps.Repr(context, name), mode);
            }

            #endregion

            #region IDisposable methods

            void IDisposable.Dispose() { }

            #endregion

            #region IWeakReferenceable Members

            WeakRefTracker? IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _tracker = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                ((IWeakReferenceable)this).SetWeakRef(value);
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<FileIO>(parameter, this);
            }

            #endregion

            #region Private implementation details

            private static void AddFilename(CodeContext context, string name, Exception ioe) {
                var pyExcep = PythonExceptions.ToPython(ioe);
                PythonOps.SetAttr(context, pyExcep, "filename", name);
            }


            private static Stream OpenFile(CodeContext/*!*/ context, PlatformAdaptationLayer pal, string name, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) {
                if (string.IsNullOrWhiteSpace(name)) throw PythonOps.OSError(PythonFileManager.ENOENT, "No such file or directory", filename: name);
                try {
                    return pal.OpenFileStream(name, fileMode, fileAccess, fileShare, 1); // Use a 1 byte buffer size to disable buffering (if the FileStream implementation supports it).
                } catch (UnauthorizedAccessException) {
                    throw PythonOps.OSError(PythonFileManager.EACCES, "Permission denied", name);
                } catch (FileNotFoundException) {
                    throw PythonOps.OSError(PythonFileManager.ENOENT, "No such file or directory", name);
                } catch (IOException e) {
                    AddFilename(context, name, e);
                    throw;
                }
            }

            private void EnsureReadable() {
                _checkClosed();
                _checkReadable("File not open for reading");
            }


            private void EnsureWritable() {
                _checkClosed();
                _checkWritable("File not open for writing");
            }

            #endregion
        }

#if !NETCOREAPP
        private static bool Contains(this string str, char value)
            => str.IndexOf(value) != -1;
#endif
    }
}
