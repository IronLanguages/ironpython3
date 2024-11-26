// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Diagnostics;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        /// <summary>
        /// Fields are created when a Structure is defined and provide
        /// introspection of the structure.
        /// </summary>
        [PythonType, PythonHidden]
        public sealed class Field : PythonTypeDataSlot, ICodeFormattable {
            private readonly INativeType _fieldType;
            private readonly int _offset, _index, _bits, _bitsOffset;
            private readonly string _fieldName;

            internal Field(string fieldName, INativeType fieldType, int offset, int index) {
                _offset = offset;
                _fieldType = fieldType;
                _index = index;
                _fieldName = fieldName;
            }

            internal Field(string fieldName, INativeType fieldType, int offset, int index, int? bits, int? bitOffset) {
                _offset = offset;
                _fieldType = fieldType;
                _index = index;
                _fieldName = fieldName;

                if (bits != null) {
                    Debug.Assert(bitOffset != null);
                    Debug.Assert(_fieldType is SimpleType);
                    _bits = bits.Value;
                    _bitsOffset = bitOffset.Value;
                    if (((SimpleType)_fieldType)._swap) {
                        _bitsOffset = _fieldType.Size * 8 - _bitsOffset - _bits;
                    }
                }
            }

            public int offset => _offset;

            public int size => _bits == 0 ? _fieldType.Size : (_bits << 16) + _bitsOffset;

            #region Internal APIs

            internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
                if (instance != null) {
                    CData inst = (CData)instance;
                    if (!IsProperBitfield) {
                        value = _fieldType.GetValue(inst.MemHolder, inst, _offset, false);
                        return true;
                    } else {
                        value = ExtractBits(inst);
                        return true;
                    }
                }

                value = this;
                return true;
            }


            internal override bool GetAlwaysSucceeds => true;

            internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
                if (instance != null) {
                    SetValue(((CData)instance).MemHolder, 0, value);
                    return true;

                }
                return base.TrySetValue(context, instance, owner, value);
            }

            internal void SetValue(MemoryHolder address, int baseOffset, object value) {
                if (!IsProperBitfield) {
                    object keepAlive = _fieldType.SetValue(address, baseOffset + _offset, value);
                    if (keepAlive != null) {
                        address.AddObject(_index.ToString(), keepAlive);
                    }
                } else {
                    SetBitsValue(address, baseOffset, value);
                }
            }

            internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
                throw PythonOps.TypeError("cannot delete fields in ctypes structures/unions");
            }

            internal INativeType NativeType => _fieldType;

            internal int? BitCount => _bits == 0 ? null : _bits;

            internal string FieldName => _fieldName;

            #endregion

            #region ICodeFormattable Members

            public string __repr__(CodeContext context) {
                if (_bits == 0) {
                    return string.Format("<Field type={0}, ofs={1}, size={2}>", ((PythonType)_fieldType).Name, offset, size);
                }
                return string.Format("<Field type={0}, ofs={1}:{2}, bits={3}>", ((PythonType)_fieldType).Name, offset, _bitsOffset, _bits);
            }

            #endregion

            #region Private implementation

            /// <summary>
            /// Called for fields which have been limited to a range of bits.  Given the
            /// value for the full type this extracts the individual bits.
            /// </summary>
            private object ExtractBits(CData instance) {
                Debug.Assert(_bits > 0);
                Debug.Assert(_bits < sizeof(ulong) * 8); // Avoid unspecified shifts: ECMA 335: III.3.58-59
                Debug.Assert(instance != null);

                object value;
                if (IsBoolType) {
                    int valueBits = instance.MemHolder.ReadByte(offset);
                    int ibVal = ExtractBitsFromInt(valueBits);
                    value = ScriptingRuntimeHelpers.BooleanToObject(ibVal != 0);

                } else {
                    value = _fieldType.GetValue(instance.MemHolder, instance, _offset, false);
                    if (value is int iVal) {
                        iVal = ExtractBitsFromInt(iVal);
                        value = ScriptingRuntimeHelpers.Int32ToObject(iVal);
                    } else if (value is BigInteger biVal) {
                        ulong validBits = (1UL << _bits) - 1;

                        ulong bits;
                        if (IsSignedType) {
                            bits = (ulong)(long)biVal;
                        } else {
                            bits = (ulong)biVal;
                        }

                        bits = (bits >> _bitsOffset) & validBits;

                        if (IsSignedType) {
                            // need to sign extend if high bit is set
                            if ((bits & (1UL << (_bits - 1))) != 0) {
                                bits |= ulong.MaxValue ^ validBits;
                            }
                            value = (BigInteger)(long)bits;
                        } else {
                            value = (BigInteger)bits;
                        }

                    } else {
                        throw new InvalidOperationException("we only return int, bigint from GetValue");
                    }
                }
                return value;

                int ExtractBitsFromInt(int iVal) {
                    int validBits = ((1 << _bits) - 1);

                    iVal = (iVal >> _bitsOffset) & validBits;
                    if (IsSignedType) {
                        // need to sign extend if high bit is set
                        if ((iVal & (1 << (_bits - 1))) != 0) {
                            iVal |= (-1) ^ validBits;
                        }
                    }

                    return iVal;
                }
            }

            /// <summary>
            /// Called for fields which have been limited to a range of bits.  Sets the
            /// specified value into the bits for the field.
            /// </summary>
            private void SetBitsValue(MemoryHolder address, int baseOffset, object value) {
                // get the value in the form of a ulong which can contain the biggest bitfield
                ulong newBits;
                if (IsBoolType) {
                    newBits = PythonOps.IsTrue(value) ? 1ul : 0ul;
                } else {
                    value = PythonOps.Index(value); // since 3.8
                    if (value is int iVal) {
                        newBits = (ulong)iVal;
                    } else if (value is BigInteger biVal) {
                        if (biVal.Sign >= 0) {
                            if (biVal <= ulong.MaxValue) {
                                newBits = (ulong)biVal;
                            } else {
                                newBits = (ulong)(biVal & ulong.MaxValue);
                            }
                        } else {
                            if (biVal >= long.MinValue) {
                                newBits = (ulong)(long)biVal;
                            } else {
                                newBits = (ulong)(biVal & ulong.MaxValue);
                            }
                        }
                    } else {
                        throw new InvalidOperationException("unreachable");
                    }
                }

                // do the same for the existing value
                int offset = checked(_offset + baseOffset);
                ulong valueBits;
                if (IsBoolType) {
                    valueBits = address.ReadByte(offset);
                } else {
                    object curValue = _fieldType.GetValue(address, null, offset, false);
                    if (curValue is int iCurVal) {
                        valueBits = (ulong)iCurVal;
                    } else {
                        valueBits = (ulong)(long)(BigInteger)curValue;
                    }
                }

                // get a mask for the bits this field owns
                ulong targetBits = ((1UL << _bits) - 1) << _bitsOffset;
                // clear the existing bits
                valueBits &= ~targetBits;
                // or in the new bits provided by the user
                valueBits |= (newBits << _bitsOffset) & targetBits;

                // and set the value
                if (IsBoolType) {
                    address.WriteByte(offset, (byte)valueBits);
                } else if (IsSignedType) {
                    if (_fieldType.Size <= 4) {
                        _fieldType.SetValue(address, offset, (int)(long)valueBits);
                    } else {
                        _fieldType.SetValue(address, offset, (BigInteger)(long)valueBits);
                    }
                } else {
                    if (_fieldType.Size < 4) {
                        _fieldType.SetValue(address, offset, (int)valueBits);
                    } else {
                        _fieldType.SetValue(address, offset, (BigInteger)valueBits);
                    }
                }
            }

            private bool IsSignedType {
                get {
                    switch (((SimpleType)_fieldType)._type) {
                        case SimpleTypeKind.SignedByte:
                        case SimpleTypeKind.SignedInt:
                        case SimpleTypeKind.SignedLong:
                        case SimpleTypeKind.SignedLongLong:
                        case SimpleTypeKind.SignedShort:
                            return true;
                    }
                    return false;
                }
            }

            private bool IsBoolType => ((SimpleType)_fieldType)._type is SimpleTypeKind.Boolean;

            /// <summary>
            /// Returns true if the field is a bitfield that does not span the whole width of the underlying field type.
            /// </summary>
            private bool IsProperBitfield => 0 < _bits && _bits < _fieldType.Size * 8;

            #endregion
        }
    }
}

#endif
