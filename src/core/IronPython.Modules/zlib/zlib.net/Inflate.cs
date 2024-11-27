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
using System.Collections.Generic;

namespace ComponentAce.Compression.Libs.ZLib
{
    /// <summary>
    /// This enumeration contains modes of inflate processing
    /// </summary>
    internal enum InflateMode
    {	
        /// <summary>
        /// waiting for method byte
        /// </summary>
        METHOD = 0, 
        /// <summary>
        /// waiting for flag byte
        /// </summary>
        FLAG = 1,
        /// <summary>
        /// four dictionary check bytes to go
        /// </summary>
		DICT4 = 2,
        /// <summary>
        /// three dictionary check bytes to go
        /// </summary>
        DICT3 = 3,
        /// <summary>
        /// two dictionary check bytes to go
        /// </summary>
        DICT2 = 4,
        /// <summary>
        /// one dictionary check byte to go
        /// </summary>
        DICT1 = 5,
        /// <summary>
        /// waiting for inflateSetDictionary
        /// </summary>
		DICT0 = 6,
        /// <summary>
        /// decompressing blocks
        /// </summary>
		BLOCKS = 7,
        /// <summary>
        /// four check bytes to go
        /// </summary>
		CHECK4 = 8,
        /// <summary>
        /// three check bytes to go
        /// </summary>
		CHECK3 = 9,
        /// <summary>
        /// two check bytes to go
        /// </summary>
		CHECK2 = 10,
        /// <summary>
        /// one check byte to go
        /// </summary>
		CHECK1 = 11,
        /// <summary>
        /// finished check, done
        /// </summary>
		DONE = 12,
        /// <summary>
        /// got an error--stay here
        /// </summary>
		BAD = 13
    }


	internal sealed class Inflate
	{

        #region Fields

		/// <summary>
        /// current inflate mode
		/// </summary>
		public InflateMode mode;

        #region mode dependent information

        /// <summary>
        /// if FLAGS, method byte
        /// </summary>
        private int method;
		
		// if CHECK, check values to compare

        /// <summary>
        /// computed check value
        /// </summary>
        private long[] was = new long[1];

        /// <summary>
        /// stream check value
        /// </summary>
        private long need;
		
		/// <summary>
        /// if BAD, inflateSync's marker bytes count
		/// </summary>
        private int marker;

        #endregion

        #region mode independent information
        /// <summary>
        /// flag for no wrapper
        /// </summary>
        private int nowrap; 
        /// <summary>
        /// log2(Window size)  (8..15, defaults to 15)
        /// </summary>
        private int wbits;

	    private IEnumerator<object> gzipHeaderRemover;

	    private bool detectHeader;

        #endregion

        /// <summary>
        /// current inflate_blocks state
        /// </summary>
        private InfBlocks blocks;
		
        #endregion

        #region Methods

        /// <summary>
        /// Resets the Inflate algorithm
        /// </summary>
        /// <param name="z">A ZStream object</param>
        /// <returns>A result code</returns>
        internal int inflateReset(ZStream z)
        {
            if (z?.istate == null)
                return (int)ZLibResultCode.Z_STREAM_ERROR;
            
            z.total_in = z.total_out = 0;
            z.msg = null;
            z.istate.mode = z.istate.nowrap != 0? InflateMode.BLOCKS: InflateMode.METHOD;
            z.istate.blocks.reset(z, null);
            return (int)ZLibResultCode.Z_OK;
        }
        
        /// <summary>
        /// Finishes the inflate algorithm processing
        /// </summary>
        /// <param name="z">A ZStream object</param>
        /// <returns>Operation result code</returns>
		internal int inflateEnd(ZStream z)
		{
		    blocks?.free(z);
		    blocks = null;
			//    ZFREE(z, z->state);
			return (int)ZLibResultCode.Z_OK;
		}

        /// <summary>
        /// Initializes the inflate algorithm
        /// </summary>
        /// <param name="z">A ZStream object</param>
        /// <param name="windowBits">Window size</param>
        /// <returns>Operation result code</returns>
		internal int inflateInit(ZStream z, int windowBits)
		{
			z.msg = null;
			blocks = null;
			
			// handle undocumented nowrap option (no zlib header or check)
			nowrap = 0;
            detectHeader = false;
			if (windowBits < 0)
			{
                // deflate, no header
				windowBits = - windowBits;
				nowrap = 1;
            }
            else if ((windowBits & 16) != 0)
            {
                gzipHeaderRemover = GzipHeader.CreateRemover(z);
                windowBits &= ~16;

            }
            else if ((windowBits & 32) != 0) 
            {
                detectHeader = true;
                windowBits &= ~32;
            }

			// set Window size
			if (windowBits < 8 || windowBits > 15)
			{
				inflateEnd(z);
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			}
			wbits = windowBits;
			
			z.istate.blocks = new InfBlocks(z, z.istate.nowrap == 0, 1 << windowBits);
			
			// reset state
			inflateReset(z);
			return (int)ZLibResultCode.Z_OK;
		}


        /// <summary>
        /// Runs inflate algorithm
        /// </summary>
        /// <param name="z">A ZStream object</param>
        /// <param name="flush">Flush strategy</param>
        /// <returns>Operation result code</returns>
		internal int inflate(ZStream z, FlushStrategy flush)
		{
		    int internalFlush = (int)flush;

		    if (z?.istate == null || z.next_in == null)
                return (int)ZLibResultCode.Z_STREAM_ERROR;
		    int res_temp = internalFlush == (int)FlushStrategy.Z_FINISH
		        ? (int)ZLibResultCode.Z_BUF_ERROR
		        : (int)ZLibResultCode.Z_OK;
			int r = (int)ZLibResultCode.Z_BUF_ERROR;

            if (detectHeader)
            {
                if (z.avail_in == 0)
                    return r;
                if (z.next_in[z.next_in_index] == 0x1F)
                    gzipHeaderRemover = GzipHeader.CreateRemover(z);
                detectHeader = false;
            }

            if (gzipHeaderRemover != null)
            {
                if (z.avail_in == 0)
                    return r;
                if (gzipHeaderRemover.MoveNext())
                    return r;
                gzipHeaderRemover = null;
                z.istate.mode = InflateMode.BLOCKS;
                z.istate.blocks.needCheck = false;
                nowrap = 1;
            }

			while (true)
			{

				switch (z.istate.mode)
				{
					
					case  InflateMode.METHOD: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						if (((z.istate.method = z.next_in[z.next_in_index++]) & 0xf) != ZLibUtil.Z_DEFLATED)
						{
							z.istate.mode =  InflateMode.BAD;
							z.msg = "unknown compression method";
							z.istate.marker = 5; // can't try inflateSync
							break;
						}
						if ((z.istate.method >> 4) + 8 > z.istate.wbits)
						{
							z.istate.mode =  InflateMode.BAD;
							z.msg = "invalid Window size";
							z.istate.marker = 5; // can't try inflateSync
							break;
						}
						z.istate.mode =  InflateMode.FLAG;
						goto case  InflateMode.FLAG;
					
					case  InflateMode.FLAG: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						int b = (z.next_in[z.next_in_index++]) & 0xff;
						
						if ((((z.istate.method << 8) + b) % 31) != 0)
						{
							z.istate.mode =  InflateMode.BAD;
							z.msg = "incorrect header check";
							z.istate.marker = 5; // can't try inflateSync
							break;
						}
						
						if ((b & ZLibUtil.PRESET_DICT) == 0)
						{
							z.istate.mode =  InflateMode.BLOCKS;
							break;
						}
						z.istate.mode =  InflateMode.DICT4;
						goto case  InflateMode.DICT4;
					
					case  InflateMode.DICT4: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need = ((long)(z.next_in[z.next_in_index++] & 0xff) << 24) & unchecked((int) 0xff000000L);
						z.istate.mode =  InflateMode.DICT3;
						goto case  InflateMode.DICT3;
					
					case  InflateMode.DICT3: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need += (((long)(z.next_in[z.next_in_index++] & 0xff) << 16) & 0xff0000L);
						z.istate.mode =  InflateMode.DICT2;
						goto case  InflateMode.DICT2;
					
					case  InflateMode.DICT2: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need += (((long)(z.next_in[z.next_in_index++] & 0xff) << 8) & 0xff00L);
						z.istate.mode =  InflateMode.DICT1;
						goto case  InflateMode.DICT1;
					
					case  InflateMode.DICT1: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need += (z.next_in[z.next_in_index++] & 0xffL);
						z.adler = z.istate.need;
						z.istate.mode =  InflateMode.DICT0;
						return (int)ZLibResultCode.Z_NEED_DICT;
					
					case  InflateMode.DICT0: 
						z.istate.mode =  InflateMode.BAD;
						z.msg = "need dictionary";
						z.istate.marker = 0; // can try inflateSync
						return (int)ZLibResultCode.Z_STREAM_ERROR;
					
					case  InflateMode.BLOCKS:
						r = z.istate.blocks.proc(z, r);
						if (r == (int)ZLibResultCode.Z_DATA_ERROR)
						{
							z.istate.mode =  InflateMode.BAD;
							z.istate.marker = 0; // can try inflateSync
							break;
						}
						if (r == (int)ZLibResultCode.Z_OK)
						{
                            r = res_temp;
						}
						if (r != (int)ZLibResultCode.Z_STREAM_END)
						{
							return r;
						}
                        r = res_temp;
						z.istate.blocks.reset(z, z.istate.was);
						if (z.istate.nowrap != 0)
						{
							z.istate.mode =  InflateMode.DONE;
							break;
						}
						z.istate.mode =  InflateMode.CHECK4;
						goto case  InflateMode.CHECK4;
					
					case  InflateMode.CHECK4: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need = ((z.next_in[z.next_in_index++] & 0xff) << 24) & unchecked((int) 0xff000000L);
						z.istate.mode =  InflateMode.CHECK3;
						goto case  InflateMode.CHECK3;
					
					case  InflateMode.CHECK3: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need += (((z.next_in[z.next_in_index++] & 0xff) << 16) & 0xff0000L);
						z.istate.mode =  InflateMode.CHECK2;
						goto case  InflateMode.CHECK2;
					
					case  InflateMode.CHECK2: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need += (((z.next_in[z.next_in_index++] & 0xff) << 8) & 0xff00L);
						z.istate.mode =  InflateMode.CHECK1;
						goto case  InflateMode.CHECK1;
					
					case  InflateMode.CHECK1: 
						
						if (z.avail_in == 0)
                            return r; r = res_temp;
						
						z.avail_in--; z.total_in++;
						z.istate.need += (z.next_in[z.next_in_index++] & 0xffL);
						
						if (unchecked(((int) (z.istate.was[0])) != ((int) (z.istate.need))))
						{
							z.istate.mode =  InflateMode.BAD;
							z.msg = "incorrect data check";
							z.istate.marker = 5; // can't try inflateSync
							break;
						}
						
						z.istate.mode =  InflateMode.DONE;
						goto case  InflateMode.DONE;
					
					case  InflateMode.DONE: 
						return (int)ZLibResultCode.Z_STREAM_END;
					
					case  InflateMode.BAD: 
						return (int)ZLibResultCode.Z_DATA_ERROR;
					
					default: 
						return (int)ZLibResultCode.Z_STREAM_ERROR;
					
				}
			}
		}
		
		/// <summary>
		/// Sets dictionary for the inflate operation
		/// </summary>
		/// <param name="z">A ZStream object</param>
		/// <param name="dictionary">An array of byte - dictionary</param>
		/// <param name="dictLength">Dictionary length</param>
		/// <returns>Operation result code</returns>
		internal int inflateSetDictionary(ZStream z, byte[] dictionary, int dictLength)
		{
			int index = 0;
			int length = dictLength;
			if (z?.istate?.mode != InflateMode.DICT0)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			
			if (Adler32.GetAdler32Checksum(1L, dictionary, 0, dictLength) != z.adler)
			{
				return (int)ZLibResultCode.Z_DATA_ERROR;
			}
			
			z.adler = Adler32.GetAdler32Checksum(0, null, 0, 0);
			
			if (length >= (1 << z.istate.wbits))
			{
				length = (1 << z.istate.wbits) - 1;
				index = dictLength - length;
			}
			z.istate.blocks.set_dictionary(dictionary, index, length);
			z.istate.mode =  InflateMode.BLOCKS;
			return (int)ZLibResultCode.Z_OK;
		}
		
	
        /// <summary>
        /// Inflate synchronization
        /// </summary>
        /// <param name="z">A ZStream object</param>
        /// <returns>Operation result code</returns>
		internal int inflateSync(ZStream z)
		{
			int n; // number of bytes to look at
			int p; // pointer to bytes
			int m; // number of marker bytes found in a row
			long r, w; // temporaries to save _total_in and _total_out
			
			// set up
			if (z?.istate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			if (z.istate.mode !=  InflateMode.BAD)
			{
				z.istate.mode =  InflateMode.BAD;
				z.istate.marker = 0;
			}
			if ((n = z.avail_in) == 0)
				return (int)ZLibResultCode.Z_BUF_ERROR;
			p = z.next_in_index;
			m = z.istate.marker;
			
			// search
			while (n != 0 && m < 4)
			{
				if (z.next_in[p] == ZLibUtil.mark[m])
				{
					m++;
				}
				else if (z.next_in[p] != 0)
				{
					m = 0;
				}
				else
				{
					m = 4 - m;
				}
				p++; n--;
			}
			
			// restore
			z.total_in += p - z.next_in_index;
			z.next_in_index = p;
			z.avail_in = n;
			z.istate.marker = m;
			
			// return no joy or set up to restart on a new block
			if (m != 4)
			{
				return (int)ZLibResultCode.Z_DATA_ERROR;
			}
			r = z.total_in; w = z.total_out;
			inflateReset(z);
			z.total_in = r; z.total_out = w;
			z.istate.mode =  InflateMode.BLOCKS;
			return (int)ZLibResultCode.Z_OK;
		}
		
        ///<summary>
		/// Returns true if inflate is currently at the End of a block generated
		/// by Z_SYNC_FLUSH or Z_FULL_FLUSH. This function is used by one PPP
		/// implementation to provide an additional safety check. PPP uses Z_SYNC_FLUSH
		/// but removes the length bytes of the resulting empty stored block. When
		/// decompressing, PPP checks that at the End of input packet, inflate is
		/// waiting for these length bytes.
        /// </summary>
		internal int inflateSyncPoint(ZStream z)
		{
			if (z?.istate?.blocks == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			return z.istate.blocks.sync_point();
        }

        #endregion
    }

    internal class GzipHeader {

        /// <summary>
        /// Creates header remover.
        /// As long as header is not completed, call to Remover.MoveNext() returns true and
        /// adjust state of z.
        /// </summary>
        /// <param name="z">Stream where gzip header will appear.</param>
        /// <returns></returns>
        public static IEnumerator<object> CreateRemover(ZStream z) {
            return new GzipHeader().StartHeaderSkipping(z).GetEnumerator();
        }

        [Flags]
        private enum HEADER_FLAG {
            // FTEXT = 1,
            FHCRC = 2,
            FEXTRA = 4,
            FNAME = 8,
            FCOMMENT = 16
        }

        private const int FIXED_HEADER_SIZE = 10;

        private byte GetNext(ZStream z) {
            z.avail_in--;
            z.total_in++;
            return z.next_in[z.next_in_index++];
        }

        private IEnumerable<object> StartHeaderSkipping(ZStream z) {
            var headerCollector = new List<byte>(FIXED_HEADER_SIZE);
            do {
                if (z.avail_in == 0)
                    yield return false;
                headerCollector.Add(GetNext(z));
            } while (headerCollector.Count < FIXED_HEADER_SIZE);

            var flag = headerCollector[3];
            if (0 != (flag & (byte)HEADER_FLAG.FEXTRA)) {
                if (z.avail_in == 0)
                    yield return null;
                var outstandingSize = (int)GetNext(z);
                if (z.avail_in == 0)
                    yield return null;
                outstandingSize += 256 * GetNext(z);
                do {
                    if (z.avail_in == 0)
                        yield return null;
                    GetNext(z);
                } while (--outstandingSize != 0);
            }

            // STATE_NAME
            if (0 != (flag & (byte)HEADER_FLAG.FNAME)) {
                do {
                    if (z.avail_in == 0) {
                        yield return null;
                    }
                } while (GetNext(z) != 0);
            }

            // STATE_COMMENT:
            if (0 != (flag & (byte)HEADER_FLAG.FCOMMENT)) {
                do {
                    if (z.avail_in == 0)
                        yield return null;
                } while (GetNext(z) != 0);
            }

            // STATE_CRC:
            if (0 != (flag & (byte)HEADER_FLAG.FHCRC)) {
                var outstandingSize = 4;
                do {
                    if (z.avail_in == 0) {
                        yield return null;
                    }
                    GetNext(z);
                } while (--outstandingSize != 0);
            }
        }
    }
}
