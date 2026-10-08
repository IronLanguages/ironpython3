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
     * | id-blake2b160 | 64-bit |   2**80   |  20  |   x.1.20   |
     * | id-blake2b256 | 64-bit |   2**128  |  32  |   x.1.32   |
     * | id-blake2b384 | 64-bit |   2**192  |  48  |   x.1.48   |
     * | id-blake2b512 | 64-bit |   2**256  |  64  |   x.1.64   |
     * +---------------+--------+-----------+------+------------+
     */

    /// <summary>
    /// Implementation of the cryptographic hash function BLAKE2b. BLAKE2b is optimized for 64-bit platforms and
    /// produces digests of any size between 1 and 64 bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BLAKE2b offers a built-in keying mechanism to be used directly for authentication ("Prefix-MAC") rather than an
    /// HMAC construction.
    /// </para>
    /// <para>
    /// BLAKE2b offers built-in support for a salt for randomized hashing and a personal string for defining a unique
    /// hash function for each application.
    /// </para>
    /// </remarks>
    internal sealed class Blake2bDigest
        : IDigest
    {
        /*
         * BLAKE2b Initialization Vector (the same as SHA-512 IV).
         *
         * Produced from the square root of primes 2, 3, 5, 7, 11, 13, 17, 19.
         */
        private static readonly ulong[] IV =
        {
            0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL, 0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
            0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL, 0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL
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
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
        };

        private const int ROUNDS = 12; // to use for Catenas H'
        private const int BLOCK_LENGTH_BYTES = 128;// bytes

        private readonly ulong[] chainValue = new ulong[8]; // State vector, in the BLAKE2 paper it is called h

        // Whenever this buffer overflows, it will be processed in the Compress() function.
        // For performance issues, long messages will not use this buffer.
        private readonly byte[] buffer = new byte[BLOCK_LENGTH_BYTES];

        // General parameters:
        private int digestLength = 64; // 1 - 64 bytes
        private byte[] m_salt = null;
        private byte[] m_personalization = null;
        private byte[] m_key = null;

        /*
         * Tree hashing parameters; because this class does not implement the Tree Hashing Mode, these parameters can be
         * treated as constants (see Init() function).
         */
        //private int fanout = 1; // 0 - 255
        //private int depth = 1; // 1 - 255
        //private int leafLength = 0;
        //private long nodeOffset = 0L;
        //private int nodeDepth = 0;
        //private int innerHashLength = 0;
        //private bool isLastNode = false;

        // Position of last inserted byte:
        private int bufferPos = 0; // a value from 0 up to BLOCK_LENGTH_BYTES

        private ulong t0 = 0UL; // holds last significant bits, counter (counts bytes)
        private ulong t1 = 0UL; // counter: Length up to 2^128 are supported
        private ulong f0 = 0UL; // finalization flag, for last block: ~0UL

        // For Tree Hashing Mode, not used here:
        //private ulong f1 = 0UL; // finalization flag, for last node: ~0UL

        /// <summary>
        /// Initializes a new instance of <see cref="Blake2bDigest"/>.
        /// </summary>
        public Blake2bDigest()
            : this(512)
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="Blake2bDigest"/> from another <see cref="Blake2bDigest"/>./>.
        /// </summary>
        /// <param name="digest">The original instance of <see cref="Blake2bDigest"/> that is copied.</param>
        public Blake2bDigest(Blake2bDigest digest)
        {
            Array.Copy(digest.chainValue, 0, chainValue, 0, 8);
            Array.Copy(digest.buffer, 0, buffer, 0, BLOCK_LENGTH_BYTES);

            this.bufferPos = digest.bufferPos;
            this.m_key = Arrays.Clone(digest.m_key);
            this.digestLength = digest.digestLength;
            this.m_personalization = Arrays.Clone(digest.m_personalization);
            this.m_salt = Arrays.Clone(digest.m_salt);
            this.t0 = digest.t0;
            this.t1 = digest.t1;
            this.f0 = digest.f0;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Blake2bDigest"/> with a given digest size.
        /// </summary>
        /// <param name="digestSize">Digest size in bits.</param>
        /// <exception cref="ArgumentException"></exception>
        public Blake2bDigest(int digestSize)
        {
            if (digestSize < 8 || digestSize > 512 || digestSize % 8 != 0)
                throw new ArgumentException("BLAKE2b digest bit length must be a multiple of 8 and not greater than 512");

            this.digestLength = digestSize / 8;

            Init();
        }

        /// <summary>
        /// <para>
        /// Initializes a new instance of <see cref="Blake2bDigest"/> with a key.
        /// </para>
        /// 
        /// BLAKE2b for authentication ("Prefix-MAC mode").
        /// After calling the <see cref="DoFinal(byte[], int)"/> method, the key will
        /// remain to be used for further computations of this instance.
        /// The key can be cleared using the <see cref="ClearKey"/> method.
        /// </summary>
        /// <param name="key">A key up to 64 bytes or null.</param>
        /// <exception cref="ArgumentException"></exception>
        public Blake2bDigest(byte[] key)
        {
            this.digestLength = 64;

            if (!Arrays.IsNullOrEmpty(key))
            {
                if (key.Length > 64)
                    throw new ArgumentException("Keys > 64 bytes are not supported", nameof(key));

                m_key = Arrays.CopyBuffer(key);
            }

            Init();
        }

        /// <summary>
        /// <para>
        /// Initializes a new instance of <see cref="Blake2bDigest"/> with a key, required digest length (in bytes), salt and personalization.
        /// </para>
        /// 
        /// After calling the <see cref="DoFinal(byte[], int)"/> method, the key, the salt and the personalization
        /// will remain and might be used for further computations with this instance.
        /// The key can be overwritten using the <see cref="ClearKey"/> method, the salt (pepper)
        /// can be overwritten using the <see cref="ClearSalt"/> method.
        /// </summary>
        /// <param name="key">A key up to 64 bytes or null.</param>
        /// <param name="digestLength">Digest length from 1 to 64 bytes.</param>
        /// <param name="salt">A 16 bytes or nullable salt.</param>
        /// <param name="personalization">A 16 bytes or null personalization.</param>
        /// <exception cref="ArgumentException"></exception>
        public Blake2bDigest(byte[] key, int digestLength, byte[] salt, byte[] personalization)
        {
            if (digestLength < 1 || digestLength > 64)
                throw new ArgumentException("Invalid digest length (required: 1 - 64)");

            this.digestLength = digestLength;

            if (!Arrays.IsNullOrEmpty(key))
            {
                if (key.Length > 64)
                    throw new ArgumentException("Keys > 64 bytes are not supported", nameof(key));

                m_key = Arrays.CopyBuffer(key);
            }

            if (salt != null)
            {
                if (salt.Length != 16)
                    throw new ArgumentException("salt length must be exactly 16 bytes", nameof(salt));

                m_salt = Arrays.CopyBuffer(salt);
            }

            if (personalization != null)
            {
                if (personalization.Length != 16)
                    throw new ArgumentException("personalization length must be exactly 16 bytes",
                        nameof(personalization));

                m_personalization = Arrays.CopyBuffer(personalization);
            }

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

            chainValue[0] = IV[0] ^ (ulong)(digestLength | (keyLength << 8) | 0x1010000);
            // 0x1010000 = ((fanout << 16) | (depth << 24) | (leafLength << 32));
            // with fanout = 1; depth = 0; leafLength = 0;
            chainValue[1] = IV[1];// ^ nodeOffset; with nodeOffset = 0;
            chainValue[2] = IV[2];// ^ ( nodeDepth | (innerHashLength << 8) );
            // with nodeDepth = 0; innerHashLength = 0;
            chainValue[3] = IV[3];

            chainValue[4] = IV[4];
            chainValue[5] = IV[5];
            if (m_salt != null)
            {
                chainValue[4] ^= Pack.LE_To_UInt64(m_salt, 0);
                chainValue[5] ^= Pack.LE_To_UInt64(m_salt, 8);
            }

            chainValue[6] = IV[6];
            chainValue[7] = IV[7];
            if (m_personalization != null)
            {
                chainValue[6] ^= Pack.LE_To_UInt64(m_personalization, 0);
                chainValue[7] ^= Pack.LE_To_UInt64(m_personalization, 8);
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
                // block wise 128 bytes without buffer:
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
                // block wise 128 bytes without buffer:
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
            f0 = 0xFFFFFFFFFFFFFFFFUL;
            //if (isLastNode)
            //{
            //    f1 = 0xFFFFFFFFFFFFFFFFUL;
            //}

            if (bufferPos > 0)
            {
                IncrementCounter(bufferPos);
            }

            Compress(buffer, 0);

            int full = digestLength >> 3, partial = digestLength & 7;
            Pack.UInt64_To_LE(chainValue, 0, full, output, outOffset);
            if (partial > 0)
            {
                Pack.UInt64_To_LE_Low(chainValue[full], output, outOffset + digestLength - partial, partial);
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

            f0 = 0xFFFFFFFFFFFFFFFFUL;
            //if (isLastNode)
            //{
            //    f1 = 0xFFFFFFFFFFFFFFFFUL;
            //}

            if (bufferPos > 0)
            {
                IncrementCounter(bufferPos);
            }

            Compress(buffer);

            int full = digestLength >> 3, partial = digestLength & 7;
            Pack.UInt64_To_LE(chainValue.AsSpan(0, full), output);
            if (partial > 0)
            {
                Pack.UInt64_To_LE_Low(chainValue[full], output.Slice(digestLength - partial, partial));
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
            f0 = 0UL;
            t0 = 0UL;
            t1 = 0UL;

            Array.Clear(buffer, 0, buffer.Length);

            Init();
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private void Compress(ReadOnlySpan<byte> message)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Blake2b_X86.IsSupported)
            {
                Blake2b_X86.Compress(chainValue, IV, t0, t1, f0, message);
                return;
            }
#endif

            Span<ulong> m = stackalloc ulong[16];
            Pack.LE_To_UInt64(message, m);

            ulong v0 = chainValue[0];
            ulong v1 = chainValue[1];
            ulong v2 = chainValue[2];
            ulong v3 = chainValue[3];
            ulong v4 = chainValue[4];
            ulong v5 = chainValue[5];
            ulong v6 = chainValue[6];
            ulong v7 = chainValue[7];
            ulong v8 = IV[0];
            ulong v9 = IV[1];
            ulong va = IV[2];
            ulong vb = IV[3];
            ulong vc = IV[4] ^ t0;
            ulong vd = IV[5] ^ t1;
            ulong ve = IV[6] ^ f0;
            ulong vf = IV[7];       // ^ f1, with f1 == 0

            for (int round = 0; round < ROUNDS; round++)
            {
                int pos = round * 16;

                // Apply G to columns of internal state
                G(m[Sigma[pos +  0]], m[Sigma[pos +  1]], ref v0, ref v4, ref v8, ref vc);
                G(m[Sigma[pos +  2]], m[Sigma[pos +  3]], ref v1, ref v5, ref v9, ref vd);
                G(m[Sigma[pos +  4]], m[Sigma[pos +  5]], ref v2, ref v6, ref va, ref ve);
                G(m[Sigma[pos +  6]], m[Sigma[pos +  7]], ref v3, ref v7, ref vb, ref vf);

                // Apply G to diagonals of internal state
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
            ulong[] m = new ulong[16];
            Pack.LE_To_UInt64(message, messagePos, m);

            ulong v0 = chainValue[0];
            ulong v1 = chainValue[1];
            ulong v2 = chainValue[2];
            ulong v3 = chainValue[3];
            ulong v4 = chainValue[4];
            ulong v5 = chainValue[5];
            ulong v6 = chainValue[6];
            ulong v7 = chainValue[7];
            ulong v8 = IV[0];
            ulong v9 = IV[1];
            ulong va = IV[2];
            ulong vb = IV[3];
            ulong vc = IV[4] ^ t0;
            ulong vd = IV[5] ^ t1;
            ulong ve = IV[6] ^ f0;
            ulong vf = IV[7];       // ^ f1, with f1 == 0

            for (int round = 0; round < ROUNDS; round++)
            {
                int pos = round * 16;

                // Apply G to columns of internal state
                G(m[Sigma[pos +  0]], m[Sigma[pos +  1]], ref v0, ref v4, ref v8, ref vc);
                G(m[Sigma[pos +  2]], m[Sigma[pos +  3]], ref v1, ref v5, ref v9, ref vd);
                G(m[Sigma[pos +  4]], m[Sigma[pos +  5]], ref v2, ref v6, ref va, ref ve);
                G(m[Sigma[pos +  6]], m[Sigma[pos +  7]], ref v3, ref v7, ref vb, ref vf);

                // Apply G to diagonals of internal state
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
        public string AlgorithmName => "BLAKE2b";

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

            ulong count64 = (ulong)count;
            t0 += count64;
            if (t0 < count64)
            {
                ++t1;
            }
        }

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static void G(ulong m1, ulong m2, ref ulong a, ref ulong b, ref ulong c, ref ulong d)
        {
            a += b + m1;
            d = Longs.RotateRight(d ^ a, 32);
            c += d;
            b = Longs.RotateRight(b ^ c, 24);

            a += b + m2;
            d = Longs.RotateRight(d ^ a, 16);
            c += d;
            b = Longs.RotateRight(b ^ c, 63);
        }
    }
}
