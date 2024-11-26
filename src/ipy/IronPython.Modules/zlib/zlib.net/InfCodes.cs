// Copyright (c) 2006, ComponentAce
// http://www.componentace.com
// All rights reserved.

// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

// Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer. 
// Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution. 
// Neither the name of ComponentAce nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission. 
// THIS SOFTWARE IS PROVIDED BY THE  InflateCodesMode.COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABI InflateCodesMode.LITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE  InflateCodesMode.COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABI InflateCodesMode.LITY, WHETHER IN CONTRACT, STRICT LIABI InflateCodesMode.LITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBI InflateCodesMode.LITY OF SUCH DAMAGE.

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
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABI InflateCodesMode.LITY AND
FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL JCRAFT,
INC. OR ANY CONTRIBUTORS TO THIS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABI InflateCodesMode.LITY, WHETHER IN CONTRACT, STRICT LIABI InflateCodesMode.LITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBI InflateCodesMode.LITY OF SUCH DAMAGE.
*/
/*
* This program is based on zlib-1.1.3, so all credit should go authors
* Jean-loup Gailly(jloup@gzip.org) and Mark Adler(madler@alumni.caltech.edu)
* and contributors of zlib.
*/
using System;

namespace ComponentAce.Compression.Libs.ZLib
{

    /// <summary>
    /// Inflate codes mode
    /// </summary>
    internal enum InflateCodesMode
    {
         START = 0, // x: set up for  InflateCodesMode.LEN
		 LEN = 1, // i: get length/literal/eob next
		 LENEXT = 2, // i: getting length extra (have base)
		 DIST = 3, // i: get distance next
		 DISTEXT = 4, // i: getting distance extra
		 COPY = 5, // o: copying bytes in Window, waiting for space
		 LIT = 6, // o: got literal, waiting for output space
		 WASH = 7, // o: got eob, possibly still output waiting
		 END = 8, // x: got eob and all data flushed
		 BADCODE = 9 // x: got error
    }

    /// <summary>
    /// This class is used by the InfBlocks class
    /// </summary>
	internal sealed class InfCodes
	{
				
        #region Fields

        /// <summary>
        /// current inflate_codes mode
        /// </summary>
        private InflateCodesMode mode;

        // mode dependent information


        /// <summary>
        /// length
        /// </summary>        
        private int count;

        /// <summary>
        /// pointer into tree
        /// </summary>
        private int[] tree;

        /// <summary>
        /// current index of the tree
        /// </summary>
        internal int tree_index = 0;

        /// <summary>
        /// 
        /// </summary>
        internal int need; // bits needed

        internal int lit;

        // if EXT or  InflateCodesMode.COPY, where and how much
        internal int get_Renamed; // bits to get for extra
        internal int dist; // distance back to copy from

        /// <summary>
        /// ltree bits decoded per branch
        /// </summary>
        private byte lbits;

        /// <summary>
        /// dtree bits decoded per branch
        /// </summary>
		private byte dbits;

        /// <summary>
        /// literal/length/eob tree
        /// </summary>
		private int[] ltree;

        /// <summary>
        /// literal/length/eob tree index
        /// </summary>
		private int ltree_index;

        /// <summary>
        /// distance tree
        /// </summary>
		private int[] dtree;

        /// <summary>
        /// distance tree index
        /// </summary>
		private int dtree_index;

        #endregion

        #region Methods

        /// <summary>
        /// Constructor which takes literal, distance trees, corresponding bites decoded for branches, corresponding indexes and a ZStream object 
        /// </summary>        
        internal InfCodes(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index, ZStream z)
		{
			mode =  InflateCodesMode.START;
			lbits = (byte) bl;
			dbits = (byte) bd;
			ltree = tl;
			ltree_index = tl_index;
			dtree = td;
			dtree_index = td_index;
		}

        /// <summary>
        /// Constructor which takes literal, distance trees, corresponding bites decoded for branches and a ZStream object 
        /// </summary>   
		internal InfCodes(int bl, int bd, int[] tl, int[] td, ZStream z)
		{
			mode =  InflateCodesMode.START;
			lbits = (byte) bl;
			dbits = (byte) bd;
			ltree = tl;
			ltree_index = 0;
			dtree = td;
			dtree_index = 0;
		}
		
        /// <summary>
        /// Block processing method
        /// </summary>
        /// <param name="s">An instance of the InfBlocks class</param>
        /// <param name="z">A ZStream object</param>
        /// <param name="r">A result code</param>
		internal int proc(InfBlocks s, ZStream z, int r)
		{
			int j; // temporary storage
			 //int[] t; // temporary pointer
			int tindex; // temporary pointer
			int e; // extra bits or operation
			int b = 0; // bit buffer
			int k = 0; // bits in bit buffer
			int p = 0; // input data pointer
			int n; // bytes available there
			int q; // output Window WritePos pointer
			int m; // bytes to End of Window or ReadPos pointer
			int f; // pointer to copy strings from
			
			// copy input/output information to locals (UPDATE macro restores)
			p = z.next_in_index; n = z.avail_in; b = s.BitB; k = s.BitK;
			q = s.WritePos; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
			
			// process input and output based on current state
			while (true)
			{
				switch (mode)
				{
					
					// waiting for "i:"=input, "o:"=output, "x:"=nothing
					case  InflateCodesMode.START:  // x: set up for  InflateCodesMode.LEN
						if (m >= 258 && n >= 10)
						{
							
							s.BitB = b; s.BitK = k;
							z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
							s.WritePos = q;
							r = inflate_fast(lbits, dbits, ltree, ltree_index, dtree, dtree_index, s, z);
							
							p = z.next_in_index; n = z.avail_in; b = s.BitB; k = s.BitK;
							q = s.WritePos; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
							
							if (r != (int)ZLibResultCode.Z_OK)
							{
								mode = r == (int)ZLibResultCode.Z_STREAM_END? InflateCodesMode.WASH: InflateCodesMode.BADCODE;
								break;
							}
						}
						need = lbits;
						tree = ltree;
						tree_index = ltree_index;
						
						mode =  InflateCodesMode.LEN;
						goto case  InflateCodesMode.LEN;
					
					case  InflateCodesMode.LEN:  // i: get length/literal/eob next
						j = need;
						
						while (k < (j))
						{
							if (n != 0)
								r = (int)ZLibResultCode.Z_OK;
							else
							{
								
								s.BitB = b; s.BitK = k;
								z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
								s.WritePos = q;
								return s.inflate_flush(z, r);
							}
							n--;
							b |= (z.next_in[p++] & 0xff) << k;
							k += 8;
						}
						
						tindex = (tree_index + (b & ZLibUtil.inflate_mask[j])) * 3;
						
						b = ZLibUtil.URShift(b, (tree[tindex + 1]));
						k -= (tree[tindex + 1]);
						
						e = tree[tindex];
						
						if (e == 0)
						{
							// literal
							lit = tree[tindex + 2];
							mode =  InflateCodesMode.LIT;
							break;
						}
						if ((e & 16) != 0)
						{
							// length
							get_Renamed = e & 15;
							count = tree[tindex + 2];
							mode =  InflateCodesMode.LENEXT;
							break;
						}
						if ((e & 64) == 0)
						{
							// next table
							need = e;
							tree_index = tindex / 3 + tree[tindex + 2];
							break;
						}
						if ((e & 32) != 0)
						{
							// End of block
							mode =  InflateCodesMode.WASH;
							break;
						}
						mode =  InflateCodesMode.BADCODE; // invalid code
						z.msg = "invalid literal/length code";
						r = (int)ZLibResultCode.Z_DATA_ERROR;
						
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						return s.inflate_flush(z, r);
					
					
					case  InflateCodesMode.LENEXT:  // i: getting length extra (have base)
						j = get_Renamed;
						
						while (k < (j))
						{
							if (n != 0)
								r = (int)ZLibResultCode.Z_OK;
							else
							{
								
								s.BitB = b; s.BitK = k;
								z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
								s.WritePos = q;
								return s.inflate_flush(z, r);
							}
							n--; b |= (z.next_in[p++] & 0xff) << k;
							k += 8;
						}
						
						count += (b & ZLibUtil.inflate_mask[j]);
						
						b >>= j;
						k -= j;
						
						need = dbits;
						tree = dtree;
						tree_index = dtree_index;
						mode =  InflateCodesMode.DIST;
						goto case  InflateCodesMode.DIST;
					
					case  InflateCodesMode.DIST:  // i: get distance next
						j = need;
						
						while (k < (j))
						{
							if (n != 0)
								r = (int)ZLibResultCode.Z_OK;
							else
							{
								
								s.BitB = b; s.BitK = k;
								z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
								s.WritePos = q;
								return s.inflate_flush(z, r);
							}
							n--; b |= (z.next_in[p++] & 0xff) << k;
							k += 8;
						}
						
						tindex = (tree_index + (b & ZLibUtil.inflate_mask[j])) * 3;
						
						b >>= tree[tindex + 1];
						k -= tree[tindex + 1];
						
						e = (tree[tindex]);
						if ((e & 16) != 0)
						{
							// distance
							get_Renamed = e & 15;
							dist = tree[tindex + 2];
							mode =  InflateCodesMode.DISTEXT;
							break;
						}
						if ((e & 64) == 0)
						{
							// next table
							need = e;
							tree_index = tindex / 3 + tree[tindex + 2];
							break;
						}
						mode =  InflateCodesMode.BADCODE; // invalid code
						z.msg = "invalid distance code";
						r = (int)ZLibResultCode.Z_DATA_ERROR;
						
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						return s.inflate_flush(z, r);
					
					
					case  InflateCodesMode.DISTEXT:  // i: getting distance extra
						j = get_Renamed;
						
						while (k < (j))
						{
							if (n != 0)
								r = (int)ZLibResultCode.Z_OK;
							else
							{
								
								s.BitB = b; s.BitK = k;
								z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
								s.WritePos = q;
								return s.inflate_flush(z, r);
							}
							n--; b |= (z.next_in[p++] & 0xff) << k;
							k += 8;
						}
						
						dist += (b & ZLibUtil.inflate_mask[j]);
						
						b >>= j;
						k -= j;
						
						mode =  InflateCodesMode.COPY;
						goto case  InflateCodesMode.COPY;
					
					case  InflateCodesMode.COPY:  // o: copying bytes in Window, waiting for space
						f = q - dist;
						while (f < 0)
						{
							// modulo Window size-"while" instead
							f += s.End; // of "if" handles invalid distances
						}
						while (count != 0)
						{
							
							if (m == 0)
							{
								if (q == s.End && s.ReadPos != 0)
								{
									q = 0; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
								}
								if (m == 0)
								{
									s.WritePos = q; r = s.inflate_flush(z, r);
									q = s.WritePos; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
									
									if (q == s.End && s.ReadPos != 0)
									{
										q = 0; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
									}
									
									if (m == 0)
									{
										s.BitB = b; s.BitK = k;
										z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
										s.WritePos = q;
										return s.inflate_flush(z, r);
									}
								}
							}
							
							s.Window[q++] = s.Window[f++]; m--;
							
							if (f == s.End)
								f = 0;
							count--;
						}
						mode =  InflateCodesMode.START;
						break;
					
					case  InflateCodesMode.LIT:  // o: got literal, waiting for output space
						if (m == 0)
						{
							if (q == s.End && s.ReadPos != 0)
							{
								q = 0; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
							}
							if (m == 0)
							{
								s.WritePos = q; r = s.inflate_flush(z, r);
								q = s.WritePos; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
								
								if (q == s.End && s.ReadPos != 0)
								{
									q = 0; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
								}
								if (m == 0)
								{
									s.BitB = b; s.BitK = k;
									z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
									s.WritePos = q;
									return s.inflate_flush(z, r);
								}
							}
						}
						r = (int)ZLibResultCode.Z_OK;
						
						s.Window[q++] = (byte) lit; m--;
						
						mode =  InflateCodesMode.START;
						break;
					
					case  InflateCodesMode.WASH:  // o: got eob, possibly more output
						if (k > 7)
						{
							// return unused byte, if any
							k -= 8;
							n++;
							p--; // can always return one
						}
						
						s.WritePos = q; r = s.inflate_flush(z, r);
						q = s.WritePos; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
						
						if (s.ReadPos != s.WritePos)
						{
							s.BitB = b; s.BitK = k;
							z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
							s.WritePos = q;
							return s.inflate_flush(z, r);
						}
						mode =  InflateCodesMode.END;
						goto case  InflateCodesMode.END;
					
					case  InflateCodesMode.END: 
						r = (int)ZLibResultCode.Z_STREAM_END;
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						return s.inflate_flush(z, r);
					
					
					case  InflateCodesMode.BADCODE:  // x: got error
						
						r = (int)ZLibResultCode.Z_DATA_ERROR;
						
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						return s.inflate_flush(z, r);
					
					
					default: 
						r = (int)ZLibResultCode.Z_STREAM_ERROR;
						
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						return s.inflate_flush(z, r);
					
				}
			}
		}
		
        /// <summary>
        /// Frees allocated resources
        /// </summary>
		internal void  free(ZStream z)
		{
		}
		

	    /// <summary>
        /// Fast inflate procedure. Called with number of bytes left to WritePos in Window at least 258
        /// (the maximum string length) and number of input bytes available
        /// at least ten.  The ten bytes are six bytes for the longest length/
        /// distance pair plus four bytes for overloading the bit buffer.
	    /// </summary>
		internal int inflate_fast(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index, InfBlocks s, ZStream z)
		{
			int t; // temporary pointer
			int[] tp; // temporary pointer
			int tp_index; // temporary pointer
			int e; // extra bits or operation
			int b; // bit buffer
			int k; // bits in bit buffer
			int p; // input data pointer
			int n; // bytes available there
			int q; // output Window WritePos pointer
			int m; // bytes to End of Window or ReadPos pointer
			int ml; // mask for literal/length tree
			int md; // mask for distance tree
			int c; // bytes to copy
			int d; // distance back to copy from
			int r; // copy source pointer
			
			// load input, output, bit values
			p = z.next_in_index; n = z.avail_in; b = s.BitB; k = s.BitK;
			q = s.WritePos; m = q < s.ReadPos?s.ReadPos - q - 1:s.End - q;
			
			// initialize masks
			ml = ZLibUtil.inflate_mask[bl];
			md = ZLibUtil.inflate_mask[bd];
			
			// do until not enough input or output space for fast loop
			do 
			{
				// assume called with m >= 258 && n >= 10
				// get literal/length code
				while (k < (20))
				{
					// max bits for literal/length code
					n--;
					b |= (z.next_in[p++] & 0xff) << k; k += 8;
				}
				
				t = b & ml;
				tp = tl;
				tp_index = tl_index;
				if ((e = tp[(tp_index + t) * 3]) == 0)
				{
					b >>= (tp[(tp_index + t) * 3 + 1]); k -= (tp[(tp_index + t) * 3 + 1]);
					
					s.Window[q++] = (byte) tp[(tp_index + t) * 3 + 2];
					m--;
					continue;
				}
				do 
				{
					
					b >>= (tp[(tp_index + t) * 3 + 1]); k -= (tp[(tp_index + t) * 3 + 1]);
					
					if ((e & 16) != 0)
					{
						e &= 15;
						c = tp[(tp_index + t) * 3 + 2] + ((int) b & ZLibUtil.inflate_mask[e]);
						
						b >>= e; k -= e;
						
						// decode distance base of block to copy
						while (k < (15))
						{
							// max bits for distance code
							n--;
							b |= (z.next_in[p++] & 0xff) << k; k += 8;
						}
						
						t = b & md;
						tp = td;
						tp_index = td_index;
						e = tp[(tp_index + t) * 3];
						
						do 
						{
							
							b >>= (tp[(tp_index + t) * 3 + 1]); k -= (tp[(tp_index + t) * 3 + 1]);
							
							if ((e & 16) != 0)
							{
								// get extra bits to add to distance base
								e &= 15;
								while (k < (e))
								{
									// get extra bits (up to 13)
									n--;
									b |= (z.next_in[p++] & 0xff) << k; k += 8;
								}
								
								d = tp[(tp_index + t) * 3 + 2] + (b & ZLibUtil.inflate_mask[e]);
								
								b >>= (e); k -= (e);
								
								// do the copy
								m -= c;
								if (q >= d)
								{
									// offset before dest
									//  just copy
									r = q - d;
									if (q - r > 0 && 2 > (q - r))
									{
										s.Window[q++] = s.Window[r++]; c--; // minimum count is three,
										s.Window[q++] = s.Window[r++]; c--; // so unroll loop a little
									}
									else
									{
										Array.Copy(s.Window, r, s.Window, q, 2);
										q += 2; r += 2; c -= 2;
									}
								}
								else
								{
									// else offset after destination
									r = q - d;
									do 
									{
										r += s.End; // force pointer in Window
									}
									while (r < 0); // covers invalid distances
									e = s.End - r;
									if (c > e)
									{
										// if source crosses,
										c -= e; // wrapped copy
										if (q - r > 0 && e > (q - r))
										{
											do 
											{
												s.Window[q++] = s.Window[r++];
											}
											while (--e != 0);
										}
										else
										{
											Array.Copy(s.Window, r, s.Window, q, e);
											q += e; r += e; e = 0;
										}
										r = 0; // copy rest from start of Window
									}
								}
								
								// copy all or what's left
								if (q - r > 0 && c > (q - r))
								{
									do 
									{
										s.Window[q++] = s.Window[r++];
									}
									while (--c != 0);
								}
								else
								{
									Array.Copy(s.Window, r, s.Window, q, c);
									q += c; r += c; c = 0;
								}
								break;
							}
							else if ((e & 64) == 0)
							{
								t += tp[(tp_index + t) * 3 + 2];
								t += (b & ZLibUtil.inflate_mask[e]);
								e = tp[(tp_index + t) * 3];
							}
							else
							{
								z.msg = "invalid distance code";
								
								c = z.avail_in - n; c = (k >> 3) < c?k >> 3:c; n += c; p -= c; k -= (c << 3);
								
								s.BitB = b; s.BitK = k;
								z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
								s.WritePos = q;
								
								return (int)ZLibResultCode.Z_DATA_ERROR;
							}
						}
						while (true);
						break;
					}
					
					if ((e & 64) == 0)
					{
						t += tp[(tp_index + t) * 3 + 2];
						t += (b & ZLibUtil.inflate_mask[e]);
						if ((e = tp[(tp_index + t) * 3]) == 0)
						{
							
							b >>= (tp[(tp_index + t) * 3 + 1]); k -= (tp[(tp_index + t) * 3 + 1]);
							
							s.Window[q++] = (byte) tp[(tp_index + t) * 3 + 2];
							m--;
							break;
						}
					}
					else if ((e & 32) != 0)
					{
						
						c = z.avail_in - n; c = (k >> 3) < c?k >> 3:c; n += c; p -= c; k -= (c << 3);
						
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						
						return (int)ZLibResultCode.Z_STREAM_END;
					}
					else
					{
						z.msg = "invalid literal/length code";
						
						c = z.avail_in - n; c = (k >> 3) < c?k >> 3:c; n += c; p -= c; k -= (c << 3);
						
						s.BitB = b; s.BitK = k;
						z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
						s.WritePos = q;
						
						return (int)ZLibResultCode.Z_DATA_ERROR;
					}
				}
				while (true);
			}
			while (m >= 258 && n >= 10);
			
			// not enough input or output--restore pointers and return
			c = z.avail_in - n; c = (k >> 3) < c?k >> 3:c; n += c; p -= c; k -= (c << 3);
			
			s.BitB = b; s.BitK = k;
			z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
			s.WritePos = q;
			
			return (int)ZLibResultCode.Z_OK;
		}

        #endregion
    }
}