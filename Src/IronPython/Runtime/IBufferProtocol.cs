// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;

namespace IronPython.Runtime {
    public interface IBufferProtocol {
        object GetItem(int index);
        void SetItem(int index, object value);
        void SetSlice(Slice index, object value);

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
    }
}
