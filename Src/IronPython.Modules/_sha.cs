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
using System.Security.Cryptography;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

//!!! This is pretty inefficient. We should probably use hasher.TransformBlock instead of
//!!! hanging onto all of the bytes.
//!!! Also, we could probably make a generic version of this that could then be specialized
//!!! for both md5 and sha.

[assembly: PythonModule("_sha", typeof(IronPython.Modules.PythonSha))]
namespace IronPython.Modules {
    public static class PythonSha {
        public const string __doc__ = "implements the SHA1 hash algorithm";

        [ThreadStatic]
        private static SHA1Managed _hasher;
        private const int blockSize = 64;

        private static SHA1Managed GetHasher() {
            if (_hasher == null) {
                _hasher = new SHA1Managed();
            }
            return _hasher;
        }

        public static int digest_size {
            [Documentation("Size of the resulting digest in bytes (constant)")]
            get { return GetHasher().HashSize / 8; }
        }

        public static int digestsize {
            [Documentation("Size of the resulting digest in bytes (constant)")]
            get { return digest_size; }
        }

        public static int blocksize {
            [Documentation("Block size")]
            get { return blockSize; }
        }

        [Documentation("new([data]) -> object (object used to calculate hash)")]
        public static sha @new(object data) {
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
        public class sha
#if FEATURE_ICLONEABLE
            : ICloneable 
#endif
        {
            byte[] _bytes;
            byte[] _hash;
            public static readonly int digest_size = PythonSha.digest_size;
            public static readonly int block_size = PythonSha.blocksize;

            public sha() : this(new byte[0]) { }

            public sha(object initialData) {
                _bytes = new byte[0];
                update(initialData);
            }

            internal sha(IList<byte> initialBytes) {
                _bytes = new byte[0];
                update(initialBytes);
            }

            [Documentation("update(string) -> None (update digest with string data)")]
            public void update(object newData) {
                update(Converter.ConvertToString(newData).MakeByteArray());
            }

            public void update(Bytes newBytes) {
                update((IList<byte>)newBytes);
            }

            public void update(PythonBuffer newBytes) {
                update((IList<byte>)newBytes);
            }

            public void update(ByteArray newBytes) {
                update((IList<byte>)newBytes);
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

            [Documentation("copy() -> object (copy of this object)")]
            public sha copy() {
                return new sha(_bytes);
            }

#if FEATURE_ICLONEABLE
            object ICloneable.Clone() {
                return copy();
            }
#endif
        }
    }
}
