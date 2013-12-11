/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using IronPython.Runtime.Exceptions;

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

            public BytesIO(CodeContext/*!*/ context, [DefaultParameterValue(null)]object initial_bytes)
                : base(context) {
            }

            public void __init__([DefaultParameterValue(null)]object initial_bytes) {
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
            public override object read(CodeContext/*!*/ context, [DefaultParameterValue(null)]object size) {
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
                ByteArray bytes = buf as ByteArray;
                if (bytes != null) {
                    return readinto(bytes);
                }

                ArrayModule.array array = buf as ArrayModule.array;
                if (array != null) {
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
            public override object readline(CodeContext/*!*/ context, [DefaultParameterValue(-1)]int limit) {
                return readline(limit);
            }

            private Bytes readline([DefaultParameterValue(-1)]int size) {
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
            public override List readlines([DefaultParameterValue(null)]object hint) {
                _checkClosed();
                int size = GetInt(hint, -1);

                List lines = new List();
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

            [Documentation("seek(pos, whence=0) -> int.  Change stream position.\n\n"
                + "Seek to byte offset pos relative to position indicated by whence:\n"
                + "     0  Start of stream (the default).  pos should be >= 0;\n"
                + "     1  Current position - pos may be negative;\n"
                + "     2  End of stream - pos usually negative.\n"
                + "Returns the new absolute position."
                )]
            public BigInteger seek(int pos, [DefaultParameterValue(0)]int whence) {
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

            public BigInteger seek(double pos, [DefaultParameterValue(0)]int whence) {
                throw PythonOps.TypeError("'float' object cannot be interpreted as an index");
            }

            public override BigInteger seek(CodeContext/*!*/ context, BigInteger pos, [DefaultParameterValue(0)]object whence) {
                _checkClosed();

                int posInt = (int)pos;
                if (whence is double || whence is Extensible<double>) {
                    if (PythonContext.GetContext(context).PythonOptions.Python30) {
                        throw PythonOps.TypeError("integer argument expected, got float");
                    } else {
                        PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "integer argument expected, got float");
                        return seek(posInt, Converter.ConvertToInt32(whence));
                    }
                }
                
                return seek(posInt, GetInt(whence));
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

            public override BigInteger truncate(CodeContext/*!*/ context, [DefaultParameterValue(null)]object size) {
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

            private int DoWrite(byte[] bytes) {
                if (bytes.Length == 0) {
                    return 0;
                }

                EnsureSizeSetLength(_pos + bytes.Length);
                Array.Copy(bytes, 0, _data, _pos, bytes.Length);

                _pos += bytes.Length;
                return bytes.Length;
            }

            private int DoWrite(ICollection<byte> bytes) {
                int nbytes = bytes.Count;
                if (nbytes == 0) {
                    return 0;
                }

                EnsureSizeSetLength(_pos + nbytes);
                bytes.CopyTo(_data, _pos);

                _pos += nbytes;
                return nbytes;
            }

            private int DoWrite(string bytes) {
                // CLR strings are natively Unicode (UTF-16 LE, to be precise).
                // In 2.x, io.BytesIO.write() takes "bytes or bytearray" as a parameter.
                // On 2.x "bytes" is an alias for str, so str types are accepted by BytesIO.write().
                // When given a unicode object, 2.x BytesIO.write() complains:
                //   TypeError: 'unicode' does not have the buffer interface

                // We will accept CLR strings, but only if the data in it is in Latin 1 (iso-8859-1)
                // encoding (i.e. ord(c) for all c is within 0-255.

                // Alternatively, we could support strings containing any Unicode character by ignoring
                // any 0x00 bytes, but as CPython doesn't support that it is unlikely that we will need to.

                int nbytes = bytes.Length;
                if (nbytes == 0) {
                    return 0;
                }

                byte[] _raw_string = new byte[nbytes];
                for (int i = 0; i < nbytes; i++) {
                    int ord = (int)bytes[i];
                    if(ord < 256) {
                        _raw_string[i] = (byte)ord;
                    } else {
                        // A character outside the range 0x00-0xFF is present in the original string.
                        // Ejecting, emulating the cPython 2.x behavior when it enounters "unicode".
                        // This should keep the unittest gods at bay.
                        throw PythonOps.TypeError("'unicode' does not have the buffer interface");
                    }
                }

                return DoWrite(_raw_string);
            }

            private int DoWrite(object bytes) {
                if (bytes is byte[]) {
                    return DoWrite((byte[])bytes);
                } else if (bytes is Bytes) {
                    return DoWrite(((Bytes)bytes)._bytes); // as byte[]
                } else if (bytes is ArrayModule.array) {
                    return DoWrite(((ArrayModule.array)bytes).ToByteArray()); // as byte[]
                } else if (bytes is ICollection<byte>) {
                    return DoWrite((ICollection<byte>)bytes);
                } else if (bytes is string) {
                    // TODO Remove this when we move to 3.x
                    return DoWrite((string)bytes); // as string
                }

                throw PythonOps.TypeError("expected a readable buffer object");
            }

            private void EnsureSize(int size) {
                Debug.Assert(size > 0);

                if (_data.Length < size) {
                    if (size <= DEFAULT_BUF_SIZE) {
                        size = DEFAULT_BUF_SIZE;
                    } else {
                        size = Math.Max(size, _data.Length * 2);
                    }

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
