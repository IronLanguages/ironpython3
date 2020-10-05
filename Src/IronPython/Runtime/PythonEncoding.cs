// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

        private PythonEncoder? _residentEncoder;
        private PythonDecoder? _residentDecoder;

        public PythonEncoding(Encoding encoding, PythonEncoderFallback encoderFallback, PythonDecoderFallback decoderFallback)
            : base(0, encoderFallback, decoderFallback) {

            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            if (encoderFallback == null) throw new ArgumentNullException(nameof(encoderFallback));
            if (decoderFallback == null) throw new ArgumentNullException(nameof(decoderFallback));

            try {
                unsafe {
                    char* markerSpan = stackalloc char[] { Pass1Marker };
                    CharacterWidth = encoding.GetByteCount(markerSpan, 1);
                    if (1 <= CharacterWidth && CharacterWidth <= 4) {
                        byte* markerBytes = stackalloc byte[CharacterWidth];
                        encoding.GetBytes(markerSpan, 1, markerBytes, CharacterWidth);
                        IsBigEndian = markerBytes[0] == 0;
                    }
                }
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

        private void PrepareResidentEncoder() {
            if (_residentEncoder == null) {
                _residentEncoder = new PythonEncoder(this);
            } else {
                _residentEncoder.Reset();
            }
        }

        private void PrepareResidentDecoder() {
            if (_residentDecoder == null) {
                _residentDecoder = new PythonDecoder(this);
            } else {
                _residentDecoder.Reset();
            }
        }

        // mandatory override
        public override int GetByteCount(char[] chars, int index, int count) {
            PrepareResidentEncoder();
            return _residentEncoder!.GetByteCount(chars, index, count, flush: true);
        }

        // NLS workhorse
        public override unsafe int GetByteCount(char* chars, int count) {
            PrepareResidentEncoder();
            return _residentEncoder!.GetByteCount(chars, count, flush: true);
        }

        // used by IronPython
        public override int GetByteCount(string s) {
            PrepareResidentEncoder();
            return _residentEncoder!.GetByteCount(s, flush: true);
        }

        // mandatory override
        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            PrepareResidentEncoder();
            return _residentEncoder!.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush: true);
        }

        // NLS workhorse
        public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount) {
            PrepareResidentEncoder();
            return _residentEncoder!.GetBytes(chars, charCount, bytes, byteCount, flush: true);
        }

        // used by IronPython
        public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            PrepareResidentEncoder();
            return _residentEncoder!.GetBytes(s, charIndex, charCount, bytes, byteIndex, flush: true);
        }

        // mandatory override
        public override int GetCharCount(byte[] bytes, int index, int count) {
            PrepareResidentDecoder();
            return _residentDecoder!.GetCharCount(bytes, index, count, flush: true);
        }

        // NLS workhorse
        public override unsafe int GetCharCount(byte* bytes, int count) {
            PrepareResidentDecoder();
            return _residentDecoder!.GetCharCount(bytes, count, flush: true);
        }

        // mandatory override
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            PrepareResidentDecoder();
            return _residentDecoder!.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);
        }

        // used by IronPython
        public string GetString(IPythonBuffer input, int index, int count) {
            PrepareResidentDecoder();
            return _residentDecoder!.GetString(input, index, count);
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

        public override string EncodingName => StringOps.GetEncodingName(Pass1Encoding, normalize: false);

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

        internal static int GetUtf16SequenceLength(int rune) => rune > char.MaxValue ? 2 : 1;

        private readonly struct MemInt {
            private readonly int _current;
            private readonly int _initial;

            private MemInt(int current, int initial) {
                _current = current;
                _initial = initial;
            }

            public int Initial => _initial;

            public static implicit operator int(MemInt mi) => mi._current;
            public static implicit operator MemInt(int value) => new MemInt(value, value);

            /// <summary>Assignment preserving the initial value</summary>
            public static MemInt operator <<(MemInt mi, int value) => new MemInt(value, mi._initial);

            /// <summary>Addition preserving the initial value</summary>
            public static MemInt operator +(MemInt mi, int value) => new MemInt(mi._current + value, mi._initial);
        }

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

            public override unsafe int GetByteCount(char* chars, int count, bool flush)
                => _encoder.GetByteCount(chars, count, flush);

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
                => _encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush);

            public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount, bool flush)
                => _encoder.GetBytes(chars, charCount, bytes, byteCount, flush);

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
            private Encoder? _pass2encoder;

            public PythonEncoder(PythonEncoding parentEncoding) {
                _parentEncoding = parentEncoding;
                _pass1encoder = GetEncoder(parentEncoding.Pass1Encoding);
            }

            private static Encoder GetEncoder(Encoding encoding) {
                Encoder encoder = encoding.GetEncoder();

                if (!(encoder.Fallback is PythonEncoderFallback) && encoding.EncoderFallback is PythonEncoderFallback) {
                    // Non-conformant Encoder implementation, the challenge is to get to the fallback buffer used by such encoder.

                    // Possibility 1: _pass1encoder is EncoderNLS (or its subclass).
                    // This weirdo (.NET Core only) does not use Fallback and FallbackBuffer properties from its Encoder base class;
                    // it redefines them as new properties and uses them instead.
                    // Although the new FallbackBuffer is public, it is not easilly accessible because the EncoderNLS class is internal.
                    // One way of accessing it is by reflection. This will be handled by GetPythonEncoderFallbackBuffer()
                    for (Type? et = encoder.GetType(); et != null && et.FullName != "System.Text.Encoder"; et = et.BaseType) {
                        if (et.FullName == "System.Text.EncoderNLS") return encoder;
                    }

                    // Possibility 2: _pass1encoder is DefaultEncoder or another stateless encoder;
                    // This makes sense only if the encoding process of the given encoding is stateless too.
                    // This should not be common: because .NET strings are UTF-16, it is practically impossible to have a universally-applicable stateless encoding.
                    // However, such encoding may still be useful in some specifc cases, like non-incremental encoding
                    // or if the input is guaranteed to never contain surrogate pairs.
                    // We use ProxyEncoder to access EncoderFallbackBuffer used by such stateless encoder.
                    return new ProxyEncoder(encoding);

                    // Possibility 3: Some 3rd party non-compliant encoder. Too bad...
                }
                return encoder;
            }

            private static PythonEncoderFallbackBuffer? GetPythonEncoderFallbackBuffer(Encoder? enc) {
                if (enc == null) return null;

                // This should be as simple as enc.FallbackBuffer as PythonEncoderFallbackBuffer
                // but it requires a workaround for a design oddity in System.Text.EncoderNLS on .NET Core
                var fbuf = enc.FallbackBuffer as PythonEncoderFallbackBuffer;
#if NETCOREAPP || NETSTANDARD
                fbuf ??= enc.GetType().GetProperty(nameof(enc.FallbackBuffer))?.GetValue(enc) as PythonEncoderFallbackBuffer;
#endif
                return fbuf;
            }

            // mandatory override of an abstract method
            public override int GetByteCount(char[] chars, int index, int count, bool flush) {
                if (chars == null) throw new ArgumentNullException(nameof(chars));
                if (index < 0 || count < 0) throw new ArgumentOutOfRangeException(index < 0 ? nameof(index) : nameof(count));
                if (chars.Length - index < count) throw new ArgumentOutOfRangeException(nameof(chars));

                return this.GetByteCount(chars.AsSpan(index, count), flush);
            }

            // optional override of a virtual method but preferable to the one from the base class
            public override unsafe int GetByteCount(char* chars, int count, bool flush) {
                if (chars == null) throw new ArgumentNullException(nameof(chars));

                var fbuf1 = GetPythonEncoderFallbackBuffer(_pass1encoder);

                var s = new string(chars, 0, count);
                fbuf1?.PrepareIncrement(s, forEncoding: false);
                int numBytes = _pass1encoder.GetByteCount(chars, count, flush);
                fbuf1?.FinalizeIncrement(count, flush);

                return numBytes;
            }

            // not declared in the base class nevertheless still useful in IronPython context, most efficient if input is a string
            public int GetByteCount(string s, bool flush) {
                if (s == null) throw new ArgumentNullException(nameof(s));

                var fbuf1 = GetPythonEncoderFallbackBuffer(_pass1encoder);

                fbuf1?.PrepareIncrement(s, forEncoding: false);
                int numBytes;
                numBytes = _pass1encoder.GetByteCount(s.AsSpan(), flush);
                fbuf1?.FinalizeIncrement(s.Length, flush);

                return numBytes;
            }

            // mandatory override
            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush) {
                if (chars == null) throw new ArgumentNullException(nameof(chars));
                if (bytes == null) throw new ArgumentNullException(nameof(bytes));
                if (charIndex < 0) throw new ArgumentOutOfRangeException(nameof(charIndex));
                if (charCount < 0) throw new ArgumentOutOfRangeException(nameof(charCount));
                if (chars.Length - charIndex < charCount) throw new ArgumentOutOfRangeException(nameof(chars));
                if (byteIndex < 0 || byteIndex > bytes.Length) throw new ArgumentOutOfRangeException(nameof(byteIndex));

                var s = new string(chars, charIndex, charCount);
                return GetBytes(s, bytes.AsSpan(byteIndex), flush);
            }

            // NLS workhorse
            public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount, bool flush) {
                if (chars == null) throw new ArgumentNullException(nameof(chars));
                if (bytes == null) throw new ArgumentNullException(nameof(bytes));
                if (charCount < 0) throw new ArgumentOutOfRangeException(nameof(charCount));
                if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));

                var s = new string(chars, 0, charCount);
                return GetBytes(s, new Span<byte>(bytes, byteCount), flush);
            }

            // used by IronPython
            public int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush) {
                if (s == null) throw new ArgumentNullException(nameof(s));
                if (bytes == null) throw new ArgumentNullException(nameof(bytes));
                if (charIndex < 0) throw new ArgumentOutOfRangeException(nameof(charIndex));
                if (charCount < 0) throw new ArgumentOutOfRangeException(nameof(charCount));
                if (s.Length - charIndex < charCount) throw new ArgumentOutOfRangeException(nameof(s));
                if (byteIndex < 0 || byteIndex > bytes.Length) throw new ArgumentOutOfRangeException(nameof(byteIndex));

                if (charIndex != 0 || charCount != s.Length) {
                    s = s.Substring(charIndex, charCount);
                }
                return GetBytes(s, bytes.AsSpan(byteIndex), flush);
            }

            private int GetBytes(string data, Span<byte> bytes, bool flush) {
                var fbuf1 = GetPythonEncoderFallbackBuffer(_pass1encoder);
                var fbuf2 = GetPythonEncoderFallbackBuffer(_pass2encoder);
                fbuf1?.PrepareIncrement(data, forEncoding: true);

                var chars = data.AsSpan();

                int written = _pass1encoder.GetBytes(chars, bytes, flush);

                // If the final increment and there were no more fallback bytes, the job is done
                if (fbuf1 == null || flush && fbuf1.IsEmpty && (fbuf2?.IsEmpty ?? true)) {
                    fbuf1?.Reset();
                    fbuf2?.Reset();
                    return written;
                }

                // Lazy creation of _pass2encoder
                if (_pass2encoder == null) {
                    _pass2encoder = GetEncoder(_parentEncoding.Pass2Encoding);
                    fbuf2 = GetPythonEncoderFallbackBuffer(_pass2encoder);
                }
                // fbuf2 is not null here because fbuf1 is not null and Pass1Encoding and Pass2Encoding are identical clones
                fbuf2!.PrepareIncrement(data, forEncoding: true);

                // Restore original fallback bytes
                var bytes2 = new byte[written];
                _pass2encoder.GetBytes(chars, bytes2, flush);

                int cwidth = _parentEncoding.CharacterWidth;
                for (int i = 0; i < written; i++) {
                    if (bytes[i] != bytes2[i]) {
                        int ofs = (i / cwidth) * cwidth;
                        for (int p = 0; p < cwidth; p++) {
                            bytes[ofs++] = fbuf2.GetFallbackByte();
                            fbuf1.GetFallbackByte(); // count the byte as consumed in fbuf1 too
                        }
                        int skip = ofs - i - 1;
                        i += skip;
                    }
                }

                // Check if all fallback bytes are restored properly
                fbuf1.FinalizeIncrement(data.Length, flush);
                fbuf2.FinalizeIncrement(data.Length, flush);

                return written;
            }

            public override void Reset() {
                _pass1encoder.Reset();
                _pass2encoder?.Reset();
            }
        }

        public abstract class PythonEncoderFallback : EncoderFallback, ICloneable {

            public PythonEncoding Encoding {
                get => _encoding ?? throw new NullReferenceException($"Property \"{nameof(Encoding)}\" not initialized before use.");
                set => _encoding = value;
            }
            private PythonEncoding? _encoding;

            public bool IsPass1 { get; set; }

            public virtual object Clone() => MemberwiseClone();
        }

        protected abstract class PythonEncoderFallbackBuffer : EncoderFallbackBuffer {
            private readonly char _marker;

            private readonly Queue<byte>? _allFallbackBytes;  // collects all fallback bytes for the whole pass, only used during actual encoding, pass 2
            private int _fbkByteCnt; // only used during actual encoding; proxy for _fallbackBytes.Count but valid in pass 1 too
            private MemInt _byteCnt; // counts unreported bytes in the buffer from the last fallback; used during both counting and encoding, but counts separately

            private ReadOnlyMemory<char> _fallbackChars;  // fallback chars (if any) from the last fallback
            private int _charCnt; // counts unreported chars in the buffer from the last fallback
            private int _fbkNumChars; // number of all (virtual) chars in the buffer from the last fallback; proxy for _fallbackChars.Length but valid for fallback bytes too

            // for error reporting
            private int _lastRuneUnknown; // in UTF-32; "rune" is an alias for "Unicode codepoint"
            private MemInt _lastIndexUnknown;

            public PythonEncoderFallbackBuffer(bool isPass1, PythonEncoding encoding) {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _allFallbackBytes = isPass1 ? null : new Queue<byte>();
                this.EncodingCharWidth = encoding.CharacterWidth;
                this.CodePage = encoding.CodePage;
                _lastIndexUnknown = -1;
            }

            protected bool EncodingMode { get; private set; }

            protected int EncodingCharWidth { get; }
            protected int CodePage { get; }
            protected string? Data { get; private set; }

            public virtual void PrepareIncrement(string data, bool forEncoding) {
                Data = data;
                if (EncodingMode) {
                    _byteCnt = (int)_byteCnt;
                    _lastIndexUnknown = (int)_lastIndexUnknown;
                } else {
                    _byteCnt = _byteCnt.Initial;
                    _lastIndexUnknown = _lastIndexUnknown.Initial;
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
                    if (index != _lastIndexUnknown + GetUtf16SequenceLength(_lastRuneUnknown)) {
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
                if (flush && !IsEmpty || _byteCnt > 0 && endIndex != _lastIndexUnknown + GetUtf16SequenceLength(_lastRuneUnknown)) {
                    throw PythonOps.UnicodeEncodeError($"incomplete input sequence", _lastRuneUnknown, _lastIndexUnknown);
                }
                Data = null; // release input data for possible collection
                _lastIndexUnknown += -endIndex; // prep. for next incremental encoding step
            }

            public override void Reset() {
                _allFallbackBytes?.Clear();
                _fbkByteCnt = 0;
                _byteCnt = 0;
                _charCnt = _fbkNumChars = 0;
                _fallbackChars = default;
                _lastRuneUnknown = '\0';
                _lastIndexUnknown = -1;
                Data = null;
            }
        }

        private class ProxyDecoder : Decoder {
            private Encoding _encoding;
            private readonly Decoder _decoder;

            public ProxyDecoder(Encoding encoding) {
                _encoding = (Encoding)encoding.Clone();
                _encoding.DecoderFallback = new ProxyDecoderFallback(_encoding.DecoderFallback.CreateFallbackBuffer(), _encoding.DecoderFallback.MaxCharCount);
                _decoder = _encoding.GetDecoder();
                Fallback = _decoder.Fallback = _encoding.DecoderFallback;
            }

            public override int GetCharCount(byte[] bytes, int index, int count)
                => GetCharCount(bytes, index, count, flush: true);

            public override int GetCharCount(byte[] bytes, int index, int count, bool flush)
                => _decoder.GetCharCount(bytes, index, count, flush);

            public override unsafe int GetCharCount(byte* bytes, int count, bool flush)
                => _decoder.GetCharCount(bytes, count, flush);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
                => GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, bool flush)
                => _decoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush);

            public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount, bool flush)
                => _decoder.GetChars(bytes, byteCount, chars, charCount, flush);

            public override void Reset() => _decoder.Reset();

            private class ProxyDecoderFallback : DecoderFallback {
                private readonly DecoderFallbackBuffer _buffer;
                private readonly int _maxCharCount;

                public ProxyDecoderFallback(DecoderFallbackBuffer buffer, int maxCharCount) {
                    _buffer = buffer;
                    _maxCharCount = maxCharCount;
                }

                public override DecoderFallbackBuffer CreateFallbackBuffer() => _buffer;
                public override int MaxCharCount => _maxCharCount;
            }
        }

        private class PythonDecoder : Decoder {
            private readonly PythonEncoding _parentEncoding;
            private readonly Decoder _pass1decoder;
            private Decoder? _pass2decoder;

            public PythonDecoder(PythonEncoding parentEncoding) {
                _parentEncoding = parentEncoding;
                _pass1decoder = GetDecoder(_parentEncoding.Pass1Encoding);
            }

            private static Decoder GetDecoder(Encoding encoding) {
                Decoder decoder = encoding.GetDecoder();

                if (!(decoder.Fallback is PythonDecoderFallback) && encoding.DecoderFallback is PythonDecoderFallback) {
                    // Non-conformant Decoder implementation, the challenge is to get to the fallback buffer used by such decoder.
                    // See notes at PythonEncoder.GetEncoder(...)
                    for (Type? dt = decoder.GetType(); dt != null && dt.FullName != "System.Text.Decoder"; dt = dt.BaseType) {
                        if (dt.FullName == "System.Text.DecoderNLS") return decoder;
                    }

                    return new ProxyDecoder(encoding);
                }
                return decoder;
            }

            private static PythonDecoderFallbackBuffer? GetPythonDecoderFallbackBuffer(Decoder? dec) {
                if (dec == null) return null;

                // see also PythonEncoder.GetPythonEncoderFallbackBuffer(...)
                var fbuf = dec.FallbackBuffer as PythonDecoderFallbackBuffer;
#if NETCOREAPP || NETSTANDARD
                fbuf ??= dec.GetType().GetProperty(nameof(dec.FallbackBuffer))?.GetValue(dec) as PythonDecoderFallbackBuffer;
#endif
                return fbuf;
            }

            // mandatory override of an abstract method
            public override int GetCharCount(byte[] bytes, int index, int count)
                => this.GetCharCount(bytes, index, count, flush: true);

            public override int GetCharCount(byte[] bytes, int index, int count, bool flush)
                => this.GetCharCount(bytes.AsSpan(index, count), flush);

            // NLS workhorse, used by GetString
            public override unsafe int GetCharCount(byte* bytes, int count, bool flush) {
                int numChars;

                var fbuf1 = GetPythonDecoderFallbackBuffer(_pass1decoder);
                fbuf1?.PrepareIncrement(forDecoding: false);
                numChars = _pass1decoder.GetCharCount(bytes, count, flush);
                fbuf1?.FinalizeIncrement(count, flush);

                return numChars;
            }

            // mandatory override of an abstract method
            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
                => this.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, bool flush)
                => this.GetChars(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex), flush);

            // NLS workhorse
            public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount, bool flush) {
                if (bytes == null) throw new ArgumentNullException(nameof(bytes));
                if (chars == null) throw new ArgumentNullException(nameof(chars));
                if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
                if (charCount < 0) throw new ArgumentOutOfRangeException(nameof(charCount));

                return GetChars(new ReadOnlySpan<byte>(bytes, byteCount), new Span<char>(chars, charCount), flush);
            }

            // IronPython workhorse, used by GetString(IBuffer,...)
#if NETCOREAPP
            public override int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars, bool flush) {
#else
            public int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars, bool flush) {
#endif
                var fbuf1 = GetPythonDecoderFallbackBuffer(_pass1decoder);
                var fbuf2 = GetPythonDecoderFallbackBuffer(_pass2decoder);
                fbuf1?.PrepareIncrement(forDecoding: true);
                int? surIdxStart = fbuf1?.FallbackCharCount;

                int written = _pass1decoder.GetChars(bytes, chars, flush);

                // If the final increment and there were no fallback characters, the job is done
                if (fbuf1 == null || flush && fbuf1.FallbackCharCount == surIdxStart && fbuf1.IsEmpty && (fbuf2?.IsEmpty ?? true)) {
                    return written;
                }

                // Lazy creation of _pass2decoder
                if (_pass2decoder == null) {
                    _pass2decoder = GetDecoder(_parentEncoding.Pass2Encoding);
                    fbuf2 = GetPythonDecoderFallbackBuffer(_pass2decoder);
                }
                // fbuf2 is not null here because fbuf1 is not null and Pass1Encoding and Pass2Encoding are identical clones
                fbuf2!.Data = fbuf1.Data;
                fbuf2.PrepareIncrement(forDecoding: true);

                // replace surrogate markers with actual surrogates
                var chars2 = new char[written];
                _pass2decoder.GetChars(bytes, chars2, flush);

                for (int i = 0; i < written; i++) {
                    if (chars[i] != chars2[i]) {
                        chars[i] = fbuf2.GetFallbackChar();
                    }
                }

                // Check if all fallback chars are restored properly
                fbuf1.FinalizeIncrement(bytes.Length, flush);
                fbuf2.FinalizeIncrement(bytes.Length, flush);

                return written;
            }

            // used by IronPython
            public string GetString(IPythonBuffer input, int index, int count) {
                var fbuf1 = GetPythonDecoderFallbackBuffer(_pass1decoder);

                // This allows for UnicodeDecodeError, if occurred, to contain the whole input
                if (fbuf1 != null) fbuf1.Data = input;

                var span = input.AsReadOnlySpan().Slice(index, count);
                int len = _pass1decoder.GetCharCount(span, flush: true);
                if (len == 0) return string.Empty;

                return StringExtensions.Create(len, Tuple.Create(input, index, count), (dest, arg) => {
                    var src = arg.Item1.AsReadOnlySpan().Slice(arg.Item2, arg.Item3);
                    GetChars(src, dest, flush: true);
                });
            }

            public override void Reset() {
                _pass1decoder.Reset();
                _pass2decoder?.Reset();
            }
        }

        public abstract class PythonDecoderFallback : DecoderFallback, ICloneable {
            public PythonEncoding Encoding {
                get => _encoding ?? throw new NullReferenceException($"Property \"{nameof(Encoding)}\" not initialized before use.");
                set => _encoding = value;
            }
            private PythonEncoding? _encoding;

            public bool IsPass1 { get; set; }

            public virtual object Clone() => MemberwiseClone();
        }

        protected abstract class PythonDecoderFallbackBuffer : DecoderFallbackBuffer {
            private readonly char _marker;
            private readonly Queue<char>? _fallbackChars; // collects all fallback chars for the whole pass, only used during actual decoding, pass 2
            private int _fbkCnt; // only used during actual decoding; proxy for _fallbackChars.Count but valid in pass 1 too
            private MemInt _charCnt;  // counts unreported chars from the last fallback; used during both counting and decoding, but counts separately
            private int _fbkNumChars; // number of all virtual chars in the buffer from the last fallback
            private ReadOnlyMemory<char> _safeFallbackChars; // chars from the last fallback that are safe to report; only used during actual decoding

            public PythonDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding) {
                _marker = isPass1 ? Pass1Marker : Pass2Marker;
                _fallbackChars = isPass1 ? null : new Queue<char>();
                this.EncodingCharWidth = encoding.CharacterWidth;
                this.CodePage = encoding.CodePage;
            }

            protected bool DecodingMode { get; private set; }
            protected int EncodingCharWidth { get; }
            protected int CodePage { get; }
            public IPythonBuffer? Data { get; set; }

            public virtual void PrepareIncrement(bool forDecoding) {
                if (DecodingMode) {
                    _charCnt = (int)_charCnt;
                } else {
                    _charCnt = _charCnt.Initial;
                }
                DecodingMode = forDecoding;
            }

            public abstract ReadOnlyMemory<char> GetFallbackChars(byte[] bytesUnknown, int index);

            public override bool Fallback(byte[] bytesUnknown, int index) {
                if (!this.DecodingMode && this.CodePage == 65001 && PythonEncoding.HasBugCorefx29898) { // only for UTF-8
                    index += bytesUnknown.Length;
                }
                ReadOnlyMemory<char> newFallbackChars = GetFallbackChars(bytesUnknown, index);
                _fbkNumChars = newFallbackChars.Length;

                if (DecodingMode && MemoryMarshal.ToEnumerable(newFallbackChars).All(ch => !char.IsSurrogate(ch))) {
                    _safeFallbackChars = newFallbackChars;
                } else {
                    _safeFallbackChars = default;
                    _fbkCnt += _fbkNumChars;
                    if (_fallbackChars != null) {
                        var chars = newFallbackChars.Span;
                        for (int i = 0; i < _fbkNumChars; i++) {
                            _fallbackChars.Enqueue(chars[i]);
                        }
                    }
                }
                _charCnt = _fbkNumChars;

                return true;
            }

            public override int Remaining => _charCnt;

            public override char GetNextChar() {
                if (_charCnt <= 0) return '\0';

                if (_safeFallbackChars.IsEmpty) {
                    _charCnt--;
                    return _marker; // unfortunately, returning the actual fallback char here might result in an exception 
                } else {
                    return _safeFallbackChars.Span[_safeFallbackChars.Length - _charCnt--];
                }
            }

            public override bool MovePrevious() {
                if (_charCnt >= _fbkNumChars) return false;

                _charCnt++;
                return true;
            }

            // not called for pass1 decoding
            public char GetFallbackChar() {
                _fbkCnt--;
                // _fallbackChars is not null for pass2 decoding
                return _fallbackChars!.Dequeue();
            }

            public virtual bool IsEmpty => (_fallbackChars?.Count ?? 0) == 0;

            public int FallbackCharCount => _fbkCnt;

            public virtual void FinalizeIncrement(int endIndex, bool flush) {
                if (flush && !IsEmpty) {
                    // If this exception is being thrown, the problem is with the code, not the input sequence.
                    // Therefore, the exception does not carry any input data.
                    throw new DecoderFallbackException("internal error");
                }
                _safeFallbackChars = default;
                Data = null; // release input data for possible collection
            }

            public override void Reset() {
                _fallbackChars?.Clear();
                _fbkCnt = 0;
                _fbkNumChars = 0;
                _charCnt = 0;
                _safeFallbackChars = default;
                Data = null;
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

            public override ReadOnlyMemory<char> GetFallbackChars(byte[] bytesUnknown, int index) {
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

                private byte[]? _savebuf;
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

            private ByteBuffer? _buffer;

            public SurrogatePassDecoderFallbackBuffer(bool isPass1, PythonEncoding encoding)
                : base(isPass1, encoding) {
            }

            public override ReadOnlyMemory<char> GetFallbackChars(byte[] bytesUnknown, int index) {
                const int bytesPerChar = 3; // UTF-8 uses 3 bytes to encode a surrogate
                int numBytes = bytesUnknown.Length;
                char fallbackChar = char.MinValue;
                char[]? fallbackChars = null;
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

            public override void PrepareIncrement(bool forDecoding) {
                if (DecodingMode) {
                    _buffer?.Save();
                } else {
                    _buffer?.Restore();
                }
                base.PrepareIncrement(forDecoding);
            }

            public override void FinalizeIncrement(int endIndex, bool flush) {
                if (_buffer != null) {
                    if (_buffer.Length != 0 && (_buffer.EndIndex != endIndex || flush)) {
                        // leftover bytes not consumed
                        Throw(_buffer.TrimmedBytes(), _buffer.Index);
                    }
                    // Prepare for next incremental decode step (if any)
                    _buffer.Index = -_buffer.Length;
                }
                base.FinalizeIncrement(endIndex, flush);
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

        public PythonErrorHandlerEncoding(CodeContext context, Encoding encoding, string errors)
            : base(encoding, new PythonHandlerEncoderFallback(), new PythonHandlerDecoderFallback()) {
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
            private object? _handler;
            private char _lastSeenChar;

            public PythonHandlerEncoderFallbackBuffer(bool isPass1, PythonErrorHandlerEncoding encoding)
                : base(isPass1, encoding) {
                _encoding = encoding;
            }

            public override Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> GetFallbackCharsOrBytes(int runeUnknown, int index) {
                if (_handler == null) {
                    _handler = LightExceptions.CheckAndThrow(PythonOps.LookupEncodingError(_encoding._context, _encoding._errors));
                }

                // create exception object to hand over to the user-function...
                int runeLen = GetUtf16SequenceLength(runeUnknown);
                string? data = Data;
                if (index < 0) {
                    // corner case, the unknown data starts at the end of the previous increment
                    Debug.Assert(index == -1); // only one char back allowed
                    data = _lastSeenChar + data;
                    index++;
                }
                var exObj = PythonExceptions.CreatePythonThrowable(
                    PythonExceptions.UnicodeEncodeError,
                    StringOps.GetEncodingName(_encoding, normalize: false),
                    data,
                    index,
                    index + runeLen,
                    "ordinal not in range for this codec");

                // call the user function...
                object? res = PythonCalls.Call(_handler, exObj);

                return ExtractEncodingReplacement(res, index + runeLen);
            }

            private static Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>> ExtractEncodingReplacement(object? res, int cursorPos) {
                // verify the result is sane...
                if (res is PythonTuple tres && tres.__len__() == 2 && Converter.TryConvertToInt32(tres[1], out int newPos)) {

                    if (newPos != cursorPos) throw new NotImplementedException("Moving an encoding cursor not implemented yet");

                    if (Converter.TryConvertToString(tres[0], out string? str)) {
                        return new Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>>(str.AsMemory(), default);
                    } else if (tres[0] is Bytes bytes) {
                        return new Tuple<ReadOnlyMemory<char>, ReadOnlyMemory<byte>>(default, bytes.AsMemory());
                    }
                }

                throw PythonOps.TypeError("encoding error handler must return (str/bytes, int) tuple");
            }

            public override void FinalizeIncrement(int endIndex, bool flush) {
                if (Data != null && Data.Length > 0 && EncodingMode) _lastSeenChar <<= Data[0];
                base.FinalizeIncrement(endIndex, flush);
            }
        }

        private class PythonHandlerDecoderFallback : PythonDecoderFallback {

            public override int MaxCharCount => int.MaxValue;

            public override DecoderFallbackBuffer CreateFallbackBuffer()
                => new PythonHandlerDecoderFallbackBuffer(this.IsPass1, (PythonErrorHandlerEncoding)this.Encoding);

            private class PythonHandlerDecoderFallbackBuffer : PythonDecoderFallbackBuffer {
                // This constant should be small but at least as large
                // as the longest encoded sequence generated by any codec for any rune, minus one
                private const int MinNumLookbackBytes = 8;

                private readonly PythonErrorHandlerEncoding _encoding;
                private object? _handler;
                private byte[] _previousData;
                private int _previousDataLen;
                private Bytes? _bytesData;

                public PythonHandlerDecoderFallbackBuffer(bool isPass1, PythonErrorHandlerEncoding encoding)
                    : base(isPass1, encoding) {
                    _encoding = encoding;
                    _previousData = new byte[MinNumLookbackBytes];
                }

                public override ReadOnlyMemory<char> GetFallbackChars(byte[] bytesUnknown, int index) {
                    _handler ??= LightExceptions.CheckAndThrow(PythonOps.LookupEncodingError(_encoding._context, _encoding._errors));

                    // prepare the data object and error position for UnicodeDecodeError
                    object bytesObj;
                    int pos;
                    if (_bytesData == null) {
                        if (Data != null) {
                            if (index < 0) {
                                // corner case, the unknown data starts at the end of the previous increment (or earlier)
                                if (_previousData.Length < -index)
                                    throw new NotImplementedException($"Not enough lookback bytes to process decoding of this increment, increase '{nameof(MinNumLookbackBytes)}'");
                                var dataSpan = Data.AsReadOnlySpan();
                                var extData = new byte[-index + dataSpan.Length];
                                Array.Copy(_previousData, _previousData.Length + index, extData, 0, -index);
                                dataSpan.CopyTo(extData.AsSpan(-index));
                                bytesObj = _bytesData = Bytes.Make(extData);
                                pos = 0;
                            } else {
                                if (Data.Object is Bytes bytes && bytes.Count == Data.NumBytes()) {
                                    // fast track, no data copy
                                    bytesObj = _bytesData = new Bytes(bytes);
                                } else {
                                    bytesObj = _bytesData = Bytes.Make(Data.AsReadOnlySpan().ToArray());
                                }
                                pos = index;
                            }
                        } else {
                            // mock-up data object
                            if (index < 0) throw new NotSupportedException("Incremental decoding not supported in this usage");
                            bytesObj = new Bytes(bytesUnknown);
                            pos = 0;
                        }
                    } else {
                        bytesObj = _bytesData;
                        // if _bytesData is not null, Data is not null also
                        pos = index + _bytesData.Count - Data!.NumBytes();
                    }

                    // create exception object to hand over to the user-function...
                    var exObj = PythonExceptions.CreatePythonThrowable(
                        PythonExceptions.UnicodeDecodeError,
                        StringOps.GetEncodingName(_encoding, normalize: false),
                        bytesObj,
                        pos,
                        pos + bytesUnknown.Length,
                        "invalid bytes");

                    // call the user function...
                    object? res = PythonCalls.Call(_handler, exObj);

                    return ExtractDecodingReplacement(res, pos + bytesUnknown.Length);
                }

                private static ReadOnlyMemory<char> ExtractDecodingReplacement(object? res, int cursorPos) {
                    // verify the result is sane...
                    if (res is PythonTuple tres && tres.__len__() == 2 && Converter.TryConvertToInt32(tres[1], out int newPos)) {

                        if (newPos != cursorPos) throw new NotImplementedException("Moving a decoding cursor not implemented yet");

                        if (Converter.TryConvertToString(tres[0], out string? str)) {
                            return str.AsMemory();
                        }
                    }

                    throw PythonOps.TypeError("decoding error handler must return (str, int) tuple");
                }

                public override void FinalizeIncrement(int endIndex, bool flush) {
                    _bytesData = null;
                    if (DecodingMode) {
                        if (flush) {
                            _previousDataLen = 0;
                        } else if (Data != null) {
                            var span = Data.AsReadOnlySpan();
                            int retain = _previousData.Length - span.Length;
                            if (retain <= 0) {
                                _previousDataLen = 0;
                            }  else if (retain < _previousDataLen) {
                                Array.Copy(_previousData, _previousDataLen - retain, _previousData, 0, retain);
                                _previousDataLen = retain;
                            }
                            int add = Math.Min(span.Length, _previousData.Length - _previousDataLen);
                            span.Slice(0, add).CopyTo(_previousData.AsSpan(_previousDataLen));
                            _previousDataLen += add;
                        }
                    }
                    base.FinalizeIncrement(endIndex, flush);
                }

                public override void Reset() {
                    base.Reset();
                    _previousDataLen = 0;
                }
            }
        }

    }

    // TODO: Move to IronPython.Runtime.StringOps
    internal static class StringExtensions {
#if NETCOREAPP

        public static string Create<TState>(int length, TState state, System.Buffers.SpanAction<char, TState> action)
            => string.Create(length, state, action);

#else

        public delegate void SpanAction<T, in TArg>(Span<T> span, TArg arg);

        public static string Create<TState>(int length, TState state, SpanAction<char, TState> action) {
            var enc = new StringCreateHelperEncoding<TState>(length, state, action);
            unsafe {
                byte* dummy = stackalloc byte[1];
                return enc.GetString(dummy, 1);
            }
        }

        /// <summary>
        /// Helper class to access unsafe static internal String CreateStringFromEncoding(byte*, int, Encoding)
        /// in .NET Framework
        /// </summary>
        private class StringCreateHelperEncoding<TState> : Encoding {
            private int _length;
            private TState _state;
            private SpanAction<char, TState> _action;

            public StringCreateHelperEncoding(int length, TState state, SpanAction<char, TState> action) {
                _length = length;
                _state = state;
                _action = action;
            }

            public override unsafe int GetCharCount(byte* bytes, int count) {
                return _length;
            }

            public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount) {
                var dest = new Span<char>(chars, charCount);
                _action(dest, _state);
                return _length;
            }

            // Mandatory overrides, unused
            public override int GetByteCount(char[] chars, int index, int count) => throw new NotImplementedException();
            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) => throw new NotImplementedException();
            public override int GetCharCount(byte[] bytes, int index, int count) => throw new NotImplementedException();
            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) => throw new NotImplementedException();
            public override int GetMaxByteCount(int charCount) => throw new NotImplementedException();
            public override int GetMaxCharCount(int byteCount) => throw new NotImplementedException();
        }
#endif
    }

#if !NETCOREAPP
    // TODO: Move to IronPython.Runtime.Text

    internal static class EncodingExtensions {
        public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes) {
            fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes)) {
                return encoding.GetString(bytesPtr, bytes.Length);
            }

        }
    }

    internal static class EncoderExtensions {
        public static unsafe int GetByteCount(this Encoder encoder, ReadOnlySpan<char> chars, bool flush) {
            fixed (char* pChars = &MemoryMarshal.GetReference(chars)) {
                return encoder.GetByteCount(pChars, chars.Length, flush);
            }
        }

        internal static unsafe int GetBytes(this Encoder encoder, ReadOnlySpan<char> chars, Span<byte> bytes, bool flush) {
            fixed (char* pChars = &MemoryMarshal.GetReference(chars))
            fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes)) {
                return encoder.GetBytes(pChars, chars.Length, pBytes, bytes.Length, flush);
            }
        }
    }

    internal static class DecoderExtensions {
        public static unsafe int GetCharCount(this Decoder decoder, ReadOnlySpan<byte> bytes, bool flush) {
            fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes)) {
                return decoder.GetCharCount(bytesPtr, bytes.Length, flush);
            }
        }

        internal static unsafe int GetChars(this Decoder decoder, ReadOnlySpan<byte> bytes, Span<char> chars, bool flush) {
            fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
            fixed (char* charsPtr = &MemoryMarshal.GetReference(chars)) {
                return decoder.GetChars(bytesPtr, bytes.Length, charsPtr, chars.Length, flush);
            }
        }
    }

#endif
}
