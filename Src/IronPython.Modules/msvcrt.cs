// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_NATIVE

using Microsoft.Win32.SafeHandles;

using System;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

#if FEATURE_PIPES
using System.IO.Pipes;
#endif

[assembly: PythonModule("msvcrt", typeof(IronPython.Modules.PythonMsvcrt), PlatformsAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    [PythonType("msvcrt")]
    [SupportedOSPlatform("windows")]
    public class PythonMsvcrt {
        public const string __doc__ = "Functions from the Microsoft Visual C Runtime.";

        #region Public API

        public const int SEM_FAILCRITICALERRORS = 1;
        public const int SEM_NOGPFAULTERRORBOX = 2;
        public const int SEM_NOALIGNMENTFAULTEXCEPT = 4;
        public const int SEM_NOOPENFILEERRORBOX = 32768;

        public static void SetErrorMode(int mode) {
            // TODO: fill this up
        }

        [Documentation(@"heapmin() -> None

Force the malloc() heap to clean itself up and return unused blocks
to the operating system. On failure, this raises IOError.")]
        public static void heapmin() {
            if (_heapmin() != 0) {
                throw PythonOps.IOError(new Win32Exception());
            }
        }

        private const int O_TEXT = 0x4000;
        private const int O_BINARY = 0x8000;

        // python call: c2pread = msvcrt.open_osfhandle(c2pread.Detach(), 0)
        [Documentation(@"open_osfhandle(handle, flags) -> file descriptor

Create a C runtime file descriptor from the file handle handle. The
flags parameter should be a bitwise OR of os.O_APPEND, os.O_RDONLY,
and os.O_TEXT. The returned file descriptor may be used as a parameter
to os.fdopen() to create a file object.
")]

        public static int open_osfhandle(CodeContext context, BigInteger os_handle, int flags) {
            if ((flags & O_TEXT) != 0) throw new NotImplementedException();
            FileStream stream = new FileStream(new SafeFileHandle(new IntPtr((long)os_handle), true), FileAccess.ReadWrite);
            return context.LanguageContext.FileManager.Add(new(stream));
        }

        private static bool TryGetFileHandle(Stream stream, out object handle) {
            if (stream is FileStream) {
                handle = ((FileStream)stream).SafeFileHandle.DangerousGetHandle().ToPython();
                return true;
            }
#if FEATURE_PIPES
            if (stream is PipeStream) {
                handle = ((PipeStream)stream).SafePipeHandle.DangerousGetHandle().ToPython();
                return true;
            }
#endif

            // if all else fails try reflection
            var sfh = stream.GetType().GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(stream);
            if (sfh is SafeFileHandle) {
                handle = ((SafeFileHandle)sfh).DangerousGetHandle().ToPython();
                return true;
            }

            handle = null;
            return false;
        }

        [Documentation(@"get_osfhandle(fd) -> file handle

Return the file handle for the file descriptor fd. Raises IOError
if fd is not recognized.")]
        public static object get_osfhandle(CodeContext context, int fd) {
            var sbox = context.LanguageContext.FileManager.GetStreams(fd);

            object handle;
            if (TryGetFileHandle(sbox.ReadStream, out handle)) return handle;

            return -1;
        }

        [Documentation(@"setmode(fd, mode) -> Previous mode

Set the line-end translation mode for the file descriptor fd. To set
it to text mode, flags should be os.O_TEXT; for binary, it should be
os.O_BINARY.")]
        public static int setmode(CodeContext context, int fd, int flags) {
            if (flags != O_BINARY) throw new NotImplementedException();
            return O_BINARY;
        }

        [Documentation(@"kbhit() -> bool

Return true if a keypress is waiting to be read.")]
        public static bool kbhit() {
            return _kbhit() == 0 ? false : true;
        }

        [Documentation(@"getch() -> key character

Read a keypress and return the resulting character. Nothing is echoed to
the console. This call will block if a keypress is not already
available, but will not wait for Enter to be pressed. If the pressed key
was a special function key, this will return '\\000' or '\\xe0'; the next
call will return the keycode. The Control-C keypress cannot be read with
this function.")]
        public static string getch() {
            return new string((char)_getch(), 1);
        }

        [Documentation(@"getwch() -> Unicode key character

Wide char variant of getch(), returning a Unicode value.")]
        public static string getwch() {
            return new string((char)_getwch(), 1);
        }

        [Documentation(@"getche() -> key character

Similar to getch(), but the keypress will be echoed if it represents
a printable character.")]
        public static string getche() {
            return new string((char)_getche(), 1);
        }

        [Documentation(@"getwche() -> Unicode key character

Wide char variant of getche(), returning a Unicode value.")]
        public static string getwche() {
            return new string((char)_getwche(), 1);
        }

        [Documentation(@"putch(char) -> None

Print the character char to the console without buffering.")]
        public static void putch(char @char) {
            _putch(@char);
        }

        [Documentation(@"putwch(unicode_char) -> None

Wide char variant of putch(), accepting a Unicode value.")]
        public static void putwch(char @char) {
            _putwch(@char);
        }

        [Documentation(@"ungetch(char) -> None

Cause the character char to be ""pushed back"" into the console buffer;
it will be the next character read by getch() or getche().")]
        public static void ungetch(char @char) {
            if (_ungetch(@char) == EOF) {
                throw PythonOps.IOError(new Win32Exception());
            }
        }

        [Documentation(@"ungetwch(unicode_char) -> None

Wide char variant of ungetch(), accepting a Unicode value.")]
        public static void ungetwch(char @char) {
            if (_ungetwch(@char) == WEOF) {
                throw PythonOps.IOError(new Win32Exception());
            }
        }

        #endregion

        #region P/Invoke Declarations

        private static int EOF = -1;
        private static ushort WEOF = 0xFFFF;

        [DllImport("msvcr100", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _heapmin();

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern int _kbhit();

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern int _getch();

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern int _getche();

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern int _putch(int c);

        [DllImport("msvcr100", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _ungetch(int c);

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern ushort _getwch();

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern ushort _getwche();

        [DllImport("msvcr100", CallingConvention = CallingConvention.Cdecl)]
        private static extern ushort _putwch(char c);

        [DllImport("msvcr100", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern ushort _ungetwch(ushort c);

        #endregion
    }
}

#endif
