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
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Simple implementation of ASCII encoding/decoding.  The default instance (PythonAsciiEncoding.Instance) is
    /// setup to always convert even values outside of the ASCII range.  The EncoderFallback/DecoderFallbacks can
    /// be replaced with versions that will throw exceptions instead though.
    /// </summary>
    [Serializable]
    sealed class PythonAsciiEncoding : Encoding {
        internal static readonly Encoding Instance = MakeNonThrowing();
        internal static readonly Encoding SourceEncoding = MakeSourceEncoding();

        internal PythonAsciiEncoding()
            : base() {
        }

        internal static Encoding MakeNonThrowing() {
            Encoding enc = new PythonAsciiEncoding();
#if FEATURE_ENCODING
            // we need to Clone the new instance here so that the base class marks us as non-readonly
            enc = (Encoding)enc.Clone();
            enc.DecoderFallback = new NonStrictDecoderFallback();
            enc.EncoderFallback = new NonStrictEncoderFallback();
#endif
            return enc;
        }

        private static Encoding MakeSourceEncoding() {
            Encoding enc = new PythonAsciiEncoding();
#if FEATURE_ENCODING
            // we need to Clone the new instance here so that the base class marks us as non-readonly
            enc = (Encoding)enc.Clone();
            enc.DecoderFallback = new SourceNonStrictDecoderFallback();
#endif
            return enc;
        }

        public override int GetByteCount(char[] chars, int index, int count) {
#if FEATURE_ENCODING
            int byteCount = 0;
            int charEnd = index + count;
            while (index < charEnd) {
                char c = chars[index];
                if (c > 0x7f) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, index)) {
                        byteCount += efb.Remaining;
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

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            int charEnd = charIndex + charCount;
            int outputBytes = 0;
            while (charIndex < charEnd) {
                char c = chars[charIndex];
#if FEATURE_ENCODING
                if (c > 0x7f) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
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

        public override int GetCharCount(byte[] bytes, int index, int count) {
            int byteEnd = index + count;
            int outputChars = 0;
            while (index < byteEnd) {
                byte b = bytes[index];
#if FEATURE_ENCODING
                if (b > 0x7f) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new[] { b }, 0)) {
                        outputChars += dfb.Remaining;
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

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            int byteEnd = byteIndex + byteCount;
            int outputChars = 0;
            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex];
#if FEATURE_ENCODING
                if (b > 0x7f) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new[] { b }, 0)) {
                        while (dfb.Remaining != 0) {
                            chars[charIndex++] = dfb.GetNextChar();
                            outputChars++;
                        }
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
            return charCount * 4;
        }

        public override int GetMaxCharCount(int byteCount) {
            return byteCount;
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
        private int _index;

        public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
            throw PythonOps.UnicodeEncodeError("'ascii' codec can't encode character '\\u{0:X}{1:04X}' in position {1}: ordinal not in range(128)", (int)charUnknownHigh, (int)charUnknownLow, index);
        }

        public override bool Fallback(char charUnknown, int index) {
            if (charUnknown > 0xff) {
                throw PythonOps.UnicodeEncodeError("'ascii' codec can't encode character '\\u{0:X}' in position {1}: ordinal not in range(128)", (int)charUnknown, index);
            }

            _buffer.Add(charUnknown);
            return true;
        }
        
        public override char GetNextChar() {
            return _buffer[_index++];
        }

        public override bool MovePrevious() {
            if (_index > 0) {
                _index--;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _buffer.Count - _index; }
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
        private int _index;

        public override bool Fallback(byte[] bytesUnknown, int index) {
            _bytes.AddRange(bytesUnknown);
            return true;
        }

        public override char GetNextChar() {
            if (_index == _bytes.Count) {
                return char.MinValue;
            }
            return (char)_bytes[_index++];
        }

        public override bool MovePrevious() {
            if (_index > 0) {
                _index--;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _bytes.Count - _index; }
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
            throw new BadSourceException(bytesUnknown[index]);
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
