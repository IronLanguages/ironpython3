// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_sha512", typeof(IronPython.Modules.PythonSha512))]
namespace IronPython.Modules {
    public static class PythonSha512 {
        public const string __doc__ = "SHA-384 and SHA-512 hash algorithms";

        [Documentation("Return a new SHA-384 hash object.")]
        public static SHA384Type sha384([NotNone] IBufferProtocol data) => new SHA384Type(data);

        [Documentation("Return a new SHA-384 hash object.")]
        public static SHA384Type sha384([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return a new SHA-384 hash object.")]
        public static SHA384Type sha384() => new SHA384Type();

        [PythonType("sha384")]
        public sealed class SHA384Type : HashBase {
            internal SHA384Type() : base("sha384", new Sha384Digest()) { }

            internal SHA384Type(IBufferProtocol initialBytes) : this()
                => update(initialBytes);
        }

        [Documentation("Return a new SHA-512 hash object.")]
        public static SHA512Type sha512([NotNone] IBufferProtocol data) => new SHA512Type(data);

        [Documentation("Return a new SHA-512 hash object.")]
        public static SHA512Type sha512([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return a new SHA-512 hash object.")]
        public static SHA512Type sha512() => new SHA512Type();

        [PythonType("sha512")]
        public sealed class SHA512Type : HashBase {
            internal SHA512Type() : base("sha512", new Sha512Digest()) { }

            internal SHA512Type(IBufferProtocol initialBytes) : this()
                => update(initialBytes);
        }
    }
}
