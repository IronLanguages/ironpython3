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

#if FEATURE_FULL_CRYPTO // SHA384, SHA512

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;

[assembly: PythonModule("_sha512", typeof(IronPython.Modules.PythonSha512))]
namespace IronPython.Modules {
    [Documentation("SHA512 hash algorithm")]
    public static class PythonSha512 {
        private const int BLOCK_SIZE = 128;

        public const string __doc__ = "SHA512 hash algorithm";

        public static Sha512Object sha512(object data) {
            return new Sha512Object(data);
        }

        public static Sha512Object sha512(ArrayModule.array data) {
            return new Sha512Object(data);
        }

        public static Sha512Object sha512(Bytes data) {
            return new Sha512Object((IList<byte>)data);
        }
        
        public static Sha512Object sha512(PythonBuffer data) {
            return new Sha512Object((IList<byte>)data);
        }

        public static Sha512Object sha512(ByteArray data) {
            return new Sha512Object((IList<byte>)data);
        }

        public static Sha512Object sha512() {
            return new Sha512Object();
        }

        public static Sha384Object sha384(object data) {
            return new Sha384Object(data);
        }

        public static Sha384Object sha384(ArrayModule.array data) {
            return new Sha384Object(data);
        }

        public static Sha384Object sha384(Bytes data) {
            return new Sha384Object((IList<byte>)data);
        }

        public static Sha384Object sha384(PythonBuffer data) {
            return new Sha384Object((IList<byte>)data);
        }

        public static Sha384Object sha384(ByteArray data) {
            return new Sha384Object((IList<byte>)data);
        }

        
        public static Sha384Object sha384() {
            return new Sha384Object();
        }

        [PythonHidden]
        public sealed class Sha384Object : HashBase<SHA384> {
            internal Sha384Object() : base("SHA384", BLOCK_SIZE, 48) { }

            internal Sha384Object(object initialData) : this() {
                update(initialData);
            }

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

            internal Sha512Object(object initialData) : this() {
                update(initialData);
            }

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
