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
        Simple        = 0,
        Writable      = 0x0001,
        Format        = 0x0004,
        ND            = 0x0008,

        Strides       = 0x0010 | ND,
        CContiguous   = 0x0020 | Strides,
        FContiguous   = 0x0040 | Strides,
        AnyContiguous = 0x0080 | Strides,
        Indirect      = 0x0100 | Strides,

        Contig    = ND | Writable,
        ContigRO  = ND,

        Strided   = Strides | Writable,
        StridedRO = Strides,

        Records   = Strides | Writable | Format,
        RecordsRO = Strides | Format,

        Full      = Indirect | Writable | Format,
        FullRO    = Indirect | Format,
    }

    public interface IPythonBuffer : IDisposable {
        object GetItem(int index);
        void SetItem(int index, object value);

        int ItemCount { get; }

        string Format { get; }

        BigInteger ItemSize { get; }

        BigInteger NumberDimensions { get; }

        bool ReadOnly { get; }

        IList<BigInteger> GetShape(int start, int? end);

        PythonTuple Strides { get; }

        PythonTuple? SubOffsets { get; }

        Bytes ToBytes(int start, int? end);

        PythonList ToList(int start, int? end);

        ReadOnlyMemory<byte> ToMemory();

        ReadOnlySpan<byte> AsReadOnlySpan();

        Span<byte> AsSpan();
    }
}
