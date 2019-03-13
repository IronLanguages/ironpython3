// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime {

    /// <summary>
    /// Wrapper class for any well-behaved <see cref="System.Text.Encoding"/> (like any encodings provided by .NET)
    /// that allows for encoding/decoding fallbacks operating on byte level.
    /// </summary>
    /// <remarks>
    /// Python encoding/decoding fallbacks (called "error handlers" in Python documentation) can deliver
    /// fallback values as strings as well as bytes. .NET fallback mechanism only allows for characters as fallbacks,
    /// and only those characters that are deemed valid by a given encoding. So, for instance, lone surrogate escapes,
    /// produced by Python's 'surrogateescape' error handler, are not allowed in the standard .NET fallback protocol.
    ///
    /// This class extends the standard .NET fallback protocol to allow fallbacks to provide values normally
    /// not allowed by .NET but allowed by Python. It also allows the fallbacks to provide fallback values
    /// as a sequence of bytes.
    ///
    /// Note: Currently, it is not possible to set the fallbacks through assignment to
    /// <see cref="EncoderFallback"/> or <see cref="DecoderFallback"/>; the fallbacks have to be provided
    /// to the constructor. Also, the fallbacks have to be of type <see cref="PythonEncoderFallback"/>
    /// and <see cref="PythonDecoderFallback"/>, which implement the extended fallback protocol.
    /// </remarks>
    internal class PythonEncoding : Encoding {
        // The following two must be different from each other and be pass-through characters for UTF-7
        private const char Pass1Marker = '?';
        private const char Pass2Marker = '-';

        public int CharacterWidth { get; }
        public bool IsBigEndian { get; } // meaningful only for wide-char encodings

        private Encoding Pass1Encoding { get; }
        private Encoding Pass2Encoding { get; }

        private Decoder _residentDecoder;
        private Encoder _residentEncoder;

        public PythonEncoding(Encoding encoding, PythonEncoderFallback encoderFallback, PythonDecoderFallback decoderFallback)
            //: base(0, encoderFallback, decoderFallback)  // unfortunately, this constructor is internal until .NET Framework 4.6
        {

            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            if (encoderFallback == null) throw new ArgumentNullException(nameof(encoderFallback));
            if (decoderFallback == null) throw new ArgumentNullException(nameof(decoderFallback));

            byte[] markerBytes = encoding.GetBytes(new[] { Pass1Marker });
            CharacterWidth = markerBytes.Length;
            IsBigEndian = markerBytes[0] == 0;

            // set up pass 1 Encoding, using provided fallback instances
            encoderFallback.Encoding = decoderFallback.Encoding = this;
            encoderFallback.IsPass1 = decoderFallback.IsPass1 = true;
            Pass1Encoding = (Encoding)encoding.Clone();
            Pass1Encoding.EncoderFallback = encoderFallback;
            Pass1Encoding.DecoderFallback = decoderFallback;

            // set up pass 2 Encoding, using clones of provided fallback instances
            encoderFallback = (PythonEncoderFallback)encoderFallback.Clone();
            decoderFallback = (PythonDecoderFallback)decoderFallback.Clone();
            encoderFallback.IsPass1 = decoderFallback.IsPass1 = false;
            Pass2Encoding = (Encoding)encoding.Clone();
            Pass2Encoding.EncoderFallback = encoderFallback;
            Pass2Encoding.DecoderFallback = decoderFallback;
        }

        public override int GetByteCount(char[] chars, int index, int count) {
            if (_residentEncoder == null) {
                _residentEncoder = GetEncoder();
            } else {
                _residentEncoder.Reset();
            }
            return _residentEncoder.GetByteCount(chars, index, count, flush: true);
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            if (_residentEncoder == null) {
                _residentEncoder = GetEncoder();
            } else {
                _residentEncoder.Reset();
            }
            return _residentEncoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush: true);
        }

        public override int GetCharCount(byte[] bytes, int index, int count) {
            if (_residentDecoder == null) {
                _residentDecoder = GetDecoder();
            } else {
                _residentDecoder.Reset();
            }
            return _residentDecoder.GetCharCount(bytes, index, count, flush: true);
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            if (_residentDecoder == null) {
                _residentDecoder = GetDecoder();
            } else {
                _residentDecoder.Reset();
            }
            return _residentDecoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);
        }

        public override int GetMaxByteCount(int charCount)
            => Pass1Encoding.GetMaxByteCount(charCount);

        public override int GetMaxCharCount(int byteCount)
            => Pass1Encoding.GetMaxCharCount(byteCount);

        public override Encoder GetEncoder()
            => new PythonEncoder(this);

        public override Decoder GetDecoder()
            => new PythonDecoder(this);

        public override int CodePage => Pass1Encoding.CodePage;
        public override int WindowsCodePage => Pass1Encoding.WindowsCodePage;

        //public override string EncodingName => Pass1Encoding.EncodingName;
        public override string EncodingName => Pass1Encoding.EncodingName + " with Surrogate Escape";

        public override string HeaderName => Pass1Encoding.BodyName;
        public override string BodyName => Pass1Encoding.BodyName;
        public override string WebName => Pass1Encoding.BodyName;
        public override bool IsBrowserDisplay => false;
        public override bool IsBrowserSave => false;
        public override bool IsMailNewsDisplay => false;
        public override bool IsMailNewsSave => false;

        public override bool IsSingleByte => Pass1Encoding.IsSingleByte;

        public override int GetHashCode() => Pass1Encoding.GetHashCode();
        public override byte[] GetPreamble() => Pass1Encoding.GetPreamble();
        public override bool IsAlwaysNormalized(NormalizationForm form) => false;

        public class PythonEncoder : Encoder {
            private readonly PythonEncoding _parentEncoding;
            private readonly Encoder _pass1encoder;
            private Encoder _pass2encoder;
            private readonly int _characterWidth;

            public PythonEncoder(PythonEncoding parentEncoding) {
                _parentEncoding = parentEncoding;
                _characterWidth = _parentEncoding.CharacterWidth;

                _pass1encoder = _parentEncoding.Pass1Encoding.GetEncoder();
            }

            public override int GetByteCount(char[] chars, int index, int count, bool flush) {
                var fbuf1 = _pass1encoder.FallbackBuffer as PythonEncoderFallbackBuffer;

                if (fbuf1 != null) fbuf1.ByteCountingMode = true;
                int numBytes = _pass1encoder.GetByteCount(chars, index, count, flush);
                if (fbuf1 != null) fbuf1.ByteCountingMode = false;

                return numBytes;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush) {
                var fbuf1 = _pass1encoder.FallbackBuffer as PythonEncoderFallbackBuffer;
                var fbuf2 = _pass2encoder?.FallbackBuffer as PythonEncoderFallbackBuffer;
                int? fbkIdxStart = fbuf1?.FallbackByteCount;

                int written = _pass1encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush);

                // If there were no fallback bytes, the job is done
                if (fbuf1 == null || fbuf1.FallbackByteCount == fbkIdxStart && (fbuf2?.IsEmpty ?? true) && flush) {
                    return written;
                }

                // Lazy creation of _pass2encoder
                if (_pass2encoder == null) {
                    _pass2encoder = _parentEncoding.Pass2Encoding.GetEncoder();
                    fbuf2 = (PythonEncoderFallbackBuffer)_pass2encoder.FallbackBuffer;
                }

                // Restore original fallback bytes
                var bytes2 = new byte[written];
                _pass2encoder.GetBytes(chars, charIndex, charCount, bytes2, 0, flush);

                for (int i = 0, j = byteIndex; i < written; i++, j++) {
                    if (bytes[j] != bytes2[i]) {
                        int ofs = (j / _characterWidth) * _characterWidth;
                        for (int p = 0; p < _characterWidth; p++) {
                            bytes[ofs++] = fbuf2.GetFallbackByte();
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

        public abstract class PythonEncoderFallback : EncoderFallback, ICloneable {
            public PythonEncoding Encoding { get; set; }
            public bool IsPass1 { get; set; }

            public virtual object Clone() => MemberwiseClone();
        }

        protected abstract class PythonEncoderFallbackBuffer : EncoderFallbackBuffer {
            private readonly char _marker;
            private readonly Queue<byte> _fallbackBytes;

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
            private int _fbkCnt;

            public PythonEncoderFallbackBuffer(bool isPass1, int encodingCharWidth)
            {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _fallbackBytes = isPass1 ? null : new Queue<byte>();
                this.EncodingCharWidth = encodingCharWidth;
            }

            protected int EncodingCharWidth { get; }

            public abstract byte[] GetFallbackBytes(char charUnknown, int index);

            public override bool Fallback(char charUnknown, int index) {
                byte[] newFallbackBytes = GetFallbackBytes(charUnknown, index);

                if (!ByteCountingMode) {
                    if (_fallbackBytes != null) {
                        foreach (byte b in newFallbackBytes) {
                            _fallbackBytes.Enqueue(b);
                        }
                    }
                    _fbkCnt += newFallbackBytes.Length;
                }
                _byteCnt.AddNumBytes(newFallbackBytes.Length);
                return _byteCnt.Value >= EncodingCharWidth;
            }

            public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index)
                => Fallback(charUnknownHigh, index) | Fallback(charUnknownLow, index + 1);

            public override int Remaining => _byteCnt.Value / EncodingCharWidth;

            public override char GetNextChar() {
                if (_byteCnt.Value < EncodingCharWidth) return char.MinValue;

                _byteCnt.AddNumBytes(-EncodingCharWidth);
                return _marker;
            }

            public override bool MovePrevious() {
                if (_byteCnt.Value > 0) return false;

                _byteCnt.AddNumBytes(EncodingCharWidth);
                return true;
            }

            public byte GetFallbackByte() {
                _fbkCnt--;
                return _fallbackBytes.Dequeue();
            }

            public bool IsEmpty => (_fallbackBytes?.Count ?? 0) == 0;

            public int FallbackByteCount => _fbkCnt;

            public bool ByteCountingMode {
                get { return _byteCnt.ByteCountingMode; }
                set { _byteCnt.ByteCountingMode = value; }
            }

            public override void Reset() {
                _fallbackBytes?.Clear();
                _fbkCnt = 0;
                _byteCnt.Reset();
            }
        }

        private class PythonDecoder : Decoder {
            private readonly PythonEncoding _parentEncoding;
            private readonly Decoder _pass1decoder;
            private Decoder _pass2decoder;

            public PythonDecoder(PythonEncoding parentEncoding) {
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
                var fbuf1 = _pass1decoder.FallbackBuffer as PythonDecoderFallbackBuffer;
                var fbuf2 = _pass2decoder?.FallbackBuffer as PythonDecoderFallbackBuffer;
                int? surIdxStart = fbuf1?.FallbackCharCount;

                int written = _pass1decoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush);

                // If there were no lone surrogates, the job is done
                if (fbuf1 == null || fbuf1.FallbackCharCount == surIdxStart && (fbuf2?.IsEmpty ?? true) && flush) {
                    return written;
                }

                // Lazy creation of _pass2decoder
                if (_pass2decoder == null) {
                    _pass2decoder = _parentEncoding.Pass2Encoding.GetDecoder();
                    fbuf2 = (PythonDecoderFallbackBuffer)_pass2decoder.FallbackBuffer;
                }

                // replace surrogate markers with actual surrogates
                var chars2 = new char[written];
                _pass2decoder.GetChars(bytes, byteIndex, byteCount, chars2, 0, flush);

                for (int i = 0, j = charIndex; i < written; i++, j++) {
                    if (chars[j] != chars2[i]) {
                        chars[j] = fbuf2.GetFallbackChar();
                    }
                }

                return written;
            }

            public override void Reset() {
                _pass1decoder.Reset();
                _pass2decoder?.Reset();
            }
        }

        public abstract class PythonDecoderFallback : DecoderFallback, ICloneable {
            public PythonEncoding Encoding { get; set; }
            public bool IsPass1 { get; set; }

            public virtual object Clone() => MemberwiseClone();
        }

        protected abstract class PythonDecoderFallbackBuffer : DecoderFallbackBuffer {
            private readonly char _marker;
            private readonly Queue<char> _fallbackChars;

            private int _fbkCnt;
            private int _charNum;
            private int _charCnt;

            public PythonDecoderFallbackBuffer(bool isPass1, int encodingCharWidth)
            {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _fallbackChars = isPass1 ? null : new Queue<char>();
                this.EncodingCharWidth = encodingCharWidth;
            }

            protected int EncodingCharWidth { get; }

            public abstract char[] GetFallbackChars(byte[] bytesUnknown, int index);

            public override bool Fallback(byte[] bytesUnknown, int index) {
                char[] newFallbackChars = GetFallbackChars(bytesUnknown, index);
                _charNum = newFallbackChars.Length;

                if (_fallbackChars != null) {
                    for (int i = 0; i < _charNum; i++) {
                        _fallbackChars.Enqueue(newFallbackChars[i]);
                    }
                }
                _fbkCnt += _charNum;
                _charCnt = _charNum;

                return true;
            }

            public override int Remaining => _charCnt;

            public override char GetNextChar() {
                if (_charCnt <= 0) return char.MinValue;

                _charCnt--;
                return _marker; // unfortunately, returning the actual fallback char here might result in an exception
            }

            public override bool MovePrevious() {
                if (_charCnt >= _charNum) return false;

                _charCnt++;
                return true;
            }

            // not called for pass1 decoding
            public char GetFallbackChar() {
                _fbkCnt--;
                return _fallbackChars.Dequeue();
            }

            public bool IsEmpty => (_fallbackChars?.Count ?? 0) == 0;

            public int FallbackCharCount => _fbkCnt;

            public override void Reset() {
                _fallbackChars?.Clear();
                _fbkCnt = 0;
                _charNum = _charCnt = 0;
            }
        }

    }

    internal class PythonSurrogateEscapeEncoding : PythonEncoding {
        // Defined in PEP 383
        private const int LoneSurrogateBase = 0xdc00;

        public PythonSurrogateEscapeEncoding(Encoding encoding)
            : base(encoding, new SurrogateEscapeEncoderFallback(), new SurrogateEscapeDecoderFallback()) { }

        public class SurrogateEscapeEncoderFallback : PythonEncoderFallback {
            public override int MaxCharCount => 1;

            public override EncoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeEncoderFallbackBuffer(this.IsPass1, this.Encoding.CharacterWidth);
        }

        private class SurrogateEscapeEncoderFallbackBuffer : PythonEncoderFallbackBuffer {
            public SurrogateEscapeEncoderFallbackBuffer(bool isPass1, int encodingCharWidth)
                : base(isPass1, encodingCharWidth) { }

            public override byte[] GetFallbackBytes(char charUnknown, int index) {
                if ((charUnknown & ~0xff) != LoneSurrogateBase) {
                    // unfortunately, EncoderFallbackException(string, char, int) is not accessible here
                    // TODO: use reflection to access it
                    throw new EncoderFallbackException(
                        $"'surrogateescape' error handler can't encode character '{charUnknown}' in position {index}: value not in range(0xdc00, 0xdd00)"
                    );
                }

                return new[] { (byte)(charUnknown & 0xff) };
            }

        }

        public class SurrogateEscapeDecoderFallback : PythonDecoderFallback {

            public override int MaxCharCount => this.Encoding.CharacterWidth;

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeDecoderFallbackBuffer(this.IsPass1, this.Encoding.CharacterWidth);
        }

        private class SurrogateEscapeDecoderFallbackBuffer : PythonDecoderFallbackBuffer {

            public SurrogateEscapeDecoderFallbackBuffer(bool isPass1, int encodingCharWidth)
                : base(isPass1, encodingCharWidth) { }

            public override char[] GetFallbackChars(byte[] bytesUnknown, int index) {
                int charNum = bytesUnknown.Length;
                char[] fallbackChars = new char[charNum];

                for (int i = 0; i < charNum; i++) {
                    if (this.EncodingCharWidth == 1) {
                        // test for value below 128
                        if (bytesUnknown[i] < 128u) {
                            throw new DecoderFallbackException(
                                $"Character '\\x{bytesUnknown[i]:X2}' in position {index + i}: values below 128 cannot be smuggled (PEP 383)",
                                bytesUnknown,
                                index
                            );
                        }
                    }
                    // no test for "else" case because all supported wide char encodings (UTF-16LE, UTF-16BE, UTF-32LE, UTF-32BE)
                    // will never fall back for values under 128

                    fallbackChars[i] = (char)(bytesUnknown[i] | LoneSurrogateBase);
                }

                return fallbackChars;
            }

        }
    }
}
