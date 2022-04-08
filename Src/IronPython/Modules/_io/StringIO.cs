// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using static IronPython.Modules.PythonIOModule.IncrementalNewlineDecoder;

namespace IronPython.Modules {
    public static partial class PythonIOModule {
        [PythonType]
        public class StringIO : _TextIOBase, IDynamicMetaObjectProvider {
            #region Fields and constructors

            private static readonly int DEFAULT_BUF_SIZE = 20;

            private char[] _data;
            private int _pos, _length;
            private string? _newline;
            private LineEnding _seenNL;

            internal StringIO(CodeContext context)
                : base(context) {
                _data = new char[DEFAULT_BUF_SIZE];
            }

            public StringIO(CodeContext context, [ParamDictionary, NotNull] IDictionary<object, object> kwArgs\u00F8, [NotNull] params object[] args)
                : this(context) { }

            public void __init__(CodeContext context, string? initial_value = "", string? newline = "\n") {
                switch (newline) {
                    case null:
                    case "":
                    case "\n":
                    case "\r":
                    case "\r\n":
                        break;
                    default:
                        throw PythonOps.ValueError("illegal newline value: {0}", PythonOps.Repr(context, newline));
                }
                _newline = newline;

                _pos = _length = 0;
                if (!string.IsNullOrEmpty(initial_value)) {
                    DoWrite(initial_value!);
                    _pos = 0;
                }
            }

            #endregion

            #region Public API

            public bool line_buffering {
                get {
                    _checkClosed();
                    return false;
                }
            }

            public string getvalue() {
                _checkClosed();

                if (_length == 0) {
                    return string.Empty;
                }

                return _data.AsSpan().Slice(0, _length).ToString();
            }

            [Documentation("Read at most size characters, returned as a string.\n\n"
                + "If the argument is negative or omitted, read until EOF\n"
                + "is reached. Return an empty string at EOF."
                )]
            public override object read(CodeContext context, object? size = null) {
                _checkClosed();
                int sz = GetInt(size, -1);

                int len = Math.Max(0, _length - _pos);
                if (sz >= 0) {
                    len = Math.Min(len, sz);
                }
                if (len == 0) {
                    return string.Empty;
                }

                var res = _data.AsSpan().Slice(_pos, len).ToString();
                _pos += len;
                return res;
            }

            public override bool readable(CodeContext context) {
                _checkClosed();
                return true;
            }

            public override object readline(CodeContext context, int limit = -1)
                => readline(limit);

            private string readline(int limit) {
                _checkClosed();
                var len = _length - _pos;
                if (limit >= 0) len = Math.Min(len, limit);
                if (len == 0) return string.Empty;

                var span = _data.AsSpan(_pos, len);
                if (_newline is null) {
                    var idx = span.IndexOf('\n');
                    if (idx != -1) {
                        span = span.Slice(0, idx + 1);
                    }
                } else if (_newline == string.Empty) {
                    var idx = span.IndexOfAny("\r\n".AsSpan());
                    if (idx != -1) {
                        if (span[idx++] == '\n') {
                            span = span.Slice(0, idx);
                        } else {
                            Debug.Assert(span[idx - 1] == '\r');
                            // ensure we don't split \r\n
                            if (idx < span.Length && span[idx] == '\n') {
                                span = span.Slice(0, idx + 1);
                            } else {
                                span = span.Slice(0, idx);
                            }
                        }
                    }
                } else {
                    var idx = span.IndexOf(_newline.AsSpan());
                    if (idx != -1) {
                        span = span.Slice(0, idx + _newline.Length);
                    }
                }

                var res = span.ToString();
                _pos += span.Length;
                return res;
            }

            [Documentation("Return a list of lines from the stream.\n\n"
                + "hint can be specified to control the number of lines read: no more\n"
                + "lines will be read if the total size (in bytes/characters) of all\n"
                + "lines so far exceeds hint."
                )]
            public override PythonList readlines(object? hint = null) {
                _checkClosed();
                int size = GetInt(hint, -1);

                PythonList lines = new PythonList();
                for (var line = readline(-1); line.Length > 0; line = readline(-1)) {
                    lines.append(line);
                    if (size > 0) {
                        size -= line.Length;
                        if (size <= 0) {
                            break;
                        }
                    }
                }

                return lines;
            }

            private BigInteger seek(int pos, int whence) {
                _checkClosed();

                switch (whence) {
                    case 0:
                        if (pos < 0) {
                            throw PythonOps.ValueError("Negative seek position {0}", pos);
                        }
                        _pos = pos;
                        return _pos;
                    case 1:
                        if (pos != 0) throw PythonOps.OSError("Can't do nonzero cur-relative seeks");
                        return _pos;
                    case 2:
                        if (pos != 0) throw PythonOps.OSError("Can't do nonzero cur-relative seeks");
                        _pos = _length;
                        return _pos;
                    default:
                        throw PythonOps.ValueError("Invalid whence ({0}, should be 0, 1 or 2)", whence);
                }
            }

            [Documentation("")]
            public BigInteger seek(double pos, [Optional] object? whence) => throw PythonOps.TypeError("integer argument expected, got float");

            [Documentation("Change stream position.\n\n"
                + "Seek to character offset pos relative to position indicated by whence:\n"
                + "     0  Start of stream (the default).  pos should be >= 0;\n"
                + "     1  Current position - pos must be 0;\n"
                + "     2  End of stream - pos must be 0.\n"
                + "Returns the new absolute position."
                )]
            public override BigInteger seek(CodeContext context, BigInteger pos, [Optional] object? whence) {
                _checkClosed();

                int posInt = (int)pos;
                switch (whence) {
                    case int v:
                        return seek(posInt, v);
                    case BigInteger v:
                        return seek(posInt, (int)v);
                    case Extensible<BigInteger> v:
                        return seek(posInt, (int)v.Value);
                    case double _:
                    case Extensible<double> _:
                        throw PythonOps.TypeError("integer argument expected, got float");
                    default:
                        return seek(posInt, GetInt(whence));
                }
            }

            public override bool seekable(CodeContext context) {
                _checkClosed();
                return true;
            }

            [Documentation("Tell the current file position.")]
            public override BigInteger tell(CodeContext context) {
                _checkClosed();
                return _pos;
            }

            [Documentation("Truncate size to pos.\n\n"
                + "The pos argument defaults to the current file position, as\n"
                + "returned by tell().  The current file position is unchanged.\n"
                + "Returns the new absolute position."
                )]
            public BigInteger truncate() {
                return truncate(_pos);
            }

            [Documentation("")]
            public BigInteger truncate(int size) {
                _checkClosed();
                if (size < 0) {
                    throw PythonOps.ValueError("negative size value {0}", size);
                }

                _length = Math.Min(_length, size);
                return (BigInteger)size;
            }

            [Documentation("")]
            public override BigInteger truncate(CodeContext context, object? size = null) {
                if (size == null) {
                    return truncate();
                }

                int sizeInt;
                if (TryGetInt(size, out sizeInt)) {
                    return truncate(sizeInt);
                }

                _checkClosed();

                throw PythonOps.TypeError("integer argument expected, got '{0}'", PythonOps.GetPythonTypeName(size));
            }

            public override bool writable(CodeContext context) {
                _checkClosed();
                return true;
            }

            [Documentation("Write string to file.\n\n"
                + "Returns the number of characters written, which is always equal to\n"
                + "the length of the string."
                )]
            public override BigInteger write(CodeContext context, object? str) {
                if (str is string s) {
                    return write(context, s);
                } else if (str is Extensible<string> es) {
                    return write(context, es.Value);
                } else {
                    throw PythonOps.TypeError("string argument expected, got '{0}'", PythonOps.GetPythonTypeName(str));
                }
            }

            // TODO: get rid of virtual? see https://github.com/IronLanguages/ironpython3/issues/1070
            [Documentation("")]
            public virtual BigInteger write(CodeContext context, [NotNull] string str) {
                _checkClosed();
                return DoWrite(str);
            }

            [Documentation("Write a list of lines to stream.\n\n"
                + "Line separators are not added, so it is usual for each of the\n"
                + "lines provided to have a line separator at the end."
                )]
            public void writelines(CodeContext context, [NotNull] IEnumerable lines) {
                _checkClosed();

                IEnumerator en = lines.GetEnumerator();
                while (en.MoveNext()) {
                    write(context, en.Current);
                }
            }

            public override object newlines => GetNewLines(_seenNL);

            #endregion

            #region Pickling

            public PythonTuple __getstate__(CodeContext context) {
                // TODO: don't initialize the __dict__ unless needed
                return PythonTuple.MakeTuple(getvalue(), _newline, tell(context), new PythonDictionary(__dict__));
            }

            public void __setstate__(CodeContext context, [NotNull] PythonTuple tuple) {
                _checkClosed();

                if (tuple.__len__() != 4) {
                    throw PythonOps.TypeError("_io.StringIO.__setstate__ argument should be 4-tuple, got tuple");
                }

                var newline = tuple[1] switch {
                    null => null,
                    string s => s,
                    Extensible<string> es => es.Value,
                    _ => throw PythonOps.TypeError($"newline must be str or None, not {0}", PythonOps.GetPythonTypeName(tuple[1])),
                };
                switch (newline) {
                    case null:
                    case "":
                    case "\n":
                    case "\r":
                    case "\r\n":
                        break;
                    default:
                        throw PythonOps.ValueError("illegal newline value: {0}", PythonOps.Repr(context, newline));
                }

                var initial_value = tuple[0] switch {
                    null => string.Empty,
                    string s => s,
                    Extensible<string> es => es.Value,
                    _ => throw PythonOps.TypeError($"initial_value must be str or None, not {PythonOps.GetPythonTypeName(tuple[0])}"),
                };

                _data = initial_value.AsSpan().ToArray();
                _length = _data.Length;
                _newline = newline;

                var pos = tuple[2] switch {
                    int i => i,
                    BigInteger bi => (int)bi,
                    _ => throw PythonOps.TypeError($"third item of state must be an integer, not {PythonOps.GetPythonTypeName(tuple[2])}"),
                };
                if (pos < 0) {
                    throw PythonOps.ValueError("position value cannot be negative");
                }
                _pos = pos;

                var dict = tuple[3] as PythonDictionary;
                if (!(tuple[3] is PythonDictionary || tuple[3] is null)) {
                    throw PythonOps.TypeError($"third item of state should be a dict, got a {PythonOps.GetPythonTypeName(tuple[3])}");
                }

                if (!(dict is null))
                    __dict__.update(context, dict);
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return new MetaExpandable<StringIO>(parameter, this);
            }

            #endregion

            #region Private implementation details

            private int DoWrite(string str) {
                if (str.Length == 0) {
                    return 0;
                }

                var span = str.AsSpan();

                if (_newline is null) {
                    while (true) {
                        var idx = span.IndexOfAny("\r\n".AsSpan());
                        if (idx == -1)
                            break;

                        EnsureSizeSetLength(_pos + idx + 1);
                        span.Slice(0, idx).CopyTo(_data.AsSpan(_pos, idx));
                        _pos += idx;
                        _data[_pos++] = '\n';
                        if (span[idx++] == '\r') {
                            if (idx < span.Length && span[idx] == '\n') {
                                idx++;
                                _seenNL |= LineEnding.CRLF;
                            } else {
                                _seenNL |= LineEnding.CR;
                            }
                        } else {
                            _seenNL |= LineEnding.LF;
                        }
                        span = span.Slice(idx);
                    }
                } else if (_newline == "" || _newline == "\n") {
                    // nothing to do
                } else {
                    while (true) {
                        var idx = span.IndexOf('\n');
                        if (idx == -1)
                            break;

                        EnsureSizeSetLength(_pos + idx + _newline.Length);
                        span.Slice(0, idx).CopyTo(_data.AsSpan(_pos, idx));
                        _pos += idx;
                        _newline.AsSpan().CopyTo(_data.AsSpan(_pos, _newline.Length));
                        _pos += _newline.Length;
                        span = span.Slice(idx + 1);
                    }
                }

                EnsureSizeSetLength(_pos + span.Length);
                span.CopyTo(_data.AsSpan(_pos, span.Length));
                _pos += span.Length;
                return str.Length;
            }

            private void EnsureSize(int size) {
                Debug.Assert(size > 0);

                if (_data.Length < size) {
                    size = size <= DEFAULT_BUF_SIZE ? DEFAULT_BUF_SIZE : Math.Max(size, _data.Length * 2);
                    Array.Resize(ref _data, size);
                }
            }

            private void EnsureSizeSetLength(int size) {
                Debug.Assert(size >= _pos);
                Debug.Assert(_length <= _data.Length);

                if (_data.Length < size) {
                    EnsureSize(size);
                    _length = size;
                    return;
                }

                _length = Math.Max(_length, size);
            }

            #endregion
        }
    }
}
