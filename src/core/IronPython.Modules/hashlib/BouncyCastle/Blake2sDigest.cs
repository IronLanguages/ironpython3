using System;
using System.Diagnostics;

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Digests
{
    /*
     * The BLAKE2 cryptographic hash function was designed by Jean-Philippe Aumasson, Samuel Neves, Zooko
     * Wilcox-O'Hearn, and Christian Winnerlein.
     *
     * Reference Implementation and Description can be found at: https://blake2.net/
     * RFC: https://tools.ietf.org/html/rfc7693
     *
     * This implementation does not support the Tree Hashing Mode.
     *
     * For unkeyed hashing, developers adapting BLAKE2 to ASN.1-based message formats SHOULD use the OID tree at:
     *     x = 1.3.6.1.4.1.1722.12.2.
     *
     * +---------------+--------+-----------+------+------------+
     * | Algorithm     | Target | Collision | Hash | Hash ASN.1 |
     * |    Identifier |  Arch  |  Security |  nn  | OID Suffix |
     * +---------------+--------+-----------+------+------------+
     * | id-blake2s128 | 32-bit |   2**64   |  16  |   x.2.4    |
     * | id-blake2s160 | 32-bit |   2**80   |  20  |   x.2.5    |
     * | id-blake2s224 | 32-bit |   2**112  |  28  |   x.2.7    |
     * | id-blake2s256 | 32-bit |   2**128  |  32  |   x.2.8    |
     * +---------------+--------+-----------+------+------------+
     */

    /// <summary>
    /// Implementation of the cryptographic hash function BLAKE2s. BLAKE2s is optimized for 32-bit platforms and
    /// produces digests of any size between 1 and 32 bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BLAKE2s offers a built-in keying mechanism to be used directly for authentication ("Prefix-MAC") rather than an
    /// HMAC construction.
    /// </para>
    /// <para>
    /// BLAKE2s offers built-in support for a salt for randomized hashing and a personal string for defining a unique
    /// hash function for each application.
    /// </para>
    /// </remarks>
    internal sealed class Blake2sDigest
        : IDigest
    {
        /*
         * BLAKE2s Initialization Vector (the same as SHA-256 IV).
         *
         * Produced from the square root of primes 2, 3, 5, 7, 11, 13, 17, 19.
         */
        private static readonly uint[] IV =
        {
            0x6a09e667U, 0xbb67ae85U, 0x3c6ef372U, 0xa54ff53aU, 0x510e527fU, 0x9b05688cU, 0x1f83d9abU, 0x5be0cd19U
        };

        // Message word permutations
        private static readonly byte[] Sigma =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
            11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
            7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
            9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
            2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
            12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
            13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
            6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
            10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
        };

        private const int ROUNDS = 10; // to use for Catenas H'
        private const int BLOCK_LENGTH_BYTES = 64;// bytes

        private readonly uint[] chainValue = new uint[8]; // State vector, in the BLAKE2 paper it is called h

        // Whenever this buffer overflows, it will be processed in the Compress() function.
        // For performance issues, long messages will not use this buffer.
        private readonly byte[] buffer = new byte[BLOCK_LENGTH_BYTES];

        // General parameters:
        private int digestLength = 32; // 1 - 32 bytes
        private byte[] m_salt = null;
        private byte[] m_personalization = null;
        private byte[] m_key = null;

        /*
         * Tree hashing parameters; the Tree Hashing Mode is not supported but these are used for the XOF
         * implementation.
         */
        private int fanout = 1; // 0 - 255
        private int depth = 1; // 1 - 255
        private int leafLength = 0;
        private long nodeOffset = 0L;
        private int nodeDepth = 0;
        private int innerHashLength = 0;
        //private bool isLastNode = false;

        // Position of last inserted byte:
        private int bufferPos = 0; // a value from 0 up to BLOCK_LENGTH_BYTES

        private uint t0 = 0U; // holds last significant bits, counter (counts bytes)
        private uint t1 = 0U; // counter: Length up to 2^64 are supported
        private uint f0 = 0U; // finalization flag, for last block: ~0U

        // For Tree Hashing Mode, not used here:
        //private uint f1 = 0U; // finalization flag, for last node: ~0U

        /// <summary>
        /// Initializes a new instance of <see cref="Blake2sDigest"/>.
        /// </summary>
        public Blake2sDigest()
            : this(256)
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="Blake2sDigest"/> from another <see cref="Blake2sDigest"/>./>.
        /// </summary>
        /// <param name="digest">The original instance of <see cref="Blake2sDigest"/> that is copied.</param>
        public Blake2sDigest(Blake2sDigest digest)
        {
            Array.Copy(digest.chainValue, 0, chainValue, 0, 8);
            Array.Copy(digest.buffer, 0, buffer, 0, BLOCK_LENGTH_BYTES);

            this.bufferPos = digest.bufferPos;
            this.m_key = Arrays.Clone(digest.m_key);
            this.digestLength = digest.digestLength;
            this.t0 = digest.t0;
            this.t1 = digest.t1;
            this.f0 = digest.f0;
            this.m_salt = Arrays.Clone(digest.m_salt);
            this.m_personalization = Arrays.Clone(digest.m_personalization);
            this.fanout = digest.fanout;
            this.depth = digest.depth;
            this.leafLength = digest.leafLength;
            this.nodeOffset = digest.nodeOffset;
            this.nodeDepth = digest.nodeDepth;
            this.innerHashLength = digest.innerHashLength;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Blake2sDigest"/> with a given digest size.
        /// </summary>
        /// <param name="digestBits">Digest size in bits.</param>
        /// <exception cref="ArgumentException"></exception>
        public Blake2sDigest(int digestBits)
        {
            if (digestBits < 8 || digestBits > 256 || digestBits % 8 != 0)
                throw new ArgumentException("BLAKE2s digest bit length must be a multiple of 8 and not greater than 256");

            this.digestLength = digestBits / 8;

            Init();
        }

        /// <summary>
        /// <para>
        /// Initializes a new instance of <see cref="Blake2sDigest"/> with a key.
        /// </para>
        /// 
        /// BLAKE2s for authentication ("Prefix-MAC mode").
        /// After calling the <see cref="DoFinal(byte[], int)"/> method, the key will
        /// remain to be used for further computations of this instance.
        /// The key can be cleared using the <see cref="ClearKey"/> method.
        /// </summary>
        /// <param name="key">A key up to 32 bytes or null.</param>
        /// <exception cref="ArgumentException"></exception>
        public Blake2sDigest(byte[] key)
        {
            this.digestLength = 32;

            if (!Arrays.IsNullOrEmpty(key))
            {
                if (key.Length > 32)
                    throw new ArgumentException("Keys > 32 bytes are not supported", nameof(key));

                m_key = Arrays.CopyBuffer(key);
            }

            Init();
        }

        /// <summary>
        /// <para>
        /// Initializes a new instance of <see cref="Blake2sDigest"/> with a key, required digest length (in bytes), salt and personalization.
        /// </para>
        /// 
        /// After calling the <see cref="DoFinal(byte[], int)"/> method, the key, the salt and the personalization
        /// will remain and might be used for further computations with this instance.
        /// The key can be overwritten using the <see cref="ClearKey"/> method, the salt (pepper)
        /// can be overwritten using the <see cref="ClearSalt"/> method.
        /// </summary>
        /// <param name="key">A key up to 32 bytes or null.</param>
        /// <param name="digestBytes">Digest length from 1 to 32 bytes.</param>
        /// <param name="salt">A 8 bytes or nullable salt.</param>
        /// <param name="personalization">A 8 bytes or null personalization.</param>
        /// <exception cref="ArgumentException"></exception>
        public Blake2sDigest(byte[] key, int digestBytes, byte[] salt, byte[] personalization)
            : this(digestBytes, key, salt, personalization, offset: 0L)
        {
        }

        // XOF root hash parameters
        internal Blake2sDigest(int digestBytes, byte[] key, byte[] salt, byte[] personalization, long offset)
        {
            if (digestBytes < 1 || digestBytes > 32)
                throw new ArgumentException("Invalid digest length (required: 1 - 32)");

            this.digestLength = digestBytes;

            if (!Arrays.IsNullOrEmpty(key))
            {
                if (key.Length > 32)
                    throw new ArgumentException("Keys > 32 bytes are not supported", nameof(key));

                m_key = Arrays.CopyBuffer(key);
            }

            if (salt != null)
            {
                if (salt.Length != 8)
                    throw new ArgumentException("salt length must be exactly 8 bytes", nameof(salt));

                m_salt = Arrays.CopyBuffer(salt);
            }

            if (personalization != null)
            {
                if (personalization.Length != 8)
                    throw new ArgumentException("personalization length must be exactly 8 bytes",
                        nameof(personalization));

                m_personalization = Arrays.CopyBuffer(personalization);
            }

            this.nodeOffset = offset;

            Init();
        }

        // XOF internal hash parameters
        internal Blake2sDigest(int digestBytes, int hashLength, long offset)
        {
            digestLength = digestBytes;
            nodeOffset = offset;
            fanout = 0;
            depth = 0;
            leafLength = hashLength;
            innerHashLength = hashLength;
            nodeDepth = 0;

            Init();
        }

        private void Init()
        {
            int keyLength = 0;
            if (m_key != null)
            {
                keyLength = m_key.Length;
                Array.Copy(m_key, 0, buffer, 0, keyLength);
                //Arrays.Fill(buffer, keyLength, BLOCK_LENGTH_BYTES, 0);
                bufferPos = BLOCK_LENGTH_BYTES; // zero padding
            }

            chainValue[0] = IV[0] ^ (uint)(digestLength | (keyLength << 8) | ((fanout << 16) | (depth << 24)));
            chainValue[1] = IV[1] ^ (uint)leafLength;

            int nofHi = (int)(nodeOffset >> 32);
            int nofLo = (int)nodeOffset;
            chainValue[2] = IV[2] ^ (uint)nofLo;
            chainValue[3] = IV[3] ^ (uint)(nofHi | (nodeDepth << 16) | (innerHashLength << 24));

            chainValue[4] = IV[4];
            chainValue[5] = IV[5];
            if (m_salt != null)
            {
                chainValue[4] ^= Pack.LE_To_UInt32(m_salt, 0);
                chainValue[5] ^= Pack.LE_To_UInt32(m_salt, 4);
            }

            chainValue[6] = IV[6];
            chainValue[7] = IV[7];
            if (m_personalization != null)
            {
                chainValue[6] ^= Pack.LE_To_UInt32(m_personalization, 0);
                chainValue[7] ^= Pack.LE_To_UInt32(m_personalization, 4);
            }
        }

        /// <inheritdoc />
        public void Update(byte b)
        {
            // process the buffer if full else add to buffer:
            int remainingLength = BLOCK_LENGTH_BYTES - bufferPos;
            if (remainingLength == 0)
            {
                // full buffer
                IncrementCounter(BLOCK_LENGTH_BYTES);
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                Compress(buffer);
#else
                Compress(buffer, 0);
#endif
                Array.Clear(buffer, 0, buffer.Length);// clear buffer
                buffer[0] = b;
                bufferPos = 1;
            }
            else
            {
                buffer[bufferPos++] = b;
            }
        }

        /// <inheritdoc />
        public void BlockUpdate(byte[] message, int offset, int len)
        {
            if (message == null || len == 0)
                return;

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BlockUpdate(message.AsSpan(offset, len));
#else
            int remainingLength = 0; // left bytes of buffer

            if (bufferPos != 0)
            {
                // commenced, incomplete buffer

                // complete the buffer:
                remainingLength = BLOCK_LENGTH_BYTES - bufferPos;
                if (remainingLength >= len)
                {
                    Array.Copy(message, offset, buffer, bufferPos, len);
                    bufferPos += len;
                    return;
                }

                // full buffer + at least 1 byte
                Array.Copy(message, offset, buffer, bufferPos, remainingLength);
                IncrementCounter(BLOCK_LENGTH_BYTES);
                Compress(buffer, 0);
                bufferPos = 0;
                Array.Clear(buffer, 0, buffer.Length);// clear buffer
            }

            // process blocks except last block (also if last block is full)
            int blockWiseLastPos = offset + len - BLOCK_LENGTH_BYTES;
            int messagePos = offset + remainingLength;
            while (messagePos < blockWiseLastPos)
            {
                // block wise 64 bytes without buffer:
                IncrementCounter(BLOCK_LENGTH_BYTES);
                Compress(message, messagePos);
                messagePos += BLOCK_LENGTH_BYTES;
            }

            // fill the buffer with left bytes, this might be a full block
            Array.Copy(message, messagePos, buffer, 0, offset + len - messagePos);
            bufferPos += offset + len - messagePos;
#endif
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        /// <inheritdoc />
        public void BlockUpdate(ReadOnlySpan<byte> input)
        {
            if (input.IsEmpty)
                return;

            int remainingLength = 0; // left bytes of buffer

            if (bufferPos != 0)
            {
                // commenced, incomplete buffer

                // complete the buffer:
                remainingLength = BLOCK_LENGTH_BYTES - bufferPos;
                if (remainingLength >= input.Length)
                {
                    input.CopyTo(buffer.AsSpan(bufferPos));
                    bufferPos += input.Length;
                    return;
                }

                // full buffer + at least 1 byte
                input[..remainingLength].CopyTo(buffer.AsSpan(bufferPos));
                IncrementCounter(BLOCK_LENGTH_BYTES);
                Compress(buffer);
                bufferPos = 0;
                Array.Clear(buffer, 0, buffer.Length);// clear buffer
            }

            // process blocks except last block (also if last block is full)
            int blockWiseLastPos = input.Length - BLOCK_LENGTH_BYTES;
            int messagePos = remainingLength;
            while (messagePos < blockWiseLastPos)
            {
                // block wise 64 bytes without buffer:
                IncrementCounter(BLOCK_LENGTH_BYTES);
                Compress(input[messagePos..]);
                messagePos += BLOCK_LENGTH_BYTES;
            }

            // fill the buffer with left bytes, this might be a full block
            input[messagePos..].CopyTo(buffer.AsSpan());
            bufferPos += input.Length - messagePos;
        }
#endif

        /// <summary>Close the digest, producing the final digest value.</summary>
        /// <remarks>
        ///  The <see cref="DoFinal(byte[], int)"/> call leaves the digest reset. 
        ///  Key, salt and personal string remain.
        /// </remarks>
        /// <param name="output">The byte array the digest is to be copied into.</param>
        /// <param name="outOffset">The offset into the byte array the digest is to start at.</param>
        /// <returns>The number of bytes written.</returns>
        public int DoFinal(byte[] output, int outOffset)
        {
            Check.OutputLength(output, outOffset, digestLength, "output buffer too short");

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return DoFinal(output.AsSpan(outOffset));
#else
            f0 = 0xFFFFFFFFU;
            //if (isLastNode)
            //{
            //    f1 = 0xFFFFFFFFU;
            //}

            if (bufferPos > 0)
            {
                IncrementCounter(bufferPos);
            }

            Compress(buffer, 0);

            int full = digestLength >> 2, partial = digestLength & 3;
            Pack.UInt32_To_LE(chainValue, 0, full, output, outOffset);
            if (partial > 0)
            {
                Pack.UInt32_To_LE_Low(chainValue[full], output, outOffset + digestLength - partial, partial);
            }

            Reset();

            return digestLength;
#endif
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        /// <summary>Close the digest, producing the final digest value.</summary>
        /// <remarks>
        ///  The <see cref="DoFinal(Span{byte})"/> call leaves the digest reset. 
        ///  Key, salt and personal string remain.
        /// </remarks>
        /// <param name="output">The span the digest is to be copied into.</param>
        /// <returns>The number of bytes written.</returns>
        public int DoFinal(Span<byte> output)
        {
            Check.OutputLength(output, digestLength, "output buffer too short");

            f0 = 0xFFFFFFFFU;
            //if (isLastNode)
            //{
            //    f1 = 0xFFFFFFFFU;
            //}

            if (bufferPos > 0)
            {
                IncrementCounter(bufferPos);
            }

            Compress(buffer);

            int full = digestLength >> 2, partial = digestLength & 3;
            Pack.UInt32_To_LE(chainValue.AsSpan(0, full), output);
            if (partial > 0)
            {
                Pack.UInt32_To_LE_Low(chainValue[full], output.Slice(digestLength - partial, partial));
            }

            Reset();

            return digestLength;
        }
#endif

        /// <summary>
        /// Reset the digest back to it's initial state.
        /// The key, the salt and the personalization will remain for further computations.
        /// </summary>
        public void Reset()
        {
            bufferPos = 0;
            f0 = 0U;
            t0 = 0U;
            t1 = 0U;

            Array.Clear(buffer, 0, buffer.Length);

            Init();
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private void Compress(ReadOnlySpan<byte> message)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Blake2s_X86.IsSupported)
            {
                Blake2s_X86.Compress(chainValue, IV, t0, t1, f0, message);
                return;
            }
#endif

            Span<uint> m = stackalloc uint[16];
            Pack.LE_To_UInt32(message, m);

            uint v0 = chainValue[0];
            uint v1 = chainValue[1];
            uint v2 = chainValue[2];
            uint v3 = chainValue[3];
            uint v4 = chainValue[4];
            uint v5 = chainValue[5];
            uint v6 = chainValue[6];
            uint v7 = chainValue[7];
            uint v8 = IV[0];
            uint v9 = IV[1];
            uint va = IV[2];
            uint vb = IV[3];
            uint vc = IV[4] ^ t0;
            uint vd = IV[5] ^ t1;
            uint ve = IV[6] ^ f0;
            uint vf = IV[7];       // ^ f1, with f1 == 0

            for (int round = 0; round < ROUNDS; round++)
            {
                int pos = round * 16;

                // G apply to columns of internalState: m[blake2s_sigma[round][2 * blockPos]] /+1
                G(m[Sigma[pos +  0]], m[Sigma[pos +  1]], ref v0, ref v4, ref v8, ref vc);
                G(m[Sigma[pos +  2]], m[Sigma[pos +  3]], ref v1, ref v5, ref v9, ref vd);
                G(m[Sigma[pos +  4]], m[Sigma[pos +  5]], ref v2, ref v6, ref va, ref ve);
                G(m[Sigma[pos +  6]], m[Sigma[pos +  7]], ref v3, ref v7, ref vb, ref vf);

                // G apply to diagonals of internalState:
                G(m[Sigma[pos +  8]], m[Sigma[pos +  9]], ref v0, ref v5, ref va, ref vf);
                G(m[Sigma[pos + 10]], m[Sigma[pos + 11]], ref v1, ref v6, ref vb, ref vc);
                G(m[Sigma[pos + 12]], m[Sigma[pos + 13]], ref v2, ref v7, ref v8, ref vd);
                G(m[Sigma[pos + 14]], m[Sigma[pos + 15]], ref v3, ref v4, ref v9, ref ve);
            }

            chainValue[0] ^= v0 ^ v8;
            chainValue[1] ^= v1 ^ v9;
            chainValue[2] ^= v2 ^ va;
            chainValue[3] ^= v3 ^ vb;
            chainValue[4] ^= v4 ^ vc;
            chainValue[5] ^= v5 ^ vd;
            chainValue[6] ^= v6 ^ ve;
            chainValue[7] ^= v7 ^ vf;
        }
#else
        private void Compress(byte[] message, int messagePos)
        {
            uint[] m = new uint[16];
            Pack.LE_To_UInt32(message, messagePos, m);

            uint v0 = chainValue[0];
            uint v1 = chainValue[1];
            uint v2 = chainValue[2];
            uint v3 = chainValue[3];
            uint v4 = chainValue[4];
            uint v5 = chainValue[5];
            uint v6 = chainValue[6];
            uint v7 = chainValue[7];
            uint v8 = IV[0];
            uint v9 = IV[1];
            uint va = IV[2];
            uint vb = IV[3];
            uint vc = IV[4] ^ t0;
            uint vd = IV[5] ^ t1;
            uint ve = IV[6] ^ f0;
            uint vf = IV[7];       // ^ f1, with f1 == 0

            for (int round = 0; round < ROUNDS; round++)
            {
                int pos = round * 16;

                // G apply to columns of internalState: m[blake2s_sigma[round][2 * blockPos]] /+1
                G(m[Sigma[pos +  0]], m[Sigma[pos +  1]], ref v0, ref v4, ref v8, ref vc);
                G(m[Sigma[pos +  2]], m[Sigma[pos +  3]], ref v1, ref v5, ref v9, ref vd);
                G(m[Sigma[pos +  4]], m[Sigma[pos +  5]], ref v2, ref v6, ref va, ref ve);
                G(m[Sigma[pos +  6]], m[Sigma[pos +  7]], ref v3, ref v7, ref vb, ref vf);

                // G apply to diagonals of internalState:
                G(m[Sigma[pos +  8]], m[Sigma[pos +  9]], ref v0, ref v5, ref va, ref vf);
                G(m[Sigma[pos + 10]], m[Sigma[pos + 11]], ref v1, ref v6, ref vb, ref vc);
                G(m[Sigma[pos + 12]], m[Sigma[pos + 13]], ref v2, ref v7, ref v8, ref vd);
                G(m[Sigma[pos + 14]], m[Sigma[pos + 15]], ref v3, ref v4, ref v9, ref ve);
            }

            chainValue[0] ^= v0 ^ v8;
            chainValue[1] ^= v1 ^ v9;
            chainValue[2] ^= v2 ^ va;
            chainValue[3] ^= v3 ^ vb;
            chainValue[4] ^= v4 ^ vc;
            chainValue[5] ^= v5 ^ vd;
            chainValue[6] ^= v6 ^ ve;
            chainValue[7] ^= v7 ^ vf;
        }
#endif

        /// <inheritdoc />
        public string AlgorithmName => "BLAKE2s";

        /// <inheritdoc />
        public int GetDigestSize() => digestLength;

        /// <summary>
        ///  Return the size in bytes of the internal buffer the digest applies it's compression 
        ///  function to.
        ///  </summary>
        /// <returns>The byte length of the digests internal buffer.</returns>
        public int GetByteLength() => BLOCK_LENGTH_BYTES;

        /// <summary>
        /// Clears the key.
        /// </summary>
        public void ClearKey()
        {
            if (m_key != null)
            {
                Array.Clear(m_key, 0, m_key.Length);
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

       /// <summary>
       /// Clears the salt (pepper).
       /// </summary>
        public void ClearSalt()
        {
            if (m_salt != null)
            {
                Array.Clear(m_salt, 0, m_salt.Length);
            }
        }

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void IncrementCounter(int count)
        {
            Debug.Assert(count > 0);

            uint count32 = (uint)count;
            t0 += count32;
            if (t0 < count32)
            {
                ++t1;
            }
        }

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static void G(uint m1, uint m2, ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b + m1;
            d = Integers.RotateRight(d ^ a, 16);
            c += d;
            b = Integers.RotateRight(b ^ c, 12);

            a += b + m2;
            d = Integers.RotateRight(d ^ a, 8);
            c += d;
            b = Integers.RotateRight(b ^ c, 7);
        }
    }
}
