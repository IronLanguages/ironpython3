// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_sha3", typeof(IronPython.Modules.PythonSha3))]
namespace IronPython.Modules {
    public static class PythonSha3 {
        public const string __doc__ = "SHA-3 and SHAKE hash algorithms";

        [PythonHidden]
        public abstract class Sha3FixedBase : HashBase {
            private protected Sha3FixedBase(string name, IDigest digest) : base(name, digest) { }

            public int _rate_bits => block_size * 8;

            public int _capacity_bits => 1600 - _rate_bits;

            public Bytes _suffix => Bytes.Make([0x06]);
        }

        [PythonHidden]
        public abstract class ShakeBase {
            private ShakeDigest _digest;

            public readonly string name;
            public readonly int block_size;

            private protected ShakeBase(string name, ShakeDigest digest) {
                this.name = name;
                _digest = digest;
                block_size = digest.GetByteLength();
            }

            public int digest_size => 0;

            public int _rate_bits => block_size * 8;

            public int _capacity_bits => 1600 - _rate_bits;

            public Bytes _suffix => Bytes.Make([0x1f]);

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] IBufferProtocol data) {
                lock (_digest) {
                    HashHelpers.Update(_digest, data);
                }
            }

            [Documentation("Update this hash object's state with the provided bytes-like object.")]
            public void update([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

            [Documentation("Return the digest value as a bytes object.")]
            public Bytes digest(int length) {
                if (length < 0) throw PythonOps.ValueError("length must be non-negative");
                if (length >= 1 << 29) throw PythonOps.ValueError("length is too large");
                var copy = CopyDigest();
                var res = new byte[length];
                copy.OutputFinal(res, 0, length);
                return Bytes.Make(res);
            }

            [Documentation("Return the digest value as a string of hexadecimal digits.")]
            public string hexdigest(int length) => digest(length).hex();

            [Documentation("Return a copy of the hash object.")]
            public ShakeBase copy() {
                var res = (ShakeBase)MemberwiseClone();
                res._digest = CopyDigest();
                return res;
            }

            public object __reduce__([NotNone] params object[] args) => throw HashHelpers.CannotPickleError(name);

            private ShakeDigest CopyDigest() {
                lock (_digest) {
                    return (ShakeDigest)_digest.Copy();
                }
            }
        }

        [PythonType]
        public sealed class sha3_224 : Sha3FixedBase {
            public sha3_224() : base("sha3_224", new Sha3Digest(224)) { }
            public sha3_224([NotNone] IBufferProtocol data) : this() => update(data);
            public sha3_224([NotNone] string data) : this() => update(data);
        }

        [PythonType]
        public sealed class sha3_256 : Sha3FixedBase {
            public sha3_256() : base("sha3_256", new Sha3Digest(256)) { }
            public sha3_256([NotNone] IBufferProtocol data) : this() => update(data);
            public sha3_256([NotNone] string data) : this() => update(data);
        }

        [PythonType]
        public sealed class sha3_384 : Sha3FixedBase {
            public sha3_384() : base("sha3_384", new Sha3Digest(384)) { }
            public sha3_384([NotNone] IBufferProtocol data) : this() => update(data);
            public sha3_384([NotNone] string data) : this() => update(data);
        }

        [PythonType]
        public sealed class sha3_512 : Sha3FixedBase {
            public sha3_512() : base("sha3_512", new Sha3Digest(512)) { }
            public sha3_512([NotNone] IBufferProtocol data) : this() => update(data);
            public sha3_512([NotNone] string data) : this() => update(data);
        }

        [PythonType]
        public sealed class shake_128 : ShakeBase {
            public shake_128() : base("shake_128", new ShakeDigest(128)) { }
            public shake_128([NotNone] IBufferProtocol data) : this() => update(data);
            public shake_128([NotNone] string data) : this() => update(data);
        }

        [PythonType]
        public sealed class shake_256 : ShakeBase {
            public shake_256() : base("shake_256", new ShakeDigest(256)) { }
            public shake_256([NotNone] IBufferProtocol data) : this() => update(data);
            public shake_256([NotNone] string data) : this() => update(data);
        }
    }
}
