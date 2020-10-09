# -*- coding: utf-8 -*-
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest
import warnings

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

long = type(sys.maxsize + 1)

class Indexable:
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class StrTest(IronPythonTestCase):

    def test_none(self):
        self.assertEqual("abc".translate({}), "abc")
        self.assertEqual("abc".translate({ord('h'): None}), "abc")
        self.assertEqual("abc".translate({ord('c'): None}), "ab")

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
        if is_cli:
            self.assertEqual('ä', str('ä'.Chars[0])) # StringOps.__new__(..., char)

        self.assertRegex(str(memoryview(b'abc')), r"^<memory at .+>$")
        self.assertEqual(str(memoryview(b'abc'), 'ascii'), 'abc')
        self.assertRaises(TypeError, str, memoryview(b'abc')[::2], 'ascii')

        import array
        self.assertEqual(str(array.array('B', b'abc')), "array('B', [97, 98, 99])")
        self.assertEqual(str(array.array('B', b'abc'), 'ascii'), 'abc')

    def test_add_mul(self):
        self.assertRaises(TypeError, lambda: "a" + 3)
        self.assertRaises(TypeError, lambda: 3 + "a")

        self.assertRaises(TypeError, lambda: "a" * "3")
        self.assertRaises(OverflowError, lambda: "a" * (sys.maxsize + 1))
        self.assertRaises(OverflowError, lambda: (sys.maxsize + 1) * "a")

        class mylong(long): pass

        if is_cli:
            from System.IO import Path
            self.assertEqual("foo" + os.sep, "foo" + Path.DirectorySeparatorChar)
            self.assertEqual(os.sep + os.sep, Path.DirectorySeparatorChar + os.sep)

        # multiply
        self.assertEqual("aaaa", "a" * long(4))
        self.assertEqual("aaaa", "a" * mylong(4))
        self.assertEqual("aaa", "a" * 3)
        self.assertEqual("a", "a" * True)
        self.assertEqual("", "a" * False)

        self.assertEqual("aaaa", long(4) * "a")
        self.assertEqual("aaaa", mylong(4) * "a")
        self.assertEqual("aaa", 3 * "a")
        self.assertEqual("a", True * "a")
        self.assertEqual("", False * "a" )

    def test_startswith(self):
        self.assertEqual("abcde".startswith('c', 2, 6), True)
        self.assertEqual("abc".startswith('c', 4, 6), False)
        self.assertEqual("abcde".startswith('cde', 2, 9), True)

        self.assertTrue('abc'.startswith('abc', -2<<222))
        self.assertFalse('abc'.startswith('abc', 2<<222))
        self.assertFalse('abc'.startswith('abc', None, -2<<222))
        self.assertTrue('abc'.startswith('abc', None, 2<<222))
        self.assertTrue('abc'.startswith('abc', None, None))

        self.assertTrue('abc'.startswith(('xyz', 'abc'), -2<<222))
        self.assertFalse('abc'.startswith(('xyz', 'abc'), 2<<222))
        self.assertFalse('abc'.startswith(('xyz', 'abc'), None, -2<<222))
        self.assertTrue('abc'.startswith(('xyz', 'abc'), None, 2<<222))
        self.assertTrue('abc'.startswith(('xyz', 'abc'), None, None))

        self.assertTrue('abc'.startswith(('xyz', 'abc'), Indexable(-2<<222)))
        self.assertFalse('abc'.startswith(('xyz', 'abc'), Indexable(2<<222)))
        self.assertFalse('abc'.startswith(('xyz', 'abc'), None, Indexable(-2<<222)))
        self.assertTrue('abc'.startswith(('xyz', 'abc'), None, (2<<222)))
        self.assertTrue('abc'.startswith(('xyz', 'b'), Indexable(1), Indexable(2)))

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

        self.assertTrue('abc'.endswith('abc', -2<<222))
        self.assertFalse('abc'.endswith('abc', 2<<222))
        self.assertFalse('abc'.endswith('abc', None, -2<<222))
        self.assertTrue('abc'.endswith('abc', None, 2<<222))
        self.assertTrue('abc'.endswith('abc', None, None))

        self.assertTrue('abc'.endswith(('xyz', 'abc'), -2<<222))
        self.assertFalse('abc'.endswith(('xyz', 'abc'), 2<<222))
        self.assertFalse('abc'.endswith(('xyz', 'abc'), None, -2<<222))
        self.assertTrue('abc'.endswith(('xyz', 'abc'), None, 2<<222))
        self.assertTrue('abc'.endswith(('xyz', 'abc'), None, None))

        self.assertTrue('abc'.endswith(('xyz', 'abc'), Indexable(-2<<222)))
        self.assertFalse('abc'.endswith(('xyz', 'abc'), Indexable(2<<222)))
        self.assertFalse('abc'.endswith(('xyz', 'abc'), None, Indexable(-2<<222)))
        self.assertTrue('abc'.endswith(('xyz', 'abc'), None, (2<<222)))
        self.assertTrue('abc'.endswith(('xyz', 'b'), Indexable(1), Indexable(2)))

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

    def test_unicode_escape(self):
        for i in range(0x7f):
            self.assertEqual(chr(i).encode('unicode-escape'), bytes(repr(chr(i))[1:-1], "ascii"))

    def test_encoding_backslashreplace(self):
                    # codec, input, output
        tests =   [ ('ascii',      u"a\xac\u1234\u20ac\u8000", b"a\\xac\\u1234\\u20ac\\u8000"),
                    ('latin-1',    u"a\xac\u1234\u20ac\u8000", b"a\xac\\u1234\\u20ac\\u8000"),
                    ('iso-8859-15', u"a\xac\u1234\u20ac\u8000", b"a\xac\\u1234\xa4\\u8000") ]

        for test in tests:
            self.assertEqual(test[1].encode(test[0], 'backslashreplace'), test[2])


    def test_encoding_xmlcharrefreplace(self):
                    # codec, input, output
        tests =   [ ('ascii',      u"a\xac\u1234\u20ac\u8000", b"a&#172;&#4660;&#8364;&#32768;"),
                    ('latin-1',    u"a\xac\u1234\u20ac\u8000", b"a\xac&#4660;&#8364;&#32768;"),
                    ('iso-8859-15', u"a\xac\u1234\u20ac\u8000", b"a\xac&#4660;\xa4&#32768;") ]
        for test in tests:
            self.assertEqual(test[1].encode(test[0], 'xmlcharrefreplace'), test[2])

    def test_encode_decode(self):
        self.assertEqual('abc'.encode(), b'abc')
        self.assertEqual(b'abc'.decode(), 'abc')

    def test_encode_decode_error(self):
        self.assertRaises(TypeError, 'abc'.encode, None)
        self.assertRaises(TypeError, b'abc'.decode, None)

    def test_string_escape_trailing_slash(self):
        ok = False
        try:
            b"\\".decode("unicode-escape")
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
        extra_str_dict_keys = [ "__radd__"]
        missing_str_dict_keys = ["casefold"]

        #It's OK that __getattribute__ does not show up in the __dict__.  It is
        #implemented.
        self.assertTrue(hasattr(str, "__getattribute__"), "str has no __getattribute__ method")
        self.assertTrue('__init__' not in str.__dict__.keys())

        for temp_key in extra_str_dict_keys:
            if is_cli:
                self.assertTrue(temp_key in str.__dict__.keys(), "str.__dict__ bug was fixed.  Please update test.")
            else:
                self.assertTrue(not temp_key in str.__dict__.keys())

        for temp_key in missing_str_dict_keys:
            if is_cli:
                self.assertTrue(not temp_key in str.__dict__.keys(), "str.__dict__ bug was fixed.  Please update test.")
            else:
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
                raise KeyError(key)
        self.assertEqual('%(abc)s %(bar)s' % (mydict()), '42 23')

        class mydict(dict):
            def __missing__(self, key):
                return 'ok'

        a = mydict()

        self.assertEqual('%(anykey)s' % a, 'ok')

    def test_str_to_numeric(self):
        class substring(str):
            def __int__(self): return 1
            def __long__(self): return long(1)
            def __complex__(self): return 1j
            def __float__(self): return 1.0

        v = substring("123")


        self.assertEqual(long(v), long(1))
        self.assertEqual(int(v), 1)
        self.assertEqual(complex(v), 123+0j)
        self.assertEqual(float(v), 1.0)

        class substring(str): pass

        v = substring("123")

        self.assertEqual(long(v), long(123))
        self.assertEqual(int(v), 123)
        self.assertEqual(complex(v), 123+0j)
        self.assertEqual(float(v), 123.0)

    def test_subclass_ctor(self):
        # verify all of the ctors work for various types...
        class myunicode(str): pass
        class myunicode2(str):
            def __init__(self, *args): pass

        class myfloat(float): pass
        class mylong(long): pass
        class myint(int): pass

        for x in [1, 1.0, long(1), 1j,
                myfloat(1.0), mylong(1), myint(1),
                True, False, None, object(),
                "", u""]:
            self.assertEqual(myunicode(x), str(x))
            self.assertEqual(myunicode2(x), str(x))
        self.assertEqual(myunicode(b'foo', 'ascii'), str(b'foo', 'ascii'))
        self.assertEqual(myunicode2(b'foo', 'ascii'), str(b'foo', 'ascii'))

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
        self.assertEqual(u"İ".lower(),u"i" if is_cli else u"i̇")

        # as defined in http://www.unicode.org/Public/UNIDATA/SpecialCasing.txt
        PERFECT_UNICODE_CASING = False

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
        self.assertEqual(u"abcd".translate({}), u"abcd")
        self.assertEqual(u"abcd".translate({ord('a') : ord('A'), ord('b') : None, ord('d') : u"XY"}) , "AcXY")
        self.assertRaisesMessage(TypeError, "character mapping must be in range(0x10000)", lambda: 'a'.translate({ord('a') : 65536}))
        self.assertRaisesMessage(TypeError, "character mapping must return integer, None or str", lambda: 'a'.translate({ord('a') : 2.0}))

run_test(__name__)
