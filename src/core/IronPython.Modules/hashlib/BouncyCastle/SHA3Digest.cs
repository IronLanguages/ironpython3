using System;
using System.Diagnostics;

using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Math.Raw;
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Digests
{
    /// <summary>
    /// Implementation of SHA-3 based on following KeccakNISTInterface.c from http://keccak.noekeon.org/
    /// </summary>
    /// <remarks>
    /// Following the naming conventions used in the C source code to enable easy review of the implementation.
    /// </remarks>
    internal class Sha3Digest
        : KeccakDigest
    {
        internal static void CalculateDigest(ulong[] input, int inputOffset, int inputLengthBits,
            byte[] output, int outputOffset, int outputLengthBits)
        {
            int requiredInputLength = (inputLengthBits + 63) >> 6;
            Check.DataLength(inputOffset > (input.Length - requiredInputLength), "input buffer too short");

            int requiredOutputLength = (outputLengthBits + 7) >> 3;
            Check.OutputLength(output, outputOffset, requiredOutputLength, "output buffer too short");

            // Require byte-alignment (could be improved later)
            if ((inputLengthBits & 7) != 0)
                throw new ArgumentOutOfRangeException(nameof(inputLengthBits));

            switch (outputLengthBits)
            {
            case 224:
            case 256:
            case 384:
            case 512:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outputLengthBits));
            }

            int rate = 1600 - (outputLengthBits << 1);
            int rate64 = rate >> 6;

            ulong[] state = new ulong[25];

            // ABSORB

            while (inputLengthBits >= rate)
            {
                Nat.XorTo64(rate64, input, inputOffset, state, 0);
                inputOffset += rate64;
                inputLengthBits -= rate;

                KeccakPermutation(state);
            }

            int remaining64 = inputLengthBits >> 6;
            int remainingPartial = inputLengthBits & 63;

            Nat.XorTo64(remaining64, input, inputOffset, state, 0);

            // If input not byte-aligned, the padding would be more complicated
            ulong pad = 0b00000110UL;
            if (remainingPartial != 0)
            {
                pad <<= remainingPartial;
                pad |= input[inputOffset + remaining64] & ~(ulong.MaxValue << remainingPartial);
            }

            state[remaining64] ^= pad;
            state[rate64 - 1]  ^= 1UL << 63;

            // SQUEEZE

            KeccakPermutation(state);

            Debug.Assert(outputLengthBits <= rate);
            int count64 = outputLengthBits >> 6;
            Pack.UInt64_To_LE(state, 0, count64, output, outputOffset);
            if ((outputLengthBits & 32) != 0)
            {
                Pack.UInt32_To_LE((uint)state[count64], output, outputOffset + (count64 << 3));
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal static void CalculateDigest(ReadOnlySpan<ulong> input, int inputLengthBits, Span<byte> output,
            int outputLengthBits)
        {
            int requiredInputLength = (inputLengthBits + 63) >> 6;
            Check.DataLength(input, requiredInputLength, "input buffer too short");

            int requiredOutputLength = (outputLengthBits + 7) >> 3;
            Check.OutputLength(output, requiredOutputLength, "output buffer too short");

            // Require byte-alignment (could be improved later)
            if ((inputLengthBits & 7) != 0)
                throw new ArgumentOutOfRangeException(nameof(inputLengthBits));

            switch (outputLengthBits)
            {
            case 224:
            case 256:
            case 384:
            case 512:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outputLengthBits));
            }

            int rate = 1600 - (outputLengthBits << 1);
            int rate64 = rate >> 6;

            Span<ulong> state = stackalloc ulong[25];

            // ABSORB

            while (inputLengthBits >= rate)
            {
                Nat.XorTo64(rate64, input, state);
                input = input[rate64..];
                inputLengthBits -= rate;

                KeccakPermutation(state);
            }

            int remaining64 = inputLengthBits >> 6;
            int remainingPartial = inputLengthBits & 63;

            Nat.XorTo64(remaining64, input, state);

            // If input not byte-aligned, the padding would be more complicated
            ulong pad = 0b00000110UL;
            if (remainingPartial != 0)
            {
                pad <<= remainingPartial;
                pad |= input[remaining64] & ~(ulong.MaxValue << remainingPartial);
            }

            state[remaining64] ^= pad;
            state[rate64 - 1]  ^= 1UL << 63;

            // SQUEEZE

            KeccakPermutation(state);

            Debug.Assert(outputLengthBits <= rate);
            int count64 = outputLengthBits >> 6;
            Pack.UInt64_To_LE(state[..count64], output);
            if ((outputLengthBits & 32) != 0)
            {
                Pack.UInt32_To_LE((uint)state[count64], output[(count64 << 3)..]);
            }
        }
#endif

        private static int CheckBitLength(int bitLength)
        {
            switch (bitLength)
            {
            case 224:
            case 256:
            case 384:
            case 512:
                return bitLength;
            default:
                throw new ArgumentException(bitLength + " not supported for SHA-3", "bitLength");
            }
        }

        public Sha3Digest()
            : this(256)
        {
        }

        public Sha3Digest(int bitLength)
            : base(CheckBitLength(bitLength))
        {
        }

        public Sha3Digest(Sha3Digest source)
            : base(source)
        {
        }

        public override string AlgorithmName
        {
            get { return "SHA3-" + fixedOutputLength; }
        }

        public override int DoFinal(byte[] output, int outOff)
        {
            AbsorbBits(0x02, 2);

            return base.DoFinal(output,  outOff);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override int DoFinal(Span<byte> output)
        {
            AbsorbBits(0x02, 2);

            return base.DoFinal(output);
        }
#endif

        /*
         * TODO Possible API change to support partial-byte suffixes.
         */
        protected override int DoFinal(byte[] output, int outOff, byte partialByte, int partialBits)
        {
            if (partialBits < 0 || partialBits > 7)
                throw new ArgumentException("must be in the range [0,7]", "partialBits");

            int finalInput = (partialByte & ((1 << partialBits) - 1)) | (0x02 << partialBits);
            Debug.Assert(finalInput >= 0);
            int finalBits = partialBits + 2;

            if (finalBits >= 8)
            {
                Absorb((byte)finalInput);
                finalBits -= 8;
                finalInput >>= 8;
            }

            return base.DoFinal(output, outOff, (byte)finalInput, finalBits);
        }

        public override IMemoable Copy()
		{
			return new Sha3Digest(this);
		}
    }
}
