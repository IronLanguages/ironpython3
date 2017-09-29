/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

#if FEATURE_FULL_NET

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using PythonArray = IronPython.Modules.ArrayModule.array;
using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("_socket", typeof(IronPython.Modules.PythonSocket))]
namespace IronPython.Modules {
    public static class PythonSocket {
        private static readonly object _defaultTimeoutKey = new object();
        private static readonly object _defaultBufsizeKey = new object();
        private const int DefaultBufferSize = 8192;
        public static PythonTuple _delegate_methods = PythonTuple.MakeTuple("recv", "recvfrom", "recv_into", "recvfrom_into", "send", "sendto");

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            if (!context.HasModuleState(_defaultTimeoutKey)) {
                context.SetModuleState(_defaultTimeoutKey, null);
            }

            context.SetModuleState(_defaultBufsizeKey, DefaultBufferSize);

            PythonType socketErr = GetSocketError(context, dict);
            context.EnsureModuleException("socketherror", socketErr, dict, "herror", "socket");
            context.EnsureModuleException("socketgaierror", socketErr, dict, "gaierror", "socket");
            context.EnsureModuleException("sockettimeout", socketErr, dict, "timeout", "socket");
        }

        internal static PythonType GetSocketError(PythonContext context, PythonDictionary dict) {
            return context.EnsureModuleException("socketerror", PythonExceptions.OSError, dict, "error", "socket");
        }


        private static IntPtr GetHandle(this Socket socket) {
            return socket.Handle;
        }

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
            internal string _hostName;
            private WeakRefTracker _weakRefTracker = null;
            private int _referenceCount = 1;
            public const string __module__ = "socket";
            internal CodeContext/*!*/ _context;
            private int _timeout;

            #endregion

            #region Public API

            public socket() {
            }


            public void __init__(CodeContext/*!*/ context, int addressFamily=DefaultAddressFamily,
                int socketType=DefaultSocketType,
                int protocolType=DefaultProtocolType,
                socket _sock=null) {
                System.Net.Sockets.SocketType type = (System.Net.Sockets.SocketType)Enum.ToObject(typeof(System.Net.Sockets.SocketType), socketType);
                if (!Enum.IsDefined(typeof(System.Net.Sockets.SocketType), type)) {
                    throw MakeException(context, new SocketException((int)SocketError.SocketNotSupported));
                }
                AddressFamily family = (AddressFamily)Enum.ToObject(typeof(AddressFamily), addressFamily);
                if (!Enum.IsDefined(typeof(AddressFamily), family)) {
                    throw MakeException(context, new SocketException((int)SocketError.AddressFamilyNotSupported));
                }
                ProtocolType proto = (ProtocolType)Enum.ToObject(typeof(ProtocolType), protocolType);
                if (!Enum.IsDefined(typeof(ProtocolType), proto)) {
                    throw MakeException(context, new SocketException((int)SocketError.ProtocolNotSupported));
                }

                if (_sock == null) {
                    Socket newSocket;
                    try {
                        newSocket = new Socket(family, type, proto);
                    } catch (SocketException e) {
                        throw MakeException(context, e);
                    }

                    Initialize(context, newSocket);
                } else {
                    _socket = _sock._socket;
                    _hostName = _sock._hostName;

                    // we now own the lifetime of the socket
                    GC.SuppressFinalize(_sock);
                    Initialize(context, _socket);
                }
            }

            public void __del__() {
                _close();
            }

            ~socket() {
                _close();
            }

            public socket _sock {
                get {
                    return this;
                }
            }

#if !NETCOREAPP2_0
            private IAsyncResult _acceptResult;
#endif
            [Documentation("accept() -> (conn, address)\n\n"
                + "Accept a connection. The socket must be bound and listening before calling\n"
                + "accept(). conn is a new socket object connected to the remote host, and\n"
                + "address is the remote host's address (e.g. a (host, port) tuple for IPv4).\n"
                + "\n"
                )]
            public PythonTuple accept() {
                socket wrappedRemoteSocket;
                Socket realRemoteSocket;
                try {
#if NETCOREAPP2_0
                    // TODO: support timeout != 0
                    realRemoteSocket = _socket.Accept();
#else
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
                                throw PythonExceptions.CreateThrowable(timeout(_context), 0, "timeout");
                            }
                        } else {
                            realRemoteSocket = _socket.Accept();
                        }
                    }
#endif
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
            public void bind(PythonTuple address) {
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

            internal void _close() {
                if (_socket != null) {
                    lock (_handleToSocket) {
                        WeakReference weakref;
                        if (_handleToSocket.TryGetValue(_socket.Handle, out weakref)) {
                            Socket target = (weakref.Target as Socket);
                            if (target == _socket || target == null) {
                                _handleToSocket.Remove(_socket.Handle);
                            }
                        }
                    }

                    ((IDisposable)_socket).Dispose();
                    _referenceCount = 0;
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
            public void connect(PythonTuple address) {
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
            public int connect_ex(PythonTuple address) {
                IPEndPoint remoteEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    _socket.Connect(remoteEP);
                } catch (SocketException e) {
                    return (int)e.SocketErrorCode;
                }
                return (int)SocketError.Success;
            }

            [Documentation("fileno() -> file_handle\n\n"
                + "Return the underlying system handle for this socket (a 64-bit integer)."
                )]
            public Int64 fileno() {
                try {
                    return _socket.GetHandle().ToInt64();
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
                    IPEndPoint remoteEP = _socket.RemoteEndPoint as IPEndPoint;
                    if (remoteEP == null) {
                        throw MakeException(_context, new SocketException((int)SocketError.AddressFamilyNotSupported));
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
                    IPEndPoint localEP = _socket.LocalEndPoint as IPEndPoint;
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
            public object getsockopt(int optionLevel, int optionName, int optionLength=0) {
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
                        return (int)_socket.GetSocketOption(level, name);
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
            public void listen(int backlog) {
                try {
                    _socket.Listen(backlog);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("makefile([mode[, bufsize]]) -> file object\n\n"
                + "Return a regular file object corresponding to the socket.  The mode\n"
                + "and bufsize arguments are as for the built-in open() function.")]
            public PythonFile makefile(string mode="r", int bufSize=8192) {
                System.Threading.Interlocked.Increment(ref _referenceCount); // dup our handle
                return new _fileobject(_context, this, mode, bufSize, false);
            }

            [Documentation("recv(bufsize[, flags]) -> string\n\n"
                + "Receive data from the socket, up to bufsize bytes. For connection-oriented\n"
                + "protocols (e.g. SOCK_STREAM), you must first call either connect() or\n"
                + "accept(). Connectionless protocols (e.g. SOCK_DGRAM) may also use recvfrom().\n"
                + "\n"
                + "recv() blocks until data is available, unless a timeout was set using\n"
                + "settimeout(). If the timeout was exceeded, socket.timeout is raised."
                + "recv() returns immediately with zero bytes when the connection is closed."
                )]
            public string recv(int maxBytes, int flags=0) {
                int bytesRead;
                if (maxBytes < 0)
                    throw PythonOps.ValueError("negative buffersize in recv");
                byte[] buffer = new byte[maxBytes];
                try {
                    bytesRead = _socket.Receive(buffer, (SocketFlags)flags);
                } catch (Exception e) {
                    throw MakeRecvException(e, SocketError.NotConnected);
                }
                return PythonOps.MakeString(buffer, bytesRead);
            }

            [Documentation("recv_into(buffer, [nbytes[, flags]]) -> nbytes_read\n\n"
                + "A version of recv() that stores its data into a buffer rather than creating\n"
                + "a new string.  Receive up to buffersize bytes from the socket.  If buffersize\n"
                + "is not specified (or 0), receive up to the size available in the given buffer.\n\n"
                + "See recv() for documentation about the flags.\n"
                )]
            public int recv_into(PythonBuffer buffer, int nbytes=0, int flags=0) {
                if (nbytes < 0) {
                    throw PythonOps.ValueError("negative buffersize in recv_into");
                }
                throw PythonOps.TypeError("buffer is read-only");
            }

            [Documentation("recv_into(buffer, [nbytes[, flags]]) -> nbytes_read\n\n"
                + "A version of recv() that stores its data into a buffer rather than creating\n"
                + "a new string.  Receive up to buffersize bytes from the socket.  If buffersize\n"
                + "is not specified (or 0), receive up to the size available in the given buffer.\n\n"
                + "See recv() for documentation about the flags.\n"
                )]
            public int recv_into(string buffer, int nbytes=0, int flags=0) {
                throw PythonOps.TypeError("Cannot use string as modifiable buffer");
            }

            [Documentation("recv_into(buffer, [nbytes[, flags]]) -> nbytes_read\n\n"
                + "A version of recv() that stores its data into a buffer rather than creating\n"
                + "a new string.  Receive up to buffersize bytes from the socket.  If buffersize\n"
                + "is not specified (or 0), receive up to the size available in the given buffer.\n\n"
                + "See recv() for documentation about the flags.\n"
                )]
            public int recv_into(PythonArray buffer, int nbytes=0, int flags=0) {
                int bytesRead;
                byte[] byteBuffer = new byte[byteBufferSize("recv_into", nbytes, buffer.__len__(), buffer.itemsize)];

                try {
                    bytesRead = _socket.Receive(byteBuffer, (SocketFlags)flags);
                } catch (Exception e) {
                    throw MakeRecvException(e, SocketError.NotConnected);
                }

                buffer.FromStream(new MemoryStream(byteBuffer), 0);
                return bytesRead;
            }


            [Documentation("recv_into(bytearray, [nbytes[, flags]]) -> nbytes_read\n\n"
                + "A version of recv() that stores its data into a bytearray rather than creating\n"
                + "a new string.  Receive up to buffersize bytes from the socket.  If buffersize\n"
                + "is not specified (or 0), receive up to the size available in the given buffer.\n\n"
                + "See recv() for documentation about the flags.\n"
                )]
            public int recv_into(ByteArray buffer, int nbytes=0, int flags=0) {
                int bytesRead;
                byte[] byteBuffer = new byte[byteBufferSize("recv_into", nbytes, buffer.Count, 1)];

                try {
                    bytesRead = _socket.Receive(byteBuffer, (SocketFlags)flags);
                } catch (Exception e) {
                    throw MakeRecvException(e, SocketError.NotConnected);
                }

                for (int i = 0; i < bytesRead; i++) {
                    buffer[i] = byteBuffer[i];
                }
                return bytesRead;
            }

            [Documentation("recv_into(memoryview, [nbytes[, flags]]) -> nbytes_read\n\n"
                + "A version of recv() that stores its data into a bytearray rather than creating\n"
                + "a new string.  Receive up to buffersize bytes from the socket.  If buffersize\n"
                + "is not specified (or 0), receive up to the size available in the given buffer.\n\n"
                + "See recv() for documentation about the flags.\n"
                )]
            public int recv_into(MemoryView buffer, int nbytes=0, int flags=0) {
                int bytesRead;
                byte[] byteBuffer = buffer.tobytes().ToByteArray();
                try {
                    bytesRead = _socket.Receive(byteBuffer, (SocketFlags)flags);
                }
                catch (Exception e) {
                    if (_socket.SendTimeout == 0) {
                        var s = new SocketException((int)SocketError.NotConnected);
                        throw PythonExceptions.CreateThrowable(error(_context), (int)SocketError.NotConnected, s.Message);
                    }
                    else
                        throw MakeException(_context, e);

                }

                buffer[new Slice(0, bytesRead)] = byteBuffer.Slice(new Slice(0, bytesRead));
                return bytesRead;

            }


            public int recv_into(object buffer, int nbytes=0, int flags=0){
                throw PythonOps.TypeError(string.Format("recv_into() argument 1 must be read-write buffer, not {0}",PythonOps.GetPythonTypeName(buffer)));
            }

            
            [Documentation("recvfrom(bufsize[, flags]) -> (string, address)\n\n"
                + "Receive data from the socket, up to bufsize bytes. string is the data\n"
                + "received, and address (whose format is protocol-dependent) is the address of\n"
                + "the socket from which the data was received."
                )]
            public PythonTuple recvfrom(int maxBytes, int flags=0) {
                if (maxBytes < 0) {
                    throw PythonOps.ValueError("negative buffersize in recvfrom");
                }

                int bytesRead;
                byte[] buffer = new byte[maxBytes];
                IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remoteEP = remoteIPEP;
                try {
                    bytesRead = _socket.ReceiveFrom(buffer, (SocketFlags)flags, ref remoteEP);
                } catch (Exception e) {
                    throw MakeRecvException(e, SocketError.InvalidArgument);
                }

                string data = PythonOps.MakeString(buffer, bytesRead);
                PythonTuple remoteAddress = EndPointToTuple((IPEndPoint)remoteEP);
                return PythonTuple.MakeTuple(data, remoteAddress);
            }

            [Documentation("recvfrom_into(buffer[, nbytes[, flags]]) -> (nbytes, address info)\n\n"
                + "Like recv_into(buffer[, nbytes[, flags]]) but also return the sender's address info.\n"
                )]
            public PythonTuple recvfrom_into(PythonBuffer buffer, int nbytes=0, int flags=0) {
                if (nbytes < 0) {
                    throw PythonOps.ValueError("negative buffersize in recvfrom_into");
                }
                throw PythonOps.TypeError("buffer is read-only");
            }

            [Documentation("recvfrom_into(buffer[, nbytes[, flags]]) -> (nbytes, address info)\n\n"
                + "Like recv_into(buffer[, nbytes[, flags]]) but also return the sender's address info.\n"
                )]
            public PythonTuple recvfrom_into(string buffer, int nbytes=0, int flags=0) {
                throw PythonOps.TypeError("Cannot use string as modifiable buffer");
            }

            [Documentation("recvfrom_into(buffer[, nbytes[, flags]]) -> (nbytes, address info)\n\n"
                + "Like recv_into(buffer[, nbytes[, flags]]) but also return the sender's address info.\n"
                )]
            public PythonTuple recvfrom_into(PythonArray buffer, int nbytes=0, int flags=0) {
                int bytesRead;
                byte[] byteBuffer = new byte[byteBufferSize("recvfrom_into", nbytes, buffer.__len__(), buffer.itemsize)];
                IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remoteEP = remoteIPEP;

                try {
                    bytesRead = _socket.ReceiveFrom(byteBuffer, (SocketFlags)flags, ref remoteEP);
                } catch (Exception e) {
                    throw MakeRecvException(e, SocketError.InvalidArgument);
                }

                buffer.FromStream(new MemoryStream(byteBuffer), 0);
                PythonTuple remoteAddress = EndPointToTuple((IPEndPoint)remoteEP);
                return PythonTuple.MakeTuple(bytesRead, remoteAddress);
            }

            [Documentation("recvfrom_into(buffer[, nbytes[, flags]]) -> (nbytes, address info)\n\n"
               + "Like recv_into(buffer[, nbytes[, flags]]) but also return the sender's address info.\n"
               )]
            public PythonTuple recvfrom_into(MemoryView buffer, int nbytes=0, int flags=0){
                
                int bytesRead;
                byte[] byteBuffer = buffer.tobytes().ToByteArray();
                IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remoteEP = remoteIPEP;

                try {
                    bytesRead = _socket.ReceiveFrom(byteBuffer, (SocketFlags)flags, ref remoteEP);
                }
                catch (Exception e) {
                    throw MakeRecvException(e, SocketError.InvalidArgument);
                }

                buffer[new Slice(0, bytesRead)] = byteBuffer.Slice(new Slice(0, bytesRead));
                PythonTuple remoteAddress = EndPointToTuple((IPEndPoint)remoteEP);
                return PythonTuple.MakeTuple(bytesRead, remoteAddress);
            }

            [Documentation("recvfrom_into(buffer[, nbytes[, flags]]) -> (nbytes, address info)\n\n"
                + "Like recv_into(buffer[, nbytes[, flags]]) but also return the sender's address info.\n"
                )]
            public PythonTuple recvfrom_into(IList<byte> buffer, int nbytes=0, int flags=0) {
                int bytesRead;
                byte[] byteBuffer = new byte[byteBufferSize("recvfrom_into", nbytes, buffer.Count, 1)];
                IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remoteEP = remoteIPEP;

                try {
                    bytesRead = _socket.ReceiveFrom(byteBuffer, (SocketFlags)flags, ref remoteEP);
                } catch (Exception e) {
                    throw MakeRecvException(e, SocketError.InvalidArgument);
                }

                for (int i = 0; i < byteBuffer.Length; i++) {
                    buffer[i] = byteBuffer[i];
                }
                PythonTuple remoteAddress = EndPointToTuple((IPEndPoint)remoteEP);
                return PythonTuple.MakeTuple(bytesRead, remoteAddress);
            }

            public PythonTuple recvfrom_into(object buffer, int nbytes=0, int flags=0) {
                throw PythonOps.TypeError(string.Format("recvfrom_into() argument 1 must be read-write buffer, not {0}", PythonOps.GetPythonTypeName(buffer)));
            }

            private static int byteBufferSize(string funcName, int nbytes, int bufLength, int itemSize) {
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

            private Exception MakeRecvException(Exception e, SocketError errorCode=SocketError.InvalidArgument) {
                // on the socket recv throw a special socket error code when SendTimeout is zero
                if (_socket.SendTimeout == 0) {
                    var s = new SocketException((int)errorCode);
                    return PythonExceptions.CreateThrowable(error(_context), (int)SocketError.InvalidArgument, s.Message);
                }
                else
                    return MakeException(_context, e);
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
            public int send(string data, int flags=0) {
                byte[] buffer = data.MakeByteArray();
                try {
                    return _socket.Send(buffer, (SocketFlags)flags);
                } catch (Exception e) {
                    throw MakeException(_context, e);
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
            public int send(Bytes data, int flags=0) {
                byte[] buffer = data.GetUnsafeByteArray();
                try {
                    return _socket.Send(buffer, (SocketFlags)flags);
                } catch (Exception e) {
                    throw MakeException(_context, e);
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
            public int send(PythonBuffer data, int flags=0) {
                byte[] buffer = data.byteCache;
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
            public void sendall(string data, int flags=0) {
                sendallWorker(data.MakeByteArray(), flags);
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
            public void sendall(Bytes data, int flags=0) {
                sendallWorker(data.GetUnsafeByteArray(), flags);
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
            public void sendall(PythonBuffer data, int flags=0) {
                sendallWorker(data.byteCache, flags);
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
            public int sendto(string data, int flags, PythonTuple address) {
                byte[] buffer = data.MakeByteArray();
                EndPoint remoteEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    return _socket.SendTo(buffer, (SocketFlags)flags, remoteEP);
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
            public int sendto(Bytes data, int flags, PythonTuple address) {
                byte[] buffer = data.GetUnsafeByteArray();
                EndPoint remoteEP = TupleToEndPoint(_context, address, _socket.AddressFamily, out _hostName);
                try {
                    return _socket.SendTo(buffer, (SocketFlags)flags, remoteEP);
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }
            }

            [Documentation("")]
            public int sendto(string data, PythonTuple address) {
                return sendto(data, 0, address);
            }

            [Documentation("")]
            public int sendto(Bytes data, PythonTuple address) {
                return sendto(data, 0, address);
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
            public void settimeout(object timeout) {
                try {
                    if (timeout == null) {
                        _socket.Blocking = true;
                        _socket.SendTimeout = 0;
                    } else {
                        double seconds;
                        seconds = Converter.ConvertToDouble(timeout);
                        if (seconds < 0) {
                            throw PythonOps.ValueError("Timeout value out of range");
                        }
                        _socket.Blocking = seconds > 0; // 0 timeout means non-blocking mode
                        _socket.SendTimeout = (int)(seconds * MillisecondsPerSecond);
                        _timeout = (int)(seconds * MillisecondsPerSecond);
                    }
                } finally {
                    _socket.ReceiveTimeout = _socket.SendTimeout;
                }
            }

            [Documentation("gettimeout() -> value\n\n"
                + "Return the timeout duration in seconds for this socket as a float. If no\n"
                + "timeout is set, return None. For more details on timeouts and blocking, see the\n"
                + "Python socket module documentation."
                )]
            public object gettimeout() {
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
            public void setsockopt(int optionLevel, int optionName, object value) {
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

                    string strValue;
                    if (Converter.TryConvertToString(value, out strValue)) {
                        _socket.SetSocketOption(level, name, strValue.MakeByteArray());
                        return;
                    }
                } catch (Exception e) {
                    throw MakeException(_context, e);
                }

                throw PythonOps.TypeError("setsockopt() argument 3 must be int or string");
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

            public int family {
                get { return (int)_socket.AddressFamily; }
            }

            public int type {
                get { return (int)_socket.SocketType; }
            }

            public int proto {
                get { return (int)_socket.ProtocolType; }
            }

            public int ioctl(BigInteger cmd, object option) {
                if(cmd == SIO_KEEPALIVE_VALS){ 
                    if (!(option is PythonTuple))
                        throw PythonOps.TypeError("option must be 3-item sequence, not int");
                    var tOption = (PythonTuple)option;
                    if (tOption.Count != 3)
                        throw PythonOps.TypeError(string.Format("option must be sequence of length 3, not {0}", tOption.Count));
                    //(onoff, timeout, interval)
                    if ((!(tOption[0] is int)) && (!(tOption[1] is int)) && (!(tOption[2] is int)))
                        throw PythonOps.TypeError("option integer required");

                    int onoff = (int)tOption[0];
                    int timeout = (int)tOption[1];
                    int interval = (int)tOption[2];
                    int size = sizeof(UInt32);
                    byte[] inArray = new byte[size * 3];
                    Array.Copy(BitConverter.GetBytes(onoff), 0, inArray, 0, size);
                    Array.Copy(BitConverter.GetBytes(timeout), 0, inArray, size, size);
                    Array.Copy(BitConverter.GetBytes(size), 0, inArray, size*2, size);
                    return _socket.IOControl((IOControlCode)(long)cmd, inArray, null);
                }
                else if(cmd == SIO_RCVALL){
                    if (!(option is int))
                        throw PythonOps.TypeError("option integer required");

                    return _socket.IOControl((IOControlCode)(long)cmd, BitConverter.GetBytes((int)option), null);
                }
                else
                    throw PythonOps.ValueError(string.Format("invalid ioctl command {0}", cmd));
            }

            public override string ToString() {
                try {

                    return String.Format("<socket object, fd={0}, family={1}, type={2}, protocol={3}>",
                        fileno(), family, type, proto);
                } catch {
                    return "<socket object, fd=?, family=?, type=, protocol=>";
                }
            }

            /// <summary>
            /// Return the internal System.Net.Sockets.Socket socket object associated with the given
            /// handle (as returned by GetHandle()), or null if no corresponding socket exists. This is
            /// primarily intended to be used by other modules (such as select) that implement
            /// networking primitives. User code should not normally need to call this function.
            /// </summary>
            internal static Socket HandleToSocket(Int64 handle) {
                WeakReference weakref;
                lock (_handleToSocket) {
                    if (_handleToSocket.TryGetValue((IntPtr)handle, out weakref)) {
                        return (weakref.Target as Socket);
                    }
                }
                return null;
            }

            #endregion

            #region IWeakReferenceable Implementation

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
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
                    _handleToSocket[socket.GetHandle()] = new WeakReference(socket);
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

        public static object _GLOBAL_DEFAULT_TIMEOUT = new object();

        [Documentation("Connect to *address* and return the socket object.\n\n"
            + "Convenience function.  Connect to *address* (a 2-tuple ``(host,\n"
            + "port)``) and return the socket object.  Passing the optional\n"
            + "*timeout* parameter will set the timeout on the socket instance\n"
            + "before attempting to connect.  If no *timeout* is supplied, the\n"
            + "global default timeout setting returned by :func:`getdefaulttimeout`\n"
            + "is used.\n"
            )]
        public static socket create_connection(CodeContext/*!*/ context, PythonTuple address) {
            return create_connection(context, address, _GLOBAL_DEFAULT_TIMEOUT);
        }

        [Documentation("Connect to *address* and return the socket object.\n\n"
            + "Convenience function.  Connect to *address* (a 2-tuple ``(host,\n"
            + "port)``) and return the socket object.  Passing the optional\n"
            + "*timeout* parameter will set the timeout on the socket instance\n"
            + "before attempting to connect.  If no *timeout* is supplied, the\n"
            + "global default timeout setting returned by :func:`getdefaulttimeout`\n"
            + "is used.\n"
            )]
        public static socket create_connection(CodeContext/*!*/ context, PythonTuple address, object timeout) {
            return create_connection(context, address, timeout, null);
        }

        public static socket create_connection(CodeContext/*!*/ context, PythonTuple address, object timeout, PythonTuple source_address) {
            string host = Converter.ConvertToString(address[0]);
            object port = address[1];
            Exception lastException = null;
            var addrInfos = getaddrinfo(context, host, port, 0, SOCK_STREAM, (int)ProtocolType.IP, (int)SocketFlags.None);
            if (addrInfos.Count == 0) {
                throw PythonExceptions.CreateThrowable(error(context), "getaddrinfo returns an empty list");
            }
            foreach (PythonTuple current in addrInfos) {
                int family = Converter.ConvertToInt32(current[0]);
                int socktype = Converter.ConvertToInt32(current[1]);
                int proto = Converter.ConvertToInt32(current[2]);

                // TODO: name is unused?
                // string name = Converter.ConvertToString(current[3]);

                PythonTuple sockaddress = (PythonTuple)current[4];
                socket socket = null;
                try {
                    socket = new socket();
                    socket.__init__(context, family, socktype, proto, null);
                    if (timeout != _GLOBAL_DEFAULT_TIMEOUT) {
                        socket.settimeout(timeout);
                    }
                    if (source_address != null) {
                        socket.bind(source_address);
                    }
                    socket.connect(sockaddress);
                    return socket;
                } catch (Exception ex) {
                    lastException = ex;
                    if (socket != null) {
                        socket.close();
                    }
                }
            }
            throw lastException;
        }

        [Documentation("")]
        public static List getaddrinfo(
            CodeContext/*!*/ context,
            string host,
            object port,
            int family= (int)AddressFamily.Unspecified,
            int socktype=0,
            int proto=(int)ProtocolType.IP,
            int flags=(int)SocketFlags.None
        ) {
            int numericPort;

            if (port == null) {
                numericPort = 0;
            } else if (port is int) {
                numericPort = (int)port;
            } else if (port is Extensible<int>) {
                numericPort = ((Extensible<int>)port).Value;
            } else if (port is string) {
                if (!Int32.TryParse((string)port, out numericPort)) {
                    try{ 
                        port = getservbyname(context,(string)port,null);
                    }
                    catch{
                        throw PythonExceptions.CreateThrowable(gaierror(context), "getaddrinfo failed");
                    }
                }
            } else if (port is ExtensibleString) {
                if (!Int32.TryParse(((ExtensibleString)port).Value, out numericPort)) {
                    try{
                        port = getservbyname(context, (string)port, null);
                    }
                    catch{
                        throw PythonExceptions.CreateThrowable(gaierror(context), "getaddrinfo failed");
                    }
                }
            } else {
                throw PythonExceptions.CreateThrowable(gaierror(context), "getaddrinfo failed");
            }

            if (socktype != 0) {
                // we just use this to validate; socketType isn't actually used
                System.Net.Sockets.SocketType socketType = (System.Net.Sockets.SocketType)Enum.ToObject(typeof(System.Net.Sockets.SocketType), socktype);
                if (socketType == System.Net.Sockets.SocketType.Unknown || !Enum.IsDefined(typeof(System.Net.Sockets.SocketType), socketType)) {
                    throw PythonExceptions.CreateThrowable(gaierror(context), PythonTuple.MakeTuple((int)SocketError.SocketNotSupported, "getaddrinfo failed"));
                }
            }

            AddressFamily addressFamily = (AddressFamily)Enum.ToObject(typeof(AddressFamily), family);
            if (!Enum.IsDefined(typeof(AddressFamily), addressFamily)) {
                throw PythonExceptions.CreateThrowable(gaierror(context), PythonTuple.MakeTuple((int)SocketError.AddressFamilyNotSupported, "getaddrinfo failed"));
            }

            // Again, we just validate, but don't actually use protocolType
            Enum.ToObject(typeof(ProtocolType), proto);

            if (host == null)
                host = "localhost";

            IPAddress[] ips = HostToAddresses(context, host, addressFamily);

            List results = new List();

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

        private static PythonType gaierror(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("socketgaierror");
        }

        private static IPHostEntry GetHostEntry(string host) {
            return Dns.GetHostEntry(host);
        }

        private static IPAddress[] GetHostAddresses(string host) {
            return Dns.GetHostAddresses(host);
        }

        [Documentation("getfqdn([hostname_or_ip]) -> hostname\n\n"
            + "Return the fully-qualified domain name for the specified hostname or IP\n"
            + "address. An unspecified or empty name is interpreted as the local host. If the\n"
            + "name lookup fails, the passed-in name is returned as-is."
            )]
        public static string getfqdn(string host) {
            if (host == null) {
                throw PythonOps.TypeError("expected string, got None");
            }
            host = host.Trim();
            if (host == string.Empty || host == "0.0.0.0") {
                host = gethostname();
            }

            if (host == BroadcastAddrToken) {
                return host;
            }
            try {
                IPHostEntry hostEntry = GetHostEntry(host);
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

        [Documentation("")]
        public static string getfqdn() {
            return getfqdn(LocalhostAddrToken);
        }

        [Documentation("gethostbyname(hostname) -> ip address\n\n"
            + "Return the string IPv4 address associated with the given hostname (e.g.\n"
            + "'10.10.0.1'). The hostname is returned as-is if it an IPv4 address. The empty\n"
            + "string is treated as the local host.\n"
            + "\n"
            + "gethostbyname() doesn't support IPv6; for IPv4/IPv6 support, use getaddrinfo()."
            )]
        public static string gethostbyname(CodeContext/*!*/ context, string host) {
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
        public static PythonTuple gethostbyname_ex(CodeContext/*!*/ context, string host) {
            string hostname;
            List aliases;
            List ips = PythonOps.MakeList();

            IPAddress addr;
            if (IPAddress.TryParse(host, out addr)) {
                if (AddressFamily.InterNetwork == addr.AddressFamily) {
                    hostname = host;
                    aliases = PythonOps.MakeEmptyList(0);
                    ips.append(host);
                } else {
                    throw PythonExceptions.CreateThrowable(gaierror(context), (int)SocketError.HostNotFound, "no IPv4 addresses associated with host");
                }
            } else {
                IPHostEntry hostEntry;
                try {
                    hostEntry = GetHostEntry(host);
                } catch (SocketException e) {
                    throw PythonExceptions.CreateThrowable(gaierror(context), (int)e.SocketErrorCode, "no IPv4 addresses associated with host");
                }
                hostname = hostEntry.HostName;
                aliases = PythonOps.MakeList(hostEntry.Aliases);
                foreach (IPAddress ip in hostEntry.AddressList) {
                    if (AddressFamily.InterNetwork == ip.AddressFamily) {
                        ips.append(ip.ToString());
                    }
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
        public static object gethostbyaddr(CodeContext/*!*/ context, string host) {
            if (host == "") {
                host = gethostname();
            }
            // This conversion seems to match CPython behavior
            host = gethostbyname(context, host);

            IPAddress[] ips = null;
            IPHostEntry hostEntry = null;
            try {
                ips = GetHostAddresses(host);
                hostEntry = GetHostEntry(host);
            }
            catch (Exception e) {
                throw MakeException(context, e);
            }

            List ipStrings = PythonOps.MakeList();
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
        public static object getnameinfo(CodeContext/*!*/ context, PythonTuple socketAddr, int flags) {
            if (socketAddr.__len__() < 2 || socketAddr.__len__() > 4) {
                throw PythonOps.TypeError("socket address must be a 2-tuple (IPv4 or IPv6) or 4-tuple (IPv6)");
            }

            string host = Converter.ConvertToString(socketAddr[0]);
            if (host == null) throw PythonOps.TypeError("argument 1 must be string");
            int port = 0;
            try {
                port = (int)socketAddr[1];
            } catch (InvalidCastException) {
                throw PythonOps.TypeError("an integer is required");
            }

            string resultHost = null;
            string resultPort = null;

            // Host
            IList<IPAddress> addrs = null;
            try {
                addrs = HostToAddresses(context, host, AddressFamily.InterNetwork);
                if (addrs.Count < 1) {
                    throw PythonExceptions.CreateThrowable(error(context), "sockaddr resolved to zero addresses");
                }
            } catch (SocketException e) {
                throw PythonExceptions.CreateThrowable(gaierror(context), (int)e.SocketErrorCode, e.Message);
            } catch (IndexOutOfRangeException) {
                throw PythonExceptions.CreateThrowable(gaierror(context), "sockaddr resolved to zero addresses");
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
                    throw PythonExceptions.CreateThrowable(error(context), "sockaddr resolved to multiple addresses");
                }
                addrs = newAddrs;
            }

            if (addrs.Count < 1) {
                throw PythonExceptions.CreateThrowable(error(context), "sockaddr resolved to zero addresses");
            }

            IPHostEntry hostEntry = null;
            try {
                hostEntry = Dns.GetHostEntry(addrs[0]);
            } catch (SocketException e) {
                throw PythonExceptions.CreateThrowable(gaierror(context), (int)e.SocketErrorCode, e.Message);
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
                try{
                    resultPort = getservbyport(context, port,null);
                }
                catch{
                    resultPort = port.ToString();
                }
                flags = flags | (int)NI_NUMERICSERV;
            }
            else
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
        public static object getprotobyname(CodeContext/*!*/ context, string protocolName) {
            switch (protocolName.ToLower()) {
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
                    throw PythonExceptions.CreateThrowable(error(context), "protocol not found");
            }
        }

        [Documentation("getservbyname(service_name[, protocol_name]) -> port\n\n"
            + "Return a port number from a service name and protocol name.\n"
            + "The optional protocol name, if given, should be 'tcp' or 'udp',\n"
            + "otherwise any protocol will match."
            )]
        public static int getservbyname(CodeContext/*!*/ context, string serviceName, string protocolName=null)
        {
            if(protocolName != null){ 
                protocolName = protocolName.ToLower();
                if(protocolName != "udp" && protocolName != "tcp")
                    throw PythonExceptions.CreateThrowable(error(context), "service/proto not found");
            }

            // try the pinvoke call if it fails fall back to hard coded swtich statement
            try {
                return SocketUtil.GetServiceByName(serviceName, protocolName);
            }
            catch{}
            
    
            switch (serviceName.ToLower()){
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
                    throw PythonExceptions.CreateThrowable(error(context), "service/proto not found");
            }
        }

        [Documentation("getservbyport(port[, protocol_name]) -> service_name\n\n"
            + "Return a service name from a port number and protocol name.\n"
            + "The optional protocol name, if given, should be 'tcp' or 'udp',\n"
            + "otherwise any protocol will match."
            )]
        public static string getservbyport(CodeContext/*!*/ context, int port, string protocolName=null) {

            if (port < 0 || port > 65535)
                throw PythonOps.OverflowError("getservbyport: port must be 0-65535.");

            if (protocolName != null){ 
                protocolName = protocolName.ToLower();
                if (protocolName != "udp" && protocolName != "tcp")
                    throw PythonExceptions.CreateThrowable(error(context), "port/proto not found");
            }

            // try the pinvoke call if it fails fall back to hard coded swtich statement
            try{
                return SocketUtil.GetServiceByPort((ushort)port, protocolName);
            }
            catch{ }

            switch (port)
            {
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
                    throw PythonExceptions.CreateThrowable(error(context), "port/proto not found");
            }
        }

        [Documentation("ntohl(x) -> integer\n\nConvert a 32-bit integer from network byte order to host byte order.")]
        public static object ntohl(object x) {
            int res = IPAddress.NetworkToHostOrder(SignInsensitiveToInt32(x));

            if (res < 0) {
                return (BigInteger)(uint)res;
            } else {
                return res;
            }
        }

        [Documentation("ntohs(x) -> integer\n\nConvert a 16-bit integer from network byte order to host byte order.")]
        public static int ntohs(object x) {
            return (int)(ushort)IPAddress.NetworkToHostOrder(SignInsensitiveToInt16(x));
        }

        [Documentation("htonl(x) -> integer\n\nConvert a 32bit integer from host byte order to network byte order.")]
        public static object htonl(object x) {
            int res = IPAddress.HostToNetworkOrder(SignInsensitiveToInt32(x));

            if (res < 0) {
                return (BigInteger)(uint)res;
            } else {
                return res;
            }
        }

        [Documentation("htons(x) -> integer\n\nConvert a 16-bit integer from host byte order to network byte order.")]
        public static int htons(object x) {
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
        public static string inet_pton(CodeContext/*!*/ context, int addressFamily, string ipString) {
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
                throw PythonExceptions.CreateThrowable(error(context), "illegal IP address passed to inet_pton");
            }
            return ip.GetAddressBytes().MakeString();
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
        public static string inet_ntop(CodeContext/*!*/ context, int addressFamily, string packedIP) {
            if (!(
                (packedIP.Length == IPv4AddrBytes && addressFamily == (int)AddressFamily.InterNetwork)
                || (packedIP.Length == IPv6AddrBytes && addressFamily == (int)AddressFamily.InterNetworkV6)
            )) {
                throw PythonExceptions.CreateThrowable(error(context), "invalid length of packed IP address string");
            }
            byte[] ipBytes = packedIP.MakeByteArray();
            if (addressFamily == (int)AddressFamily.InterNetworkV6) {
                return IPv6BytesToColonHex(ipBytes);
            }
            return (new IPAddress(ipBytes)).ToString();
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
        public static string inet_aton(CodeContext/*!*/ context, string ipString) {
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
        public static string inet_ntoa(CodeContext/*!*/ context, string packedIP) {
            return inet_ntop(context, (int)AddressFamily.InterNetwork, packedIP);
        }

        [Documentation("getdefaulttimeout() -> timeout\n\n"
            + "Return the default timeout for new socket objects in seconds as a float. A\n"
            + "value of None means that sockets have no timeout and begin in blocking mode.\n"
            + "The default value when the module is imported is None."
            )]
        public static object getdefaulttimeout(CodeContext/*!*/ context) {
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
        public static void setdefaulttimeout(CodeContext/*!*/ context, object timeout) {
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
        public const int AF_UNSPEC = (int)AddressFamily.Unspecified;
        public const int AI_CANONNAME = (int)0x2;
        public const int AI_NUMERICHOST = (int)0x4;
        public const int AI_PASSIVE = (int)0x1;
        public const int EAI_AGAIN = (int)SocketError.TryAgain;
        public const int EAI_BADFLAGS = (int)SocketError.InvalidArgument;
        public const int EAI_FAIL = (int)SocketError.NoRecovery;
        public const int EAI_FAMILY = (int)SocketError.AddressFamilyNotSupported;
        public const int EAI_MEMORY = (int)SocketError.NoBufferSpaceAvailable;
        public const int EAI_NODATA = (int)SocketError.HostNotFound; // not SocketError.NoData, like you would think
        public const int EAI_NONAME = (int)SocketError.HostNotFound;
        public const int EAI_SERVICE = (int)SocketError.TypeNotFound;
        public const int EAI_SOCKTYPE = (int)SocketError.SocketNotSupported;
        public const int EAI_SYSTEM = (int)SocketError.SocketError;
        public const int EBADF = (int)0x9;
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
        public const int IPV6_HOPLIMIT = (int)SocketOptionName.HopLimit;
        public const int IPV6_JOIN_GROUP = (int)SocketOptionName.AddMembership;
        public const int IPV6_LEAVE_GROUP = (int)SocketOptionName.DropMembership;
        public const int IPV6_MULTICAST_HOPS = (int)SocketOptionName.MulticastTimeToLive;
        public const int IPV6_MULTICAST_IF = (int)SocketOptionName.MulticastInterface;
        public const int IPV6_MULTICAST_LOOP = (int)SocketOptionName.MulticastLoopback;
        public const int IPV6_PKTINFO = (int)SocketOptionName.PacketInformation;
        public const int IPV6_UNICAST_HOPS = (int)SocketOptionName.IpTimeToLive;
#if FEATURE_IPV6 && __MonoCS__
        //TODO: Is this filed in a Mono bug report?
        public const int IPV6_V6ONLY = 27;
#elif FEATURE_IPV6
        public const int IPV6_V6ONLY = (int) SocketOptionName.IPv6Only;
#endif
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
        public const int SOL_IPV6 = (int)SocketOptionLevel.IPv6;
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly BigInteger SIO_RCVALL = (long)IOControlCode.ReceiveAll;
        public static readonly BigInteger SIO_KEEPALIVE_VALS = (long)IOControlCode.KeepAliveValues;
        public const int RCVALL_ON = 1;
        public const int RCVALL_OFF = 0;
        public const int RCVALL_SOCKETLEVELONLY = 2;
        public const int RCVALL_MAX = 3;

        public const int has_ipv6 = (int)1;

        #endregion

        #region Private implementation

        /// <summary>
        /// Return a standard socket exception (socket.error) whose message and error code come from a SocketException
        /// This will eventually be enhanced to generate the correct error type (error, herror, gaierror) based on the error code.
        /// </summary>
        internal static Exception MakeException(CodeContext/*!*/ context, Exception exception) {
            // !!! this shouldn't just blindly set the type to error (see summary)
            if (exception is SocketException) {
                SocketException se = (SocketException)exception;
                switch (se.SocketErrorCode) {
                    case SocketError.NotConnected:  // CPython times out when the socket isn't connected.
                    case SocketError.TimedOut:
                        return PythonExceptions.CreateThrowable(timeout(context), (int)se.SocketErrorCode, se.Message);
                    default:
                        return PythonExceptions.CreateThrowable(error(context), (int)se.SocketErrorCode, se.Message);
                }
            } else if (exception is ObjectDisposedException) {
                return PythonExceptions.CreateThrowable(error(context), (int)EBADF, "the socket is closed");
            } else if (exception is InvalidOperationException) {
                return MakeException(context, new SocketException((int)SocketError.InvalidArgument));
            } else {
                return exception;
            }
        }

        /// <summary>
        /// Convert an IPv6 address byte array to a string in standard colon-hex notation.
        /// The .NET IPAddress.ToString() method uses dotted-quad for the last 32 bits,
        /// which differs from the normal Python implementation (but is allowed by the IETF);
        /// this method returns the standard (no dotted-quad) colon-hex form.
        /// </summary>
        private static string IPv6BytesToColonHex(byte[] ipBytes) {
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
                IPAddress addr;

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
                    if (dotCount == 3 && IPAddress.TryParse(host, out addr)) {
                        if (family == AddressFamily.Unspecified || family == addr.AddressFamily) {
                            return new IPAddress[] { addr };
                        }
                    }
                    // Incorrect family will raise exception below
                } else {
                    IPHostEntry hostEntry = GetHostEntry(host);
                    List<IPAddress> addrs = new List<IPAddress>();
                    foreach (IPAddress ip in hostEntry.AddressList) {
                        if (family == AddressFamily.Unspecified || family == ip.AddressFamily) {
                            addrs.Add(ip);
                        }
                    }
                    if (addrs.Count > 0) return addrs.ToArray();
                }
                throw new SocketException((int)SocketError.HostNotFound);
            } catch (SocketException e) {
                throw PythonExceptions.CreateThrowable(gaierror(context), (int)e.SocketErrorCode, "no addresses of the specified family associated with host");
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
            if (address.__len__() != 2 && address.__len__() != 4) {
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

            if (address.__len__() == 2) {
                return new IPEndPoint(ip, port);
            } else {
                try {
                    Converter.ConvertToInt64(address[2]);
                } catch (ArgumentTypeException) {
                    throw PythonOps.TypeError("flowinfo must be integer");
                }
                // We don't actually do anything with flowinfo right now, but we validate it
                // in case we want to do something in the future.

                long scopeId;
                try {
                    scopeId = Converter.ConvertToInt64(address[3]);
                } catch (ArgumentTypeException) {
                    throw PythonOps.TypeError("scopeid must be integer");
                }

                IPEndPoint endPoint = new IPEndPoint(ip, port);
                endPoint.Address.ScopeId = scopeId;
                return endPoint;
            }
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

        class PythonUserSocketStream : Stream {
            private readonly object _userSocket;
            private List<string> _data = new List<string>();
            private int _dataSize;
            private readonly int _bufSize;
            private readonly bool _close;

            public PythonUserSocketStream(object userSocket, int bufferSize, bool close) {
                _userSocket = userSocket;
                _bufSize = bufferSize;
                _close = close;
            }

            public override bool CanRead {
                get { return true; }
            }

            public override bool CanSeek {
                get { return false; }
            }

            public override bool CanWrite {
                get { return true; }
            }

            public override void Flush() {
                if (_data.Count > 0) {
                    StringBuilder res = new StringBuilder();
                    foreach (string s in _data) {
                        res.Append(s);
                    }
                    DefaultContext.DefaultPythonContext.CallSplat(PythonOps.GetBoundAttr(DefaultContext.Default, _userSocket, "sendall"), res.ToString());
                    _data.Clear();
                }
            }

            public override long Length {
                get { throw new NotImplementedException(); }
            }

            public override long Position {
                get {
                    throw new NotImplementedException();
                }
                set {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count) {
                object received = DefaultContext.DefaultPythonContext.CallSplat(PythonOps.GetBoundAttr(DefaultContext.Default, _userSocket, "recv"), count);
                string data = Converter.ConvertToString(received);

                return PythonAsciiEncoding.Instance.GetBytes(data, 0, data.Length, buffer, offset);
            }

            public override long Seek(long offset, SeekOrigin origin) {
                throw new NotImplementedException();
            }

            public override void SetLength(long value) {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count) {
                string strData = new string(PythonAsciiEncoding.Instance.GetChars(buffer, offset, count));
                _data.Add(strData);
                _dataSize += strData.Length;
                if (_dataSize > _bufSize) {
                    Flush();
                }
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                    object closeObj;
                    if (PythonOps.TryGetBoundAttr(_userSocket, "close", out closeObj))
                        PythonCalls.Call(closeObj);
                }

                base.Dispose(disposing);
            }
        }

        [PythonType]
        public class _fileobject : PythonFile {
            public new const string name = "<socket>";
            private readonly socket _socket;
            public const string __module__ = "socket";
            private bool _close;

            public object _sock; // Only present for compatibility with CPython tests

            public object bufsize = DefaultBufferSize; // Only present for compatibility with CPython public API

            public _fileobject(CodeContext/*!*/ context, object socket, string mode="rb", int bufsize=-1, bool close=false)
                : base(context.LanguageContext) {

                Stream stream;
                _close = close;
                // subtypes of socket need to go through the user defined methods
                if (socket != null && socket.GetType() == typeof(socket) && ((socket)socket)._socket.Connected) {
                    socket s = (socket as socket);
                    _socket = s;
                    stream = new NetworkStream(s._socket, false);
                } else {
                    _socket = null;
                    stream = new PythonUserSocketStream(socket, GetBufferSize(context, bufsize), close);
                }
                _sock = socket;
               
                base.__init__(stream, Encoding.GetEncoding(0), mode);

                _isOpen = socket != null;
                _close = (socket == null) ? false : close;
            }

            public void __init__(params object[] args) {
            }

            public void __init__([ParamDictionary]IDictionary<object, object> kwargs, params object[] args) {
            }

            public void __del__() {
                if (_socket != null && _isOpen) {
                    if (_close) _socket.close();
                    _isOpen = false;
                }
            }

            protected override void Dispose(bool disposing) {
                if (_socket != null && _isOpen) {
                    if (_close) _socket.close();
                    _isOpen = false;
                }
                base.Dispose(disposing);
            }

            public override object close() {
                if (!_isOpen) return null;
                if (_socket != null && _close) {
                    _socket.close();
                }
                else if (this._stream != null && _close) {
                    _stream.Dispose();
                }
                _isOpen = false;
                var obj = base.close();
                return obj;
            }

            private static int GetBufferSize(CodeContext/*!*/ context, int size) {
                if (size == -1) return Converter.ConvertToInt32(Getdefault_bufsize(context));
                return size;
            }

            [SpecialName, PropertyMethod, StaticExtensionMethod]
            public static object Getdefault_bufsize(CodeContext/*!*/ context) {
                return context.LanguageContext.GetModuleState(_defaultBufsizeKey);
            }

            [SpecialName, PropertyMethod, StaticExtensionMethod]
            public static void Setdefault_bufsize(CodeContext/*!*/ context, object value) {
                context.LanguageContext.SetModuleState(_defaultBufsizeKey, value);
            }

        }
        #endregion

        private static int? GetDefaultTimeout(CodeContext/*!*/ context) {
            return (int?)context.LanguageContext.GetModuleState(_defaultTimeoutKey);
        }

        private static void SetDefaultTimeout(CodeContext/*!*/ context, int? timeout) {
            context.LanguageContext.SetModuleState(_defaultTimeoutKey, timeout);
        }

        private static PythonType error(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("socketerror");
        }

        private static PythonType herror(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("socketherror");
        }

        private static PythonType timeout(CodeContext/*!*/ context) {
            return (PythonType)context.LanguageContext.GetModuleState("sockettimeout");
        }

        public class ssl {
            private SslStream _sslStream;
            private socket _socket;
            private readonly X509Certificate2Collection _certCollection;
            private readonly X509Certificate _cert;
            private readonly int _protocol, _certsMode;
            private readonly bool _validate, _serverSide;
            private readonly CodeContext _context;
            private readonly RemoteCertificateValidationCallback _callback;
            private Exception _validationFailure;
            private static readonly bool _supportsTls12;
            private static readonly SslProtocols _SslProtocol_Tls12;
            private static readonly SslProtocols _SslProtocol_Tls11;

            static ssl()
            {
                // If .Net 4.5 or 4.6 are installed, try and detect
                // the availability of Tls11/Tls12 dynamically:
                SslProtocols result = SslProtocols.None;
                bool succeeded = false;
                try {
                    result = (SslProtocols)Enum.Parse(typeof(SslProtocols), "Tls12");
                    succeeded = true;
                }
                catch (ArgumentException) {
                    // This is thrown for invalid enumeration values:
                    // This is ok on .Net 2.0 or .Net 4.0 without .Net 4.5/4.6 installed.
                }
                _supportsTls12 = succeeded;
                if (_supportsTls12)
                {
                    _SslProtocol_Tls12 = result;
                    try {
                        _SslProtocol_Tls11 = (SslProtocols)Enum.Parse(typeof(SslProtocols), "Tls11");
                    }
                    catch (ArgumentException) {
                        // This is thrown for invalid enumeration values by Enum.Parse.
                        throw new InvalidOperationException("Tls12 exists in SslProtocols but not Tls11? Very unexpected!");
                    }
                }
                else
                {
                    _SslProtocol_Tls11 = SslProtocols.None;
                    _SslProtocol_Tls12 = SslProtocols.None;
                }
            }

            public ssl(CodeContext context, PythonSocket.socket sock, string keyfile=null, string certfile=null, X509Certificate2Collection certs=null) {
                _context = context;
                _sslStream = new SslStream(new NetworkStream(sock._socket, false), true, CertValidationCallback);
                _socket = sock;
                _protocol = PythonSsl.PROTOCOL_SSLv23 | PythonSsl.OP_NO_SSLv2 | PythonSsl.OP_NO_SSLv3;
                _validate = false;
                _certCollection = certs ?? new X509Certificate2Collection();
            }

            internal ssl(CodeContext context,
               PythonSocket.socket sock,
               bool server_side,
               string keyfile=null,
               string certfile=null,
               int certs_mode=PythonSsl.CERT_NONE,
               int protocol=(PythonSsl.PROTOCOL_SSLv23 | PythonSsl.OP_NO_SSLv2 | PythonSsl.OP_NO_SSLv3),
               string cacertsfile=null,
               X509Certificate2Collection certs=null) {
                if (sock == null) {
                    throw PythonOps.TypeError("expected socket object, got None");
                }

                _serverSide = server_side;
                bool validate;
                _certsMode = certs_mode;

                RemoteCertificateValidationCallback callback;
                switch (certs_mode) {
                    case PythonSsl.CERT_NONE:
                        validate = false;
                        callback = CertValidationCallback;
                        break;
                    case PythonSsl.CERT_OPTIONAL:
                        validate = true;
                        callback = CertValidationCallbackOptional;
                        break;
                    case PythonSsl.CERT_REQUIRED:
                        validate = true;
                        callback = CertValidationCallbackRequired;
                        break;
                    default:
                        throw new InvalidOperationException(String.Format("bad certs_mode: {0}", certs_mode));
                }

                _callback = callback;

                if (certs != null) {
                    _certCollection = certs;
                }

                if (certfile != null) {
                    _cert = PythonSsl.ReadCertificate(context, certfile);
                }

                if (cacertsfile != null) {
                    _certCollection = new X509Certificate2Collection(new[] { PythonSsl.ReadCertificate(context, cacertsfile) });
                }

                _socket = sock;

                EnsureSslStream(false);

                _protocol = protocol;
                _validate = validate;
                _context = context;
            }

            private void EnsureSslStream(bool throwWhenNotConnected) {
                if (_sslStream == null && _socket._socket.Connected) {
                    if (_serverSide) {
                        _sslStream = new SslStream(
                            new NetworkStream(_socket._socket, false),
                            true,
                            _callback
                        );
                    } else {
                        _sslStream = new SslStream(
                            new NetworkStream(_socket._socket, false),
                            true,
                            _callback,
                            CertSelectLocal
                        );
                    }
                }

                if (throwWhenNotConnected && _sslStream == null) {
                    var socket = _context.LanguageContext.GetBuiltinModule("_socket");
                    var socketError = PythonSocket.GetSocketError(_context.LanguageContext, socket.__dict__);

                    throw PythonExceptions.CreateThrowable(socketError, 10057, "A request to send or receive data was disallowed because the socket is not connected.");
                }
            }

            internal bool CertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                return true;
            }

            internal bool CertValidationCallbackOptional(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                if (!_serverSide) {
                    if (certificate != null && sslPolicyErrors != SslPolicyErrors.None) {
                        ValidateCertificate(certificate, chain, sslPolicyErrors);
                    }
                }

                return true;
            }

            internal X509Certificate CertSelectLocal(object sender, string targetHost, X509CertificateCollection collection, X509Certificate remoteCertificate, string[] acceptableIssuers) {
                if (acceptableIssuers != null && acceptableIssuers.Length > 0 && collection != null && collection.Count > 0) {
                    // Use the first certificate that is from an acceptable issuer.
                    foreach (X509Certificate certificate in collection) {
                        string issuer = certificate.Issuer;
                        if (Array.IndexOf(acceptableIssuers, issuer) != -1)
                            return certificate;
                    }
                }

                if (collection != null && collection.Count > 0) {
                    return collection[0];
                }

                return null;
            }

            internal bool CertValidationCallbackRequired(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                if (!_serverSide) {
                    // client check
                    if (certificate == null) {
                        ValidationError(SslPolicyErrors.None);
                    } else if (sslPolicyErrors != SslPolicyErrors.None) {
                        ValidateCertificate(certificate, chain, sslPolicyErrors);
                    }
                }

                return true;
            }

            private void ValidateCertificate(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                chain = new X509Chain();
                X509Certificate2Collection certificates = new X509Certificate2Collection();
                foreach (object cert in _certCollection) {
                    if (cert is X509Certificate2) {
                        certificates.Add((X509Certificate2)cert);
                    }
                    else if (cert is X509Certificate) {
                        certificates.Add(new X509Certificate2((X509Certificate)cert));
                    }
                }
                chain.ChainPolicy.ExtraStore.AddRange(certificates);
                chain.Build(new X509Certificate2(certificate));

                if (chain.ChainStatus.Length > 0) {
                    foreach (var elem in chain.ChainStatus) {
                        if (elem.Status == X509ChainStatusFlags.UntrustedRoot) {
                            bool isOk = false;
                            foreach (var cert in _certCollection) {
                                if (certificate.Issuer == cert.Subject) {
                                    isOk = true;
                                }
                            }

                            if (isOk) {
                                continue;
                            }
                        }

                        ValidationError(sslPolicyErrors);
                        break;
                    }
                }
            }

            private void ValidationError(object reason) {
                _validationFailure = PythonExceptions.CreateThrowable(PythonSsl.SSLError(_context), "errors while validating certificate chain: ", reason.ToString());
            }

            public void do_handshake() {
                try {
                    // make sure the remote side hasn't shutdown before authenticating so we don't
                    // hang if we're in blocking mode.
#pragma warning disable 219 // unused variable
                    int available = _socket._socket.Available;
#pragma warning restore 219
                } catch (SocketException) {
                    throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, "socket closed before handshake");
                }

                EnsureSslStream(true);

                try {
                    if (_serverSide) {
#if NETCOREAPP2_0
                        _sslStream.AuthenticateAsServerAsync(_cert, _certsMode == PythonSsl.CERT_REQUIRED, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false).Wait();
#else
                        _sslStream.AuthenticateAsServer(_cert, _certsMode == PythonSsl.CERT_REQUIRED, SslProtocols.Default, false);
#endif
                    } else {

                        var collection = new X509CertificateCollection();

                        if (_cert != null) {
                            collection.Add(_cert);
                        }
#if NETCOREAPP2_0
                        _sslStream.AuthenticateAsClientAsync(_socket._hostName, collection, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false).Wait();
#else
                        _sslStream.AuthenticateAsClient(_socket._hostName, collection, SslProtocols.Default, false);
#endif
                    }
                } catch (AuthenticationException e) {
                    ((IDisposable)_socket._socket).Dispose();
                    throw PythonExceptions.CreateThrowable(PythonSsl.SSLError(_context), "errors while performing handshake: ", e.ToString());
                }

                if (_validationFailure != null) {
                    throw _validationFailure;
                }
            }

            public socket shutdown() {
                _sslStream.Dispose();
                return _socket;
            }

            /* supported communication based upon what the client & server specify
             * as per the CPython docs:
             * client / server SSLv2 SSLv3 SSLv23 TLSv1 
                         SSLv2 yes      no   yes*    no 
                         SSLv3 yes     yes   yes     no 
                        SSLv23 yes      no   yes     no 
                         TLSv1 no       no   yes    yes 
             */

            private static SslProtocols GetProtocolType(int type) {
                SslProtocols result = SslProtocols.None;

                switch (type & ~PythonSsl.OP_NO_ALL) {
#pragma warning disable CS0618 // Type or member is obsolete
                    case PythonSsl.PROTOCOL_SSLv2:
                        result = SslProtocols.Ssl2;
                        break;
                    case PythonSsl.PROTOCOL_SSLv3:
                        result = SslProtocols.Ssl3;
                        break;
                    case PythonSsl.PROTOCOL_SSLv23:
                        result = SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | _SslProtocol_Tls11 | _SslProtocol_Tls12;
                        break;
#pragma warning restore CS0618 // Type or member is obsolete
                    case PythonSsl.PROTOCOL_TLSv1:
                        result = SslProtocols.Tls;
                        break;
                    case PythonSsl.PROTOCOL_TLSv1_1:
                        if (!_supportsTls12) {
                            throw new InvalidOperationException("bad ssl protocol type: " + type);
                        }
                        result = _SslProtocol_Tls11;
                        break;
                    case PythonSsl.PROTOCOL_TLSv1_2:
                        if (!_supportsTls12) {
                            throw new InvalidOperationException("bad ssl protocol type: " + type);
                        }
                        result = _SslProtocol_Tls12;
                        break;
                    default:
                        throw new InvalidOperationException("bad ssl protocol type: " + type);
                }
                // Filter out requested protocol exclusions:
#pragma warning disable CS0618 // Type or member is obsolete
                result &= (type & PythonSsl.OP_NO_SSLv3) != 0 ? ~SslProtocols.Ssl3 : ~SslProtocols.None;
                result &= (type & PythonSsl.OP_NO_SSLv2) != 0 ? ~SslProtocols.Ssl2 : ~SslProtocols.None;
#pragma warning restore CS0618 // Type or member is obsolete
                result &= (type & PythonSsl.OP_NO_TLSv1) != 0 ? ~SslProtocols.Tls : ~SslProtocols.None;
                if (_supportsTls12)
                {
                    result &= (type & PythonSsl.OP_NO_TLSv1_1) != 0 ? ~_SslProtocol_Tls11 : ~SslProtocols.None;
                    result &= (type & PythonSsl.OP_NO_TLSv1_2) != 0 ? ~_SslProtocol_Tls12 : ~SslProtocols.None;
                }
                return result;
            }

            public PythonTuple cipher() {
                if (_sslStream != null && _sslStream.IsAuthenticated) {
                    return PythonTuple.MakeTuple(
                        _sslStream.CipherAlgorithm.ToString(),
                        ProtocolToPython(),
                        _sslStream.CipherStrength
                    );
                }
                return null;
            }

            private string ProtocolToPython() {
                switch (_sslStream.SslProtocol) {
#pragma warning disable CS0618 // Type or member is obsolete
                    case SslProtocols.Ssl2: return "SSLv2";
                    case SslProtocols.Ssl3: return "TLSv1/SSLv3";
#pragma warning restore CS0618 // Type or member is obsolete
                    case SslProtocols.Tls: return "TLSv1";
                    default: return _sslStream.SslProtocol.ToString();
                }
            }

            public object peer_certificate(bool binary_form) {
                if (_sslStream != null) {
                    var peerCert = _sslStream.RemoteCertificate;

                    if (peerCert != null) {
                        if (binary_form) {
                            return peerCert.GetRawCertData().MakeString();
                        } else if (_validate) {
                            return PythonSsl.CertificateToPython(_context, peerCert, true);
                        }
                    }
                }
                return null;
            }

            public int pending() {
                return _socket._socket.Available;
            }

            [Documentation("issuer() -> issuer_certificate\n\n"
                + "Returns a string that describes the issuer of the server's certificate. Only useful for debugging purposes."
                )]
            public string issuer() {
                if (_sslStream != null && _sslStream.IsAuthenticated) {
                    X509Certificate remoteCertificate = _sslStream.RemoteCertificate;
                    if (remoteCertificate != null) {
                        return remoteCertificate.Issuer;
                    } else {
                        return String.Empty;
                    }
                }
                return String.Empty;
            }

            [Documentation(@"read([len]) -> string

Read up to len bytes from the SSL socket.")]
            public string read(CodeContext/*!*/ context, int len) {
                EnsureSslStream(true);

                try {
                    byte[] buffer = new byte[2048];
                    MemoryStream result = new MemoryStream(len);
                    while (true) {
                        int readLength = (len < buffer.Length) ? len : buffer.Length;
                        int bytes = _sslStream.Read(buffer, 0, readLength);
                        if (bytes > 0) {
                            result.Write(buffer, 0, bytes);
                            len -= bytes;
                        }
                        if (bytes == 0 || len == 0 || bytes < readLength) {
                            return result.ToArray().MakeString();
                        }
                    }

                } catch (Exception e) {
                    throw PythonSocket.MakeException(context, e);
                }
            }

            [Documentation("server() -> server_certificate\n\n"
                + "Returns a string that describes the server's certificate. Only useful for debugging purposes."
                )]
            public string server() {
                if (_sslStream != null && _sslStream.IsAuthenticated) {
                    X509Certificate remoteCertificate = _sslStream.RemoteCertificate;
                    if (remoteCertificate != null) {
                        return remoteCertificate.Subject;
                    }
                }
                return String.Empty;
            }

            [Documentation("write(s) -> bytes_sent\n\n"
                + "Writes the string s through the SSL connection."
                )]
            public int write(CodeContext/*!*/ context, string data) {
                EnsureSslStream(true);

                byte[] buffer = data.MakeByteArray();
                try {
                    _sslStream.Write(buffer);
                    return buffer.Length;
                } catch (Exception e) {
                    throw PythonSocket.MakeException(context, e);
                }
            }
        }
    }


    internal class SocketUtilException : Exception {

        public SocketUtilException() {
        }

        public SocketUtilException(string message)
            : base(message) {
        }

        public SocketUtilException(string message, Exception inner)
            : base(message, inner) {
        }

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
        public struct servent64
        {
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
        private static extern IntPtr getservbyname_linux(string name, string proto);
        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "getservbyport")]
        private static extern IntPtr getservbyport_linux(ushort port, string proto);


        [DllImport("ws2_32.dll", SetLastError = true)]
        static extern Int32 WSAStartup(Int16 wVersionRequested, out WSAData wsaData);
        [DllImport("ws2_32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr getservbyname(string name, string proto);
        [DllImport("ws2_32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr getservbyport(ushort port, string proto);
        [DllImport("Ws2_32.dll", SetLastError = true)]
        private static extern Int32 WSAGetLastError();
        [DllImport("ws2_32.dll", SetLastError = true)]
        static extern Int32 WSACleanup();

        private static T PtrToStructure<T>(IntPtr result) {
            return (T)Marshal.PtrToStructure(result, typeof(T));
        }

        public static string GetServiceByPortWindows(ushort port, string protocol) {

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

        public static string GetServiceByPortNonWindows(ushort port, string protocol) {
            var netport = unchecked((ushort)IPAddress.HostToNetworkOrder(unchecked((short)port)));
            var result = getservbyport_linux(netport, protocol);
            if (IntPtr.Zero == result) {
                throw new SocketUtilException(
                    string.Format("Could not resolve service for port {0}", port));
            }

            return PtrToStructure<servent>(result).s_name;
        }

        public static string GetServiceByPort(ushort port, string protocol) {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                return GetServiceByPortNonWindows(port, protocol);
            else
                return GetServiceByPortWindows(port, protocol);
        }


        public static ushort GetServiceByNameWindows(string service, string protocol) {

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

        public static ushort GetServiceByNameNonWindows(string service, string protocol) {
            var result = getservbyname_linux(service, protocol);
            if (IntPtr.Zero == result) {
                throw new SocketUtilException(
                    string.Format("Could not resolve port for service {0}", service));
            }

            ushort port = PtrToStructure<servent>(result).s_port;
            var hostport = IPAddress.NetworkToHostOrder(unchecked((short)port));
            return unchecked((ushort)hostport);
        }

        public static ushort GetServiceByName(string service, string protocol) {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                return GetServiceByNameNonWindows(service, protocol);
            else
                return GetServiceByNameWindows(service, protocol);
        }

    }
}



#endif
