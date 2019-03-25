// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using IronPython.Runtime.Operations;
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

        public override string EncodingName => Pass1Encoding.EncodingName;

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

        public bool HasBugCorefx29898 {
            get {
                if (_hasBugCorefx29898 == null) {
                    try {
                        var codec = (Encoding)new UTF8Encoding(false, throwOnInvalidBytes: true);
                        codec.GetCharCount(new byte[] { 255 });
                        _hasBugCorefx29898 = false;
                    } catch (DecoderFallbackException ex) {
                        _hasBugCorefx29898 = (ex.Index < 0);
                    }
                }
                return (bool)_hasBugCorefx29898;
            }
        }
        private bool? _hasBugCorefx29898;

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
                int numBytes;

                var fbuf1 = _pass1encoder.FallbackBuffer as PythonEncoderFallbackBuffer;
                if (fbuf1 != null) fbuf1.ByteCountingMode = true;
                try {
                    numBytes = _pass1encoder.GetByteCount(chars, index, count, flush);
                    fbuf1?.ThrowIfNotEmpty(count, flush);
                } finally {
                    if (fbuf1 != null) fbuf1.ByteCountingMode = false;
                }
                return numBytes;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush) {
                var fbuf1 = _pass1encoder.FallbackBuffer as PythonEncoderFallbackBuffer;
                var fbuf2 = _pass2encoder?.FallbackBuffer as PythonEncoderFallbackBuffer;
                int? fbkIdxStart = fbuf1?.FallbackByteCount;

                int written = _pass1encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush);

                // If the final increment and there were no fallback bytes, the job is done
                if (fbuf1 == null || flush && fbuf1.FallbackByteCount == fbkIdxStart && fbuf1.IsEmpty && (fbuf2?.IsEmpty ?? true)) {
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

                // Check if all fallback bytes are restored properly
                fbuf1.ThrowIfNotEmpty(charCount, flush);
                fbuf2.ThrowIfNotEmpty(charCount, flush);

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

            // for error reporting
            private char _lastCharUnknown;
            private int _lastIndexUnknown = -1;

            public PythonEncoderFallbackBuffer(bool isPass1, PythonEncoding encoding) {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _fallbackBytes = isPass1 ? null : new Queue<byte>();
                this.EncodingCharWidth = encoding.CharacterWidth;
                this.CodePage = encoding.CodePage;
            }

            protected int EncodingCharWidth { get; }
            protected int CodePage { get; }

            public abstract byte[] GetFallbackBytes(char charUnknown, int index);

            public override bool Fallback(char charUnknown, int index) {
                // The design limitation fow wide-char encodings is that
                // fallback bytes must be char-aligned.
                if (_byteCnt.Value % EncodingCharWidth != 0) {
                    // bytes are not char-aligned, the fallback chars must be consecutive
                    if (index != _lastIndexUnknown + 1) {
                        throw PythonOps.UnicodeEncodeError($"incomplete input sequence at index: {_lastIndexUnknown}", _lastCharUnknown, _lastIndexUnknown);
                    }
                }
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
                _lastCharUnknown = charUnknown;
                _lastIndexUnknown = index;
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

            public virtual bool IsEmpty => (_fallbackBytes?.Count ?? 0) == 0;

            public int FallbackByteCount => _fbkCnt;

            public virtual bool ByteCountingMode {
                get { return _byteCnt.ByteCountingMode; }
                set { _byteCnt.ByteCountingMode = value; }
            }

            public virtual void ThrowIfNotEmpty(int endIndex, bool flush) {
                if (flush && !IsEmpty || _byteCnt.Value % EncodingCharWidth != 0 && endIndex != _lastIndexUnknown + 1) {
                    throw PythonOps.UnicodeEncodeError($"incomplete input sequence at index {_lastIndexUnknown}", _lastCharUnknown, _lastIndexUnknown);
                }
                if (ByteCountingMode) {
                    // This increment has successfully been counted.
                    // Therefore there will be no errors during translation.
                    _lastIndexUnknown = -1;
                } else {
                    _lastIndexUnknown -= endIndex; // prep. for next incremental encoding step
                }
            }

            public override void Reset() {
                _fallbackBytes?.Clear();
                _fbkCnt = 0;
                _byteCnt.Reset();
                _lastCharUnknown = '\0';
                _lastIndexUnknown = -1;
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

            public override int GetCharCount(byte[] bytes, int index, int count, bool flush) {
                int numChars;

                var fbuf1 = _pass1decoder.FallbackBuffer as PythonDecoderFallbackBuffer;
                if (fbuf1 != null) fbuf1.CharCountingMode = true;
                try {
                    numChars = _pass1decoder.GetCharCount(bytes, index, count, flush);
                    fbuf1?.ThrowIfNotEmpty(count, flush);
                } finally {
                    if (fbuf1 != null) fbuf1.CharCountingMode = false;
                }
                return numChars;
            }


            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
                => GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, bool flush) {
                var fbuf1 = _pass1decoder.FallbackBuffer as PythonDecoderFallbackBuffer;
                var fbuf2 = _pass2decoder?.FallbackBuffer as PythonDecoderFallbackBuffer;
                int? surIdxStart = fbuf1?.FallbackCharCount;

                int written = _pass1decoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush);

                // If the final increment and there were no fallback characters, the job is done
                if (fbuf1 == null || flush && fbuf1.FallbackCharCount == surIdxStart && fbuf1.IsEmpty && (fbuf2?.IsEmpty ?? true)) {
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

                // Check if all fallback chars are restored properly
                fbuf1.ThrowIfNotEmpty(byteCount, flush);
                fbuf2.ThrowIfNotEmpty(byteCount, flush);

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
            private readonly bool _hasIndexBug;

            private int _fbkCnt;
            private int _charNum;
            private int _charCnt;

            public PythonDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding) {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _fallbackChars = isPass1 ? null : new Queue<char>();
                this.EncodingCharWidth = encoding.CharacterWidth;
                this.CodePage = encoding.CodePage;
                _hasIndexBug = encoding.HasBugCorefx29898;
            }

            protected int EncodingCharWidth { get; }
            protected int CodePage { get; }

            public abstract char[] GetFallbackChars(byte[] bytesUnknown, int index);

            public override bool Fallback(byte[] bytesUnknown, int index) {
                if (_hasIndexBug && this.CharCountingMode && this.CodePage == 65001) { // only for UTF-8
                    index += bytesUnknown.Length;
                }
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

            public virtual bool IsEmpty => (_fallbackChars?.Count ?? 0) == 0;

            public int FallbackCharCount => _fbkCnt;

            public virtual bool CharCountingMode { get; set; }

            public virtual void ThrowIfNotEmpty(int endIndex, bool flush) {
                if (flush && !IsEmpty) {
                    // If this exception is being thrown, the problem is with the code, not the input sequence.
                    // Therefore, the exception does not carry any input data.
                    throw new DecoderFallbackException("decoding failure");
                }
            }

            public override void Reset() {
                _fallbackChars?.Clear();
                _fbkCnt = 0;
                _charNum = _charCnt = 0;
            }
        }

    }

    internal class PythonSurrogateEscapeEncoding : PythonEncoding {
        // Defined in PEP 383
        private const ushort LoneSurrogateBase = 0xdc00;

        public PythonSurrogateEscapeEncoding(Encoding encoding)
            : base(encoding, new SurrogateEscapeEncoderFallback(), new SurrogateEscapeDecoderFallback()) { }

        public class SurrogateEscapeEncoderFallback : PythonEncoderFallback {
            public override int MaxCharCount => 1;

            public override EncoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeEncoderFallbackBuffer(this.IsPass1, this.Encoding);
        }

        private class SurrogateEscapeEncoderFallbackBuffer : PythonEncoderFallbackBuffer {
            public SurrogateEscapeEncoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                : base(isPass1, encoding) { }

            public override byte[] GetFallbackBytes(char charUnknown, int index) {
                if ((charUnknown & ~0xff) != LoneSurrogateBase) {
                    // EncoderFallbackException(string, char, int) is not accessible here
                    throw PythonOps.UnicodeEncodeError(
                        $"'surrogateescape' error handler can't encode character '{charUnknown}' at index {index}: value not in range(0xdc00, 0xdd00)",
                        charUnknown,
                        index
                    );
                }

                return new[] { (byte)(charUnknown & 0xff) };
            }

        }

        public class SurrogateEscapeDecoderFallback : PythonDecoderFallback {

            public override int MaxCharCount => this.Encoding.CharacterWidth;

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeDecoderFallbackBuffer(this.IsPass1, this.Encoding);
        }

        private class SurrogateEscapeDecoderFallbackBuffer : PythonDecoderFallbackBuffer {

            public SurrogateEscapeDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                : base(isPass1, encoding) { }

            public override char[] GetFallbackChars(byte[] bytesUnknown, int index) {
                int charNum = bytesUnknown.Length;
                char[] fallbackChars = new char[charNum];

                for (int i = 0; i < charNum; i++) {
                    if (this.EncodingCharWidth == 1) {
                        // test for value below 128
                        if (bytesUnknown[i] < 128u) {
                            throw new DecoderFallbackException(
                                $"Character '\\x{bytesUnknown[i]:X2}' at index {index + i}: values below 128 cannot be smuggled (PEP 383)",
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

    internal class PythonSurrogatePassEncoding : PythonEncoding {
        private const ushort SurrogateRangeStart = 0xd800;
        private const ushort SurrogateRangeEnd = 0xdfff;
        private const byte Utf8LeadByte = 0b_1110_0000;
        private const byte Utf8LeadBytePayload = 0b_1111;
        private const byte Utf8ContByte = 0b_10_000000;
        private const byte Utf8ContBytePayload = 0b_111111;

        public PythonSurrogatePassEncoding(Encoding encoding)
            : base(encoding, new SurrogatePassEncoderFallback(), new SurrogatePassDecoderFallback()) { }

        public class SurrogatePassEncoderFallback : PythonEncoderFallback {
            public override int MaxCharCount => 1;

            public override EncoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogatePassEncoderFallbackBuffer(this.IsPass1, this.Encoding);
        }

        private class SurrogatePassEncoderFallbackBuffer : PythonEncoderFallbackBuffer {
            private readonly int _codePage;
            private readonly bool _isBigEndianEncoding;

            public SurrogatePassEncoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                : base(isPass1, encoding) {
                _codePage = encoding.CodePage;
                _isBigEndianEncoding = encoding.IsBigEndian;
            }

            public override byte[] GetFallbackBytes(char charUnknown, int index) {
                if (charUnknown < SurrogateRangeStart || SurrogateRangeEnd < charUnknown) {
                    // EncoderFallbackException(string, char, int) is not accessible here
                    throw PythonOps.UnicodeEncodeError(
                        $"'surrogatepass' error handler can't encode character '{charUnknown}' at index {index}: value not in range(0x{SurrogateRangeStart:x4}, 0x{SurrogateRangeEnd + 1:x4})",
                        charUnknown,
                        index
                    );
                }

                byte[] fallbackBytes;
                if (this.EncodingCharWidth > 1) {
                    fallbackBytes = BitConverter.GetBytes(charUnknown);
                    if (fallbackBytes.Length == this.EncodingCharWidth) {
                        // UTF-16LE or UTF-16BE
                        if (BitConverter.IsLittleEndian == _isBigEndianEncoding) {
                            // swap bytes for non-native endianness encoding
                            //(fallbackBytes[0], fallbackBytes[1]) = (fallbackBytes[1], fallbackBytes[0]);
                            // the above requires .NET Core 2.x, .NET Standard 2.0 or .NET Framework 4.7
                            // For .NET 4.5 use: <PackageReference Include="System.ValueTuple" Version="4.4.0" />
                            var temp = fallbackBytes[0];
                            fallbackBytes[0] = fallbackBytes[1];
                            fallbackBytes[1] = temp;
                        }
                    } else {
                        // UTF-32LE or UTF-32BE
                        byte[] paddedBytes = new byte[this.EncodingCharWidth];

                        if (!BitConverter.IsLittleEndian) {
                            Array.Reverse(fallbackBytes); // to little endian
                        }

                        Array.Copy(fallbackBytes, 0, paddedBytes, 0, fallbackBytes.Length);
                        fallbackBytes = paddedBytes;

                        if (_isBigEndianEncoding) {
                            Array.Reverse(fallbackBytes);
                        }
                    }
                } else if (_codePage == 65001) {
                    // UTF-8
                    fallbackBytes = new byte[3]; // UTF-8 for range U+0800 to U+FFFF
                    ushort codepoint = charUnknown;
                    fallbackBytes[0] = (byte)(Utf8LeadByte | codepoint >> 12);
                    codepoint &= 0b_111111_111111;
                    fallbackBytes[1] = (byte)(Utf8ContByte | codepoint >> 6);
                    codepoint &= 0b_111111;
                    fallbackBytes[2] = (byte)(Utf8ContByte | codepoint);
                } else {
                    throw PythonOps.UnicodeEncodeError($"'surrogatepass' error handler does not support this encoding (cp{_codePage})", charUnknown, index);
                }

                return fallbackBytes;
            }

        }

        public class SurrogatePassDecoderFallback : PythonDecoderFallback {

            public override int MaxCharCount => this.Encoding.CharacterWidth;

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogatePassDecoderFallbackBuffer(this.IsPass1, this.Encoding);
        }

        private class SurrogatePassDecoderFallbackBuffer : PythonDecoderFallbackBuffer {

            private class ByteBuffer {
                private byte[] _buffer;
                private int _buflen;
                private int _bufidx;

                private byte[] _savebuf;
                private int _savelen;
                private int _saveidx;

                public ByteBuffer(int size) {
                    _buffer = new byte[size];
                }

                public byte[] Bytes => _buffer;
                public int Length => _buflen;
                public int Index { get => _bufidx; set { _bufidx = value; } }
                public int EndIndex => _bufidx + _buflen;

                public void AddByte(byte b) {
                    _buffer[_buflen++] = b;
                }

                public void Flush() {
                    _bufidx += _buflen;
                    _buflen = 0;
                }

                public byte[] TrimmedBytes() {
                    var copy = new byte[_buflen];
                    Array.Copy(_buffer, 0, copy, 0, _buflen);
                    return copy;
                }

                public void Save() {
                    if (_savebuf != null) {
                        Array.Copy(_buffer, _savebuf, _buflen);
                    } else {
                        _savebuf = (byte[])_buffer.Clone();
                    }
                    _savelen = _buflen;
                    _saveidx = _bufidx;
                }

                public void Restore() {
                    if (_savebuf != null) {
                        Array.Copy(_savebuf, _buffer, _savelen);
                    }
                    _buflen = _savelen;
                    _bufidx = _saveidx;
                }
            }

            private ByteBuffer _buffer;

            public SurrogatePassDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                : base(isPass1, encoding) {
            }

            public override char[] GetFallbackChars(byte[] bytesUnknown, int index) {
                const int bytesPerChar = 3; // UTF-8 uses 3 bytes to encode a surrogate
                int numBytes = bytesUnknown.Length;
                char fallbackChar = char.MinValue;
                char[] fallbackChars = null;
                int numFallbackChars = 0;

                switch (this.CodePage) {
                    case 65001: // UTF-8
                        if (_buffer == null) _buffer = new ByteBuffer(bytesPerChar);
                        if (index != _buffer.EndIndex) {
                            // new fallback sequence
                            if (_buffer.Length != 0) {
                                // leftover bytes not consumed
                                Throw(_buffer.TrimmedBytes(), _buffer.Index);
                            }
                            _buffer.Index = index;
                        }
                        fallbackChars = new char[(_buffer.Length + numBytes) / bytesPerChar];
                        for (int i = 0; i < numBytes; i++) {
                            _buffer.AddByte(bytesUnknown[i]);
                            if (_buffer.Length == bytesPerChar) {
                                byte[] bytes = _buffer.Bytes;
                                if ((bytes[0] & ~Utf8LeadBytePayload) != Utf8LeadByte
                                 || (bytes[1] & ~Utf8ContBytePayload) != Utf8ContByte
                                 || (bytes[2] & ~Utf8ContBytePayload) != Utf8ContByte) {
                                    Throw(bytes, _buffer.Index);
                                }

                                int fallbackValue = bytes[0] & Utf8LeadBytePayload;
                                fallbackValue = (bytes[1] & Utf8ContBytePayload) | (fallbackValue << 6);
                                fallbackValue = (bytes[2] & Utf8ContBytePayload) | (fallbackValue << 6);

                                if (fallbackValue < SurrogateRangeStart || SurrogateRangeEnd < fallbackValue) {
                                    Throw(bytes, _buffer.Index);
                                }

                                fallbackChars[numFallbackChars++] = (char)fallbackValue;
                                _buffer.Flush();
                            }
                        }
                        break;

                    case 1200: // UTF-16LE
                        if (numBytes != 2) break;
                        fallbackChar = (char)(bytesUnknown[0] | (bytesUnknown[1] << 8));
                        break;

                    case 1201: // UTF-16BE
                        if (numBytes != 2) break;
                        fallbackChar = (char)(bytesUnknown[1] | (bytesUnknown[0] << 8));
                        break;

                    case 12000: // UTF-32LE
                        if (numBytes != 4) break;
                        if (bytesUnknown[2] != 0 || bytesUnknown[3] != 0) break;
                        fallbackChar = (char)(bytesUnknown[0] | (bytesUnknown[1] << 8));
                        break;

                    case 12001: // UTF-32BE
                        if (numBytes != 4) break;
                        if (bytesUnknown[1] != 0 || bytesUnknown[0] != 0) break;
                        fallbackChar = (char)(bytesUnknown[3] | (bytesUnknown[2] << 8));
                        break;

                    default:
                        throw new DecoderFallbackException($"'surrogatepass' error handler does not support this encoding (cp{this.CodePage})", bytesUnknown, index);
                }

                if (fallbackChars == null) {
                    if (fallbackChar < SurrogateRangeStart || SurrogateRangeEnd < fallbackChar) {
                        Throw(bytesUnknown, index);
                    }
                    fallbackChars = new[] { fallbackChar };
                }
                return fallbackChars;
            }

            public override bool IsEmpty => base.IsEmpty && (_buffer?.Length ?? 0) == 0;

            public override bool CharCountingMode {
                get => base.CharCountingMode;
                set {
                    base.CharCountingMode = value;
                    if (value) {
                        _buffer?.Save();
                    } else {
                        _buffer?.Restore();
                    }
                }
            }

            public override void ThrowIfNotEmpty(int endIndex, bool flush) {
                if (_buffer != null) {
                    if (_buffer.Length != 0 && (_buffer.EndIndex != endIndex || flush)) {
                        // leftover bytes not consumed
                        Throw(_buffer.TrimmedBytes(), _buffer.Index);
                    }
                    // Prepare for next incremental decode step (if any)
                    _buffer.Index = -_buffer.Length;
                }
                base.ThrowIfNotEmpty(endIndex, flush);
            }

            public override void Reset() {
                base.Reset();
                _buffer = null;
            }

            // Method like this belongs to PythonOps
            protected void Throw(byte[] bytesUnknown, int index) {
                // Create a string representation of our bytes.
                const int maxNumBytes = 20;

                StringBuilder strBytes = new StringBuilder(Math.Min(bytesUnknown.Length, maxNumBytes + 1) * 4);

                int i;
                for (i = 0; i < bytesUnknown.Length && i < maxNumBytes; i++) {
                    strBytes.Append("[");
                    strBytes.Append(bytesUnknown[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                    strBytes.Append("]");
                }

                // In case the string's really long...
                if (i == maxNumBytes) strBytes.Append(" ...");

                throw new DecoderFallbackException($"'surrogatepass' error handler can't decode bytes {strBytes} at index {index}: not a surrogate character", bytesUnknown, index);
            }
        }
    }
}
