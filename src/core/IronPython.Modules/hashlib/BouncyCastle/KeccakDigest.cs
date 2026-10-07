using System;
using System.Diagnostics;
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

using Org.BouncyCastle.Crypto.Utilities;
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
using Org.BouncyCastle.Math.Raw;
#endif
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Digests
{
    /// <summary>
    /// Implementation of Keccak based on following KeccakNISTInterface.c from http://keccak.noekeon.org/
    /// </summary>
    /// <remarks>
    /// Following the naming conventions used in the C source code to enable easy review of the implementation.
    /// </remarks>
    internal class KeccakDigest
        : IDigest, IMemoable
    {
        private static readonly ulong[] KeccakRoundConstants = new ulong[]{
            0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL,
            0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
            0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL,
            0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL,
            0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL,
            0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL
        };

        private readonly ulong[] state = new ulong[25];
        protected byte[] dataQueue = new byte[192];
        protected int rate;
        protected int bitsInQueue;
        protected internal int fixedOutputLength;
        protected bool squeezing;

        public KeccakDigest()
            : this(288)
        {
        }

        public KeccakDigest(int bitLength)
        {
            Init(bitLength);
        }

        public KeccakDigest(KeccakDigest source)
        {
            CopyIn(source);
        }

        private void CopyIn(KeccakDigest source)
        {
            Array.Copy(source.state, 0, this.state, 0, source.state.Length);
            Array.Copy(source.dataQueue, 0, this.dataQueue, 0, source.dataQueue.Length);
            this.rate = source.rate;
            this.bitsInQueue = source.bitsInQueue;
            this.fixedOutputLength = source.fixedOutputLength;
            this.squeezing = source.squeezing;
        }

        public virtual string AlgorithmName
        {
            get { return "Keccak-" + fixedOutputLength; }
        }

        public virtual int GetDigestSize()
        {
            return fixedOutputLength >> 3;
        }

        public virtual void Update(byte input)
        {
            Absorb(input);
        }

        public virtual void BlockUpdate(byte[] input, int inOff, int len)
        {
            Absorb(input, inOff, len);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public virtual void BlockUpdate(ReadOnlySpan<byte> input)
        {
            Absorb(input);
        }
#endif

        public virtual int DoFinal(byte[] output, int outOff)
        {
            Squeeze(output, outOff, fixedOutputLength);

            Reset();

            return GetDigestSize();
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public virtual int DoFinal(Span<byte> output)
        {
            int digestSize = GetDigestSize();
            Squeeze(output[..digestSize]);

            Reset();

            return digestSize;
        }
#endif

        /*
         * TODO Possible API change to support partial-byte suffixes.
         */
        protected virtual int DoFinal(byte[] output, int outOff, byte partialByte, int partialBits)
        {
            if (partialBits > 0)
            {
                AbsorbBits(partialByte, partialBits);
            }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Squeeze(output.AsSpan(outOff, fixedOutputLength >> 3));
#else
            Squeeze(output, outOff, fixedOutputLength);
#endif

            Reset();

            return GetDigestSize();
        }

        public virtual void Reset()
        {
            Init(fixedOutputLength);
        }

        /**
         * Return the size of block that the compression function is applied to in bytes.
         *
         * @return internal byte length of a block.
         */
        public virtual int GetByteLength()
        {
            return rate >> 3;
        }

        private void Init(int bitLength)
        {
            switch (bitLength)
            {
                case 128:
                case 224:
                case 256:
                case 288:
                case 384:
                case 512:
                    InitSponge(1600 - (bitLength << 1));
                    break;
                default:
                    throw new ArgumentException("must be one of 128, 224, 256, 288, 384, or 512.", "bitLength");
            }
        }

        private void InitSponge(int rate)
        {
            if (rate <= 0 || rate >= 1600 || (rate & 63) != 0)
                throw new InvalidOperationException("invalid rate value");

            this.rate = rate;
            Array.Clear(state, 0, state.Length);
            Arrays.Fill(this.dataQueue, (byte)0);
            this.bitsInQueue = 0;
            this.squeezing = false;
            this.fixedOutputLength = (1600 - rate) >> 1;
        }

        protected void Absorb(byte data)
        {
            if ((bitsInQueue & 7) != 0)
                throw new InvalidOperationException("attempt to absorb with odd length queue");
            if (squeezing)
                throw new InvalidOperationException("attempt to absorb while squeezing");

            dataQueue[bitsInQueue >> 3] = data;
            if ((bitsInQueue += 8) == rate)
            {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                KeccakAbsorb(dataQueue);
#else
                KeccakAbsorb(dataQueue, 0);
#endif
                bitsInQueue = 0;
            }
        }

        protected void Absorb(byte[] data, int off, int len)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Absorb(data.AsSpan(off, len));
#else
            if ((bitsInQueue & 7) != 0)
                throw new InvalidOperationException("attempt to absorb with odd length queue");
            if (squeezing)
                throw new InvalidOperationException("attempt to absorb while squeezing");

            int bytesInQueue = bitsInQueue >> 3;
            int rateBytes = rate >> 3;

            int available = rateBytes - bytesInQueue;
            if (len < available)
            {
                Array.Copy(data, off, dataQueue, bytesInQueue, len);
                this.bitsInQueue += len << 3;
                return;
            }

            int count = 0;
            if (bytesInQueue > 0)
            {
                Array.Copy(data, off, dataQueue, bytesInQueue, available);
                count += available;
                KeccakAbsorb(dataQueue, 0);
            }

            int remaining;
            while ((remaining = (len - count)) >= rateBytes)
            {
                KeccakAbsorb(data, off + count);
                count += rateBytes;
            }

            Array.Copy(data, off + count, dataQueue, 0, remaining);
            this.bitsInQueue = remaining << 3;
#endif
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        protected void Absorb(ReadOnlySpan<byte> data)
        {
            if ((bitsInQueue & 7) != 0)
                throw new InvalidOperationException("attempt to absorb with odd length queue");
            if (squeezing)
                throw new InvalidOperationException("attempt to absorb while squeezing");

            int bytesInQueue = bitsInQueue >> 3;
            int rateBytes = rate >> 3;

            int len = data.Length;
            int available = rateBytes - bytesInQueue;
            if (len < available)
            {
                data.CopyTo(dataQueue.AsSpan(bytesInQueue));
                this.bitsInQueue += len << 3;
                return;
            }

            int count = 0;
            if (bytesInQueue > 0)
            {
                data[..available].CopyTo(dataQueue.AsSpan(bytesInQueue));
                count += available;
                KeccakAbsorb(dataQueue);
            }

            int remaining;
            while ((remaining = len - count) >= rateBytes)
            {
                KeccakAbsorb(data[count..]);
                count += rateBytes;
            }

            data[count..].CopyTo(dataQueue.AsSpan());
            this.bitsInQueue = remaining << 3;
        }
#endif

        protected void AbsorbBits(int data, int bits)
        {
            if (bits < 1 || bits > 7)
                throw new ArgumentException("must be in the range 1 to 7", "bits");
            if ((bitsInQueue & 7) != 0)
                throw new InvalidOperationException("attempt to absorb with odd length queue");
            if (squeezing)
                throw new InvalidOperationException("attempt to absorb while squeezing");

            int mask = (1 << bits) - 1;
            dataQueue[bitsInQueue >> 3] = (byte)(data & mask);

            // NOTE: After this, bitsInQueue is no longer a multiple of 8, so no more absorbs will work
            bitsInQueue += bits;
        }

        private void PadAndSwitchToSqueezingPhase()
        {
            Debug.Assert(bitsInQueue < rate);

            dataQueue[bitsInQueue >> 3] |= (byte)(1 << (bitsInQueue & 7));

            if (++bitsInQueue == rate)
            {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                KeccakAbsorb(dataQueue);
#else
                KeccakAbsorb(dataQueue, 0);
#endif
            }
            else
            {
                int full = bitsInQueue >> 6, partial = bitsInQueue & 63;
                int off = 0;
                for (int i = 0; i < full; ++i)
                {
                    state[i] ^= Pack.LE_To_UInt64(dataQueue, off);
                    off += 8;
                }
                if (partial > 0)
                {
                    ulong mask = (1UL << partial) - 1UL;
                    state[full] ^= Pack.LE_To_UInt64(dataQueue, off) & mask;
                }
            }

            state[(rate - 1) >> 6] ^= (1UL << 63);

            bitsInQueue = 0;
            squeezing = true;
        }

        // TODO[api] Not needed under NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        protected void Squeeze(byte[] output, int offset, long outputLength)
        {
            if ((outputLength & 7L) != 0L)
                throw new InvalidOperationException("outputLength not a multiple of 8");

            if (!squeezing)
            {
                PadAndSwitchToSqueezingPhase();
            }
            else if (bitsInQueue > 0)
            {
                int availableBits = bitsInQueue;
                int dataQueuePos = (rate - bitsInQueue) >> 3;

                if (outputLength <= availableBits)
                {
                    SqueezePartialState(dataQueuePos, (int)outputLength >> 3, output, offset);
                    bitsInQueue -= (int)outputLength;
                    return;
                }

                SqueezePartialState(dataQueuePos, availableBits >> 3, output, offset);
                offset += availableBits >> 3;
                outputLength -= availableBits;
                bitsInQueue = 0;
            }

            while (outputLength >= rate)
            {
                KeccakPermutation(state);
                SqueezeFullState(output, offset);
                offset += rate >> 3;
                outputLength -= rate;
            }

            if (outputLength > 0)
            {
                KeccakPermutation(state);
                SqueezePartialState(0, (int)outputLength >> 3, output, offset);
                bitsInQueue = rate - (int)outputLength;
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        protected void Squeeze(Span<byte> output)
        {
            int rateBytes = rate >> 3;

            if (!squeezing)
            {
                PadAndSwitchToSqueezingPhase();
            }
            else if (bitsInQueue > 0)
            {
                int available = bitsInQueue >> 3;
                int dataQueuePos = rateBytes - available;

                if (output.Length <= available)
                {
                    SqueezePartialState(dataQueuePos, output.Length, output);
                    bitsInQueue -= output.Length << 3;
                    return;
                }

                SqueezePartialState(dataQueuePos, available, output);
                output = output[available..];
                bitsInQueue = 0;
            }

            while (output.Length >= rateBytes)
            {
                KeccakPermutation(state);
                SqueezeFullState(output);
                output = output[rateBytes..];
            }

            if (!output.IsEmpty)
            {
                KeccakPermutation(state);
                SqueezePartialState(0, output.Length, output);
                bitsInQueue = rate - (output.Length << 3);
            }
        }
#endif

        private void EnsureQueuePacked(int byteOff, int byteLen)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            if (BitConverter.IsLittleEndian)
                return;
#endif

            int from = (byteOff + 7) / 8;
            int to = (byteOff + byteLen + 7) / 8;
            Pack.UInt64_To_LE(state, from, to - from, dataQueue, from * 8);
        }

        private void SqueezeFullState(byte[] output, int outputOff)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            SqueezeFullState(output.AsSpan(outputOff));
#else
            Pack.UInt64_To_LE(state, 0, rate >> 6, output, outputOff);
#endif
        }

        private void SqueezePartialState(int byteOff, int byteLen, byte[] output, int outputOff)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            SqueezePartialState(byteOff, byteLen, output.AsSpan(outputOff));
#else
            EnsureQueuePacked(byteOff, byteLen);
            Array.Copy(dataQueue, byteOff, output, outputOff, byteLen);
#endif
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SqueezeFullState(Span<byte> output)
        {
            var fullState = state.AsSpan(0, rate >> 6);
            if (BitConverter.IsLittleEndian)
            {
                MemoryMarshal.AsBytes(fullState).CopyTo(output);
            }
            else
            {
                Pack.UInt64_To_LE(fullState, output);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SqueezePartialState(int byteOff, int byteLen, Span<byte> output)
        {
            if (BitConverter.IsLittleEndian)
            {
                MemoryMarshal.AsBytes(state.AsSpan()).Slice(byteOff, byteLen).CopyTo(output);
            }
            else
            {
                EnsureQueuePacked(byteOff, byteLen);
                dataQueue.AsSpan(byteOff, byteLen).CopyTo(output);
            }
        }
#endif

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private void KeccakAbsorb(ReadOnlySpan<byte> data)
        {
            if (BitConverter.IsLittleEndian)
            {
                Nat.XorTo64(rate >> 6, MemoryMarshal.Cast<byte, ulong>(data), state);
            }
            else
            {
                int count = rate >> 6, off = 0;
                for (int i = 0; i < count; ++i)
                {
                    state[i] ^= Pack.LE_To_UInt64(data[off..]);
                    off += 8;
                }
            }

            KeccakPermutation(state);
        }
#else
        private void KeccakAbsorb(byte[] data, int off)
        {
            int count = rate >> 6;
            for (int i = 0; i < count; ++i)
            {
                state[i] ^= Pack.LE_To_UInt64(data, off);
                off += 8;
            }

            KeccakPermutation(state);
        }
#endif

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal static void KeccakPermutation(Span<ulong> A)
#else
        internal static void KeccakPermutation(ulong[] A)
#endif
        {
            var bounds = A[24];

            ulong a00 = A[ 0], a01 = A[ 1], a02 = A[ 2], a03 = A[ 3], a04 = A[ 4];
            ulong a05 = A[ 5], a06 = A[ 6], a07 = A[ 7], a08 = A[ 8], a09 = A[ 9];
            ulong a10 = A[10], a11 = A[11], a12 = A[12], a13 = A[13], a14 = A[14];
            ulong a15 = A[15], a16 = A[16], a17 = A[17], a18 = A[18], a19 = A[19];
            ulong a20 = A[20], a21 = A[21], a22 = A[22], a23 = A[23], a24 = A[24];

            for (int i = 0; i < 24; i++)
            {
                // theta
                ulong c0 = a00 ^ a05 ^ a10 ^ a15 ^ a20;
                ulong c1 = a01 ^ a06 ^ a11 ^ a16 ^ a21;
                ulong c2 = a02 ^ a07 ^ a12 ^ a17 ^ a22;
                ulong c3 = a03 ^ a08 ^ a13 ^ a18 ^ a23;
                ulong c4 = a04 ^ a09 ^ a14 ^ a19 ^ a24;

                ulong d1 = Longs.RotateLeft(c1, 1) ^ c4;
                ulong d2 = Longs.RotateLeft(c2, 1) ^ c0;
                ulong d3 = Longs.RotateLeft(c3, 1) ^ c1;
                ulong d4 = Longs.RotateLeft(c4, 1) ^ c2;
                ulong d0 = Longs.RotateLeft(c0, 1) ^ c3;

                a00 ^= d1; a05 ^= d1; a10 ^= d1; a15 ^= d1; a20 ^= d1;
                a01 ^= d2; a06 ^= d2; a11 ^= d2; a16 ^= d2; a21 ^= d2;
                a02 ^= d3; a07 ^= d3; a12 ^= d3; a17 ^= d3; a22 ^= d3;
                a03 ^= d4; a08 ^= d4; a13 ^= d4; a18 ^= d4; a23 ^= d4;
                a04 ^= d0; a09 ^= d0; a14 ^= d0; a19 ^= d0; a24 ^= d0;

                // rho/pi
                c1  = Longs.RotateLeft(a01,  1);
                a01 = Longs.RotateLeft(a06, 44);
                a06 = Longs.RotateLeft(a09, 20);
                a09 = Longs.RotateLeft(a22, 61);
                a22 = Longs.RotateLeft(a14, 39);
                a14 = Longs.RotateLeft(a20, 18);
                a20 = Longs.RotateLeft(a02, 62);
                a02 = Longs.RotateLeft(a12, 43);
                a12 = Longs.RotateLeft(a13, 25);
                a13 = Longs.RotateLeft(a19,  8);
                a19 = Longs.RotateLeft(a23, 56);
                a23 = Longs.RotateLeft(a15, 41);
                a15 = Longs.RotateLeft(a04, 27);
                a04 = Longs.RotateLeft(a24, 14);
                a24 = Longs.RotateLeft(a21,  2);
                a21 = Longs.RotateLeft(a08, 55);
                a08 = Longs.RotateLeft(a16, 45);
                a16 = Longs.RotateLeft(a05, 36);
                a05 = Longs.RotateLeft(a03, 28);
                a03 = Longs.RotateLeft(a18, 21);
                a18 = Longs.RotateLeft(a17, 15);
                a17 = Longs.RotateLeft(a11, 10);
                a11 = Longs.RotateLeft(a07,  6);
                a07 = Longs.RotateLeft(a10,  3);
                a10 = c1;

                // chi
                c0 = a00 ^ (~a01 & a02);
                c1 = a01 ^ (~a02 & a03);
                a02 ^= ~a03 & a04;
                a03 ^= ~a04 & a00;
                a04 ^= ~a00 & a01;
                a00 = c0;
                a01 = c1;

                c0 = a05 ^ (~a06 & a07);
                c1 = a06 ^ (~a07 & a08);
                a07 ^= ~a08 & a09;
                a08 ^= ~a09 & a05;
                a09 ^= ~a05 & a06;
                a05 = c0;
                a06 = c1;

                c0 = a10 ^ (~a11 & a12);
                c1 = a11 ^ (~a12 & a13);
                a12 ^= ~a13 & a14;
                a13 ^= ~a14 & a10;
                a14 ^= ~a10 & a11;
                a10 = c0;
                a11 = c1;

                c0 = a15 ^ (~a16 & a17);
                c1 = a16 ^ (~a17 & a18);
                a17 ^= ~a18 & a19;
                a18 ^= ~a19 & a15;
                a19 ^= ~a15 & a16;
                a15 = c0;
                a16 = c1;

                c0 = a20 ^ (~a21 & a22);
                c1 = a21 ^ (~a22 & a23);
                a22 ^= ~a23 & a24;
                a23 ^= ~a24 & a20;
                a24 ^= ~a20 & a21;
                a20 = c0;
                a21 = c1;

                // iota
                a00 ^= KeccakRoundConstants[i];
            }

            A[ 0] = a00; A[ 1] = a01; A[ 2] = a02; A[ 3] = a03; A[ 4] = a04;
            A[ 5] = a05; A[ 6] = a06; A[ 7] = a07; A[ 8] = a08; A[ 9] = a09;
            A[10] = a10; A[11] = a11; A[12] = a12; A[13] = a13; A[14] = a14;
            A[15] = a15; A[16] = a16; A[17] = a17; A[18] = a18; A[19] = a19;
            A[20] = a20; A[21] = a21; A[22] = a22; A[23] = a23; A[24] = a24;
        }

        public virtual IMemoable Copy()
        {
            return new KeccakDigest(this);
        }

        public virtual void Reset(IMemoable other)
        {
            CopyIn((KeccakDigest)other);
        }
    }
}
