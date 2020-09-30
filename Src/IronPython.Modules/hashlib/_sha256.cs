// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using Mono.Security.Cryptography;

[assembly: PythonModule("_sha256", typeof(IronPython.Modules.PythonSha256))]
namespace IronPython.Modules {
    [Documentation("SHA256 hash algorithm")]
    public static class PythonSha256 {
        private const int BLOCK_SIZE = 64;

        public const string __doc__ = "SHA256 hash algorithm";

        public static SHA256Type sha256([NotNull] IBufferProtocol data) {
            return new SHA256Type(data);
        }

        public static SHA256Type sha256([NotNull] string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static SHA256Type sha256() {
            return new SHA256Type();
        }

        [PythonType("sha256")]
        public sealed class SHA256Type : HashBase<SHA256> {
            internal SHA256Type() : base("sha256", BLOCK_SIZE, 32) { }

            internal SHA256Type(IBufferProtocol initialBytes) : this() {
                update(initialBytes);
            }

            [Documentation("copy() -> object (copy of this object)")]
            public override HashBase<SHA256> copy() {
                SHA256Type res = new SHA256Type();
                res._hasher = CloneHasher();
                return res;
            }

            protected override void CreateHasher() {
                _hasher = new Mono.Security.Cryptography.SHA256Managed();
            }
        }

        public static SHA224Type sha224([NotNull] IBufferProtocol data) {
            return new SHA224Type(data);
        }

        public static SHA256Type sha224([NotNull] string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static SHA224Type sha224() {
            return new SHA224Type();
        }

        [PythonType("sha224")]
        public sealed class SHA224Type : HashBase<SHA224> {
            internal SHA224Type() : base("sha224", BLOCK_SIZE, 28) {
            }

            internal SHA224Type(IBufferProtocol initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new SHA224Managed();
            }

            [Documentation("copy() -> object (copy of this object)")]
            public override HashBase<SHA224> copy() {
                SHA224Type res = new SHA224Type();
                res._hasher = CloneHasher();
                return res;
            }
        }
    }
}
