// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;

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
        Writable      = 0x0001,

        /// <summary>
        /// The consumer expects the item format to be reported and is prepared to deal with items that are more complex than a byte.
        /// </summary>
        Format        = 0x0004,

        /// <summary>
        /// An N-dimensional array, 0 &lt;= N &lt;= 64. The dimensions are reported as shape.
        /// </summary>
        ND            = 0x0008,

        /// <summary>
        /// An N-dimensional array with strides, so data may not be memory contiguous.
        /// However, it will fit in a contiguous memory range. 
        /// </summary>
        Strides       = 0x0010 | ND,

        /// <summary>
        /// An N-dimensional array with strides, so it may not be memory contiguous.
        /// The last dimension is contiguous.
        /// </summary>
        CContiguous = 0x0020 | Strides,

        /// <summary>
        /// An N-dimensional array with strides, so it may not be memory contiguous.
        /// The first dimension is contiguous.
        /// </summary>
        FContiguous   = 0x0040 | Strides,

        /// <summary>
        /// The client is prepared to deal with either <see cref="CContiguous"/> or <see cref="FContiguous"/>.
        /// </summary>
        AnyContiguous = 0x0080 | Strides,

        /// <summary>
        /// An N-dimensional array that has a complex structure, where higher dimensions
        /// may contains pointers to memory blocks holding lower dimenstions.
        /// </summary>
        Indirect      = 0x0100 | Strides,

        #region Flag combinations for common requests

        /// <summary>
        /// Unformmatted (i.e. byte-oriented) read-only blob with no structure.
        /// </summary>
        Simple = 0,

        /// <summary>
        /// Contiguous writable buffer of any dimension.
        /// </summary>
        Contig    = ND | Writable,

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

        Full      = Indirect | Writable | Format,
        FullRO    = Indirect | Format,

        #endregion
    }

    /// <summary>
    /// Provides low-level read-write access to byte data of the underlying object.
    /// </summary>
    /// <remarks>
    /// 1. The buffer should be disposed after usage. Failing to dispose a buffer
    /// instance will render the underlying object permanently locked in an export state.
    ///
    /// 2. <see cref="IDisposable.Dispose()"/> can be called multiple times (subsequent cals being no-op).
    /// In multi-thread use scanarios, the client has to ensure that the call to Dispose()
    /// happens only after all calls to other methods are terminated.
    ///
    /// 3. The buffer provides low-level byte acces to the exported data
    /// that may not be synchronized for multiper read-write threads,
    /// even if the exporter itself is a synchronized object.
    /// The client can ensure proper synchronization by locking on <see cref="IPythonBuffer.Object"/>.
    /// </remarks>
    public interface IPythonBuffer : IDisposable {
        /// <summary>
        /// A reference to the exporting object.
        /// </summary>
        object Object { get;  }

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
        /// If Format is null (indicating unformmatted access), it matches the original element type.
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
}
