/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

using System;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;

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
            _buffer =@object;
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
                index = ValidateIndex(index);
                return _buffer.GetItem(index + _start);
            }
            set {
                if (_buffer.ReadOnly) {
                    throw PythonOps.TypeError("cannot modify read-only memory");
                }
                ValidateIndex(index);
                _buffer.SetItem(index + _start, value);
            }
        }

        private int ValidateIndex(int index) {
            if (_end != null && (index + _start) >= _end) {
                throw PythonOps.IndexError("index out of range ", index);
            } else if (index < 0) {
                int len = __len__();
                if (index * -1 > len) {
                    throw PythonOps.IndexError("index out of range ", index);
                }
                index = len + index;
            }
            return index;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void __delitem__(int index) {
            // crashes CPython
            throw new NotImplementedException();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void __delitem__(Slice slice) {
            // crashes CPython
            throw new NotImplementedException();
        }

        public object this[[NotNull]Slice slice] {
            get {
                if (slice.step != null) {
                    throw PythonOps.NotImplementedError("");
                }

                return new MemoryView(
                    _buffer,
                    slice.start == null ? _start : (Converter.ConvertToInt32(slice.start) + _start),
                    slice.stop == null ? _end : (Converter.ConvertToInt32(slice.stop) + _start)
                );
            }
            set {
                if (_start != 0 || _end != null) {
                    slice = new Slice(
                        slice.start == null ? _start : (Converter.ConvertToInt32(slice.start) + _start),
                        slice.stop == null ? _end : (Converter.ConvertToInt32(slice.stop) + _start)
                    );
                }

                int len = PythonOps.Length(value);
                int start, stop, step;
                slice.indices(PythonOps.Length(_buffer), out start, out stop, out step);
                if (stop - start != len) {
                    throw PythonOps.ValueError("cannot resize memory view");
                }

                _buffer.SetSlice(slice, value);
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
