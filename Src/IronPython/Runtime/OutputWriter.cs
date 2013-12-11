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

using System;
using System.IO;
using System.Text;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    internal sealed class OutputWriter : TextWriter {
        private readonly PythonContext _context;
        private readonly bool _isErrorOutput;

        public OutputWriter(PythonContext/*!*/ context, bool isErrorOutput) {
            Assert.NotNull(context);
            _context = context;
            _isErrorOutput = isErrorOutput;
        }

        public object Sink {
            get {
                return (_isErrorOutput) ? _context.SystemStandardError : _context.SystemStandardOut;
            }
        }

        public override Encoding Encoding {
            get {
                PythonFile file = Sink as PythonFile;
                return (file != null) ? file.Encoding : null;
            }
        }

        public override void Write(string value) {
            // the context arg is only used to get stdout if it's not passed in
            try {
                PythonOps.PrintWithDestNoNewline(DefaultContext.Default, Sink, value);
            } catch (Exception e) {
                PythonOps.PrintWithDest(DefaultContext.Default, _context.SystemStandardOut, _context.FormatException(e));
            }
        }

        public override void Write(char value) {
            Write(value.ToString());
        }

        public override void Write(char[] value) {
            Write(new string(value));
        }

        public override void Flush() {
            // avoid creating a site in the common case
            PythonFile pf = Sink as PythonFile;
            if (pf != null) {
                pf.flush();
                return;
            }

            if (PythonOps.HasAttr(_context.SharedContext, Sink, "flush")) {
                PythonOps.Invoke(_context.SharedContext, Sink, "flush");
            }
        }
    }
}
