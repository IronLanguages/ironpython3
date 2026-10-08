// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

// Minimal replacements for the Bouncy Castle utility classes used by the digests in the BouncyCastle folder.
// Only the members needed by those files are implemented. The digest files themselves are kept
// identical to upstream so that they can be refreshed easily (see BouncyCastle/README.md).

using System;

namespace Org.BouncyCastle.Crypto {
    internal static class Check {
        internal static void DataLength(bool condition, string message) {
            if (condition) throw new ArgumentException(message);
        }

        internal static void OutputLength(byte[] buf, int off, int len, string message) {
            if (off > buf.Length - len) throw new ArgumentException(message);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal static void DataLength<T>(ReadOnlySpan<T> input, int len, string message) {
            if (input.Length < len) throw new ArgumentException(message);
        }

        internal static void OutputLength<T>(Span<T> output, int len, string message) {
            if (output.Length < len) throw new ArgumentException(message);
        }
#endif
    }
}

namespace Org.BouncyCastle.Crypto.Utilities {
    internal static class Pack {
        internal static uint LE_To_UInt32(byte[] bs, int off) {
            return bs[off]
                | (uint)bs[off + 1] << 8
                | (uint)bs[off + 2] << 16
                | (uint)bs[off + 3] << 24;
        }

        internal static void LE_To_UInt32(byte[] bs, int off, uint[] ns) {
            for (int i = 0; i < ns.Length; ++i) {
                ns[i] = LE_To_UInt32(bs, off);
                off += 4;
            }
        }

        internal static ulong LE_To_UInt64(byte[] bs, int off) {
            uint lo = LE_To_UInt32(bs, off);
            uint hi = LE_To_UInt32(bs, off + 4);
            return (ulong)hi << 32 | lo;
        }

        internal static void LE_To_UInt64(byte[] bs, int off, ulong[] ns) {
            for (int i = 0; i < ns.Length; ++i) {
                ns[i] = LE_To_UInt64(bs, off);
                off += 8;
            }
        }

        internal static void UInt32_To_LE(uint n, byte[] bs, int off) {
            bs[off] = (byte)n;
            bs[off + 1] = (byte)(n >> 8);
            bs[off + 2] = (byte)(n >> 16);
            bs[off + 3] = (byte)(n >> 24);
        }

        internal static void UInt32_To_LE(uint[] ns, int nsOff, int nsLen, byte[] bs, int bsOff) {
            for (int i = 0; i < nsLen; ++i) {
                UInt32_To_LE(ns[nsOff + i], bs, bsOff);
                bsOff += 4;
            }
        }

        internal static void UInt32_To_LE_Low(uint n, byte[] bs, int off, int len) {
            for (int i = 0; i < len; ++i) {
                bs[off + i] = (byte)n;
                n >>= 8;
            }
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs, int off) {
            UInt32_To_LE((uint)n, bs, off);
            UInt32_To_LE((uint)(n >> 32), bs, off + 4);
        }

        internal static void UInt64_To_LE(ulong[] ns, int nsOff, int nsLen, byte[] bs, int bsOff) {
            for (int i = 0; i < nsLen; ++i) {
                UInt64_To_LE(ns[nsOff + i], bs, bsOff);
                bsOff += 8;
            }
        }

        internal static void UInt64_To_LE_Low(ulong n, byte[] bs, int off, int len) {
            for (int i = 0; i < len; ++i) {
                bs[off + i] = (byte)n;
                n >>= 8;
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal static void LE_To_UInt32(ReadOnlySpan<byte> bs, Span<uint> ns) {
            for (int i = 0; i < ns.Length; ++i) {
                ns[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bs.Slice(i * 4));
            }
        }

        internal static ulong LE_To_UInt64(ReadOnlySpan<byte> bs) {
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bs);
        }

        internal static void LE_To_UInt64(ReadOnlySpan<byte> bs, Span<ulong> ns) {
            for (int i = 0; i < ns.Length; ++i) {
                ns[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bs.Slice(i * 8));
            }
        }

        internal static void UInt32_To_LE(uint n, Span<byte> bs) {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bs, n);
        }

        internal static void UInt32_To_LE(ReadOnlySpan<uint> ns, Span<byte> bs) {
            for (int i = 0; i < ns.Length; ++i) {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bs.Slice(i * 4), ns[i]);
            }
        }

        internal static void UInt32_To_LE_Low(uint n, Span<byte> bs) {
            for (int i = 0; i < bs.Length; ++i) {
                bs[i] = (byte)n;
                n >>= 8;
            }
        }

        internal static void UInt64_To_LE(ReadOnlySpan<ulong> ns, Span<byte> bs) {
            for (int i = 0; i < ns.Length; ++i) {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bs.Slice(i * 8), ns[i]);
            }
        }

        internal static void UInt64_To_LE_Low(ulong n, Span<byte> bs) {
            for (int i = 0; i < bs.Length; ++i) {
                bs[i] = (byte)n;
                n >>= 8;
            }
        }
#endif
    }
}

namespace Org.BouncyCastle.Math.Raw {
    internal static class Nat {
        internal static void XorTo64(int len, ulong[] x, int xOff, ulong[] z, int zOff) {
            for (int i = 0; i < len; ++i) {
                z[zOff + i] ^= x[xOff + i];
            }
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal static void XorTo64(int len, ReadOnlySpan<ulong> x, Span<ulong> z) {
            for (int i = 0; i < len; ++i) {
                z[i] ^= x[i];
            }
        }
#endif
    }
}

namespace Org.BouncyCastle.Utilities {
    internal static class Arrays {
        internal static byte[] Clone(byte[] data) {
            return data == null ? null : (byte[])data.Clone();
        }

        internal static byte[] CopyBuffer(byte[] buf) {
            return (byte[])buf.Clone();
        }

        internal static void Fill(byte[] buf, byte b) {
            for (int i = 0; i < buf.Length; ++i) {
                buf[i] = b;
            }
        }

        internal static bool IsNullOrEmpty(byte[] array) {
            return array == null || array.Length < 1;
        }
    }

    internal static class Integers {
        internal static uint RotateRight(uint i, int distance) {
            return (i >> distance) | (i << -distance);
        }
    }

    internal static class Longs {
        internal static ulong RotateLeft(ulong i, int distance) {
            return (i << distance) | (i >> -distance);
        }

        internal static ulong RotateRight(ulong i, int distance) {
            return (i >> distance) | (i << -distance);
        }
    }
}

#if NETCOREAPP3_0_OR_GREATER
namespace Org.BouncyCastle.Runtime.Intrinsics {
    internal static class Vector {
        internal static bool IsPackedLittleEndian => BitConverter.IsLittleEndian;
    }
}

namespace Org.BouncyCastle.Runtime.Intrinsics.X86 {
    internal static class Avx2 {
        internal static bool IsEnabled => System.Runtime.Intrinsics.X86.Avx2.IsSupported;
    }

    internal static class Sse41 {
        internal static bool IsEnabled => System.Runtime.Intrinsics.X86.Sse41.IsSupported;
    }
}
#endif
