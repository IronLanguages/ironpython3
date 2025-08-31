// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_PROCESS

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Win32.SafeHandles;

[assembly: PythonModule("_winapi", typeof(IronPython.Modules.PythonWinApi), PlatformsAttribute.PlatformFamily.Windows)]
namespace IronPython.Modules {
    [SupportedOSPlatform("windows")]
    public static class PythonWinApi {
        #region Public API

        public static object? ConnectNamedPipe(BigInteger handle, bool overlapped = false) {
            if (overlapped) throw new NotImplementedException();

            if (overlapped) {
                throw new NotImplementedException();
            }
            else {
                var result = ConnectNamedPipe(checked((IntPtr)(long)handle), IntPtr.Zero);

                if (!result) throw PythonNT.GetLastWin32Error();

                return null;
            }
        }

        public static BigInteger CreateFile([NotNone] string file_name, int desired_access, int share_mode, int security_attributes, int creation_disposition, int flags_and_attributes, BigInteger template_file) {
            if (security_attributes != 0) throw new NotImplementedException();
            if (template_file != 0) throw new NotImplementedException();

            var handle = CreateFile(file_name, desired_access, share_mode, IntPtr.Zero, creation_disposition, flags_and_attributes, IntPtr.Zero);

            if (handle == new IntPtr(-1)) throw PythonNT.GetLastWin32Error();

            return (long)handle;
        }

        public static BigInteger CreateNamedPipe([NotNone] string name, int open_mode, int pipe_mode, int max_instances, int out_buffer_size, int in_buffer_size, int default_timeout, int security_attributes) {
            if (security_attributes != 0) throw new NotImplementedException();

            var handle = CreateNamedPipePI(name, (uint)open_mode, (uint)pipe_mode, (uint)max_instances, (uint)out_buffer_size, (uint)in_buffer_size, (uint)default_timeout, IntPtr.Zero);

            if (handle == new IntPtr(-1)) throw PythonNT.GetLastWin32Error();

            return (long)handle;
        }

        public static PythonTuple CreatePipe(object? pipe_attrs, int size) {
            SECURITY_ATTRIBUTES pSecA = new SECURITY_ATTRIBUTES();
            pSecA.nLength = Marshal.SizeOf(pSecA);
            if (pipe_attrs != null) {
                /* If pSec passed in from Python is not NULL
                 * there needs to be some conversion done here...*/
                throw new NotImplementedException();
            }

            var result = CreatePipePI(out IntPtr hReadPipe, out IntPtr hWritePipe, ref pSecA, (uint)size);

            if (!result) throw PythonNT.GetLastWin32Error();

            return PythonTuple.MakeTuple((BigInteger)(long)hReadPipe, (BigInteger)(long)hWritePipe);
        }

        public static PythonTuple CreateProcess(
            CodeContext context,
            string? application_name,
            string? command_line,
            object? proc_attrs /*subprocess.py passes None*/,
            object? thread_attrs /*subprocess.py passes None*/,
            int? inherit_handles,
            uint? creation_flags,
            object? env_mapping,
            string? current_directory,
            object? startup_info /* subprocess.py passes STARTUPINFO*/) {

            PythonOps.TryGetBoundAttr(context, startup_info, "dwFlags", out object? dwFlags); //public Int32 dwFlags;
            PythonOps.TryGetBoundAttr(context, startup_info, "hStdInput", out object? hStdInput); //public IntPtr hStdInput;
            PythonOps.TryGetBoundAttr(context, startup_info, "hStdOutput", out object? hStdOutput); //public IntPtr hStdOutput;
            PythonOps.TryGetBoundAttr(context, startup_info, "hStdError", out object? hStdError); //public IntPtr hStdError;
            PythonOps.TryGetBoundAttr(context, startup_info, "wShowWindow", out object? wShowWindow); //Int16 wShowWindow;

            int dwFlagsInt32 = dwFlags != null ? Converter.ConvertToInt32(dwFlags) : 0;
            IntPtr hStdInputIntPtr = hStdInput != null ? new IntPtr(Converter.ConvertToInt32(hStdInput)) : IntPtr.Zero;
            IntPtr hStdOutputIntPtr = hStdOutput != null ? new IntPtr(Converter.ConvertToInt32(hStdOutput)) : IntPtr.Zero;
            IntPtr hStdErrorIntPtr = hStdError != null ? new IntPtr(Converter.ConvertToInt32(hStdError)) : IntPtr.Zero;
            short wShowWindowInt16 = wShowWindow != null ? Converter.ConvertToInt16(wShowWindow) : (short)0;

            STARTUPINFO startupInfo = new STARTUPINFO {
                dwFlags = dwFlagsInt32,
                hStdInput = hStdInputIntPtr,
                hStdOutput = hStdOutputIntPtr,
                hStdError = hStdErrorIntPtr,
                wShowWindow = wShowWindowInt16
            };

            // No special security
            SECURITY_ATTRIBUTES pSecSA = new SECURITY_ATTRIBUTES();
            pSecSA.nLength = Marshal.SizeOf(pSecSA);

            SECURITY_ATTRIBUTES tSecSA = new SECURITY_ATTRIBUTES();
            tSecSA.nLength = Marshal.SizeOf(tSecSA);

            if (proc_attrs != null) {
                /* If pSec paseed in from Python is not NULL 
                 * there needs to be some conversion done here...*/
            }
            if (thread_attrs != null) {
                /* If tSec paseed in from Python is not NULL 
                 * there needs to be some conversion done here...*/
            }

            // If needed convert lpEnvironment Dictionary to lpEnvironmentIntPtr
            string? lpEnvironment = EnvironmentToNative(context, env_mapping);

            bool result = CreateProcessPI(
                string.IsNullOrEmpty(application_name) ? null : application_name/*applicationNameHelper*//*processStartInfo.FileName*/,
                string.IsNullOrEmpty(command_line) ? null : command_line/*commandLineArgsHelper*//*processStartInfo.Arguments*/,
                ref pSecSA, ref tSecSA,
                inherit_handles.HasValue && inherit_handles.Value > 0,
                creation_flags ?? 0,
                lpEnvironment,
                current_directory,
                ref startupInfo,
                out PROCESS_INFORMATION lpProcessInformation);

            if (!result) throw PythonNT.GetLastWin32Error();

            IntPtr hp = lpProcessInformation.hProcess;
            IntPtr ht = lpProcessInformation.hThread;
            int pid = lpProcessInformation.dwProcessId;
            int tid = lpProcessInformation.dwThreadId;

            return PythonTuple.MakeTuple((BigInteger)(long)hp, (BigInteger)(long)ht, pid, tid);

            static string? EnvironmentToNative(CodeContext context, object? environment) {
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
        }

        public static void CloseHandle(BigInteger handle) {
            CloseHandle(new IntPtr((long)handle));
        }

        public static BigInteger DuplicateHandle(
            BigInteger source_process_handle,
            BigInteger source_handle,
            BigInteger target_process_handle,
            int desired_access,
            bool inherit_handle,
            object? DUPLICATE_SAME_ACCESS) {
            
            IntPtr currentProcessIntPtr = new IntPtr((long)source_process_handle);
            IntPtr handleIntPtr = new IntPtr((long)source_handle);
            IntPtr currentProcess2IntPtr = new IntPtr((long)target_process_handle);

            bool sameAccess = DUPLICATE_SAME_ACCESS != null && Converter.ConvertToBoolean(DUPLICATE_SAME_ACCESS);

            var result = DuplicateHandlePI(
                currentProcessIntPtr,
                handleIntPtr,
                currentProcess2IntPtr,
                out IntPtr lpTargetHandle,
                Converter.ConvertToUInt32(desired_access),
                inherit_handle,
                sameAccess ? (uint)DuplicateOptions.DUPLICATE_SAME_ACCESS : (uint)DuplicateOptions.DUPLICATE_CLOSE_SOURCE
            );

            if (!result) throw PythonNT.GetLastWin32Error();

            return (long)lpTargetHandle;
        }

        public static void ExitProcess(int exit_code) => Environment.Exit(exit_code);

        public static BigInteger GetCurrentProcess() => (long)GetCurrentProcessPI();

        public static int GetExitCodeProcess(BigInteger process) {
            GetExitCodeProcessPI(new IntPtr((long)process), out int exitCode);
            return exitCode;
        }

        public static int GetLastError() => Marshal.GetLastWin32Error();

        public static string? GetModuleFileName(int module_handle) {
            // Alternative Managed API: System.Diagnostics.ProcessModule.FileName or System.Reflection.Module.FullyQualifiedName.
            return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        }

        public static object? GetStdHandle(int std_handle) {
            var handle = GetStdHandlePI(std_handle);
            if (handle == IntPtr.Zero) return null;
            return handle.ToPython();
        }

        public static int GetVersion() {
            return GetVersionPI();
        }

        public static BigInteger OpenProcess(int desired_access, bool inherit_handle, int process_id) {
            return OpenProcessPI(desired_access, inherit_handle, process_id).ToInt64();
        }

        public static void TerminateProcess(BigInteger handle, object? exit_code) {
            uint uExitCodeUint = Converter.ConvertToUInt32(exit_code);
            bool result = TerminateProcessPI(new IntPtr((long)handle), uExitCodeUint);

            if (!result) throw PythonNT.GetLastWin32Error();
        }

        public static int WaitForSingleObject(BigInteger handle, int dwMilliseconds) {
            return WaitForSingleObjectPI(new IntPtr((long)handle), dwMilliseconds);
        }

        public static void SetNamedPipeHandleState(BigInteger named_pipe, object? mode, object? max_collection_count, object? collect_data_timeout) {
            if (max_collection_count is not null) throw new NotImplementedException();
            if (collect_data_timeout is not null) throw new NotImplementedException();
            var pipeHandle = new SafePipeHandle(new IntPtr((long)named_pipe), false);
            int m = Converter.ConvertToInt32(mode);
            var result = Interop.Kernel32.SetNamedPipeHandleState(pipeHandle, ref m, IntPtr.Zero, IntPtr.Zero);

            if (!result) throw PythonNT.GetLastWin32Error();
        }

        #endregion

        #region struct's and enum's

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ConnectNamedPipe(IntPtr hNamedPipe, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr securityAttrs,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipe", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateNamedPipePI(string lpName, uint dwOpenMode, uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize, uint nDefaultTimeOut, IntPtr lpSecurityAttributes);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", EntryPoint = "CreateProcess", SetLastError = true)]
        private static extern bool CreateProcessPI(string? lpApplicationName,
            string? lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)]bool bInheritHandles,
            uint dwCreationFlags, string? lpEnvironment, string? lpCurrentDirectory,
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
