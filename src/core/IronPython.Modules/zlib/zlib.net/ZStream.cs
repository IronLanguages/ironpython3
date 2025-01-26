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

#pragma warning disable 419 // msc: Ambiguous reference in cref attribute

namespace ComponentAce.Compression.Libs.ZLib
{
    /// <summary>
    /// ZStream is used to store user data to compress/decompress.
    /// </summary>
    public sealed class ZStream
    {

        #region Constants
        
        private const int DEF_WBITS = ZLibUtil.MAX_WBITS;

        #endregion

        #region Fields
        
        /// <summary>
        /// Next input byte array
        /// </summary>
        private byte[] _next_in;

        /// <summary>
        /// Index of the first byte in the <see cref="next_in">input array</see>.
        /// </summary>
        private int _next_in_index;

        /// <summary>
        /// Number of bytes available at _next_in
        /// </summary>
        private int _avail_in;
        
        /// <summary>
        /// total nb of input bytes ReadPos so far
        /// </summary>
        private long _total_in;

        /// <summary>
        /// Byte array for the next output block
        /// </summary>
        private byte[] _next_out;

        /// <summary>
        /// Index of the first byte in the _next_out array
        /// </summary>
        private int _next_out_index;

        /// <summary>
        /// Remaining free space at _next_out
        /// </summary>
        private int _avail_out;

        /// <summary>
        /// Total number of bytes in output array
        /// </summary>
        private long _total_out;

        /// <summary>
        /// A string to store operation result message (corresponding to result codes)
        /// </summary>
        private string _msg;

        /// <summary>
        /// A deflate object to perform data compression
        /// </summary>
        private Deflate _dstate;

        /// <summary>
        /// Inflate object to perform data decompression
        /// </summary>
        private Inflate _istate;

        #endregion

        #region Properties

        /// <summary>
        /// Adler-32 value for uncompressed data processed so far.
        /// </summary>
        public long adler { get; set; }

        /// <summary>
        /// Best guess about the data type: ascii or binary
        /// </summary>
        public BlockType Data_type { get; set; }

        /// <summary>
        /// Gets/Sets the next input byte array.
        /// </summary>
        public byte[] next_in
        {
            get { return _next_in; }
            set { _next_in = value; }
        }

        /// <summary>
        /// Index of the first byte in the <see cref="next_in">input array</see>.
        /// </summary>
        public int next_in_index
        {
            get { return _next_in_index; }
            set { _next_in_index = value; }
        }

        /// <summary>
        /// Gets/Sets the number of bytes available in the <see cref="next_in">input buffer</see>.
        /// </summary>
        public int avail_in
        {
            get { return _avail_in; }
            set { _avail_in = value; }
        }

        /// <summary>
        /// Gets/Sets the total number of bytes in the <see cref="next_in">input buffer</see>.
        /// </summary>
        public long total_in
        {
            get { return _total_in; }
            set { _total_in = value; }
        }

        /// <summary>
        /// Gets/Sets the buffer for the next output data.
        /// </summary>
        public byte[] next_out
        {
            get { return _next_out; }
            set { _next_out = value; }
        }

        /// <summary>
        /// Gets/Sets the index of the first byte in the <see cref="next_out" /> byte array to write to.
        /// </summary>
        public int next_out_index
        {
            get { return _next_out_index; }
            set { _next_out_index = value; }
        }

        /// <summary>
        /// Gets/Sets the remaining free space in the <see cref="next_out" /> buffer.
        /// </summary>
        public int avail_out
        {
            get { return _avail_out; }
            set { _avail_out = value; }
        }

        /// <summary>
        /// Gets/Sets the total number of bytes in the <see cref="next_out">output array</see>.
        /// </summary>
        public long total_out
        {
            get { return _total_out; }
            set { _total_out = value; }
        }

        /// <summary>
        /// Gets sets the last error message occurred during class operations.
        /// </summary>
        public string msg
        {
            get { return _msg; }
            set { _msg = value; }
        }

        /// <summary>
        /// A deflate object to perform data compression
        /// </summary>
        internal Deflate dstate
        {
            get { return _dstate; }
            set { _dstate = value; }
        }

        /// <summary>
        /// Inflate object to perform data decompression
        /// </summary>
        internal Inflate istate
        {
            get { return _istate; }
            set { _istate = value; }
        }

        public bool IsInitialized {
            get { return _dstate != null || _istate != null; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the internal stream state for decompression. The fields <see cref="next_in" />, <see cref="avail_in" /> must be 
        /// initialized before by the caller. If <see cref="next_in" /> is not <c>null</c> and <see cref="avail_in" /> is large 
        /// enough (the exact value depends on the compression method), <see cref="inflateInit()" /> determines the compression 
        /// method from the ZLib header and allocates all data structures accordingly; otherwise the allocation will be deferred 
        /// to the first call of <see cref="inflate" />. 
        /// </summary>
        /// <returns>
        /// inflateInit returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_MEM_ERROR" /> if there was not enough memory,  
        /// <see cref="ZLibResultCode.Z_VERSION_ERROR" /> if the ZLib library version is incompatible with the version assumed by the caller. 
        /// <see cref="msg" /> is set to <c>null</c> if there is no error message. <see cref="inflateInit()" /> does not perform any decompression 
        /// apart from reading the ZLib header if present: this will be done by <see cref="inflate" />. (So <see cref="next_in" /> and <see cref="avail_in" /> 
        /// may be modified, but <see cref="next_out" /> and <see cref="avail_out" /> are unchanged.)
        /// </returns>
        public int inflateInit()
		{
			return inflateInit(DEF_WBITS);
		}

        /// <summary>
        /// This is another version of <see cref="inflateInit()" /> with an extra parameter. The fields <see cref="next_in" />, <see cref="avail_in" /> must be 
        /// initialized before by the caller. If <see cref="next_in" /> is not <c>null</c> and <see cref="avail_in" /> is large enough 
        /// (the exact value depends on the compression method), <see cref="inflateInit(int)" /> determines the compression method from 
        /// the ZLib header and allocates all data structures accordingly; otherwise the allocation will be deferred to the first 
        /// call of <see cref="inflate" />. 
        /// </summary>
        /// <param name="windowBits">The <c>windowBits</c> parameter is the base two logarithm of the maximum window size (the size of the history buffer). 
        /// It should be in the range <c>8..15</c> for this version of the library. The default value is 15 if <see cref="inflateInit(int)" /> is used instead.
        /// If a compressed stream with a larger window size is given as input, <see cref="inflate" /> will return with the error code 
        /// <see cref="ZLibResultCode.Z_DATA_ERROR" /> instead of trying to allocate a larger window.</param>
        /// <returns>
        /// inflateInit returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_MEM_ERROR" /> if there was not enough memory,
        /// <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if a parameter is invalid (such as a negative memLevel). <see cref="msg" /> is set to null 
        /// if there is no error message. <see cref="inflateInit(int)" /> does not perform any decompression apart from reading the ZLib header 
        /// if present: this will be done by <see cref="inflate" />. (So <see cref="next_in" /> and <see cref="avail_in" /> may be modified, 
        /// but <see cref="next_out" /> and <see cref="avail_out" /> are unchanged.)
        /// </returns>
		public int inflateInit(int windowBits)
		{
			_istate = new Inflate();
			return _istate.inflateInit(this, windowBits);
		}
		
        /// <summary>
        /// <para>This method decompresses as much data as possible, and stops when the input buffer (<see cref="next_in" />) becomes empty or 
        /// the output buffer (<see cref="next_out" />) becomes full. It may some introduce some output latency (reading input without producing any output) 
        /// except when forced to flush. </para>
        /// <para>The detailed semantics are as follows. <see cref="inflate" /> performs one or both of the following actions: </para>
        /// <para>
        /// <list type="bullet">
        /// <item>Decompress more input starting at <see cref="ZStream.next_in" /> and update <see cref="ZStream.next_in" /> and <see cref="ZStream.avail_in" /> 
        /// accordingly. If not all input can be processed (because there is not enough room in the output buffer), <see cref="next_in" /> is updated and 
        /// processing will resume at this point for the next call of <see cref="inflate" />. </item>
        /// <item>Provide more output starting at <see cref="next_out" /> and update <see cref="ZStream.next_out" /> and <see cref="ZStream.avail_out" /> 
        /// accordingly. <see cref="ZStream.inflate" /> provides as much output as possible, until there is no more input data or no more space in 
        /// the output buffer (see below about the <paramref name="flush" /> parameter).</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="flush"><see cref="FlushStrategy">Flush strategy</see> to use.</param>
        /// <remarks>
        /// <para>Before the call of <see cref="inflate" />, the application should ensure that at least one of the actions is possible, by providing 
        /// more input and/or consuming more output, and updating the next_* and avail_* values accordingly. The application can consume the uncompressed 
        /// output when it wants, for example when the output buffer is full (<c>avail_out == 0</c>), or after each call of <see cref="inflate" />. 
        /// If <see cref="inflate" /> returns <see cref="ZLibResultCode.Z_OK" /> and with zero <see cref="avail_out" />, it must be called again 
        /// after making room in the <see cref="next_out">output buffer</see> because there might be more output pending. </para>
        /// <para>If the parameter <paramref name="flush" /> is set to <see cref="FlushStrategy.Z_SYNC_FLUSH" />, <see cref="inflate" /> flushes 
        /// as much output as possible to the output buffer. The flushing behavior of <see cref="inflate" /> is not specified for values of 
        /// the <paramref name="flush" /> parameter other than <see cref="FlushStrategy.Z_SYNC_FLUSH" /> and <see cref="FlushStrategy.Z_FINISH" />, 
        /// but the current implementation actually flushes as much output as possible anyway. </para>
        /// <para><see cref="inflate" /> should normally be called until it returns <see cref="ZLibResultCode.Z_STREAM_END" /> or an error. 
        /// However if all decompression is to be performed in a single step (a single call of inflate), the parameter <paramref name="flush" /> 
        /// should be set to <see cref="FlushStrategy.Z_FINISH" />. In this case all pending input is processed and all pending output is flushed; 
        /// <see cref="avail_out" /> must be large enough to hold all the uncompressed data. (The size of the uncompressed data may have been 
        /// saved by the compressor for this purpose.) The next operation on this stream must be <see cref="inflateEnd" /> to deallocate the decompression 
        /// state. The use of <see cref="FlushStrategy.Z_FINISH" /> is never required, but can be used to inform <see cref="inflate" /> that a faster 
        /// routine may be used for the single <see cref="inflate" /> call. </para>
        /// <para>If a preset dictionary is needed at this point (see <see cref = "inflateSetDictionary" />), <see cref="inflate" /> sets strm-adler 
        /// to the adler32 checksum of the dictionary chosen by the compressor and returns <see cref="ZLibResultCode.Z_NEED_DICT" />; otherwise it 
        /// sets strm->adler to the adler32 checksum of all output produced so far (that is, <see cref="total_out" /> bytes) and returns
        /// <see cref="ZLibResultCode.Z_OK" />, <see cref="ZLibResultCode.Z_STREAM_END" /> or an error code as described below. At the end of the stream, 
        /// <see cref="inflate" />) checks that its computed adler32 checksum is equal to that saved by the compressor and returns
        /// <see cref="ZLibResultCode.Z_STREAM_END" /> only if the checksum is correct.</para>
        /// </remarks>
        /// <returns>
        /// <see cref="inflate" /> returns <see cref="ZLibResultCode.Z_OK" /> if some progress has been made (more input processed or more output produced), 
        /// <see cref="ZLibResultCode.Z_STREAM_END" /> if the end of the compressed data has been reached and all uncompressed output has been produced, 
        /// <see cref="ZLibResultCode.Z_NEED_DICT" /> if a preset dictionary is needed at this point, <see cref="ZLibResultCode.Z_DATA_ERROR" /> if 
        /// the input data was corrupted (input stream not conforming to the ZLib format or incorrect adler32 checksum), 
        /// <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if the stream structure was inconsistent (for example if <see cref="next_in" /> or 
        /// <see cref="next_out" /> was <c>null</c>), <see cref="ZLibResultCode.Z_MEM_ERROR" /> if there was not enough memory, 
        /// <see cref="ZLibResultCode.Z_BUF_ERROR" /> if no progress is possible or if there was not enough room in the output buffer 
        /// when <see cref="FlushStrategy.Z_FINISH" /> is used. In the <see cref="ZLibResultCode.Z_DATA_ERROR" /> case, the application 
        /// may then call <see cref="inflateSync" /> to look for a good compression block.
        /// </returns>
		public int inflate(FlushStrategy flush)
		{
			if (_istate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			return _istate.inflate(this, flush);
		}

        /// <summary>
        /// All dynamically allocated data structures for this stream are freed. This function discards any unprocessed input and does not flush any 
        /// pending output.
        /// </summary>
        /// <returns>
        /// inflateEnd returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_STREAM_ERROR" /> 
        /// if the stream state was inconsistent. In the error case, msg may be set but then points to a static string (which must not be deallocated).
        /// </returns>
		public int inflateEnd()
		{
		 next_in_index = 0;
		 next_out_index = 0;

			if (_istate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			int ret = _istate.inflateEnd(this);
			_istate = null;
			return ret;
		}

        /// <summary>
        /// Skips invalid compressed data until a full flush point (see the description of <see cref="deflate">deflate with Z_FULL_FLUSH</see>) can be found, 
        /// or until all available input is skipped. No output is provided.
        /// </summary>
        /// <returns>
        /// <see cref="inflateSync" /> returns <seec ref="ZLibResultCode.Z_OK" /> if a full flush point has been found, <see cref="ZLibResultCode.Z_BUF_ERROR" />
        /// if no more input was provided, <see cref="ZLibResultCode.Z_DATA_ERROR" /> if no flush point has been found, or 
        /// <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if the stream structure was inconsistent. In the success case, the application may save the current 
        /// current value of <see cref="total_in" /> which indicates where valid compressed data was found. In the error case, the application may repeatedly 
        /// call <see cref="inflateSync" />, providing more input each time, until success or end of the input data.
        /// </returns>
		public int inflateSync()
		{
			if (_istate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			return _istate.inflateSync(this);
		}

        /// <summary>
        /// Initializes the decompression dictionary from the given uncompressed byte sequence. This function must be called immediately after a call of <see cref="inflate" /> if this call returned <see cref="ZLibResultCode.Z_NEED_DICT" />. The dictionary chosen by the compressor can be determined from the Adler32 value returned by this call of <see cref="inflate" />. The compressor and decompresser must use exactly the same dictionary.
        /// </summary>
        /// <param name="dictionary">A byte array - a dictionary.</param>
        /// <param name="dictLength">The length of the dictionary.</param>
        /// <returns>
        /// inflateSetDictionary returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if a parameter is invalid (such as <c>null</c> dictionary) or the stream state is inconsistent, <see cref="ZLibResultCode.Z_DATA_ERROR" /> if the given dictionary doesn't match the expected one (incorrect Adler32 value). inflateSetDictionary does not perform any decompression: this will be done by subsequent calls of <see cref="inflate" />.
        /// </returns>
		public int inflateSetDictionary(byte[] dictionary, int dictLength)
		{
			if (_istate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			return _istate.inflateSetDictionary(this, dictionary, dictLength);
		}

        /// <summary>
        /// Initializes the internal stream state for compression. 
        /// </summary>
        /// <param name="level">An integer value from 0 to 9 indicating the desired compression level.</param>
        /// <returns>
        /// DeflateInit returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_MEM_ERROR" /> if there was not enough memory, 
        /// <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if level is not a valid compression level. <see cref="msg" /> is set to <c>null</c> if there is 
        /// no error message. <see cref="DeflateInit(int)" /> does not perform any compression: this will be done by <see cref="deflate" />.
        /// </returns>
        public int DeflateInit(int level)
		{
			return DeflateInit(level, ZLibUtil.MAX_WBITS);
		}

        /// <summary>
        /// Initializes the internal stream state for compression. 
        /// </summary>
        /// <param name="level">An integer value from 0 to 9 indicating the desired compression level.</param>
        /// <param name="bits"> The windowBits parameter is the base two logarithm of the window size (the size of the history buffer). It should be in the 
        /// range 8..15 for this version of the library. Larger values of this parameter result in better compression at the expense of memory usage. 
        /// The default value is 15 if DeflateInit is used instead.</param>
        /// <returns>
        /// DeflateInit returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_MEM_ERROR" /> if there was not enough memory,
        /// <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if level is not a valid compression level. <see cref="msg" /> is set to <c>null</c> if there 
        /// is no error message. <see cref="DeflateInit(int,int)" /> does not perform any compression: this will be done by <see cref="deflate" />.
        /// </returns>
		public int DeflateInit(int level, int bits)
		{
			_dstate = new Deflate();
			return _dstate.DeflateInit(this, level, bits);
		}

        public int DeflateInit(int level, int windowBits, int memLevel, CompressionStrategy strategy) {
            _dstate = new Deflate();
            return _dstate.DeflateInit2(this, level, windowBits, memLevel, strategy);
        }

        public int reset() {
            if (_dstate != null) {
               return _dstate.deflateReset(this);
            }

            if (_istate != null) {
                return _istate.inflateReset(this);
            }

            return (int)ZLibResultCode.Z_STREAM_ERROR;
        }

        /// <summary>
        /// <para>Deflate compresses as much data as possible, and stops when the <see cref="next_in">input buffer</see> becomes empty or the 
        /// <see cref="next_out">output buffer</see> becomes full. It may introduce some output latency (reading input without producing any output) 
        /// except when forced to flush.</para>
        /// <para>The detailed semantics are as follows. deflate performs one or both of the following actions:
        /// <list type="bullet">
        /// <item>Compress more input starting at <see cref="next_in" /> and update <see cref="next_in" /> and <see cref="avail_in" /> accordingly. 
        /// If not all input can be processed (because there is not enough room in the output buffer), <see cref="next_in" /> and <see cref="avail_in" /> 
        /// are updated and processing will resume at this point for the next call of <see cref="deflate" />. </item>
        /// <item>Provide more output starting at <see cref="next_out" /> and update <see cref="next_out" /> and <see cref="avail_out" /> accordingly. 
        /// This action is forced if the parameter flush is non zero. Forcing flush frequently degrades the compression ratio, so this parameter should 
        /// be set only when necessary (in interactive applications). Some output may be provided even if flush is not set.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="flush">The <see cref="FlushStrategy">flush strategy</see> to use.</param>
        /// <remarks>
        /// <para>
        /// Before the call of <seec ref="deflate" />, the application should ensure that at least one of the actions is possible, by providing 
        /// more input and/or consuming more output, and updating <see cref="avail_in" /> or <see cref="avail_out" /> accordingly ; <see cref="avail_out" /> 
        /// should never be zero before the call. The application can consume the compressed output when it wants, for example when the output buffer is full
        /// (<c>avail_out == 0</c>), or after each call of <see cref="deflate" />. If <see cref="deflate" /> returns <see cref="ZLibResultCode.Z_OK" /> 
        /// and with zero <see cref="avail_out" />, it must be called again after making room in the output buffer because there might be more output pending. 
        /// </para>
        /// <para>
        /// If the parameter <paramref name="flush"/> is set to <see cref="FlushStrategy.Z_SYNC_FLUSH" />, all pending output is flushed to the 
        /// <see cref="next_out">output buffer</see> and the output is aligned on a byte boundary, so that the decompressor can get all input 
        /// data available so far. (In particular <see cref="avail_in" /> is zero after the call if enough output space has been provided before the call.) 
        /// Flushing may degrade compression for some compression algorithms and so it should be used only when necessary. 
        /// </para>
        /// <para>
        /// If flush is set to <see cref="FlushStrategy.Z_FULL_FLUSH" />, all output is flushed as with <see cref="FlushStrategy.Z_SYNC_FLUSH" />, 
        /// and the compression state is reset so that decompression can restart from this point if previous compressed data has been damaged or if 
        /// random access is desired. Using <see cref="FlushStrategy.Z_FULL_FLUSH" /> too often can seriously degrade the compression.
        /// </para>
        /// </remarks>
        /// <returns>
        /// <para>
        /// If deflate returns with <c><see cref="avail_out" /> == 0</c>, this function must be called again with the same value of the flush
        /// parameter and more output space (updated <see cref="avail_out" />), until the flush is complete (<see cref="deflate" /> returns with
        /// non-zero <see cref="avail_out" />). 
        /// </para>
        /// <para>
        /// If the parameter <paramref name="flush"/> is set to <see cref="FlushStrategy.Z_FINISH" />, pending input is processed, pending 
        /// output is flushed and deflate returns with <see cref="ZLibResultCode.Z_STREAM_END" /> if there was enough output space ; 
        /// if deflate returns with <see cref="ZLibResultCode.Z_OK" />, this function must be called again with <see cref="FlushStrategy.Z_FINISH" /> 
        /// and more output space (updated <see cref="avail_out" />) but no more input data, until it returns with <see cref="ZLibResultCode.Z_STREAM_END" /> 
        /// or an error. After deflate has returned <see cref="ZLibResultCode.Z_STREAM_END" />, the only possible operation on the stream is
        /// <see cref="deflateEnd" />. </para>
        /// <para>
        /// <see cref="FlushStrategy.Z_FINISH" /> can be used immediately after <see cref="DeflateInit(int)" /> if all the compression is to be 
        /// done in a single step. In this case, avail_out must be at least 0.1% larger than avail_in plus 12 bytes. If deflate does not return 
        /// Z_STREAM_END, then it must be called again as described above. 
        /// </para>
        /// <para>
        /// <see cref="deflate" /> sets strm-> adler to the adler32 checksum of all input read so far (that is, <see cref="total_in" /> bytes). 
        /// </para>
        /// <para>
        /// <see cref="deflate" /> may update data_type if it can make a good guess about the input data type (<see cref="BlockType">Z_ASCII or Z_BINARY</see>).
        /// In doubt, the data is considered binary. This field is only for information purposes and does not affect the compression algorithm in any manner. 
        /// </para>
        /// <para>
        /// <see cref="deflate" /> returns <see cref="ZLibResultCode.Z_OK" /> if some progress has been made (more input processed or more output produced), 
        /// <see cref="ZLibResultCode.Z_STREAM_END" /> if all input has been consumed and all output has been produced (only when flush is set to
        /// <see cref="FlushStrategy.Z_FINISH" />), <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if the stream state was inconsistent (for example if 
        /// <see cref="next_in" /> or <see cref="next_out" /> was <c>null</c>), <see cref="ZLibResultCode.Z_BUF_ERROR" /> if no progress is possible
        /// (for example <see cref="avail_in" /> or <see cref="avail_out" /> was zero).
        /// </para>
        /// </returns>
		public int deflate(FlushStrategy flush)
		{
			if (_dstate == null)
			{
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			}
			return _dstate.deflate(this, flush);
		}

        /// <summary>
        /// All dynamically allocated data structures for this stream are freed. This function discards any unprocessed input and does not flush any pending 
        /// output.
        /// </summary>
        /// <returns>
        /// deflateEnd returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if the stream state was inconsistent, 
        /// <see cref="ZLibResultCode.Z_DATA_ERROR" /> if the stream was freed prematurely (some input or output was discarded). In the error case, 
        /// <see cref="msg" /> may be set but then points to a static string (which must not be deallocated).
        /// </returns>
		public int deflateEnd()
		{
		 next_in_index = 0;
		 next_out_index = 0;

			if (_dstate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			int ret = _dstate.deflateEnd();
			_dstate = null;
			return ret;
		}

        /// <summary>
        /// Dynamically update the compression level and compression strategy. The interpretation of level is as in <see cref="DeflateInit(int)"/>. 
        /// This can be used to switch between compression and straight copy of the input data, or to switch to a different kind of input data 
        /// requiring a different strategy. If the compression level is changed, the input available so far is compressed with the old level 
        /// (and may be flushed); the new level will take effect only at the next call of <see cref="deflate" />
        /// </summary>
        /// <param name="level">An integer value indicating the desired compression level.</param>
        /// <param name="strategy">A <see cref="FlushStrategy">flush strategy</see> to use.</param>
        /// <remarks>
        /// Before the call of <see cref="deflateParams" />, the stream state must be set as for a call of <see cref="deflate" />, since the 
        /// currently available input may have to be compressed and flushed. In particular, <see cref="avail_out" /> must be non-zero.
        /// </remarks>
        /// <returns>
        /// deflateParams returns <see cref="ZLibResultCode.Z_OK" /> if success, <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if the source stream 
        /// state was inconsistent or if a parameter was invalid, <see cref="ZLibResultCode.Z_BUF_ERROR" /> if <see cref="avail_out" /> was zero.
        /// </returns>
		public int deflateParams(int level, CompressionStrategy strategy)
		{
			if (_dstate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			return _dstate.deflateParams(this, level, strategy);
		}

        /// <summary>
        /// Initializes the compression dictionary from the given byte sequence without producing any compressed output. This function must be called 
        /// immediately after <see cref="DeflateInit(int)" />, before any call of <see cref="deflate" />. The compressor and decompressor must use 
        /// exactly the same dictionary (see <see cref="inflateSetDictionary" />).
        /// </summary>
        /// <param name="dictionary">A byte array - a dictionary.</param>
        /// <param name="dictLength">The length of the dictionary byte array</param>
        /// <remarks>
        /// <para>
        /// The dictionary should consist of strings (byte sequences) that are likely to be encountered later in the data to be compressed, 
        /// with the most commonly used strings preferably put towards the end of the dictionary. Using a dictionary is most useful when the data 
        /// to be compressed is short and can be predicted with good accuracy; the data can then be compressed better than with the default empty dictionary.
        /// </para>
        /// <para>Depending on the size of the compression data structures selected by <see cref="DeflateInit(int)" />, a part of the dictionary may 
        /// in effect be discarded, for example if the dictionary is larger than the window size in <see cref="deflate" />. Thus the strings most likely 
        /// to be useful should be put at the end of the dictionary, not at the front.</para>
        /// <para>Upon return of this function, adler is set to the Adler32 value of the dictionary; the decompresser may later use this value to determine 
        /// which dictionary has been used by the compressor. (The Adler32 value applies to the whole dictionary even if only a subset of the dictionary 
        /// is actually used by the compressor.)</para>
        /// </remarks>
        /// <returns>
        /// deflateSetDictionary returns <see cref="ZLibResultCode.Z_OK" /> if success, or <see cref="ZLibResultCode.Z_STREAM_ERROR" /> if a parameter 
        /// is invalid (such as <c>null</c> dictionary) or the stream state is inconsistent (for example if <see cref="deflate" /> has already been 
        /// called for this stream or if the compression method is bsort). <see cref="deflateSetDictionary" /> does not perform any compression: 
        /// this will be done by <see cref="deflate" />.
        /// </returns>
		public int deflateSetDictionary(byte[] dictionary, int dictLength)
		{
			if (_dstate == null)
				return (int)ZLibResultCode.Z_STREAM_ERROR;
			return _dstate.deflateSetDictionary(this, dictionary, dictLength);
		}

        /// <summary>
        /// Flush as much pending output as possible. All <see cref="deflate" /> output goes through this function so some applications may wish to 
        /// modify it to avoid allocating a large <see cref="next_out" /> buffer and copying into it.
        /// </summary>
        /// <seealso cref="ReadBuf" />
        public void FlushPending()
		{
			int len = _dstate.Pending;
			
			if (len > _avail_out)
				len = _avail_out;
			if (len == 0)
				return ;
			
			Array.Copy(_dstate.Pending_buf, _dstate.Pending_out, _next_out, _next_out_index, len);
			
			_next_out_index += len;
			_dstate.Pending_out += len;
			_total_out += len;
			_avail_out -= len;
			_dstate.Pending -= len;
			if (_dstate.Pending == 0)
			{
				_dstate.Pending_out = 0;
			}
		}

        /// <summary>
        /// Read a new buffer from the current input stream, update the adler32 and total number of bytes read.  All <see cref="deflate" /> input goes 
        /// through this function so some applications may wish to modify it to avoid allocating a large <see cref="next_in" /> buffer and copying from it.
        /// </summary>
        /// <seealso cref="FlushPending"/>
		public int ReadBuf(byte[] buf, int start, int size)
		{
			int len = _avail_in;
			
			if (len > size)
				len = size;
			if (len == 0)
				return 0;
			
			_avail_in -= len;
			
			if (_dstate.NoHeader == 0)
			{
				adler = Adler32.GetAdler32Checksum(adler, _next_in, _next_in_index, len);
			}
			Array.Copy(_next_in, _next_in_index, buf, start, len);
			_next_in_index += len;
			_total_in += len;
			return len;
		}

        /// <summary>
        /// Frees all inner <see cref="ZStream" /> buffers.
        /// </summary>
        public void free()
        {
            _next_in = null;
            _next_out = null;
            _msg = null;
        }

        #endregion
    }
}
