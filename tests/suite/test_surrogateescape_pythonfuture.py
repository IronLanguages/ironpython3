# -*- coding: utf-8 -*-
"""
Tests for the surrogateescape codec
From https://github.com/PythonCharmers/python-future/blob/master/tests/test_future/test_surrogateescape.py

Copyright (c) 2013-2018 Python Charmers Pty Ltd, Australia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
"""

import unittest
import codecs

from iptest import run_test, is_netcoreapp

class TestSurrogateEscape(unittest.TestCase):
    def test_surrogateescape(self):
        """
        From the backport of the email package
        """
        s = b'From: foo@bar.com\nTo: baz\nMime-Version: 1.0\nContent-Type: text/plain; charset=utf-8\nContent-Transfer-Encoding: base64\n\ncMO2c3RhbA\xc3\xa1=\n'
        u = 'From: foo@bar.com\nTo: baz\nMime-Version: 1.0\nContent-Type: text/plain; charset=utf-8\nContent-Transfer-Encoding: base64\n\ncMO2c3RhbA\udcc3\udca1=\n'
        s2 = s.decode('ASCII', errors='surrogateescape')
        self.assertEqual(s2, u)

    def test_encode_ascii_surrogateescape(self):
        """
        This crops up in the email module. It would be nice if it worked ...
        """
        payload = str(u'cMO2c3RhbA\udcc3\udca1=\n')
        b = payload.encode('ascii', 'surrogateescape')
        self.assertEqual(b, b'cMO2c3RhbA\xc3\xa1=\n')

    def test_encode_ascii_unicode(self):
        """
        Verify that exceptions are raised properly.
        """
        self.assertRaises(UnicodeEncodeError, u'\N{SNOWMAN}'.encode, 'US-ASCII', 'surrogateescape')

    def test_encode_ascii_surrogateescape_non_newstr(self):
        """
        As above but without a newstr object. Fails on Py2.
        """
        payload = u'cMO2c3RhbA\udcc3\udca1=\n'
        b = payload.encode('ascii', 'surrogateescape')
        self.assertEqual(b, b'cMO2c3RhbA\xc3\xa1=\n')


class SurrogateEscapeTest(unittest.TestCase):
    """
    These tests are from Python 3.3's test suite
    """
    def test_utf8(self):
        # Bad byte
        self.assertEqual(b"foo\x80bar".decode("utf-8", "surrogateescape"),
                         "foo\udc80bar")
        self.assertEqual(str("foo\udc80bar").encode("utf-8", "surrogateescape"),
                         b"foo\x80bar")
        # bad-utf-8 encoded surrogate
        # self.assertEqual(b"\xed\xb0\x80".decode("utf-8", "surrogateescape"),
        #                  "\udced\udcb0\udc80")
        self.assertEqual(str("\udced\udcb0\udc80").encode("utf-8", "surrogateescape"),
                         b"\xed\xb0\x80")

    def test_ascii(self):
        # bad byte
        self.assertEqual(b"foo\x80bar".decode("ascii", "surrogateescape"),
                         "foo\udc80bar")
        # Fails:
        # self.assertEqual("foo\udc80bar".encode("ascii", "surrogateescape"),
        #                  b"foo\x80bar")

    @unittest.skip(".NET maps \xa5 to U+F7F5 in ISO-8859-3")
    def test_charmap(self):
        # bad byte: \xa5 is unmapped in iso-8859-3
        self.assertEqual(b"foo\xa5bar".decode("iso-8859-3", "surrogateescape"),
                         "foo\udca5bar")
        self.assertEqual("foo\udca5bar".encode("iso-8859-3", "surrogateescape"),
                         b"foo\xa5bar")

    def test_latin1(self):
        # Issue6373
        self.assertEqual("\udce4\udceb\udcef\udcf6\udcfc".encode("latin-1", "surrogateescape"),
                         b"\xe4\xeb\xef\xf6\xfc")

    def test_encoding_works_normally(self):
        """
        Test that encoding into various encodings (particularly utf-16)
        still works with the surrogateescape error handler in action ...
        """
        TEST_UNICODE_STR = u'ℝεα∂@ßʟ℮ ☂ℯṧт υηḯ¢☺ḓ℮'
        # Tk icon as a .gif:
        TEST_BYTE_STR = b'GIF89a\x0e\x00\x0b\x00\x80\xff\x00\xff\x00\x00\xc0\xc0\xc0!\xf9\x04\x01\x00\x00\x01\x00,\x00\x00\x00\x00\x0e\x00\x0b\x00@\x02\x1f\x0c\x8e\x10\xbb\xcan\x90\x99\xaf&\xd8\x1a\xce\x9ar\x06F\xd7\xf1\x90\xa1c\x9e\xe8\x84\x99\x89\x97\xa2J\x01\x00;\x1a\x14\x00;;\xba\nD\x14\x00\x00;;'
        # s1 = 'quéstionable'
        s1 = TEST_UNICODE_STR
        b1 = s1.encode('utf-8')
        b2 = s1.encode('utf-16')
        # b3 = s1.encode('latin-1')
        self.assertEqual(b1, str(s1).encode('utf-8', 'surrogateescape'))
        self.assertEqual(b2, str(s1).encode('utf-16', 'surrogateescape'))
        # self.assertEqual(b3, str(s1).encode('latin-1', 'surrogateescape'))

        s2 = 'きたないのよりきれいな方がいい'
        b4 = s2.encode('utf-8')
        b5 = s2.encode('utf-16')
        self.assertEqual(b4, str(s2).encode('utf-8', 'surrogateescape'))
        self.assertEqual(b5, str(s2).encode('utf-16', 'surrogateescape'))
        if not is_netcoreapp:
            b6 = s2.encode('shift-jis')
            self.assertEqual(b6, str(s2).encode('shift-jis', 'surrogateescape'))

    def xtest_decoding_works_normally(self):
        """
        Test that decoding into various encodings (particularly utf-16)
        still works with the surrogateescape error handler in action ...
        """
        s1 = 'quéstionable'
        b1 = s1.encode('utf-8')
        b2 = s1.encode('utf-16')
        b3 = s1.encode('latin-1')
        self.assertEqual(s1, b1.decode('utf-8', 'surrogateescape'))
        self.assertEqual(s1, b2.decode('utf-16', 'surrogateescape'))
        self.assertEqual(s1, b3.decode('latin-1', 'surrogateescape'))

        s2 = '文'
        b4 = s2.encode('utf-8')
        b5 = s2.encode('utf-16')
        self.assertEqual(s2, b4.decode('utf-8', 'surrogateescape'))
        self.assertEqual(s2, b5.decode('utf-16', 'surrogateescape'))
        if not is_netcoreapp:
            b6 = s2.encode('shift-jis')
            self.assertEqual(s2, b6.decode('shift-jis', 'surrogateescape'))


run_test(__name__)
