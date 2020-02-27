# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# test codecs
#

'''
TODO - essentially all the tests currently here are barebones sanity checks
to ensure a minimal level of functionality exists. In other words, there are
many special cases that are not being covered *yet*.
'''

import codecs
import os
import re
import shutil
import string
import subprocess
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_cpython, is_mono, is_netcoreapp, is_posix, is_linux, is_windows, run_test, skipUnlessIronPython
from iptest.misc_util import ip_supported_encodings

if is_cpython and is_linux:
    import time

class CodecTest(IronPythonTestCase):
    def test_escape_decode(self):
        # escape_decode decodes bytes to bytes, but when given a string it encodes it first with UTF-8
        self.assertEqual(codecs.escape_decode("abc€ghi🐍xyz"), codecs.escape_decode(b'abc\xe2\x82\xacghi\xf0\x9f\x90\x8dxyz'))

        value, length = codecs.escape_decode("ab\a\b\t\n\r\f\vba")
        self.assertEqual(value, b'ab\x07\x08\t\n\r\x0c\x0bba')
        self.assertEqual(length, 11)

        value, length = codecs.escape_decode("\\a")
        self.assertEqual(value, b'\x07')
        self.assertEqual(length, 2)

        value, length = codecs.escape_decode("ab\a\b\t\n\r\f\v\'\"baab\\a\\b\\t\\n\\r\\f\\v\\'\\\"baab\\\a\\\b\\\t\\\n\\\r\\\f\\\vba")
        self.assertEqual(value, b'ab\x07\x08\t\n\r\x0c\x0b\'\"baab\x07\x08\t\n\r\x0c\x0b\'\"baab\\\x07\\\x08\\\t\\\r\\\x0c\\\x0bba')
        self.assertEqual(length, 53)

        value, length = codecs.escape_decode("\\\a")
        self.assertEqual(value, b'\\\x07')
        self.assertEqual(length, 2)

        value, length = codecs.escape_decode("\\07")
        self.assertEqual(value, b'\x07')
        self.assertEqual(length, 3)

        value, length = codecs.escape_decode("\\047")
        self.assertEqual(value, b"'")
        self.assertEqual(length, 4)

        self.assertEqual(codecs.escape_decode(b"ab\nc"), (b"ab\nc", 4))
        self.assertEqual(codecs.escape_decode(b"ab\rc"), (b"ab\rc", 4))
        self.assertEqual(codecs.escape_decode(b"ab\r\nc"), (b"ab\r\nc", 5))

        self.assertEqual(codecs.escape_decode(b"ab\\\nc"), (b"abc", 5))
        self.assertEqual(codecs.escape_decode(b"ab\\\rc"), (b"ab\\\rc", 5))
        self.assertEqual(codecs.escape_decode(b"ab\\\r\\\nc"), (b"ab\\\rc", 7))

    def test_escape_decode_errors(self):
        self.assertEqual(codecs.escape_decode("abc", None), (b"abc", 3))

        self.assertEqual(b"?", codecs.escape_decode("\\x", 'replace')[0])
        self.assertEqual(b"?", codecs.escape_decode("\\x2", 'replace')[0])
        self.assertEqual(b"?I", codecs.escape_decode("\\xI", 'replace')[0])
        self.assertEqual(b"?II", codecs.escape_decode("\\xII", 'replace')[0])
        self.assertEqual(b"?I", codecs.escape_decode("\\x1I", 'replace')[0])
        self.assertEqual(b"?I1", codecs.escape_decode("\\xI1", 'replace')[0])

        self.assertEqual(b"abc", codecs.escape_decode("abc\\x", 'ignore')[0])
        self.assertEqual(b"abc", codecs.escape_decode("abc\\x2", 'ignore')[0])
        self.assertEqual(b"abcI", codecs.escape_decode("abc\\xI", 'ignore')[0])
        self.assertEqual(b"abcII", codecs.escape_decode("abc\\xII", 'ignore')[0])
        self.assertEqual(b"abcI", codecs.escape_decode("abc\\x1I", 'ignore')[0])
        self.assertEqual(b"abcI1", codecs.escape_decode("abc\\xI1", 'ignore')[0])

        self.assertRaisesRegex(ValueError, r"Trailing \\ in string", codecs.escape_decode, b"\\", None)
        self.assertRaisesRegex(ValueError, r"Trailing \\ in string", codecs.escape_decode, b"\\", 'strict')
        self.assertRaisesRegex(ValueError, r"Trailing \\ in string", codecs.escape_decode, b"\\", 'replace')
        self.assertRaisesRegex(ValueError, r"Trailing \\ in string", codecs.escape_decode, b"\\", 'ignore')
        self.assertRaisesRegex(ValueError, r"Trailing \\ in string", codecs.escape_decode, b"\\", 'non-existent')

        self.assertRaisesRegex(ValueError, r"invalid \\x escape at position 3", codecs.escape_decode, b"abc\\xii")
        self.assertRaisesRegex(ValueError, r"invalid \\x escape at position 3", codecs.escape_decode, b"abc\\x1i")
        self.assertRaisesRegex(ValueError, r"invalid \\x escape at position 3", codecs.escape_decode, b"abc\\xii", 'strict')
        self.assertRaisesRegex(ValueError, r"invalid \\x escape at position 3", codecs.escape_decode, b"abc\\x1i", 'strict')
        self.assertRaisesRegex(ValueError, r"invalid \\x escape at position 3", codecs.escape_decode, b"abc\\xii", None)
        self.assertRaisesRegex(ValueError, r"invalid \\x escape at position 3", codecs.escape_decode, b"abc\\x1i", None)

        for errors in ['backslashreplace', 'xmlcharrefreplace', 'namereplace', 'surrogateescape', 'surrogatepass', 'non-existent', '']:
            self.assertRaisesRegex(ValueError, "decoding error; unknown error handling code: " + errors, codecs.escape_decode, b"abc\\xii", errors)
            self.assertRaisesRegex(ValueError, "decoding error; unknown error handling code: " + errors, codecs.escape_decode, b"abc\\x1i", errors)

    def test_escape_encode(self):
        #sanity checks
        value, length = codecs.escape_encode(b"abba")
        self.assertEqual(value, b"abba")
        self.assertEqual(length, 4)

        value, length = codecs.escape_encode(b"ab\a\b\t\n\r\f\vba")
        self.assertEqual(value, b'ab\\x07\\x08\\t\\n\\r\\x0c\\x0bba')
        self.assertEqual(length, 11)

        value, length = codecs.escape_encode(b"\\a")
        self.assertEqual(value, b"\\\\a")
        self.assertEqual(length, 2)

        value, length = codecs.escape_encode(bytes(range(256)))
        self.assertEqual(value, b'\\x00\\x01\\x02\\x03\\x04\\x05\\x06\\x07\\x08\\t\\n\\x0b\\x0c\\r\\x0e\\x0f\\x10\\x11\\x12\\x13\\x14\\x15\\x16\\x17\\x18\\x19\\x1a\\x1b\\x1c\\x1d\\x1e\\x1f !"#$%&\\\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\\x7f\\x80\\x81\\x82\\x83\\x84\\x85\\x86\\x87\\x88\\x89\\x8a\\x8b\\x8c\\x8d\\x8e\\x8f\\x90\\x91\\x92\\x93\\x94\\x95\\x96\\x97\\x98\\x99\\x9a\\x9b\\x9c\\x9d\\x9e\\x9f\\xa0\\xa1\\xa2\\xa3\\xa4\\xa5\\xa6\\xa7\\xa8\\xa9\\xaa\\xab\\xac\\xad\\xae\\xaf\\xb0\\xb1\\xb2\\xb3\\xb4\\xb5\\xb6\\xb7\\xb8\\xb9\\xba\\xbb\\xbc\\xbd\\xbe\\xbf\\xc0\\xc1\\xc2\\xc3\\xc4\\xc5\\xc6\\xc7\\xc8\\xc9\\xca\\xcb\\xcc\\xcd\\xce\\xcf\\xd0\\xd1\\xd2\\xd3\\xd4\\xd5\\xd6\\xd7\\xd8\\xd9\\xda\\xdb\\xdc\\xdd\\xde\\xdf\\xe0\\xe1\\xe2\\xe3\\xe4\\xe5\\xe6\\xe7\\xe8\\xe9\\xea\\xeb\\xec\\xed\\xee\\xef\\xf0\\xf1\\xf2\\xf3\\xf4\\xf5\\xf6\\xf7\\xf8\\xf9\\xfa\\xfb\\xfc\\xfd\\xfe\\xff')
        self.assertEqual(length, 256)

    def test_register_error(self):
            '''
            TODO: test that these are actually used.
            '''
            #Sanity
            def garbage_error0(): print("garbage_error0")
            def garbage_error1(param1): print("garbage_error1:", param1)
            def garbage_error2(param1, param2): print("garbage_error2:", param1, "; ", param2)

            codecs.register_error("garbage0", garbage_error0)
            codecs.register_error("garbage1", garbage_error1)
            codecs.register_error("garbage2", garbage_error2)
            codecs.register_error("garbage1dup", garbage_error1)

    def test_charmap_decode(self):
        #Sanity
        new_str, num_processed = codecs.charmap_decode(b"abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(num_processed, 3)
        self.assertEqual(codecs.charmap_decode(b"a", 'strict', {ord('a') : 'a'})[0], 'a')
        self.assertEqual(codecs.charmap_decode(b"a", "replace", {})[0], '\ufffd')
        self.assertEqual(codecs.charmap_decode(b"a", "replace", {ord('a'): None})[0], '\ufffd')

        self.assertEqual(codecs.charmap_decode(b""), ('', 0))

        # using a string mapping
        self.assertEqual(codecs.charmap_decode(b'\x02\x01\x00', 'strict', "abc"), ('cba', 3))

        #Negative
        self.assertRaises(UnicodeDecodeError, codecs.charmap_decode, b"a", "strict", {})
        self.assertRaises(UnicodeDecodeError, codecs.charmap_decode, b"a", "strict", {'a': None})

        self.assertRaises(TypeError, codecs.charmap_decode, b"a", "strict", {ord('a'): 2.0})

    def test_decode(self):
        #sanity
        new_str = codecs.decode(b"abc")
        self.assertEqual(new_str, 'abc')

    def test_encode(self):
        #sanity
        new_str = codecs.encode("abc")
        self.assertEqual(new_str, b'abc')

    def test_raw_unicode_escape_decode(self):
        new_str, num_processed = codecs.raw_unicode_escape_decode("abc")
        self.assertEqual(new_str, "abc")
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.raw_unicode_escape_decode(b"abc")
        self.assertEqual(new_str, "abc")
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.raw_unicode_escape_decode("abc\\u20ACxyz")
        self.assertEqual(new_str, "abc€xyz")
        self.assertEqual(num_processed, 12)

        new_str, num_processed = codecs.raw_unicode_escape_decode("abc\\U0001F40Dxyz\\")
        self.assertEqual(new_str, "abc🐍xyz\\")
        self.assertEqual(num_processed, 17)

    def test_raw_unicode_escape_decode_errors(self):
        with self.assertRaises(UnicodeDecodeError) as cm:
            codecs.raw_unicode_escape_decode("abc\\u20klm\xffxyz\u20ac") # Unicode string

        self.assertEqual(cm.exception.encoding, 'rawunicodeescape')
        self.assertTrue(cm.exception.reason.startswith("truncated \\uXXXX"))
        self.assertEqual(cm.exception.start, 3)
        self.assertEqual(cm.exception.end, 7)
        self.assertEqual(cm.exception.object, b"abc\\u20klm\xc3\xbfxyz\xe2\x82\xac") # in UTF-8

        with self.assertRaises(UnicodeDecodeError) as cm:
            codecs.raw_unicode_escape_decode("abc\\U0001F44xyz")

        self.assertEqual(cm.exception.encoding, 'rawunicodeescape')
        if is_cpython and sys.version_info < (3, 6):
            self.assertEqual(cm.exception.reason, "truncated \\uXXXX")
        else:
            self.assertEqual(cm.exception.reason, "truncated \\UXXXXXXXX escape")
        self.assertEqual(cm.exception.start, 3)
        self.assertEqual(cm.exception.end, 12)
        self.assertEqual(cm.exception.object, b"abc\\U0001F44xyz")

        with self.assertRaises(UnicodeDecodeError) as cm:
            codecs.raw_unicode_escape_decode("abc\\U00110011xyz")

        self.assertEqual(cm.exception.encoding, 'rawunicodeescape')
        self.assertEqual(cm.exception.reason, "\\Uxxxxxxxx out of range")
        self.assertEqual(cm.exception.start, 3)
        self.assertEqual(cm.exception.end, 13)
        self.assertEqual(cm.exception.object, b"abc\\U00110011xyz")

        new_str, num_processed = codecs.raw_unicode_escape_decode(b"abc\\u20klm\\U0001F44nop\\U00110011xyz",'ignore')
        self.assertEqual(new_str, "abcklmnopxyz")
        self.assertEqual(num_processed, 35)

        new_str, num_processed = codecs.raw_unicode_escape_decode(b"abc\\u20klm\\U0001F44nop\\U00110011xyz",'replace')
        self.assertEqual(new_str, "abc\uFFFDklm\uFFFDnop\uFFFDxyz")
        self.assertEqual(num_processed, 35)

    def test_raw_unicode_escape_encode(self):
        new_str, num_processed = codecs.raw_unicode_escape_encode("abc")
        self.assertEqual(new_str, b'abc')
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.raw_unicode_escape_encode("\\a\tbc\r\n")
        self.assertEqual(new_str, b'\\a\tbc\r\n')
        self.assertEqual(num_processed, 7)

        new_str, num_processed = codecs.raw_unicode_escape_encode("=\0\x7f\x80¡ÿ!=")
        self.assertEqual(new_str, b'=\0\x7f\x80\xa1\xff!=')
        self.assertEqual(num_processed, 8)

        new_str, num_processed = codecs.raw_unicode_escape_encode("=€=")
        self.assertEqual(new_str, b'=\\u20ac=')
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.raw_unicode_escape_encode("=🜋=")
        self.assertEqual(new_str, b'=\\U0001f70b=')
        if is_cli: # surrogate pair processed
            self.assertEqual(num_processed, 4)
        else:
            self.assertEqual(num_processed, 3)

    def test_unicode_escape_decode(self):
        new_str, num_processed = codecs.unicode_escape_decode("abc")
        self.assertEqual(new_str, "abc")
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.unicode_escape_decode(b"abc")
        self.assertEqual(new_str, "abc")
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.unicode_escape_decode("abc\\u20ACxyz")
        self.assertEqual(new_str, "abc€xyz")
        self.assertEqual(num_processed, 12)

        new_str, num_processed = codecs.unicode_escape_decode("abc\\U0001F40Dxyz")
        self.assertEqual(new_str, "abc🐍xyz")
        self.assertEqual(num_processed, 16)

        new_str, num_processed = codecs.unicode_escape_decode("=\\u20AC=\\15\\n=\\xFF=\\\\\\=\\N{euro sign}=")
        self.assertEqual(new_str, "=€=\r\n=ÿ=\\\\=€=")
        self.assertEqual(num_processed, 37)

        new_str, num_processed = codecs.unicode_escape_decode("\\\n")
        self.assertEqual(new_str, "")
        self.assertEqual(num_processed, 2)

        new_str, num_processed = codecs.unicode_escape_decode("\\\r\\\n")
        self.assertEqual(new_str, "\\\r")
        self.assertEqual(num_processed, 4)

    def test_unicode_escape_decode_errors(self):
        def check(data, msg, start, end, ex_data):
            with self.assertRaises(UnicodeDecodeError) as cm:
                codecs.unicode_escape_decode(data)

            self.assertEqual(cm.exception.encoding, 'unicodeescape')
            self.assertEqual(cm.exception.reason, msg)
            self.assertEqual(cm.exception.start, start)
            self.assertEqual(cm.exception.end, end)
            self.assertEqual(cm.exception.object, ex_data)

        test_data = [
            ("abc\\xyz", "truncated \\xXX escape", 3, 5, b"abc\\xyz"), # str to bytes
            ("abc\\x0xyz", "truncated \\xXX escape", 3, 6, b"abc\\x0xyz"),
            ("abc\\u20klm\xffxyz\u20ac", "truncated \\uXXXX escape", 3, 7, b"abc\\u20klm\xc3\xbfxyz\xe2\x82\xac"), # Unicode to UTF-8
            ("abc\\U0001F44xyz", "truncated \\UXXXXXXXX escape", 3, 12, b"abc\\U0001F44xyz"),
            ("abc\\U00110011xyz", "illegal Unicode character", 3, 13, b"abc\\U00110011xyz"),
            ("abc\\N{EURO}xyz", "unknown Unicode character name", 3, 11, b"abc\\N{EURO}xyz"),
            ("abc\\Nxyz", "malformed \\N character escape", 3, 5, b"abc\\Nxyz"),
            ("abc\\N", "malformed \\N character escape", 3, 5, b"abc\\N"),
            ("abc\\N{xyz", "malformed \\N character escape", 3, 9, b"abc\\N{xyz"),
            ("abc\\N{", "malformed \\N character escape", 3, 6, b"abc\\N{"),
            ("abc\\N{}xyz", "malformed \\N character escape", 3, 6, b"abc\\N{}xyz"),
            ("abc\\N{}", "malformed \\N character escape", 3, 6, b"abc\\N{}"),
            ("abc\\", "\\ at end of string", 3, 4, b"abc\\"),
        ]

        for params in test_data:
            check(*params)

    def test_unicode_escape_decode_errors_ignore(self):
        test_data = [
            (b"abc\\xyz", "abcyz"),
            (b"abc\\x0xyz", "abcxyz"),
            (b"abc\\u20klm\xffxyz", "abcklm\xffxyz"),
            (b"abc\\U0001F44xyz", "abcxyz"),
            (b"abc\\U00110011xyz", "abcxyz"),
            (b"abc\\N{EURO}xyz", "abcxyz"),
            (b"abc\\Nxyz", "abcxyz"),
            (b"abc\\N", "abc"),
            (b"abc\\N{xyz", "abc"),
            (b"abc\\N{", "abc"),
            (b"abc\\N{}xyz", "abc}xyz"),
            (b"abc\\N{}", "abc}"),
            (b"abc\\", "abc"),
        ]

        for sample in test_data:
            self.assertEqual(codecs.unicode_escape_decode(sample[0], 'ignore')[0], sample[1])

    def test_unicode_escape_decode_errors_replace(self):
        test_data = [
            (b"abc\\xyz", "abc�yz"),
            (b"abc\\x0xyz", "abc�xyz"),
            (b"abc\\u20klm\xffxyz", "abc�klm\xffxyz"),
            (b"abc\\U0001F44xyz", "abc�xyz"),
            (b"abc\\U00110011xyz", "abc�xyz"),
            (b"abc\\N{EURO}xyz", "abc�xyz"),
            (b"abc\\Nxyz", "abc�xyz"),
            (b"abc\\N", "abc�"),
            (b"abc\\N{xyz", "abc�"),
            (b"abc\\N{", "abc�"),
            (b"abc\\N{}xyz", "abc�}xyz"),
            (b"abc\\N{}", "abc�}"),
            (b"abc\\", "abc�"),
        ]

        for sample in test_data:
            self.assertEqual(codecs.unicode_escape_decode(sample[0], 'replace')[0], sample[1])

    def test_unicode_escape_encode(self):
        new_str, num_processed = codecs.unicode_escape_encode("\\a\tbc\r\n")
        self.assertEqual(new_str, b'\\\\a\\tbc\\r\\n')
        self.assertEqual(num_processed, 7)

        new_str, num_processed = codecs.unicode_escape_encode("=\0\x7f\x80¡ÿ!=")
        self.assertEqual(new_str, b'=\\x00\\x7f\\x80\\xa1\\xff!=')
        self.assertEqual(num_processed, 8)

        new_str, num_processed = codecs.unicode_escape_encode("=€=")
        self.assertEqual(new_str, b'=\\u20ac=')
        self.assertEqual(num_processed, 3)

        new_str, num_processed = codecs.unicode_escape_encode("=🜋=")
        self.assertEqual(new_str, b'=\\U0001f70b=')
        if is_cli: # surrogate pair processed
            self.assertEqual(num_processed, 4)
        else:
            self.assertEqual(num_processed, 3)

    def test_utf_7_decode(self):
        #sanity
        new_str, num_processed = codecs.utf_7_decode(b"abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(num_processed, 3)

    def test_utf7_decode_incremental(self):
        b = "abc\u20acxyz".encode('utf-7')
        b += "abc\u20ad\u20aexyz".encode('utf-7')
        b += "abc\u20af\u20b0\u20b1xyz".encode('utf-7')

        # expected results generated by CPython 3.4
        expected = [
            ('', 0),
            ('a', 1),
            ('ab', 2),
            ('abc', 3),
            ('abc', 3),
            ('abc', 3),
            ('abc', 3),
            ('abc', 3),
            ('abc€', 8),
            ('abc€x', 9),
            ('abc€xy', 10),
            ('abc€xyz', 11),
            ('abc€xyza', 12),
            ('abc€xyzab', 13),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc', 14),
            ('abc€xyzabc₭₮', 22),
            ('abc€xyzabc₭₮x', 23),
            ('abc€xyzabc₭₮xy', 24),
            ('abc€xyzabc₭₮xyz', 25),
            ('abc€xyzabc₭₮xyza', 26),
            ('abc€xyzabc₭₮xyzab', 27),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc', 28),
            ('abc€xyzabc₭₮xyzabc₯₰₱', 38),
            ('abc€xyzabc₭₮xyzabc₯₰₱x', 39),
            ('abc€xyzabc₭₮xyzabc₯₰₱xy', 40),
            ('abc€xyzabc₭₮xyzabc₯₰₱xyz', 41)            
        ]

        for i in range(len(b) + 1):
            res = codecs.utf_7_decode(b[:i])
            self.assertEqual(res, expected[i])


    def test_utf_7_encode(self):
        #sanity
        new_str, num_processed = codecs.utf_7_encode("abc")
        self.assertEqual(new_str, b'abc')
        self.assertEqual(num_processed, 3)

    def test_ascii_decode(self):
        #sanity
        new_str, num_processed = codecs.ascii_decode(b"abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(num_processed, 3)

    def test_ascii_encode(self):
        #sanity
        new_str, num_processed = codecs.ascii_encode("abc")
        self.assertEqual(new_str, b'abc')
        self.assertEqual(num_processed, 3)

    def test_latin_1_decode(self):
        #sanity
        new_str, num_processed = codecs.latin_1_decode(b"abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(num_processed, 3)

    def test_latin_1_encode(self):
        #sanity
        new_str, num_processed = codecs.latin_1_encode("abc")
        self.assertEqual(new_str, b'abc')
        self.assertEqual(num_processed, 3)

        # so many ways to express latin 1...
        for x in ['iso-8859-1', 'iso8859-1', '8859', 'cp819', 'latin', 'latin1', 'L1']:
            self.assertEqual('abc'.encode(x), b'abc')

    def test_error_handlers(self):
        ude = UnicodeDecodeError('dummy', b"abcdefgh", 3, 5, "decoding testing purposes")
        uee = UnicodeEncodeError('dummy', "abcdefgh", 2, 6, "encoding testing purposes")
        ute = UnicodeTranslateError("abcdefgh", 2, 6, "translating testing purposes")
        unicode_data = "ab\xff\u20ac\U0001f40d\0\t\r\nz"
        uee_unicode = UnicodeEncodeError('dummy', unicode_data, 2, len(unicode_data), "encoding testing purposes")

        strict = codecs.lookup_error('strict')
        self.assertEqual(strict, codecs.strict_errors)
        with self.assertRaises(UnicodeDecodeError) as cm:
            strict(ude)
        self.assertEqual(cm.exception, ude)
        with self.assertRaises(UnicodeEncodeError) as cm:
            strict(uee)
        self.assertEqual(cm.exception, uee)
        with self.assertRaises(UnicodeTranslateError) as cm:
            strict(ute)
        self.assertEqual(cm.exception, ute)
        self.assertRaisesRegex(TypeError, "codec must pass exception instance", strict, None)
        self.assertRaisesRegex(TypeError, "\w+\(\) takes exactly (one|1) argument \(0 given\)", strict)
        self.assertRaisesRegex(TypeError, "\w+\(\) takes exactly (one|1) argument \(2 given\)", strict, ude, uee)
        self.assertRaises(LookupError, codecs.lookup_error, "STRICT")

        ignore = codecs.lookup_error('ignore')
        self.assertEqual(ignore, codecs.ignore_errors)
        self.assertEqual(ignore(ude), ("", 5))
        self.assertEqual(ignore(uee), ("", 6))
        self.assertEqual(ignore(ute), ("", 6))
        self.assertEqual(ignore(uee_unicode), ("", uee_unicode.end))

        replace = codecs.lookup_error('replace')
        self.assertEqual(replace, codecs.replace_errors)
        self.assertEqual(replace(ude), ("�", 5))
        self.assertEqual(replace(uee), ("????", 6))
        self.assertEqual(replace(ute), ("����", 6))
        self.assertEqual(replace(uee_unicode), ("?" * (uee_unicode.end - uee_unicode.start), uee_unicode.end))

        backslashreplace = codecs.lookup_error('backslashreplace')
        self.assertEqual(backslashreplace, codecs.backslashreplace_errors)
        self.assertRaisesRegex(TypeError, "don't know how to handle UnicodeDecodeError in error callback", backslashreplace, ude)
        self.assertEqual(backslashreplace(uee), (r"\x63\x64\x65\x66", 6))
        self.assertRaisesRegex(TypeError, "don't know how to handle UnicodeTranslateError in error callback", backslashreplace, ute)
        self.assertEqual(backslashreplace(uee_unicode), (r"\xff\u20ac\U0001f40d\x00\x09\x0d\x0a\x7a", uee_unicode.end))

        xmlcharrefreplace = codecs.lookup_error('xmlcharrefreplace')
        self.assertEqual(xmlcharrefreplace, codecs.xmlcharrefreplace_errors)
        self.assertRaisesRegex(TypeError, "don't know how to handle UnicodeDecodeError in error callback", xmlcharrefreplace, ude)
        self.assertEqual(xmlcharrefreplace(uee), ("&#99;&#100;&#101;&#102;", 6))
        self.assertRaisesRegex(TypeError, "don't know how to handle UnicodeTranslateError in error callback", xmlcharrefreplace, ute)
        self.assertEqual(xmlcharrefreplace(uee_unicode), ("&#255;&#8364;&#128013;&#0;&#9;&#13;&#10;&#122;", uee_unicode.end))

    def test_error_handlers_surrogateescape(self):
        surrogateescape = codecs.lookup_error('surrogateescape')

        # Decoding with surrogateescape

        self.assertEqual(surrogateescape(UnicodeDecodeError('dummy', b"a\xff\x7fz", 1, 2, "encoding testing purposes")), ("\udcff", 2))
        self.assertEqual(surrogateescape(UnicodeDecodeError('dummy', b"a\xff\x7fz", 1, 3, "encoding testing purposes")), ("\udcff", 2))
        self.assertEqual(surrogateescape(UnicodeDecodeError('dummy', b"a\xff\x80z", 1, 3, "encoding testing purposes")), ("\udcff\udc80", 3))
        self.assertEqual(surrogateescape(UnicodeDecodeError('dummy', b"a\xff\x80\x81z", 1, 3, "encoding testing purposes")), ("\udcff\udc80", 3))

        ude = UnicodeDecodeError('dummy', b"abcd", 1, 3, "ASCII bytes cannot be smuggled (PEP 383)")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogateescape(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('dummy', b"a\x7f\xffz", 1, 3, "ASCII bytes cannot be smuggled, 0x7f is withing ASCII range")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogateescape(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('utf-16-le', b"a\x00\x00\xdcz\x00", 2, 4, r"although \x00\xdc is \udc00 in utf-16-le, it contains ASCII byte \x00")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogateescape(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('utf-16-le', b"a\x00\xff\xdcz\x00", 2, 4, r"\xff\xdc is \udcff in utf-16-le, and each byte is being escaped individually")
        self.assertEqual(surrogateescape(ude), ("\udcff\udcdc", 4))

        # Encoding with surrogateescape

        self.assertEqual(surrogateescape(UnicodeEncodeError('dummy', "a\udcff\udc7fz", 1, 2, "encoding testing purposes")), (b"\xff", 2))
        self.assertEqual(surrogateescape(UnicodeEncodeError('dummy', "a\udcff\udc80z", 1, 3, "encoding testing purposes")), (b"\xff\x80", 3))
        self.assertEqual(surrogateescape(UnicodeEncodeError('dummy', "a\udcff\udc80\udc81z", 1, 3, "encoding testing purposes")), (b"\xff\x80", 3))

        uee = UnicodeEncodeError('dummy', "abcd", 1, 3, "no low surrogates to un-escape")
        with self.assertRaises(UnicodeEncodeError) as cm:
            surrogateescape(uee)
        self.assertEqual(cm.exception, uee)

        uee = UnicodeEncodeError('dummy', "\x80\x81\x82\x83", 1, 3, "no low surrogates to un-escape")
        with self.assertRaises(UnicodeEncodeError) as cm:
            surrogateescape(uee)
        self.assertEqual(cm.exception, uee)

        uee = UnicodeEncodeError('dummy', "a\udcff\udc7fz", 1, 3, r"ASCII bytes cannot be smuggled, \udc7f carries ASCI byte \x7f")
        with self.assertRaises(UnicodeEncodeError) as cm:
            surrogateescape(uee)
        self.assertEqual(cm.exception, uee)

        ude = UnicodeEncodeError('utf-16-le', "a\udc00\udcdcz", 1, 3, r"ASCII bytes cannot be smuggled, \udc00 carries ASCI byte \x00")
        with self.assertRaises(UnicodeEncodeError) as cm:
            surrogateescape(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeEncodeError('utf-16-le', "a\udcff\udcdcz", 1, 3, "encoding testing purposes")
        self.assertEqual(surrogateescape(ude), (b"\xff\xdc", 3))

        ude = UnicodeEncodeError('utf-16-le', "a\udcff\udcdcz", 1, 2, "one byte at a time for widechar encoding")
        self.assertEqual(surrogateescape(ude), (b"\xff", 2))

        # Translating with surrogateescape

        ute = UnicodeTranslateError("abcd", 1, 3, "UnicodeTranslateError not supported with surrogateescape")
        self.assertRaisesRegex(TypeError, "don't know how to handle UnicodeTranslateError in error callback", surrogateescape, ute)

    def test_error_handlers_surrogatepass(self):
        surrogatepass = codecs.lookup_error('surrogatepass')

        # Decoding with surrogatepass

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xaez', 1, 4, "encoding testing purposes")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('dummy', b'a\xed\xb7\xaez', 1, 4, "for unrecognized encoding fall back to utf-8")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16-lex', b'a\xed\xb7\xaez', 1, 4, "for misspelled encoding fall back to utf-8")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16--le', b'a\xed\xb7\xaez', 1, 4, "for misspelled encoding fall back to utf-8")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf--16-le', b'a\xed\xb7\xaez', 1, 4, "for misspelled encoding fall back to utf-8")), ("\uddee", 4))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 4, "only one surrogate at a time")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 7, "only one surrogate at a time")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 4, 7, "only one surrogate at a time")), ("\uddff", 7))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf16', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16-le', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf-16LE', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf_16LE', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16s")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf_16_LE', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16s")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf16Le', b"a\0\xff\xdcz\0", 2, 4, "various names for UTF-16")), ("\udcff", 4))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16-be', b"\0a\xdc\xff\0z", 2, 4, "various names for UTF-16BE")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf-16BE', b"\0a\xdc\xff\0z", 2, 4, "various names for UTF-16BE")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf_16BE', b"\0a\xdc\xff\0z", 2, 4, "various names for UTF-16BE")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf16Be', b"\0a\xdc\xff\0z", 2, 4, "various names for UTF-16BE")), ("\udcff", 4))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf32', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32-le', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf-32LE', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf_32LE', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf_32_LE', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf32Le', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 8, "various names for UTF-32")), ("\udcff", 8))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32-be', b"\0\0\0a\0\0\xdc\xff\0\0\0z", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf-32BE', b"\0\0\0a\0\0\xdc\xff\0\0\0z", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf_32BE', b"\0\0\0a\0\0\xdc\xff\0\0\0z", 4, 8, "various names for UTF-32")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('Utf32Be', b"\0\0\0a\0\0\xdc\xff\0\0\0z", 4, 8, "various names for UTF-32")), ("\udcff", 8))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 0, "end index ignored")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 1, "end index ignored")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 2, "end index ignored")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 3, "end index ignored")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 5, "end index ignored")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, 50, "end index ignored")), ("\uddee", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-8', b'a\xed\xb7\xae\xed\xb7\xbfz', 1, -50, "end index ignored")), ("\uddee", 4))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, 0, "end index ignored")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, 2, "end index ignored")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, 3, "end index ignored")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, 5, "end index ignored")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, 50, "end index ignored")), ("\udcff", 4))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\0\xff\xdcz\0", 2, -50, "end index ignored")), ("\udcff", 4))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 0, "end index ignored")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 2, "end index ignored")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, 22, "end index ignored")), ("\udcff", 8))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32', b"a\0\0\0\xff\xdc\0\0z\0\0\0", 4, -22, "end index ignored")), ("\udcff", 8))

        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-16', b"a\xff\xdcz", 1, 0, "misaligned bytes")), ("\udcff", 3))
        self.assertEqual(surrogatepass(UnicodeDecodeError('utf-32', b"a\xff\xdc\0\0z", 1, 0, "misaligned bytes")), ("\udcff", 5))

        ude = UnicodeDecodeError('utf-8', b"abcde", 1, 4, "no surrogate present")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogatepass(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('utf-8', b'a\xed\xb7\xed\xb7\xbfz', 1, 4, "incomplete surogate")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogatepass(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('utf-8', b'a\xed\xb7', 1, 4, "incomplete surogate")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogatepass(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('utf-16', b"abcde", 2, 4, "no surrogate present")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogatepass(ude)
        self.assertEqual(cm.exception, ude)

        ude = UnicodeDecodeError('utf-32', b"abcde", 2, 4, "no surrogate present")
        with self.assertRaises(UnicodeDecodeError) as cm:
            surrogatepass(ude)
        self.assertEqual(cm.exception, ude)

        # Encoding with surrogatepass

        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-8', "a\udcff\ud880z", 1, 2, "encoding testing purposes")), (b'\xed\xb3\xbf', 2))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-8', "a\udcff\ud880z", 1, 3, "encoding testing purposes")), (b'\xed\xb3\xbf\xed\xa2\x80', 3))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-8', "a\udcff\ud880\udc81z", 1, 3, "encoding testing purposes")), (b'\xed\xb3\xbf\xed\xa2\x80', 3))
        self.assertEqual(surrogatepass(UnicodeEncodeError('dummy', "a\udcff\udc80z", 1, 2, "for unrecognized encoding fall back to utf-8")), (b'\xed\xb3\xbf', 2))

        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-16', "a\udcff\ud880z", 1, 2, "encoding testing purposes")), (b'\xff\xdc', 2))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-16', "a\udcff\ud880z", 1, 3, "encoding testing purposes")), (b'\xff\xdc\x80\xd8', 3))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-16', "a\udcff\ud880\udc81z", 1, 3, "encoding testing purposes")), (b'\xff\xdc\x80\xd8', 3))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-16', "a\ud800\udc00z", 1, 3, "encoding testing purposes")), (b'\x00\xd8\x00\xdc', 3))

        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-32', "a\udcff\ud880z", 1, 2, "encoding testing purposes")), (b'\xff\xdc\0\0', 2))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-32', "a\udcff\ud880z", 1, 3, "encoding testing purposes")), (b'\xff\xdc\0\0\x80\xd8\0\0', 3))
        self.assertEqual(surrogatepass(UnicodeEncodeError('utf-32', "a\udcff\ud880\udc81z", 1, 3, "encoding testing purposes")), (b'\xff\xdc\0\0\x80\xd8\0\0', 3))

        for encoding in ['utf-8', 'utf-16', 'utf-16-le', 'utf-16-be' 'utf-32', 'utf-32-le', 'utf-32-be']:
            uee = UnicodeEncodeError(encoding, "abcd", 1, 3, "no surrogates to pass")
            with self.assertRaises(UnicodeEncodeError) as cm:
                surrogatepass(uee)
            self.assertEqual(cm.exception, uee)

            uee = UnicodeEncodeError(encoding, "a\uddddcd", 1, 3, "not all surrogates")
            with self.assertRaises(UnicodeEncodeError) as cm:
                surrogatepass(uee)
            self.assertEqual(cm.exception, uee)

        # Translating with surrogatepass

        ute = UnicodeTranslateError("abcd", 1, 3, "UnicodeTranslateError not supported with surrogatepass")
        self.assertRaisesRegex(TypeError, "don't know how to handle UnicodeTranslateError in error callback", surrogatepass, ute)

    #TODO: @skip("multiple_execute")
    def test_lookup_error(self):
        #sanity
        self.assertRaises(LookupError, codecs.lookup_error, "blah garbage xyz")
        def garbage_error1(someError): pass
        codecs.register_error("blah garbage xyz", garbage_error1)
        self.assertEqual(codecs.lookup_error("blah garbage xyz"), garbage_error1)
        def garbage_error2(someError): pass
        codecs.register_error("some other", garbage_error2)
        self.assertEqual(codecs.lookup_error("some other"), garbage_error2)

    #TODO: @skip("multiple_execute")
    def test_register(self):
        '''
        TODO: test that functions passed in are actually used
        '''
        #sanity check - basically just ensure that functions can be registered
        def garbage_func1(param1): pass
        codecs.register(garbage_func1)

        #negative cases
        self.assertRaises(TypeError, codecs.register)
        self.assertRaises(TypeError, codecs.register, None)
        self.assertRaises(TypeError, codecs.register, ())
        self.assertRaises(TypeError, codecs.register, [])
        self.assertRaises(TypeError, codecs.register, 1)
        self.assertRaises(TypeError, codecs.register, "abc")
        self.assertRaises(TypeError, codecs.register, 3.14)

    def test_unicode_internal_encode(self):
        # takes one or two parameters, not zero or three
        self.assertRaises(TypeError, codecs.unicode_internal_encode)
        self.assertRaises(TypeError, codecs.unicode_internal_encode, 'abc', 'def', 'qrt')
        if is_cli or is_windows:
            self.assertEqual(codecs.unicode_internal_encode('abc'), (b'a\x00b\x00c\x00', 3))
        else:
            self.assertEqual(codecs.unicode_internal_encode('abc'), (b'a\x00\x00\x00b\x00\x00\x00c\x00\x00\x00', 3))

    def test_unicode_internal_decode(self):
        # takes one or two parameters, not zero or three
        self.assertRaises(TypeError, codecs.unicode_internal_decode)
        self.assertRaises(TypeError, codecs.unicode_internal_decode, 'abc', 'def', 'qrt')
        if is_cli or is_windows:
            self.assertEqual(codecs.unicode_internal_decode(b'ab'), ('\u6261', 2))
        else:
            self.assertEqual(codecs.unicode_internal_decode(b'ab\0\0'), ('\u6261', 4))

    def test_utf_16_be_decode(self):
        string, num_processed = codecs.utf_16_be_decode(b'\0a\0b\0c')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 3 * 2)

        string, num_processed = codecs.utf_16_be_decode(codecs.BOM_UTF16_BE + b'\0a\0b\0c')
        self.assertEqual(string, "\uFEFFabc")
        self.assertEqual(num_processed, 4 * 2)

    def test_utf_16_be_decode_incremental(self):
        b = b"\xff\xfe\x00\x41\xd9\x00\xdd\x00\xdc\x00\xd8\x00\xdc\x00"
        expected = [
            ('', 0),
            ('', 0),
            ('\ufffe', 2),
            ('\ufffe', 2),
            ('\ufffeA', 4),
            ('\ufffeA', 4),
            ('\ufffeA', 4),
            ('\ufffeA', 4),
            ('\ufffeA\U00050100', 8),
            ('\ufffeA\U00050100', 8),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd\U00010000', 14)
        ]
        if not is_cli:
            # CPython's strings are UTF-32 so an invalid surrogate pair results in one replacement char.
            # Therefore CPython cannot report error on a dangling low surrogate until it verifies
            # that the next char is not an invalid surrogate as well.
            expected[10] = expected[11] = ('\ufffeA\U00050100', 8)

        for i in range(len(b) + 1):
            res = codecs.utf_16_be_decode(b[:i], 'replace')
            self.assertEqual(res, expected[i])

        self.assertRaises(UnicodeDecodeError, codecs.utf_16_be_decode, b"\x41\x00\xd8\x00\xd8\x00", 'strict', False)

    def test_utf_16_be_encode(self):
        data, num_processed = codecs.utf_16_be_encode("abc")
        self.assertEqual(data, b'\0a\0b\0c')
        self.assertEqual(num_processed, 3)

    def test_utf_16_le_decode(self):
        string, num_processed = codecs.utf_16_le_decode(b'a\0b\0c\0')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 3 * 2)

        string, num_processed = codecs.utf_16_le_decode(codecs.BOM_UTF16_LE + b'a\0b\0c\0')
        self.assertEqual(string, "\uFEFFabc")
        self.assertEqual(num_processed, 4 * 2)

    def test_utf_16_le_decode_incremental(self):
        b = b"\xfe\xff\x41\x00\x00\xd9\x00\xdd\x00\xdc\x00\xd8\x00\xdc"
        expected = [
            ('', 0),
            ('', 0),
            ('\ufffe', 2),
            ('\ufffe', 2),
            ('\ufffeA', 4),
            ('\ufffeA', 4),
            ('\ufffeA', 4),
            ('\ufffeA', 4),
            ('\ufffeA\U00050100', 8),
            ('\ufffeA\U00050100', 8),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd', 10),
            ('\ufffeA\U00050100\ufffd\U00010000', 14)
        ]
        if not is_cli:
            # CPython's strings are UTF-32 so an invalid surrogate pair results in one replacement char.
            # Therefore CPython cannot report error on a dangling low surrogate until it verifies
            # that the next char is not an invalid surrogate as well.
            expected[10] = expected[11] = ('\ufffeA\U00050100', 8)

        for i in range(len(b) + 1):
            res = codecs.utf_16_le_decode(b[:i], 'replace')
            self.assertEqual(res, expected[i])

        self.assertRaises(UnicodeDecodeError, codecs.utf_16_le_decode, b"\x00\x41\x00\xd8\x00\xd8", 'strict', False)

    def test_utf_16_le_encode(self):
        data, num_processed = codecs.utf_16_le_encode("abc")
        self.assertEqual(data, b'a\0b\0c\0')
        self.assertEqual(num_processed, 3)

    def test_utf_16_ex_decode(self):
        #sanity
        new_str, num_processed, zero = codecs.utf_16_ex_decode(b"abc")
        self.assertEqual(new_str, '\u6261')
        self.assertEqual(num_processed, 2)
        self.assertEqual(zero, 0)

        self.utf_ex_decode_test_helper(
            charwidth=2,
            abc="abc",
            func=codecs.utf_16_ex_decode,
            abc_le=b'a\0b\0c\0',
            abc_be=b'\0a\0b\0c',
            bom_le=codecs.BOM_UTF16_LE,
            bom_be=codecs.BOM_UTF16_BE)

    def test_utf_16_decode(self):
        # When BOM present: it is removed and the proper UTF-16 variant is automatically selected
        string, num_processed = codecs.utf_16_decode(codecs.BOM_UTF16_LE + b'a\0b\0c\0')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 4 * 2)

        string, num_processed = codecs.utf_16_decode(codecs.BOM_UTF16_BE + b'\0a\0b\0c')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 4 * 2)

        # When no BOM: on little-endian systems, UTF-16 defaults to UTF-16-LE
        string, num_processed = codecs.utf_16_decode(b'a\0b\0c\0')
        self.assertEqual(string, 'abc')
        self.assertEqual(num_processed, 3 * 2)

    def test_utf_16_encode(self):
        # On little-endian systems, UTF-16 encodes in UTF-16-LE prefixed with BOM
        data, num_processed = codecs.utf_16_encode("abc")
        self.assertEqual(data, codecs.BOM_UTF16 + b'a\0b\0c\0')
        self.assertEqual(num_processed, 3)

    def test_utf_16_le_encode_alias(self):
        for x in ('utf_16_le', 'UTF-16LE', 'utf-16le', 'utf-16-le'):
            self.assertEqual('abc'.encode(x), b'a\x00b\x00c\x00')

    def test_utf_32_be_decode(self):
        string, num_processed = codecs.utf_32_be_decode(b'\0\0\0a\0\0\0b\0\0\0c')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 3 * 4)

        string, num_processed = codecs.utf_32_be_decode(codecs.BOM_UTF32_BE + b'\0\0\0a\0\0\0b\0\0\0c')
        self.assertEqual(string, "\uFEFFabc")
        self.assertEqual(num_processed, 4 * 4)

    def test_utf_32_be_encode(self):
        data, num_processed = codecs.utf_32_be_encode("abc")
        self.assertEqual(data, b'\0\0\0a\0\0\0b\0\0\0c')
        self.assertEqual(num_processed, 3)

    def test_utf_32_le_decode(self):
        string, num_processed = codecs.utf_32_le_decode(b'a\0\0\0b\0\0\0c\0\0\0')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 3 * 4)

        string, num_processed = codecs.utf_32_le_decode(codecs.BOM_UTF32_LE + b'a\0\0\0b\0\0\0c\0\0\0')
        self.assertEqual(string, "\uFEFFabc")
        self.assertEqual(num_processed, 4 * 4)

    def test_utf_32_le_encode(self):
        data, num_processed = codecs.utf_32_le_encode("abc")
        self.assertEqual(data, b'a\0\0\0b\0\0\0c\0\0\0')
        self.assertEqual(num_processed, 3)

    def test_utf_32_ex_decode(self):
        self.utf_ex_decode_test_helper(
            charwidth=4,
            abc="abc",
            func=codecs.utf_32_ex_decode,
            abc_le=b'a\0\0\0b\0\0\0c\0\0\0',
            abc_be=b'\0\0\0a\0\0\0b\0\0\0c',
            bom_le=codecs.BOM_UTF32_LE,
            bom_be=codecs.BOM_UTF32_BE)

    def test_utf_32_decode(self):
        # When BOM present: it is removed and the proper UTF-32 variant is automatically selected
        string, num_processed = codecs.utf_32_decode(codecs.BOM_UTF32_LE + b'a\0\0\0b\0\0\0c\0\0\0')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 4 * 4)

        string, num_processed = codecs.utf_32_decode(codecs.BOM_UTF32_BE + b'\0\0\0a\0\0\0b\0\0\0c')
        self.assertEqual(string, "abc")
        self.assertEqual(num_processed, 4 * 4)

        # When no BOM: on little-endian systems, UTF-32 defaults to UTF-32-LE
        string, num_processed = codecs.utf_32_decode(b'a\0\0\0b\0\0\0c\0\0\0')
        self.assertEqual(string, 'abc')
        self.assertEqual(num_processed, 3 * 4)

        with self.assertRaises(UnicodeDecodeError):
            codecs.utf_32_decode(b'\0\0\0a\0\0\0b\0\0\0c')

    def test_utf_32_encode(self):
        # On little-endian systems, UTF-32 encodes in UTF-32-LE prefixed with BOM
        data, num_processed = codecs.utf_32_encode("abc")
        self.assertEqual(data, codecs.BOM_UTF32 + b'a\0\0\0b\0\0\0c\0\0\0')
        self.assertEqual(num_processed, 3)

    def utf_ex_decode_test_helper(self, charwidth, abc, func, abc_le, abc_be, bom_le, bom_be):
        bom_abc_le = bom_le + abc_le
        bom_abc_be = bom_be + abc_be

        order = 0
        # When BOM present, and no order given: BOM is removed and the proper UTF-32 variant is automatically detected and used
        string, num_processed, detected = func(bom_abc_le, 'strict', order)
        self.assertEqual(string, abc)
        self.assertEqual(num_processed, (1 + len(abc)) * charwidth)
        self.assertLess(detected, order)

        string, num_processed, detected = func(bom_abc_be, 'strict', order)
        self.assertEqual(string, abc)
        self.assertEqual(num_processed, (1 + len(abc)) * charwidth)
        self.assertGreater(detected, order)

        # When only BOM present, and no order given: the decoded string is empty but the UTF-32 variant is detected
        string, num_processed, detected = func(bom_le, 'strict', order)
        self.assertEqual(string, "")
        self.assertEqual(num_processed, charwidth)
        self.assertLess(detected, order)

        string, num_processed, detected = func(bom_be, 'strict', order)
        self.assertEqual(string, "")
        self.assertEqual(num_processed, charwidth)
        self.assertGreater(detected, order)

        # When no BOM, and no order given: on little-endian systems, UTF-XX defaults to UTF-XX-LE, but no BOM detection
        string, num_processed, detected = func(abc_le, 'strict', order)
        self.assertEqual(string, abc)
        self.assertEqual(num_processed, len(abc) * charwidth)
        self.assertEqual(detected, order)

        # When BOM present, and order given: BOM must match order and is passed to output, order unchanged
        for order in [1, 42]:
            string, num_processed, detected = func(bom_abc_be, 'strict', order)
            self.assertEqual(string, "\uFEFF" + abc)
            self.assertEqual(num_processed, (1 + len(abc)) * charwidth)
            self.assertEqual(detected, order)

            string, num_processed, detected = func(bom_abc_le, 'strict', -order)
            self.assertEqual(string, "\uFEFF" + abc)
            self.assertEqual(num_processed, (1 + len(abc)) * charwidth)
            self.assertEqual(detected, -order)

        # When no BOM, and order given: on little-endian systems, UTF-XX defaults to UTF-XX-LE, order unchanged
        for order in [1, 42]:
            string, num_processed, detected = func(abc_be, 'strict', order)
            self.assertEqual(string, abc)
            self.assertEqual(num_processed, len(abc) * charwidth)
            self.assertEqual(detected, order)

            string, num_processed, detected = func(abc_le, 'strict', -order)
            self.assertEqual(string, abc)
            self.assertEqual(num_processed, len(abc) * charwidth)
            self.assertEqual(detected, -order)

    def test_utf_8_decode(self):
        #sanity
        new_str, num_processed = codecs.utf_8_decode(b"abc")
        self.assertEqual(new_str, 'abc')
        self.assertEqual(num_processed, 3)

    def test_cp34951(self):
        def internal_cp34951(sample1, preamble, bom_len):
            self.assertEqual(codecs.utf_8_decode(sample1), (preamble + '12\u20ac\x0a', 6 + bom_len))
            sample1 = sample1[:-1] # 12<euro>
            self.assertEqual(codecs.utf_8_decode(sample1), (preamble + '12\u20ac', 5 + bom_len))
            sample1 = sample1[:-1] # 12<uncomplete euro>
            self.assertEqual(codecs.utf_8_decode(sample1), (preamble + '12', 2 + bom_len))

            sample1 = sample1 + b'x7f' # makes it invalid
            self.assertRaises(UnicodeDecodeError, codecs.utf_8_decode, sample1)

        internal_cp34951(b'\x31\x32\xe2\x82\xac\x0a', '', 0) # 12<euro><cr>
        internal_cp34951(b'\xef\xbb\xbf\x31\x32\xe2\x82\xac\x0a', '\ufeff', 3) # <BOM>12<euro><cr>

    def test_utf_8_decode_incremental(self):
        b = '\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff\U00100000\U0010ffff'.encode('utf-8')
        # expected results generated by CPython 3.4
        expected = [
            ('', 0),
            ('\x00', 1),
            ('\x00\x7f', 2),
            ('\x00\x7f', 2),
            ('\x00\x7f\x80', 4),
            ('\x00\x7f\x80', 4),
            ('\x00\x7f\x80\xff', 6),
            ('\x00\x7f\x80\xff', 6),
            ('\x00\x7f\x80\xff\u0100', 8),
            ('\x00\x7f\x80\xff\u0100', 8),
            ('\x00\x7f\x80\xff\u0100\u07ff', 10),
            ('\x00\x7f\x80\xff\u0100\u07ff', 10),
            ('\x00\x7f\x80\xff\u0100\u07ff', 10),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800', 13),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800', 13),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800', 13),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd', 16),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd', 16),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd', 16),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff', 19),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff', 19),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff', 19),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff', 19),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000', 23),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000', 23),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000', 23),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000', 23),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff', 27),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff', 27),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff', 27),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff', 27),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff\U00100000', 31),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff\U00100000', 31),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff\U00100000', 31),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff\U00100000', 31),
            ('\x00\x7f\x80\xff\u0100\u07ff\u0800\ufffd\uffff\U00010000\U000fffff\U00100000\U0010ffff', 35)
        ]

        for i in range(len(b) + 1):
            res = codecs.utf_8_decode(b[:i])
            self.assertEqual(res, expected[i])

    def test_utf_8_encode(self):
        #sanity
        new_str, num_processed = codecs.utf_8_encode("abc")
        self.assertEqual(new_str, b'abc')
        self.assertEqual(num_processed, 3)

    def test_charmap_encode(self):
        # Sanity
        self.assertEqual(codecs.charmap_encode("abc"), (b'abc', 3))
        self.assertEqual(codecs.charmap_encode("abc", "strict"), (b'abc', 3))
        self.assertEqual(codecs.charmap_encode("", "strict", {}), (b'', 0))

        # Ignore errors
        self.assertEqual(codecs.charmap_encode("abc", "ignore", {}), (b'', 3))
        charmap = {ord(c): None for c in "abcdefgh"}
        self.assertEqual(codecs.charmap_encode("abc", "ignore", charmap), (b'', 3))

        # Dict[int, int]
        charmap = {ord(c): ord(c.upper()) for c in "abcdefgh"}
        self.assertEqual(codecs.charmap_encode("abc", "strict", charmap), (b'ABC', 3))

        # Dict[int, bytes]
        charmap = {ord(c): bytes(2*c.upper(), "ascii") for c in "abcdefgh"}
        self.assertEqual(codecs.charmap_encode("abc", "strict", charmap), (b'AABBCC', 3))

        # Fallback characters are charmapped again
        charmap = {ord(c): ord(c.upper()) for c in "abcdefgh"}
        charmap.update({ord('?'): ord('*')})
        self.assertEqual(codecs.charmap_encode("abcxyz", "replace", charmap), (b'ABC***', 6))
        charmap.update({ord('?'): b'+?-'})
        self.assertEqual(codecs.charmap_encode("abcxyz", "replace", charmap), (b'ABC+?-+?-+?-', 6))

        fallbacks = codecs.backslashreplace_errors(UnicodeEncodeError('dummy', "klm", 0, 3, "replace these chars"))[0]
        charmap = {ord(c): ord(c.upper()) for c in "abcdefgh" + fallbacks}
        charmap[ord('\\')] = ord('/')
        self.assertEqual(codecs.charmap_encode("abcklm", "backslashreplace", charmap), (b'ABC/X6B/X6C/X6D', 6))

        charmap = {ord(c): ord(c.upper()) for c in "abcdefgh0123456789"}
        charmap.update({ord('&'): b'@', ord('#'): b'$:', ord(';'): b':'})
        self.assertEqual(codecs.charmap_encode("abcklm", "xmlcharrefreplace", charmap), (b'ABC@$:107:@$:108:@$:109:', 6))

        # However, if the error handler returns bytes, these are not remapped, but used as they are
        if not is_cli: # TODO: IronPython does not support fallback bytes yet
            def my_encode_replace(uee): return (b"?!" * (uee.end - uee.start), uee.end)
            codecs.register_error('my_encode_replace', my_encode_replace)
            charmap = {ord(c): ord(c.upper()) for c in "abcdefgh"}
            self.assertEqual(codecs.charmap_encode("abcxyz", "my_encode_replace", charmap), (b'ABC?!?!?!', 6))

        # Fallback characters are not charmapped recursively
        for errors in ['strict', 'replace', 'backslashreplace', 'xmlcharrefreplace']:
            # Missing key
            self.assertRaisesRegex(UnicodeEncodeError, "'charmap' codec can't encode characters in position 0-2: character maps to <undefined>",
            #self.assertRaisesRegex(UnicodeEncodeError, "'charmap' codec can't encode character",
                codecs.charmap_encode, "abc", errors, {})

            # Character key is not recognized, it must be an int (character ordinal)
            self.assertRaisesRegex(UnicodeEncodeError, "'charmap' codec can't encode characters in position 0-2: character maps to <undefined>",
            #self.assertRaisesRegex(UnicodeEncodeError, "'charmap' codec can't encode character",
                codecs.charmap_encode, "abc", errors, {c: ord(c.upper()) for c in "abcdefgh"})

            # Explict None as value mapping
            self.assertRaisesRegex(UnicodeEncodeError, "'charmap' codec can't encode characters in position 0-2: character maps to <undefined>",
            #self.assertRaisesRegex(UnicodeEncodeError, "'charmap' codec can't encode character",
                codecs.charmap_encode, "abc", errors, {ord(c): None for c in "abcdefgh"})

        self.assertRaisesRegex(LookupError, "unknown error handler name 'non-existent'",
            codecs.charmap_encode, "abc", 'non-existent', {})

        # Unsupported: Dict[int, str]
        self.assertRaisesRegex(TypeError, "character mapping must return integer, bytes or None, not str",
            codecs.charmap_encode, "abc", "strict", {ord(c): c.upper() for c in "abcdefgh"})

        # Negative values
        self.assertRaisesRegex(TypeError, r"character mapping must be in range\(256\)",
            codecs.charmap_encode, "abc", "strict", {ord(c): -ord(c) for c in "abcdefgh"})

        # Values outside of bytes range
        self.assertRaisesRegex(TypeError, r"character mapping must be in range\(256\)",
            codecs.charmap_encode, "abc", "strict", {ord(c): ord(c) + 0x100 for c in "abcdefgh"})


    @unittest.skipIf(is_posix, 'only UTF8 on posix - mbcs_decode/encode only exist on windows versions of python')
    def test_mbcs_decode(self):
        for mode in ['strict', 'replace', 'ignore', 'badmodethatdoesnotexist']:
            if is_netcoreapp and mode == 'badmodethatdoesnotexist': continue # FallbackBuffer created even if not used
            self.assertEqual(codecs.mbcs_decode(b'foo', mode), ('foo', 3))
            cpyres = '\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\u20ac\x81\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\x8d\u017d\x8f\x90\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\x9d\u017e\u0178\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
            allchars = bytes(range(256))
            self.assertEqual(codecs.mbcs_decode(allchars, mode)[0], cpyres)

            # round tripping
            self.assertEqual(codecs.mbcs_encode(codecs.mbcs_decode(allchars, mode)[0])[0], allchars)

    @unittest.skipIf(is_posix, 'only UTF8 on posix - mbcs_decode/encode only exist on windows versions of python')
    def test_mbcs_encode(self):
        # these are invalid
        invalid = [0x80, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x8e, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9e, 0x9f]
        uinvalid = ''.join([chr(i) for i in invalid])
        uall = ''.join([chr(i) for i in range(256) if i not in invalid])
        cpyres = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\x81\x8d\x8f\x90\x9d\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        for mode in ['strict', 'replace', 'ignore', 'badmodethatdoesnotexist']:
            self.assertEqual(codecs.mbcs_encode('foo', mode), (b'foo', 3))
            ipyres = codecs.mbcs_encode(uall, mode)[0]
            self.assertEqual(cpyres, ipyres)

            # all weird unicode characters that are supported
            chrs = '\u20ac\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u017d\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u017e\u0178'
            self.assertEqual(codecs.mbcs_encode(chrs, mode), (b'\x80\x82\x83\x84\x85\x86\x87\x88\x89\x8a\x8b\x8c\x8e\x91\x92\x93\x94\x95\x96\x97\x98\x99\x9a\x9b\x9c\x9e\x9f', 27))

        self.assertEqual(codecs.mbcs_encode(uinvalid, 'replace'), (b'?'*len(uinvalid), len(uinvalid)))
        self.assertEqual(codecs.mbcs_encode(uinvalid, 'ignore'), (b'', len(uinvalid)))

    def test_misc_encodings(self):
        self.assertEqual('abc'.encode('utf-16'), b'\xff\xfea\x00b\x00c\x00')
        self.assertEqual('abc'.encode('utf-16-be'), b'\x00a\x00b\x00c')
        for unicode_escape in ['unicode-escape', 'unicode escape', 'unicode_escape']:
            self.assertEqual('abc'.encode(unicode_escape), b'abc')
            self.assertEqual('abc\\u1234'.encode(unicode_escape), b'abc\\\\u1234')

    def test_file_encodings(self):
        '''
        Tests valid PEP-236 style file encoding declarations during import
        '''

        sys.path.append(os.path.join(self.temporary_dir, "tmp_encodings"))
        try:
            os.mkdir(os.path.join(self.temporary_dir, "tmp_encodings"))
        except:
            pass

        try:
            #positive cases
            for coding in ip_supported_encodings:
                # check if the coding name matches PEP-263 requirements; this test is meaningless for names that do not match
                # https://www.python.org/dev/peps/pep-0263/#defining-the-encoding
                if not re.match('[-_.a-zA-Z0-9]+$', coding):
                    continue

                # wide-char Unicode encodings not supported by CPython
                if not is_cli and re.match('utf[-_](16|32)', coding, re.IGNORECASE):
                    continue

                temp_mod_name = "test_encoding_" + coding.replace('-','_')
                with open(os.path.join(self.temporary_dir, "tmp_encodings", temp_mod_name + ".py"), "w", encoding=coding) as f:
                    # wide-char Unicode encodings need a BOM to be recognized
                    if re.match('utf[-_](16|32).', coding, re.IGNORECASE):
                        f.write("\ufeff")

                    # UTF-8 with signature may only use 'utf-8' as coding (PEP-263)
                    if re.match('utf[-_]8[-_]sig$', coding, re.IGNORECASE):
                        coding = 'utf-8'

                    f.write("# coding: %s" % (coding))

                if is_cpython and is_linux:
                    time.sleep(0.01)
                __import__(temp_mod_name)
                os.remove(os.path.join(self.temporary_dir, "tmp_encodings", temp_mod_name + ".py"))

        finally:
            #cleanup
            sys.path.remove(os.path.join(self.temporary_dir, "tmp_encodings"))
            shutil.rmtree(os.path.join(self.temporary_dir, "tmp_encodings"), True)


        # handcrafted positive cases
        sys.path.append(os.path.join(self.test_dir, "encoded_files"))
        try:
            # Test that using tab of formfeed whitespace characters before "# coding ..." is OK
            # and that a tab between 'coding:' and the encoding name is OK too
            __import__('ok_encoding_whitespace')

            # Test that non-ASCII letters in the encoding name are not part of the name
            __import__('ok_encoding_nonascii')

        finally:
            sys.path.remove(os.path.join(self.test_dir, "encoded_files"))

    def test_file_encodings_negative(self):
        '''
        Test source file encoding errorr on import
        '''
        sys.path.append(os.path.join(self.test_dir, "encoded_files"))
        try:
            # Test that "# coding ..." declaration in the first line shadows the second line
            with self.assertRaises(SyntaxError) as cm:
                __import__("bad_encoding_name")
            # CPython's message differs when running this file, but is the same when importing it
            self.assertEqual(cm.exception.msg, "unknown encoding: bad-coding-name")

            # Test that latin-1 encoded files result in error if a coding declaration is missing
            with self.assertRaises(SyntaxError) as cm:
                __import__("bad_latin1_nodecl")
            # CPython's message differs when importing this file, but is the same when running it
            if is_cli:
                self.assertTrue(cm.exception.msg.startswith("Non-UTF-8 code starting with '\\xb5' in file"))
            else:
                self.assertTrue(cm.exception.msg.startswith("(unicode error) 'utf-8' codec can't decode byte 0xb5 in position "))

            # Test that latin-1 encoded files result in error if a UTF-8 BOM is present
            with self.assertRaises(SyntaxError) as cm:
                __import__("bad_latin1_bom")
            # CPython's message is the same (both on import and run)
            self.assertTrue(cm.exception.msg.startswith("(unicode error) 'utf-8' codec can't decode byte 0xb5 in position"))

            # Test that latin-1 encoded files result in error if a UTF-8 BOM is present and 'utf-8' encoding is declared
            with self.assertRaises(SyntaxError) as cm:
                __import__("bad_latin1_bom_decl")
            # CPython's message is the same (both on import and run)
            self.assertTrue(cm.exception.msg.startswith("(unicode error) 'utf-8' codec can't decode byte 0xb5 in position"))

            # Test that utf-8 encoded files result in error if a UTF-8 BOM is present and 'iso-8859-1' encoding is declared
            with self.assertRaises(SyntaxError) as cm:
                __import__("bad_utf8_bom_decl")
            # CPython's message is the same (both on import and run)
            self.assertTrue(cm.exception.msg.startswith("encoding problem: iso-8859-1 with BOM"))

            # Test that using a non-breaking whitespace inside the magic comment removes the magic
            self.assertRaises(SyntaxError, __import__, "bad_latin1_nbsp")

        finally:
            sys.path.remove(os.path.join(self.test_dir, "encoded_files"))

    @unittest.skipIf(is_posix, "https://github.com/IronLanguages/ironpython3/issues/541")
    @unittest.skipIf(is_mono, "https://github.com/IronLanguages/main/issues/1608")
    def test_cp11334(self):
        def run_python(filename):
            p = subprocess.Popen([sys.executable, os.path.join(self.test_dir, "encoded_files", filename)], shell=True, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            t_in, t_out, t_err = (p.stdin, p.stdout, p.stderr)
            t_err_lines = t_err.readlines()
            t_out_lines = t_out.readlines()
            t_err.close()
            t_out.close()
            t_in.close()
            return t_out_lines, t_err_lines

        #--Test that not using "# coding ..." results in an error
        t_out_lines, t_err_lines = run_python("cp11334_bad.py")

        self.assertEqual(len(t_out_lines), 0)
        self.assertTrue(t_err_lines[0].startswith(b"  File"))
        self.assertTrue(t_err_lines[0].rstrip().endswith(b', line 1'))
        self.assertTrue(t_err_lines[1].startswith(b"SyntaxError: Non-UTF-8 code starting with '\\xb5' in file"))

        #--Test that using "# coding ..." is OK
        t_out_lines, t_err_lines = run_python("cp11334_ok.py")

        self.assertEqual(len(t_err_lines), 0)
        self.assertEqual(len(t_out_lines), 1)
        if is_cli:
            # CodePlex 11334: IronPython uses active console codepage for output
            # Check active codepage in cmd.exe by running 'chcp' or 'mode'
            # Check active codepage in powershell.exe by evaluating [Console]::OutputEncoding
            import clr
            import System
            clr.AddReference('System.Console')
            # expected = '\xb5ble'.encode(System.Console.OutputEncoding) # this will not work correctly if encoding is UTF-8, which by default adds a preamble
            expected = '\xb5ble'.encode("cp" + str(System.Console.OutputEncoding.CodePage))
            self.assertEqual(t_out_lines[0].rstrip(), expected)
        else:
            # CPython uses locale.getpreferredencoding() for pipe output
            # unless overriden by PYTHONIOENCODING emvironment vairable.
            # The active console codepage is ignored (which is a confirmed bug: bpo-6135, bpo-27179)
            if not 'PYTHONIOENCODING' in os.environ:
                import locale
                expected = '\xb5ble'.encode(locale.getpreferredencoding())
                self.assertEqual(t_out_lines[0].rstrip(), expected)

    def test_cp1214(self):
        """
        TODO: extend this a great deal
        """
        with self.assertRaises(LookupError):
            b'7FF80000000000007FF0000000000000'.decode('hex')

        self.assertEqual(codecs.decode(b'7FF80000000000007FF0000000000000', 'hex'),
                b'\x7f\xf8\x00\x00\x00\x00\x00\x00\x7f\xf0\x00\x00\x00\x00\x00\x00')

    def test_codecs_lookup(self):
        l = []
        def my_func(encoding, cache = l):
            l.append(encoding)

        codecs.register(my_func)
        allchars = ''.join([chr(i) for i in range(1, 256)])
        self.assertRaises(LookupError, codecs.lookup, allchars)

        # Only ASCII chars are set to lowercase for lookup purposes
        lowerchars = allchars.translate(str.maketrans(' ' + string.ascii_uppercase, '-' + string.ascii_lowercase))
        for i in range(len(lowerchars)):
            if l[0][i] != lowerchars[i]:
                self.assertTrue(False, 'bad chars at index %d: %r %r' % (i, l[0][i], lowerchars[i]))

        self.assertRaises(TypeError, codecs.lookup, '\0')
        self.assertRaises(TypeError, codecs.lookup, 'abc\0')
        self.assertEqual(len(l), 1)

    def test_lookup_encodings(self):
        try:
            with self.assertRaises(UnicodeError):
                b'a'.decode('undefined')
        except LookupError:
            # if we don't have encodings then this will fail so
            # make sure we're failing because we don't have encodings
            self.assertRaises(ImportError, __import__, 'encodings')

    @unittest.skipIf(is_posix, "https://github.com/IronLanguages/ironpython3/issues/541")
    def test_cp1019(self):
        #--Test that bogus encodings fail properly
        # https://github.com/IronLanguages/main/issues/255
        p = subprocess.Popen([sys.executable, os.path.join(self.test_dir, "encoded_files", "cp1019.py")], shell=True, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        t_in, t_out, t_err = (p.stdin, p.stdout, p.stderr)
        t_err_lines = t_err.readlines()
        t_out_lines = t_out.readlines()
        t_err.close()
        t_out.close()
        t_in.close()

        self.assertEqual(len(t_out_lines), 0)
        self.assertTrue(t_err_lines[0].startswith(b"  File"))
        # CPython's message differs when running this file, but is the same when importing it
        if is_cli:
            self.assertTrue(t_err_lines[1].startswith(b"SyntaxError: unknown encoding: garbage"))
        else:
            self.assertTrue(t_err_lines[1].startswith(b"SyntaxError: encoding problem: garbage"))

    def test_cp20302(self):
        import _codecs
        for encoding in ip_supported_encodings:
            _codecs.lookup(encoding)

    def test_charmap_build(self):
        decodemap = ''.join([chr(i).upper() if chr(i).islower() else chr(i).lower() for i in range(256)])
        encodemap = codecs.charmap_build(decodemap)
        self.assertEqual(codecs.charmap_decode(b'Hello World', 'strict', decodemap), ('hELLO wORLD', 11))
        self.assertEqual(codecs.charmap_encode('Hello World', 'strict', encodemap), (b'hELLO wORLD', 11))

    def test_gh16(self):
        """
        https://github.com/IronLanguages/ironpython2/issues/16
        """
        # test with a standard error handler
        res = "\xac\u1234\u20ac\u8000".encode("ptcp154", "backslashreplace")
        self.assertEqual(res, b"\xac\\u1234\\u20ac\\u8000")

        # test with a custom error handler
        def handler(ex):
            return ("", ex.end)
        codecs.register_error("test_unicode_error", handler)
        res = "\xac\u1234\u20ac\u8000".encode("ptcp154", "test_unicode_error")
        self.assertEqual(res, b"\xac")

run_test(__name__)
