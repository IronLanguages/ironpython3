// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES
#pragma warning disable SYSLIB0004 // The Constrained Execution Region (CER) feature is not supported in .NET 5.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;

using IronPython.Runtime;

namespace IronPython.Modules {
    /// <summary>
    /// A wrapper around allocated memory to ensure it gets released and isn't accessed
    /// when it could be finalized.
    /// </summary>
    internal sealed class MemoryHolder : CriticalFinalizerObject, IDisposable {
        private readonly IntPtr _data;
        private readonly bool _ownsData;
        private readonly int _size;
        private readonly IPythonBuffer? _buffer;
        private readonly MemoryHandle _handle;
        private readonly MemoryHolder? _parent;
        private bool _disposeRequested;
        private bool _disposed;
        private int _numChildren;
        private PythonDictionary? _objects;

        /// <summary>
        /// Creates a new MemoryHolder and allocates a buffer of the specified size.
        /// The buffer will be released on a call to <see cref="Dispose"/> or by the finalizer.
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public MemoryHolder(int size) {
            RuntimeHelpers.PrepareConstrainedRegions();
            try {
            } finally {
                _size = size;
                _data = NativeFunctions.Calloc(checked((nuint)size));
                if (_data == IntPtr.Zero) {
                    GC.SuppressFinalize(this);
                    throw new OutOfMemoryException();
                }
                _ownsData = true;
            }
        }

        /// <summary>
        /// Creates a new MemoryHolder at the specified address which is not tracked
        /// by us and we will never free.
        /// Therefore no need to call <see cref="Dispose"/>.
        /// </summary>
        public MemoryHolder(IntPtr data, int size) {
            GC.SuppressFinalize(this);
            _data = data;
            _size = size;
        }

        /// <summary>
        /// Creates a new MemoryHolder at the specified address which will keep alive the 
        /// parent memory holder.
        /// </summary>
        public MemoryHolder(IntPtr data, int size, MemoryHolder parent) {
            GC.SuppressFinalize(this);
            _data = data;
            _parent = parent;
            parent._numChildren++;
            _objects = parent._objects;
            _size = size;
        }

        /// <summary>
        /// Creates a new MemoryHolder from a Python buffer
        /// that will be disposed on a call to <see cref="Dispose"/> or by the finalizer.
        /// </summary>
        public MemoryHolder(IPythonBuffer buffer, int offset, int size) {
            if (buffer.IsReadOnly) throw new ArgumentException("Buffer must be writable.");
            if (!buffer.IsCContiguous()) throw new ArgumentException("Buffer must be c-contiguous.");
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), size, "Non-negative number required.");
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Non-negative number required.");

            int bufSize = buffer.NumBytes();
            ReadOnlySpan<byte> memblock = buffer.AsSpan();
            if (memblock.Length != bufSize) new ArgumentException("Invalid buffer.");
            if (size > bufSize - offset) throw new ArgumentException("Requested memory block exceeds buffer boundaries.");


            _buffer = buffer;
            _handle = buffer.Pin();
            unsafe { _data = (IntPtr)_handle.Pointer + offset; }
            _size = size;
        }

        /// <summary>
        /// Gets the address of the held memory.  The caller should ensure the MemoryHolder
        /// is always alive and not disposed as long as the address will continue to be accessed.
        /// </summary>
        public IntPtr UnsafeAddress => !_disposed ? _data : IntPtr.Zero;

        public int Size => _size;

        /// <summary>
        /// Gets a list of objects which need to be kept alive for this MemoryHolder to be 
        /// remain valid.
        /// </summary>
        public PythonDictionary? Objects {
            get => _objects;
            set => _objects = value;
        }

        internal PythonDictionary EnsureObjects() {
            if (_objects is null) {
                Interlocked.CompareExchange(ref _objects, new PythonDictionary(), null);
            }
            return _objects;
        }

        /// <summary>
        /// Used to track the lifetime of objects when one memory region depends upon
        /// another memory region.  For example if you have an array of objects that
        /// each have an element which has it's own lifetime the array needs to keep
        /// the individual elements alive.
        /// 
        /// The keys used here match CPython's keys as tested by CPython's test_ctypes. 
        /// Typically they are a string which is the array index, "ffffffff" when
        /// from_buffer is used, or when it's a simple type there's just a string
        /// instead of the full dictionary - we store that under the key "str".
        /// </summary>
        internal void AddObject(object key, object value) {
            EnsureObjects()[key] = value;
        }

        private short Swap(short val) {
            return (short)((((ushort)val & 0xFF00) >> 8) | (((ushort)val & 0x00FF) << 8));
        }

        private int Swap(int val) {
            // swap adjacent 16-bit blocks
            val = (int)(((uint)val >> 16) | ((uint)val << 16));
            // swap adjacent 8-bit blocks
            return (int)((((uint)val & 0xFF00FF00) >> 8) | (((uint)val & 0x00FF00FF) << 8));
        }

        private long Swap(long val) {
            // swap adjacent 32-bit blocks
            val = (long)(((ulong)val >> 32) | ((ulong)val << 32));
            // swap adjacent 16-bit blocks
            val = (long)((((ulong)val & 0xFFFF0000FFFF0000) >> 16) | (((ulong)val & 0x0000FFFF0000FFFF) << 16));
            // swap adjacent 8-bit blocks
            return (long)((((ulong)val & 0xFF00FF00FF00FF00) >> 8) | (((ulong)val & 0x00FF00FF00FF00FF) << 8));
        }

        public byte ReadByte(int offset) {
            byte res = Marshal.ReadByte(_data, offset);
            GC.KeepAlive(this);
            return res;
        }

        public short ReadInt16(int offset, bool swap=false) {
            short res = Marshal.ReadInt16(_data, offset);
            GC.KeepAlive(this);
            if(swap) res = Swap(res);
            return res;
        }

        public int ReadInt32(int offset, bool swap=false) {
            int res = Marshal.ReadInt32(_data, offset);
            GC.KeepAlive(this);
            if(swap) res = Swap(res);
            return res;
        }

        public long ReadInt64(int offset, bool swap=false) {
            long res = Marshal.ReadInt64(_data, offset);
            GC.KeepAlive(this);
            if(swap) res = Swap(res);
            return res;
        }

        public IntPtr ReadIntPtr(int offset) {
            IntPtr res = Marshal.ReadIntPtr(_data, offset);
            GC.KeepAlive(this);
            return res;
        }

        public MemoryHolder ReadMemoryHolder(int offset) {
            IntPtr res = Marshal.ReadIntPtr(_data, offset);
            return new MemoryHolder(res, IntPtr.Size, this);
        }

        internal Bytes? ReadBytes(int offset) {
            try {
                return ReadBytes(_data, offset);
            } finally {
                GC.KeepAlive(this);
            }
        }

        internal string? ReadUnicodeString(int offset) {
            try {
                return Marshal.PtrToStringUni(_data.Add(offset));
            } finally {
                GC.KeepAlive(this);
            }
        }

        internal Bytes ReadBytes(int offset, int length) {
            try {
                return ReadBytes(_data, offset, length);
            } finally {
                GC.KeepAlive(this);
            }
        }

        internal static Bytes ReadBytes(IntPtr addr, int offset, int length) {
            // instead of Marshal.PtrToStringAnsi we do this because
            // ptrToStringAnsi gives special treatment to values >= 128.
            var buffer = new byte[length];
            if (checked(offset + length) < int.MaxValue) {
                for (int i = 0; i < length; i++) {
                    buffer[i] = Marshal.ReadByte(addr, offset + i);
                }
            }
            return Bytes.Make(buffer);
        }

        internal static Bytes? ReadBytes(IntPtr addr, int offset) {
            if (addr == IntPtr.Zero) return null;

            // instead of Marshal.PtrToStringAnsi we do this because
            // ptrToStringAnsi gives special treatment to values >= 128.
            MemoryStream res = new MemoryStream();
            byte b;
            while((b = Marshal.ReadByte(addr, offset++)) != 0) {
                res.WriteByte(b);
            }
            return Bytes.Make(res.ToArray());
        }

        internal string ReadUnicodeString(int offset, int length) {
            try {
                return Marshal.PtrToStringUni(_data.Add(offset), length);
            } finally {
                GC.KeepAlive(this);
            }
        }

        public void WriteByte(int offset, byte value) {
            Marshal.WriteByte(_data, offset, value);
            GC.KeepAlive(this);
        }

        public void WriteInt16(int offset, short value, bool swap=false) {
            Marshal.WriteInt16(_data, offset, swap ? Swap(value) : value);
            GC.KeepAlive(this);
        }

        public void WriteInt32(int offset, int value, bool swap=false) {
            Marshal.WriteInt32(_data, offset, swap ? Swap(value) : value);
            GC.KeepAlive(this);
        }

        public void WriteInt64(int offset, long value, bool swap=false) {
            Marshal.WriteInt64(_data, offset, swap ? Swap(value) : value);
            GC.KeepAlive(this);
        }

        public void WriteIntPtr(int offset, IntPtr value) {
            Marshal.WriteIntPtr(_data, offset, value);
            GC.KeepAlive(this);
        }

        public void WriteIntPtr(int offset, MemoryHolder address) {
            Marshal.WriteIntPtr(_data, offset, address.UnsafeAddress);
            GC.KeepAlive(this);
            GC.KeepAlive(address);
        }


        /// <summary>
        /// Copies the data in data into this MemoryHolder.
        /// </summary>
        public void CopyFrom(IntPtr source, nint size) {
            NativeFunctions.MemCopy(_data, source, checked((nuint)size));
            GC.KeepAlive(this);
        }

        internal void WriteUnicodeString(int offset, string value) {
            // TODO: There's gotta be a better way to do this
            for (int i = 0; i < value.Length; i++) {
                WriteInt16(checked(offset + i * 2), (short)value[i]);
            }
        }

        internal void WriteSpan(int offset, ReadOnlySpan<byte> value) {
            unsafe {
                var dest = new Span<byte>((void*)_data, _size).Slice(offset);
                value.CopyTo(dest);
            }
            GC.KeepAlive(this);
        }

        public MemoryHolder GetSubBlock(int offset) {
            // No GC.KeepAlive here because the new MemoryHolder holds onto the previous one.
            return new MemoryHolder(_data.Add(offset), _size - offset, this);
        }

        /// <summary>
        /// Copies memory from one location to another keeping the associated memory holders alive during the
        /// operation.
        /// </summary>
        public void CopyTo(MemoryHolder/*!*/ destAddress, int writeOffset, int size) {
            NativeFunctions.MemCopy(destAddress._data.Add(writeOffset), _data, checked((nuint)size));
            GC.KeepAlive(destAddress);
            GC.KeepAlive(this);
        }

        private void ReleaseChild() {
            Debug.Assert(_numChildren >= 0);
            _numChildren--;
            if (_disposeRequested) Dispose();
        }

        public void Dispose() {
            _disposeRequested = true;
            if (!_disposed && _numChildren == 0) {
                _disposed = true;

                if (_ownsData) {
                    Marshal.FreeHGlobal(_data);
                }
                _handle.Dispose();
                _buffer?.Dispose();
                _parent?.ReleaseChild();

                GC.SuppressFinalize(this);
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        ~MemoryHolder() {
            if (!_disposed) {
                _disposed = true;

                if (_ownsData) {
                    Marshal.FreeHGlobal(_data);
                }
                try { _handle.Dispose(); } catch { /*ignore*/ }
                try { _buffer?.Dispose(); } catch { /*ignore*/ }
                try { _parent?.ReleaseChild(); } catch { /*ignore*/ }
            }
        }
    }
}

#endif
