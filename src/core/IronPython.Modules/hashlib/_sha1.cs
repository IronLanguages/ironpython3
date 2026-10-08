// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_sha1", typeof(IronPython.Modules.PythonSha1))]
namespace IronPython.Modules {
    public static class PythonSha1 {
        public const string __doc__ = "SHA-1 hash algorithm";

        [Documentation("Return a new SHA-1 hash object.")]
        public static SHA1Type sha1([NotNone] IBufferProtocol data) => new SHA1Type(data);

        [Documentation("Return a new SHA-1 hash object.")]
        public static SHA1Type sha1([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return a new SHA-1 hash object.")]
        public static SHA1Type sha1() => new SHA1Type();

        [PythonType("sha1")]
        public sealed class SHA1Type : HashBase {
            internal SHA1Type() : base("sha1", new Sha1Digest()) { }

            internal SHA1Type(IBufferProtocol initialBytes) : this()
                => update(initialBytes);
        }
    }
}
