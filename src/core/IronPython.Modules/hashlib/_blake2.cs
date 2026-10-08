// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_blake2", typeof(IronPython.Modules.PythonBlake2))]
namespace IronPython.Modules {
    public static class PythonBlake2 {
        public const string __doc__ = "BLAKE2b and BLAKE2s hash algorithms";

        public const int BLAKE2B_SALT_SIZE = blake2b.SALT_SIZE;
        public const int BLAKE2B_PERSON_SIZE = blake2b.PERSON_SIZE;
        public const int BLAKE2B_MAX_KEY_SIZE = blake2b.MAX_KEY_SIZE;
        public const int BLAKE2B_MAX_DIGEST_SIZE = blake2b.MAX_DIGEST_SIZE;

        public const int BLAKE2S_SALT_SIZE = blake2s.SALT_SIZE;
        public const int BLAKE2S_PERSON_SIZE = blake2s.PERSON_SIZE;
        public const int BLAKE2S_MAX_KEY_SIZE = blake2s.MAX_KEY_SIZE;
        public const int BLAKE2S_MAX_DIGEST_SIZE = blake2s.MAX_DIGEST_SIZE;

        [PythonType]
        public sealed class blake2b {
            public const int SALT_SIZE = 16;
            public const int PERSON_SIZE = 16;
            public const int MAX_KEY_SIZE = 64;
            public const int MAX_DIGEST_SIZE = 64;
            private const long MAX_NODE_OFFSET = long.MaxValue;

            private readonly Blake2bDigest _digest;

            public blake2b(IBufferProtocol? data = null, int digest_size = MAX_DIGEST_SIZE,
                IBufferProtocol? key = null, IBufferProtocol? salt = null, IBufferProtocol? person = null,
                int fanout = 1, int depth = 1, long leaf_size = 0, long node_offset = 0, int node_depth = 0,
                int inner_size = 0, bool last_node = false) {

                CheckParameters(digest_size, MAX_DIGEST_SIZE, MAX_NODE_OFFSET, fanout, depth, leaf_size, node_offset, node_depth, inner_size, last_node);

                _digest = new Blake2bDigest(
                    GetParameter(key, MAX_KEY_SIZE, "key", pad: false),
                    digest_size,
                    GetParameter(salt, SALT_SIZE, "salt", pad: true),
                    GetParameter(person, PERSON_SIZE, "person", pad: true));

                if (data != null) update(data);
            }

            private blake2b(Blake2bDigest digest) {
                _digest = digest;
            }

            public string name => "blake2b";

            public int digest_size => _digest.GetDigestSize();

            public int block_size => _digest.GetByteLength();

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] IBufferProtocol data) {
                lock (_digest) {
                    HashHelpers.Update(_digest, data);
                }
            }

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] string data) {
                // TODO: error message changes in Python 3.9
                throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
            }

            [Documentation("Return the digest value as a bytes object.")]
            public Bytes digest() {
                var copy = CopyDigest();
                var res = new byte[copy.GetDigestSize()];
                copy.DoFinal(res, 0);
                return Bytes.Make(res);
            }

            [Documentation("Return the digest value as a string of hexadecimal digits.")]
            public string hexdigest() => HashHelpers.ToHex(digest().UnsafeByteArray);

            [Documentation("Return a copy of the hash object.")]
            public blake2b copy() => new blake2b(CopyDigest());

            private Blake2bDigest CopyDigest() {
                lock (_digest) {
                    return new Blake2bDigest(_digest);
                }
            }
        }

        [PythonType]
        public sealed class blake2s {
            public const int SALT_SIZE = 8;
            public const int PERSON_SIZE = 8;
            public const int MAX_KEY_SIZE = 32;
            public const int MAX_DIGEST_SIZE = 32;
            private const long MAX_NODE_OFFSET = (1L << 48) - 1;

            private readonly Blake2sDigest _digest;

            public blake2s(IBufferProtocol? data = null, int digest_size = MAX_DIGEST_SIZE,
                IBufferProtocol? key = null, IBufferProtocol? salt = null, IBufferProtocol? person = null,
                int fanout = 1, int depth = 1, long leaf_size = 0, long node_offset = 0, int node_depth = 0,
                int inner_size = 0, bool last_node = false) {

                CheckParameters(digest_size, MAX_DIGEST_SIZE, MAX_NODE_OFFSET, fanout, depth, leaf_size, node_offset, node_depth, inner_size, last_node);

                _digest = new Blake2sDigest(
                    GetParameter(key, MAX_KEY_SIZE, "key", pad: false),
                    digest_size,
                    GetParameter(salt, SALT_SIZE, "salt", pad: true),
                    GetParameter(person, PERSON_SIZE, "person", pad: true));

                if (data != null) update(data);
            }

            private blake2s(Blake2sDigest digest) {
                _digest = digest;
            }

            public string name => "blake2s";

            public int digest_size => _digest.GetDigestSize();

            public int block_size => _digest.GetByteLength();

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] IBufferProtocol data) {
                lock (_digest) {
                    HashHelpers.Update(_digest, data);
                }
            }

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] string data) {
                throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
            }

            [Documentation("Return the digest value as a bytes object.")]
            public Bytes digest() {
                var copy = CopyDigest();
                var res = new byte[copy.GetDigestSize()];
                copy.DoFinal(res, 0);
                return Bytes.Make(res);
            }

            [Documentation("Return the digest value as a string of hexadecimal digits.")]
            public string hexdigest() => HashHelpers.ToHex(digest().UnsafeByteArray);

            [Documentation("Return a copy of the hash object.")]
            public blake2s copy() => new blake2s(CopyDigest());

            private Blake2sDigest CopyDigest() {
                lock (_digest) {
                    return new Blake2sDigest(_digest);
                }
            }
        }

        private static void CheckParameters(int digest_size, int maxDigestSize, long maxNodeOffset, int fanout, int depth, long leaf_size,
            long node_offset, int node_depth, int inner_size, bool last_node) {

            if (digest_size < 1 || digest_size > maxDigestSize)
                throw PythonOps.ValueError("digest_size must be between 1 and {0} bytes", maxDigestSize);
            if (fanout < 0 || fanout > 255)
                throw PythonOps.ValueError("fanout must be between 0 and 255");
            if (depth < 1 || depth > 255)
                throw PythonOps.ValueError("depth must be between 1 and 255");
            if (leaf_size < 0 || leaf_size > uint.MaxValue)
                throw PythonOps.OverflowError("leaf_size is too large");
            if (node_offset < 0 || node_offset > maxNodeOffset)
                throw PythonOps.OverflowError("node_offset is too large");
            if (node_depth < 0 || node_depth > 255)
                throw PythonOps.ValueError("node_depth must be between 0 and 255");
            if (inner_size < 0 || inner_size > maxDigestSize)
                throw PythonOps.ValueError("inner_size must be between 0 and is {0}", maxDigestSize);

            // The underlying implementation does not support the tree hashing mode.
            if (fanout != 1 || depth != 1 || leaf_size != 0 || node_offset != 0 || node_depth != 0 || inner_size != 0 || last_node)
                throw new NotImplementedException("BLAKE2 tree hashing parameters are not supported");
        }

        private static byte[]? GetParameter(IBufferProtocol? value, int maxSize, string name, bool pad) {
            if (value is null) return null;

            byte[] bytes;
            using (var buffer = value.GetBuffer()) {
                bytes = buffer.ToArray();
            }

            if (bytes.Length > maxSize)
                throw PythonOps.ValueError("maximum {0} length is {1} bytes", name, maxSize);
            if (bytes.Length == 0)
                return null;
            if (pad && bytes.Length < maxSize)
                Array.Resize(ref bytes, maxSize);

            return bytes;
        }
    }
}
