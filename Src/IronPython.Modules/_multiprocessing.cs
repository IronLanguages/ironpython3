using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;
using Microsoft.Win32.SafeHandles;

using IronPython.Runtime;

[assembly: PythonModule("_multiprocessing", typeof(IronPython.Modules.MultiProcessing))]
namespace IronPython.Modules {
    public static class MultiProcessing {
        // TODO: implement SemLock and sem_unlink

        public static object flags { get; set; } = new PythonDictionary();

        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern SocketError closesocket([In] IntPtr socketHandle);

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
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

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static Bytes recv(int handle, int size) {
            var buf = new byte[size];
            recv(new IntPtr(handle), buf, size, 0);
            return Bytes.Make(buf);
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int send(
                                         [In] IntPtr socketHandle,
                                         [In] IntPtr pinnedBuffer, // const char*
                                         [In] int len,
                                         [In] SocketFlags socketFlags
                                         );

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static unsafe int send(int handle, [NotNull] IBufferProtocol data) {
            using var buffer = data.GetBuffer();
            var span = buffer.AsReadOnlySpan();
            fixed (byte* ptr = &MemoryMarshal.GetReference(span))
                return send(new IntPtr(handle), new IntPtr(ptr), span.Length, 0);
        }
    }
}
