// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Security.Cryptography;

using Microsoft.Scripting.Runtime;

using Mono.Security.Cryptography;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_sha256", typeof(IronPython.Modules.PythonSha256))]
namespace IronPython.Modules {
    [Documentation("SHA256 hash algorithm")]
    public static class PythonSha256 {
        private const int BLOCK_SIZE = 64;

        public const string __doc__ = "SHA256 hash algorithm";

        public static Sha256Object sha256(string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static Sha256Object sha256(ArrayModule.array data) {
            return new Sha256Object(data.ToByteArray());
        }

        public static Sha256Object sha256([BytesConversion]IList<byte> data) {
            return new Sha256Object(data);
        }

        public static Sha256Object sha256() {
            return new Sha256Object();
        }

        [PythonHidden]
        public sealed class Sha256Object : HashBase<SHA256> {
            internal Sha256Object() : base("SHA256", BLOCK_SIZE, 32) { }

            internal Sha256Object(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            [Documentation("copy() -> object (copy of this object)")]
            public override HashBase<SHA256> copy() {
                Sha256Object res = new Sha256Object();
                res._hasher = CloneHasher();
                return res;
            }

            protected override void CreateHasher() {
                _hasher = SHA256.Create();
            }
        }

        public static Sha256Object sha224(string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static Sha224Object sha224(ArrayModule.array data) {
            return new Sha224Object(data.ToByteArray());
        }

        public static Sha224Object sha224([BytesConversion]IList<byte> data) {
            return new Sha224Object(data);
        }

        public static Sha224Object sha224() {
            return new Sha224Object();
        }

        [PythonHidden]
        public sealed class Sha224Object : HashBase<SHA224> {
            internal Sha224Object() : base("SHA224", BLOCK_SIZE, 28) {
            }

            internal Sha224Object(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = SHA224.Create();
            }

            [Documentation("copy() -> object (copy of this object)")]
            public override HashBase<SHA224> copy() {
                Sha224Object res = new Sha224Object();
                res._hasher = CloneHasher();
                return res;
            }
        }
    }
}
