// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_FULL_CRYPTO // SHA384, SHA512

using System.Collections.Generic;
using System.Security.Cryptography;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_sha512", typeof(IronPython.Modules.PythonSha512))]
namespace IronPython.Modules {
    [Documentation("SHA512 hash algorithm")]
    public static class PythonSha512 {
        private const int BLOCK_SIZE = 128;

        public const string __doc__ = "SHA512 hash algorithm";

        public static Sha512Object sha512(string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static Sha512Object sha512(ArrayModule.array data) {
            return new Sha512Object(data.ToByteArray());
        }

        public static Sha512Object sha512([BytesLike]IList<byte> data) {
            return new Sha512Object(data);
        }
        
        public static Sha512Object sha512() {
            return new Sha512Object();
        }

        public static Sha384Object sha384(string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static Sha384Object sha384(ArrayModule.array data) {
            return new Sha384Object(data.ToByteArray());
        }

        public static Sha384Object sha384([BytesLike]IList<byte> data) {
            return new Sha384Object(data);
        }
        
        public static Sha384Object sha384() {
            return new Sha384Object();
        }

        [PythonHidden]
        public sealed class Sha384Object : HashBase<SHA384> {
            internal Sha384Object() : base("SHA384", BLOCK_SIZE, 48) { }

            internal Sha384Object(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = SHA384.Create();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public override HashBase<SHA384> copy() {
                Sha384Object res = new Sha384Object();
                res._hasher = CloneHasher();
                return res;
            }
        }

        [PythonHidden]
        public sealed class Sha512Object : HashBase<SHA512> {
            internal Sha512Object() : base("SHA512", BLOCK_SIZE, 64) { }

            internal Sha512Object(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = SHA512.Create();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public override HashBase<SHA512> copy() {
                Sha512Object res = new Sha512Object();
                res._hasher = CloneHasher();
                return res;
            }
        }        
    }   
}

#endif
