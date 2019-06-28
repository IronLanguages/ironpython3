﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using System.Collections.Generic;
using System.Threading;

namespace IronPython.Runtime {
    [PythonType("memoryview")]
    public sealed class MemoryView : ICodeFormattable, IWeakReferenceable {
        private const int MaximumDimensions = 64;

        private IBufferProtocol _buffer;
        private readonly int _start;
        private readonly int? _end;
        private readonly int _step;
        private readonly string _format;
        private readonly PythonTuple _shape;
        private readonly int _itemsize;

        private int? _storedHash;
        private WeakRefTracker _tracker;

        // Variable to determine whether this memoryview is aligned
        // with and has the same type as the underlying buffer. This
        // allows us to fast-path by getting the specific item instead
        // of having to convert to and from bytes.
        private readonly bool _matchesBuffer;

        public MemoryView(IBufferProtocol @object) {
            _buffer = @object;
            _step = 1;
            _format = _buffer.Format;
            _itemsize = (int)_buffer.ItemSize;
            _matchesBuffer = true;

            var shape = _buffer.GetShape(_start, _end);
            if (shape == null) {
                _shape = null;
            }
            _shape = new PythonTuple(shape);
        }

        public MemoryView(MemoryView @object) :
            this(@object._buffer, @object._start, @object._end, @object._step, @object._format, @object._shape) { }

        internal MemoryView(IBufferProtocol @object, int start, int? end, int step, string format, PythonTuple shape) {
            _buffer = @object;
            _format = format;
            _shape = shape;
            _start = start;
            _end = end;
            _step = step;

            if (!TypecodeOps.TryGetTypecodeWidth(format, out _itemsize)) {
                _itemsize = (int) _buffer.ItemSize;
            }

            _matchesBuffer = _format == _buffer.Format && _start % itemsize == 0;
        }

        private void CheckBuffer() {
            if (_buffer == null) throw PythonOps.ValueError("operation forbidden on released memoryview object");
        }

        private int numberOfElements() {
            if (_end != null) {
                return (_end.Value - _start) / (_itemsize * _step);
            }
            return _buffer.ItemCount * (int)_buffer.ItemSize / (_itemsize * _step);
        }

        public int __len__() {
            CheckBuffer();
            return Converter.ConvertToInt32(shape[0]);
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
            CheckBuffer();
            return this;
        }

        public void __exit__(CodeContext/*!*/ context, params object[] excinfo) {
            release(context);
        }

        public string format {
            get {
                CheckBuffer();
                return _format ?? _buffer.Format;
            }
        }

        public BigInteger itemsize {
            get {
                CheckBuffer();
                return _itemsize;
            }
        }

        public BigInteger ndim {
            get {
                CheckBuffer();
                return (_shape?.__len__()) ?? _buffer.NumberDimensions;
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
                return _shape;
            }
        }

        public PythonTuple strides {
            get {
                CheckBuffer();
                return _buffer.Strides;
            }
        }

        public PythonTuple suboffsets {
            get {
                CheckBuffer();
                return _buffer.SubOffsets ?? PythonTuple.EMPTY;
            }
        }

        public Bytes tobytes() {
            CheckBuffer();
            if (_matchesBuffer && _step == 1) {
                return _buffer.ToBytes(_start / _itemsize, _end / _itemsize);
            }

            byte[] bytes = getByteRange(_start, numberOfElements() * _itemsize);

            if (_step == 1) {
                return Bytes.Make(bytes);
            }

            // getByteRange() doesn't care about our _step, so if we have one
            // that isn't 1, we will need to get rid of any bytes we don't care
            // about and potentially adjust for a reversed memoryview.
            byte[] stridedBytes = new byte[bytes.Length / _itemsize];
            for (int indexStrided = 0; indexStrided < stridedBytes.Length; indexStrided += _itemsize) {

                int indexInBytes = indexStrided * _step;

                for (int j = 0; j < _itemsize; j++) {
                    stridedBytes[indexStrided + j] = bytes[indexInBytes + j];
                }
            }

            return Bytes.Make(stridedBytes);
        }

        public PythonList tolist() {
            CheckBuffer();

            if (_matchesBuffer && _step == 1) {
                return _buffer.ToList(_start / _itemsize, _end / _itemsize);
            }

            int length = numberOfElements();
            object[] elements = new object[length];

            for (int i = 0; i < length; i++) {
                elements[i] = getAtFlatIndex(i);
            }

            return PythonList.FromArrayNoCopy(elements);
        }

        public MemoryView cast(object format) {
            return cast(format, null);
        }

        public MemoryView cast(object format, [NotNull]object shape) {
            if (!(format is string formatAsString)) {
                throw PythonOps.TypeError("memoryview: format argument must be a string");
            }

            if (_step != 1) {
                throw PythonOps.TypeError("memoryview: casts are restricted to C-contiguous views");
            }

            if ((shape != null || ndim != 0) && this.shape.Contains(0)) {
                throw PythonOps.TypeError("memoryview: cannot cast view with zeros in shape or strides");
            }

            PythonTuple shapeAsTuple = null;

            if (shape != null) {
                if (!(shape is PythonList) && !(shape is PythonTuple)) {
                    throw PythonOps.TypeError("shape must be a list or a tuple");
                }

                shapeAsTuple = PythonOps.MakeTupleFromSequence(shape);
                int newNDim = shapeAsTuple.Count;

                if (newNDim > MaximumDimensions) {
                    throw PythonOps.TypeError("memoryview: number of dimensions must not exceed {0}", MaximumDimensions);
                }

                if (ndim != 1 && newNDim != 1) {
                    throw PythonOps.TypeError("memoryview: cast must be 1D -> ND or ND -> 1D");
                }
            }

            int newItemsize;
            if (!TypecodeOps.TryGetTypecodeWidth(formatAsString, out newItemsize)) {
                throw PythonOps.ValueError(
                    "memoryview: destination format must be a native single character format prefixed with an optional '@'");
            }

            bool thisIsBytes = this.format == "B" || this.format == "b" || this.format == "c";
            bool otherIsBytes = formatAsString == "B" || formatAsString == "b" || formatAsString == "c";

            if (!thisIsBytes && !otherIsBytes) {
                throw PythonOps.TypeError("memoryview: cannot cast between two non-byte formats");
            }

            int length = numberOfElements();

            if (length % newItemsize != 0) {
                throw PythonOps.TypeError("memoryview: length is not a multiple of itemsize");
            }

            int newLength = length * _itemsize / newItemsize;
            if (shapeAsTuple != null) {
                int lengthGivenShape = 1;
                for (int i = 0; i < shapeAsTuple.Count; i++) {
                    lengthGivenShape *= Converter.ConvertToInt32(shapeAsTuple[i]);
                }

                if (lengthGivenShape != newLength) {
                    throw PythonOps.TypeError("memoryview: product(shape) * itemsize != buffer size");
                }
            }

            return new MemoryView(_buffer, _start, _end, _step, formatAsString, shapeAsTuple ?? PythonOps.MakeTuple(newLength));
        }

        private byte[] unpackBytes(string format, object o) {
            if (TypecodeOps.TryGetBytes(format, o, out byte[] bytes)) {
                return bytes;
            } else if (o is Bytes b) {
                return b._bytes; // CData returns a bytes object for its type
            } else {
                throw PythonOps.NotImplementedError("No conversion for type {0} to byte array", PythonOps.GetPythonTypeName(o));
            }
        }

        private object packBytes(string format, byte[] bytes, int offset, int itemsize) {
            if (TypecodeOps.TryGetFromBytes(format, bytes, offset, out object result))
                return result;
            else {
                byte[] obj = new byte[itemsize];
                for (int i = 0; i < obj.Length; i++) {
                    obj[i] = bytes[offset + i];
                }
                return new Bytes(obj);
            }
        }

        private void setByteRange(int startByte, byte[] toWrite) {
            string bufferTypeCode = _buffer.Format;
            int bufferItemSize = (int)_buffer.ItemSize;

            // Because memoryviews can be cast to bytes, sliced, and then
            // cast to a different format, we have no guarantee of being aligned
            // with the underlying buffer.
            int startAlignmentOffset = startByte % bufferItemSize;
            int endAlignmentOffset = (startByte + toWrite.Length) % bufferItemSize;

            int indexInBuffer = startByte / bufferItemSize;

            // Special case: when the bytes we set fall within the boundary
            // of a single item, we have to worry about both the start and
            // end offsets
            if (startAlignmentOffset + toWrite.Length < bufferItemSize) {
                byte[] existingBytes = unpackBytes(bufferTypeCode, _buffer.GetItem(indexInBuffer));

                for (int i = 0; i < toWrite.Length; i++) {
                    existingBytes[i + startAlignmentOffset] = toWrite[i];
                }

                _buffer.SetItem(indexInBuffer, packBytes(bufferTypeCode, existingBytes, 0, bufferItemSize));
                return;
            }

            // If we aren't aligned at the start, we have to preserve the first x bytes as
            // they already are in the buffer, and overwrite the last (size - x) bytes
            if (startAlignmentOffset != 0) {
                byte[] existingBytes = unpackBytes(bufferTypeCode, _buffer.GetItem(indexInBuffer));

                for (int i = startAlignmentOffset; i < existingBytes.Length; i++) {
                    existingBytes[i] = toWrite[i - startAlignmentOffset];
                }

                _buffer.SetItem(indexInBuffer, packBytes(bufferTypeCode, existingBytes, 0, bufferItemSize));
                indexInBuffer++;
            }

            for (int i = startAlignmentOffset; i + bufferItemSize <= toWrite.Length; i += bufferItemSize, indexInBuffer++) {
                _buffer.SetItem(indexInBuffer, packBytes(bufferTypeCode, toWrite, i, bufferItemSize));
            }

            // Likewise at the end, we may have to overwrite the first x bytes, but
            // preserve the last (size - x) bytes
            if (endAlignmentOffset != 0) {
                byte[] existingBytes = unpackBytes(bufferTypeCode, _buffer.GetItem(indexInBuffer));

                for (int i = 0; i < endAlignmentOffset; i++) {
                    existingBytes[i] = toWrite[toWrite.Length - startAlignmentOffset + i];
                }
                _buffer.SetItem(indexInBuffer, packBytes(bufferTypeCode, existingBytes, 0, bufferItemSize));
            }
        }

        private byte[] getByteRange(int startByte, int length) {
            string bufferTypeCode = _buffer.Format;
            int bufferItemsize = (int)_buffer.ItemSize;

            byte[] bytes = new byte[length];
            int startAlignmentOffset = startByte % bufferItemsize;
            int indexInBuffer = startByte / bufferItemsize;

            for (int i = -startAlignmentOffset; i < length; i += bufferItemsize, indexInBuffer++) {
                byte[] currentBytes = unpackBytes(bufferTypeCode, _buffer.GetItem(indexInBuffer));

                for (int j = 0; j < currentBytes.Length; j++) {
                    // Because we don't have a guarantee that we are aligned with the
                    // the buffer's data, we may potentially read extra bits to the left
                    // and write of what we want, so we must ignore these bytes.
                    if (j + i < 0) {
                        continue;
                    }

                    if (j + i >= bytes.Length) {
                        continue;
                    }

                    bytes[i + j] = currentBytes[j];
                }
            }

            return bytes;
        }

        /// <summary>
        /// Treats the memoryview as if it were a flattened array, instead
        /// of having multiple dimensions. So, for a memoryview with the shape
        /// (2,2,2), retrieving at index 6 would be equivalent to getting at the
        /// index (1,1,0).
        /// </summary>
        private object getAtFlatIndex(int index) {
            if (_matchesBuffer) {
                return _buffer.GetItem((_start / _itemsize) + (index * _step));
            }

            int firstByteIndex = _start + index * _itemsize * _step;
            object result = packBytes(format, getByteRange(firstByteIndex, _itemsize), 0, (int)_buffer.ItemSize);

            return PythonOps.ConvertToPythonPrimitive(result);
        }

        private void setAtFlatIndex(int index, object value) {
            switch (format) {
                case "d": // double
                case "f": // float
                    double convertedValueDouble = 0;
                    if (!Converter.TryConvertToDouble(value, out convertedValueDouble)) {
                        throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", format);
                    }
                    value = convertedValueDouble;
                    break;

                case "c": // char
                case "b": // signed byte
                case "B": // unsigned byte
                case "u": // unicode char
                case "h": // signed short
                case "H": // unsigned short
                case "i": // signed int
                case "I": // unsigned int
                case "l": // signed long
                case "L": // unsigned long
                case "q": // signed long long
                case "P": // pointer
                case "Q": // unsigned long long
                    if (!PythonOps.IsNumericObject(value)) {
                        throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", format);
                    }

                    if (TypecodeOps.CausesOverflow(value, format)) {
                        throw PythonOps.ValueError("memoryview: invalid value for format '{0}'", format);
                    }

                    if (format == "Q") {
                        value = Converter.ConvertToUInt64(value);
                    } else {
                        value = Converter.ConvertToInt64(value);
                    }
                    break;
                default:
                    break; // This could be a variety of types, let the _buffer decide
            }

            if (_matchesBuffer) {
                _buffer.SetItem((_start / _itemsize) + (index * _step), value);
                return;
            }

            int firstByteIndex = _start + index * _itemsize * _step;
            setByteRange(firstByteIndex, unpackBytes(format, value));
        }

        public object this[int index] {
            get {
                CheckBuffer();
                index = PythonOps.FixIndex(index, __len__());
                if (ndim > 1) {
                    throw PythonOps.NotImplementedError("multi-dimensional sub-views are not implemented");
                }

                return getAtFlatIndex(index);
            }
            set {
                CheckBuffer();
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }
                index = PythonOps.FixIndex(index, __len__());
                if (ndim > 1) {
                    throw PythonOps.NotImplementedError("multi-dimensional sub-views are not implemented");
                }

                setAtFlatIndex(index, value);
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
                int start, stop, step;
                FixSlice(slice, __len__(), out start, out stop, out step);

                int newStart = _start + start * _itemsize;
                int newEnd = _start + stop * _itemsize;
                int newStep = _step * step;

                List<int> dimensions = new List<int>();

                // When a multidimensional memoryview is sliced, the slice
                // applies to only the first dimension. Therefore, other
                // dimensions are inherited.
                dimensions.Add((stop - start) / step);

                for (int i = 1; i < shape.__len__(); i++) {
                    dimensions.Add(Converter.ConvertToInt32(shape[i]));
                }

                PythonTuple newShape = PythonOps.MakeTupleFromSequence(dimensions);

                return new MemoryView(_buffer, newStart, newEnd, newStep, format, newShape);
            }
            set {
                CheckBuffer();
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }

                int start, stop, step;
                FixSlice(slice, __len__(), out start, out stop, out step);

                slice = new Slice(start, stop, step);

                int newLen = PythonOps.Length(value);
                if (stop - start != newLen) {
                    throw PythonOps.ValueError("cannot resize memory view");
                }

                slice.DoSliceAssign(SliceAssign, __len__(), value);
            }
        }

        private void SliceAssign(int index, object value) {
            setAtFlatIndex(index, value);
        }

        /// <summary>
        /// MemoryView slicing is somewhat different and more restricted than
        /// standard slicing.
        /// </summary>
        private static void FixSlice(Slice slice, int len, out int start, out int stop, out int step) {
            slice.indices(len, out start, out stop, out step);

            if (stop < start && step >= 0) {
                // wrapped iteration is interpreted as empty slice
                stop = start;
            }
        }

        public int __hash__(CodeContext context) {
            if (_storedHash != null) {
                return _storedHash.Value;
            }

            if (!@readonly) {
                throw PythonOps.ValueError("cannot hash writable memoryview object");
            }

            if (format != "B" && format != "b" && format != "c") {
                throw PythonOps.ValueError("memoryview: hashing is restricted to formats 'B', 'b' or 'c'");
            }

            _storedHash = tobytes().GetHashCode();
            return _storedHash.Value;
        }

        public bool __eq__(CodeContext/*!*/ context, [NotNull]MemoryView value) {
            if (_buffer == null) {
                return value._buffer == null;
            }
            return tobytes().Equals(value.tobytes());
        }

        public bool __eq__(CodeContext/*!*/ context, [NotNull]IBufferProtocol value) => __eq__(context, new MemoryView(value));

        [return: MaybeNotImplemented]
        public object __eq__(CodeContext/*!*/ context, object value) => NotImplementedType.Value;


        public bool __ne__(CodeContext/*!*/ context, [NotNull]MemoryView value) => !__eq__(context, value);

        public bool __ne__(CodeContext/*!*/ context, [NotNull]IBufferProtocol value) => !__eq__(context, value);

        [return: MaybeNotImplemented]
        public object __ne__(CodeContext/*!*/ context, object value) => NotImplementedType.Value;

        public bool __lt__(CodeContext/*!*/ context, object value) {
            throw PythonOps.TypeError("'<' not supported between instances of '{0}' and '{1}'",
                                      PythonOps.GetPythonTypeName(this), PythonOps.GetPythonTypeName(value));
        }

        public bool __le__(CodeContext/*!*/ context, object value) {
            throw PythonOps.TypeError("'<=' not supported between instances of '{0}' and '{1}'",
                                      PythonOps.GetPythonTypeName(this), PythonOps.GetPythonTypeName(value));
        }

        public bool __gt__(CodeContext/*!*/ context, object value) {
            throw PythonOps.TypeError("'>' not supported between instances of '{0}' and '{1}'",
                                      PythonOps.GetPythonTypeName(this), PythonOps.GetPythonTypeName(value));
        }

        public bool __ge__(CodeContext/*!*/ context, object value) {
            throw PythonOps.TypeError("'>=' not supported between instances of '{0}' and '{1}'",
                                      PythonOps.GetPythonTypeName(this), PythonOps.GetPythonTypeName(value));
        }

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            if (_buffer == null) {
                return String.Format("<released memory at {0}>", PythonOps.Id(this));
            }
            return String.Format("<memory at {0}>", PythonOps.Id(this));
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            return Interlocked.CompareExchange(ref _tracker, value, null) == null;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion
    }
}
