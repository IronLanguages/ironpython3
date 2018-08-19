// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [PythonType("memoryview")]
    public sealed class MemoryView : ICodeFormattable {
        private readonly IBufferProtocol _buffer;
        private readonly int _start;
        private readonly int? _end;

        public MemoryView(IBufferProtocol @object) {
            _buffer = @object;
        }

        private MemoryView(IBufferProtocol @object, int start, int? end) {
            _buffer = @object;
            _start = start;
            _end = end;
        }

        public int __len__() {
            if (_end != null) {
                return _end.Value - _start;
            }
            return _buffer.ItemCount;
        }

        public string format {
            get { return _buffer.Format; }
        }

        public BigInteger itemsize {
            get { return _buffer.ItemSize; }
        }

        public BigInteger ndim {
            get { return _buffer.NumberDimensions; }
        }

        public bool @readonly {
            get { return _buffer.ReadOnly; }
        }

        public PythonTuple shape {
            get {
                var shape = _buffer.GetShape(_start, _end);
                if (shape == null) {
                    return null;
                }
                return new PythonTuple(shape); 
            }
        }

        public PythonTuple strides {
            get { return _buffer.Strides; }
        }

        public object suboffsets {
            get { return _buffer.SubOffsets; }
        }

        public Bytes tobytes() {
            return _buffer.ToBytes(_start, _end);
        }

        public List tolist() {
            return _buffer.ToList(_start, _end);
        }

        public object this[int index] {
            get {
                index = PythonOps.FixIndex(index, __len__());
                return _buffer.GetItem(index + _start);
            }
            set {
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }
                index = PythonOps.FixIndex(index, __len__());
                _buffer.SetItem(index + _start, value);
            }
        }

        public void __delitem__(int index) {
            if (_buffer.ReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public void __delitem__([NotNull]Slice slice) {
            if (_buffer.ReadOnly) {
                throw PythonOps.TypeError("cannot modify read-only memory");
            }
            throw PythonOps.TypeError("cannot delete memory");
        }

        public object this[[NotNull]Slice slice] {
            get {
                int start, stop;
                FixSlice(slice, __len__(), out start, out stop);

                return new MemoryView(_buffer, _start + start, _start + stop);
            }
            set {
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }

                int start, stop;
                FixSlice(slice, __len__(), out start, out stop);

                int newLen = PythonOps.Length(value);
                if (stop - start != newLen) {
                    throw PythonOps.ValueError("cannot resize memory view");
                }

                _buffer.SetSlice(new Slice(_start + start, _start + stop), value);
            }
        }

        /// <summary>
        /// MemoryView slicing is somewhat different and more restricted than
        /// standard slicing.
        /// </summary>
        public static void FixSlice(Slice slice, int len, out int start, out int stop) {
            if (slice.step != null) {
                throw PythonOps.NotImplementedError("");
            }

            int step;
            slice.indices(len, out start, out stop, out step);

            if (stop < start) {
                // backwards iteration is interpreted as empty slice
                stop = start;
            }
        }

        public static bool operator >(MemoryView self, IBufferProtocol other) {
            return self > new MemoryView(other);
        }

        public static bool operator >(IBufferProtocol self, MemoryView other) {
            return new MemoryView(self) > other;
        }

        public static bool operator >(MemoryView self, MemoryView other) {
            if ((object)self == null) {
                return (object)other != null;
            } else if ((object)other == null) {
                return true;
            }
            return self.tobytes() > other.tobytes();
        }

        public static bool operator <(MemoryView self, MemoryView other) {
            if ((object)self == null) {
                return (object)other == null;
            } else if ((object)other == null) {
                return false;
            }
            return self.tobytes() < other.tobytes();
        }

        public static bool operator <(MemoryView self, IBufferProtocol other) {
            return self < new MemoryView(other);
        }

        public static bool operator <(IBufferProtocol self, MemoryView other) {
            return new MemoryView(self) < other;
        }

        public static bool operator >=(MemoryView self, MemoryView other) {
            if ((object)self == null) {
                return (object)other == null;
            } else if ((object)other == null) {
                return false;
            }
            return self.tobytes() >= other.tobytes();
        }

        public static bool operator >=(MemoryView self, IBufferProtocol other) {
            return self >= new MemoryView(other);
        }

        public static bool operator >=(IBufferProtocol self, MemoryView other) {
            return new MemoryView(self) >= other;
        }

        public static bool operator <=(MemoryView self, MemoryView other) {
            if ((object)self == null) {
                return (object)other != null;
            } else if ((object)other == null) {
                return true;
            }
            return self.tobytes() <= other.tobytes();
        }

        public static bool operator <=(MemoryView self, IBufferProtocol other) {
            return self <= new MemoryView(other);
        }

        public static bool operator <=(IBufferProtocol self, MemoryView other) {
            return new MemoryView(self) <= other;
        }

        public static bool operator ==(MemoryView self, MemoryView other) {
            if ((object)self == null) {
                return (object)other == null;
            } else if ((object)other == null) {
                return false;
            }
            return self.tobytes().Equals(other.tobytes());
        }

        public static bool operator ==(MemoryView self, IBufferProtocol other) {
            return self == new MemoryView(other);
        }

        public static bool operator ==(IBufferProtocol self, MemoryView other) {
            return new MemoryView(self) == other;
        }

        public static bool operator !=(MemoryView self, MemoryView other) {
            if ((object)self == null) {
                return (object)other != null;
            } else if ((object)other == null) {
                return true;
            }
            return !self.tobytes().Equals(other.tobytes());
        }

        public static bool operator !=(MemoryView self, IBufferProtocol other) {
            return self != new MemoryView(other);
        }

        public static bool operator !=(IBufferProtocol self, MemoryView other) {
            return new MemoryView(self) != other;
        }

        public const object __hash__ = null;

        public override bool Equals(object obj) {
            MemoryView mv = obj as MemoryView;
            if ((object)mv != null) {
                return this == mv;
            }
            return false;
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            return String.Format("<memory at {0}>", PythonOps.Id(this));
        }

        #endregion
    }
}
