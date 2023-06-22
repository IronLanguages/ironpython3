﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("_io", typeof(IronPython.Modules.PythonIOModule))]
namespace IronPython.Modules {
    public static partial class PythonIOModule {
        public const int DEFAULT_BUFFER_SIZE = 8192;

        private static readonly object _blockingIOErrorKey = new object();
        private static readonly object _unsupportedOperationKey = new object();

        private const int O_RDONLY = 0x0000;
        private const int O_WRONLY = 0x0001;  
        private const int O_RDWR = 0x0002;  
        
        private const int O_APPEND = 0x0008;
        private const int O_CREAT = 0x0100;
        private const int O_TRUNC = 0x0200;
        private const int O_EXCL = 0x0400;

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException(
                _unsupportedOperationKey,
                new PythonType[] { PythonExceptions.ValueError, PythonExceptions.OSError },
                typeof(PythonExceptions.BaseException),
                dict,
                "UnsupportedOperation",
                "io"
            );
        }

        [PythonType, DontMapGetMemberNamesToDir]
        public class _IOBase : IDisposable, IEnumerator<object>, IEnumerable<object>, IWeakReferenceable, IDynamicMetaObjectProvider, IPythonExpandable {
            private bool _closed;
            internal CodeContext/*!*/ context;

            public _IOBase(CodeContext/*!*/ context) {
                this.context = context;
            }

            #region Public API

            public void __del__(CodeContext/*!*/ context) {
                close(context);
            }

            public _IOBase __enter__() {
                _checkClosed();
                return this;
            }

            public void __exit__(CodeContext/*!*/ context, params object[] excinfo) {
                close(context);
            }

            public void _checkClosed() {
                _checkClosed(null);
            }

            public void _checkClosed(string msg) {
                if (closed) {
                    throw PythonOps.ValueError(msg ?? "I/O operation on closed file.");
                }
            }

            public void _checkReadable() {
                _checkReadable(null);
            }

            public void _checkReadable(string msg) {
                if (!readable(context)) {
                    throw UnsupportedOperationWithMessage(context, msg ?? "File or stream is not readable.");
                }
            }

            public void _checkSeekable() {
                _checkSeekable(null);
            }

            public void _checkSeekable(string msg) {
                if (!seekable(context)) {
                    throw PythonOps.ValueError(msg ?? "File or stream is not seekable.");
                }
            }

            public void _checkWritable() {
                _checkWritable(null);
            }

            public void _checkWritable(string msg) {
                if (!writable(context)) {
                    throw UnsupportedOperationWithMessage(context, msg ?? "File or stream is not writable.");
                }
            }

            public virtual void close(CodeContext/*!*/ context) {
                try {
                    if (!_closed) {
                        flush(context);
                    }
                } finally {
                    _closed = true;
                }
            }

            public virtual bool closed {
                get { return _closed; }
            }

            public virtual int fileno(CodeContext/*!*/ context) {
                throw UnsupportedOperation(context, "fileno");
            }

            public virtual void flush(CodeContext/*!*/ context) {
                _checkClosed();
            }

            public virtual bool isatty(CodeContext/*!*/ context) {
                _checkClosed();
                return false;
            }

            [PythonHidden]
            public virtual Bytes peek(CodeContext/*!*/ context, int length=0) {
                _checkClosed();
                throw AttributeError("peek");
            }

            [PythonHidden]
            public virtual object read(CodeContext/*!*/ context, object length=null) {
                throw AttributeError("read");
            }

            [PythonHidden]
            public virtual Bytes read1(CodeContext/*!*/ context, int length=0) {
                throw AttributeError("read1");
            }

            public virtual bool readable(CodeContext/*!*/ context) {
                return false;
            }

            public virtual object readline(CodeContext/*!*/ context, int limit) {
                _checkClosed();

                List<Bytes> res = new List<Bytes>();
                int count = 0;
                while (limit < 0 || res.Count < limit) {
                    object cur = read(context, 1);
                    if (cur == null) {
                        break;
                    }
                    
                    Bytes curBytes = GetBytes(cur, "read()");
                    if (curBytes.Count == 0) {
                        break;
                    }

                    res.Add(curBytes);
                    count += curBytes.Count;
                    if (((IList<byte>)curBytes)[curBytes.Count - 1] == (byte)'\n') {
                        break;
                    }
                }

                return Bytes.Concat(res, count);
            }

            public object readline(CodeContext/*!*/ context, object limit=null) {
                return readline(context, GetInt(limit, -1));
            }

            public virtual PythonList readlines() {
                return readlines(null);
            }

            public virtual PythonList readlines(object hint=null) {
                int size = GetInt(hint, -1);

                PythonList res = new PythonList();
                if (size <= 0) {
                    foreach (object line in this) {
                        res.AddNoLock(line);
                    }
                    return res;
                }

                int count = 0;
                foreach (object line in this) {
                    if (line is Bytes bytes) {
                        res.AddNoLock(line);
                        count += bytes.Count;
                        if (count >= size) {
                            break;
                        }
                        continue;
                    }

                    if (line is string str) {
                        res.AddNoLock(line);
                        count += str.Length;
                        if (count >= size) {
                            break;
                        }
                        continue;
                    }

                    throw PythonOps.TypeError("next() should return string or bytes");
                }

                return res;
            }

            public virtual BigInteger seek(CodeContext/*!*/ context, BigInteger pos, [Optional]object whence) {
                throw UnsupportedOperation(context, "seek");
            }

            public virtual bool seekable(CodeContext/*!*/ context) {
                return false;
            }

            public virtual BigInteger tell(CodeContext/*!*/ context) {
                return seek(context, 0, 1);
            }

            public virtual BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
                throw UnsupportedOperation(context, "truncate");
            }

            public virtual bool writable(CodeContext/*!*/ context) {
                return false;
            }

            [PythonHidden]
            public virtual BigInteger write(CodeContext/*!*/ context, object buf) {
                throw AttributeError("write");
            }

            public virtual void writelines(CodeContext/*!*/ context, object lines) {
                _checkClosed();
                IEnumerator en = PythonOps.GetEnumerator(context, lines);
                while (en.MoveNext()) {
                    write(context, en.Current);
                }
            }

            #endregion

            ~_IOBase() {
                try {
                    close(context);
                } catch { }
            }

            #region IEnumerator<object> Members

            private object _current;

            object IEnumerator<object>.Current {
                get { return _current; }
            }

            #endregion

            #region IEnumerator Members

            object IEnumerator.Current {
                get { return _current; }
            }

            bool IEnumerator.MoveNext() {
                _current = readline(context, -1);

                if (_current == null) {
                    return false;
                }

                if (_current is Bytes bytes) {
                    return bytes.Count > 0;
                }

                if (_current is string str) {
                    return str.Length > 0;
                }

                return PythonOps.IsTrue(_current);
            }

            void IEnumerator.Reset() {
                _current = null;
                seek(context, 0, 0);
            }

            #endregion

            #region IEnumerable<object> Members

            [PythonHidden]
            public IEnumerator<object> GetEnumerator() {
                _checkClosed();
                return this;
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator() {
                _checkClosed();
                return this;
            }

            #endregion

            #region IDisposable Members

            void IDisposable.Dispose() { }

            #endregion

            #region IWeakReferenceable Members

            private WeakRefTracker _weakref;

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return _weakref;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _weakref = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                ((IWeakReferenceable)this).SetWeakRef(value);
            }

            #endregion

            #region IPythonExpandable Members


            private PythonDictionary _dict;

            private PythonDictionary EnsureCustomAttributes() {
                if (_dict is null) _dict = new PythonDictionary();
                return _dict;
            }

            public PythonDictionary __dict__ => EnsureCustomAttributes();

            IDictionary<object, object> IPythonExpandable.EnsureCustomAttributes() => EnsureCustomAttributes();

            IDictionary<object, object> IPythonExpandable.CustomAttributes => _dict;

            CodeContext IPythonExpandable.Context => context;

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<_IOBase>(parameter, this);
            }
            
            #endregion

            #region Private implementation details

            internal Exception UnsupportedOperation(CodeContext/*!*/ context, string attr) {
                throw PythonExceptions.CreateThrowable(
                    (PythonType)context.LanguageContext.GetModuleState(_unsupportedOperationKey),
                    string.Format("{0}.{1} not supported", PythonOps.GetPythonTypeName(this), attr)
                );
            }

            internal Exception UnsupportedOperationWithMessage(CodeContext/*!*/ context, string msg)
                => PythonExceptions.CreateThrowable((PythonType)context.LanguageContext.GetModuleState(_unsupportedOperationKey), msg);

            internal Exception AttributeError(string attrName) {
                throw PythonOps.AttributeError("'{0}' object has no attribute '{1}'", PythonOps.GetPythonTypeName(this), attrName);
            }

            internal Exception InvalidPosition(BigInteger pos) {
                return PythonOps.IOError("Raw stream returned invalid position {0}", pos);
            }
            
            #endregion
        }

        [PythonType]
        public class _RawIOBase : _IOBase, IDynamicMetaObjectProvider {
            public _RawIOBase(CodeContext/*!*/ context) : base(context) { }

            #region Public API

            public override object read(CodeContext/*!*/ context, object size=null) {
                int sizeInt = GetInt(size, -1);
                if (sizeInt < 0) {
                    return readall(context);
                }

                ByteArray arr = new ByteArray(new byte[sizeInt]);
                sizeInt = (int)readinto(context, arr);

                // cannot use arr.UnsafeByteList.RemoveRange(sizeInt, res.Count - sizeInt)
                // because HTTPResponse.client.readinto may wrap `arr` in `memoryview`
                // and never release it
                using var buf = ((IBufferProtocol)arr).GetBuffer();
                return Bytes.Make(buf.AsReadOnlySpan().Slice(0, sizeInt).ToArray());
            }

            public Bytes readall(CodeContext/*!*/ context) {
                List<Bytes> res = new List<Bytes>();
                int count = 0;
                for (; ; ) {
                    object cur = read(context, DEFAULT_BUFFER_SIZE);
                    if (cur == null) {
                        break;
                    }

                    Bytes curBytes = GetBytes(cur, "read()");
                    if (curBytes.Count == 0) {
                        break;
                    }

                    count += curBytes.Count;
                    res.Add(curBytes);
                }

                return Bytes.Concat(res, count);
            }

            public virtual BigInteger readinto(CodeContext/*!*/ context, object buf) {
                throw UnsupportedOperation(context, "readinto");
            }

            public override BigInteger write(CodeContext/*!*/ context, object buf) {
                throw UnsupportedOperation(context, "write");
            }

            #endregion
            
            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<_RawIOBase>(parameter, this);
            }

            #endregion
        }

        [PythonType]
        public class _BufferedIOBase : _IOBase, IDynamicMetaObjectProvider {
            public _BufferedIOBase(CodeContext/*!*/ context) : base(context) { }

            #region Public API

            public virtual object detach(CodeContext/*!*/ context) {
                throw UnsupportedOperation(context, "detach");
            }

            public override object read(CodeContext/*!*/ context, object length=null) {
                throw UnsupportedOperation(context, "read");
            }

            public virtual BigInteger readinto(CodeContext/*!*/ context, object buf) {
                int length = -1;
                if (PythonOps.HasAttr(context, buf, "__len__")) {
                    length = PythonOps.Length(buf);
                }

                object dataObj = read(context, length);
                if (dataObj == null) {
                    return BigInteger.Zero;
                }

                Bytes data = GetBytes(dataObj, "read()");
                if (buf is IList<byte> bytes) {
                    for (int i = 0; i < data.Count; i++) {
                        bytes[i] = ((IList<byte>)data)[i];
                    }
                    GC.KeepAlive(this);
                    return data.Count;
                }

                object setter;
                if (PythonOps.TryGetBoundAttr(buf, "__setitem__", out setter)) {
                    for (int i = 0; i < data.Count; i++) {
                        PythonOps.CallWithContext(context, setter, i, data[i]);
                    }
                    GC.KeepAlive(this);
                    return data.Count;
                }

                throw PythonOps.TypeError("must be read-write buffer, not " + PythonOps.GetPythonTypeName(buf));
            }

            public override BigInteger write(CodeContext/*!*/ context, object buf) {
                throw UnsupportedOperation(context, "write");
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<_BufferedIOBase>(parameter, this);
            }

            #endregion
        }

        [PythonType]
        public class _TextIOBase : _IOBase, IDynamicMetaObjectProvider {
            public _TextIOBase(CodeContext/*!*/ context) : base(context) { }

            #region Public API

            public virtual object detach(CodeContext/*!*/ context) {
                throw UnsupportedOperation(context, "detach");
            }

            public virtual string encoding {
                get { return null; }
            }

            public virtual string errors {
                get { return null; }
            }

            public virtual object newlines {
                get { return null; }
            }

            public override object read(CodeContext/*!*/ context, [DefaultParameterValue(-1)]object length) {
                throw UnsupportedOperation(context, "read");
            }

            public override object readline(CodeContext/*!*/ context, int limit=-1) {
                throw UnsupportedOperation(context, "readline");
            }

            public override BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
                throw UnsupportedOperation(context, "truncate");
            }

            public override BigInteger write(CodeContext/*!*/ context, object str) {
                throw UnsupportedOperation(context, "write");
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<_TextIOBase>(parameter, this);
            }

            #endregion
        }

        [PythonType]
        public class BufferedReader : _BufferedIOBase, IDynamicMetaObjectProvider {
            private _IOBase _rawIO;
            private object _raw;

            private int _bufSize;
            private Bytes _readBuf;
            private int _readBufPos;

            internal static BufferedReader Create(CodeContext/*!*/ context, object raw, int buffer_size=DEFAULT_BUFFER_SIZE) {
                var res = new BufferedReader(context, raw, buffer_size);
                res.__init__(context, raw, buffer_size);
                return res;
            }

            public BufferedReader(
                CodeContext/*!*/ context,
                params object[] args
            ) : base(context) {
            }

            public void __init__(
                CodeContext/*!*/ context,
                object raw,
                int buffer_size=DEFAULT_BUFFER_SIZE
            ) {
                this.raw = raw;

                if (_rawIO != null) {
                    if (!_rawIO.readable(context)) {
                        throw PythonOps.IOError("\"raw\" argument must be readable.");
                    }
                } else {
                    if (PythonOps.Not(PythonOps.Invoke(context, _raw, "readable"))) {
                        throw PythonOps.IOError("\"raw\" argument must be readable.");
                    }
                }
                if (buffer_size <= 0) {
                    throw PythonOps.ValueError("invalid buffer size (must be positive)");
                }

                _bufSize = buffer_size;
                _readBuf = Bytes.Empty;
            }

            #region Public API

            public object raw {
                get {
                    return _raw;
                }
                set {
                    _rawIO = value as _IOBase;
                    _raw = value;
                }
            }

            #region _BufferedIOMixin

            public override BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
                if (_rawIO != null) {
                    return _rawIO.truncate(context, pos);
                }

                return GetBigInt(
                    PythonOps.Invoke(context, _raw, "truncate", pos),
                    "truncate() should return integer"
                );
            }

            public override void close(CodeContext/*!*/ context) {
                if (!closed) {
                    try {
                        flush(context);
                    } finally {
                        if (_rawIO != null) {
                            _rawIO.close(context);
                        } else {
                            PythonOps.Invoke(context, _raw, "close");
                        }
                    }
                }
            }

            public override object detach(CodeContext/*!*/ context) {
                if (_raw == null) {
                    throw PythonOps.ValueError("raw stream already detached");
                }

                flush(context);
                object res = _raw;
                raw = null;
                return res;
            }

            public override bool seekable(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    return _rawIO.seekable(context);
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "seekable"));
            }

            public override bool readable(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    return _rawIO.readable(context);
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "readable"));
            }

            public override bool writable(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    return _rawIO.writable(context);
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "writable"));
            }

            public override bool closed {
                get {
                    if (_rawIO != null) {
                        return _rawIO.closed;
                    }
                    return PythonOps.IsTrue(PythonOps.GetBoundAttr(context, _raw, "closed"));
                }
            }

            public object name {
                get {
                    return PythonOps.GetBoundAttr(context, _raw, "name");
                }
            }

            public object mode {
                get {
                    return PythonOps.GetBoundAttr(context, _raw, "mode");
                }
            }

            public override int fileno(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    return _rawIO.fileno(context);
                }
                return GetInt(
                    PythonOps.Invoke(context, _raw, "fileno"),
                    "fileno() should return integer"
                );
            }

            public override bool isatty(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    return _rawIO.isatty(context);
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "isatty"));
            }

            #endregion

            public override object read(CodeContext/*!*/ context, object length=null) {
                int len = GetInt(length, -1);

                if (len < -1) {
                    throw PythonOps.ValueError("invalid number of bytes to read");
                }

                lock (this) {
                    return ReadNoLock(context, len);
                }
            }

            private Bytes ReadNoLock(CodeContext/*!*/ context, int length, bool read1 = false) {
                if (length == 0) {
                    return Bytes.Empty;
                }

                if (length < 0) {
                    List<Bytes> chunks = new List<Bytes>();
                    int count = 0;
                    if (_readBuf.Count > 0) {
                        chunks.Add(ResetReadBuf());
                        count += chunks[0].Count;
                    }
                    for (; ; ) {
                        object chunkObj;
                        if (_rawIO != null) {
                            chunkObj = _rawIO.read(context, -1);
                        } else {
                            chunkObj = PythonOps.Invoke(context, _raw, "read", -1);
                        }

                        Bytes chunk = GetBytes(chunkObj, "read()");
                        if (chunk == null || chunk.Count == 0) {
                            if (count == 0) {
                                return chunk;
                            }
                            break;
                        }
                        chunks.Add(chunk); 
                        count += chunk.Count;
                        if (read1) break;
                    }

                    GC.KeepAlive(this);
                    return Bytes.Concat(chunks, count);
                }

                if (length <= _readBuf.Count - _readBufPos) {
                    // requested data is already buffered
                    byte[] res = new byte[length];
                    Array.Copy(_readBuf.UnsafeByteArray, _readBufPos, res, 0, length);
                    _readBufPos += length;
                    if (_readBufPos == _readBuf.Count) {
                        ResetReadBuf();
                    }

                    GC.KeepAlive(this);
                    return Bytes.Make(res);
                } else {
                    // a read is required to provide requested amount of data
                    List<Bytes> chunks = new List<Bytes>();
                    int remaining = length;
                    if (_readBuf.Count > 0) {
                        chunks.Add(ResetReadBuf());
                        remaining -= chunks[0].Count;
                    }

                    while (remaining > 0) {
                        object chunkObj;
                        if (_rawIO != null) {
                            chunkObj = _rawIO.read(context, _bufSize);
                        } else {
                            chunkObj = PythonOps.Invoke(context, _raw, "read", _bufSize);
                        }

                        Bytes chunk = chunkObj != null ? GetBytes(chunkObj, "read()") : Bytes.Empty;

                        _readBuf = chunk;
                        if (_readBuf.Count == 0) {
                            break;
                        }
                        if (remaining >= _readBuf.Count - _readBufPos) {
                            remaining -= _readBuf.Count - _readBufPos;
                            chunks.Add(ResetReadBuf());
                        } else {
                            byte[] bytes = new byte[remaining];
                            Array.Copy(_readBuf.UnsafeByteArray, 0, bytes, 0, remaining);
                            chunks.Add(Bytes.Make(bytes));
                            _readBufPos = remaining;
                            remaining = 0;
                            break;
                        }
                        if (read1) break;
                    }
                    GC.KeepAlive(this);
                    return Bytes.Concat(chunks, length - remaining);
                }
            }

            public override Bytes peek(CodeContext/*!*/ context, int length=0) {
                _checkClosed();

                if (length <= 0 || length > _bufSize) {
                    length = _bufSize;
                }

                lock (this) {
                    return PeekNoLock(context, length);
                }
            }

            private Bytes PeekNoLock(CodeContext/*!*/ context, int length) {
                int bufLen = _readBuf.Count - _readBufPos;
                byte[] bytes = new byte[length];

                if (length <= bufLen) {
                    Array.Copy(_readBuf.UnsafeByteArray, _readBufPos, bytes, 0, length);
                    return Bytes.Make(bytes);
                }

                object nextObj;
                if (_rawIO != null) {
                    nextObj = _rawIO.read(context, length - _readBuf.Count + _readBufPos);
                } else {
                    nextObj = PythonOps.Invoke(context, _raw, "read", length - _readBuf.Count + _readBufPos);
                }

                Bytes next = nextObj != null ? GetBytes(nextObj, "read()") : Bytes.Empty;

                _readBuf = ResetReadBuf() + next;
                return _readBuf;
            }

            public override Bytes read1(CodeContext/*!*/ context, int length=0) {
                if (length == 0) {
                    return Bytes.Empty;
                } else if (length < 0) {
                    throw PythonOps.ValueError("number of bytes to read must be positive");
                }

                lock (this) {
                    int bufLen = _readBuf.Count - _readBufPos;
                    return ReadNoLock(context, bufLen > 0 ? Math.Min(length, bufLen) : length, read1: true);
                }
            }

            public override object readline(CodeContext context, int limit) {
                _checkClosed();

                if (limit == 0) {
                    return Bytes.Empty;
                }

                lock (this) {
                    bool limited = limit > 0;

                    List<Bytes> chunks = null;
                    int cnt = 0;
                    while (true) {
                        var buf = _readBuf.AsSpan().Slice(_readBufPos);
                        if (buf.Length > 0) {
                            // we hit the limit so we're done
                            bool done = false;
                            if (limited && buf.Length > limit - cnt) {
                                buf = buf.Slice(0, limit - cnt);
                                done = true;
                            }

                            // we found the eol so we're done
                            var idx = buf.IndexOf((byte)'\n');
                            if (idx != -1) {
                                buf = buf.Slice(0, idx + 1);
                                done = true;
                            }

                            if (done) {
                                _readBufPos += buf.Length;
                                if (_readBufPos == _readBuf.Count) {
                                    ResetReadBuf();
                                }
                                var bytes = Bytes.Make(buf.ToArray());
                                if (chunks is null) {
                                    return bytes;
                                }
                                chunks.Add(bytes);
                                cnt += buf.Length;
                                return Bytes.Concat(chunks, cnt);
                            }

                            (chunks ??= new List<Bytes>()).Add(ResetReadBuf());
                            cnt += buf.Length;
                        }

                        // end of file
                        if (!TryReadNextChunk(context)) {
                            if (chunks is null) {
                                return Bytes.Empty;
                            }
                            Debug.Assert(cnt > 0);
                            return Bytes.Concat(chunks, cnt);
                        }
                    }
                }

                bool TryReadNextChunk(CodeContext context) {
                    object chunkObj;
                    if (_rawIO != null) {
                        chunkObj = _rawIO.read(context, _bufSize);
                    } else {
                        chunkObj = PythonOps.Invoke(context, _raw, "read", _bufSize);
                    }

                    Bytes chunk = chunkObj != null ? GetBytes(chunkObj, "read()") : Bytes.Empty;

                    _readBuf = chunk;
                    return chunk.Count != 0;
                }
            }

            public override BigInteger tell(CodeContext/*!*/ context) {
                BigInteger res = _rawIO != null ?
                    _rawIO.tell(context) :
                    GetBigInt(
                        PythonOps.Invoke(context, _raw, "tell"),
                        "tell() should return integer"
                    );
                if (res < 0) {
                    throw InvalidPosition(res);
                }

                return res - _readBuf.Count + _readBufPos;
            }

            public BigInteger seek(double offset, [Optional]object whence) {
                _checkClosed();

                throw PythonOps.TypeError("an integer is required");
            }

            public override BigInteger seek(CodeContext/*!*/ context, BigInteger pos, [Optional]object whence) {
                int whenceInt = GetInt(whence);
                if (whenceInt < 0 || whenceInt > 2) {
                    throw PythonOps.ValueError("invalid whence ({0}, should be 0, 1, or 2)", whenceInt);
                }

                lock (this) {
                    if (whenceInt == 1) {
                        pos -= _readBuf.Count - _readBufPos;
                    }

                    object posObj;
                    if (_rawIO != null) {
                        posObj = _rawIO.seek(context, pos, whenceInt);
                    } else {
                        posObj = PythonOps.Invoke(context, _raw, "seek", whenceInt);
                    }

                    pos = GetBigInt(posObj, "seek() should return integer");
                    ResetReadBuf();
                    if (pos < 0) {
                        throw InvalidPosition(pos);
                    } 
                    GC.KeepAlive(this);

                    return pos;
                }
            }

            #endregion

            public string __repr__(CodeContext/*!*/ context) {
                string name = string.Empty;
                if (PythonOps.TryGetBoundAttr(this, "name", out var nameObj))
                    name = $" name={PythonOps.Repr(context, nameObj)}";

                return $"<_io.BufferedReader{name}>";
            }

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<BufferedReader>(parameter, this);
            }

            #endregion

            #region Private implementation details

            private Bytes ResetReadBuf() {
                Bytes res;
                if (_readBufPos == 0) {
                    res = _readBuf;
                } else {
                    byte[] bytes = new byte[_readBuf.Count - _readBufPos];
                    Array.Copy(_readBuf.UnsafeByteArray, _readBufPos, bytes, 0, bytes.Length);
                    res = Bytes.Make(bytes);
                    _readBufPos = 0;
                }
                _readBuf = Bytes.Empty;
                
                return res;
            }

            #endregion
        }

        [PythonType]
        public class BufferedWriter : _BufferedIOBase, IDynamicMetaObjectProvider {
            private _IOBase _rawIO;
            private object _raw;

            private int _bufSize;
            private List<byte> _writeBuf;

            internal static BufferedWriter Create(CodeContext/*!*/ context,
                object raw,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null) {

                var res = new BufferedWriter(context, raw, buffer_size, max_buffer_size);
                res.__init__(context, raw, buffer_size, max_buffer_size);
                return res;
            }

            public BufferedWriter(
                CodeContext/*!*/ context,
                object raw,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null
            )
                : base(context) {
            }

            public void __init__(
                CodeContext/*!*/ context,
                object raw,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null
            ) {
                if (max_buffer_size != null) {
                    PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "max_buffer_size is deprecated");
                }

                this.raw = raw;
                if (_rawIO != null) {
                    if (!_rawIO.writable(context)) {
                        throw PythonOps.IOError("\"raw\" argument must be writable.");
                    }
                } else {
                    if (PythonOps.Not(PythonOps.Invoke(context, _raw, "writable"))) {
                        throw PythonOps.IOError("\"raw\" argument must be writable.");
                    }
                }
                if (buffer_size <= 0) {
                    throw PythonOps.ValueError("invalid buffer size (must be positive)");
                }

                _bufSize = buffer_size;
                _writeBuf = new List<byte>();
            }

            #region Public API

            public object raw {
                get {
                    return _raw;
                }
                set {
                    _rawIO = value as _IOBase;
                    _raw = value;
                }
            }

            #region _BufferedIOMixin

            public override void close(CodeContext/*!*/ context) {
                if (!closed) {
                    try {
                        flush(context);
                    } finally {
                        if (_rawIO != null) {
                            _rawIO.close(context);
                        } else {
                            PythonOps.Invoke(context, _raw, "close");
                        }
                    }
                }
            }

            public override object detach(CodeContext/*!*/ context) {
                if (_raw == null) {
                    throw PythonOps.ValueError("raw stream already detached");
                }

                flush(context);
                object res = _raw;
                raw = null;
                return res;
            }

            public override bool seekable(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    var res = _rawIO.seekable(context);
                    GC.KeepAlive(this);
                    return res;
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "seekable"));
            }

            public override bool readable(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    var res = _rawIO.readable(context);
                    GC.KeepAlive(this);
                    return res;
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "readable"));
            }

            public override bool writable(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    var res = _rawIO.writable(context);
                    GC.KeepAlive(this);
                    return res;
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "writable"));
            }

            public override bool closed {
                get {
                    if (_rawIO != null) {
                        return _rawIO.closed;
                    }
                    return PythonOps.IsTrue(PythonOps.GetBoundAttr(context, _raw, "closed"));
                }
            }

            public object name {
                get {
                    return PythonOps.GetBoundAttr(context, _raw, "name");
                }
            }

            public object mode {
                get {
                    return PythonOps.GetBoundAttr(context, _raw, "mode");
                }
            }

            public override int fileno(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    var res = _rawIO.fileno(context);
                    GC.KeepAlive(this);
                    return res;
                }
                return GetInt(
                    PythonOps.Invoke(context, _raw, "fileno"),
                    "fileno() should return integer"
                );
            }

            public override bool isatty(CodeContext/*!*/ context) {
                if (_rawIO != null) {
                    var res = _rawIO.isatty(context);
                    GC.KeepAlive(this);
                    return res;
                }
                return PythonOps.IsTrue(PythonOps.Invoke(context, _raw, "isatty"));
            }

            #endregion

            public override BigInteger write(CodeContext/*!*/ context, object buf) {
                _checkClosed("write to closed file");

                using var buffer = Converter.Convert<IBufferProtocol>(buf).GetBuffer();
                var bytes = buffer.AsReadOnlySpan();
                lock (this) {
                    if (_writeBuf.Count > _bufSize) {
                        FlushNoLock(context);
                    }

                    int count = _writeBuf.Count;
                    _writeBuf.AddRange(bytes.ToArray());
                    count = _writeBuf.Count - count;

                    if (_writeBuf.Count > _bufSize) {
                        try {
                            FlushNoLock(context);
                        } catch (BlockingIOException) {
                            if (_writeBuf.Count > _bufSize) {
                                // do a partial write
                                int extra = _writeBuf.Count - _bufSize;
                                count -= extra;
                                _writeBuf.RemoveRange(_bufSize, extra);
                            }
                            throw;
                        }
                    }

                    return count;
                }
            }

            public override BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
                lock (this) {
                    FlushNoLock(context);
                    if (pos == null) {
                        if (_rawIO != null) {
                            pos = _rawIO.tell(context);
                        } else {
                            pos = GetBigInt(
                                PythonOps.Invoke(context, _raw, "tell"),
                                "tell() should return integer"
                            );
                        }
                    }

                    if (_rawIO != null) {
                        return _rawIO.truncate(context, pos);
                    }
                    var res = GetBigInt(
                        PythonOps.Invoke(context, _raw, "truncate", pos),
                        "truncate() should return integer"
                    );
                    GC.KeepAlive(this);
                    return res;
                }
            }

            public override void flush(CodeContext/*!*/ context) {
                lock (this) {
                    FlushNoLock(context);
                }
            }

            private void FlushNoLock(CodeContext/*!*/ context) {
                _checkClosed("flush of closed file");

                if (_writeBuf == null) return; // finalizer can call flush without this being initialized

                int count = 0;
                try {
                    while (_writeBuf.Count > 0) {
                        object writtenObj;
                        var bytes = Bytes.Make(_writeBuf.ToArray());
                        if (_rawIO != null) {
                            writtenObj = _rawIO.write(context, bytes);
                        } else {
                            writtenObj = PythonOps.Invoke(context, _raw, "write", bytes);
                        }

                        int written = GetInt(writtenObj, "write() should return integer");
                        if (written > _writeBuf.Count || written < 0) {
                            throw PythonOps.IOError("write() returned incorrect number of bytes");
                        }
                        _writeBuf.RemoveRange(0, written);
                        count += written;
                    }
                } catch (BlockingIOException e) {
                    object w;
                    int written;
                    if (!PythonOps.TryGetBoundAttr(e, "characters_written", out w) ||
                        !TryGetInt(w, out written)) {
                        throw;
                    }
                    _writeBuf.RemoveRange(0, written);
                    count += written;
                    throw;
                }
            }

            public override BigInteger tell(CodeContext/*!*/ context) {
                BigInteger res = _rawIO != null ?
                    _rawIO.tell(context) :
                    GetBigInt(
                        PythonOps.Invoke(context, _raw, "tell"),
                        "tell() should return integer"
                    );
                if (res < 0) {
                    throw InvalidPosition(res);
                }
                GC.KeepAlive(this);
                return res + _writeBuf.Count;
            }

            public BigInteger seek(double offset, [Optional]object whence) {
                _checkClosed();

                throw PythonOps.TypeError("an integer is required");
            }

            public override BigInteger seek(CodeContext/*!*/ context, BigInteger pos, [Optional]object whence) {
                int whenceInt = GetInt(whence);
                if (whenceInt < 0 || whenceInt > 2) {
                    throw PythonOps.ValueError("invalid whence ({0}, should be 0, 1, or 2)", whenceInt);
                }
                lock (this) {
                    FlushNoLock(context);

                    BigInteger res = _rawIO != null ?
                        _rawIO.seek(context, pos, whenceInt) :
                        res = GetBigInt(
                            PythonOps.Invoke(context, _raw, "seek", pos, whenceInt),
                            "seek() should return integer"
                        );
                    if (res < 0) {
                        throw InvalidPosition(pos);
                    }
                    GC.KeepAlive(this);
                    return res;
                }
            }

            #endregion

            public string __repr__(CodeContext/*!*/ context) {
                string name = string.Empty;
                if (PythonOps.TryGetBoundAttr(this, "name", out var nameObj))
                    name = $" name={PythonOps.Repr(context, nameObj)}";

                return $"<_io.BufferedWriter{name}>";
            }

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<BufferedWriter>(parameter, this);
            }

            #endregion
        }

        [PythonType]
        public class BufferedRandom : _BufferedIOBase, IDynamicMetaObjectProvider {
            private _IOBase _inner;
            
            private int _bufSize;
            private Bytes _readBuf;
            private int _readBufPos;
            private List<byte> _writeBuf;

            internal static BufferedRandom Create(CodeContext/*!*/ context,
                _IOBase raw,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null) {
                var res = new BufferedRandom(context, raw, buffer_size, max_buffer_size);
                res.__init__(context, raw, buffer_size, max_buffer_size);
                return res;
            }

            public BufferedRandom(
                CodeContext/*!*/ context,
                _IOBase raw,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null
            ) : base(context) {
            }

            public void __init__(
                CodeContext/*!*/ context,
                _IOBase raw,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null
            ) {
                if (max_buffer_size != null) {
                    PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "max_buffer_size is deprecated");
                }
                raw._checkSeekable();
                if (buffer_size <= 0) {
                    throw PythonOps.ValueError("invalid buffer size (must be positive)");
                } else if (!raw.readable(context)) {
                    throw PythonOps.IOError("\"raw\" argument must be readable.");
                } else if (!raw.writable(context)) {
                    throw PythonOps.IOError("\"raw\" argument must be writable.");
                }

                _bufSize = buffer_size;
                _inner = raw;
                _readBuf = Bytes.Empty;
                _writeBuf = new List<byte>();
            }

            #region Public API

            public _IOBase raw {
                get {
                    return _inner;
                }
                set {
                    _inner = value;
                }
            }

            #region _BufferedIOMixin

            public override void close(CodeContext/*!*/ context) {
                if (!closed) {
                    try {
                        flush(context);
                    } finally {
                        _inner.close(context);
                    }
                }
            }

            public override object detach(CodeContext/*!*/ context) {
                if (_inner == null) {
                    throw PythonOps.ValueError("raw stream already detached");
                }

                flush(context);
                _IOBase res = _inner;
                _inner = null;
                return res;
            }

            public override bool seekable(CodeContext/*!*/ context) {
                var res = _inner.seekable(context);
                GC.KeepAlive(this);
                return res;
            }

            public override bool readable(CodeContext/*!*/ context) {
                var res = _inner.readable(context);
                GC.KeepAlive(this);
                return res;
            }

            public override bool writable(CodeContext/*!*/ context) {
                var res = _inner.writable(context);
                GC.KeepAlive(this);
                return res;
            }

            public override bool closed {
                get { return _inner.closed; }
            }

            public object name {
                get {
                    return PythonOps.GetBoundAttr(context, _inner, "name");
                }
            }

            public object mode {
                get {
                    return PythonOps.GetBoundAttr(context, _inner, "mode");
                }
            }

            public override int fileno(CodeContext/*!*/ context) {
                var res = _inner.fileno(context);
                GC.KeepAlive(this);
                return res;
            }

            public override bool isatty(CodeContext/*!*/ context) {
                return _inner.isatty(context);
            }

            #endregion

            #region BufferedReader

            public override object read(CodeContext/*!*/ context, object length=null) {
                flush(context);
                int len = GetInt(length, -1);

                if (len < -1) {
                    throw PythonOps.ValueError("invalid number of bytes to read");
                }

                lock (this) {
                    return ReadNoLock(context, len);
                }
            }

            private Bytes ReadNoLock(CodeContext/*!*/ context, int length) {
                if (length == 0) {
                    return Bytes.Empty;
                }

                if (length < 0) {
                    List<Bytes> chunks = new List<Bytes>();
                    int count = 0;
                    if (_readBuf.Count > 0) {
                        chunks.Add(ResetReadBuf());
                        count += chunks[0].Count;
                    }
                    for (; ; ) {
                        Bytes chunk = (Bytes)_inner.read(context, -1);
                        if (chunk == null || chunk.Count == 0) {
                            if (count == 0) {
                                return chunk;
                            }
                            break;
                        }
                        chunks.Add(chunk);
                        count += chunk.Count;
                    }
                    GC.KeepAlive(this);
                    return Bytes.Concat(chunks, count);
                }

                if (length < _readBuf.Count - _readBufPos) {
                    // requested data is already buffered
                    byte[] res = new byte[length];
                    Array.Copy(_readBuf.UnsafeByteArray, _readBufPos, res, 0, length);
                    _readBufPos += length;
                    if (_readBufPos == _readBuf.Count) {
                        ResetReadBuf();
                    }
                    GC.KeepAlive(this);
                    return Bytes.Make(res);
                } else {
                    // a read is required to provide requested amount of data
                    List<Bytes> chunks = new List<Bytes>();
                    int remaining = length;
                    if (_readBuf.Count > 0) {
                        chunks.Add(ResetReadBuf());
                        remaining -= chunks[0].Count;
                    }

                    while (remaining > 0) {
                        _readBuf = (Bytes)_inner.read(context, _bufSize) ?? Bytes.Empty;
                        if (_readBuf.Count == 0) {
                            break;
                        }
                        if (remaining >= _readBuf.Count - _readBufPos) {
                            remaining -= _readBuf.Count - _readBufPos;
                            chunks.Add(ResetReadBuf());
                        } else {
                            byte[] bytes = new byte[remaining];
                            Array.Copy(_readBuf.UnsafeByteArray, 0, bytes, 0, remaining);
                            chunks.Add(Bytes.Make(bytes));
                            _readBufPos = remaining;
                            remaining = 0;
                            break;
                        }
                    }
                    GC.KeepAlive(this);
                    return Bytes.Concat(chunks, length - remaining);
                }
            }

            public override Bytes peek(CodeContext/*!*/ context, int length=0) {
                _checkClosed();

                flush(context);
                if (length <= 0 || length > _bufSize) {
                    length = _bufSize;
                }

                lock (this) {
                    return PeekNoLock(context, length);
                }
            }

            private Bytes PeekNoLock(CodeContext/*!*/ context, int length) {
                int bufLen = _readBuf.Count - _readBufPos;
                byte[] bytes = new byte[length];

                if (length <= bufLen) {
                    Array.Copy(_readBuf.UnsafeByteArray, _readBufPos, bytes, 0, length);
                    return Bytes.Make(bytes);
                }

                Bytes next = (Bytes)_inner.read(context, length - _readBuf.Count + _readBufPos) ?? Bytes.Empty;
                _readBuf = ResetReadBuf() + next;
                GC.KeepAlive(this);
                return _readBuf;
            }

            public override Bytes read1(CodeContext/*!*/ context, int length=0) {
                flush(context);
                if (length == 0) {
                    return Bytes.Empty;
                } else if (length < 0) {
                    throw PythonOps.ValueError("number of bytes to read must be positive");
                }

                lock (this) {
                    PeekNoLock(context, 1);
                    return ReadNoLock(context, Math.Min(length, _readBuf.Count - _readBufPos));
                }
            }

            #region Private implementation details

            private Bytes ResetReadBuf() {
                Bytes res;
                if (_readBufPos == 0) {
                    res = _readBuf;
                } else {
                    byte[] bytes = new byte[_readBuf.Count - _readBufPos];
                    Array.Copy(_readBuf.UnsafeByteArray, _readBufPos, bytes, 0, bytes.Length);
                    res = Bytes.Make(bytes);
                    _readBufPos = 0;
                }
                _readBuf = Bytes.Empty;

                return res;
            }

            #endregion

            #endregion

            #region BufferedWriter

            public override BigInteger write(CodeContext/*!*/ context, object buf) {
                _checkClosed("write to closed file");

                // undo any read-ahead
                if (_readBuf.Count > 0) {
                    lock (this) {
                        _inner.seek(context, _readBufPos - _readBuf.Count, 1);
                        ResetReadBuf();
                    }
                }

                using var buffer = Converter.Convert<IBufferProtocol>(buf).GetBuffer();
                var bytes = buffer.AsReadOnlySpan();
                lock (this) {
                    if (_writeBuf.Count > _bufSize) {
                        FlushNoLock(context);
                    }

                    int count = _writeBuf.Count;
                    _writeBuf.AddRange(bytes.ToArray());
                    count = _writeBuf.Count - count;

                    if (_writeBuf.Count > _bufSize) {
                        try {
                            FlushNoLock(context);
                        } catch (BlockingIOException) {
                            if (_writeBuf.Count > _bufSize) {
                                // do a partial write
                                int extra = _writeBuf.Count - _bufSize;
                                count -= extra;
                                _writeBuf.RemoveRange(_bufSize, extra);
                            }
                            throw;
                        }
                    }

                    return count;
                }
            }

            public override void flush(CodeContext/*!*/ context) {
                lock (this) {
                    FlushNoLock(context);
                }
            }

            private void FlushNoLock(CodeContext/*!*/ context) {
                _checkClosed("flush of closed file");

                int count = 0;
                try {
                    while (_writeBuf.Count > 0) {
                        var bytes = Bytes.Make(_writeBuf.ToArray());
                        int written = (int)_inner.write(context, bytes);
                        if (written > _writeBuf.Count || written < 0) {
                            throw PythonOps.IOError("write() returned incorrect number of bytes");
                        }
                        _writeBuf.RemoveRange(0, written);
                        count += written;
                    }
                } catch (BlockingIOException e) {
                    object w;
                    int written;
                    if (!PythonOps.TryGetBoundAttr(e, "characters_written", out w) ||
                        !TryGetInt(w, out written)) {
                        throw;
                    }
                    _writeBuf.RemoveRange(0, written);
                    count += written;
                    throw;
                }
            }

            #endregion

            public override BigInteger readinto(CodeContext/*!*/ context, object buf) {
                flush(context);
                return base.readinto(context, buf);
            }

            public BigInteger seek(double offset, [Optional]object whence) {
                _checkClosed();

                throw PythonOps.TypeError("an integer is required");
            }

            public override BigInteger seek(CodeContext/*!*/ context, BigInteger pos, [Optional]object whence) {
                int whenceInt = GetInt(whence);
                if (whenceInt < 0 || whenceInt > 2) {
                    throw PythonOps.ValueError("invalid whence ({0}, should be 0, 1, or 2)", whenceInt);
                }

                lock (this) {
                    FlushNoLock(context);

                    // undo any read-ahead
                    if (_readBuf.Count > 0) {
                        _inner.seek(context, _readBufPos - _readBuf.Count, 1);
                    }

                    pos = _inner.seek(context, pos, whence);
                    ResetReadBuf();
                    if (pos < 0) {
                        throw PythonOps.IOError("seek() returned invalid position");
                    }
                    GC.KeepAlive(this);
                    return pos;
                }
            }

            public override BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
                lock (this) {
                    FlushNoLock(context);
                    if (pos == null) {
                        pos = tell(context);
                    }
                    var res = _inner.truncate(context, pos);
                    GC.KeepAlive(this);
                    return res;
                }
            }

            public override BigInteger tell(CodeContext/*!*/ context) {
                BigInteger res = _inner.tell(context);
                if (res < 0) {
                    throw InvalidPosition(res);
                }

                if (_writeBuf.Count > 0) {
                    return res + _writeBuf.Count;
                } else {
                    return res - _readBuf.Count + _readBufPos;
                }
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<BufferedRandom>(parameter, this);
            }

            #endregion
        }

        [PythonType]
        public class BufferedRWPair : _BufferedIOBase, IDynamicMetaObjectProvider {
            private BufferedReader _reader;
            private BufferedWriter _writer;

            public BufferedRWPair(
                CodeContext/*!*/ context,
                object reader,
                object writer,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null
            ) : base(context) {
            }

            public void __init__(
                CodeContext/*!*/ context,
                object reader,
                object writer,
                int buffer_size=DEFAULT_BUFFER_SIZE,
                object max_buffer_size=null
            ) {
                if (max_buffer_size != null) {
                    PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "max_buffer_size is deprecated");
                }
                this.reader = reader;
                this.writer = writer;

                if (!_reader.readable(context)) {
                    throw PythonOps.IOError("\"reader\" object must be readable.");
                }
                if (!_writer.writable(context)) {
                    throw PythonOps.IOError("\"writer\" object must be writable.");
                }
            }

            #region Public API

            public object reader {
                get { return _reader; }
                set {
                    if (!(value is BufferedReader reader)) {
                        reader = BufferedReader.Create(context, value, DEFAULT_BUFFER_SIZE);
                    }
                    _reader = reader;
                }
            }

            public object writer {
                get { return _writer; }
                set {
                    if (!(value is BufferedWriter writer)) {
                        writer = BufferedWriter.Create(context, value, DEFAULT_BUFFER_SIZE, null);
                    }
                    _writer = writer;
                }
            }

            public override object read(CodeContext/*!*/ context, object length=null) {
                var res = _reader.read(context, length);
                GC.KeepAlive(this);
                return res;
            }

            public override BigInteger readinto(CodeContext/*!*/ context, object buf) {
                var res = _reader.readinto(context, buf);
                GC.KeepAlive(this);
                return res;
            }

            public override BigInteger write(CodeContext/*!*/ context, object buf) {
                var res = _writer.write(context, buf);
                GC.KeepAlive(this);
                return res;
            }

            public override Bytes peek(CodeContext/*!*/ context, int length=0) {
                var res = _reader.peek(context, length);
                GC.KeepAlive(this);
                return res;
            }

            public override Bytes read1(CodeContext/*!*/ context, int length) {
                var res = _reader.read1(context, length);
                GC.KeepAlive(this);
                return res;
            }

            public override bool readable(CodeContext/*!*/ context) {
                var res = _reader.readable(context);
                GC.KeepAlive(this);
                return res;
            }

            public override bool writable(CodeContext/*!*/ context) {
                var res = _writer.writable(context);
                GC.KeepAlive(this);
                return res;
            }

            public override void flush(CodeContext/*!*/ context) {
                _writer.flush(context);
                GC.KeepAlive(this);
            }

            public override void close(CodeContext/*!*/ context) {
                try {
                    _writer.close(context);
                } finally {
                    _reader.close(context);
                }
                GC.KeepAlive(this);
            }

            public override bool isatty(CodeContext/*!*/ context) {
                var res = _reader.isatty(context) || _writer.isatty(context);
                GC.KeepAlive(this);
                return res;
            }

            public override bool closed {
                get {
                    return _writer.closed;
                }
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<BufferedRWPair>(parameter, this);
            }

            #endregion
        }

        [PythonType]
        public class TextIOWrapper : _TextIOBase, IEnumerator<object>, IEnumerable<object>, ICodeFormattable, IDynamicMetaObjectProvider {
            public int _CHUNK_SIZE = 128;

            internal _BufferedIOBase _bufferTyped;
            private object _buffer;
            internal string _encoding, _errors;
            private bool _seekable, _telling;
            private object _encoder, _decoder;

            private bool _line_buffering;
            private bool _write_through;
            private bool _readUniversal;
            private bool _readTranslate;
            internal bool _writeTranslate;
            private string _readNL, _writeNL;

            private int _decodedCharsUsed;
            private string _decodedChars;

            // snapshots: Used for accurate tell() and seek() behavior for multibyte codecs.
            // _nextInput, if not null, is the chunk of input bytes that comes after the
            // snapshot point. _decodeFlags is used to reconstruct the decoder state.
            private Bytes _nextInput;
            private int _decodeFlags;

            public TextIOWrapper(CodeContext/*!*/ context) : base(context) { }

            internal static TextIOWrapper Create(CodeContext/*!*/ context,
                object buffer,
                string encoding = null,
                string errors = null,
                string newline = null,
                bool line_buffering = false, bool write_through = false) {
                var res = new TextIOWrapper(context);
                res.__init__(context, buffer, encoding, errors, newline, line_buffering, write_through);
                return res;
            }

            public void __init__(
                CodeContext/*!*/ context,
                object buffer,
                string encoding = null,
                string errors = null,
                string newline = null,
                bool line_buffering = false,
                bool write_through = false
            ) {
                switch(newline) {
                    case null:
                    case "":
                    case "\n":
                    case "\r":
                    case "\r\n":
                        break;
                    default:
                        throw PythonOps.ValueError(string.Format("illegal newline value: " + newline));
                }

                encoding ??= context.LanguageContext.PythonOptions.Utf8Mode ? "UTF-8" : PythonLocale.PreferredEncoding;

                if (errors == null) {
                    errors = "strict";
                }

                _bufferTyped = buffer as _BufferedIOBase;
                _buffer = buffer;
                _encoding = encoding;
                _errors = errors;
                _seekable = _telling = _bufferTyped != null ?
                    _bufferTyped.seekable(context) :
                    PythonOps.IsTrue(PythonOps.Invoke(context, _buffer, "seekable"));

                _line_buffering = line_buffering;
                _write_through = write_through;
                _readUniversal = string.IsNullOrEmpty(newline);
                _readTranslate = newline == null;
                _readNL = newline;
                _writeTranslate = newline != "";
                _writeNL = string.IsNullOrEmpty(newline) ? System.Environment.NewLine : newline;

                _decodedChars = "";
                _decodedCharsUsed = 0;
            }

            #region Public API

            public object buffer => _buffer;

            public override string encoding => _encoding;

            public override string errors => _errors;

            public bool line_buffering => _line_buffering;

            public bool write_through => _write_through;

            public override object newlines {
                get {
                    if (_readUniversal && _decoder != null) {
                        if (_decoder is IncrementalNewlineDecoder typedDecoder) {
                            return typedDecoder.newlines;
                        } else {
                            return PythonOps.GetBoundAttr(context, _decoder, "newlines");
                        }
                    }

                    return null;
                }
            }

            public override bool seekable(CodeContext/*!*/ context) {
                return _bufferTyped != null ?
                    _bufferTyped.seekable(context) :
                    PythonOps.IsTrue(PythonOps.Invoke(context, _buffer, "seekable"));
            }

            public override bool readable(CodeContext/*!*/ context) {
                return _bufferTyped != null ?
                    _bufferTyped.readable(context) :
                    PythonOps.IsTrue(PythonOps.Invoke(context, _buffer, "readable"));
            }

            public override bool writable(CodeContext/*!*/ context) {
                return _bufferTyped != null ?
                    _bufferTyped.writable(context) :
                    PythonOps.IsTrue(PythonOps.Invoke(context, _buffer, "writable"));
            }

            public override void flush(CodeContext/*!*/ context) {
                if (_bufferTyped != null) {
                    _bufferTyped.flush(context);
                } else {
                    PythonOps.Invoke(context, _buffer, "flush");
                }
                _telling = _seekable;
            }

            public override void close(CodeContext/*!*/ context) {
                if (!closed) {
                    try {
                        flush(context);
                    } finally {
                        if (_bufferTyped != null) {
                            _bufferTyped.close(context);
                        } else {
                            PythonOps.Invoke(context, _buffer, "close");
                        }
                    }
                }
            }

            public override bool closed {
                get {
                    return _bufferTyped != null ?
                        _bufferTyped.closed :
                        PythonOps.IsTrue(
                            PythonOps.GetBoundAttr(context, _buffer, "closed")
                        );
                }
            }

            public object name {
                get {
                    return PythonOps.GetBoundAttr(context, _buffer, "name");
                }
            }

            public override int fileno(CodeContext/*!*/ context) {
                return _bufferTyped != null ?
                    _bufferTyped.fileno(context) :
                    GetInt(
                        PythonOps.Invoke(context, _buffer, "fileno"),
                        "fileno() should return an int"
                    );
            }

            public override bool isatty(CodeContext/*!*/ context) {
                return _bufferTyped != null ?
                    _bufferTyped.isatty(context) :
                    PythonOps.IsTrue(PythonOps.Invoke(context, _buffer, "isatty"));
            }

            public override BigInteger write(CodeContext/*!*/ context, object s) {
                if (!(s is string str)) {
                    if (!(s is Extensible<string> es)) {
                        throw PythonOps.TypeError("must be unicode, not {0}", PythonOps.GetPythonTypeName(s));
                    }
                    str = es.Value;
                }
                if (closed) {
                    throw PythonOps.ValueError("write to closed file");
                }

                int length = str.Length;
                bool hasLF = (_writeTranslate || _line_buffering) && str.Contains("\n");
                if (hasLF && _writeTranslate && _writeNL != "\n") {
                    str = str.Replace("\n", _writeNL);
                }

                var bytes = StringOps.encode(context, str, _encoding, _errors);
                if (_bufferTyped != null) {
                    _bufferTyped.write(context, bytes);
                } else {
                    PythonOps.Invoke(context, _buffer, "write", bytes);
                }

                if (_write_through || _line_buffering && (hasLF || str.Contains("\r"))) {
                    flush(context);
                }

                _nextInput = null;
                if (_decoder != null) {
                    PythonOps.Invoke(context, _decoder, "reset");
                }

                GC.KeepAlive(this);
                return length;
            }

            public override BigInteger tell(CodeContext/*!*/ context) {
                if (!_seekable) {
                    throw PythonOps.IOError("underlying stream is not seekable");
                }
                if (!_telling) {
                    throw PythonOps.IOError("telling position disabled by next() call");
                }

                flush(context);
                BigInteger pos = _bufferTyped != null ?
                    _bufferTyped.tell(context) :
                    GetBigInt(
                        PythonOps.Invoke(context, _buffer, "tell"),
                        "tell() should return an integer"
                    );
                if (pos < 0) {
                    throw InvalidPosition(pos);
                }

                object decoder = _decoder;
                if (decoder == null || _nextInput == null) {
                    if (!string.IsNullOrEmpty(_decodedChars)) {
                        throw PythonOps.AssertionError("pending decoded text");
                    }
                    return pos;
                }
                IncrementalNewlineDecoder typedDecoder = decoder as IncrementalNewlineDecoder;

                // skip backwards to snapshot point
                pos -= _nextInput.Count;
                
                // determine number of decoded chars used up after snapshot
                int skip = _decodedCharsUsed;
                if (skip == 0) {
                    return pos;
                }

                // start at snapshot point and run the decoder until we have enough chars
                PythonTuple state;
                if (typedDecoder != null) {
                    state = typedDecoder.getstate(context);
                } else {
                    state = (PythonTuple)PythonOps.Invoke(context, decoder, "getstate");
                }
                try {
                    // keep track of starting position
                    if (typedDecoder != null) {
                        typedDecoder.SetState(context, Bytes.Empty, _decodeFlags);
                    } else {
                        PythonOps.Invoke(context, decoder, "setstate", PythonTuple.MakeTuple(Bytes.Empty, _decodeFlags));
                    }
                    BigInteger startPos = pos;
                    int bytesFed = 0;
                    int charsDecoded = 0;

                    // Feed the decoder one byte at a time while keeping track of the most recent
                    // safe position for snapshotting, i.e. a position in the file where the
                    // decoder's buffer is empty, allowing seek() to safely start advancing from
                    // there.
                    foreach (byte nextByte in _nextInput) {
                        Bytes next = Bytes.FromByte(nextByte);
                        bytesFed++;
                        if (typedDecoder != null) {
                            charsDecoded += typedDecoder.decode(context, next, false).Length;
                        } else {
                            charsDecoded += ((string)PythonOps.Invoke(context, decoder, "decode", next)).Length;
                        }

                        Bytes decodeBuffer;
                        if (typedDecoder != null) {
                            typedDecoder.GetState(context, out decodeBuffer, out _decodeFlags);
                        } else {
                            PythonTuple tuple = (PythonTuple)PythonOps.Invoke(context, decoder, "getstate");
                            decodeBuffer = GetBytes(tuple[0], "getstate");
                            _decodeFlags = Converter.ConvertToInt32(tuple[1]);
                        }

                        if ((decodeBuffer == null || decodeBuffer.Count == 0) && charsDecoded <= skip) {
                            // safe starting point
                            startPos += bytesFed;
                            skip -= charsDecoded;
                            bytesFed = 0;
                            charsDecoded = 0;
                        }

                        if (charsDecoded >= skip) {
                            // not enough decoded data; signal EOF for more
                            if (typedDecoder != null) {
                                charsDecoded += typedDecoder.decode(context, Bytes.Empty, true).Length;
                            } else {
                                charsDecoded += ((string)PythonOps.Invoke(context, decoder, "decode", Bytes.Empty, true)).Length;
                            }

                            if (charsDecoded < skip) {
                                throw PythonOps.IOError("can't reconstruct logical file position");
                            }
                            break;
                        }
                    }

                    return startPos;
                } finally {
                    if (typedDecoder != null) {
                        typedDecoder.setstate(context, state);
                    } else {
                        PythonOps.Invoke(context, decoder, "setstate", state);
                    }
                }
            }

            public override BigInteger truncate(CodeContext/*!*/ context, object pos=null) {
                flush(context);
                if (pos == null) {
                    pos = tell(context);
                }

                BigInteger position;
                if (pos is int) {
                    position = (int)pos;
                } else if (pos is BigInteger) {
                    position = (BigInteger)pos;
                } else if (!Converter.TryConvertToBigInteger(pos, out position)) {
                    throw PythonOps.TypeError("an integer is required");
                }

                var savePos = tell(context);
                seek(context, position, 0);
                var ret = _bufferTyped != null ?
                    _bufferTyped.truncate(context, null) :
                    GetBigInt(
                        PythonOps.Invoke(context, _buffer, "truncate"),
                        "truncate() should return an integer"
                    );
                seek(context, savePos, 0);
                return ret;
            }

            public override object detach(CodeContext/*!*/ context) {
                if (_buffer == null) {
                    throw PythonOps.ValueError("buffer is already detached");
                }

                flush(context);
                object res = _bufferTyped ?? _buffer;
                _buffer = _bufferTyped = null;
                return res;
            }

            public BigInteger seek(double offset, [Optional]object whence) => throw PythonOps.TypeError("integer argument expected, got float");

            public override BigInteger seek(CodeContext/*!*/ context, BigInteger cookie, [Optional]object whence) {
                int whenceInt = GetInt(whence);
                if (closed) {
                    throw PythonOps.ValueError("tell on closed file");
                }
                if (!_seekable) {
                    throw PythonOps.IOError("underlying stream is not seekable");
                }

                IncrementalNewlineDecoder typedDecoder;
                if (whenceInt == 1) {
                    // seek relative to the current position
                    if (cookie != 0) {
                        throw PythonOps.IOError("can't do nonzero cur-relative seeks");
                    }
                    whenceInt = 0;
                    cookie = tell(context);
                } else if (whenceInt == 2) {
                    // seek relative to the end of the stream
                    if (cookie != 0) {
                        throw PythonOps.IOError("can't do nonzero end-relative seeks");
                    }
                    flush(context);
                    BigInteger pos = _bufferTyped != null ?
                        _bufferTyped.seek(context, BigInteger.Zero, 2) :
                        GetBigInt(
                            PythonOps.Invoke(context, _buffer, "seek", BigInteger.Zero, 2),
                            "seek() should return an integer"
                        );
                    if (pos < 0) {
                        throw InvalidPosition(pos);
                    }
                    SetDecodedChars(string.Empty);
                    _nextInput = null;
                    if (_decoder != null) {
                        typedDecoder = _decoder as IncrementalNewlineDecoder;
                        if (typedDecoder != null) {
                            typedDecoder.reset(context);
                        } else {
                            PythonOps.Invoke(context, _decoder, "reset");
                        }
                    }

                    GC.KeepAlive(this);
                    return pos;
                }

                if (whenceInt != 0) {
                    throw PythonOps.ValueError("invalid whence ({0}, should be 0, 1, or 2)", whenceInt);
                }
                if (cookie < 0) {
                    throw PythonOps.ValueError("negative seek position {0}", cookie);
                }
                flush(context);

                // seek() works by going back to a safe starting point and replaying read(skip)
                BigInteger startPos;
                int decodeFlags;
                int bytesFed;
                int skip;
                bool needEOF;
                UnpackCookie(cookie, out startPos, out decodeFlags, out bytesFed, out skip, out needEOF);

                // seek to safe starting point
                if (_bufferTyped != null) {
                    _bufferTyped.seek(context, startPos, 0);
                } else {
                    PythonOps.Invoke(context, _buffer, "seek", startPos, 0);
                }
                SetDecodedChars(string.Empty);
                _nextInput = null;

                // set decoder's state at starting point
                object decoder = _decoder;
                typedDecoder = decoder as IncrementalNewlineDecoder;
                if (cookie == BigInteger.Zero && decoder != null) {
                    if (typedDecoder != null) {
                        typedDecoder.reset(context);
                    } else {
                        PythonOps.Invoke(context, decoder, "reset");
                    }
                } else if (decoder != null || decodeFlags != 0 || skip != 0) {
                    if (_decoder == null) {
                        decoder = GetDecoder(context);
                        typedDecoder = decoder as IncrementalNewlineDecoder;
                    }

                    if (typedDecoder != null) {
                        typedDecoder.SetState(context, Bytes.Empty, decodeFlags);
                    } else {
                        PythonOps.Invoke(context, decoder, "setstate", PythonTuple.MakeTuple(Bytes.Empty, decodeFlags));
                    }
                    _decodeFlags = decodeFlags;
                    _nextInput = Bytes.Empty;
                }

                if (skip > 0) {
                    // similar to ReadChunk(); feed the decoder and save a snapshot
                    object chunkObj = _bufferTyped != null ?
                        _bufferTyped.read(context, bytesFed) :
                        PythonOps.Invoke(context, _buffer, "read", bytesFed);
                    Bytes chunk = chunkObj != null ? GetBytes(chunkObj, "read()") : Bytes.Empty;

                    if (typedDecoder != null) {
                        SetDecodedChars(typedDecoder.decode(context, chunk, needEOF));
                    } else {
                        SetDecodedChars((string)PythonOps.Invoke(context, decoder, "decode", chunk, needEOF));
                    }

                    // skip appropriate number of decoded chars
                    if (_decodedChars.Length < skip) {
                        throw PythonOps.IOError("can't restore logical file position");
                    }
                    _decodedCharsUsed = skip;
                }

                // reset the encoder for proper BOM handling
                try {
                    object encoder = _encoder ?? GetEncoder(context);
                    if (cookie == 0) {
                        PythonOps.Invoke(context, encoder, "reset");
                    } else {
                        PythonOps.Invoke(context, encoder, "setstate", 0);
                    }
                } catch (LookupException) {
                    // the encoder may not exist
                }

                GC.KeepAlive(this);
                return cookie;
            }

            public override object read(CodeContext/*!*/ context, object length=null) {
                _checkClosed();
                if (!readable(context)) {
                    throw UnsupportedOperationWithMessage(context, "not readable");
                }

                int size = GetInt(length, -1);

                object decoder = _decoder ?? GetDecoder(context);

                if (size < 0) {
                    string res = GetDecodedChars();

                    object next = _bufferTyped != null ?
                        _bufferTyped.read(context, -1) :
                        PythonOps.Invoke(context, _buffer, "read", -1);
                    object decodeFunc = PythonOps.GetBoundAttr(context, decoder, "decode");
                    string decoded = (string)PythonCalls.CallWithKeywordArgs(
                        context,
                        decodeFunc,
                        new object[] { next, true },
                        new string[] { "final" }
                    );
                    SetDecodedChars(string.Empty);
                    _nextInput = null;

                    if (res == null) {
                        res = decoded;
                    } else {
                        res += decoded;
                    }

                    return res;
                } else {
                    StringBuilder res = new StringBuilder(GetDecodedChars(size));

                    bool notEof = true;
                    while (res.Length < size && notEof) {
                        notEof = ReadChunk(context);
                        res.Append(GetDecodedChars(size - res.Length));
                    }

                    return res.ToString();
                }
            }

            public override object readline(CodeContext/*!*/ context, int limit=-1) {
                _checkClosed("read from closed file");
                if (!readable(context)) {
                    throw UnsupportedOperationWithMessage(context, "not readable");
                }

                string line = GetDecodedChars();

                int start = 0;
                if (_decoder == null) {
                    GetDecoder(context);
                }

                int pos, endPos;
                for (; ; ) {
                    if (_readTranslate) {
                        // Newlines have already been translated into "\n"
                        pos = line.IndexOf('\n', start);
                        if (pos >= 0) {
                            endPos = pos + 1;
                            break;
                        }
                        start = line.Length;
                    } else if (_readUniversal) {
                        // Search for any newline, "\r" and/or "\n". The decoder ensures that
                        // "\r\n" isn't split up.
                        int nlPos = line.IndexOfAny(new char[] { '\r', '\n' }, start);

                        if (nlPos == -1) {
                            // no newlines found
                            start = line.Length;
                        } else if (line[nlPos] == '\n') {
                            // "\n" newline found
                            endPos = nlPos + 1;
                            break;
                        } else if (line.Length > nlPos + 1 && line[nlPos + 1] == '\n') {
                            // "\r\n" newline found
                            endPos = nlPos + 2;
                            break;
                        } else {
                            // "\r" newline found
                            endPos = nlPos + 1;
                            break;
                        }
                    } else {
                        // Non-universal newlines
                        pos = line.IndexOf(_readNL, StringComparison.Ordinal);
                        if (pos >= 0) {
                            endPos = pos + _readNL.Length;
                            break;
                        }
                    }

                    if (limit >= 0 && line.Length >= limit) {
                        endPos = limit;
                        break;
                    }

                    while (ReadChunk(context) && string.IsNullOrEmpty(_decodedChars)) { }
                    if (!string.IsNullOrEmpty(_decodedChars)) {
                        line += GetDecodedChars();
                    } else {
                        // EOF
                        SetDecodedChars(string.Empty);
                        _nextInput = null;
                        return line;
                    }
                }

                if (limit >= 0 && endPos > limit) {
                    endPos = limit;
                }

                // rewind to just after the line ending
                RewindDecodedChars(line.Length - endPos);
                GC.KeepAlive(this);
                return line.Substring(0, endPos);
            }

            #endregion
            
            #region IEnumerator<object> Members

            private object _current;

            object IEnumerator<object>.Current {
                get { return _current; }
            }

            #endregion

            #region IEnumerator Members

            object IEnumerator.Current {
                get { return _current; }
            }

            bool IEnumerator.MoveNext() {
                _telling = false;
                _current = readline(context, -1);

                Bytes bytes;
                string str;
                bool res = _current != null && (
                    (bytes = _current as Bytes) != null && bytes.Count > 0 ||
                    (str = _current as string) != null && str.Length > 0 ||
                    PythonOps.IsTrue(_current)
                );

                if (!res) {
                    _nextInput = null;
                    _telling = _seekable;
                }
                return res;
            }

            void IEnumerator.Reset() {
                _current = null;
                seek(context, 0, 0);
            }

            #endregion

            #region IEnumerable<object> Members

            IEnumerator<object> IEnumerable<object>.GetEnumerator() {
                _checkClosed();
                return this;
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator() {
                _checkClosed();
                return this;
            }

            #endregion

            #region ICodeFormattable Members

#nullable enable
            public string __repr__(CodeContext/*!*/ context) {
                string name = string.Empty;
                if (PythonOps.TryGetBoundAttr(buffer, "name", out var nameObj))
                    name = $" name={PythonOps.Repr(context, nameObj)}";

                string mode = string.Empty;
                if (PythonOps.TryGetBoundAttr(this, "mode", out var modeObj))
                    mode = $" mode={PythonOps.Repr(context, modeObj)}";

                return $"<_io.TextIOWrapper{name}{mode} encoding='{_encoding}'>";
            }
#nullable restore

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<TextIOWrapper>(parameter, this);
            }

            #endregion

            #region Private implementation details

            private void UnpackCookie(BigInteger cookie, out BigInteger pos, out int decodeFlags, out int bytesFed, out int skip, out bool needEOF) {
                BigInteger mask = (BigInteger.One << 64) - 1;
                pos = cookie & mask;
                cookie >>= 64;
                decodeFlags = (int)(cookie & mask);
                cookie >>= 64;
                bytesFed = (int)(cookie & mask);
                cookie >>= 64;
                skip = (int)(cookie & mask);
                needEOF = cookie > mask;
            }

            private object GetEncoder(CodeContext/*!*/ context) {
                object lookup = PythonOps.LookupEncoding(context, _encoding);
                object factory;
                if (lookup == null || !PythonOps.TryGetBoundAttr(context, lookup, "incrementalencoder", out factory)) {
                    throw PythonOps.LookupError(_encoding);
                }

                _encoder = PythonOps.CallWithContext(context, factory, _errors);
                return _encoder;
            }

            private object GetDecoder(CodeContext/*!*/ context) {
                object lookup = PythonOps.LookupEncoding(context, _encoding);
                object factory;
                if (lookup == null || !PythonOps.TryGetBoundAttr(context, lookup, "incrementaldecoder", out factory)) {
                    throw PythonOps.LookupError(_encoding);
                }

                _decoder = PythonOps.CallWithContext(context, factory, _errors);
                if (_readUniversal) {
                    _decoder = new IncrementalNewlineDecoder(_decoder, _readTranslate, "strict");
                }
                return _decoder;
            }

            private void SetDecodedChars(string chars) {
                _decodedChars = chars;
                _decodedCharsUsed = 0;
            }

            private string GetDecodedChars() {
                Debug.Assert(_decodedCharsUsed <= _decodedChars.Length);

                string res = _decodedChars.Substring(_decodedCharsUsed);
                _decodedCharsUsed += res.Length;
                return res;
            }

            private string GetDecodedChars(int length) {
                Debug.Assert(_decodedCharsUsed <= _decodedChars.Length);

                length = Math.Min(length, _decodedChars.Length - _decodedCharsUsed);
                string res = _decodedChars.Substring(_decodedCharsUsed, length);
                _decodedCharsUsed += length;
                return res;
            }

            private void RewindDecodedChars(int length) {
                if (_decodedCharsUsed < length) {
                    throw PythonOps.AssertionError("rewind decoded_chars out of bounds");
                }
                _decodedCharsUsed -= length;
            }

            /// <summary>
            /// Read and decode the next chunk from the buffered reader. Returns true if EOF was
            /// not reached. Places decoded string in _decodedChars.
            /// </summary>
            private bool ReadChunk(CodeContext/*!*/ context) {
                if (_decoder == null) {
                    throw PythonOps.ValueError("no decoder");
                }

                IncrementalNewlineDecoder typedDecoder = _decoder as IncrementalNewlineDecoder;
                Bytes decodeBuffer = null;
                int decodeFlags = 0;
                if (_telling) {
                    // take a snapshot where the decoder's input buffer is empty
                    if (typedDecoder != null) {
                        typedDecoder.GetState(context, out decodeBuffer, out decodeFlags);
                    } else {
                        PythonTuple tuple = (PythonTuple)PythonOps.Invoke(context, _decoder, "getstate");
                        decodeBuffer = GetBytes(tuple[0], "getstate");
                        decodeFlags = (int)tuple[1];
                    }
                }

                object chunkObj;
                string callName;
                if (_bufferTyped != null) {
                    chunkObj = _bufferTyped.read1(context, _CHUNK_SIZE);
                    callName = "read1()";
                } else {
                    if (PythonOps.TryGetBoundAttr(_buffer, "read1", out object read1)) {
                        chunkObj = PythonCalls.Call(context, read1, _CHUNK_SIZE);
                        callName = "read1()";
                    } else {
                        chunkObj = PythonOps.Invoke(context, _buffer, "read", _CHUNK_SIZE);
                        callName = "read()";
                    }
                }

                Bytes chunk = chunkObj != null ? GetBytes(chunkObj, callName) : Bytes.Empty;
                bool eof = chunkObj == null || chunk.Count == 0;

                string decoded;
                if (typedDecoder != null) {
                    decoded = typedDecoder.decode(context, chunk, eof);
                } else {
                    decoded = (string)PythonOps.Invoke(context, _decoder, "decode", chunk, eof);
                }
                SetDecodedChars(decoded);

                if (_telling) {
                    _decodeFlags = decodeFlags;
                    _nextInput = decodeBuffer + chunk;
                }

                return !eof;
            }

            #endregion
        }

        public static _IOBase open(
            CodeContext/*!*/ context,
            object file,
            string mode="r",
            int buffering=-1,
            string encoding=null,
            string errors=null,
            string newline=null,
            bool closefd=true,
            object opener=null
        ) {
            string fname = null;
            if (!Converter.TryConvertToIndex(file, out int fd, false, false)) {
                fd = -1;
                fname = PythonOps.FsPathDecoded(context, file);
            }

            HashSet<char> modes = MakeSet(mode);
            if (modes.Count < mode.Length || !_validModes.IsSupersetOf(modes)) {
                throw PythonOps.ValueError("invalid mode: '{0}'", mode);
            }

            bool reading = modes.Contains('r');
            bool writing = modes.Contains('w');
            bool appending = modes.Contains('a');
            bool updating = modes.Contains('+');
            bool text = modes.Contains('t');
            bool binary = modes.Contains('b');
            bool creating = modes.Contains('x');
            if (modes.Contains('U')) {
                if (creating || writing || appending || updating) {
                    // error message from Python 3.6
                    throw PythonOps.ValueError("mode U cannot be combined with 'x', 'w', 'a', or '+'");
                }
                PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "'U' mode is deprecated");
                reading = true;
            }
            if (text && binary) {
                throw PythonOps.ValueError("can't have text and binary mode at once");
            }
            if (Convert.ToInt32(creating) + Convert.ToInt32(reading) + Convert.ToInt32(writing) + Convert.ToInt32(appending) > 1) {
                throw PythonOps.ValueError("must have exactly one of create/read/write/append mode");
            }
            if (binary && encoding != null) {
                throw PythonOps.ValueError("binary mode doesn't take an encoding argument");
            }
            if (binary && newline != null) {
                throw PythonOps.ValueError("binary mode doesn't take a newline argument");
            }

            mode = reading ? "r" : "";
            if (creating)
                mode += "x";
            if (writing)
                mode += "w";
            if (appending)
                mode += "a";
            if (updating)
                mode += '+';

            if (buffering == 0 && !binary) throw PythonOps.ValueError("can't have unbuffered text I/O");

            FileIO fio = fname != null
                    ? new FileIO(context, fname, mode, closefd, opener)
                    : new FileIO(context, fd, mode, closefd, opener);

            bool line_buffering = false;
            if (buffering == 1 || buffering < 0 && fio.isatty(context)) {
                buffering = -1;
                line_buffering = true;
            }
            if (buffering < 0) {
                buffering = DEFAULT_BUFFER_SIZE;
            }
            if (buffering == 0) {
                Debug.Assert(binary);
                return fio;
            }

            _BufferedIOBase buffer;
            if (updating) {
                buffer = BufferedRandom.Create(context, fio, buffering, null);
            } else if (writing || appending || creating) {
                buffer = BufferedWriter.Create(context, fio, buffering, null);
            } else if (reading) {
                buffer = BufferedReader.Create(context, fio, buffering);
            } else {
                throw PythonOps.ValueError("unknown mode: {0}", mode);
            }

            if (binary) {
                return buffer;
            }
            TextIOWrapper res = TextIOWrapper.Create(context, buffer, encoding, errors, newline, line_buffering);
            ((IPythonExpandable)res).EnsureCustomAttributes()["mode"] = mode;
            return res;
        }

        internal static TextIOWrapper CreateConsole(PythonContext context, SharedIO io, ConsoleStreamType type, string name, out FileIO fio) {
            var cc = context.SharedContext;
            if (type == ConsoleStreamType.Input) {
                var encoding = StringOps.GetEncodingName(io.InputEncoding);
                fio = new FileIO(cc, io.GetStreamProxy(type), type) { name = name };
                var buffer = BufferedReader.Create(cc, fio, DEFAULT_BUFFER_SIZE);
                return TextIOWrapper.Create(cc, buffer, encoding, null, null, true);
            }
            else if (type == ConsoleStreamType.Output) {
                var encoding = StringOps.GetEncodingName(io.OutputEncoding);
                fio = new FileIO(cc, io.GetStreamProxy(type), type) { name = name };
                var buffer = BufferedWriter.Create(cc, fio, DEFAULT_BUFFER_SIZE, null);
                return TextIOWrapper.Create(cc, buffer, encoding, null, null, true);
            }
            else {
                Debug.Assert(type == ConsoleStreamType.ErrorOutput);
                var encoding = StringOps.GetEncodingName(io.ErrorEncoding);
                fio = new FileIO(cc, io.GetStreamProxy(type), type) { name = name };
                var buffer = BufferedWriter.Create(cc, fio, DEFAULT_BUFFER_SIZE, null);
                return TextIOWrapper.Create(cc, buffer, encoding, "backslashreplace", null, true);
            }
        }

        [PythonType]
        public class IncrementalNewlineDecoder {
            [Flags]
            internal enum LineEnding {
                None = 0,
                CR = 1,
                LF = 2,
                CRLF = 4,
                All = CR | LF | CRLF
            }

            private readonly object _decoder;
            private bool _translate;
            private LineEnding _seenNL;
            private bool _pendingCR;

#pragma warning disable 414 // TODO: unused field
            private string _errors;
#pragma warning restore 414

            public IncrementalNewlineDecoder(object decoder, bool translate, string errors="strict") {
                _decoder = decoder;
                _translate = translate;
                _errors = errors;
            }

            public string decode(CodeContext/*!*/ context, [NotNone] IList<byte> input, bool final=false) {
                object output;
                if (_decoder == null) {
                    output = input.MakeString();
                } else {
                    output = PythonCalls.CallWithKeywordArgs(
                        context,
                        PythonOps.GetBoundAttr(context, _decoder, "decode"),
                        new object[] { input, final },
                        new string[] { "final" }
                    );
                }

                if (!(output is string decoded)) {
                    if (output is Extensible<string>) {
                        decoded = ((Extensible<string>)output).Value;
                    } else {
                        throw PythonOps.TypeError("decoder produced {0}, expected str", PythonOps.GetPythonTypeName(output));
                    }
                }

                return DecodeWorker(context, decoded, final);
            }

            public string decode(CodeContext/*!*/ context, [NotNone] string input, bool final=false) {
                if (_decoder == null) {
                    return DecodeWorker(context, input, final);
                }

                return decode(context, Bytes.Make(input.MakeByteArray()), final);
            }

            private string DecodeWorker(CodeContext/*!*/ context, string decoded, bool final) {
                if (_pendingCR && (final || decoded.Length > 0)) {
                    decoded = "\r" + decoded;
                    _pendingCR = false;
                }

                if (decoded.Length == 0) {
                    return decoded;
                }

                // retain last "\r" to avoid splitting "\r\n"
                if (!final && decoded.Length > 0 && decoded[decoded.Length - 1] == '\r') {
                    decoded = decoded.Substring(0, decoded.Length - 1);
                    _pendingCR = true;
                }

                if (_translate || _seenNL != LineEnding.All) {
                    int crlf = decoded.count("\r\n");
                    int cr = decoded.count("\r") - crlf;

                    if (_seenNL != LineEnding.All) {
                        int lf = decoded.count("\n") - crlf;
                        _seenNL |=
                            (crlf > 0 ? LineEnding.CRLF : LineEnding.None) |
                            (lf > 0 ? LineEnding.LF : LineEnding.None) |
                            (cr > 0 ? LineEnding.CR : LineEnding.None);
                    }

                    if (_translate) {
                        if (crlf > 0) {
                            decoded = decoded.Replace("\r\n", "\n");
                        }
                        if (cr > 0) {
                            decoded = decoded.Replace('\r', '\n');
                        }
                    }
                }

                return decoded;
            }

            public PythonTuple getstate(CodeContext/*!*/ context) {
                object buf = Bytes.Empty;
                int flags = 0;
                if (_decoder != null) {
                    PythonTuple state = (PythonTuple)PythonOps.Invoke(context, _decoder, "getstate");
                    buf = state[0];
                    flags = Converter.ConvertToInt32(state[1]) << 1;
                }
                if (_pendingCR) {
                    flags |= 1;
                }

                return PythonTuple.MakeTuple(buf, flags);
            }

            internal void GetState(CodeContext/*!*/ context, out Bytes buf, out int flags) {
                PythonTuple state = (PythonTuple)PythonOps.Invoke(context, _decoder, "getstate");

                buf = GetBytes(state[0], "getstate");
                flags = Converter.ConvertToInt32(state[1]) << 1;
                if (_pendingCR) {
                    flags |= 1;
                }
            }

            public void setstate(CodeContext/*!*/ context, [NotNone] PythonTuple state) {
                object buf = state[0];
                int flags = Converter.ConvertToInt32(state[1]);

                _pendingCR = (flags & 1) != 0;
                if (_decoder != null) {
                    PythonOps.Invoke(context, _decoder, "setstate", PythonTuple.MakeTuple(buf, flags >> 1));
                }
            }

            internal void SetState(CodeContext/*!*/ context, Bytes buffer, int flags) {
                _pendingCR = (flags & 1) != 0;
                if (_decoder != null) {
                    PythonOps.Invoke(context, _decoder, "setstate", PythonTuple.MakeTuple(buffer, flags >> 1));
                }
            }

            public void reset(CodeContext/*!*/ context) {
                _seenNL = LineEnding.None;
                _pendingCR = false;
                if (_decoder != null) {
                    PythonOps.Invoke(context, _decoder, "reset");
                }
            }

            internal static object GetNewlines(LineEnding _seenNL) {
                return _seenNL switch {
                    LineEnding.None => null,
                    LineEnding.CR => "\r",
                    LineEnding.LF => "\n",
                    LineEnding.CRLF => "\r\n",
                    LineEnding.CR | LineEnding.LF => PythonTuple.MakeTuple("\r", "\n"),
                    LineEnding.CR | LineEnding.CRLF => PythonTuple.MakeTuple("\r", "\r\n"),
                    LineEnding.LF | LineEnding.CRLF => PythonTuple.MakeTuple("\n", "\r\n"),
                    _ => PythonTuple.MakeTuple("\r", "\n", "\r\n"), // LineEnding.All
                };
            }

            public object newlines => GetNewlines(_seenNL);
        }

        public static PythonType BlockingIOError {
            get { return PythonExceptions.BlockingIOError; }
        }

        #region Private implementation details

        private static readonly HashSet<char> _validModes = MakeSet("abrtwxU+");

        private static HashSet<char> MakeSet(string chars) {
            HashSet<char> res = new HashSet<char>();
            for (int i = 0; i < chars.Length; i++) {
                res.Add(chars[i]);
            }
            return res;
        }

        private static BigInteger GetBigInt(object i, string msg) {
            BigInteger res;
            if (TryGetBigInt(i, out res)) {
                return res;
            }

            throw PythonOps.TypeError(msg);
        }

        private static bool TryGetBigInt(object i, out BigInteger res) {
            if (i is BigInteger bi) {
                res = bi;
                return true;
            }

            if (i is int i32) {
                res = i32;
                return true;
            }

            if (i is long i64) {
                res = i64;
                return true;
            }

            if (i is Extensible<BigInteger> ebi) {
                res = ebi.Value;
                return true;
            }

            res = BigInteger.Zero;
            return false;
        }

        private static int GetInt(object i) {
            return GetInt(i, null, null);
        }

        private static int GetInt(object i, int defaultValue) {
            return GetInt(i, defaultValue, null, null);
        }

        private static int GetInt(object i, string msg, params object[] args) {
            if (i == Missing.Value) return 0;

            int res;
            if (TryGetInt(i, out res)) {
                return res;
            }

            if (msg == null) {
                throw PythonOps.TypeError("integer argument expected, got '{0}'", PythonOps.GetPythonTypeName(i));
            }
            
            throw PythonOps.TypeError(msg, args);
        }

        private static int GetInt(object i, int defaultValue, string msg, params object[] args) {
            if (i == null) {
                return defaultValue;
            }

            return GetInt(i, msg, args);
        }

        private static bool TryGetInt(object i, out int value) {
            if (i == null) {
                value = int.MinValue;
                return false;
            } else if (i is int i32) {
                value = i32;
                return true;
            } else if (i is BigInteger bi) {
                return bi.AsInt32(out value);
            }

            if (i is Extensible<BigInteger> ebi) {
                return ebi.Value.AsInt32(out value);
            }

            value = int.MinValue;
            return false;
        }

        /// <summary>
        /// Convert string or bytes into bytes
        /// </summary>
        private static Bytes GetBytes(object o, string name) {
            if(o == null)
                return null;

            if (o is Bytes bytes) {
                return bytes;
            }

            string s = o as string;
            if (s == null) {
                if (o is Extensible<string> es) {
                    s = es.Value;
                }
            }

            if (s != null) {
                return Bytes.Make(s.MakeByteArray());
            }

            throw PythonOps.TypeError("'" + name + "' should have returned bytes");
        }

        #endregion
    }
}
