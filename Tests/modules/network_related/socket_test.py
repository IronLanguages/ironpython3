#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

#
# test socket
#

from iptest.assert_util import *
skiptest("silverlight")
import sys
import _thread
import time

#workaround - _socket does not appear to be in $PYTHONPATH for CPython
#only when run from the old test suite.
try:
    import socket
except:
    print("Unable to import socket (_socket) from CPython")
    sys.exit(0)

#-----------------------
#--GLOBALS
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

@retry_on_failure
def test_getprotobyname():
    '''
    Tests socket.getprotobyname
    '''
    #IP and CPython
    proto_map = {
                "icmp": socket.IPPROTO_ICMP,
                "ip": socket.IPPROTO_IP,
                "tcp": socket.IPPROTO_TCP,
                "udp": socket.IPPROTO_UDP,
    }
    
    #supported only by IP
    if is_cli:
        proto_map.update(
            {"dstopts": socket.IPPROTO_DSTOPTS,
             "none": socket.IPPROTO_NONE,
             "raw": socket.IPPROTO_RAW,
             "ipv4": socket.IPPROTO_IPV4,
             "ipv6": socket.IPPROTO_IPV6,
             "esp": socket.IPPROTO_ESP,
             "fragment": socket.IPPROTO_FRAGMENT,
             "nd": socket.IPPROTO_ND,
             "icmpv6": socket.IPPROTO_ICMPV6,
             "routing": socket.IPPROTO_ROUTING,
             "pup": socket.IPPROTO_PUP, #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21918
             "ggp": socket.IPPROTO_GGP, #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21918
            })
    
    for proto_name, good_val in proto_map.items():
        temp_val = socket.getprotobyname(proto_name)
        AreEqual(temp_val, good_val)
        
    #negative cases
    bad_list = ["", "blah", "i"]
    for name in bad_list:
        AssertError(socket.error, socket.getprotobyname, name)

@retry_on_failure
def test_getaddrinfo():
    '''
    Tests socket.getaddrinfo
    '''
    joe = { ("127.0.0.1", 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
            ("127.0.0.1", 1) : "[(2, 0, 0, '', ('127.0.0.1', 1))]",
            ("127.0.0.1", 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
            ("127.0.0.1", 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
            ("127.0.0.1", 0, 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
            ("127.0.0.1", 0, 0, 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
            ("127.0.0.1", 0, 0, 0, 0, 0) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
            ("127.0.0.1", 0, 0, 0, 0, 1) : "[(2, 0, 0, '', ('127.0.0.1', 0))]",
    }
    
    tmp = socket.getaddrinfo("127.0.0.1", 0, 0, 0, -100000, 0)
    tmp = socket.getaddrinfo("127.0.0.1", 0, 0, 0, 100000, 0)
    tmp = socket.getaddrinfo("127.0.0.1", 0, 0, 0, 0, 0)

    #just try them as-is
    for params,value in joe.items():
        addrinfo = socket.getaddrinfo(*params)
        AreEqual(repr(addrinfo), value)
    
    #change the address family
    for addr_fam in ["AF_INET", "AF_UNSPEC"]:
        addrinfo = socket.getaddrinfo("127.0.0.1",
                                       0,
                                       eval("socket." + addr_fam),
                                       0,
                                       0,
                                       0)
            
        AreEqual(repr(addrinfo), "[(2, 0, 0, '', ('127.0.0.1', 0))]")
            
    #change the socket type
    for socktype in ["SOCK_DGRAM", "SOCK_RAW", "SOCK_STREAM"]:
        socktype = eval("socket." + socktype)
        addrinfo = socket.getaddrinfo("127.0.0.1",
                                       0,
                                       0,
                                       socktype,
                                       0,
                                       0)
        AreEqual(repr(addrinfo), "[(2, " + str(socktype) + ", 0, '', ('127.0.0.1', 0))]")
        
        
    #change the protocol
    for proto in list(IPPROTO_DICT.keys()):#["SOCK_DGRAM", "SOCK_RAW", "SOCK_STREAM"]:
        try:
            proto = eval("socket." + proto)
        except:
            print(proto)
            continue
        addrinfo = socket.getaddrinfo("127.0.0.1",
                                       0,
                                       0,
                                       0,
                                       proto,
                                       0)
        AreEqual(repr(addrinfo), "[(2, 0, " + str(proto) + ", '', ('127.0.0.1', 0))]")
    
    #negative cases
    #TODO - this actually passes on a Windows 7 machine...
    #AssertError(socket.gaierror, socket.getaddrinfo, "should never work.dfkdfjkkjdfkkdfjkdjf", 0)

    AssertError(socket.gaierror, socket.getaddrinfo, "1", 0)
    AssertError(socket.gaierror, socket.getaddrinfo, ".", 0)
    AssertError(socket.error, socket.getaddrinfo, "127.0.0.1", 3.14, 0, 0, 0, 0)
    AssertError(socket.error, socket.getaddrinfo, "127.0.0.1", 0, -1, 0, 0, 0)
    AssertError(socket.error, socket.getaddrinfo, "127.0.0.1", 0, 0, -1, 0, 0)

    socket.getaddrinfo("127.0.0.1", 0, 0, 0, 1000000, 0)
    socket.getaddrinfo("127.0.0.1", 0, 0, 0, -1000000, 0)
    socket.getaddrinfo("127.0.0.1", 0, 0, 0, 0, 0)

@retry_on_failure
def test_getnameinfo():
    '''
    Tests socket.getnameinfo()
    '''
    #sanity
    socket.getnameinfo(("127.0.0.1", 80), 8)
    socket.getnameinfo(("127.0.0.1", 80), 9)
        
    host, service = socket.getnameinfo( ("127.0.0.1", 80), 8)
    AreEqual(service, '80')
        
    host, service = socket.getnameinfo( ("127.0.0.1", 80), 0)
    AreEqual(service, "http")
    #IP gives a TypeError
    #AssertError(SystemError, socket.getnameinfo, ("127.0.0.1"), 8)
    #AssertError(SystemError, socket.getnameinfo, (321), 8)
    AssertError(TypeError, socket.getnameinfo, ("127.0.0.1"), '0')
    AssertError(TypeError, socket.getnameinfo, ("127.0.0.1", 80, 0, 0, 0), 8)
    AssertError(socket.gaierror, socket.getnameinfo, ('no such host will ever exist', 80), 8)

@retry_on_failure    
def test_gethostbyaddr():
    '''
    Tests socket.gethostbyaddr
    '''
    socket.gethostbyaddr("localhost")
    socket.gethostbyaddr("127.0.0.1")
    if is_cli and not is_net40: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24495
        socket.gethostbyaddr("<broadcast>")
    
@retry_on_failure
def test_gethostbyname():
    '''
    Tests socket.gethostbyname
    '''
    #sanity
    AreEqual(socket.gethostbyname("localhost"), "127.0.0.1")
    AreEqual(socket.gethostbyname("127.0.0.1"), "127.0.0.1")
    AreEqual(socket.gethostbyname("<broadcast>"), "255.255.255.255")
    
    #negative
    AssertError(socket.gaierror, socket.gethostbyname, "should never work")
    
@retry_on_failure    
def test_gethostbyname_ex():
    '''
    Tests socket.gethostbyname_ex
    '''
    #sanity
    joe = socket.gethostbyname_ex("localhost")[2]
    Assert("127.0.0.1" in joe)
    joe = socket.gethostbyname_ex("127.0.0.1")[2]
    Assert("127.0.0.1" in joe)
    
    #negative
    AssertError(socket.gaierror, socket.gethostbyname_ex, "should never work")
    

def test_getservbyport():
    AreEqual(socket.getservbyport(80), "http")

def test_getservbyname():
    AreEqual(socket.getservbyname("http"), 80)

@retry_on_failure        
def test_inet_ntop():
    '''
    Tests socket.inet_ntop
    '''
    if not is_cli:
        return
    
    #negative
    AssertError(socket.error, socket.inet_ntop, socket.AF_INET, "garbage dkfjdkfjdkfj")

@retry_on_failure
def test_inet_pton():
    '''
    Tests socket.inet_pton
    '''
    if not is_cli:
        return
    
    #sanity
    socket.inet_pton(socket.AF_INET, "127.0.0.1")
        
    #negative
    AssertError(socket.error, socket.inet_pton, socket.AF_INET, "garbage dkfjdkfjdkfj")

@retry_on_failure
def test_getfqdn():
    '''
    Tests socket.getfqdn
    '''
    #TODO
    pass

@retry_on_failure
def test_cp12452():
    '''
    Fully test socket._fileobj compatibility
    '''
    expected_dir = [
                    '__module__',
                    #-- Implementation dependent
                    #'__slots__',
                    #-- "_xyz" members probably do not need to be reimplemented in IP...
                    #'_close', '_get_wbuf_len', '_getclosed', '_rbuf',
                    #'_rbufsize', '_sock', '_wbuf', '_wbufsize',
                    '__class__', '__del__', '__delattr__', '__doc__',
                    '__getattribute__', '__hash__', '__init__', '__iter__',
                    '__new__', '__reduce__', '__reduce_ex__',
                    '__repr__', '__setattr__', '__str__',
                     'bufsize',
                    'close', 'closed', 'default_bufsize', 'fileno', 'flush',
                    'mode', 'name', 'next', 'read', 'readline', 'readlines',
                    'softspace', 'write', 'writelines']
    fileobject_dir = dir(socket._fileobject)
    
    missing = [ x for x in expected_dir if x not in fileobject_dir ]
    AreEqual([], missing)

@retry_on_failure
def test_misc():
    f = socket.socket().makefile()
    f.bufsize = 4096
    AreEqual(4096, f.bufsize)

#Dev10 446426
@skip("multiple_execute")
@retry_on_failure
def test_makefile_refcount():
    "Ensures that the socket stays open while there's still a file associated"
    
    def echoer(port):
        s = socket.socket()
        s.bind(('localhost', port))
        s.listen(5)
        (s2, ignore) = s.accept()
        s2.send(s2.recv(10))
    
    port = 50008
    
    _thread.start_new_thread(echoer, (port, ))
    time.sleep(0)
    s = socket.socket()
    s.connect(('localhost', port))
    f1 = s.makefile('r')
    f2 = s.makefile('w')
    s.close()
    test_msg = 'abc\n'
    f2.write(test_msg)
    f2.flush()
    str = f1.readline()
    Assert(str==test_msg)

@retry_on_failure
def test_fileobject_close():
    """verify we can construct fileobjects w/ the close kw arg"""
    fd = socket._fileobject(None, close=True)
    AreEqual(fd.mode, 'rb')
    if sys.platform=="win32":
        #CodePlex 17894
        AreEqual(fd.closed, True)

@disabled("TODO: fails consistently on certain machines")
@retry_on_failure
def test_cp5814():
    global HAS_EXITED
    global EXIT_CODE
    HAS_EXITED = False
    
    import os
    import _thread
    import time
    
    #Server code
    server = """
from time import sleep
import socket

HOST = 'localhost'
PORT = 50007
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.bind((HOST, PORT))

try:
    s.listen(1)
    conn, addr = s.accept()
    
    #Whatever we get from the client, send it back.
    data = conn.recv(1024)
    conn.send(data)
    
    #Verifications
    if not addr[0] in [HOST, '127.0.0.1']:
        raise Exception('The address, %s, was unexpected' % str(addr))
    if data!='stuff':
        raise Exception('%s!=stuff' % str(data))
    sleep(10)
    
finally:
    conn.close()
"""
    #Spawn off a thread to startup the server
    def server_thread():
        global EXIT_CODE
        global HAS_EXITED
        import os
        serverFile = path_combine(testpath.temporary_dir, "cp5814server.py")
        write_to_file(serverFile, server)
        EXIT_CODE = os.system("%s %s" %
                    (sys.executable, serverFile))
        HAS_EXITED = True
        try:
            os.remove(serverFile)
        except:
            pass
    
    _thread.start_new_thread(server_thread, ())
    #Give the server a chance to startup
    time.sleep(5)
    
    
    #Client
    HOST = 'localhost'
    PORT = 50007
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.connect((HOST, PORT))
    s.send("stuff")
    data, addr = s.recvfrom(1024)
    s.close()
    
    #Ensure the server didn't die
    for i in range(100):
        if not HAS_EXITED:
            print("*", end=' ')
            time.sleep(1)
        else:
            AreEqual(EXIT_CODE, 0)
            break
    Assert(HAS_EXITED)

    #Verification
    AreEqual(data, "stuff")
    if sys.platform=="win32":
        AreEqual(addr[0], 0)

@disabled("TODO: fails consistently on certain machines")
@retry_on_failure
def test_cp7451():
    global HAS_EXITED
    global EXIT_CODE
    HAS_EXITED = False
    
    import os
    import _thread
    import time
    
    #Server code
    server = """
from time import sleep
import socket

HOST = 'localhost'
PORT = 50007
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.bind((HOST, PORT))

try:
    s.listen(1)
    conn, addr = s.accept()
    
    #Whatever we get from the client, send it back.
    data = conn.recv(1024)
    conn.send(data)
    
    #Verifications
    if not addr[0] in [HOST, '127.0.0.1']:
        raise Exception('The address, %s, was unexpected' % str(addr))
    if data!='stuff2':
        raise Exception('%s!=stuff2' % str(data))
    sleep(10)
    
finally:
    conn.close()
"""
    #Spawn off a thread to startup the server
    def server_thread():
        global EXIT_CODE
        global HAS_EXITED
        import os
        serverFile = path_combine(testpath.temporary_dir, "cp7451server.py")
        write_to_file(serverFile, server)
        EXIT_CODE = os.system("%s %s" %
                    (sys.executable, serverFile))
        HAS_EXITED = True
        try:
            os.remove(serverFile)
        except:
            pass
    
    _thread.start_new_thread(server_thread, ())
    #Give the server a chance to startup
    time.sleep(5)
    
    
    #Client
    HOST = 'localhost'
    PORT = 50007
    s = socket.socket()
    s.connect((HOST, PORT))
    s.send("stuff2")
    f = s.makefile()
    s.close()
    
    #Ensure the server didn't die
    for i in range(100):
        if not HAS_EXITED:
            print("*", end=' ')
            time.sleep(1)
        else:
            AreEqual(EXIT_CODE, 0)
            break
    Assert(HAS_EXITED)

    #Verification
    AreEqual(f.read(6), "stuff2")
    
#------------------------------------------------------------------------------
run_test(__name__)
