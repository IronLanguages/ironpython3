// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Linq;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [PythonType("memoryview")]
    public sealed class MemoryView : ICodeFormattable, IWeakReferenceable, IBufferProtocol, IPythonBuffer {
        private const int MaximumDimensions = 64;
        private const string ValidCodes = "cbB?hHiIlLqQnNfdPrR";

        private readonly IBufferProtocol _exporter;
        private IPythonBuffer? _buffer;   // null if disposed
        // TODO: rename _offset to _startOffset
        private readonly BufferFlags _flags;
        private readonly int _offset;    // in bytes
        private readonly bool _isReadOnly;
        private readonly int _numDims;

        private readonly string _format;
        private readonly int _itemSize;
        private readonly IReadOnlyList<int> _shape;    // always in items
        private readonly IReadOnlyList<int> _strides;  // always in bytes

        private int? _storedHash;
        private WeakRefTracker? _tracker;

        // Fields below are computed based on readonly fields above
        private readonly int _numItems;   // product(shape); 1 for scalars
        private readonly bool _isCContig;
        private readonly bool _isFContig;

        /// <seealso href="https://docs.python.org/3/c-api/memoryview.html#c.PyMemoryView_FromObject"/>
        public MemoryView([NotNone] IBufferProtocol @object) {
            _exporter = @object;

            // MemoryView should support all possible buffer exports (BufferFlags.FullRO)
            // but handling of suboffsets (BufferFlags.Indirect) is not implemented yet.
            // Hence the request is for BufferFlags.RecordsRO
            _flags = BufferFlags.RecordsRO;
            _buffer = @object.GetBuffer(_flags);
            // doublecheck that we don't have to deal with suboffsets
            if (_buffer.SubOffsets != null)
                throw PythonOps.NotImplementedError("memoryview: indirect buffers are not supported");

            ReadOnlySpan<byte> memblock = _buffer.AsReadOnlySpan();

            if (   (_buffer.ItemCount != 0 && !VerifyStructure(memblock.Length, _buffer.ItemSize, _buffer.NumOfDims, _buffer.Shape, _buffer.Strides, _buffer.Offset))
                || (_buffer.Shape == null && (_buffer.Offset != 0 || _buffer.NumBytes() != memblock.Length))
               ) {
                throw PythonOps.BufferError("memoryview: invalid buffer exported from object of type {0}", PythonOps.GetPythonTypeName(@object));
            }

            _offset = _buffer.Offset;
            _isReadOnly = _buffer.IsReadOnly;
            _numDims = _buffer.NumOfDims;

            // in flags we requested format be provided, check that the exporter complied
            if (_buffer.Format == null)
                throw PythonOps.BufferError("memoryview: object of type {0} did not report its format", PythonOps.GetPythonTypeName(@object));
            _format = _buffer.Format;

            _itemSize = _buffer.ItemSize;
            // for convenience _shape and _strides are never null, even if _numDims == 0 or _flags indicate no _shape or _strides
            _shape = _buffer.Shape ?? (_numDims > 0 ? new int[] { _buffer.ItemCount } : Array.Empty<int>());

            if (_numDims == 0) {
                _strides = Array.Empty<int>();
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
            _isFContig = _isCContig && _numDims <= 1;  // TODO: support for ND Fortran arrays not implemented

            // sanity check
            Debug.Assert(_numItems == 0 || VerifyStructure(memblock.Length, _itemSize, _numDims, _numDims > 0 ? _shape : null, _numDims > 0 ? _strides : null, _offset));
        }

        public MemoryView([NotNone] MemoryView @object) {
            _exporter    = @object._exporter;
            _flags       = BufferFlags.RecordsRO;
            _buffer      = _exporter.GetBuffer(_flags);
            _offset      = @object._offset;
            _isReadOnly  = @object._isReadOnly;
            _numDims     = @object._numDims;
            _format      = @object._format;
            _itemSize    = @object._itemSize;
            _shape       = @object._shape;
            _strides     = @object._strides;
            _isCContig   = @object._isCContig;
            _isFContig   = @object._isFContig;
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
            _isFContig = _isCContig && _numDims <= 1;  // TODO: support for ND Fortran arrays not implemented
        }

        private MemoryView(MemoryView mv, string newFormat, int newItemSize, IReadOnlyList<int> newShape) : this(mv) {
            // arguments already checked for consistency

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

        private MemoryView(MemoryView @object, BufferFlags flags) : this(@object) {
            // flags already checked for consistency with the underlying buffer
            _flags = flags;

            if (!_flags.HasFlag(BufferFlags.ND)) {
                // flatten
                _numDims = 1;
                _shape = new[] { _numItems };
                _isCContig = _isFContig = true;
            }
        }

        ~MemoryView() {
            try {
                if (_buffer != null) {
                    _buffer.Dispose();
                }
            } catch {}
        }

        [MemberNotNull(nameof(_buffer))]
        private void CheckBuffer() {
            if (_buffer == null) throw PythonOps.ValueError("operation forbidden on released memoryview object");
        }

        /// <summary>
        /// Verify that the parameters represent a valid buffer within
        /// the bounds of the allocated memory and conforms to the buffer protocol rules.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#numpy-style-shape-and-strides"/>
        private static bool VerifyStructure(int memlen, int itemsize, int ndim, IReadOnlyList<int>? shape, IReadOnlyList<int>? strides, int offset) {
            // do some basic checks on shape and stride
            if (shape != null && shape.Count != ndim)
                return false;
            if (strides != null && strides.Count != ndim)
                return false;
            if (shape != null && shape.Any(v => v < 0))
                return false;

            // ----------------
            // verify_structure

            if (itemsize == 0) {
                // itemsize == 0 is not checked in verify_structure, but can cause a failure below
                // such a buffer can be built with ctypes and an empty struct
                if (offset != 0)
                    return false;
                if (memlen != 0)
                    return false;
                if (strides != null && strides.Any(v => v != 0))
                    return false;
            } else {
                if (offset % itemsize != 0)
                    return false;
                if (offset < 0 || offset + itemsize > memlen)
                    return false;
                if (strides != null && strides.Any(v => v % itemsize != 0))
                    return false;
            }

            if (ndim <= 0)
                return ndim == 0 && shape == null && strides == null;

            // ----------------
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
                return shape.Aggregate(1, (num, size) => num * size) * itemsize <= memlen;
            // ----------------

            if (shape.Contains(0))
                return true;

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

        public void __exit__(CodeContext/*!*/ context, [NotNone] params object?[] excinfo) {
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
                return _isFContig;
            }
        }

        public bool contiguous {
            get {
                CheckBuffer();
                return _isCContig || _isFContig;
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

        public int nbytes {
            get {
                CheckBuffer();
                return _itemSize * _numItems;
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
                Debug.Assert(_buffer.SubOffsets == null); // TODO: implement suboffsets support
                return PythonTuple.EMPTY;
            }
        }

        // new in CPython 3.5
        public string hex() {
            CheckBuffer();

            if (_isCContig) {
                return Bytes.ToHex(_buffer.AsReadOnlySpan().Slice(_offset, _numItems * _itemSize));
            }
            else {
                var builder = new System.Text.StringBuilder(_numItems * _itemSize * 2);
                foreach (byte b in this.EnumerateBytes()) {
                    builder.Append(ToAscii(b >> 4));
                    builder.Append(ToAscii(b & 0xf));
                }
                return builder.ToString();
            }

            static char ToAscii(int b) {
                return (char)(b < 10 ? '0' + b : 'a' + (b - 10));
            }
        }

        public Bytes tobytes() {
            CheckBuffer();

            if (_shape.Contains(0)) {
                return Bytes.Empty;
            }

            var buf = _buffer.AsReadOnlySpan();

            if (_isCContig) {
                return Bytes.Make(buf.Slice(_offset, _numItems * _itemSize).ToArray());
            }

            byte[] bytes = new byte[_numItems * _itemSize];
            int i = 0;
            foreach (byte b in this.EnumerateBytes()) {
                bytes[i++] = b;
            }
            return Bytes.Make(bytes);

        }

        public object tolist() {
            CheckBuffer();
            TypecodeOps.DecomposeTypecode(_format, out char byteorder, out char typecode);

            return subdimensionToList(_buffer.AsReadOnlySpan(), _offset, dim: 0);

            object subdimensionToList(ReadOnlySpan<byte> source, int ofs, int dim) {
                if (dim >= _shape.Count) {
                    // extract individual element (scalar)
                    return PackBytes(typecode, source.Slice(ofs));
                }

                object[] elements = new object[_shape[dim]];
                for (int i = 0; i < _shape[dim]; i++) {
                    elements[i] = subdimensionToList(source, ofs, dim + 1);
                    ofs += _strides[dim];
                }
                return PythonList.FromArrayNoCopy(elements);
            }
        }

        public MemoryView cast([NotNone] string format) {
            return cast(format, null);
        }

        public MemoryView cast([NotNone] string format, [NotNone, AllowNull]object shape) {
            if (!_isCContig) {
                throw PythonOps.TypeError("memoryview: casts are restricted to C-contiguous views");
            }

            if ((shape != null || _numDims != 1) && (_shape.Contains(0) || _strides.Contains(0))) {
                throw PythonOps.TypeError("memoryview: cannot cast view with zeros in shape or strides");
            }

            PythonTuple? shapeAsTuple = null;

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

            if ( !TypecodeOps.TryDecomposeTypecode(format, out char byteorder, out char typecode)
              || !IsSupportedTypecode(typecode)
              || byteorder != '@'
            ) {
                throw PythonOps.ValueError(
                    "memoryview: destination format must be a native single character format prefixed with an optional '@'");
            }

            if (!TypecodeOps.IsByteCode(typecode)) {
                TypecodeOps.DecomposeTypecode(_format, out _, out char thisTypecode);
                if (!TypecodeOps.IsByteCode(thisTypecode)) {
                    throw PythonOps.TypeError("memoryview: cannot cast between two non-byte formats");
                }
            }

            int newItemsize = TypecodeOps.GetTypecodeWidth(typecode);
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

        private static bool IsSupportedTypecode(char code) {
            return ValidCodes.IndexOf(code) >= 0;
        }

        private static void UnpackBytes(char typecode, object o, Span<byte> dest) {
            if (!IsSupportedTypecode(typecode)) {
                throw PythonOps.NotImplementedError("memoryview: format {0} not supported", typecode);
            }

            // TODO: support non-native byteorder
            if (!TypecodeOps.TryGetBytes(typecode, o, dest)) {
                throw PythonOps.NotImplementedError("No conversion for type {0} to byte array", PythonOps.GetPythonTypeName(o));
            }
        }

        private static object PackBytes(char typecode, ReadOnlySpan<byte> bytes) {
            // TODO: support non-native byteorder
            if (IsSupportedTypecode(typecode) && TypecodeOps.TryGetFromBytes(typecode, bytes, out object? result))
                return result;
            else {
                throw PythonOps.NotImplementedError("memoryview: format {0} not supported", typecode);
            }
        }

        private object GetItem(int offset) {
            TypecodeOps.DecomposeTypecode(_format, out char byteorder, out char typecode);
            object result = PackBytes(typecode, _buffer!.AsReadOnlySpan().Slice(offset, _itemSize));

            return PythonOps.ConvertToPythonPrimitive(result);
        }

        private void SetItem(int offset, object? value) {
            if (value == null) {
                throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", _format);
            }
            TypecodeOps.DecomposeTypecode(_format, out char byteorder, out char typecode);
            switch (typecode) {
                case 'd': // double
                case 'f': // float
                    if (!Converter.TryConvertToDouble(value, out double convertedValueDouble)) {
                        throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", _format);
                    }
                    value = convertedValueDouble;
                    break;

                case 'c': // bytechar
                    if (!(value is Bytes b)) {
                        throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", _format);
                    }
                    if (b.Count != 1) {
                        throw PythonOps.ValueError("memoryview: invalid value for format '{0}'", _format);
                    }
                    break;

                case 'b': // signed byte
                case 'B': // unsigned byte
                case 'h': // signed short
                case 'H': // unsigned short
                case 'i': // signed int
                case 'I': // unsigned int
                case 'l': // signed long
                case 'L': // unsigned long
                case 'n': // signed index
                case 'N': // unsigned index
                case 'q': // signed long long
                case 'Q': // unsigned long long
                    if (!PythonOps.IsNumericObject(value)) {
                        throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", _format);
                    }

                    if (TypecodeOps.CausesOverflow(value, typecode)) {
                        throw PythonOps.ValueError("memoryview: invalid value for format '{0}'", _format);
                    }

                    if (typecode == 'Q') {
                        value = Converter.ConvertToUInt64(value);
                    } else {
                        value = Converter.ConvertToInt64(value);
                    }
                    break;

                case 'P': // void pointer
                    if (!PythonOps.IsNumericObject(value)) {
                        throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", _format);
                    }

                    var bi = Converter.ConvertToBigInteger(value);
                    if (TypecodeOps.CausesOverflow(bi, typecode)) {
                        throw PythonOps.ValueError("memoryview: invalid value for format '{0}'", _format);
                    }
                    value = bi;
                    break;

                case 'r': // .NET signed pointer
                case 'R': // .NET unsigned pointer
                    if (value is UIntPtr uptr) {
                        if (typecode == 'r') {
                            value = new IntPtr(unchecked((Int64)uptr.ToUInt64()));
                        }
                        break;
                    }

                    if (value is IntPtr iptr) {
                        if (typecode == 'R') {
                            value = new UIntPtr(unchecked((UInt64)iptr.ToInt64()));
                        }
                        break;
                    }
                    throw PythonOps.TypeError("memoryview: invalid type for format '{0}'", _format);

                default:
                    break; // This could be a variety of types, let the UnpackBytes decide
            }

            UnpackBytes(typecode, value, _buffer!.AsSpan().Slice(offset, _itemSize));
        }

        // TODO: support indexable, BigInteger etc.
        public object? this[int index] {
            get {
                CheckBuffer();
                if (_numDims == 0) {
                    throw PythonOps.TypeError("invalid indexing of 0-dim memory");
                }

                index = PythonOps.FixIndex(index, __len__());
                if (_numDims > 1) {
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
                if (_numDims > 1) {
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

        public void __delitem__([NotNone] Slice slice) {
            CheckBuffer();
            if (_isReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public object? this[[NotNone] Slice slice] {
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
                if (_numDims != 1) {
                    throw PythonOps.NotImplementedError("memoryview assignments are restricted to ndim = 1");
                }

                int start, stop, step, sliceCnt;
                FixSlice(slice, __len__(), out start, out stop, out step, out sliceCnt);

                int newLen = PythonOps.Length(value);
                if (sliceCnt != newLen) {
                    throw PythonOps.ValueError("cannot resize memory view");
                }

                // TODO: treat value as bytes-like object, not enumerable
                slice = new Slice(start, stop, step);
                slice.DoSliceAssign(SliceAssign, __len__(), value);
            }
        }

        private void SliceAssign(int index, object? value) {
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

        public object? this[[NotNone] PythonTuple index] {
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
                object? indexObject = tuple[i];
                if (Converter.TryConvertToInt32(indexObject, out int indexValue)) {
                    allSlices = false;
                    // If we have a "bad" tuple, we no longer care
                    // about the resulting flat index, but still need
                    // to check the rest of the tuple in case it has a
                    // non-int value
                    if (i >= _numDims || firstOutOfRangeIndex > -1) {
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

            if (tupleLength < _numDims) {
                throw PythonOps.NotImplementedError("sub-views are not implemented");
            }

            if (tupleLength > _numDims) {
                throw PythonOps.TypeError("cannot index {0}-dimension view with {1}-element tuple", _numDims, tupleLength);
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

            TypecodeOps.DecomposeTypecode(_format, out _, out char typecode);
            if (!TypecodeOps.IsByteCode(typecode)) {
                throw PythonOps.ValueError("memoryview: hashing is restricted to formats 'B', 'b' or 'c'");
            }

            _storedHash = tobytes().GetHashCode();
            return _storedHash.Value;
        }

        private bool EquivalentShape(MemoryView mv) {
            if (_numDims != mv._numDims) return false;
            for (int i = 0; i < _numDims; i++) {
                if (_shape[i] != mv._shape[i]) return false;
                if (_shape[i] == 0) break;
            }
            return true;
        }

        public bool __eq__(CodeContext/*!*/ context, [NotNone] MemoryView value) {
            if (_buffer == null) return ReferenceEquals(this, value);
            if (value._buffer == null) return false;
            if (!EquivalentShape(value)) return false;

            TypecodeOps.DecomposeTypecode(_format, out char ourByteorder, out char ourTypecode);
            // TODO: Support non-native byteorder

            // fast tracks if item formats match
            if (_format == value._format && !TypecodeOps.IsFloatCode(ourTypecode)) {
                if (ReferenceEquals(this, value)) return true;

                if (_isCContig && value._isCContig) {
                    // compare blobs
                    return ((IPythonBuffer)this).AsReadOnlySpan().SequenceEqual(((IPythonBuffer)value).AsReadOnlySpan());
                }

                // compare byte by byte
                using var ourBytes = this.EnumerateBytes();
                using var theirBytes = value.EnumerateBytes();
                while (ourBytes.MoveNext() && theirBytes.MoveNext()) {
                    if (ourBytes.Current != theirBytes.Current) return false;
                }

                return true;
            }

            // compare item by item
            TypecodeOps.DecomposeTypecode(value._format, out char theirByteorder, out char theirTypecode);

            using var us = this.EnumerateItemData();
            using var them = value.EnumerateItemData();
            while (us.MoveNext() && them.MoveNext()) {
                _ = TypecodeOps.TryGetFromBytes(ourTypecode, us.Current, out object? x);
                _ = TypecodeOps.TryGetFromBytes(theirTypecode, them.Current, out object? y);

                if (!PythonOps.EqualRetBool(x, y)) return false;
            }

            return true;
        }

        public bool __eq__(CodeContext/*!*/ context, [NotNone] IBufferProtocol value) => __eq__(context, new MemoryView(value));

        [return: MaybeNotImplemented]
        public NotImplementedType __eq__(CodeContext/*!*/ context, object? value) => NotImplementedType.Value;

        public bool __ne__(CodeContext/*!*/ context, [NotNone] MemoryView value) => !__eq__(context, value);

        public bool __ne__(CodeContext/*!*/ context, [NotNone] IBufferProtocol value) => !__eq__(context, value);

        [return: MaybeNotImplemented]
        public NotImplementedType __ne__(CodeContext/*!*/ context, object? value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __lt__(CodeContext/*!*/ context, object? value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __le__(CodeContext/*!*/ context, object? value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __gt__(CodeContext/*!*/ context, object? value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __ge__(CodeContext/*!*/ context, object? value) => NotImplementedType.Value;

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            if (_buffer == null) {
                return String.Format("<released memory at {0}>", PythonOps.Id(this));
            }
            return String.Format("<memory at {0}>", PythonOps.Id(this));
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker? IWeakReferenceable.GetWeakRef() {
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

        IPythonBuffer? IBufferProtocol.GetBuffer(BufferFlags flags, bool throwOnError) {
            CheckBuffer();

            if (flags.HasFlag(BufferFlags.Writable) && _isReadOnly)
                return ReportError("memoryview: underlying buffer is not writable");

            if (flags.HasFlag(BufferFlags.CContiguous) && !_isCContig)
                return ReportError("memoryview: underlying buffer is not C-contiguous");

            if (flags.HasFlag(BufferFlags.FContiguous) && !_isFContig)
                return ReportError("memoryview: underlying buffer is not Fortran contiguous");

            if (flags.HasFlag(BufferFlags.AnyContiguous) && !_isCContig && !_isFContig)
                return ReportError("memoryview: underlying buffer is not contiguous");

            // TODO: Support for suboffsets
            //if (!flags.HasFlag(!BufferFlags.Indirect) && _suboffsets != null)
            //    return ReportError("memoryview: underlying buffer requires suboffsets");

            if (!flags.HasFlag(BufferFlags.Strides) && !_isCContig)
                return ReportError("memoryview: underlying buffer is not C-contiguous");

            if (!flags.HasFlag(BufferFlags.ND) && flags.HasFlag(BufferFlags.Format))
                return ReportError("memoryview: cannot cast to unsigned bytes if the format flag is present");

            return new MemoryView(this, flags);

            IPythonBuffer? ReportError(string msg) {
                if (throwOnError) {
                    throw PythonOps.BufferError(msg);
                }
                return null;
            }
        }

        #endregion

        #region IPythonBuffer Members

        void IDisposable.Dispose() {
            if (_buffer != null) {
                _buffer.Dispose();
                _buffer = null;
                GC.SuppressFinalize(this);
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

        MemoryHandle IPythonBuffer.Pin()
            => _buffer?.Pin() ?? throw new ObjectDisposedException(nameof(MemoryView));

        int IPythonBuffer.Offset
            => _isCContig ? 0 : _offset;

        int IPythonBuffer.ItemCount
            => _numItems;

        string? IPythonBuffer.Format
            => _flags.HasFlag(BufferFlags.Format) ? _format : null;

        int IPythonBuffer.ItemSize
            => _itemSize;

        int IPythonBuffer.NumOfDims
            => _numDims;

        IReadOnlyList<int>? IPythonBuffer.Shape
            => _numDims > 0 && _flags.HasFlag(BufferFlags.ND) ? _shape : null;

        IReadOnlyList<int>? IPythonBuffer.Strides
            => !_isCContig ? _strides : null;

        IReadOnlyList<int>? IPythonBuffer.SubOffsets
            => null; // TODO: suboffsets not supported yet

        #endregion
    }
}
