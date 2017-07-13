# -*- coding: utf-8 -*-
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

import unittest

from iptest import is_mono, run_test

class UnicodeTest(unittest.TestCase):
    def test_constructor(self):
        self.assertEqual('', unicode())
        self.assertEqual('None', unicode(None))
        self.assertEqual('ä', unicode('ä'))
        self.assertEqual('ä', unicode(b'\xc3\xa4', 'utf-8')) # TODO kunom: reasonable?

    def test_raw_unicode_escape(self):
        for raw_unicode_escape in ['raw-unicode-escape', 'raw unicode escape']:
            s = unicode('\u0663\u0661\u0664 ', raw_unicode_escape)
            self.assertEqual(len(s), 4)
            self.assertEqual(int(s), 314)
            s = unicode('\u0663.\u0661\u0664 ',raw_unicode_escape)
            self.assertEqual(float(s), 3.14)

    def test_raw_unicode_escape_noescape_lowchars(self):
        for raw_unicode_escape in ['raw-unicode-escape', 'raw unicode escape']:
            for i in range(0x100):
                self.assertEqual(unichr(i).encode(raw_unicode_escape), chr(i))
        
            self.assertEqual(unichr(0x100).encode(raw_unicode_escape), r'\u0100')

    def test_raw_unicode_escape_dashes(self):
        """Make sure that either dashes or underscores work in raw encoding name"""
        ok = True
        try:
            unicode('hey', 'raw_unicode-escape')
        except LookupError:
            ok = False

        self.assertTrue(ok, "dashes and underscores should be interchangable")

    def test_raw_unicode_escape_trailing_backslash(self):
        self.assertEqual(unicode('\\', 'raw_unicode_escape'), u'\\')


    @unittest.skipIf(is_mono, 'throws an exception within Mono, needs debug see https://github.com/IronLanguages/main/issues/1617')
    def test_unicode_error(self):
        from iptest.misc_util import ip_supported_encodings
        from _codecs import register_error
        def handler(ex):
            self.assertEqual(ex.object, u'\uac00')
            return (u"", ex.end)
        register_error("test_unicode_error", handler)
                            
        for mode in ip_supported_encodings:  unichr(0xac00).encode(mode, "test_unicode_error")


    def test_ignore(self):
        """only UTF8, no encoding fallbacks..."""
        self.assertEqual(unicode('', 'ascii', 'ignore'), '')
        self.assertEqual(unicode('\xff', 'ascii', 'ignore'), '')
        self.assertEqual(unicode('a\xffb\xffc\xff', 'ascii', 'ignore'), 'abc')

    def test_cp19005(self):
        foo = u'\xef\xbb\xbf'
        self.assertEqual(repr(foo), r"u'\xef\xbb\xbf'")

    @unittest.skip('Conflicts with test_str.test_constructor. There is no reliable way to decide whether the client anticipates decoding or not.')
    def test_cp34689(self):
        xx_full_width_a = u'xx\uff21'
        caught = False
        try:
            dummy = str(xx_full_width_a)
        except UnicodeEncodeError as ex:
            caught = True
            self.assertEqual(ex.encoding, 'ascii')
            self.assertEqual(ex.start, 2)
            self.assertEqual(ex.end, 3)
            self.assertEqual(ex.object, u'\uff21')
            self.assertTrue(ex.reason != None)
            self.assertTrue(len(ex.reason) > 0)

        self.assertTrue(caught)

    def test_gh590(self):
        self.assertEqual(unicode(''.join(chr(i) for i in range(0x80, 0x100)), 'ascii', 'replace'), u'\ufffd'*0x80)


run_test(__name__)