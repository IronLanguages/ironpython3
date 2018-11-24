using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using IronPython.Runtime;

[assembly: PythonModule("_multiprocessing", typeof(IronPython.Modules.MultiProcessing))]
namespace IronPython.Modules {
    public static class MultiProcessing {
        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern SocketError closesocket([In] IntPtr socketHandle);

        public static object closesocket(int handle) {
            var error = closesocket(new IntPtr(handle));
            // TODO: raise error
            return null;
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int recv(
                                     [In] IntPtr socketHandle,
                                     [In] byte[] pinnedBuffer,
                                     [In] int len,
                                     [In] SocketFlags socketFlags
                                     );

        public static IList<byte> recv(int handle, int size) {
            var buf = new byte[size];
            recv(new IntPtr(handle), buf, size, 0);
            return buf;
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int send(
                                         [In] IntPtr socketHandle,
                                         [In] byte[] pinnedBuffer,
                                         [In] int len,
                                         [In] SocketFlags socketFlags
                                         );

        public static int send(int handle, [BytesConversion]IList<byte> buf) {
            return send(new IntPtr(handle), buf.ToArray(), buf.Count, 0);
        }

    }
}
