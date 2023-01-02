// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_FULL_NET

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("_socket", typeof(IronPython.Modules.PythonSocket))]
namespace IronPython.Modules {
    public static class PythonSocket {
        private static readonly object _defaultTimeoutKey = new object();
        private static readonly object _defaultBufsizeKey = new object();
        private const int DefaultBufferSize = 8192;

#pragma warning disable IPY01 // Parameter which is marked not nullable does not have the NotNullAttribute
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            if (!context.HasModuleState(_defaultTimeoutKey)) {
                context.SetModuleState(_defaultTimeoutKey, null);
            }

            context.SetModuleState(_defaultBufsizeKey, DefaultBufferSize);

            context.EnsureModuleException("socketherror", error, dict, "herror", "socket");
            context.EnsureModuleException("socketgaierror", error, dict, "gaierror", "socket");
            context.EnsureModuleException("sockettimeout", error, dict, "timeout", "socket");
        }
#pragma warning restore IPY01 // Parameter which is marked not nullable does not have the NotNullAttribute

        public static PythonType error => PythonExceptions.OSError;

        public const string __doc__ = "Implementation module for socket operations.\n\n"
            + "This module is a loose wrapper around the .NET System.Net.Sockets API, so you\n"
            + "may find the corresponding MSDN documentation helpful in decoding error\n"
            + "messages and understanding corner cases.\n"
            + "\n"
            + "This implementation of socket differs slightly from the standard CPython\n"
            + "socket module. Many of these differences are due to the implementation of the\n"
            + ".NET socket libraries. These differences are summarized below. For full\n"
            + "details, check the docstrings of the functions mentioned.\n"
            + " - s.accept(), s.connect(), and s.connect_ex() do not support timeouts.\n"
            + " - Timeouts in s.sendall() don't work correctly.\n"
            + " - s.dup() is not implemented.\n"
            + " - SSL support is not implemented."
            + "\n"
            + "An Extra IronPython-specific function is exposed only if the clr module is\n"
            + "imported:\n"
            + " - s.HandleToSocket() returns the System.Net.Sockets.Socket object associated\n"
            + "   with a particular \"file descriptor number\" (as returned by s.fileno()).\n"
            ;

        #region Socket object

        public static PythonType SocketType = DynamicHelpers.GetPythonTypeFromType(typeof(socket));

        [PythonType]
        [Documentation("socket([family[, type[, proto]]]) -> socket object\n\n"
                + "Create a socket (a network connection endpoint) of the given family, type,\n"
                + "and protocol. socket() accepts keyword arguments.\n"
                + " - family (address family) defaults to AF_INET\n"
                + " - type (socket type) defaults to SOCK_STREAM\n"
                + " - proto (protocol type) defaults to 0, which specifies the default protocol\n"
                + "\n"
                + "This module supports only IP sockets. It does not support raw or Unix sockets.\n"
                + "Both IPv4 and IPv6 are supported.")]
        public class socket : IWeakReferenceable {
            #region Fields

            /// <summary>
            /// handleToSocket allows us to translate from Python's idea of a socket resource (file
            /// descriptor numbers) to .NET's idea of a socket resource (System.Net.Socket objects).
            /// In particular, this allows the select module to convert file numbers (as returned by
            /// fileno()) and convert them to Socket objects so that it can do something useful with them.
            /// </summary>
            private static readonly Dictionary<IntPtr, WeakReference> _handleToSocket = new Dictionary<IntPtr, WeakReference>();

            private const int DefaultAddressFamily = (int)AddressFamily.InterNetwork;
            private const int DefaultSocketType = (int)System.Net.Sockets.SocketType.Stream;
            private const int DefaultProtocolType = (int)ProtocolType.Unspecified;

            internal Socket _socket;
            internal string? _hostName;
            private WeakRefTracker? _weakRefTracker;
            private int _referenceCount = 1; // TODO: this is no longer incremented by anything...
            public const string __module__ = "socket";
            internal CodeContext/*!*/ _context;
            private int _timeout;

            #endregion

            #region Public API

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public socket() { } // TODO: _socket and _context are null!
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public void __init__(CodeContext/*!*/ context, int family = DefaultAddressFamily,
                int type = DefaultSocketType,
                int proto = DefaultProtocolType,
                object? fileno = null) {

                var socketType = (SocketType)Enum.ToObject(typeof(SocketType), type);
                if (!Enum.IsDefined(typeof(SocketType), socketType)) {
                    throw MakeException(context, new SocketException((int)SocketError.SocketNotSupported));
                }
                var addressFamily = (AddressFamily)Enum.ToObject(typeof(AddressFamily), family);
                if (!Enum.IsDefined(typeof(AddressFamily), addressFamily)) {
                    throw MakeException(context, new SocketException((int)SocketError.AddressFamilyNotSupported));
                }
                var protocolType = (ProtocolType)Enum.ToObject(typeof(ProtocolType), proto);
                if (!Enum.IsDefined(typeof(ProtocolType), protocolType)) {
                    throw MakeException(context, new SocketException((int)SocketError.ProtocolNotSupported));
                }

                Socket? socket = null;
                if (fileno is socket sock) {
                    socket = sock._socket;
                    _hostName = sock._hostName;
                    // we now own the lifetime of the socket
                    GC.SuppressFinalize(sock);
                } else if (fileno != null) {
                    if (!PythonOps.TryToIndex(fileno, out object? handleObj)) {
                        throw PythonOps.TypeErrorForUnIndexableObject(fileno);
                    }
                    long handle = Converter.ConvertToInt64(handleObj);
                    // Windows reserves only INVALID_SOCKET (~0) as an invalid handle
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? handle == -1 : handle < 0) {
                        throw PythonOps.ValueError("negative file descriptor");
                    }
                    socket = HandleToSocket(handle);
                    if (socket is null) {
                        throw PythonOps.OSError(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? PythonErrorNumber.WSAENOTSOCK : PythonErrorNumber.EBADF,
                            "Bad file descriptor");
                    }
                } else {
                    try {
                        socket = new Socket(addressFamily, socketType, protocolType);
                        if (ClrModule.IsMono) {
                            // for whatever reason Mono sets this to true on Linux
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                        }
                    } catch (SocketException e) {
                        throw MakeException(context, e);
                    }
                }
                Initialize(context, socket);
            }

            public void __del__() {
                _close();
            }

            ~socket() {
                _close();
            }

            private IAsyncResult? _acceptResult;

            [Documentation("accept() -> (conn, address)\n\n"
                + "Accept a connection. The socket must be bound and listening before calling\n"
                + "accept(). conn is a new socket object connected to the remote host, and\n"
                + "address is the remote host's address (e.g. a (host, port) tuple for IPv4).\n"
                + "\n"
                )]
            public PythonTuple _accept() {
                socket wrappedRemoteSocket;
                Socket realRemoteSocket;
                try {
                    if (_acceptResult != null && _acceptResult.IsCompleted) {
                        // previous async result has completed
                        realRemoteSocket = _socket.EndAccept(_acceptResult);
                    } else {
                        int timeoutTime = _timeout;
                        if (timeoutTime != 0) {
                            // use the existing or create a new async request
                            var asyncResult = _acceptResult ?? _socket.BeginAccept((x) => { }, null);

                            if (asyncResult.AsyncWaitHandle.WaitOne(timeoutTime)) {
                                // it's completed, end and throw it away
                                realRemoteSocket = _socket.EndAccept(asyncResult);
                                _acceptResult = null;
                            } else {
                                // save the async result for later incase it completes
                                _acceptResult = asyncResult;
                                throw new SocketException((int)SocketError.TimedOut);
                            }
                        } else {
                            realRemoteSocket = _socket.Accept();
                        }
                    }
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }

                wrappedRemoteSocket = new socket(_context, realRemoteSocket);
                return PythonTuple.MakeTuple(wrappedRemoteSocket, wrappedRemoteSocket.getpeername());
            }

            [Documentation("bind(address) -> None\n\n"
                + "Bind to an address. If the socket is already bound, socket.error is raised.\n"
                + "For IP sockets, address is a (host, port) tuple. Raw sockets are not\n"
                + "supported.\n"
                + "\n"
                + "If you do not care which local address is assigned, set host to INADDR_ANY and\n"
                + "the system will assign the most appropriate network address. Similarly, if you\n"
                + "set port to 0, the system will assign an available port number between 1024\n"
                + "and 5000."
                )]
            public void bind([NotNone] PythonTuple address) {
                IPEndPoint localEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    _socket.Bind(localEP);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("close() -> None\n\nClose the socket. It cannot be used after being closed.")]
            public void close() {
                var refs = System.Threading.Interlocked.Decrement(ref _referenceCount);

                // Don't actually close the socket if other file objects are
                // still referring to this socket.                
                if (refs < 1) {
                    _close();
                }
            }

            private void _close() {
                if (_socket != null) {
                    lock (_handleToSocket) {
                        if (_handleToSocket.TryGetValue(_socket.Handle, out WeakReference? weakref)) {
                            Socket? target = weakref.Target as Socket;
                            if (target == _socket || target == null) {
                                _handleToSocket.Remove(_socket.Handle);
                            }
                        }
                    }

                    ((IDisposable)_socket).Dispose();
                    if (System.Threading.Interlocked.Exchange(ref _referenceCount, 0) > 0) {
                        try { // this may throw when called from the Finalizer (on Mono) so just swallow any exception
                            PythonOps.Warn(_context, PythonExceptions.ResourceWarning, $"unclosed {PythonOps.Repr(_context, this)}");
                        } catch { }
                    }
                }
            }

            [Documentation("connect(address) -> None\n\n"
                + "Connect to a remote socket at the given address. IP addresses are expressed\n"
                + "as (host, port).\n"
                + "\n"
                + "Raises socket.error if the socket has been closed, the socket is listening, or\n"
                + "another connection error occurred."
                + "\n"
                + "Difference from CPython: connect() does not support timeouts in blocking mode.\n"
                + "If a timeout is set and the socket is in blocking mode, connect() will block\n"
                + "indefinitely until a connection is made or an error occurs."
                )]
            public void connect([NotNone] PythonTuple address) {
                IPEndPoint remoteEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    _socket.Connect(remoteEP);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("connect_ex(address) -> error_code\n\n"
                + "Like connect(), but return an error code insted of raising an exception for\n"
                + "socket exceptions raised by the underlying system Connect() call. Note that\n"
                + "exceptions other than SocketException generated by the system Connect() call\n"
                + "will still be raised.\n"
                + "\n"
                + "A return value of 0 indicates that the connect call was successful."
                + "\n"
                + "Difference from CPython: connect_ex() does not support timeouts in blocking\n"
                + "mode. If a timeout is set and the socket is in blocking mode, connect_ex() will\n"
                + "block indefinitely until a connection is made or an error occurs."
                )]
            public int connect_ex([NotNone] PythonTuple address) {
                IPEndPoint remoteEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    _socket.Connect(remoteEP);
                } catch (SocketException ex) {
                    return !ClrModule.IsMono ? ex.NativeErrorCode : MapMonoSocketErrorToErrno(ex.SocketErrorCode);
                }
                return PythonErrorNumber.ENOERROR;
            }

            public long detach() {
                var fd = fileno();
                _socket = null!; // TODO: this is wrong, no null checks on _socket anywhere!
                return fd;
            }

            [Documentation("fileno() -> file_handle\n\n"
                + "Return the underlying system handle for this socket (a 64-bit integer)."
                )]
            public long fileno() {
                try {
                    return _socket.Handle.ToInt64();
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("getpeername() -> address\n\n"
                + "Return the address of the remote end of this socket. The address format is\n"
                + "family-dependent (e.g. a (host, port) tuple for IPv4)."
                )]
            public PythonTuple getpeername() {
                try {
                    IPEndPoint? remoteEP = _socket.RemoteEndPoint as IPEndPoint;
                    if (remoteEP == null) {
                        throw MakeException(_context, new SocketException((int)SocketError.NotConnected));
                    }
                    return EndPointToTuple(remoteEP);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("getsockname() -> address\n\n"
                + "Return the address of the local end of this socket. The address format is\n"
                + "family-dependent (e.g. a (host, port) tuple for IPv4)."
                )]
            public PythonTuple getsockname() {
                try {
                    IPEndPoint? localEP = _socket.LocalEndPoint as IPEndPoint;
                    if (localEP == null) {
                        throw MakeException(_context, new SocketException((int)SocketError.InvalidArgument));
                    }
                    return EndPointToTuple(localEP);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("getsockopt(level, optname[, buflen]) -> value\n\n"
                + "Return the value of a socket option. level is one of the SOL_* constants\n"
                + "defined in this module, and optname is one of the SO_* constants. If buflen is\n"
                + "omitted or zero, an integer value is returned. If it is present, a byte string\n"
                + "whose maximum length is buflen bytes) is returned. The caller must the decode\n"
                + "the resulting byte string."
                )]
            public object getsockopt(int optionLevel, int optionName, int optionLength = 0) {
                SocketOptionLevel level = (SocketOptionLevel)Enum.ToObject(typeof(SocketOptionLevel), optionLevel);
                if (!Enum.IsDefined(typeof(SocketOptionLevel), level)) {
                    throw MakeException(_context, new SocketException((int)SocketError.InvalidArgument));
                }
                SocketOptionName name = (SocketOptionName)Enum.ToObject(typeof(SocketOptionName), optionName);
                if (!Enum.IsDefined(typeof(SocketOptionName), name)) {
                    throw MakeException(_context, new SocketException((int)SocketError.ProtocolOption));
                }

                try {
                    if (optionLength == 0) {
                        // Integer return value
                        return (int)_socket.GetSocketOption(level, name)!;
                    } else {
                        // Byte string return value
                        return _socket.GetSocketOption(level, name, optionLength).MakeString();
                    }
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("listen(backlog) -> None\n\n"
                + "Listen for connections on the socket. Backlog is the maximum length of the\n"
                + "pending connections queue. The maximum value is system-dependent."
                )]
            public void listen() => listen(Math.Min(SOMAXCONN, 128)); // new in CPython 3.5

            public void listen(int backlog) {
                try {
                    _socket.Listen(backlog);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            private static ArrayPool<byte> ArrayPool => ArrayPool<byte>.Shared;

            [Documentation("recv(bufsize[, flags]) -> string\n\n"
                + "Receive data from the socket, up to bufsize bytes. For connection-oriented\n"
                + "protocols (e.g. SOCK_STREAM), you must first call either connect() or\n"
                + "accept(). Connectionless protocols (e.g. SOCK_DGRAM) may also use recvfrom().\n"
                + "\n"
                + "recv() blocks until data is available, unless a timeout was set using\n"
                + "settimeout(). If the timeout was exceeded, socket.timeout is raised."
                + "recv() returns immediately with zero bytes when the connection is closed."
                )]
            public Bytes recv(int bufsize, int flags = 0) {
                if (bufsize < 0) throw PythonOps.ValueError($"negative buffersize in {nameof(recv)}");

                var buffer = ArrayPool.Rent(bufsize);
                try {
                    int bytesRead;
                    try {
                        bytesRead = _socket.Receive(buffer, bufsize, (SocketFlags)flags);
                    } catch (Exception e) {
                        throw MakeException(_context, e);
                    }

                    var bytes = new byte[bytesRead];
                    Array.Copy(buffer, bytes, bytes.Length);

                    return Bytes.Make(bytes);
                } finally {
                    ArrayPool.Return(buffer);
                }
            }

            [Documentation("recv_into(memoryview, [nbytes[, flags]]) -> nbytes_read\n\n"
                + "A version of recv() that stores its data into a bytearray rather than creating\n"
                + "a new string.  Receive up to buffersize bytes from the socket.  If buffersize\n"
                + "is not specified (or 0), receive up to the size available in the given buffer.\n\n"
                + "See recv() for documentation about the flags.\n"
                )]
            public int recv_into([NotNone] IBufferProtocol buffer, int nbytes = 0, int flags = 0) {
                using var buf = buffer.GetBufferNoThrow(BufferFlags.Writable);
                if (buf is null) throw PythonOps.TypeError($"{nameof(recv_into)}() argument 1 must be read-write buffer, not {PythonOps.GetPythonTypeName(buffer)}");
                var span = buf.AsSpan();

                if (nbytes < 0) throw PythonOps.ValueError("negative buffersize in " + nameof(recv_into));
                if (nbytes > span.Length) throw PythonOps.ValueError("buffer too small for requested bytes");
                if (nbytes == 0) nbytes = span.Length;

                var byteBuffer = ArrayPool.Rent(nbytes);
                try {
                    IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                    EndPoint remoteEP = remoteIPEP;

                    int bytesRead;
                    try {
                        bytesRead = _socket.Receive(byteBuffer, nbytes, (SocketFlags)flags);
                    } catch (Exception e) {
                        throw MakeException(_context, e);
                    }

                    byteBuffer.AsSpan(0, bytesRead).CopyTo(span);

                    return bytesRead;
                } finally {
                    ArrayPool.Return(byteBuffer);
                }
            }

            [Documentation("")]
            public int recv_into(object? buffer, int nbytes = 0, int flags = 0) {
                if (Converter.TryConvert<IBufferProtocol>(buffer, out var bufferProtocol) && bufferProtocol is not null) {
                    return recv_into(bufferProtocol, nbytes, flags);
                }

                throw PythonOps.TypeError($"{nameof(recv_into)}() argument 1 must be read-write buffer, not {PythonOps.GetPythonTypeName(buffer)}");
            }

            [Documentation("recvfrom(bufsize[, flags]) -> (string, address)\n\n"
                + "Receive data from the socket, up to bufsize bytes. string is the data\n"
                + "received, and address (whose format is protocol-dependent) is the address of\n"
                + "the socket from which the data was received."
                )]
            public PythonTuple recvfrom(int bufsize, int flags = 0) {
                if (bufsize < 0) throw PythonOps.ValueError($"negative buffersize in {nameof(recvfrom)}");

                var buffer = ArrayPool.Rent(bufsize);
                try {
                    IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                    EndPoint remoteEP = remoteIPEP;

                    int bytesRead;
                    try {
                        bytesRead = _socket.ReceiveFrom(buffer, bufsize, (SocketFlags)flags, ref remoteEP);
                    } catch (Exception e) {
                        throw MakeException(_context, e);
                    }

                    var bytes = new byte[bytesRead];
                    Array.Copy(buffer, bytes, bytes.Length);

                    return PythonTuple.MakeTuple(Bytes.Make(bytes), EndPointToTuple((IPEndPoint)remoteEP));
                } finally {
                    ArrayPool.Return(buffer);
                }
            }

            [Documentation("recvfrom_into(buffer[, nbytes[, flags]]) -> (nbytes, address info)\n\n"
                + "Like recv_into(buffer[, nbytes[, flags]]) but also return the sender's address info.\n"
                )]
            public PythonTuple recvfrom_into([NotNone] IBufferProtocol buffer, int nbytes = 0, int flags = 0) {
                using var buf = buffer.GetBufferNoThrow(BufferFlags.Writable);
                if (buf is null) throw PythonOps.TypeError($"{nameof(recvfrom_into)}() argument 1 must be read-write buffer, not {PythonOps.GetPythonTypeName(buffer)}");
                var span = buf.AsSpan();

                if (nbytes < 0) throw PythonOps.ValueError("negative buffersize in " + nameof(recvfrom_into));
                if (nbytes > span.Length) throw PythonOps.ValueError("nbytes is greater than the length of the buffer");
                if (nbytes == 0) nbytes = span.Length;

                var byteBuffer = ArrayPool.Rent(nbytes);
                try {
                    IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                    EndPoint remoteEP = remoteIPEP;

                    int bytesRead;
                    try {
                        bytesRead = _socket.ReceiveFrom(byteBuffer, nbytes, (SocketFlags)flags, ref remoteEP);
                    } catch (Exception e) {
                        throw MakeException(_context, e);
                    }

                    byteBuffer.AsSpan(0, bytesRead).CopyTo(span);

                    return PythonTuple.MakeTuple(bytesRead, EndPointToTuple((IPEndPoint)remoteEP));
                } finally {
                    ArrayPool.Return(byteBuffer);
                }
            }

            [Documentation("")]
            public PythonTuple recvfrom_into(object? buffer, int nbytes = 0, int flags = 0) {
                if (Converter.TryConvert<IBufferProtocol>(buffer, out var bufferProtocol) && bufferProtocol is not null) {
                    return recvfrom_into(bufferProtocol, nbytes, flags);
                }

                throw PythonOps.TypeError($"{nameof(recvfrom_into)}() argument 1 must be read-write buffer, not {PythonOps.GetPythonTypeName(buffer)}");
            }

            private static int byteBufferSize(string funcName, int nbytes, int bufLength, int itemSize = 1) {
                if (nbytes < 0) {
                    throw PythonOps.ValueError("negative buffersize in " + funcName);
                } else if (nbytes == 0) {
                    return bufLength * itemSize;
                } else {
                    int remainder = nbytes % itemSize;
                    return Math.Min(remainder == 0 ? nbytes : nbytes + itemSize - remainder,
                        bufLength * itemSize);
                }
            }

            [Documentation("send(string[, flags]) -> bytes_sent\n\n"
                + "Send data to the remote socket. The socket must be connected to a remote\n"
                + "socket (by calling either connect() or accept(). Returns the number of bytes\n"
                + "sent to the remote socket.\n"
                + "\n"
                + "Note that the successful completion of a send() call does not mean that all of\n"
                + "the data was sent. The caller must keep track of the number of bytes sent and\n"
                + "retry the operation until all of the data has been sent.\n"
                + "\n"
                + "Also note that there is no guarantee that the data you send will appear on the\n"
                + "network immediately. To increase network efficiency, the underlying system may\n"
                + "delay transmission until a significant amount of outgoing data is collected. A\n"
                + "successful completion of the Send method means that the underlying system has\n"
                + "had room to buffer your data for a network send"
                )]
            public int send([NotNone] Bytes data, int flags = 0) {
                return sendWorker(data.UnsafeByteArray, flags);
            }

            public int send([NotNone] IBufferProtocol data, int flags = 0) {
                using IPythonBuffer buffer = data.GetBuffer();
                return sendWorker(buffer.AsUnsafeArray() ?? buffer.ToArray(), flags);
            }

            private int sendWorker(byte[] buffer, int flags) {
                try {
                    return _socket.Send(buffer, (SocketFlags)flags);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("sendall(string[, flags]) -> None\n\n"
                + "Send data to the remote socket. The socket must be connected to a remote\n"
                + "socket (by calling either connect() or accept().\n"
                + "\n"
                + "Unlike send(), sendall() blocks until all of the data has been sent or until a\n"
                + "timeout or an error occurs. None is returned on success. If an error occurs,\n"
                + "there is no way to tell how much data, if any, was sent.\n"
                + "\n"
                + "Difference from CPython: timeouts do not function as you would expect. The\n"
                + "function is implemented using multiple calls to send(), so the timeout timer\n"
                + "is reset after each of those calls. That means that the upper bound on the\n"
                + "time that it will take for sendall() to return is the number of bytes in\n"
                + "string times the timeout interval.\n"
                + "\n"
                + "Also note that there is no guarantee that the data you send will appear on the\n"
                + "network immediately. To increase network efficiency, the underlying system may\n"
                + "delay transmission until a significant amount of outgoing data is collected. A\n"
                + "successful completion of the Send method means that the underlying system has\n"
                + "had room to buffer your data for a network send"
                )]
            public void sendall([NotNone] Bytes data, int flags = 0) {
                sendallWorker(data.UnsafeByteArray, flags);
            }

            public void sendall([NotNone] IBufferProtocol data, int flags = 0) {
                using IPythonBuffer buffer = data.GetBuffer();
                sendallWorker(buffer.AsUnsafeArray() ?? buffer.ToArray(), flags);
            }

            private void sendallWorker(byte[] buffer, int flags) {
                try {
                    int bytesTotal = buffer.Length;
                    int bytesRemaining = bytesTotal;
                    while (bytesRemaining > 0) {
                        bytesRemaining -= _socket.Send(buffer, bytesTotal - bytesRemaining, bytesRemaining, (SocketFlags)flags);
                    }
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("sendto(string[, flags], address) -> bytes_sent\n\n"
                + "Send data to the remote socket. The socket does not need to be connected to a\n"
                + "remote socket since the address is specified in the call to sendto(). Returns\n"
                + "the number of bytes sent to the remote socket.\n"
                + "\n"
                + "Blocking sockets will block until the all of the bytes in the buffer are sent.\n"
                + "Since a nonblocking Socket completes immediately, it might not send all of the\n"
                + "bytes in the buffer. It is your application's responsibility to keep track of\n"
                + "the number of bytes sent and to retry the operation until the application sends\n"
                + "all of the bytes in the buffer.\n"
                + "\n"
                + "Note that there is no guarantee that the data you send will appear on the\n"
                + "network immediately. To increase network efficiency, the underlying system may\n"
                + "delay transmission until a significant amount of outgoing data is collected. A\n"
                + "successful completion of the Send method means that the underlying system has\n"
                + "had room to buffer your data for a network send"
                )]
            public int sendto([NotNone] Bytes data, int flags, [NotNone] PythonTuple address) {
                return sendtoWorker(data.UnsafeByteArray, flags, address);
            }

            public int sendto([NotNone] IBufferProtocol data, int flags, [NotNone] PythonTuple address) {
                using IPythonBuffer buffer = data.GetBuffer();
                return sendtoWorker(buffer.AsUnsafeArray() ?? buffer.ToArray(), flags, address);
            }

            [Documentation("")]
            public int sendto([NotNone] Bytes data, [NotNone] PythonTuple address)
                => sendto(data, 0, address);

            public int sendto([NotNone] IBufferProtocol data, [NotNone] PythonTuple address)
                => sendto(data, 0, address);

            private int sendtoWorker(byte[] buffer, int flags, PythonTuple address) {
                EndPoint remoteEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    return _socket.SendTo(buffer, (SocketFlags)flags, remoteEP);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("setblocking(flag) -> None\n\n"
                + "Set the blocking mode of the socket. If flag is 0, the socket will be set to\n"
                + "non-blocking mode; otherwise, it will be set to blocking mode. If the socket is\n"
                + "in blocking mode, and a method is called (such as send() or recv() which does\n"
                + "not complete immediately, the caller will block execution until the requested\n"
                + "operation completes. In non-blocking mode, a socket.timeout exception would\n"
                + "would be raised in this case.\n"
                + "\n"
                + "Note that changing blocking mode also affects the timeout setting:\n"
                + "setblocking(0) is equivalent to settimeout(0), and setblocking(1) is equivalent\n"
                + "to settimeout(None)."
                )]
            public void setblocking(int shouldBlock) {
                if (shouldBlock == 0) {
                    settimeout(0);
                } else {
                    settimeout(null);
                }
            }

            [Documentation("settimeout(value) -> None\n\n"
                + "Set a timeout on blocking socket methods. value may be either None or a\n"
                + "non-negative float, with one of the following meanings:\n"
                + " - None: disable timeouts and block indefinitely"
                + " - 0.0: don't block at all (return immediately if the operation can be\n"
                + "   completed; raise socket.error otherwise)\n"
                + " - float > 0.0: block for up to the specified number of seconds; raise\n"
                + "   socket.timeout if the operation cannot be completed in time\n"
                + "\n"
                + "settimeout(None) is equivalent to setblocking(1), and settimeout(0.0) is\n"
                + "equivalent to setblocking(0)."
                + "\n"
                + "If the timeout is non-zero and is less than 0.5, it will be set to 0.5. This\n"
                + "limitation is specific to IronPython.\n"
                )]
            // NOTE: The above IronPython specific timeout behavior is due to the underlying
            // .Net Socket.SendTimeout behavior and is outside of our control.
            public void settimeout(object? timeout) {
                bool blocking = true;
                int timeoutVal = 0;
                if (timeout is not null) {
                    double seconds;
                    seconds = Converter.ConvertToDouble(timeout);
                    if (seconds < 0) {
                        throw PythonOps.ValueError("Timeout value out of range");
                    }
                    blocking = seconds > 0; // 0 timeout means non-blocking mode
                    timeoutVal = (int)(seconds * MillisecondsPerSecond);
                }

                try {
                    _socket.Blocking = blocking;
                    _socket.SendTimeout = timeoutVal;
                    _socket.ReceiveTimeout = _socket.SendTimeout;
                    _timeout = timeoutVal;
                } catch (Exception ex) {
                    throw MakeException(_context, ex);
                }
            }

            [Documentation("gettimeout() -> value\n\n"
                + "Return the timeout duration in seconds for this socket as a float. If no\n"
                + "timeout is set, return None. For more details on timeouts and blocking, see the\n"
                + "Python socket module documentation."
                )]
            public object? gettimeout() {
                try {
                    if (_socket.Blocking && _socket.SendTimeout == 0) {
                        return null;
                    } else {
                        return (double)_socket.SendTimeout / MillisecondsPerSecond;
                    }
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("setsockopt(level, optname[, value]) -> None\n\n"
                + "Set the value of a socket option. level is one of the SOL_* constants defined\n"
                + "in this module, and optname is one of the SO_* constants. value may be either\n"
                + "an integer or a string containing a binary structure. The caller is responsible\n"
                + "for properly encoding the byte string."
                )]
            public void setsockopt(int optionLevel, int optionName, object? value) {
                SocketOptionLevel level = (SocketOptionLevel)Enum.ToObject(typeof(SocketOptionLevel), optionLevel);
                if (!Enum.IsDefined(typeof(SocketOptionLevel), level)) {
                    throw MakeException(_context, new SocketException((int)SocketError.InvalidArgument));
                }
                SocketOptionName name = (SocketOptionName)Enum.ToObject(typeof(SocketOptionName), optionName);
                if (!Enum.IsDefined(typeof(SocketOptionName), name)) {
                    throw MakeException(_context, new SocketException((int)SocketError.ProtocolOption));
                }

                try {
                    int intValue;
                    if (Converter.TryConvertToInt32(value, out intValue)) {
                        _socket.SetSocketOption(level, name, intValue);
                        return;
                    }

                    if (value is IBufferProtocol bp) {
                        using IPythonBuffer buf = bp.GetBuffer();
                        _socket.SetSocketOption(level, name, buf.AsReadOnlySpan().ToArray());
                        return;
                    }

                    throw PythonOps.TypeError("a bytes-like object is required, not '{0}'", PythonOps.GetPythonTypeName(value));
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("shutdown() -> None\n\n"
                + "Return the timeout duration in seconds for this socket as a float. If no\n"
                + "timeout is set, return None. For more details on timeouts and blocking, see the\n"
                + "Python socket module documentation."
                )]
            public void shutdown(int how) {
                SocketShutdown howValue = (SocketShutdown)Enum.ToObject(typeof(SocketShutdown), how);
                if (!Enum.IsDefined(typeof(SocketShutdown), howValue)) {
                    throw MakeException(_context, new SocketException((int)SocketError.InvalidArgument));
                }
                try {
                    _socket.Shutdown(howValue);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            public int family => (int)_socket.AddressFamily;

            public int type => (int)_socket.SocketType;

            public int proto => (int)_socket.ProtocolType;

            public int ioctl(BigInteger cmd, object? option) {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw PythonOps.ValueError(string.Format("invalid ioctl command {0}", cmd));

                if (cmd == SIO_KEEPALIVE_VALS) {
                    if (!(option is PythonTuple))
                        throw PythonOps.TypeError("option must be 3-item sequence, not int");
                    var tOption = (PythonTuple)option;
                    if (tOption.Count != 3)
                        throw PythonOps.TypeError(string.Format("option must be sequence of length 3, not {0}", tOption.Count));
                    //(onoff, timeout, interval)
                    if ((!(tOption[0] is int)) && (!(tOption[1] is int)) && (!(tOption[2] is int)))
                        throw PythonOps.TypeError("option integer required");

                    int onoff = (int)tOption[0]!;
                    int timeout = (int)tOption[1]!;
                    int interval = (int)tOption[2]!;
                    int size = sizeof(UInt32);
                    byte[] inArray = new byte[size * 3];
                    Array.Copy(BitConverter.GetBytes(onoff), 0, inArray, 0, size);
                    Array.Copy(BitConverter.GetBytes(timeout), 0, inArray, size, size);
                    Array.Copy(BitConverter.GetBytes(size), 0, inArray, size * 2, size);
                    return _socket.IOControl((IOControlCode)(long)cmd, inArray, null);
                } else if (cmd == SIO_RCVALL) {
                    if (!(option is int))
                        throw PythonOps.TypeError("option integer required");

                    return _socket.IOControl((IOControlCode)(long)cmd, BitConverter.GetBytes((int)option), null);
                } else {
                    throw PythonOps.ValueError(string.Format("invalid ioctl command {0}", cmd));
                }
            }

            public string __repr__(CodeContext context) {
                try {
                    return $"<socket object, fd={fileno()}, family={family}, type={type}, proto={proto}>";
                } catch {
                    return "<socket object, fd=-1, family=0, type=0, proto=0>";
                }
            }

            /// <summary>
            /// Return the internal System.Net.Sockets.Socket socket object associated with the given
            /// handle (as returned by GetHandle()), or null if no corresponding socket exists. This is
            /// primarily intended to be used by other modules (such as select) that implement
            /// networking primitives. User code should not normally need to call this function.
            /// </summary>
            internal static Socket? HandleToSocket(Int64 handle) {
                lock (_handleToSocket) {
                    if (_handleToSocket.TryGetValue((IntPtr)handle, out WeakReference? weakref)) {
                        return weakref.Target as Socket;
                    }
                }
                return null;
            }

            #endregion

            #region IWeakReferenceable Implementation

            WeakRefTracker? IWeakReferenceable.GetWeakRef() {
                return _weakRefTracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _weakRefTracker = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _weakRefTracker = value;
            }

            #endregion

            #region Private Implementation

            /// <summary>
            /// Create a Python socket object from an existing .NET socket object
            /// (like one returned from Socket.Accept())
            /// </summary>
            private socket(CodeContext/*!*/ context, Socket socket) {
                Initialize(context, socket);
            }

            /// <summary>
            /// Perform initialization common to all constructors
            /// </summary>
            [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_socket), nameof(_context))]
            private void Initialize(CodeContext context, Socket socket) {
                _socket = socket;
                _context = context;
                int? defaultTimeout = GetDefaultTimeout(context);
                if (defaultTimeout == null) {
                    settimeout(null);
                } else {
                    settimeout((double)defaultTimeout / MillisecondsPerSecond);
                }

                lock (_handleToSocket) {
                    _handleToSocket[socket.Handle] = new WeakReference(socket);
                }
            }

            #endregion
        }

        #endregion

        #region Fields

        private const string AnyAddrToken = "";
        private const string BroadcastAddrToken = "<broadcast>";
        private const string LocalhostAddrToken = "";
        private const int IPv4AddrBytes = 4;
        private const int IPv6AddrBytes = 16;
        private const double MillisecondsPerSecond = 1000.0;

        #endregion

        #region Public API

        [Documentation("")]
        public static PythonList getaddrinfo(
            CodeContext/*!*/ context,
            string? host,
            object? port,
            int family = (int)AddressFamily.Unspecified,
            int socktype = 0,
            int proto = (int)ProtocolType.IP,
            int flags = (int)SocketFlags.None
        ) {
            var numericPort = port switch {
                null => 0,
                int i => i,
                BigInteger bi => (int)bi,
                Extensible<BigInteger> bi => (int)bi.Value,
                Bytes b => ParsePort(context, b.MakeString()),
                string s => ParsePort(context, s),
                ExtensibleString es => ParsePort(context, es.Value),
                _ => throw MakeGaiException(context, EAI_NONAME),
            };

            static int ParsePort(CodeContext context, string port) {
                if (int.TryParse(port, out var numericPort)) return numericPort;
                try {
                    return getservbyname(context, port);
                } catch {
                    throw MakeGaiException(context, EAI_NONAME);
                }
            }

            if (socktype != 0) {
                // we just use this to validate; socketType isn't actually used
                System.Net.Sockets.SocketType socketType = (System.Net.Sockets.SocketType)Enum.ToObject(typeof(System.Net.Sockets.SocketType), socktype);
                if (socketType == System.Net.Sockets.SocketType.Unknown || !Enum.IsDefined(typeof(System.Net.Sockets.SocketType), socketType)) {
                    throw MakeGaiException(context, EAI_BADHINTS);
                }
            }

            AddressFamily addressFamily = (AddressFamily)Enum.ToObject(typeof(AddressFamily), family);
            if (!Enum.IsDefined(typeof(AddressFamily), addressFamily)) {
                throw MakeGaiException(context, EAI_FAMILY);
            }

            // Again, we just validate, but don't actually use protocolType
            Enum.ToObject(typeof(ProtocolType), proto);

            if (host == null)
                host = "localhost";

            IPAddress[] ips = HostToAddresses(context, host, addressFamily);

            PythonList results = new PythonList();

            foreach (IPAddress ip in ips) {
                results.append(PythonTuple.MakeTuple(
                    (int)ip.AddressFamily,
                    socktype,
                    proto,
                    "",
                    EndPointToTuple(new IPEndPoint(ip, numericPort))
                ));
            }

            return results;
        }

        [Documentation("")]
        public static PythonList getaddrinfo(
            CodeContext/*!*/ context,
            [NotNone] Bytes host,
            object? port,
            int family = (int)AddressFamily.Unspecified,
            int socktype = 0,
            int proto = (int)ProtocolType.IP,
            int flags = (int)SocketFlags.None
        ) => getaddrinfo(context, host.MakeString(), port, family, socktype, proto, flags);

        private static PythonType gaierror(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("socketgaierror");
        }

        private static string getfqdn() {
            string host = LocalhostAddrToken;
            host = host.Trim();
            if (host == string.Empty || host == "0.0.0.0") {
                host = gethostname();
            }

            if (host == BroadcastAddrToken) {
                return host;
            }
            try {
                IPHostEntry hostEntry = Dns.GetHostEntry(host);
                if (hostEntry.HostName.Contains(".")) {
                    return hostEntry.HostName;
                } else {
                    foreach (string addr in hostEntry.Aliases) {
                        if (addr.Contains(".")) {
                            return addr;
                        }
                    }
                }
            } catch (SocketException) {
                // ignore and return host below
            }
            // seems to match CPython behavior, although docs say gethostname() should be returned
            return host;
        }

        [Documentation("gethostbyname(hostname) -> ip address\n\n"
            + "Return the string IPv4 address associated with the given hostname (e.g.\n"
            + "'10.10.0.1'). The hostname is returned as-is if it an IPv4 address. The empty\n"
            + "string is treated as the local host.\n"
            + "\n"
            + "gethostbyname() doesn't support IPv6; for IPv4/IPv6 support, use getaddrinfo()."
            )]
        public static string gethostbyname(CodeContext/*!*/ context, [NotNone] string host) {
            return HostToAddress(context, host, AddressFamily.InterNetwork).ToString();
        }

        [Documentation("gethostbyname_ex(hostname) -> (hostname, aliases, ip_addresses)\n\n"
            + "Return the real host name, a list of aliases, and a list of IP addresses\n"
            + "associated with the given hostname. If the hostname is an IPv4 address, the\n"
            + "tuple ([hostname, [], [hostname]) is returned without doing a DNS lookup.\n"
            + "\n"
            + "gethostbyname_ex() doesn't support IPv6; for IPv4/IPv6 support, use\n"
            + "getaddrinfo()."
            )]
        public static PythonTuple gethostbyname_ex(CodeContext/*!*/ context, [NotNone] string host) {
            string hostname;
            PythonList aliases;
            PythonList ips = new PythonList();

            if (IPAddress.TryParse(host, out IPAddress? addr)) {
                if (AddressFamily.InterNetwork == addr.AddressFamily) {
                    hostname = host;
                    aliases = new PythonList(0);
                    ips.append(host);
                } else {
                    throw MakeGaiException(context, EAI_ADDRFAMILY);
                }
            } else {
                IPHostEntry hostEntry;
                try {
                    hostEntry = Dns.GetHostEntry(host);
                } catch (SocketException ex) {
                    throw MakeGaiException(context, ex);
                }
                hostname = hostEntry.HostName;
                aliases = PythonOps.MakeList(hostEntry.Aliases);
                foreach (IPAddress ip in hostEntry.AddressList) {
                    if (AddressFamily.InterNetwork == ip.AddressFamily) {
                        ips.append(ip.ToString());
                    }
                }
                if (ips.Count == 0) {
                    throw MakeGaiException(context, EAI_NONAME);
                }
            }

            return PythonTuple.MakeTuple(hostname, aliases, ips);
        }

        [Documentation("gethostname() -> hostname\nReturn this machine's hostname")]
        public static string gethostname() {
            return Dns.GetHostName();
        }

        [Documentation("gethostbyaddr(host) -> (hostname, aliases, ipaddrs)\n\n"
            + "Return a tuple of (primary hostname, alias hostnames, ip addresses). host may\n"
            + "be either a hostname or an IP address."
            )]
        public static object gethostbyaddr(CodeContext/*!*/ context, [NotNone] string host) {
            if (host == "") {
                host = gethostname();
            }
            // This conversion seems to match CPython behavior
            host = gethostbyname(context, host);

            IPAddress[] ips;
            IPHostEntry hostEntry;
            try {
                ips = Dns.GetHostAddresses(host);
                hostEntry = Dns.GetHostEntry(host);
            } catch (Exception e) {
                throw MakeException(context, e);
            }

            PythonList ipStrings = new PythonList();
            foreach (IPAddress ip in ips) {
                ipStrings.append(ip.ToString());
            }

            return PythonTuple.MakeTuple(hostEntry.HostName, PythonOps.MakeList(hostEntry.Aliases), ipStrings);
        }

        [Documentation("getnameinfo(socketaddr, flags) -> (host, port)\n"
            + "Given a socket address, the return a tuple of the corresponding hostname and\n"
            + "port. Available flags:\n"
            + " - NI_NOFQDN: Return only the hostname part of the domain name for hosts on the\n"
            + "   same domain as the executing machine.\n"
            + " - NI_NUMERICHOST: return the numeric form of the host (e.g. '127.0.0.1' or\n"
            + "   '::1' rather than 'localhost').\n"
            + " - NI_NAMEREQD: Raise an error if the hostname cannot be looked up.\n"
            + " - NI_NUMERICSERV: Return string containing the numeric form of the port (e.g.\n"
            + "   '80' rather than 'http'). This flag is required (see below).\n"
            + " - NI_DGRAM: Silently ignored (see below).\n"
            + "\n"
            )]
        public static object getnameinfo(CodeContext/*!*/ context, [NotNone] PythonTuple socketAddr, int flags) {
            var socketAddrLen = socketAddr.__len__();
            if (socketAddrLen < 2 || socketAddrLen > 4) {
                throw PythonOps.TypeError("socket address must be a 2-tuple (IPv4 or IPv6) or 4-tuple (IPv6)");
            }

            string host = Converter.ConvertToString(socketAddr[0]);
            if (host == null) throw PythonOps.TypeError("argument 1 must be string");
            int port = 0;
            try {
                port = (int)socketAddr[1]!;
            } catch (InvalidCastException) {
                throw PythonOps.TypeError("an integer is required");
            }

            if (socketAddrLen > 2) {
                if (!Converter.TryConvertToBigInteger(socketAddr[2], out var flowinfo)) {
                    throw PythonOps.TypeError("an integer is required");
                }
                if (flowinfo < 0 || flowinfo > 0xfffff) {
                    throw PythonOps.OverflowError($"{nameof(getnameinfo)}(): flowinfo must be 0-1048575.");
                }
            }

            string resultHost;
            string resultPort;

            // Host
            IList<IPAddress> addrs;
            try {
                addrs = HostToAddresses(context, host, AddressFamily.InterNetwork);
            } catch (IndexOutOfRangeException) {
                throw MakeGaiException(context, EAI_NODATA);
            }

            if (addrs.Count > 1) {
                // ignore non-IPV4 addresses
                List<IPAddress> newAddrs = new List<IPAddress>(addrs.Count);
                foreach (IPAddress addr in addrs) {
                    if (addr.AddressFamily == AddressFamily.InterNetwork) {
                        newAddrs.Add(addr);
                    }
                }
                if (newAddrs.Count > 1) {
                    throw PythonExceptions.CreateThrowable(error, "sockaddr resolved to multiple addresses");
                }
                addrs = newAddrs;
            }

            if (addrs.Count < 1) {
                throw MakeGaiException(context, EAI_NODATA);
            }

            IPHostEntry hostEntry;
            try {
                hostEntry = Dns.GetHostEntry(addrs[0]);
            } catch (SocketException ex) {
                throw MakeGaiException(context, ex);
            }
            if ((flags & (int)NI_NUMERICHOST) != 0) {
                resultHost = addrs[0].ToString();
            } else if ((flags & (int)NI_NOFQDN) != 0) {
                resultHost = RemoveLocalDomain(hostEntry.HostName);
            } else {
                resultHost = hostEntry.HostName;
            }

            // Port
            if ((flags & (int)NI_NUMERICSERV) == 0) {
                //call the servbyport to translate port if not just use the port.ToString as the result
                try {
                    resultPort = getservbyport(context, port);
                } catch {
                    resultPort = port.ToString();
                }
                flags = flags | (int)NI_NUMERICSERV;
            } else
                resultPort = port.ToString();

            return PythonTuple.MakeTuple(resultHost, resultPort);
        }

        [Documentation("getprotobyname(protoname) -> integer proto\n\n"
            + "Given a string protocol name (e.g. \"udp\"), return the associated integer\n"
            + "protocol number, suitable for passing to socket(). The name is case\n"
            + "insensitive.\n"
            + "\n"
            + "Raises socket.error if no protocol number can be found."
            )]
        public static object getprotobyname(CodeContext/*!*/ context, [NotNone] string protocolName) {
            switch (protocolName.ToLowerInvariant()) {
                case "ah": return IPPROTO_AH;
                case "esp": return IPPROTO_ESP;
                case "dstopts": return IPPROTO_DSTOPTS;
                case "fragment": return IPPROTO_FRAGMENT;
                case "ggp": return IPPROTO_GGP;
                case "icmp": return IPPROTO_ICMP;
                case "icmpv6": return IPPROTO_ICMPV6;
                case "ip": return IPPROTO_IP;
                case "ipv4": return IPPROTO_IPV4;
                case "ipv6": return IPPROTO_IPV6;
                case "nd": return IPPROTO_ND;
                case "none": return IPPROTO_NONE;
                case "pup": return IPPROTO_PUP;
                case "raw": return IPPROTO_RAW;
                case "routing": return IPPROTO_ROUTING;
                case "tcp": return IPPROTO_TCP;
                case "udp": return IPPROTO_UDP;
                default:
                    throw PythonExceptions.CreateThrowable(error, "protocol not found");
            }
        }

        [Documentation("getservbyname(service_name[, protocol_name]) -> port\n\n"
            + "Return a port number from a service name and protocol name.\n"
            + "The optional protocol name, if given, should be 'tcp' or 'udp',\n"
            + "otherwise any protocol will match."
            )]
        public static int getservbyname(CodeContext/*!*/ context, [NotNone] string serviceName)
            => getservbyname(context, serviceName, null!);

        [Documentation("")]
        public static int getservbyname(CodeContext/*!*/ context, [NotNone] string serviceName, [NotNone] string protocolName) {
            if (protocolName != null) {
                protocolName = protocolName.ToLowerInvariant();
                if (protocolName != "udp" && protocolName != "tcp")
                    throw PythonExceptions.CreateThrowable(error, "service/proto not found");
            }

            // try the pinvoke call if it fails fall back to hard coded swtich statement
            try {
                return SocketUtil.GetServiceByName(serviceName, protocolName);
            } catch { }


            switch (serviceName.ToLowerInvariant()) {
                case "echo": return 7;
                case "daytime": return 13;
                case "ftp-data": return 20;
                case "ftp": if (protocolName == null || protocolName == "tcp") return 21; else goto default;
                case "ssh": return 22;
                case "telnet": return 23;
                case "smtp": if (protocolName == null || protocolName == "tcp") return 25; else goto default;
                case "time": return 37;
                case "rlp": return 39;
                case "nameserver": return 42;
                case "nicname": if (protocolName == null || protocolName == "tcp") return 43; else goto default;
                case "domain": return 53;
                case "bootps": if (protocolName == null || protocolName == "udp") return 67; else goto default;
                case "bootpc": if (protocolName == null || protocolName == "udp") return 68; else goto default;
                case "tftp": if (protocolName == null || protocolName == "udp") return 69; else goto default;
                case "http": if (protocolName == null || protocolName == "tcp") return 80; else goto default;
                case "kerberos": return 88;
                case "rtelnet": if (protocolName == null || protocolName == "tcp") return 107; else goto default;
                case "pop2": if (protocolName == null || protocolName == "tcp") return 109; else goto default;
                case "pop3": if (protocolName == null || protocolName == "tcp") return 110; else goto default;
                case "nntp": if (protocolName == null || protocolName == "tcp") return 119; else goto default;
                case "ntp": if (protocolName == null || protocolName == "udp") return 123; else goto default;
                case "imap": if (protocolName == null || protocolName == "tcp") return 143; else goto default;
                case "snmp": if (protocolName == null || protocolName == "udp") return 161; else goto default;
                case "snmptrap": if (protocolName == null || protocolName == "udp") return 162; else goto default;
                case "ldap": if (protocolName == null || protocolName == "tcp") return 389; else goto default;
                case "https": if (protocolName == null || protocolName == "tcp") return 430; else goto default;
                case "dhcpv6-client": return 546;
                case "dhcpv6-server": return 547;
                case "rtsp": return 554;
                default:
                    throw PythonExceptions.CreateThrowable(error, "service/proto not found");
            }
        }

        [Documentation("getservbyport(port[, protocol_name]) -> service_name\n\n"
            + "Return a service name from a port number and protocol name.\n"
            + "The optional protocol name, if given, should be 'tcp' or 'udp',\n"
            + "otherwise any protocol will match."
            )]
        public static string getservbyport(CodeContext/*!*/ context, int port)
            => getservbyport(context, port, null!);

        [Documentation("")]
        public static string getservbyport(CodeContext/*!*/ context, int port, [NotNone] string protocolName) {
            if (port < 0 || port > 65535)
                throw PythonOps.OverflowError("getservbyport: port must be 0-65535.");

            if (protocolName != null) {
                protocolName = protocolName.ToLowerInvariant();
                if (protocolName != "udp" && protocolName != "tcp")
                    throw PythonExceptions.CreateThrowable(error, "port/proto not found");
            }

            // try the pinvoke call if it fails fall back to hard coded swtich statement
            try {
                return SocketUtil.GetServiceByPort((ushort)port, protocolName);
            } catch { }

            switch (port) {
                case 7: return "echo";
                case 13: return "daytime";
                case 20: return "ftp-data";
                case 21: if (protocolName == null || protocolName == "tcp") return "ftp"; else goto default;
                case 22: return "ssh";
                case 23: return "telnet";
                case 25: if (protocolName == null || protocolName == "tcp") return "smtp"; else goto default;
                case 37: return "time";
                case 39: return "rlp";
                case 42: return "nameserver";
                case 43: if (protocolName == null || protocolName == "tcp") return "nicname"; else goto default;
                case 53: return "domain";
                case 67: if (protocolName == null || protocolName == "udp") return "bootps"; else goto default;
                case 68: if (protocolName == null || protocolName == "udp") return "bootpc"; else goto default;
                case 69: if (protocolName == null || protocolName == "udp") return "tftp"; else goto default;
                case 80: if (protocolName == null || protocolName == "tcp") return "http"; else goto default;
                case 88: return "kerberos";
                case 107: if (protocolName == null || protocolName == "tcp") return "rtelnet"; else goto default;
                case 109: if (protocolName == null || protocolName == "tcp") return "pop2"; else goto default;
                case 110: if (protocolName == null || protocolName == "tcp") return "pop3"; else goto default;
                case 119: if (protocolName == null || protocolName == "tcp") return "nntp"; else goto default;
                case 123: if (protocolName == null || protocolName == "udp") return "ntp"; else goto default;
                case 143: if (protocolName == null || protocolName == "tcp") return "imap"; else goto default;
                case 161: if (protocolName == null || protocolName == "udp") return "snmp"; else goto default;
                case 162: if (protocolName == null || protocolName == "udp") return "snmptrap"; else goto default;
                case 389: if (protocolName == null || protocolName == "tcp") return "ldap"; else goto default;
                case 430: if (protocolName == null || protocolName == "tcp") return "https"; else goto default;
                case 546: return "dhcpv6-client";
                case 547: return "dhcpv6-server";
                case 554: return "rtsp";
                default:
                    throw PythonExceptions.CreateThrowable(error, "port/proto not found");
            }
        }

        [Documentation("ntohl(x) -> integer\n\nConvert a 32-bit integer from network byte order to host byte order.")]
        public static object ntohl([NotNone] object x) {
            int res = IPAddress.NetworkToHostOrder(SignInsensitiveToInt32(x));

            if (res < 0) {
                return (BigInteger)(uint)res;
            } else {
                return res;
            }
        }

        [Documentation("ntohs(x) -> integer\n\nConvert a 16-bit integer from network byte order to host byte order.")]
        public static int ntohs([NotNone] object x) {
            return (int)(ushort)IPAddress.NetworkToHostOrder(SignInsensitiveToInt16(x));
        }

        [Documentation("htonl(x) -> integer\n\nConvert a 32bit integer from host byte order to network byte order.")]
        public static object htonl([NotNone] object x) {
            int res = IPAddress.HostToNetworkOrder(SignInsensitiveToInt32(x));

            if (res < 0) {
                return (BigInteger)(uint)res;
            } else {
                return res;
            }
        }

        [Documentation("htons(x) -> integer\n\nConvert a 16-bit integer from host byte order to network byte order.")]
        public static int htons([NotNone] object x) {
            return (int)(ushort)IPAddress.HostToNetworkOrder(SignInsensitiveToInt16(x));
        }

        /// <summary>
        /// Convert an object to a 32-bit integer. This adds two features to Converter.ToInt32:
        ///   1. Sign is ignored. For example, 0xffff0000 converts to 4294901760, where Convert.ToInt32
        ///      would throw because 0xffff0000 is less than zero.
        ///   2. Overflow exceptions are thrown. Converter.ToInt32 throws TypeError if x is
        ///      an integer, but is bigger than 32 bits. Instead, we throw OverflowException.
        /// </summary>
        private static int SignInsensitiveToInt32(object x) {
            BigInteger bigValue = Converter.ConvertToBigInteger(x);

            if (bigValue < 0) {
                throw PythonOps.OverflowError("can't convert negative number to unsigned long");
            } else if (bigValue <= int.MaxValue) {
                return (int)bigValue;
            } else {
                return (int)(uint)bigValue;
            }
        }

        /// <summary>
        /// Convert an object to a 16-bit integer. This adds two features to Converter.ToInt16:
        ///   1. Sign is ignored. For example, 0xff00 converts to 65280, where Convert.ToInt16
        ///      would throw because signed 0xff00 is -256.
        ///   2. Overflow exceptions are thrown. Converter.ToInt16 throws TypeError if x is
        ///      an integer, but is bigger than 16 bits. Instead, we throw OverflowException.
        /// </summary>
        private static short SignInsensitiveToInt16(object x) {
            BigInteger bigValue = Converter.ConvertToBigInteger(x);
            if (bigValue < 0) {
                throw PythonOps.OverflowError("can't convert negative number to unsigned long");
            } else if (bigValue <= short.MaxValue) {
                return (short)bigValue;
            } else {
                return (short)(ushort)bigValue;
            }
        }

        [Documentation("inet_pton(addr_family, ip_string) -> packed_ip\n\n"
            + "Convert an IP address (in string format, e.g. '127.0.0.1' or '::1') to a 32-bit\n"
            + "packed binary format, as 4-byte (IPv4) or 16-byte (IPv6) string. The return\n"
            + "format matches the format of the standard C library's in_addr or in6_addr\n"
            + "struct.\n"
            + "\n"
            + "If the address format is invalid, socket.error will be raised. Validity is\n"
            + "determined by the .NET System.Net.IPAddress.Parse() method.\n"
            + "\n"
            + "inet_pton() supports IPv4 and IPv6."
            )]
        public static Bytes inet_pton(CodeContext/*!*/ context, int addressFamily, [NotNone] string ipString) {
            if (addressFamily != (int)AddressFamily.InterNetwork && addressFamily != (int)AddressFamily.InterNetworkV6) {
                throw MakeException(context, new SocketException((int)SocketError.AddressFamilyNotSupported));
            }

            IPAddress ip;
            try {
                ip = IPAddress.Parse(ipString);
                if (addressFamily != (int)ip.AddressFamily) {
                    throw MakeException(context, new SocketException((int)SocketError.AddressFamilyNotSupported));
                }
            } catch (FormatException) {
                throw PythonExceptions.CreateThrowable(error, "illegal IP address passed to inet_pton");
            }
            return Bytes.Make(ip.GetAddressBytes());
        }

        [Documentation("inet_ntop(address_family, packed_ip) -> ip_string\n\n"
            + "Convert a packed IP address (a 4-byte [IPv4] or 16-byte [IPv6] string) to a\n"
            + "string IP address (e.g. '127.0.0.1' or '::1').\n"
            + "\n"
            + "The input format matches the format of the standard C library's in_addr or\n"
            + "in6_addr struct. If the input string is not exactly 4 bytes or 16 bytes,\n"
            + "socket.error will be raised.\n"
            + "\n"
            + "inet_ntop() supports IPv4 and IPv6."
            )]
        public static string inet_ntop(CodeContext/*!*/ context, int addressFamily, [NotNone] IBufferProtocol packedIP) {
            using var buffer = packedIP.GetBuffer();
            var span = buffer.AsReadOnlySpan();
            if (!(
                (span.Length == IPv4AddrBytes && addressFamily == (int)AddressFamily.InterNetwork)
                || (span.Length == IPv6AddrBytes && addressFamily == (int)AddressFamily.InterNetworkV6)
            )) {
                throw PythonOps.ValueError("invalid length of packed IP address string");
            }
            if (addressFamily == (int)AddressFamily.InterNetworkV6) {
                return IPv6BytesToColonHex(span);
            }
#if NETCOREAPP
            return new IPAddress(span).ToString();
#else
            return new IPAddress(buffer.AsUnsafeArray() ?? buffer.ToArray()).ToString();
#endif
        }

        [Documentation("inet_aton(ip_string) -> packed_ip\n"
            + "Convert an IP address (in string dotted quad format, e.g. '127.0.0.1') to a\n"
            + "32-bit packed binary format, as four-character string. The return format\n"
            + "matches the format of the standard C library's in_addr struct.\n"
            + "\n"
            + "If the address format is invalid, socket.error will be raised. Validity is\n"
            + "determined by the .NET System.Net.IPAddress.Parse() method.\n"
            + "\n"
            + "inet_aton() supports only IPv4."
            )]
        public static Bytes inet_aton(CodeContext/*!*/ context, [NotNone] string ipString) {
            return inet_pton(context, (int)AddressFamily.InterNetwork, ipString);
        }

        [Documentation("inet_ntoa(packed_ip) -> ip_string\n\n"
            + "Convert a packed IP address (a 4-byte string) to a string IP address (in dotted\n"
            + "quad format, e.g. '127.0.0.1'). The input format matches the format of the\n"
            + "standard C library's in_addr struct.\n"
            + "\n"
            + "If the input string is not exactly 4 bytes, socket.error will be raised.\n"
            + "\n"
            + "inet_ntoa() supports only IPv4."
            )]
        public static string inet_ntoa(CodeContext/*!*/ context, [NotNone] IBufferProtocol packedIP) {
            return inet_ntop(context, (int)AddressFamily.InterNetwork, packedIP);
        }

        [Documentation("getdefaulttimeout() -> timeout\n\n"
            + "Return the default timeout for new socket objects in seconds as a float. A\n"
            + "value of None means that sockets have no timeout and begin in blocking mode.\n"
            + "The default value when the module is imported is None."
            )]
        public static object? getdefaulttimeout(CodeContext/*!*/ context) {
            int? defaultTimeout = GetDefaultTimeout(context);
            if (defaultTimeout == null) {
                return null;
            } else {
                return (double)(defaultTimeout.Value) / MillisecondsPerSecond;
            }
        }

        [Documentation("setdefaulttimeout(timeout) -> None\n\n"
            + "Set the default timeout for new socket objects. timeout must be either None,\n"
            + "meaning that sockets have no timeout and start in blocking mode, or a\n"
            + "non-negative float that specifies the default timeout in seconds."
            )]
        public static void setdefaulttimeout(CodeContext/*!*/ context, object? timeout) {
            if (timeout == null) {
                SetDefaultTimeout(context, null);
            } else {
                double seconds;
                seconds = Converter.ConvertToDouble(timeout);
                if (seconds < 0) {
                    throw PythonOps.ValueError("a non-negative float is required");
                }
                SetDefaultTimeout(context, (int)(seconds * MillisecondsPerSecond));
            }
        }

        #endregion

        #region Exported constants

        public const int AF_APPLETALK = (int)AddressFamily.AppleTalk;
        public const int AF_DECnet = (int)AddressFamily.DecNet;
        public const int AF_INET = (int)AddressFamily.InterNetwork;
        public const int AF_INET6 = (int)AddressFamily.InterNetworkV6;
        public const int AF_IPX = (int)AddressFamily.Ipx;
        public const int AF_IRDA = (int)AddressFamily.Irda;
        public const int AF_SNA = (int)AddressFamily.Sna;
        // TODO: adding this causes test failures since bind does not support it
        //[PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        //public const int AF_UNIX = (int)AddressFamily.Unix;
        public const int AF_UNSPEC = (int)AddressFamily.Unspecified;
        public const int AI_ADDRCONFIG = (int)0x400;
        public const int AI_ALL = (int)0x100;
        public const int AI_CANONNAME = (int)0x2;
        public const int AI_NUMERICHOST = (int)0x4;
        public const int AI_NUMERICSERV = (int)0x8;
        public const int AI_PASSIVE = (int)0x1;
        public const int AI_V4MAPPED = (int)0x800;

        // EAI_* values are error codes of gaierror.
        // Linux: man 3 getaddrinfo
        // Darwin: man 3 gai_strerror
        // The specified network host does not have any network addresses in the requested address family.
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int EAI_ADDRFAMILY  => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 1 : -9;
        // The name server returned a temporary failure indication. Try again later.
        public static int EAI_AGAIN       => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 2 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -3 : (int)SocketError.TryAgain;
        // hints.ai_flags contains invalid flags.
        public static int EAI_BADFLAGS    => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 3 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -1 : (int)SocketError.InvalidArgument;
        // The name server returned a permanent failure indication.
        public static int EAI_FAIL        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 4 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -4 : (int)SocketError.NoRecovery;
        // The requested address family is not supported.
        public static int EAI_FAMILY      => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 5 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -6 : (int)SocketError.AddressFamilyNotSupported;
        // Out of memory.
        public static int EAI_MEMORY      => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 6 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -10 : (int)SocketError.NoBufferSpaceAvailable;
        // The specified network host exists, but does not have any network addresses defined.
        public static int EAI_NODATA      => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 7 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -5 : (int)SocketError.HostNotFound; // not SocketError.NoData, like you would think
        // The node or service is not known.
        public static int EAI_NONAME      => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -2 : (int)SocketError.HostNotFound;
        // The requested service is not available for the requested socket type.
        public static int EAI_SERVICE     => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 9 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -8 : (int)SocketError.TypeNotFound;
        // The requested socket type is not supported.
        public static int EAI_SOCKTYPE    => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 10 : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? -7 : (int)SocketError.SocketNotSupported;
        // Other system error, check errno for details.
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int EAI_SYSTEM      => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 11 : -11;
        // Invalid value for hints.
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.Unix)]
        public static int EAI_BADHINTS    => 12;
        // Resolved protocol is unknown.
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.Unix)]
        public static int EAI_PROTOCOL    => 13;
        // Argument buffer overflow.
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int EAI_OVERFLOW    => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 14 : -12;
        // Maximum value of EAI_* erors plus one.
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.Unix)]
        public static int EAI_MAX         => 15;

        public const int INADDR_ALLHOSTS_GROUP = unchecked((int)0xe0000001);
        public const int INADDR_ANY = (int)0x00000000;
        public const int INADDR_BROADCAST = unchecked((int)0xFFFFFFFF);
        public const int INADDR_LOOPBACK = unchecked((int)0x7F000001);
        public const int INADDR_MAX_LOCAL_GROUP = unchecked((int)0xe00000FF);
        public const int INADDR_NONE = unchecked((int)0xFFFFFFFF);
        public const int INADDR_UNSPEC_GROUP = unchecked((int)0xE0000000);
        public const int IPPORT_RESERVED = 1024;
        public const int IPPORT_USERRESERVED = 5000;
        public const int IPPROTO_AH = (int)ProtocolType.IPSecAuthenticationHeader;
        public const int IPPROTO_DSTOPTS = (int)ProtocolType.IPv6DestinationOptions;
        public const int IPPROTO_ESP = (int)ProtocolType.IPSecEncapsulatingSecurityPayload;
        public const int IPPROTO_FRAGMENT = (int)ProtocolType.IPv6FragmentHeader;
        public const int IPPROTO_GGP = (int)ProtocolType.Ggp;
        public const int IPPROTO_HOPOPTS = (int)ProtocolType.IPv6HopByHopOptions;
        public const int IPPROTO_ICMP = (int)ProtocolType.Icmp;
        public const int IPPROTO_ICMPV6 = (int)ProtocolType.IcmpV6;
        public const int IPPROTO_IDP = (int)ProtocolType.Idp;
        public const int IPPROTO_IGMP = (int)ProtocolType.Igmp;
        public const int IPPROTO_IP = (int)ProtocolType.IP;
        public const int IPPROTO_IPV4 = (int)ProtocolType.IPv4;
        public const int IPPROTO_IPV6 = (int)ProtocolType.IPv6;
        public const int IPPROTO_MAX = 256;
        public const int IPPROTO_ND = (int)ProtocolType.ND;
        public const int IPPROTO_NONE = (int)ProtocolType.IPv6NoNextHeader;
        public const int IPPROTO_PUP = (int)ProtocolType.Pup;
        public const int IPPROTO_RAW = (int)ProtocolType.Raw;
        public const int IPPROTO_ROUTING = (int)ProtocolType.IPv6RoutingHeader;
        public const int IPPROTO_TCP = (int)ProtocolType.Tcp;
        public const int IPPROTO_UDP = (int)ProtocolType.Udp;
        public const int IPV6_CHECKSUM = 26;
        public const int IPV6_HOPLIMIT = (int)SocketOptionName.HopLimit;
        public const int IPV6_HOPOPTS = 1;
        public const int IPV6_JOIN_GROUP = (int)SocketOptionName.AddMembership;
        public const int IPV6_LEAVE_GROUP = (int)SocketOptionName.DropMembership;
        public const int IPV6_MULTICAST_HOPS = (int)SocketOptionName.MulticastTimeToLive;
        public const int IPV6_MULTICAST_IF = (int)SocketOptionName.MulticastInterface;
        public const int IPV6_MULTICAST_LOOP = (int)SocketOptionName.MulticastLoopback;
        public const int IPV6_PKTINFO = (int)SocketOptionName.PacketInformation;
        public const int IPV6_RECVRTHDR = 38;
        public const int IPV6_TCLASS = 39;
        public const int IPV6_UNICAST_HOPS = (int)SocketOptionName.IpTimeToLive;
        public const int IPV6_V6ONLY = (int)SocketOptionName.IPv6Only;
        public const int IP_ADD_MEMBERSHIP = (int)SocketOptionName.AddMembership;
        public const int IP_DROP_MEMBERSHIP = (int)SocketOptionName.DropMembership;
        public const int IP_HDRINCL = (int)SocketOptionName.HeaderIncluded;
        public const int IP_MULTICAST_IF = (int)SocketOptionName.MulticastInterface;
        public const int IP_MULTICAST_LOOP = (int)SocketOptionName.MulticastLoopback;
        public const int IP_MULTICAST_TTL = (int)SocketOptionName.MulticastTimeToLive;
        public const int IP_OPTIONS = (int)SocketOptionName.IPOptions;
        public const int IP_TOS = (int)SocketOptionName.TypeOfService;
        public const int IP_TTL = (int)SocketOptionName.IpTimeToLive;
        public const int MSG_DONTROUTE = (int)SocketFlags.DontRoute;
        public const int MSG_MCAST = (int)SocketFlags.Multicast;
        public const int MSG_OOB = (int)SocketFlags.OutOfBand;
        public const int MSG_PEEK = (int)SocketFlags.Peek;
        public const int NI_DGRAM = 0x0010;
        public const int NI_MAXHOST = 1025;
        public const int NI_MAXSERV = 32;
        public const int NI_NAMEREQD = 0x0004;
        public const int NI_NOFQDN = 0x0001;
        public const int NI_NUMERICHOST = 0x0002;
        public const int NI_NUMERICSERV = 0x0008;
        public const int SHUT_RD = (int)SocketShutdown.Receive;
        public const int SHUT_RDWR = (int)SocketShutdown.Both;
        public const int SHUT_WR = (int)SocketShutdown.Send;
        public const int SOCK_DGRAM = (int)System.Net.Sockets.SocketType.Dgram;
        public const int SOCK_RAW = (int)System.Net.Sockets.SocketType.Raw;
        public const int SOCK_RDM = (int)System.Net.Sockets.SocketType.Rdm;
        public const int SOCK_SEQPACKET = (int)System.Net.Sockets.SocketType.Seqpacket;
        public const int SOCK_STREAM = (int)System.Net.Sockets.SocketType.Stream;
        public const int SOL_IP = (int)SocketOptionLevel.IP;
        public const int SOL_IPV6 = (int)SocketOptionLevel.IPv6; // not in CPython
        public const int SOL_SOCKET = (int)SocketOptionLevel.Socket;
        public const int SOL_TCP = (int)SocketOptionLevel.Tcp;
        public const int SOL_UDP = (int)SocketOptionLevel.Udp;
        public const int SOMAXCONN = (int)SocketOptionName.MaxConnections;
        public const int SO_ACCEPTCONN = (int)SocketOptionName.AcceptConnection;
        public const int SO_BROADCAST = (int)SocketOptionName.Broadcast;
        public const int SO_DEBUG = (int)SocketOptionName.Debug;
        public const int SO_DONTROUTE = (int)SocketOptionName.DontRoute;
        public const int SO_ERROR = (int)SocketOptionName.Error;
        public const int SO_EXCLUSIVEADDRUSE = (int)SocketOptionName.ExclusiveAddressUse;
        public const int SO_KEEPALIVE = (int)SocketOptionName.KeepAlive;
        public const int SO_LINGER = (int)SocketOptionName.Linger;
        public const int SO_OOBINLINE = (int)SocketOptionName.OutOfBandInline;
        public const int SO_RCVBUF = (int)SocketOptionName.ReceiveBuffer;
        public const int SO_RCVLOWAT = (int)SocketOptionName.ReceiveLowWater;
        public const int SO_RCVTIMEO = (int)SocketOptionName.ReceiveTimeout;
        public const int SO_REUSEADDR = (int)SocketOptionName.ReuseAddress;
        public const int SO_SNDBUF = (int)SocketOptionName.SendBuffer;
        public const int SO_SNDLOWAT = (int)SocketOptionName.SendLowWater;
        public const int SO_SNDTIMEO = (int)SocketOptionName.SendTimeout;
        public const int SO_TYPE = (int)SocketOptionName.Type;
        public const int SO_USELOOPBACK = (int)SocketOptionName.UseLoopback;
        public const int TCP_NODELAY = (int)SocketOptionName.NoDelay;

        // Windows only
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static readonly BigInteger SIO_RCVALL = (long)IOControlCode.ReceiveAll;

        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static readonly BigInteger SIO_KEEPALIVE_VALS = (long)IOControlCode.KeepAliveValues;

        public const int RCVALL_ON = 1;
        public const int RCVALL_OFF = 0;
        public const int RCVALL_SOCKETLEVELONLY = 2;
        public const int RCVALL_MAX = 3;

        public const int has_ipv6 = (int)1;

        #endregion

        #region Private implementation

        private static int HERROR_HOST_NOT_FOUND = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (int)SocketError.HostNotFound : 1;
        private static int HERROR_TRY_AGAIN      = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (int)SocketError.TryAgain     : 2;
        private static int HERROR_NO_RECOVERY    = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (int)SocketError.NoRecovery   : 3;
        private static int HERROR_NO_DATA        = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (int)SocketError.NoData       : 4;

        /// <summary>
        /// Return a socket exception (socket.error, socket.timeout, socket.herror) based on the information in the existing exception.
        /// </summary>
        internal static Exception MakeException(CodeContext/*!*/ context, Exception exception) {
            if (exception is SocketException se) {
                switch (se.SocketErrorCode) {
                    case SocketError.TimedOut:
                        return PythonExceptions.CreateThrowable(timeout(context), se.Message); // no error code used

                    // The following SockerErrors have no mapping to errno, but can be represented by herror
                    case SocketError.HostNotFound:
                        return PythonExceptions.CreateThrowable(herror(context), HERROR_HOST_NOT_FOUND, "Unknown host");
                    case SocketError.NoRecovery:
                        return PythonExceptions.CreateThrowable(herror(context), HERROR_NO_RECOVERY, "A nonrecoverable error occurred");

                    // The mapping to errno exists but is not perfect; therefore herror is used
                    case SocketError.NoData:
                        return PythonExceptions.CreateThrowable(herror(context), HERROR_NO_DATA, "No host data available"); // ENODATA
                    case SocketError.TryAgain:
                        return PythonExceptions.CreateThrowable(herror(context), HERROR_TRY_AGAIN, "Try again"); // EAGAIN

                    // The following SocketErrors have no defined mapping to errno, so a generic errno is used instead
                    case SocketError.ProcessLimit:
                        return PythonExceptions.CreateThrowable(error,
                            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? PythonErrorNumber.EPROCLIM : PythonErrorNumber.ENOTSUP,
                            "Too many processes");
                    case SocketError.NotInitialized:
                    case SocketError.SystemNotReady:
                    case SocketError.VersionNotSupported:
                    case SocketError.TypeNotFound:
                        return PythonExceptions.CreateThrowable(error, PythonErrorNumber.ENOTSUP, $"Socket error: {se.SocketErrorCode}");
                    case SocketError.SocketError:
                        return PythonExceptions.CreateThrowable(error, PythonErrorNumber.EIO, $"Unknown socket error");

                    // For the rest, NativeErrorCode provides the errno (except on Mono)
                    default:
                        if (ClrModule.IsMono) {
                            // On Mono, NativeErrorCode is simply (int)SocketErrorCode, so of no use.
                            return PythonExceptions.CreateThrowable(error, MapMonoSocketErrorToErrno(se.SocketErrorCode), se.Message);
                        }
                        return PythonExceptions.CreateThrowable(error, se.NativeErrorCode, se.Message);
                }
            } else if (exception is ObjectDisposedException) {
                return PythonExceptions.CreateThrowable(error, PythonErrorNumber.EBADF, "Socket is closed");
            } else if (exception is InvalidOperationException or ArgumentException) {
                return MakeException(context, new SocketException((int)SocketError.InvalidArgument));
            } else {
                return exception;
            }
        }

        private static Exception MakeGaiException(CodeContext context, Exception exception) {
            if (exception is SocketException se) {
                switch (se.SocketErrorCode) {
                    case SocketError.TryAgain:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_AGAIN, se.Message);
                    case SocketError.InvalidArgument:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_BADFLAGS, se.Message);
                    case SocketError.NoRecovery:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_FAIL, se.Message);
                    case SocketError.AddressFamilyNotSupported:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_FAMILY, se.Message);
                    case SocketError.NoBufferSpaceAvailable:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_MEMORY, se.Message);
                    case SocketError.HostNotFound:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_NONAME, se.Message);
                    case SocketError.TypeNotFound:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_SERVICE, se.Message);
                    case SocketError.SocketNotSupported:
                        return PythonExceptions.CreateThrowable(gaierror(context), EAI_SOCKTYPE, se.Message);
                    case SocketError.SocketError:
                        return MakeGaiException(context, EAI_SYSTEM);
                    default:
                        break; // fall through to MakeException
                }
            }
            return MakeException(context, exception);
        }

        private static Exception MakeGaiException(CodeContext context, int eaiCode) {
            // gai messages here are a mix from Linux/Darwin; messages are different on Linux, Darwin and Windows (but with the same meaning)
            if (eaiCode == EAI_NONAME) {
                return PythonExceptions.CreateThrowable(gaierror(context), EAI_NONAME, "Name or service not known");
            } else if (eaiCode == EAI_NODATA) {
                return PythonExceptions.CreateThrowable(gaierror(context), EAI_NODATA, "No addresses of the specified family associated with host");
            } else if (eaiCode == EAI_SERVICE) {
                return PythonExceptions.CreateThrowable(gaierror(context), EAI_SERVICE, "Service not available for socket type");
            } else if (eaiCode == EAI_FAMILY) {
                return PythonExceptions.CreateThrowable(gaierror(context), EAI_FAMILY, "Address family not supported by protocol family");
            } else if (eaiCode == EAI_ADDRFAMILY) { // Posix only
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    return MakeGaiException(context, EAI_NONAME);
                }
                return PythonExceptions.CreateThrowable(gaierror(context), EAI_ADDRFAMILY, "Address family for hostname not supported");
            } else if (eaiCode == EAI_BADHINTS) { // OSX only
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    return PythonExceptions.CreateThrowable(gaierror(context), EAI_BADHINTS, "Bad hints");
                }
                return MakeGaiException(context, EAI_SERVICE);
            // TODO: Handle other defined codes (currently unused). Remap codes that are not available on a specific system.
            } else {
                // fallback to generic error
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    return PythonExceptions.CreateThrowable(gaierror(context), EAI_FAIL, "Unknown socket error");
                }
                return PythonExceptions.CreateThrowable(gaierror(context), EAI_SYSTEM, "Socket error; check errno for mode information"); // Posix only
            }
        }

        private static int MapMonoSocketErrorToErrno(SocketError serror) {
            monoSocketErrorToNativeError ??= new Dictionary<SocketError, int>(45)
            {
                { SocketError.AccessDenied, PythonErrorNumber.EACCES}, // could also have been EPERM
                { SocketError.AddressAlreadyInUse, PythonErrorNumber.EADDRINUSE  },
                { SocketError.AddressNotAvailable, PythonErrorNumber.EADDRNOTAVAIL },
                { SocketError.AddressFamilyNotSupported, PythonErrorNumber.EAFNOSUPPORT  },
                { SocketError.AlreadyInProgress, PythonErrorNumber.EALREADY },
                { SocketError.ConnectionAborted, PythonErrorNumber.ECONNABORTED },
                { SocketError.ConnectionRefused, PythonErrorNumber.ECONNREFUSED },
                { SocketError.ConnectionReset, PythonErrorNumber.ECONNRESET },
                { SocketError.DestinationAddressRequired, PythonErrorNumber.EDESTADDRREQ },
                { SocketError.Disconnecting, PythonErrorNumber.ESHUTDOWN },
                { SocketError.Fault, PythonErrorNumber.EFAULT },
                { SocketError.HostDown, PythonErrorNumber.EHOSTDOWN },
                { SocketError.HostNotFound, PythonErrorNumber.ENOENT },
                { SocketError.HostUnreachable, PythonErrorNumber.EHOSTUNREACH },
                { SocketError.InProgress, PythonErrorNumber.EINPROGRESS },
                { SocketError.Interrupted, PythonErrorNumber.EINTR },
                { SocketError.InvalidArgument, PythonErrorNumber.EINVAL },
                { SocketError.IOPending, PythonErrorNumber.EINPROGRESS },
                { SocketError.IsConnected, PythonErrorNumber.EISCONN },
                { SocketError.MessageSize, PythonErrorNumber.EMSGSIZE },
                { SocketError.NetworkDown, PythonErrorNumber.ENETDOWN },
                { SocketError.NetworkReset, PythonErrorNumber.ENETRESET },
                { SocketError.NetworkUnreachable, PythonErrorNumber.ENETUNREACH },
                { SocketError.NoBufferSpaceAvailable, PythonErrorNumber.ENOBUFS },
                { SocketError.NoData, PythonErrorNumber.ENODATA },
                { SocketError.NotConnected, PythonErrorNumber.ENOTCONN },
                { SocketError.NotInitialized, PythonErrorNumber.ENOTSUP },
                { SocketError.NotSocket, PythonErrorNumber.ENOTSOCK },
                { SocketError.OperationAborted, PythonErrorNumber.ECANCELED },
                { SocketError.OperationNotSupported, PythonErrorNumber.ENOTSUP },
                { SocketError.ProcessLimit, !RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? PythonErrorNumber.EPROCLIM : PythonErrorNumber.ENOTSUP },
                { SocketError.ProtocolFamilyNotSupported, PythonErrorNumber.EPFNOSUPPORT },
                { SocketError.ProtocolNotSupported, PythonErrorNumber.EPROTONOSUPPORT },
                { SocketError.ProtocolOption, PythonErrorNumber.ENOPROTOOPT },
                { SocketError.ProtocolType, PythonErrorNumber.EPROTOTYPE },
                { SocketError.Shutdown, PythonErrorNumber.EPIPE },
                { SocketError.SocketNotSupported, PythonErrorNumber.ESOCKTNOSUPPORT },
                { SocketError.Success, PythonErrorNumber.ENOERROR },
                { SocketError.SystemNotReady, PythonErrorNumber.ENOTSUP }, // or EAGAIN
                { SocketError.TimedOut, PythonErrorNumber.ETIMEDOUT },
                { SocketError.TooManyOpenSockets, PythonErrorNumber.ENFILE }, // could also have been EMFILE
                { SocketError.TryAgain, PythonErrorNumber.EAGAIN }, // not a perfect mapping, but better than nothing
                { SocketError.TypeNotFound, PythonErrorNumber.ENOTSOCK },
                { SocketError.VersionNotSupported, PythonErrorNumber.EPROTONOSUPPORT },
                { SocketError.WouldBlock, PythonErrorNumber.EWOULDBLOCK }, // on Linux/OSX, same as EAGAIN
            };
            if (monoSocketErrorToNativeError.TryGetValue(serror, out int errno)) {
                return errno;
            } else {
                return PythonErrorNumber.EIO;
            }
        }

        [DisallowNull]
        private static Dictionary<SocketError, int>? monoSocketErrorToNativeError;

        /// <summary>
        /// Convert an IPv6 address byte array to a string in standard colon-hex notation.
        /// The .NET IPAddress.ToString() method uses dotted-quad for the last 32 bits,
        /// which differs from the normal Python implementation (but is allowed by the IETF);
        /// this method returns the standard (no dotted-quad) colon-hex form.
        /// </summary>
        private static string IPv6BytesToColonHex(ReadOnlySpan<byte> ipBytes) {
            Debug.Assert(ipBytes.Length == IPv6AddrBytes);

            const int bytesPerWord = 2; // in bytes
            const int bitsPerByte = 8;
            int[] words = new int[IPv6AddrBytes / bytesPerWord];

            // Convert to array of 16-bit words
            for (int i = 0; i < words.Length; i++) {
                for (int j = 0; j < bytesPerWord; j++) {
                    words[i] <<= bitsPerByte;
                    words[i] += ipBytes[i * bytesPerWord + j];
                }
            }

            // Find longest series of 0-valued words (to collapse to ::)
            int longestStart = 0;
            int longestLen = 0;

            for (int i = 0; i < words.Length; i++) {
                if (words[i] == 0) {
                    for (int j = i; j < words.Length; j++) {
                        if (words[j] != 0) {
                            i += longestLen;
                            break;
                        }
                        if (j - i + 1 > longestLen) {
                            longestStart = i;
                            longestLen = j - i + 1;
                        }
                    }
                }
            }

            // Build colon-hex string
            StringBuilder result = new StringBuilder(IPv6AddrBytes * 3);
            for (int i = 0; i < words.Length; i++) {
                if (i != 0) result.Append(':');
                if (longestLen > 0 && i == longestStart) {
                    if (longestStart == 0) result.Append(':');
                    if (longestStart + longestLen == words.Length) result.Append(':');
                    i += longestLen - 1;
                    continue;
                } else {
                    result.Append(words[i].ToString("x"));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Handle conversion of "" to INADDR_ANY and "&lt;broadcast&gt;" to INADDR_BROADCAST.
        /// Otherwise returns host unchanged.
        /// </summary>
        private static string ConvertSpecialAddresses(string host) {
            switch (host) {
                case AnyAddrToken:
                    return IPAddress.Any.ToString();
                case BroadcastAddrToken:
                    return IPAddress.Broadcast.ToString();
                default:
                    return host;
            }
        }

        /// <summary>
        /// Return the IP address associated with host, with optional address family checking.
        /// host may be either a name or an IP address (in string form).
        /// 
        /// If family is non-null, a gaierror will be thrown if the host's address family is
        /// not the same as the specified family. gaierror is also raised if the hostname cannot be
        /// converted to an IP address (e.g. through a name lookup failure).
        /// </summary>
        private static IPAddress HostToAddress(CodeContext/*!*/ context, string host, AddressFamily family) {
            return HostToAddresses(context, host, family)[0];
        }

        /// <summary>
        /// Return the IP address associated with host, with optional address family checking.
        /// host may be either a name or an IP address (in string form).
        /// 
        /// If family is non-null, a gaierror will be thrown if the host's address family is
        /// not the same as the specified family. gaierror is also raised if the hostname cannot be
        /// converted to an IP address (e.g. through a name lookup failure).
        /// </summary>
        private static IPAddress[] HostToAddresses(CodeContext/*!*/ context, string host, AddressFamily family) {
            host = ConvertSpecialAddresses(host);
            try {
                bool numeric = true;
                int dotCount = 0;
                foreach (char c in host) {
                    if (!Char.IsNumber(c) && c != '.') {
                        numeric = false;
                    } else if (c == '.') {
                        dotCount++;
                    }
                }
                if (numeric) {
                    if (dotCount == 3 && IPAddress.TryParse(host, out IPAddress? addr)) {
                        if (family == AddressFamily.Unspecified || family == addr.AddressFamily) {
                            return new IPAddress[] { addr };
                        }
                    }
                    // Incorrect family will raise EAI_NODATA exception below
                } else {
                    IPHostEntry hostEntry = Dns.GetHostEntry(host);
                    List<IPAddress> addrs = new List<IPAddress>();
                    foreach (IPAddress ip in hostEntry.AddressList) {
                        if (family == AddressFamily.Unspecified || family == ip.AddressFamily) {
                            addrs.Add(ip);
                        }
                    }
                    if (addrs.Count > 0) return addrs.ToArray();
                }
                throw MakeGaiException(context, EAI_NODATA);
            } catch  (SocketException ex) {
                throw MakeGaiException(context, ex);
            }
        }

        /// <summary>
        /// Return fqdn, but with its domain removed if it's on the same domain as the local machine.
        /// </summary>
        private static string RemoveLocalDomain(string fqdn) {
            char[] DNS_SEP = new char[] { '.' };
            string[] myName = getfqdn().Split(DNS_SEP, 2);
            string[] otherName = fqdn.Split(DNS_SEP, 2);

            if (myName.Length < 2 || otherName.Length < 2) return fqdn;

            if (myName[1] == otherName[1]) {
                return otherName[0];
            } else {
                return fqdn;
            }
        }

        /// <summary>
        /// Convert a (host, port) tuple [IPv4] (host, port, flowinfo, scopeid) tuple [IPv6]
        /// to its corresponding IPEndPoint.
        /// 
        /// Throws gaierror if host is not a valid address.
        /// Throws ArgumentTypeException if any of the following are true:
        ///  - address does not have exactly two elements
        ///  - address[0] is not a string
        ///  - address[1] is not an int
        /// </summary>
        private static IPEndPoint TupleToEndPoint(CodeContext/*!*/ context, PythonTuple address, AddressFamily family, out string host) {
            var addressLen = address.__len__();
            if (addressLen < 2 || addressLen > 4) {
                throw PythonOps.TypeError("address tuple must have exactly 2 (IPv4) or exactly 4 (IPv6) elements");
            }

            try {
                host = Converter.ConvertToString(address[0]);
            } catch (ArgumentTypeException) {
                throw PythonOps.TypeError("host must be string");
            }

            int port;
            try {
                port = context.LanguageContext.ConvertToInt32(address[1]);
            } catch (ArgumentTypeException) {
                throw PythonOps.TypeError("port must be integer");
            }

            if (port < 0 || port > 65535) {
                throw PythonOps.OverflowError("getsockaddrarg: port must be 0-65535");
            }

            IPAddress ip = HostToAddress(context, host, family);
            var endPoint = new IPEndPoint(ip, port);

            if (addressLen > 2) {
                if (!Converter.TryConvertToBigInteger(address[2], out var flowinfo)) {
                    throw PythonOps.TypeError("an integer is required");
                }
                if (flowinfo < 0 || flowinfo > 0xfffff) {
                    throw PythonOps.OverflowError($"{nameof(socket.bind)}(): flowinfo must be 0-1048575.");
                }
                // We don't actually do anything with flowinfo right now, but we validate it
                // in case we want to do something in the future.

                if (addressLen > 3) {
                    long scopeId;
                    try {
                        scopeId = Converter.ConvertToInt64(address[3]);
                    } catch (ArgumentTypeException) {
                        throw PythonOps.TypeError("scopeid must be integer");
                    }

                    endPoint.Address.ScopeId = scopeId;
                }
            }

            return endPoint;
        }

        /// <summary>
        /// Convert an IPEndPoint to its corresponding (host, port) [IPv4] or (host, port, flowinfo, scopeid) [IPv6] tuple.
        /// Throws SocketException if the address family is other than IPv4 or IPv6.
        /// </summary>
        private static PythonTuple EndPointToTuple(IPEndPoint endPoint) {
            string ip = endPoint.Address.ToString();
            int port = endPoint.Port;
            switch (endPoint.Address.AddressFamily) {
                case AddressFamily.InterNetwork:
                    return PythonTuple.MakeTuple(ip, port);
                case AddressFamily.InterNetworkV6:
                    long flowInfo = 0; // RFC 3493 p. 7 
                    long scopeId = endPoint.Address.ScopeId;
                    return PythonTuple.MakeTuple(ip, port, flowInfo, scopeId);
                default:
                    throw new SocketException((int)SocketError.AddressFamilyNotSupported);
            }
        }

        #endregion

        private static int? GetDefaultTimeout(CodeContext/*!*/ context) {
            return (int?)context.LanguageContext.GetModuleState(_defaultTimeoutKey);
        }

        private static void SetDefaultTimeout(CodeContext/*!*/ context, int? timeout) {
            context.LanguageContext.SetModuleState(_defaultTimeoutKey, timeout);
        }

        private static PythonType herror(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("socketherror");
        }

        private static PythonType timeout(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("sockettimeout");
        }
    }

    internal class SocketUtilException : Exception {
        public SocketUtilException() { }

        public SocketUtilException(string message)
            : base(message) { }

        public SocketUtilException(string message, Exception inner)
            : base(message, inner) { }
    }

    // based on http://stackoverflow.com/questions/13246099/using-c-sharp-to-reference-a-port-number-to-service-name
    // for the Linux Mono version and http://www.pinvoke.net/ for dllimports for winsock libraries
    internal static class SocketUtil {

        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct servent {
            public string s_name;
            public IntPtr s_aliases;
            public ushort s_port;
            public string s_proto;
        }

        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct servent64 {
            public string s_name;
            public IntPtr s_aliases;
            public string s_proto;
            public ushort s_port;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WSAData {
            public short wVersion;
            public short wHighVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            public string szDescription;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
            public string szSystemStatus;
            public short iMaxSockets;
            public short iMaxUdpDg;
            public int lpVendorInfo;
        }


        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "getservbyname")]
        private static extern IntPtr getservbyname_linux(string name, string? proto);
        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "getservbyport")]
        private static extern IntPtr getservbyport_linux(ushort port, string? proto);


        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern Int32 WSAStartup(Int16 wVersionRequested, out WSAData wsaData);
        [DllImport("ws2_32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr getservbyname(string name, string? proto);
        [DllImport("ws2_32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr getservbyport(ushort port, string? proto);
        [DllImport("Ws2_32.dll", SetLastError = true)]
        private static extern Int32 WSAGetLastError();
        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern Int32 WSACleanup();

        private static T PtrToStructure<T>(IntPtr result) {
            return (T)Marshal.PtrToStructure(result, typeof(T))!;
        }

        public static string GetServiceByPort(ushort port, string? protocol) {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                return GetServiceByPortNonWindows(port, protocol);
            else
                return GetServiceByPortWindows(port, protocol);

            static string GetServiceByPortWindows(ushort port, string? protocol) {

                var test = Socket.OSSupportsIPv6; // make sure Winsock library is initiliazed

                var netport = unchecked((ushort)IPAddress.HostToNetworkOrder(unchecked((short)port)));
                var result = getservbyport(netport, protocol);
                if (IntPtr.Zero == result)
                    throw new SocketUtilException(string.Format("Could not resolve service for port {0}", port));

                if (Environment.Is64BitProcess)
                    return PtrToStructure<servent64>(result).s_name;
                else
                    return PtrToStructure<servent>(result).s_name;
            }

            static string GetServiceByPortNonWindows(ushort port, string? protocol) {
                var netport = unchecked((ushort)IPAddress.HostToNetworkOrder(unchecked((short)port)));
                var result = getservbyport_linux(netport, protocol);
                if (IntPtr.Zero == result) {
                    throw new SocketUtilException(
                        string.Format("Could not resolve service for port {0}", port));
                }

                return PtrToStructure<servent>(result).s_name;
            }
        }

        public static ushort GetServiceByName(string service, string? protocol) {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                return GetServiceByNameNonWindows(service, protocol);
            else
                return GetServiceByNameWindows(service, protocol);

            static ushort GetServiceByNameWindows(string service, string? protocol) {
                var test = Socket.OSSupportsIPv6; // make sure Winsock library is initiliazed

                var result = getservbyname(service, protocol);
                if (IntPtr.Zero == result)
                    throw new SocketUtilException(string.Format("Could not resolve port for service {0}", service));

                ushort port;
                if (Environment.Is64BitProcess)
                    port = PtrToStructure<servent64>(result).s_port;
                else
                    port = PtrToStructure<servent>(result).s_port;

                var hostport = IPAddress.NetworkToHostOrder(unchecked((short)port));
                return unchecked((ushort)hostport);
            }

            static ushort GetServiceByNameNonWindows(string service, string? protocol) {
                var result = getservbyname_linux(service, protocol);
                if (IntPtr.Zero == result) {
                    throw new SocketUtilException(
                        string.Format("Could not resolve port for service {0}", service));
                }

                ushort port = PtrToStructure<servent>(result).s_port;
                var hostport = IPAddress.NetworkToHostOrder(unchecked((short)port));
                return unchecked((ushort)hostport);
            }
        }
    }
}

#endif
