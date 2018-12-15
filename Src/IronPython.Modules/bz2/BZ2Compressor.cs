// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright 2012 Jeff Hardy
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Ionic.BZip2;
using IronPython.Runtime;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Modules.Bz2 {
    public static partial class Bz2Module {
        [PythonType]
        public class BZ2Compressor {
            public const string __doc__ = 
@"BZ2Compressor([compresslevel=9]) -> compressor object

Create a new compressor object. This object may be used to compress
data sequentially. If you want to compress data in one shot, use the
compress() function instead. The compresslevel parameter, if given,
must be a number between 1 and 9.
";

            private int compresslevel;
            private MemoryStream output;
            private BZip2OutputStream bz2Output;
            private long lastPosition = 0;

            public BZ2Compressor(int compresslevel=DEFAULT_COMPRESSLEVEL) {
                this.compresslevel = compresslevel;

                this.output = new MemoryStream();
                this.bz2Output = new BZip2OutputStream(this.output, true);
            }

            [Documentation(@"compress(data) -> string

Provide more data to the compressor object. It will return chunks of
compressed data whenever possible. When you've finished providing data
to compress, call the flush() method to finish the compression process,
and return what is left in the internal buffers.
")]
            public Bytes compress([BytesConversion]IList<byte> data) {
                byte[] bytes = data.ToArrayNoCopy();

                this.bz2Output.Write(bytes, 0, bytes.Length);

                return new Bytes(this.GetLatestData());
            }

            [Documentation(@"flush() -> string

Finish the compression process and return what is left in internal buffers.
You must not use the compressor object after calling this method.
")]
            public Bytes flush() {
                this.bz2Output.Close();

                return new Bytes(this.GetLatestData());
            }

            /// <summary>
            /// Copy the latest data from the memory buffer.
            /// 
            /// This won't always contain data, because comrpessed data is only written after a block is filled.
            /// </summary>
            /// <returns></returns>
            private byte[] GetLatestData() {
                long newDataCount = this.output.Position - this.lastPosition;

                byte[] result = new byte[newDataCount];
                if (newDataCount > 0) {
                    Array.Copy(this.output.GetBuffer(), this.lastPosition, result, 0, newDataCount);

                    this.lastPosition = this.output.Position;
                }

                return result;
            }
        }
    }
}

