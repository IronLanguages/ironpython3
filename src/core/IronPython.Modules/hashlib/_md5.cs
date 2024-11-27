// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_FULL_CRYPTO // MD5

using System.Security.Cryptography;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

[assembly: PythonModule("_md5", typeof(IronPython.Modules.PythonMD5))]
namespace IronPython.Modules {
    public static class PythonMD5 {
        public const string __doc__ = "MD5 hash algorithm";

        private const int DIGEST_SIZE = 16;
        private const int BLOCK_SIZE = 64;

        [Documentation("md5([data]) -> object (new md5 object)")]
        public static MD5Type md5([NotNone] IBufferProtocol data) {
            return new MD5Type(data);
        }

        public static MD5Type md5([NotNone] string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        [Documentation("md5([data]) -> object (new md5 object)")]
        public static MD5Type md5() {
            return new MD5Type();
        }

        [Documentation("md5([data]) -> object (object used to calculate MD5 hash)")]
        [PythonType("md5")]
        public sealed class MD5Type : HashBase<MD5> {
            internal MD5Type() : base("md5", BLOCK_SIZE, DIGEST_SIZE) { }

            internal MD5Type(IBufferProtocol initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new Mono.Security.Cryptography.MD5CryptoServiceProvider();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public override HashBase<MD5> copy() {
                MD5Type res = new MD5Type();
                res._hasher = CloneHasher();
                return res;
            }
        }
    }
}

#endif
