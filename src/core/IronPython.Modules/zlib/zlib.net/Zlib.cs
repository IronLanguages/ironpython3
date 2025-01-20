// Copyright (c) 2006, ComponentAce
// http://www.componentace.com
// All rights reserved.

// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

// Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer. 
// Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution. 
// Neither the name of ComponentAce nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission. 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

/*
Copyright (c) 2000,2001,2002,2003 ymnk, JCraft,Inc. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright 
notice, this list of conditions and the following disclaimer in 
the documentation and/or other materials provided with the distribution.

3. The names of the authors may not be used to endorse or promote products
derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESSED OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL JCRAFT,
INC. OR ANY CONTRIBUTORS TO THIS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
/*
* This program is based on zlib-1.1.3, so all credit should go authors
* Jean-loup Gailly(jloup@gzip.org) and Mark Adler(madler@alumni.caltech.edu)
* and contributors of zlib.
*/
using System;
using System.Text;

namespace ComponentAce.Compression.Libs.ZLib
{
    /// <summary>
    /// Some constants for specifying compression levels. Methods which takes a compression level as a parameter expects an integer value from 0 to 9. You can either specify an integer value or use constants for some most widely used compression levels.
    /// </summary>
    public static class ZLibCompressionLevel
    {
        /// <summary>
        ///  No compression should be used at all.
        /// </summary>
        public const int Z_NO_COMPRESSION = 0;
        /// <summary>
        /// Minimal compression, but greatest speed.
        /// </summary>
		public const int Z_BEST_SPEED = 1;
        /// <summary>
        /// Maximum compression, but slowest.
        /// </summary>
		public const int Z_BEST_COMPRESSION = 9;
        /// <summary>
        /// Select default compression level (good compression, good speed).
        /// </summary>
		public const int Z_DEFAULT_COMPRESSION = -1;
    }

    /// <summary>
    /// Compression strategies. The strategy parameter is used to tune the compression algorithm. The strategy parameter only affects the compression ratio but not the correctness of the compressed output even if it is not set appropriately.
    /// </summary>
    public enum CompressionStrategy
    {
        /// <summary>
        /// This strategy is designed for filtered data. Data which consists of mostly small values, with random distribution should use Z_FILTERED. With this strategy, less string matching is performed.
        /// </summary>
        Z_FILTERED = 1,
        /// <summary>
        /// Z_HUFFMAN_ONLY forces Huffman encoding only (no string match)
        /// </summary>
		Z_HUFFMAN_ONLY = 2,
        /// <summary>
        /// The default strategy is the most commonly used. With this strategy, string matching and huffman compression are balanced.
        /// </summary>
        Z_DEFAULT_STRATEGY = 0
    }

    /// <summary>
    /// Flush strategies
    /// </summary>
    public enum FlushStrategy
    {
        /// <summary>
        ///   Do not internalFlush data, but just write data as normal to the output buffer. This is the normal way in which data is written to the output buffer.
        /// </summary>
        Z_NO_FLUSH = 0,
        /// <summary>
        /// Obsolete. You should use Z_SYNC_FLUSH instead.
        /// </summary>        
		Z_PARTIAL_FLUSH = 1,
        /// <summary>
        /// All pending output is flushed to the output buffer and the output is aligned on a byte boundary, so that the decompressor can get all input data available so far.
        /// </summary>
		Z_SYNC_FLUSH = 2,
        /// <summary>
        /// All output is flushed as with Z_SYNC_FLUSH, and the compression state is reset so that decompression can restart from this point if previous compressed data has been damaged or if random access is desired. Using Z_FULL_FLUSH too often can seriously degrade the compression. ZLib_InflateSync will locate points in the compression string where a full has been performed.
        /// </summary>
		Z_FULL_FLUSH = 3,
        /// <summary>
        /// Notifies the module that the input has now been exhausted. Pending input is processed, pending output is flushed and calls return with Z_STREAM_END if there was enough output space.
        /// </summary>
        Z_FINISH = 4
    }

    /// <summary>
    /// Results of operations in ZLib library
    /// </summary>
    public enum ZLibResultCode
    {
        /// <summary>
        ///  No failure was encountered, the operation completed without problem.
        /// </summary>
        Z_OK = 0,
        /// <summary>
        /// No failure was encountered, and the input has been exhausted.
        /// </summary>
		Z_STREAM_END = 1,
        /// <summary>
        /// A preset dictionary is required for decompression of the data.
        /// </summary>
		Z_NEED_DICT = 2,
        /// <summary>
        /// An internal error occurred
        /// </summary>
		Z_ERRNO = - 1,
        /// <summary>
        /// The stream structure was inconsistent
        /// </summary>
		Z_STREAM_ERROR = - 2,
        /// <summary>
        /// Input data has been corrupted (for decompression).
        /// </summary>
		Z_DATA_ERROR = - 3,
        /// <summary>
        /// Memory allocation failed.
        /// </summary>
		Z_MEM_ERROR = - 4,
        /// <summary>
        /// There was not enough space in the output buffer.
        /// </summary>
		Z_BUF_ERROR = - 5,
        /// <summary>
        /// The version supplied does not match that supported by the ZLib module.
        /// </summary>
		Z_VERSION_ERROR = - 6
    }

    /// <summary>
    /// States of deflate operation
    /// </summary>
    internal enum DeflateState
    {
        INIT_STATE = 42,
        BUSY_STATE = 113,
        FINISH_STATE = 666
    }

    /// <summary>
    /// Data block types, i.e. binary or ascii text
    /// </summary>
    public enum BlockType
    {
        Z_BINARY = 0,
        Z_ASCII = 1,
        Z_UNKNOWN = 2
    }

    /// <summary>
    /// Helper class
    /// </summary>
    public static class ZLibUtil
    {
        #region Copy large array to a small one in several steps

        internal class CopyLargeArrayToSmall
        {

            private static byte[] srcBuf;
            private static int srcOff;
            private static int srcDataLen;
            private static byte[] destBuff;
            private static int destOff;
            private static int destLen;
            private static int nWritten;

            public static void Initialize(byte[] srcBuf, int srcOff, int srcDataLen, byte[] destBuff, int destOff, int destLen)
            {
                ZLibUtil.CopyLargeArrayToSmall.srcBuf = srcBuf;
                ZLibUtil.CopyLargeArrayToSmall.srcOff = srcOff;
                ZLibUtil.CopyLargeArrayToSmall.srcDataLen = srcDataLen;
                ZLibUtil.CopyLargeArrayToSmall.destBuff = destBuff;
                ZLibUtil.CopyLargeArrayToSmall.destOff = destOff;
                ZLibUtil.CopyLargeArrayToSmall.destLen = destLen;
                ZLibUtil.CopyLargeArrayToSmall.nWritten = 0;
            }
            
            public static int GetRemainingDataSize() { return srcDataLen; }

            /// <summary>
            /// Copies large array which was passed as srcBuf to the Initialize method into the destination array which were passes as destBuff
            /// </summary>
            /// <returns>The number of bytes copied</returns>
            public static int CopyData()
            {
                if (srcDataLen > destLen)
                {
                    Array.Copy(srcBuf, srcOff, destBuff, destOff, destLen);
                    srcDataLen -= destLen;
                    srcOff += destLen;
                    nWritten = destLen;
                    return nWritten;
                }
                else
                {
                    Array.Copy(srcBuf, srcOff, destBuff, destOff, srcDataLen);
                    nWritten = srcDataLen;
                    srcDataLen = 0;
                    return nWritten;
                }
            }
        }

        #endregion

        /// <summary>
        /// Max Window size
        /// </summary>
        public const int MAX_WBITS = 15; // 32K LZ77 Window

        internal static readonly byte[] mark = new byte[] { (byte)0, (byte)0, (byte)ZLibUtil.Identity(0xff), (byte)ZLibUtil.Identity(0xff) };

        /// <summary>
        /// preset dictionary flag in zlib header
        /// </summary>
        internal const int PRESET_DICT = 0x20;

        /// <summary>
        /// The size of the buffer
        /// </summary>
        internal const int zLibBufSize = 1048576;

        internal static readonly string[] z_errmsg = new System.String[] { "need dictionary", "stream End", "", "file error", "stream error", "data error", "insufficient memory", "buffer error", "incompatible version", "" };

        internal static readonly int[] inflate_mask = new int[] { 0x00000000, 0x00000001, 0x00000003, 0x00000007, 0x0000000f, 0x0000001f, 0x0000003f, 0x0000007f, 0x000000ff, 0x000001ff, 0x000003ff, 0x000007ff, 0x00000fff, 0x00001fff, 0x00003fff, 0x00007fff, 0x0000ffff };

        internal static readonly int[] border = new int[] { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

        /// <summary>
        /// Deflate compression method index
        /// </summary>
        internal const int Z_DEFLATED = 8;

        /// <summary>
		/// This method returns the literal value received
		/// </summary>
		/// <param name="literal">The literal to return</param>
		/// <returns>The received value</returns>
		internal static long Identity(long literal)
		{
			return literal;
		}

		/// <summary>
		/// This method returns the literal value received
		/// </summary>
		/// <param name="literal">The literal to return</param>
		/// <returns>The received value</returns>
        internal static ulong Identity(ulong literal)
		{
			return literal;
		}

		/// <summary>
		/// This method returns the literal value received
		/// </summary>
		/// <param name="literal">The literal to return</param>
		/// <returns>The received value</returns>
		internal static float Identity(float literal)
		{
			return literal;
		}

		/// <summary>
		/// This method returns the literal value received
		/// </summary>
		/// <param name="literal">The literal to return</param>
		/// <returns>The received value</returns>
		internal static double Identity(double literal)
		{
			return literal;
		}

		/*******************************/
		/// <summary>
		/// Performs an unsigned bitwise right shift with the specified number
		/// </summary>
		/// <param name="number">Number to operate on</param>
		/// <param name="bits">Ammount of bits to shift</param>
		/// <returns>The resulting number from the shift operation</returns>
		internal static int URShift(int number, int bits)
		{
			if ( number >= 0)
				return number >> bits;
			else
				return (number >> bits) + (2 << ~bits);
		}

		/// <summary>
		/// Performs an unsigned bitwise right shift with the specified number
		/// </summary>
		/// <param name="number">Number to operate on</param>
		/// <param name="bits">Ammount of bits to shift</param>
		/// <returns>The resulting number from the shift operation</returns>
		internal static int URShift(int number, long bits)
		{
			return URShift(number, (int)bits);
		}

		/// <summary>
		/// Performs an unsigned bitwise right shift with the specified number
		/// </summary>
		/// <param name="number">Number to operate on</param>
		/// <param name="bits">Ammount of bits to shift</param>
		/// <returns>The resulting number from the shift operation</returns>
		internal static long URShift(long number, int bits)
		{
			if ( number >= 0)
				return number >> bits;
			else
				return (number >> bits) + (2L << ~bits);
		}

		/// <summary>
		/// Performs an unsigned bitwise right shift with the specified number
		/// </summary>
		/// <param name="number">Number to operate on</param>
		/// <param name="bits">Ammount of bits to shift</param>
		/// <returns>The resulting number from the shift operation</returns>
		internal static long URShift(long number, long bits)
		{
			return URShift(number, (int)bits);
		}

		/*******************************/
		/// <summary>Reads a number of characters from the current source Stream and writes the data to the target array at the specified index.</summary>
		/// <param name="sourceStream">The source Stream to ReadPos from.</param>
		/// <param name="target">Contains the array of characters ReadPos from the source Stream.</param>
		/// <param name="start">The starting index of the target array.</param>
		/// <param name="count">The maximum number of characters to ReadPos from the source Stream.</param>
		/// <returns>The number of characters ReadPos. The number will be less than or equal to count depending on the data available in the source Stream. Returns -1 if the End of the stream is reached.</returns>
		internal static System.Int32 ReadInput(System.IO.Stream sourceStream, byte[] target, int start, int count)
		{
			// Returns 0 bytes if not enough space in target
			if (target.Length == 0)
				return 0;

			byte[] receiver = new byte[target.Length];
			int bytesRead   = sourceStream.Read(receiver, start, count);

			// Returns -1 if EOF
			if (bytesRead == 0)	
				return -1;
                
			for(int i = start; i < start + bytesRead; i++)
				target[i] = (byte)receiver[i];
                
			return bytesRead;
		}

		/// <summary>Reads a number of characters from the current source TextReader and writes the data to the target array at the specified index.</summary>
		/// <param name="sourceTextReader">The source TextReader to ReadPos from</param>
		/// <param name="target">Contains the array of characteres ReadPos from the source TextReader.</param>
		/// <param name="start">The starting index of the target array.</param>
		/// <param name="count">The maximum number of characters to ReadPos from the source TextReader.</param>
		/// <returns>The number of characters ReadPos. The number will be less than or equal to count depending on the data available in the source TextReader. Returns -1 if the End of the stream is reached.</returns>
		internal static System.Int32 ReadInput(System.IO.TextReader sourceTextReader, byte[] target, int start, int count)
		{
			// Returns 0 bytes if not enough space in target
			if (target.Length == 0) return 0;

			char[] charArray = new char[target.Length];
			int bytesRead = sourceTextReader.Read(charArray, start, count);

			// Returns -1 if EOF
			if (bytesRead == 0) return -1;

			for(int index=start; index<start+bytesRead; index++)
				target[index] = (byte)charArray[index];

			return bytesRead;
		}

		/// <summary>
		/// Converts a string to an array of bytes
		/// </summary>
		/// <param name="sourceString">The string to be converted</param>
		/// <returns>The new array of bytes</returns>
		internal static byte[] ToByteArray(string sourceString)
		{
			return Encoding.UTF8.GetBytes(sourceString);
		}

		/// <summary>
		/// Converts an array of bytes to an array of chars
		/// </summary>
		/// <param name="byteArray">The array of bytes to convert</param>
		/// <returns>The new array of chars</returns>
		internal static char[] ToCharArray(byte[] byteArray) 
		{
            return Encoding.UTF8.GetChars(byteArray);
        }


        #region Tree constants

        internal const int BL_CODES = 19;

        internal const int D_CODES = 30;

        internal const int LITERALS = 256;

        internal const int LENGTH_CODES = 29;

        internal const int L_CODES = (LITERALS + 1 + LENGTH_CODES);

        internal const int HEAP_SIZE = (2 * L_CODES + 1);

        // Bit length codes must not exceed MAX_BL_BITS bits
        internal const int MAX_BL_BITS = 7;

        // End of block literal code
        internal const int END_BLOCK = 256;

        // repeat previous bit length 3-6 times (2 bits of repeat count)
        internal const int REP_3_6 = 16;

        // repeat a zero length 3-10 times  (3 bits of repeat count)
        internal const int REPZ_3_10 = 17;

        // repeat a zero length 11-138 times  (7 bits of repeat count)
        internal const int REPZ_11_138 = 18;

        // extra bits for each length code		
        internal static readonly int[] extra_lbits = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };

        // extra bits for each distance code		
        internal static readonly int[] extra_dbits = new int[] { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };

        // extra bits for each bit length code		
        internal static readonly int[] extra_blbits = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 7 };

        internal static readonly byte[] bl_order = new byte[] { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };


        // The lengths of the bit length codes are sent in order of decreasing
        // probability, to avoid transmitting the lengths for unused bit
        // length codes.

        internal const int Buf_size = 8 * 2;

        /// <summary>
        /// see definition of array dist_code below
        /// </summary>
        internal const int DIST_CODE_LEN = 512;

        internal static readonly byte[] _dist_code = new byte[]{0, 1, 2, 3, 4, 4, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 0, 0, 16, 17, 18, 18, 19, 19, 20, 20, 20, 20, 21, 21, 21, 21, 22, 22, 22, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 
			29, 29, 29, 29, 29, 29, 29, 29, 29};

        internal static readonly byte[] _length_code = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 14, 15, 15, 15, 15, 16, 16, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 19, 19, 19, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28 };

        internal static readonly int[] base_length = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 16, 20, 24, 28, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 0 };

        internal static readonly int[] base_dist = new int[] { 0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512, 768, 1024, 1536, 2048, 3072, 4096, 6144, 8192, 12288, 16384, 24576 };


        #endregion
    }
}