// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace IronPython.Runtime {
    [PythonType("memoryview")]
    public sealed class MemoryView : ICodeFormattable, IWeakReferenceable, IBufferProtocol {
        private const int MaximumDimensions = 64;

        private IBufferProtocol _buffer;
        private readonly int _start;
        private readonly int? _end;
        private readonly int _step;
        private readonly string _format;
        private readonly PythonTuple _shape;
        private readonly bool _isReadOnly;
        private readonly int _itemsize;

        private int? _storedHash;
        private WeakRefTracker _tracker;

        // Variable to determine whether this memoryview is aligned
        // with and has the same type as the underlying buffer. This
        // allows us to fast-path by getting the specific item instead
        // of having to convert to and from bytes.
        private readonly bool _matchesBuffer;

        public MemoryView([NotNull]IBufferProtocol @object) {
            _buffer = @object;
            _step = 1;
            _format = _buffer.Format;
            _isReadOnly = _buffer.ReadOnly;
            _itemsize = (int)_buffer.ItemSize;
            _matchesBuffer = true;

            var shape = _buffer.GetShape(_start, _end);
            if (shape != null) {
                _shape = new PythonTuple(shape);
            } else {
                _shape = PythonTuple.MakeTuple(_buffer.ItemCount);
            }
        }

        public MemoryView([NotNull]MemoryView @object) :
            this(@object._buffer, @object._start, @object._end, @object._step, @object._format, @object._shape, @object._isReadOnly) { }

        internal MemoryView(IBufferProtocol @object, int start, int? end, int step, string format, PythonTuple shape, bool readonlyView) {
            _buffer = @object;
            CheckBuffer();

            _format = format;
            _shape = shape;
            _isReadOnly = readonlyView || _buffer.ReadOnly;
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
                return PythonOps.GetSliceCount(_start, _end.Value, ((long)_step * _itemsize).ClampToInt32());
            }
            return PythonOps.GetSliceCount(0, _buffer.ItemCount * (int)_buffer.ItemSize, ((long)_step * _itemsize).ClampToInt32());
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
                return _isReadOnly;
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

                shapeAsTuple = PythonTuple.Make(shape);
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

            return new MemoryView(_buffer, _start, _end, _step, formatAsString, shapeAsTuple ?? PythonTuple.MakeTuple(newLength), _isReadOnly);
        }

        private byte[] unpackBytes(string format, object o) {
            if (TypecodeOps.TryGetBytes(format, o, out byte[] bytes)) {
                return bytes;
            } else if (o is Bytes b) {
                return b.UnsafeByteArray; // CData returns a bytes object for its type
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
                return Bytes.Make(obj);
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
                if (_isReadOnly) {
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
            if (_isReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public void __delitem__([NotNull]Slice slice) {
            CheckBuffer();
            if (_isReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public object this[[NotNull]Slice slice] {
            get {
                CheckBuffer();
                int start, stop, step, count;
                FixSlice(slice, __len__(), out start, out stop, out step, out count);

                List<int> dimensions = new List<int>();

                // When a multidimensional memoryview is sliced, the slice
                // applies to only the first dimension. Therefore, other
                // dimensions are inherited.
                dimensions.Add(count);

                // In a 1-dimensional memoryview, the difference in bytes
                // between the position of mv[x] and mv[x + 1] is guaranteed
                // to be just the size of the data. For multidimensional
                // memoryviews, we must worry about the width of all the other
                // dimensions for the difference between mv[(x, y, z...)] and
                // mv[(x + 1, y, z...)]
                int firstIndexWidth = _itemsize;
                for (int i = 1; i < shape.__len__(); i++) {
                    int dimensionWidth = Converter.ConvertToInt32(shape[i]);
                    dimensions.Add(dimensionWidth);
                    firstIndexWidth *= dimensionWidth;
                }

                int newStart = _start + start * firstIndexWidth;
                int newEnd = _start + stop * firstIndexWidth;
                int newStep = ((long)_step * step).ClampToInt32();

                PythonTuple newShape = PythonTuple.Make(dimensions);

                return new MemoryView(_buffer, newStart, newEnd, newStep, format, newShape, _isReadOnly);
            }
            set {
                CheckBuffer();
                if (_isReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }

                if (ndim != 1) {
                    throw PythonOps.NotImplementedError("memoryview assignments are restricted to ndim = 1");
                }

                int start, stop, step, sliceCnt;
                FixSlice(slice, __len__(), out start, out stop, out step, out sliceCnt);

                slice = new Slice(start, stop, step);

                int newLen = PythonOps.Length(value);
                if (sliceCnt != newLen) {
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
        private static void FixSlice(Slice slice, int len, out int start, out int stop, out int step, out int count) {
            slice.GetIndicesAndCount(len, out start, out stop, out step, out count);

            if (stop < start && step >= 0) {
                // wrapped iteration is interpreted as empty slice
                stop = start;
            } else if (stop > start && step < 0) {
                stop = start;
            }
        }

        public object this[[NotNull]PythonTuple index] {
            get {
                CheckBuffer();
                return getAtFlatIndex(GetFlatIndex(index));
            }
            set {
                CheckBuffer();
                setAtFlatIndex(GetFlatIndex(index), value);
            }
        }

        /// <summary>
        /// Gets the "flat" index from the access of a tuple as if the
        /// multidimensional tuple were layed out in contiguous memory.
        /// </summary>
        private int GetFlatIndex(PythonTuple tuple) {
            int flatIndex = 0;
            int tupleLength = tuple.Count;
            int ndim = (int)this.ndim;
            int firstOutOfRangeIndex = -1;

            bool allInts = true;
            bool allSlices = true;

            // A few notes about the ordering of operations here:
            // 1) CPython checks the types of the objects in the tuple
            //    first, then the dimensions, then finally for the range.
            //    Because we do a range check while we go through the tuple,
            //    we have to remember that we had something out of range
            // 2) CPython checks for a multislice tuple, then for all ints,
            //    and throws an invalid slice key otherwise. We again try to
            //    do this in one pass, so we remember whether we've seen an int
            //    and whether we've seen a slice
            for (int i = 0; i < tupleLength; i++) {
                object indexObject = tuple[i];
                if (Converter.TryConvertToInt32(indexObject, out int indexValue)) {
                    allSlices = false;
                    // If we have a "bad" tuple, we no longer care
                    // about the resulting flat index, but still need
                    // to check the rest of the tuple in case it has a
                    // non-int value
                    if (i >= ndim || firstOutOfRangeIndex > -1) {
                        continue;
                    }

                    int dimensionWidth = (int)shape[i];

                    // If we have an out of range exception, that will only
                    // be thrown if the tuple length is correct, so we have to
                    // defer throwing to later
                    if (!PythonOps.TryFixIndex(indexValue, dimensionWidth, out indexValue)) {
                        firstOutOfRangeIndex = i;
                        continue;
                    }

                    flatIndex *= dimensionWidth;
                    flatIndex += indexValue;
                } else if (indexObject is Slice) {
                    allInts = false;
                } else {
                    throw PythonOps.TypeError("memoryview: invalid slice key");
                }
            }

            if (!allInts) {
                if (allSlices) {
                    throw PythonOps.NotImplementedError("multi-dimensional slicing is not implemented");
                } else {
                    throw PythonOps.TypeError("memoryview: invalid slice key");
                }
            }

            if (tupleLength < ndim) {
                throw PythonOps.NotImplementedError("sub-views are not implemented");
            }

            if (tupleLength > ndim) {
                throw PythonOps.TypeError("cannot index {0}-dimension view with {1}-element tuple", ndim, tupleLength);
            }

            if (firstOutOfRangeIndex != -1) {
                PythonOps.IndexError("index out of bounds on dimension {0}", firstOutOfRangeIndex + 1);
            }

            return flatIndex;
        }

        public int __hash__(CodeContext context) {
            if (_storedHash != null) {
                return _storedHash.Value;
            }

            CheckBuffer();
            if (!_isReadOnly) {
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

        #region IBufferProtocol Members

        object IBufferProtocol.GetItem(int index) => this[index];

        void IBufferProtocol.SetItem(int index, object value) => this[index] = value;

        void IBufferProtocol.SetSlice(Slice index, object value) => this[index] = value;

        int IBufferProtocol.ItemCount => numberOfElements();

        string IBufferProtocol.Format => format;

        BigInteger IBufferProtocol.ItemSize => itemsize;

        BigInteger IBufferProtocol.NumberDimensions => ndim;

        bool IBufferProtocol.ReadOnly => @readonly;

        IList<BigInteger> IBufferProtocol.GetShape(int start, int? end) {
            if (start == 0 && end == null) {
                return _shape.Select(n => Converter.ConvertToBigInteger(n)).ToList();
            } else {
                return ((IBufferProtocol)this[new Slice(start, end)]).GetShape(0, null);
            }
        }

        PythonTuple IBufferProtocol.Strides => strides;

        PythonTuple IBufferProtocol.SubOffsets => suboffsets;

        Bytes IBufferProtocol.ToBytes(int start, int? end) {
            if (start == 0 && end == null) {
                return tobytes();
            } else {
                return ((MemoryView)this[new Slice(start, end)]).tobytes();
            }
        }

        PythonList IBufferProtocol.ToList(int start, int? end) {
            if (start == 0 && end == null) {
                return tolist();
            } else {
                return ((MemoryView)this[new Slice(start, end)]).tolist();
            }
        }

        ReadOnlyMemory<byte> IBufferProtocol.ToMemory() {
            if (_step == 1) {
                CheckBuffer();
                return _end.HasValue ? _buffer.ToMemory().Slice(_start, _end.Value - _start) : _buffer.ToMemory().Slice(_start);
            } else {
                return ((IBufferProtocol)tobytes()).ToMemory();
            }
        }

        #endregion
    }
}
