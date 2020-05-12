// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

namespace IronPython.Modules {
    public static partial class PythonIOModule {
        /// <summary>
        /// BytesIO([initializer]) -> object
        /// 
        /// Create a buffered I/O implementation using an in-memory bytes
        /// buffer, ready for reading and writing.
        /// </summary>
        [PythonType, DontMapIDisposableToContextManager]
        public class BytesIO : _BufferedIOBase, IEnumerator, IDisposable, IDynamicMetaObjectProvider {
            #region Fields and constructors

            private static readonly int DEFAULT_BUF_SIZE = 20;

            private byte[] _data;
            private int _pos, _length;

            internal BytesIO(CodeContext/*!*/ context)
                : base(context) {
            }

            public BytesIO(CodeContext/*!*/ context, object initial_bytes=null)
                : base(context) {
            }

            public void __init__(IBufferProtocol initial_bytes=null) {
                if (Object.ReferenceEquals(_data, null)) {
                    _data = new byte[DEFAULT_BUF_SIZE];
                }

                _pos = _length = 0;
                if (initial_bytes != null) {
                    DoWrite(initial_bytes);
                    _pos = 0;
                }
            }

            #endregion

            #region Public API

            /// <summary>
            /// close() -> None.  Disable all I/O operations.
            /// </summary>
            public override void close(CodeContext/*!*/ context) {
                _data = null;
            }

            /// <summary>
            /// True if the file is closed.
            /// </summary>
            public override bool closed {
                get {
                    return _data == null;
                }
            }

            /// <summary>
            /// getvalue() -> bytes.
            /// 
            /// Retrieve the entire contents of the BytesIO object.
            /// </summary>
            public Bytes getvalue() {
                _checkClosed();

                if (_length == 0) {
                    return Bytes.Empty;
                }

                byte[] arr = new byte[_length];
                Array.Copy(_data, arr, _length);
                return Bytes.Make(arr);
            }

            public MemoryView getbuffer() {
                _checkClosed();
                // TODO: MemoryView constructor should accept Memory/ReadOnlyMemory
                return (MemoryView)new MemoryView(new Bytes(_data))[new Slice(0, _length, 1)];
            }

            [Documentation("isatty() -> False\n\n"
                + "Always returns False since BytesIO objects are not connected\n"
                + "to a TTY-like device."
                )]
            public override bool isatty(CodeContext/*!*/ context) {
                _checkClosed();

                return false;
            }

            [Documentation("read([size]) -> read at most size bytes, returned as a bytes object.\n\n"
                + "If the size argument is negative, read until EOF is reached.\n"
                + "Return an empty string at EOF."
                )]
            public override object read(CodeContext/*!*/ context, object size=null) {
                _checkClosed();
                int sz = GetInt(size, -1);

                int len = Math.Max(0, _length - _pos);
                if (sz >= 0) {
                    len = Math.Min(len, sz);
                }
                if (len == 0) {
                    return Bytes.Empty;
                }

                byte[] arr = new byte[len];
                Array.Copy(_data, _pos, arr, 0, len);
                _pos += len;

                return Bytes.Make(arr);
            }

            [Documentation("read1(size) -> read at most size bytes, returned as a bytes object.\n\n"
                + "If the size argument is negative or omitted, read until EOF is reached.\n"
                + "Return an empty string at EOF."
                )]
            public override Bytes read1(CodeContext/*!*/ context, int size) {
                return (Bytes)read(context, size);
            }

            public override bool readable(CodeContext/*!*/ context) {
                return true;
            }

            [Documentation("readinto(array_or_bytearray) -> int.  Read up to len(b) bytes into b.\n\n"
                + "Returns number of bytes read (0 for EOF)."
                )]
            public BigInteger readinto([NotNull]ByteArray buffer) {
                _checkClosed();

                int len = Math.Min(_length - _pos, buffer.Count);
                for (int i = 0; i < len; i++) {
                    buffer[i] = _data[_pos++];
                }

                return len;
            }

            public BigInteger readinto([NotNull]ArrayModule.array buffer) {
                _checkClosed();

                int len = Math.Min(_length - _pos, buffer.__len__() * buffer.itemsize);
                int tailLen = len % buffer.itemsize;
                buffer.FromStream(new MemoryStream(_data, _pos, len - tailLen, false, false), 0);
                _pos += len - tailLen;

                if (tailLen != 0) {
                    byte[] tail = buffer.RawGetItem(len / buffer.itemsize);
                    for (int i = 0; i < tailLen; i++) {
                        tail[i] = _data[_pos++];
                    }
                    buffer.FromStream(new MemoryStream(tail), len / buffer.itemsize);
                }

                return len;
            }

            public override BigInteger readinto(CodeContext/*!*/ context, object buf) {
                if (buf is ByteArray bytes) {
                    return readinto(bytes);
                }

                if (buf is ArrayModule.array array) {
                    return readinto(array);
                }

                _checkClosed();
                throw PythonOps.TypeError("must be read-write buffer, not {0}", PythonTypeOps.GetName(buf));
            }

            [Documentation("readline([size]) -> next line from the file, as bytes.\n\n"
                + "Retain newline.  A non-negative size argument limits the maximum\n"
                + "number of bytes to return (an incomplete line may be returned then).\n"
                + "Return an empty string at EOF."
                )]
            public override object readline(CodeContext/*!*/ context, int limit=-1) {
                return readline(limit);
            }

            private Bytes readline(int size=-1) {
                _checkClosed();
                if (_pos >= _length || size == 0) {
                    return Bytes.Empty;
                }

                int origPos = _pos;
                while ((size < 0 || _pos - origPos < size) && _pos < _length) {
                    if (_data[_pos] == '\n') {
                        _pos++;
                        break;
                    }
                    _pos++;
                }

                byte[] arr = new byte[_pos - origPos];
                Array.Copy(_data, origPos, arr, 0, _pos - origPos);
                return Bytes.Make(arr);
            }

            public Bytes readline(object size) {
                if (size == null) {
                    return readline(-1);
                }

                _checkClosed();

                throw PythonOps.TypeError("integer argument expected, got '{0}'", PythonTypeOps.GetName(size));
            }

            [Documentation("readlines([size]) -> list of bytes objects, each a line from the file.\n\n"
                + "Call readline() repeatedly and return a list of the lines so read.\n"
                + "The optional size argument, if given, is an approximate bound on the\n"
                + "total number of bytes in the lines returned."
                )]
            public override PythonList readlines(object hint=null) {
                _checkClosed();
                int size = GetInt(hint, -1);

                PythonList lines = new PythonList();
                for (Bytes line = readline(-1); line.Count > 0; line = readline(-1)) {
                    lines.append(line); 
                    if (size > 0) {
                        size -= line.Count;
                        if (size <= 0) {
                            break;
                        }
                    }
                }

                return lines;
            }

            private BigInteger seek(int pos, int whence) {
                _checkClosed();

                switch (whence) {
                    case 0:
                        if (pos < 0) {
                            throw PythonOps.ValueError("negative seek value {0}", pos);
                        }
                        _pos = pos;
                        return _pos;
                    case 1:
                        _pos = Math.Max(0, _pos + pos);
                        return _pos;
                    case 2:
                        _pos = Math.Max(0, _length + pos);
                        return _pos;
                    default:
                        throw PythonOps.ValueError("invalid whence ({0}, should be 0, 1 or 2)", whence);
                }
            }

            public BigInteger seek(double pos, [Optional]object whence) => throw PythonOps.TypeError("integer argument expected, got float");

            [Documentation("seek(pos, whence=0) -> int.  Change stream position.\n\n"
                + "Seek to byte offset pos relative to position indicated by whence:\n"
                + "     0  Start of stream (the default).  pos should be >= 0;\n"
                + "     1  Current position - pos may be negative;\n"
                + "     2  End of stream - pos usually negative.\n"
                + "Returns the new absolute position."
                )]
            public override BigInteger seek(CodeContext/*!*/ context, BigInteger pos, [Optional]object whence) {
                _checkClosed();

                int posInt = (int)pos;
                switch (whence) {
                    case int v:
                        return seek(posInt, v);
                    case Extensible<int> v:
                        return seek(posInt, v);
                    case BigInteger v:
                        return seek(posInt, (int)v);
                    case Extensible<BigInteger> v:
                        return seek(posInt, (int)v.Value);
                    case double _:
                    case Extensible<double> _:
                        throw PythonOps.TypeError("integer argument expected, got float");
                    default:
                        return seek(posInt, GetInt(whence));
                }
            }

            public override bool seekable(CodeContext/*!*/ context) {
                return true;
            }

            [Documentation("tell() -> current file position, an integer")]
            public override BigInteger tell(CodeContext/*!*/ context) {
                _checkClosed();

                return _pos;
            }

            [Documentation("truncate([size]) -> int.  Truncate the file to at most size bytes.\n\n"
                + "Size defaults to the current file position, as returned by tell().\n"
                + "Returns the new size.  Imply an absolute seek to the position size."
                )]
            public BigInteger truncate() {
                return truncate(_pos);
            }

            public BigInteger truncate(int size) {
                _checkClosed();
                if (size < 0) {
                    throw PythonOps.ValueError("negative size value {0}", size);
                }

                _length = Math.Min(_length, size);
                return (BigInteger)size;
            }

            public override BigInteger truncate(CodeContext/*!*/ context, object size=null) {
                if (size == null) {
                    return truncate();
                }

                int sizeInt;
                if (TryGetInt(size, out sizeInt)) {
                    return truncate(sizeInt);
                }

                _checkClosed();

                throw PythonOps.TypeError("integer argument expected, got '{0}'", PythonTypeOps.GetName(size));
            }

            public override bool writable(CodeContext/*!*/ context) {
                return true;
            }

            [Documentation("write(bytes) -> int.  Write bytes to file.\n\n"
                + "Return the number of bytes written."
                )]
            public override BigInteger write(CodeContext/*!*/ context, object bytes) {
                _checkClosed();
                if (bytes is IBufferProtocol bufferProtocol) return DoWrite(bufferProtocol);
                throw PythonOps.TypeError("a bytes-like object is required, not '{0}'", PythonTypeOps.GetName(bytes));
            }

            public BigInteger write(CodeContext/*!*/ context, IBufferProtocol bytes) {
                _checkClosed();
                return DoWrite(bytes);
            }

            [Documentation("writelines(sequence_of_strings) -> None.  Write strings to the file.\n\n"
                + "Note that newlines are not added.  The sequence can be any iterable\n"
                + "object producing strings. This is equivalent to calling write() for\n"
                + "each string."
                )]
            public void writelines([NotNull]IEnumerable lines) {
                _checkClosed();

                IEnumerator en = lines.GetEnumerator();
                while (en.MoveNext()) {
                    DoWrite(en.Current);
                }
            }

            #endregion

            #region IDisposable methods

            void IDisposable.Dispose() { }

            #endregion
            
            #region IEnumerator methods

            private object _current = null;

            object IEnumerator.Current {
                get {
                    _checkClosed();
                    return _current;
                }
            }

            bool IEnumerator.MoveNext() {
                Bytes line = readline(-1);
                if (line.Count == 0) {
                    return false;
                }
                _current = line;
                return true;
            }

            void IEnumerator.Reset() {
                seek(0, 0);
                _current = null;
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<BytesIO>(parameter, this);
            }

            #endregion

            #region Private implementation details

            private int DoWrite(IBufferProtocol bufferProtocol) {
                using var buffer = bufferProtocol.GetBuffer();
                var bytes = buffer.AsReadOnlySpan();

                if (bytes.Length == 0) {
                    return 0;
                }

                EnsureSizeSetLength(_pos + bytes.Length);
                bytes.CopyTo(_data.AsSpan(_pos, bytes.Length));

                _pos += bytes.Length;
                return bytes.Length;
            }

            private int DoWrite(object bytes) {
                if (bytes is IBufferProtocol bufferProtocol) return DoWrite(bufferProtocol);
                throw PythonOps.TypeError("a bytes-like object is required, not '{0}'", PythonTypeOps.GetName(bytes));
            }

            private void EnsureSize(int size) {
                Debug.Assert(size > 0);

                if (_data.Length < size) {
                    size = size <= DEFAULT_BUF_SIZE ? DEFAULT_BUF_SIZE : Math.Max(size, _data.Length * 2);

                    byte[] oldBuffer = _data;
                    _data = new byte[size];
                    Array.Copy(oldBuffer, _data, _length);
                }
            }

            private void EnsureSizeSetLength(int size) {
                Debug.Assert(size >= _pos);
                Debug.Assert(_length <= _data.Length);

                if (_data.Length < size) {
                    // EnsureSize is guaranteed to resize, so we need not write any zeros here.
                    EnsureSize(size);
                    _length = size;
                    return;
                }

                // _data[_pos:size] is about to be overwritten, so we only need to zero out _data[_length:_pos]
                while (_length < _pos) {
                    _data[_length++] = 0;
                }

                _length = Math.Max(_length, size);
            }

            #endregion
        }
    }
}
