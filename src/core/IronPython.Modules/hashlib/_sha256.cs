// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_sha256", typeof(IronPython.Modules.PythonSha256))]
namespace IronPython.Modules {
    public static class PythonSha256 {
        public const string __doc__ = "SHA-224 and SHA-256 hash algorithms";

        [Documentation("Return a new SHA-256 hash object.")]
        public static SHA256Type sha256([NotNone] IBufferProtocol data) => new SHA256Type(data);

        [Documentation("Return a new SHA-256 hash object.")]
        public static SHA256Type sha256([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return a new SHA-256 hash object.")]
        public static SHA256Type sha256() => new SHA256Type();

        [PythonType("sha256")]
        public sealed class SHA256Type : HashBase {
            internal SHA256Type() : base("sha256", new Sha256Digest()) { }

            internal SHA256Type(IBufferProtocol initialBytes) : this()
                => update(initialBytes);
        }

        [Documentation("Return a new SHA-224 hash object.")]
        public static SHA224Type sha224([NotNone] IBufferProtocol data) => new SHA224Type(data);

        [Documentation("Return a new SHA-224 hash object.")]
        public static SHA224Type sha224([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return a new SHA-224 hash object.")]
        public static SHA224Type sha224() => new SHA224Type();

        [PythonType("sha224")]
        public sealed class SHA224Type : HashBase {
            internal SHA224Type() : base("sha224", new Sha224Digest()) { }

            internal SHA224Type(IBufferProtocol initialBytes) : this()
                => update(initialBytes);
        }
    }
}
