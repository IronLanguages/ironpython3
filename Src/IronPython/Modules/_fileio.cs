// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
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
using Microsoft.Scripting.Utils;

// TODO: Documentation copied from CPython is inadequate in some places and wrong in others.

namespace IronPython.Modules {
    public static partial class PythonIOModule {
        [Documentation("file(name: str[, mode: str]) -> file IO object\n\n"
            + "Open a file.  The mode can be 'r', 'w' or 'a' for reading (default),\n"
            + "writing or appending.   The file will be created if it doesn't exist\n"
            + "when opened for writing or appending; it will be truncated when\n"
            + "opened for writing.  Add a '+' to the mode to allow simultaneous\n"
            + "reading and writing."
            )]
        [PythonType, DontMapIDisposableToContextManager]
        public class FileIO : _RawIOBase, IDisposable, IWeakReferenceable, ICodeFormattable, IDynamicMetaObjectProvider {
            #region Fields and constructors

            private static readonly int DEFAULT_BUF_SIZE = 32;

            private StreamBox _streams;
            private bool _closed, _closefd;
            private WeakRefTracker _tracker;
            private PythonContext _context;

            public object name;

            internal FileIO(CodeContext/*!*/ context, Stream stream)
                : this(context, new StreamBox(stream)) {
            }

            internal FileIO(CodeContext/*!*/ context, StreamBox streams)
                : base(context) {
                _context = context.LanguageContext;

                Stream stream = streams.ReadStream;
                string mode;
                if (stream.CanRead && stream.CanWrite) mode = "w+";
                else if (stream.CanWrite) mode = "w";
                else mode = "r";
                this.mode = mode;

                _streams = streams;
                _closefd = !streams.IsConsoleStream();
            }

            public FileIO(CodeContext/*!*/ context, int fd, string mode = "r", bool closefd = true, object opener = null)
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

            public FileIO(CodeContext/*!*/ context, string name, string mode = "r", bool closefd = true, object opener = null)
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
                            _streams.ReadStream.Seek(0L, SeekOrigin.End);
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
                }
                else {
                    object fdobj = PythonOps.CallWithContext(context, opener, name, flags);
                    if (fdobj is int fd) {
                        if (fd < 0) {
                            throw PythonOps.ValueError("opener returned {0}", fd);
                        }

                        if (!_context.FileManager.TryGetStreams(fd, out _streams)) {
                            throw PythonOps.OSError(9, "Bad file descriptor");
                        }
                    } else {
                        throw PythonOps.TypeError("expected integer from opener");
                    }
                }

                _closefd = true;
            }

            private static string NormalizeMode(string mode, out int flags) {
                switch (StandardizeMode(mode)) {
                    case "r":
                        flags = O_RDONLY;
                        return "rb";
                    case "w":
                        flags = O_CREAT | O_TRUNC | O_WRONLY;
                        return "wb";
                    case "a":
                        flags = O_APPEND | O_CREAT;
                        return "ab";
                    case "x":
                        flags = O_CREAT | O_EXCL;
                        return "xb";
                    case "r+":
                    case "+r":
                        flags = O_RDWR;
                        return "rb+";
                    case "w+":
                    case "+w":
                        flags = O_CREAT | O_TRUNC | O_RDWR;
                        return "wb+";
                    case "a+":
                    case "+a":
                        flags = O_APPEND | O_CREAT | O_RDWR;
                        return "ab+";
                    case "x+":
                    case "+x":
                        flags = O_CREAT | O_RDWR | O_EXCL;
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
                                    return PythonOps.ValueError("Must have exactly one of create/read/write/append mode and at most one plus");
                                } else {
                                    foundMode = true;
                                    continue;
                                }
                            case '+':
                                if (foundPlus) {
                                    return PythonOps.ValueError("Must have exactly one of create/read/write/append mode and at most one plus");
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

                    return PythonOps.ValueError("Must have exactly one of create/read/write/append mode and at most one plus");
                }
            }

            #endregion

            #region Public API

            [Documentation("close() -> None.  Close the file.\n\n"
                + "A closed file cannot be used for further I/O operations.  close() may be"
                + "called more than once without error.  Changes the fileno to -1."
                )]
            public override void close(CodeContext/*!*/ context) {
                if (_closed) {
                    return;
                }

                try {
                    flush(context);
                } catch (IOException) {
                    // flushing can fail, esp. if the other half of a pipe is closed
                    // ignore it because we're closing anyway
                }
                _closed = true;

                if (_closefd) {
                    _streams.CloseStreams(_context.RawFileManager);
                }
            }

            [Documentation("True if the file is closed")]
            public override bool closed {
                get {
                    return _closed;
                }
            }

            public bool closefd => _closefd;

            [Documentation("fileno() -> int. \"file descriptor\".\n\n"
                + "This is needed for lower-level file interfaces, such as the fcntl module."
                )]
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

            [Documentation("isatty() -> bool.  True if the file is connected to a tty device.")]
            public override bool isatty(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.IsConsoleStream();
            }

            [Documentation("String giving the file mode")]
            public string mode { get; }

            [Documentation("read(size: int) -> bytes.  read at most size bytes, returned as bytes.\n\n"
                + "Only makes one system call, so less data may be returned than requested\n"
                + "In non-blocking mode, returns None if no data is available.\n"
                + "On end-of-file, returns ''."
                )]
            public override object read(CodeContext/*!*/ context, object size = null) {
                int sizeInt = GetInt(size, -1);
                if (sizeInt < 0) {
                    return readall();
                }
                EnsureReadable();

                return Bytes.Make(_streams.Read(sizeInt));
            }

            [Documentation("readable() -> bool.  True if file was opened in a read mode.")]
            public override bool readable(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.ReadStream.CanRead;
            }

            [Documentation("readall() -> bytes.  read all data from the file, returned as bytes.\n\n"
                + "In non-blocking mode, returns as much as is immediately available,\n"
                + "or None if no data is available.  On end-of-file, returns ''."
                )]
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

                var span = pythonBuffer.AsSpan();
                for (int i = 0; i < span.Length; i++) {
                    int b = _streams.ReadStream.ReadByte();
                    if (b == -1) return i;
                    span[i] = (byte)b;
                }

                return span.Length;
            }

            public override BigInteger readinto(CodeContext/*!*/ context, object buf) {
                var bufferProtocol = Converter.Convert<IBufferProtocol>(buf);
                return readinto(bufferProtocol);
            }

            [Documentation("seek(offset: int[, whence: int]) -> None.  Move to new file position.\n\n"
                + "Argument offset is a byte count.  Optional argument whence defaults to\n"
                + "0 (offset from start of file, offset should be >= 0); other values are 1\n"
                + "(move relative to current position, positive or negative), and 2 (move\n"
                + "relative to end of file, usually negative, although many platforms allow\n"
                + "seeking beyond the end of a file).\n"
                + "Note that not all file objects are seekable."
                )]
            public override BigInteger seek(CodeContext/*!*/ context, BigInteger offset, [Optional] object whence) {
                _checkClosed();

                return _streams.ReadStream.Seek((long)offset, (SeekOrigin)GetInt(whence));
            }

            public BigInteger seek(double offset, [Optional] object whence) {
                _checkClosed();

                throw PythonOps.TypeError("an integer is required");
            }

            [Documentation("seekable() -> bool.  True if file supports random-access.")]
            public override bool seekable(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.ReadStream.CanSeek;
            }

            [Documentation("tell() -> int.  Current file position")]
            public override BigInteger tell(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.ReadStream.Position;
            }

            public BigInteger truncate(BigInteger size) {
                EnsureWritable();

                return _streams.Truncate((long)size);
            }

            public BigInteger truncate(double size) {
                EnsureWritable();

                throw PythonOps.TypeError("an integer is required");
            }

            [Documentation("truncate([size: int]) -> None.  Truncate the file to at most size bytes.\n\n"
                + "Size defaults to the current file position, as returned by tell()."
                + "The current file position is changed to the value of size."
                )]
            public override BigInteger truncate(CodeContext/*!*/ context, object pos = null) {
                if (pos == null) {
                    return truncate(tell(context));
                }

                BigInteger bi;
                if (TryGetBigInt(pos, out bi)) {
                    return truncate(bi);
                }

                EnsureWritable();
                throw PythonOps.TypeError("an integer is required");
            }

            [Documentation("writable() -> bool.  True if file was opened in a write mode.")]
            public override bool writable(CodeContext/*!*/ context) {
                _checkClosed();

                return _streams.WriteStream.CanWrite;
            }

            [Documentation("write(b: bytes) -> int.  Write bytes b to file, return number written.\n\n"
                + "Only makes one system call, so not all the data may be written.\n"
                + "The number of bytes actually written is returned."
                )]
            public override BigInteger write(CodeContext/*!*/ context, object b) {
                var bufferProtocol = Converter.Convert<IBufferProtocol>(b);
                using var buffer = bufferProtocol.GetBuffer();
                var bytes = buffer.AsReadOnlySpan();

                EnsureWritable();
                return _streams.Write(bytes);
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

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
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
                if (string.IsNullOrWhiteSpace(name)) throw PythonOps.OSError(2, "No such file or directory", filename: name);
                try {
                    return pal.OpenFileStream(name, fileMode, fileAccess, fileShare, 1); // Use a 1 byte buffer size to disable buffering (if the FileStream implementation supports it).
                } catch (UnauthorizedAccessException) {
                    throw PythonOps.OSError(13, "Permission denied", name);
                } catch (FileNotFoundException) {
                    throw PythonOps.OSError(2, "No such file or directory", name);
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
