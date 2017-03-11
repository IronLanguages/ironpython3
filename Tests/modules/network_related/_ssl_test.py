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
'''
Tests for the _ssl module.  See http://docs.python.org/library/ssl.html
'''
#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

#http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24266
import _ssl as real_ssl

import socket


#--GLOBALS---------------------------------------------------------------------
SSL_URL      = "www.microsoft.com"
SSL_ISSUER   = "CN=Symantec Class 3 Secure Server CA - G4, OU=Symantec Trust Network, O=Symantec Corporation, C=US"
SSL_SERVER   = "www.microsoft.com"
SSL_PORT     = 443
SSL_REQUEST  = "GET / HTTP/1.0\r\nHost: www.microsoft.com\r\n\r\n"
SSL_RESPONSE = "Microsoft Corporation"


#--HELPERS---------------------------------------------------------------------


#--TEST CASES------------------------------------------------------------------
@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_CERT_NONE():
    AreEqual(real_ssl.CERT_NONE,
             0)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_CERT_OPTIONAL():
    AreEqual(real_ssl.CERT_OPTIONAL,
             1)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_CERT_REQUIRED():
    AreEqual(real_ssl.CERT_REQUIRED,
             2)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_PROTOCOL_SSLv2():
    AreEqual(real_ssl.PROTOCOL_SSLv2,
             0)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_PROTOCOL_SSLv23():
    AreEqual(real_ssl.PROTOCOL_SSLv23,
             2)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_PROTOCOL_SSLv3():
    AreEqual(real_ssl.PROTOCOL_SSLv3,
             1)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test_PROTOCOL_TLSv1():
    AreEqual(real_ssl.PROTOCOL_TLSv1,
             3)

@skip("silverlight")
def test_PROTOCOL_TLSv1_1():
    AreEqual(real_ssl.PROTOCOL_TLSv1_1,
             4)

@skip("silverlight")
def test_PROTOCOL_TLSv1_2():
    AreEqual(real_ssl.PROTOCOL_TLSv1_2,
             5)

@skip("silverlight")
def test_OP_NO_SSLv2():
    AreEqual(real_ssl.OP_NO_SSLv2,
             0x0000000)

@skip("silverlight")
def test_OP_NO_SSLv3():
    AreEqual(real_ssl.OP_NO_SSLv3,
             0x2000000)

@skip("silverlight")
def test_OP_NO_TLSv1():
    AreEqual(real_ssl.OP_NO_TLSv1,
             0x4000000)

@skip("silverlight")
def test_OP_NO_TLSv1_1():
    AreEqual(real_ssl.OP_NO_TLSv1_1,
             0x10000000)

@skip("silverlight")
def test_OP_NO_TLSv1_2():
    AreEqual(real_ssl.OP_NO_TLSv1_2,
             0x8000000)

def test_RAND_add():
    #--Positive
    AreEqual(real_ssl.RAND_add("", 3.14),
             None)
    AreEqual(real_ssl.RAND_add("", 3.14),
             None)
    AreEqual(real_ssl.RAND_add("", 3),
             None)
    
    #--Negative
    for g1, g2 in [ (None, None),
                    ("", None),
                    (None, 3.14), #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24276
                    ]:
        AssertError(TypeError, real_ssl.RAND_add, g1, g2)
    
    AssertError(TypeError, real_ssl.RAND_add)
    AssertError(TypeError, real_ssl.RAND_add, "")
    AssertError(TypeError, real_ssl.RAND_add, 3.14)
    AssertError(TypeError, real_ssl.RAND_add, "", 3.14, "")


def test_RAND_status():
    #--Positive
    AreEqual(real_ssl.RAND_status(),
             1)
             
    #--Negative
    AssertError(TypeError, real_ssl.RAND_status, None)
    AssertError(TypeError, real_ssl.RAND_status, "")
    AssertError(TypeError, real_ssl.RAND_status, 1)
    AssertError(TypeError, real_ssl.RAND_status, None, None)

    
def test_SSLError():
    AreEqual(real_ssl.SSLError.__bases__, (socket.error, ))


def test_SSL_ERROR_EOF():
    AreEqual(real_ssl.SSL_ERROR_EOF, 8)


def test_SSL_ERROR_INVALID_ERROR_CODE():
    AreEqual(real_ssl.SSL_ERROR_INVALID_ERROR_CODE, 9)


def test_SSL_ERROR_SSL():
    AreEqual(real_ssl.SSL_ERROR_SSL, 1)


def test_SSL_ERROR_SYSCALL():
    AreEqual(real_ssl.SSL_ERROR_SYSCALL, 5)


def test_SSL_ERROR_WANT_CONNECT():
    AreEqual(real_ssl.SSL_ERROR_WANT_CONNECT, 7)


def test_SSL_ERROR_WANT_READ():
    AreEqual(real_ssl.SSL_ERROR_WANT_READ, 2)


def test_SSL_ERROR_WANT_WRITE():
    AreEqual(real_ssl.SSL_ERROR_WANT_WRITE, 3)


def test_SSL_ERROR_WANT_X509_LOOKUP():
    AreEqual(real_ssl.SSL_ERROR_WANT_X509_LOOKUP, 4)


def test_SSL_ERROR_ZERO_RETURN():
    AreEqual(real_ssl.SSL_ERROR_ZERO_RETURN, 6)


@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21411
def test___doc__():
    expected_doc = """Implementation module for SSL socket operations.  See the socket module
for documentation."""
    AreEqual(real_ssl.__doc__, expected_doc)


def test__test_decode_cert():
    if not is_cpython and hasattr(real_ssl, "decode_cert"):
        raise Exception("Please add a test for _ssl.decode_cert")
    print('TODO: no implementation to test yet.')


def test_sslwrap():
    print('TODO: no implementation to test yet.')


def test_SSLType():
    #--Positive
    if is_cpython:
        AreEqual(str(real_ssl.SSLType),
                 "<type 'ssl.SSLContext'>")
    else:
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24266
        AreEqual(str(real_ssl.SSLType),
                 "<type 'socket.ssl'>")
                 
    #--Negative
    if is_cpython:
        AssertErrorWithMessage(TypeError, "cannot create 'ssl.SSLContext' instances",
                               real_ssl.SSLType)
    else:
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24277
        AssertErrorWithMessage(TypeError, "ssl() takes at least 1 argument (0 given)",
                               real_ssl.SSLType)


#--TEST CASES for _ssl.sll-----------------------------------------------------
'''
TODO: once we have a proper implementation of _ssl.sslwrap the tests below need
      to be rewritten.
'''
@retry_on_failure
def test_SSLType_ssl():
    '''
    Should be essentially the same as _ssl.sslwrap.  It's not though and will
    simply be tested as implemented for the time being.
    
    ssl(PythonSocket.socket sock, 
        [DefaultParameterValue(null)] string keyfile, 
        [DefaultParameterValue(null)] string certfile)
    '''
    #--Positive
    
    #sock
    s = socket.socket(socket.AF_INET)
    s.connect((SSL_URL, SSL_PORT))
    ssl_s = real_ssl.sslwrap(s._sock, False)

    if is_cpython:
        pass #ssl_s.shutdown()  #Too slow
    s.close()
    
    #sock, keyfile, certfile
    #TODO!


@disabled
@retry_on_failure
def test_SSLType_ssl_neg():
    '''
    See comments on test_SSLType_ssl.  Basically this needs to be revisited
    entirely (TODO) after we're more compatible with CPython.
    '''
    s = socket.socket(socket.AF_INET)
    s.connect((SSL_URL, SSL_PORT))
    
    #--Negative
    
    #Empty
    AssertError(TypeError, real_ssl.sslwrap)
    AssertError(TypeError, real_ssl.sslwrap, False)
    
    #None
    AssertError(TypeError, real_ssl.sslwrap, None, False)
    
    #s, bad keyfile
    #Should throw _ssl.SSLError because both keyfile and certificate weren't specified
    AssertError(real_ssl.SSLError, real_ssl.sslwrap, s._sock, False, "bad keyfile")
    
    #s, bad certfile
    #Should throw _ssl.SSLError because both keyfile and certificate weren't specified
    
    #s, bad keyfile, bad certfile
    #Should throw ssl.SSLError
    AssertError(real_ssl.SSLError, real_ssl.sslwrap, s._sock, False, "bad keyfile", "bad certfile")
    
    #Cleanup
    s.close()

@retry_on_failure    
def test_SSLType_issuer():
    #--Positive
    s = socket.socket(socket.AF_INET)
    s.connect((SSL_URL, SSL_PORT))
    ssl_s = real_ssl.sslwrap(s._sock, False)
    AreEqual(ssl_s.issuer(), '')  #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24281
    ssl_s.do_handshake()
    
    if is_cpython:
        AreEqual(ssl_s.issuer.__doc__, None)
    else:
        #Incompat, but a good one at that
        Assert("Returns a string that describes the issuer of the server's certificate" in ssl_s.issuer.__doc__)
    
    issuer = ssl_s.issuer()
    #If we can get the issuer once, we should be able to do it again
    AreEqual(issuer, ssl_s.issuer())
    Assert(SSL_ISSUER in issuer)
    
    
    #--Negative
    AssertErrorWithMessage(TypeError, "issuer() takes no arguments (1 given)",
                           ssl_s.issuer, None)
    AssertErrorWithMessage(TypeError, "issuer() takes no arguments (1 given)",
                           ssl_s.issuer, 1)
    AssertErrorWithMessage(TypeError, "issuer() takes no arguments (2 given)",
                           ssl_s.issuer, 3.14, "abc")

    #Cleanup
    if is_cpython:
        pass #ssl_s.shutdown()  #Too slow
    s.close()

@retry_on_failure
def test_SSLType_server():
    #--Positive
    s = socket.socket(socket.AF_INET)
    s.connect((SSL_URL, SSL_PORT))
    ssl_s = real_ssl.sslwrap(s._sock, False)
    AreEqual(ssl_s.server(), '')  #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24281
    ssl_s.do_handshake()
    
    if is_cpython:
        AreEqual(ssl_s.server.__doc__, None)
    else:
        #Incompat, but a good one at that
        Assert("Returns a string that describes the issuer of the server's certificate" in ssl_s.issuer.__doc__)
    
    server = ssl_s.server()
    #If we can get the server once, we should be able to do it again
    AreEqual(server, ssl_s.server())
    Assert(SSL_SERVER in server)
    
    
    #--Negative
    AssertErrorWithMessage(TypeError, "server() takes no arguments (1 given)",
                           ssl_s.server, None)
    AssertErrorWithMessage(TypeError, "server() takes no arguments (1 given)",
                           ssl_s.server, 1)
    AssertErrorWithMessage(TypeError, "server() takes no arguments (2 given)",
                           ssl_s.server, 3.14, "abc")

    #Cleanup
    if is_cpython:
        pass #ssl_s.shutdown()  #Too slow
    s.close()
    
@retry_on_failure
def test_SSLType_read_and_write():
    #--Positive
    s = socket.socket(socket.AF_INET)
    s.connect((SSL_URL, SSL_PORT))
    ssl_s = real_ssl.sslwrap(s._sock, False)
    ssl_s.do_handshake()
    
    if is_cpython:
        Assert("Writes the string s into the SSL object" in ssl_s.write.__doc__)
        Assert("Read up to len bytes from the SSL socket" in ssl_s.read.__doc__)
    else:
        #Incompat, but we can live with this
        Assert("Writes the string s through the SSL connection" in ssl_s.write.__doc__)
        Assert("If n is present, reads up to n bytes from the SSL connection" in ssl_s.read.__doc__)
    
    #Write
    AreEqual(ssl_s.write(SSL_REQUEST),
             len(SSL_REQUEST))
    
    #Read
    AreEqual(ssl_s.read(4).lower(), "http")
    
    response = ssl_s.read(5000)
    Assert(SSL_RESPONSE in response)
    
    #Cleanup
    if is_cpython:
        pass #ssl_s.shutdown()  #Too slow
    s.close()
    
#--MAIN------------------------------------------------------------------------
run_test(__name__)
