# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test surrogatepass encoding error handler
##

import unittest
import codecs

from iptest import run_test

class SurrogatePassTest(unittest.TestCase):
    def test_ascii(self):
        self.assertEqual("abc".encode("ascii", errors="surrogatepass"), b"abc")
        self.assertEqual(b"abc".decode("ascii", errors="surrogatepass"), "abc")

    def test_utf_7(self):
        self.assertEqual("abc\ud810xyz".encode("utf_7", errors="surrogatepass"), b"abc+2BA-xyz")
        self.assertEqual(b"abc+2BA-xyz".decode("utf_7", errors="surrogatepass"), "abc\ud810xyz")

    def test_utf_8(self):
        self.assertEqual("abc\ud810xyz".encode("utf_8", errors="surrogatepass"), b"abc\xed\xa0\x90xyz")
        self.assertEqual(b"abc\xed\xa0\x90xyz".decode("utf_8", errors="surrogatepass"), "abc\ud810xyz")

    def test_utf_16_le(self):
        # lone high surrogate
        self.assertEqual("\ud810".encode("utf_16_le", errors="surrogatepass"), b"\x10\xd8")
        self.assertEqual(b"\x10\xd8".decode("utf_16_le", errors="surrogatepass"), "\ud810")

        #lone low surrogate
        self.assertEqual("\udc0a".encode("utf_16_le", errors="surrogatepass"), b"\n\xdc")
        self.assertEqual(b"\n\xdc".decode("utf_16_le", errors="surrogatepass"), "\udc0a")
        
        # invalid surrogate pair (low, high)
        self.assertEqual("\ude51\uda2f".encode("utf_16_le", errors="surrogatepass"), b"Q\xde/\xda")
        self.assertEqual(b"Q\xde/\xda".decode("utf_16_le", errors="surrogatepass"), "\ude51\uda2f")
        
    def test_utf_16_be(self):
        # lone high surrogate
        self.assertEqual("\ud810".encode("utf_16_be", errors="surrogatepass"), b"\xd8\x10")
        self.assertEqual(b"\xd8\x10".decode("utf_16_be", errors="surrogatepass"), "\ud810")

        #lone low surrogate
        self.assertEqual("\udc0a".encode("utf_16_be", errors="surrogatepass"), b"\xdc\n")
        self.assertEqual(b"\xdc\n".decode("utf_16_be", errors="surrogatepass"), "\udc0a")
        
        # invalid surrogate pair (low, high)
        self.assertEqual("\ude51\uda2f".encode("utf_16_be", errors="surrogatepass"), b"\xdeQ\xda/")
        self.assertEqual(b"\xdeQ\xda/".decode("utf_16_be", errors="surrogatepass"), "\ude51\uda2f")

    def test_utf_32_le(self):
        # lone high surrogate
        self.assertEqual("\ud810".encode("utf_32_le", errors="surrogatepass"), b"\x10\xd8\x00\x00")
        self.assertEqual(b"\x10\xd8\x00\x00".decode("utf_32_le", errors="surrogatepass"), "\ud810")

        #lone low surrogate
        self.assertEqual("\udc0a".encode("utf_32_le", errors="surrogatepass"), b"\n\xdc\x00\x00")
        self.assertEqual(b"\n\xdc\x00\x00".decode("utf_32_le", errors="surrogatepass"), "\udc0a")
        
        # invalid surrogate pair (low, high)
        self.assertEqual("\ude51\uda2f".encode("utf_32_le", errors="surrogatepass"), b"Q\xde\x00\x00/\xda\x00\x00")
        self.assertEqual(b"Q\xde\x00\x00/\xda\x00\x00".decode("utf_32_le", errors="surrogatepass"), "\ude51\uda2f")
        
    def test_utf_32_be(self):
        # lone high surrogate
        self.assertEqual("\ud810".encode("utf_32_be", errors="surrogatepass"), b"\x00\x00\xd8\x10")
        self.assertEqual(b"\x00\x00\xd8\x10".decode("utf_32_be", errors="surrogatepass"), "\ud810")

        #lone low surrogate
        self.assertEqual("\udc0a".encode("utf_32_be", errors="surrogatepass"), b"\x00\x00\xdc\n")
        self.assertEqual(b"\x00\x00\xdc\n".decode("utf_32_be", errors="surrogatepass"), "\udc0a")
        
        # invalid surrogate pair (low, high)
        self.assertEqual("\ude51\uda2f".encode("utf_32_be", errors="surrogatepass"), b"\x00\x00\xdeQ\x00\x00\xda/")
        self.assertEqual(b"\x00\x00\xdeQ\x00\x00\xda/".decode("utf_32_be", errors="surrogatepass"), "\ude51\uda2f")

run_test(__name__)
