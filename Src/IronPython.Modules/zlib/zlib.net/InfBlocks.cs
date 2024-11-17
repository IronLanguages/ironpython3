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

namespace ComponentAce.Compression.Libs.ZLib {

    internal enum InflateBlockMode {
        TYPE = 0, // get type bits (3, including End bit)
        LENS = 1, // get lengths for stored
        STORED = 2, // processing stored block
        TABLE = 3, // get table lengths
        BTREE = 4, // get bit lengths tree for a dynamic block
        DTREE = 5, // get length, distance trees for a dynamic block
        CODES = 6, // processing fixed or dynamic block
        DRY = 7, // output remaining Window bytes
        DONE = 8, // finished last block, done
        BAD = 9 // a data error--stuck here
    }

    internal sealed class InfBlocks {
        #region Fields

        private const int MANY = 1440;

        /// <summary>
        /// current inflate_block mode 
        /// </summary>
        private InflateBlockMode mode;

        /// <summary>
        /// if STORED, bytes left to copy 
        /// </summary>
        private int left;

        /// <summary>
        /// table lengths (14 bits) 
        /// </summary>
        private int table;

        /// <summary>
        /// index into blens (or border) 
        /// </summary>
        private int index;

        /// <summary>
        /// bit lengths of codes 
        /// </summary>
        private int[] blens;

        /// <summary>
        /// bit length tree depth 
        /// </summary>
        private int[] bb = new int[1];

        /// <summary>
        /// bit length decoding tree 
        /// </summary>
        private int[] tb = new int[1];

        /// <summary>
        /// if CODES, current state 
        /// </summary>
        private InfCodes codes;

        /// <summary>
        /// true if this block is the last block 
        /// </summary>
        private int last;

        // mode independent information 

        /// <summary>
        /// single malloc for tree space 
        /// </summary>
        private int[] hufts;

        /// <summary>
        /// need check
        /// </summary>
        internal bool needCheck;

        /// <summary>
        /// check on output 
        /// </summary>
        private long check;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the sliding window.
        /// </summary>
        public byte[] Window { get; set; }

        /// <summary>
        /// Gets or sets the one byte after sliding Window.
        /// </summary>
        public int End { get; set; }

        /// <summary>
        /// Gets or sets the Window ReadPos pointer.
        /// </summary>
        public int ReadPos { get; set; }

        /// <summary>
        /// Gets or sets the Window WritePos pointer.
        /// </summary>
        public int WritePos { get; set; }

        /// <summary>
        /// Gets or sets the bits in bit buffer. 
        /// </summary>
        public int BitK { get; set; }

        /// <summary>
        /// Gets or sets the bit buffer.
        /// </summary>
        public int BitB { get; set; }

        #endregion

        #region Methods

        internal InfBlocks(ZStream z, bool needCheck, int w) {
            hufts = new int[MANY * 3];
            Window = new byte[w];
            End = w;
            this.needCheck = needCheck;
            mode = InflateBlockMode.TYPE;
            reset(z, null);
        }

        /// <summary>
        /// Resets this InfBlocks class instance
        /// </summary>
        internal void reset(ZStream z, long[] c) {
            if (c != null)
                c[0] = check;
            if (mode == InflateBlockMode.BTREE || mode == InflateBlockMode.DTREE) {
                blens = null;
            }
            if (mode == InflateBlockMode.CODES) {
                codes.free(z);
            }
            mode = InflateBlockMode.TYPE;
            BitK = 0;
            BitB = 0;
            ReadPos = WritePos = 0;

            if (this.needCheck)
                z.adler = check = Adler32.GetAdler32Checksum(0L, null, 0, 0);
        }

        /// <summary>
        /// Block processing functions
        /// </summary>
        internal int proc(ZStream z, int r) {
            int t; // temporary storage
            int b; // bit buffer
            int k; // bits in bit buffer
            int p; // input data pointer
            int n; // bytes available there
            int q; // output Window WritePos pointer
            int m; // bytes to End of Window or ReadPos pointer

            // copy input/output information to locals (UPDATE macro restores)
            {
                p = z.next_in_index; n = z.avail_in; b = BitB; k = BitK;
            }
            {
                q = WritePos; m = (int)(q < ReadPos ? ReadPos - q - 1 : End - q);
            }

            // process input based on current state
            while (true) {
                switch (mode) {

                    case InflateBlockMode.TYPE:

                        while (k < (3)) {
                            if (n != 0) {
                                r = (int)ZLibResultCode.Z_OK;
                            } else {
                                BitB = b; BitK = k;
                                z.avail_in = n;
                                z.total_in += p - z.next_in_index; z.next_in_index = p;
                                WritePos = q;
                                return inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }
                        t = (int)(b & 7);
                        last = t & 1;

                        switch (ZLibUtil.URShift(t, 1)) {

                            case 0:  // stored 
                                {
                                    b = ZLibUtil.URShift(b, (3)); k -= (3);
                                }
                                t = k & 7; // go to byte boundary
                                {
                                    b = ZLibUtil.URShift(b, (t)); k -= (t);
                                }
                                mode = InflateBlockMode.LENS; // get length of stored block
                                break;

                            case 1:  // fixed
                                {
                                    int[] bl = new int[1];
                                    int[] bd = new int[1];
                                    int[][] tl = new int[1][];
                                    int[][] td = new int[1][];

                                    InfTree.inflate_trees_fixed(bl, bd, tl, td, z);
                                    codes = new InfCodes(bl[0], bd[0], tl[0], td[0], z);
                                } {
                                    b = ZLibUtil.URShift(b, (3)); k -= (3);
                                }

                                mode = InflateBlockMode.CODES;
                                break;

                            case 2:  // dynamic
                                {
                                    b = ZLibUtil.URShift(b, (3)); k -= (3);
                                }

                                mode = InflateBlockMode.TABLE;
                                break;

                            case 3:  // illegal
                                {
                                    b = ZLibUtil.URShift(b, (3)); k -= (3);
                                }
                                mode = InflateBlockMode.BAD;
                                z.msg = "invalid block type";
                                r = (int)ZLibResultCode.Z_DATA_ERROR;

                                BitB = b; BitK = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                WritePos = q;
                                return inflate_flush(z, r);
                        }
                        break;

                    case InflateBlockMode.LENS:

                        while (k < (32)) {
                            if (n != 0) {
                                r = (int)ZLibResultCode.Z_OK;
                            } else {
                                BitB = b; BitK = k;
                                z.avail_in = n;
                                z.total_in += p - z.next_in_index; z.next_in_index = p;
                                WritePos = q;
                                return inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }

                        if (((ZLibUtil.URShift((~b), 16)) & 0xffff) != (b & 0xffff)) {
                            mode = InflateBlockMode.BAD;
                            z.msg = "invalid stored block lengths";
                            r = (int)ZLibResultCode.Z_DATA_ERROR;

                            BitB = b; BitK = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            WritePos = q;
                            return inflate_flush(z, r);
                        }
                        left = (b & 0xffff);
                        b = k = 0; // dump bits
                        mode = (left != 0) ? InflateBlockMode.STORED : (last != 0 ? InflateBlockMode.DRY : InflateBlockMode.TYPE);
                        break;

                    case InflateBlockMode.STORED:
                        if (n == 0) {
                            BitB = b; BitK = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            WritePos = q;
                            return inflate_flush(z, r);
                        }

                        if (m == 0) {
                            if (q == End && ReadPos != 0) {
                                q = 0; m = (int)(q < ReadPos ? ReadPos - q - 1 : End - q);
                            }
                            if (m == 0) {
                                WritePos = q;
                                r = inflate_flush(z, r);
                                q = WritePos; m = (int)(q < ReadPos ? ReadPos - q - 1 : End - q);
                                if (q == End && ReadPos != 0) {
                                    q = 0; m = (int)(q < ReadPos ? ReadPos - q - 1 : End - q);
                                }
                                if (m == 0) {
                                    BitB = b; BitK = k;
                                    z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                    WritePos = q;
                                    return inflate_flush(z, r);
                                }
                            }
                        }
                        r = (int)ZLibResultCode.Z_OK;

                        t = left;
                        if (t > n)
                            t = n;
                        if (t > m)
                            t = m;
                        Array.Copy(z.next_in, p, Window, q, t);
                        p += t; n -= t;
                        q += t; m -= t;
                        if ((left -= t) != 0)
                            break;
                        mode = last != 0 ? InflateBlockMode.DRY : InflateBlockMode.TYPE;
                        break;

                    case InflateBlockMode.TABLE:

                        while (k < (14)) {
                            if (n != 0) {
                                r = (int)ZLibResultCode.Z_OK;
                            } else {
                                BitB = b; BitK = k;
                                z.avail_in = n;
                                z.total_in += p - z.next_in_index; z.next_in_index = p;
                                WritePos = q;
                                return inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }

                        table = t = (b & 0x3fff);
                        if ((t & 0x1f) > 29 || ((t >> 5) & 0x1f) > 29) {
                            mode = InflateBlockMode.BAD;
                            z.msg = "too many length or distance symbols";
                            r = (int)ZLibResultCode.Z_DATA_ERROR;

                            BitB = b; BitK = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            WritePos = q;
                            return inflate_flush(z, r);
                        }
                        t = 258 + (t & 0x1f) + ((t >> 5) & 0x1f);
                        blens = new int[t]; {
                            b = ZLibUtil.URShift(b, (14)); k -= (14);
                        }

                        index = 0;
                        mode = InflateBlockMode.BTREE;
                        goto case InflateBlockMode.BTREE;

                    case InflateBlockMode.BTREE:
                        while (index < 4 + (ZLibUtil.URShift(table, 10))) {
                            while (k < (3)) {
                                if (n != 0) {
                                    r = (int)ZLibResultCode.Z_OK;
                                } else {
                                    BitB = b; BitK = k;
                                    z.avail_in = n;
                                    z.total_in += p - z.next_in_index; z.next_in_index = p;
                                    WritePos = q;
                                    return inflate_flush(z, r);
                                }

                                n--;
                                b |= (z.next_in[p++] & 0xff) << k;
                                k += 8;
                            }

                            blens[ZLibUtil.border[index++]] = b & 7;

                            {
                                b = ZLibUtil.URShift(b, (3)); k -= (3);
                            }
                        }

                        while (index < 19) {
                            blens[ZLibUtil.border[index++]] = 0;
                        }

                        bb[0] = 7;
                        t = InfTree.inflate_trees_bits(blens, bb, tb, hufts, z);
                        if (t != (int)ZLibResultCode.Z_OK) {
                            r = t;
                            if (r == (int)ZLibResultCode.Z_DATA_ERROR) {
                                blens = null;
                                mode = InflateBlockMode.BAD;
                            }

                            BitB = b; BitK = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            WritePos = q;
                            return inflate_flush(z, r);
                        }

                        index = 0;
                        mode = InflateBlockMode.DTREE;
                        goto case InflateBlockMode.DTREE;

                    case InflateBlockMode.DTREE:
                        while (true) {
                            t = table;
                            if (!(index < 258 + (t & 0x1f) + ((t >> 5) & 0x1f))) {
                                break;
                            }


                            int i, j, c;

                            t = bb[0];

                            while (k < (t)) {
                                if (n != 0) {
                                    r = (int)ZLibResultCode.Z_OK;
                                } else {
                                    BitB = b; BitK = k;
                                    z.avail_in = n;
                                    z.total_in += p - z.next_in_index; z.next_in_index = p;
                                    WritePos = q;
                                    return inflate_flush(z, r);
                                }

                                n--;
                                b |= (z.next_in[p++] & 0xff) << k;
                                k += 8;
                            }

                            t = hufts[(tb[0] + (b & ZLibUtil.inflate_mask[t])) * 3 + 1];
                            c = hufts[(tb[0] + (b & ZLibUtil.inflate_mask[t])) * 3 + 2];

                            if (c < 16) {
                                b = ZLibUtil.URShift(b, (t)); k -= (t);
                                blens[index++] = c;
                            } else {
                                // c == 16..18
                                i = c == 18 ? 7 : c - 14;
                                j = c == 18 ? 11 : 3;

                                while (k < (t + i)) {
                                    if (n != 0) {
                                        r = (int)ZLibResultCode.Z_OK;
                                    } else {
                                        BitB = b; BitK = k;
                                        z.avail_in = n;
                                        z.total_in += p - z.next_in_index; z.next_in_index = p;
                                        WritePos = q;
                                        return inflate_flush(z, r);
                                    }

                                    n--;
                                    b |= (z.next_in[p++] & 0xff) << k;
                                    k += 8;
                                }

                                b = ZLibUtil.URShift(b, (t)); k -= (t);

                                j += (b & ZLibUtil.inflate_mask[i]);

                                b = ZLibUtil.URShift(b, (i)); k -= (i);

                                i = index;
                                t = table;
                                if (i + j > 258 + (t & 0x1f) + ((t >> 5) & 0x1f) || (c == 16 && i < 1)) {
                                    blens = null;
                                    mode = InflateBlockMode.BAD;
                                    z.msg = "invalid bit length repeat";
                                    r = (int)ZLibResultCode.Z_DATA_ERROR;

                                    BitB = b; BitK = k;
                                    z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                    WritePos = q;
                                    return inflate_flush(z, r);
                                }

                                c = c == 16 ? blens[i - 1] : 0;
                                do {
                                    blens[i++] = c;
                                }
                                while (--j != 0);
                                index = i;
                            }
                        }

                        tb[0] = -1; {
                            int[] bl = new int[1];
                            int[] bd = new int[1];
                            int[] tl = new int[1];
                            int[] td = new int[1];


                            bl[0] = 9; // must be <= 9 for lookahead assumptions
                            bd[0] = 6; // must be <= 9 for lookahead assumptions
                            t = table;
                            t = InfTree.inflate_trees_dynamic(257 + (t & 0x1f), 1 + ((t >> 5) & 0x1f), blens, bl, bd, tl, td, hufts, z);
                            if (t != (int)ZLibResultCode.Z_OK) {
                                if (t == (int)ZLibResultCode.Z_DATA_ERROR) {
                                    blens = null;
                                    mode = InflateBlockMode.BAD;
                                }
                                r = t;

                                BitB = b; BitK = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                WritePos = q;
                                return inflate_flush(z, r);
                            }

                            codes = new InfCodes(bl[0], bd[0], hufts, tl[0], hufts, td[0], z);
                        }
                        blens = null;
                        mode = InflateBlockMode.CODES;
                        goto case InflateBlockMode.CODES;

                    case InflateBlockMode.CODES:
                        BitB = b; BitK = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        WritePos = q;

                        if ((r = codes.proc(this, z, r)) != (int)ZLibResultCode.Z_STREAM_END) {
                            return inflate_flush(z, r);
                        }
                        r = (int)ZLibResultCode.Z_OK;
                        codes.free(z);

                        p = z.next_in_index; n = z.avail_in; b = BitB; k = BitK;
                        q = WritePos; m = (int)(q < ReadPos ? ReadPos - q - 1 : End - q);

                        if (last == 0) {
                            mode = InflateBlockMode.TYPE;
                            break;
                        }
                        mode = InflateBlockMode.DRY;
                        goto case InflateBlockMode.DRY;

                    case InflateBlockMode.DRY:
                        WritePos = q;
                        r = inflate_flush(z, r);
                        q = WritePos; m = (int)(q < ReadPos ? ReadPos - q - 1 : End - q);
                        if (ReadPos != WritePos) {
                            BitB = b; BitK = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            WritePos = q;
                            return inflate_flush(z, r);
                        }
                        mode = InflateBlockMode.DONE;
                        goto case InflateBlockMode.DONE;

                    case InflateBlockMode.DONE:
                        r = (int)ZLibResultCode.Z_STREAM_END;

                        BitB = b; BitK = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        WritePos = q;
                        return inflate_flush(z, r);

                    case InflateBlockMode.BAD:
                        r = (int)ZLibResultCode.Z_DATA_ERROR;

                        BitB = b; BitK = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        WritePos = q;
                        return inflate_flush(z, r);


                    default:
                        r = (int)ZLibResultCode.Z_STREAM_ERROR;

                        BitB = b; BitK = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        WritePos = q;
                        return inflate_flush(z, r);

                }
            }
        }

        /// <summary>
        /// Frees inner buffers
        /// </summary>
        internal void free(ZStream z) {
            reset(z, null);
            Window = null;
            hufts = null;
            //ZFREE(z, s);
        }

        /// <summary>
        /// Sets dictionary
        /// </summary>

        internal void set_dictionary(byte[] d, int start, int n) {
            Array.Copy(d, start, Window, 0, n);
            ReadPos = WritePos = n;
        }

        ///<summary>
        /// Returns true if inflate is currently at the End of a block generated
        /// by Z_SYNC_FLUSH or Z_FULL_FLUSH. 
        /// </summary>
        internal int sync_point() {
            return mode == InflateBlockMode.LENS ? 1 : 0;
        }

        /// <summary>
        /// copy as much as possible from the sliding Window to the output area
        /// </summary>
		internal int inflate_flush(ZStream z, int r) {
            // local copies of source and destination pointers
            int p = z.next_out_index;
            int q = ReadPos;

            // compute number of bytes to copy as far as End of Window
            int n = (int)((q <= WritePos ? WritePos : End) - q);
            if (n > z.avail_out)
                n = z.avail_out;
            if (n != 0 && r == (int)ZLibResultCode.Z_BUF_ERROR)
                r = (int)ZLibResultCode.Z_OK;

            // update counters
            z.avail_out -= n;
            z.total_out += n;

            // update check information
            if (needCheck)
                z.adler = check = Adler32.GetAdler32Checksum(check, Window, q, n);

            // copy as far as End of Window
            Array.Copy(Window, q, z.next_out, p, n);
            p += n;
            q += n;

            // see if more to copy at beginning of Window
            if (q == End) {
                // wrap pointers
                q = 0;
                if (WritePos == End)
                    WritePos = 0;

                // compute bytes to copy
                n = WritePos - q;
                if (n > z.avail_out)
                    n = z.avail_out;
                if (n != 0 && r == (int)ZLibResultCode.Z_BUF_ERROR)
                    r = (int)ZLibResultCode.Z_OK;

                // update counters
                z.avail_out -= n;
                z.total_out += n;

                // update check information
                if (needCheck)
                    z.adler = check = Adler32.GetAdler32Checksum(check, Window, q, n);

                // copy
                Array.Copy(Window, q, z.next_out, p, n);
                p += n;
                q += n;
            }

            // update pointers
            z.next_out_index = p;
            ReadPos = q;

            // done
            return r;
        }

        #endregion
    }
}