// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Utilities;

namespace IronPython.Modules {

    [PythonHidden]
    public abstract class HashBase {
        private IDigest _digest;

        public readonly string name;
        public readonly int block_size;
        public readonly int digest_size;

        private protected HashBase(string name, IDigest digest) {
            this.name = name;
            _digest = digest;
            block_size = digest.GetByteLength();
            digest_size = digest.GetDigestSize();
        }

        [Documentation("Update this hash object's state with the provided bytes-like object.")]
        public void update([NotNone] IBufferProtocol data) {
            lock (_digest) {
                HashHelpers.Update(_digest, data);
            }
        }

        [Documentation("Update this hash object's state with the provided bytes-like object.")]
        public void update([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return the digest value as a bytes object.")]
        public Bytes digest() {
            IDigest copy = CopyDigest();
            var res = new byte[digest_size];
            copy.DoFinal(res, 0);
            return Bytes.Make(res);
        }

        [Documentation("Return the digest value as a string of hexadecimal digits.")]
        public string hexdigest() => digest().hex();

        [Documentation("Return a copy of the hash object.")]
        public HashBase copy() {
            var res = (HashBase)MemberwiseClone();
            res._digest = CopyDigest();
            return res;
        }

        public object __reduce__([NotNone] params object[] args) => throw HashHelpers.CannotPickleError(name);

        private IDigest CopyDigest() {
            lock (_digest) {
                return CloneDigest(_digest);
            }
        }

        private protected virtual IDigest CloneDigest(IDigest digest) => (IDigest)((IMemoable)digest).Copy();
    }

    internal static class HashHelpers {
        internal static void Update(IDigest digest, IBufferProtocol data) {
            using var buffer = data.GetBuffer();
#if NET
            if (buffer.IsCContiguous()) {
                digest.BlockUpdate(buffer.AsReadOnlySpan());
                return;
            }
#endif
            byte[] bytes = buffer.AsUnsafeArray() ?? buffer.ToArray();
            digest.BlockUpdate(bytes, 0, bytes.Length);
        }

        internal static Exception CannotPickleError(string name) => PythonOps.TypeError($"can't pickle {name} objects");

        internal static Exception StringNotEncodedError()
            // TODO: error message changes in Python 3.9
            => PythonOps.TypeError("Unicode-objects must be encoded before hashing");
    }
}
