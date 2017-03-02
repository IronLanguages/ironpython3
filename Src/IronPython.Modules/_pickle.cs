/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

[assembly: PythonModule("_pickle", typeof(IronPython.Modules.PythonPickle))]
namespace IronPython.Modules {
    public static class PythonPickle {
        public const string __doc__ = "Fast object serialization/deserialization.\n\n"
            + "Differences from CPython:\n"
            + " - does not implement the undocumented fast mode\n";
        [System.Runtime.CompilerServices.SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException("PickleError", dict, "PickleError", "_pickle");
            context.EnsureModuleException("PicklingError", dict, "PicklingError", "_pickle");
            context.EnsureModuleException("UnpicklingError", dict, "UnpicklingError", "_pickle");
            context.EnsureModuleException("UnpickleableError", dict, "UnpickleableError", "_pickle");
            context.EnsureModuleException("BadPickleGet", dict, "BadPickleGet", "_pickle");
            dict["__builtins__"] = context.BuiltinModuleInstance;
            dict["compatible_formats"] = PythonOps.MakeList("1.0", "1.1", "1.2", "1.3", "2.0");
        }

        private static readonly PythonStruct.Struct _float64 = PythonStruct.Struct.Create(">d");
        
        private const int highestProtocol = 2;
        
        public const string __version__ = "1.71";
        public const string format_version = "2.0";
        
        public static int HIGHEST_PROTOCOL {
            get { return highestProtocol; }
        }
        
        private const string Newline = "\n";

        #region Public module-level functions

        [Documentation("dump(obj, file, protocol=0) -> None\n\n"
            + "Pickle obj and write the result to file.\n"
            + "\n"
            + "See documentation for Pickler() for a description the file, protocol, and\n"
            + "(deprecated) bin parameters."
            )]
        public static void dump(CodeContext/*!*/ context, object obj, object file, [DefaultParameterValue(null)] object protocol, [DefaultParameterValue(null)] object bin) {
            PicklerObject/*!*/ pickler = new PicklerObject(context, file, protocol, bin);
            pickler.dump(context, obj);
        }

        [Documentation("dumps(obj, protocol=0) -> pickle string\n\n"
            + "Pickle obj and return the result as a string.\n"
            + "\n"
            + "See the documentation for Pickler() for a description of the protocol and\n"
            + "(deprecated) bin parameters."
            )]
        public static string dumps(CodeContext/*!*/ context, object obj, [DefaultParameterValue(null)] object protocol, [DefaultParameterValue(null)] object bin) {
            //??? possible perf enhancement: use a C# TextWriter-backed IFileOutput and
            // thus avoid Python call overhead. Also do similar thing for LoadFromString.
            var stringIO = new StringBuilderOutput();
            PicklerObject/*!*/ pickler = new PicklerObject(context, stringIO, protocol, bin);
            pickler.dump(context, obj);
            return stringIO.GetString();
        }

        [Documentation("load(file) -> unpickled object\n\n"
            + "Read pickle data from the open file object and return the corresponding\n"
            + "unpickled object. Data after the first pickle found is ignored, but the file\n"
            + "cursor is not reset, so if a file objects contains multiple pickles, then\n"
            + "load() may be called multiple times to unpickle them.\n"
            + "\n"
            + "file: an object (such as an open file or a StringIO) with read(num_chars) and\n"
            + "    readline() methods that return strings\n"
            + "\n"
            + "load() automatically determines if the pickle data was written in binary or\n"
            + "text mode."
            )]
        public static object load(CodeContext/*!*/ context, object file) {
            return new UnpicklerObject(context, file).load(context);
        }

        [Documentation("loads(string) -> unpickled object\n\n"
            + "Read a pickle object from a string, unpickle it, and return the resulting\n"
            + "reconstructed object. Characters in the string beyond the end of the first\n"
            + "pickle are ignored."
            )]
        public static object loads(CodeContext/*!*/ context, [BytesConversion]string @string) {
            return new UnpicklerObject(context, new PythonStringInput(@string)).load(context);
        }

        #endregion

        #region File I/O wrappers

        /// <summary>
        /// Interface for "file-like objects" that implement the protocol needed by load() and friends.
        /// This enables the creation of thin wrappers that make fast .NET types and slow Python types look the same.
        /// </summary>
        internal abstract class FileInput {
            public abstract string Read(CodeContext/*!*/ context, int size);
            public abstract string ReadLine(CodeContext/*!*/ context);
            public virtual string ReadLineNoNewLine(CodeContext/*!*/ context) {
                var raw = ReadLine(context);
                return raw.Substring(0, raw.Length - 1);
            }

            public virtual char ReadChar(CodeContext context) {
                string res = Read(context, 1);
                if (res.Length < 1) {
                    throw PythonOps.EofError("unexpected EOF while unpickling");
                }
                return res[0];
            }

            public virtual int ReadInt(CodeContext context) {
                return (int)ReadChar(context) |
                       ((int)ReadChar(context)) << 8 |
                       ((int)ReadChar(context)) << 16 |
                       ((int)ReadChar(context)) << 24;
            }
        }

        /// <summary>
        /// Interface for "file-like objects" that implement the protocol needed by dump() and friends.
        /// This enables the creation of thin wrappers that make fast .NET types and slow Python types look the same.
        /// </summary>
        internal abstract class FileOutput {
            private readonly char[] int32chars = new char[4];

            public abstract void Write(CodeContext/*!*/ context, string data);

            public virtual void Write(CodeContext context, int data) {
                int32chars[0] = (char)(int)((data & 0xff));
                int32chars[1] = (char)(int)((data >> 8) & 0xff);
                int32chars[2] = (char)(int)((data >> 16) & 0xff);
                int32chars[3] = (char)(int)((data >> 24) & 0xff);
                Write(context, new string(int32chars));
            }

            public virtual void Write(CodeContext context, char data) {
                Write(context, ScriptingRuntimeHelpers.CharToString(data));
            }
        }

        private class PythonFileInput : FileInput {
            private object _readMethod;
            private object _readLineMethod;

            public PythonFileInput(CodeContext/*!*/ context, object file) {
                if (!PythonOps.TryGetBoundAttr(context, file, "read", out _readMethod) ||
                    !PythonOps.IsCallable(context, _readMethod) ||
                    !PythonOps.TryGetBoundAttr(context, file, "readline", out _readLineMethod) ||
                    !PythonOps.IsCallable(context, _readLineMethod)
                ) {
                    throw PythonOps.TypeError("argument must have callable 'read' and 'readline' attributes");
                }
            }

            public override string Read(CodeContext/*!*/ context, int size) {
                return Converter.ConvertToString(PythonCalls.Call(context, _readMethod, size));
            }

            public override string ReadLine(CodeContext/*!*/ context) {
                return Converter.ConvertToString(PythonCalls.Call(context, _readLineMethod));
            }
        }

        internal class PythonStringInput : FileInput {
            private readonly string _data;
            int _offset;

            public PythonStringInput(string data) {
                _data = data;
            }

            public override string Read(CodeContext context, int size) {
                var res = _data.Substring(_offset, size);
                _offset += size;
                return res;
            }

            public override string ReadLine(CodeContext context) {
                return ReadLineWorker(true);
            }

            public override string ReadLineNoNewLine(CodeContext context) {
                return ReadLineWorker(false);
            }

            public override char ReadChar(CodeContext context) {
                if (_offset < _data.Length) {
                    return _data[_offset++];
                }
                throw PythonOps.EofError("unexpected EOF while unpickling");
            }

            public override int ReadInt(CodeContext context) {
                if (_offset + 4 <= _data.Length) {
                    int res = _data[_offset]|
                       ((int)_data[_offset  + 1]) << 8 |
                       ((int)_data[_offset + 2]) << 16 |
                       ((int)_data[_offset + 3]) << 24;

                    _offset += 4;
                    return res;
                }

                throw PythonOps.EofError("unexpected EOF while unpickling");
            }

            private string ReadLineWorker(bool includeNewLine) {
                string res;
                for (int i = _offset; i < _data.Length; i++) {
                    if (_data[i] == '\n') {
                        res = _data.Substring(_offset, i - _offset + (includeNewLine ? 1 : 0));
                        _offset = i + 1;
                        return res;
                    }
                }
                res = _data.Substring(_offset);
                _offset = _data.Length;
                return res;
            }
        }

        private class PythonFileLikeOutput : FileOutput {
            private object _writeMethod;

            public PythonFileLikeOutput(CodeContext/*!*/ context, object file) {
                if (!PythonOps.TryGetBoundAttr(context, file, "write", out _writeMethod) ||
                    !PythonOps.IsCallable(context, this._writeMethod)
                ) {
                    throw PythonOps.TypeError("argument must have callable 'write' attribute");
                }
            }

            public override void Write(CodeContext/*!*/ context, string data) {
                PythonCalls.Call(context, _writeMethod, data);
            }
        }

        private class PythonFileOutput : FileOutput {
            private readonly PythonFile _file;

            public PythonFileOutput(PythonFile file) {
                _file = file;
            }

            public override void Write(CodeContext/*!*/ context, string data) {
                _file.write(data);
            }
        }

        private class StringBuilderOutput : FileOutput {
            private readonly StringBuilder _builder = new StringBuilder(4096);

            public string GetString() {
                return _builder.ToString();
            }

            public override void Write(CodeContext context, char data) {
                _builder.Append(data);
            }

            public override void Write(CodeContext context, int data) {
                _builder.Append((char)(int)((data) & 0xff));
                _builder.Append((char)(int)((data >> 8) & 0xff));
                _builder.Append((char)(int)((data >> 16) & 0xff));
                _builder.Append((char)(int)((data >> 24) & 0xff));
            }

            public override void Write(CodeContext context, string data) {
                _builder.Append(data);
            }
        }

        private class PythonReadableFileOutput : PythonFileLikeOutput {
            private object _getValueMethod;

            public PythonReadableFileOutput(CodeContext/*!*/ context, object file)
                : base(context, file) {
                if (!PythonOps.TryGetBoundAttr(context, file, "getvalue", out _getValueMethod) ||
                    !PythonOps.IsCallable(context, _getValueMethod)
                ) {
                    throw PythonOps.TypeError("argument must have callable 'getvalue' attribute");
                }
            }

            public object GetValue(CodeContext/*!*/ context) {
                return PythonCalls.Call(context, _getValueMethod);
            }
        }

        #endregion

        #region Opcode constants

        internal static class Opcode {
            public const char Append = 'a';
            public const char Appends = 'e';
            public const char BinFloat = 'G';
            public const char BinGet = 'h';
            public const char BinInt = 'J';
            public const char BinInt1 = 'K';
            public const char BinInt2 = 'M';
            public const char BinPersid = 'Q';
            public const char BinPut = 'q';
            public const char BinString = 'T';
            public const char BinUnicode = 'X';
            public const char Build = 'b';
            public const char Dict = 'd';
            public const char Dup = '2';
            public const char EmptyDict = '}';
            public const char EmptyList = ']';
            public const char EmptyTuple = ')';
            public const char Ext1 = '\x82';
            public const char Ext2 = '\x83';
            public const char Ext4 = '\x84';
            public const char Float = 'F';
            public const char Get = 'g';
            public const char Global = 'c';
            public const char Inst = 'i';
            public const char Int = 'I';
            public const char List = 'l';
            public const char Long = 'L';
            public const char Long1 = '\x8a';
            public const char Long4 = '\x8b';
            public const char LongBinGet = 'j';
            public const char LongBinPut = 'r';
            public const char Mark = '(';
            public const char NewFalse = '\x89';
            public const char NewObj = '\x81';
            public const char NewTrue = '\x88';
            public const char NoneValue = 'N';
            public const char Obj = 'o';
            public const char PersId = 'P';
            public const char Pop = '0';
            public const char PopMark = '1';
            public const char Proto = '\x80';
            public const char Put = 'p';
            public const char Reduce = 'R';
            public const char SetItem = 's';
            public const char SetItems = 'u';
            public const char ShortBinstring = 'U';
            public const char Stop = '.';
            public const char String = 'S';
            public const char Tuple = 't';
            public const char Tuple1 = '\x85';
            public const char Tuple2 = '\x86';
            public const char Tuple3 = '\x87';
            public const char Unicode = 'V';
        }

        #endregion

        #region Pickler object

        public static PicklerObject/*!*/ Pickler(CodeContext/*!*/ context, [DefaultParameterValue(null)]object file, [DefaultParameterValue(null)]object protocol, [DefaultParameterValue(null)]object bin) {
            return new PicklerObject(context, file, protocol, bin);
        }

        [Documentation("Pickler(file, protocol=0) -> Pickler object\n\n"
            + "A Pickler object serializes Python objects to a pickle bytecode stream, which\n"
            + "can then be converted back into equivalent objects using an Unpickler.\n"
            + "\n"
            + "file: an object (such as an open file) that has a write(string) method.\n"
            + "protocol: if omitted, protocol 0 is used. If HIGHEST_PROTOCOL or a negative\n"
            + "    number, the highest available protocol is used.\n"
            + "bin: (deprecated; use protocol instead) for backwards compability, a 'bin'\n"
            + "    keyword parameter is supported. When protocol is specified it is ignored.\n"
            + "    If protocol is not specified, then protocol 0 is used if bin is false, and\n"
            + "    protocol 1 is used if bin is true."
            )]
        [PythonType("Pickler"), PythonHidden]
        public class PicklerObject {

            private const char LowestPrintableChar = (char)32;
            private const char HighestPrintableChar = (char)126;
            // max elements that can be set/appended at a time using SETITEMS/APPENDS

            private delegate void PickleFunction(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object value);
            private static readonly Dictionary<Type, PickleFunction> _dispatchTable;

            private int _batchSize = 1000;
            private FileOutput _file;
            private int _protocol;
            private PythonDictionary _memo;               // memo if the user accesses the memo property
            private Dictionary<object, int> _privMemo;    // internal fast memo which we can use if the user doesn't access memo
            private object _persist_id;

            static PicklerObject() {
                _dispatchTable = new Dictionary<Type, PickleFunction>();
                _dispatchTable[typeof(PythonDictionary)] = SaveDict;
                _dispatchTable[typeof(PythonTuple)] = SaveTuple;
                _dispatchTable[typeof(List)] = SaveList;
                _dispatchTable[typeof(PythonFunction)] = SaveGlobal;
                _dispatchTable[typeof(BuiltinFunction)] = SaveGlobal;
                _dispatchTable[typeof(PythonType)] = SaveGlobal;
            }
            #region Public API

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
            public PythonDictionary memo {
                get {
                    if (_memo == null) {
                        // create publicly viewable memo
                        PythonDictionary resMemo = new PythonDictionary();
                        foreach (var v in _privMemo) {
                            resMemo._storage.AddNoLock(
                                ref resMemo._storage,
                                Builtin.id(v.Key),
                                PythonTuple.MakeTuple(v.Value, v.Key)
                            );
                        }
                        _memo = resMemo;
                    }
                    return _memo;
                }
                set {
                    _memo = value;
                    _privMemo = null;
                }
            }

            public int proto {
                get { return _protocol; }
                set { _protocol = value; }
            }

            public int _BATCHSIZE {
                get { return _batchSize; }
                set { _batchSize = value; }
            }

            public object persistent_id {
                get {
                    return _persist_id;
                }
                set {
                    _persist_id = value;
                }
            }

            public int binary {
                get { return _protocol == 0 ? 1 : 0; }
                set { _protocol = value; }
            }

            public int fast {
                // We don't implement fast, but we silently ignore it when it's set so that test_cpickle works.
                // For a description of fast, see http://mail.python.org/pipermail/python-bugs-list/2001-October/007695.html
                get { return 0; }
                set { /* ignore */ }
            }

            public PicklerObject(CodeContext/*!*/ context, object file, object protocol, object bin) {
                int intProtocol;
                if (file == null) {
                    _file = new PythonReadableFileOutput(context, new PythonIOModule.StringIO(context, "", "\n"));
                } else if (Converter.TryConvertToInt32(file, out intProtocol)) {
                    // For undocumented (yet tested in official CPython tests) list-based pickler, the
                    // user could do something like Pickler(1), which would create a protocol-1 pickler
                    // with an internal string output buffer (retrievable using getvalue()). For a little
                    // more info, see
                    // https://sourceforge.net/tracker/?func=detail&atid=105470&aid=939395&group_id=5470
                    _file = new PythonReadableFileOutput(context, new PythonIOModule.StringIO(context, "", "\n"));
                    protocol = file;
                } else if (file is PythonFile) {
                    _file = new PythonFileOutput((PythonFile)file);
                } else if (file is FileOutput) {
                    _file = (FileOutput)file;
                } else {
                    _file = new PythonFileLikeOutput(context, file);
                }

                _privMemo = new Dictionary<object, int>(256, ReferenceEqualityComparer.Instance);

                if (protocol == null) protocol = PythonOps.IsTrue(bin) ? 1 : 0;

                intProtocol = context.LanguageContext.ConvertToInt32(protocol);
                if (intProtocol > highestProtocol) {
                    throw PythonOps.ValueError("pickle protocol {0} asked for; the highest available protocol is {1}", intProtocol, highestProtocol);
                } else if (intProtocol < 0) {
                    this._protocol = highestProtocol;
                } else {
                    this._protocol = intProtocol;
                }
            }

            [Documentation("dump(obj) -> None\n\n"
                + "Pickle obj and write the result to the file object that was passed to the\n"
                + "constructor\n."
                + "\n"
                + "Note that you may call dump() multiple times to pickle multiple objects. To\n"
                + "unpickle the stream, you will need to call Unpickler's load() method a\n"
                + "corresponding number of times.\n"
                + "\n"
                + "The first time a particular object is encountered, it will be pickled normally.\n"
                + "If the object is encountered again (in the same or a later dump() call), a\n"
                + "reference to the previously generated value will be pickled. Unpickling will\n"
                + "then create multiple references to a single object."
                )]
            public void dump(CodeContext/*!*/ context, object obj) {
                if (_protocol >= 2) WriteProto(context);
                Save(context, obj);
                Write(context, Opcode.Stop);
            }

            [Documentation("clear_memo() -> None\n\n"
                + "Clear the memo, which is used internally by the pickler to keep track of which\n"
                + "objects have already been pickled (so that shared or recursive objects are\n"
                + "pickled only once)."
                )]
            public void clear_memo() {
                if (_memo != null) {
                    _memo.Clear();
                } else {
                    _privMemo.Clear();
                }
            }

            private void Memoize(object obj) {
                if (_memo != null) {
                    if (!MemoContains(PythonOps.Id(obj))) {
                        _memo[PythonOps.Id(obj)] = PythonTuple.MakeTuple(_memo.Count, obj);
                    }
                } else {
                    if(!_privMemo.ContainsKey(obj)) {
                        _privMemo[obj] = _privMemo.Count;
                    }
                }
            }

            private int MemoizeNew(object obj) {
                
                int res;
                if (_memo != null) {
                    Debug.Assert(!_memo.ContainsKey(obj));
                    _memo[PythonOps.Id(obj)] = PythonTuple.MakeTuple(res = _memo.Count, obj);
                } else {
                    Debug.Assert(!_privMemo.ContainsKey(obj));
                    _privMemo[obj] = res = _privMemo.Count;
                }
                return res;
            }

            private bool MemoContains(object obj) {                
                if (_memo != null) {
                    return _memo.Contains(PythonOps.Id(obj));
                }

                return _privMemo.ContainsKey(obj);
            }

            private bool TryWriteFastGet(CodeContext context, object obj) {
                int value;
                if (_memo != null) {
                    return TryWriteSlowGet(context, obj);
                } else if (_privMemo.TryGetValue(obj, out value)) {
                    WriteGetOrPut(context, true, value);
                    return true;
                }
                return false;
            }

            private bool TryWriteSlowGet(CodeContext context, object obj) {
                object value;
                if (_memo.TryGetValue(obj, out value)) {
                    WriteGetOrPut(context, true, (PythonTuple)value);
                    return true;
                }
                return false;
            }

            [Documentation("getvalue() -> string\n\n"
                + "Return the value of the internal string. Raises PicklingError if a file object\n"
                + "was passed to this pickler's constructor."
                )]
            public object getvalue(CodeContext/*!*/ context) {
                if (_file is PythonReadableFileOutput) {
                    return ((PythonReadableFileOutput)_file).GetValue(context);
                }
                throw PythonExceptions.CreateThrowable(PicklingError(context), "Attempt to getvalue() a non-list-based pickler");
            }

            #endregion

            #region Save functions

            private void Save(CodeContext/*!*/ context, object obj) {
                if (_persist_id == null || !TrySavePersistId(context, obj)) {
                    PickleFunction pickleFunction;
                    // several typees are never memoized, check for these first.
                    if (obj == null) {
                        SaveNone(this, context, obj);
                    } else if (obj is int) {
                        SaveInteger(this, context, obj);
                    } else if(obj is BigInteger) {
                        SaveLong(this, context, obj);
                    } else if (obj is bool) {
                        SaveBoolean(this, context, obj);
                    } else if (obj is double) {
                        SaveFloat(this, context, obj);
                    } else if(!TryWriteFastGet(context, obj)) {
                        if (obj is string) {
                            // strings are common, specialize them.
                            SaveUnicode(this, context, obj);
                        } else {
                            if (!_dispatchTable.TryGetValue(obj.GetType(), out pickleFunction)) {
                                if (obj is PythonType) {
                                    // treat classes with metaclasses like regular classes
                                    pickleFunction = SaveGlobal;
                                } else {
                                    pickleFunction = SaveObject;
                                }
                            }
                            pickleFunction(this, context, obj);
                        }      
                    }
                }
            }

            private bool TrySavePersistId(CodeContext context, object obj) {
                Debug.Assert(_persist_id != null);
                string res = Converter.ConvertToString(PythonContext.GetContext(context).CallSplat(_persist_id, obj));
                if (res != null) {
                    SavePersId(context, res);
                    return true;
                }
                
                return false;
            }

            private void SavePersId(CodeContext/*!*/ context, string res) {
                if (this.binary != 0) {
                    Save(context, res);
                    Write(context, Opcode.BinPersid);
                } else {
                    Write(context, Opcode.PersId);
                    Write(context, res);
                    Write(context, "\n");
                }
            }

            private static void SaveBoolean(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.Boolean), "arg must be bool");
                if (pickler._protocol < 2) {
                    pickler.Write(context, Opcode.Int);
                    pickler.Write(context, String.Format("0{0}", ((bool)obj) ? 1 : 0));
                    pickler.Write(context, Newline);
                } else {
                    if ((bool)obj) {
                        pickler.Write(context, Opcode.NewTrue);
                    } else {
                        pickler.Write(context, Opcode.NewFalse);
                    }
                }
            }

            private static void SaveDict(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.Dict), "arg must be dict");
                Debug.Assert(!pickler.MemoContains(obj));

                int index = pickler.MemoizeNew(obj);

                if (pickler._protocol < 1) {
                    pickler.Write(context, Opcode.Mark);
                    pickler.Write(context, Opcode.Dict);
                } else {
                    pickler.Write(context, Opcode.EmptyDict);
                }

                pickler.WritePut(context, index);

                pickler.BatchSetItems(context, (PythonDictionary)obj);
            }

            private static void SaveFloat(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.Double), "arg must be float");

                if (pickler._protocol < 1) {
                    pickler.Write(context, Opcode.Float);
                    pickler.WriteFloatAsString(context, obj);
                } else {
                    pickler.Write(context, Opcode.BinFloat);
                    pickler.WriteFloat64(context, obj);
                }
            }

            private static void SaveGlobal(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(
                    DynamicHelpers.GetPythonType(obj).Equals(TypeCache.Function) ||
                    DynamicHelpers.GetPythonType(obj).Equals(TypeCache.BuiltinFunction) ||
                    DynamicHelpers.GetPythonType(obj).Equals(TypeCache.PythonType) ||
                    DynamicHelpers.GetPythonType(obj).IsSubclassOf(TypeCache.PythonType),
                    "arg must be classic class, function, built-in function or method, or new-style type"
                );

                PythonType pt = obj as PythonType;
                if (pt != null) {
                    pickler.SaveGlobalByName(context, obj, pt.Name);
                } else {
                    object name;
                    if (PythonOps.TryGetBoundAttr(context, obj, "__name__", out name)) {
                        pickler.SaveGlobalByName(context, obj, name);
                    } else {
                        throw pickler.CannotPickle(context, obj, "could not determine its __name__");
                    }
                }
            }

            private void SaveGlobalByName(CodeContext/*!*/ context, object obj, object name) {
                Debug.Assert(!MemoContains(obj));

                object moduleName = FindModuleForGlobal(context, obj, name);

                if (_protocol >= 2) {
                    object code;
                    if (PythonCopyReg.GetExtensionRegistry(context).TryGetValue(PythonTuple.MakeTuple(moduleName, name), out code)) {
                        if (IsUInt8(context, code)) {
                            Write(context, Opcode.Ext1);
                            WriteUInt8(context, code);
                        } else if (IsUInt16(context, code)) {
                            Write(context, Opcode.Ext2);
                            WriteUInt16(context, code);
                        } else if (IsInt32(context, code)) {
                            Write(context, Opcode.Ext4);
                            WriteInt32(context, code);
                        } else {
                            throw PythonOps.RuntimeError("unrecognized integer format");
                        }
                        return;
                    }
                }

                MemoizeNew(obj);

                Write(context, Opcode.Global);
                WriteStringPair(context, moduleName, name);
                WritePut(context, obj);
            }            

            private static void SaveInteger(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.Int32), "arg must be int");
                if (pickler._protocol < 1) {
                    pickler.Write(context, Opcode.Int);
                    pickler.WriteIntAsString(context, obj);
                } else {
                    if (IsUInt8(context, obj)) {
                        pickler.Write(context, Opcode.BinInt1);
                        pickler.WriteUInt8(context, obj);
                    } else if (IsUInt16(context, obj)) {
                        pickler.Write(context, Opcode.BinInt2);
                        pickler.WriteUInt16(context, obj);
                    } else if (IsInt32(context, obj)) {
                        pickler.Write(context, Opcode.BinInt);
                        pickler.WriteInt32(context, obj);
                    } else {
                        throw PythonOps.RuntimeError("unrecognized integer format");
                    }
                }
            }

            private static void SaveList(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.List), "arg must be list");
                Debug.Assert(!pickler.MemoContains(obj));

                int index = pickler.MemoizeNew(obj);
                if (pickler._protocol < 1) {
                    pickler.Write(context, Opcode.Mark);
                    pickler.Write(context, Opcode.List);
                } else {
                    pickler.Write(context, Opcode.EmptyList);
                }

                pickler.WritePut(context, index);
                pickler.BatchAppends(context, ((IEnumerable)obj).GetEnumerator());
            }
#if CLR2
            private static readonly BigInteger MaxInt = BigInteger.Create(Int32.MaxValue);
            private static readonly BigInteger MinInt = BigInteger.Create(Int32.MinValue);
#else
            private static readonly BigInteger MaxInt = new BigInteger(Int32.MaxValue);
            private static readonly BigInteger MinInt = new BigInteger(Int32.MinValue);
#endif
            private static void SaveLong(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.BigInteger), "arg must be long");

                BigInteger bi = (BigInteger)obj;
                if (pickler._protocol < 2) {
                    pickler.Write(context, Opcode.Long);
                    pickler.WriteLongAsString(context, obj);
                } else if (bi.IsZero()) {
                    pickler.Write(context, Opcode.Long1);
                    pickler.WriteUInt8(context, 0);
                } else if (bi <= MaxInt && bi >= MinInt) {
                    pickler.Write(context, Opcode.Long1);
                    int value = (int)bi;
                    if (IsInt8(value)) {
                        pickler.WriteUInt8(context, 1);
                        pickler._file.Write(context, (char)(byte)value);
                    } else if (IsInt16(value)) {
                        pickler.WriteUInt8(context, 2);
                        pickler.WriteUInt8(context, value & 0xff);
                        pickler.WriteUInt8(context, (value >> 8) & 0xff);
                    } else {
                        pickler.WriteUInt8(context, 4);
                        pickler.WriteInt32(context, value);
                    }
                } else {
                    byte[] dataBytes = bi.ToByteArray();
                    if (dataBytes.Length < 256) {
                        pickler.Write(context, Opcode.Long1);
                        pickler.WriteUInt8(context, dataBytes.Length);
                    } else {
                        pickler.Write(context, Opcode.Long4);
                        pickler.WriteInt32(context, dataBytes.Length);
                    }

                    foreach (byte b in dataBytes) {
                        pickler.WriteUInt8(context, b);
                    }
                }
            }

            private static void SaveNone(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.Null), "arg must be None");
                pickler.Write(context, Opcode.NoneValue);
            }

            /// <summary>
            /// Call the appropriate reduce method for obj and pickle the object using
            /// the resulting data. Use the first available of
            /// copyreg.dispatch_table[type(obj)], obj.__reduce_ex__, and obj.__reduce__.
            /// </summary>
            private void SaveObject(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(!MemoContains(obj));
                MemoizeNew(obj);

                object reduceCallable, result;
                PythonType objType = DynamicHelpers.GetPythonType(obj);

                if (((IDictionary<object, object>)PythonCopyReg.GetDispatchTable(context)).TryGetValue(objType, out reduceCallable)) {
                    result = PythonCalls.Call(context, reduceCallable, obj);
                } else if (PythonOps.TryGetBoundAttr(context, obj, "__reduce_ex__", out reduceCallable)) {
                    if (obj is PythonType) {
                        result = context.LanguageContext.Call(context, reduceCallable, obj, _protocol);
                    } else {
                        result = context.LanguageContext.Call(context, reduceCallable, _protocol);
                    }
                } else if (PythonOps.TryGetBoundAttr(context, obj, "__reduce__", out reduceCallable)) {
                    if (obj is PythonType) {
                        result = context.LanguageContext.Call(context, reduceCallable, obj);
                    } else {
                        result = context.LanguageContext.Call(context, reduceCallable);
                    }
                } else {
                    throw PythonOps.AttributeError("no reduce function found for {0}", obj);
                }

                if (objType.Equals(TypeCache.String)) {
                    if (!TryWriteFastGet(context, obj)) {
                        SaveGlobalByName(context, obj, result);
                    }
                } else if (result is PythonTuple) {
                    PythonTuple rt = (PythonTuple)result;
                    switch (rt.__len__()) {
                        case 2:
                            SaveReduce(context, obj, reduceCallable, rt[0], rt[1], null, null, null);
                            break;
                        case 3:
                            SaveReduce(context, obj, reduceCallable, rt[0], rt[1], rt[2], null, null);
                            break;
                        case 4:
                            SaveReduce(context, obj, reduceCallable, rt[0], rt[1], rt[2], rt[3], null);
                            break;
                        case 5:
                            SaveReduce(context, obj, reduceCallable, rt[0], rt[1], rt[2], rt[3], rt[4]);
                            break;
                        default:
                            throw CannotPickle(context, obj, "tuple returned by {0} must have to to five elements", reduceCallable);
                    }
                } else {
                    throw CannotPickle(context, obj, "{0} must return string or tuple", reduceCallable);
                }
            }

            /// <summary>
            /// Pickle the result of a reduce function.
            /// 
            /// Only context, obj, func, and reduceCallable are required; all other arguments may be null.
            /// </summary>
            private void SaveReduce(CodeContext/*!*/ context, object obj, object reduceCallable, object func, object args, object state, object listItems, object dictItems) {
                if (!PythonOps.IsCallable(context, func)) {
                    throw CannotPickle(context, obj, "func from reduce() should be callable");
                } else if (!(args is PythonTuple) && args != null) {
                    throw CannotPickle(context, obj, "args from reduce() should be a tuple");
                } else if (listItems != null && !(listItems is IEnumerator)) {
                    throw CannotPickle(context, obj, "listitems from reduce() should be a list iterator");
                } else if (dictItems != null && !(dictItems is IEnumerator)) {
                    throw CannotPickle(context, obj, "dictitems from reduce() should be a dict iterator");
                }

                object funcName;
                string funcNameString;
                if (func is PythonType) {
                    funcNameString = ((PythonType)func).Name;
                } else {
                    if (!PythonOps.TryGetBoundAttr(context, func, "__name__", out funcName)) {
                        throw CannotPickle(context, obj, "func from reduce() ({0}) should have a __name__ attribute");
                    } else if (!Converter.TryConvertToString(funcName, out funcNameString) || funcNameString == null) {
                        throw CannotPickle(context, obj, "__name__ of func from reduce() must be string");
                    }
                }

                if (_protocol >= 2 && "__newobj__" == funcNameString) {
                    if (args == null) {
                        throw CannotPickle(context, obj, "__newobj__ arglist is None");
                    }
                    PythonTuple argsTuple = (PythonTuple)args;
                    if (argsTuple.__len__() == 0) {
                        throw CannotPickle(context, obj, "__newobj__ arglist is empty");
                    } else if (!DynamicHelpers.GetPythonType(obj).Equals(argsTuple[0])) {
                        throw CannotPickle(context, obj, "args[0] from __newobj__ args has the wrong class");
                    }
                    Save(context, argsTuple[0]);
                    Save(context, argsTuple[new Slice(1, null)]);
                    Write(context, Opcode.NewObj);
                } else {
                    Save(context, func);
                    Save(context, args);
                    Write(context, Opcode.Reduce);
                }

                WritePut(context, obj);

                if (state != null) {
                    Save(context, state);
                    Write(context, Opcode.Build);
                }

                if (listItems != null) {
                    BatchAppends(context, (IEnumerator)listItems);
                }

                if (dictItems != null) {
                    BatchSetItems(context, (IEnumerator)dictItems);
                }
            }

            private static void SaveTuple(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.PythonTuple), "arg must be tuple");
                Debug.Assert(!pickler.MemoContains(obj));
                PythonTuple t = (PythonTuple)obj;
                char opcode;
                bool needMark = false;
                int len = t._data.Length;
                if (pickler._protocol > 0 && len == 0) {
                    opcode = Opcode.EmptyTuple;
                } else if (pickler._protocol >= 2 && len == 1) {
                    opcode = Opcode.Tuple1;
                } else if (pickler._protocol >= 2 && len == 2) {
                    opcode = Opcode.Tuple2;
                } else if (pickler._protocol >= 2 && len == 3) {
                    opcode = Opcode.Tuple3;
                } else {
                    opcode = Opcode.Tuple;
                    needMark = true;
                }

                if (needMark) pickler.Write(context, Opcode.Mark);
                var data = t._data;
                for (int i = 0; i < data.Length; i++) {
                    pickler.Save(context, data[i]);
                }

                if (len > 0) {
                    if (pickler.MemoContains(obj)) {
                        // recursive tuple
                        if (pickler._protocol == 1) {
                            pickler.Write(context, Opcode.PopMark);
                        } else {
                            if (pickler._protocol == 0) {
                                pickler.Write(context, Opcode.Pop);
                            }
                            for (int i = 0; i < len; i++) {
                                pickler.Write(context, Opcode.Pop);
                            }
                        }
                        pickler.WriteGet(context, obj);
                        return;
                    }

                    pickler.Write(context, opcode);

                    pickler.Memoize(t);
                    pickler.WritePut(context, t);
                } else {
                    pickler.Write(context, opcode);
                }
            }

            private static void SaveUnicode(PicklerObject/*!*/ pickler, CodeContext/*!*/ context, object obj) {
                Debug.Assert(DynamicHelpers.GetPythonType(obj).Equals(TypeCache.String), "arg must be unicode");
                Debug.Assert(!pickler.MemoContains(obj));

                if (pickler._memo != null) {
                    pickler.MemoizeNew(obj);
                    if (pickler._protocol < 1) {
                        pickler.Write(context, Opcode.Unicode);
                        pickler.WriteUnicodeStringRaw(context, obj);
                    } else {
                        pickler.Write(context, Opcode.BinUnicode);
                        pickler.WriteUnicodeStringUtf8(context, obj);
                    }

                    pickler.WritePut(context, obj);
                } else {
                    var memo = pickler._privMemo[obj] = pickler._privMemo.Count;

                    if (pickler._protocol < 1) {
                        pickler.Write(context, Opcode.Unicode);
                        pickler.WriteUnicodeStringRaw(context, obj);
                    } else {
                        pickler.Write(context, Opcode.BinUnicode);
                        pickler.WriteUnicodeStringUtf8(context, obj);
                    }

                    pickler.WriteGetOrPut(context, false, memo);
                }

            }

            #endregion

            #region Output encoding

            /// <summary>
            /// Write value in pickle decimalnl_short format.
            /// </summary>
            private void WriteFloatAsString(CodeContext/*!*/ context, object value) {                
                Debug.Assert(DynamicHelpers.GetPythonType(value).Equals(TypeCache.Double));
                Write(context, DoubleOps.__repr__(context, (double)value));
                Write(context, Newline);
            }

            /// <summary>
            /// Write value in pickle float8 format.
            /// </summary>
            private void WriteFloat64(CodeContext/*!*/ context, object value) {
                Debug.Assert(DynamicHelpers.GetPythonType(value).Equals(TypeCache.Double));
                Write(context, _float64.pack(context, value));
            }

            /// <summary>
            /// Write value in pickle uint1 format.
            /// </summary>
            private void WriteUInt8(CodeContext/*!*/ context, object value) {
                Debug.Assert(IsUInt8(context, value));

                if (value is int) {
                    Write(context, ScriptingRuntimeHelpers.CharToString((char)(int)(value)));
                } else if (value is BigInteger) {
                    Write(context, ScriptingRuntimeHelpers.CharToString((char)(int)(BigInteger)(value)));
                } else if (value is byte) {
                    // TODO: Shouldn't be here
                    Write(context, ScriptingRuntimeHelpers.CharToString((char)(byte)(value)));
                } else {
                    throw Assert.Unreachable;
                }
            }

            private void WriteUInt8(CodeContext/*!*/ context, int value) {
                _file.Write(context, (char)value);
            }

            /// <summary>
            /// Write value in pickle uint2 format.
            /// </summary>
            private void WriteUInt16(CodeContext/*!*/ context, object value) {
                Debug.Assert(IsUInt16(context, value));
                int iVal = (int)value;
                WriteUInt8(context, iVal & 0xff);
                WriteUInt8(context, (iVal >> 8) & 0xff);
            }

            /// <summary>
            /// Write value in pickle int4 format.
            /// </summary>
            private void WriteInt32(CodeContext/*!*/ context, object value) {
                Debug.Assert(IsInt32(context, value));

                int val = (int)value;
                WriteInt32(context, val);
            }

            private void WriteInt32(CodeContext context, int val) {
                _file.Write(context, val);
            }

            /// <summary>
            /// Write value in pickle decimalnl_short format.
            /// </summary>
            private void WriteIntAsString(CodeContext/*!*/ context, object value) {
                Debug.Assert(IsInt32(context, value));
                Write(context, PythonOps.Repr(context, value));
                Write(context, Newline);
            }

            /// <summary>
            /// Write value in pickle decimalnl_short format.
            /// </summary>
            private void WriteIntAsString(CodeContext/*!*/ context, int value) {
                Write(context, value.ToString());
                Write(context, Newline);
            }

            /// <summary>
            /// Write value in pickle decimalnl_long format.
            /// </summary>
            private void WriteLongAsString(CodeContext/*!*/ context, object value) {
                Debug.Assert(DynamicHelpers.GetPythonType(value).Equals(TypeCache.BigInteger));
                Write(context, PythonOps.Repr(context, value));
                Write(context, Newline);
            }

            /// <summary>
            /// Write value in pickle unicodestringnl format.
            /// </summary>
            private void WriteUnicodeStringRaw(CodeContext/*!*/ context, object value) {
                Debug.Assert(DynamicHelpers.GetPythonType(value).Equals(TypeCache.String));
                // manually escape backslash and newline
                Write(context, StringOps.RawUnicodeEscapeEncode(((string)value).Replace("\\", "\\u005c").Replace("\n", "\\u000a")));
                Write(context, Newline);
            }

            /// <summary>
            /// Write value in pickle unicodestring4 format.
            /// </summary>
            private void WriteUnicodeStringUtf8(CodeContext/*!*/ context, object value) {
                Debug.Assert(DynamicHelpers.GetPythonType(value).Equals(TypeCache.String));
                string strVal = (string)value;

                // if the string contains non-ASCII elements it needs to be re-encoded as UTF8.
                for (int i = 0; i < strVal.Length; i++) {                    
                    if (strVal[i] >= 128) {
                        string encodedString = System.Text.Encoding.UTF8.GetBytes((string)value).MakeString();
                        WriteInt32(context, encodedString.Length);
                        Write(context, encodedString);
                        return;
                    }
                }

                WriteInt32(context, strVal.Length);
                Write(context, strVal);
            }

            /// <summary>
            /// Write value in pickle stringnl_noescape_pair format.
            /// </summary>
            private void WriteStringPair(CodeContext/*!*/ context, object value1, object value2) {
                Debug.Assert(DynamicHelpers.GetPythonType(value1).Equals(TypeCache.String));
                Debug.Assert(DynamicHelpers.GetPythonType(value2).Equals(TypeCache.String));
#if DEBUG
                Debug.Assert(IsPrintableAscii(value1));
                Debug.Assert(IsPrintableAscii(value2));
#endif
                Write(context, (string)value1);
                Write(context, Newline);
                Write(context, (string)value2);
                Write(context, Newline);
            }

            #endregion

            #region Type checking

            /// <summary>
            /// Return true if value is appropriate for formatting in pickle uint1 format.
            /// </summary>
            private static bool IsUInt8(CodeContext/*!*/ context, object value) {
                if (value is int) {
                    return IsUInt8((int)value);
                }
                PythonContext pc = PythonContext.GetContext(context);

                return pc.LessThanOrEqual(0, value) && pc.LessThan(value, 1 << 8);
            }

            private static bool IsUInt8(int value) {
                return (value >= 0 && value < 1 << 8);
            }

            private static bool IsInt8(int value) {
                return (value >= SByte.MinValue && value <= SByte.MaxValue);
            }

            /// <summary>
            /// Return true if value is appropriate for formatting in pickle uint2 format.
            /// </summary>
            private static bool IsUInt16(CodeContext/*!*/ context, object value) {
                if (value is int) {
                    return IsUInt16((int)value);
                }
                PythonContext pc = PythonContext.GetContext(context);

                return pc.LessThanOrEqual(1 << 8, value) && pc.LessThan(value, 1 << 16);
            }

            private static bool IsUInt16(int value) {
                return (value >= 0 && value < 1 << 16);
            }

            private static bool IsInt16(int value) {
                return (value >= short.MinValue && value <= short.MaxValue);
            }

            /// <summary>
            /// Return true if value is appropriate for formatting in pickle int4 format.
            /// </summary>
            private static bool IsInt32(CodeContext/*!*/ context, object value) {
                PythonContext pc = PythonContext.GetContext(context);

                return pc.LessThanOrEqual(Int32.MinValue, value) && pc.LessThanOrEqual(value, Int32.MaxValue);
            }

#if DEBUG
            /// <summary>
            /// Return true if value is a string where each value is in the range of printable ASCII characters.
            /// </summary>
            private bool IsPrintableAscii(object value) {
                Debug.Assert(DynamicHelpers.GetPythonType(value).Equals(TypeCache.String));
                string strValue = (string)value;
                foreach (char c in strValue) {
                    if (!(LowestPrintableChar <= c && c <= HighestPrintableChar)) return false;
                }
                return true;
            }
#endif

            #endregion

            #region Output generation helpers

            private void Write(CodeContext/*!*/ context, string data) {
                _file.Write(context, data);
            }

            private void Write(CodeContext/*!*/ context, char data) {
                _file.Write(context, data);
            }

            private void WriteGet(CodeContext/*!*/ context, object obj) {
                Debug.Assert(MemoContains(obj));

                WriteGetOrPut(context, obj, true);
            }

            private void WriteGetOrPut(CodeContext context, object obj, bool isGet) {
                Debug.Assert(MemoContains(obj));
                
                if (_memo == null) {
                    WriteGetOrPut(context, isGet, _privMemo[obj]);
                } else {
                    WriteGetOrPut(context, isGet, (PythonTuple)_memo[PythonOps.Id(obj)]);
                }
            }

            private void WriteGetOrPut(CodeContext context, bool isGet, PythonTuple tup) {
                object index = tup[0];
                Debug.Assert(PythonContext.GetContext(context).GreaterThanOrEqual(index, 0));
                if (_protocol < 1) {
                    Write(context, isGet ? Opcode.Get : Opcode.Put);
                    WriteIntAsString(context, index);
                } else {
                    if (IsUInt8(context, index)) {
                        Write(context, isGet ? Opcode.BinGet : Opcode.BinPut);
                        WriteUInt8(context, index);
                    } else {
                        Write(context, isGet ? Opcode.LongBinGet : Opcode.LongBinPut);
                        WriteInt32(context, index);
                    }
                }
            }

            private void WriteGetOrPut(CodeContext context, bool isGet, int index) {
                if (_protocol < 1) {
                    Write(context, isGet ? Opcode.Get : Opcode.Put);
                    WriteIntAsString(context, index);
                } else if(index >= 0 && index <= 1 << 8) {
                    Write(context, isGet ? Opcode.BinGet : Opcode.BinPut);
                    WriteUInt8(context, index);
                } else {
                    Write(context, isGet ? Opcode.LongBinGet : Opcode.LongBinPut);
                    WriteInt32(context, index);
                }
            }

            private void WriteInitArgs(CodeContext/*!*/ context, object obj) {
                object getInitArgsCallable;
                if (PythonOps.TryGetBoundAttr(context, obj, "__getinitargs__", out getInitArgsCallable)) {
                    object initArgs = PythonCalls.Call(context, getInitArgsCallable);
                    if (!(initArgs is PythonTuple)) {
                        throw CannotPickle(context, obj, "__getinitargs__() must return tuple");
                    }
                    foreach (object arg in (PythonTuple)initArgs) {
                        Save(context, arg);
                    }
                }
            }

            private void WritePut(CodeContext/*!*/ context, object obj) {
                WriteGetOrPut(context, obj, false);
            }

            private void WritePut(CodeContext/*!*/ context, int index) {
                WriteGetOrPut(context, false, index);
            }

            private void WriteProto(CodeContext/*!*/ context) {
                Write(context, Opcode.Proto);
                WriteUInt8(context, _protocol);
            }

            /// <summary>
            /// Emit a series of opcodes that will set append all items indexed by iter
            /// to the object at the top of the stack. Use APPENDS if possible, but
            /// append no more than BatchSize items at a time.
            /// </summary>
            private void BatchAppends(CodeContext/*!*/ context, IEnumerator enumerator) {
                if (_protocol < 1) {
                    while (enumerator.MoveNext()) {
                        Save(context, enumerator.Current);
                        Write(context, Opcode.Append);
                    }
                } else {
                    object next;
                    if (enumerator.MoveNext()) {
                        next = enumerator.Current;
                    } else {
                        return;
                    }

                    int batchCompleted = 0;
                    object current;

                    // We do a one-item lookahead to avoid emitting an APPENDS for a
                    // single remaining item.
                    while (enumerator.MoveNext()) {
                        current = next;
                        next = enumerator.Current;

                        if (batchCompleted == _BATCHSIZE) {
                            Write(context, Opcode.Appends);
                            batchCompleted = 0;
                        }

                        if (batchCompleted == 0) {
                            Write(context, Opcode.Mark);
                        }

                        Save(context, current);
                        batchCompleted++;
                    }

                    if (batchCompleted == _BATCHSIZE) {
                        Write(context, Opcode.Appends);
                        batchCompleted = 0;
                    }
                    Save(context, next);
                    batchCompleted++;

                    if (batchCompleted > 1) {
                        Write(context, Opcode.Appends);
                    } else {
                        Write(context, Opcode.Append);
                    }
                }
            }

            /// <summary>
            /// Emit a series of opcodes that will set all (key, value) pairs indexed by
            /// iter in the object at the top of the stack. Use SETITEMS if possible,
            /// but append no more than BatchSize items at a time.
            /// </summary>
            private void BatchSetItems(CodeContext/*!*/ context, PythonDictionary dict) {
                KeyValuePair<object, object> kvTuple;
                using (var enumerator = dict._storage.GetEnumerator()) {
                    if (_protocol < 1) {
                        while (enumerator.MoveNext()) {
                            kvTuple = enumerator.Current;
                            Save(context, kvTuple.Key);
                            Save(context, kvTuple.Value);
                            Write(context, Opcode.SetItem);
                        }
                    } else {
                        object nextKey, nextValue;
                        if (enumerator.MoveNext()) {
                            kvTuple = enumerator.Current;
                            nextKey = kvTuple.Key;
                            nextValue = kvTuple.Value;
                        } else {
                            return;
                        }

                        int batchCompleted = 0;
                        object curKey, curValue;

                        // We do a one-item lookahead to avoid emitting a SETITEMS for a
                        // single remaining item.
                        while (enumerator.MoveNext()) {
                            curKey = nextKey;
                            curValue = nextValue;
                            kvTuple = enumerator.Current;
                            nextKey = kvTuple.Key;
                            nextValue = kvTuple.Value;

                            if (batchCompleted == _BATCHSIZE) {
                                Write(context, Opcode.SetItems);
                                batchCompleted = 0;
                            }

                            if (batchCompleted == 0) {
                                Write(context, Opcode.Mark);
                            }

                            Save(context, curKey);
                            Save(context, curValue);
                            batchCompleted++;
                        }

                        if (batchCompleted == _BATCHSIZE) {
                            Write(context, Opcode.SetItems);
                            batchCompleted = 0;
                        }
                        Save(context, nextKey);
                        Save(context, nextValue);
                        batchCompleted++;

                        if (batchCompleted > 1) {
                            Write(context, Opcode.SetItems);
                        } else {
                            Write(context, Opcode.SetItem);
                        }
                    }
                }
            }

            /// <summary>
            /// Emit a series of opcodes that will set all (key, value) pairs indexed by
            /// iter in the object at the top of the stack. Use SETITEMS if possible,
            /// but append no more than BatchSize items at a time.
            /// </summary>
            private void BatchSetItems(CodeContext/*!*/ context, IEnumerator enumerator) {
                PythonTuple kvTuple;
                if (_protocol < 1) {
                    while (enumerator.MoveNext()) {
                        kvTuple = (PythonTuple)enumerator.Current;
                        Save(context, kvTuple[0]);
                        Save(context, kvTuple[1]);
                        Write(context, Opcode.SetItem);
                    }
                } else {
                    object nextKey, nextValue;
                    if (enumerator.MoveNext()) {
                        kvTuple = (PythonTuple)enumerator.Current;
                        nextKey = kvTuple[0];
                        nextValue = kvTuple[1];
                    } else {
                        return;
                    }

                    int batchCompleted = 0;
                    object curKey, curValue;

                    // We do a one-item lookahead to avoid emitting a SETITEMS for a
                    // single remaining item.
                    while (enumerator.MoveNext()) {
                        curKey = nextKey;
                        curValue = nextValue;
                        kvTuple = (PythonTuple)enumerator.Current;
                        nextKey = kvTuple[0];
                        nextValue = kvTuple[1];

                        if (batchCompleted == _BATCHSIZE) {
                            Write(context, Opcode.SetItems);
                            batchCompleted = 0;
                        }

                        if (batchCompleted == 0) {
                            Write(context, Opcode.Mark);
                        }

                        Save(context, curKey);
                        Save(context, curValue);
                        batchCompleted++;
                    }

                    if (batchCompleted == _BATCHSIZE) {
                        Write(context, Opcode.SetItems);
                        batchCompleted = 0;
                    }
                    Save(context, nextKey);
                    Save(context, nextValue);
                    batchCompleted++;

                    if (batchCompleted > 1) {
                        Write(context, Opcode.SetItems);
                    } else {
                        Write(context, Opcode.SetItem);
                    }
                }
            }

            #endregion

            #region Other private helper methods

            private Exception CannotPickle(CodeContext/*!*/ context, object obj, string format, params object[] args) {
                StringBuilder msgBuilder = new StringBuilder();
                msgBuilder.Append("Can't pickle ");
                msgBuilder.Append(PythonOps.ToString(context, obj));
                if (format != null) {
                    msgBuilder.Append(": ");
                    msgBuilder.Append(String.Format(format, args));
                }
                return PythonExceptions.CreateThrowable(PickleError(context), msgBuilder.ToString());
            }

            
            /// <summary>
            /// Find the module for obj and ensure that obj is reachable in that module by the given name.
            /// 
            /// Throw PicklingError if any of the following are true:
            ///  - The module couldn't be determined.
            ///  - The module couldn't be loaded.
            ///  - The given name doesn't exist in the module.
            ///  - The given name is a different object than obj.
            /// 
            /// Otherwise, return the name of the module.
            /// 
            /// To determine which module obj lives in, obj.__module__ is used if available. The
            /// module named by obj.__module__ is loaded if needed. If obj has no __module__
            /// attribute, then each loaded module is searched. If a loaded module has an
            /// attribute with the given name, and that attribute is the same object as obj,
            /// then that module is used.
            /// </summary>
            private object FindModuleForGlobal(CodeContext/*!*/ context, object obj, object name) {
                object module;
                object moduleName;
                PythonType pt = obj as PythonType;
                if (pt != null) {
                    return PythonType.Get__module__(context, pt);
                } else if (PythonOps.TryGetBoundAttr(context, obj, "__module__", out moduleName)) {
                    // TODO: Global SystemState
                    LightExceptions.CheckAndThrow(Builtin.__import__(context, Converter.ConvertToString(moduleName)));

                    object foundObj;
                    if (Importer.TryGetExistingModule(context, Converter.ConvertToString(moduleName), out module) &&
                        PythonOps.TryGetBoundAttr(context, module, Converter.ConvertToString(name), out foundObj)) {
                        if (PythonOps.IsRetBool(foundObj, obj)) {
                            return moduleName;
                        } else {
                            throw CannotPickle(context, obj, "it's not the same object as {0}.{1}", moduleName, name);
                        }
                    } else {
                        throw CannotPickle(context, obj, "it's not found as {0}.{1}", moduleName, name);
                    }
                } else {
                    // No obj.__module__, so crawl through all loaded modules looking for obj
                    foreach (KeyValuePair<object, object> modulePair in context.LanguageContext.SystemStateModules) {
                        moduleName = modulePair.Key;
                        module = modulePair.Value;
                        object foundObj;
                        if (PythonOps.TryGetBoundAttr(context, module, Converter.ConvertToString(name), out foundObj) &&
                            PythonOps.IsRetBool(foundObj, obj)
                        ) {
                            return moduleName;
                        }
                    }
                    throw CannotPickle(context, obj, "could not determine its module");
                }

            }

            #endregion

        }

        #endregion

        #region Unpickler object

        public static UnpicklerObject Unpickler(CodeContext/*!*/ context, object file) {
            return new UnpicklerObject(context, file);
        }

        [Documentation("Unpickler(file) -> Unpickler object\n\n"
            + "An Unpickler object reads a pickle bytecode stream and creates corresponding\n"
            + "objects."
            + "\n"
            + "file: an object (such as an open file or a StringIO) with read(num_chars) and\n"
            + "    readline() methods that return strings"
            )]
        [PythonType("Unpickler"), PythonHidden]
        public class UnpicklerObject {
            private static readonly object _mark = new object();

            private FileInput _file;
            private List<object> _stack;
            private PythonDictionary _memo;
            private List<object> _privMemo;
            private object _pers_loader;

            public UnpicklerObject() {
                _privMemo = new List<object>(200);
            }

            public UnpicklerObject(CodeContext context, object file)
                : this() {
                _file = new PythonFileInput(context, file);
            }

            internal UnpicklerObject(CodeContext context, FileInput input)
                : this() {
                _file = input;
            }

            [Documentation("load() -> unpickled object\n\n"
                + "Read pickle data from the file object that was passed to the constructor and\n"
                + "return the corresponding unpickled objects."
               )]
            public object load(CodeContext/*!*/ context) {
                _stack = new List<object>(32);

                for (; ; ) {
                    var opcode = _file.ReadChar(context);

                    switch (opcode) {
                        case Opcode.Append: LoadAppend(context); break;
                        case Opcode.Appends: LoadAppends(context); break;
                        case Opcode.BinFloat: LoadBinFloat(context); break;
                        case Opcode.BinGet: LoadBinGet(context); break;
                        case Opcode.BinInt: LoadBinInt(context); break;
                        case Opcode.BinInt1: LoadBinInt1(context); break;
                        case Opcode.BinInt2: LoadBinInt2(context); break;
                        case Opcode.BinPersid: LoadBinPersid(context); break;
                        case Opcode.BinPut: LoadBinPut(context); break;
                        case Opcode.BinString: LoadBinString(context); break;
                        case Opcode.BinUnicode: LoadBinUnicode(context); break;
                        case Opcode.Build: LoadBuild(context); break;
                        case Opcode.Dict: LoadDict(context); break;
                        case Opcode.Dup: LoadDup(context); break;
                        case Opcode.EmptyDict: LoadEmptyDict(context); break;
                        case Opcode.EmptyList: LoadEmptyList(context); break;
                        case Opcode.EmptyTuple: LoadEmptyTuple(context); break;
                        case Opcode.Ext1: LoadExt1(context); break;
                        case Opcode.Ext2: LoadExt2(context); break;
                        case Opcode.Ext4: LoadExt4(context); break;
                        case Opcode.Float: LoadFloat(context); break;
                        case Opcode.Get: LoadGet(context); break;
                        case Opcode.Global: LoadGlobal(context); break;
                        case Opcode.Inst: LoadInst(context); break;
                        case Opcode.Int: LoadInt(context); break;
                        case Opcode.List: LoadList(context); break;
                        case Opcode.Long: LoadLong(context); break;
                        case Opcode.Long1: LoadLong1(context); break;
                        case Opcode.Long4: LoadLong4(context); break;
                        case Opcode.LongBinGet: LoadLongBinGet(context); break;
                        case Opcode.LongBinPut: LoadLongBinPut(context); break;
                        case Opcode.Mark: LoadMark(context); break;
                        case Opcode.NewFalse: LoadNewFalse(context); break;
                        case Opcode.NewObj: LoadNewObj(context); break;
                        case Opcode.NewTrue: LoadNewTrue(context); break;
                        case Opcode.NoneValue: LoadNoneValue(context); break;
                        case Opcode.Obj: LoadObj(context); break;
                        case Opcode.PersId: LoadPersId(context); break;
                        case Opcode.Pop: LoadPop(context); break;
                        case Opcode.PopMark: LoadPopMark(context); break;
                        case Opcode.Proto: LoadProto(context); break;
                        case Opcode.Put: LoadPut(context); break;
                        case Opcode.Reduce: LoadReduce(context); break;
                        case Opcode.SetItem: LoadSetItem(context); break;
                        case Opcode.SetItems: LoadSetItems(context); break;
                        case Opcode.ShortBinstring: LoadShortBinstring(context); break;
                        case Opcode.String: LoadString(context); break;
                        case Opcode.Tuple: LoadTuple(context); break;
                        case Opcode.Tuple1: LoadTuple1(context); break;
                        case Opcode.Tuple2: LoadTuple2(context); break;
                        case Opcode.Tuple3: LoadTuple3(context); break;
                        case Opcode.Unicode: LoadUnicode(context); break;
                        case Opcode.Stop: return PopStack();
                        default: throw CannotUnpickle(context, "invalid opcode: {0}", PythonOps.Repr(context, opcode));
                    }
                }
            }

            private object PopStack() {
                var res = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                return res;
            }

            private object PeekStack() {
                return _stack[_stack.Count - 1];
            }

            public object[] StackGetSliceAsArray(int start) {
                object[] res = new object[_stack.Count - start];
                for (int i = 0; i < res.Length; i++) {
                    res[i] = _stack[i + start];
                }
                return res;
            }

            [Documentation("noload() -> unpickled object\n\n"
                // 1234567890123456789012345678901234567890123456789012345678901234567890123456789
                + "Like load(), but don't import any modules or create create any instances of\n"
                + "user-defined types. (Builtin objects such as ints, tuples, etc. are created as\n"
                + "with load().)\n"
                + "\n"
                + "This is primarily useful for scanning a pickle for persistent ids without\n"
                + "incurring the overhead of completely unpickling an object. See the pickle\n"
                + "module documentation for more information about persistent ids."
               )]
            public void noload(CodeContext/*!*/ context) {
                throw PythonOps.NotImplementedError("noload() is not implemented");
            }

            private Exception CannotUnpickle(CodeContext/*!*/ context, string format, params object[] args) {
                return PythonExceptions.CreateThrowable(UnpicklingError(context), String.Format(format, args));
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
            public PythonDictionary memo {
                get {
                    if (_memo == null) {
                        var newMemo = new PythonDictionary();
                        for (int i = 0; i < _privMemo.Count; i++) {
                            if (_privMemo[i] != _mark) {
                                newMemo[i] = _privMemo[i];
                            }
                        }
                        _memo = newMemo;
                    }
                    return _memo; 
                }
                set { 
                    _memo = value;
                    _privMemo = null;
                }
            }

            public object persistent_load {
                get {
                    return _pers_loader;
                }
                set {
                    _pers_loader = value;
                }
            }

            private object MemoGet(CodeContext/*!*/ context, int key) {
                object value;
                if (_memo != null) {
                    if (_memo.TryGetValue(key, out value)) {
                        return value;
                    }
                } else if (key < _privMemo.Count && (value = _privMemo[key]) != _mark) {
                    return value;
                }
                
                throw PythonExceptions.CreateThrowable(BadPickleGet(context), String.Format("memo key {0} not found", key));
            }

            private void MemoPut(int key, object value) {
                if (_memo != null) {
                    _memo[key] = value;
                } else {
                    while (key >= _privMemo.Count) {
                        _privMemo.Add(_mark);
                    }
                    _privMemo[key] = value;
                }
            }

            private int GetMarkIndex(CodeContext/*!*/ context) {
                int i = _stack.Count - 1;
                while (i > 0 && _stack[i] != _mark) i -= 1;
                if (i == -1) throw CannotUnpickle(context, "mark not found");
                return i;
            }

            private string Read(CodeContext/*!*/ context, int size) {
                string res = _file.Read(context, size);
                if (res.Length < size) {
                    throw PythonOps.EofError("unexpected EOF while unpickling");
                }
                return res;
            }

            private string ReadLineNoNewline(CodeContext/*!*/ context) {
                string raw = _file.ReadLine(context);
                return raw.Substring(0, raw.Length - 1);
            }

            private object ReadFloatString(CodeContext/*!*/ context) {
                return DoubleOps.__new__(context, TypeCache.Double, ReadLineNoNewline(context));
            }

            private double ReadFloat64(CodeContext/*!*/ context) {
                int index = 0;
                return PythonStruct.CreateDoubleValue(context, ref index, false, Read(context, 8));
            }

            private object ReadIntFromString(CodeContext/*!*/ context) {
                string raw = ReadLineNoNewline(context);
                if ("00" == raw) return ScriptingRuntimeHelpers.False;
                else if ("01" == raw) return ScriptingRuntimeHelpers.True;
                return Int32Ops.__new__(context, TypeCache.Int32, raw);
            }

            private int ReadInt32(CodeContext/*!*/ context) {
                return _file.ReadInt(context);
            }

            private object ReadLongFromString(CodeContext/*!*/ context) {
                return BigIntegerOps.__new__(context, TypeCache.BigInteger, ReadLineNoNewline(context));
            }

            private object ReadLong(CodeContext/*!*/ context, int size) {
                return new BigInteger(Read(context, size).MakeByteArray());
            }

            private char ReadUInt8(CodeContext/*!*/ context) {
                return _file.ReadChar(context);
            }

            private ushort ReadUInt16(CodeContext/*!*/ context) {
                int index = 0;
                return PythonStruct.CreateUShortValue(context, ref index, true, Read(context, 2));
            }

            public object find_global(CodeContext/*!*/ context, object module, object attr) {
                object moduleObject;
                if (!Importer.TryGetExistingModule(context, Converter.ConvertToString(module), out moduleObject)) {
                    LightExceptions.CheckAndThrow(Builtin.__import__(context, Converter.ConvertToString(module)));
                    moduleObject = context.LanguageContext.SystemStateModules[module];
                }
                return PythonOps.GetBoundAttr(context, moduleObject, Converter.ConvertToString(attr));
            }

            private object MakeInstance(CodeContext/*!*/ context, object cls, object[] args) {
                return PythonOps.CallWithContext(context, cls, args);
            }

            private void PopMark(int markIndex) {
                for (int i = _stack.Count - 1; i >= markIndex; i--) {
                    _stack.RemoveAt(i);
                }
            }

            /// <summary>
            /// Interpret everything from markIndex to the top of the stack as a sequence
            /// of key, value, key, value, etc. Set dict[key] = value for each. Pop
            /// everything from markIndex up when done.
            /// </summary>
            private void SetItems(PythonDictionary dict, int markIndex) {
                var storage = dict._storage;
                storage.EnsureCapacityNoLock((_stack.Count - (markIndex + 1)) / 2);
                for (int i = markIndex + 1; i < _stack.Count; i += 2) {
                    storage.AddNoLock(ref dict._storage, _stack[i], _stack[i + 1]);
                }
                PopMark(markIndex);
            }

            private void LoadAppend(CodeContext/*!*/ context) {
                object item = PopStack();
                object seq = PeekStack();
                if (seq is List) {
                    ((List)seq).append(item);
                } else {
                    PythonCalls.Call(context, PythonOps.GetBoundAttr(context, seq, "append"), item);
                }
            }

            private void LoadAppends(CodeContext/*!*/ context) {
                int markIndex = GetMarkIndex(context);
                List seq = (List)_stack[markIndex - 1];
                for (int i = markIndex + 1; i < _stack.Count; i++) {
                    seq.AddNoLock(_stack[i]);
                }
                PopMark(markIndex);
            }

            private void LoadBinFloat(CodeContext/*!*/ context) {
                _stack.Add(ReadFloat64(context));
            }

            private void LoadBinGet(CodeContext/*!*/ context) {
                _stack.Add(MemoGet(context, ReadUInt8(context)));
            }

            private void LoadBinInt(CodeContext/*!*/ context) {
                _stack.Add(ReadInt32(context));
            }

            private void LoadBinInt1(CodeContext/*!*/ context) {
                _stack.Add((int)ReadUInt8(context));
            }

            private void LoadBinInt2(CodeContext/*!*/ context) {
                _stack.Add((int)ReadUInt16(context));
            }

            private void LoadBinPersid(CodeContext/*!*/ context) {
                if (_pers_loader == null) throw CannotUnpickle(context, "cannot unpickle binary persistent ID w/o persistent_load");

                _stack.Add(PythonContext.GetContext(context).CallSplat(_pers_loader, PopStack()));
            }

            private void LoadBinPut(CodeContext/*!*/ context) {
                MemoPut(ReadUInt8(context), PeekStack());
            }

            private void LoadBinString(CodeContext/*!*/ context) {
                _stack.Add(Read(context, ReadInt32(context)));
            }

            private void LoadBinUnicode(CodeContext/*!*/ context) {
                string text = Read(context, ReadInt32(context));
                for (int i = 0; i < text.Length; i++) {
                    if (text[i] >= 128) {
                        _stack.Add(StringOps.decode(context, text, "utf-8", "strict"));
                        return;
                    }
                }
                _stack.Add(text);
            }

            private void LoadBuild(CodeContext/*!*/ context) {
                object arg = PopStack();
                object inst = PeekStack();
                object setStateCallable;
                if (PythonOps.TryGetBoundAttr(context, inst, "__setstate__", out setStateCallable)) {
                    PythonOps.CallWithContext(context, setStateCallable, arg);
                    return;
                }

                PythonDictionary dict;
                PythonDictionary slots;
                if (arg == null) {
                    dict = null;
                    slots = null;
                } else if (arg is PythonDictionary) {
                    dict = (PythonDictionary)arg;
                    slots = null;
                } else if (arg is PythonTuple) {
                    PythonTuple argsTuple = (PythonTuple)arg;
                    if (argsTuple.__len__() != 2) {
                        throw PythonOps.ValueError("state for object without __setstate__ must be None, dict, or 2-tuple");
                    }
                    dict = (PythonDictionary)argsTuple[0];
                    slots = (PythonDictionary)argsTuple[1];
                } else {
                    throw PythonOps.ValueError("state for object without __setstate__ must be None, dict, or 2-tuple");
                }

                if (dict != null) {
                    object instDict;
                    if (PythonOps.TryGetBoundAttr(context, inst, "__dict__", out instDict)) {
                        PythonDictionary realDict = instDict as PythonDictionary;
                        if (realDict != null) {
                            realDict.update(context, dict);
                        } else {
                            object updateCallable;
                            if (PythonOps.TryGetBoundAttr(context, instDict, "update", out updateCallable)) {
                                PythonOps.CallWithContext(context, updateCallable, dict);
                            } else {
                                throw CannotUnpickle(context, "could not update __dict__ {0} when building {1}", dict, inst);
                            }
                        }
                    }
                }

                if (slots != null) {
                    foreach (object key in (IEnumerable)slots) {
                        PythonOps.SetAttr(context, inst, (string)key, slots[key]);
                    }
                }
            }

            private void LoadDict(CodeContext/*!*/ context) {
                int markIndex = GetMarkIndex(context);
                PythonDictionary dict = new PythonDictionary((_stack.Count - 1 - markIndex) / 2);
                SetItems(dict, markIndex);
                _stack.Add(dict);
            }

            private void LoadDup(CodeContext/*!*/ context) {
                _stack.Add(PeekStack());
            }

            private void LoadEmptyDict(CodeContext/*!*/ context) {
                _stack.Add(new PythonDictionary(new CommonDictionaryStorage()));
            }

            private void LoadEmptyList(CodeContext/*!*/ context) {
                _stack.Add(PythonOps.MakeList());
            }

            private void LoadEmptyTuple(CodeContext/*!*/ context) {
                _stack.Add(PythonTuple.MakeTuple());
            }

            private void LoadExt1(CodeContext/*!*/ context) {
                PythonTuple global = (PythonTuple)PythonCopyReg.GetInvertedRegistry(context)[(int)ReadUInt8(context)];
                _stack.Add(find_global(context, global[0], global[1]));
            }

            private void LoadExt2(CodeContext/*!*/ context) {
                PythonTuple global = (PythonTuple)PythonCopyReg.GetInvertedRegistry(context)[(int)ReadUInt16(context)];
                _stack.Add(find_global(context, global[0], global[1]));
            }

            private void LoadExt4(CodeContext/*!*/ context) {
                PythonTuple global = (PythonTuple)PythonCopyReg.GetInvertedRegistry(context)[ReadInt32(context)];
                _stack.Add(find_global(context, global[0], global[1]));
            }

            private void LoadFloat(CodeContext/*!*/ context) {
                _stack.Add(ReadFloatString(context));
            }

            private void LoadGet(CodeContext/*!*/ context) {
                try {
                    _stack.Add(MemoGet(context, (int)ReadIntFromString(context)));
                } catch (ArgumentException) {
                    throw PythonExceptions.CreateThrowable(BadPickleGet(context), "while executing GET: invalid integer value");
                }
            }

            private void LoadGlobal(CodeContext/*!*/ context) {
                string module = ReadLineNoNewline(context);
                string attr = ReadLineNoNewline(context);
                _stack.Add(find_global(context, module, attr));
            }

            private void LoadInst(CodeContext/*!*/ context) {
                LoadGlobal(context);
                object cls = PopStack();
                if (cls is PythonType) {
                    int markIndex = GetMarkIndex(context);
                    object[] args = StackGetSliceAsArray(markIndex + 1);
                    PopMark(markIndex);

                    _stack.Add(MakeInstance(context, cls, args));
                } else {
                    throw PythonOps.TypeError("expected class or type after INST, got {0}", DynamicHelpers.GetPythonType(cls));
                }
            }

            private void LoadInt(CodeContext/*!*/ context) {
                _stack.Add(ReadIntFromString(context));
            }

            private void LoadList(CodeContext/*!*/ context) {
                int markIndex = GetMarkIndex(context);
                List list = List.FromArrayNoCopy(StackGetSliceAsArray(markIndex + 1));
                PopMark(markIndex);
                _stack.Add(list);
            }

            private void LoadLong(CodeContext/*!*/ context) {
                _stack.Add(ReadLongFromString(context));
            }

            private void LoadLong1(CodeContext/*!*/ context) {
                int size = ReadUInt8(context);
                if (size == 4) {
                    _stack.Add((BigInteger)ReadInt32(context));
                } else {
                    _stack.Add(ReadLong(context, size));
                }
            }

            private void LoadLong4(CodeContext/*!*/ context) {
                _stack.Add(ReadLong(context, ReadInt32(context)));
            }

            private void LoadLongBinGet(CodeContext/*!*/ context) {
                _stack.Add(MemoGet(context, (int)ReadInt32(context)));
            }

            private void LoadLongBinPut(CodeContext/*!*/ context) {
                MemoPut(ReadInt32(context), PeekStack());
            }

            private void LoadMark(CodeContext/*!*/ context) {
                _stack.Add(_mark);
            }

            private void LoadNewFalse(CodeContext/*!*/ context) {
                _stack.Add(ScriptingRuntimeHelpers.False);
            }

            private void LoadNewObj(CodeContext/*!*/ context) {
                PythonTuple args = PopStack() as PythonTuple;
                if (args == null) {
                    throw PythonOps.TypeError("expected tuple as second argument to NEWOBJ, got {0}", DynamicHelpers.GetPythonType(args));
                }

                PythonType cls = PopStack() as PythonType;
                if (args == null) {
                    throw PythonOps.TypeError("expected new-style type as first argument to NEWOBJ, got {0}", DynamicHelpers.GetPythonType(args));
                }

                PythonTypeSlot dts;
                object value;
                if (cls.TryResolveSlot(context, "__new__", out dts) &&
                    dts.TryGetValue(context, null, cls, out value)) {
                    object[] newargs = new object[args.__len__() + 1];
                    ((ICollection)args).CopyTo(newargs, 1);
                    newargs[0] = cls;

                    _stack.Add(PythonOps.CallWithContext(context, value, newargs));
                    return;
                }
                
                throw PythonOps.TypeError("didn't find __new__");
            }

            private void LoadNewTrue(CodeContext/*!*/ context) {
                _stack.Add(ScriptingRuntimeHelpers.True);
            }

            private void LoadNoneValue(CodeContext/*!*/ context) {
                _stack.Add(null);
            }

            private void LoadObj(CodeContext/*!*/ context) {
                int markIndex = GetMarkIndex(context);
                if ((markIndex + 1) >= _stack.Count) {
                    throw PythonExceptions.CreateThrowable(UnpicklingError(context), "could not find MARK");
                }

                object cls = _stack[markIndex + 1];
                if (cls is PythonType) {
                    object[] args = StackGetSliceAsArray(markIndex + 2);
                    PopMark(markIndex);
                    _stack.Add(MakeInstance(context, cls, args));
                } else {
                    throw PythonOps.TypeError("expected class or type as first argument to INST, got {0}", DynamicHelpers.GetPythonType(cls));
                }
            }

            private void LoadPersId(CodeContext/*!*/ context) {
                if (_pers_loader == null) {
                    throw CannotUnpickle(context, "A load persistent ID instruction is present but no persistent_load function is available");
                }
                _stack.Add(PythonContext.GetContext(context).CallSplat(_pers_loader, ReadLineNoNewline(context)));
            }

            private void LoadPop(CodeContext/*!*/ context) {
                PopStack();
            }

            private void LoadPopMark(CodeContext/*!*/ context) {
                PopMark(GetMarkIndex(context));
            }

            private void LoadProto(CodeContext/*!*/ context) {
                int proto = ReadUInt8(context);
                if (proto > 2) throw PythonOps.ValueError("unsupported pickle protocol: {0}", proto);
                // discard result
            }

            private void LoadPut(CodeContext/*!*/ context) {
                MemoPut((int)ReadIntFromString(context), PeekStack());
            }

            private void LoadReduce(CodeContext/*!*/ context) {
                object args = PopStack();
                object callable = PopStack();
                if (args == null) {
                    _stack.Add(PythonCalls.Call(context, PythonOps.GetBoundAttr(context, callable, "__basicnew__")));
                } else if (args.GetType() != typeof(PythonTuple)) {
                    throw PythonOps.TypeError(
                        "while executing REDUCE, expected tuple at the top of the stack, but got {0}",
                        DynamicHelpers.GetPythonType(args)
                    );
                }

                _stack.Add(PythonCalls.Call(context, callable, ((PythonTuple)args)._data));
            }

            private void LoadSetItem(CodeContext/*!*/ context) {
                object value = PopStack();
                object key = PopStack();
                PythonDictionary dict = PeekStack() as PythonDictionary;
                if (dict == null) {
                    throw PythonOps.TypeError(
                        "while executing SETITEM, expected dict at stack[-3], but got {0}",
                        DynamicHelpers.GetPythonType(PeekStack())
                    );
                }
                dict[key] = value;
            }

            private void LoadSetItems(CodeContext/*!*/ context) {
                int markIndex = GetMarkIndex(context);
                PythonDictionary dict = _stack[markIndex - 1] as PythonDictionary;
                
                if (dict == null) {
                    throw PythonOps.TypeError(
                        "while executing SETITEMS, expected dict below last mark, but got {0}",
                        DynamicHelpers.GetPythonType(_stack[markIndex - 1])
                    );
                }
                SetItems(dict, markIndex);
            }

            private void LoadShortBinstring(CodeContext/*!*/ context) {
                _stack.Add(Read(context, ReadUInt8(context)));
            }

            private void LoadString(CodeContext/*!*/ context) {
                string repr = ReadLineNoNewline(context);
                if (repr.Length < 2 ||
                    !(
                    repr[0] == '"' && repr[repr.Length - 1] == '"' ||
                    repr[0] == '\'' && repr[repr.Length - 1] == '\''
                    )
                ) {
                    throw PythonOps.ValueError("while executing STRING, expected string that starts and ends with quotes");
                }
                _stack.Add(StringOps.decode(context, repr.Substring(1, repr.Length - 2), "string-escape", "strict"));
            }

            private void LoadTuple(CodeContext/*!*/ context) {
                int markIndex = GetMarkIndex(context);
                PythonTuple tuple = PythonTuple.MakeTuple(StackGetSliceAsArray(markIndex + 1));
                PopMark(markIndex);
                _stack.Add(tuple);
            }

            private void LoadTuple1(CodeContext/*!*/ context) {
                object item0 = PopStack();
                _stack.Add(PythonTuple.MakeTuple(item0));
            }

            private void LoadTuple2(CodeContext/*!*/ context) {
                object item1 = PopStack();
                object item0 = PopStack();
                _stack.Add(PythonTuple.MakeTuple(item0, item1));
            }

            private void LoadTuple3(CodeContext/*!*/ context) {
                object item2 = PopStack();
                object item1 = PopStack();
                object item0 = PopStack();
                _stack.Add(PythonTuple.MakeTuple(item0, item1, item2));
            }

            private void LoadUnicode(CodeContext/*!*/ context) {
                _stack.Add(StringOps.decode(context, ReadLineNoNewline(context), "raw-unicode-escape", "strict"));
            }
        }

        #endregion

        private static PythonType PicklingError(CodeContext/*!*/ context) {
            return (PythonType)PythonContext.GetContext(context).GetModuleState("PicklingError");
        }

        private static PythonType PickleError(CodeContext/*!*/ context) {
            return (PythonType)PythonContext.GetContext(context).GetModuleState("PickleError");
        }

        private static PythonType UnpicklingError(CodeContext/*!*/ context) {
            return (PythonType)PythonContext.GetContext(context).GetModuleState("UnpicklingError");
        }

        private static PythonType BadPickleGet(CodeContext/*!*/ context) {
            return (PythonType)PythonContext.GetContext(context).GetModuleState("BadPickleGet");
        }

        class ReferenceEqualityComparer : IEqualityComparer<object> {
            public static ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            #region IEqualityComparer<object> Members

            public new bool Equals(object x, object y) {
                return x == y;
            }

            public int GetHashCode(object obj) {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }

            #endregion
        }

    }
}
