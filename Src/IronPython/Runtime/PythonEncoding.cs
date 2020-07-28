// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <br/>
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

        private readonly string _name;
        private Decoder _residentDecoder;
        private Encoder _residentEncoder;

        public PythonEncoding(Encoding encoding, PythonEncoderFallback encoderFallback, PythonDecoderFallback decoderFallback, string name)
            : base(0, encoderFallback, decoderFallback) {

            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            if (encoderFallback == null) throw new ArgumentNullException(nameof(encoderFallback));
            if (decoderFallback == null) throw new ArgumentNullException(nameof(decoderFallback));

            _name = name ?? "<unknown>";

            // TODO: make lazy
            try {
                byte[] markerBytes = encoding.GetBytes(new[] { Pass1Marker });
                CharacterWidth = markerBytes.Length;
                IsBigEndian = markerBytes[0] == 0;
            } catch (EncoderFallbackException) {
                // Q: What encoding cannot encode '?' A: Incomplete charmap.
                CharacterWidth = 1;
                IsBigEndian = false;
            }

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

        public string PythonEncodingName => _name;
        public override string EncodingName => Pass1Encoding.EncodingName ?? _name;

        public override string HeaderName => Pass1Encoding.HeaderName;
        public override string BodyName => Pass1Encoding.BodyName;
        public override string WebName => Pass1Encoding.WebName;
        public override bool IsBrowserDisplay => false;
        public override bool IsBrowserSave => false;
        public override bool IsMailNewsDisplay => false;
        public override bool IsMailNewsSave => false;

        public override bool IsSingleByte => Pass1Encoding.IsSingleByte;

        public override int GetHashCode() => Pass1Encoding.GetHashCode();
        public override byte[] GetPreamble() => Pass1Encoding.GetPreamble();
        public override bool IsAlwaysNormalized(NormalizationForm form) => false;

        public static bool HasBugCorefx29898 {
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
        private static bool? _hasBugCorefx29898;

        private class ProxyEncoder : Encoder {
            private Encoding _encoding;
            private readonly Encoder _encoder;

            public ProxyEncoder(Encoding encoding) {
                _encoding = (Encoding)encoding.Clone();
                _encoding.EncoderFallback = new ProxyEncoderFallback(_encoding.EncoderFallback.CreateFallbackBuffer(), _encoding.EncoderFallback.MaxCharCount);
                _encoder = _encoding.GetEncoder();
                Fallback = _encoder.Fallback = _encoding.EncoderFallback;
            }

            public override int GetByteCount(char[] chars, int index, int count, bool flush)
                => _encoder.GetByteCount(chars, index, count, flush);

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
                => _encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush);

            public override void Reset() => _encoder.Reset();

            private class ProxyEncoderFallback : EncoderFallback {
                private readonly EncoderFallbackBuffer _buffer;
                private readonly int _maxCharCount;

                public ProxyEncoderFallback(EncoderFallbackBuffer buffer, int maxCharCount) {
                    _buffer = buffer;
                    _maxCharCount = maxCharCount;
                }

                public override EncoderFallbackBuffer CreateFallbackBuffer() => _buffer;
                public override int MaxCharCount => _maxCharCount;
            }
        }

        public class PythonEncoder : Encoder {
            private readonly PythonEncoding _parentEncoding;
            private readonly Encoder _pass1encoder;
            private Encoder _pass2encoder;

            public PythonEncoder(PythonEncoding parentEncoding) {
                _parentEncoding = parentEncoding;
                _pass1encoder = parentEncoding.Pass1Encoding.GetEncoder();

                if (!(_pass1encoder.Fallback is PythonEncoderFallback) && _parentEncoding.Pass1Encoding.EncoderFallback is PythonEncoderFallback) {
                    // Non-conformant Encoder implementation, the challenge is to get to the fallback buffer used by such encoder.

                    // Possibility 1: _pass1encoder is EncoderNLS .
                    // This weirdo (.NET Core only) doe not use Falback and FallbackBuffer properties from its Encoder base class;
                    // it redefines them as new properties and uses them instead.
                    // Although the new FallbackBuffer is public, it is not easilly accessible because the EncoderNLS class is internal.
                    // One way of accessing it is by reflection. This will be handled by GetPythonEncoderFallbackBuffer()
                    if (_pass1encoder.GetType().FullName == "System.Text.EncoderNLS") return;

                    // Possibility 2: _pass1encoder is DefaultEncoder or another stateless encoder;
                    // This makes sense only if the encoding process of the given encoding is stateless too.
                    // This should not be common: because .NET strings are UTF-16, it is practically impossible to have a universally-applicable stateless encoding.
                    // However, such encoding may still be useful in some specifc cases, like non-incremental encoding
                    // or if the input is guaranteed to never contain surrogate pairs.
                    // We use ProxyEncoder to access EncoderFallbackBuffer used by such stateless encoder.
                    _pass1encoder = new ProxyEncoder(parentEncoding.Pass1Encoding);

                    // Possibility 3: Some 3rd party non-compliant encoder. Too bad...
                }
            }

            private static PythonEncoderFallbackBuffer GetPythonEncoderFallbackBuffer(Encoder enc) {
                if (enc == null) return null;

                // This should be as simple as enc.FallbackBuffer as PythonEncoderFallbackBuffer
                // but it requires a workaround for a design oddity in System.Text.EncoderNLS on .NET Core
                var fbuf = enc.FallbackBuffer as PythonEncoderFallbackBuffer;
#if NETCOREAPP
                if (fbuf == null) {
                    fbuf = enc.GetType().GetProperty(nameof(enc.FallbackBuffer)).GetValue(enc) as PythonEncoderFallbackBuffer;
                }
#endif
                return fbuf;
            }

            public override int GetByteCount(char[] chars, int index, int count, bool flush) {
                var fbuf1 = GetPythonEncoderFallbackBuffer(_pass1encoder);

                fbuf1?.PrepareIncrement(chars, forEncoding: false);
                int numBytes = _pass1encoder.GetByteCount(chars, index, count, flush);
                fbuf1?.FinalizeIncrement(count, flush);

                return numBytes;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush) {
                var fbuf1 = GetPythonEncoderFallbackBuffer(_pass1encoder);
                var fbuf2 = GetPythonEncoderFallbackBuffer(_pass2encoder);
                fbuf1?.PrepareIncrement(chars, forEncoding: true);

                int written = _pass1encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush);

                // If the final increment and there were no more fallback bytes, the job is done
                if (fbuf1 == null || flush && fbuf1.IsEmpty && (fbuf2?.IsEmpty ?? true)) {
                    fbuf1?.Reset();
                    fbuf2?.Reset();
                    return written;
                }

                // Lazy creation of _pass2encoder
                if (_pass2encoder == null) {
                    _pass2encoder = _parentEncoding.Pass2Encoding.GetEncoder();
                    fbuf2 = GetPythonEncoderFallbackBuffer(_pass2encoder);
                    if (fbuf2 == null && _parentEncoding.Pass2Encoding.EncoderFallback is PythonEncoderFallback && _pass2encoder.GetType().FullName != "System.Text.EncoderNLS") {
                        // _pass2encoder must be DefaultEncoder or another stateless encoder
                        _pass2encoder = new ProxyEncoder(_parentEncoding.Pass2Encoding);
                        fbuf2 = (PythonEncoderFallbackBuffer)_pass2encoder.FallbackBuffer;
                    }
                }
                fbuf2.PrepareIncrement(chars, forEncoding: true);

                // Restore original fallback bytes
                var bytes2 = new byte[written];
                _pass2encoder.GetBytes(chars, charIndex, charCount, bytes2, 0, flush);

                int cwidth = _parentEncoding.CharacterWidth;
                for (int i = 0, j = byteIndex; i < written; i++, j++) {
                    if (bytes[j] != bytes2[i]) {
                        int ofs = (j / cwidth) * cwidth;
                        for (int p = 0; p < cwidth; p++) {
                            bytes[ofs++] = fbuf2.GetFallbackByte();
                            fbuf1.GetFallbackByte(); // count the byte as consumed in fbuf1 too
                        }
                        int skip = ofs - j - 1;
                        i += skip;
                        j += skip;
                    }
                }

                // Check if all fallback bytes are restored properly
                fbuf1.FinalizeIncrement(charCount, flush);
                fbuf2.FinalizeIncrement(charCount, flush);

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
            private struct RestorableInt {
                private int _value;
                private int _saved;

                public static implicit operator int(RestorableInt dc) => dc._value;

                /// <summary>Assignment preserving the save</summary>
                public static RestorableInt operator << (RestorableInt dc, int value) {
                    dc._value = value;
                    return dc;
                }

                public static RestorableInt operator + (RestorableInt dc, int value) {
                    dc._value += value;
                    return dc;
                }

                public void Save() => _saved = _value;
                public void Restore() => _value = _saved;
                public void Reset(int value) => _value = _saved = value;
            }

            private readonly char _marker;

            private readonly Queue<byte> _allFallbackBytes;  // collects all fallback bytes for the whole pass, only used during actual encoding, pass 2
            private int _fbkByteCnt; // only used during actual encoding; proxy for _fallbackBytes.Count but valid in pass 1 too
            private RestorableInt _byteCnt; // counts unreported bytes in the buffer from the last fallback; used during both counting and encoding, but counts separately

            private ReadOnlyMemory<char> _fallbackChars;  // fallback chars (if any) from the last fallback
            private int _charCnt; // counts unreported chars in the buffer from the last fallback
            private int _fbkNumChars; // number of all (virtual) chars in the buffer from the last fallback; proxy for _fallbackChars.Length but valid for fallback bytes too

            // for error reporting
            private int _lastRuneUnknown; // in UTF-32; "rune" is an alias for "Unicode codepoint"
            private RestorableInt _lastIndexUnknown;

            public PythonEncoderFallbackBuffer(bool isPass1, PythonEncoding encoding) {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _allFallbackBytes = isPass1 ? null : new Queue<byte>();
                this.EncodingCharWidth = encoding.CharacterWidth;
                this.CodePage = encoding.CodePage;
                _lastIndexUnknown.Reset(-1);
            }

            protected bool EncodingMode { get; private set; }

            protected int EncodingCharWidth { get; }
            protected int CodePage { get; }
            protected char[] Data { get; private set; }

            public virtual void PrepareIncrement(char[] data, bool forEncoding) {
                Data = data;
                if (EncodingMode) {
                    _byteCnt.Save();
                    _lastIndexUnknown.Save();
                } else {
                    _byteCnt.Restore();
                    _lastIndexUnknown.Restore();
                }
                EncodingMode = forEncoding;
            }

            public abstract Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> GetFallbackCharsOrBytes(int runeUnknown, int index);

            public override bool Fallback(char charUnknown, int index)
                => FallbackImpl(charUnknown, index);

            public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index)
                => FallbackImpl(char.ConvertToUtf32(charUnknownHigh, charUnknownLow), index);

            private bool FallbackImpl(int runeUnknown, int index) {
                if (_charCnt > 0) {
                    // There are some unread characters from the previous fallback
                    // InvalidOperationException would be a better choice, but ArgumentException is what .NET fallback buffers throw
                    if (_lastRuneUnknown <= char.MaxValue) {
                        throw new ArgumentException($"Recursive fallback not allowed for character '\\u{_lastRuneUnknown:X4}'");
                    } else {
                        throw new ArgumentException($"Recursive fallback not allowed for character '\\U{_lastRuneUnknown:X8}'");
                    }
                }

                // The design limitation for wide-char encodings is that
                // fallback bytes must be char-aligned (to fill in marker chars)
                if (_byteCnt > 0) {
                    // bytes are not char-aligned yet, so the fallback chars must be consecutive
                    if (index != _lastIndexUnknown + (_lastRuneUnknown > char.MaxValue ? 2 : 1)) {
                        throw PythonOps.UnicodeEncodeError("incomplete input sequence", _lastRuneUnknown, _lastIndexUnknown);
                    }
                }

                var fallbackData = GetFallbackCharsOrBytes(runeUnknown, index);
                var newFallbackChars = fallbackData.Item1;
                var newFallbackBytes = fallbackData.Item2;

                if (newFallbackBytes.IsEmpty) {
                    if (_byteCnt > 0 && !newFallbackChars.IsEmpty) {
                        // bytes are not char-aligned yet, so the fallback should have produced remaining bytes, not chars
                        throw PythonOps.UnicodeEncodeError("incomplete fallback sequence", _lastRuneUnknown, _lastIndexUnknown);
                    }
                    // use fallback chars, may be none
                    _fallbackChars = newFallbackChars;
                    _charCnt = _fbkNumChars = _fallbackChars.Length;
                } else {
                    if (!newFallbackChars.IsEmpty) {
                        throw new NotSupportedException("Encoding error handler may produce either chars or bytes, not both at the same time.");
                    }
                    // use fallback bytes
                    if (EncodingMode) {
                        if (_allFallbackBytes != null) { // pass 2
                            foreach (byte b in newFallbackBytes.Span) {
                                _allFallbackBytes.Enqueue(b);
                            }
                        }
                        _fbkByteCnt += newFallbackBytes.Length;
                    }
                    _fallbackChars = default; // will report _marker instead
                    _byteCnt += newFallbackBytes.Length;
                    _charCnt = _fbkNumChars = _byteCnt / EncodingCharWidth;
                }

                _lastRuneUnknown = runeUnknown;
                _lastIndexUnknown <<= index;
                return _charCnt > 0;
            }

            public override int Remaining => _charCnt;

            public override char GetNextChar() {
                if (_charCnt == 0) return '\0';

                if (_fallbackChars.IsEmpty) {
                    _charCnt--;
                    _byteCnt += -EncodingCharWidth;
                    return _marker;
                } else {
                    return _fallbackChars.Span[_fallbackChars.Length - _charCnt--];
                }
            }

            public override bool MovePrevious() {
                if (_charCnt == _fbkNumChars) return false;

                if (_fallbackChars.IsEmpty) {
                    _byteCnt += EncodingCharWidth;
                }
                _charCnt++;
                return true;
            }

            public byte GetFallbackByte() {
                _fbkByteCnt--;
                return _allFallbackBytes?.Dequeue() ?? 0;
            }

            public virtual bool IsEmpty => _charCnt == 0 && (_fbkByteCnt == 0 || !EncodingMode) && _byteCnt == 0;

            public virtual void FinalizeIncrement(int endIndex, bool flush) {
                if (flush && !IsEmpty || _byteCnt > 0 && endIndex != _lastIndexUnknown + (_lastRuneUnknown > char.MaxValue ? 2 : 1)) {
                    throw PythonOps.UnicodeEncodeError($"incomplete input sequence", _lastRuneUnknown, _lastIndexUnknown);
                }
                Data = null; // release input data for possible collection
                _lastIndexUnknown += -endIndex; // prep. for next incremental encoding step
            }

            public override void Reset() {
                _allFallbackBytes?.Clear();
                _fbkByteCnt = 0;
                _byteCnt.Reset(0);
                _charCnt = _fbkNumChars = 0;
                _fallbackChars = default;
                _lastRuneUnknown = '\0';
                _lastIndexUnknown.Reset(-1);
                Data = null;
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

            private int _fbkCnt;
            private int _charNum;
            private int _charCnt;

            public PythonDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding) {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _fallbackChars = isPass1 ? null : new Queue<char>();
                this.EncodingCharWidth = encoding.CharacterWidth;
                this.CodePage = encoding.CodePage;
            }

            protected int EncodingCharWidth { get; }
            protected int CodePage { get; }

            public abstract char[] GetFallbackChars(byte[] bytesUnknown, int index);

            public override bool Fallback(byte[] bytesUnknown, int index) {
                if (this.CharCountingMode && this.CodePage == 65001 && PythonEncoding.HasBugCorefx29898) { // only for UTF-8
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
                    throw new DecoderFallbackException("internal error");
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

        public PythonSurrogateEscapeEncoding(Encoding encoding, string name = null)
            : base(encoding, new SurrogateEscapeEncoderFallback(), new SurrogateEscapeDecoderFallback(), name ?? encoding.WebName) { }

        public class SurrogateEscapeEncoderFallback : PythonEncoderFallback {
            public override int MaxCharCount => 1;

            public override EncoderFallbackBuffer CreateFallbackBuffer()
                => new SurrogateEscapeEncoderFallbackBuffer(this.IsPass1, this.Encoding);
        }

        private class SurrogateEscapeEncoderFallbackBuffer : PythonEncoderFallbackBuffer {
            public SurrogateEscapeEncoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                : base(isPass1, encoding) { }

            public override Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> GetFallbackCharsOrBytes(int runeUnknown, int index) {
                if ((runeUnknown & ~0xff) != LoneSurrogateBase) {
                    // EncoderFallbackException(string, char, int) is not accessible here
                    throw PythonOps.UnicodeEncodeError(
                        $"'surrogateescape' error handler: value not in range(0x{LoneSurrogateBase:x4}, 0x{LoneSurrogateBase+0x100:x4})",
                        runeUnknown,
                        index
                    );
                }
                byte b = (byte)(runeUnknown & 0xff);
                if (b < 128) {
                    throw PythonOps.UnicodeEncodeError(
                        "'surrogateescape' error handler: bytes below 128 cannot be smuggled (PEP 383)",
                        runeUnknown,
                        index
                    );
                }
                var fallbackBytes = new byte[] { b };
                return new Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>>(default, fallbackBytes.AsMemory());
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
                    if (bytesUnknown[i] < 128) {
                        throw new DecoderFallbackException(
                            "'surrogateescape' error handler: bytes below 128 cannot be smuggled (PEP 383)",
                            bytesUnknown,
                            index
                        );
                    }

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

        public PythonSurrogatePassEncoding(Encoding encoding, string name = null)
            : base(encoding, new SurrogatePassEncoderFallback(), new SurrogatePassDecoderFallback(), name ?? encoding.WebName) { }

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

            public override Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> GetFallbackCharsOrBytes(int runeUnknown, int index) {
                if (runeUnknown < SurrogateRangeStart || SurrogateRangeEnd < runeUnknown) {
                    // EncoderFallbackException(string, char, int) is not accessible here
                    throw PythonOps.UnicodeEncodeError(
                        $"'surrogatepass' error handler: value not in range(0x{SurrogateRangeStart:x4}, 0x{SurrogateRangeEnd + 1:x4})",
                        runeUnknown,
                        index
                    );
                }

                byte[] fallbackBytes;
                if (this.EncodingCharWidth > 1) {
                    fallbackBytes = BitConverter.GetBytes((ushort)runeUnknown);
                    if (fallbackBytes.Length == this.EncodingCharWidth) {
                        // UTF-16LE or UTF-16BE
                        if (BitConverter.IsLittleEndian == _isBigEndianEncoding) {
                            // swap bytes for non-native endianness encoding
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
                    fallbackBytes[0] = (byte)(Utf8LeadByte | runeUnknown >> 12);
                    runeUnknown &= 0b_111111_111111;
                    fallbackBytes[1] = (byte)(Utf8ContByte | runeUnknown >> 6);
                    runeUnknown &= 0b_111111;
                    fallbackBytes[2] = (byte)(Utf8ContByte | runeUnknown);
                } else {
                    throw PythonOps.UnicodeEncodeError($"'surrogatepass' error handler does not support this encoding (cp{_codePage})", runeUnknown, index);
                }

                return new Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>>(default, fallbackBytes.AsMemory());
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

            protected void Throw(byte[] bytesUnknown, int index) 
                => throw new DecoderFallbackException($"'surrogatepass' error handler: not a surrogate character", bytesUnknown, index);
        }
    }

    internal class PythonErrorHandlerEncoding : PythonEncoding {
        private readonly CodeContext _context;
        private readonly string _errors;

        public PythonErrorHandlerEncoding(CodeContext context, Encoding encoding, string name, string errors)
            : base(encoding, new PythonHandlerEncoderFallback(), new PythonHandlerDecoderFallback(), name) {
            _context = context;
            _errors = errors;
        }

        private class PythonHandlerEncoderFallback : PythonEncoderFallback {
            public override int MaxCharCount => int.MaxValue;

            public override EncoderFallbackBuffer CreateFallbackBuffer() {
                return new PythonHandlerEncoderFallbackBuffer(this.IsPass1, (PythonErrorHandlerEncoding)this.Encoding);
            }
        }

        private class PythonHandlerEncoderFallbackBuffer : PythonEncoderFallbackBuffer {
            private readonly PythonErrorHandlerEncoding _encoding;
            private object _handler;
            private char _lastSeenChar;

            public PythonHandlerEncoderFallbackBuffer(bool isPass1, PythonErrorHandlerEncoding encoding)
                : base(isPass1, encoding) {
                _encoding = encoding;
            }

            public override void PrepareIncrement(char[] data, bool forEncoding) {
                if (Data != null && Data.Length > 0 && EncodingMode) _lastSeenChar <<= Data[0];
                base.PrepareIncrement(data, forEncoding);
            }

            public override Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> GetFallbackCharsOrBytes(int runeUnknown, int index) {
                if (_handler == null) {
                    _handler = LightExceptions.CheckAndThrow(PythonOps.LookupEncodingError(_encoding._context, _encoding._errors));
                }

                // create the exception object to hand to the user-function...
                int runeLen = runeUnknown > char.MaxValue ? 2 : 1;
                char[] data = Data;
                if (index < 0) {
                    // corner case, the unknown data starts at the end of the previous increment
                    Debug.Assert(index == -1); // only one char back allowed
                    data = new char[Data.Length + 1];
                    data[0] = _lastSeenChar;
                    Array.Copy(Data, 0, data, 1, Data.Length);
                    index++;
                }
                var exObj = PythonExceptions.CreatePythonThrowable(
                    PythonExceptions.UnicodeEncodeError,
                    StringOps.GetEncodingName(_encoding, normalize: false),
                    new string(data),
                    index,
                    index + runeLen,
                    "ordinal not in range for this codec");

                // call the user function...
                object res = PythonCalls.Call(_handler, exObj);

                return ExtractEncodingReplacement(res, index + runeLen);
            }

            private static Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> ExtractEncodingReplacement(object res, int cursorPos) {
                // verify the result is sane...
                if (res is PythonTuple tres && tres.__len__() == 2 && Converter.TryConvertToInt32(tres[1], out int newPos)) {

                    if (newPos != cursorPos) throw new NotImplementedException($"Moving encoding cursor not implemented yet");

                    if (Converter.TryConvertToString(tres[0], out string str)) {
                        return new Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>>(str.AsMemory(), default);
                    } else if (tres[0] is Bytes bytes) {
                        return new Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>>(default, bytes.AsMemory());
                    }
                }

                throw PythonOps.TypeError("encoding error handler must return (str/bytes, int) tuple");
            }
        }

        private class PythonHandlerDecoderFallback : PythonDecoderFallback {

            public override int MaxCharCount => int.MaxValue;

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => new PythonHandlerDecoderFallbackBuffer(this.IsPass1, this.Encoding);

            private class PythonHandlerDecoderFallbackBuffer : PythonDecoderFallbackBuffer {

                public PythonHandlerDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                    : base(isPass1, encoding) { }

                public override char[] GetFallbackChars(byte[] bytesUnknown, int index) {
                    throw new NotImplementedException();
                }

            }
        }

    }
}
