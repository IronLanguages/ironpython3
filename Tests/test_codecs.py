# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test codecs, in addition to test_codecs from StdLib and from modules/io_related
##

import unittest
import codecs

import System.Text

from iptest import run_test

class CodecsTest(unittest.TestCase):
    def test_interop_ascii(self):
        self.assertEqual("abc".encode(System.Text.Encoding.ASCII), b"abc")
        self.assertEqual(b"abc".decode(System.Text.Encoding.ASCII), "abc")

        us_ascii = System.Text.ASCIIEncoding()
        self.assertEqual("abc".encode(us_ascii), b"abc")
        self.assertEqual(b"abc".decode(us_ascii), "abc")

    def test_interop_utf8(self):
        utf_8 = System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=False, throwOnInvalidBytes=True)
        utf_8_sig = System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=True, throwOnInvalidBytes=True)

        self.assertEqual("abć".encode(utf_8), b"ab\xc4\x87")
        self.assertEqual(b"ab\xc4\x87".decode(utf_8), "abć")
        self.assertEqual(b"ab\xc4\x87".decode(utf_8_sig), "abć")

        self.assertEqual("abć".encode(utf_8_sig), b"\xef\xbb\xbfab\xc4\x87")
        self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode(utf_8), "\ufeffabć")
        self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode(utf_8_sig), "abć")

        # now for comparison the same but with codec names
        self.assertEqual("abć".encode('utf_8'), b"ab\xc4\x87")
        self.assertEqual(b"ab\xc4\x87".decode('utf_8'), "abć")
        self.assertEqual(b"ab\xc4\x87".decode('utf_8_sig'), "abć")

        self.assertEqual("abć".encode('utf_8_sig'), b"\xef\xbb\xbfab\xc4\x87")
        self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode('utf_8'), "\ufeffabć")
        self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode('utf_8_sig'), "abć")

    def test_interop_utf16(self):
        utf_16 = System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=True, throwOnInvalidBytes=True)
        utf_16_le = System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=False, throwOnInvalidBytes=True)
        utf_16_be = System.Text.UnicodeEncoding(bigEndian=True, byteOrderMark=False, throwOnInvalidBytes=True)

        self.assertEqual("abć".encode(utf_16), b"\xff\xfea\x00b\x00\x07\x01")
        self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode(utf_16), "abć")
        self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode(utf_16_le), "\ufeffabć")

        self.assertEqual("abć".encode(utf_16_le), b"a\x00b\x00\x07\x01")
        self.assertEqual(b"a\x00b\x00\x07\x01".decode(utf_16), "abć")
        self.assertEqual(b"a\x00b\x00\x07\x01".decode(utf_16_le), "abć")

        self.assertEqual("abć".encode(utf_16_be), b"\x00a\x00b\x01\x07")
        self.assertEqual(b"\x00a\x00b\x01\x07".decode(utf_16_be), "abć")

        # now for comparison the same but with codec names
        self.assertEqual("abć".encode('utf_16'), b"\xff\xfea\x00b\x00\x07\x01")
        self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode('utf_16'), "abć")
        self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode('utf_16_le'), "\ufeffabć")

        self.assertEqual("abć".encode('utf_16_le'), b"a\x00b\x00\x07\x01")
        self.assertEqual(b"a\x00b\x00\x07\x01".decode('utf_16'), "abć")
        self.assertEqual(b"a\x00b\x00\x07\x01".decode('utf_16_le'), "abć")

        self.assertEqual("abć".encode('utf_16_be'), b"\x00a\x00b\x01\x07")
        self.assertEqual(b"\x00a\x00b\x01\x07".decode('utf_16_be'), "abć")

    def test_interop_utf32(self):
        utf_32 = System.Text.UTF32Encoding(bigEndian=False, byteOrderMark=True, throwOnInvalidCharacters=True)
        utf_32_le = System.Text.UTF32Encoding(bigEndian=False, byteOrderMark=False, throwOnInvalidCharacters=True)
        utf_32_be = System.Text.UTF32Encoding(bigEndian=True, byteOrderMark=False, throwOnInvalidCharacters=True)

        self.assertEqual("abć".encode(utf_32), b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
        self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32), "abć")
        self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32_le), "\ufeffabć")

        self.assertEqual("abć".encode(utf_32_le), b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
        self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32), "abć")
        self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32_le), "abć")

        self.assertEqual("abć".encode(utf_32_be), b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07")
        self.assertEqual(b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07".decode(utf_32_be), "abć")

        # now for comparison the same but with codec names
        self.assertEqual("abć".encode('utf_32'), b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
        self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32'), "abć")
        self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32_le'), "\ufeffabć")

        self.assertEqual("abć".encode('utf_32_le'), b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
        self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32'), "abć")
        self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32_le'), "abć")

        self.assertEqual("abć".encode('utf_32_be'), b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07")
        self.assertEqual(b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07".decode('utf_32_be'), "abć")

run_test(__name__)
