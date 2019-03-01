// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime {

    internal class PythonSurrogateEscapeEncoding : Encoding {
        // Defined in PEP 383
        private const int LoneSurrogateBase = 0xdc00;

        // The following two must be different from each other and pass-through characters for UTF-7
        private const char Pass1SurrogateMarker = '?';
        private const char Pass2SurrogateMarker = '-';

        private Encoding Pass1Encoding { get; }
        private Encoding Pass2Encoding { get; }

        public int CharacterWidth { get; }
        public bool IsBigEndian { get; } // meaningul only for wide-char encodings

        public PythonSurrogateEscapeEncoding(Encoding encoding) {
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));

            byte[] markerBytes = encoding.GetBytes(new[] { Pass1SurrogateMarker });
            CharacterWidth = markerBytes.Length;
            IsBigEndian = markerBytes[0] == 0;

            Pass1Encoding = (Encoding)encoding.Clone();
            Pass1Encoding.DecoderFallback = new SurrogateEscapeDecoderFallback(isPass1: true, CharacterWidth, IsBigEndian);
            Pass1Encoding.EncoderFallback = new SurrogateEscapeEncoderFallback(isPass1: true);

            Pass2Encoding = (Encoding)encoding.Clone();
            Pass2Encoding.DecoderFallback = new SurrogateEscapeDecoderFallback(isPass1: false, CharacterWidth, IsBigEndian);
            Pass2Encoding.EncoderFallback = new SurrogateEscapeEncoderFallback(isPass1: false);
        }

        public override int GetByteCount(char[] chars, int index, int count)
            => GetEncoder().GetByteCount(chars, index, count, flush: true);

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
            => GetEncoder().GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush: true);

        public override int GetCharCount(byte[] bytes, int index, int count)
            => GetDecoder().GetCharCount(bytes, index, count);

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            => GetDecoder().GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);

        public override int GetMaxByteCount(int charCount)
            => Pass1Encoding.GetMaxByteCount(charCount);

        public override int GetMaxCharCount(int byteCount)
            => Pass1Encoding.GetMaxCharCount(byteCount);

        public override Decoder GetDecoder()
            => new SurrogateEscapeDecoder(this);

        public override Encoder GetEncoder()
            => new SurrogateEscapeEncoder(this);

        public override int CodePage => Pass1Encoding.CodePage;
        public override int WindowsCodePage => Pass1Encoding.WindowsCodePage;

        public override string BodyName => Pass1Encoding.BodyName + "-surrogateescape";
        public override string EncodingName => Pass1Encoding.EncodingName + " with Surrogate Escape";

        public override string HeaderName => BodyName;
        public override string WebName => BodyName;
        public override bool IsBrowserDisplay => false;
        public override bool IsBrowserSave => false;
        public override bool IsMailNewsDisplay => false;
        public override bool IsMailNewsSave => false;

        public override bool IsSingleByte => Pass1Encoding.IsSingleByte;

        public override int GetHashCode() => Pass1Encoding.GetHashCode();
        public override byte[] GetPreamble() => Pass1Encoding.GetPreamble();
        public override bool IsAlwaysNormalized(NormalizationForm form) => false;

        private class SurrogateEscapeDecoder : Decoder {
            private readonly PythonSurrogateEscapeEncoding _parentEncoding;
            private readonly Decoder _pass1decoder;
            private Decoder _pass2decoder;

            public SurrogateEscapeDecoder(PythonSurrogateEscapeEncoding parentEncoding) {
                _parentEncoding = parentEncoding;
                _pass1decoder = _parentEncoding.Pass1Encoding.GetDecoder();
            }

            public override int GetCharCount(byte[] bytes, int index, int count)
                => _pass1decoder.GetCharCount(bytes, index, count, flush: true);

            public override int GetCharCount(byte[] bytes, int index, int count, bool flush)
                => _pass1decoder.GetCharCount(bytes, index, count, flush);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
                => GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, bool flush) {
                var fbuf1 = _pass1decoder.FallbackBuffer as SurrogateEscapeDecoderFallbackBuffer;
                var fbuf2 = _pass2decoder?.FallbackBuffer as SurrogateEscapeDecoderFallbackBuffer;
                int? surIdxStart = fbuf1?.LoneSurrogateCount;

                int written = _pass1decoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush);

                // If there were no lone surrogates, the job is done
                if (fbuf1?.LoneSurrogateCount == surIdxStart && (fbuf2?.IsEmpty ?? true) && flush) {
                    return written;
                }

                // Lazy creation of _pass2decoder
                if (_pass2decoder == null) {
                    _pass2decoder = _parentEncoding.Pass2Encoding.GetDecoder();
                    fbuf2 = (SurrogateEscapeDecoderFallbackBuffer)_pass2decoder.FallbackBuffer;
                }

                // replace surrogate markers with actual surrogates
                var chars2 = new char[written];
                _pass2decoder.GetChars(bytes, byteIndex, byteCount, chars2, 0, flush);

                for (int i = 0, j = charIndex; i < written; i++, j++) {
                    if (chars[j] != chars2[i]) {
                        chars[j] = fbuf2.GetLoneSurrogate();
                    }
                }

                return written;
            }

            public override void Reset() {
                _pass1decoder.Reset();
                _pass2decoder?.Reset();
            }
        }

        private class SurrogateEscapeDecoderFallback : DecoderFallback {
            private readonly bool _isPass1;
            private readonly int _charWidth;
            private readonly bool _isBigEndian;

            public SurrogateEscapeDecoderFallback(bool isPass1, int charWidth, bool isBigEndian) {
                _isPass1 = isPass1;
                _charWidth = charWidth;
                _isBigEndian = isBigEndian;
            }

            public override int MaxCharCount => _charWidth;

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeDecoderFallbackBuffer(_isPass1, _charWidth, _isBigEndian);
        }

        private class SurrogateEscapeDecoderFallbackBuffer : DecoderFallbackBuffer {
            private readonly char _marker;
            private readonly Queue<byte> _escapes;
            private readonly int _charWidth;
            private readonly bool _isBigEndian;

            private int _escCnt;
            private int _charCnt;

            public SurrogateEscapeDecoderFallbackBuffer(bool isPass1, int charWidth, bool isBigEndian) {
                _marker = isPass1 ? Pass1SurrogateMarker : Pass2SurrogateMarker;
                _escapes = isPass1 ? null : new Queue<byte>();
                _charWidth = charWidth;
                _isBigEndian = isBigEndian;
            }

            public override int Remaining => _charCnt;

            public override bool Fallback(byte[] bytesUnknown, int index) {

                if (bytesUnknown.Length != _charWidth) {
                    throw new DecoderFallbackException($"Invalid encoding bytes at position {index}", bytesUnknown, index);
                }

                // test for value below 128
                byte[] orderedBytes = bytesUnknown;
                if (_charWidth > 1 && _isBigEndian == BitConverter.IsLittleEndian) {
                    // non-native endianness
                    orderedBytes = new byte[_charWidth];
                    for (int i = 0, j = _charWidth - 1; i < _charWidth / 2; i++, j--) {
                        orderedBytes[j] = bytesUnknown[i];
                    }
                }
                uint unknown;
                switch (_charWidth) {
                    case 1: unknown = bytesUnknown[0]; break;
                    case 2: unknown = BitConverter.ToUInt16(orderedBytes, 0); break;
                    case 4: unknown = BitConverter.ToUInt32(orderedBytes, 0); break;
                    default: throw new DecoderFallbackException($"Invalid encoding bytes at position {index}", bytesUnknown, index);
                }
                if (unknown < 128u) {
                    throw new DecoderFallbackException(
                        $"Character '\\x{unknown:X2}' at position {index}: values below 128 cannot be smuggled (PEP 383)",
                        bytesUnknown,
                        index
                    );
                }

                if (_escapes != null) {
                    for (int i = 0; i < bytesUnknown.Length; i++) {
                        _escapes.Enqueue(bytesUnknown[i]);
                    }
                }
                _escCnt += _charWidth;
                _charCnt = _charWidth;

                return true;
            }

            public override char GetNextChar() {
                if (_charCnt <= 0) return '\0';
                _charCnt--;
                return _marker; // unfortunately, returning the actual lone surrogate here would result in an exception
            }

            public override bool MovePrevious() {
                if (_charCnt >= _charWidth) return false;
                _charCnt++;
                return true;
            }

            // not called for pass1 decoding
            public char GetLoneSurrogate() {
                _escCnt--;
                return (char)(_escapes.Dequeue() | LoneSurrogateBase);
            }

            public bool IsEmpty => (_escapes?.Count ?? 0) == 0;

            public int LoneSurrogateCount => _escCnt;

            public override void Reset() {
                _escapes?.Clear();
                _escCnt = 0;
                _charCnt = 0;
            }
        }

        private class SurrogateEscapeEncoder : Encoder {
            private readonly PythonSurrogateEscapeEncoding _parentEncoding;
            private readonly Encoder _pass1encoder;
            private Encoder _pass2encoder;
            private readonly int _characterWidth;

            public SurrogateEscapeEncoder(PythonSurrogateEscapeEncoding parentEncoding) {
                _parentEncoding = parentEncoding;
                _characterWidth = _parentEncoding.CharacterWidth;

                _pass1encoder = _parentEncoding.Pass1Encoding.GetEncoder();

                var fbuf1 = _pass1encoder.FallbackBuffer as SurrogateEscapeEncoderFallbackBuffer;
                if (fbuf1 != null) fbuf1.EncodingCharWidth = _characterWidth;
            }

            public override int GetByteCount(char[] chars, int index, int count, bool flush) {
                var fbuf1 = _pass1encoder.FallbackBuffer as SurrogateEscapeEncoderFallbackBuffer;

                if (fbuf1 != null) fbuf1.ByteCountingMode = true;
                int numBytes = _pass1encoder.GetByteCount(chars, index, count, flush);
                if (fbuf1 != null) fbuf1.ByteCountingMode = false;

                return numBytes;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush) {
                var fbuf1 = _pass1encoder.FallbackBuffer as SurrogateEscapeEncoderFallbackBuffer;
                var fbuf2 = _pass2encoder?.FallbackBuffer as SurrogateEscapeEncoderFallbackBuffer;
                int? surIdxStart = fbuf1?.LoneSurrogateCount;

                int written = _pass1encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush);

                // If there were no lone surrogates, the job is done
                if (fbuf1?.LoneSurrogateCount == surIdxStart && (fbuf2?.IsEmpty ?? true) && flush) {
                    return written;
                }

                // Lazy creation of _pass2encoder
                if (_pass2encoder == null) {
                    _pass2encoder = _parentEncoding.Pass2Encoding.GetEncoder();
                    fbuf2 = (SurrogateEscapeEncoderFallbackBuffer)_pass2encoder.FallbackBuffer;
                    fbuf2.EncodingCharWidth = _characterWidth;
                }

                // Restore original escaped bytes
                var bytes2 = new byte[written];
                _pass2encoder.GetBytes(chars, charIndex, charCount, bytes2, 0, flush);

                for (int i = 0, j = byteIndex; i < written; i++, j++) {
                    if (bytes[j] != bytes2[i]) {
                        int ofs = (j / _characterWidth) * _characterWidth;
                        for (int p = 0; p < _characterWidth; p++) {
                            bytes[ofs++] = fbuf2.GetLoneSurrogate();
                        }
                        int skip = ofs - j - 1;
                        i += skip;
                        j += skip;
                    }
                }

                return written;
            }

            public override void Reset() {
                _pass1encoder.Reset();
                _pass2encoder?.Reset();
            }
        }

        private class SurrogateEscapeEncoderFallback : EncoderFallback {
            private readonly bool _isPass1;

            public SurrogateEscapeEncoderFallback(bool isPass1) {
                _isPass1 = isPass1;
            }

            public override int MaxCharCount => 1;

            public override EncoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeEncoderFallbackBuffer(_isPass1);
        }

        private class SurrogateEscapeEncoderFallbackBuffer : EncoderFallbackBuffer {
            private readonly char _marker;
            private readonly Queue<byte> _escapes;

            private struct ByteCounter {
                private int _bcmCounter; // used when only counting bytes
                private int _encCounter; // used during actual encoding

                public bool ByteCountingMode { get; set; }

                public int Value => ByteCountingMode ? _bcmCounter : _encCounter;

                public void AddNumBytes(int numBytes) {
                    if (ByteCountingMode) {
                        _bcmCounter += numBytes;
                    } else {
                        _encCounter += numBytes;
                    }
                }

                public void Reset() => _bcmCounter = _encCounter = 0;
            }
            private ByteCounter _byteCnt;
            private int _escCnt;

            public SurrogateEscapeEncoderFallbackBuffer(bool isPass1) {
                _marker = isPass1 ? Pass1SurrogateMarker : Pass2SurrogateMarker;
                _escapes = isPass1 ? null : new Queue<byte>();
            }

            public override int Remaining => _byteCnt.Value / EncodingCharWidth;

            public override bool Fallback(char charUnknown, int index) {
                if ((charUnknown & ~0xff) != LoneSurrogateBase) throw new EncoderFallbackException($"Cannot encode character '{charUnknown}' at position {index}");

                if (!ByteCountingMode) {
                    _escapes?.Enqueue((byte)(charUnknown & 0xff));
                    _escCnt++;
                }
                _byteCnt.AddNumBytes(1);
                return _byteCnt.Value >= EncodingCharWidth;
            }

            public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
                // does not handle surrogate pairs, but they will never be created in a round-trip situation
                throw new EncoderFallbackException($"Cannot encode surrogate pair '{charUnknownHigh}', '{charUnknownLow}' at position {index}");
            }

            public override char GetNextChar() {
                if (_byteCnt.Value < EncodingCharWidth) return '\0';
                _byteCnt.AddNumBytes(-EncodingCharWidth);
                //return (char)_surrogates[_surrogates.Count - 1].ByteValue; // unfortunately, this would be encoded if used with a multibyte encoding
                return _marker;
            }

            public override bool MovePrevious() {
                if (_byteCnt.Value > 0) return false;
                _byteCnt.AddNumBytes(EncodingCharWidth);
                return true;
            }

            public byte GetLoneSurrogate() {
                _escCnt--;
                return _escapes.Dequeue();
            }

            public bool IsEmpty => (_escapes?.Count ?? 0) == 0;

            public int LoneSurrogateCount => _escCnt;

            public int EncodingCharWidth { get; set; }

            public bool ByteCountingMode {
                get { return _byteCnt.ByteCountingMode; }
                set { _byteCnt.ByteCountingMode = value; }
            }

            public override void Reset() {
                _escapes?.Clear();
                _escCnt = 0;
                _byteCnt.Reset();
            }
        }
    }
}
