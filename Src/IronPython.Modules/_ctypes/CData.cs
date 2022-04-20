// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

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
        public abstract class CData : IBufferProtocol, IPythonBuffer {
            internal MemoryHolder MemHolder {
                get => _memHolder ?? throw new InvalidOperationException($"{nameof(CData)} object not fully initialized.");
                set {
                    _memHolder?.Dispose();
                    _memHolder = value;
                }
            }
            private MemoryHolder? _memHolder;

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
                    ?? throw PythonOps.TypeErrorForBadInstance("{0} object does not have the buffer interface", data);
                    // Python 3.5: PythonOps.TypeErrorForBytesLikeTypeMismatch(data);

                IPythonBuffer buffer;
                try {
                    buffer = bp.GetBuffer(BufferFlags.Writable);
                } catch (BufferException ex) {
                    throw PythonOps.TypeError("{0}", ex.Message);
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
                    ?? throw PythonOps.TypeErrorForBadInstance("{0} object does not have the buffer interface", data);
                    // Python 3.5: PythonOps.TypeErrorForBytesLikeTypeMismatch(data);

                IPythonBuffer buffer;
                try {
                    buffer = bp.GetBuffer(BufferFlags.Simple);
                } catch (BufferException ex) {
                    throw PythonOps.TypeError("{0}", ex.Message);
                }
                try {
                    var span = buffer.AsReadOnlySpan();
                    ValidateArraySizes(span.Length, offset, size);
                    MemHolder = new MemoryHolder(size);
                    MemHolder.WriteSpan(0, span.Slice(offset, size));
                } finally {
                    buffer.Dispose();
                }
            }

            internal virtual PythonTuple GetBufferInfo()
                => PythonTuple.MakeTuple(NativeType.TypeFormat, 0, PythonTuple.EMPTY);


            #region IBufferProtocol Members

            IPythonBuffer IBufferProtocol.GetBuffer(BufferFlags flags) => this;

            void IDisposable.Dispose() { } // TODO

            object IPythonBuffer.Object => this;

            unsafe ReadOnlySpan<byte> IPythonBuffer.AsReadOnlySpan()
                => new Span<byte>(MemHolder.UnsafeAddress.ToPointer(), MemHolder.Size);

            unsafe Span<byte> IPythonBuffer.AsSpan()
                => new Span<byte>(MemHolder.UnsafeAddress.ToPointer(), MemHolder.Size);

            unsafe MemoryHandle IPythonBuffer.Pin()
                => new MemoryHandle(MemHolder.UnsafeAddress.ToPointer());

            int IPythonBuffer.Offset => 0;

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

            string IPythonBuffer.Format => NativeType.TypeFormat;

            [PythonHidden]
            public virtual int ItemSize => NativeType.Size;

            int IPythonBuffer.NumOfDims => Shape?.Count ?? (ItemCount > 1 ? 1 : 0);

            bool IPythonBuffer.IsReadOnly => false;

            [PythonHidden]
            public virtual IReadOnlyList<int>? Shape => null;

            IReadOnlyList<int>? IPythonBuffer.Strides => null;

            IReadOnlyList<int>? IPythonBuffer.SubOffsets => null;

            #endregion
        }
    }
}

#endif
