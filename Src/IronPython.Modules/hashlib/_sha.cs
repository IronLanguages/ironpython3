/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_sha", typeof(IronPython.Modules.PythonSha))]
namespace IronPython.Modules {
    public static class PythonSha {
        public const string __doc__ = "implements the SHA1 hash algorithm";

        private const int DIGEST_SIZE = 20;
        private const int BLOCK_SIZE = 64;

        private static readonly Encoding _raw = Encoding.GetEncoding("iso-8859-1");
        private static readonly byte[] _empty = _raw.GetBytes(string.Empty);

        public static int digest_size {
            [Documentation("Size of the resulting digest in bytes (constant)")]
            get { return DIGEST_SIZE; }
        }

        public static int digestsize {
            [Documentation("Size of the resulting digest in bytes (constant)")]
            get { return digest_size; }
        }

        public static int blocksize {
            [Documentation("Block size")]
            get { return BLOCK_SIZE; }
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new(object data) {
            return new sha(data);
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new(ArrayModule.array data) {
            return new sha(data);
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new(Bytes data) {
            return new sha((IList<byte>)data);
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new(PythonBuffer data) {
            return new sha(data);
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new(ByteArray data) {
            return new sha((IList<byte>)data);
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new() {
            return new sha();
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        [PythonType, PythonHidden]
        public class sha : HashBase<SHA1>
        {
            public sha() : base("SHA1", BLOCK_SIZE, DIGEST_SIZE) { }

            public sha(object initialData) : this() {
                update(initialData);
            }

            internal sha(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = new SHA1Managed();
            }

            [Documentation("copy() -> object (copy of this object)")]
            public override HashBase<SHA1> copy() {
                sha clone = new sha();
                clone._hasher = CloneHasher();
                return clone;
            }
        }
    }
}
