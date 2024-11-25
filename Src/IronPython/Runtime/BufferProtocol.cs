// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using IronPython.Runtime.Exceptions;

namespace IronPython.Runtime {
    /// <summary>
    /// Equivalent functionality of CPython's <a href="https://docs.python.org/3/c-api/buffer.html">Buffer Protocol</a>.
    /// </summary>
    public interface IBufferProtocol {
        IPythonBuffer GetBuffer(BufferFlags flags = BufferFlags.Simple);
    }

    /// <summary>
    /// Used to specify what kind of buffer the consumer is prepared to deal with
    /// and therefore what kind of buffer the exporter is allowed to return.
    /// </summary>
    /// <seealso href="https://docs.python.org/3/c-api/buffer.html#buffer-request-types"/>
    [Flags]
    public enum BufferFlags {
        /// <summary>
        /// The buffer must be writable. The consumer cannot handle read-only buffers.
        /// </summary>
        Writable = 0x0001,

        /// <summary>
        /// The consumer expects the item format to be reported and is prepared to deal with items that are more complex than a byte.
        /// </summary>
        /// <remarks>
        /// If format is requested, the consumer must also indicate preparedness to handle shape (<see cref="ND"/> flag set).
        /// </remarks>
        Format = 0x0004,

        /// <summary>
        /// An N-dimensional array, 0 &lt;= N &lt;= 64. The dimensions are reported as shape.
        /// </summary>
        ND = 0x0008,

        /// <summary>
        /// An N-dimensional array with strides, so data may not be memory contiguous.
        /// However, it will fit in a contiguous memory range. 
        /// </summary>
        Strides = 0x0010 | ND,

        /// <summary>
        /// An N-dimensional array with strides, so it may not be memory contiguous.
        /// The last dimension is contiguous.
        /// </summary>
        CContiguous = 0x0020 | Strides,

        /// <summary>
        /// An N-dimensional array with strides, so it may not be memory contiguous.
        /// The first dimension is contiguous.
        /// </summary>
        FContiguous = 0x0040 | Strides,

        /// <summary>
        /// The client is prepared to deal with either <see cref="CContiguous"/> or <see cref="FContiguous"/>.
        /// </summary>
        AnyContiguous = 0x0080 | Strides,

        /// <summary>
        /// An N-dimensional array that has a complex structure, where higher dimensions
        /// may contains pointers to memory blocks holding lower dimensions.
        /// </summary>
        Indirect = 0x0100 | Strides,

        #region Flag combinations for common requests

        /// <summary>
        /// Unformatted (i.e. byte-oriented) read-only blob with no structure.
        /// </summary>
        Simple = 0,

        /// <summary>
        /// Contiguous writable buffer of any dimension.
        /// </summary>
        Contig = ND | Writable,

        /// <summary>
        /// Contiguous buffer of any dimension.
        /// </summary>
        ContigRO = ND,

        /// <summary>
        /// Writable byte buffer of any dimension, possibly non-contiguous, but with no embedded pointers.
        /// </summary>
        Strided = Strides | Writable,

        /// <summary>
        /// Byte buffer of any dimension, possibly non-contiguous, but with no embedded pointers.
        /// </summary>
        StridedRO = Strides,

        /// <summary>
        /// Writable buffer of any dimension, possibly non-contiguous, but with no embedded pointers.
        /// Elements can be any struct type.
        /// </summary>
        Records = Strides | Writable | Format,

        /// <summary>
        /// Buffer of any dimension, possibly non-contiguous, but with no embedded pointers.
        /// Elements can be any struct type.
        /// </summary>
        RecordsRO = Strides | Format,

        Full = Indirect | Writable | Format,
        FullRO = Indirect | Format,

        #endregion
    }

    internal static class BufferProtocolExtensions {
        internal static IPythonBuffer? GetBufferNoThrow(this IBufferProtocol bufferProtocol, BufferFlags flags = BufferFlags.Simple) {
            try {
                return bufferProtocol.GetBuffer(flags);
            } catch (BufferException) {
                return null;
            }
        }
    }

    /// <summary>
    /// Provides low-level read-write access to byte data of the underlying object.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>
    /// The buffer should be disposed after usage. Failing to dispose a buffer
    /// instance will render the underlying object permanently locked in an export state.
    /// </item>
    /// <item>
    /// <see cref="IDisposable.Dispose()"/> can be called multiple times (subsequent calls being no-op).
    /// In multi-thread use scenarios, the client has to ensure that the call to Dispose()
    /// happens only after all calls to other methods are terminated.
    /// </item>
    /// <item>
    /// The buffer provides low-level byte access to the exported data
    /// that may not be synchronized for multiple read-write threads
    /// even if the exporter itself is a synchronized object.
    /// The client can ensure proper synchronization by locking on <see cref="IPythonBuffer.Object"/>.
    /// </item>
    /// <item>
    /// The buffer can be pinned (fixed) by obtaining a memory handle by calling method <see cref="Pin"/>.
    /// The client should make sure that all obtained memory handles are disposed before the buffer itself is disposed.
    /// </item>
    /// </list>
    /// </remarks>
    public interface IPythonBuffer : IDisposable {
        /// <summary>
        /// A reference to the exporting object.
        /// </summary>
        object Object { get; }

        /// <summary>
        /// Indicates whether only read access is permitted.
        /// For writable buffers this is false.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Read-only access to binary buffer data. Apart from indirect buffers, all of buffer data lies within the span,
        /// although if strides are used, not all of span data have to be buffer data.
        /// </summary>
        ReadOnlySpan<byte> AsReadOnlySpan();

        /// <summary>
        /// Writable access to the same data as provided by <see cref="AsReadOnlySpan"/>.
        /// May only be used if <see cref="IsReadOnly"/> is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if accessed on a read-only buffer (<see cref="IsReadOnly"/> is true).
        /// </exception>
        Span<byte> AsSpan();

        /// <summary>
        /// Creates a handle for the buffer memory exposed through <see cref="AsReadOnlySpan"/>.
        /// If the buffer is backed by managed memory, the garbage collector will not move the memory
        /// until the returned handle is disposed. This enables you to retrieve and use the buffers's address.
        /// </summary>
        /// <returns>A handle for the <see cref="IPythonBuffer"/> object.</returns>
        /// <exception cref="BufferException">
        /// Buffers backed by memory on the stack cannot be pinned.
        /// </exception>
        MemoryHandle Pin();

        /// <summary>
        /// Offset (in bytes) of the first logical element in the buffer with respect to the beginning of
        /// the byte span. For non-strided buffers it is always 0.
        /// </summary>
        int Offset { get; }

        /// <summary>
        /// String describing the format of a single element, using element codes as defined in the 'struct' module.
        /// Value null implies unformatted, byte-oriented data access.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.format"/>
        /// <seealso href="https://docs.python.org/3/library/struct.html#format-strings"/>
        string? Format { get; }

        /// <summary>
        /// Total number of elements in the buffer.
        /// Equal to a product of all dimension lengths as defined by <see cref="Shape"/>, if Shape is not null.
        /// </summary>
        /// <remarks>
        /// Can be used to calculate number of bytes of data in the buffer (len).
        /// (equal to ItemCount * ItemSize).
        /// </remarks>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.len"/>
        int ItemCount { get; }

        /// <summary>
        /// Size in bytes of one element. It matches the element <see cref="Format"/>.
        /// If Format is null (indicating unformatted access), it matches the original element type.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.itemsize"/>
        int ItemSize { get; }

        /// <summary>
        /// Number of dimensions of the buffer (e.g. array rank).
        /// 0 for single values, 1 for simple arrays, 2 and more for ND-arrays.
        /// Maximum number of dimensions is 64.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.ndim"/>
        int NumOfDims { get; }

        /// <summary>
        /// A list of number of elements in each dimension.
        /// If null, it implies a scalar or a simple 1-dimensional array.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.shape"/>
        IReadOnlyList<int>? Shape { get; }

        /// <summary>
        /// For each dimension, a value indicating how many bytes to skip to get to the next element.
        /// If null, it implies a scalar or a contiguous n-dimensional array.
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.strides"/>
        IReadOnlyList<int>? Strides { get; }

        /// <summary>
        /// For each dimension, it provides information how to find the start of data in its subdimension.
        /// If null, it implies that all buffer data lies within the span provided by <see cref="AsReadOnlySpan()"/>
        /// and can be accessed by simple offset calculations (no pointer dereferencing).
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.suboffsets"/>
        IReadOnlyList<int>? SubOffsets { get; }
    }

    public static class PythonBufferExtensions {
        /// <summary>
        /// Number of bytes of data in the buffer (len).
        /// </summary>
        /// <seealso href="https://docs.python.org/3/c-api/buffer.html#c.Py_buffer.len"/>
        public static int NumBytes(this IPythonBuffer buffer)
            => buffer.ItemCount * buffer.ItemSize;

        public static BufferBytesEnumerator EnumerateBytes(this IPythonBuffer buffer)
            => new BufferBytesEnumerator(buffer);

        public static BufferEnumerator EnumerateItemData(this IPythonBuffer buffer)
            => new BufferEnumerator(buffer, chunkSize: buffer.ItemSize);

        /// <summary>
        /// Checks if the data in buffer uses a contiguous memory block. If the buffer uses more than one dimension,
        /// the data is organized according to the C multi-dimensional array layout.
        /// </summary>
        /// <remarks>
        /// This does not directly correspond to the <see cref="BufferFlags.CContiguous"/> flag.
        /// Buffers requested with purely such flag are only guaranteed to have contiguous data in the lowest dimension.
        /// Flag <see cref="BufferFlags.Contig"/>, on the other hand, does guarante a fully C-contiguous data block.
        /// </remarks>
        public static bool IsCContiguous(this IPythonBuffer buffer) {
            Debug.Assert(buffer.Strides != null || buffer.Offset == 0);
            return buffer.Strides == null;
        }

        public static byte[] ToArray(this IPythonBuffer buffer) {
            if (buffer.IsCContiguous()) {
                return buffer.AsReadOnlySpan().ToArray();
            } else {
                var bytes = new byte[buffer.NumBytes()];
                int i = 0;
                foreach (byte b in buffer.EnumerateBytes()) {
                    bytes[i++] = b;
                }
                return bytes;
            }
        }

        public static void CopyTo(this IPythonBuffer buffer, Span<byte> dest) {
            if (buffer.IsCContiguous()) {
                buffer.AsReadOnlySpan().CopyTo(dest);
            } else {
                int i = 0;
                foreach (byte b in buffer.EnumerateBytes()) {
                    dest[i++] = b;
                }
            }
        }

        /// <summary>
        /// Obtain the underlying array, if possible.
        /// The returned array is unsafe because it should not be written to.
        /// </summary>
        internal static byte[]? AsUnsafeArray(this IPythonBuffer buffer) {
            if (!buffer.IsCContiguous())
                return null;

            ReadOnlySpan<byte> bufdata = buffer.AsReadOnlySpan();
            if (buffer.Object is Bytes b) {
                if (b.UnsafeByteArray.AsSpan() == bufdata)
                    return b.UnsafeByteArray;
            } else if (buffer.Object is ByteArray ba) {
                byte[] arrdata = ba.UnsafeByteList.Data;
                if (arrdata.AsSpan() == bufdata)
                    return arrdata;
            } else if (buffer.Object is Memory<byte> mem) {
                if (MemoryMarshal.TryGetArray(mem, out ArraySegment<byte> seg) && seg.Array is not null && seg.Array.AsSpan() == bufdata)
                    return seg.Array;
            } else if (buffer.Object is ReadOnlyMemory<byte> rom) {
                if (MemoryMarshal.TryGetArray(rom, out ArraySegment<byte> seg) && seg.Array is not null && seg.Array.AsSpan() == bufdata)
                    return seg.Array;
            }

            return null;
        }

        /// <summary>
        /// Obtain the underlying writable array, if possible.
        /// The returned array is unsafe because it can be longer than the buffer.
        /// </summary>
        internal static byte[]? AsUnsafeWritableArray(this IPythonBuffer buffer) {
            if (!buffer.IsCContiguous() || buffer.IsReadOnly)
                return null;

            Span<byte> bufdata = buffer.AsSpan();
            if (buffer.Object is ByteArray ba) {
                byte[] arrdata = ba.UnsafeByteList.Data;
                if (UseSameMemory(arrdata, bufdata))
                    return arrdata;
            } else if (buffer.Object is Memory<byte> mem) {
                if (MemoryMarshal.TryGetArray(mem, out ArraySegment<byte> seg) && seg.Array is not null && UseSameMemory(seg.Array, bufdata))
                    return seg.Array;
            }

            return null;
        }

        private static bool UseSameMemory(byte[] arr, ReadOnlySpan<byte> span)
            => arr.Length >= span.Length && arr.AsSpan(0, span.Length) == span;
    }

    public ref struct BufferBytesEnumerator {
        private readonly BufferEnumerator _enumerator;

        public BufferBytesEnumerator(IPythonBuffer buffer)
            => _enumerator = new BufferEnumerator(buffer, chunkSize: 1);

        public byte Current => _enumerator.Current[0];
        public bool MoveNext() => _enumerator.MoveNext();
        public void Dispose() => _enumerator.Dispose();

        public BufferBytesEnumerator GetEnumerator() => this;
    }

    public ref struct BufferEnumerator {
        private readonly int _chunksize;
        private readonly ReadOnlySpan<byte> _span;
        private readonly IEnumerator<int> _offsets;

        public BufferEnumerator(IPythonBuffer buffer, int chunkSize) {
            if (buffer.SubOffsets != null)
                throw new NotImplementedException("buffers with suboffsets are not supported");

            _chunksize = chunkSize;
            _span = buffer.AsReadOnlySpan();
            _offsets = EnumerateDimension(buffer, buffer.Offset, chunkSize, 0).GetEnumerator();
        }

        public ReadOnlySpan<byte> Current => _span.Slice(_offsets.Current, _chunksize);
        public bool MoveNext() => _offsets.MoveNext();
        public void Dispose() => _offsets.Dispose();

        public BufferEnumerator GetEnumerator() => this;

        private static IEnumerable<int> EnumerateDimension(IPythonBuffer buffer, int ofs, int step, int dim) {
            IReadOnlyList<int>? shape = buffer.Shape;
            IReadOnlyList<int>? strides = buffer.Strides;

            if (shape == null || strides == null) {
                // simple C-contiguous case
                Debug.Assert(buffer.Offset == 0);
                int len = buffer.NumBytes();
                for (int i = 0; i < len; i += step) {
                    yield return i;
                }
            } else if (dim >= shape.Count) {
                // iterate individual element (scalar)
                for (int i = 0; i < buffer.ItemSize; i += step) {
                    yield return ofs + i;
                }
            } else {
                for (int i = 0; i < shape[dim]; i++) {
                    // iterate all bytes from a subdimension
                    foreach (int j in EnumerateDimension(buffer, ofs, step, dim + 1)) {
                        yield return j;
                    }
                    ofs += strides[dim];
                }
            }
        }
    }
}
