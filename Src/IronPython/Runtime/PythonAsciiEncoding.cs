// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Reflection;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Simple implementation of ASCII encoding/decoding.  The default instance (PythonAsciiEncoding.Instance) is
    /// setup to always convert even values outside of the ASCII range.  The EncoderFallback/DecoderFallbacks can
    /// be replaced with versions that will throw exceptions instead though.
    /// </summary>
    [Serializable]
    sealed class PythonAsciiEncoding : Encoding {
        // Singleton (global) instances are readonly, so their fallbacks cannot be accidentally modified unless cloned
        internal static readonly Encoding Instance = MakeNonThrowing();
        internal static readonly Encoding SourceEncoding = MakeSourceEncoding();

        internal PythonAsciiEncoding()
            : base() {
        }

        internal PythonAsciiEncoding(EncoderFallback encoderFallback, DecoderFallback decoderFallback)
#if !NET45
            // base(0, encoderFallback, decoderFallback) publicly accessible only in .NET Framework 4.6 and up
            : base(0, encoderFallback, decoderFallback) {
#else
            : base() {
            // workaround for lack of proper base constructor access - implementation dependent
            typeof(Encoding).GetField("encoderFallback", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, encoderFallback);
            typeof(Encoding).GetField("decoderFallback", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, decoderFallback);
#endif
        }

        internal static Encoding MakeNonThrowing() {
            Encoding enc;
#if FEATURE_ENCODING
            enc = new PythonAsciiEncoding(new NonStrictEncoderFallback(), new NonStrictDecoderFallback());
#else
            enc = new PythonAsciiEncoding();
#endif
            return enc;
        }

        private static Encoding MakeSourceEncoding() {
            Encoding enc;
#if FEATURE_ENCODING
            enc = new PythonAsciiEncoding(new NonStrictEncoderFallback(), new SourceNonStrictDecoderFallback());
#else
            enc = new PythonAsciiEncoding();
#endif
            return enc;
        }

        public override int GetByteCount(char[] chars, int index, int count)
            => GetByteCount(chars, index, count, null);

        private int GetByteCount(char[] chars, int index, int count, EncoderFallbackBuffer efb) {
#if FEATURE_ENCODING
            int byteCount = 0;
            int charEnd = index + count;
            while (index < charEnd) {
                char c = chars[index];
                if (c > 0x7f) {
                    if (efb == null) {
                        efb = EncoderFallback.CreateFallbackBuffer();
                    }
                    if (efb.Fallback(c, index)) {
                        byteCount += efb.Remaining;
                        while (efb.GetNextChar() != char.MinValue) { /* empty */ }
                    }
                } else {
                    byteCount++;
                }
                index++;
            }
            return byteCount;
#else
            return count;
#endif
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
            => GetBytes(chars, charIndex, charCount, bytes, byteIndex, null);

        private int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, EncoderFallbackBuffer efb) {
            int charEnd = charIndex + charCount;
            int outputBytes = 0;
            while (charIndex < charEnd) {
                char c = chars[charIndex];
#if FEATURE_ENCODING
                if (c > 0x7f) {
                    if (efb == null) {
                        efb = EncoderFallback.CreateFallbackBuffer();
                    }
                    if (efb.Fallback(c, charIndex)) {
                        while (efb.Remaining != 0) {
                            bytes[byteIndex++] = (byte)efb.GetNextChar();
                            outputBytes++;
                        }
                    }
                } else {
                    bytes[byteIndex++] = (byte)c;
                    outputBytes++;
                }
#else
                bytes[byteIndex++] = (byte)c;
                outputBytes++;
#endif
                charIndex++;
            }
            return outputBytes;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
            => GetCharCount(bytes, index, count, null);

        private int GetCharCount(byte[] bytes, int index, int count, DecoderFallbackBuffer dfb) {
            int byteEnd = index + count;
            int outputChars = 0;
            while (index < byteEnd) {
                byte b = bytes[index];
#if FEATURE_ENCODING
                if (b > 0x7f) {
                    if (dfb == null) {
                        dfb = DecoderFallback.CreateFallbackBuffer();
                    }
                    try {
                        if (dfb.Fallback(new[] { b }, index)) {
                            outputChars += dfb.Remaining;
                            while (dfb.GetNextChar() != char.MinValue) { /* empty */ }
                        }
                    } catch (DecoderFallbackException ex) {
                        var dfe = new DecoderFallbackException("ordinal out of range(128)", ex.BytesUnknown, ex.Index);
                        dfe.Data.Add("encoding", EncodingName);
                        throw dfe;
                    }
                } else {
                    outputChars++;
                }
#else
                outputChars++;
#endif
                index++;
            }
            return outputChars;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            => GetChars(bytes, byteIndex, byteCount, chars, charIndex, null);

        private int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, DecoderFallbackBuffer dfb) {
            int byteEnd = byteIndex + byteCount;
            int outputChars = 0;
            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex];
#if FEATURE_ENCODING
                if (b > 0x7f) {
                    if (dfb == null) {
                        dfb = DecoderFallback.CreateFallbackBuffer();
                    }
                    try {
                        if (dfb.Fallback(new[] { b }, byteIndex)) {
                            while (dfb.Remaining != 0) {
                                chars[charIndex++] = dfb.GetNextChar();
                                outputChars++;
                            }
                        }
                    } catch (DecoderFallbackException ex) {
                        var dfe = new DecoderFallbackException("ordinal out of range(128)", ex.BytesUnknown, ex.Index);
                        dfe.Data.Add("encoding", EncodingName);
                        throw dfe;
                    }
                } else {
                    chars[charIndex++] = (char)b;
                    outputChars++;
                }
#else
                chars[charIndex++] = (char)b;
                outputChars++;
#endif
                byteIndex++;
            }
            return outputChars;
        }

        public override int GetMaxByteCount(int charCount) {
            return charCount * Math.Max(1, EncoderFallback.MaxCharCount);
        }

        public override int GetMaxCharCount(int byteCount) {
            return byteCount * Math.Max(1, DecoderFallback.MaxCharCount);
        }

        public override string WebName {
            get {
                return "ascii";
            }
        }

#if FEATURE_ENCODING
        public override string EncodingName {
            get {
                return "ascii";
            }
        }

        public override Encoder GetEncoder() => new PythonAsciiEncoder(this);

        private class PythonAsciiEncoder : Encoder {
            private readonly PythonAsciiEncoding _encoding;

            public PythonAsciiEncoder(PythonAsciiEncoding encoding) {
                _encoding = encoding;
                this.Fallback = encoding.EncoderFallback;
            }

            public override int GetByteCount(char[] chars, int index, int count, bool flush)
                => _encoding.GetByteCount(chars, index, count, this.FallbackBuffer);

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
                => _encoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex, this.FallbackBuffer);
        }

        public override Decoder GetDecoder() => new PythonAsciiDecoder(this);

        private class PythonAsciiDecoder : Decoder {
            private readonly PythonAsciiEncoding _encoding;

            public PythonAsciiDecoder(PythonAsciiEncoding encoding) {
                _encoding = encoding;
                this.Fallback = encoding.DecoderFallback;
            }

            public override int GetCharCount(byte[] bytes, int index, int count)
                => _encoding.GetCharCount(bytes, index, count, this.FallbackBuffer);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
                => _encoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex, this.FallbackBuffer);
        }
#endif
    }

#if FEATURE_ENCODING
    class NonStrictEncoderFallback : EncoderFallback {
        public override EncoderFallbackBuffer CreateFallbackBuffer() {
            return new NonStrictEncoderFallbackBuffer();
        }
        
        public override int MaxCharCount {
            get { return 1; }
        }
    }

    class NonStrictEncoderFallbackBuffer : EncoderFallbackBuffer {
        private List<char> _buffer = new List<char>();
        private int _curIndex;
        private int _prevIndex;

        public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
            throw PythonOps.UnicodeEncodeError("'ascii' codec can't encode character '\\u{0:X}{1:04X}' in position {2}: ordinal not in range(128)", (int)charUnknownHigh, (int)charUnknownLow, index);
        }

        public override bool Fallback(char charUnknown, int index) {
            if (charUnknown > 0xff) {
                throw PythonOps.UnicodeEncodeError("ascii", charUnknown.ToString(), index, index + 1, "ordinal not in range(128)");
            }

            if (_curIndex == _buffer.Count && _curIndex > 0) {
                // save memory
                _buffer.Clear();
                _curIndex = 0;
            }
            _prevIndex = _curIndex;
            _buffer.Add(charUnknown);
            return true;
        }
        
        public override char GetNextChar() {
            if (_curIndex == _buffer.Count) {
                return char.MinValue;
            }
            return _buffer[_curIndex++];
        }

        public override bool MovePrevious() {
            if (_curIndex > _prevIndex) {
                _curIndex--;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _buffer.Count - _curIndex; }
        }
    }

    class NonStrictDecoderFallback : DecoderFallback {
        public override DecoderFallbackBuffer CreateFallbackBuffer() {
            return new NonStrictDecoderFallbackBuffer();
        }

        public override int MaxCharCount {
            get { return 1; }
        }
    }

    // no ctors on DecoderFallbackBuffer in Silverlight
    class NonStrictDecoderFallbackBuffer : DecoderFallbackBuffer {
        private List<byte> _bytes = new List<byte>();
        private int _curIndex;
        private int _prevIndex;

        public override bool Fallback(byte[] bytesUnknown, int index) {
            if (_curIndex == _bytes.Count && _curIndex > 0) {
                // save memory
                _bytes.Clear();
                _curIndex = 0;
            }
            _prevIndex = _curIndex;
            _bytes.AddRange(bytesUnknown);
            return true;
        }

        public override char GetNextChar() {
            if (_curIndex == _bytes.Count) {
                return char.MinValue;
            }
            return (char)_bytes[_curIndex++];
        }

        public override bool MovePrevious() {
            if (_curIndex > _prevIndex) {
                _curIndex--;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _bytes.Count - _curIndex; }
        }
    }
    
    class SourceNonStrictDecoderFallback : DecoderFallback {
        public override DecoderFallbackBuffer CreateFallbackBuffer() {
            return new SourceNonStrictDecoderFallbackBuffer();
        }

        public override int MaxCharCount {
            get { return 1; }
        }
    }

    // no ctors on DecoderFallbackBuffer in Silverlight
    class SourceNonStrictDecoderFallbackBuffer : DecoderFallbackBuffer {
        public override bool Fallback(byte[] bytesUnknown, int index) {
            throw new BadSourceException(bytesUnknown[0]);
        }

        public override char GetNextChar() {
            throw new NotImplementedException();
        }

        public override bool MovePrevious() {
            throw new NotImplementedException();
        }

        public override int Remaining {
            get { throw new NotImplementedException(); }
        }
    }
#endif

    [Serializable]
    internal class BadSourceException : Exception {
        internal byte _badByte;
        public BadSourceException(byte b) {
            _badByte = b;
        }

        public BadSourceException() : base() { }
        public BadSourceException(string msg)
            : base(msg) {
        }
        public BadSourceException(string message, Exception innerException)
            : base(message, innerException) {
        }

#if FEATURE_SERIALIZATION
        protected BadSourceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }
}
