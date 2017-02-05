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

##
## Test builtin-method of str
##

from iptest.assert_util import *
from iptest.misc_util import ip_supported_encodings
import sys
 
def test_none():
    AreEqual("abc".translate(None), "abc")
    AreEqual("abc".translate(None, 'h'), "abc")
    AreEqual("abc".translate(None, 'c'), "ab")

    AssertError(TypeError, "abc".replace, "new")
    AssertError(TypeError, "abc".replace, "new", 2)

    for fn in ['find', 'index', 'rfind', 'count', 'startswith', 'endswith']:
        f = getattr("abc", fn)
        AssertError(TypeError, f, None)
        AssertError(TypeError, f, None, 0)
        AssertError(TypeError, f, None, 0, 2)

    AssertError(TypeError, 'abc'.replace, None, 'ef')
    AssertError(TypeError, 'abc'.replace, None, 'ef', 1)
    
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for +: 'NoneType' and 'str'",
                           lambda: None + 'abc')
    AssertError(TypeError, #"cannot concatenate 'str' and 'NoneType' objects", #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21947
                           lambda: 'abc' + None)
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for +: 'NoneType' and 'str'",
                           lambda: None + '')
    AssertError(TypeError, #"cannot concatenate 'str' and 'NoneType' objects", #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21947
                           lambda: '' + None)

def test_add_mul():
    AssertError(TypeError, lambda: "a" + 3)
    AssertError(TypeError, lambda: 3 + "a")

    import sys
    AssertError(TypeError, lambda: "a" * "3")
    AssertError(OverflowError, lambda: "a" * (sys.maxsize + 1))
    AssertError(OverflowError, lambda: (sys.maxsize + 1) * "a")

    class mylong(long): pass
    
    if is_cli:
        from System.IO import Path
        AreEqual("foo\\", "foo" + Path.DirectorySeparatorChar)
        AreEqual("\\\\", Path.DirectorySeparatorChar + '\\')

    # multiply
    AreEqual("aaaa", "a" * 4)
    AreEqual("aaaa", "a" * mylong(4))
    AreEqual("aaa", "a" * 3)
    AreEqual("a", "a" * True)
    AreEqual("", "a" * False)

    AreEqual("aaaa", 4 * "a")
    AreEqual("aaaa", mylong(4) * "a")
    AreEqual("aaa", 3 * "a")
    AreEqual("a", True * "a")
    AreEqual("", False * "a" )

def test_startswith():
    AreEqual("abcde".startswith('c', 2, 6), True)
    AreEqual("abc".startswith('c', 4, 6), False)
    AreEqual("abcde".startswith('cde', 2, 9), True)
    
    hw = "hello world"
    Assert(hw.startswith("hello"))
    Assert(not hw.startswith("heloo"))
    Assert(hw.startswith("llo", 2))
    Assert(not hw.startswith("lno", 2))
    Assert(hw.startswith("wor", 6, 9))
    Assert(not hw.startswith("wor", 6, 7))
    Assert(not hw.startswith("wox", 6, 10))
    Assert(not hw.startswith("wor", 6, 2))


def test_endswith():
    for x in (0, 1, 2, 3, -10, -3, -4):
        AreEqual("abcdef".endswith("def", x), True)
        AreEqual("abcdef".endswith("de", x, 5), True)
        AreEqual("abcdef".endswith("de", x, -1), True)

    for x in (4, 5, 6, 10, -1, -2):
        AreEqual("abcdef".endswith("def", x), False)
        AreEqual("abcdef".endswith("de", x, 5), False)
        AreEqual("abcdef".endswith("de", x, -1), False)

@skip("silverlight") # CoreCLR bug xxxx found in build 30324 from silverlight_w2
def test_rfind():
    AreEqual("abcdbcda".rfind("cd", 1), 5)
    AreEqual("abcdbcda".rfind("cd", 3), 5)
    AreEqual("abcdbcda".rfind("cd", 7), -1)
    
    AreEqual('abc'.rfind('', 0, 0), 0)
    AreEqual('abc'.rfind('', 0, 1), 1)
    AreEqual('abc'.rfind('', 0, 2), 2)
    AreEqual('abc'.rfind('', 0, 3), 3)
    AreEqual('abc'.rfind('', 0, 4), 3)
    
    AreEqual('x'.rfind('x', 0, 0), -1)
    
    AreEqual('x'.rfind('x', 3, 0), -1)
    AreEqual('x'.rfind('', 3, 0), -1)
    

def test_split():
    x="Hello Worllds"
    s = x.split("ll")
    Assert(s[0] == "He")
    Assert(s[1] == "o Wor")
    Assert(s[2] == "ds")

    Assert("1,2,3,4,5,6,7,8,9,0".split(",") == ['1','2','3','4','5','6','7','8','9','0'])
    Assert("1,2,3,4,5,6,7,8,9,0".split(",", -1) == ['1','2','3','4','5','6','7','8','9','0'])
    Assert("1,2,3,4,5,6,7,8,9,0".split(",", 2) == ['1','2','3,4,5,6,7,8,9,0'])
    Assert("1--2--3--4--5--6--7--8--9--0".split("--") == ['1','2','3','4','5','6','7','8','9','0'])
    Assert("1--2--3--4--5--6--7--8--9--0".split("--", -1) == ['1','2','3','4','5','6','7','8','9','0'])
    Assert("1--2--3--4--5--6--7--8--9--0".split("--", 2) == ['1', '2', '3--4--5--6--7--8--9--0'])

    AreEqual("".split(None), [])
    AreEqual("ab".split(None), ["ab"])
    AreEqual("a b".split(None), ["a", "b"])


def test_rsplit():
    x="Hello Worllds"
    s = x.split("ll")
    Assert(s[0] == "He")
    Assert(s[1] == "o Wor")
    Assert(s[2] == "ds")

    Assert("1--2--3--4--5--6--7--8--9--0".rsplit("--", 2) == ['1--2--3--4--5--6--7--8', '9', '0'])

    for temp_string in ["", "  ", "   ", "\t", " \t", "\t ", "\t\t", "\n", "\n\n", "\n \n"]:
        AreEqual(temp_string.rsplit(None), [])
    
    AreEqual("ab".rsplit(None), ["ab"])
    AreEqual("a b".rsplit(None), ["a", "b"])


def test_codecs():
    if is_silverlight:
        encodings = [ 'ascii', 'utf-8', 'utf-16-le', 'raw-unicode-escape']
    else:
        encodings = [ x for x in ip_supported_encodings]
        
    for encoding in encodings: Assert('abc'.encode(encoding).decode(encoding)=='abc', encoding + " failed!")
    
def test_count():
    Assert("adadad".count("d") == 3)
    Assert("adbaddads".count("ad") == 3)

def test_expandtabs():
    Assert("\ttext\t".expandtabs(0) == "text")
    Assert("\ttext\t".expandtabs(-10) == "text")
    
    AreEqual(len("aaa\taaa\taaa".expandtabs()), 19)
    AreEqual("aaa\taaa\taaa".expandtabs(), "aaa     aaa     aaa")

# zero-length string
def test_empty_string():
    AreEqual(''.title(), '')
    AreEqual(''.capitalize(), '')
    AreEqual(''.count('a'), 0)
    table = '10' * 128
    AreEqual(''.translate(table), '')
    AreEqual(''.replace('a', 'ef'), '')
    AreEqual(''.replace('bc', 'ef'), '')
    AreEqual(''.split(), [])
    AreEqual(''.split(' '), [''])
    AreEqual(''.split('a'), [''])

def test_string_escape():
    for i in range(0x7f):
        if chr(i) == "'":
            AreEqual(chr(i).encode('string-escape'), "\\" + repr(chr(i))[1:-1])
        else:
            AreEqual(chr(i).encode('string-escape'), repr(chr(i))[1:-1])

@skip("silverlight") # not implemented exception on Silverlight
def test_encoding_backslashreplace():
                # codec, input, output
    tests =   [ ('ascii',      "a\xac\u1234\u20ac\u8000", "a\\xac\\u1234\\u20ac\\u8000"),
                ('latin-1',    "a\xac\u1234\u20ac\u8000", "a\xac\\u1234\\u20ac\\u8000"),
                ('iso-8859-15', "a\xac\u1234\u20ac\u8000", "a\xac\\u1234\xa4\\u8000") ]
    
    for test in tests:
        AreEqual(test[1].encode(test[0], 'backslashreplace'), test[2])

@skip("silverlight") # not implemented exception on Silverlight
def test_encoding_xmlcharrefreplace():
                # codec, input, output
    tests =   [ ('ascii',      "a\xac\u1234\u20ac\u8000", "a&#172;&#4660;&#8364;&#32768;"),
                ('latin-1',    "a\xac\u1234\u20ac\u8000", "a\xac&#4660;&#8364;&#32768;"),
                ('iso-8859-15', "a\xac\u1234\u20ac\u8000", "a\xac&#4660;\xa4&#32768;") ]
    for test in tests:
        AreEqual(test[1].encode(test[0], 'xmlcharrefreplace'), test[2])

def test_encode_decode():
    AreEqual('abc'.encode(), 'abc')
    AreEqual('abc'.decode(), 'abc')

def test_encode_decode():
    AssertError(TypeError, 'abc'.encode, None)
    AssertError(TypeError, 'abc'.decode, None)
      
    
def test_string_escape_trailing_slash():
    ok = False
    try:
        "\\".decode("string-escape")
    except ValueError:
        ok = True
    Assert(ok, "string that ends in trailing slash should fail string decode")

def test_str_subclass():
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
    AreEqual(str(o), 'ABC')
    AreEqual(repr(o), '<abc>')
    AreEqual(hash(o), 42)
    AreEqual(o * 3, 'multiplied')
    AreEqual(o + 'abc', 23)
    AreEqual(len(o), 2300)
    AreEqual('a' in o, False)

@skip('win32')
def test_str_char_hash():
    import System
    #System.Char.Parse('a') is not available in Silverlight mscorlib
    if is_silverlight:
        a = 'a'.ToCharArray()[0]
    else:
        a = System.Char.Parse('a')
        
    for x in [{'a':'b'}, set(['a']), 'abc', ['a'], ('a',)]:
        AreEqual(a in x, True)

    AreEqual(hash(a), hash('a'))
    
    AreEqual('a' in a, True)

def test_str_equals():
    x = 'abc' == 'abc'
    y = 'def' == 'def'
    AreEqual(id(x), id(y))
    AreEqual(id(x), id(True))
    
    x = 'abc' != 'abc'
    y = 'def' != 'def'
    AreEqual(id(x), id(y))
    AreEqual(id(x), id(False))
    
    x = 'abcx' == 'abc'
    y = 'defx' == 'def'
    AreEqual(id(x), id(y))
    AreEqual(id(x), id(False))
    
    x = 'abcx' != 'abc'
    y = 'defx' != 'def'
    AreEqual(id(x), id(y))
    AreEqual(id(x), id(True))

def test_str_dict():
    print("CodePlex Work Item 13115")
    extra_str_dict_keys = [ "__radd__", "isdecimal", "isnumeric", "isunicode"]
    missing_str_dict_keys = ["__rmod__"]
    
    #It's OK that __getattribute__ does not show up in the __dict__.  It is
    #implemented.
    Assert(hasattr(str, "__getattribute__"), "str has no __getattribute__ method")
    Assert('__init__' not in list(str.__dict__.keys()))
    
    for temp_key in extra_str_dict_keys:
        if sys.platform=="win32":
            Assert(not temp_key in list(str.__dict__.keys()))
        else:
            Assert(temp_key in list(str.__dict__.keys()), "str.__dict__ bug was fixed.  Please update test.")
        
    for temp_key in missing_str_dict_keys:
        Assert(temp_key in list(str.__dict__.keys()))
        
    class x(str): pass
    
    AreEqual('abc'.__rmod__('-%s-'), '-abc-')
    AreEqual(x('abc').__rmod__('-%s-'), '-abc-')
    AreEqual(x('abc').__rmod__(2), NotImplemented)
    
def test_formatting_userdict():
    """verify user mapping object works with string formatting"""
    class mydict(object):
        def __getitem__(self, key):
            if key == 'abc': return 42
            elif key == 'bar': return 23
            raise KeyError(key)
    AreEqual('%(abc)s %(bar)s' % (mydict()), '42 23')
    
    class mydict(dict):
        def __missing__(self, key):
            return 'ok' 
    
    a = mydict()

    AreEqual('%(anykey)s' % a, 'ok')

def test_str_to_numeric():
    class substring(str):
        def __int__(self): return 1
        def __long__(self): return 1
        def __complex__(self): return 1j
        def __float__(self): return 1.0
    
    v = substring("123")
    
    
    AreEqual(int(v), 1)
    AreEqual(int(v), 1)
    AreEqual(complex(v), 123+0j)
    if is_cpython and sys.version_info[:3] <= (2,6,2):
        AreEqual(float(v), 123.0)
    else:
        AreEqual(float(v), 1.0)
    
    class substring(str): pass
    
    v = substring("123")
    
    AreEqual(int(v), 123)
    AreEqual(int(v), 123)
    AreEqual(complex(v), 123+0j)
    AreEqual(float(v), 123.0)

def test_subclass_ctor():
    # verify all of the ctors work for various types...
    class myunicode(str): pass
    class myunicode2(str): 
        def __init__(self, *args): pass
    
    class myfloat(float): pass
    class mylong(long): pass
    class myint(int): pass
    
    for x in [1, 1.0, 1, 1j, 
              myfloat(1.0), mylong(1), myint(1), 
              True, False, None, object(),
              "", ""]:
        AreEqual(myunicode(x), str(x))
        AreEqual(myunicode2(x), str(x))
    AreEqual(myunicode('foo', 'ascii'), str('foo', 'ascii'))
    AreEqual(myunicode2('foo', 'ascii'), str('foo', 'ascii'))

def test_upper_lower():
    # CodePlex work item #33133
    AreEqual("a".upper(),"A")
    AreEqual("A".lower(),"a")
    AreEqual("A".upper(),"A")
    AreEqual("a".lower(),"a")

    AreEqual("z".upper(),"Z")
    AreEqual("Z".lower(),"z")
    AreEqual("Z".upper(),"Z")
    AreEqual("z".lower(),"z")

    AreEqual("-".lower(),"-")
    AreEqual("-".upper(),"-")

    # explicit unicode is required for cpython 2.7
    AreEqual("ä".upper(),"Ä")
    AreEqual("Ä".lower(),"ä")
    AreEqual("ö".upper(),"Ö")
    AreEqual("Ö".lower(),"ö")
    AreEqual("ü".upper(),"Ü")
    AreEqual("U".lower(),"u")

    AreEqual("ą".upper(),"Ą")
    AreEqual("Ą".lower(),"ą")

def test_turkish_upper_lower():
    AreEqual("ı".upper(),"I")
    AreEqual("İ".lower(),"i")

    # as defined in http://www.unicode.org/Public/UNIDATA/SpecialCasing.txt
    PERFECT_UNICODE_CASING=False
   
    import locale
    lang,encoding = locale.getlocale()

    if sys.platform == "win32":
        locale.setlocale(locale.LC_ALL, "turkish")
    else:
        locale.setlocale(locale.LC_ALL,"tr_TR")

    if PERFECT_UNICODE_CASING:
        AreEqual("I".lower(),"ı")
        AreEqual("i".upper(),"İ")
    else:
        # cpython compatibility
        AreEqual("I".lower(),"i")
        AreEqual("i".upper(),"I")

    locale.setlocale(locale.LC_ALL, (lang,encoding))

    # Note:
    # IronPython casing matches cpython implementation (linux and windows)
    # In order to take advantage of better build-in unicode support in Windows 
    # ToUpper/ToLower can be called directly
    if sys.platform == "cli":
        import System.Globalization.CultureInfo as CultureInfo
        AreEqual("I".ToLower(CultureInfo("tr-TR")),"ı")
        AreEqual("i".ToUpper(CultureInfo("tr-TR")),"İ")




run_test(__name__)
