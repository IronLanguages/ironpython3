/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***********************************************************************/

#if FEATURE_NATIVE

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("msvcrt", typeof(IronPython.Modules.PythonMsvcrt), PythonModuleAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    [PythonType("msvcrt")]
    public class PythonMsvcrt {
        public const string __doc__ = "Functions from the Microsoft Visual C Runtime.";

        #region Public API

        [Documentation(@"heapmin() -> None

Force the malloc() heap to clean itself up and return unused blocks
to the operating system. On failure, this raises IOError.")]
        public static void heapmin() {
            if (_heapmin() != 0) {
                throw PythonOps.IOError(new Win32Exception());
            }
        }

        // python call: c2pread = msvcrt.open_osfhandle(c2pread.Detach(), 0)
        [Documentation(@"open_osfhandle(handle, flags) -> file descriptor

Create a C runtime file descriptor from the file handle handle. The
flags parameter should be a bitwise OR of os.O_APPEND, os.O_RDONLY,
and os.O_TEXT. The returned file descriptor may be used as a parameter
to os.fdopen() to create a file object.
")]

        public static int open_osfhandle(CodeContext context, BigInteger os_handle, int arg1) {
            FileStream stream = new FileStream(new SafeFileHandle(new IntPtr((long)os_handle), true), FileAccess.ReadWrite);
            return context.LanguageContext.FileManager.AddToStrongMapping(stream);
        }

        [Documentation(@"get_osfhandle(fd) -> file handle

Return the file handle for the file descriptor fd. Raises IOError
if fd is not recognized.")]
        public static object get_osfhandle(CodeContext context, int fd) {
            PythonFile pfile = context.LanguageContext.FileManager.GetFileFromId(context.LanguageContext, fd);

            object handle;
            if (pfile.TryGetFileHandle(out handle)) return handle;

            return -1;
        }

        [Documentation(@"setmode(fd, mode) -> Previous mode

Set the line-end translation mode for the file descriptor fd. To set
it to text mode, flags should be os.O_TEXT; for binary, it should be
os.O_BINARY.")]
        public static int setmode(CodeContext context, int fd, int flags) {
            PythonFile pfile = context.LanguageContext.FileManager.GetFileFromId(context.LanguageContext, fd);
            int oldMode;
            if (flags == PythonNT.O_TEXT) {
                oldMode = pfile.SetMode(context, true) ? PythonNT.O_TEXT : PythonNT.O_BINARY;
            } else if (flags == PythonNT.O_BINARY) {
                oldMode = pfile.SetMode(context, false) ? PythonNT.O_TEXT : PythonNT.O_BINARY;
            } else {
                throw PythonOps.ValueError("unknown mode: {0}", flags);
            }
            return oldMode;
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

        [DllImport("msvcr100", SetLastError=true, CallingConvention=CallingConvention.Cdecl)]
        private static extern int _heapmin();

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern int _kbhit();

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern int _getch();

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern int _getche();

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern int _putch(int c);

        [DllImport("msvcr100", SetLastError=true, CallingConvention=CallingConvention.Cdecl)]
        private static extern int _ungetch(int c);

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern ushort _getwch();

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern ushort _getwche();

        [DllImport("msvcr100", CallingConvention=CallingConvention.Cdecl)]
        private static extern ushort _putwch(char c);

        [DllImport("msvcr100", SetLastError=true, CallingConvention=CallingConvention.Cdecl)]
        private static extern ushort _ungetwch(ushort c);
        #endregion
    }
}
#endif
