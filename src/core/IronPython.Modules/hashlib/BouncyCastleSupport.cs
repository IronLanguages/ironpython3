// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

// Minimal replacements for the Bouncy Castle utility classes used by the digests in the BouncyCastle folder.
// Only the members needed by those files are implemented. The digest files themselves are kept
// identical to upstream so that they can be refreshed easily (see BouncyCastle/README.md).

using System;
using System.Numerics;

namespace Org.BouncyCastle.Crypto {
    internal static class Check {
        internal static void DataLength(bool condition, string message) {
            if (condition) throw new ArgumentException(message);
        }

        internal static void OutputLength(byte[] buf, int off, int len, string message) {
            if (off > buf.Length - len) throw new ArgumentException(message);
        }

#if NET
        internal static void DataLength<T>(ReadOnlySpan<T> input, int len, string message) {
            if (input.Length < len) throw new ArgumentException(message);
        }

        internal static void OutputLength<T>(Span<T> output, int len, string message) {
            if (output.Length < len) throw new ArgumentException(message);
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

#if NET
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
        internal static byte[] Clone(byte[] data) => (byte[])data?.Clone();

        internal static byte[] CopyBuffer(byte[] buf) => (byte[])buf.Clone();

        internal static void Fill(byte[] buf, byte b) {
            for (int i = 0; i < buf.Length; ++i) {
                buf[i] = b;
            }
        }

        internal static bool IsNullOrEmpty(byte[] array) => array is null || array.Length < 1;
    }

    internal static class Integers {
        internal static uint RotateLeft(uint i, int distance) {
#if NET
            return BitOperations.RotateLeft(i, distance);
#else
            return (i << distance) | (i >> -distance);
#endif
        }

        internal static uint RotateRight(uint i, int distance) {
#if NET
            return BitOperations.RotateRight(i, distance);
#else
            return (i >> distance) | (i << -distance);
#endif
        }
    }

    internal static class Longs {
        internal static ulong RotateLeft(ulong i, int distance) {
#if NET
            return BitOperations.RotateLeft(i, distance);
#else
            return (i << distance) | (i >> -distance);
#endif
        }

        internal static ulong RotateRight(ulong i, int distance) {
#if NET
            return BitOperations.RotateRight(i, distance);
#else
            return (i >> distance) | (i << -distance);
#endif
        }
    }
}
