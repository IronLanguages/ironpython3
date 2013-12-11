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
using Microsoft.Scripting.Runtime;
using System.Security.Cryptography;
using System.Text;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

//!!! This is pretty inefficient. We should probably use hasher.TransformBlock instead of
//!!! hanging onto all of the bytes.
//!!! Also, we could probably make a generic version of this that could then be specialized
//!!! for both md5 and sha.

[assembly: PythonModule("_md5", typeof(IronPython.Modules.PythonMD5))]
namespace IronPython.Modules {
    public static class PythonMD5 {
        public const string __doc__ = "MD5 hash algorithm";

        [ThreadStatic]
        private static MD5CryptoServiceProvider _hasher;

        private static MD5CryptoServiceProvider GetHasher() {
            if (_hasher == null) {
                _hasher = new MD5CryptoServiceProvider();
            }
            return _hasher;
        }

        public static int digest_size {
            [Documentation("Size of the resulting digest in bytes (constant)")]
            get { return GetHasher().HashSize / 8; }
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Type @new(object data) {
            return new MD5Type(data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Type @new(Bytes data) {
            return new MD5Type((IList<byte>)data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Type @new(PythonBuffer data) {
            return new MD5Type(data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Type @new(ByteArray data) {
            return new MD5Type((IList<byte>)data);
        }

        [Documentation("new([data]) -> object (new md5 object)")]
        public static MD5Type @new() {
            return new MD5Type();
        }

        [Documentation("new([data]) -> object (object used to calculate MD5 hash)")]
        [PythonType]
        public class MD5Type : ICloneable {
            private byte[] _bytes;
            private byte[] _hash;
            public const int digest_size = 16, digestsize = 16;

            public MD5Type() : this(new byte[0]) { }

            public MD5Type(object initialData) {                
                _bytes = new byte[0];
                update(initialData);
            }

            public string name {
                get {
                    return "MD5";
                }
            }

            public int block_size {
                get {
                    return GetHasher().InputBlockSize;
                }
            }

            internal MD5Type(IList<byte> initialBytes) {
                _bytes = new byte[0];
                update(initialBytes);
            }

            [Documentation("update(string) -> None (update digest with string data)")]
            public void update(object newData) {
                update(Converter.ConvertToString(newData).MakeByteArray());
            }

            [Documentation("update(bytes) -> None (update digest with string data)")]
            public void update(Bytes newData) {
                update((IList<byte>)newData);
            }

            [Documentation("update(bytes) -> None (update digest with string data)")]
            public void update(ByteArray newData) {
                update((IList<byte>)newData);
            }

            [Documentation("update(bytes) -> None (update digest with string data)")]
            public void update(PythonBuffer newData) {
                update((IList<byte>)newData);
            }

            private void update(IList<byte> newBytes) {
                byte[] updatedBytes = new byte[_bytes.Length + newBytes.Count];
                Array.Copy(_bytes, updatedBytes, _bytes.Length);
                newBytes.CopyTo(updatedBytes, _bytes.Length);
                _bytes = updatedBytes;
                _hash = GetHasher().ComputeHash(_bytes);
            }

            [Documentation("digest() -> int (current digest value)")]
            public string digest() {
                return _hash.MakeString();
            }

            [Documentation("hexdigest() -> string (current digest as hex digits)")]
            public string hexdigest() {
                StringBuilder result = new StringBuilder(2 * _hash.Length);
                for (int i = 0; i < _hash.Length; i++) {
                    result.Append(_hash[i].ToString("x2"));
                }
                return result.ToString();
            }

            [Documentation("copy() -> object (copy of this md5 object)")]
            public MD5Type copy() {
                return new MD5Type(_bytes);
            }

            object ICloneable.Clone() {
                return copy();
            }
        }
    }
}

#endif