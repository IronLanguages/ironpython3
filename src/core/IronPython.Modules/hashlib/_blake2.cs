// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto;
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
        public sealed class blake2b : HashBase {
            public const int SALT_SIZE = 16;
            public const int PERSON_SIZE = 16;
            public const int MAX_KEY_SIZE = 64;
            public const int MAX_DIGEST_SIZE = 64;
            private const long MAX_NODE_OFFSET = long.MaxValue;

            public blake2b([NotNone] IBufferProtocol data = null!, int digest_size = MAX_DIGEST_SIZE,
                IBufferProtocol? key = null, IBufferProtocol? salt = null, IBufferProtocol? person = null,
                int fanout = 1, int depth = 1, long leaf_size = 0, long node_offset = 0, int node_depth = 0,
                int inner_size = 0, bool last_node = false)
                : base("blake2b", CreateDigest(digest_size, key, salt, person, fanout, depth, leaf_size, node_offset, node_depth, inner_size, last_node)) {

                if (data != null) update(data);
            }

            public blake2b([NotNone] string data, int digest_size = MAX_DIGEST_SIZE,
                IBufferProtocol? key = null, IBufferProtocol? salt = null, IBufferProtocol? person = null,
                int fanout = 1, int depth = 1, long leaf_size = 0, long node_offset = 0, int node_depth = 0,
                int inner_size = 0, bool last_node = false) : this()
                => update(data);

            private protected override IDigest CloneDigest(IDigest digest) => new Blake2bDigest((Blake2bDigest)digest);

            private static Blake2bDigest CreateDigest(int digest_size, IBufferProtocol? key, IBufferProtocol? salt, IBufferProtocol? person,
                int fanout, int depth, long leaf_size, long node_offset, int node_depth, int inner_size, bool last_node) {

                CheckParameters(digest_size, MAX_DIGEST_SIZE, MAX_NODE_OFFSET, fanout, depth, leaf_size, node_offset, node_depth, inner_size, last_node);

                return new Blake2bDigest(
                    GetParameter(key, MAX_KEY_SIZE, "key", pad: false),
                    digest_size,
                    GetParameter(salt, SALT_SIZE, "salt", pad: true),
                    GetParameter(person, PERSON_SIZE, "person", pad: true));
            }
        }

        [PythonType]
        public sealed class blake2s : HashBase {
            public const int SALT_SIZE = 8;
            public const int PERSON_SIZE = 8;
            public const int MAX_KEY_SIZE = 32;
            public const int MAX_DIGEST_SIZE = 32;
            private const long MAX_NODE_OFFSET = (1L << 48) - 1;

            public blake2s([NotNone] IBufferProtocol data = null!, int digest_size = MAX_DIGEST_SIZE,
                IBufferProtocol? key = null, IBufferProtocol? salt = null, IBufferProtocol? person = null,
                int fanout = 1, int depth = 1, long leaf_size = 0, long node_offset = 0, int node_depth = 0,
                int inner_size = 0, bool last_node = false)
                : base("blake2s", CreateDigest(digest_size, key, salt, person, fanout, depth, leaf_size, node_offset, node_depth, inner_size, last_node)) {

                if (data != null) update(data);
            }

            public blake2s([NotNone] string data, int digest_size = MAX_DIGEST_SIZE,
                IBufferProtocol? key = null, IBufferProtocol? salt = null, IBufferProtocol? person = null,
                int fanout = 1, int depth = 1, long leaf_size = 0, long node_offset = 0, int node_depth = 0,
                int inner_size = 0, bool last_node = false) : this()
                => update(data);

            private protected override IDigest CloneDigest(IDigest digest) => new Blake2sDigest((Blake2sDigest)digest);

            private static Blake2sDigest CreateDigest(int digest_size, IBufferProtocol? key, IBufferProtocol? salt, IBufferProtocol? person,
                int fanout, int depth, long leaf_size, long node_offset, int node_depth, int inner_size, bool last_node) {

                CheckParameters(digest_size, MAX_DIGEST_SIZE, MAX_NODE_OFFSET, fanout, depth, leaf_size, node_offset, node_depth, inner_size, last_node);

                return new Blake2sDigest(
                    GetParameter(key, MAX_KEY_SIZE, "key", pad: false),
                    digest_size,
                    GetParameter(salt, SALT_SIZE, "salt", pad: true),
                    GetParameter(person, PERSON_SIZE, "person", pad: true));
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

            using var buffer = value.GetBuffer();
            byte[] bytes = buffer.ToArray();

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
