# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# test _socket
#

import os
import _socket
import sys
import _thread
import time
import unittest

from iptest import IronPythonTestCase, is_cli, is_osx, is_linux, is_windows, is_cpython, run_test

AF_DICT = {"AF_APPLETALK" : 5,
           "AF_DECnet" : 12,
           "AF_INET" : 2,
           "AF_INET6" : 10,
           "AF_IPX" : 4,
           "AF_IRDA" : 23,
           "AF_SNA" : 22,
           "AF_UNSPEC" : 0,
}

ST_DICT = {"SOCK_DGRAM" : 2,
           "SOCK_RAW" : 3,
           "SOCK_RDM" : 4,
           "SOCK_SEQPACKET" : 5,
           "SOCK_STREAM" : 1,
           }

IPPROTO_DICT = { "IPPROTO_AH" : 51,
                 "IPPROTO_DSTOPTS" : 60,
                 "IPPROTO_ESP" : 50,
                 "IPPROTO_FRAGMENT" : 44,
                 "IPPROTO_HOPOPTS" : 0,
                 "IPPROTO_ICMP" : 1,
                 "IPPROTO_ICMPV6" : 58,
                 "IPPROTO_IDP" : 22,
                 "IPPROTO_IGMP" : 2,
                 "IPPROTO_IP" : 0,
                 "IPPROTO_IPV6" : 41,
                 "IPPROTO_NONE" : 59,
                 "IPPROTO_PUP" : 12,
                 "IPPROTO_RAW" : 255,
                 "IPPROTO_ROUTING" : 43,
                 "IPPROTO_TCP" : 6,
                 "IPPROTO_UDP" : 17,
}

# Linux-specific values
OTHER_GLOBALS = {"AI_ADDRCONFIG" : 32,
                 "AI_ALL" : 16,
                 "AI_CANONNAME" : 2,
                 "AI_NUMERICHOST" : 4,
                 "AI_PASSIVE" : 1,
                 "AI_V4MAPPED" : 8,

                 "EAI_ADDRFAMILY" : -9,
                 "EAI_AGAIN" : -3,
                 "EAI_BADFLAGS" : -1,
                 "EAI_FAIL" : -4,
                 "EAI_FAMILY" : -6,
                 "EAI_MEMORY" : -10,
                 "EAI_NODATA" : -5,
                 "EAI_NONAME" : -2,
                 "EAI_SERVICE" : -8,
                 "EAI_SOCKTYPE" : -7,
                 "EAI_SYSTEM" : -11,
                 "EAI_OVERFLOW" : -12,

                 "INADDR_ALLHOSTS_GROUP" : -536870911,
                 "INADDR_ANY" : 0,
                 "INADDR_BROADCAST" : -1,
                 "INADDR_LOOPBACK" : 2130706433,
                 "INADDR_MAX_LOCAL_GROUP" : -536870657,
                 "INADDR_NONE" : -1,
                 "INADDR_UNSPEC_GROUP" : -536870912,

                 "IPPORT_RESERVED" : 1024,
                 "IPPORT_USERRESERVED" : 5000,

                 "IPV6_CHECKSUM" : 7,
                 "IPV6_DSTOPTS" : 4,
                 "IPV6_HOPLIMIT" : 8,
                 "IPV6_HOPOPTS" : 3,
                 "IPV6_JOIN_GROUP" : 20,
                 "IPV6_LEAVE_GROUP" : 21,
                 "IPV6_MULTICAST_HOPS" : 18,
                 "IPV6_MULTICAST_IF" : 17,
                 "IPV6_MULTICAST_LOOP" : 19,
                 "IPV6_NEXTHOP" : 9,
                 "IPV6_PKTINFO" : 2,
                 "IPV6_RTHDR" : 5,
                 "IPV6_RTHDR_TYPE_0" : 0,
                 "IPV6_UNICAST_HOPS" : 16,
                 "IPV6_V6ONLY" : 26,
                 "IP_ADD_MEMBERSHIP" : 35,
                 "IP_DEFAULT_MULTICAST_LOOP" : 1,
                 "IP_DEFAULT_MULTICAST_TTL" : 1,
                 "IP_DROP_MEMBERSHIP" : 36,
                 "IP_HDRINCL" : 3,
                 "IP_MAX_MEMBERSHIPS" : 20,
                 "IP_MULTICAST_IF" : 32,
                 "IP_MULTICAST_LOOP" : 34,
                 "IP_MULTICAST_TTL" : 33,
                 "IP_OPTIONS" : 4,
                 "IP_RECVOPTS" : 6,
                 "IP_RECVRETOPTS" : 7,
                 "IP_RETOPTS" : 7,
                 "IP_TOS" : 1,
                 "IP_TTL" : 2,
                 "MSG_CTRUNC" : 8,
                 "MSG_DONTROUTE" : 4,
                 "MSG_DONTWAIT" : 64,
                 "MSG_EOR" : 128,
                 "MSG_OOB" : 1,
                 "MSG_PEEK" : 2,
                 "MSG_TRUNC" : 32,
                 "MSG_WAITALL" : 256,
                 "NI_DGRAM" : 16,
                 "NI_MAXHOST" : 1025,
                 "NI_MAXSERV" : 32,
                 "NI_NAMEREQD" : 8,
                 "NI_NOFQDN" : 4,
                 "NI_NUMERICHOST" : 1,
                 "NI_NUMERICSERV" : 2,
                 "PACKET_BROADCAST" : 1,
                 "PACKET_FASTROUTE" : 6,
                 "PACKET_HOST" : 0,
                 "PACKET_LOOPBACK" : 5,
                 "PACKET_MULTICAST" : 2,
                 "PACKET_OTHERHOST" : 3,
                 "PACKET_OUTGOING" : 4,
                 "PF_PACKET" : 17,
                 "SHUT_RD" : 0,
                 "SHUT_RDWR" : 2,
                 "SHUT_WR" : 1,
                 "SOL_IP" : 0,
                 "SOL_SOCKET" : 1,
                 "SOL_TCP" : 6,
                 "SOL_UDP" : 17,
                 "SOMAXCONN" : 128,
                 "SO_ACCEPTCONN" : 30,
                 "SO_BROADCAST" : 6,
                 "SO_DEBUG" : 1,
                 "SO_DONTROUTE" : 5,
                 "SO_ERROR" : 4,
                 "SO_KEEPALIVE" : 9,
                 "SO_LINGER" : 13,
                 "SO_OOBINLINE" : 10,
                 "SO_RCVBUF" : 8,
                 "SO_RCVLOWAT" : 18,
                 "SO_RCVTIMEO" : 20,
                 "SO_REUSEADDR" : 2,
                 "SO_SNDBUF" : 7,
                 "SO_SNDLOWAT" : 19,
                 "SO_SNDTIMEO" : 21,
                 "SO_TYPE" : 3,
                 "SSL_ERROR_EOF" : 8,
                 "SSL_ERROR_INVALID_ERROR_CODE" : 9,
                 "SSL_ERROR_SSL" : 1,
                 "SSL_ERROR_SYSCALL" : 5,
                 "SSL_ERROR_WANT_CONNECT" : 7,
                 "SSL_ERROR_WANT_READ" : 2,
                 "SSL_ERROR_WANT_WRITE" : 3,
                 "SSL_ERROR_WANT_X509_LOOKUP" : 4,
                 "SSL_ERROR_ZERO_RETURN" : 6,
                 "TCP_CORK" : 3,
                 "TCP_DEFER_ACCEPT" : 9,
                 "TCP_INFO" : 11,
                 "TCP_KEEPCNT" : 6,
                 "TCP_KEEPIDLE" : 4,
                 "TCP_KEEPINTVL" : 5,
                 "TCP_LINGER2" : 8,
                 "TCP_MAXSEG" : 2,
                 "TCP_NODELAY" : 1,
                 "TCP_QUICKACK" : 12,
                 "TCP_SYNCNT" : 7,
                 "TCP_WINDOW_CLAMP" : 10}

# CPython: {s: getattr(_socket, s) for s in dir(_socket) if s.startswith("EAI_")}
EAI_DARWIN = {'EAI_ADDRFAMILY': 1, 'EAI_AGAIN': 2, 'EAI_BADFLAGS': 3, 'EAI_BADHINTS': 12, 'EAI_FAIL': 4, 'EAI_FAMILY': 5, 'EAI_MAX': 15, 'EAI_MEMORY': 6, 'EAI_NODATA': 7, 'EAI_NONAME': 8, 'EAI_OVERFLOW': 14, 'EAI_PROTOCOL': 13, 'EAI_SERVICE': 9, 'EAI_SOCKTYPE': 10, 'EAI_SYSTEM': 11}
EAI_WINDOWS = {'EAI_SERVICE': 10109, 'EAI_NONAME': 11001, 'EAI_MEMORY': 8, 'EAI_BADFLAGS': 10022, 'EAI_FAMILY': 10047, 'EAI_AGAIN': 11002, 'EAI_NODATA': 11001, 'EAI_SOCKTYPE': 10044, 'EAI_FAIL': 11003}

class SocketTest(IronPythonTestCase):

    @unittest.skipUnless(is_osx, "OSX-specific test")
    def test_eai_codes_darwin(self):
        '''Tests EAI_* codes on OSX'''
        for k, v in EAI_DARWIN.items():
            self.assertEqual(getattr(_socket, k), v)

    @unittest.skipUnless(is_linux, "Linux-specific test")
    def test_eai_codes_linux(self):
        '''Tests EAI_* codes on Linux'''
        for k, v in OTHER_GLOBALS.items():
            if k.startswith('EAI_'):
                self.assertEqual(getattr(_socket, k), v)

    @unittest.skipUnless(is_windows, "Windows-specific test")
    def test_eai_codes_windows(self):
        '''Tests EAI_* codes on Windows'''
        for k, v in EAI_WINDOWS.items():
            if k == 'EAI_MEMORY' and v == 8 and is_cli:
                # CPython anomaly
                v = 10055 # SocketError.NoBufferSpaceAvailable
            self.assertEqual(getattr(_socket, k), v)

    def test_getprotobyname(self):
        '''Tests _socket.getprotobyname'''
        #IP and CPython
        proto_map = {
            "icmp": _socket.IPPROTO_ICMP,
            "ip": _socket.IPPROTO_IP,
            "tcp": _socket.IPPROTO_TCP,
            "udp": _socket.IPPROTO_UDP,
        }

        if is_cli or sys.version_info >= (3,8):
            proto_map.update({
                "ipv6": _socket.IPPROTO_IPV6,
                "esp": _socket.IPPROTO_ESP,
                "pup": _socket.IPPROTO_PUP, #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21918
                "ggp": _socket.IPPROTO_GGP, #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21918
            })

            proto_map_cli_only = {
                "dstopts": _socket.IPPROTO_DSTOPTS,
                "none": _socket.IPPROTO_NONE,
                "raw": _socket.IPPROTO_RAW,
                "ipv4": _socket.IPPROTO_IPV4,
                "fragment": _socket.IPPROTO_FRAGMENT,
                "nd": _socket.IPPROTO_ND,
                "icmpv6": _socket.IPPROTO_ICMPV6,
                "routing": _socket.IPPROTO_ROUTING,
            }

            for proto_name, good_val in proto_map_cli_only.items():
                if is_cli:
                    temp_val = _socket.getprotobyname(proto_name)
                    self.assertEqual(temp_val, good_val)
                else:
                    with self.assertRaises(OSError):
                        _socket.getprotobyname(proto_name)

        for proto_name, good_val in proto_map.items():
            temp_val = _socket.getprotobyname(proto_name)
            self.assertEqual(temp_val, good_val)

        #negative cases
        bad_list = ["", "blah", "i"]
        for name in bad_list:
            self.assertRaises(_socket.error, _socket.getprotobyname, name)

    @unittest.skipIf(is_cpython and not is_windows, "TODO: figure out why this is different with CPython on Posix")
    def test_getaddrinfo(self):
        '''Tests _socket.getaddrinfo'''
        joe = { ("127.0.0.1", 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
                ("127.0.0.1", 1) : "[(2, 0, 0, '', ('127.0.0.1', 1))]",
                ("127.0.0.1", 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
                ("127.0.0.1", 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
                ("127.0.0.1", 0, 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
                ("127.0.0.1", 0, 0, 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
                ("127.0.0.1", 0, 0, 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
                ("127.0.0.1", 0, 0, 0, 0, 1) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
        }
        # TODO: On Darwin with CPython 3.7: value:  [(2, 2, 17, '', ('127.0.0.1', 1)), (2, 1, 6, '', ('127.0.0.1', 1))]
        # TODO: On Linux with CPython 3.8: value:   [(2, 1, 6, '', ('127.0.0.1', 0)), (2, 2, 17, '', ('127.0.0.1', 0)), (2, 3, 0, '', ('127.0.0.1', 0))]

        if is_windows:
            _socket.getaddrinfo("127.0.0.1", 0, 0, 0, -100000, 0)
            _socket.getaddrinfo("127.0.0.1", 0, 0, 0, 100000, 0)
        elif is_linux:
            # TODO: socket.gaierror: [Errno -8] Servname not supported for ai_socktype
            _socket.getaddrinfo("127.0.0.1", 0, 0, 0, -100000, 0)
            _socket.getaddrinfo("127.0.0.1", 0, 0, 0, 100000, 0)
        elif is_osx:
            # TODO: Darwin: socket.gaierror: [Errno 12] Bad hints
            _socket.getaddrinfo("127.0.0.1", 0, 0, 0, -100000, 0)
            _socket.getaddrinfo("127.0.0.1", 0, 0, 0, 100000, 0)
        tmp = _socket.getaddrinfo("127.0.0.1", 0, 0, 0, 0, 0)

        #just try them as-is
        for params,value in joe.items():
            with self.subTest(params=params, value=value):
                addrinfo = _socket.getaddrinfo(*params)
                self.assertEqual(repr(addrinfo), value)

        #change the address family
        for addr_fam in ["AF_INET", "AF_UNSPEC"]:
            addrinfo = _socket.getaddrinfo("127.0.0.1",
                                        0,
                                        eval("_socket." + addr_fam),
                                        0,
                                        0,
                                        0)

            self.assertEqual(repr(addrinfo), "[(2, 0, 0, '', ('127.0.0.1', 0))]")

        #change the _socket type
        for socktype in ["SOCK_DGRAM", "SOCK_RAW", "SOCK_STREAM"]:
            socktype = eval("_socket." + socktype)
            addrinfo = _socket.getaddrinfo("127.0.0.1",
                                        0,
                                        0,
                                        socktype,
                                        0,
                                        0)
            self.assertEqual(repr(addrinfo), "[(2, " + str(socktype) + ", 0, '', ('127.0.0.1', 0))]")

        #change the protocol
        for proto in IPPROTO_DICT.keys():#["SOCK_DGRAM", "SOCK_RAW", "SOCK_STREAM"]:
            try:
                proto = eval("_socket." + proto)
            except:
                print(proto)
                continue
            addrinfo = _socket.getaddrinfo("127.0.0.1",
                                        0,
                                        0,
                                        0,
                                        proto,
                                        0)
            self.assertEqual(repr(addrinfo), "[(2, 0, " + str(proto) + ", '', ('127.0.0.1', 0))]")

        #negative cases
        #TODO - this actually passes on a Windows 7 machine...
        #self.assertRaises(_socket.gaierror, _socket.getaddrinfo, "should never work.dfkdfjkkjdfkkdfjkdjf", 0)

        self.assertRaises(_socket.gaierror, _socket.getaddrinfo, "1", 0)
        if is_cli:
            self.assertRaises(_socket.gaierror, _socket.getaddrinfo, ".", 0)
        else:
            self.assertRaises(UnicodeError, _socket.getaddrinfo, ".", 0)
        self.assertRaises(_socket.error, _socket.getaddrinfo, "127.0.0.1", 3.14, 0, 0, 0, 0)
        self.assertRaises(_socket.error, _socket.getaddrinfo, "127.0.0.1", 0, -1, 0, 0, 0)
        self.assertRaises(_socket.error, _socket.getaddrinfo, "127.0.0.1", 0, 0, -1, 0, 0)

    def test_getnameinfo(self):
        '''Tests _socket.getnameinfo()'''
        #sanity
        _socket.getnameinfo(("127.0.0.1", 80), 8)
        _socket.getnameinfo(("127.0.0.1", 80), 9)

        host, service = _socket.getnameinfo( ("127.0.0.1", 80), 8)
        self.assertEqual(service, '80')

        host, service = _socket.getnameinfo( ("127.0.0.1", 80), 0)
        self.assertEqual(service, "http")
        #IP gives a TypeError
        #self.assertRaises(SystemError, _socket.getnameinfo, ("127.0.0.1"), 8)
        #self.assertRaises(SystemError, _socket.getnameinfo, (321), 8)
        self.assertRaises(TypeError, _socket.getnameinfo, ("127.0.0.1"), '0')
        self.assertRaises(TypeError, _socket.getnameinfo, ("127.0.0.1", 80, 0, 0, 0), 8)
        self.assertRaises(_socket.gaierror, _socket.getnameinfo, ('no such host will ever exist', 80), 8)

    def test_gethostbyaddr(self):
        '''Tests _socket.gethostbyaddr'''
        _socket.gethostbyaddr("localhost")
        _socket.gethostbyaddr("127.0.0.1")

    def test_gethostbyname(self):
        '''Tests _socket.gethostbyname'''
        #sanity
        self.assertEqual(_socket.gethostbyname("localhost"), "127.0.0.1")
        self.assertEqual(_socket.gethostbyname("127.0.0.1"), "127.0.0.1")
        self.assertEqual(_socket.gethostbyname("<broadcast>"), "255.255.255.255")

        #negative
        with self.assertRaises(_socket.gaierror) as cm:
            _socket.gethostbyname("should never work")
        self.assertEqual(cm.exception.errno, _socket.EAI_NONAME)

    def test_gethostbyname_ex(self):
        '''Tests _socket.gethostbyname_ex'''
        #sanity
        joe = _socket.gethostbyname_ex("localhost")[2]
        self.assertIn("127.0.0.1" , joe)
        joe = _socket.gethostbyname_ex("127.0.0.1")[2]
        self.assertIn("127.0.0.1", joe)

        #negative
        with self.assertRaises(_socket.gaierror) as cm:
            _socket.gethostbyname_ex("should never work")
        self.assertEqual(cm.exception.errno, _socket.EAI_NONAME)

        with self.assertRaises(_socket.gaierror) as cm:
            _socket.gethostbyname_ex("ipv6.google.com") # has only ipv6 addresses
        self.assertEqual(cm.exception.errno, _socket.EAI_NONAME)

    def test_getservbyport(self):
        self.assertEqual(_socket.getservbyport(80), "http")

    def test_getservbyname(self):
        self.assertEqual(_socket.getservbyname("http"), 80)

    def test_inet_ntop(self):
        '''Tests _socket.inet_ntop'''

        #negative
        self.assertRaises(ValueError, _socket.inet_ntop, _socket.AF_INET, b"garbage dkfjdkfjdkfj")

    def test_inet_pton(self):
        '''Tests _socket.inet_pton'''

        #sanity
        _socket.inet_pton(_socket.AF_INET, "127.0.0.1")

        #negative
        self.assertRaises(_socket.error, _socket.inet_pton, _socket.AF_INET, "garbage dkfjdkfjdkfj")

    def test_getfqdn(self):
        '''Tests _socket.getfqdn'''
        #TODO
        pass

    def test_cp5814(self):
        global EXIT_CODE
        global HAS_EXITED
        EXIT_CODE = -1
        HAS_EXITED = False

        portFile = os.path.join(self.temporary_dir, "cp5814port_%d" % os.getpid())

        #Server code
        server = """
from time import sleep
import _socket
import os

HOST = 'localhost'
PORT = 0
s = _socket.socket(_socket.AF_INET, _socket.SOCK_STREAM)
s.setsockopt(_socket.SOL_SOCKET, _socket.SO_REUSEADDR, 1) # prevents an "Address already in use" error when the socket is in a TIME_WAIT state
s.settimeout(20) # prevents the server from staying open if the client never connects
s.bind((HOST, PORT))
s.listen(1)

try:
    with open(r"{PORTFILE}", "w") as f:
        print(s.getsockname()[1], file=f)

    fd, addr = s._accept()
    conn = _socket.socket(_socket.AF_INET, _socket.SOCK_STREAM, fileno=fd)

    #Whatever we get from the client, send it back.
    data = conn.recv(1024)
    conn.send(data)

    #Verifications
    if not addr[0] in [HOST, '127.0.0.1']:
        raise Exception('The address, %s, was unexpected' % str(addr))
    if data!=b'stuff':
        raise Exception('%s!=stuff' % str(data))
    sleep(10)

finally:
    conn.close()
    try:
        os.remove(r"{PORTFILE}")
    except:
        pass
""".format(PORTFILE=portFile)
        #Spawn off a thread to startup the server
        def server_thread():
            global EXIT_CODE
            global HAS_EXITED
            serverFile = os.path.join(self.temporary_dir, "cp5814server_%d.py" % os.getpid())
            self.write_to_file(serverFile, server)
            EXIT_CODE = os.system('"%s" %s' %
                        (sys.executable, serverFile))
            HAS_EXITED = True
            try:
                os.remove(serverFile)
            except:
                pass

        _thread.start_new_thread(server_thread, ())
        #Give the server a chance to startup
        portex = None
        startTime = time.perf_counter()
        for _ in range(20):
            time.sleep(1)
            if EXIT_CODE > 0:
                self.fail("Server died with exit code %d" % EXIT_CODE)
            try:
                with open(portFile) as f:
                    PORT = int(f.read())
                break
            except Exception as ex:
                portex = ex
        else:
            duration = time.perf_counter() - startTime
            self.fail("Server not detected after trying for %g s, last detection attempt resulted in %r" % (duration, portex))

        #Client
        HOST = 'localhost'
        s = _socket.socket(_socket.AF_INET, _socket.SOCK_STREAM)
        s.connect((HOST, PORT))
        s.send(b"stuff")
        data, addr = s.recvfrom(1024)
        s.close()

        #Ensure the server didn't die
        for i in range(100):
            if not HAS_EXITED:
                print("*", end="")
                time.sleep(1)
            else:
                self.assertEqual(EXIT_CODE, 0)
                break
        self.assertTrue(HAS_EXITED)

        #Verification
        self.assertEqual(data, b"stuff")
        if is_cli:
            self.assertEqual(addr[0], "0.0.0.0")
        else:
            self.assertTrue(addr is None or addr[0] == 0)

import socket

class SocketMakefileTest(IronPythonTestCase):
    def test_misc(self):
        f = socket.socket().makefile()
        f.bufsize = 4096
        self.assertEqual(4096, f.bufsize)

    def test_makefile_refcount(self):
        "Ensures that the _socket stays open while there's still a file associated"

        global PORT
        def echoer():
            global PORT
            s = socket.socket()
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1) # prevents an "Address already in use" error when the socket is in a TIME_WAIT state
            s.settimeout(15) # prevents the server from staying open if the client never connects
            s.bind(('localhost', 0))
            PORT = s.getsockname()[1]
            s.listen(5)
            (s2, ignore) = s.accept()
            s2.send(s2.recv(10))

        _thread.start_new_thread(echoer, ())
        time.sleep(1)
        s = socket.socket()
        s.connect(('localhost', PORT))
        f1 = s.makefile('r')
        f2 = s.makefile('w')
        s.close()
        test_msg = 'abc\n'
        f2.write(test_msg)
        f2.flush()
        str = f1.readline()
        self.assertEqual(str, test_msg)

    def test_cp7451(self):
        global EXIT_CODE
        global HAS_EXITED
        EXIT_CODE = -1
        HAS_EXITED = False

        portFile = os.path.join(self.temporary_dir, "cp7451port_%d" % os.getpid())

        #Server code
        server = """
from time import sleep
import socket as _socket
import os

HOST = 'localhost'
PORT = 0
s = _socket.socket(_socket.AF_INET, _socket.SOCK_STREAM)
s.setsockopt(_socket.SOL_SOCKET, _socket.SO_REUSEADDR, 1) # prevents an "Address already in use" error when the socket is in a TIME_WAIT state
s.settimeout(20) # prevents the server from staying open if the client never connects
s.bind((HOST, PORT))
s.listen(1)

try:
    with open(r"{PORTFILE}", "w") as f:
        print(s.getsockname()[1], file=f)

    conn, addr = s.accept()

    #Whatever we get from the client, send it back.
    data = conn.recv(1024)
    conn.send(data)

    #Verifications
    if not addr[0] in [HOST, '127.0.0.1']:
        raise Exception('The address, %s, was unexpected' % str(addr))
    if data!=b'stuff2':
        raise Exception('%s!=stuff2' % str(data))
    sleep(10)

finally:
    conn.close()
    try:
        os.remove(r"{PORTFILE}")
    except:
        pass
""".format(PORTFILE=portFile)
        #Spawn off a thread to startup the server
        def server_thread():
            global EXIT_CODE
            global HAS_EXITED
            serverFile = os.path.join(self.temporary_dir, "cp7451server_%d.py" % os.getpid())
            self.write_to_file(serverFile, server)
            EXIT_CODE = os.system('"%s" %s' %
                        (sys.executable, serverFile))
            HAS_EXITED = True
            try:
                os.remove(serverFile)
            except:
                pass

        _thread.start_new_thread(server_thread, ())
        #Give the server a chance to startup
        portex = None
        startTime = time.perf_counter()
        for _ in range(20):
            time.sleep(1)
            if EXIT_CODE > 0:
                self.fail("Server died with exit code %d" % EXIT_CODE)
            try:
                with open(portFile) as f:
                    PORT = int(f.read())
                break
            except Exception as ex:
                portex = ex
        else:
            duration = time.perf_counter() - startTime
            self.fail("Server not detected after trying for %g s, last detection attempt resulted in %r" % (duration, portex))

        #Client
        HOST = 'localhost'
        s = socket.socket()
        s.connect((HOST, PORT))
        s.send(b"stuff2")
        f = s.makefile()
        s.close()

        #Ensure the server didn't die
        for i in range(100):
            if not HAS_EXITED:
                print("*", end="")
                time.sleep(1)
            else:
                self.assertEqual(EXIT_CODE, 0)
                break
        self.assertTrue(HAS_EXITED)

        #Verification
        self.assertEqual(f.read(6), "stuff2")

run_test(__name__)
