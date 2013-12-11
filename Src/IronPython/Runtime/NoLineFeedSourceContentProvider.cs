/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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
