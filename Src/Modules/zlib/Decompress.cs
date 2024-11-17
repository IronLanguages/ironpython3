// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright 2008-2010 Jeff Hardy
//

using System;
using System.Linq;

using ComponentAce.Compression.Libs.ZLib;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Zlib {
    [PythonType]
    public class Decompress {
        private const int Z_OK = ZlibModule.Z_OK;
        private const int Z_STREAM_END = ZlibModule.Z_STREAM_END;
        private const int Z_BUF_ERROR = ZlibModule.Z_BUF_ERROR;

        private const int Z_SYNC_FLUSH = ZlibModule.Z_SYNC_FLUSH;
        private const int Z_FINISH = ZlibModule.Z_FINISH;

        internal Decompress(int wbits) {
            zst = new ZStream();
            int err = zst.inflateInit(wbits);
            switch (err) {
                case ZlibModule.Z_OK:
                    break;

                case ZlibModule.Z_STREAM_ERROR:
                    throw PythonOps.ValueError("Invalid initialization option");

                default:
                    throw ZlibModule.zlib_error(this.zst, err, "while creating decompression object");
            }

            unused_data = Bytes.Empty;
            unconsumed_tail = Bytes.Empty;
        }

        public Bytes unused_data { get; private set; }
        public Bytes unconsumed_tail { get; private set; }

        [Documentation(@"decompress(data, max_length) -- Return a bytes object containing the decompressed
version of the data.

After calling this function, some of the input data may still be stored in
internal buffers for later processing.
Call the flush() method to clear these buffers.
If the max_length parameter is specified then the return value will be
no longer than max_length.  Unconsumed input data will be stored in
the unconsumed_tail attribute.")]
        public Bytes decompress([NotNone] IBufferProtocol data, int max_length = 0) {
            if (max_length < 0) throw new ArgumentException("max_length must be greater than zero");

            using var buffer = data.GetBuffer();
            byte[] input = buffer.AsUnsafeArray() ?? buffer.ToArray();
            byte[] output = new byte[max_length > 0 && ZlibModule.DEFAULTALLOC > max_length ? max_length : ZlibModule.DEFAULTALLOC];

            long start_total_out = zst.total_out;
            zst.next_in = input;
            zst.next_in_index = 0;
            zst.avail_in = input.Length;
            zst.next_out = output;
            zst.next_out_index = 0;
            zst.avail_out = output.Length;

            int err = zst.inflate(FlushStrategy.Z_SYNC_FLUSH);

            while (err == Z_OK && zst.avail_out == 0) {
                if (max_length > 0 && output.Length >= max_length)
                    break;

                int old_length = output.Length;
                Array.Resize(ref output, output.Length * 2);
                zst.next_out = output;
                zst.avail_out = old_length;

                err = zst.inflate(FlushStrategy.Z_SYNC_FLUSH);
            }

            if (max_length > 0) {
                unconsumed_tail = GetBytes(zst.next_in, zst.next_in_index, zst.avail_in);
            }

            if (err == Z_STREAM_END) {
                unused_data += GetBytes(zst.next_in, zst.next_in_index, zst.avail_in);
                eof = true;
            } else if (err != Z_OK && err != Z_BUF_ERROR) {
                throw ZlibModule.zlib_error(this.zst, err, "while decompressing");
            }

            return GetBytes(output, 0, (int)(zst.total_out - start_total_out));
        }

        public bool eof { get; set; }

        [Documentation(@"flush( [length] ) -- Return a bytes object  containing any remaining
decompressed data. length, if given, is the initial size of the
output buffer.

The decompressor object can no longer be used after this call.")]
        public Bytes flush(int length = ZlibModule.DEFAULTALLOC) {
            if (length < 1)
                throw PythonOps.ValueError("length must be greater than 0.");

            byte[] output = new byte[length];

            long start_total_out = zst.total_out;
            zst.next_out = output;
            zst.next_out_index = 0;
            zst.avail_out = output.Length;

            int err = zst.inflate(FlushStrategy.Z_FINISH);

            while ((err == Z_OK || err == Z_BUF_ERROR) && zst.avail_out == 0) {
                int old_length = output.Length;
                Array.Resize(ref output, output.Length * 2);
                zst.next_out = output;
                zst.avail_out = old_length;

                err = zst.inflate(FlushStrategy.Z_FINISH);
            }

            if (err == Z_STREAM_END) {
                err = zst.inflateEnd();
                if (err != Z_OK) {
                    throw ZlibModule.zlib_error(this.zst, err, "from inflateEnd()");
                }
            }

            return GetBytes(output, 0, (int)(zst.total_out - start_total_out));
        }

        //[Documentation("copy() -- Return a copy of the decompression object.")]
        //public Compress copy()
        //{
        //    throw new NotImplementedException();
        //}

        private ZStream zst;

        private static Bytes GetBytes(byte[] bytes, int index, int count) {
            var res = new byte[count];
            Array.Copy(bytes, index, res, 0, count);
            return Bytes.Make(res);
        }
    }
}
