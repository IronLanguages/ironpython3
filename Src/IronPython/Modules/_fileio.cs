// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

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

            internal Stream _readStream;
            private Stream _writeStream;
            private bool _closed, _closefd;
            private WeakRefTracker _tracker;
            private PythonContext _context;
            public object name;

            private bool _isConsole;
            private ConsoleStreamType _consoleStreamType;

            internal FileIO(CodeContext/*!*/ context, Stream stream)
                : base(context) {
                _context = context.LanguageContext;
                string mode;
                if (stream.CanRead && stream.CanWrite) mode = "w+";
                else if (stream.CanWrite) mode = "w";
                else mode = "r";
                this.mode = mode;
                _writeStream = stream;
                _readStream = stream;
                _closefd = true;
            }

            internal FileIO(CodeContext/*!*/ context, Stream stream, ConsoleStreamType consoleStreamType)
                : this(context, stream) {
                _isConsole = true;
                _consoleStreamType = consoleStreamType;
            }

            public FileIO(CodeContext/*!*/ context, int fd, string mode="r", bool closefd=true)
                : base(context) {
                if (fd < 0) {
                    throw PythonOps.ValueError("fd must be >= 0");
                }

                PythonContext pc = context.LanguageContext;

                _context = pc;
                switch (StandardizeMode(mode)) {
                    case "r": this.mode = "rb"; break;
                    case "w": this.mode = "wb"; break;
                    case "a": this.mode = "w"; break;
                    case "r+":
                    case "+r": this.mode = "rb+"; break;
                    case "w+":
                    case "+w": this.mode = "rb+"; break;
                    case "a+":
                    case "+a": this.mode = "r+"; break;
                    default:
                        BadMode(mode);
                        break;
                }

                if (pc.FileManager.TryGetFileFromId(pc, fd, out PythonFile pf)) {
                    name = (object)pf.name ?? fd;
                    _readStream = pf._stream;
                    _writeStream = pf._stream;
                } else if (pc.FileManager.TryGetObjectFromId(pc, fd, out object fileObj)) {
                    if (fileObj is FileIO file) {
                        name = file.name ?? fd;
                        _readStream = file._readStream;
                        _writeStream = file._writeStream;
                    } else if (fileObj is Stream stream) {
                        name = fd;
                        _readStream = stream;
                        _writeStream = stream;
                    }
                }

                _closefd = closefd;
            }
            
            public FileIO(CodeContext/*!*/ context, string name, string mode="r", bool closefd=true)
                : base(context) {
                if (!closefd) {
                    throw PythonOps.ValueError("Cannot use closefd=False with file name");
                }
                _closefd = true;
                this.name = name;
                PlatformAdaptationLayer pal = context.LanguageContext.DomainManager.Platform;

                switch (StandardizeMode(mode)) {
                    case "r":
                        _readStream = _writeStream = OpenFile(context, pal, name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        this.mode = "rb";
                        break;
                    case "w":
                        _readStream = _writeStream = OpenFile(context, pal, name, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        this.mode = "wb";
                        break;
                    case "a":
                        _readStream = _writeStream = OpenFile(context, pal, name, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        _readStream.Seek(0L, SeekOrigin.End);
                        this.mode = "ab";
                        break;
                    case "r+":
                    case "+r":
                        _readStream = _writeStream = OpenFile(context, pal, name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        this.mode = "rb+";
                        break;
                    case "w+":
                    case "+w":
                        _readStream = _writeStream = OpenFile(context, pal, name, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                        this.mode = "rb+";
                        break;
                    case "a+":
                    case "+a":
                        _readStream = OpenFile(context, pal, name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        _writeStream = OpenFile(context, pal, name, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        _readStream.Seek(0L, SeekOrigin.End);
                        _writeStream.Seek(0L, SeekOrigin.End);
                        this.mode = "ab+";
                        break;
                    default:
                        BadMode(mode);
                        break;
                }

                _context = context.LanguageContext;
            }

            /// <summary>
            /// Remove all 'b's from mode string to simplify parsing
            /// </summary>
            private static string StandardizeMode(string mode) {
                int index = mode.IndexOf('b');

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

            private static void BadMode(string mode) {
                bool foundMode = false, foundPlus = false;
                foreach (char c in mode) {
                    switch (c) {
                        case 'r':
                        case 'w':
                        case 'a':
                            if (foundMode) {
                                throw PythonOps.ValueError("Must have exactly one of read/write/append mode");
                            } else {
                                foundMode = true;
                                continue;
                            }
                        case '+':
                            if (foundPlus) {
                                throw PythonOps.ValueError("Must have exactly one of read/write/append mode");
                            } else {
                                foundPlus = true;
                                continue;
                            }
                        case 'b':
                            // any number of 'b's is acceptable
                            continue;
                        default:
                            throw PythonOps.ValueError("invalid mode: {0}", mode);
                    }
                }

                throw PythonOps.ValueError("Must have exactly one of read/write/append mode");
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

                flush(context);
                _closed = true;

                if (_closefd) {
                    _readStream.Close();
                    _readStream.Dispose();
                    if (!object.ReferenceEquals(_readStream, _writeStream)) {
                        _writeStream.Close();
                        _writeStream.Dispose();
                    }

                    PythonFileManager myManager = _context.RawFileManager;
                    if (myManager != null) {
                        myManager.Remove(this);
                    }
                }                
            }

            [Documentation("True if the file is closed")]
            public override bool closed {
                get {
                    return _closed;
                }
            }

            public bool closefd {
                get {
                    return _closefd;
                }
            }

            [Documentation("fileno() -> int. \"file descriptor\".\n\n"
                + "This is needed for lower-level file interfaces, such as the fcntl module."
                )]
            public override int fileno(CodeContext/*!*/ context) {
                _checkClosed();

                return _context.FileManager.GetOrAssignIdForObject(this);
            }

            [Documentation("Flush write buffers, if applicable.\n\n"
                + "This is not implemented for read-only and non-blocking streams.\n"
                )]
            public override void flush(CodeContext/*!*/ context) {
                _checkClosed();

                if (_writeStream != null) {
                    _writeStream.Flush();
                }
            }

            [Documentation("isatty() -> bool.  True if the file is connected to a tty device.")]
            public override bool isatty(CodeContext/*!*/ context) {
                _checkClosed();

                if (Environment.OSVersion.Platform == PlatformID.Unix) {
                    return isattyUnix();
                }

                return _isConsole && !isRedirected();

                // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Posix assembly on Windows.
                bool isattyUnix() {
                    if (_isConsole) {
                        if (_consoleStreamType == ConsoleStreamType.Input) {
                            return Mono.Unix.Native.Syscall.isatty(0);
                        }
                        if (_consoleStreamType == ConsoleStreamType.Output) {
                            return Mono.Unix.Native.Syscall.isatty(1);
                        }
                        Debug.Assert(_consoleStreamType == ConsoleStreamType.ErrorOutput);
                        return Mono.Unix.Native.Syscall.isatty(2);
                    }
                    return false;
                }
            }

            private bool isRedirected() {
                if (_consoleStreamType == ConsoleStreamType.Output) {
                    return Console.IsOutputRedirected;
                }
                if (_consoleStreamType == ConsoleStreamType.Input) {
                    return Console.IsInputRedirected;
                }
                Debug.Assert(_consoleStreamType == ConsoleStreamType.ErrorOutput);
                return Console.IsErrorRedirected;
            }

            [Documentation("String giving the file mode")]
            public string mode { get; }

            [Documentation("read(size: int) -> bytes.  read at most size bytes, returned as bytes.\n\n"
                + "Only makes one system call, so less data may be returned than requested\n"
                + "In non-blocking mode, returns None if no data is available.\n"
                + "On end-of-file, returns ''."
                )]
            public override object read(CodeContext/*!*/ context, object size=null) {
                int sizeInt = GetInt(size, -1);
                if (sizeInt < 0) {
                    return readall();
                }
                EnsureReadable();

                byte[] buffer = new byte[sizeInt];
                int bytesRead = _readStream.Read(buffer, 0, sizeInt);
                
                Array.Resize(ref buffer, bytesRead);
                return Bytes.Make(buffer);
            }

            [Documentation("readable() -> bool.  True if file was opened in a read mode.")]
            public override bool readable(CodeContext/*!*/ context) {
                _checkClosed();

                return _readStream.CanRead;
            }

            [Documentation("readall() -> bytes.  read all data from the file, returned as bytes.\n\n"
                + "In non-blocking mode, returns as much as is immediately available,\n"
                + "or None if no data is available.  On end-of-file, returns ''."
                )]
            public Bytes readall() {
                EnsureReadable();

                int bufSize = DEFAULT_BUF_SIZE;
                byte[] buffer = new byte[bufSize];
                int bytesRead = _readStream.Read(buffer, 0, bufSize);

                for (; bytesRead == bufSize; bufSize *= 2) {
                    Array.Resize(ref buffer, bufSize * 2);
                    bytesRead += _readStream.Read(buffer, bufSize, bufSize);
                }

                Array.Resize(ref buffer, bytesRead);
                return Bytes.Make(buffer);
            }

            [Documentation("readinto() -> Same as RawIOBase.readinto().")]
            public BigInteger readinto([NotNull]ArrayModule.array buffer) {
                EnsureReadable();

                return (int)buffer.FromStream(_readStream, 0, buffer.__len__() * buffer.itemsize);
            }

            public BigInteger readinto([NotNull]ByteArray buffer) {
                EnsureReadable();

                for (int i = 0; i < buffer.Count; i++) {
                    int b = _readStream.ReadByte();
                    if (b == -1) return i - 1;
                    buffer[i] = (byte)b;
                }
                return buffer.Count;
            }

            public override BigInteger readinto(CodeContext/*!*/ context, object buf) {
                ByteArray bytes = buf as ByteArray;
                if (bytes != null) {
                    return readinto(bytes);
                }

                ArrayModule.array arr = buf as ArrayModule.array;
                if (arr != null) {
                    return readinto(bytes);
                };

                EnsureReadable();
                throw PythonOps.TypeError(
                    "argument 1 must be read/write buffer, not {0}",
                    DynamicHelpers.GetPythonType(buf).Name
                );
            }

            [Documentation("seek(offset: int[, whence: int]) -> None.  Move to new file position.\n\n"
                + "Argument offset is a byte count.  Optional argument whence defaults to\n"
                + "0 (offset from start of file, offset should be >= 0); other values are 1\n"
                + "(move relative to current position, positive or negative), and 2 (move\n"
                + "relative to end of file, usually negative, although many platforms allow\n"
                + "seeking beyond the end of a file).\n"
                + "Note that not all file objects are seekable."
                )]
            public override BigInteger seek(CodeContext/*!*/ context, BigInteger offset, [DefaultParameterValue(0)]object whence) {
                _checkClosed();

                return _readStream.Seek((long)offset, (SeekOrigin)GetInt(whence));
            }

            public BigInteger seek(double offset, [DefaultParameterValue(0)]object whence) {
                _checkClosed();

                throw PythonOps.TypeError("an integer is required");
            }

            [Documentation("seekable() -> bool.  True if file supports random-access.")]
            public override bool seekable(CodeContext/*!*/ context) {
                _checkClosed();

                return _readStream.CanSeek;
            }

            [Documentation("tell() -> int.  Current file position")]
            public override BigInteger tell(CodeContext/*!*/ context) {
                _checkClosed();

                return _readStream.Position;
            }

            public BigInteger truncate(BigInteger size) {
                EnsureWritable();

                long pos = _readStream.Position;
                _writeStream.SetLength((long)size);
                _readStream.Seek(pos, SeekOrigin.Begin);

                return size;
            }

            public BigInteger truncate(double size) {
                EnsureWritable();

                throw PythonOps.TypeError("an integer is required");
            }

            [Documentation("truncate([size: int]) -> None.  Truncate the file to at most size bytes.\n\n"
                + "Size defaults to the current file position, as returned by tell()."
                + "The current file position is changed to the value of size."
                )]
            public override BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
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

                return _writeStream.CanWrite;
            }

            private BigInteger write([NotNull]byte[] b) {
                EnsureWritable();

                _writeStream.Write(b, 0, b.Length);
                SeekToEnd();

                return b.Length;
            }

            private BigInteger write([NotNull]Bytes b) {
                return write(b._bytes);
            }

            private BigInteger write([NotNull]ICollection<byte> b) {
                EnsureWritable();

                int len = b.Count;
                byte[] bytes = new byte[len];
                b.CopyTo(bytes, 0);
                _writeStream.Write(bytes, 0, len);
                SeekToEnd();

                return len;
            }

            private BigInteger write([NotNull]string s) {
                return write(s.MakeByteArray());
            }

            [Documentation("write(b: bytes) -> int.  Write bytes b to file, return number written.\n\n"
                + "Only makes one system call, so not all the data may be written.\n"
                + "The number of bytes actually written is returned."
                )]
            public override BigInteger write(CodeContext/*!*/ context, object b) {
                byte[] bArray = b as byte[];
                if (bArray != null) {
                    return write(bArray);
                }

                Bytes bBytes = b as Bytes;
                if (bBytes != null) {
                    return write(bBytes);
                }

                ArrayModule.array bPythonArray = b as ArrayModule.array;
                if (bPythonArray != null) {
                    return write(bPythonArray.ToByteArray());
                }

                ICollection<byte> bCollection = b as ICollection<byte>;
                if (bCollection != null) {
                    return write(bCollection);
                }

                EnsureWritable();

                throw PythonOps.TypeError("expected a readable buffer object");
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

            private static Stream OpenFile(CodeContext/*!*/ context, PlatformAdaptationLayer pal, string name, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) {
                try {
                    return pal.OpenInputFileStream(name, fileMode, fileAccess, fileShare);
                } catch (UnauthorizedAccessException e) {
                    throw PythonFile.ToIoException(context, name, e);
                } catch (IOException e) {
                    PythonFile.AddFilename(context, name, e);
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

            private void SeekToEnd() {
                if (!object.ReferenceEquals(_readStream, _writeStream)) {
                    _readStream.Seek(_writeStream.Position, SeekOrigin.Begin);
                }
            }

            #endregion
        }
    }
}
