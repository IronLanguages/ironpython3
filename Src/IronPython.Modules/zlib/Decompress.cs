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
    public class Decompress
    {
        private const int Z_OK = ZlibModule.Z_OK;
        private const int Z_STREAM_END = ZlibModule.Z_STREAM_END;
        private const int Z_BUF_ERROR = ZlibModule.Z_BUF_ERROR;

        private const int Z_SYNC_FLUSH = ZlibModule.Z_SYNC_FLUSH;
        private const int Z_FINISH = ZlibModule.Z_FINISH;

        internal Decompress(int wbits)
        {
            zst = new ZStream();
            int err = zst.inflateInit(wbits);
            switch(err)
            {
                case ZlibModule.Z_OK:
                    break;

                case ZlibModule.Z_STREAM_ERROR:
                    throw PythonOps.ValueError("Invalid initialization option");

                default:
                    throw ZlibModule.zlib_error(this.zst, err, "while creating decompression object");
            }

            _unused_data = string.Empty;
            _unconsumed_tail = string.Empty;
        }

        private string _unused_data;
        public string unused_data
        {
            get { return _unused_data; }
        }

        private string _unconsumed_tail;
        public string unconsumed_tail
        {
            get { return _unconsumed_tail; }
        }

        [Documentation(@"decompress(data, max_length) -- Return a string containing the decompressed
version of the data.

After calling this function, some of the input data may still be stored in
internal buffers for later processing.
Call the flush() method to clear these buffers.
If the max_length parameter is specified then the return value will be
no longer than max_length.  Unconsumed input data will be stored in
the unconsumed_tail attribute.")]
        public string decompress([BytesConversion]IList<byte> value, int max_length=0)
        {
            if(max_length < 0) throw new ArgumentException("max_length must be greater than zero");

            byte[] input = value.ToArray();
            byte[] output = new byte[max_length > 0 && ZlibModule.DEFAULTALLOC > max_length ? max_length : ZlibModule.DEFAULTALLOC];

            long start_total_out = zst.total_out;
            zst.next_in = input;
            zst.next_in_index = 0;
            zst.avail_in = input.Length;
            zst.next_out = output;
            zst.next_out_index = 0;
            zst.avail_out = output.Length;

            int err = zst.inflate(FlushStrategy.Z_SYNC_FLUSH);

            while(err == Z_OK && zst.avail_out == 0)
            {
                if(max_length > 0 && output.Length >= max_length)
                    break;

                int old_length = output.Length;
                Array.Resize(ref output, output.Length * 2);
                zst.next_out = output;
                zst.avail_out = old_length;

                err = zst.inflate(FlushStrategy.Z_SYNC_FLUSH);
            }

            if(max_length > 0)
            {
                _unconsumed_tail = PythonAsciiEncoding.Instance.GetString(zst.next_in, zst.next_in_index, zst.avail_in);
            }

            if(err == Z_STREAM_END)
            {
                _unused_data += PythonAsciiEncoding.Instance.GetString(zst.next_in, zst.next_in_index, zst.avail_in);
            }
            else if(err != Z_OK && err != Z_BUF_ERROR)
            {
                throw ZlibModule.zlib_error(this.zst, err, "while decompressing");
            }

            return PythonAsciiEncoding.Instance.GetString(output, 0, (int)(zst.total_out - start_total_out));
        }

        [Documentation(@"flush( [length] ) -- Return a string containing any remaining
decompressed data. length, if given, is the initial size of the
output buffer.

The decompressor object can no longer be used after this call.")]
        public string flush(int length=ZlibModule.DEFAULTALLOC)
        {
            if(length < 1)
                throw PythonOps.ValueError("length must be greater than 0.");

            byte[] output = new byte[length];

            long start_total_out = zst.total_out;
            zst.next_out = output;
            zst.next_out_index = 0;
            zst.avail_out = output.Length;

            int err = zst.inflate(FlushStrategy.Z_FINISH);

            while((err == Z_OK || err == Z_BUF_ERROR) &&zst.avail_out == 0)
            {
                int old_length = output.Length;
                Array.Resize(ref output, output.Length * 2);
                zst.next_out = output;
                zst.avail_out = old_length;

                err = zst.inflate(FlushStrategy.Z_FINISH);
            }

            if(err == Z_STREAM_END)
            {
                err = zst.inflateEnd();
                if(err != Z_OK)
                {
                    throw ZlibModule.zlib_error(this.zst, err, "from inflateEnd()");
                }
            }

            return PythonAsciiEncoding.Instance.GetString(output, 0, (int)(zst.total_out - start_total_out));
        }

        //[Documentation("copy() -- Return a copy of the decompression object.")]
        //public Compress copy()
        //{
        //    throw new NotImplementedException();
        //}

        private ZStream zst;
    }
}
