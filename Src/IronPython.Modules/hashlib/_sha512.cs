// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_FULL_CRYPTO // SHA384, SHA512

using System.Security.Cryptography;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

[assembly: PythonModule("_sha512", typeof(IronPython.Modules.PythonSha512))]
namespace IronPython.Modules {
    [Documentation("SHA512 hash algorithm")]
    public static class PythonSha512 {
        private const int BLOCK_SIZE = 128;

        public const string __doc__ = "SHA512 hash algorithm";

        public static SHA384Type sha384([NotNone] IBufferProtocol data) {
            return new SHA384Type(data);
        }

        public static SHA384Type sha384([NotNone] string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static SHA384Type sha384() {
            return new SHA384Type();
        }

        [PythonType("sha384")]
        public sealed class SHA384Type : HashBase<SHA384> {
            internal SHA384Type() : base("sha384", BLOCK_SIZE, 48) { }

            internal SHA384Type(IBufferProtocol initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new Mono.Security.Cryptography.SHA384Managed();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public override HashBase<SHA384> copy() {
                SHA384Type res = new SHA384Type();
                res._hasher = CloneHasher();
                return res;
            }
        }

        public static SHA512Type sha512([NotNone] IBufferProtocol data) {
            return new SHA512Type(data);
        }

        public static SHA512Type sha512([NotNone] string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static SHA512Type sha512() {
            return new SHA512Type();
        }

        [PythonType("sha512")]
        public sealed class SHA512Type : HashBase<SHA512> {
            internal SHA512Type() : base("sha512", BLOCK_SIZE, 64) { }

            internal SHA512Type(IBufferProtocol initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new Mono.Security.Cryptography.SHA512Managed();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public override HashBase<SHA512> copy() {
                SHA512Type res = new SHA512Type();
                res._hasher = CloneHasher();
                return res;
            }
        }
    }
}

#endif
