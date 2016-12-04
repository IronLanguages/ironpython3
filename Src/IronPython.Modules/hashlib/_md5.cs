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

#if FEATURE_FULL_CRYPTO // MD5

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Scripting.Runtime;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Object @new(object data) {
            return new MD5Object(data);
        }

        public static MD5Object @new(ArrayModule.array data) {
            return new MD5Object(data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Object @new(Bytes data) {
            return new MD5Object((IList<byte>)data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Object @new(PythonBuffer data) {
            return new MD5Object(data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Object @new(ByteArray data) {
            return new MD5Object((IList<byte>)data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Object @new() {
            return new MD5Object();
        }

        [Documentation("new([data]) -> object (object used to calculate MD5 hash)")]
        [PythonHidden]
        public class MD5Object : HashBase<MD5> {
            public MD5Object() : base("MD5", BLOCK_SIZE, DIGEST_SIZE) { }

            public MD5Object(object initialData) : this() {
                update(initialData);
            }

            internal MD5Object(IList<byte> initialBytes) : this() {
                update(initialBytes);
            }

            protected override void CreateHasher() {
                _hasher = MD5.Create();
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