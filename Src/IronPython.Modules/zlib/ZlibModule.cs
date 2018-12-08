// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright 2008-2010 Jeff Hardy
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ComponentAce.Compression.Libs.ZLib;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("zlib", typeof(IronPython.Zlib.ZlibModule))]

namespace IronPython.Zlib
{
    public static class ZlibModule
    {
        public const string __doc__ = @"The functions in this module allow compression and decompression using the
zlib library, which is based on GNU zip.

adler32(string[, start]) -- Compute an Adler-32 checksum.
compress(string[, level]) -- Compress string, with compression level in 1-9.
compressobj([level]) -- Return a compressor object.
crc32(string[, start]) -- Compute a CRC-32 checksum.
decompress(string,[wbits],[bufsize]) -- Decompresses a compressed string.
decompressobj([wbits]) -- Return a decompressor object.

'wbits' is window buffer size.
Compressor objects support compress() and flush() methods; decompressor
objects support decompress() and flush().";

        public const string ZLIB_VERSION = "1.2.3";     // just match the zlib version in Python 2.6

        internal const int Z_OK = (int)ZLibResultCode.Z_OK;
        internal const int Z_STREAM_END = (int)ZLibResultCode.Z_STREAM_END;
        internal const int Z_NEED_DICT = (int)ZLibResultCode.Z_NEED_DICT;
        internal const int Z_ERRNO = (int)ZLibResultCode.Z_ERRNO;
        internal const int Z_STREAM_ERROR = (int)ZLibResultCode.Z_STREAM_ERROR;
        internal const int Z_DATA_ERROR = (int)ZLibResultCode.Z_DATA_ERROR;
        internal const int Z_MEM_ERROR = (int)ZLibResultCode.Z_MEM_ERROR;
        internal const int Z_BUF_ERROR = (int)ZLibResultCode.Z_BUF_ERROR;
        internal const int Z_VERSION_ERROR = (int)ZLibResultCode.Z_VERSION_ERROR;

        public const int Z_NO_FLUSH = (int)FlushStrategy.Z_NO_FLUSH;
        public const int Z_SYNC_FLUSH = (int)FlushStrategy.Z_SYNC_FLUSH;
        public const int Z_FULL_FLUSH = (int)FlushStrategy.Z_FULL_FLUSH;
        public const int Z_FINISH = (int)FlushStrategy.Z_FINISH;

        public const int Z_BEST_SPEED = ZLibCompressionLevel.Z_BEST_SPEED;
        public const int Z_BEST_COMPRESSION = ZLibCompressionLevel.Z_BEST_COMPRESSION;
        public const int Z_DEFAULT_COMPRESSION = ZLibCompressionLevel.Z_DEFAULT_COMPRESSION;

        public const int Z_FILTERED = (int)CompressionStrategy.Z_FILTERED;
        public const int Z_HUFFMAN_ONLY = (int)CompressionStrategy.Z_HUFFMAN_ONLY;
        public const int Z_DEFAULT_STRATEGY = (int)CompressionStrategy.Z_DEFAULT_STRATEGY;

        public const int DEFLATED = 8;
        public const int DEF_MEM_LEVEL = 8;
        public const int MAX_WBITS = ZLibUtil.MAX_WBITS;

        internal const int DEFAULTALLOC = 16 * 1024;

        [Documentation(@"adler32(string[, start]) -- Compute an Adler-32 checksum of string.

An optional starting value can be specified.  The returned checksum is
a signed integer.")]
        public static int adler32([BytesConversion]IList<byte> data, long baseValue=1L)
        {
            return (int)Adler32.GetAdler32Checksum(baseValue, data.ToArray(), 0, data.Count());
        }

        [Documentation(@"crc32(string[, start]) -- Compute a CRC-32 checksum of string.

An optional starting value can be specified.  The returned checksum is
a signed integer.")]
        public static int crc32([BytesConversion]IList<byte> data, long baseValue=0L)
        {
            if(baseValue < int.MinValue || baseValue > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(baseValue));

            if(baseValue >= 0 && baseValue <= uint.MaxValue)
                return IronPython.Modules.PythonBinaryAscii.crc32(data.ToArray(), (uint)baseValue);
            else
                return IronPython.Modules.PythonBinaryAscii.crc32(data.ToArray(), (int)baseValue);
        }

        [Documentation(@"compress(string[, level]) -- Returned compressed string.

Optional arg level is the compression level, in 1-9.")]
        public static string compress([BytesConversion]IList<byte> data,
            int level=Z_DEFAULT_COMPRESSION)
        {
            byte[] input = data.ToArray();
            byte[] output = new byte[input.Length + input.Length / 1000 + 12 + 1];

            ZStream zst = new ZStream();
            zst.next_in = input;
            zst.avail_in = input.Length;
            zst.next_out = output;
            zst.avail_out = output.Length;

            int err = zst.DeflateInit(level);
            switch(err)
            {
                case (Z_OK):
                    break;
                
                case (Z_STREAM_ERROR):
                    throw PythonOps.CreateThrowable(error,
                                    "Bad compression level");
                
                default:
                    zst.deflateEnd();
                    zlib_error(zst, err, "while compressing data");
                    return null;
            }

            err = zst.deflate(FlushStrategy.Z_FINISH);

            if(err != Z_STREAM_END)
            {
                zst.deflateEnd();
                throw zlib_error(zst, err, "while compressing data");
            }

            err = zst.deflateEnd();

            if(err == Z_OK)
                return PythonAsciiEncoding.Instance.GetString(output, 0, (int)zst.total_out);
            else
                throw zlib_error(zst, err, "while finishing compression");
        }

        [Documentation(@"compressobj([level]) -- Return a compressor object.

Optional arg level is the compression level, in 1-9.")]
        public static Compress compressobj(
            int level=Z_DEFAULT_COMPRESSION,
            int method=DEFLATED,
            int wbits=MAX_WBITS,
            int memlevel=DEF_MEM_LEVEL,
            int strategy=Z_DEFAULT_STRATEGY)
        {
            return new Compress(level, method, wbits, memlevel, strategy);
        }

        [Documentation(@"decompress(string[, wbits[, bufsize]]) -- Return decompressed string.

Optional arg wbits is the window buffer size.  Optional arg bufsize is
the initial output buffer size.")]
        public static string decompress([BytesConversion]IList<byte> data,
            int wbits=MAX_WBITS,
            int bufsize=DEFAULTALLOC)
        {
            var bytes = Decompress(data.ToArray(), wbits, bufsize);
            return PythonAsciiEncoding.Instance.GetString(bytes, 0, bytes.Length);
        }

        [Documentation(@"decompressobj([wbits]) -- Return a decompressor object.

Optional arg wbits is the window buffer size.")]
        public static Decompress decompressobj(int wbits=MAX_WBITS)
        {
            return new Decompress(wbits);
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext context, PythonDictionary dict)
        {
            error = context.EnsureModuleException("zlib.error", PythonExceptions.Exception, dict, "error", "zlib");
        }

        public static PythonType error;
        internal static Exception MakeError(params object[] args)
        {
            return PythonOps.CreateThrowable(error, args);
        }

        internal static Exception zlib_error(ZStream zst, int err, string msg)
        {
            string zmsg = zst.msg;
            if(zmsg == null)
            {
                switch(err)
                {
                    case Z_BUF_ERROR:
                        zmsg = "incomplete or truncated stream";
                        break;
                    case Z_STREAM_ERROR:
                        zmsg = "inconsistent stream state";
                        break;
                    case Z_DATA_ERROR:
                        zmsg = "invalid input data";
                        break;
                }
            }

            if(zmsg == null)
                return MakeError(string.Format("Error {0} {1}", err, msg));
            else
                return MakeError(string.Format("Error {0} {1}: {2}", err, msg, zmsg));
        }

        [PythonHidden]
        internal static byte[] Decompress(byte[] input, int wbits=MAX_WBITS, int bufsize=DEFAULTALLOC) 
        {
            byte[] outputBuffer = new byte[bufsize];
            byte[] output = new byte[bufsize];
            int outputOffset = 0;

            ZStream zst = new ZStream();
            zst.next_in = input;
            zst.avail_in = input.Length;
            zst.next_out = outputBuffer;
            zst.avail_out = outputBuffer.Length;

            int err = zst.inflateInit(wbits);
            if(err != Z_OK)
            {
                zst.inflateEnd();
                throw zlib_error(zst, err, "while preparing to decompress data");
            }

            do
            {
                err = zst.inflate(FlushStrategy.Z_FINISH);
                if(err != Z_STREAM_END)
                {
                    if(err == Z_BUF_ERROR && zst.avail_out > 0)
                    {
                        zst.inflateEnd();
                        throw zlib_error(zst, err, "while decompressing data");
                    }
                    else if(err == Z_OK || (err == Z_BUF_ERROR && zst.avail_out == 0))
                    {
                        // copy to the output and reset the buffer
                        if(outputOffset + outputBuffer.Length > output.Length)
                            Array.Resize(ref output, output.Length * 2);

                        Array.Copy(outputBuffer, 0, output, outputOffset, outputBuffer.Length);
                        outputOffset += outputBuffer.Length;

                        zst.next_out = outputBuffer;
                        zst.avail_out = outputBuffer.Length;
                        zst.next_out_index = 0;
                    }
                    else
                    {
                        zst.inflateEnd();
                        throw zlib_error(zst, err, "while decompressing data");
                    }
                }

            } while(err != Z_STREAM_END);

            err = zst.inflateEnd();
            if(err != Z_OK)
            {
                throw zlib_error(zst, err, "while finishing data decompression");
            }

            if(outputOffset + outputBuffer.Length - zst.avail_out > output.Length)
                Array.Resize(ref output, output.Length * 2);

            Array.Copy(outputBuffer, 0, output, outputOffset, outputBuffer.Length - zst.avail_out);
            outputOffset += outputBuffer.Length - zst.avail_out;

            return output.Take(outputOffset).ToArray();
        }
    }
}
