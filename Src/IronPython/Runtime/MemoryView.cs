// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace IronPython.Runtime {
    [PythonType("memoryview")]
    public sealed class MemoryView : ICodeFormattable, IWeakReferenceable, IBufferProtocol, IPythonBuffer {
        private const int MaximumDimensions = 64;

        private IBufferProtocol _exporter;
        private IPythonBuffer _buffer;   // null if disposed
        // TODO: rename _offset to _startOffset
        private readonly int _offset;    // in bytes
        private readonly bool _isReadOnly;
        private readonly int _numDims;

        private readonly string _format;
        private readonly int _itemSize;
        private readonly IReadOnlyList<int> _shape;    // always in items
        private readonly IReadOnlyList<int> _strides;  // always in bytes

        private int? _storedHash;
        private WeakRefTracker _tracker;

        // Fields below are computed based on readonly fields above
        private readonly int _numItems;   // product(shape); 1 for scalars
        private readonly bool _isCContig;

        /// <seealso href="https://docs.python.org/3/c-api/memoryview.html#c.PyMemoryView_FromObject"/>
        public MemoryView([NotNull]IBufferProtocol @object) {
            _exporter = @object;

            // MemoryView should support all possible buffer exports (BufferFlags.FullRO)
            // but handling of suboffsets (BufferFlags.Indirect) is not implemented yet.
            // Hence the request is for BufferFlags.RecordsRO
            _buffer = @object.GetBuffer(BufferFlags.RecordsRO);
            // doublecheck that we don't have to deal with suboffsets
            if (_buffer.SubOffsets != null)
                throw PythonOps.NotImplementedError("memoryview: indirect buffers are not supported");

            ReadOnlySpan<byte> memblock = _buffer.AsReadOnlySpan();

            if (   (_buffer.ItemCount != 0 && !VerifyStructure(memblock.Length, _buffer.ItemSize, _buffer.NumOfDims, _buffer.Shape, _buffer.Strides, _buffer.Offset))
                || (_buffer.Shape == null && (_buffer.Offset != 0 || _buffer.ItemCount * _buffer.ItemSize != memblock.Length))
               ) {
                throw PythonOps.BufferError("memoryview: invalid buffer exported from object of type {0}", PythonOps.GetPythonTypeName(@object));
            }

            _offset = _buffer.Offset;
            _isReadOnly = _buffer.IsReadOnly;
            _numDims = _buffer.NumOfDims;

            _format = _buffer.Format;
            // in flags we requested format be provided, check that the exporter complied
            if (_format == null)
                throw PythonOps.BufferError("memoryview: object of type {0} did not report its format", PythonOps.GetPythonTypeName(@object));

            _itemSize = _buffer.ItemSize;
            // for convenience _shape and _strides are never null, even if _numDims == 0
            _shape = _buffer.Shape ?? (_numDims > 0 ? new int[] { _buffer.ItemCount } : new int[0]);  // TODO: use a static singleton

            //_strides = _buffer.Strides ?? _shape.Reverse().PreScan(_itemSize, (sub, size) => sub * size).Reverse();
            if (shape.Count == 0) {
                _strides = _shape; // TODO: use a static singleton
                _isCContig = true;
            } else if (_buffer.Strides != null) {
                _strides = _buffer.Strides;
                _isCContig = true;
                for (int i = _strides.Count - 1, curStride = _itemSize; i >= 0 && _isCContig; i--) {
                    _isCContig &= _strides[i] == curStride;
                    curStride *= _shape[i];
                }
            } else {
                _strides = GetContiguousStrides(_shape, _itemSize);
                _isCContig = true;
            }

            // invariants
            _numItems = _buffer.ItemCount;

            // sanity check
            Debug.Assert(_numItems == 0 || VerifyStructure(memblock.Length, _itemSize, _numDims, _numDims > 0 ? _shape : null, _numDims > 0 ? _strides : null, _offset));
        }

        public MemoryView([NotNull]MemoryView @object) {
            _exporter    = @object._exporter;
            _buffer      = _exporter.GetBuffer(BufferFlags.RecordsRO);
            _offset      = @object._offset;
            _isReadOnly  = @object._isReadOnly;
            _numDims     = @object._numDims;
            _format      = @object._format;
            _itemSize    = @object._itemSize;
            _shape       = @object._shape;
            _strides     = @object._strides;
            _isCContig   = @object._isCContig;
            _numItems    = @object._numItems;
        }

        internal MemoryView(IBufferProtocol @object, bool readOnly) : this(@object) {
            _isReadOnly = _isReadOnly || readOnly;
        }

        internal MemoryView(MemoryView mv, bool readOnly) : this(mv) {
            _isReadOnly = _isReadOnly || readOnly;
        }

        private MemoryView(MemoryView mv, int newStart, int newStop, int newStep, int newLen) : this(mv) {
            Debug.Assert(_numDims > 0);
            Debug.Assert(newLen <= _shape[0]);

            var oldLen = _shape[0];
            var newShape = _shape.ToArray();
            newShape[0] = newLen;
            _shape = newShape;

            _offset += newStart * _strides[0];

            if (newLen > 1) {
                var newStrides = _strides.ToArray();
                newStrides[0] *= newStep;
                _strides = newStrides;
            }

            if (oldLen != 0) _numItems /= oldLen;
            _numItems *= newLen;
            _isCContig = _isCContig && newStep == 1;
        }

        private MemoryView(MemoryView mv, string newFormat, int newItemSize, IReadOnlyList<int> newShape) : this(mv) {
            // arguemnts already checked for consistency

            // reformat
            _format = newFormat;

            _numItems *= _itemSize;
            _numItems /= newItemSize;

            _itemSize = newItemSize;

            // reshape
            _shape = newShape;
            _numDims = _shape.Count;
            _strides = GetContiguousStrides(_shape, _itemSize);
        }

        // TODO: use cached flags on the fly iso changing fields
        private MemoryView(MemoryView @object, BufferFlags flags) : this(@object) {
            if (!flags.HasFlag(BufferFlags.Strides)) {
                Debug.Assert(_isCContig);
                _strides = null;
            }

            if (!flags.HasFlag(BufferFlags.ND)) {
                // flatten
                _shape = null;
            }

            if (!flags.HasFlag(BufferFlags.Format)) {
                _format = null;
                // keep original _itemSize
                // TODO: verify: adjustments to _shape needed?
            }
        }

        private void CheckBuffer([System.Runtime.CompilerServices.CallerMemberName] string memberName = null) {
            if (_buffer == null) throw PythonOps.ValueError("operation forbidden on released memoryview object");

            // TODO: properly handle unformmated views
            if (_format == null && memberName != nameof(tobytes)) throw PythonOps.ValueError("memoryview: not formatted");
        }

        /// <summary>
        /// Verify that the parameters represent a valid buffer within
        /// the bounds of the allocated memory and conforms to the buffer protocol rules.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#numpy-style-shape-and-strides"/>
        private static bool VerifyStructure(int memlen, int itemsize, int ndim, IReadOnlyList<int> shape, IReadOnlyList<int> strides, int offset) {
            if (offset % itemsize != 0)
                return false;
            if (offset < 0 || offset + itemsize > memlen)
                return false;
            if (strides != null && strides.Any(v => v % itemsize != 0))
                return false;

            if (ndim <= 0)
                return ndim == 0 && shape == null && strides == null;
            if (shape != null && shape.Contains(0))
                return true;

            // additional tests of shape and strides that were not in the original verify_structure
            // but which are described in other places in Python documentation about buffer protocol
            if (strides != null && shape == null)
                return false;
            if (ndim > 1 && (shape == null || shape.Count != ndim))
                return false;
            if (strides != null && strides.Count != ndim)
                return false;
            if (strides == null && offset != 0)
                return false;
            if (shape == null)
                return true;

            if (strides == null)
                return shape.Aggregate(1, (num, size) => num * size) <= memlen;

            /*
            imin = sum(strides[j] * (shape[j] - 1) for j in range(ndim)
                       if strides[j] <= 0)
            imax = sum(strides[j] * (shape[j] - 1) for j in range(ndim)
                        if strides[j] > 0)
            */
            int imin = 0, imax = 0;
            for (int j = 0; j < ndim; j++) {
                if (strides[j] <= 0) {
                    imin += strides[j] * (shape[j] - 1);
                } else {
                    imax += strides[j] * (shape[j] - 1);
                }
            }

            return 0 <= offset + imin && offset + imax + itemsize <= memlen;
        }

        private static IReadOnlyList<int> GetContiguousStrides(IReadOnlyList<int> shape, int itemSize) {
            var strides = new int[shape.Count];
            if (strides.Length > 0) {
                strides[strides.Length - 1] = itemSize;
                for (int i = strides.Length - 1; i > 0; i--) {
                    strides[i - 1] = shape[i] * strides[i];
                }
            }
            return strides;
        }

        public int __len__() {
            CheckBuffer();
            return _shape.Count > 0 ? _shape[0] : 1;
        }

        public object obj {
            get {
                CheckBuffer();
                return _exporter;
            }
        }

        public void release(CodeContext /*!*/ context) {
            ((IPythonBuffer)this).Dispose();
        }

        public object __enter__() {
            CheckBuffer();
            return this;
        }

        public void __exit__(CodeContext/*!*/ context, params object[] excinfo) {
            release(context);
        }

        public bool c_contiguous {
            get {
                CheckBuffer();
                return _isCContig;
            }
        }

        public bool f_contiguous {
            get {
                CheckBuffer();
                return _isCContig && _numDims <= 1;  // TODO: support for ND Fortran arrays not implemented
            }
        }

        public bool contiguous {
            get {
                CheckBuffer();
                return _isCContig;
            }
        }

        public string format {
            get {
                CheckBuffer();
                return _format;
            }
        }

        public int itemsize {
            get {
                CheckBuffer();
                return _itemSize;
            }
        }

        public int ndim {
            get {
                CheckBuffer();
                return _numDims;
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
                return PythonTuple.Make(_shape);
            }
        }

        public PythonTuple strides {
            get {
                CheckBuffer();
                return PythonTuple.Make(_strides);
            }
        }

        public PythonTuple suboffsets {
            get {
                CheckBuffer();
                return PythonTuple.EMPTY;
            }
        }

        public Bytes tobytes() {
            CheckBuffer();

            if (_shape?.Contains(0) ?? false) {
                return Bytes.Empty;
            }

            var buf = _buffer.AsReadOnlySpan();

            if (_isCContig) {
                return Bytes.Make(buf.Slice(_offset, _numItems * _itemSize).ToArray());
            }

            byte[] bytes = new byte[_numItems * _itemSize];
            copyDimension(buf, bytes, _offset, dim: 0);
            return Bytes.Make(bytes);

            int copyDimension(ReadOnlySpan<byte> source, Span<byte> dest, int ofs, int dim) {
                if (dim >= _shape.Count) {
                    // copy individual element (scalar)
                    source.Slice(ofs, _itemSize).CopyTo(dest);
                    return _itemSize;
                }

                int copied = 0;
                for (int i = 0; i < _shape[dim]; i++) {
                    copied += copyDimension(source, dest.Slice(copied), ofs, dim + 1);
                    ofs += _strides[dim];
                }
                return copied;
            }
        }

        public object tolist() {
            CheckBuffer();

            return subdimensionToList(_buffer.AsReadOnlySpan(), _offset, dim: 0);

            object subdimensionToList(ReadOnlySpan<byte> source, int ofs, int dim) {
                if (dim >= _shape.Count) {
                    // extract individual element (scalar)
                    return packBytes(_format, source.Slice(ofs));
                }

                object[] elements = new object[_shape[dim]];
                for (int i = 0; i < _shape[dim]; i++) {
                    elements[i] = subdimensionToList(source, ofs, dim + 1);
                    ofs += _strides[dim];
                }
                return PythonList.FromArrayNoCopy(elements);
            }
        }

        public MemoryView cast([NotNull]string format) {
            return cast(format, null);
        }

        public MemoryView cast([NotNull]string format, [NotNull]object shape) {
            if (!_isCContig) {
                throw PythonOps.TypeError("memoryview: casts are restricted to C-contiguous views");
            }

            if (_shape.Contains(0) || _strides.Contains(0)) {
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

                if (_numDims != 1 && newNDim != 1) {
                    throw PythonOps.TypeError("memoryview: cast must be 1D -> ND or ND -> 1D");
                }
            }

            int newItemsize;
            if (!TypecodeOps.TryGetTypecodeWidth(format, out newItemsize)) {
                throw PythonOps.ValueError(
                    "memoryview: destination format must be a native single character format prefixed with an optional '@'");
            }

            if (!TypecodeOps.IsByteFormat(this._format) && !TypecodeOps.IsByteFormat(format)) {
                throw PythonOps.TypeError("memoryview: cannot cast between two non-byte formats");
            }

            if ((_numItems * _itemSize) % newItemsize != 0) {
                throw PythonOps.TypeError("memoryview: length is not a multiple of itemsize");
            }

            int newLength = _numItems * _itemSize / newItemsize;
            int[] newShape;
            if (shapeAsTuple != null) {
                newShape = new int[shapeAsTuple.Count];
                int lengthGivenShape = 1;
                for (int i = 0; i < shapeAsTuple.Count; i++) {
                    newShape[i] = Converter.ConvertToInt32(shapeAsTuple[i]);
                    lengthGivenShape *= newShape[i];
                }

                if (lengthGivenShape != newLength) {
                    throw PythonOps.TypeError("memoryview: product(shape) * itemsize != buffer size");
                }
            } else {
                newShape = new int[] { newLength };
            }

            return new MemoryView(this, format, newItemsize, newShape);
        }

        private void unpackBytes(string format, object o, Span<byte> dest) {
            if (TypecodeOps.TryGetBytes(format, o, dest)) {
                return;
            } else {
                throw PythonOps.NotImplementedError("No conversion for type {0} to byte array", PythonOps.GetPythonTypeName(o));
            }
        }

        private static object packBytes(string format, ReadOnlySpan<byte> bytes) {
            if (TypecodeOps.TryGetFromBytes(format, bytes, out object result))
                return result;
            else {
                return Bytes.Make(bytes.ToArray());
            }
        }

        private object GetItem(int offset) {
            object result = packBytes(_format, _buffer.AsReadOnlySpan().Slice(offset, _itemSize));

            return PythonOps.ConvertToPythonPrimitive(result);
        }

        private void SetItem(int offset, object value) {
            switch (_format) {
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
                    break; // This could be a variety of types, let the UnpackBytes decide
            }

            unpackBytes(format, value, _buffer.AsSpan().Slice(offset, _itemSize));
        }

        // TODO: support indexable, BigInteger etc.
        public object this[int index] {
            get {
                CheckBuffer();
                if (_numDims == 0) {
                    throw PythonOps.TypeError("invalid indexing of 0-dim memory");
                }

                index = PythonOps.FixIndex(index, __len__());
                if (ndim > 1) {
                    throw PythonOps.NotImplementedError("multi-dimensional sub-views are not implemented");
                }

                return GetItem(GetItemOffset(index));
            }
            set {
                CheckBuffer();
                if (_isReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }
                if (_numDims == 0) {
                    throw PythonOps.TypeError("invalid indexing of 0-dim memory");
                }

                index = PythonOps.FixIndex(index, __len__());
                if (ndim > 1) {
                    throw PythonOps.NotImplementedError("multi-dimensional sub-views are not implemented");
                }

                SetItem(GetItemOffset(index), value);
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
                if (_numDims == 0) {
                    throw PythonOps.TypeError("invalid indexing of 0-dim memory");
                }

                int start, stop, step, count;
                FixSlice(slice, __len__(), out start, out stop, out step, out count);

                return new MemoryView(this, start, stop, step, count);
            }
            set {
                CheckBuffer();
                if (_isReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }
                if (_numDims == 0) {
                    throw PythonOps.TypeError("invalid indexing of 0-dim memory");
                }
                if (ndim != 1) {
                    throw PythonOps.NotImplementedError("memoryview assignments are restricted to ndim = 1");
                }

                int start, stop, step, sliceCnt;
                FixSlice(slice, __len__(), out start, out stop, out step, out sliceCnt);

                int newLen = PythonOps.Length(value);
                if (sliceCnt != newLen) {
                    throw PythonOps.ValueError("cannot resize memory view");
                }

                slice = new Slice(start, stop, step);
                slice.DoSliceAssign(SliceAssign, __len__(), value);
            }
        }

        private void SliceAssign(int index, object value) {
            SetItem(GetItemOffset(index), value);
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
                return GetItem(GetItemOffset(index));
            }
            set {
                CheckBuffer();
                SetItem(GetItemOffset(index), value);
            }
        }

        /// <summary>
        /// Gets the offset of the item byte data
        /// from the beginning of buffer memory span,
        /// where the given tuple is the multidimensional index.
        /// </summary>
        private int GetItemOffset(PythonTuple tuple) {
            int flatIndex = _offset;
            int tupleLength = tuple.Count;
            int ndim = (int)this.ndim;
            int firstOutOfRangeIndex = -1;

            bool allInts = true;
            bool allSlices = true;

            // A few notes about the ordering of operations here:
            // 0) Before anything else, CPython handles indexing of
            //    0-dim memory as a special case, the tuple elements
            //    are not checked
            // 1) CPython checks the types of the objects in the tuple
            //    first, then the dimensions, then finally for the range.
            //    Because we do a range check while we go through the tuple,
            //    we have to remember that we had something out of range
            // 2) CPython checks for a multislice tuple, then for all ints,
            //    and throws an invalid slice key otherwise. We again try to
            //    do this in one pass, so we remember whether we've seen an int
            //    and whether we've seen a slice

            if (_numDims == 0) {
                if (tupleLength != 0) {
                    throw PythonOps.TypeError("invalid indexing of 0-dim memory");
                }
                return flatIndex;
            }

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

                    int dimensionWidth = _shape[i];

                    // If we have an out of range exception, that will only
                    // be thrown if the tuple length is correct, so we have to
                    // defer throwing to later
                    if (!PythonOps.TryFixIndex(indexValue, dimensionWidth, out indexValue)) {
                        firstOutOfRangeIndex = i;
                        continue;
                    }

                    flatIndex += indexValue * _strides[i];
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

        private int GetItemOffset(int index) {
            Debug.Assert(_numDims == 1);
            return _offset + index * _strides[0];
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
            // TODO: comparing flat bytes is oversimplification; besides, no data copyimg
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

        IPythonBuffer IBufferProtocol.GetBuffer(BufferFlags flags) {
            CheckBuffer();

            if (_isReadOnly && flags.HasFlag(BufferFlags.Writable))
                throw PythonOps.BufferError("Object is not writable.");

            if (!_isCContig && !flags.HasFlag(BufferFlags.Strides))
                throw PythonOps.BufferError("memoryview: underlying buffer is not c-contiguous");

            return new MemoryView(this, flags);
        }

        void IDisposable.Dispose() {
            if (_buffer != null) {
                _buffer.Dispose();
                _buffer = null;
                _exporter = null;
            }
        }

        object IPythonBuffer.Object => _exporter;

        bool IPythonBuffer.IsReadOnly => _isReadOnly;

        ReadOnlySpan<byte> IPythonBuffer.AsReadOnlySpan() {
            if (_buffer == null) throw new ObjectDisposedException(nameof(MemoryView));

            if (_isCContig) {
                return _buffer.AsReadOnlySpan().Slice(_offset, _numItems * _itemSize);
            } else {
                return _buffer.AsReadOnlySpan();
            }
        }

        Span<byte> IPythonBuffer.AsSpan() {
            if (_buffer == null) throw new ObjectDisposedException(nameof(MemoryView));
            if (_isReadOnly) throw new InvalidOperationException("memoryview: object is not writable");

            if (_isCContig) {
                return _buffer.AsSpan().Slice(_offset, _numItems * _itemSize);
            } else {
                return _buffer.AsSpan();
            }
        }

        int IPythonBuffer.Offset => _isCContig ? 0 : _offset;

        int IPythonBuffer.ItemCount => _numItems;

        string IPythonBuffer.Format => _format;

        int IPythonBuffer.ItemSize => _itemSize;

        int IPythonBuffer.NumOfDims => _numDims;

        IReadOnlyList<int> IPythonBuffer.Shape => _numDims > 0 ? _shape : null;

        IReadOnlyList<int> IPythonBuffer.Strides => !_isCContig ? _strides : null;

        IReadOnlyList<int> IPythonBuffer.SubOffsets => null; // not supported yet

        Bytes IPythonBuffer.ToBytes(int start, int? end) {
            if (start == 0 && end == null) {
                return tobytes();
            } else {
                return ((MemoryView)this[new Slice(start, end)]).tobytes();
            }
        }

        ReadOnlyMemory<byte> IPythonBuffer.ToMemory() {
            return ((IPythonBuffer)this).AsReadOnlySpan().ToArray();
        }

        #endregion
    }
}
