using System;

using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Digests
{
    /// <summary>Implementation of SHA-256 as defined in FIPS 180-2.</summary>
    /// <remarks>
    /// <pre>
    ///         block  word  digest
    /// SHA-1   512    32    160
    /// SHA-256 512    32    256
    /// SHA-384 1024   64    384
    /// SHA-512 1024   64    512
    /// </pre>
    /// </remarks>
    internal class Sha256Digest
        : GeneralDigest
    {
        private const int DigestLength = 32;

        private uint H1, H2, H3, H4, H5, H6, H7, H8;
        private uint[] X = new uint[16];
        private int xOff;

        /// <summary>Initializes a new instance of <see cref="Sha256Digest"/>.</summary>
        public Sha256Digest()
        {
            initHs();
        }

        /// <summary>Initializes a new instance of <see cref="Sha256Digest"/> from an existing one.</summary>
        /// <param name="t">The digest to copy from.</param>
        public Sha256Digest(Sha256Digest t) : base(t)
        {
            CopyIn(t);
        }

        private void CopyIn(Sha256Digest t)
        {
            base.CopyIn(t);

            H1 = t.H1;
            H2 = t.H2;
            H3 = t.H3;
            H4 = t.H4;
            H5 = t.H5;
            H6 = t.H6;
            H7 = t.H7;
            H8 = t.H8;

            Array.Copy(t.X, 0, X, 0, t.X.Length);
            xOff = t.xOff;
        }

        public override string AlgorithmName
        {
            get { return "SHA-256"; }
        }

        public override int GetDigestSize()
        {
            return DigestLength;
        }

        internal override void ProcessWord(byte[] input, int inOff)
        {
            X[xOff] = Pack.BE_To_UInt32(input, inOff);

            if (++xOff == 16)
            {
                ProcessBlock();
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal override void ProcessWord(ReadOnlySpan<byte> word)
        {
            X[xOff] = Pack.BE_To_UInt32(word);

            if (++xOff == 16)
            {
                ProcessBlock();
            }
        }
#endif

        internal override void ProcessLength(
            long bitLength)
        {
            if (xOff > 14)
            {
                ProcessBlock();
            }

            X[14] = (uint)((ulong)bitLength >> 32);
            X[15] = (uint)((ulong)bitLength);
        }

        public override int DoFinal(byte[] output, int outOff)
        {
            Finish();

            Pack.UInt32_To_BE(H1, output, outOff);
            Pack.UInt32_To_BE(H2, output, outOff + 4);
            Pack.UInt32_To_BE(H3, output, outOff + 8);
            Pack.UInt32_To_BE(H4, output, outOff + 12);
            Pack.UInt32_To_BE(H5, output, outOff + 16);
            Pack.UInt32_To_BE(H6, output, outOff + 20);
            Pack.UInt32_To_BE(H7, output, outOff + 24);
            Pack.UInt32_To_BE(H8, output, outOff + 28);

            Reset();

            return DigestLength;
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override int DoFinal(Span<byte> output)
        {
            Finish();

            Pack.UInt32_To_BE(H1, output);
            Pack.UInt32_To_BE(H2, output[4..]);
            Pack.UInt32_To_BE(H3, output[8..]);
            Pack.UInt32_To_BE(H4, output[12..]);
            Pack.UInt32_To_BE(H5, output[16..]);
            Pack.UInt32_To_BE(H6, output[20..]);
            Pack.UInt32_To_BE(H7, output[24..]);
            Pack.UInt32_To_BE(H8, output[28..]);

            Reset();

            return DigestLength;
        }
#endif

        /**
        * reset the chaining variables
        */
        public override void Reset()
        {
            base.Reset();

            initHs();

            xOff = 0;
            Array.Clear(X, 0, X.Length);
        }

        private void initHs()
        {
            /* SHA-256 initial hash value
            * The first 32 bits of the fractional parts of the square roots
            * of the first eight prime numbers
            */
            H1 = 0x6a09e667;
            H2 = 0xbb67ae85;
            H3 = 0x3c6ef372;
            H4 = 0xa54ff53a;
            H5 = 0x510e527f;
            H6 = 0x9b05688c;
            H7 = 0x1f83d9ab;
            H8 = 0x5be0cd19;
        }

        internal override void ProcessBlock()
        {
            uint[] X = this.X;

            // The message schedule lives in 16 rotating locals instead of a 64/80-word array: no bounds
            // checks or memory traffic for the expansion, and the array only ever holds the 16 input words.
            uint x00 = X[0], x01 = X[1], x02 = X[2], x03 = X[3];
            uint x04 = X[4], x05 = X[5], x06 = X[6], x07 = X[7];
            uint x08 = X[8], x09 = X[9], x10 = X[10], x11 = X[11];
            uint x12 = X[12], x13 = X[13], x14 = X[14], x15 = X[15];

            uint a = H1, b = H2, c = H3, d = H4, e = H5, f = H6, g = H7, h = H8;

            for (int t = 0; ; t += 16)
            {
                h += Sum1(e) + Ch(e, f, g) + K[t] + x00;
                d += h;
                h += Sum0(a) + Maj(a, b, c);

                g += Sum1(d) + Ch(d, e, f) + K[t + 1] + x01;
                c += g;
                g += Sum0(h) + Maj(h, a, b);

                f += Sum1(c) + Ch(c, d, e) + K[t + 2] + x02;
                b += f;
                f += Sum0(g) + Maj(g, h, a);

                e += Sum1(b) + Ch(b, c, d) + K[t + 3] + x03;
                a += e;
                e += Sum0(f) + Maj(f, g, h);

                d += Sum1(a) + Ch(a, b, c) + K[t + 4] + x04;
                h += d;
                d += Sum0(e) + Maj(e, f, g);

                c += Sum1(h) + Ch(h, a, b) + K[t + 5] + x05;
                g += c;
                c += Sum0(d) + Maj(d, e, f);

                b += Sum1(g) + Ch(g, h, a) + K[t + 6] + x06;
                f += b;
                b += Sum0(c) + Maj(c, d, e);

                a += Sum1(f) + Ch(f, g, h) + K[t + 7] + x07;
                e += a;
                a += Sum0(b) + Maj(b, c, d);

                h += Sum1(e) + Ch(e, f, g) + K[t + 8] + x08;
                d += h;
                h += Sum0(a) + Maj(a, b, c);

                g += Sum1(d) + Ch(d, e, f) + K[t + 9] + x09;
                c += g;
                g += Sum0(h) + Maj(h, a, b);

                f += Sum1(c) + Ch(c, d, e) + K[t + 10] + x10;
                b += f;
                f += Sum0(g) + Maj(g, h, a);

                e += Sum1(b) + Ch(b, c, d) + K[t + 11] + x11;
                a += e;
                e += Sum0(f) + Maj(f, g, h);

                d += Sum1(a) + Ch(a, b, c) + K[t + 12] + x12;
                h += d;
                d += Sum0(e) + Maj(e, f, g);

                c += Sum1(h) + Ch(h, a, b) + K[t + 13] + x13;
                g += c;
                c += Sum0(d) + Maj(d, e, f);

                b += Sum1(g) + Ch(g, h, a) + K[t + 14] + x14;
                f += b;
                b += Sum0(c) + Maj(c, d, e);

                a += Sum1(f) + Ch(f, g, h) + K[t + 15] + x15;
                e += a;
                a += Sum0(b) + Maj(b, c, d);

                if (t == 48)
                    break;

                // W[t+16+j] = sigma1(W[t+14+j]) + W[t+9+j] + sigma0(W[t+1+j]) + W[t+j]; wrapped indices refer to
                // words already updated in this pass, which is exactly the required schedule.
                x00 += Theta1(x14) + x09 + Theta0(x01);
                x01 += Theta1(x15) + x10 + Theta0(x02);
                x02 += Theta1(x00) + x11 + Theta0(x03);
                x03 += Theta1(x01) + x12 + Theta0(x04);
                x04 += Theta1(x02) + x13 + Theta0(x05);
                x05 += Theta1(x03) + x14 + Theta0(x06);
                x06 += Theta1(x04) + x15 + Theta0(x07);
                x07 += Theta1(x05) + x00 + Theta0(x08);
                x08 += Theta1(x06) + x01 + Theta0(x09);
                x09 += Theta1(x07) + x02 + Theta0(x10);
                x10 += Theta1(x08) + x03 + Theta0(x11);
                x11 += Theta1(x09) + x04 + Theta0(x12);
                x12 += Theta1(x10) + x05 + Theta0(x13);
                x13 += Theta1(x11) + x06 + Theta0(x14);
                x14 += Theta1(x12) + x07 + Theta0(x15);
                x15 += Theta1(x13) + x08 + Theta0(x00);
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
            xOff = 0;
            Array.Clear(X, 0, 16);
        }

        //private static uint Ch(uint x, uint y, uint z) => (x & y) ^ (z & ~x);
        private static uint Ch(uint x, uint y, uint z) => z ^ (x & (y ^ z));

        //private static uint Maj(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);
        private static uint Maj(uint x, uint y, uint z) => (x & y) | (z & (x ^ y));

        private static uint Sum0(uint x) => (x >> 2 | x << 30) ^ (x >> 13 | x << 19) ^ (x >> 22 | x << 10);

        private static uint Sum1(uint x) => (x >> 6 | x << 26) ^ (x >> 11 | x << 21) ^ (x >> 25 | x << 7);

        private static uint Theta0(uint x) => (x >> 7 | x << 25) ^ (x >> 18 | x << 14) ^ (x >> 3);

        private static uint Theta1(uint x) => (x >> 17 | x << 15) ^ (x >> 19 | x << 13) ^ (x >> 10);

        /// <summary>
        /// SHA-256 Constants (represent the first 32 bits of the fractional parts of the cube roots of the first
        /// sixty-four prime numbers)
        /// </summary>
        private static readonly uint[] K = {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };

        public override IMemoable Copy() => new Sha256Digest(this);

        public override void Reset(IMemoable other) => CopyIn((Sha256Digest)other);
    }
}
