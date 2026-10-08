using System;

using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Digests
{
    /**
    * Base class for SHA-384 and SHA-512.
    */
    internal abstract class LongDigest
        : IDigest, IMemoable
    {
        private const int MyByteLength = 128;

        private byte[] xBuf;
        private int xBufOff;

        private long byteCount1;
        private long byteCount2;

        internal ulong H1, H2, H3, H4, H5, H6, H7, H8;

        private ulong[] W = new ulong[16];
        private int wOff;

        /**
        * Constructor for variable length word
        */
        internal LongDigest()
        {
            xBuf = new byte[8];

            Reset();
        }

        /**
        * Copy constructor.  We are using copy constructors in place
        * of the object.Clone() interface as this interface is not
        * supported by J2ME.
        */
        internal LongDigest(
            LongDigest t)
        {
            xBuf = new byte[t.xBuf.Length];

            CopyIn(t);
        }

        protected void CopyIn(LongDigest t)
        {
            Array.Copy(t.xBuf, 0, xBuf, 0, t.xBuf.Length);

            xBufOff = t.xBufOff;
            byteCount1 = t.byteCount1;
            byteCount2 = t.byteCount2;

            H1 = t.H1;
            H2 = t.H2;
            H3 = t.H3;
            H4 = t.H4;
            H5 = t.H5;
            H6 = t.H6;
            H7 = t.H7;
            H8 = t.H8;

            Array.Copy(t.W, 0, W, 0, t.W.Length);
            wOff = t.wOff;
        }

        public void Update(
            byte input)
        {
            xBuf[xBufOff++] = input;

            if (xBufOff == xBuf.Length)
            {
                ProcessWord(xBuf, 0);
                xBufOff = 0;
            }

            byteCount1++;
        }

        public void BlockUpdate(
            byte[] input,
            int inOff,
            int length)
        {
            //
            // fill the current word
            //
            while ((xBufOff != 0) && (length > 0))
            {
                Update(input[inOff]);

                inOff++;
                length--;
            }

            //
            // process whole words.
            //
            while (length >= xBuf.Length)
            {
                ProcessWord(input, inOff);

                inOff += xBuf.Length;
                length -= xBuf.Length;
                byteCount1 += xBuf.Length;
            }

            //
            // load in the remainder.
            //
            while (length > 0)
            {
                Update(input[inOff]);

                inOff++;
                length--;
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public void BlockUpdate(ReadOnlySpan<byte> input)
        {
            int inOff = 0;
            int length = input.Length;

            //
            // fill the current word
            //
            while ((xBufOff != 0) && (length > 0))
            {
                Update(input[inOff]);

                inOff++;
                length--;
            }

            //
            // process whole words.
            //
            while (length >= xBuf.Length)
            {
                ProcessWord(input.Slice(inOff, xBuf.Length));

                inOff += xBuf.Length;
                length -= xBuf.Length;
                byteCount1 += xBuf.Length;
            }

            //
            // load in the remainder.
            //
            while (length > 0)
            {
                Update(input[inOff]);

                inOff++;
                length--;
            }
        }
#endif

        public void Finish()
        {
            AdjustByteCounts();

            long lowBitLength = byteCount1 << 3;
            long hiBitLength = byteCount2;

            //
            // add the pad bytes.
            //
            Update((byte)128);

            while (xBufOff != 0)
            {
                Update((byte)0);
            }

            ProcessLength(lowBitLength, hiBitLength);

            ProcessBlock();
        }

        public virtual void Reset()
        {
            byteCount1 = 0;
            byteCount2 = 0;

            xBufOff = 0;
            for (int i = 0; i < xBuf.Length; i++)
            {
                xBuf[i] = 0;
            }

            wOff = 0;
            Array.Clear(W, 0, W.Length);
        }

        internal void ProcessWord(
            byte[] input,
            int inOff)
        {
            W[wOff] = Pack.BE_To_UInt64(input, inOff);

            if (++wOff == 16)
            {
                ProcessBlock();
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal void ProcessWord(ReadOnlySpan<byte> word)
        {
            W[wOff] = Pack.BE_To_UInt64(word);

            if (++wOff == 16)
            {
                ProcessBlock();
            }
        }
#endif

        /**
        * adjust the byte counts so that byteCount2 represents the
        * upper long (less 3 bits) word of the byte count.
        */
        private void AdjustByteCounts()
        {
            if (byteCount1 > 0x1fffffffffffffffL)
            {
                byteCount2 += (long)((ulong)byteCount1 >> 61);
                byteCount1 &= 0x1fffffffffffffffL;
            }
        }

        internal void ProcessLength(
            long lowW,
            long hiW)
        {
            if (wOff > 14)
            {
                ProcessBlock();
            }

            W[14] = (ulong)hiW;
            W[15] = (ulong)lowW;
        }

        internal void ProcessBlock()
        {
            AdjustByteCounts();

            ulong[] W = this.W;

            // The message schedule lives in 16 rotating locals instead of a 64/80-word array: no bounds
            // checks or memory traffic for the expansion, and the array only ever holds the 16 input words.
            ulong w00 = W[0], w01 = W[1], w02 = W[2], w03 = W[3];
            ulong w04 = W[4], w05 = W[5], w06 = W[6], w07 = W[7];
            ulong w08 = W[8], w09 = W[9], w10 = W[10], w11 = W[11];
            ulong w12 = W[12], w13 = W[13], w14 = W[14], w15 = W[15];

            ulong a = H1, b = H2, c = H3, d = H4, e = H5, f = H6, g = H7, h = H8;

            for (int t = 0; ; t += 16)
            {
                h += Sum1(e) + Ch(e, f, g) + K[t] + w00;
                d += h;
                h += Sum0(a) + Maj(a, b, c);

                g += Sum1(d) + Ch(d, e, f) + K[t + 1] + w01;
                c += g;
                g += Sum0(h) + Maj(h, a, b);

                f += Sum1(c) + Ch(c, d, e) + K[t + 2] + w02;
                b += f;
                f += Sum0(g) + Maj(g, h, a);

                e += Sum1(b) + Ch(b, c, d) + K[t + 3] + w03;
                a += e;
                e += Sum0(f) + Maj(f, g, h);

                d += Sum1(a) + Ch(a, b, c) + K[t + 4] + w04;
                h += d;
                d += Sum0(e) + Maj(e, f, g);

                c += Sum1(h) + Ch(h, a, b) + K[t + 5] + w05;
                g += c;
                c += Sum0(d) + Maj(d, e, f);

                b += Sum1(g) + Ch(g, h, a) + K[t + 6] + w06;
                f += b;
                b += Sum0(c) + Maj(c, d, e);

                a += Sum1(f) + Ch(f, g, h) + K[t + 7] + w07;
                e += a;
                a += Sum0(b) + Maj(b, c, d);

                h += Sum1(e) + Ch(e, f, g) + K[t + 8] + w08;
                d += h;
                h += Sum0(a) + Maj(a, b, c);

                g += Sum1(d) + Ch(d, e, f) + K[t + 9] + w09;
                c += g;
                g += Sum0(h) + Maj(h, a, b);

                f += Sum1(c) + Ch(c, d, e) + K[t + 10] + w10;
                b += f;
                f += Sum0(g) + Maj(g, h, a);

                e += Sum1(b) + Ch(b, c, d) + K[t + 11] + w11;
                a += e;
                e += Sum0(f) + Maj(f, g, h);

                d += Sum1(a) + Ch(a, b, c) + K[t + 12] + w12;
                h += d;
                d += Sum0(e) + Maj(e, f, g);

                c += Sum1(h) + Ch(h, a, b) + K[t + 13] + w13;
                g += c;
                c += Sum0(d) + Maj(d, e, f);

                b += Sum1(g) + Ch(g, h, a) + K[t + 14] + w14;
                f += b;
                b += Sum0(c) + Maj(c, d, e);

                a += Sum1(f) + Ch(f, g, h) + K[t + 15] + w15;
                e += a;
                a += Sum0(b) + Maj(b, c, d);

                if (t == 64)
                    break;

                // W[t+16+j] = sigma1(W[t+14+j]) + W[t+9+j] + sigma0(W[t+1+j]) + W[t+j]; wrapped indices refer to
                // words already updated in this pass, which is exactly the required schedule.
                w00 += Sigma1(w14) + w09 + Sigma0(w01);
                w01 += Sigma1(w15) + w10 + Sigma0(w02);
                w02 += Sigma1(w00) + w11 + Sigma0(w03);
                w03 += Sigma1(w01) + w12 + Sigma0(w04);
                w04 += Sigma1(w02) + w13 + Sigma0(w05);
                w05 += Sigma1(w03) + w14 + Sigma0(w06);
                w06 += Sigma1(w04) + w15 + Sigma0(w07);
                w07 += Sigma1(w05) + w00 + Sigma0(w08);
                w08 += Sigma1(w06) + w01 + Sigma0(w09);
                w09 += Sigma1(w07) + w02 + Sigma0(w10);
                w10 += Sigma1(w08) + w03 + Sigma0(w11);
                w11 += Sigma1(w09) + w04 + Sigma0(w12);
                w12 += Sigma1(w10) + w05 + Sigma0(w13);
                w13 += Sigma1(w11) + w06 + Sigma0(w14);
                w14 += Sigma1(w12) + w07 + Sigma0(w15);
                w15 += Sigma1(w13) + w08 + Sigma0(w00);
            }

            H1 += a;
            H2 += b;
            H3 += c;
            H4 += d;
            H5 += e;
            H6 += f;
            H7 += g;
            H8 += h;

            //
            // reset the offset and clean out the word buffer.
            //
            wOff = 0;
            Array.Clear(W, 0, 16);
        }

        //private static ulong Ch(ulong x, ulong y, ulong z) => (x & y) ^ (z & ~x);
        private static ulong Ch(ulong x, ulong y, ulong z) => z ^ (x & (y ^ z));

        //private static ulong Maj(ulong x, ulong y, ulong z) => (x & y) ^ (x & z) ^ (y & z);
        private static ulong Maj(ulong x, ulong y, ulong z) => (x & y) | (z & (x ^ y));

        private static ulong Sum0(ulong x) => (x << 36 | x >> 28) ^ (x << 30 | x >> 34) ^ (x << 25 | x >> 39);

        private static ulong Sum1(ulong x) => (x << 50 | x >> 14) ^ (x << 46 | x >> 18) ^ (x << 23 | x >> 41);

        private static ulong Sigma0(ulong x) => (x << 63 | x >> 1) ^ (x << 56 | x >> 8) ^ (x >> 7);

        private static ulong Sigma1(ulong x) => (x << 45 | x >> 19) ^ (x << 3 | x >> 61) ^ (x >> 6);

        /// <summary>
        /// SHA-512 Constants (represent the first 64 bits of the fractional parts of the cube roots of the first
        /// sixty-four prime numbers)
        /// </summary>
        internal static readonly ulong[] K =
        {
            0x428a2f98d728ae22, 0x7137449123ef65cd, 0xb5c0fbcfec4d3b2f, 0xe9b5dba58189dbbc,
            0x3956c25bf348b538, 0x59f111f1b605d019, 0x923f82a4af194f9b, 0xab1c5ed5da6d8118,
            0xd807aa98a3030242, 0x12835b0145706fbe, 0x243185be4ee4b28c, 0x550c7dc3d5ffb4e2,
            0x72be5d74f27b896f, 0x80deb1fe3b1696b1, 0x9bdc06a725c71235, 0xc19bf174cf692694,
            0xe49b69c19ef14ad2, 0xefbe4786384f25e3, 0x0fc19dc68b8cd5b5, 0x240ca1cc77ac9c65,
            0x2de92c6f592b0275, 0x4a7484aa6ea6e483, 0x5cb0a9dcbd41fbd4, 0x76f988da831153b5,
            0x983e5152ee66dfab, 0xa831c66d2db43210, 0xb00327c898fb213f, 0xbf597fc7beef0ee4,
            0xc6e00bf33da88fc2, 0xd5a79147930aa725, 0x06ca6351e003826f, 0x142929670a0e6e70,
            0x27b70a8546d22ffc, 0x2e1b21385c26c926, 0x4d2c6dfc5ac42aed, 0x53380d139d95b3df,
            0x650a73548baf63de, 0x766a0abb3c77b2a8, 0x81c2c92e47edaee6, 0x92722c851482353b,
            0xa2bfe8a14cf10364, 0xa81a664bbc423001, 0xc24b8b70d0f89791, 0xc76c51a30654be30,
            0xd192e819d6ef5218, 0xd69906245565a910, 0xf40e35855771202a, 0x106aa07032bbd1b8,
            0x19a4c116b8d2d0c8, 0x1e376c085141ab53, 0x2748774cdf8eeb99, 0x34b0bcb5e19b48a8,
            0x391c0cb3c5c95a63, 0x4ed8aa4ae3418acb, 0x5b9cca4f7763e373, 0x682e6ff3d6b2b8a3,
            0x748f82ee5defb2fc, 0x78a5636f43172f60, 0x84c87814a1f0ab72, 0x8cc702081a6439ec,
            0x90befffa23631e28, 0xa4506cebde82bde9, 0xbef9a3f7b2c67915, 0xc67178f2e372532b,
            0xca273eceea26619c, 0xd186b8c721c0c207, 0xeada7dd6cde0eb1e, 0xf57d4f7fee6ed178,
            0x06f067aa72176fba, 0x0a637dc5a2c898a6, 0x113f9804bef90dae, 0x1b710b35131c471b,
            0x28db77f523047d84, 0x32caab7b40c72493, 0x3c9ebe0a15c9bebc, 0x431d67c49c100d4c,
            0x4cc5d4becb3e42b6, 0x597f299cfc657e2a, 0x5fcb6fab3ad6faec, 0x6c44198c4a475817
        };

        public int GetByteLength()
        {
            return MyByteLength;
        }

        public abstract string AlgorithmName { get; }
        public abstract int GetDigestSize();
        public abstract int DoFinal(byte[] output, int outOff);
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public abstract int DoFinal(Span<byte> output);
#endif
        public abstract IMemoable Copy();
        public abstract void Reset(IMemoable t);
    }
}
