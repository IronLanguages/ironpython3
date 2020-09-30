// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

[assembly: PythonModule("_sha1", typeof(IronPython.Modules.PythonSha))]
namespace IronPython.Modules {
    public static class PythonSha {
        public const string __doc__ = "implements the SHA1 hash algorithm";

        private const int DIGEST_SIZE = 20;
        private const int BLOCK_SIZE = 64;

        [Documentation("sha1([data]) -> object (object used to calculate hash)")]
        public static SHA1Type sha1([NotNull] IBufferProtocol data) {
            return new SHA1Type(data);
        }

        public static SHA1Type sha1([NotNull] string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        [Documentation("sha1([data]) -> object (object used to calculate hash)")]
        public static SHA1Type sha1() {
            return new SHA1Type();
        }

        [Documentation("sha1([data]) -> object (object used to calculate hash)")]
        [PythonType("sha1")]
        public sealed class SHA1Type : HashBase<SHA1> {
            internal SHA1Type() : base("sha1", BLOCK_SIZE, DIGEST_SIZE) { }

            internal SHA1Type(IBufferProtocol initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new Mono.Security.Cryptography.SHA1CryptoServiceProvider();
            }

            [Documentation("copy() -> object (copy of this object)")]
            public override HashBase<SHA1> copy() {
                SHA1Type clone = new SHA1Type();
                clone._hasher = CloneHasher();
                return clone;
            }
        }
    }
}
