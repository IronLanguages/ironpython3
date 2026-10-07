// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_sha3", typeof(IronPython.Modules.PythonSha3))]
namespace IronPython.Modules {
    public static class PythonSha3 {
        public const string __doc__ = "SHA-3 and SHAKE hash algorithms";

        [PythonHidden]
        public abstract class Sha3Base {
            private protected readonly KeccakDigest _digest;

            private protected Sha3Base(KeccakDigest digest) {
                _digest = digest;
            }

            public abstract string name { get; }

            public int block_size => _digest.GetByteLength();

            public int _rate_bits => _digest.GetByteLength() * 8;

            public int _capacity_bits => 1600 - _rate_bits;

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] IBufferProtocol data) {
                lock (_digest) {
                    HashHelpers.Update(_digest, data);
                }
            }

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] string data) {
                throw PythonOps.TypeError("Unicode-objects must be encoded before hashing");
            }

            private protected T CopyDigest<T>() where T : KeccakDigest {
                lock (_digest) {
                    return (T)_digest.Copy();
                }
            }
        }

        [PythonHidden]
        public abstract class Sha3FixedBase : Sha3Base {
            private protected Sha3FixedBase(int bitLength) : base(new Sha3Digest(bitLength)) { }

            private protected Sha3FixedBase(Sha3Digest digest) : base(digest) { }

            public int digest_size => _digest.GetDigestSize();

            public Bytes _suffix => Bytes.Make([0x06]);

            [Documentation("Return the digest value as a bytes object.")]
            public Bytes digest() {
                var copy = CopyDigest<Sha3Digest>();
                var res = new byte[copy.GetDigestSize()];
                copy.DoFinal(res, 0);
                return Bytes.Make(res);
            }

            [Documentation("Return the digest value as a string of hexadecimal digits.")]
            public string hexdigest() => HashHelpers.ToHex(digest().UnsafeByteArray);
        }

        [PythonHidden]
        public abstract class ShakeBase : Sha3Base {
            private protected ShakeBase(int bitLength) : base(new ShakeDigest(bitLength)) { }

            private protected ShakeBase(ShakeDigest digest) : base(digest) { }

            public int digest_size => 0;

            public Bytes _suffix => Bytes.Make([0x1f]);

            [Documentation("Return the digest value as a bytes object.")]
            public Bytes digest(int length) {
                if (length < 0) throw PythonOps.ValueError("length must be non-negative");
                if (length >= 1 << 29) throw PythonOps.ValueError("length is too large");
                var copy = CopyDigest<ShakeDigest>();
                var res = new byte[length];
                copy.OutputFinal(res, 0, length);
                return Bytes.Make(res);
            }

            [Documentation("Return the digest value as a string of hexadecimal digits.")]
            public string hexdigest(int length) => HashHelpers.ToHex(digest(length).UnsafeByteArray);
        }

        [PythonType]
        public sealed class sha3_224 : Sha3FixedBase {
            public sha3_224() : base(224) { }
            public sha3_224([NotNone] IBufferProtocol data) : this() { update(data); }
            public sha3_224([NotNone] string data) : this() { update(data); }
            private sha3_224(Sha3Digest digest) : base(digest) { }

            public override string name => "sha3_224";

            [Documentation("Return a copy of the hash object.")]
            public sha3_224 copy() => new sha3_224(CopyDigest<Sha3Digest>());
        }

        [PythonType]
        public sealed class sha3_256 : Sha3FixedBase {
            public sha3_256() : base(256) { }
            public sha3_256([NotNone] IBufferProtocol data) : this() { update(data); }
            public sha3_256([NotNone] string data) : this() { update(data); }
            private sha3_256(Sha3Digest digest) : base(digest) { }

            public override string name => "sha3_256";

            [Documentation("Return a copy of the hash object.")]
            public sha3_256 copy() => new sha3_256(CopyDigest<Sha3Digest>());
        }

        [PythonType]
        public sealed class sha3_384 : Sha3FixedBase {
            public sha3_384() : base(384) { }
            public sha3_384([NotNone] IBufferProtocol data) : this() { update(data); }
            public sha3_384([NotNone] string data) : this() { update(data); }
            private sha3_384(Sha3Digest digest) : base(digest) { }

            public override string name => "sha3_384";

            [Documentation("Return a copy of the hash object.")]
            public sha3_384 copy() => new sha3_384(CopyDigest<Sha3Digest>());
        }

        [PythonType]
        public sealed class sha3_512 : Sha3FixedBase {
            public sha3_512() : base(512) { }
            public sha3_512([NotNone] IBufferProtocol data) : this() { update(data); }
            public sha3_512([NotNone] string data) : this() { update(data); }
            private sha3_512(Sha3Digest digest) : base(digest) { }

            public override string name => "sha3_512";

            [Documentation("Return a copy of the hash object.")]
            public sha3_512 copy() => new sha3_512(CopyDigest<Sha3Digest>());
        }

        [PythonType]
        public sealed class shake_128 : ShakeBase {
            public shake_128() : base(128) { }
            public shake_128([NotNone] IBufferProtocol data) : this() { update(data); }
            public shake_128([NotNone] string data) : this() { update(data); }
            private shake_128(ShakeDigest digest) : base(digest) { }

            public override string name => "shake_128";

            [Documentation("Return a copy of the hash object.")]
            public shake_128 copy() => new shake_128(CopyDigest<ShakeDigest>());
        }

        [PythonType]
        public sealed class shake_256 : ShakeBase {
            public shake_256() : base(256) { }
            public shake_256([NotNone] IBufferProtocol data) : this() { update(data); }
            public shake_256([NotNone] string data) : this() { update(data); }
            private shake_256(ShakeDigest digest) : base(digest) { }

            public override string name => "shake_256";

            [Documentation("Return a copy of the hash object.")]
            public shake_256 copy() => new shake_256(CopyDigest<ShakeDigest>());
        }
    }
}
