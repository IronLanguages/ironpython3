// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright 2012 Jeff Hardy
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.BZip2;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;

namespace IronPython.Modules.Bz2 {
    public static partial class Bz2Module {
        [PythonType]
        public class BZ2Decompressor {
            public const string __doc__ = 
@"BZ2Decompressor() -> decompressor object

Create a new decompressor object. This object may be used to decompress
data sequentially. If you want to decompress data in one shot, use the
decompress() function instead.
";

            private MemoryStream input = null;
            private BZip2InputStream bz2Input = null;
            private long lastSuccessfulPosition = 0;
            private bool _finished = false;

            public Bytes unused_data {
                get {
                    var buffer = this.input.GetBuffer();

                    var unusedCount = this.input.Length - this.lastSuccessfulPosition;
                    var unused = new byte[unusedCount];

                    Array.Copy(buffer, this.lastSuccessfulPosition, unused, 0, unusedCount);
                    
                    return Bytes.Make(unused);
                }
            }

            [Documentation(@"decompress(data) -> string

Provide more data to the decompressor object. It will return chunks
of decompressed data whenever possible. If you try to decompress data
after the end of stream is found, EOFError will be raised. If any data
was found after the end of stream, it'll be ignored and saved in
unused_data attribute.
")]
            public Bytes decompress([BytesLike]IList<byte> data) {
                if (_finished)
                    throw PythonOps.EofError("End of stream was already found");

                var bytes = data.ToArrayNoCopy();

                if (!InitializeMemoryStream(bytes))
                    AddData(bytes);

                List<byte> output = new List<byte>();
                if (InitializeBZ2Stream()) {
                    long memoryPosition = this.input.Position;
                    object state = this.bz2Input.DumpState();
                    
                    try {
                        // this is the same as what Read() does, so it's unlikely to be
                        // any slower. However, using blocks would require fewer state saves,
                        // which would probably be faster.
                        int b;
                        while ((b = this.bz2Input.ReadByte()) != -1) {
                            output.Add((byte)b);

                            memoryPosition = this.input.Position;
                            state = this.bz2Input.DumpState();
                        }
                        
                        this.lastSuccessfulPosition = this.input.Position;
                        this._finished = true;
                    } catch (IOException) {
                        // rewind the decompressor and the memory buffer to try again when
                        // more data arrives
                        this.input.Position = memoryPosition;
                        this.bz2Input.RestoreState(state);
                    }
                }

                return new Bytes(output);
            }

            private bool InitializeMemoryStream(byte[] data) {
                if (this.input != null) {
                    return false;
                }

                this.input = new MemoryStream();
                this.input.Write(data, 0, data.Length);
                this.input.Position = 0;

                return true;
            }

            private bool InitializeBZ2Stream() {
                if (this.bz2Input != null) {
                    return true;
                }

                try {
                    this.bz2Input = new BZip2InputStream(this.input, true);
                    return true;
                } catch (IOException) {
                    // need to rewind the memory buffer so that the next attempt will start from
                    // the beginning of the block
                    this.input.Position = lastSuccessfulPosition;
                    return false;
                }
            }

            /// <summary>
            /// Add data to the input buffer. This manipulates the position of the stream
            /// to make it appear to the BZip2 stream that nothing has actually changed.
            /// </summary>
            /// <param name="bytes">The data to append to the buffer.</param>
            private void AddData(byte[] bytes) {
                // Move the memory pointer to the end to add more data
                long position = this.input.Position;
                this.input.Position = this.input.Length;

                this.input.Write(bytes.ToArray(), 0, bytes.Length);

                // restore the position so that bz2 doesn't get confused
                this.input.Position = position;
            }
        }
    }
}

