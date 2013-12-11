/* **************************************************************************
 *
 * Copyright 2012 Jeff Hardy
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * *************************************************************************/

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

            public BZ2Compressor([DefaultParameterValue(DEFAULT_COMPRESSLEVEL)]int compresslevel) {
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
#if !SILVERLIGHT && !WP75
                    Array.Copy(this.output.GetBuffer(), this.lastPosition, result, 0, newDataCount);
#else
                    Array.Copy(this.output.GetBuffer(), (int)this.lastPosition, result, 0, (int)newDataCount);
#endif

                    this.lastPosition = this.output.Position;
                }

                return result;
            }
        }
    }
}

