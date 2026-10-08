// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;

using Microsoft.Scripting.Runtime;

using Org.BouncyCastle.Crypto.Digests;

[assembly: PythonModule("_md5", typeof(IronPython.Modules.PythonMD5))]
namespace IronPython.Modules {
    public static class PythonMD5 {
        public const string __doc__ = "MD5 hash algorithm";

        [Documentation("Return a new MD5 hash object.")]
        public static MD5Type md5([NotNone] IBufferProtocol data) => new MD5Type(data);

        [Documentation("Return a new MD5 hash object.")]
        public static MD5Type md5([NotNone] string data) => throw HashHelpers.StringNotEncodedError();

        [Documentation("Return a new MD5 hash object.")]
        public static MD5Type md5() => new MD5Type();

        [PythonType("md5")]
        public sealed class MD5Type : HashBase {
            internal MD5Type() : base("md5", new MD5Digest()) { }

            internal MD5Type(IBufferProtocol initialBytes) : this()
                => update(initialBytes);
        }
    }
}
