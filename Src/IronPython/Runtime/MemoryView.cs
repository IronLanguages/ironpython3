// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [PythonType("memoryview")]
    public sealed class MemoryView : ICodeFormattable {
        private IBufferProtocol _buffer;
        private readonly int _start;
        private readonly int? _end;

        public MemoryView(IBufferProtocol obj) {
            _buffer = obj;
        }

        internal MemoryView(IBufferProtocol obj, int start, int? end) : this(obj) {
            _start = start;
            _end = end;
        }

        public MemoryView(MemoryView obj) : this(obj._buffer, obj._start, obj._end) { }

        private void CheckBuffer() {
            if (_buffer == null) throw PythonOps.ValueError("operation forbidden on released memoryview object");
        }

        public int __len__() {
            CheckBuffer();
            if (_end != null) {
                return _end.Value - _start;
            }
            return _buffer.ItemCount;
        }

        public object obj {
            get {
                CheckBuffer();
                return _buffer;
            }
        }

        public void release(CodeContext /*!*/ context) {
            _buffer = null;
        }

        public object __enter__() {
            return this;
        }

        public void __exit__(CodeContext/*!*/ context, params object[] excinfo) {
            release(context);
        }

        public string format {
            get {
                CheckBuffer();
                return _buffer.Format;
            }
        }

        public BigInteger itemsize {
            get {
                CheckBuffer();
                return _buffer.ItemSize;
            }
        }

        public BigInteger ndim {
            get {
                CheckBuffer();
                return _buffer.NumberDimensions;
            }
        }

        public bool @readonly {
            get {
                CheckBuffer();
                return _buffer.ReadOnly;
            }
        }

        public PythonTuple shape {
            get {
                CheckBuffer();
                var shape = _buffer.GetShape(_start, _end);
                if (shape == null) {
                    return null;
                }
                return new PythonTuple(shape); 
            }
        }

        public PythonTuple strides {
            get {
                CheckBuffer();
                return _buffer.Strides;
            }
        }

        public object suboffsets {
            get {
                CheckBuffer();
                return _buffer.SubOffsets;
            }
        }

        public Bytes tobytes() {
            CheckBuffer();
            return _buffer.ToBytes(_start, _end);
        }

        public PythonList tolist() {
            CheckBuffer();
            return _buffer.ToList(_start, _end);
        }

        public object this[int index] {
            get {
                CheckBuffer();
                index = PythonOps.FixIndex(index, __len__());
                return _buffer.GetItem(index + _start);
            }
            set {
                CheckBuffer();
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }
                index = PythonOps.FixIndex(index, __len__());
                _buffer.SetItem(index + _start, value);
            }
        }

        public void __delitem__(int index) {
            CheckBuffer();
            if (_buffer.ReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public void __delitem__([NotNull]Slice slice) {
            CheckBuffer();
            if (_buffer.ReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public object this[[NotNull]Slice slice] {
            get {
                CheckBuffer();
                int start, stop;
                FixSlice(slice, __len__(), out start, out stop);

                return new MemoryView(_buffer, _start + start, _start + stop);
            }
            set {
                CheckBuffer();
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }

                int start, stop;
                FixSlice(slice, __len__(), out start, out stop);

                int newLen = PythonOps.Length(value);
                if (stop - start != newLen) {
                    throw PythonOps.ValueError("cannot resize memory view");
                }

                _buffer.SetSlice(new Slice(_start + start, _start + stop), value);
            }
        }

        /// <summary>
        /// MemoryView slicing is somewhat different and more restricted than
        /// standard slicing.
        /// </summary>
        private static void FixSlice(Slice slice, int len, out int start, out int stop) {
            if (slice.step != null) {
                throw PythonOps.NotImplementedError("");
            }

            slice.indices(len, out start, out stop, out _);

            if (stop < start) {
                // backwards iteration is interpreted as empty slice
                stop = start;
            }
        }

        public const object __hash__ = null;

        public bool __eq__(CodeContext/*!*/ context, [NotNull]MemoryView value) => tobytes().Equals(value.tobytes());

        public bool __eq__(CodeContext/*!*/ context, [NotNull]IBufferProtocol value) => __eq__(context, new MemoryView(value));

        [return: MaybeNotImplemented]
        public object __eq__(CodeContext/*!*/ context, object value) => NotImplementedType.Value;


        public bool __ne__(CodeContext/*!*/ context, [NotNull]MemoryView value) => !__eq__(context, value);

        public bool __ne__(CodeContext/*!*/ context, [NotNull]IBufferProtocol value) => !__eq__(context, value);

        [return: MaybeNotImplemented]
        public object __ne__(CodeContext/*!*/ context, object value) => NotImplementedType.Value;

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            return String.Format("<memory at {0}>", PythonOps.Id(this));
        }

        #endregion
    }
}
