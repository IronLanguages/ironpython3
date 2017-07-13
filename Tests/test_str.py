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

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, run_test, skipUnlessIronPython

class StrTest(IronPythonTestCase):
 
    def test_none(self):
        self.assertEqual("abc".translate(None), "abc")
        self.assertEqual("abc".translate(None, 'h'), "abc")
        self.assertEqual("abc".translate(None, 'c'), "ab")

        self.assertRaises(TypeError, "abc".replace, "new")
        self.assertRaises(TypeError, "abc".replace, "new", 2)

        for fn in ['find', 'index', 'rfind', 'count', 'startswith', 'endswith']:
            f = getattr("abc", fn)
            self.assertRaises(TypeError, f, None)
            self.assertRaises(TypeError, f, None, 0)
            self.assertRaises(TypeError, f, None, 0, 2)

        self.assertRaises(TypeError, 'abc'.replace, None, 'ef')
        self.assertRaises(TypeError, 'abc'.replace, None, 'ef', 1)
        
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'NoneType' and 'str'",
                            lambda: None + 'abc')
        self.assertRaises(TypeError, #"cannot concatenate 'str' and 'NoneType' objects", #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21947
                            lambda: 'abc' + None)
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'NoneType' and 'str'",
                            lambda: None + '')
        self.assertRaises(TypeError, #"cannot concatenate 'str' and 'NoneType' objects", #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21947
                            lambda: '' + None)

    def test_constructor(self):
        self.assertEqual('', str())
        self.assertEqual('None', str(None))
        
        # https://github.com/IronLanguages/main/issues/1108
        self.assertEqual('ä', str('ä')) # StringOps.__new__(..., string)
        self.assertEqual('ä', str('ä'.Chars[0])) # StringOps.__new__(..., char)
                           
    def test_add_mul(self):
        self.assertRaises(TypeError, lambda: "a" + 3)
        self.assertRaises(TypeError, lambda: 3 + "a")

        self.assertRaises(TypeError, lambda: "a" * "3")
        self.assertRaises(OverflowError, lambda: "a" * (sys.maxint + 1))
        self.assertRaises(OverflowError, lambda: (sys.maxint + 1) * "a")

        class mylong(long): pass
        
        if is_cli:
            from System.IO import Path
            self.assertEqual("foo" + os.sep, "foo" + Path.DirectorySeparatorChar)
            self.assertEqual(os.sep + os.sep, Path.DirectorySeparatorChar + os.sep)

        # multiply
        self.assertEqual("aaaa", "a" * 4L)
        self.assertEqual("aaaa", "a" * mylong(4L))
        self.assertEqual("aaa", "a" * 3)
        self.assertEqual("a", "a" * True)
        self.assertEqual("", "a" * False)

        self.assertEqual("aaaa", 4L * "a")
        self.assertEqual("aaaa", mylong(4L) * "a")
        self.assertEqual("aaa", 3 * "a")
        self.assertEqual("a", True * "a")
        self.assertEqual("", False * "a" )

    def test_startswith(self):
        self.assertEqual("abcde".startswith('c', 2, 6), True)
        self.assertEqual("abc".startswith('c', 4, 6), False)
        self.assertEqual("abcde".startswith('cde', 2, 9), True)
        
        hw = "hello world"
        self.assertTrue(hw.startswith("hello"))
        self.assertTrue(not hw.startswith("heloo"))
        self.assertTrue(hw.startswith("llo", 2))
        self.assertTrue(not hw.startswith("lno", 2))
        self.assertTrue(hw.startswith("wor", 6, 9))
        self.assertTrue(not hw.startswith("wor", 6, 7))
        self.assertTrue(not hw.startswith("wox", 6, 10))
        self.assertTrue(not hw.startswith("wor", 6, 2))


    def test_endswith(self):
        for x in (0, 1, 2, 3, -10, -3, -4):
            self.assertEqual("abcdef".endswith("def", x), True)
            self.assertEqual("abcdef".endswith("de", x, 5), True)
            self.assertEqual("abcdef".endswith("de", x, -1), True)

        for x in (4, 5, 6, 10, -1, -2):
            self.assertEqual("abcdef".endswith("def", x), False)
            self.assertEqual("abcdef".endswith("de", x, 5), False)
            self.assertEqual("abcdef".endswith("de", x, -1), False)

    def test_rfind(self):
        self.assertEqual("abcdbcda".rfind("cd", 1), 5)
        self.assertEqual("abcdbcda".rfind("cd", 3), 5)
        self.assertEqual("abcdbcda".rfind("cd", 7), -1)
        
        self.assertEqual('abc'.rfind('', 0, 0), 0)
        self.assertEqual('abc'.rfind('', 0, 1), 1)
        self.assertEqual('abc'.rfind('', 0, 2), 2)
        self.assertEqual('abc'.rfind('', 0, 3), 3)
        self.assertEqual('abc'.rfind('', 0, 4), 3)
        
        self.assertEqual('x'.rfind('x', 0, 0), -1)
        
        self.assertEqual('x'.rfind('x', 3, 0), -1)
        self.assertEqual('x'.rfind('', 3, 0), -1)
    

    def test_split(self):
        x="Hello Worllds"
        s = x.split("ll")
        self.assertTrue(s[0] == "He")
        self.assertTrue(s[1] == "o Wor")
        self.assertTrue(s[2] == "ds")

        self.assertTrue("1,2,3,4,5,6,7,8,9,0".split(",") == ['1','2','3','4','5','6','7','8','9','0'])
        self.assertTrue("1,2,3,4,5,6,7,8,9,0".split(",", -1) == ['1','2','3','4','5','6','7','8','9','0'])
        self.assertTrue("1,2,3,4,5,6,7,8,9,0".split(",", 2) == ['1','2','3,4,5,6,7,8,9,0'])
        self.assertTrue("1--2--3--4--5--6--7--8--9--0".split("--") == ['1','2','3','4','5','6','7','8','9','0'])
        self.assertTrue("1--2--3--4--5--6--7--8--9--0".split("--", -1) == ['1','2','3','4','5','6','7','8','9','0'])
        self.assertTrue("1--2--3--4--5--6--7--8--9--0".split("--", 2) == ['1', '2', '3--4--5--6--7--8--9--0'])

        self.assertEqual("".split(None), [])
        self.assertEqual("ab".split(None), ["ab"])
        self.assertEqual("a b".split(None), ["a", "b"])


    def test_rsplit(self):
        x="Hello Worllds"
        s = x.split("ll")
        self.assertTrue(s[0] == "He")
        self.assertTrue(s[1] == "o Wor")
        self.assertTrue(s[2] == "ds")

        self.assertTrue("1--2--3--4--5--6--7--8--9--0".rsplit("--", 2) == ['1--2--3--4--5--6--7--8', '9', '0'])

        for temp_string in ["", "  ", "   ", "\t", " \t", "\t ", "\t\t", "\n", "\n\n", "\n \n"]:
            self.assertEqual(temp_string.rsplit(None), [])
        
        self.assertEqual("ab".rsplit(None), ["ab"])
        self.assertEqual("a b".rsplit(None), ["a", "b"])


    def test_codecs(self):
        from iptest.misc_util import ip_supported_encodings
        encodings = [ x for x in ip_supported_encodings]
            
        for encoding in encodings: self.assertTrue('abc'.encode(encoding).decode(encoding)=='abc', encoding + " failed!")
    
    def test_count(self):
        self.assertTrue("adadad".count("d") == 3)
        self.assertTrue("adbaddads".count("ad") == 3)

    def test_expandtabs(self):
        self.assertTrue("\ttext\t".expandtabs(0) == "text")
        self.assertTrue("\ttext\t".expandtabs(-10) == "text")
        
        self.assertEqual(len("aaa\taaa\taaa".expandtabs()), 19)
        self.assertEqual("aaa\taaa\taaa".expandtabs(), "aaa     aaa     aaa")

    def test_empty_string(self):
        """zero length string"""
        self.assertEqual(''.title(), '')
        self.assertEqual(''.capitalize(), '')
        self.assertEqual(''.count('a'), 0)
        table = '10' * 128
        self.assertEqual(''.translate(table), '')
        self.assertEqual(''.replace('a', 'ef'), '')
        self.assertEqual(''.replace('bc', 'ef'), '')
        self.assertEqual(''.split(), [])
        self.assertEqual(''.split(' '), [''])
        self.assertEqual(''.split('a'), [''])

    def test_string_escape(self):
        for i in range(0x7f):
            if chr(i) == "'":
                self.assertEqual(chr(i).encode('string-escape'), "\\" + repr(chr(i))[1:-1])
            else:
                self.assertEqual(chr(i).encode('string-escape'), repr(chr(i))[1:-1])

    def test_encoding_backslashreplace(self):
                    # codec, input, output
        tests =   [ ('ascii',      u"a\xac\u1234\u20ac\u8000", "a\\xac\\u1234\\u20ac\\u8000"),
                    ('latin-1',    u"a\xac\u1234\u20ac\u8000", "a\xac\\u1234\\u20ac\\u8000"),
                    ('iso-8859-15', u"a\xac\u1234\u20ac\u8000", "a\xac\\u1234\xa4\\u8000") ]
        
        for test in tests:
            # undo this when mono bug https://bugzilla.xamarin.com/show_bug.cgi?id=53296 is fixed
            if is_mono and test[0] in ['latin-1', 'iso-8859-15']: continue
            self.assertEqual(test[1].encode(test[0], 'backslashreplace'), test[2])


    def test_encoding_xmlcharrefreplace(self):
                    # codec, input, output
        tests =   [ ('ascii',      u"a\xac\u1234\u20ac\u8000", "a&#172;&#4660;&#8364;&#32768;"),
                    ('latin-1',    u"a\xac\u1234\u20ac\u8000", "a\xac&#4660;&#8364;&#32768;"),
                    ('iso-8859-15', u"a\xac\u1234\u20ac\u8000", "a\xac&#4660;\xa4&#32768;") ]
        for test in tests:
            # undo this when mono bug https://bugzilla.xamarin.com/show_bug.cgi?id=53296 is fixed
            if is_mono and test[0] in ['latin-1', 'iso-8859-15']: continue
            self.assertEqual(test[1].encode(test[0], 'xmlcharrefreplace'), test[2])

    def test_encode_decode(self):
        self.assertEqual('abc'.encode(), 'abc')
        self.assertEqual('abc'.decode(), 'abc')

    def test_encode_decode_error(self):
        self.assertRaises(TypeError, 'abc'.encode, None)
        self.assertRaises(TypeError, 'abc'.decode, None)

    def test_string_escape_trailing_slash(self):
        ok = False
        try:
            "\\".decode("string-escape")
        except ValueError:
            ok = True
        self.assertTrue(ok, "string that ends in trailing slash should fail string decode")

    def test_str_subclass(self):
        import binascii
        class customstring(str):
            def __str__(self): return self.swapcase()
            def __repr__(self): return '<' + self + '>'
            def __hash__(self): return 42
            def __mul__(self, count): return 'multiplied'
            def __add__(self, other): return 23
            def __len__(self): return 2300
            def __contains__(self, value): return False
        
        o = customstring('abc')
        self.assertEqual(str(o), 'ABC')
        self.assertEqual(repr(o), '<abc>')
        self.assertEqual(hash(o), 42)
        self.assertEqual(o * 3, 'multiplied')
        self.assertEqual(o + 'abc', 23)
        self.assertEqual(len(o), 2300)
        self.assertEqual('a' in o, False)

    @skipUnlessIronPython()
    def test_str_char_hash(self):
        import System
        #System.Char.Parse('a') is not available in Silverlight mscorlib
        a = 'a'.ToCharArray()[0]
            
        for x in [{'a':'b'}, set(['a']), 'abc', ['a'], ('a',)]:
            self.assertEqual(a in x, True)

        self.assertEqual(hash(a), hash('a'))
        
        self.assertEqual('a' in a, True)

    def test_str_equals(self):
        x = 'abc' == 'abc'
        y = 'def' == 'def'
        self.assertEqual(id(x), id(y))
        self.assertEqual(id(x), id(True))
        
        x = 'abc' != 'abc'
        y = 'def' != 'def'
        self.assertEqual(id(x), id(y))
        self.assertEqual(id(x), id(False))
        
        x = 'abcx' == 'abc'
        y = 'defx' == 'def'
        self.assertEqual(id(x), id(y))
        self.assertEqual(id(x), id(False))
        
        x = 'abcx' != 'abc'
        y = 'defx' != 'def'
        self.assertEqual(id(x), id(y))
        self.assertEqual(id(x), id(True))

    def test_str_dict(self):
        extra_str_dict_keys = [ "__radd__", "isdecimal", "isnumeric", "isunicode"]
        missing_str_dict_keys = ["__rmod__"]
        
        #It's OK that __getattribute__ does not show up in the __dict__.  It is
        #implemented.
        self.assertTrue(hasattr(str, "__getattribute__"), "str has no __getattribute__ method")
        self.assertTrue('__init__' not in str.__dict__.keys())
        
        for temp_key in extra_str_dict_keys:
            if sys.platform=="win32":
                self.assertTrue(not temp_key in str.__dict__.keys())
            else:
                self.assertTrue(temp_key in str.__dict__.keys(), "str.__dict__ bug was fixed.  Please update test.")
            
        for temp_key in missing_str_dict_keys:
            self.assertTrue(temp_key in str.__dict__.keys())
            
        class x(str): pass
        
        self.assertEqual('abc'.__rmod__('-%s-'), '-abc-')
        self.assertEqual(x('abc').__rmod__('-%s-'), '-abc-')
        self.assertEqual(x('abc').__rmod__(2), NotImplemented)
    
    def test_formatting_userdict(self):
        """verify user mapping object works with string formatting"""
        class mydict(object):
            def __getitem__(self, key):
                if key == 'abc': return 42
                elif key == 'bar': return 23
                raise KeyError, key
        self.assertEqual('%(abc)s %(bar)s' % (mydict()), '42 23')
        
        class mydict(dict):
            def __missing__(self, key):
                return 'ok' 
        
        a = mydict()

        self.assertEqual('%(anykey)s' % a, 'ok')

    def test_str_to_numeric(self):
        class substring(str):
            def __int__(self): return 1
            def __long__(self): return 1L
            def __complex__(self): return 1j
            def __float__(self): return 1.0
        
        v = substring("123")
        
        
        self.assertEqual(long(v), 1L)
        self.assertEqual(int(v), 1)
        self.assertEqual(complex(v), 123+0j)
        self.assertEqual(float(v), 1.0)
        
        class substring(str): pass
        
        v = substring("123")
        
        self.assertEqual(long(v), 123L)
        self.assertEqual(int(v), 123)
        self.assertEqual(complex(v), 123+0j)
        self.assertEqual(float(v), 123.0)

    def test_subclass_ctor(self):
        # verify all of the ctors work for various types...
        class myunicode(unicode): pass
        class myunicode2(unicode): 
            def __init__(self, *args): pass
        
        class myfloat(float): pass
        class mylong(long): pass
        class myint(int): pass
        
        for x in [1, 1.0, 1L, 1j, 
                myfloat(1.0), mylong(1L), myint(1), 
                True, False, None, object(),
                "", u""]:
            self.assertEqual(myunicode(x), unicode(x))
            self.assertEqual(myunicode2(x), unicode(x))
        self.assertEqual(myunicode('foo', 'ascii'), unicode('foo', 'ascii'))
        self.assertEqual(myunicode2('foo', 'ascii'), unicode('foo', 'ascii'))

    def test_upper_lower(self):
        # CodePlex work item #33133
        self.assertEqual("a".upper(),"A")
        self.assertEqual("A".lower(),"a")
        self.assertEqual("A".upper(),"A")
        self.assertEqual("a".lower(),"a")

        self.assertEqual("z".upper(),"Z")
        self.assertEqual("Z".lower(),"z")
        self.assertEqual("Z".upper(),"Z")
        self.assertEqual("z".lower(),"z")

        self.assertEqual("-".lower(),"-")
        self.assertEqual("-".upper(),"-")

        # explicit unicode is required for cpython 2.7
        self.assertEqual(u"ä".upper(),u"Ä")
        self.assertEqual(u"Ä".lower(),u"ä")
        self.assertEqual(u"ö".upper(),u"Ö")
        self.assertEqual(u"Ö".lower(),u"ö")
        self.assertEqual(u"ü".upper(),u"Ü")
        self.assertEqual(u"U".lower(),u"u")

        self.assertEqual(u"ą".upper(),u"Ą")
        self.assertEqual(u"Ą".lower(),u"ą")

    def test_turkish_upper_lower(self):
        self.assertEqual(u"ı".upper(),u"I")
        self.assertEqual(u"İ".lower(),u"i")

        # as defined in http://www.unicode.org/Public/UNIDATA/SpecialCasing.txt
        PERFECT_UNICODE_CASING=False
    
        import locale
        lang,encoding = locale.getlocale()

        if is_cli:
            locale.setlocale(locale.LC_ALL, "tr_TR")
        else:
            locale.setlocale(locale.LC_ALL, "turkish")

        if PERFECT_UNICODE_CASING:
            self.assertEqual(u"I".lower(),u"ı")
            self.assertEqual(u"i".upper(),u"İ")
        else:
            # cpython compatibility
            self.assertEqual(u"I".lower(),u"i")
            self.assertEqual(u"i".upper(),u"I")

        locale.setlocale(locale.LC_ALL, (lang,encoding))

        # Note:
        # IronPython casing matches cpython implementation (linux and windows)
        # In order to take advantage of better build-in unicode support in Windows 
        # ToUpper/ToLower can be called directly
        if is_cli:
            import System.Globalization.CultureInfo as CultureInfo
            self.assertEqual(u"I".ToLower(CultureInfo("tr-TR")),u"ı")
            self.assertEqual(u"i".ToUpper(CultureInfo("tr-TR")),u"İ")

    def test_translate(self):
        self.assertEqual(u"abcd".translate(None), u"abcd")
        self.assertEqual(u"abcd".translate({ord('a') : ord('A'), ord('b') : None, ord('d') : u"XY"}) , "AcXY")
        self.assertRaisesMessage(TypeError, "character mapping must be in range(0x%lx)", lambda: 'a'.translate({ord('a') : 65536}))
        self.assertRaisesMessage(TypeError, "character mapping must return integer, None or unicode", lambda: 'a'.translate({ord('a') : 2.0}))

run_test(__name__)
