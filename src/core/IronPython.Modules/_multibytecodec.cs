// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
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