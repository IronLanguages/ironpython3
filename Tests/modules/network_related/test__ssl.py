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


# https://github.com/IronLanguages/main/issues/733
import _ssl as real_ssl
import socket
import unittest

from iptest import IronPythonTestCase, is_cli, retryOnFailure, run_test

SSL_URL      = "www.microsoft.com"
SSL_ISSUER   = "CN=Symantec Class 3 Secure Server CA - G4, OU=Symantec Trust Network, O=Symantec Corporation, C=US"
SSL_SERVER   = "www.microsoft.com"
SSL_PORT     = 443
SSL_REQUEST  = "GET / HTTP/1.0\r\nHost: www.microsoft.com\r\n\r\n"
SSL_RESPONSE = "Microsoft Corporation"

class _SslTest(IronPythonTestCase):
    def test_constants(self):
        self.assertEqual(real_ssl.CERT_NONE, 0)
        self.assertEqual(real_ssl.CERT_OPTIONAL, 1)
        self.assertEqual(real_ssl.CERT_REQUIRED, 2)
        self.assertEqual(real_ssl.PROTOCOL_SSLv2, 0)
        self.assertEqual(real_ssl.PROTOCOL_SSLv23, 2)
        self.assertEqual(real_ssl.PROTOCOL_SSLv3, 1)
        self.assertEqual(real_ssl.PROTOCOL_TLSv1, 3)
        self.assertEqual(real_ssl.PROTOCOL_TLSv1_1, 4)
        self.assertEqual(real_ssl.PROTOCOL_TLSv1_2, 5)
        self.assertEqual(real_ssl.OP_NO_SSLv2, 0x1000000)
        self.assertEqual(real_ssl.OP_NO_SSLv3, 0x2000000)
        self.assertEqual(real_ssl.OP_NO_TLSv1, 0x4000000)
        self.assertEqual(real_ssl.OP_NO_TLSv1_1, 0x10000000)
        self.assertEqual(real_ssl.OP_NO_TLSv1_2, 0x8000000)
        self.assertEqual(real_ssl.SSL_ERROR_EOF, 8)
        self.assertEqual(real_ssl.SSL_ERROR_INVALID_ERROR_CODE, 9)
        self.assertEqual(real_ssl.SSL_ERROR_SSL, 1)
        self.assertEqual(real_ssl.SSL_ERROR_SYSCALL, 5)
        self.assertEqual(real_ssl.SSL_ERROR_WANT_CONNECT, 7)
        self.assertEqual(real_ssl.SSL_ERROR_WANT_READ, 2)
        self.assertEqual(real_ssl.SSL_ERROR_WANT_WRITE, 3)
        self.assertEqual(real_ssl.SSL_ERROR_WANT_X509_LOOKUP, 4)
        self.assertEqual(real_ssl.SSL_ERROR_ZERO_RETURN, 6)

    def test_RAND_add(self):
        #--Positive
        self.assertEqual(real_ssl.RAND_add("", 3.14), None)
        self.assertEqual(real_ssl.RAND_add(u"", 3.14), None)
        self.assertEqual(real_ssl.RAND_add("", 3), None)
        
        #--Negative
        for g1, g2 in [ (None, None),
                        ("", None),
                        (None, 3.14), #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24276
                        ]:
            self.assertRaises(TypeError, real_ssl.RAND_add, g1, g2)
        
        self.assertRaises(TypeError, real_ssl.RAND_add)
        self.assertRaises(TypeError, real_ssl.RAND_add, "")
        self.assertRaises(TypeError, real_ssl.RAND_add, 3.14)
        self.assertRaises(TypeError, real_ssl.RAND_add, "", 3.14, "")

    def test_RAND_status(self):
        #--Positive
        self.assertEqual(real_ssl.RAND_status(), 1)
                
        #--Negative
        self.assertRaises(TypeError, real_ssl.RAND_status, None)
        self.assertRaises(TypeError, real_ssl.RAND_status, "")
        self.assertRaises(TypeError, real_ssl.RAND_status, 1)
        self.assertRaises(TypeError, real_ssl.RAND_status, None, None)

    def test_SSLError(self):
        self.assertEqual(real_ssl.SSLError.__bases__, (socket.error, ))

    def test___doc__(self):
        expected_doc = """Implementation module for SSL socket operations.  See the socket module
for documentation."""
        self.assertEqual(real_ssl.__doc__, expected_doc)


    def test__test_decode_cert(self):
        if is_cli and hasattr(real_ssl, "decode_cert"):
            self.fail("Please add a test for _ssl.decode_cert")
        print('TODO: no implementation to test yet.')


    def test_sslwrap(self):
        print('TODO: no implementation to test yet.')


    def test_SSLType(self):
        #--Positive
        if is_cli:
            #https://github.com/IronLanguages/main/issues/733
            self.assertEqual(str(real_ssl.SSLType),
                    "<type '_socket.ssl'>")
        else:        
            self.assertEqual(str(real_ssl.SSLType),
                    "<type 'ssl.SSLContext'>")
                 
    
    '''
    TODO: once we have a proper implementation of _ssl.sslwrap the tests below need
        to be rewritten.
    '''

    @retryOnFailure
    def test_SSLType_ssl(self):
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

        ssl_s.shutdown()
        s.close()
        
        #sock, keyfile, certfile
        #TODO!



    @unittest.expectedFailure
    @retryOnFailure
    def test_SSLType_ssl_neg(self):
        '''
        See comments on test_SSLType_ssl.  Basically this needs to be revisited
        entirely (TODO) after we're more compatible with CPython.
        '''
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        
        #--Negative
        
        #Empty
        self.assertRaises(TypeError, real_ssl.sslwrap)
        self.assertRaises(TypeError, real_ssl.sslwrap, False)
        
        #None
        self.assertRaises(TypeError, real_ssl.sslwrap, None, False)
        
        #s, bad keyfile
        #Should throw _ssl.SSLError because both keyfile and certificate weren't specified
        self.assertRaises(real_ssl.SSLError, real_ssl.sslwrap, s._sock, False, "bad keyfile")
        
        #s, bad certfile
        #Should throw _ssl.SSLError because both keyfile and certificate weren't specified
        
        #s, bad keyfile, bad certfile
        #Should throw ssl.SSLError
        self.assertRaises(real_ssl.SSLError, real_ssl.sslwrap, s._sock, False, "bad keyfile", "bad certfile")
        
        #Cleanup
        s.close()

    @retryOnFailure
    def test_SSLType_issuer(self):
        #--Positive
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        ssl_s = real_ssl.sslwrap(s._sock, False)
        self.assertEqual(ssl_s.issuer(), '')  #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24281
        ssl_s.do_handshake()
        
        #Incompat, but a good one at that
        if is_cli:
            self.assertTrue("Returns a string that describes the issuer of the server's certificate" in ssl_s.issuer.__doc__)
        else:
            self.assertEqual(ssl_s.issuer.__doc__, None)            
        
        issuer = ssl_s.issuer()
        #If we can get the issuer once, we should be able to do it again
        self.assertEqual(issuer, ssl_s.issuer())
        self.assertTrue(SSL_ISSUER in issuer)
        
        
        #--Negative
        self.assertRaisesMessage(TypeError, "issuer() takes no arguments (1 given)",
                            ssl_s.issuer, None)
        self.assertRaisesMessage(TypeError, "issuer() takes no arguments (1 given)",
                            ssl_s.issuer, 1)
        self.assertRaisesMessage(TypeError, "issuer() takes no arguments (2 given)",
                            ssl_s.issuer, 3.14, "abc")

        #Cleanup
        ssl_s.shutdown()
        s.close()

    @retryOnFailure
    def test_SSLType_server(self):
        #--Positive
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        ssl_s = real_ssl.sslwrap(s._sock, False)
        self.assertEqual(ssl_s.server(), '')  #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24281
        ssl_s.do_handshake()
        
        if is_cli:
            #Incompat, but a good one at that
            self.assertTrue("Returns a string that describes the issuer of the server's certificate" in ssl_s.issuer.__doc__)
        else:
            self.assertEqual(ssl_s.server.__doc__, None)

        server = ssl_s.server()
        #If we can get the server once, we should be able to do it again
        self.assertEqual(server, ssl_s.server())
        self.assertTrue(SSL_SERVER in server)
        
        
        #--Negative
        self.assertRaisesMessage(TypeError, "server() takes no arguments (1 given)",
                            ssl_s.server, None)
        self.assertRaisesMessage(TypeError, "server() takes no arguments (1 given)",
                            ssl_s.server, 1)
        self.assertRaisesMessage(TypeError, "server() takes no arguments (2 given)",
                            ssl_s.server, 3.14, "abc")

        #Cleanup
        ssl_s.shutdown()
        s.close()
    
    @retryOnFailure
    def test_SSLType_read_and_write(self):
        #--Positive
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        ssl_s = real_ssl.sslwrap(s._sock, False)
        ssl_s.do_handshake()
        
        self.assertTrue("Writes the string s into the SSL object." in ssl_s.write.__doc__)
        self.assertTrue("Read up to len bytes from the SSL socket." in ssl_s.read.__doc__)
        
        #Write
        self.assertEqual(ssl_s.write(SSL_REQUEST),
                len(SSL_REQUEST))
        
        #Read
        self.assertEqual(ssl_s.read(4).lower(), "http")
        
        response = ssl_s.read(5000)
        self.assertTrue(SSL_RESPONSE in response)
        
        #Cleanup
        ssl_s.shutdown()  
        s.close()

run_test(__name__)