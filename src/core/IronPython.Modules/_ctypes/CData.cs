// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType _SimpleCData = SimpleType.MakeSystemType(typeof(SimpleCData));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType CFuncPtr = CFuncPtrType.MakeSystemType(typeof(_CFuncPtr));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType Structure = StructType.MakeSystemType(typeof(_Structure));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType Union = UnionType.MakeSystemType(typeof(_Union));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType _Pointer = PointerType.MakeSystemType(typeof(Pointer));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType Array = ArrayType.MakeSystemType(typeof(_Array));

        /// <summary>
        /// Base class for all ctypes interop types.
        /// </summary>
        [PythonType("_CData"), PythonHidden]
        public abstract class CData : IBufferProtocol, IDisposable {
            internal MemoryHolder MemHolder {
                get => _memHolder ?? throw new InvalidOperationException($"{nameof(CData)} object not fully initialized.");
                set {
                    _memHolder?.Dispose();
                    _memHolder = value;
                }
            }
            private MemoryHolder? _memHolder;
            private bool _disposed;

            // members: __setstate__,  __reduce__ _b_needsfree_ __ctypes_from_outparam__ __hash__ _objects _b_base_ __doc__
            protected CData() { }

            // TODO: What if a user directly subclasses CData?
            [PythonHidden]
            public int Size => NativeType.Size;

            // TODO: Accesses via Ops class
            [PythonHidden]
            public IntPtr UnsafeAddress => MemHolder.UnsafeAddress;

            internal INativeType NativeType => (INativeType)DynamicHelpers.GetPythonType(this);

            public virtual object? _objects => MemHolder.Objects;

            internal void SetAddress(IntPtr address) {
                // TODO: Debug.Assert(_memHolder == null); // fails for structures
                MemHolder = new MemoryHolder(address, NativeType.Size);
            }

            internal void InitializeFromBuffer(object? data, int offset, int size) {
                var bp = data as IBufferProtocol
                    ?? throw PythonOps.TypeErrorForBytesLikeTypeMismatch(data);

                IPythonBuffer buffer = bp.GetBuffer(BufferFlags.FullRO);
                if (buffer.IsReadOnly) {
                    buffer.Dispose();
                    throw PythonOps.TypeError("underlying buffer is not writable");
                }
                if (!buffer.IsCContiguous()) {
                    buffer.Dispose();
                    throw PythonOps.TypeError("underlying buffer is not C contiguous");
                }

                try {
                    ValidateArraySizes(buffer.NumBytes(), offset, size);
                    MemHolder = new MemoryHolder(buffer, offset, size);
                } catch {
                    buffer.Dispose();
                    throw;
                }
                MemHolder.AddObject("ffffffff", buffer.Object);
            }

            internal void InitializeFromBufferCopy(object? data, int offset, int size) {
                var bp = data as IBufferProtocol
                    ?? throw PythonOps.TypeErrorForBytesLikeTypeMismatch(data);

                using IPythonBuffer buffer = bp.GetBuffer();
                var span = buffer.AsReadOnlySpan();
                ValidateArraySizes(span.Length, offset, size);
                MemHolder = new MemoryHolder(size);
                MemHolder.WriteSpan(0, span.Slice(offset, size));
            }

            internal virtual PythonTuple GetBufferInfo()
                => PythonTuple.MakeTuple(NativeType.TypeFormat, 0, PythonTuple.EMPTY);

            [PythonHidden]
            public void Dispose() {
                if (!_disposed) {
                    _disposed = true;
                    if (_numExports == 0) {
                        MemoryHolder? holder = Interlocked.Exchange(ref _memHolder, null);
                        holder?.Dispose();
                    }
                }
            }

            #region IBufferProtocol

            private int _numExports;

            IPythonBuffer IBufferProtocol.GetBuffer(BufferFlags flags, bool throwOnError) {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);
                _ = MemHolder; // check if fully initialized
                Interlocked.Increment(ref _numExports);
                return new CDataView(this);
            }

            // May be executed concurremtly with Dispose(), GetBuffer(), or another ReleaseBuffer()
            private void ReleaseBuffer() {
                Debug.Assert(_numExports > 0);
                int cnt = Interlocked.Decrement(ref _numExports);
                if (cnt == 0 && _disposed) {
                    MemoryHolder? holder = Interlocked.Exchange(ref _memHolder, null);
                    holder?.Dispose();
                }
            }

            [PythonHidden]
            public virtual int ItemCount {
                get {
                    var shape = Shape;
                    if (shape is null) return 1;
                    var count = 1;
                    foreach (var item in shape) {
                        count *= item;
                    }
                    return count;
                }
            }

            [PythonHidden]
            public virtual int ItemSize => NativeType.Size;

            [PythonHidden]
            public virtual IReadOnlyList<int>? Shape => null;


            private sealed class CDataView : IPythonBuffer {
                private CData? _cdata;
                private CData CData => _cdata ?? throw new ObjectDisposedException(nameof(CDataView));

                internal CDataView(CData cdata) => _cdata = cdata;

                public void Dispose() {
                    CData? cdata = Interlocked.Exchange(ref _cdata, null);
                    cdata?.ReleaseBuffer();
                }

                public object Object => CData;

                unsafe ReadOnlySpan<byte> IPythonBuffer.AsReadOnlySpan()
                    => new Span<byte>(CData.MemHolder.UnsafeAddress.ToPointer(), CData.MemHolder.Size);

                unsafe Span<byte> IPythonBuffer.AsSpan()
                    => new Span<byte>(CData.MemHolder.UnsafeAddress.ToPointer(), CData.MemHolder.Size);

                unsafe MemoryHandle IPythonBuffer.Pin()
                    => new MemoryHandle(CData.MemHolder.UnsafeAddress.ToPointer());

                public int Offset => 0;

                public int ItemCount => CData.ItemCount;

                public string Format => CData.NativeType.TypeFormat;

                public int ItemSize => CData.ItemSize;

                public int NumOfDims => Shape?.Count ?? (ItemCount > 1 ? 1 : 0);

                public bool IsReadOnly => false;

                public IReadOnlyList<int>? Shape => CData.Shape;

                IReadOnlyList<int>? IPythonBuffer.Strides => null;

                IReadOnlyList<int>? IPythonBuffer.SubOffsets => null;
            }

            #endregion
        }
    }
}

#endif
