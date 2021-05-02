// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_PROCESS

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_winapi", typeof(IronPython.Modules.PythonWinApi), PlatformsAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    public static class PythonWinApi {
        public const string __doc__ = "_subprocess Module";

        #region Public API

        public static PythonTuple CreatePipe(
            CodeContext context,
            object pSec /*Python passes None*/,
            int bufferSize) {
            IntPtr hReadPipe;
            IntPtr hWritePipe;
            
            SECURITY_ATTRIBUTES pSecA = new SECURITY_ATTRIBUTES();
            pSecA.nLength = Marshal.SizeOf(pSecA);
            if (pSec != null) {
                /* If pSec paseed in from Python is not NULL 
                 * there needs to be some conversion done here...*/
            }

            // TODO: handle failures
            CreatePipePI(
                out hReadPipe,
                out hWritePipe,
                ref pSecA,
                (uint)bufferSize);

            return PythonTuple.MakeTuple((BigInteger)(long)hReadPipe, (BigInteger)(long)hWritePipe);
        }

        private static string FormatError(int errorCode) {
            return new Win32Exception(errorCode).Message;
        }

        public static PythonTuple CreateProcess(
            CodeContext context,
            string applicationName,
            string commandLineArgs,
            object pSec /*subprocess.py passes None*/,
            object tSec /*subprocess.py passes None*/,
            int? bInheritHandles,
            uint? dwCreationFlags,
            object lpEnvironment,
            string lpCurrentDirectory,
            object lpStartupInfo /* subprocess.py passes STARTUPINFO*/) {

            int dwFlagsInt32 = PythonOps.TryGetBoundAttr(context, lpStartupInfo, "dwFlags", out object dwFlags) ? Converter.ConvertToInt32(dwFlags) : 0;
            IntPtr hStdInputIntPtr = PythonOps.TryGetBoundAttr(context, lpStartupInfo, "hStdInput", out object hStdInput) ? new IntPtr(Converter.ConvertToInt32(hStdInput)) : IntPtr.Zero;
            IntPtr hStdOutputIntPtr = PythonOps.TryGetBoundAttr(context, lpStartupInfo, "hStdOutput", out object hStdOutput) ? new IntPtr(Converter.ConvertToInt32(hStdOutput)) : IntPtr.Zero;
            IntPtr hStdErrorIntPtr = PythonOps.TryGetBoundAttr(context, lpStartupInfo, "hStdError", out object hStdError) ? new IntPtr(Converter.ConvertToInt32(hStdError)) : IntPtr.Zero;
            short wShowWindowInt16 = PythonOps.TryGetBoundAttr(context, lpStartupInfo, "wShowWindow", out object wShowWindow) ? Converter.ConvertToInt16(wShowWindow) : (short)0;

            STARTUPINFO startupInfo = new STARTUPINFO();
            startupInfo.dwFlags = dwFlagsInt32;
            startupInfo.hStdInput = hStdInputIntPtr;
            startupInfo.hStdOutput = hStdOutputIntPtr;
            startupInfo.hStdError = hStdErrorIntPtr;
            startupInfo.wShowWindow = wShowWindowInt16;

            // No special security
            SECURITY_ATTRIBUTES pSecSA = new SECURITY_ATTRIBUTES();
            pSecSA.nLength = Marshal.SizeOf(pSecSA);

            SECURITY_ATTRIBUTES tSecSA = new SECURITY_ATTRIBUTES();
            tSecSA.nLength = Marshal.SizeOf(tSecSA);

            if (pSec != null) {
                /* If pSec paseed in from Python is not NULL 
                 * there needs to be some conversion done here...*/
            }
            if (tSec != null) {
                /* If tSec paseed in from Python is not NULL 
                 * there needs to be some conversion done here...*/
            }

            // If needed convert lpEnvironment Dictionary to lpEnvironmentIntPtr
            string lpEnvironmentStr = EnvironmentToNative(context, lpEnvironment);

            PROCESS_INFORMATION lpProcessInformation = new PROCESS_INFORMATION();
            bool result = CreateProcessPI(
                String.IsNullOrEmpty(applicationName) ? null : applicationName/*applicationNameHelper*//*processStartInfo.FileName*/,
                String.IsNullOrEmpty(commandLineArgs) ? null : commandLineArgs/*commandLineArgsHelper*//*processStartInfo.Arguments*/,
                ref pSecSA, ref tSecSA,
                bInheritHandles.HasValue && bInheritHandles.Value > 0 ? true : false,
                dwCreationFlags.HasValue ? dwCreationFlags.Value : 0,
                lpEnvironmentStr,
                lpCurrentDirectory,
                ref startupInfo,
                out lpProcessInformation);

            if (!result) {
                int error = Marshal.GetLastWin32Error();
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, error, FormatError(error), null, error);
            }

            IntPtr hp = lpProcessInformation.hProcess;
            IntPtr ht = lpProcessInformation.hThread;
            int pid = lpProcessInformation.dwProcessId;
            int tid = lpProcessInformation.dwThreadId;

            return PythonTuple.MakeTuple((BigInteger)(long)hp, (BigInteger)(long)ht, pid, tid);
        }

        private static string EnvironmentToNative(CodeContext context, object environment) {
            if (environment == null) {
                return null;
            }
            var dict = environment as PythonDictionary ?? new PythonDictionary(context, environment);
            var res = new StringBuilder();
            foreach (var keyValue in dict) {
                res.Append(keyValue.Key);
                res.Append('=');
                res.Append(keyValue.Value);
                res.Append('\0');
            }
            return res.ToString();
        }

        public static void CloseHandle(BigInteger handle) {
            CloseHandle(new IntPtr((long)handle));
        }

        public static BigInteger DuplicateHandle(
            CodeContext context,
            BigInteger sourceProcess,
            BigInteger handle,
            BigInteger targetProcess,
            int desiredAccess,
            bool inherit_handle,
            object DUPLICATE_SAME_ACCESS) {
            
            IntPtr currentProcessIntPtr = new IntPtr((long)sourceProcess);
            IntPtr handleIntPtr = new IntPtr((long)handle);
            IntPtr currentProcess2IntPtr = new IntPtr((long)targetProcess);

            IntPtr lpTargetHandle;
            bool sameAccess = DUPLICATE_SAME_ACCESS != null && Converter.ConvertToBoolean(DUPLICATE_SAME_ACCESS);

            // TODO: handle failures
            DuplicateHandlePI(
                currentProcessIntPtr,
                handleIntPtr,
                currentProcess2IntPtr,
                out lpTargetHandle,
                Converter.ConvertToUInt32(desiredAccess),
                inherit_handle,
                sameAccess ? (uint)DuplicateOptions.DUPLICATE_SAME_ACCESS : (uint)DuplicateOptions.DUPLICATE_CLOSE_SOURCE
            );

            return (long)lpTargetHandle;
        }

        public static BigInteger GetCurrentProcess() {
            return (long)GetCurrentProcessPI();
        }

        public static int GetExitCodeProcess(BigInteger hProcess) {
            int exitCode;
            GetExitCodeProcessPI(new IntPtr((long)hProcess), out exitCode);
            return exitCode;
        }

        public static string GetModuleFileName(object ignored) {
            // Alternative Managed API: System.Diagnostics.ProcessModule.FileName or System.Reflection.Module.FullyQualifiedName.
            return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        }

        public static object GetStdHandle(int STD_OUTPUT_HANDLE) {
            return GetStdHandlePI(STD_OUTPUT_HANDLE).ToPython();
        }

        public static int GetVersion() {
            return GetVersionPI();
        }

        public static BigInteger OpenProcess(CodeContext context, int desired_access, bool inherit_handle, int process_id) {
            return OpenProcessPI(desired_access, inherit_handle, process_id).ToInt64();
        }

        public static bool TerminateProcess(
            BigInteger handle,
            object uExitCode) {

            uint uExitCodeUint = Converter.ConvertToUInt32(uExitCode);
            bool result = TerminateProcessPI(
                new IntPtr((long)handle),
                uExitCodeUint);

            return result;
        }

        public static int WaitForSingleObject(BigInteger handle, int dwMilliseconds) {
            return WaitForSingleObjectPI(new IntPtr((long)handle), dwMilliseconds);
        }

        #endregion

        #region struct's and enum's

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal /*class*/ struct SECURITY_ATTRIBUTES {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [Flags]
        internal enum DuplicateOptions : uint {
            DUPLICATE_CLOSE_SOURCE = (0x00000001),// Closes the source handle. This occurs regardless of any error status returned.
            DUPLICATE_SAME_ACCESS = (0x00000002), //Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
        }

        #endregion

        #region Privates / PInvokes

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", EntryPoint = "CreateProcess", SetLastError = true)]
        private static extern bool CreateProcessPI(string lpApplicationName,
            string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)]bool bInheritHandles,
            uint dwCreationFlags, string lpEnvironment, string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", EntryPoint = "CreatePipe")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipePI(out IntPtr hReadPipe, out IntPtr hWritePipe,
            ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DuplicateHandle")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateHandlePI(IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
            uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
        private static extern IntPtr GetCurrentProcessPI();

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetExitCodeProcess")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcessPI(IntPtr hProcess, out /*uint*/ int lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetStdHandle")]
        private static extern IntPtr GetStdHandlePI(int nStdHandle);

        [DllImport("kernel32.dll", EntryPoint = "GetVersion")]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api")]
        private static extern int GetVersionPI();

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CloseHandle")]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "OpenProcess")]
        private static extern IntPtr OpenProcessPI(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "TerminateProcess")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcessPI(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32", CharSet = CharSet.Ansi,
            SetLastError = true, ExactSpelling = true, EntryPoint = "WaitForSingleObject")]
        internal static extern int WaitForSingleObjectPI(IntPtr hHandle, int dwMilliseconds);

        #endregion

        #region Constants

        public const int CREATE_NEW_CONSOLE = 0x10;
        public const int CREATE_NEW_PROCESS_GROUP = 0x200;
        public const int DUPLICATE_CLOSE_SOURCE = 0x1;
        public const int DUPLICATE_SAME_ACCESS = 0x2;
        public const int ERROR_ALREADY_EXISTS = 0xb7;
        public const int ERROR_BROKEN_PIPE = 0x6d;
        public const int ERROR_IO_PENDING = 0x3e5;
        public const int ERROR_MORE_DATA = 0xea;
        public const int ERROR_NETNAME_DELETED = 0x40;
        public const int ERROR_NO_DATA = 0xe8;
        public const int ERROR_NO_SYSTEM_RESOURCES = 0x5aa;
        public const int ERROR_OPERATION_ABORTED = 0x3e3;
        public const int ERROR_PIPE_BUSY = 0xe7;
        public const int ERROR_PIPE_CONNECTED = 0x217;
        public const int ERROR_SEM_TIMEOUT = 0x79;
        public const int FILE_FLAG_FIRST_PIPE_INSTANCE = 0x80000;
        public const int FILE_FLAG_OVERLAPPED = 0x40000000;
        public const int FILE_GENERIC_READ = 0x120089;
        public const int FILE_GENERIC_WRITE = 0x120116;
        public const int GENERIC_READ = unchecked((int)0x80000000);
        public const int GENERIC_WRITE = 0x40000000;
        public const int INFINITE = unchecked((int)0xffffffff);
        public const int NMPWAIT_WAIT_FOREVER = unchecked((int)0xffffffff);
        public const int NULL = 0x0;
        public const int OPEN_EXISTING = 0x3;
        public const int PIPE_ACCESS_DUPLEX = 0x3;
        public const int PIPE_ACCESS_INBOUND = 0x1;
        public const int PIPE_READMODE_MESSAGE = 0x2;
        public const int PIPE_TYPE_MESSAGE = 0x4;
        public const int PIPE_UNLIMITED_INSTANCES = 0xff;
        public const int PIPE_WAIT = 0x0;
        public const int PROCESS_ALL_ACCESS = 0x1f0fff;
        public const int PROCESS_DUP_HANDLE = 0x40;
        public const int STARTF_USESHOWWINDOW = 0x1;
        public const int STARTF_USESTDHANDLES = 0x100;
        public const int STD_ERROR_HANDLE = -12;
        public const int STD_INPUT_HANDLE = -10;
        public const int STD_OUTPUT_HANDLE = -11;
        public const int STILL_ACTIVE = 0x103;
        public const int SW_HIDE = 0x0;
        public const int WAIT_ABANDONED_0 = 0x80;
        public const int WAIT_OBJECT_0 = 0x0;
        public const int WAIT_TIMEOUT = 0x102;

        #endregion
    }
}

#endif
