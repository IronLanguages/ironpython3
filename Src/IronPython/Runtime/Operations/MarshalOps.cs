using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public class MarshalOps {
        public static byte[] GetBytes (object o, int version) {
            MarshalWriter mw = new MarshalWriter (version);
            mw.WriteObject (o);
            return mw.GetBytes ();
        }

        public static object GetObject (IEnumerator<byte> bytes) {
            MarshalReader mr = new MarshalReader (bytes);
            return mr.ReadObject ();
        }


        //
        // Format 
        //  
        // Tuple: '(',int cnt,tuple items
        // List:  '[',int cnt, list items
        // Dict:  '{',key item, value item, '0' terminator
        // Int :  'i',4 bytes
        // float: 'f', 1 byte len, float in string
        // float: 'g', 8 bytes - float in binary form
        // BigInt:  'l', int encodingSize
        //      if the value is negative then the size is negative
        //      and needs to be subtracted from int.MaxValue
        // 
        //      the bytes are encoded in 15 bit multiples, a size of
        //      0 represents a value of zero.
        // 
        // True: 'T'
        // False: 'F'
        // Float: 'f', str len, float in str
        // string: 't', int len, bytes  (ascii)
        // string: 'u', int len, bytes (unicode)
        // string: 'R' <id> - refer to interned string
        // StopIteration: 'S'   
        // None: 'N'
        // Long: 'I' followed by 8 bytes (little endian 64-bit value)
        // complex: 'x', byte len, real str, byte len, imag str
        // Buffer (array.array too, but they don't round trip): 's', int len, buffer bytes
        // code: 'c', more stuff...
        // 
        private class MarshalWriter {
            private readonly List<byte> _bytes;
            private readonly int _version;
            private readonly Dictionary<string, int> _strings;

            public MarshalWriter (int version) {
                _bytes = new List<byte> ();
                _version = version;
                if (_version > 0) {
                    _strings = new Dictionary<string, int> ();
                }
            }

            public void WriteObject (object o) {
                List<object> infinite = PythonOps.GetReprInfinite ();

                if (infinite.Contains (o)) throw PythonOps.ValueError ("Marshaled data contains infinite cycle");

                int index = infinite.Count;
                infinite.Add (o);
                try {
                    if (o == null) _bytes.Add ((byte)'N');
                    else if (o == ScriptingRuntimeHelpers.True || (o is bool && (bool)o)) _bytes.Add ((byte)'T');
                    else if (o == ScriptingRuntimeHelpers.False || (o is bool && (!(bool)o))) _bytes.Add ((byte)'F');
                    else if (o is string) WriteString (o as string);
                    else if (o is int) WriteInt ((int)o);
                    else if (o is float) WriteFloat ((float)o);
                    else if (o is double) WriteFloat ((double)o);
                    else if (o is long) WriteLong ((long)o);
                    else if (o.GetType () == typeof (PythonList)) WriteList (o);
                    else if (o.GetType () == typeof (PythonDictionary)) WriteDict (o);
                    else if (o.GetType () == typeof (PythonTuple)) WriteTuple (o);
                    else if (o.GetType () == typeof (SetCollection)) WriteSet (o);
                    else if (o.GetType () == typeof (FrozenSetCollection)) WriteFrozenSet (o);
                    else if (o is BigInteger) WriteInteger ((BigInteger)o);
                    else if (o is Complex) WriteComplex ((Complex)o);
                    else if (o is PythonBuffer) WriteBuffer ((PythonBuffer)o);
                    else if (o == PythonExceptions.StopIteration) WriteStopIteration ();
                    else throw PythonOps.ValueError ("unmarshallable object");
                } finally {
                    infinite.RemoveAt (index);
                }
            }

            private void WriteFloat (float f) {
                if (_version > 1) {
                    _bytes.Add ((byte)'g');
                    _bytes.AddRange (BitConverter.GetBytes ((double)f));
                } else {
                    _bytes.Add ((byte)'f');
                    WriteDoubleString (f);
                }
            }

            private void WriteFloat (double f) {
                if (_version > 1) {
                    _bytes.Add ((byte)'g');
                    _bytes.AddRange (BitConverter.GetBytes (f));
                } else {
                    _bytes.Add ((byte)'f');
                    WriteDoubleString (f);
                }
            }

            private void WriteDoubleString (double d) {
                string s = DoubleOps.__repr__ (DefaultContext.Default, d);
                _bytes.Add ((byte)s.Length);
                for (int i = 0; i < s.Length; i++) {
                    _bytes.Add ((byte)s[i]);
                }
            }

            private void WriteInteger (BigInteger val) {
                _bytes.Add ((byte)'l');
                int wordCount = 0, dir;
                if (val < BigInteger.Zero) {
                    val *= -1;
                    dir = -1;
                } else {
                    dir = 1;
                }

                List<byte> bytes = new List<byte> ();
                while (val != BigInteger.Zero) {
                    int word = (int)(val & 0x7FFF);
                    val = val >> 15;

                    bytes.Add ((byte)(word & 0xFF));
                    bytes.Add ((byte)((word >> 8) & 0xFF));
                    wordCount += dir;
                }

                WriteInt32 (wordCount);

                _bytes.AddRange (bytes);
            }

            private void WriteBuffer (PythonBuffer b) {
                _bytes.Add ((byte)'s');
                List<byte> newBytes = new List<byte> ();
                for (int i = 0; i < b.Size; i++) {
                    if (b[i] is string) {
                        string str = b[i] as string;
                        byte[] utfBytes = Encoding.UTF8.GetBytes (str);
                        if (utfBytes.Length != str.Length) {
                            newBytes.AddRange (utfBytes);
                        } else {
                            byte[] strBytes = PythonAsciiEncoding.Instance.GetBytes (str);
                            newBytes.AddRange (strBytes);
                        }
                    } else {
                        newBytes.Add ((byte)b[i]);
                    }
                }
                WriteInt32 (newBytes.Count);
                _bytes.AddRange (newBytes);
            }

            private void WriteLong (long l) {
                _bytes.Add ((byte)'I');

                for (int i = 0; i < 8; i++) {
                    _bytes.Add ((byte)(l & 0xff));
                    l = l >> 8;
                }
            }

            private void WriteComplex (Complex val) {
                _bytes.Add ((byte)'x');
                WriteDoubleString (val.Real);
                WriteDoubleString (val.Imaginary ());
            }

            private void WriteStopIteration () {
                _bytes.Add ((byte)'S');
            }

            private void WriteInt (int val) {
                _bytes.Add ((byte)'i');
                WriteInt32 (val);
            }

            private void WriteInt32 (int val) {
                _bytes.Add ((byte)(val & 0xff));
                _bytes.Add ((byte)((val >> 8) & 0xff));
                _bytes.Add ((byte)((val >> 16) & 0xff));
                _bytes.Add ((byte)((val >> 24) & 0xff));
            }

            private void WriteString (string s) {
                byte[] utfBytes = Encoding.UTF8.GetBytes (s);
                if (utfBytes.Length != s.Length) {
                    _bytes.Add ((byte)'u');
                    WriteInt32 (utfBytes.Length);
                    for (int i = 0; i < utfBytes.Length; i++) {
                        _bytes.Add (utfBytes[i]);
                    }
                } else {
                    int index;
                    if (_strings != null && _strings.TryGetValue (s, out index)) {
                        _bytes.Add ((byte)'R');
                        WriteInt32 (index);
                    } else {
                        byte[] strBytes = PythonAsciiEncoding.Instance.GetBytes (s);
                        if (_strings != null) {
                            _bytes.Add ((byte)'t');
                        } else {
                            _bytes.Add ((byte)'s');
                        }
                        WriteInt32 (strBytes.Length);
                        for (int i = 0; i < strBytes.Length; i++) {
                            _bytes.Add (strBytes[i]);
                        }

                        if (_strings != null) {
                            _strings[s] = _strings.Count;
                        }
                    }
                }
            }

            private void WriteList (object o) {
                PythonList l = o as PythonList;
                _bytes.Add ((byte)'[');
                WriteInt32 (l.__len__ ());
                for (int i = 0; i < l.__len__ (); i++) {
                    WriteObject (l[i]);
                }
            }

            private void WriteDict (object o) {
                PythonDictionary d = o as PythonDictionary;
                _bytes.Add ((byte)'{');
                IEnumerator<KeyValuePair<object, object>> ie = ((IEnumerable<KeyValuePair<object, object>>)d).GetEnumerator ();
                while (ie.MoveNext ()) {
                    WriteObject (ie.Current.Key);
                    WriteObject (ie.Current.Value);
                }
                _bytes.Add ((byte)'0');
            }

            private void WriteTuple (object o) {
                PythonTuple t = o as PythonTuple;
                _bytes.Add ((byte)'(');
                WriteInt32 (t.__len__ ());
                for (int i = 0; i < t.__len__ (); i++) {
                    WriteObject (t[i]);
                }
            }

            private void WriteSet (object set) {
                SetCollection s = set as SetCollection;
                _bytes.Add ((byte)'<');
                WriteInt32 (s.__len__ ());
                foreach (object o in s) {
                    WriteObject (o);
                }
            }

            private void WriteFrozenSet (object set) {
                FrozenSetCollection s = set as FrozenSetCollection;
                _bytes.Add ((byte)'>');
                WriteInt32 (s.__len__ ());
                foreach (object o in s) {
                    WriteObject (o);
                }
            }

            public byte[] GetBytes () {
                return _bytes.ToArray ();
            }
        }

        private class MarshalReader {
            private IEnumerator<byte> _myBytes;
            private Stack<ProcStack> _stack;
            private readonly Dictionary<int, string> _strings;
            private object _result;

            public MarshalReader (IEnumerator<byte> bytes) {
                _myBytes = bytes;
                _strings = new Dictionary<int, string> ();
            }

            public object ReadObject () {
                while (_myBytes.MoveNext ()) {
                    byte cur = _myBytes.Current;
                    object res;
                    switch ((char)cur) {
                        case '(': PushStack (StackType.Tuple); break;
                        case '[': PushStack (StackType.List); break;
                        case '{': PushStack (StackType.Dict); break;
                        case '<': PushStack (StackType.Set); break;
                        case '>': PushStack (StackType.FrozenSet); break;
                        case '0':
                            // end of dictionary
                            if (_stack == null || _stack.Count == 0) {
                                throw PythonOps.ValueError ("bad marshal data");
                            }
                            _stack.Peek ().StackCount = 0;
                            break;
                        // case 'c': break;
                        default:
                            res = YieldSimple ();
                            if (_stack == null) {
                                return res;
                            }

                            do {
                                res = UpdateStack (res);
                            } while (res != null && _stack.Count > 0);

                            if (_stack.Count == 0) {
                                return _result;
                            }

                            continue;
                    }

                    // handle empty lists/tuples...
                    if (_stack != null && _stack.Count > 0 && _stack.Peek ().StackCount == 0) {
                        ProcStack ps = _stack.Pop ();
                        res = ps.StackObj;

                        if (ps.StackType == StackType.Tuple) {
                            res = PythonTuple.Make (res);
                        } else if (ps.StackType == StackType.FrozenSet) {
                            res = FrozenSetCollection.Make (TypeCache.FrozenSet, res);
                        }

                        if (_stack.Count > 0) {
                            // empty list/tuple
                            do {
                                res = UpdateStack (res);
                            } while (res != null && _stack.Count > 0);
                            if (_stack.Count == 0) break;
                        } else {
                            _result = res;
                            break;
                        }
                    }
                }

                return _result;
            }

            private void PushStack (StackType type) {
                ProcStack newStack = new ProcStack ();
                newStack.StackType = type;

                switch (type) {
                    case StackType.Dict:
                        newStack.StackObj = new PythonDictionary ();
                        newStack.StackCount = -1;
                        break;
                    case StackType.List:
                        newStack.StackObj = new PythonList ();
                        newStack.StackCount = ReadInt32 ();
                        break;
                    case StackType.Tuple:
                        newStack.StackCount = ReadInt32 ();
                        newStack.StackObj = new List<object> (newStack.StackCount);
                        break;
                    case StackType.Set:
                        newStack.StackObj = new SetCollection ();
                        newStack.StackCount = ReadInt32 ();
                        break;
                    case StackType.FrozenSet:
                        newStack.StackCount = ReadInt32 ();
                        newStack.StackObj = new List<object> (newStack.StackCount);
                        break;
                }

                if (_stack == null) _stack = new Stack<ProcStack> ();

                _stack.Push (newStack);
            }

            private object UpdateStack (object res) {
                ProcStack curStack = _stack.Peek ();
                switch (curStack.StackType) {
                    case StackType.Dict:
                        PythonDictionary od = curStack.StackObj as PythonDictionary;
                        if (curStack.HaveKey) {
                            od[curStack.Key] = res;
                            curStack.HaveKey = false;
                        } else {
                            curStack.HaveKey = true;
                            curStack.Key = res;
                        }
                        break;
                    case StackType.Tuple:
                        List<object> objs = curStack.StackObj as List<object>;
                        objs.Add (res);
                        curStack.StackCount--;
                        if (curStack.StackCount == 0) {
                            _stack.Pop ();
                            object tuple = PythonTuple.Make (objs);
                            if (_stack.Count == 0) {
                                _result = tuple;
                            }
                            return tuple;
                        }
                        break;
                    case StackType.List:
                        PythonList ol = curStack.StackObj as PythonList;
                        ol.AddNoLock (res);
                        curStack.StackCount--;
                        if (curStack.StackCount == 0) {
                            _stack.Pop ();
                            if (_stack.Count == 0) {
                                _result = ol;
                            }
                            return ol;
                        }
                        break;
                    case StackType.Set:
                        SetCollection os = curStack.StackObj as SetCollection;
                        os.add (res);
                        curStack.StackCount--;
                        if (curStack.StackCount == 0) {
                            _stack.Pop ();
                            if (_stack.Count == 0) {
                                _result = os;
                            }
                            return os;
                        }
                        break;
                    case StackType.FrozenSet:
                        List<object> ofs = curStack.StackObj as List<object>;
                        ofs.Add (res);
                        curStack.StackCount--;
                        if (curStack.StackCount == 0) {
                            _stack.Pop ();
                            object frozenSet = FrozenSetCollection.Make (TypeCache.FrozenSet, ofs);
                            if (_stack.Count == 0) {
                                _result = frozenSet;
                            }
                            return frozenSet;
                        }
                        break;
                }
                return null;
            }

            private enum StackType {
                Tuple,
                Dict,
                List,
                Set,
                FrozenSet
            }

            private class ProcStack {
                public StackType StackType;
                public object StackObj;
                public int StackCount;
                public bool HaveKey;
                public object Key;
            }

            private object YieldSimple () {
                object res;
                switch ((char)_myBytes.Current) {
                    // simple ops to be read in
                    case 'i': res = ReadInt (); break;
                    case 'l': res = ReadBigInteger (); break;
                    case 'T': res = ScriptingRuntimeHelpers.True; break;
                    case 'F': res = ScriptingRuntimeHelpers.False; break;
                    case 'f': res = ReadFloat (); break;
                    case 't': res = ReadAsciiString (); break;
                    case 'u': res = ReadUnicodeString (); break;
                    case 'S': res = PythonExceptions.StopIteration; break;
                    case 'N': res = null; break;
                    case 'x': res = ReadComplex (); break;
                    case 's': res = ReadBuffer (); break;
                    case 'I': res = ReadLong (); break;
                    case 'R': res = _strings[ReadInt32 ()]; break;
                    case 'g': res = ReadBinaryFloat (); break;
                    default: throw PythonOps.ValueError ("bad marshal data");
                }
                return res;
            }

            private byte[] ReadBytes (int len) {
                byte[] bytes = new byte[len];
                for (int i = 0; i < len; i++) {
                    if (!_myBytes.MoveNext ()) {
                        throw PythonOps.ValueError ("bad marshal data");
                    }
                    bytes[i] = _myBytes.Current;
                }

                return bytes;
            }

            private int ReadInt32 () {
                byte[] intBytes = ReadBytes (4);

                int res = intBytes[0] |
                    (intBytes[1] << 8) |
                    (intBytes[2] << 16) |
                    (intBytes[3] << 24);

                return res;
            }

            private double ReadFloatStr () {
                MoveNext ();

                string str = DecodeString (PythonAsciiEncoding.Instance, ReadBytes (_myBytes.Current));

                double res = 0;
                if (double.TryParse (str, out res)) {
                    return res;
                }
                return 0;
            }

            private void MoveNext () {
                if (!_myBytes.MoveNext ()) {
                    throw PythonOps.EofError ("EOF read where object expected");
                }
            }

            private string DecodeString (Encoding enc, byte[] bytes) {
                return enc.GetString (bytes, 0, bytes.Length);
            }

            object ReadInt () {
                
                // bytes not present are treated as being -1
                byte b1 = ReadIntPart ();
                byte b2 = ReadIntPart ();
                byte b3 = ReadIntPart ();
                byte b4 = ReadIntPart ();

                byte[] bytes = new byte[] { b1, b2, b3, b4 };
                return ScriptingRuntimeHelpers.Int32ToObject (BitConverter.ToInt32 (bytes, 0));
                //return Ops.int2object(b1 | (b2 << 8) | (b3 << 16) | (b4 << 24));
            }

            private byte ReadIntPart () {
                byte b;
                if (_myBytes.MoveNext ()) {
                    b = _myBytes.Current;
                } else {
                    b = 255;
                }
                return b;
            }

            object ReadFloat () {
                return ReadFloatStr ();
            }

            private object ReadBinaryFloat () {
                return BitConverter.ToDouble (ReadBytes (8), 0);
            }

            private object ReadAsciiString () {
                string res = DecodeString (PythonAsciiEncoding.Instance, ReadBytes (ReadInt32 ()));
                _strings[_strings.Count] = res;
                return res;
            }

            private object ReadUnicodeString () {
                return DecodeString (Encoding.UTF8, ReadBytes (ReadInt32 ()));
            }

            private object ReadComplex () {
                double real = ReadFloatStr ();
                double imag = ReadFloatStr ();

                return new Complex (real, imag);
            }

            private object ReadBuffer () {
                return DecodeString (Encoding.UTF8, ReadBytes (ReadInt32 ()));
            }

            private object ReadLong () {
                byte[] bytes = ReadBytes (8);

                long res = 0;
                for (int i = 0; i < 8; i++) {
                    res |= (((long)bytes[i]) << (i * 8));
                }

                return res;
            }

            private object ReadBigInteger () {
                int encodingSize = ReadInt32 ();
                if (encodingSize == 0) {
                    return BigInteger.Zero;
                }

                int sign = 1;
                if (encodingSize < 0) {
                    sign = -1;
                    encodingSize *= -1;
                }
                int len = encodingSize * 2;

                byte[] bytes = ReadBytes (len);

                // re-pack our 15-bit values into bytes
                byte[] data = new byte[bytes.Length];
                for (int bytesIndex = 0, dataIndex = 0, shift = 0; bytesIndex < len; bytesIndex++) {
                    // write 8 bits
                    if (shift == 0) {
                        data[dataIndex] = bytes[bytesIndex];
                    } else {
                        data[dataIndex] = (byte)((bytes[bytesIndex] >> shift) | (bytes[bytesIndex + 1] << (8 - shift)));
                    }

                    //done
                    dataIndex++;
                    bytesIndex++;

                    if (shift == 7) {
                        shift = 0;
                        continue;
                    }

                    // write 7 bits
                    if (bytesIndex < len - 1) {
                        data[dataIndex] = (byte)((bytes[bytesIndex] >> shift) | (bytes[bytesIndex + 1] << (7 - shift)));
                    } else {
                        data[dataIndex] = (byte)(bytes[bytesIndex] >> shift);
                    }

                    //done
                    dataIndex++;
                    shift++;
                }

                // and finally pass the data onto the big integer.
                BigInteger res = new BigInteger (data);
                return sign < 0 ? -res : res;
            }
        }
    }
}
