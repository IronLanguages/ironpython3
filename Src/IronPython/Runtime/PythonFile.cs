// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;

namespace IronPython.Runtime {
    #region Readers

    // The following set of classes is used to translate between pythonic file stream semantics and those of
    // the runtime and the underlying system.
    //
    // Python supports opening files in binary and text mode. Binary is fairly obvious: we want to preserve
    // the data as is, to the point where it should be possible to round-trip an arbitrary binary file without
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

            // If size is zero return empty string
            if (size == 0)
                return String.Empty;

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
            if (_buffer == null)
                _buffer = new byte[BufferSize];
            while (true) {
                int count = _stream.Read(_buffer, 0, BufferSize);
                if (count == 0)
                    break;
                sb.Append(PackDataIntoString(_buffer, count));
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
                _position++;
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
                _position++;

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

        public abstract void FlushToDisk();

        public void FlushToDiskWorker(Stream stream) {
            if (stream is FileStream fs) {
                fs.Flush(true);
            }
        }
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

        public override void FlushToDisk() {
            FlushToDiskWorker(_stream);
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

        public override void FlushToDisk() {
            var streamWriter = _writer as StreamWriter;
            if (streamWriter != null) {
                streamWriter.Flush();
                FlushToDiskWorker(streamWriter.BaseStream);
            }
        }
    }

    #endregion

    #region File Manager

    internal class PythonFileManager {
        private HybridMapping<object> mapping = new HybridMapping<object>(3);

        public int AddToStrongMapping(object o, int pos = -1) {
            return mapping.StrongAdd(o, pos);
        }

        public void Remove(object o) {
            mapping.RemoveOnObject(o);
        }

        public void RemoveObjectOnId(int id) {
            mapping.RemoveOnId(id);
        }

        public Modules.PythonIOModule.FileIO GetFileFromId(PythonContext context, int id) {
            if (TryGetFileFromId(context, id, out Modules.PythonIOModule.FileIO pf)) {
                return pf;
            }

            throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
        }

        public bool TryGetFileFromId(PythonContext context, int id, out Modules.PythonIOModule.FileIO pf) {
            pf = mapping.GetObjectFromId(id) as Modules.PythonIOModule.FileIO;
            return pf != null;
        }


        public bool TryGetObjectFromId(PythonContext context, int id, out object o) {
            o = mapping.GetObjectFromId(id);
            return o != null;
        }

        public object GetObjectFromId(int id) {
            object o = mapping.GetObjectFromId(id);

            if (o == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }
            return o;
        }

        public int GetIdFromFile(Modules.PythonIOModule.FileIO pf) {
            return mapping.GetIdFromObject(pf);
        }

        public void CloseIfLast(CodeContext context, int fd, Modules.PythonIOModule.FileIO pf) {
            mapping.RemoveOnId(fd);
            if (-1 == mapping.GetIdFromObject(pf)) {
                pf.close(context);
            }
        }

        public void CloseIfLast(int fd, Stream stream) {
            mapping.RemoveOnId(fd);
            if (-1 == mapping.GetIdFromObject(stream)) {
                stream.Close();
            }
        }

        public int GetOrAssignIdForFile(Modules.PythonIOModule.FileIO pf) {
            int res = mapping.GetIdFromObject(pf);
            if (res == -1) {
                // lazily created weak mapping
                res = mapping.WeakAdd(pf);
            }
            return res;
        }

        public int GetIdFromObject(object o) {
            return mapping.GetIdFromObject(o);
        }


        public int GetOrAssignIdForObject(object o) {
            int res = mapping.GetIdFromObject(o);
            if (res == -1) {
                // lazily created weak mapping
                res = mapping.WeakAdd(o);
            }
            return res;
        }

        public bool ValidateFdRange(int fd) {
            return fd >= 0 && fd < HybridMapping<object>.SIZE;
        }
    }

    #endregion
}
