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

using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Ionic.BZip2;
using IronPython.Runtime;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("bz2", typeof(IronPython.Modules.Bz2.Bz2Module))]

namespace IronPython.Modules.Bz2 {
    public static partial class Bz2Module {
        public const string __doc__ = 
@"The python bz2 module provides a comprehensive interface for
the bz2 compression library. It implements a complete file
interface, one shot (de)compression functions, and types for
sequential (de)compression.";

        internal const int DEFAULT_COMPRESSLEVEL = 9;
        private const int PARALLEL_THRESHOLD = 10 * 1024 * 1024;    // 10 MiB recommended by ParallelBZip2OutputStream devs

        [Documentation(@"compress(data [, compresslevel=9]) -> string

Compress data in one shot. If you want to compress data sequentially,
use an instance of BZ2Compressor instead. The compresslevel parameter, if
given, must be a number between 1 and 9.
")]
        public static Bytes compress([BytesConversion]IList<byte> data, 
                                      int compresslevel=DEFAULT_COMPRESSLEVEL) {
            using (var mem = new MemoryStream()) {
                using (var bz2 = data.Count > PARALLEL_THRESHOLD ? 
                            (Stream)new ParallelBZip2OutputStream(mem, true) :
                            (Stream)new BZip2OutputStream(mem, true)) {
                    var buffer = data.ToArrayNoCopy();
                    bz2.Write(buffer, 0, data.Count);
                }

                return Bytes.Make(mem.ToArray());
            }
        }

        [Documentation(@"decompress(data) -> decompressed data

Decompress data in one shot. If you want to decompress data sequentially,
use an instance of BZ2Decompressor instead.
")]
        public static Bytes decompress([BytesConversion]IList<byte> data) {
            if (data.Count == 0) {
                return new Bytes();
            }

            byte[] buffer = new byte[1024];

            using (var output = new MemoryStream()) {
                using (var input = new MemoryStream(data.ToArrayNoCopy(), false)) {
                    using (var bz2 = new BZip2InputStream(input)) {

                        int read = 0;
                        while(true) {
                            try {
                                read = bz2.Read(buffer, 0, buffer.Length);
                            } catch (IOException e) {
                                throw PythonOps.ValueError(e.Message);
                            }
                            if (read > 0) {
                                output.Write(buffer, 0, read);
                            } else {
                                break;
                            }
                        }
                    }
                }

                return Bytes.Make(output.ToArray());
            }
        }

        /// <summary>
        /// Try to convert IList(Of byte) to byte[] without copying, if possible.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static byte[] ToArrayNoCopy(this IList<byte> bytes) {
            byte[] bytesA = bytes as byte[];
            if (bytesA != null) {
                return bytesA;
            }

            Bytes bytesP = bytes as Bytes;
            if (bytesP != null) {
                return bytesP.GetUnsafeByteArray();
            }

            return bytes.ToArray();
        }
    }
}
