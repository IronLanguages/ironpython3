using System;
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
using System.Buffers.Binary;
#endif
using System.Diagnostics;
#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Org.BouncyCastle.Crypto.Utilities
{
    internal static class Pack
    {
        internal static void UInt16_To_BE(ushort n, byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt16BigEndian(bs, n);
#else
            bs[0] = (byte)(n >> 8);
            bs[1] = (byte)n;
#endif
        }

        internal static void UInt16_To_BE(ushort n, byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt16BigEndian(bs.AsSpan(off), n);
#else
            bs[off] = (byte)(n >> 8);
            bs[off + 1] = (byte)n;
#endif
        }

        internal static void UInt16_To_BE(ushort[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt16_To_BE(ns[i], bs, off);
                off += 2;
            }
        }

        internal static void UInt16_To_BE(ushort[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt16_To_BE(ns[nsOff + i], bs, bsOff);
                bsOff += 2;
            }
        }

        internal static byte[] UInt16_To_BE(ushort n)
        {
            byte[] bs = new byte[2];
            UInt16_To_BE(n, bs);
            return bs;
        }

        internal static byte[] UInt16_To_BE(ushort[] ns) => UInt16_To_BE(ns, 0, ns.Length);

        internal static byte[] UInt16_To_BE(ushort[] ns, int nsOff, int nsLen)
        {
            byte[] bs = new byte[2 * nsLen];
            UInt16_To_BE(ns, nsOff, nsLen, bs, 0);
            return bs;
        }

        internal static ushort BE_To_UInt16(byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt16BigEndian(bs);
#else
            uint n = (uint)bs[0] << 8
                | bs[1];
            return (ushort)n;
#endif
        }

        internal static ushort BE_To_UInt16(byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt16BigEndian(bs.AsSpan(off));
#else
            uint n = (uint)bs[off] << 8
                | bs[off + 1];
            return (ushort)n;
#endif
        }

        internal static void BE_To_UInt16(byte[] bs, int bsOff, ushort[] ns, int nsOff)
        {
            ns[nsOff] = BE_To_UInt16(bs, bsOff);
        }

        internal static void BE_To_UInt16(byte[] bs, int off, ushort[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt16(bs, off);
                off += 2;
            }
        }

        internal static void BE_To_UInt16(byte[] bs, int bsOff, ushort[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = BE_To_UInt16(bs, bsOff);
                bsOff += 2;
            }
        }

        internal static ushort[] BE_To_UInt16(byte[] bs, int off, int count)
        {
            ushort[] ns = new ushort[count];
            BE_To_UInt16(bs, off, ns);
            return ns;
        }

        internal static void UInt24_To_BE(uint n, byte[] bs)
        {
            bs[0] = (byte)(n >> 16);
            bs[1] = (byte)(n >> 8);
            bs[2] = (byte)n;
        }

        internal static void UInt24_To_BE(uint n, byte[] bs, int off)
        {
            bs[off + 0] = (byte)(n >> 16);
            bs[off + 1] = (byte)(n >> 8);
            bs[off + 2] = (byte)n;
        }

        internal static uint BE_To_UInt24(byte[] bs)
        {
            return (uint)bs[0] << 16
                | (uint)bs[1] << 8
                | bs[2];
        }

        internal static uint BE_To_UInt24(byte[] bs, int off)
        {
            return (uint)bs[off] << 16
                | (uint)bs[off + 1] << 8
                | bs[off + 2];
        }

        internal static void UInt32_To_BE(uint n, byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt32BigEndian(bs, n);
#else
            bs[0] = (byte)(n >> 24);
            bs[1] = (byte)(n >> 16);
            bs[2] = (byte)(n >> 8);
            bs[3] = (byte)n;
#endif
        }

        internal static void UInt32_To_BE(uint n, byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt32BigEndian(bs.AsSpan(off), n);
#else
            bs[off] = (byte)(n >> 24);
            bs[off + 1] = (byte)(n >> 16);
            bs[off + 2] = (byte)(n >> 8);
            bs[off + 3] = (byte)n;
#endif
        }

        internal static void UInt32_To_BE(uint[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_BE(ns[i], bs, off);
                off += 4;
            }
        }

        internal static void UInt32_To_BE(uint[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt32_To_BE(ns[nsOff + i], bs, bsOff);
                bsOff += 4;
            }
        }

        internal static byte[] UInt32_To_BE(uint n)
        {
            byte[] bs = new byte[4];
            UInt32_To_BE(n, bs);
            return bs;
        }

        internal static byte[] UInt32_To_BE(uint[] ns) => UInt32_To_BE(ns, 0, ns.Length);

        internal static byte[] UInt32_To_BE(uint[] ns, int nsOff, int nsLen)
        {
            byte[] bs = new byte[4 * nsLen];
            UInt32_To_BE(ns, nsOff, nsLen, bs, 0);
            return bs;
        }

        internal static void UInt32_To_BE_High(uint n, byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 4);

            int pos = 24;
            bs[off] = (byte)(n >> pos);
            for (int i = 1; i < len; ++i)
            {
                pos -= 8;
                bs[off + i] = (byte)(n >> pos);
            }
        }

        internal static void UInt32_To_BE_Low(uint n, byte[] bs, int off, int len)
        {
            UInt32_To_BE_High(n << ((4 - len) << 3), bs, off, len);
        }

        internal static uint BE_To_UInt32(byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt32BigEndian(bs);
#else
            return (uint)bs[0] << 24
                | (uint)bs[1] << 16
                | (uint)bs[2] << 8
                | bs[3];
#endif
        }

        internal static uint BE_To_UInt32(byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt32BigEndian(bs.AsSpan(off));
#else
            return (uint)bs[off] << 24
                | (uint)bs[off + 1] << 16
                | (uint)bs[off + 2] << 8
                | bs[off + 3];
#endif
        }

        internal static void BE_To_UInt32(byte[] bs, int off, uint[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt32(bs, off);
                off += 4;
            }
        }

        internal static void BE_To_UInt32(byte[] bs, int bsOff, uint[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = BE_To_UInt32(bs, bsOff);
                bsOff += 4;
            }
        }

        internal static uint[] BE_To_UInt32(byte[] bs, int off, int count)
        {
            uint[] ns = new uint[count];
            BE_To_UInt32(bs, off, ns);
            return ns;
        }

        internal static uint BE_To_UInt32_High(byte[] bs, int off, int len)
        {
            return BE_To_UInt32_Low(bs, off, len) << ((4 - len) << 3);
        }

        internal static uint BE_To_UInt32_Low(byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 4);

            uint result = bs[off];
            for (int i = 1; i < len; ++i)
            {
                result <<= 8;
                result |= bs[off + i];
            }
            return result;
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt64BigEndian(bs, n);
#else
            UInt32_To_BE((uint)(n >> 32), bs);
            UInt32_To_BE((uint)n, bs, 4);
#endif
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt64BigEndian(bs.AsSpan(off), n);
#else
            UInt32_To_BE((uint)(n >> 32), bs, off);
            UInt32_To_BE((uint)n, bs, off + 4);
#endif
        }

        internal static void UInt64_To_BE(ulong[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_BE(ns[i], bs, off);
                off += 8;
            }
        }

        internal static void UInt64_To_BE(ulong[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt64_To_BE(ns[nsOff + i], bs, bsOff);
                bsOff += 8;
            }
        }

        internal static byte[] UInt64_To_BE(ulong n)
        {
            byte[] bs = new byte[8];
            UInt64_To_BE(n, bs);
            return bs;
        }

        internal static byte[] UInt64_To_BE(ulong[] ns) => UInt64_To_BE(ns, 0, ns.Length);

        internal static byte[] UInt64_To_BE(ulong[] ns, int nsOff, int nsLen)
        {
            byte[] bs = new byte[8 * nsLen];
            UInt64_To_BE(ns, nsOff, nsLen, bs, 0);
            return bs;
        }

        internal static void UInt64_To_BE_High(ulong n, byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 8);

            int pos = 56;
            bs[off] = (byte)(n >> pos);
            for (int i = 1; i < len; ++i)
            {
                pos -= 8;
                bs[off + i] = (byte)(n >> pos);
            }
        }

        internal static void UInt64_To_BE_Low(ulong n, byte[] bs, int off, int len)
        {
            UInt64_To_BE_High(n << ((8 - len) << 3), bs, off, len);
        }

        internal static ulong BE_To_UInt64(byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt64BigEndian(bs);
#else
            uint hi = BE_To_UInt32(bs);
            uint lo = BE_To_UInt32(bs, 4);
            return ((ulong)hi << 32) | (ulong)lo;
#endif
        }

        internal static ulong BE_To_UInt64(byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt64BigEndian(bs.AsSpan(off));
#else
            uint hi = BE_To_UInt32(bs, off);
            uint lo = BE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | (ulong)lo;
#endif
        }

        internal static void BE_To_UInt64(byte[] bs, int off, ulong[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt64(bs, off);
                off += 8;
            }
        }

        internal static void BE_To_UInt64(byte[] bs, int bsOff, ulong[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = BE_To_UInt64(bs, bsOff);
                bsOff += 8;
            }
        }

        internal static ulong[] BE_To_UInt64(byte[] bs, int off, int count)
        {
            ulong[] ns = new ulong[count];
            BE_To_UInt64(bs, off, ns);
            return ns;
        }

        internal static ulong BE_To_UInt64_High(byte[] bs, int off, int len)
        {
            return BE_To_UInt64_Low(bs, off, len) << ((8 - len) << 3);
        }

        internal static ulong BE_To_UInt64_Low(byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 8);

            ulong result = bs[off];
            for (int i = 1; i < len; ++i)
            {
                result <<= 8;
                result |= bs[off + i];
            }
            return result;
        }

        internal static void UInt16_To_LE(ushort n, byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt16LittleEndian(bs, n);
#else
            bs[0] = (byte)n;
            bs[1] = (byte)(n >> 8);
#endif
        }

        internal static void UInt16_To_LE(ushort n, byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt16LittleEndian(bs.AsSpan(off), n);
#else
            bs[off] = (byte)n;
            bs[off + 1] = (byte)(n >> 8);
#endif
        }

        internal static void UInt16_To_LE(ushort[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt16_To_LE(ns[i], bs, off);
                off += 2;
            }
        }

        internal static void UInt16_To_LE(ushort[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt16_To_LE(ns[nsOff + i], bs, bsOff);
                bsOff += 2;
            }
        }

        internal static byte[] UInt16_To_LE(ushort n)
        {
            byte[] bs = new byte[2];
            UInt16_To_LE(n, bs);
            return bs;
        }

        internal static byte[] UInt16_To_LE(ushort[] ns) => UInt16_To_LE(ns, 0, ns.Length);

        internal static byte[] UInt16_To_LE(ushort[] ns, int nsOff, int nsLen)
        {
            byte[] bs = new byte[2 * nsLen];
            UInt16_To_LE(ns, nsOff, nsLen, bs, 0);
            return bs;
        }

        internal static ushort LE_To_UInt16(byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt16LittleEndian(bs);
#else
            uint n = bs[0]
                | (uint)bs[1] << 8;
            return (ushort)n;
#endif
        }

        internal static ushort LE_To_UInt16(byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt16LittleEndian(bs.AsSpan(off));
#else
            uint n = bs[off]
                | (uint)bs[off + 1] << 8;
            return (ushort)n;
#endif
        }

        internal static void LE_To_UInt16(byte[] bs, int off, ushort[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt16(bs, off);
                off += 2;
            }
        }

        internal static void LE_To_UInt16(byte[] bs, int bsOff, ushort[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = LE_To_UInt16(bs, bsOff);
                bsOff += 2;
            }
        }

        internal static ushort[] LE_To_UInt16(byte[] bs, int off, int count)
        {
            ushort[] ns = new ushort[count];
            LE_To_UInt16(bs, off, ns);
            return ns;
        }

        internal static void UInt32_To_LE(uint n, byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt32LittleEndian(bs, n);
#else
            bs[0] = (byte)n;
            bs[1] = (byte)(n >> 8);
            bs[2] = (byte)(n >> 16);
            bs[3] = (byte)(n >> 24);
#endif
        }

        internal static void UInt32_To_LE(uint n, byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt32LittleEndian(bs.AsSpan(off), n);
#else
            bs[off] = (byte)n;
            bs[off + 1] = (byte)(n >> 8);
            bs[off + 2] = (byte)(n >> 16);
            bs[off + 3] = (byte)(n >> 24);
#endif
        }

        internal static void UInt32_To_LE(uint[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_LE(ns[i], bs, off);
                off += 4;
            }
        }

        internal static void UInt32_To_LE(uint[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt32_To_LE(ns[nsOff + i], bs, bsOff);
                bsOff += 4;
            }
        }

        internal static byte[] UInt32_To_LE(uint n)
        {
            byte[] bs = new byte[4];
            UInt32_To_LE(n, bs);
            return bs;
        }

        internal static byte[] UInt32_To_LE(uint[] ns) => UInt32_To_LE(ns, 0, ns.Length);

        internal static byte[] UInt32_To_LE(uint[] ns, int nsOff, int nsLen)
        {
            byte[] bs = new byte[4 * nsLen];
            UInt32_To_LE(ns, nsOff, nsLen, bs, 0);
            return bs;
        }

        internal static void UInt32_To_LE_High(uint n, byte[] bs, int off, int len)
        {
            UInt32_To_LE_Low(n >> ((4 - len) << 3), bs, off, len);
        }

        internal static void UInt32_To_LE_Low(uint n, byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 4);

            bs[off] = (byte)n;
            for (int i = 1; i < len; ++i)
            {
                n >>= 8;
                bs[off + i] = (byte)n;
            }
        }

        internal static uint LE_To_UInt24(byte[] bs, int off)
        {
            return bs[off]
                | (uint)bs[off + 1] << 8
                | (uint)bs[off + 2] << 16;
        }

        internal static uint LE_To_UInt32(byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt32LittleEndian(bs);
#else
            return bs[0]
                | (uint)bs[1] << 8
                | (uint)bs[2] << 16
                | (uint)bs[3] << 24;
#endif
        }

        internal static uint LE_To_UInt32(byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt32LittleEndian(bs.AsSpan(off));
#else
            return bs[off]
                | (uint)bs[off + 1] << 8
                | (uint)bs[off + 2] << 16
                | (uint)bs[off + 3] << 24;
#endif
        }

        internal static void LE_To_UInt32(byte[] bs, int off, uint[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt32(bs, off);
                off += 4;
            }
        }

        internal static void LE_To_UInt32(byte[] bs, int bsOff, uint[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = LE_To_UInt32(bs, bsOff);
                bsOff += 4;
            }
        }

        internal static uint[] LE_To_UInt32(byte[] bs, int off, int count)
        {
            uint[] ns = new uint[count];
            LE_To_UInt32(bs, off, ns);
            return ns;
        }

        internal static uint LE_To_UInt32_High(byte[] bs, int off, int len)
        {
            return LE_To_UInt32_Low(bs, off, len) << ((4 - len) << 3);
        }

        internal static uint LE_To_UInt32_Low(byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 4);

            uint result = bs[off];
            int pos = 0;
            for (int i = 1; i < len; ++i)
            {
                pos += 8;
                result |= (uint)bs[off + i] << pos;
            }
            return result;
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt64LittleEndian(bs, n);
#else
            UInt32_To_LE((uint)n, bs);
            UInt32_To_LE((uint)(n >> 32), bs, 4);
#endif
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BinaryPrimitives.WriteUInt64LittleEndian(bs.AsSpan(off), n);
#else
            UInt32_To_LE((uint)n, bs, off);
            UInt32_To_LE((uint)(n >> 32), bs, off + 4);
#endif
        }

        internal static void UInt64_To_LE(ulong[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_LE(ns[i], bs, off);
                off += 8;
            }
        }

        internal static void UInt64_To_LE(ulong[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt64_To_LE(ns[nsOff + i], bs, bsOff);
                bsOff += 8;
            }
        }

        internal static byte[] UInt64_To_LE(ulong n)
        {
            byte[] bs = new byte[8];
            UInt64_To_LE(n, bs);
            return bs;
        }

        internal static byte[] UInt64_To_LE(ulong[] ns) => UInt64_To_LE(ns, 0, ns.Length);

        internal static byte[] UInt64_To_LE(ulong[] ns, int nsOff, int nsLen)
        {
            byte[] bs = new byte[8 * nsLen];
            UInt64_To_LE(ns, nsOff, nsLen, bs, 0);
            return bs;
        }

        internal static void UInt64_To_LE_High(ulong n, byte[] bs, int off, int len)
        {
            UInt64_To_LE_Low(n >> ((8 - len) << 3), bs, off, len);
        }

        internal static void UInt64_To_LE_Low(ulong n, byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 8);

            bs[off] = (byte)n;
            for (int i = 1; i < len; ++i)
            {
                n >>= 8;
                bs[off + i] = (byte)n;
            }
        }

        internal static ulong LE_To_UInt64(byte[] bs)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt64LittleEndian(bs);
#else
            uint lo = LE_To_UInt32(bs);
            uint hi = LE_To_UInt32(bs, 4);
            return ((ulong)hi << 32) | (ulong)lo;
#endif
        }

        internal static ulong LE_To_UInt64(byte[] bs, int off)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return BinaryPrimitives.ReadUInt64LittleEndian(bs.AsSpan(off));
#else
            uint lo = LE_To_UInt32(bs, off);
            uint hi = LE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | (ulong)lo;
#endif
        }

        internal static void LE_To_UInt64(byte[] bs, int off, ulong[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt64(bs, off);
                off += 8;
            }
        }

        internal static void LE_To_UInt64(byte[] bs, int bsOff, ulong[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = LE_To_UInt64(bs, bsOff);
                bsOff += 8;
            }
        }

        internal static ulong[] LE_To_UInt64(byte[] bs, int off, int count)
        {
            ulong[] ns = new ulong[count];
            LE_To_UInt64(bs, off, ns);
            return ns;
        }

        internal static ulong LE_To_UInt64_High(byte[] bs, int off, int len)
        {
            return LE_To_UInt64_Low(bs, off, len) << ((8 - len) << 3);
        }

        internal static ulong LE_To_UInt64_Low(byte[] bs, int off, int len)
        {
            Debug.Assert(1 <= len && len <= 8);

            ulong result = bs[off];
            int pos = 0;
            for (int i = 1; i < len; ++i)
            {
                pos += 8;
                result |= (ulong)bs[off + i] << pos;
            }
            return result;
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort BE_To_UInt16(ReadOnlySpan<byte> bs)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort BE_To_UInt16(ReadOnlySpan<byte> bs, int off)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(bs[off..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BE_To_UInt16(ReadOnlySpan<byte> bs, Span<ushort> ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt16(bs);
                bs = bs[2..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint BE_To_UInt32(ReadOnlySpan<byte> bs)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint BE_To_UInt32(ReadOnlySpan<byte> bs, int off)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(bs[off..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BE_To_UInt32(ReadOnlySpan<byte> bs, Span<uint> ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt32(bs);
                bs = bs[4..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint BE_To_UInt32_High(ReadOnlySpan<byte> bs)
        {
            return BE_To_UInt32_Low(bs) << ((4 - bs.Length) << 3);
        }

        internal static uint BE_To_UInt32_Low(ReadOnlySpan<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 4);

            uint result = bs[0];
            for (int i = 1; i < len; ++i)
            {
                result <<= 8;
                result |= bs[i];
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BE_To_UInt64(ReadOnlySpan<byte> bs)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BE_To_UInt64(ReadOnlySpan<byte> bs, int off)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(bs[off..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BE_To_UInt64(ReadOnlySpan<byte> bs, Span<ulong> ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt64(bs);
                bs = bs[8..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BE_To_UInt64_High(ReadOnlySpan<byte> bs)
        {
            return BE_To_UInt64_Low(bs) << ((8 - bs.Length) << 3);
        }

        internal static ulong BE_To_UInt64_Low(ReadOnlySpan<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 8);

            ulong result = bs[0];
            for (int i = 1; i < len; ++i)
            {
                result <<= 8;
                result |= bs[i];
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort LE_To_UInt16(ReadOnlySpan<byte> bs)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort LE_To_UInt16(ReadOnlySpan<byte> bs, int off)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(bs[off..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LE_To_UInt16(ReadOnlySpan<byte> bs, Span<ushort> ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt16(bs);
                bs = bs[2..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint LE_To_UInt24(ReadOnlySpan<byte> bs)
        {
            return bs[0]
                | (uint)bs[1] << 8
                | (uint)bs[2] << 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint LE_To_UInt24(ReadOnlySpan<byte> bs, int off)
        {
            return bs[off]
                | (uint)bs[off + 1] << 8
                | (uint)bs[off + 2] << 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint LE_To_UInt32(ReadOnlySpan<byte> bs)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint LE_To_UInt32(ReadOnlySpan<byte> bs, int off)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(bs[off..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LE_To_UInt32(ReadOnlySpan<byte> bs, Span<uint> ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt32(bs);
                bs = bs[4..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint LE_To_UInt32_High(ReadOnlySpan<byte> bs)
        {
            return LE_To_UInt32_Low(bs) << ((4 - bs.Length) << 3);
        }

        internal static uint LE_To_UInt32_Low(ReadOnlySpan<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 4);

            uint result = bs[0];
            int pos = 0;
            for (int i = 1; i < len; ++i)
            {
                pos += 8;
                result |= (uint)bs[i] << pos;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong LE_To_UInt64(ReadOnlySpan<byte> bs)
        {
            return BinaryPrimitives.ReadUInt64LittleEndian(bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong LE_To_UInt64(ReadOnlySpan<byte> bs, int off)
        {
            return BinaryPrimitives.ReadUInt64LittleEndian(bs[off..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LE_To_UInt64(ReadOnlySpan<byte> bs, Span<ulong> ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt64(bs);
                bs = bs[8..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong LE_To_UInt64_High(ReadOnlySpan<byte> bs)
        {
            return LE_To_UInt64_Low(bs) << ((8 - bs.Length) << 3);
        }

        internal static ulong LE_To_UInt64_Low(ReadOnlySpan<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 8);

            ulong result = bs[0];
            int pos = 0;
            for (int i = 1; i < len; ++i)
            {
                pos += 8;
                result |= (ulong)bs[i] << pos;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt16_To_BE(ushort n, Span<byte> bs)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bs, n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt16_To_BE(ushort n, Span<byte> bs, int off)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bs[off..], n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt16_To_BE(ReadOnlySpan<ushort> ns, Span<byte> bs)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt16_To_BE(ns[i], bs);
                bs = bs[2..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt16_To_LE(ushort n, Span<byte> bs)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bs, n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt16_To_LE(ushort n, Span<byte> bs, int off)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bs[off..], n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt16_To_LE(ReadOnlySpan<ushort> ns, Span<byte> bs)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt16_To_LE(ns[i], bs);
                bs = bs[2..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_BE(uint n, Span<byte> bs)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bs, n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_BE(uint n, Span<byte> bs, int off)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bs[off..], n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_BE(ReadOnlySpan<uint> ns, Span<byte> bs)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_BE(ns[i], bs);
                bs = bs[4..];
            }
        }

        internal static void UInt32_To_BE_High(uint n, Span<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 4);

            int pos = 24;
            bs[0] = (byte)(n >> pos);
            for (int i = 1; i < len; ++i)
            {
                pos -= 8;
                bs[i] = (byte)(n >> pos);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_BE_Low(uint n, Span<byte> bs)
        {
            UInt32_To_BE_High(n << ((4 - bs.Length) << 3), bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_LE(uint n, Span<byte> bs)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bs, n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_LE(uint n, Span<byte> bs, int off)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bs[off..], n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_LE(ReadOnlySpan<uint> ns, Span<byte> bs)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_LE(ns[i], bs);
                bs = bs[4..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_LE_High(uint n, Span<byte> bs)
        {
            UInt32_To_LE_Low(n >> ((4 - bs.Length) << 3), bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt32_To_LE_Low(uint n, Span<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 4);

            bs[0] = (byte)n;
            for (int i = 1; i < len; ++i)
            {
                n >>= 8;
                bs[i] = (byte)n;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_BE(ulong n, Span<byte> bs)
        {
            BinaryPrimitives.WriteUInt64BigEndian(bs, n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_BE(ulong n, Span<byte> bs, int off)
        {
            BinaryPrimitives.WriteUInt64BigEndian(bs[off..], n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_BE(ReadOnlySpan<ulong> ns, Span<byte> bs)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_BE(ns[i], bs);
                bs = bs[8..];
            }
        }

        internal static void UInt64_To_BE_High(ulong n, Span<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 8);

            int pos = 56;
            bs[0] = (byte)(n >> pos);
            for (int i = 1; i < len; ++i)
            {
                pos -= 8;
                bs[i] = (byte)(n >> pos);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_BE_Low(ulong n, Span<byte> bs)
        {
            UInt64_To_BE_High(n << ((8 - bs.Length) << 3), bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_LE(ulong n, Span<byte> bs)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bs, n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_LE(ulong n, Span<byte> bs, int off)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bs[off..], n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_LE(ReadOnlySpan<ulong> ns, Span<byte> bs)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_LE(ns[i], bs);
                bs = bs[8..];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_LE_High(ulong n, Span<byte> bs)
        {
            UInt64_To_LE_Low(n >> ((8 - bs.Length) << 3), bs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UInt64_To_LE_Low(ulong n, Span<byte> bs)
        {
            int len = bs.Length;
            Debug.Assert(1 <= len && len <= 8);

            bs[0] = (byte)n;
            for (int i = 1; i < len; ++i)
            {
                n >>= 8;
                bs[i] = (byte)n;
            }
        }
#endif
    }
}
