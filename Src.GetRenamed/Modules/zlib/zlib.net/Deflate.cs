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

    /// <summary>
    /// Implementation of the Deflate compression algorithm.
    /// </summary>
    public sealed class Deflate {

        #region Nested class

        /// <summary>
        /// Deflate algorithm configuration parameters class
        /// </summary>
        internal class Config {
            /// <summary>
            /// reduce lazy search above this match length
            /// </summary>
            internal int good_length;

            /// <summary>
            /// do not perform lazy search above this match length
            /// </summary>
            internal int max_lazy;

            /// <summary>
            /// quit search above this match length
            /// </summary>
            internal int nice_length;

            internal int max_chain;

            internal int func;

            /// <summary>
            /// Constructor which initializes class inner fields
            /// </summary>
            internal Config(int good_length, int max_lazy, int nice_length, int max_chain, int func) {
                this.good_length = good_length;
                this.max_lazy = max_lazy;
                this.nice_length = nice_length;
                this.max_chain = max_chain;
                this.func = func;
            }
        }

        #endregion

        #region Constants

        /// <summary>
        /// Maximum memory level
        /// </summary>
        private const int MAX_MEM_LEVEL = 9;

        /// <summary>
        /// Defalult compression method
        /// </summary>
        public const int Z_DEFAULT_COMPRESSION = -1;

        /// <summary>
        /// Default memory level
        /// </summary>
        public const int DEF_MEM_LEVEL = 8;

        //Compression methods
        private const int STORED = 0;
        private const int FAST = 1;
        private const int SLOW = 2;

        /// <summary>
        /// Deflate class congiration table
        /// </summary>
        private static Config[] config_table;

        /// <summary>
        /// block not completed, need more input or more output
        /// </summary>
        private const int NeedMore = 0;

        /// <summary>
        /// Block internalFlush performed
        /// </summary>
        private const int BlockDone = 1;

        /// <summary>
        /// Finish started, need only more output at next deflate
        /// </summary>
        private const int FinishStarted = 2;

        /// <summary>
        /// finish done, accept no more input or output
        /// </summary>
        private const int FinishDone = 3;

        /// <summary>
        /// preset dictionary flag in zlib header
        /// </summary>
        private const int PRESET_DICT = 0x20;

        /// <summary>
        /// The deflate compression method
        /// </summary>
        private const int Z_DEFLATED = 8;

        private const int STORED_BLOCK = 0;
        private const int STATIC_TREES = 1;
        private const int DYN_TREES = 2;

        /// <summary>
        /// The size of the buffer
        /// </summary>
        private const int Buf_size = 8 * 2;

        /// <summary>
        /// repeat previous bit length 3-6 times (2 bits of repeat count)
        /// </summary>
        private const int REP_3_6 = 16;

        /// <summary>
        /// repeat a zero length 3-10 times  (3 bits of repeat count)
        /// </summary>
        private const int REPZ_3_10 = 17;

        /// <summary>
        /// repeat a zero length 11-138 times  (7 bits of repeat count)
        /// </summary>
        private const int REPZ_11_138 = 18;

        private const int MIN_MATCH = 3;
        private const int MAX_MATCH = 258;
        private const int MIN_LOOKAHEAD = (MAX_MATCH + MIN_MATCH + 1);

        private const int MAX_BITS = 15;
        private const int D_CODES = 30;
        private const int BL_CODES = 19;
        private const int LENGTH_CODES = 29;
        private const int LITERALS = 256;
        private const int L_CODES = (LITERALS + 1 + LENGTH_CODES);
        private const int HEAP_SIZE = (2 * L_CODES + 1);

        private const int END_BLOCK = 256;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the Compression level.
        /// </summary>
        public int level { get; set; }

        /// <summary>
        /// Gets or sets the Number of bytes in the pending buffer.
        /// </summary>
        public int Pending { get; set; }

        /// <summary>
        /// Gets or sets the Output pending buffer.
        /// </summary>
        public byte[] Pending_buf { get; set; }

        /// <summary>
        /// Gets or sets the next pending byte to output to the stream.
        /// </summary>
        public int Pending_out { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to suppress zlib header and adler32.
        /// </summary>
        public int NoHeader { get; set; }

        #endregion

        #region Fields

        /// <summary>
        /// Pointer back to this zlib stream
        /// </summary>
        private ZStream strm;

        /// <summary>
        /// As the name implies
        /// </summary>
        private DeflateState status;

        /// <summary>
        /// Size of Pending_buf
        /// </summary>
        private int pending_buf_size;

        /// <summary>
        /// UNKNOWN, BINARY or ASCII
        /// </summary>
        private BlockType data_type;

#pragma warning disable 414 // TODO: unused field
        /// <summary>
        /// STORED (for zip only) or DEFLATED
        /// </summary>
        private byte method;
#pragma warning restore

        /// <summary>
        /// Value of internalFlush parameter for previous deflate call
        /// </summary>
        private int last_flush;

        /// <summary>
        /// LZ77 Window size (32K by default)
        /// </summary>
        private int w_size;

        /// <summary>
        /// log2(w_size)  (8..16)
        /// </summary>
        private int w_bits;

        /// <summary>
        /// w_size - 1
        /// </summary>
        private int w_mask;

        /// <summary>
        /// Sliding Window. Input bytes are ReadPos into the second half of the Window,
        /// and move to the first half later to keep a dictionary of at least wSize
        /// bytes. With this organization, matches are limited to a distance of
        /// wSize-MAX_MATCH bytes, but this ensures that IO is always
        /// performed with a length multiple of the block size. Also, it limits
        /// the Window size to 64K, which is quite useful on MSDOS.
        /// To do: use the user input buffer as sliding Window.
        /// </summary>
        private byte[] window;

        /// <summary>
        /// Actual size of Window: 2*wSize, except when the user input buffer is directly used as sliding Window.
        /// </summary>
        private int window_size;

        /// <summary>
        /// Link to older string with same hash index. To limit the size of this
        /// array to 64K, this link is maintained only for the last 32K strings.
        /// An index in this array is thus a Window index modulo 32K.
        /// </summary>
        private short[] prev;

        /// <summary>
        /// Heads of the hash chains or NIL.
        /// </summary>
        private short[] head;

        /// <summary>
        /// hash index of string to be inserted
        /// </summary>
        private int ins_h;

        /// <summary>
        /// number of elements in hash table
        /// </summary>
        private int hash_size;

        /// <summary>
        /// log2(hash_size)
        /// </summary>
        private int hash_bits;

        /// <summary>
        /// hash_size-1
        /// </summary>
        private int hash_mask;

        /// <summary>
        /// Number of bits by which ins_h must be shifted at each input
        /// step. It must be such that after MIN_MATCH steps, the oldest
        /// byte no longer takes part in the hash key, that is:
        /// hash_shift * MIN_MATCH >= hash_bits
        /// </summary>
        private int hash_shift;

        /// <summary>
        /// Window position at the beginning of the current output block. Gets negative when the Window is moved backwards.
        /// </summary>
        private int block_start;

        /// <summary>
        /// length of best match
        /// </summary>
        private int match_length;

        /// <summary>
        /// previous match
        /// </summary>
        private int prev_match;

        /// <summary>
        /// set if previous match exists
        /// </summary>
        private int match_available;

        /// <summary>
        /// start of string to insert
        /// </summary>
        private int strstart;

        /// <summary>
        /// start of matching string
        /// </summary>
        private int match_start;

        /// <summary>
        /// number of valid bytes ahead in Window
        /// </summary>
        private int lookahead;

        /// <summary>
        /// Length of the best match at previous step. Matches not greater than this
        /// are discarded. This is used in the lazy match evaluation.
        /// </summary>
        private int prev_length;

        /// <summary>
        /// To speed up deflation, hash chains are never searched beyond this
        /// length.  A higher limit improves compression ratio but degrades the speed.
        /// </summary>
        private int max_chain_length;

        /// <summary>
        /// Attempt to find a better match only when the current match is strictly
        /// smaller than this value. This mechanism is used only for compression
        /// levels >= 4.
        /// </summary>
        private int max_lazy_match;

        // Insert new strings in the hash table only if the match length is not
        // greater than this length. This saves time but degrades compression.
        // max_insert_length is used only for compression levels <= 3.

        /// <summary>
        /// favor or force Huffman coding
        /// </summary>
        private CompressionStrategy strategy;

        /// <summary>
        /// Use a faster search when the previous match is longer than this
        /// </summary>
        private int good_match;

        /// <summary>
        /// Stop searching when current match exceeds this
        /// </summary>
        private int nice_match;

        /// <summary>
        /// literal and length tree
        /// </summary>
        private short[] dyn_ltree;

        /// <summary>
        /// distance tree
        /// </summary>
        private short[] dyn_dtree;

        /// <summary>
        ///  Huffman tree for bit lengths
        /// </summary>
        private short[] bl_tree;

        /// <summary>
        /// Desc for literal tree
        /// </summary>
        private Tree l_desc = new Tree();

        /// <summary>
        /// desc for distance tree
        /// </summary>
        private Tree d_desc = new Tree();

        /// <summary>
        /// desc for bit length tree
        /// </summary>
        private Tree bl_desc = new Tree();

        /// <summary>
        /// number of codes at each bit length for an optimal tree
        /// </summary>
        internal short[] bl_count = new short[MAX_BITS + 1];

        /// <summary>
        /// heap used to build the Huffman trees
        /// </summary>
        internal int[] heap = new int[2 * L_CODES + 1];

        /// <summary>
        /// number of elements in the heap
        /// </summary>
        internal int heap_len;

        /// <summary>
        /// element of largest frequency
        /// </summary>
        internal int heap_max;

        // The sons of heap[n] are heap[2*n] and heap[2*n+1]. heap[0] is not used.
        // The same heap array is used to build all trees.

        /// <summary>
        /// Depth of each subtree used as tie breaker for trees of equal frequency
        /// </summary>
        internal byte[] depth = new byte[2 * L_CODES + 1];

        /// <summary>
        /// index for literals or lengths
        /// </summary>
        internal int l_buf;

        ///<summary>
        /// Size of match buffer for literals/lengths.  There are 4 reasons for
        /// limiting lit_bufsize to 64K:
        ///   - frequencies can be kept in 16 bit counters
        ///   - if compression is not successful for the first block, all input
        ///     data is still in the Window so we can still emit a stored block even
        ///     when input comes from standard input.  (This can also be done for
        ///     all blocks if lit_bufsize is not greater than 32K.)
        ///   - if compression is not successful for a file smaller than 64K, we can
        ///     even emit a stored file instead of a stored block (saving 5 bytes).
        ///     This is applicable only for zip (not gzip or zlib).
        ///   - creating new Huffman trees less frequently may not provide fast
        ///     adaptation to changes in the input data statistics. (Take for
        ///     example a binary file with poorly compressible code followed by
        ///     a highly compressible string table.) Smaller buffer sizes give
        ///     fast adaptation but have of course the overhead of transmitting
        ///     trees more frequently.
        ///   - I can't count above 4
        ///</summary> 
        private int lit_bufsize;

        /// <summary>
        /// running index in l_buf
        /// </summary>
        private int last_lit;

        // Buffer for distances. To simplify the code, d_buf and l_buf have
        // the same number of elements. To use different lengths, an extra flag
        // array would be necessary.

        /// <summary>
        /// index of pendig_buf
        /// </summary>
        private int d_buf;

        /// <summary>
        /// bit length of current block with optimal trees
        /// </summary>
        internal int opt_len;

        /// <summary>
        /// bit length of current block with static trees
        /// </summary>
        internal int static_len;

        /// <summary>
        /// number of string matches in current block
        /// </summary>
        internal int matches;

        /// <summary>
        /// bit length of EOB code for last block
        /// </summary>
        internal int last_eob_len;

        /// <summary>
        /// Output buffer. bits are inserted starting at the bottom (least
        /// significant bits).
        /// </summary>
        private short bi_buf;

        /// <summary>
        /// Number of valid bits in bi_buf.  All bits above the last valid bit
        /// are always zero.
        /// </summary>
        private int bi_valid;

        #endregion

        #region Methods

        /// <summary>
        /// Default constructor
        /// </summary>
        internal Deflate() {
            dyn_ltree = new short[HEAP_SIZE * 2];
            dyn_dtree = new short[(2 * D_CODES + 1) * 2]; // distance tree
            bl_tree = new short[(2 * BL_CODES + 1) * 2]; // Huffman tree for bit lengths
        }

        /// <summary>
        /// Initialization
        /// </summary>
        private void lm_init() {
            window_size = 2 * w_size;

            Array.Clear(head, 0, hash_size);

            // Set the default configuration parameters:
            max_lazy_match = config_table[level].max_lazy;
            good_match = config_table[level].good_length;
            nice_match = config_table[level].nice_length;
            max_chain_length = config_table[level].max_chain;

            strstart = 0;
            block_start = 0;
            lookahead = 0;
            match_length = prev_length = MIN_MATCH - 1;
            match_available = 0;
            ins_h = 0;
        }

        /// <summary>
        /// Initialize the tree data structures for a new zlib stream.
        /// </summary>
        private void tr_init() {

            l_desc.DynTree = dyn_ltree;
            l_desc.StatDesc = StaticTree.static_l_desc;

            d_desc.DynTree = dyn_dtree;
            d_desc.StatDesc = StaticTree.static_d_desc;

            bl_desc.DynTree = bl_tree;
            bl_desc.StatDesc = StaticTree.static_bl_desc;

            bi_buf = 0;
            bi_valid = 0;
            last_eob_len = 8; // enough lookahead for inflate

            // Initialize the first block of the first file:
            init_block();
        }

        /// <summary>
        /// Initializes block
        /// </summary>
        private void init_block() {
            // Initialize the trees.
            for (int i = 0; i < L_CODES; i++)
                dyn_ltree[i * 2] = 0;
            for (int i = 0; i < D_CODES; i++)
                dyn_dtree[i * 2] = 0;
            for (int i = 0; i < BL_CODES; i++)
                bl_tree[i * 2] = 0;

            dyn_ltree[END_BLOCK * 2] = 1;
            opt_len = static_len = 0;
            last_lit = matches = 0;
        }

        ///<summary>
        /// Restore the heap property by moving down the tree starting at node k,
        /// exchanging a node with the smallest of its two sons if necessary, stopping
        /// when the heap property is re-established (each father smaller than its
        /// two sons).
        /// </summary>
        internal void pqdownheap(short[] tree, int k) {
            int v = heap[k];
            int j = k << 1; // left son of k
            while (j <= heap_len) {
                // Set j to the smallest of the two sons:
                if (j < heap_len && smaller(tree, heap[j + 1], heap[j], depth)) {
                    j++;
                }
                // Exit if v is smaller than both sons
                if (smaller(tree, v, heap[j], depth))
                    break;

                // Exchange v with the smallest son
                heap[k] = heap[j]; k = j;
                // And continue down the tree, setting j to the left son of k
                j <<= 1;
            }
            heap[k] = v;
        }

        internal static bool smaller(short[] tree, int n, int m, byte[] depth) {
            return (tree[n * 2] < tree[m * 2] || (tree[n * 2] == tree[m * 2] && depth[n] <= depth[m]));
        }

        ///<summary>
        /// Scan a literal or distance tree to determine the frequencies of the codes
        /// in the bit length tree.
        /// </summary>
        private void scan_tree(short[] tree, int max_code) {
            int n; // iterates over all tree elements
            int prevlen = -1; // last emitted length
            int curlen; // length of current code
            int nextlen = tree[0 * 2 + 1]; // length of next code
            int count = 0; // repeat count of the current code
            int max_count = 7; // max repeat count
            int min_count = 4; // min repeat count

            if (nextlen == 0) {
                max_count = 138; min_count = 3;
            }
            tree[(max_code + 1) * 2 + 1] = (short)ZLibUtil.Identity(0xffff); // guard

            for (n = 0; n <= max_code; n++) {
                curlen = nextlen; nextlen = tree[(n + 1) * 2 + 1];
                if (++count < max_count && curlen == nextlen) {
                    continue;
                }

                if (count < min_count) {
                    bl_tree[curlen * 2] = (short)(bl_tree[curlen * 2] + count);
                } else if (curlen != 0) {
                    if (curlen != prevlen)
                        bl_tree[curlen * 2]++;
                    bl_tree[REP_3_6 * 2]++;
                } else if (count <= 10) {
                    bl_tree[REPZ_3_10 * 2]++;
                } else {
                    bl_tree[REPZ_11_138 * 2]++;
                }
                count = 0; prevlen = curlen;
                if (nextlen == 0) {
                    max_count = 138; min_count = 3;
                } else if (curlen == nextlen) {
                    max_count = 6; min_count = 3;
                } else {
                    max_count = 7; min_count = 4;
                }
            }
        }

        ///<summary>
        /// Construct the Huffman tree for the bit lengths and return the index in
        /// bl_order of the last bit length code to send.
        /// </summary>
        private int build_bl_tree() {
            int max_blindex; // index of last bit length code of non zero freq

            // Determine the bit length frequencies for literal and distance trees
            scan_tree(dyn_ltree, l_desc.MaxCode);
            scan_tree(dyn_dtree, d_desc.MaxCode);

            // Build the bit length tree:
            bl_desc.build_tree(this);
            // opt_len now includes the length of the tree representations, except
            // the lengths of the bit lengths codes and the 5+5+4 bits for the counts.

            // Determine the number of bit length codes to send. The pkzip format
            // requires that at least 4 bit length codes be sent. (appnote.txt says
            // 3 but the actual value used is 4.)
            for (max_blindex = BL_CODES - 1; max_blindex >= 3; max_blindex--) {
                if (bl_tree[ZLibUtil.bl_order[max_blindex] * 2 + 1] != 0)
                    break;
            }
            // Update opt_len to include the bit length tree and counts
            opt_len += 3 * (max_blindex + 1) + 5 + 5 + 4;

            return max_blindex;
        }

        ///<summary>
        /// Send the header for a block using dynamic Huffman trees: the counts, the
        /// lengths of the bit length codes, the literal tree and the distance tree.
        /// IN assertion: lcodes >= 257, dcodes >= 1, blcodes >= 4.
        /// </summary>
        private void send_all_trees(int lcodes, int dcodes, int blcodes) {
            int rank; // index in bl_order

            send_bits(lcodes - 257, 5); // not +255 as stated in appnote.txt
            send_bits(dcodes - 1, 5);
            send_bits(blcodes - 4, 4); // not -3 as stated in appnote.txt
            for (rank = 0; rank < blcodes; rank++) {
                send_bits(bl_tree[ZLibUtil.bl_order[rank] * 2 + 1], 3);
            }
            send_tree(dyn_ltree, lcodes - 1); // literal tree
            send_tree(dyn_dtree, dcodes - 1); // distance tree
        }

        ///<summary>
        /// Send a literal or distance tree in compressed form, using the codes in
        /// bl_tree.
        /// </summary>
        private void send_tree(short[] tree, int max_code) {
            int n; // iterates over all tree elements
            int prevlen = -1; // last emitted length
            int curlen; // length of current code
            int nextlen = tree[0 * 2 + 1]; // length of next code
            int count = 0; // repeat count of the current code
            int max_count = 7; // max repeat count
            int min_count = 4; // min repeat count

            if (nextlen == 0) {
                max_count = 138; min_count = 3;
            }

            for (n = 0; n <= max_code; n++) {
                curlen = nextlen; nextlen = tree[(n + 1) * 2 + 1];
                if (++count < max_count && curlen == nextlen) {
                    continue;
                } else if (count < min_count) {
                    do {
                        send_code(curlen, bl_tree);
                    }
                    while (--count != 0);
                } else if (curlen != 0) {
                    if (curlen != prevlen) {
                        send_code(curlen, bl_tree); count--;
                    }
                    send_code(REP_3_6, bl_tree);
                    send_bits(count - 3, 2);
                } else if (count <= 10) {
                    send_code(REPZ_3_10, bl_tree);
                    send_bits(count - 3, 3);
                } else {
                    send_code(REPZ_11_138, bl_tree);
                    send_bits(count - 11, 7);
                }
                count = 0; prevlen = curlen;
                if (nextlen == 0) {
                    max_count = 138; min_count = 3;
                } else if (curlen == nextlen) {
                    max_count = 6; min_count = 3;
                } else {
                    max_count = 7; min_count = 4;
                }
            }
        }

        ///<summary>
        /// Output a byte on the stream.
        /// IN assertion: there is enough room in Pending_buf.
        /// </summary>
        private void put_byte(byte[] p, int start, int len) {
            Array.Copy(p, start, Pending_buf, Pending, len);
            Pending += len;
        }

        /// <summary>
        /// Adds a byte to the buffer
        /// </summary>
        private void put_byte(byte c) {
            Pending_buf[Pending++] = c;
        }

        private void put_short(int w) {
            put_byte((byte)(w));
            put_byte((byte)(ZLibUtil.URShift(w, 8)));
        }

        private void putShortMSB(int b) {
            put_byte((byte)(b >> 8));
            put_byte((byte)(b));
        }

        private void send_code(int c, short[] tree) {
            send_bits((tree[c * 2] & 0xffff), (tree[c * 2 + 1] & 0xffff));
        }

        private void send_bits(int value_Renamed, int length) {
            int len = length;
            if (bi_valid > (int)Buf_size - len) {
                int val = value_Renamed;
                //      bi_buf |= (val << bi_valid);
                bi_buf = (short)((ushort)bi_buf | (ushort)(((val << bi_valid) & 0xffff)));
                put_short(bi_buf);
                bi_buf = (short)(ZLibUtil.URShift(val, (Buf_size - bi_valid)));
                bi_valid += len - Buf_size;
            } else {
                //      bi_buf |= (value) << bi_valid;
                bi_buf = (short)((ushort)bi_buf | (ushort)((((value_Renamed) << bi_valid) & 0xffff)));
                bi_valid += len;
            }
        }

        ///<summary>
        /// Send one empty static block to give enough lookahead for inflate.
        /// This takes 10 bits, of which 7 may remain in the bit buffer.
        /// The current inflate code requires 9 bits of lookahead. If the
        /// last two codes for the previous block (real code plus EOB) were coded
        /// on 5 bits or less, inflate may have only 5+3 bits of lookahead to decode
        /// the last real code. In this case we send two empty static blocks instead
        /// of one. (There are no problems if the previous block is stored or fixed.)
        /// To simplify the code, we assume the worst case of last real code encoded
        /// on one bit only.
        /// </summary>
        private void _tr_align() {
            send_bits(STATIC_TREES << 1, 3);
            send_code(END_BLOCK, StaticTree.static_ltree);

            bi_flush();

            // Of the 10 bits for the empty block, we have already sent
            // (10 - bi_valid) bits. The lookahead for the last real code (before
            // the EOB of the previous block) was thus at least one plus the length
            // of the EOB plus what we have just sent of the empty static block.
            if (1 + last_eob_len + 10 - bi_valid < 9) {
                send_bits(STATIC_TREES << 1, 3);
                send_code(END_BLOCK, StaticTree.static_ltree);
                bi_flush();
            }
            last_eob_len = 7;
        }

        /// <summary>
        /// Save the match info and tally the frequency counts. Return true if
        /// the current block must be flushed.
        /// </summary>
        private bool _tr_tally(int dist, int lc) {

            Pending_buf[d_buf + last_lit * 2] = (byte)(ZLibUtil.URShift(dist, 8));
            Pending_buf[d_buf + last_lit * 2 + 1] = (byte)dist;

            Pending_buf[l_buf + last_lit] = (byte)lc; last_lit++;

            if (dist == 0) {
                // lc is the unmatched char
                dyn_ltree[lc * 2]++;
            } else {
                matches++;
                // Here, lc is the match length - MIN_MATCH
                dist--; // dist = match distance - 1
                dyn_ltree[(ZLibUtil._length_code[lc] + LITERALS + 1) * 2]++;
                dyn_dtree[Tree.d_code(dist) * 2]++;
            }

            if ((last_lit & 0x1fff) == 0 && level > 2) {
                // Compute an upper bound for the compressed length
                int out_length = last_lit * 8;
                int in_length = strstart - block_start;
                int dcode;
                for (dcode = 0; dcode < D_CODES; dcode++) {
                    out_length = (int)(out_length + (int)dyn_dtree[dcode * 2] * (5L + ZLibUtil.extra_dbits[dcode]));
                }
                out_length = ZLibUtil.URShift(out_length, 3);
                if ((matches < (last_lit / 2)) && out_length < in_length / 2)
                    return true;
            }

            return (last_lit == lit_bufsize - 1);
            // We avoid equality with lit_bufsize because of wraparound at 64K
            // on 16 bit machines and because stored blocks are restricted to
            // 64K-1 bytes.
        }

        ///<summary>
        /// Send the block data compressed using the given Huffman trees
        ///</summary>
        private void compress_block(short[] ltree, short[] dtree) {
            int dist; // distance of matched string
            int lc; // match length or unmatched char (if dist == 0)
            int lx = 0; // running index in l_buf
            int code; // the code to send
            int extra; // number of extra bits to send

            if (last_lit != 0) {
                do {
                    dist = ((Pending_buf[d_buf + lx * 2] << 8) & 0xff00) | (Pending_buf[d_buf + lx * 2 + 1] & 0xff);
                    lc = (Pending_buf[l_buf + lx]) & 0xff; lx++;

                    if (dist == 0) {
                        send_code(lc, ltree); // send a literal byte
                    } else {
                        // Here, lc is the match length - MIN_MATCH
                        code = ZLibUtil._length_code[lc];

                        send_code(code + LITERALS + 1, ltree); // send the length code
                        extra = ZLibUtil.extra_lbits[code];
                        if (extra != 0) {
                            lc -= ZLibUtil.base_length[code];
                            send_bits(lc, extra); // send the extra length bits
                        }
                        dist--; // dist is now the match distance - 1
                        code = Tree.d_code(dist);

                        send_code(code, dtree); // send the distance code
                        extra = ZLibUtil.extra_dbits[code];
                        if (extra != 0) {
                            dist -= ZLibUtil.base_dist[code];
                            send_bits(dist, extra); // send the extra distance bits
                        }
                    } // literal or match pair ?

                    // Check that the overlay between Pending_buf and d_buf+l_buf is ok:
                }
                while (lx < last_lit);
            }

            send_code(END_BLOCK, ltree);
            last_eob_len = ltree[END_BLOCK * 2 + 1];
        }

        /// <summary>
        /// Set the data type to ASCII or BINARY, using a crude approximation:
        /// binary if more than 20% of the bytes are &lt;= 6 or &gt;= 128, ascii otherwise.
        /// IN assertion: the fields freq of dyn_ltree are set and the total of all
        /// frequencies does not exceed 64K (to fit in an int on 16 bit machines).
        /// </summary>
        private void set_data_type() {
            int n = 0;
            int ascii_freq = 0;
            int bin_freq = 0;
            while (n < 7) {
                bin_freq += dyn_ltree[n * 2]; n++;
            }
            while (n < 128) {
                ascii_freq += dyn_ltree[n * 2]; n++;
            }
            while (n < LITERALS) {
                bin_freq += dyn_ltree[n * 2]; n++;
            }
            data_type = (bin_freq > (ZLibUtil.URShift(ascii_freq, 2)) ? BlockType.Z_BINARY : BlockType.Z_ASCII);
        }

        /// <summary>
        /// Flush the bit buffer, keeping at most 7 bits in it.
        /// </summary>
        private void bi_flush() {
            if (bi_valid == 16) {
                put_short(bi_buf);
                bi_buf = 0;
                bi_valid = 0;
            } else if (bi_valid >= 8) {
                put_byte((byte)bi_buf);
                bi_buf = (short)(ZLibUtil.URShift(bi_buf, 8));
                bi_valid -= 8;
            }
        }

        /// <summary>
        /// Flush the bit buffer and align the output on a byte boundary
        /// </summary>
        private void bi_windup() {
            if (bi_valid > 8) {
                put_short(bi_buf);
            } else if (bi_valid > 0) {
                put_byte((byte)bi_buf);
            }
            bi_buf = 0;
            bi_valid = 0;
        }

        /// <summary>
        /// Copy a stored block, storing first the length and its
        /// one's complement if requested.
        /// </summary>
        private void copy_block(int buf, int len, bool header) {

            bi_windup(); // align on byte boundary
            last_eob_len = 8; // enough lookahead for inflate

            if (header) {
                put_short((short)len);
                put_short((short)~len);
            }

            put_byte(window, buf, len);
        }

        /// <summary>
        /// Flushes block
        /// </summary>
        private void flush_block_only(bool eof) {
            _tr_flush_block(block_start >= 0 ? block_start : -1, strstart - block_start, eof);
            block_start = strstart;
            strm.FlushPending();
        }

        /// <summary>
        /// Copy without compression as much as possible from the input stream, return
        /// the current block state.
        /// This function does not insert new strings in the dictionary since
        /// uncompressible data is probably not useful. This function is used
        /// only for the level=0 compression option.
        /// NOTE: this function should be optimized to avoid extra copying from
        /// Window to Pending_buf.
        /// </summary>
        private int deflate_stored(int flush) {
            // Stored blocks are limited to 0xffff bytes, Pending_buf is limited
            // to pending_buf_size, and each stored block has a 5 byte header:

            int max_block_size = 0xffff;
            int max_start;

            if (max_block_size > pending_buf_size - 5) {
                max_block_size = pending_buf_size - 5;
            }

            // Copy as much as possible from input to output:
            while (true) {
                // Fill the Window as much as possible:
                if (lookahead <= 1) {
                    fill_window();
                    if (lookahead == 0 && flush == (int)FlushStrategy.Z_NO_FLUSH)
                        return NeedMore;
                    if (lookahead == 0)
                        break; // internalFlush the current block
                }

                strstart += lookahead;
                lookahead = 0;

                // Emit a stored block if Pending_buf will be full:
                max_start = block_start + max_block_size;
                if (strstart == 0 || strstart >= max_start) {
                    // strstart == 0 is possible when wraparound on 16-bit machine
                    lookahead = (int)(strstart - max_start);
                    strstart = (int)max_start;

                    flush_block_only(false);
                    if (strm.avail_out == 0)
                        return NeedMore;
                }

                // Flush if we may have to slide, otherwise block_start may become
                // negative and the data will be gone:
                if (strstart - block_start >= w_size - MIN_LOOKAHEAD) {
                    flush_block_only(false);
                    if (strm.avail_out == 0)
                        return NeedMore;
                }
            }

            flush_block_only(flush == (int)FlushStrategy.Z_FINISH);
            if (strm.avail_out == 0)
                return (flush == (int)FlushStrategy.Z_FINISH) ? FinishStarted : NeedMore;

            return flush == (int)FlushStrategy.Z_FINISH ? FinishDone : BlockDone;
        }

        /// <summary>
        /// Send a stored block
        /// </summary>
        private void _tr_stored_block(int buf, int stored_len, bool eof) {
            send_bits((STORED_BLOCK << 1) + (eof ? 1 : 0), 3); // send block type
            copy_block(buf, stored_len, true); // with header
        }

        /// <summary>
        /// Determine the best encoding for the current block: dynamic trees, static
        /// trees or store, and output the encoded block to the zip file.
        /// </summary>
        private void _tr_flush_block(int buf, int stored_len, bool eof) {
            int opt_lenb, static_lenb; // opt_len and static_len in bytes
            int max_blindex = 0; // index of last bit length code of non zero freq

            // Build the Huffman trees unless a stored block is forced
            if (level > 0) {
                // Check if the file is ascii or binary
                if (data_type == BlockType.Z_UNKNOWN)
                    set_data_type();

                // Construct the literal and distance trees
                l_desc.build_tree(this);

                d_desc.build_tree(this);

                // At this point, opt_len and static_len are the total bit lengths of
                // the compressed block data, excluding the tree representations.

                // Build the bit length tree for the above two trees, and get the index
                // in bl_order of the last bit length code to send.
                max_blindex = build_bl_tree();

                // Determine the best encoding. Compute first the block length in bytes
                opt_lenb = ZLibUtil.URShift((opt_len + 3 + 7), 3);
                static_lenb = ZLibUtil.URShift((static_len + 3 + 7), 3);

                if (static_lenb <= opt_lenb)
                    opt_lenb = static_lenb;
            } else {
                opt_lenb = static_lenb = stored_len + 5; // force a stored block
            }

            if (stored_len + 4 <= opt_lenb && buf != -1) {
                // 4: two words for the lengths
                // The test buf != NULL is only necessary if LIT_BUFSIZE > WSIZE.
                // Otherwise we can't have processed more than WSIZE input bytes since
                // the last block internalFlush, because compression would have been
                // successful. If LIT_BUFSIZE <= WSIZE, it is never too late to
                // transform a block into a stored block.
                _tr_stored_block(buf, stored_len, eof);
            } else if (static_lenb == opt_lenb) {
                send_bits((STATIC_TREES << 1) + (eof ? 1 : 0), 3);
                compress_block(StaticTree.static_ltree, StaticTree.static_dtree);
            } else {
                send_bits((DYN_TREES << 1) + (eof ? 1 : 0), 3);
                send_all_trees(l_desc.MaxCode + 1, d_desc.MaxCode + 1, max_blindex + 1);
                compress_block(dyn_ltree, dyn_dtree);
            }

            // The above check is made mod 2^32, for files larger than 512 MB
            // and uLong implemented on 32 bits.

            init_block();

            if (eof) {
                bi_windup();
            }
        }

        ///<summary>
        /// Fill the Window when the lookahead becomes insufficient.
        /// Updates strstart and lookahead.
        ///
        /// IN assertion: lookahead less than MIN_LOOKAHEAD
        /// OUT assertions: strstart less than or equal to window_size-MIN_LOOKAHEAD
        ///    At least one byte has been ReadPos, or _avail_in == 0; reads are
        ///    performed for at least two bytes (required for the zip translate_eol
        ///    option -- not supported here).
        /// </summary>
        private void fill_window() {
            int n, m;
            int p;
            int more; // Amount of free space at the End of the Window.

            do {
                more = (window_size - lookahead - strstart);

                // Deal with !@#$% 64K limit:
                if (more == 0 && strstart == 0 && lookahead == 0) {
                    more = w_size;
                } else if (more == -1) {
                    // Very unlikely, but possible on 16 bit machine if strstart == 0
                    // and lookahead == 1 (input done one byte at time)
                    more--;

                    // If the Window is almost full and there is insufficient lookahead,
                    // move the upper half to the lower one to make room in the upper half.
                } else if (strstart >= w_size + w_size - MIN_LOOKAHEAD) {
                    Array.Copy(window, w_size, window, 0, w_size);
                    match_start -= w_size;
                    strstart -= w_size; // we now have strstart >= MAX_DIST
                    block_start -= w_size;

                    // Slide the hash table (could be avoided with 32 bit values
                    // at the expense of memory usage). We slide even when level == 0
                    // to keep the hash table consistent if we switch back to level > 0
                    // later. (Using level 0 permanently is not an optimal usage of
                    // zlib, so we don't care about this pathological case.)

                    n = hash_size;
                    p = n;
                    do {
                        m = (head[--p] & 0xffff);
                        head[p] = (short)(m >= w_size ? (m - w_size) : 0);
                        //head[p] = (m >= w_size?(short) (m - w_size):0);
                    }
                    while (--n != 0);

                    n = w_size;
                    p = n;
                    do {
                        m = (prev[--p] & 0xffff);
                        prev[p] = (short)(m >= w_size ? (m - w_size) : 0);
                        //prev[p] = (m >= w_size?(short) (m - w_size):0);
                        // If n is not on any hash chain, prev[n] is garbage but
                        // its value will never be used.
                    }
                    while (--n != 0);
                    more += w_size;
                }

                if (strm.avail_in == 0)
                    return;

                // If there was no sliding:
                //    strstart <= WSIZE+MAX_DIST-1 && lookahead <= MIN_LOOKAHEAD - 1 &&
                //    more == window_size - lookahead - strstart
                // => more >= window_size - (MIN_LOOKAHEAD-1 + WSIZE + MAX_DIST-1)
                // => more >= window_size - 2*WSIZE + 2
                // In the BIG_MEM or MMAP case (not yet supported),
                //   window_size == input_size + MIN_LOOKAHEAD  &&
                //   strstart + s->lookahead <= input_size => more >= MIN_LOOKAHEAD.
                // Otherwise, window_size == 2*WSIZE so more >= 2.
                // If there was sliding, more >= WSIZE. So in all cases, more >= 2.

                n = strm.ReadBuf(window, strstart + lookahead, more);
                lookahead += n;

                // Initialize the hash value now that we have some input:
                if (lookahead >= MIN_MATCH) {
                    ins_h = window[strstart] & 0xff;
                    ins_h = (((ins_h) << hash_shift) ^ (window[strstart + 1] & 0xff)) & hash_mask;
                }
                // If the whole input has less than MIN_MATCH bytes, ins_h is garbage,
                // but this is not important since only literal bytes will be emitted.
            }
            while (lookahead < MIN_LOOKAHEAD && strm.avail_in != 0);
        }

        ///<summary>
        /// Compress as much as possible from the input stream, return the current
        /// block state.
        /// This function does not perform lazy evaluation of matches and inserts
        /// new strings in the dictionary only for unmatched strings or for short
        /// matches. It is used only for the fast compression options.
        /// </summary>
        private int deflate_fast(int flush) {
            //    short hash_head = 0; // head of the hash chain
            int hash_head = 0; // head of the hash chain
            bool bflush; // set if current block must be flushed

            while (true) {
                // Make sure that we always have enough lookahead, except
                // at the End of the input file. We need MAX_MATCH bytes
                // for the next match, plus MIN_MATCH bytes to insert the
                // string following the next match.
                if (lookahead < MIN_LOOKAHEAD) {
                    fill_window();
                    if (lookahead < MIN_LOOKAHEAD && flush == (int)FlushStrategy.Z_NO_FLUSH) {
                        return NeedMore;
                    }
                    if (lookahead == 0)
                        break; // internalFlush the current block
                }

                // Insert the string Window[strstart .. strstart+2] in the
                // dictionary, and set hash_head to the head of the hash chain:
                if (lookahead >= MIN_MATCH) {
                    ins_h = (((ins_h) << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;

                    //	prev[strstart&w_mask]=hash_head=head[ins_h];
                    hash_head = (head[ins_h] & 0xffff);
                    prev[strstart & w_mask] = head[ins_h];
                    head[ins_h] = (short)strstart;
                }

                // Find the longest match, discarding those <= prev_length.
                // At this point we have always match_length < MIN_MATCH

                if (hash_head != 0L && ((strstart - hash_head) & 0xffff) <= w_size - MIN_LOOKAHEAD) {
                    // To simplify the code, we prevent matches with the string
                    // of Window index 0 (in particular we have to avoid a match
                    // of the string with itself at the start of the input file).
                    if (strategy != CompressionStrategy.Z_HUFFMAN_ONLY) {
                        match_length = longest_match(hash_head);
                    }
                    // longest_match() sets match_start
                }
                if (match_length >= MIN_MATCH) {
                    //        check_match(strstart, match_start, match_length);

                    bflush = _tr_tally(strstart - match_start, match_length - MIN_MATCH);

                    lookahead -= match_length;

                    // Insert new strings in the hash table only if the match length
                    // is not too large. This saves time but degrades compression.
                    if (match_length <= max_lazy_match && lookahead >= MIN_MATCH) {
                        match_length--; // string at strstart already in hash table
                        do {
                            strstart++;

                            ins_h = ((ins_h << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                            //	    prev[strstart&w_mask]=hash_head=head[ins_h];
                            hash_head = (head[ins_h] & 0xffff);
                            prev[strstart & w_mask] = head[ins_h];
                            head[ins_h] = (short)strstart;

                            // strstart never exceeds WSIZE-MAX_MATCH, so there are
                            // always MIN_MATCH bytes ahead.
                        }
                        while (--match_length != 0);
                        strstart++;
                    } else {
                        strstart += match_length;
                        match_length = 0;
                        ins_h = window[strstart] & 0xff;

                        ins_h = (((ins_h) << hash_shift) ^ (window[strstart + 1] & 0xff)) & hash_mask;
                        // If lookahead < MIN_MATCH, ins_h is garbage, but it does not
                        // matter since it will be recomputed at next deflate call.
                    }
                } else {
                    // No match, output a literal byte

                    bflush = _tr_tally(0, window[strstart] & 0xff);
                    lookahead--;
                    strstart++;
                }
                if (bflush) {

                    flush_block_only(false);
                    if (strm.avail_out == 0)
                        return NeedMore;
                }
            }

            flush_block_only(flush == (int)FlushStrategy.Z_FINISH);
            if (strm.avail_out == 0) {
                if (flush == (int)FlushStrategy.Z_FINISH)
                    return FinishStarted;
                else
                    return NeedMore;
            }
            return flush == (int)FlushStrategy.Z_FINISH ? FinishDone : BlockDone;
        }

        ///<summary>
        /// Same as above, but achieves better compression. We use a lazy
        /// evaluation for matches: a match is finally adopted only if there is
        /// no better match at the next Window position.
        ///</summary>
        private int deflate_slow(int flush) {
            //    short hash_head = 0;    // head of hash chain
            int hash_head = 0; // head of hash chain
            bool bflush; // set if current block must be flushed

            // Process the input block.
            while (true) {
                // Make sure that we always have enough lookahead, except
                // at the End of the input file. We need MAX_MATCH bytes
                // for the next match, plus MIN_MATCH bytes to insert the
                // string following the next match.

                if (lookahead < MIN_LOOKAHEAD) {
                    fill_window();
                    if (lookahead < MIN_LOOKAHEAD && flush == (int)FlushStrategy.Z_NO_FLUSH) {
                        return NeedMore;
                    }
                    if (lookahead == 0)
                        break; // internalFlush the current block
                }

                // Insert the string Window[strstart .. strstart+2] in the
                // dictionary, and set hash_head to the head of the hash chain:

                if (lookahead >= MIN_MATCH) {
                    ins_h = (((ins_h) << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                    //	prev[strstart&w_mask]=hash_head=head[ins_h];
                    hash_head = (head[ins_h] & 0xffff);
                    prev[strstart & w_mask] = head[ins_h];
                    head[ins_h] = (short)strstart;
                }

                // Find the longest match, discarding those <= prev_length.
                prev_length = match_length; prev_match = match_start;
                match_length = MIN_MATCH - 1;

                if (hash_head != 0 && prev_length < max_lazy_match && ((strstart - hash_head) & 0xffff) <= w_size - MIN_LOOKAHEAD) {
                    // To simplify the code, we prevent matches with the string
                    // of Window index 0 (in particular we have to avoid a match
                    // of the string with itself at the start of the input file).

                    if (strategy != CompressionStrategy.Z_HUFFMAN_ONLY) {
                        match_length = longest_match(hash_head);
                    }
                    // longest_match() sets match_start

                    if (match_length <= 5 && (strategy == CompressionStrategy.Z_FILTERED || (match_length == MIN_MATCH && strstart - match_start > 4096))) {

                        // If prev_match is also MIN_MATCH, match_start is garbage
                        // but we will ignore the current match anyway.
                        match_length = MIN_MATCH - 1;
                    }
                }

                // If there was a match at the previous step and the current
                // match is not better, output the previous match:
                if (prev_length >= MIN_MATCH && match_length <= prev_length) {
                    int max_insert = strstart + lookahead - MIN_MATCH;
                    // Do not insert strings in hash table beyond this.

                    //          check_match(strstart-1, prev_match, prev_length);

                    bflush = _tr_tally(strstart - 1 - prev_match, prev_length - MIN_MATCH);

                    // Insert in hash table all strings up to the End of the match.
                    // strstart-1 and strstart are already inserted. If there is not
                    // enough lookahead, the last two strings are not inserted in
                    // the hash table.
                    lookahead -= (prev_length - 1);
                    prev_length -= 2;
                    do {
                        if (++strstart <= max_insert) {
                            ins_h = (((ins_h) << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                            //prev[strstart&w_mask]=hash_head=head[ins_h];
                            hash_head = (head[ins_h] & 0xffff);
                            prev[strstart & w_mask] = head[ins_h];
                            head[ins_h] = (short)strstart;
                        }
                    }
                    while (--prev_length != 0);
                    match_available = 0;
                    match_length = MIN_MATCH - 1;
                    strstart++;

                    if (bflush) {
                        flush_block_only(false);
                        if (strm.avail_out == 0)
                            return NeedMore;
                    }
                } else if (match_available != 0) {

                    // If there was no match at the previous position, output a
                    // single literal. If there was a match but the current match
                    // is longer, truncate the previous match to a single literal.

                    bflush = _tr_tally(0, window[strstart - 1] & 0xff);

                    if (bflush) {
                        flush_block_only(false);
                    }
                    strstart++;
                    lookahead--;
                    if (strm.avail_out == 0)
                        return NeedMore;
                } else {
                    // There is no previous match to compare with, wait for
                    // the next step to decide.

                    match_available = 1;
                    strstart++;
                    lookahead--;
                }
            }

            if (match_available != 0) {
                bflush = _tr_tally(0, window[strstart - 1] & 0xff);
                match_available = 0;
            }
            flush_block_only(flush == (int)FlushStrategy.Z_FINISH);

            if (strm.avail_out == 0) {
                if (flush == (int)FlushStrategy.Z_FINISH)
                    return FinishStarted;

                return NeedMore;
            }

            return flush == (int)FlushStrategy.Z_FINISH ? FinishDone : BlockDone;
        }

        /// <summary>
        /// Finds the longest matching data part
        /// </summary>
        private int longest_match(int cur_match) {
            int chain_length = max_chain_length; // max hash chain length
            int scan = strstart; // current string
            int match; // matched string
            int len; // length of current match
            int best_len = prev_length; // best match length so far
            int limit = strstart > (w_size - MIN_LOOKAHEAD) ? strstart - (w_size - MIN_LOOKAHEAD) : 0;
            int nice_match = this.nice_match;

            // Stop when cur_match becomes <= limit. To simplify the code,
            // we prevent matches with the string of Window index 0.

            int wmask = w_mask;

            int strend = strstart + MAX_MATCH;
            byte scan_end1 = window[scan + best_len - 1];
            byte scan_end = window[scan + best_len];

            // The code is optimized for HASH_BITS >= 8 and MAX_MATCH-2 multiple of 16.
            // It is easy to get rid of this optimization if necessary.

            // Do not waste too much time if we already have a good match:
            if (prev_length >= good_match) {
                chain_length >>= 2;
            }

            // Do not look for matches beyond the End of the input. This is necessary
            // to make deflate deterministic.
            if (nice_match > lookahead)
                nice_match = lookahead;

            do {
                match = cur_match;

                // Skip to next match if the match length cannot increase
                // or if the match length is less than 2:
                if (window[match + best_len] != scan_end || window[match + best_len - 1] != scan_end1 || window[match] != window[scan] || window[++match] != window[scan + 1])
                    continue;

                // The check at best_len-1 can be removed because it will be made
                // again later. (This heuristic is not always a win.)
                // It is not necessary to compare scan[2] and match[2] since they
                // are always equal when the other bytes match, given that
                // the hash keys are equal and that HASH_BITS >= 8.
                scan += 2; match++;

                // We check for insufficient lookahead only every 8th comparison;
                // the 256th check will be made at strstart+258.
                do {
                }
                while (window[++scan] == window[++match] && window[++scan] == window[++match] && window[++scan] == window[++match] && window[++scan] == window[++match] && window[++scan] == window[++match] && window[++scan] == window[++match] && window[++scan] == window[++match] && window[++scan] == window[++match] && scan < strend);

                len = MAX_MATCH - (int)(strend - scan);
                scan = strend - MAX_MATCH;

                if (len > best_len) {
                    match_start = cur_match;
                    best_len = len;
                    if (len >= nice_match)
                        break;
                    scan_end1 = window[scan + best_len - 1];
                    scan_end = window[scan + best_len];
                }
            }
            while ((cur_match = (prev[cur_match & wmask] & 0xffff)) > limit && --chain_length != 0);

            if (best_len <= lookahead)
                return best_len;
            return lookahead;
        }

        /// <summary>
        /// Deflate algorithm initialization
        /// </summary>
        /// <param name="strm">ZStream object</param>
        /// <param name="level">Compression level</param>
        /// <param name="bits">Window bits</param>
        /// <returns>A result code</returns>
        internal int DeflateInit(ZStream strm, int level, int bits) {
            return DeflateInit2(strm, level, bits, DEF_MEM_LEVEL, CompressionStrategy.Z_DEFAULT_STRATEGY);
        }

        /// <summary>
        /// Initializes deflate algorithm
        /// </summary>
        /// <param name="strm">ZStream object</param>
        /// <param name="level">Compression level</param>
        /// <returns>Operation result result code</returns>
        internal int DeflateInit(ZStream strm, int level) {
            return DeflateInit(strm, level, ZLibUtil.MAX_WBITS);
        }

        /// <summary>
        /// Deflate algorithm initialization
        /// </summary>
        /// <param name="strm">ZStream object</param>
        /// <param name="level">Compression level</param>
        /// <param name="windowBits">Window bits</param>
        /// <param name="memLevel">Memory level</param>
        /// <param name="strategy">Compression strategy</param>
        /// <returns>Operation result code</returns>
        internal int DeflateInit2(ZStream strm, int level, int windowBits, int memLevel, CompressionStrategy strategy) {
            int noheader = 0;

            strm.msg = null;

            if (level == Z_DEFAULT_COMPRESSION)
                level = 6;

            if (windowBits < 0) {
                // undocumented feature: suppress zlib header
                noheader = 1;
                windowBits = -windowBits;
            }

            if (memLevel < 1 || memLevel > MAX_MEM_LEVEL || windowBits < 9 || windowBits > 15 || level < 0 || level > 9 || strategy < 0 || strategy > CompressionStrategy.Z_HUFFMAN_ONLY) {
                return (int)ZLibResultCode.Z_STREAM_ERROR;
            }

            strm.dstate = (Deflate)this;

            this.NoHeader = noheader;
            w_bits = windowBits;
            w_size = 1 << w_bits;
            w_mask = w_size - 1;

            hash_bits = memLevel + 7;
            hash_size = 1 << hash_bits;
            hash_mask = hash_size - 1;
            hash_shift = ((hash_bits + MIN_MATCH - 1) / MIN_MATCH);

            window = new byte[w_size * 2];
            prev = new short[w_size];
            head = new short[hash_size];

            lit_bufsize = 1 << (memLevel + 6); // 16K elements by default

            // We overlay Pending_buf and d_buf+l_buf. This works since the average
            // output size for (length,distance) codes is <= 24 bits.
            Pending_buf = new byte[lit_bufsize * 4];
            pending_buf_size = lit_bufsize * 4;

            d_buf = lit_bufsize;
            l_buf = (1 + 2) * lit_bufsize;

            this.level = level;

            this.strategy = strategy;
            method = Z_DEFLATED;

            return deflateReset(strm);
        }

        /// <summary>
        /// Resets the current state of deflate object
        /// </summary>
        internal int deflateReset(ZStream strm) {
            strm.total_in = strm.total_out = 0;
            strm.msg = null; //
            strm.Data_type = BlockType.Z_UNKNOWN;

            Pending = 0;
            Pending_out = 0;

            if (NoHeader < 0) {
                NoHeader = 0; // was set to -1 by deflate(..., Z_FINISH);
            }
            status = (NoHeader != 0) ? DeflateState.BUSY_STATE : DeflateState.INIT_STATE;

            strm.adler = Adler32.GetAdler32Checksum(0, null, 0, 0);

            last_flush = (int)FlushStrategy.Z_NO_FLUSH;

            tr_init();
            lm_init();
            return (int)ZLibResultCode.Z_OK;
        }

        /// <summary>
        /// Finish compression with deflate algorithm
        /// </summary>
        internal int deflateEnd() {
            if (status != DeflateState.INIT_STATE && status != DeflateState.BUSY_STATE && status != DeflateState.FINISH_STATE) {
                return (int)ZLibResultCode.Z_STREAM_ERROR;
            }
            // Deallocate in reverse order of allocations:
            Pending_buf = null;
            head = null;
            prev = null;
            window = null;

            return status == DeflateState.BUSY_STATE ? (int)ZLibResultCode.Z_DATA_ERROR : (int)ZLibResultCode.Z_OK;
        }

        /// <summary>
        /// Sets deflate algorithm parameters
        /// </summary>
        internal int deflateParams(ZStream strm, int level, CompressionStrategy strategy) {
            int err = (int)ZLibResultCode.Z_OK;

            if (level == Z_DEFAULT_COMPRESSION) {
                level = 6;
            }
            if (level < 0 || level > 9 || strategy < 0 || strategy > CompressionStrategy.Z_HUFFMAN_ONLY) {
                return (int)ZLibResultCode.Z_STREAM_ERROR;
            }

            if (config_table[this.level].func != config_table[level].func && strm.total_in != 0) {
                // Flush the last buffer:
                err = strm.deflate(FlushStrategy.Z_PARTIAL_FLUSH);
            }

            if (this.level != level) {
                this.level = level;
                max_lazy_match = config_table[this.level].max_lazy;
                good_match = config_table[this.level].good_length;
                nice_match = config_table[this.level].nice_length;
                max_chain_length = config_table[this.level].max_chain;
            }
            this.strategy = strategy;
            return err;
        }

        /// <summary>
        /// Sets deflate dictionary
        /// </summary>
        internal int deflateSetDictionary(ZStream strm, byte[] dictionary, int dictLength) {
            int length = dictLength;
            int index = 0;

            if (dictionary == null || status != DeflateState.INIT_STATE)
                return (int)ZLibResultCode.Z_STREAM_ERROR;

            strm.adler = Adler32.GetAdler32Checksum(strm.adler, dictionary, 0, dictLength);

            if (length < MIN_MATCH)
                return (int)ZLibResultCode.Z_OK;
            if (length > w_size - MIN_LOOKAHEAD) {
                length = w_size - MIN_LOOKAHEAD;
                index = dictLength - length; // use the tail of the dictionary
            }
            Array.Copy(dictionary, index, window, 0, length);
            strstart = length;
            block_start = length;

            // Insert all strings in the hash table (except for the last two bytes).
            // s->lookahead stays null, so s->ins_h will be recomputed at the next
            // call of fill_window.

            ins_h = window[0] & 0xff;
            ins_h = (((ins_h) << hash_shift) ^ (window[1] & 0xff)) & hash_mask;

            for (int n = 0; n <= length - MIN_MATCH; n++) {
                ins_h = (((ins_h) << hash_shift) ^ (window[(n) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                prev[n & w_mask] = head[ins_h];
                head[ins_h] = (short)n;
            }
            return (int)ZLibResultCode.Z_OK;
        }

        /// <summary>
        /// Performs data compression with the deflate algorithm
        /// </summary>
        internal int deflate(ZStream strm, FlushStrategy f) {
            int internalFlush = (int)f;

            if (internalFlush > (int)FlushStrategy.Z_FINISH || internalFlush < 0) {
                return (int)ZLibResultCode.Z_STREAM_ERROR;
            }

            if (strm.next_out == null || (strm.next_in == null && strm.avail_in != 0) || (status == DeflateState.FINISH_STATE && internalFlush != (int)FlushStrategy.Z_FINISH)) {
                strm.msg = ZLibUtil.z_errmsg[(int)ZLibResultCode.Z_NEED_DICT - ((int)ZLibResultCode.Z_STREAM_ERROR)];
                return (int)ZLibResultCode.Z_STREAM_ERROR;
            }
            if (strm.avail_out == 0) {
                strm.msg = ZLibUtil.z_errmsg[(int)ZLibResultCode.Z_NEED_DICT - ((int)ZLibResultCode.Z_BUF_ERROR)];
                return (int)ZLibResultCode.Z_BUF_ERROR;
            }

            this.strm = strm; // just in case
            int old_flush = last_flush;
            last_flush = internalFlush;

            // Write the zlib header
            if (status == DeflateState.INIT_STATE) {
                int header = (Z_DEFLATED + ((w_bits - 8) << 4)) << 8;
                int level_flags = (level > 0) ? ((level - 1) & 0xff) >> 1 : 0;

                if (level_flags > 3)
                    level_flags = 3;
                header |= (level_flags << 6);
                if (strstart != 0)
                    header |= PRESET_DICT;
                header += 31 - (header % 31);

                status = DeflateState.BUSY_STATE;
                putShortMSB(header);


                // Save the adler32 of the preset dictionary:
                if (strstart != 0) {
                    putShortMSB((int)(ZLibUtil.URShift(strm.adler, 16)));
                    putShortMSB((int)(strm.adler & 0xffff));
                }
                strm.adler = Adler32.GetAdler32Checksum(0, null, 0, 0);
            }

            // Flush as much pending output as possible
            if (Pending != 0) {
                strm.FlushPending();
                if (strm.avail_out == 0) {
                    //System.out.println("  _avail_out==0");
                    // Since _avail_out is 0, deflate will be called again with
                    // more output space, but possibly with both pending and
                    // _avail_in equal to zero. There won't be anything to do,
                    // but this is not an error situation so make sure we
                    // return OK instead of BUF_ERROR at next call of deflate:
                    last_flush = -1;
                    return (int)ZLibResultCode.Z_OK;
                }

                // Make sure there is something to do and avoid duplicate consecutive
                // flushes. For repeated and useless calls with Z_FINISH, we keep
                // returning (int)ZLibResultCode.Z_STREAM_END instead of Z_BUFF_ERROR.
            } else if (strm.avail_in == 0 && internalFlush <= old_flush && internalFlush != (int)FlushStrategy.Z_FINISH) {
                strm.msg = ZLibUtil.z_errmsg[(int)ZLibResultCode.Z_NEED_DICT - ((int)ZLibResultCode.Z_BUF_ERROR)];
                return (int)ZLibResultCode.Z_BUF_ERROR;
            }

            // User must not provide more input after the first FINISH:
            if (status == DeflateState.FINISH_STATE && strm.avail_in != 0) {
                strm.msg = ZLibUtil.z_errmsg[(int)ZLibResultCode.Z_NEED_DICT - ((int)ZLibResultCode.Z_BUF_ERROR)];
                return (int)ZLibResultCode.Z_BUF_ERROR;
            }

            // Start a new block or continue the current one.
            if (strm.avail_in != 0 || lookahead != 0 || (internalFlush != (int)FlushStrategy.Z_NO_FLUSH && status != DeflateState.FINISH_STATE)) {
                int bstate = -1;
                switch (config_table[level].func) {

                    case STORED:
                        bstate = deflate_stored(internalFlush);
                        break;

                    case FAST:
                        bstate = deflate_fast(internalFlush);
                        break;

                    case SLOW:
                        bstate = deflate_slow(internalFlush);
                        break;

                    default:
                        break;

                }

                if (bstate == FinishStarted || bstate == FinishDone) {
                    status = DeflateState.FINISH_STATE;
                }
                if (bstate == NeedMore || bstate == FinishStarted) {
                    if (strm.avail_out == 0) {
                        last_flush = -1; // avoid BUF_ERROR next call, see above
                    }
                    return (int)ZLibResultCode.Z_OK;
                    // If internalFlush != Z_NO_FLUSH && _avail_out == 0, the next call
                    // of deflate should use the same internalFlush parameter to make sure
                    // that the internalFlush is complete. So we don't have to output an
                    // empty block here, this will be done at next call. This also
                    // ensures that for a very small output buffer, we emit at most
                    // one empty block.
                }

                if (bstate == BlockDone) {
                    if (internalFlush == (int)FlushStrategy.Z_PARTIAL_FLUSH) {
                        _tr_align();
                    } else {
                        // FULL_FLUSH or SYNC_FLUSH
                        _tr_stored_block(0, 0, false);
                        // For a full internalFlush, this empty block will be recognized
                        // as a special marker by inflate_sync().
                        if (internalFlush == (int)FlushStrategy.Z_FULL_FLUSH) {
                            for (int i = 0; i < hash_size; i++)
                                // forget history
                                head[i] = 0;
                        }
                    }
                    strm.FlushPending();
                    if (strm.avail_out == 0) {
                        last_flush = -1; // avoid BUF_ERROR at next call, see above
                        return (int)ZLibResultCode.Z_OK;
                    }
                }
            }

            if (internalFlush != (int)FlushStrategy.Z_FINISH)
                return (int)ZLibResultCode.Z_OK;
            if (NoHeader != 0)
                return (int)ZLibResultCode.Z_STREAM_END;

            // Write the zlib trailer (adler32)
            putShortMSB((int)(ZLibUtil.URShift(strm.adler, 16)));
            putShortMSB((int)(strm.adler & 0xffff));
            strm.FlushPending();

            // If _avail_out is zero, the application will call deflate again
            // to internalFlush the rest.
            NoHeader = -1; // WritePos the trailer only once!
            return Pending != 0 ? (int)ZLibResultCode.Z_OK : (int)ZLibResultCode.Z_STREAM_END;
        }

        #endregion

        /// <summary>
        /// Static constructor initializes config_table
        /// </summary>
        static Deflate() {
            {
                config_table = new Config[10];
                // good  lazy  nice  chain
                config_table[0] = new Config(0, 0, 0, 0, STORED);
                config_table[1] = new Config(4, 4, 8, 4, FAST);
                config_table[2] = new Config(4, 5, 16, 8, FAST);
                config_table[3] = new Config(4, 6, 32, 32, FAST);

                config_table[4] = new Config(4, 4, 16, 16, SLOW);
                config_table[5] = new Config(8, 16, 32, 32, SLOW);
                config_table[6] = new Config(8, 16, 128, 128, SLOW);
                config_table[7] = new Config(8, 32, 128, 256, SLOW);
                config_table[8] = new Config(32, 128, 258, 1024, SLOW);
                config_table[9] = new Config(32, 258, 258, 4096, SLOW);
            }
        }
    }
}
