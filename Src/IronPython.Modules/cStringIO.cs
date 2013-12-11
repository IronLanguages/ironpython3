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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("cStringIO", typeof(IronPython.Modules.PythonStringIO))]
namespace IronPython.Modules {
    class StringStream {        
        private StringBuilder _data;        // builder used for reading/writing
        private string _lastValue;          // a cached copy of the builder in string form
        private int _position;              // our current position in the builder

        public StringStream(string data) {
            _data = new StringBuilder(_lastValue = data);
            _position = 0;
        }

        public bool EOF {
            get { return _position >= _data.Length; }
        }

        public int Position {
            get { return _position; }
        }

        public string Data {
            get {
                if (_lastValue == null) {
                    _lastValue = _data.ToString();
                }
                return _lastValue;
            }
        }

        public string Prefix {
            get {
                return _data.ToString(0, _position);
            }
        }

        public string Read(int i) {
            if (_position + i > _data.Length) {
                i = _data.Length - _position;
            }
            string ret = _data.ToString(_position, i);
            _position += i;
            return ret;
        }

        public string ReadLine(int size) {
            if (size < 0) {
                size = Int32.MaxValue;
            }
            int i = _position;
            int count = 0;
            while (i < _data.Length && count < size) {
                char c = _data[i];
                if (c == '\n' || c == '\r') {
                    i++;
                    if (c == '\r' && _position < _data.Length && _data[i] == '\n') {
                        i++;
                    }
                    // preserve newline character like StringIO

                    string res = _data.ToString(_position, i - _position);
                    _position = i;
                    return res;
                }
                i++;
                count++;
            }

            if (i > _position) {
                string res = _data.ToString(_position, i - _position);
                _position = i;
                return res;
            }

            return "";
        }

        public string ReadToEnd() {
            if (_position < _data.Length) {                
                string res = _data.ToString(_position, _data.Length - _position);
                
                _position = _data.Length;
                return res;
            } 
            
            return String.Empty;
        }

        public void Reset() {
            _position = 0;
        }

        public int Seek(int offset, SeekOrigin origin) {
            switch (origin) {
                case SeekOrigin.Begin:
                    _position = offset; break;
                case SeekOrigin.Current:
                    _position = _position + offset; break;
                case SeekOrigin.End:
                    _position = _data.Length + offset; break;
                default:
                    throw new ValueErrorException("origin");
            }

            return _position;
        }

        public void Truncate() {
            _lastValue = null;
            _data.Length = _position;
        }

        public void Truncate(int size) {
            _lastValue = null;
            if (size > _data.Length) {
                size = _data.Length;
            } else if (size < 0) {
                throw PythonOps.IOError("(22, 'Negative size not allowed')");
            }
            _data.Length = size;
            _position = size;
        }

        internal void Write(string s) {
            if (_data.Length < _position) {
                _data.Length = _position;
            }
            _lastValue = null;
            if (_position == _data.Length) {
                _data.Append(s);
            } else {
                // replace the existing text
                _data.Remove(_position, Math.Min(s.Length, _data.Length - _position));
                _data.Insert(_position, s);
            }
            _position += s.Length;
        }
    }

    public static class PythonStringIO {
        public static PythonType InputType = DynamicHelpers.GetPythonTypeFromType(typeof(StringI));
        public static PythonType OutputType = DynamicHelpers.GetPythonTypeFromType(typeof(StringO));

        public const string __doc__ = "Provides file like objects for reading and writing to strings.";

        [PythonType, PythonHidden]
        public class StringI : IEnumerator<string>, IEnumerator {
            private StringStream _sr;
            private string _enumValue;

            internal StringI(string data) {
                _sr = new StringStream(data);
            }

            public void close() {
                _sr = null;
            }

            public bool closed {
                get {
                    return _sr == null;
                }
            }

            public void flush() {
                ThrowIfClosed();
            }

            public string getvalue() {
                ThrowIfClosed();
                return _sr.Data;
            }

            public string getvalue(bool usePos) {
                return _sr.Prefix;
            }

            public bool isatty() {
                ThrowIfClosed();
                return false;
            }

            public object __iter__() {
                return this;
            }

            public string next() {
                ThrowIfClosed();
                if (_sr.EOF) {
                    throw PythonOps.StopIteration();
                }
                return readline();
            }

            public string read() {
                ThrowIfClosed();
                return _sr.ReadToEnd();
            }

            public string read(int s) {
                ThrowIfClosed();
                return (s < 0) ? _sr.ReadToEnd() : _sr.Read(s);
            }

            public string readline() {
                ThrowIfClosed();
                return _sr.ReadLine(-1);
            }

            public string readline(int size) {
                ThrowIfClosed();
                return _sr.ReadLine(size);
            }

            public List readlines() {
                ThrowIfClosed();
                List list = PythonOps.MakeList();
                while (!_sr.EOF) {
                    list.AddNoLock(readline());
                }
                return list;
            }

            public List readlines(int size) {
                ThrowIfClosed();
                List list = PythonOps.MakeList();
                while (!_sr.EOF) {
                    string line = readline();
                    list.AddNoLock(line);
                    if (line.Length >= size) break;
                    size -= line.Length;
                }
                return list;
            }

            public void reset() {
                ThrowIfClosed();
                _sr.Reset();
            }

            public void seek(int position) {
                seek(position, 0);
            }

            public void seek(int position, int mode) {
                ThrowIfClosed();
                SeekOrigin so;
                switch (mode) {
                    case 1: so = SeekOrigin.Current; break;
                    case 2: so = SeekOrigin.End; break;
                    default: so = SeekOrigin.Begin; break;
                }
                _sr.Seek(position, so);
            }

            public int tell() {
                ThrowIfClosed();
                return _sr.Position;
            }

            public void truncate() {
                ThrowIfClosed();
                _sr.Truncate();
            }

            public void truncate(int size) {
                ThrowIfClosed();
                _sr.Truncate(size);
            }

            private void ThrowIfClosed() {
                if (closed) {
                    throw PythonOps.ValueError("I/O operation on closed file");
                }
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get { return _enumValue; }
            }

            bool IEnumerator.MoveNext() {
                if (!_sr.EOF) {
                    _enumValue = readline();
                    return true;
                }
                _enumValue = null;
                return false;
            }

            void IEnumerator.Reset() {
                throw new NotImplementedException();
            }

            #endregion

            #region IEnumerator<string> Members

            string IEnumerator<string>.Current {
                get { return _enumValue; }
            }

            #endregion

            #region IDisposable Members

            void IDisposable.Dispose() {
            }

            #endregion
        }

        [PythonType, PythonHidden, DontMapIEnumerableToContains]
        public class StringO : IEnumerator<string>, IEnumerator {
            private StringStream _sr = new StringStream("");
            private int _softspace;
            private string _enumValue;

            internal StringO() {
            }

            public object __iter__() {
                return this;
            }

            public void close() {
                if (_sr != null) { _sr = null; }
            }

            public bool closed {
                get {
                    return _sr == null;
                }
            }

            public void flush() {
            }

            public string getvalue() {
                ThrowIfClosed();
                return _sr.Data;
            }

            public string getvalue(bool usePos) {
                ThrowIfClosed();
                return _sr.Prefix;
            }

            public bool isatty() {
                ThrowIfClosed();
                return false;
            }

            public string next() {
                ThrowIfClosed();
                if (_sr.EOF) {
                    throw PythonOps.StopIteration();
                }
                return readline();
            }

            public string read() {
                ThrowIfClosed();
                return _sr.ReadToEnd();
            }

            public string read(int i) {
                ThrowIfClosed();
                return (i < 0) ? _sr.ReadToEnd() : _sr.Read(i);
            }

            public string readline() {
                ThrowIfClosed();
                return _sr.ReadLine(-1);
            }

            public string readline(int size) {
                ThrowIfClosed();
                return _sr.ReadLine(size);
            }

            public List readlines() {
                ThrowIfClosed();
                List list = PythonOps.MakeList();
                while (!_sr.EOF) {
                    list.AddNoLock(readline());
                }
                return list;
            }

            public List readlines(int size) {
                ThrowIfClosed();
                List list = PythonOps.MakeList();
                while (!_sr.EOF) {
                    string line = readline();
                    list.AddNoLock(line);
                    if (line.Length >= size) break;
                    size -= line.Length;
                }
                return list;
            }

            public void reset() {
                ThrowIfClosed();
                _sr.Reset();
            }

            public void seek(int position) {
                seek(position, 0);
            }

            public void seek(int offset, int origin) {
                ThrowIfClosed();
                SeekOrigin so;
                switch (origin) {
                    case 1: so = SeekOrigin.Current; break;
                    case 2: so = SeekOrigin.End; break;
                    default: so = SeekOrigin.Begin; break;
                }
                _sr.Seek(offset, so);
            }

            public int softspace {
                get { return _softspace; }
                set { _softspace = value; }
            }

            public int tell() {
                ThrowIfClosed();
                return _sr.Position;
            }

            public void truncate() {
                ThrowIfClosed();
                _sr.Truncate();
            }

            public void truncate(int size) {
                ThrowIfClosed();
                _sr.Truncate(size);
            }

            public void write(string s) {
                if (s == null) {
                    throw PythonOps.TypeError("write argument must be a string or read-only character buffer, not None");
                }

                ThrowIfClosed();
                _sr.Write(s);
            }

            public void write([NotNull]PythonBuffer buffer) {
                _sr.Write(buffer.ToString());
            }

            public void writelines(object o) {
                IEnumerator e = PythonOps.GetEnumerator(o);
                while (e.MoveNext()) {
                    string s = e.Current as string;
                    if (s == null) {
                        throw PythonOps.TypeError("string expected");
                    }
                    write(s);
                }
            }

            private void ThrowIfClosed() {
                if (closed) {
                    throw PythonOps.ValueError("I/O operation on closed file");
                }
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get { return _enumValue; }
            }

            bool IEnumerator.MoveNext() {
                if (!_sr.EOF) {
                    _enumValue = readline();
                    return true;
                }
                _enumValue = null;
                return false;
            }

            void IEnumerator.Reset() {
                throw new NotImplementedException();
            }

            #endregion

            #region IEnumerator<string> Members

            string IEnumerator<string>.Current {
                get { return _enumValue; }
            }

            #endregion

            #region IDisposable Members

            void IDisposable.Dispose() {
            }

            #endregion
        }

        public static object StringIO() {
            return new StringO();
        }

        public static object StringIO(string data) {
            return new StringI(data);
        }
    }
}
