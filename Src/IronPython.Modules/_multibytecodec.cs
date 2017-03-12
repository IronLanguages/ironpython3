/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting;

#if FALSE
[assembly: PythonModule("_multibytecodec", typeof(IronPython.Modules._multibytecodec))]
namespace IronPython.Modules {
    public class _multibytecodec {
        [PythonType]
        public class MultibyteIncrementalDecoder {
            private readonly MultibyteCodec _codec;

            public MultibyteIncrementalDecoder(CodeContext context) {
                _codec = GetCodec(this, context);
            }

            public object decode(CodeContext/*!*/ context, string @string, string errors="strict") {
                return _codec.decode(context, @string, errors)[0];
            }

            public object errors {
                get {
                    throw new NotImplementedException();
                }

            }

            public void reset() {
            }
        }

        [PythonType]
        public class MultibyteIncrementalEncoder {
            private readonly MultibyteCodec _codec;

            public MultibyteIncrementalEncoder(CodeContext context) {
                _codec = GetCodec(this, context);
            }

            public PythonTuple encode(CodeContext/*!*/ context, string unicode, string errors="strict") {
                return _codec.encode(context, unicode, errors);
            }

            public object errors {
                get {
                    throw new NotImplementedException();
                }
            }

            public void reset() {
            }
        }

        [PythonType]
        public class MultibyteStreamReader {
            private readonly MultibyteCodec _codec;
            private readonly object _stream;
            private readonly string _errors;

            public MultibyteStreamReader(CodeContext context, object stream, string errors="strict") {
                _codec = GetCodec(this, context);
                _stream = stream;
                _errors = errors;
            }

            public string read(CodeContext context) {
                return (string)_codec.encode(context, (string)PythonOps.Invoke(context, _stream, "read"), _errors)[0];
            }

            public string read(CodeContext context, int size) {
                return (string)_codec.encode(context, (string)PythonOps.Invoke(context, _stream, "read", size), _errors)[0];
            }

            public string readline(CodeContext context) {
                return (string)_codec.encode(context, (string)PythonOps.Invoke(context, _stream, "readline"), _errors)[0];
            }

            public void readlines() {
            }

            public void reset() {
            }

            public object errors {
                get {
                    return _errors;
                }
            }
            
            public object stream {
                get {
                    return _stream;
                }
            }
        }

        [PythonType]
        public class MultibyteStreamWriter {
            private readonly MultibyteCodec _codec;
            private readonly object _stream;
            private readonly string _errors;

            public MultibyteStreamWriter(CodeContext context, object stream, string errors="strict") {
                _codec = GetCodec(this, context);
                _stream = stream;
                _errors = errors;
            }

            public void write(CodeContext context, string @string) {
                PythonOps.Invoke(context, _stream, "read", _codec.decode(context, @string, _errors)[0]);
            }

            public void writelines() {
            }

            public void reset() {
            }

            public object errors {
                get {
                    return _errors;
                }
            }

            public object stream {
                get {
                    return _stream;
                }
            }
        }

        private static MultibyteCodec GetCodec(object self, CodeContext context) {
            MultibyteCodec codec = PythonOps.GetBoundAttr(context, self, "codec") as MultibyteCodec;
            if (codec == null) {
                throw PythonOps.TypeError("codec is unexpected type");
            }

            return codec;
        }
    }

    [PythonType]
    public class MultibyteCodec {
        private readonly Encoding _encoding;
        private readonly string _encName;

        public MultibyteCodec(Encoding encoding, string encName) {
            _encoding = encoding;
            _encName = encName;
        }

        public PythonTuple encode(CodeContext/*!*/ context, string unicode, string errors="strict") {
            return PythonTuple.MakeTuple(
                StringOps.DoEncode(context, unicode, errors, _encName, _encoding),
                unicode.Length
            );
        }

        public PythonTuple decode(CodeContext/*!*/ context, string @string, string errors="strict") {
            return PythonTuple.MakeTuple(
                StringOps.DoDecode(context, @string, errors, _encName, _encoding),
                @string.Length
            );
        }
    }
}
#endif