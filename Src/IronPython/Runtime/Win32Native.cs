using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace IronPython.Runtime {
#if FEATURE_NATIVE
    class NativeMethods {
        [Serializable]
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        [BestFitMapping(false)]
        internal struct WIN32_FIND_DATA {
            internal int dwFileAttributes;
            // ftCreationTime was a by-value FILETIME structure
            internal uint ftCreationTime_dwLowDateTime;
            internal uint ftCreationTime_dwHighDateTime;
            // ftLastAccessTime was a by-value FILETIME structure
            internal uint ftLastAccessTime_dwLowDateTime;
            internal uint ftLastAccessTime_dwHighDateTime;
            // ftLastWriteTime was a by-value FILETIME structure
            internal uint ftLastWriteTime_dwLowDateTime;
            internal uint ftLastWriteTime_dwHighDateTime;
            internal int nFileSizeHigh;
            internal int nFileSizeLow;
            // If the file attributes' reparse point flag is set, then
            // dwReserved0 is the file tag (aka reparse tag) for the 
            // reparse point.  Use this to figure out whether something is
            // a volume mount point or a symbolic link.
            internal int dwReserved0;
            internal int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal String cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            internal String cAlternateFileName;
        }

        public static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        internal static extern IntPtr FindFirstFile(String fileName, out WIN32_FIND_DATA data);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        internal static extern bool FindNextFile(
                    IntPtr hndFindFile,
                    out WIN32_FIND_DATA lpFindFileData);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32")]
        internal static extern bool FindClose(IntPtr handle);

    }
#endif
}
