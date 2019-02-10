// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_FULL_CRYPTO // MD5

using System.Collections.Generic;
using System.Security.Cryptography;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_md5", typeof(IronPython.Modules.PythonMD5))]
namespace IronPython.Modules {
    public static class PythonMD5 {
        public const string __doc__ = "MD5 hash algorithm";

        private const int DIGEST_SIZE = 16;
        private const int BLOCK_SIZE = 64;

        [Documentation("Size of the resulting digest in bytes (constant)")]
        public static int digest_size {            
            get { return DIGEST_SIZE; }
        }

        public static MD5Object md5(string data) {
            throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
        }

        public static MD5Object md5(ArrayModule.array data) {
            return new MD5Object(data.ToByteArray());
        }

        [Documentation("md5([data]) -> object (new md5 object)")]
        public static MD5Object md5([BytesConversion]IList<byte> data) {
            return new MD5Object(data);
        }

        [Documentation("md5([data]) -> object (new md5 object)")]
        public static MD5Object md5() {
            return new MD5Object();
        }

        [Documentation("md5([data]) -> object (object used to calculate MD5 hash)")]
        [PythonHidden]
        public class MD5Object : HashBase<MD5> {
            public MD5Object() : base("MD5", BLOCK_SIZE, DIGEST_SIZE) { }

            internal MD5Object(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new Mono.Security.Cryptography.MD5CryptoServiceProvider();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public override HashBase<MD5> copy() {
                MD5Object res = new MD5Object();
                res._hasher = CloneHasher();
                return res;
            }          
        }
    }
}

#endif
