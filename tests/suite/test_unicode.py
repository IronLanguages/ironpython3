# -*- coding: utf-8 -*-
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import run_test

unicode = str
unichr = chr

class UnicodeTest(unittest.TestCase):
    def test_constructor(self):
        self.assertEqual('', unicode())
        self.assertEqual('None', unicode(None))
        self.assertEqual('ä', unicode('ä'))
        self.assertEqual('ä', unicode(b'\xc3\xa4', 'utf-8')) # TODO kunom: reasonable?

    def test_raw_unicode_escape(self):
        for raw_unicode_escape in ['raw-unicode-escape', 'raw unicode escape']:
            s = unicode(b'\u0663\u0661\u0664 ', raw_unicode_escape)
            self.assertEqual(len(s), 4)
            self.assertEqual(int(s), 314)
            s = unicode(b'\u0663.\u0661\u0664 ', raw_unicode_escape)
            self.assertEqual(float(s), 3.14)

    def test_raw_unicode_escape_noescape_lowchars(self):
        for raw_unicode_escape in ['raw-unicode-escape', 'raw unicode escape']:
            for i in range(0x100):
                self.assertEqual(unichr(i).encode(raw_unicode_escape), bytes([i]))

            self.assertEqual(unichr(0x100).encode(raw_unicode_escape), rb'\u0100')

    def test_raw_unicode_escape_dashes(self):
        """Make sure that either dashes or underscores work in raw encoding name"""
        ok = True
        try:
            unicode(b'hey', 'raw_unicode-escape')
        except LookupError:
            ok = False

        self.assertTrue(ok, "dashes and underscores should be interchangable")

    def test_raw_unicode_escape_trailing_backslash(self):
        self.assertEqual(unicode(b'\\', 'raw_unicode_escape'), u'\\')

    def test_unicode_error(self):
        from iptest.misc_util import ip_supported_encodings
        from _codecs import register_error
        def handler(ex):
            self.assertEqual(ex.object, u'\uac00')
            return (u"", ex.end)
        register_error("test_unicode_error", handler)

        for mode in ip_supported_encodings:
            unichr(0xac00).encode(mode, "test_unicode_error")

    def test_ignore(self):
        """only UTF8, no encoding fallbacks..."""
        self.assertEqual(unicode(b'', 'ascii', 'ignore'), '')
        self.assertEqual(unicode(b'\xff', 'ascii', 'ignore'), '')
        self.assertEqual(unicode(b'a\xffb\xffc\xff', 'ascii', 'ignore'), 'abc')

    def test_cp19005(self):
        foo = u'\xef\xbb\xbf'
        self.assertEqual(repr(foo), r"'ï»¿'")

    def test_cp34689(self):
        xx_full_width_a = u'xx\uff21'
        caught = False
        try:
            dummy = bytes(xx_full_width_a, "ascii")
        except UnicodeEncodeError as ex:
            caught = True
            self.assertEqual(ex.encoding, 'ascii')
            self.assertEqual(ex.start, 2)
            self.assertEqual(ex.end, 3)
            self.assertEqual(ex.object, u'xx\uff21')
            self.assertTrue(ex.reason is not None)
            self.assertTrue(len(ex.reason) > 0)

        self.assertTrue(caught)

    def test_gh590(self):
        self.assertEqual(unicode(bytes(range(0x80, 0x100)), 'ascii', 'replace'), u'\ufffd'*0x80)

    def test_escape(self):
        self.assertEqual(r"a\u", "a\\u")

        with self.assertRaises(UnicodeDecodeError):
            b"a\\u".decode("unicode-escape")

        with self.assertRaises(UnicodeDecodeError):
            b"a\\u".decode("raw-unicode-escape")

        self.assertEqual(b"\\a\\u1234".decode("unicode-escape"), "\x07\u1234")
        self.assertEqual(b"\\a\\u1234".decode("raw-unicode-escape"), "\\a\u1234")

run_test(__name__)
