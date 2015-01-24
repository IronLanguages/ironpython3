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
 * ***************************************************************************/

#if FEATURE_PROCESS

#if CLR2
using Microsoft.Scripting.Math;
#else
using System.Numerics;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_subprocess", typeof(IronPython.Modules.PythonSubprocess))]
namespace IronPython.Modules {
    [PythonType("_subprocess")]
    public static class PythonSubprocess {
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
            
            return PythonTuple.MakeTuple(
                new PythonSubprocessHandle(hReadPipe),
                new PythonSubprocessHandle(hWritePipe)
            );
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


            object dwFlags = PythonOps.GetBoundAttr(context, lpStartupInfo, "dwFlags"); //public Int32 dwFlags;
            object hStdInput = PythonOps.GetBoundAttr(context, lpStartupInfo, "hStdInput"); //public IntPtr hStdInput;
            object hStdOutput = PythonOps.GetBoundAttr(context, lpStartupInfo, "hStdOutput"); //public IntPtr hStdOutput;
            object hStdError = PythonOps.GetBoundAttr(context, lpStartupInfo, "hStdError"); //public IntPtr hStdError;
            object wShowWindow = PythonOps.GetBoundAttr(context, lpStartupInfo, "wShowWindow"); //Int16 wShowWindow;

            Int32 dwFlagsInt32 = dwFlags != null ? Converter.ConvertToInt32(dwFlags) : 0;
            IntPtr hStdInputIntPtr = hStdInput != null ? new IntPtr(Converter.ConvertToInt32(hStdInput)) : IntPtr.Zero;
            IntPtr hStdOutputIntPtr = hStdOutput != null ? new IntPtr(Converter.ConvertToInt32(hStdOutput)) : IntPtr.Zero;
            IntPtr hStdErrorIntPtr = hStdError != null ? new IntPtr(Converter.ConvertToInt32(hStdError)) : IntPtr.Zero;
            Int16 wShowWindowInt16 = wShowWindow != null ? Converter.ConvertToInt16(wShowWindow) : (short)0;

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
                throw PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, error, CTypes.FormatError(error));
            }

            IntPtr hp = lpProcessInformation.hProcess;
            IntPtr ht = lpProcessInformation.hThread;
            int pid = lpProcessInformation.dwProcessId;
            int tid = lpProcessInformation.dwThreadId;

            return PythonTuple.MakeTuple(
                new PythonSubprocessHandle(hp, true),
                new PythonSubprocessHandle(ht),
                pid, tid);
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

        /// <summary>
        /// Duplicates a subprocess handle which was created for piping.
        /// 
        /// This is only called when we're duplicating the handle to make it inheritable to the child process.  In CPython
        /// the parent handle is always reliably garbage collected.  Because we know this handle is not going to be 
        /// used we close the handle being duplicated.
        /// </summary>
        public static PythonSubprocessHandle DuplicateHandle(CodeContext context,
            BigInteger sourceProcess,
            PythonSubprocessHandle handle,
            BigInteger targetProcess,
            int desiredAccess,
            bool inherit_handle,
            object DUPLICATE_SAME_ACCESS) {

            if (handle._duplicated) {
                // more ref counting issues - when stderr is set to subprocess.STDOUT we can't close the target handle so we need
                // to track this situation.
                return DuplicateHandle(context, sourceProcess, (BigInteger)handle, targetProcess, desiredAccess, inherit_handle, DUPLICATE_SAME_ACCESS);
            }

            var res = DuplicateHandle(context, sourceProcess, (BigInteger)handle, targetProcess, desiredAccess, inherit_handle, DUPLICATE_SAME_ACCESS);
            res._duplicated = true;
            handle.Close();
            return res;
        }

        public static PythonSubprocessHandle DuplicateHandle(
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

            return new PythonSubprocessHandle(lpTargetHandle); //Converter.ConvertToBigInteger( lpTargetHandle.ToInt32());
        }

        public static PythonSubprocessHandle GetCurrentProcess() {
            IntPtr id = GetCurrentProcessPI();
            return new PythonSubprocessHandle(id);
        }

        public static int GetExitCodeProcess(PythonSubprocessHandle hProcess) {
            if (hProcess._isProcess && hProcess._closed) {
                // deal with finalization & resurrection oddness...  see PythonSubprocessHandle finalizer
                return hProcess._exitCode;
            }

            IntPtr hProcessIntPtr = new IntPtr(Converter.ConvertToInt32(hProcess));
            int exitCode = int.MinValue;
            GetExitCodeProcessPI(hProcessIntPtr, out exitCode);
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

        public static bool TerminateProcess(
            PythonSubprocessHandle handle,
            object uExitCode) {

            // Alternative Managed API: System.Diagnostics.Process.Kill()
            IntPtr hProcessIntPtr = new IntPtr(Converter.ConvertToInt32(handle));
            uint uExitCodeUint = Converter.ConvertToUInt32(uExitCode);
            bool result = TerminateProcessPI(
                hProcessIntPtr,
                uExitCodeUint);

            return result;
        }

        public static int WaitForSingleObject(PythonSubprocessHandle handle, int dwMilliseconds) {
            return WaitForSingleObjectPI(handle, dwMilliseconds);
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
        internal static extern bool CreatePipePI(out IntPtr hReadPipe, out IntPtr hWritePipe,
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

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "TerminateProcess")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcessPI(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32", CharSet = CharSet.Ansi,
            SetLastError = true, ExactSpelling = true, EntryPoint = "WaitForSingleObject")]
        internal static extern int WaitForSingleObjectPI(IntPtr hHandle, int dwMilliseconds);

        #endregion

#region Constants

        public const int CREATE_NEW_CONSOLE = 16;
        public const int CREATE_NEW_PROCESS_GROUP = 512;
        public const int DUPLICATE_SAME_ACCESS = 2;
        public const int INFINITE = -1;
        public const int STARTF_USESHOWWINDOW = 1;
        public const int STARTF_USESTDHANDLES = 256;
        public const int STD_ERROR_HANDLE = -12;
        public const int STD_INPUT_HANDLE = -10;
        public const int STD_OUTPUT_HANDLE = -11;
        public const int SW_HIDE = 0;
        public const int WAIT_OBJECT_0 = 0;
        public const int PIPE = -1;
        public const int STDOUT = -2;

        #endregion

    }

    [PythonType("_subprocess_handle")]
    public class PythonSubprocessHandle {
        private readonly IntPtr _internalHandle;
        internal bool _closed;
        internal bool _duplicated, _isProcess;
        internal int _exitCode;
        private static List<PythonSubprocessHandle> _active = new List<PythonSubprocessHandle>();

        internal PythonSubprocessHandle(IntPtr handle) {
            _internalHandle = handle;
        }

        internal PythonSubprocessHandle(IntPtr handle, bool isProcess) {
            _internalHandle = handle;
            _isProcess = isProcess;
        }

        ~PythonSubprocessHandle() {
            if (_isProcess) {
                lock (_active) {
                    // we need to deal w/ order of finalization and the fact that Popen will resurrect
                    // it's self and want to be able to poll us for exit.  Therefore we can't close until
                    // the process has really exited (and we've captured that exit code).  So we keep
                    // resurrecting ourselves until it finally happens.
                    int insertion = -1;
                    for (int i = 0; i < _active.Count; i++) {
                        if (_active[i] == null) {
                            insertion = i;
                        } else if (_active[i].PollForExit()) {
                            _active[i] = null;
                            insertion = i;
                            if (_active[i] == this) {
                                // we've exited, and we're removed from the list
                                Close();
                                return;
                            }
                        } else if (_active[i] == this) {
                            // we haven't exited
                            return;
                        }
                    }

                    // we're not in the list.
                    if (!PollForExit()) {
                        // resurrect ourselves - this is to account for subprocess.py's resurrection of
                        // handles.  We cannot close our handle until we have been successfully polled
                        // for the end of the process.
                        if (insertion != -1) {
                            _active[insertion] = this;
                        } else {
                            _active.Add(this);
                        }
                        return;
                    } else {
                        Close();
                    }
                }
            }

            Close();
        }

        private bool PollForExit() {
            if (PythonSubprocess.WaitForSingleObjectPI(_internalHandle, 0) == PythonSubprocess.WAIT_OBJECT_0) {
                PythonSubprocess.GetExitCodeProcessPI(_internalHandle, out _exitCode);
                return true;
            }
            return false;
        }

        public void Close() {
            lock (this) {
                if (!_closed) {
                    PythonSubprocess.CloseHandle(_internalHandle);
                    _closed = true;
                    GC.SuppressFinalize(this);
                }
            }
        }

        public object Detach(CodeContext context) {
            lock (this) {
                if (!_closed) {
                    _closed = true;
                    GC.SuppressFinalize(this);
                    return _internalHandle.ToPython();
                }
            }
            return -1;
        }

        public static implicit operator int(PythonSubprocessHandle type) {
            return type._internalHandle.ToInt32(); // ToPython()
        }

        public static implicit operator BigInteger(PythonSubprocessHandle type) {
            return type._internalHandle.ToInt32(); // ToPython()
        }

        public static implicit operator IntPtr(PythonSubprocessHandle type) {
            return type._internalHandle; // ToPython()
        }
    }
}
#endif