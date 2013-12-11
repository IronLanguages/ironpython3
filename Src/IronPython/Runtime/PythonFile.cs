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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using Microsoft.Win32.SafeHandles;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;

#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Runtime {

    #region Readers

    // The following set of classes is used to translate between pythonic file stream semantics and those of
    // the runtime and the underlying system.
    //
    // Python supports opening files in binary and text mode. Binary is fairly obvious: we want to preserve
    // the data as is, to the point where it should be possible to round-trip an arbitary binary file without
    // introducing corruptions.
    //
    // Text mode is more complex. Python further subdivides this class into the regular text mode where the
    // newline convention is defined by the underlying system, and universal newline mode where python will
    // treat '\n', '\r' and '\r\n' as equivalently terminating a line. In all these text modes reading from
    // the file will translate the associated newline format into '\n' and writing will convert '\n' back to
    // the original newline format.
    //
    // We want to support all these modes and also not tie ourselves to a particular platform. So although
    // Win32 always terminates lines with '\r\n' we want to support running on platforms where '\r' or '\n' is
    // the terminator as well. Further, we don't wish to bog down the performance of the implementation by
    // checking the newline semantics throughout the code. So instead we define abstract reader and writer
    // classes that roughly support the APIs and semantics that python needs and provide a set of
    // implementations of those classes that match the mode selected at runtime.
    //
    // The classes defined below have the following hierarchy:
    //
    //      PythonStreamReader          :: Abstract reader APIs
    //          PythonBinaryReader      :: Read binary data
    //          PythonTextCRLFReader    :: Read text data with lines terminated with '\r\n'
    //          PythonTextCRReader      :: Read text data with lines terminated with '\r'
    //          PythonTextLFReader      :: Read text data with lines terminated with '\n'
    //          PythonUniversalReader   :: Read text data with lines terminated with '\r\n', '\r' or '\n'
    //      PythonStreamWriter          :: Abstract writer APIs
    //          PythonBinaryWriter      :: Write binary data
    //          PythonTextCRLFWriter    :: Write text data with lines terminated with '\r\n'
    //          PythonTextCRWriter      :: Write text data with lines terminated with '\r'
    //          PythonTextLFWriter      :: Write text data with lines terminated with '\n'
    //
    // Note that there is no universal newline write mode since there's no reasonable way to define this.

    // The abstract reader API.
    internal abstract class PythonStreamReader {

        protected Encoding _encoding;

        public Encoding Encoding { get { return _encoding; } }
        public abstract TextReader TextReader { get; }

        public PythonStreamReader(Encoding encoding) {
            _encoding = encoding;
        }

        // Read at most size characters and return the result as a string.
        public abstract String Read(int size);

        // Read until the end of the stream and return the result as a single string.
        public abstract String ReadToEnd();

        // Read characters up to and including the mode defined newline (or until EOF, in which case the
        // string will not be newline terminated).
        public abstract String ReadLine();

        // Read characters up to and including the mode defined newline (or until EOF or the given size, in
        // which case the string will not be newline terminated).
        public abstract String ReadLine(int size);

        // Discard any data we may have buffered based on the current stream position. Called after seeking in
        // the stream.
        public abstract void DiscardBufferedData();

        public abstract long Position {
            get;
            internal set; // update position bookkeeping
        }
    }

    // Read data as binary. We encode binary data in the low order byte of each character of the strings
    // returned so there will be a X2 expansion in space required (but normal string indexing can be used to
    // inspect the data).
    internal class PythonBinaryReader : PythonStreamReader {

        private readonly Stream/*!*/ _stream;
        public override TextReader TextReader { get { return null; } }

        // Buffer size (in bytes) used when reading until the end of the stream.
        private const int BufferSize = 4096;
        private byte[] _buffer;

        public PythonBinaryReader(Stream/*!*/ stream)
            : base(null) {
            Assert.NotNull(stream);
            _stream = stream;
        }

        // Read at most size characters (bytes in this case) and return the result as a string.
        public override String Read(int size) {
            byte[] data;
            if (size <= BufferSize) {
                if (_buffer == null)
                    _buffer = new byte[BufferSize];
                data = _buffer;
            } else
                data = new byte[size];
            int leftCount = size;
            int offset = 0;
            while (true) {
                int count = _stream.Read(data, offset, leftCount);
                if (count <= 0) break;
                leftCount -= count;
                if (leftCount <= 0) break;
                offset += count;
            }

            System.Diagnostics.Debug.Assert(leftCount >= 0);

            return PackDataIntoString(data, size - leftCount);
        }

        // Read until the end of the stream and return the result as a single string.
        public override String ReadToEnd() {
            StringBuilder sb = new StringBuilder();
            int totalcount = 0;
            if (_buffer == null)
                _buffer = new byte[BufferSize];
            while (true) {
                int count = _stream.Read(_buffer, 0, BufferSize);
                if (count == 0)
                    break;
                sb.Append(PackDataIntoString(_buffer, count));
                totalcount += count;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\n' (or until EOF, in which case the string will not be
        // newline terminated).
        public override String ReadLine() {
            StringBuilder sb = new StringBuilder(80);
            while (true) {
                int b = _stream.ReadByte();
                if (b == -1)
                    break;
                sb.Append((char)b);
                if (b == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\n' (or until EOF or the given size, in which case the
        // string will not be newline terminated).
        public override String ReadLine(int size) {
            StringBuilder sb = new StringBuilder(80);
            while (size-- > 0) {
                int b = _stream.ReadByte();
                if (b == -1)
                    break;
                sb.Append((char)b);
                if (b == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Discard any data we may have buffered based on the current stream position. Called after seeking in
        // the stream.
        public override void DiscardBufferedData() {
            // No buffering is performed.
        }

        public override long Position {
            get {
                return _stream.Position;
            }
            internal set {
            }
        }

        // Convert a byte array into a string by casting each byte into a character.
        internal static String PackDataIntoString(byte[] data, int count) {
            if (count == 1) {
                return ScriptingRuntimeHelpers.CharToString((char)data[0]);
            }

            StringBuilder sb = new StringBuilder(count);
            for (int i = 0; i < count; i++)
                sb.Append((char)data[i]);
            return sb.ToString();
        }
    }

    internal abstract class PythonTextReader : PythonStreamReader {

        // We read the stream through a StreamReader to take advantage of stream buffering and encoding to
        // translate incoming bytes into characters.  This requires us to keep control of our own position.
        protected readonly TextReader/*!*/ _reader;
        protected long _position;

        public override TextReader TextReader { get { return _reader; } }

        public override long Position {
            get {
                return _position;
            }
            internal set {
                _position = value;
            }
        }

        public PythonTextReader(TextReader/*!*/ reader, Encoding/*!*/ encoding, long position)
            : base(encoding) {
            _reader = reader;
            _position = position;
        }

        // Discard any data we may have buffered based on the current stream position. Called after seeking in
        // the stream.
        public override void DiscardBufferedData() {
            StreamReader streamReader = _reader as StreamReader;
            if (streamReader != null) {
                streamReader.DiscardBufferedData();
            }
        }
    }

    // Read data as text with lines terminated with '\r\n' (the Windows convention). Such terminators will be
    // translated to '\n' in the strings returned.
    internal class PythonTextCRLFReader : PythonTextReader {

        // We read the stream through a StreamReader to take advantage of stream buffering and encoding to
        // translate incoming bytes into characters.  This requires us to keep track of our own position.

        // the size of this buffer is optimized for reading at least one full line of text and avoding 
        // creating StringBuilder's in that case - we therefore want something larger than common widths
        // for lines in files.  This results in reading lines being about 4/5ths of the cost vs. a smaller
        // buffer
        private char[] _buffer = new char[160];
        private int _bufPos, _bufLen;

        public PythonTextCRLFReader(TextReader/*!*/ reader, Encoding/*!*/ encoding, long position)
            : base(reader, encoding, position) {
        }

        private int Read() {
            if (_bufPos >= _bufLen && ReadBuffer() == 0) {
                return -1;
            }

            _position++;
            return _buffer[_bufPos++];
        }

        private int Peek() {
            if (_bufPos >= _bufLen && ReadBuffer() == 0) {
                return -1;
            }

            return _buffer[_bufPos];
        }

        private int ReadBuffer() {
            _bufLen = _reader.Read(_buffer, 0, _buffer.Length);
            _bufPos = 0;
            return _bufLen;
        }

        // Read at most size characters and return the result as a string.
        public override String Read(int size) {
            if (size == 1) {
                int c = Read();
                if (c == -1) {
                    return String.Empty;
                }

                if (c == '\r' && Peek() == '\n') {
                    c = Read();
                }
                return ScriptingRuntimeHelpers.CharToString((char)c);
            }

            StringBuilder sb = new StringBuilder(size);
            while (size-- > 0) {
                int c = Read();
                if (c == -1)
                    break;
                if (c == '\r' && Peek() == '\n') {
                    c = Read();
                }
                sb.Append((char)c);
            }
            if (sb.Length == 0) {
                return String.Empty;
            }
            return sb.ToString();
        }

        // Read until the end of the stream and return the result as a single string.
        public override String ReadToEnd() {
            StringBuilder sb = new StringBuilder();
            while (true) {
                int c = Read();
                if (c == -1)
                    break;
                if (c == '\r' && Peek() == '\n') {
                    c = Read();
                }
                sb.Append((char)c);
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\r\n', converted to '\n' (or until EOF, in which case the
        // string will not be newline terminated).
        public override String ReadLine() {
            return ReadLine(Int32.MaxValue);
        }

        // Read characters up to and including a '\r\n', converted to '\n' (or until EOF or the given size, in
        // which case the string will not be newline terminated).
        public override String ReadLine(int size) {
            StringBuilder sb = null;
            // start off w/ some text
            if (_bufPos >= _bufLen) ReadBuffer();
            if (_bufLen == 0) return String.Empty;

            int curIndex = _bufPos;
            int bytesWritten = 0;
            int lenAdj = 0;
            while (true) {
                if (curIndex >= _bufLen) {
                    // need more text...
                    if (sb == null) {
                        sb = new StringBuilder((curIndex - _bufPos) * 2);
                    }
                    sb.Append(_buffer, _bufPos, curIndex - _bufPos);
                    if (ReadBuffer() == 0) {
                        return sb.ToString();
                    }
                    curIndex = 0;
                }

                char c = _buffer[curIndex++];
                if (c == '\r') {
                    if (curIndex < _bufLen) {
                        if (_buffer[curIndex] == '\n') {
                            _position++;
                            c = _buffer[curIndex++];
                            lenAdj = 2;
                        }
                    } else if (_reader.Peek() == '\n') {
                        c = (char)_reader.Read();
                        lenAdj = 1;
                    }
                }
                _position++;
                if (c == '\n') {
                    break;
                }
                if (++bytesWritten >= size) break;
            }

            return FinishString(sb, curIndex, lenAdj);
        }

        private string FinishString(StringBuilder sb, int curIndex, int lenAdj) {
            int len = curIndex - _bufPos;
            int pos = _bufPos;
            _bufPos = curIndex;
            if (sb != null) {
                if (lenAdj != 0) {
                    sb.Append(_buffer, pos, len - lenAdj);
                    sb.Append('\n');
                } else {
                    sb.Append(_buffer, pos, len);
                }

                return sb.ToString();
            } else if (lenAdj != 0) {
                return new String(_buffer, pos, len - lenAdj) + "\n";
            } else {
                return new String(_buffer, pos, len);
            }
        }

        // Discard any data we may have buffered based on the current stream position. Called after seeking in
        // the stream.
        public override void DiscardBufferedData() {
            _bufPos = _bufLen = 0;
            base.DiscardBufferedData();
        }
    }

    // Read data as text with lines terminated with '\r' (the Macintosh convention). Such terminators will be
    // translated to '\n' in the strings returned.
    internal class PythonTextCRReader : PythonTextReader {

        public PythonTextCRReader(TextReader/*!*/ reader, Encoding/*!*/ encoding, long position)
            : base(reader, encoding, position) {
        }

        // Read at most size characters and return the result as a string.
        public override String Read(int size) {
            if (size == 1) {
                int c = _reader.Read();
                if (c == -1) {
                    return String.Empty;
                }
                if (c == '\r') c = '\n';
                return ScriptingRuntimeHelpers.CharToString((char)c);
            }

            StringBuilder sb = new StringBuilder(size);
            while (size-- > 0) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                if (c == '\r')
                    c = '\n';
                sb.Append((char)c);
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read until the end of the stream and return the result as a single string.
        public override String ReadToEnd() {
            StringBuilder sb = new StringBuilder();
            while (true) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                if (c == '\r')
                    c = '\n';
                sb.Append((char)c);
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\r', converted to '\n' (or until EOF, in which case the
        // string will not be newline terminated).
        public override String ReadLine() {
            StringBuilder sb = new StringBuilder(80);
            while (true) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                if (c == '\r')
                    c = '\n';
                sb.Append((char)c);
                if (c == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\r', converted to '\n' (or until EOF or the given size, in
        // which case the string will not be newline terminated).
        public override String ReadLine(int size) {
            StringBuilder sb = new StringBuilder(80);
            while (size-- > 0) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                if (c == '\r')
                    c = '\n';
                sb.Append((char)c);
                if (c == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }
    }

    // Read data as text with lines terminated with '\n' (the Unix convention).
    internal class PythonTextLFReader : PythonTextReader {

        public PythonTextLFReader(TextReader/*!*/ reader, Encoding/*!*/ encoding, long position)
            : base(reader, encoding, position) {
        }

        // Read at most size characters and return the result as a string.
        public override String Read(int size) {
            if (size == 1) {
                int c = _reader.Read();
                if (c == -1) {
                    return String.Empty;
                }

                return ScriptingRuntimeHelpers.CharToString((char)c);
            }

            StringBuilder sb = new StringBuilder(size);
            while (size-- > 0) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                sb.Append((char)c);
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read until the end of the stream and return the result as a single string.
        public override String ReadToEnd() {
            return _reader.ReadToEnd();
        }

        // Read characters up to and including a '\n' (or until EOF, in which case the string will not be
        // newline terminated).
        public override String ReadLine() {
            StringBuilder sb = new StringBuilder(80);
            while (true) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                sb.Append((char)c);
                if (c == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\n' (or until EOF or the given size, in which case the
        // string will not be newline terminated).
        public override String ReadLine(int size) {
            StringBuilder sb = new StringBuilder(80);
            while (size-- > 0) {
                int c = _reader.Read();
                if (c == -1)
                    break;
                _position++;
                sb.Append((char)c);
                if (c == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }
    }

    // Read data as text with lines terminated with any of '\n', '\r' or '\r\n'. Such terminators will be
    // translated to '\n' in the strings returned. This class also records whcih of these have been seen so
    // far in the stream to support python semantics (see the Terminators property).
    internal class PythonUniversalReader : PythonTextReader {
        private int _lastChar = -1;
        // Symbols for the different styles of newline terminator we might have seen in this stream so far.
        public enum TerminatorStyles {
            None = 0x0,
            CrLf = 0x1,  // '\r\n'
            Cr = 0x2,  // '\r'
            Lf = 0x4   // '\n'
        }

        // We read the stream through a StreamReader to take advantage of stream buffering and encoding to
        // translate incoming bytes into characters.  This requires that we keep track of our own position.
        private TerminatorStyles _terminators;

        public PythonUniversalReader(TextReader/*!*/ reader, Encoding/*!*/ encoding, long position)
            : base(reader, encoding, position) {
            _terminators = TerminatorStyles.None;
        }

        private int ReadOne() {
            if (_lastChar != -1) {
                var res = _lastChar;
                _lastChar = -1;
                return res;
            }

            return _reader.Read();
        }

        // Private helper used to check for newlines and transform and record as necessary. Returns the
        // possibly translated character read.
        private int ReadChar() {
            int c = ReadOne();
            if (c != -1) _position++;
            if (c == '\r') {
                Debug.Assert(_lastChar == -1);
                // we can't Peek here because Peek() won't block for more input
                int next = _reader.Read();
                if (next == '\n') {
                    _position++;
                    _terminators |= TerminatorStyles.CrLf;
                } else {
                    _lastChar = next;
                    _terminators |= TerminatorStyles.Cr;
                }
                c = '\n';
            } else if (c == '\n') {
                _terminators |= TerminatorStyles.Lf;
            }
            return c;
        }

        // Read at most size characters and return the result as a string.
        public override String Read(int size) {
            if (size == 1) {
                int c = ReadChar();
                if (c == -1) {
                    return String.Empty;
                }

                return ScriptingRuntimeHelpers.CharToString((char)c);
            }

            StringBuilder sb = new StringBuilder(size);
            while (size-- > 0) {
                int c = ReadChar();
                if (c == -1)
                    break;
                sb.Append((char)c);
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read until the end of the stream and return the result as a single string.
        public override String ReadToEnd() {
            StringBuilder sb = new StringBuilder();
            while (true) {
                int c = ReadChar();
                if (c == -1)
                    break;
                sb.Append((char)c);
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\r\n', '\r' or '\n' converted to '\n' (or until EOF, in
        // which case the string will not be newline terminated).
        public override String ReadLine() {
            StringBuilder sb = new StringBuilder(80);
            while (true) {
                int c = ReadChar();
                if (c == -1)
                    break;
                sb.Append((char)c);
                if (c == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // Read characters up to and including a '\r\n', '\r' or '\n' converted to '\n' (or until EOF or the
        // given size, in which case the string will not be newline terminated).
        public override String ReadLine(int size) {
            StringBuilder sb = new StringBuilder(80);
            while (size-- > 0) {
                int c = ReadChar();
                if (c == -1)
                    break;
                sb.Append((char)c);
                if (c == '\n')
                    break;
            }
            if (sb.Length == 0)
                return String.Empty;
            return sb.ToString();
        }

        // PythonUniversalReader specific property that returns a bitmask of all the newline termination
        // styles seen in the stream so far.
        public TerminatorStyles Terminators { get { return _terminators; } }
    }

    #endregion

    #region Writers

    // The abstract writer API.
    internal abstract class PythonStreamWriter {

        protected Encoding _encoding;

        public Encoding Encoding { get { return _encoding; } }
        public abstract TextWriter TextWriter { get; }

        public PythonStreamWriter(Encoding encoding) {
            _encoding = encoding;
        }

        // Write the data in the input string to the output stream, converting line terminators ('\n') into
        // the output format as necessary.  Returns the number of bytes written
        public abstract int Write(String/*!*/ data);

        // Write the raw input data to the output stream
        public abstract int WriteBytes(IList<byte> data);

        // Flush any buffered data to the file.
        public abstract void Flush();
    }

    // Write binary data embedded in the low-order byte of each string character to the output stream with no
    // other translation.
    internal class PythonBinaryWriter : PythonStreamWriter {
        private Stream/*!*/ _stream;

        public override TextWriter TextWriter { get { return null; } }

        public PythonBinaryWriter(Stream/*!*/ stream)
            : base(null) {
            _stream = stream;
        }

        // Write the data in the input string to the output stream. No newline conversion is performed.
        public override int Write(string/*!*/ data) {
            byte[] bytes = PythonAsciiEncoding.Instance.GetBytes(data);
            Debug.Assert(bytes.Length == data.Length);
            _stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }

        // Write the raw input data to the output stream. No newline conversion is performed.
        public override int WriteBytes(IList<byte> data) {
            int count = data.Count;
            for (int i = 0; i < count; i++) {
                _stream.WriteByte(data[i]);
            }

            return count;
        }

        // Flush any buffered data to the file.
        public override void Flush() {
            _stream.Flush();
        }
    }

    // Write data with '\r', '\n' or '\r\n' line termination.
    internal class PythonTextWriter : PythonStreamWriter {

        // We write the stream through a StreamWriter to take advantage of stream buffering and encoding to
        // translate outgoing characters into bytes.
        private TextWriter/*!*/ _writer;
        private readonly string _eoln;

        public override TextWriter TextWriter { get { return _writer; } }

        public PythonTextWriter(TextWriter/*!*/ writer, string eoln)
            : base(writer.Encoding) {
            _writer = writer;
            _eoln = eoln;
        }

        // Write the data in the input string to the output stream, converting line terminators ('\n') into
        // _eoln as necessary.
        public override int Write(string/*!*/ data) {
            if (_eoln != null) {
                data = data.Replace("\n", _eoln);
            }
            _writer.Write(data);
            return data.Length;
        }

        // Write the input data to the output stream, converting line terminators ('\n') into _eoln as necessary.
        public override int WriteBytes(IList<byte> data) {
            // Result is equivalent to "return Write(data.MakeString());" but more efficient because
            // MakeString() and Replace() are done at the same time.

            int count = data.Count;
            StringBuilder sb = new StringBuilder(_eoln.Length > 1 ? (int)(count * 1.2) : count);
            for (int i = 0; i < count; i++) {
                char c = (char)data[i];
                if (c == '\n') {
                    sb.Append(_eoln);
                } else {
                    sb.Append(c);
                }
            }

            _writer.Write(sb.ToString());
            return count;
        }

        // Flush any buffered data to the file.
        public override void Flush() {
            _writer.Flush();
        }
    }

    #endregion

    #region File Manager

    internal class PythonFileManager {
        private HybridMapping<PythonFile> fileMapping = new HybridMapping<PythonFile>(3);
        private HybridMapping<object> objMapping = new HybridMapping<object>(3);

        public int AddToStrongMapping(PythonFile pf) {
            return fileMapping.StrongAdd(pf);
        }

        public int AddToStrongMapping(object o) {
            return objMapping.StrongAdd(o);
        }

        public void Remove(PythonFile pf) {
            fileMapping.RemoveOnObject(pf);
        }

        public void Remove(object o) {
            objMapping.RemoveOnObject(o);
        }

        public PythonFile GetFileFromId(PythonContext context, int id) {
            PythonFile pf;
            if (!TryGetFileFromId(context, id, out pf)) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }

            return pf;
        }

        public bool TryGetFileFromId(PythonContext context, int id, out PythonFile pf) {
            switch (id) {
                case 0:
                    pf = (context.GetSystemStateValue("__stdin__") as PythonFile);
                    break;
                case 1:
                    pf = (context.GetSystemStateValue("__stdout__") as PythonFile);
                    break;
                case 2:
                    pf = (context.GetSystemStateValue("__stderr__") as PythonFile);
                    break;
                default:
                    pf = fileMapping.GetObjectFromId(id);
                    break;
            }

            return pf != null;
        }

        public object GetObjectFromId(int id) {
            object o = objMapping.GetObjectFromId(id);

            if (o == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }
            return o;
        }

        public int GetIdFromFile(PythonFile pf) {
            if (pf.IsConsole) {
                for (int i = 0; i < 3; i++) {
                    if (pf == GetFileFromId(pf.Context, i)) {
                        return i;
                    }
                }
            }

            int res = fileMapping.GetIdFromObject(pf);
            if (res == -1) {
                // lazily created weak mapping
                res = fileMapping.WeakAdd(pf);
            }
            return res;
        }

        public int GetIdFromObject(object o) {
            int res = objMapping.GetIdFromObject(o);
            if (res == -1) {
                // lazily created weak mapping
                res = objMapping.WeakAdd(o);
            }
            return res;
        }
    }

    #endregion

    [PythonType("file")]
    [DontMapIEnumerableToContains]
    public class PythonFile : IDisposable, ICodeFormattable, IEnumerator<string>, IEnumerator, IWeakReferenceable {
        private ConsoleStreamType _consoleStreamType;
        private SharedIO _io;   // null for non-console
        internal Stream _stream; // null for console
        private string _mode;
        private string _name, _encoding;
        private PythonFileMode _fileMode;

        private PythonStreamReader _reader;
        private PythonStreamWriter _writer;
        protected bool _isOpen;
        private Nullable<long> _reseekPosition; // always null for console
        private WeakRefTracker _weakref;
        private string _enumValue;
        internal readonly PythonContext/*!*/ _context;

        private bool _softspace;

        internal bool IsConsole {
            get {
                return _stream == null;
            }
        }

        internal PythonFile(PythonContext/*!*/ context) {
            _context = context;
        }

        public PythonFile(CodeContext/*!*/ context)
            : this(PythonContext.GetContext(context)) {
        }

        internal static PythonFile/*!*/ Create(CodeContext/*!*/ context, Stream/*!*/ stream, string/*!*/ name, string/*!*/ mode) {
            return Create(context, stream, PythonContext.GetContext(context).DefaultEncoding, name, mode);
        }

        internal static PythonFile/*!*/ Create(CodeContext/*!*/ context, Stream/*!*/ stream, Encoding/*!*/ encoding, string/*!*/ name, string/*!*/ mode) {
            PythonFile res = new PythonFile(PythonContext.GetContext(context));
            res.__init__(stream, encoding, name, mode);
            return res;
        }

        internal static PythonFile/*!*/ CreateConsole(PythonContext/*!*/ context, SharedIO/*!*/ io, ConsoleStreamType type, string/*!*/ name) {
            PythonFile res = new PythonFile(context);
            res.InitializeConsole(io, type, name);
            return res;
        }

        ~PythonFile() {
            try {
                Dispose(false);
            } catch (ObjectDisposedException) {
            } catch (EncoderFallbackException) {
                // flushing could fail due to encoding, ignore it
            }
        }

        #region Python initialization

        //
        // Here are the mode rules for IronPython "file":
        //          (r|a|w|rU|U|Ur) [ [+][b|t] | [b|t][+] ]
        // 
        // Seems C-Python allows "b|t" at the beginning too.
        // 
        public void __init__(CodeContext/*!*/ context, string name, [DefaultParameterValue("r")]string mode, [DefaultParameterValue(-1)]int buffering) {
            FileShare fshare = FileShare.ReadWrite;
            FileMode fmode;
            FileAccess faccess;

            if (name == null) {
                throw PythonOps.TypeError("file name must be string, found NoneType");
            }

            if (mode == null) {
                throw PythonOps.TypeError("mode must be string, not None");
            }

            if (String.IsNullOrEmpty(mode)) {
                throw PythonOps.ValueError("empty mode string");
            }

            bool seekEnd;
            TranslateAndValidateMode(mode, out fmode, out faccess, out seekEnd);

            try {
                Stream stream;
                try {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT && name == "nul") {
                        stream = Stream.Null;
                    } else if (buffering <= 0) {
                        stream = PythonContext.GetContext(context).DomainManager.Platform.OpenInputFileStream(name, fmode, faccess, fshare);
                    } else {
                        stream = PythonContext.GetContext(context).DomainManager.Platform.OpenInputFileStream(name, fmode, faccess, fshare, buffering);
                    }
                } catch (IOException e) {
                    AddFilename(context, name, e);
                    throw;
                }

                // we want to own the lifetime of the stream so we can flush & dispose in our finalizer...
                GC.SuppressFinalize(stream);

                if (seekEnd) stream.Seek(0, SeekOrigin.End);

                __init__(stream, PythonContext.GetContext(context).DefaultEncoding, name, mode);
                this._isOpen = true;
            } catch (UnauthorizedAccessException e) {
                throw ToIoException(context, name, e);
            }
        }

        internal static Exception ToIoException(CodeContext context, string name, UnauthorizedAccessException e) {
            Exception excp = new IOException(e.Message, e);
            AddFilename(context, name, excp);
            return excp;
        }

        internal static void AddFilename(CodeContext context, string name, Exception ioe) {
            var pyExcep = PythonExceptions.ToPython(ioe);
            PythonOps.SetAttr(context, pyExcep, "filename", name);
        }

        internal static void ValidateMode(string mode) {
            FileMode fmode;
            FileAccess access;
            bool seekEnd;
            TranslateAndValidateMode(mode, out fmode, out access, out seekEnd);
        }

        private static void TranslateAndValidateMode(string mode, out FileMode fmode, out FileAccess faccess, out bool seekEnd) {
            if (mode.Length == 0) {
                throw PythonOps.ValueError("empty mode string");
            }

            // remember the original mode for error reporting
            string inMode = mode;

            if (mode.IndexOf('U') != -1) {
                mode = mode.Replace("U", String.Empty);
                if (mode.Length == 0) {
                    mode = "r";
                } else if (mode == "+") {
                    mode = "r+";
                } else if (mode[0] == 'w' || mode[0] == 'a') {
                    throw PythonOps.ValueError("universal newline mode can only be used with modes starting with 'r'");
                } else {
                    mode = "r" + mode;
                }
            }

            // process read/write/append
            seekEnd = false;
            switch (mode[0]) {
                case 'r': fmode = FileMode.Open; break;
                case 'w': fmode = FileMode.Create; break;
                case 'a': fmode = FileMode.Append; break;
                default:
                    throw PythonOps.ValueError("mode string must begin with one of 'r', 'w', 'a' or 'U', not '{0}'", inMode);
            }

            // process +
            if (mode.IndexOf('+') != -1) {
                faccess = FileAccess.ReadWrite;
                if (fmode == FileMode.Append) {
                    fmode = FileMode.OpenOrCreate;
                    seekEnd = true;
                }
            } else {
                switch (fmode) {
                    case FileMode.Create: faccess = FileAccess.Write; break;
                    case FileMode.Open: faccess = FileAccess.Read; break;
                    case FileMode.Append: faccess = FileAccess.Write; break;
                    default: throw new InvalidOperationException();
                }
            }
        }

        public void __init__(CodeContext/*!*/ context, [NotNull]Stream/*!*/ stream) {
            ContractUtils.RequiresNotNull(stream, "stream");

            string mode;
            if (stream.CanRead && stream.CanWrite) mode = "w+";
            else if (stream.CanWrite) mode = "w";
            else mode = "r";

            __init__(stream, PythonContext.GetContext(context).DefaultEncoding, mode);
        }

        public void __init__(CodeContext/*!*/ context, [NotNull]Stream/*!*/ stream, string mode) {
            __init__(stream, PythonContext.GetContext(context).DefaultEncoding, mode);
        }

        public void __init__([NotNull]Stream/*!*/ stream, Encoding encoding, string mode) {
            InternalInitialize(stream, encoding, mode);
        }

        public void __init__([NotNull]Stream/*!*/ stream, [NotNull]Encoding/*!*/ encoding, string name, string mode) {
            ContractUtils.RequiresNotNull(stream, "stream");
            ContractUtils.RequiresNotNull(encoding, "encoding");

            InternalInitialize(stream, encoding, name, mode);
        }

        private PythonTextReader/*!*/ CreateTextReader(TextReader/*!*/ reader, Encoding/*!*/ encoding, long initPosition) {
            switch (_fileMode) {
                case PythonFileMode.TextCrLf:
                    return new PythonTextCRLFReader(reader, encoding, initPosition);

                case PythonFileMode.TextCr:
                    return new PythonTextCRReader(reader, encoding, initPosition);

                case PythonFileMode.TextLf:
                    return new PythonTextLFReader(reader, encoding, initPosition);

                case PythonFileMode.UniversalNewline:
                    return new PythonUniversalReader(reader, encoding, initPosition);
            }

            throw Assert.Unreachable;
        }

        private PythonTextReader/*!*/ CreateConsoleReader() {
            Debug.Assert(_io != null);

            Encoding encoding;
            return CreateTextReader(_io.GetReader(out encoding), encoding, 0);
        }

        private PythonTextWriter/*!*/ CreateTextWriter(TextWriter/*!*/ writer) {
            PythonFileMode fileMode = _fileMode;
            if (_fileMode == PythonFileMode.UniversalNewline) {
                if (Environment.OSVersion.Platform == PlatformID.Unix) {
                    fileMode = PythonFileMode.TextLf;
                } else {
                    fileMode = PythonFileMode.TextCrLf;
                }
                // TODO: Identify Mac?
            }

            switch (fileMode) {
                case PythonFileMode.TextCrLf:
                    return new PythonTextWriter(writer, "\r\n");

                case PythonFileMode.TextCr:
                    return new PythonTextWriter(writer, "\r");

                case PythonFileMode.TextLf:
                    return new PythonTextWriter(writer, null);
            }

            throw Assert.Unreachable;
        }

        /// <summary>
        /// Sets the mode to text or binary.  Returns true if previously set to text, false if previously set to binary.
        /// </summary>
        internal bool SetMode(CodeContext context, bool text) {
            lock (this) {
                var mode = MapFileMode(_mode);
                if (text) {
                    if (mode == PythonFileMode.Binary) {
                        _fileMode = PythonFileMode.UniversalNewline;
                    } else {
                        _fileMode = mode;
                    }
                } else {
                    _fileMode = PythonFileMode.Binary;
                }

                if (_stream != null) {
                    Encoding enc;
                    if (!StringOps.TryGetEncoding(_encoding, out enc)) {
                        enc = context.LanguageContext.DefaultEncoding;
                    }
                    InitializeReaderAndWriter(_stream, enc);
                } else if (text) {
                    InitializeConsole(_io, _consoleStreamType, _name);
                } else {
                    // making console binary
                    InitializeReaderAndWriter(_stream = _io.GetStream(_consoleStreamType), _io.GetEncoding(_consoleStreamType));
                }

                if (_fileMode == PythonFileMode.Binary) {
                    return false;
                } else {
                    return true;
                }
            }
        }

        internal void InternalInitialize(Stream/*!*/ stream, Encoding/*!*/ encoding, string/*!*/ mode) {
            Assert.NotNull(stream, encoding, mode);

            _stream = stream;
            _mode = mode;
            _isOpen = true;
            _io = null;
            _fileMode = MapFileMode(mode);
            _encoding = StringOps.GetEncodingName(encoding);

            InitializeReaderAndWriter(stream, encoding);

#if !SILVERLIGHT
            // only possible if the user provides us w/ the stream directly
            FileStream fs = stream as FileStream;
            if (fs != null) {
                _name = fs.Name;
            } else {
                _name = "nul";
            }
#else
            _name = "stream";
#endif
        }

        private void InitializeReaderAndWriter(Stream stream, Encoding encoding) {
            if (stream.CanRead) {
                if (_fileMode == PythonFileMode.Binary) {
                    _reader = new PythonBinaryReader(stream);
                } else {
                    long initPosition = (stream.CanSeek) ? stream.Position : 0;
                    _reader = CreateTextReader(new StreamReader(stream, encoding), encoding, initPosition);
                }
            }

            if (stream.CanWrite) {
                if (_fileMode == PythonFileMode.Binary) {
                    _writer = new PythonBinaryWriter(stream);
                } else {
                    _writer = CreateTextWriter(new StreamWriter(stream, encoding));
                }
            }
        }

        internal void InitializeConsole(SharedIO/*!*/ io, ConsoleStreamType type, string/*!*/ name) {
            Debug.Assert(io != null);
            Debug.Assert(name != null);

            _consoleStreamType = type;
            _io = io;
            _mode = (type == ConsoleStreamType.Input) ? "r" : "w";
            _isOpen = true;
            _fileMode = MapFileMode(_mode);
            _name = name;
            _encoding = StringOps.GetEncodingName(io.OutputEncoding);

            if (type == ConsoleStreamType.Input) {
                _reader = CreateConsoleReader();
            } else {
                _writer = CreateTextWriter(_io.GetWriter(type));
            }
        }

        internal void InternalInitialize(Stream stream, Encoding encoding, string name, string mode) {
            InternalInitialize(stream, encoding, mode);
            _name = name;
        }

        #endregion

        // Enumeration of each stream mode.
        private enum PythonFileMode {
            Binary,
            TextCrLf,
            TextCr,
            TextLf,
            UniversalNewline
        }

        // Map a python mode string into a PythonFileMode.
        private static PythonFileMode MapFileMode(String mode) {
            // Assume "mode" is in reasonable good shape, since we checked it in "Make"
            if (mode.Contains("b"))
                return PythonFileMode.Binary;

            if (mode.Contains("U"))
                return PythonFileMode.UniversalNewline;

            // Must be platform specific text mode. Work out which line termination the platform
            // supports based on the value of Environment.NewLine.
            switch (Environment.NewLine) {
                case "\r\n":
                    return PythonFileMode.TextCrLf;
                case "\r":
                    return PythonFileMode.TextCr;
                case "\n":
                    return PythonFileMode.TextLf;
                default:
                    throw new NotImplementedException("Unsupported Environment.NewLine value");
            }
        }

        internal Encoding Encoding {
            get {
                return (_reader != null) ? _reader.Encoding : (_writer != null) ? _writer.Encoding : null;
            }
        }

        internal PythonContext Context {
            get { return _context; }
        }

        void IDisposable.Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [PythonHidden]
        protected virtual void Dispose(bool disposing) {
            lock (this) {
                if (!_isOpen) {
                    return;
                }

                FlushNoLock();
                _isOpen = false;

                if (!IsConsole) {
                    _stream.Close();
                }

                PythonFileManager myManager = _context.RawFileManager;
                if (myManager != null) {
                    myManager.Remove(this);
                    myManager.Remove(_stream);
                }
            }
        }

        public virtual object close() {
            Dispose(true);
            GC.SuppressFinalize(this);
            return null;
        }

        [Documentation("True if the file is closed, False if the file is still open")]
        public bool closed {
            get {
                return !_isOpen;
            }
        }

        [PythonHidden]
        protected void ThrowIfClosed() {
            if (!_isOpen) {
                throw PythonOps.ValueError("I/O operation on closed file");
            }
        }

        public virtual void flush() {
            lock (this) {
                FlushNoLock();
            }
        }

        private void FlushNoLock() {
            ThrowIfClosed();
            if (_writer != null) {
                _writer.Flush();
                if (!IsConsole) {
                    _stream.Flush();
                }
            }
        }

        public int fileno() {
            ThrowIfClosed();
            return _context.FileManager.GetIdFromFile(this);
        }

        [Documentation("gets the mode of the file")]
        public string mode {
            get {
                return _mode;
            }
        }

        [Documentation("gets the name of the file")]
        public string name {
            get {
                return _name;
            }
        }

        [Documentation("gets the encoding used when reading/writing text")]
        public string encoding {
            get {
                return _encoding;
            }
        }

        public string read() {
            return read(-1);
        }

        public string read(int size) {
            PythonStreamReader reader = GetReader();
            if (size < 0) {
                return reader.ReadToEnd();
            } else {
                return reader.Read(size);
            }
        }

        public string readline() {
            return GetReader().ReadLine();
        }

        public string readline(int size) {
            return GetReader().ReadLine(size);
        }

        public List readlines() {
            List ret = new List();
            string line;
            for (; ; ) {
                line = readline();
                if (String.IsNullOrEmpty(line)) break;
                ret.AddNoLock(line);
            }
            return ret;
        }

        public List readlines(int sizehint) {
            List ret = new List();
            for (; ; ) {
                string line = readline();
                if (String.IsNullOrEmpty(line)) break;
                ret.AddNoLock(line);
                if (line.Length >= sizehint) break;
                sizehint -= line.Length;
            }
            return ret;
        }

        public void seek(long offset) {
            seek(offset, 0);
        }

        public void seek(long offset, int whence) {
            if (_mode == "a") {
                // nop when seeking on streams opened for append.
                return;
            }

            ThrowIfClosed();

            if (IsConsole || !_stream.CanSeek) {
                throw PythonOps.IOError("Can not seek on file " + _name);
            }

            lock (this) {
                // flush before saving our position to ensure it's accurate.
                FlushNoLock();

                SavePositionPreSeek();

                SeekOrigin origin = (SeekOrigin)whence;

                long newPos = _stream.Seek(offset, origin);
                if (_reader != null) {
                    _reader.DiscardBufferedData();
                    _reader.Position = newPos;
                }
            }
        }

        public bool softspace {
            get {
                return _softspace;
            }
            set {
                _softspace = value;
            }
        }

        public object tell() {
            long l = GetCurrentPosition();
            if (l <= Int32.MaxValue) {
                return (int)l;
            }
            return (BigInteger)l;
        }

        private long GetCurrentPosition() {
            if (_reader != null) {
                return _reader.Position;
            }
            if (_stream != null) {
                return _stream.Position;
            }

            throw PythonExceptions.CreateThrowable(PythonExceptions.IOError, 9, "Bad file descriptor");
        }

        /// <summary>
        /// Truncates the file to the current length as indicated by tell().
        /// </summary>
        public void truncate() {
            lock (this) {
                FlushNoLock();
                TruncateNoLock(GetCurrentPosition());
            }
        }

        /// <summary>
        /// Truncates the file to the specified length.
        /// </summary>
        /// <param name="size"></param>
        public void truncate(long size) {
            lock (this) {
                FlushNoLock();
                TruncateNoLock(size);
            }
        }

        private void TruncateNoLock(long size) {
            if (size < 0) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.IOError, 22, "Invalid argument");
            }

            lock (this) {
                FileStream fs = _stream as FileStream;
                if (fs != null) {
                    if (fs.CanWrite) {
                        fs.SetLength(size);
                    } else {
                        throw PythonExceptions.CreateThrowable(PythonExceptions.IOError, 13, "Permission denied");
                    }
                } else {
                    throw PythonExceptions.CreateThrowable(PythonExceptions.IOError, 9, "Bad file descriptor");
                }
            }
        }

        public void write(string s) {
            if (s == null) {
                throw PythonOps.TypeError("must be string or read-only character buffer, not None");
            }

            lock (this) {
                WriteNoLock(s);
            }
        }

        public void write([NotNull]IList<byte> bytes) {
            lock (this) {
                WriteNoLock(bytes);
            }
        }

        private void WriteNoLock(string s) {
            PythonStreamWriter writer = GetWriter();
            int bytesWritten = writer.Write(s);
            if (!IsConsole && _reader != null && _stream.CanSeek) {
                _reader.Position += bytesWritten;
            }

            if (IsConsole) {
                FlushNoLock();
            }
        }

        private void WriteNoLock([NotNull]IList<byte> b) {
            PythonStreamWriter writer = GetWriter();
            int bytesWritten = writer.WriteBytes(b);
            if (!IsConsole && _reader != null && _stream.CanSeek) {
                _reader.Position += bytesWritten;
            }

            if (IsConsole) {
                FlushNoLock();
            }
        }

        public void write([NotNull]PythonBuffer buf) {
            write((IList<byte>)buf);
        }

        public void write([NotNull]object arr) {
            WriteWorker(arr, true);
        }

        private void WriteWorker(object/*!*/ arr, bool locking) {
            Debug.Assert(arr != null);

            IPythonArray array = arr as IPythonArray;
            if (array == null) {
                throw PythonOps.TypeError("file.write() argument must be string or read-only character buffer, not {0}", DynamicHelpers.GetPythonType(arr).Name);
            } else if (_fileMode != PythonFileMode.Binary) {
                throw PythonOps.TypeError("file.write() argument must be string or buffer, not {0}", DynamicHelpers.GetPythonType(arr).Name);
            }

            if (locking) {
                write(array.tostring());
            } else {
                WriteNoLock(array.tostring());
            }
        }

        public void writelines(object o) {
            System.Collections.IEnumerator e = PythonOps.GetEnumerator(o);

            if (!e.MoveNext()) {
                return;
            }

            lock (this) {
                do {
                    string line = e.Current as string;
                    if (line == null) {
                        Bytes b = e.Current as Bytes;
                        if (b != null) {
                            WriteWorker(b, false);
                            continue;
                        }

                        PythonBuffer buf = e.Current as PythonBuffer;
                        if (buf != null) {
                            WriteNoLock(buf);
                            continue;
                        }

                        IPythonArray arr = e.Current as IPythonArray;
                        if (arr != null) {
                            WriteWorker(arr, false);
                            continue;
                        }

                        throw PythonOps.TypeError("writelines() argument must be a sequence of strings");

                    }
                    WriteNoLock(line);
                } while (e.MoveNext());
            }
        }

        [Python3Warning("f.xreadlines() not supported in 3.x, try 'for line in f' instead")]
        public PythonFile xreadlines() {
            return this;
        }

        public Object newlines {
            get {
                if (_reader == null || !(_reader is PythonUniversalReader))
                    return null;

                PythonUniversalReader.TerminatorStyles styles = ((PythonUniversalReader)_reader).Terminators;
                switch (styles) {
                    case PythonUniversalReader.TerminatorStyles.None:
                        return null;
                    case PythonUniversalReader.TerminatorStyles.CrLf:
                        return "\r\n";
                    case PythonUniversalReader.TerminatorStyles.Cr:
                        return "\r";
                    case PythonUniversalReader.TerminatorStyles.Lf:
                        return "\n";
                    default:
                        System.Collections.Generic.List<String> styleStrings = new System.Collections.Generic.List<String>();
                        if ((styles & PythonUniversalReader.TerminatorStyles.CrLf) != 0)
                            styleStrings.Add("\r\n");
                        if ((styles & PythonUniversalReader.TerminatorStyles.Cr) != 0)
                            styleStrings.Add("\r");
                        if ((styles & PythonUniversalReader.TerminatorStyles.Lf) != 0)
                            styleStrings.Add("\n");
                        return new PythonTuple(styleStrings.ToArray());
                }
            }
        }

        private void SavePositionPreSeek() {
            if (_mode == "a+") {
                Debug.Assert(!IsConsole);
                _reseekPosition = _stream.Position;
            }
        }

        // called before each read operation
        private PythonStreamReader/*!*/ GetReader() {
            ThrowIfClosed();
            if (_reader == null) {
                throw PythonOps.IOError("Can not read from " + _name);
            }

            if (IsConsole) {
                // update reader if redirected:
                lock (this) {
                    if (!ReferenceEquals(_io.InputReader, _reader.TextReader)) {
                        _reader = CreateConsoleReader();
                    }
                }
            }

            return _reader;
        }

        // called before each write operation
        private PythonStreamWriter/*!*/ GetWriter() {
            ThrowIfClosed();

            if (_writer == null) {
                throw PythonOps.IOError("Can not write to " + _name);
            }

            lock (this) {
                if (IsConsole) {
                    // update writer if redirected:
                    if (_stream != _io.GetStream(_consoleStreamType)) {
                        TextWriter currentWriter = _io.GetWriter(_consoleStreamType);

                        if (!ReferenceEquals(currentWriter, _writer.TextWriter)) {
                            try {
                                _writer.Flush();
                            } catch (ObjectDisposedException) {
                                //no way to tell if stream has been closed outside of execution
                                //so don't blow up if it has
                            }
                            _writer = CreateTextWriter(currentWriter);
                        }
                    }
                } else if (_reseekPosition != null) {
                    _stream.Seek(_reseekPosition.Value, SeekOrigin.Begin);
                    _reader.Position = _reseekPosition.Value;
                    _reseekPosition = null;
                }
            }

            return _writer;
        }

        public object next() {
            string line = readline();
            if (String.IsNullOrEmpty(line)) {
                throw PythonOps.StopIteration();
            }
            return line;
        }

        public object __iter__() {
            ThrowIfClosed();
            return this;
        }

#if FEATURE_NATIVE
        public bool isatty() {
            return IsConsole && !isRedirected();
        }

        private bool isRedirected() {
            if (_consoleStreamType == ConsoleStreamType.Output) {
                return StreamRedirectionInfo.IsOutputRedirected;
            }
            if (_consoleStreamType == ConsoleStreamType.Input) {
                return StreamRedirectionInfo.IsInputRedirected;
            }
            return StreamRedirectionInfo.IsErrorRedirected;
        }
#else
        public bool isatty() {
            return IsConsole;
        }
#endif

        public object __enter__() {
            ThrowIfClosed();
            return this;
        }

        public void __exit__(params object[] excinfo) {
            close();
        }

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<{0} file '{1}', mode '{2}' at 0x{3:X8}>",
                _isOpen ? "open" : "closed",
                _name ?? "<uninitialized file>",
                _mode ?? "<uninitialized file>",
                this.GetHashCode()
                );
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _weakref;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _weakref = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            ((IWeakReferenceable)this).SetWeakRef(value);
        }

        #endregion

        #region IEnumerator<string> Members

        string IEnumerator<string>.Current {
            get { return _enumValue; }
        }

        #endregion

        #region IEnumerator Members

        object IEnumerator.Current {
            get { return _enumValue; }
        }

        bool IEnumerator.MoveNext() {
            _enumValue = readline();
            if (String.IsNullOrEmpty(_enumValue)) {
                return false;
            }

            return true;
        }

        void IEnumerator.Reset() {
            throw new NotImplementedException();
        }

        #endregion
    }

#if FEATURE_NATIVE
    // dotnet45 backport
    // http://msdn.microsoft.com/en-us/library/system.console.isoutputredirected%28v=VS.110%29.aspx

    internal static class StreamRedirectionInfo {

        private enum FileType { Unknown, Disk, Char, Pipe };
        private enum StdHandle { Stdin = -10, Stdout = -11, Stderr = -12 };
        [DllImport("kernel32.dll")]
        private static extern FileType GetFileType(SafeFileHandle hdl);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(StdHandle std);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int mode);

        private static bool IsHandleRedirected(IntPtr ioHandle) {
            SafeFileHandle safeIOHandle = new SafeFileHandle(ioHandle, false);
            // console, printer, com port are Char
            var fileType = GetFileType(safeIOHandle);
            if (fileType != FileType.Char) {
                return true;
            }
            // narrow down to console only
            int mode;
            bool success = GetConsoleMode(ioHandle, out mode);
            return !success;
        }

        private static Object s_InternalSyncObject;

        private static Object InternalSyncObject {
            get {
                if (s_InternalSyncObject == null) {
                    Object o = new Object();
                    Interlocked.CompareExchange<Object>(ref s_InternalSyncObject, o, null);
                }
                return s_InternalSyncObject;
            }
        }

        private static bool _stdInRedirectQueried = false;

        private static bool _isStdInRedirected = false;

        internal static bool IsInputRedirected {
            get {
                if (_stdInRedirectQueried) {
                    return _isStdInRedirected;
                }
                lock (InternalSyncObject) {
                    if (_stdInRedirectQueried) {
                        return _isStdInRedirected;
                    }
                    _isStdInRedirected = IsHandleRedirected(GetStdHandle(StdHandle.Stdin));
                    _stdInRedirectQueried = true;
                    return _isStdInRedirected;
                }
            }
        }

        private static bool _stdOutRedirectQueried = false;

        private static bool _isStdOutRedirected = false;

        internal static bool IsOutputRedirected {
            get {
                if (_stdOutRedirectQueried) {
                    return _isStdOutRedirected;
                }
                lock (InternalSyncObject) {
                    if (_stdOutRedirectQueried) {
                        return _isStdOutRedirected;
                    }
                    _isStdOutRedirected = IsHandleRedirected(GetStdHandle(StdHandle.Stdout));
                    _stdOutRedirectQueried = true;
                    return _isStdOutRedirected;
                }
            }
        }

        private static bool _stdErrRedirectQueried = false;

        private static bool _isStdErrRedirected = false;

        internal static bool IsErrorRedirected {
            get {
                if (_stdErrRedirectQueried) {
                    return _isStdErrRedirected;
                }
                lock (InternalSyncObject) {
                    if (_stdErrRedirectQueried) {
                        return _isStdErrRedirected;
                    }
                    _isStdErrRedirected = IsHandleRedirected(GetStdHandle(StdHandle.Stderr));
                    _stdErrRedirectQueried = true;
                    return _isStdErrRedirected;
                }
            }
        }
    }
#endif
}
