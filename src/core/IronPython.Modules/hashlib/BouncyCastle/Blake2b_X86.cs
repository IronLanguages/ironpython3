#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Org.BouncyCastle.Crypto.Digests
{
    // License from the original code created by Clinton Ingram (saucecontrol) for Blake2Fast 
    // at https://github.com/saucecontrol/Blake2Fast. The code has been copied and modified.

    // The MIT License

    // Copyright(c) 2018-2021 Clinton Ingram

    // Permission is hereby granted, free of charge, to any person obtaining a copy
    // of this software and associated documentation files (the "Software"), to deal
    // in the Software without restriction, including without limitation the rights
    // to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    // copies of the Software, and to permit persons to whom the Software is
    // furnished to do so, subject to the following conditions:

    // The above copyright notice and this permission notice shall be included in
    // all copies or substantial portions of the Software.

    // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    // THE SOFTWARE.

    internal static class Blake2b_X86
    {
        internal static bool IsSupported =>
            Org.BouncyCastle.Runtime.Intrinsics.X86.Avx2.IsEnabled &&
            Org.BouncyCastle.Runtime.Intrinsics.Vector.IsPackedLittleEndian;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Compress(Span<ulong> hashBuffer, ReadOnlySpan<ulong> blakeIV, ulong t0, ulong t1, ulong f0,
            ReadOnlySpan<byte> message)
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException(nameof(Blake2b_X86));

            Debug.Assert(hashBuffer.Length >= 8);
            Debug.Assert(blakeIV.Length >= 8);
            Debug.Assert(message.Length >= 128);

            var hashBytes = MemoryMarshal.AsBytes(hashBuffer);
            var ivBytes = MemoryMarshal.AsBytes(blakeIV);

            var t_0 = Vector256.Create(t0, t1, f0, 0);

            var row1 = MemoryMarshal.Read<Vector256<ulong>>(hashBytes);
            var row2 = MemoryMarshal.Read<Vector256<ulong>>(hashBytes[32..]);
            var row3 = MemoryMarshal.Read<Vector256<ulong>>(ivBytes);
            var row4 = MemoryMarshal.Read<Vector256<ulong>>(ivBytes[32..]);
            row4 = Avx2.Xor(row4, t_0);

            var orig_1 = row1;
            var orig_2 = row2;

            Perform12Rounds(message, ref row1, ref row2, ref row3, ref row4);

            row1 = Avx2.Xor(row1, row3);
            row2 = Avx2.Xor(row2, row4);
            row1 = Avx2.Xor(row1, orig_1);
            row2 = Avx2.Xor(row2, orig_2);

#if NET8_0_OR_GREATER
            MemoryMarshal.Write(hashBytes, in row1);
            MemoryMarshal.Write(hashBytes[32..], in row2);
#else
            MemoryMarshal.Write(hashBytes, ref row1);
            MemoryMarshal.Write(hashBytes[32..], ref row2);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Perform12Rounds(ReadOnlySpan<byte> m, ref Vector256<ulong> row1, ref Vector256<ulong> row2, ref Vector256<ulong> row3, ref Vector256<ulong> row4)
        {
#region Rounds
            //ROUND 1
            var m0 = Broadcast128ToVector256<ulong>(m);
            var m1 = Broadcast128ToVector256<ulong>(m[16..]);
            var m2 = Broadcast128ToVector256<ulong>(m[32..]);
            var m3 = Broadcast128ToVector256<ulong>(m[48..]);

            var t0 = Avx2.UnpackLow(m0, m1);
            var t1 = Avx2.UnpackLow(m2, m3);
            var b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m0, m1);
            t1 = Avx2.UnpackHigh(m2, m3);
            var b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            var m4 = Broadcast128ToVector256<ulong>(m[64..]);
            var m5 = Broadcast128ToVector256<ulong>(m[80..]);
            var m6 = Broadcast128ToVector256<ulong>(m[96..]);
            var m7 = Broadcast128ToVector256<ulong>(m[112..]);

            t0 = Avx2.UnpackLow(m7, m4);
            t1 = Avx2.UnpackLow(m5, m6);
            var b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m7, m4);
            t1 = Avx2.UnpackHigh(m5, m6);
            var b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 2
            t0 = Avx2.UnpackLow(m7, m2);
            t1 = Avx2.UnpackHigh(m4, m6);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m5, m4);
            t1 = Avx2.AlignRight(m3, m7, 8);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m2, m0);
            t1 = Avx2.Blend(m0.AsUInt32(), m5.AsUInt32(), 0b_1100_1100).AsUInt64();
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m6, m1, 8);
            t1 = Avx2.Blend(m1.AsUInt32(), m3.AsUInt32(), 0b_1100_1100).AsUInt64();
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 3
            t0 = Avx2.AlignRight(m6, m5, 8);
            t1 = Avx2.UnpackHigh(m2, m7);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m4, m0);
            t1 = Avx2.Blend(m1.AsUInt32(), m6.AsUInt32(), 0b_1100_1100).AsUInt64();
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m5, m4, 8);
            t1 = Avx2.UnpackHigh(m1, m3);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m2, m7);
            t1 = Avx2.Blend(m3.AsUInt32(), m0.AsUInt32(), 0b_1100_1100).AsUInt64();
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 4
            t0 = Avx2.UnpackHigh(m3, m1);
            t1 = Avx2.UnpackHigh(m6, m5);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m4, m0);
            t1 = Avx2.UnpackLow(m6, m7);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m1, m7, 8);
            t1 = Avx2.Shuffle(m2.AsUInt32(), 0b_01_00_11_10).AsUInt64();
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m4, m3);
            t1 = Avx2.UnpackLow(m5, m0);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 5
            t0 = Avx2.UnpackHigh(m4, m2);
            t1 = Avx2.UnpackLow(m1, m5);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.Blend(m0.AsUInt32(), m3.AsUInt32(), 0b_1100_1100).AsUInt64();
            t1 = Avx2.Blend(m2.AsUInt32(), m7.AsUInt32(), 0b_1100_1100).AsUInt64();
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m7, m1, 8);
            t1 = Avx2.AlignRight(m3, m5, 8);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m6, m0);
            t1 = Avx2.UnpackLow(m6, m4);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 6
            t0 = Avx2.UnpackLow(m1, m3);
            t1 = Avx2.UnpackLow(m0, m4);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m6, m5);
            t1 = Avx2.UnpackHigh(m5, m1);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m2, m0, 8);
            t1 = Avx2.UnpackHigh(m3, m7);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m4, m6);
            t1 = Avx2.AlignRight(m7, m2, 8);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 7
            t0 = Avx2.Blend(m6.AsUInt32(), m0.AsUInt32(), 0b_1100_1100).AsUInt64();
            t1 = Avx2.UnpackLow(m7, m2);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m2, m7);
            t1 = Avx2.AlignRight(m5, m6, 8);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m4, m0);
            t1 = Avx2.Blend(m3.AsUInt32(), m4.AsUInt32(), 0b_1100_1100).AsUInt64();
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m5, m3);
            t1 = Avx2.Shuffle(m1.AsUInt32(), 0b_01_00_11_10).AsUInt64();
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 8
            t0 = Avx2.UnpackHigh(m6, m3);
            t1 = Avx2.Blend(m6.AsUInt32(), m1.AsUInt32(), 0b_1100_1100).AsUInt64();
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m7, m5, 8);
            t1 = Avx2.UnpackHigh(m0, m4);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.Blend(m1.AsUInt32(), m2.AsUInt32(), 0b_1100_1100).AsUInt64();
            t1 = Avx2.AlignRight(m4, m7, 8);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m5, m0);
            t1 = Avx2.UnpackLow(m2, m3);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 9
            t0 = Avx2.UnpackLow(m3, m7);
            t1 = Avx2.AlignRight(m0, m5, 8);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m7, m4);
            t1 = Avx2.AlignRight(m4, m1, 8);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m5, m6);
            t1 = Avx2.UnpackHigh(m6, m0);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m1, m2, 8);
            t1 = Avx2.AlignRight(m2, m3, 8);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 10
            t0 = Avx2.UnpackLow(m5, m4);
            t1 = Avx2.UnpackHigh(m3, m0);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m1, m2);
            t1 = Avx2.Blend(m3.AsUInt32(), m2.AsUInt32(), 0b_1100_1100).AsUInt64();
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m6, m7);
            t1 = Avx2.UnpackHigh(m4, m1);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.Blend(m0.AsUInt32(), m5.AsUInt32(), 0b_1100_1100).AsUInt64();
            t1 = Avx2.UnpackLow(m7, m6);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 11
            t0 = Avx2.UnpackLow(m0, m1);
            t1 = Avx2.UnpackLow(m2, m3);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m0, m1);
            t1 = Avx2.UnpackHigh(m2, m3);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m7, m4);
            t1 = Avx2.UnpackLow(m5, m6);
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m7, m4);
            t1 = Avx2.UnpackHigh(m5, m6);
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);

            //ROUND 12
            t0 = Avx2.UnpackLow(m7, m2);
            t1 = Avx2.UnpackHigh(m4, m6);
            b1 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackLow(m5, m4);
            t1 = Avx2.AlignRight(m3, m7, 8);
            b2 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.UnpackHigh(m2, m0);
            t1 = Avx2.Blend(m0.AsUInt32(), m5.AsUInt32(), 0b_1100_1100).AsUInt64();
            b3 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            t0 = Avx2.AlignRight(m6, m1, 8);
            t1 = Avx2.Blend(m1.AsUInt32(), m3.AsUInt32(), 0b_1100_1100).AsUInt64();
            b4 = Avx2.Blend(t0.AsUInt32(), t1.AsUInt32(), 0b_1111_0000).AsUInt64();

            Round(ref row1, ref row2, ref row3, ref row4, b1, b2, b3, b4);
#endregion
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Round(ref Vector256<ulong> row1, ref Vector256<ulong> row2, ref Vector256<ulong> row3,
            ref Vector256<ulong> row4, Vector256<ulong> b1, Vector256<ulong> b2, Vector256<ulong> b3,
            Vector256<ulong> b4)
        {
            Vector256<byte> r24 = Vector256.Create(
                (byte)3, 4, 5, 6, 7, 0, 1, 2, 11, 12, 13, 14, 15, 8, 9, 10, 3, 4, 5, 6, 7, 0, 1, 2, 11, 12, 13, 14, 15, 8, 9, 10);
            Vector256<byte> r16 = Vector256.Create(
                (byte)2, 3, 4, 5, 6, 7, 0, 1, 10, 11, 12, 13, 14, 15, 8, 9, 2, 3, 4, 5, 6, 7, 0, 1, 10, 11, 12, 13, 14, 15, 8, 9);

            G1(r24, ref row1, ref row2, ref row3, ref row4, b1);
            G2(r16, ref row1, ref row2, ref row3, ref row4, b2);

            Diagonalize(ref row1, ref row3, ref row4);

            G1(r24, ref row1, ref row2, ref row3, ref row4, b3);
            G2(r16, ref row1, ref row2, ref row3, ref row4, b4);

            Diagonalize(ref row3, ref row1, ref row4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Diagonalize(ref Vector256<ulong> row1, ref Vector256<ulong> row3, ref Vector256<ulong> row4)
        {
            //     +-------------------+        +-------------------+
            //     |  0 |  1 |  2 |  3 |        |  3 |  0 |  1 |  2 |
            //     +-------------------+        +-------------------+
            //     |  8 |  9 | 10 | 11 |  --->  |  9 | 10 | 11 |  8 |
            //     +-------------------+        +-------------------+
            //     | 12 | 13 | 14 | 15 |        | 14 | 15 | 12 | 13 |
            //     +-------------------+        +-------------------+

            row1 = Avx2.Permute4x64(row1, 0b_10_01_00_11);
            row3 = Avx2.Permute4x64(row3, 0b_00_11_10_01);
            row4 = Avx2.Permute4x64(row4, 0b_01_00_11_10);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void G1(Vector256<byte> r24, ref Vector256<ulong> row1, ref Vector256<ulong> row2,
            ref Vector256<ulong> row3, ref Vector256<ulong> row4, Vector256<ulong> b0)
        {
            row1 = Avx2.Add(Avx2.Add(row1, b0), row2);
            row4 = Avx2.Xor(row4, row1);
            row4 = Avx2.Shuffle(row4.AsUInt32(), 0b_10_11_00_01).AsUInt64();

            row3 = Avx2.Add(row3, row4);
            row2 = Avx2.Xor(row2, row3);
            row2 = Avx2.Shuffle(row2.AsByte(), r24).AsUInt64();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void G2(Vector256<byte> r16, ref Vector256<ulong> row1, ref Vector256<ulong> row2,
            ref Vector256<ulong> row3, ref Vector256<ulong> row4, Vector256<ulong> b0)
        {
            row1 = Avx2.Add(Avx2.Add(row1, b0), row2);
            row4 = Avx2.Xor(row4, row1);
            row4 = Avx2.Shuffle(row4.AsByte(), r16).AsUInt64();

            row3 = Avx2.Add(row3, row4);
            row2 = Avx2.Xor(row2, row3);
            row2 = Avx2.Xor(Avx2.ShiftRightLogical(row2, 63), Avx2.Add(row2, row2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<T> Broadcast128ToVector256<T>(ReadOnlySpan<byte> source) where T : struct
        {
            var vector = MemoryMarshal.Read<Vector128<T>>(source);
            Vector256<T> result = vector.ToVector256Unsafe();
            return result.WithUpper(vector);
        }
    }
}
#endif
