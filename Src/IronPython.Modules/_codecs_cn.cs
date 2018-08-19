// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;

#if FALSE
[assembly: PythonModule("_codecs_cn", typeof(IronPython.Modules._codecs_cn))]
namespace IronPython.Modules {
    public class _codecs_cn {
        public static MultibyteCodec getcodec(string name) {
            switch(name) {
                case "gbk": 
                    return new MultibyteCodec(Encoding.GetEncoding(936), name);
                case "gb2312":
                    return new MultibyteCodec(Encoding.GetEncoding("GB2312"), name);
                case "gb18030":
                    return new MultibyteCodec(Encoding.GetEncoding("GB18030"), name);
            }

            throw PythonOps.LookupError("no such codec is supported: {0}", name);
        }
    }

}
#endif