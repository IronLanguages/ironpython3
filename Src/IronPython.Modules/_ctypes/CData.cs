// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Types;
using System.Collections.Generic;
using System.Linq;

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
            internal MemoryHolder _memHolder;

            // members: __setstate__,  __reduce__ _b_needsfree_ __ctypes_from_outparam__ __hash__ _objects _b_base_ __doc__
            protected CData() {
            }

            public int Size {
                [PythonHidden]
                get {
                    // TODO: What if a user directly subclasses CData?
                    return NativeType.Size;
                }
            }

            // TODO: Accesses via Ops class
            public IntPtr UnsafeAddress {
                [PythonHidden]
                get {
                    return _memHolder.UnsafeAddress;
                }
            }

            private byte[] GetBytes(int offset, int length) {
                int maxLen = checked(offset + length);
                byte[] res = new byte[length];
                for (int i = offset; i < maxLen; i++) {
                    res[i - offset] = _memHolder.ReadByte(i);
                }
                return res;
            }

            internal INativeType NativeType {
                get {
                    return (INativeType)DynamicHelpers.GetPythonType(this);
                }
            }

            public virtual object _objects {
                get {
                    return _memHolder.Objects;
                }
            }

            internal void SetAddress(IntPtr address) {
                Debug.Assert(_memHolder == null);
                _memHolder = new MemoryHolder(address, NativeType.Size);
            }

            internal virtual PythonTuple GetBufferInfo() {
                return PythonTuple.MakeTuple(
                    NativeType.TypeFormat,
                    0,
                    PythonTuple.EMPTY
                );
            }


            #region IBufferProtocol Members

            IPythonBuffer IBufferProtocol.GetBuffer(BufferFlags flags) {
                return this;
            }

            void IDisposable.Dispose() { }

            object IPythonBuffer.Object => this;

            unsafe ReadOnlySpan<byte> IPythonBuffer.AsReadOnlySpan()
                => new Span<byte>(_memHolder.UnsafeAddress.ToPointer(), _memHolder.Size);

            unsafe Span<byte> IPythonBuffer.AsSpan()
                => new Span<byte>(_memHolder.UnsafeAddress.ToPointer(), _memHolder.Size);

            int IPythonBuffer.Offset => 0;

            public virtual int ItemCount {
                [PythonHidden]
                get {
                    return 1;
                }
            }

            string IPythonBuffer.Format {
                get { return NativeType.TypeFormat; }
            }

            // TODO: change sig
            public virtual BigInteger ItemSize {
                [PythonHidden]
                get { return this.NativeType.Size; }
            }

            int IPythonBuffer.ItemSize => (int)this.ItemSize;

            int IPythonBuffer.NumOfDims {
                get {
                    return GetShape(0, null)?.Count ?? (ItemCount > 1 ? 1 : 0);
                }
            }

            bool IPythonBuffer.IsReadOnly {
                get { return false; }
            }

            // TODO: change sig
            [PythonHidden]
            public virtual IList<BigInteger> GetShape(int start, int? end) {
                return null;
            }

            IReadOnlyList<int> IPythonBuffer.Shape => GetShape(0, null)?.Select(big => (int)big).ToArray();  // TODO: remove using Linq when done with sig change


            IReadOnlyList<int> IPythonBuffer.Strides {
                get { return null; }
            }

            IReadOnlyList<int> IPythonBuffer.SubOffsets {
                get { return null; }
            }

            Bytes IPythonBuffer.ToBytes(int start, int? end) {
                return Bytes.Make(GetBytes(start, NativeType.Size));
            }

            ReadOnlyMemory<byte> IPythonBuffer.ToMemory() {
                return GetBytes(0, NativeType.Size);
            }

            #endregion
        }
    }
}
#endif
