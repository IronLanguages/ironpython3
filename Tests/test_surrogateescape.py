# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test surrogateescape encoding error handler
##

import unittest
import codecs

from iptest import run_test

class SurrogateEscapeTest(unittest.TestCase):
    def test_ascii(self):
        b = bytes(range(256))
        s = b.decode("ascii", errors="surrogateescape")
        encoded = s.encode("ascii", errors="surrogateescape")
        self.assertEqual(encoded, b)

    def test_utf_8(self):
        b = bytes(range(256))
        s = b.decode("utf_8", errors="surrogateescape")
        encoded = s.encode("utf_8", errors="surrogateescape")
        self.assertEqual(encoded, b)

    def test_utf_16(self):
        b_dabcd = b'\xda\xdb\xdc\xdd'
        s_dabcd = b_dabcd.decode("utf_16", errors="surrogateescape")
        self.assertEqual(s_dabcd, '\U001069dc')
        encoded = s_dabcd.encode("utf_16", errors="surrogateescape")
        # encoded will have BOM added
        self.assertEqual(encoded, codecs.BOM_UTF16 + b_dabcd)

    def test_utf_16_le(self):
        b_dabcd = b'\xda\xdb\xdc\xdd'
        s_dabcd = b_dabcd.decode("utf_16_le", errors="surrogateescape")
        encoded = s_dabcd.encode("utf_16_le", errors="surrogateescape")
        self.assertEqual(encoded, b_dabcd)

    def test_utf_16_be(self):
        b_dabcd = b'\xda\xdb\xdc\xdd'
        s_dabcd = b_dabcd.decode("utf_16_be", errors="surrogateescape")
        encoded = s_dabcd.encode("utf_16_be", errors="surrogateescape")
        self.assertEqual(encoded, b_dabcd)

    def test_utf_32(self):
        b_dabcdef = b'\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf'
        s_dabcdef = b_dabcdef.decode("utf_32", errors="surrogateescape")
        encoded = s_dabcdef.encode("utf_32", errors="surrogateescape")
        # encoded will have BOM added
        self.assertEqual(encoded, codecs.BOM_UTF32 + b_dabcdef)

run_test(__name__)
