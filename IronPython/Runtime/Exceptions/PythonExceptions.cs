// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

#if !FEATURE_REMOTING
using MarshalByRefObject = System.Object;
#endif

namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// Implementation of the Python exceptions module and the IronPython/CLR exception mapping 
    /// mechanism.  The exception module is the parent module for all Python exception classes
    /// and therefore is built-in to IronPython.dll instead of IronPython.Modules.dll.
    /// 
    /// The exception mapping mechanism is exposed as internal surface area available to only
    /// IronPython / IronPython.Modules.dll.  The actual exceptions themselves are all public.
    /// 
    /// Because the oddity of the built-in exception types all sharing the same physical layout
    /// (see also PythonExceptions.BaseException) some classes are defined as classes w/ their
    /// proper name and some classes are defined as PythonType fields.  When a class is defined
    /// for convenience their's also an _TypeName version which is the PythonType.
    /// </summary>
    public static partial class PythonExceptions {
        private static readonly object _pythonExceptionKey = typeof(BaseException);
        internal const string DefaultExceptionModule = "exceptions";
        public const string __doc__ = "Provides the most commonly used exceptions for Python programs";

        #region Custom Exception Code

        public partial class _SyntaxError : BaseException {
            public override string ToString() {
                if (filename is string fn) {
                    if (lineno is int ln) {
                        return $"{PythonOps.ToString(msg)} ({fn}, line {ln})";
                    }
                    return $"{PythonOps.ToString(msg)} ({fn})";
                } else if (lineno is int ln) {
                    return $"{PythonOps.ToString(msg)} (line {ln})";
                }
                return PythonOps.ToString(msg);
            }

            public override void __init__(params object[] args) {
                base.__init__(args);

                if (args != null && args.Length != 0) {
                    msg = args[0];

                    // (msg, (filename, lineno, offset, text))
                    if (args.Length == 2) {
                        var locationInfo = PythonTuple.Make(args[1]);
                        if (locationInfo.__len__() != 4) {
                            throw PythonOps.IndexError("tuple index out of range");
                        }

                        filename = locationInfo[0];
                        lineno = locationInfo[1];
                        offset = locationInfo[2];
                        text = locationInfo[3];
                    }
                }
            }
        }

        public partial class _UnicodeTranslateError : BaseException {
            public override void __init__(params object[] args) {
                if (args.Length != 4) {
                    throw PythonOps.TypeError("function takes exactly 4 arguments ({0} given)", args.Length);
                }

                if (args[0] is string || args[0] is Extensible<string>) {
                    @object = args[0];
                } else {
                    throw PythonOps.TypeError("argument 4 must be unicode, not {0}", PythonOps.GetPythonTypeName(args[0]));
                }

                start = args[1];
                end = args[2];

                if (args[3] is string || args[3] is Extensible<string>) {
                    reason = args[3];
                } else {
                    throw PythonOps.TypeError("argument 4 must be str, not {0}", PythonOps.GetPythonTypeName(args[3]));
                }

                base.__init__(args);
            }

            public override string ToString() {
                string repr(int c) => c < 0x100 ? $"\\x{c:x2}" : $"\\u{c:x4}";

                if (@object is string s && start is int startIdx && end is int endIdx) {
                    if (0 <= startIdx && startIdx + 1 == endIdx && endIdx <= s.Length) {
                        return $"can't translate character '{repr(s[startIdx])}' in position {start}: {reason}";
                    } else {
                        return $"can't translate characters in position {startIdx}-{endIdx - 1}: {reason}";
                    }
                }
                return reason?.ToString() ?? GetType().Name;
            }
        }

        public partial class _OSError {
            public static new object __new__(PythonType cls, [ParamDictionary] IDictionary<string, object> kwArgs, params object[] args) {
                if (cls == OSError && args.Length >= 1 && args[0] is int errno) {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                        if (args.Length >= 4 && args[3] is int winerror) {
                            errno = WinErrorToErrno(winerror);
                        }
                    }
                    cls = ErrnoToPythonType(ErrnoToErrorEnum(errno));
                }
                return Activator.CreateInstance(cls.UnderlyingSystemType, cls);
            }

            private static readonly object Undefined = new object();

            private object _filename = Undefined;
            public object filename {
                get { return ReferenceEquals(_filename, Undefined) ? null : _filename; }
                set { _filename = value; }
            }

            // TODO: hide property on Unix
            private object _winerror = Undefined;
            public object winerror {
                get { return ReferenceEquals(_winerror, Undefined) ? null : _winerror; }
                set { _winerror = value; }
            }

            private object _filename2 = Undefined;
            public object filename2 {
                get { return ReferenceEquals(_filename2, Undefined) ? null : _filename2; }
                set { _filename2 = value; }
            }

            private object _characters_written = Undefined;
            public object characters_written {
                get { return ReferenceEquals(_characters_written, Undefined) ? throw PythonOps.AttributeError(nameof(characters_written)) : _characters_written; }
                set { _characters_written = PythonOps.Index(value); }
            }

            public override void __init__(params object[] args) {
                if (args.Length >= 2 && args.Length <= 5) {
                    errno = args[0];
                    strerror = args[1];
                    if (args.Length >= 3) {
                        filename = args[2] ?? Undefined;
                    }
                    if (args.Length >= 4) {
                        winerror = args[3] ?? Undefined;
                        if (winerror is int) {
                            errno = WinErrorToErrno((int)winerror);
                        }
                    }
                    if (args.Length >= 5) {
                        filename2 = args[4] ?? Undefined;
                    }
                    args = new object[] { errno, strerror };
                }
                base.__init__(args);
            }

            private enum Error {
                UNSPECIFIED = -1,
                EPERM = 1,
                ENOENT = 2,
                ESRCH = 3,
                EINTR = 4,
                ECHILD = 10,
                EAGAIN = 11, // 35 on OSX
                EACCES = 13,
                EEXIST = 17,
                ENOTDIR = 20,
                EISDIR = 21,
                EPIPE = 32,
                // Linux
                ECONNABORTED = 103, // 53 on OSX
                ECONNRESET = 104, // 54 on OSX
                ESHUTDOWN = 108, // 58 on OSX
                ETIMEDOUT = 110, // 60 on OSX
                ECONNREFUSED = 111, // 61 on OSX
                EALREADY = 114, // 37 on OSX
                EINPROGRESS = 115, // 36 on OSX
                // Windows
                WSAEWOULDBLOCK = 10035,
                WSAEINPROGRESS = 10036,
                WSAEALREADY = 10037,
                WSAECONNABORTED = 10053,
                WSAECONNRESET = 10054,
                WSAESHUTDOWN = 10058,
                WSAETIMEDOUT = 10060,
                WSAECONNREFUSED = 10061,
            }

            private static Error ErrnoToErrorEnum(int errno) {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    if (errno == 11) return Error.UNSPECIFIED; // EAGAIN on Linux/Windows but EDEADLK on OSX, which is not being remapped
                    if (errno >= 35) errno += 10000; // add WSABASEERR to map to Windows error range
                }
                return (Error)errno;
            }

            private static PythonType ErrnoToPythonType(Error errno) {
                var res = errno switch {
                    Error.EPERM => PermissionError,
                    Error.ENOENT => FileNotFoundError,
                    Error.ESRCH => ProcessLookupError,
                    Error.EINTR => InterruptedError,
                    Error.ECHILD => ChildProcessError,
                    Error.EAGAIN => BlockingIOError,
                    Error.EACCES => PermissionError,
                    Error.EEXIST => FileExistsError,
                    Error.ENOTDIR => NotADirectoryError,
                    Error.EISDIR => IsADirectoryError,
                    Error.EPIPE => BrokenPipeError,
                    _ => null
                };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    res ??= errno switch {
                        // Windows or remapped OSX
                        Error.WSAEWOULDBLOCK => BlockingIOError,
                        Error.WSAEINPROGRESS => BlockingIOError,
                        Error.WSAEALREADY => BlockingIOError,
                        Error.WSAECONNABORTED => ConnectionAbortedError,
                        Error.WSAECONNRESET => ConnectionResetError,
                        Error.WSAESHUTDOWN => BrokenPipeError,
                        Error.WSAETIMEDOUT => TimeoutError,
                        Error.WSAECONNREFUSED => ConnectionRefusedError,
                        _ => null
                    };
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    res ??= errno switch {
                        // Linux
                        Error.ECONNABORTED => ConnectionAbortedError,
                        Error.ECONNRESET => ConnectionResetError,
                        Error.ESHUTDOWN => BrokenPipeError,
                        Error.ETIMEDOUT => TimeoutError,
                        Error.ECONNREFUSED => ConnectionRefusedError,
                        Error.EALREADY => BlockingIOError,
                        Error.EINPROGRESS => BlockingIOError,
                        _ => null
                    };
                }
                return res ?? OSError;
            }

            /*
             * errors were generated using this script run against CPython:
f = open(r'C:\Program Files\Microsoft SDKs\Windows\v6.0A\Include\WinError.h', 'r')
allErrors = []
toError = {}
for x in f:
    if x.startswith('#define ERROR_'):
        name = x[8:]
        endName = name.find(' ')
        justName = name[:endName]
        error = name[endName + 1:].strip()
        for i in range(len(error)):
            if error[i] < '0' or error[i] > '9':
                error = error[:i]
                break
            
        if not error:
            continue

        errNo = OSError(0, justName, None, int(error)).errno
        if errNo == 22 or errNo >= 10000:
            continue
        allErrors.append((justName, error))
        
        l = toError.get(errNo) 
        if l is None:
            toError[errNo] = l = []
        l.append(justName)
        
for name, err in allErrors:
    print('internal const int %s = %s;' % (name, err))

for k, v in toError.items():
    for name in v:
        print('case %s:' % name)
    print('    errno = %d;' % k)
    print('    break;')
             */

            internal const int ERROR_FILE_NOT_FOUND = 2;
            internal const int ERROR_PATH_NOT_FOUND = 3;
            internal const int ERROR_TOO_MANY_OPEN_FILES = 4;
            internal const int ERROR_ACCESS_DENIED = 5;
            internal const int ERROR_INVALID_HANDLE = 6;
            internal const int ERROR_ARENA_TRASHED = 7;
            internal const int ERROR_NOT_ENOUGH_MEMORY = 8;
            internal const int ERROR_INVALID_BLOCK = 9;
            internal const int ERROR_BAD_ENVIRONMENT = 10;
            internal const int ERROR_BAD_FORMAT = 11;
            internal const int ERROR_INVALID_DRIVE = 15;
            internal const int ERROR_CURRENT_DIRECTORY = 16;
            internal const int ERROR_NOT_SAME_DEVICE = 17;
            internal const int ERROR_NO_MORE_FILES = 18;
            internal const int ERROR_WRITE_PROTECT = 19;
            internal const int ERROR_BAD_UNIT = 20;
            internal const int ERROR_NOT_READY = 21;
            internal const int ERROR_BAD_COMMAND = 22;
            internal const int ERROR_CRC = 23;
            internal const int ERROR_BAD_LENGTH = 24;
            internal const int ERROR_SEEK = 25;
            internal const int ERROR_NOT_DOS_DISK = 26;
            internal const int ERROR_SECTOR_NOT_FOUND = 27;
            internal const int ERROR_OUT_OF_PAPER = 28;
            internal const int ERROR_WRITE_FAULT = 29;
            internal const int ERROR_READ_FAULT = 30;
            internal const int ERROR_GEN_FAILURE = 31;
            internal const int ERROR_SHARING_VIOLATION = 32;
            internal const int ERROR_LOCK_VIOLATION = 33;
            internal const int ERROR_WRONG_DISK = 34;
            internal const int ERROR_SHARING_BUFFER_EXCEEDED = 36;
            internal const int ERROR_BAD_NETPATH = 53;
            internal const int ERROR_NETWORK_ACCESS_DENIED = 65;
            internal const int ERROR_BAD_NET_NAME = 67;
            internal const int ERROR_FILE_EXISTS = 80;
            internal const int ERROR_CANNOT_MAKE = 82;
            internal const int ERROR_FAIL_I24 = 83;
            internal const int ERROR_NO_PROC_SLOTS = 89;
            internal const int ERROR_DRIVE_LOCKED = 108;
            internal const int ERROR_BROKEN_PIPE = 109;
            internal const int ERROR_DISK_FULL = 112;
            internal const int ERROR_INVALID_TARGET_HANDLE = 114;
            internal const int ERROR_WAIT_NO_CHILDREN = 128;
            internal const int ERROR_CHILD_NOT_COMPLETE = 129;
            internal const int ERROR_DIRECT_ACCESS_HANDLE = 130;
            internal const int ERROR_SEEK_ON_DEVICE = 132;
            internal const int ERROR_DIR_NOT_EMPTY = 145;
            internal const int ERROR_NOT_LOCKED = 158;
            internal const int ERROR_BAD_PATHNAME = 161;
            internal const int ERROR_MAX_THRDS_REACHED = 164;
            internal const int ERROR_LOCK_FAILED = 167;
            internal const int ERROR_ALREADY_EXISTS = 183;
            internal const int ERROR_INVALID_STARTING_CODESEG = 188;
            internal const int ERROR_INVALID_STACKSEG = 189;
            internal const int ERROR_INVALID_MODULETYPE = 190;
            internal const int ERROR_INVALID_EXE_SIGNATURE = 191;
            internal const int ERROR_EXE_MARKED_INVALID = 192;
            internal const int ERROR_BAD_EXE_FORMAT = 193;
            internal const int ERROR_ITERATED_DATA_EXCEEDS_64k = 194;
            internal const int ERROR_INVALID_MINALLOCSIZE = 195;
            internal const int ERROR_DYNLINK_FROM_INVALID_RING = 196;
            internal const int ERROR_IOPL_NOT_ENABLED = 197;
            internal const int ERROR_INVALID_SEGDPL = 198;
            internal const int ERROR_AUTODATASEG_EXCEEDS_64k = 199;
            internal const int ERROR_RING2SEG_MUST_BE_MOVABLE = 200;
            internal const int ERROR_RELOC_CHAIN_XEEDS_SEGLIM = 201;
            internal const int ERROR_INFLOOP_IN_RELOC_CHAIN = 202;
            internal const int ERROR_FILENAME_EXCED_RANGE = 206;
            internal const int ERROR_NESTING_NOT_ALLOWED = 215;
            internal const int ERROR_NO_DATA = 232;
            internal const int ERROR_DIRECTORY = 267;
            internal const int ERROR_NOT_ENOUGH_QUOTA = 1816;

            // These map to POSIX errno 22 and are added by hand as needed.
            internal const int ERROR_INVALID_PARAMETER = 87;
            internal const int ERROR_INVALID_NAME = 123;
            internal const int ERROR_FILE_INVALID = 1006;
            internal const int ERROR_MAPPED_ALIGNMENT = 1132;

            internal static int WinErrorToErrno(int winerror) {
                int errno = winerror;
                if (winerror < 10000) {
                    switch (winerror) {
                        case ERROR_BROKEN_PIPE:
                        case ERROR_NO_DATA:
                            errno = 32;
                            break;
                        case ERROR_FILE_NOT_FOUND:
                        case ERROR_PATH_NOT_FOUND:
                        case ERROR_INVALID_DRIVE:
                        case ERROR_NO_MORE_FILES:
                        case ERROR_BAD_NETPATH:
                        case ERROR_BAD_NET_NAME:
                        case ERROR_BAD_PATHNAME:
                        case ERROR_FILENAME_EXCED_RANGE:
                            errno = 2;
                            break;
                        case ERROR_BAD_ENVIRONMENT:
                            errno = 7;
                            break;
                        case ERROR_BAD_FORMAT:
                        case ERROR_INVALID_STARTING_CODESEG:
                        case ERROR_INVALID_STACKSEG:
                        case ERROR_INVALID_MODULETYPE:
                        case ERROR_INVALID_EXE_SIGNATURE:
                        case ERROR_EXE_MARKED_INVALID:
                        case ERROR_BAD_EXE_FORMAT:
                        case ERROR_ITERATED_DATA_EXCEEDS_64k:
                        case ERROR_INVALID_MINALLOCSIZE:
                        case ERROR_DYNLINK_FROM_INVALID_RING:
                        case ERROR_IOPL_NOT_ENABLED:
                        case ERROR_INVALID_SEGDPL:
                        case ERROR_AUTODATASEG_EXCEEDS_64k:
                        case ERROR_RING2SEG_MUST_BE_MOVABLE:
                        case ERROR_RELOC_CHAIN_XEEDS_SEGLIM:
                        case ERROR_INFLOOP_IN_RELOC_CHAIN:
                            errno = 8;
                            break;
                        case ERROR_INVALID_HANDLE:
                        case ERROR_INVALID_TARGET_HANDLE:
                        case ERROR_DIRECT_ACCESS_HANDLE:
                            errno = 9;
                            break;
                        case ERROR_WAIT_NO_CHILDREN:
                        case ERROR_CHILD_NOT_COMPLETE:
                            errno = 10;
                            break;
                        case ERROR_NO_PROC_SLOTS:
                        case ERROR_MAX_THRDS_REACHED:
                        case ERROR_NESTING_NOT_ALLOWED:
                            errno = 11;
                            break;
                        case ERROR_ARENA_TRASHED:
                        case ERROR_NOT_ENOUGH_MEMORY:
                        case ERROR_INVALID_BLOCK:
                        case ERROR_NOT_ENOUGH_QUOTA:
                            errno = 12;
                            break;
                        case ERROR_ACCESS_DENIED:
                        case ERROR_CURRENT_DIRECTORY:
                        case ERROR_WRITE_PROTECT:
                        case ERROR_BAD_UNIT:
                        case ERROR_NOT_READY:
                        case ERROR_BAD_COMMAND:
                        case ERROR_CRC:
                        case ERROR_BAD_LENGTH:
                        case ERROR_SEEK:
                        case ERROR_NOT_DOS_DISK:
                        case ERROR_SECTOR_NOT_FOUND:
                        case ERROR_OUT_OF_PAPER:
                        case ERROR_WRITE_FAULT:
                        case ERROR_READ_FAULT:
                        case ERROR_GEN_FAILURE:
                        case ERROR_SHARING_VIOLATION:
                        case ERROR_LOCK_VIOLATION:
                        case ERROR_WRONG_DISK:
                        case ERROR_SHARING_BUFFER_EXCEEDED:
                        case ERROR_NETWORK_ACCESS_DENIED:
                        case ERROR_CANNOT_MAKE:
                        case ERROR_FAIL_I24:
                        case ERROR_DRIVE_LOCKED:
                        case ERROR_SEEK_ON_DEVICE:
                        case ERROR_NOT_LOCKED:
                        case ERROR_LOCK_FAILED:
                            errno = 13;
                            break;
                        case ERROR_FILE_EXISTS:
                        case ERROR_ALREADY_EXISTS:
                            errno = 17;
                            break;
                        case ERROR_NOT_SAME_DEVICE:
                            errno = 18;
                            break;
                        case ERROR_DIRECTORY:
                            errno = 20;
                            break;
                        case ERROR_DIR_NOT_EMPTY:
                            errno = 41;
                            break;
                        case ERROR_TOO_MANY_OPEN_FILES:
                            errno = 24;
                            break;
                        case ERROR_DISK_FULL:
                            errno = 28;
                            break;
                        default:
                            errno = 22;
                            break;
                    }
                }
                return errno;
            }

            public string/*!*/ __str__(CodeContext/*!*/ context) {
                if (errno != null && strerror != null) {
                    var sb = new StringBuilder();
                    sb.Append("[");
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !ReferenceEquals(_winerror, Undefined)) {
                        sb.Append("WinError ");
                        sb.Append(PythonOps.Repr(context, winerror));
                    } else {
                        sb.Append("Errno ");
                        sb.Append(PythonOps.Repr(context, errno));
                    }
                    sb.Append("] ");
                    sb.Append(strerror);
                    if (!ReferenceEquals(_filename, Undefined)) {
                        sb.Append(": ");
                        sb.Append(PythonOps.Repr(context, filename));
                        if (!ReferenceEquals(_filename2, Undefined)) {
                            sb.Append(" -> ");
                            sb.Append(PythonOps.Repr(context, filename2));
                        }
                    }
                    return sb.ToString();
                }
                return base.ToString();
            }
        }

        public partial class _ImportError {
            public void __init__([ParamDictionary] IDictionary<string, object> kwargs, params object[] args) {
                base.__init__(args);

                foreach (var pair in kwargs) {
                    switch (pair.Key) {
                        case "name":
                            name = pair.Value;
                            break;
                        case "path":
                            path = pair.Value;
                            break;
                        default:
                            throw PythonOps.TypeError($"'{pair.Key}' is an invalid keyword argument for this function");
                    }
                }
            }
        }

        public partial class _UnicodeDecodeError : BaseException {
            [PythonHidden]
            protected internal override void InitializeFromClr(System.Exception/*!*/ exception) {
                if (exception is DecoderFallbackException ex) {
                    object inputData = ex.BytesUnknown;
                    int startIdx = 0;
                    int endIdx = ex.BytesUnknown?.Length ?? 0;
                    if (ex.Data.Contains("object") && ex.Data["object"] is Bytes b) {
                        inputData = b;
                        startIdx = ex.Index;
                        endIdx += startIdx;
                    }
                    __init__(ex.Data.Contains("encoding") ? ex.Data["encoding"] : "unknown", inputData, startIdx, endIdx, ex.Message);
                } else {
                    base.InitializeFromClr(exception);
                }
            }

            public override string ToString() {
                if (@object is IList<byte> s && start is int startIdx && end is int endIdx) {
                    if (0 <= startIdx && startIdx < endIdx && endIdx <= s.Count) {
                        int numBytes = endIdx - startIdx;
                        if (numBytes == 1) {
                            return $"'{encoding}' codec can't decode byte 0x{s[startIdx]:x2} in position {startIdx}: {reason}";
                        } else {
                            // Create a string representation of our bytes.
                            const int maxNumBytes = 20;

                            StringBuilder strBytes = new StringBuilder(3 + Math.Min(numBytes, maxNumBytes + 1) * 4);

                            strBytes.Append("b'");
                            int i;
                            for (i = startIdx; i < endIdx && i < s.Count && i < maxNumBytes; i++) {
                                strBytes.Append("\\x");
                                strBytes.Append(s[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                            }
                            strBytes.Append("'");

                            // In case the string's really long...
                            if (i < endIdx) strBytes.Append("...");

                            return $"'{encoding}' codec can't decode bytes {strBytes} in position {startIdx}-{endIdx - 1}: {reason}";
                        }
                    } else {
                        return $"'{encoding}' codec can't decode bytes in position {startIdx}-{endIdx - 1}: {reason}";
                    }
                }
                return reason?.ToString() ?? GetType().Name;
            }
        }

        public partial class _UnicodeEncodeError : BaseException {
            [PythonHidden]
            protected internal override void InitializeFromClr(System.Exception/*!*/ exception) {
                if (exception is EncoderFallbackException ex) {
                    object inputString = null;
                    int startIdx = 0;
                    int endIdx = 1;
                    if (ex.Data.Contains("object") && ex.Data["object"] is string s) {
                        startIdx += ex.Index;
                        endIdx += ex.Index;
                        inputString = s;
                    }
                    if (ex.CharUnknownHigh != default(char) || ex.CharUnknownLow != default(char)) {
                        endIdx++;
                        if (inputString == null) inputString = new string(new[] { ex.CharUnknownHigh, ex.CharUnknownLow });
                    } else if (ex.CharUnknown != default(char)) {
                        if (inputString == null) inputString = new string(ex.CharUnknown, 1);
                    }

                    if (inputString == null) startIdx = endIdx = 0;  // no data

                    __init__((ex.Data.Contains("encoding")) ? ex.Data["encoding"] : "unknown", inputString, startIdx, endIdx, ex.Message);
                } else {
                    base.InitializeFromClr(exception);
                }
            }

            public override string ToString() {
                string repr(int c) => c < 0x100 ? $"\\x{c:x2}" : $"\\u{c:x4}";

                if (@object is string s && start is int startIdx && end is int endIdx) {
                    if (0 <= startIdx && endIdx <= s.Length) {
                        switch (endIdx - startIdx) {
                            case 1: return $"'{encoding}' codec can't encode character '{repr(s[startIdx])}' in position {start}: {reason}";
                            case 2: return $"'{encoding}' codec can't encode character pair '{repr(s[startIdx])}{repr(s[startIdx + 1])}' in position {start}: {reason}";
                        }
                    }
                    return $"'{encoding}' codec can't encode characters in position {startIdx}-{endIdx - 1}: {reason}";
                }
                return reason?.ToString() ?? GetType().Name;
            }
        }

        public partial class _SystemExit : BaseException {
            public override void __init__([NotNone] params object[] args) {
                base.__init__(args);

                if (args?.Length > 0) {
                    code = args.Length == 1 ? args[0] : base.args;
                }
            }
        }

        public partial class _StopIteration : BaseException {
            public override void __init__(params object[] args) {
                base.__init__(args);

                if (args?.Length > 0) {
                    value = args[0];
                }
            }
        }

        public partial class _BlockingIOError {
            public override void __init__(params object[] args) {
                if (args.Length >= 3) {
                    if (PythonOps.TryToIndex(args[2], out object index)) // this is the behavior since CPython 3.8
                        characters_written = index;
                }
                base.__init__(args);
            }
        }

        #endregion

        #region Exception translation

        internal static System.Exception CreateThrowable(PythonType type, params object[] args) {
            BaseException be = CreatePythonThrowable(type, args);

            return be.GetClrException();
        }

        internal static BaseException CreatePythonThrowable(PythonType type, params object[] args) {
            BaseException be;
            if (type.UnderlyingSystemType == typeof(BaseException)) {
                be = new BaseException(type);
            } else if (type == OSError) {
                be = (BaseException)_OSError.__new__(type, null, args);
            } else {
                be = (BaseException)Activator.CreateInstance(type.UnderlyingSystemType, type);
            }
            be.__init__(args);
            return be;
        }

#nullable enable

        internal static BaseException CreateBaseExceptionForRaise(CodeContext/*!*/ context, PythonType/*!*/ type, object? value) {
            object? pyEx;

            if (PythonOps.IsInstance(value, type)) {
                pyEx = value;
            } else if (value is PythonTuple) {
                pyEx = PythonOps.CallWithArgsTuple(type, ArrayUtils.EmptyObjects, value);
            } else if (value != null) {
                pyEx = PythonCalls.Call(context, type, value);
            } else {
                pyEx = PythonCalls.Call(context, type);
            }

            if (pyEx is BaseException be) return be;

            throw PythonOps.TypeError($"calling {PythonOps.ToString(type)} should have returned an instance of BaseException, not {PythonOps.ToString(DynamicHelpers.GetPythonType(pyEx))}");
        }

        /// <summary>
        /// Returns the CLR exception associated with a Python exception
        /// creating a new exception if necessary
        /// </summary>
        internal static Exception? ToClr(object pythonException) {
            return pythonException as Exception ?? (pythonException as BaseException)?.GetClrException();
        }

        /// <summary>
        /// Given a CLR exception returns the Python exception which most closely maps to the CLR exception.
        /// </summary>
        public static object ToPython(System.Exception/*!*/ clrException) {
            var res = clrException.GetPythonException();
            if (res is null) {
                // explicit extra conversions that need a special transformation
                if (clrException is SyntaxErrorException syntax) {
                    return SyntaxErrorToPython(syntax);
                }

#if FEATURE_EXCEPTION_STATE
                if (clrException is ThreadAbortException ta) {
                    // transform TA w/ our reason into a KeyboardInterrupt exception.
                    if (ta.ExceptionState is KeyboardInterruptException reason) {
                        ta.Data[typeof(KeyboardInterruptException)] = reason;
                        return ToPython(reason);
                    }

                    // check for cleared but saved reason...
                    if (ta.Data[typeof(KeyboardInterruptException)] is KeyboardInterruptException reasonFromData) {
                        return ToPython(reasonFromData);
                    }
                }
#endif

                res = ToPythonNewStyle(clrException);
                clrException.SetPythonException(res);
            }

            return res;
        }

        /// <summary>
        /// Creates a new style Python exception from the .NET exception
        /// </summary>
        private static BaseException/*!*/ ToPythonNewStyle(System.Exception/*!*/ clrException) {
            // EndOfStreamException is not part of the OSError hierarchy
            if (clrException is Win32Exception || clrException is IOException && clrException is not EndOfStreamException) {
                int errorCode = clrException.HResult;

                if ((errorCode & ~0xfff) == unchecked((int)0x80070000)) {
                    errorCode = _OSError.WinErrorToErrno(errorCode & 0xfff);
                }

                // TODO: can we get filename and such?
                return CreatePythonThrowable(OSError, errorCode, clrException.Message);
            }

            BaseException pyExcep;
            if (clrException is InvalidCastException || clrException is ArgumentNullException) {
                // explicit extra conversions outside the generated hierarchy
                pyExcep = new BaseException(TypeError);
            } else {
                // conversions from generated code (in the generated hierarchy)...
                pyExcep = ToPythonHelper(clrException);
            }

            pyExcep.InitializeFromClr(clrException);

            return pyExcep;
        }

        [Serializable]
        private class ExceptionDataWrapper : MarshalByRefObject {
            public ExceptionDataWrapper(BaseException value) {
                Value = value;
            }

            public BaseException Value { get; }
        }

        /// <summary>
        /// Internal helper to associate a .NET exception and a Python exception.
        /// </summary>
        private static void SetPythonException(this Exception e, BaseException exception) {
            if (e is IPythonAwareException pyAware) {
                pyAware.PythonException = exception;
            } else {
                e.Data[_pythonExceptionKey] = new ExceptionDataWrapper(exception);
            }

            exception.clsException = e;
        }

        /// <summary>
        /// Internal helper to get the associated Python exception from a .NET exception.
        /// </summary>
        internal static BaseException? GetPythonException(this Exception e) {
            if (e is IPythonAwareException pyAware) {
                return pyAware.PythonException;
            }

            if (e.Data[_pythonExceptionKey] is ExceptionDataWrapper wrapper) {
                return wrapper.Value;
            }

            return null;
        }

#nullable restore

        internal static List<DynamicStackFrame> GetFrameList(this Exception e) {
            if (e is IPythonAwareException pyAware) {
                return pyAware.Frames;
            } else {
                return e.Data[typeof(DynamicStackFrame)] as List<DynamicStackFrame>;
            }
        }

        internal static void SetFrameList(this Exception e, List<DynamicStackFrame> frames) {
            if (e is IPythonAwareException pyAware) {
                pyAware.Frames = frames;
            } else {
                e.Data[typeof(DynamicStackFrame)] = frames;
            }
        }

        internal static void RemoveFrameList(this Exception e) {
            if (e is IPythonAwareException pyAware) {
                pyAware.Frames = null;
            } else {
                e.Data.Remove(typeof(DynamicStackFrame));
            }
        }

        internal static TraceBack GetTraceBack(this Exception e) {
            if (e is IPythonAwareException pyAware) {
                return pyAware.TraceBack;
            } else {
                return e.Data[typeof(TraceBack)] as TraceBack;
            }
        }

        internal static void SetTraceBack(this Exception e, TraceBack traceback) {
            if (e is IPythonAwareException pyAware) {
                pyAware.TraceBack = traceback;
            } else {
                e.Data[typeof(TraceBack)] = traceback;
            }
        }

        internal static void RemoveTraceBack(this Exception e) {
            if (e is IPythonAwareException pyAware) {
                pyAware.TraceBack = null;
            } else {
                e.Data.Remove(typeof(TraceBack));
            }
        }

        /// <summary>
        /// Converts the DLR SyntaxErrorException into a Python new-style SyntaxError instance.
        /// </summary>
        private static BaseException/*!*/ SyntaxErrorToPython(SyntaxErrorException/*!*/ e) {
            PythonExceptions._SyntaxError se;
            if (e.GetType() == typeof(IndentationException)) {
                se = new _SyntaxError(IndentationError);
            } else if (e.GetType() == typeof(TabException)) {
                se = new _SyntaxError(TabError);
            } else {
                se = new _SyntaxError();
            }

            string sourceLine = PythonContext.GetSourceLine(e);
            string fileName = e.GetSymbolDocumentName();
            object column = (e.Column == 0 || e.Data[PythonContext._syntaxErrorNoCaret] != null) ? null : (object)e.Column;

            se.args = PythonTuple.MakeTuple(e.Message, PythonTuple.MakeTuple(fileName, e.Line, column, sourceLine));

            se.filename = fileName;
            se.lineno = e.Line;
            se.offset = column;
            se.text = sourceLine;
            se.msg = e.Message;

            e.SetPythonException(se);

            return se;
        }

        /// <summary>
        /// Creates a PythonType for a built-in module.  These types are mutable like
        /// normal user types.
        /// </summary>
        [PythonHidden]
        public static PythonType CreateSubType(PythonContext/*!*/ context, PythonType baseType, string name, string module, string documentation, Func<string, Exception, Exception> exceptionMaker) {
            PythonType res = new PythonType(context, baseType, name, module, documentation, exceptionMaker);
            res.SetCustomMember(context.SharedContext, "__weakref__", new PythonTypeWeakRefSlot(res));
            res.IsWeakReferencable = true;
            return res;
        }

        /// <summary>
        /// Creates a PythonType for a built-in module.  These types are mutable like
        /// normal user types.
        /// </summary>
        [PythonHidden]
        public static PythonType CreateSubType(PythonContext/*!*/ context, PythonType baseType, Type underlyingType, string name, string module, string documentation, Func<string, Exception, Exception> exceptionMaker) {
            PythonType res = new PythonType(context, new PythonType[] { baseType }, underlyingType, name, module, documentation, exceptionMaker);
            res.SetCustomMember(context.SharedContext, "__weakref__", new PythonTypeWeakRefSlot(res));
            res.IsWeakReferencable = true;
            return res;
        }

        /// <summary>
        /// Creates a PythonType for a built-in module, where the type may inherit
        /// from multiple bases.  These types are mutable like normal user types. 
        /// </summary>
        [PythonHidden]
        public static PythonType CreateSubType(PythonContext/*!*/ context, PythonType[] baseTypes, Type underlyingType, string name, string module, string documentation, Func<string, Exception, Exception> exceptionMaker) {
            PythonType res = new PythonType(context, baseTypes, underlyingType, name, module, documentation, exceptionMaker);
            res.SetCustomMember(context.SharedContext, "__weakref__", new PythonTypeWeakRefSlot(res));
            res.IsWeakReferencable = true;
            return res;
        }


        /// <summary>
        /// Creates a new type for a built-in exception which derives from another Python
        /// type.  .  These types are built-in and immutable like any other normal type.  For 
        /// example StandardError.x = 3 is illegal.  This isn't for module exceptions which 
        /// are like user defined types.  thread.error.x = 3 is legal.
        /// </summary>
        private static PythonType CreateSubType(PythonType baseType, string name, Func<string, Exception, Exception> exceptionMaker) {
            return new PythonType(baseType, name, exceptionMaker);
        }

        /// <summary>
        /// Creates a new type for a built-in exception which is the root concrete type.  
        /// </summary>
        private static PythonType/*!*/ CreateSubType(PythonType/*!*/ baseType, Type/*!*/ concreteType, Func<string, Exception, Exception> exceptionMaker) {
            Assert.NotNull(baseType, concreteType);

            PythonType myType = DynamicHelpers.GetPythonTypeFromType(concreteType);

            myType.ResolutionOrder = Mro.Calculate(myType, new PythonType[] { baseType });
            myType.BaseTypes = new PythonType[] { baseType };
            myType.HasDictionary = true;
            myType._makeException = exceptionMaker;

            return myType;
        }

        #endregion

        #region .NET/Python Exception Merging/Tracking

        /// <summary>
        /// Gets the list of DynamicStackFrames for the current exception.
        /// </summary>
        internal static DynamicStackFrame[] GetDynamicStackFrames(Exception e) {
            List<DynamicStackFrame> frames = e.GetFrameList();

            if (frames == null) {
                return Array.Empty<DynamicStackFrame>();
            }

            frames = new List<DynamicStackFrame>(frames);
            List<DynamicStackFrame> identified = new List<DynamicStackFrame>();

            // merge .NET frames w/ any dynamic frames that we have
            try {
                StackTrace outermostTrace = new StackTrace(e, false);
                IList<StackTrace> otherTraces = ExceptionHelpers.GetExceptionStackTraces(e) ?? new List<StackTrace>();
                List<StackFrame> clrFrames = new List<StackFrame>();

                foreach (StackTrace trace in otherTraces) {
                    clrFrames.AddRange(trace.GetFrames() ?? Array.Empty<StackFrame>()); // rare, sometimes GetFrames returns null
                }
                clrFrames.AddRange(outermostTrace.GetFrames() ?? Array.Empty<StackFrame>());    // rare, sometimes GetFrames returns null

                int lastFound = 0;
                foreach (StackFrame clrFrame in InterpretedFrame.GroupStackFrames(clrFrames)) {
                    MethodBase method = clrFrame.GetMethod();

                    for (int j = lastFound; j < frames.Count; j++) {
                        MethodBase other = frames[j].GetMethod();
                        // method info's don't always compare equal, check based
                        // upon name/module/declaring type which will always be a correct
                        // check for dynamic methods.
                        if (MethodsMatch(method, other)) {
                            identified.Add(frames[j]);
                            frames.RemoveAt(j);
                            lastFound = j;
                            break;
                        }
                    }
                }
            } catch (MemberAccessException) {
                // can't access new StackTrace(e) due to security
            }

            // combine identified and any remaining frames we couldn't find
            // this is equivalent of adding remaining frames in front of identified
            frames.AddRange(identified);
            return frames.ToArray();
        }

        private static bool MethodsMatch(MethodBase method, MethodBase other) {
            return (method.Module == other.Module &&
                    method.DeclaringType == other.DeclaringType &&
                    method.Name == other.Name);
        }

        #endregion
    }
}
