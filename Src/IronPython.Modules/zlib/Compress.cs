/* **************************************************************************
 *
 * Copyright 2008-2010 Jeff Hardy
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
using System.Linq;
using System.Runtime.InteropServices;
using ComponentAce.Compression.Libs.ZLib;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Zlib
{
    [PythonType]
    public class Compress
    {
        private const int Z_OK = ZlibModule.Z_OK;
        private const int Z_BUF_ERROR = ZlibModule.Z_BUF_ERROR;
        private const int Z_STREAM_END = ZlibModule.Z_STREAM_END;
        
        private const int Z_NO_FLUSH = ZlibModule.Z_NO_FLUSH;
        private const int Z_FINISH = ZlibModule.Z_FINISH;

        internal Compress(int level, int method, int wbits, int memlevel, int strategy)
        {
            zst = new ZStream();
            int err = zst.deflateInit(level, wbits, memlevel, (CompressionStrategy)strategy);
            switch(err)
            {
                case ZlibModule.Z_OK:
                    break;

                case ZlibModule.Z_STREAM_ERROR:
                    throw PythonOps.ValueError("Invalid initialization option");

                default:
                    throw ZlibModule.zlib_error(this.zst, err, "while creating compression object");
            }
        }

        [Documentation(@"compress(data) -- Return a string containing data compressed.

After calling this function, some of the input data may still
be stored in internal buffers for later processing.
Call the flush() method to clear these buffers.")]
        public string compress([BytesConversion]IList<byte> data)
        {
            byte[] input = data.ToArray();
            byte[] output = new byte[ZlibModule.DEFAULTALLOC];

            long start_total_out = zst.total_out;
            zst.next_in = input;
            zst.next_in_index = 0;
            zst.avail_in = input.Length;
            zst.next_out = output;
            zst.next_out_index = 0;
            zst.avail_out = output.Length;

            int err = zst.deflate(Z_NO_FLUSH);

            while(err == Z_OK && zst.avail_out == 0)
            {
                int length = output.Length;
                Array.Resize(ref output, output.Length * 2);

                zst.next_out = output;
                zst.avail_out = length;

                err = zst.deflate(Z_NO_FLUSH);
            }

            if(err != Z_OK && err != Z_BUF_ERROR)
            {
                throw ZlibModule.zlib_error(this.zst, err, "while compressing");
            }

            return PythonAsciiEncoding.Instance.GetString(output, 0, (int)(zst.total_out - start_total_out));
        }

        [Documentation(@"flush( [mode] ) -- Return a string containing any remaining compressed data.

mode can be one of the constants Z_SYNC_FLUSH, Z_FULL_FLUSH, Z_FINISH; the
default value used when mode is not specified is Z_FINISH.
If mode == Z_FINISH, the compressor object can no longer be used after
calling the flush() method.  Otherwise, more data can still be compressed.")]
        public string flush([DefaultParameterValue(Z_FINISH)]int mode)
        {
            byte[] output = new byte[ZlibModule.DEFAULTALLOC];

            if(mode == Z_NO_FLUSH)
            {
                return string.Empty;
            }

            long start_total_out = zst.total_out;
            zst.avail_in = 0;
            zst.next_out = output;
            zst.next_out_index = 0;
            zst.avail_out = output.Length;

            int err = zst.deflate((FlushStrategy)mode);
            while(err == Z_OK && zst.avail_out == 0)
            {
                int old_length = output.Length;
                Array.Resize(ref output, output.Length * 2);

                zst.next_out = output;
                zst.avail_out = old_length;

                err = zst.deflate((FlushStrategy)mode);
            }

            if(err == Z_STREAM_END && mode == Z_FINISH)
            {
                err = zst.deflateEnd();
                if(err != Z_OK)
                {
                    throw ZlibModule.zlib_error(this.zst, err, "from deflateEnd()");
                }
            }
            else if(err != Z_OK && err != Z_BUF_ERROR)
            {
                throw ZlibModule.zlib_error(this.zst, err, "while flushing");
            }

            return PythonAsciiEncoding.Instance.GetString(output, 0, (int)(zst.total_out - start_total_out));
        }

        //[Documentation("copy() -- Return a copy of the compression object.")]
        //public Compress copy()
        //{
        //    throw new NotImplementedException();
        //}

        private ZStream zst;
    }
}
