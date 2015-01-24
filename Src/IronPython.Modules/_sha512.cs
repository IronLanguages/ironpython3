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

//!!! This is pretty inefficient. We should probably use hasher.TransformBlock instead of
//!!! hanging onto all of the bytes.
//!!! Also, we could probably make a generic version of this that could then be specialized
//!!! for both md5 and sha.

[assembly: PythonModule("_sha512", typeof(IronPython.Modules.PythonSha512))]
namespace IronPython.Modules {
    [Documentation("SHA512 hash algorithm")]
    public static class PythonSha512 {
        [ThreadStatic]
        private static SHA512 _hasher512;
        [ThreadStatic]
        private static SHA384 _hasher384;

        private const int blockSize = 128;

        private static SHA512 GetHasher512() {
            if (_hasher512 == null) {
                _hasher512 = SHA512Managed.Create();
            }
            return _hasher512;
        }

        private static SHA384 GetHasher384() {
            if (_hasher384 == null) {
                _hasher384 = SHA384Managed.Create();
            }
            return _hasher384;
        }

        public const string __doc__ = "SHA512 hash algorithm";

        public static Sha512Object sha512(object data) {
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
        public sealed class Sha384Object : HashBase, ICloneable {
            internal Sha384Object() : this(new byte[0]) { }

            internal Sha384Object(object initialData) {
                _bytes = new byte[0];
                update(initialData);
            }

            internal Sha384Object(IList<byte> initialBytes) {
                _bytes = new byte[0];
                update(initialBytes);
            }

            internal override HashAlgorithm Hasher {
                get {
                    return GetHasher384();
                }
            }

            [Documentation("copy() -> object (copy of this object)")]
            public Sha384Object copy() {
                return new Sha384Object(_bytes);
            }

            object ICloneable.Clone() {
                return copy();
            }

            public const int block_size = 128;
            public const int digest_size = 48;
            public const int digestsize = 48;
            public const string name = "SHA384";
        }

        [PythonHidden]
        public sealed class Sha512Object : HashBase, ICloneable {
            internal Sha512Object() : this(new byte[0]) { }

            internal Sha512Object(object initialData) {
                _bytes = new byte[0];
                update(initialData);
            }

            internal Sha512Object(IList<byte> initialBytes) {
                _bytes = new byte[0];
                update(initialBytes);
            }

            internal override HashAlgorithm Hasher {
                get {
                    return GetHasher512();
                }
            }

            [Documentation("copy() -> object (copy of this object)")]
            public Sha512Object copy() {
                return new Sha512Object(_bytes);
            }

            object ICloneable.Clone() {
                return copy();
            }

            public const int block_size = 128;
            public const int digest_size = 64;
            public const int digestsize = 64;
            public const string name = "SHA512";
        }        
    }   
}
#endif
