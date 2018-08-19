// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    internal sealed class NoLineFeedSourceContentProvider : TextContentProvider {
        private readonly string/*!*/ _code;

        internal sealed class Reader : SourceCodeReader {

            internal Reader(string/*!*/ s)
                : base(new StringReader(s), null) {
            }

            public override string/*!*/ ReadLine() {
                return IOUtils.ReadTo(this, '\n');
            }

            public override bool SeekLine(int line) {
                int currentLine = 1;

                for (;;) {
                    if (currentLine == line) return true;
                    if (!IOUtils.SeekTo(this, '\n')) return false;
                    currentLine++;
                }
            }
        }        
        
        public NoLineFeedSourceContentProvider(string/*!*/ code) {
            Assert.NotNull(code);
            _code = code;
        }

        public override SourceCodeReader/*!*/ GetReader() {
            return new Reader(_code);
        }
    }
}
