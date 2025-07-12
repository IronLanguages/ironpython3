# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests for the _ssl module.  See http://docs.python.org/library/ssl.html
'''

import _ssl
import os
import socket
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, retryOnFailure, run_test, skipUnlessIronPython

SSL_URL      = "www.python.org"
SSL_ISSUER   = "CN=GlobalSign Atlas R3 DV TLS CA 2025 Q1, O=GlobalSign nv-sa, C=BE"
SSL_SERVER   = "www.python.org"
SSL_PORT     = 443
SSL_REQUEST  = b"GET /en-us HTTP/1.0\r\nHost: www.python.org\r\n\r\n"
SSL_RESPONSE = b"Python Programming Language"

CERTFILE = os.path.join(os.path.dirname(__file__), "keycert.pem")

class _SslTest(IronPythonTestCase):
    def test_constants(self):
        self.assertEqual(_ssl.CERT_NONE, 0)
        self.assertEqual(_ssl.CERT_OPTIONAL, 1)
        self.assertEqual(_ssl.CERT_REQUIRED, 2)
        if is_cli or sys.version_info >= (3,5):
            self.assertRaises(AttributeError, lambda: _ssl.PROTOCOL_SSLv2)
        else:
            self.assertEqual(_ssl.PROTOCOL_SSLv2, 0)
        self.assertEqual(_ssl.PROTOCOL_SSLv23, 2)
        if is_cli or sys.version_info >= (3,7):
            self.assertRaises(AttributeError, lambda: _ssl.PROTOCOL_SSLv3)
        else:
            self.assertEqual(_ssl.PROTOCOL_SSLv3, 1)
        self.assertEqual(_ssl.PROTOCOL_TLSv1, 3)
        self.assertEqual(_ssl.PROTOCOL_TLSv1_1, 4)
        self.assertEqual(_ssl.PROTOCOL_TLSv1_2, 5)
        if sys.version_info >= (3,7):
            self.assertEqual(_ssl.OP_NO_SSLv2, 0)
        else:
            self.assertEqual(_ssl.OP_NO_SSLv2, 0x1000000)
        self.assertEqual(_ssl.OP_NO_SSLv3, 0x2000000)
        self.assertEqual(_ssl.OP_NO_TLSv1, 0x4000000)
        self.assertEqual(_ssl.OP_NO_TLSv1_1, 0x10000000)
        self.assertEqual(_ssl.OP_NO_TLSv1_2, 0x8000000)
        self.assertEqual(_ssl.SSL_ERROR_EOF, 8)
        self.assertEqual(_ssl.SSL_ERROR_INVALID_ERROR_CODE, 10)
        self.assertEqual(_ssl.SSL_ERROR_SSL, 1)
        self.assertEqual(_ssl.SSL_ERROR_SYSCALL, 5)
        self.assertEqual(_ssl.SSL_ERROR_WANT_CONNECT, 7)
        self.assertEqual(_ssl.SSL_ERROR_WANT_READ, 2)
        self.assertEqual(_ssl.SSL_ERROR_WANT_WRITE, 3)
        self.assertEqual(_ssl.SSL_ERROR_WANT_X509_LOOKUP, 4)
        self.assertEqual(_ssl.SSL_ERROR_ZERO_RETURN, 6)

    def test_RAND_add(self):
        #--Positive
        self.assertEqual(_ssl.RAND_add("", 3.14), None)
        self.assertEqual(_ssl.RAND_add(u"", 3.14), None)
        self.assertEqual(_ssl.RAND_add("", 3), None)

        #--Negative
        for g1, g2 in [ (None, None),
                        ("", None),
                        (None, 3.14), #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24276
                        ]:
            self.assertRaises(TypeError, _ssl.RAND_add, g1, g2)

        self.assertRaises(TypeError, _ssl.RAND_add)
        self.assertRaises(TypeError, _ssl.RAND_add, "")
        self.assertRaises(TypeError, _ssl.RAND_add, 3.14)
        self.assertRaises(TypeError, _ssl.RAND_add, "", 3.14, "")

    def test_RAND_status(self):
        #--Positive
        self.assertEqual(_ssl.RAND_status(), 1)

        #--Negative
        self.assertRaises(TypeError, _ssl.RAND_status, None)
        self.assertRaises(TypeError, _ssl.RAND_status, "")
        self.assertRaises(TypeError, _ssl.RAND_status, 1)
        self.assertRaises(TypeError, _ssl.RAND_status, None, None)

    def test_SSLError(self):
        self.assertEqual(_ssl.SSLError.__bases__, (socket.error, ))

    def test___doc__(self):
        expected_doc = """Implementation module for SSL socket operations.  See the socket module
for documentation."""
        self.assertEqual(_ssl.__doc__, expected_doc)

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
        context = _ssl._SSLContext(_ssl.PROTOCOL_SSLv23)
        ssl_s = context._wrap_socket(s, False)

        if is_cli:
            ssl_s.shutdown()
        s.close()

        #sock, keyfile, certfile
        #TODO!

    def test_SSLType_ssl_neg(self):
        '''
        See comments on test_SSLType_ssl.  Basically this needs to be revisited
        entirely (TODO) after we're more compatible with CPython.
        '''
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        context = _ssl._SSLContext(_ssl.PROTOCOL_SSLv23)

        #--Negative

        #Empty
        self.assertRaises(TypeError, context._wrap_socket)
        self.assertRaises(TypeError, context._wrap_socket, False)

        #None
        self.assertRaises(TypeError, context._wrap_socket, None, False)

        #Cleanup
        s.close()

    @skipUnlessIronPython()
    def test_SSLType_issuer(self):
        #--Positive
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        context = _ssl._SSLContext(_ssl.PROTOCOL_SSLv23)
        ssl_s = context._wrap_socket(s, False)
        self.assertEqual(ssl_s.issuer(), '')  #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24281
        ssl_s.do_handshake()

        #Incompat, but a good one at that
        if is_cli:
            self.assertIn("Returns a string that describes the issuer of the server's certificate", ssl_s.issuer.__doc__)
        else:
            self.assertEqual(ssl_s.issuer.__doc__, None)

        issuer = ssl_s.issuer()
        #If we can get the issuer once, we should be able to do it again
        self.assertEqual(issuer, ssl_s.issuer())
        self.assertIn(SSL_ISSUER, issuer)

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

    @skipUnlessIronPython()
    def test_SSLType_server(self):
        #--Positive
        s = socket.socket(socket.AF_INET)
        s.connect((SSL_URL, SSL_PORT))
        context = _ssl._SSLContext(_ssl.PROTOCOL_SSLv23)
        ssl_s = context._wrap_socket(s, False)
        self.assertEqual(ssl_s.server(), '')  #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24281
        ssl_s.do_handshake()

        if is_cli:
            #Incompat, but a good one at that
            self.assertIn("Returns a string that describes the issuer of the server's certificate", ssl_s.issuer.__doc__)
        else:
            self.assertEqual(ssl_s.server.__doc__, None)

        server = ssl_s.server()
        #If we can get the server once, we should be able to do it again
        self.assertEqual(server, ssl_s.server())
        self.assertIn(SSL_SERVER, server)

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
        context = _ssl._SSLContext(_ssl.PROTOCOL_SSLv23)
        ssl_s = context._wrap_socket(s, False)
        ssl_s.do_handshake()

        if is_cli or sys.version_info >= (3,5):
            self.assertIn("Writes the bytes-like object b into the SSL object.", ssl_s.write.__doc__)
            self.assertIn("Read up to size bytes from the SSL socket.", ssl_s.read.__doc__)
        else:
            self.assertIn("Writes the string s into the SSL object.", ssl_s.write.__doc__)
            self.assertIn("Read up to len bytes from the SSL socket.", ssl_s.read.__doc__)

        #Write
        self.assertEqual(ssl_s.write(SSL_REQUEST),
                len(SSL_REQUEST))

        #Read
        self.assertEqual(ssl_s.read(4).lower(), b"http")

        max_response_length = 5000
        response = b''
        while len(response) < max_response_length:
            r = ssl_s.read(max_response_length - len(response))
            if not r: break
            response += r
        self.assertIn(SSL_RESPONSE, response)

        #Cleanup
        if is_cli:
            ssl_s.shutdown()
        s.close()

    def test_parse_cert(self):
        """part of test_parse_cert from CPython.test_ssl"""

        # note that this uses an 'unofficial' function in _ssl.c,
        # provided solely for this test, to exercise the certificate
        # parsing code
        p = _ssl._test_decode_cert(CERTFILE)
        self.assertEqual(p['issuer'],
                         ((('countryName', 'XY'),),
                          (('localityName', 'Castle Anthrax'),),
                          (('organizationName', 'Python Software Foundation'),),
                          (('commonName', 'localhost'),))
                        )
        # Note the next three asserts will fail if the keys are regenerated
        self.assertEqual(p['notAfter'], 'Oct  5 23:01:56 2020 GMT')
        self.assertEqual(p['notBefore'], 'Oct  8 23:01:56 2010 GMT')
        self.assertEqual(p['serialNumber'], 'D7C7381919AFC24E')
        self.assertEqual(p['subject'],
                         ((('countryName', 'XY'),),
                          (('localityName', 'Castle Anthrax'),),
                          (('organizationName', 'Python Software Foundation'),),
                          (('commonName', 'localhost'),))
                        )
        self.assertEqual(p['subjectAltName'], (('DNS', 'localhost'),))

    @skipUnlessIronPython()
    def test_cert_date_locale(self):
        import System
        if is_netcoreapp:
            import clr
            clr.AddReference("System.Threading.Thread")

        culture = System.Threading.Thread.CurrentThread.CurrentCulture
        try:
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo("fr")
            p = _ssl._test_decode_cert(CERTFILE)
            self.assertEqual(p['notAfter'], 'Oct  5 23:01:56 2020 GMT')
            self.assertEqual(p['notBefore'], 'Oct  8 23:01:56 2010 GMT')
        finally:
            System.Threading.Thread.CurrentThread.CurrentCulture = culture

import _ssl as ssl

# These come from the 3.5 stdlib and can eventually be removed
@unittest.skipUnless(is_cli or sys.version_info >= (3,5), "not in CPython 3.4")
class MemoryBIOTests(unittest.TestCase):
    def test_read_write(self):
        bio = ssl.MemoryBIO()
        bio.write(b'foo')
        self.assertEqual(bio.read(), b'foo')
        self.assertEqual(bio.read(), b'')
        bio.write(b'foo')
        bio.write(b'bar')
        self.assertEqual(bio.read(), b'foobar')
        self.assertEqual(bio.read(), b'')
        bio.write(b'baz')
        self.assertEqual(bio.read(2), b'ba')
        self.assertEqual(bio.read(1), b'z')
        self.assertEqual(bio.read(1), b'')

    def test_eof(self):
        bio = ssl.MemoryBIO()
        self.assertFalse(bio.eof)
        self.assertEqual(bio.read(), b'')
        self.assertFalse(bio.eof)
        bio.write(b'foo')
        self.assertFalse(bio.eof)
        bio.write_eof()
        self.assertFalse(bio.eof)
        self.assertEqual(bio.read(2), b'fo')
        self.assertFalse(bio.eof)
        self.assertEqual(bio.read(1), b'o')
        self.assertTrue(bio.eof)
        self.assertEqual(bio.read(), b'')
        self.assertTrue(bio.eof)

    def test_pending(self):
        bio = ssl.MemoryBIO()
        self.assertEqual(bio.pending, 0)
        bio.write(b'foo')
        self.assertEqual(bio.pending, 3)
        for i in range(3):
            bio.read(1)
            self.assertEqual(bio.pending, 3-i-1)
        for i in range(3):
            bio.write(b'x')
            self.assertEqual(bio.pending, i+1)
        bio.read()
        self.assertEqual(bio.pending, 0)

    def test_buffer_types(self):
        bio = ssl.MemoryBIO()
        bio.write(b'foo')
        self.assertEqual(bio.read(), b'foo')
        bio.write(bytearray(b'bar'))
        self.assertEqual(bio.read(), b'bar')
        bio.write(memoryview(b'baz'))
        self.assertEqual(bio.read(), b'baz')

    def test_error_types(self):
        bio = ssl.MemoryBIO()
        self.assertRaises(TypeError, bio.write, 'foo')
        self.assertRaises(TypeError, bio.write, None)
        self.assertRaises(TypeError, bio.write, True)
        self.assertRaises(TypeError, bio.write, 1)

run_test(__name__)
