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