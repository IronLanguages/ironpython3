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

from iptest.assert_util import *

import re

def test_none():
    for x in 'compile search match split findall finditer'.split():
        y = getattr(re, x)
        AssertError(TypeError, y, None)
        AssertError(TypeError, y, None, None)
        AssertError(TypeError, y, None, 'abc')
        AssertError(TypeError, y, 'abc', None)

    # Other exceptional input tests
    for x in (re.sub, re.subn):
        AssertError(TypeError, x, 'abc', None, 'abc')
        AssertError(TypeError, x, 'abc', None, None)
        AssertError(TypeError, x, None, 'abc', 'abc')
        AssertError(TypeError, x, 'abc', 'abc', None)

    AssertError(TypeError, re.escape, None)
    
    
def test_sanity_re():
    '''
    Basic sanity tests for the re module.  Each module member is
    used at least once.
    '''
    #compile
    Assert(hasattr(re.compile("(abc){1}"), "pattern"))
    Assert(hasattr(re.compile("(abc){1}", re.L), "pattern"))
    Assert(hasattr(re.compile("(abc){1}", flags=re.L), "pattern"))
    
    #I IGNORECASE L LOCAL MMULTILINE S DOTALL U UNICODE X VERBOSE
    flags = ["I", "IGNORECASE",
                 "L", "LOCALE",
                 "M", "MULTILINE",
                 "S", "DOTALL",
                 "U", "UNICODE",
                 "X", "VERBOSE"]
    
    for f in flags:
        Assert(hasattr(re, f))
    
    #search
    AreEqual(re.search("(abc){1}", ""), None)
    AreEqual(re.search("(abc){1}", "abcxyz").span(), (0,3))
    AreEqual(re.search("(abc){1}", "abcxyz", re.L).span(), (0,3))
    AreEqual(re.search("(abc){1}", "abcxyz", flags=re.L).span(), (0,3))
    AreEqual(re.search("(abc){1}", "xyzabc").span(), (3,6))
    
    AreEqual(re.search("(abc){1}", buffer("")), None)
    AreEqual(re.search("(abc){1}", buffer("abcxyz")).span(), (0,3))
    AreEqual(re.search("(abc){1}", buffer("abcxyz"), re.L).span(), (0,3))
    AreEqual(re.search("(abc){1}", buffer("abcxyz"), flags=re.L).span(), (0,3))
    AreEqual(re.search("(abc){1}", buffer("xyzabc")).span(), (3,6))
    
    #match
    AreEqual(re.match("(abc){1}", ""), None)
    AreEqual(re.match("(abc){1}", "abcxyz").span(), (0,3))
    AreEqual(re.match("(abc){1}", "abcxyz", re.L).span(), (0,3))
    AreEqual(re.match("(abc){1}", "abcxyz", flags=re.L).span(), (0,3))
    
    #split
    AreEqual(re.split("(abc){1}", ""), [''])
    AreEqual(re.split("(abc){1}", "abcxyz"), ['', 'abc', 'xyz'])
    #maxsplit
    AreEqual(re.split("(abc){1}", "abc", 0), ['', 'abc', ''])
    for i in range(3):
        AreEqual(re.split("(abc){1}", "abc", maxsplit=i), ['', 'abc', ''])
        AreEqual(re.split("(abc){1}", "", maxsplit=i), [''])
        AreEqual(re.split("(abc){1}", "abcxyz", maxsplit=i), ['', 'abc', 'xyz'])
    AreEqual(re.split("(abc){1}", "abcxyzabc", maxsplit=0), ['', 'abc', 'xyz', 'abc', ''])
    AreEqual(re.split("(abc){1}", "abcxyzabc", maxsplit=1), ['', 'abc', 'xyzabc'])
    AreEqual(re.split("(abc){1}", "abcxyzabc", maxsplit=2), ['', 'abc', 'xyz', 'abc', ''])
    
    #findall
    AreEqual(re.findall("(abc){1}", ""), [])
    AreEqual(re.findall("(abc){1}", "abcxyz"), ['abc'])
    AreEqual(re.findall("(abc){1}", "abcxyz", re.L), ['abc'])
    AreEqual(re.findall("(abc){1}", "abcxyz", flags=re.L), ['abc'])
    AreEqual(re.findall("(abc){1}", "xyzabcabc"), ['abc', 'abc'])
    
    #finditer
    AreEqual([x.group() for x in re.finditer("(abc){1}", "")], [])
    AreEqual([x.group() for x in re.finditer("(abc){1}", "abcxyz")], ['abc'])
    AreEqual([x.group() for x in re.finditer("(abc){1}", "abcxyz", re.L)], ['abc'])
    AreEqual([x.group() for x in re.finditer("(abc){1}", "abcxyz", flags=re.L)], ['abc'])
    AreEqual([x.group() for x in re.finditer("(abc){1}", "xyzabcabc")], ['abc', 'abc'])
    rex = re.compile("foo")
    for m in rex.finditer("this is a foo and a foo bar"):
        AreEqual((m.pos, m.endpos), (0, 27))
    for m in rex.finditer(""):
        AreEqual((m.pos, m.endpos), (0, 1))
    for m in rex.finditer("abc"):
        AreEqual((m.pos, m.endpos), (0, 4))
    for m in rex.finditer("foo foo foo foo foo"):
        AreEqual((m.pos, m.endpos), (0, 19))
    
    #sub
    AreEqual(re.sub("(abc){1}", "9", "abcd"), "9d")
    AreEqual(re.sub("(abc){1}", "abcxyz",'abcd'), "abcxyzd")
    AreEqual(re.sub("(abc){1}", "1", "abcd", 0), "1d")
    AreEqual(re.sub("(abc){1}", "1", "abcd", count=0), "1d")
    AreEqual(re.sub("(abc){1}", "1", "abcdabcd", 1), "1dabcd")
    AreEqual(re.sub("(abc){1}", "1", "abcdabcd", 2), "1d1d")
    AreEqual(re.sub("(abc){1}", "1", "ABCdabcd", 2, flags=re.I), "1d1d")
    
    #subn
    AreEqual(re.subn("(abc){1}", "9", "abcd"), ("9d", 1))
    AreEqual(re.subn("(abc){1}", "abcxyz",'abcd'), ("abcxyzd",1))
    AreEqual(re.subn("(abc){1}", "1", "abcd", 0), ("1d",1))
    AreEqual(re.subn("(abc){1}", "1", "abcd", count=0), ("1d",1))
    AreEqual(re.subn("(abc){1}", "1", "abcdabcd", 1), ("1dabcd",1))
    AreEqual(re.subn("(abc){1}", "1", "abcdabcd", 2), ("1d1d",2))
    AreEqual(re.subn("(abc){1}", "1", "ABCdabcd", 2, flags=re.I), ("1d1d",2))
    
    #escape
    AreEqual(re.escape("abc"), "abc")
    AreEqual(re.escape(""), "")
    AreEqual(re.escape("_"), "\\_")
    AreEqual(re.escape("a_c"), "a\\_c")
    
    #error
    exc = re.error()
    exc = re.error("some args")
    
    #purge
    re.purge()
    
    
def test_sanity_re_pattern():
    '''
    Basic sanity tests for the re module's Regular Expression
    objects (i.e., Pattern in CPython).  Each method/member is
    utilized at least once.
    '''
    pattern = re.compile("(abc){1}")
    
    #match
    AreEqual(pattern.match(""), None)
    AreEqual(pattern.match("abcxyz").span(), (0,3))
    AreEqual(pattern.match("abc", 0).span(), (0,3))
    AreEqual(pattern.match("abc", 0, 3).span(), (0,3))
    AreEqual(pattern.match("abc", pos=0, endpos=3).span(), (0,3))
    for i in [-1, -2, -5, -7, -8, -65536]:
        for j in [3, 4, 5, 7, 8, 65536]:
            AreEqual(pattern.match("abc", i, j).span(), (0,3))
    AssertError(OverflowError, lambda: pattern.match("abc", 0, 2**32).span())
    AssertError(OverflowError, lambda: pattern.match("abc", -(2**32), 3).span())
    
    #search
    AreEqual(pattern.search(""), None)
    AreEqual(pattern.search("abcxyz").span(), (0,3))
    AreEqual(pattern.search("abc", 0).span(), (0,3))
    AreEqual(pattern.search("abc", 0, 3).span(), (0,3))
    AreEqual(pattern.search("abc", pos=0, endpos=3).span(), (0,3))
    AreEqual(pattern.search("xyzabc").span(), (3,6))
    
    #split
    AreEqual(pattern.split(""), [''])
    AreEqual(pattern.split("abcxyz"), ['', 'abc', 'xyz'])
    AreEqual(pattern.split("abc", 0), ['', 'abc', ''])
    AreEqual(pattern.split("abc", maxsplit=0), ['', 'abc', ''])
    AreEqual(pattern.split("abcxyzabc", maxsplit=1), ['', 'abc', 'xyzabc'])
    
    #findall
    AreEqual(pattern.findall(""), [])
    AreEqual(pattern.findall("abcxyz"), ['abc'])
    AreEqual(pattern.findall("abc", 0), ['abc'])
    AreEqual(pattern.findall("abc", 0, 3), ['abc'])
    AreEqual(pattern.findall("abc", pos=0, endpos=3), ['abc'])
    AreEqual(pattern.findall("xyzabcabc"), ['abc', 'abc'])
    
    #sub
    AreEqual(pattern.sub("9", "abcd"), "9d")
    AreEqual(pattern.sub("abcxyz",'abcd'), "abcxyzd")
    AreEqual(pattern.sub("1", "abcd", 0), "1d")
    AreEqual(pattern.sub("1", "abcd", count=0), "1d")
    AreEqual(pattern.sub("1", "abcdabcd", 1), "1dabcd")
    AreEqual(pattern.sub("1", "abcdabcd", 2), "1d1d")
    
    #subn
    AreEqual(pattern.subn("9", "abcd"), ("9d", 1))
    AreEqual(pattern.subn("abcxyz",'abcd'), ("abcxyzd",1))
    AreEqual(pattern.subn("1", "abcd", 0), ("1d",1))
    AreEqual(pattern.subn("1", "abcd", count=0), ("1d",1))
    AreEqual(pattern.subn("1", "abcdabcd", 1), ("1dabcd",1))
    AreEqual(pattern.subn("1", "abcdabcd", 2), ("1d1d",2))
    
    #flags
    AreEqual(pattern.flags, 0)
    AreEqual(re.compile("(abc){1}", re.L).flags, re.L)
    
    #groupindex
    AreEqual(pattern.groupindex, {})
    AreEqual(re.compile("(?P<abc>)(?P<bcd>)").groupindex, {'bcd': 2, 'abc': 1})
    
    #pattern
    AreEqual(pattern.pattern, "(abc){1}")
    AreEqual(re.compile("").pattern, "")
    
def test_groupindex_empty():
    test_list = [   ".", "^", "$", "1*", "2+", "3?", "4*?", "5+?", "6??", "7{1}", "8{1,2}",
                    "9{1,2}?", "[a-z]", "|", "(...)", "(?:abc)",
                    "\(\?P\<Blah\>abc\)", "(?#...)", "(?=...)", "(?!...)", "(?<=...)",
                    "(?<!...)", "\1", "\A", "\d"
                    ]
    for x in test_list:
        AreEqual(re.compile(x).groupindex, {})

    
def test_sanity_re_match():
    '''
    Basic sanity tests for the re module's Match objects.  Each method/member
    is utilized at least once.
    '''
    pattern = re.compile("(abc){1}")
    match_obj = pattern.match("abcxyzabc123 and some other words...")
    
    #expand
    AreEqual(match_obj.expand("\1\g<1>.nt"), '\x01abc.nt')
    
    #group
    AreEqual(match_obj.group(), 'abc')
    AreEqual(match_obj.group(1), 'abc')
    
    #groups
    AreEqual(match_obj.groups(), ('abc',))
    AreEqual(match_obj.groups(1), ('abc',))
    AreEqual(match_obj.groups(99), ('abc',))
    
    #groupdict
    AreEqual(match_obj.groupdict(), {})
    AreEqual(match_obj.groupdict(None), {})
    AreEqual(re.compile("(abc)").match("abcxyzabc123 and...").groupdict(), {})
    
    #start
    AreEqual(match_obj.start(), 0)
    AreEqual(match_obj.start(1), 0)
    
    #end
    AreEqual(match_obj.end(), 3)
    AreEqual(match_obj.end(1), 3)
    
    #span
    AreEqual(match_obj.span(), (0,3))
    AreEqual(match_obj.span(1), (0,3))
    
    #pos
    AreEqual(match_obj.pos, 0)
    
    #endpos
    AreEqual(match_obj.endpos, 36)
    
    #lastindex
    AreEqual(match_obj.lastindex, 1)
    
    #lastgroup
    #CodePlex Work Item 5518
    #AreEqual(match_obj.lastgroup, None)
    
    #re
    Assert(match_obj.re==pattern)
    
    #string
    AreEqual(match_obj.string, "abcxyzabc123 and some other words...")
    
    
def test_comment():
    '''
    (?#...)
    '''
    pattern = "a(?#foo)bc"
    c = re.compile(pattern)
    AreEqual(c.findall("abc"), ['abc'])
    
    pattern = "a(?#)bc"
    c = re.compile(pattern)
    AreEqual(c.findall("abc"), ['abc'])
    
    pattern = "a(?#foo)bdc"
    c = re.compile(pattern)
    AreEqual(len(c.findall("abc")), 0)

def test_optional_paren():
    pattern = r"""\(?\w+\)?"""
    c = re.compile(pattern, re.X)
    AreEqual(c.findall('abc'), ['abc'])

def test_back_match():
    p = re.compile('(?P<grp>.+?)(?P=grp)')
    AreEqual(p.match('abcabc').groupdict(), {'grp':'abc'})
    p = re.compile(r'(?P<delim>[%$])(?P<escaped>(?P=delim))')
    AreEqual(p.match('$$').groupdict(), {'escaped': '$', 'delim': '$'})
    AreEqual(p.match('$%'), None)
    p = re.compile(r'(?P<grp>ab)(a(?P=grp)b)')
    AreEqual(p.match('abaabb').groups(), ('ab', 'aabb'))
    
def test_expand():
    AreEqual(re.match("(a)(b)", "ab").expand("blah\g<1>\g<2>"), "blahab")
    AreEqual(re.match("(a)()", "ab").expand("blah\g<1>\g<2>\n\r\t\\\\"),'blaha\n\r\t\\')
    AreEqual(re.match("(a)()", "ab").expand(""),'')

def test_sub():
    x = '\n   #region Generated Foo\nblah\nblah#end region'
    a = re.compile("^([ \t]+)#region Generated Foo.*?#end region", re.MULTILINE|re.DOTALL)
    
    AreEqual(a.sub("xx", x), "\nxx")            # should match successfully
    AreEqual(a.sub("\\x12", x), "\n\\x12")      # should match, but shouldn't un-escape for \x

    #if optional count arg is 0 then all occurrences should be replaced
    AreEqual('bbbb', re.sub("a","b","abab", 0))

    AreEqual(re.sub(r'(?P<id>b)', '\g<id>\g<id>yadayada', 'bb'), 'bbyadayadabbyadayada')
    AreEqual(re.sub(r'(?P<id>b)', '\g<1>\g<id>yadayada', 'bb'), 'bbyadayadabbyadayada')
    AssertError(IndexError, re.sub, r'(?P<id>b)', '\g<1>\g<i2>yadayada', 'bb')
    # the native implementation just gives a sre_constants.error instead indicating an invalid
    # group reference
    if is_cli:
        AssertError(IndexError, re.sub, r'(?P<id>b)', '\g<1>\g<30>yadayada', 'bb')
    
    AreEqual(re.sub('x*', '-', 'abc'), '-a-b-c-')
    AreEqual(re.subn('x*', '-', 'abc'), ('-a-b-c-', 4))
    AreEqual(re.sub('a*', '-', 'abc'), '-b-c-')
    AreEqual(re.subn('a*', '-', 'abc'), ('-b-c-', 3))
    AreEqual(re.sub('a*', '-', 'a'), '-')
    AreEqual(re.subn('a*', '-', 'a'), ('-', 1))
    AreEqual(re.sub("a*", "-", "abaabb"), '-b-b-b-')
    AreEqual(re.subn("a*", "-", "abaabb"), ('-b-b-b-', 4))
    AreEqual(re.sub("(a*)b", "-", "abaabb"), '---')
    AreEqual(re.subn("(a*)b", "-", "abaabb"), ('---', 3))
    
    AreEqual(re.subn("(ab)*", "cd", "abababababab", 10), ('cd', 1))
    
    AreEqual(re.sub('x*', '-', 'abxd'), '-a-b-d-')
    AreEqual(re.subn('x*', '-', 'abxd'), ('-a-b-d-', 4))

    Assert(re.sub('([^aeiou])y$', r'\lies', 'vacancy') == 'vacan\\lies')
    Assert(re.sub('([^aeiou])y$', r'\1ies', 'vacancy') == 'vacancies')
    
    AreEqual(re.sub("a+", "\n\t\\\?\"\b", "abc"), '\n\t\\?"\x08bc')
    AreEqual(re.sub("a+", r"\n\t\\\?\"\b", "abc"), '\n\t\\\\?\\"\x08bc')
    AreEqual(re.sub("a+", "\n\t\\\\\\?\"\b", "abc"), '\n\t\\\\?"\x08bc')

def test_dot():
    a = re.compile('.')
    AreEqual(a.groupindex, {})
    
    p = re.compile('.')
    z = []
    for c in p.finditer('abc'):  z.append((c.start(), c.end()))
    z.sort()
    AreEqual(z, [(0,1), (1,2), (2,3)])

def test_x():
    nonmatchingp = re.compile('x')
    AreEqual(nonmatchingp.search('ecks', 1, 4), None)

def test_match():
    p = re.compile('.')
    AreEqual(p.match('bazbar', 1,2).span(), (1,2))
    
def test_span():
    AreEqual(re.match('(baz)(bar)(m)', "bazbarmxyz").span(2),(3, 6))
    
def test_regs():
    AreEqual(re.match('(baz)(bar)(m)', "bazbarmxyz").regs,
             ((0, 7), (0, 3), (3, 6), (6, 7)))

    AreEqual(re.match('bazbar(mm)+(abc)(xyz)', "bazbarmmmmabcxyz123456abc").regs,
             ((0, 16), (8, 10), (10, 13), (13, 16)))
             
def test_endpos():
    AreEqual(re.match('(baz)(bar)(m)', "bazbarmx").endpos, 8)
    pass
    
def test_re():
    #Just ensure it's there for now
    stuff = re.match('a(baz)(bar)(m)', "abazbarmx")
    Assert(hasattr(stuff, "re"))
    Assert(hasattr(stuff.re, "sub"))
    
def test_pos():
    AreEqual(re.match('(baz)(bar)(m)', "bazbarmx").pos, 0)
 
def test_startandend():
    m = re.match(r'(a)|(b)', 'b')
    AreEqual(m.groups(), (None, 'b'))
    AreEqual(m.group(0), "b")
    AreEqual(m.start(0), 0)
    AreEqual(m.end(0), 1)
    AreEqual(m.start(1), -1)
    AreEqual(m.end(1), -1)
    m = re.match(".*", '')
    AreEqual(m.groups(), ())
    AreEqual(m.start(0), 0)
    AreEqual(m.end(0), 0)
    AssertError(IndexError, m.group, "112")
    AssertError(IndexError, m.group, 112)
    AssertError(IndexError, m.group, "-1")
    AssertError(IndexError, m.group, -1)
    AssertError(IndexError, m.start, 112)
    AssertError(IndexError, m.start, -1)
    AssertError(IndexError, m.end, "112")
    AssertError(IndexError, m.end, 112)
    AssertError(IndexError, m.end, "-1")
    AssertError(IndexError, m.end, -1)

    match = re.match(r'(?P<test>test)', 'test')
    AreEqual(match.start('test'), 0)
    AreEqual(match.end('test'), 4)
    
def test_start_of_str():
    startOfStr = re.compile('^')
    AreEqual(startOfStr.match('bazbar', 1), None)
    AreEqual(startOfStr.match('bazbar', 0,0).span(), (0,0))
    AreEqual(startOfStr.match('bazbar', 1,2), None)
    AreEqual(startOfStr.match('bazbar', endpos=3).span(), (0,0))
    
    AreEqual(re.sub('^', 'x', ''), 'x')
    AreEqual(re.sub('^', 'x', ' '), 'x ')
    AreEqual(re.sub('^', 'x', 'abc'), 'xabc')

# check that groups in split RE are added properly
def test_split():
    AreEqual(re.split('{(,)?}', '1 {} 2 {,} 3 {} 4'), ['1 ', None, ' 2 ', ',', ' 3 ', None, ' 4'])

    pnogrp = ','
    
    ptwogrp = '((,))'
    csv = '0,1,1,2,3,5,8,13,21,44'
    AreEqual(re.split(pnogrp, csv, 1), ['0', csv[2:]])
    AreEqual(re.split(pnogrp, csv, 2), ['0','1', csv[4:]])
    AreEqual(re.split(pnogrp, csv, 1000), re.split(pnogrp, csv))
    AreEqual(re.split(pnogrp, csv, 0), re.split(pnogrp, csv))
    AreEqual(re.split(pnogrp, csv, -1), [csv])
    
    ponegrp = '(,)'
    AreEqual(re.split(ponegrp, csv, 1), ['0', ',', csv[2:]])

def test_escape():
    compiled = re.compile(re.escape("hi_"))
    
    all = re.compile('(.*)')
    AreEqual(all.search('abcdef', 3).group(0), 'def')
    
    AssertError(IndexError, re.match("a[bcd]*b", 'abcbd').group, 1)
    AreEqual(re.match('(a[bcd]*b)', 'abcbd').group(1), 'abcb')

    s = ''
    for i in range(32, 128):
        if not chr(i).isalnum():
            s = s + chr(i)
    x = re.escape(s)
    Assert(x == '\\ \\!\\"\\#\\$\\%\\&\\\'\\(\\)\\*\\+\\,\\-\\.\\/\\:\\;\\<\\=\\>\\?\\@\\[\\\\\\]\\^\\_\\`\\{\\|\\}\\~\\\x7f')

    x = re.compile(r'[\\A-Z\.\+]')
    Assert(x.search('aaaA\\B\\Caaa'))
    
# From the docs: "^" matches only at the start of the string, or in MULTILINE mode also immediately
# following a newline.
m = re.compile("a").match("ba", 1)  # succeed
AreEqual('a', m.group(0))
# bug 23668
#AreEqual(re.compile("^a").search("ba", 1), None)   # fails; 'a' not at start
#AreEqual(re.compile("^a").search("\na", 1), None)  # fails; 'a' not at start
m = re.compile("^a", re.M).search("\na", 1)        # succeed (multiline)
AreEqual('a', m.group(0))

# bug 938
#AreEqual(re.compile("^a", re.M).search("ba", 1), None) # fails; no preceding \n

# findall
def test_findall():
    for (x, y, z) in (
            ('\d+', '99 blahblahblah 183 blah 12 blah 7777 yada yada', ['99', '183', '12', '7777']),
            ('^\d+', '0blahblahblah blah blah yada yada1', ['0']),
            ('^\d+', 'blahblahblah blah blah yada yada1', []),
            ("(\d+)|(\w+)", "x = 999y + 23", [('', 'x'), ('999', ''), ('', 'y'), ('23', '')]),
            ("(\d)(\d\d)(\d\d\d)", "123456789123456789", [('1', '23', '456'), ('7', '89', '123'), ('4', '56', '789')]),
            (r"(?i)(\w+)\s+fish\b", "green fish black fish red fish blue fish", ['green', 'black', 'red', 'blue']),
            ('(a)(b)', 'abab', [('a', 'b'), ('a', 'b')]),
        ):
        AreEqual(re.findall(x, y), z)
        AreEqual(re.compile(x).findall(y), z)

def test_match_groups():
    m = re.match('(?P<test>a)(b)', 'ab')
    Assert(m.groups() == ('a', 'b'))
    m = re.match('(u)(?P<test>v)(b)(?P<Named2>w)(x)(y)', 'uvbwxy')
    Assert(m.groups() == ('u', 'v', 'b', 'w', 'x', 'y'))

def test_options():
    # coverage for ?iLmsux options in re.compile path
    tests = [ ("t(?=s)", "atreftsadbeatwttta", ['t']),
              ("t(?!s)", "atreftsadbeatststs", ['t']) ]

    # native implementation does not handle extensions specified in this way
    if is_cli:
        tests.extend([
            ("(?i:foo)", "fooFoo FOO fOo fo oFO O\n\t\nFo ofO O", ['foo', 'Foo', 'FOO', 'fOo']),
            ("(?im:^foo)", "fooFoo FOO fOo\n\t\nFoo\nFOO", ['foo', 'Foo', 'FOO']), # ignorecase, multiline (matches at beginning of string and at each newline)
            ("(?s:foo.*bar)", "foo yadayadayada\nyadayadayada bar", ['foo yadayadayada\nyadayadayada bar']), # dotall (make "." match any chr, including a newline)
            ("(?x:baz  bar)", "bazbar foo bar      bazbar \n\n\tbazbar", ['bazbar', 'bazbar', 'bazbar']),  #verbose (ignore whitespace)
            ])
    for (x, y, z) in tests:
        AreEqual(re.findall(x, y), z)
        AreEqual(re.compile(x).findall(y), z)
        
def test_bug858():
    pattern = r"""\(? #optional paren
       \)? #optional paren
       \d+ """
    c = re.compile(pattern, re.X)
    l = c.findall("989")
    Assert(l == ['989'])

def test_finditer():
    # finditer
    matches = re.finditer("baz","barbazbarbazbar")
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    Assert(num == 2)

    matches = re.finditer("baz","barbazbarbazbar", re.L)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    Assert(num == 2)
    
    matches = re.compile("baz").finditer("barbazbarbazbar", 0)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    Assert(num == 2)
    
    matches = re.compile("baz").finditer("barbazbarbazbar", 14)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    Assert(num == 0)
    
    matches = re.compile("baz").finditer("barbazbarbazbar", 0, 14)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    Assert(num == 2)
    
    matches = re.compile("baz").finditer("barbazbarbazbar", 9, 12)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    AreEqual(num, 1)
    
    matches = re.compile("baz").finditer("barbazbarbazbar", 9, 11)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    AreEqual(num, 0)
    
    matches = re.compile("baz").finditer("barbazbarbazbar", 10, 12)
    num = 0
    for m in matches:
        num = num + 1
        AreEqual("baz", m.group(0))
    AreEqual(num, 0)

def test_search():
    # search
    sp = re.search('super', 'blahsupersuper').span()
    Assert(sp == (4, 9))
    
    sp = re.search('super', 'superblahsuper').span()
    Assert(sp == (0, 5))

    #re.search.group() index error

    AreEqual(re.search("z.*z", "az123za").group(),'z123z')
    AreEqual(re.search("z.*z", "az12za").group(),'z12z')
    AreEqual(re.search("z.*z", "azza").group(),'zz')
    AreEqual(re.search("z123p+z", "az123ppppppppppza").group(),'z123ppppppppppz')
    AreEqual(re.search("z123p+z", "az123pza").group(),'z123pz')
    AreEqual(re.search("z123p?z", "az123pza").group(),'z123pz')
    AreEqual(re.search("z123p?z", "az123za").group(),'z123z')
    
    AreEqual(re.search('b', 'abc').string, 'abc')
    
def test_subn():
    # subn
    tup = re.subn("ab", "cd", "abababababab")
    Assert(tup == ('cdcdcdcdcdcd', 6))
    tup = re.subn("ab", "cd", "abababababab", 0)
    Assert(tup == ('cdcdcdcdcdcd', 6))
    tup = re.subn("ab", "cd", "abababababab", 1)
    Assert(tup == ('cdababababab', 1))
    tup = re.subn("ab", "cd", "abababababab", 10)
    Assert(tup == ('cdcdcdcdcdcd', 6))
    tup = re.subn("ababab", "cd", "ab", 10)
    Assert(tup == ('ab', 0))
    tup = re.subn("ababab", "cd", "ab")
    Assert(tup == ('ab', 0))
    
    tup = re.subn("(ab)*", "cd", "abababababab", 10)
    Assert(tup == ('cd', 1))
    tup = re.subn("(ab)?", "cd", "abababababab", 10)
    Assert(tup == ('cdcdcdcdcdcd', 6))


def test_groups():
    reg = re.compile("\[(?P<header>.*?)\]")
    m = reg.search("[DEFAULT]")
    Assert( m.groups() == ('DEFAULT',))
    Assert( m.group('header') == 'DEFAULT' )
    
    reg2 = re.compile("(?P<grp>\S+)?")
    m2 = reg2.search("")
    Assert ( m2.groups() == (None,))
    Assert ( m2.groups('Default') == ('Default',))

def test_locale_flags():
    AreEqual(re.compile(r"^\#[ \t]*(\w[\d\w]*)[ \t](.*)").flags, 0)
    AreEqual(re.compile(r"^\#[ \t]*(\w[\d\w]*)[ \t](.*)", re.L).flags, re.L)
    AreEqual(re.compile(r"(?L)^\#[ \t]*(\w[\d\w]*)[ \t](.*)").flags, re.L)

def test_end():
    ex = re.compile(r'\s+')
    m = ex.match('(object Petal', 7)
    Assert (m.end(0) == 8)

def test_lone_hat():
    """Single ^ reg-ex shouldn't match w/ a sub-set of a string"""
    sol = re.compile('^')
    AreEqual(sol.match('bazbar', 1, 2), None)

    AreEqual(sol.match('foobar', 1, 2), None)

def test_escape_backslash():
    x = re.compile (r"[\\A-Z\.\+]")
    AreEqual(x.search('aaaA\\B\\Caaa').span(), (3,4))

def test_eol():
    r = re.compile(r'<(/|\Z)')
    s = r.search("<", 0)
    Assert(s != None)
    AreEqual(s.span(), (0, 1))
    AreEqual(s.group(0), '<')
    AreEqual(r.search("<Z", 0), None)
    
def test_lastindex():
    for (pat, index) in [
              ('(a)b', 1), ('((a)(b))', 1), ('((ab))', 1),
              ('(a)(b)', 2),
              ('(a)?ab', None),
              ('(a)?b', 1),
            ]:
        AreEqual(re.match(pat, 'ab').lastindex, index)
        
    for (pat, index) in [
              ('(a)ab', 1),
              ('(a)(a)b', 2),
              ('(a)(a)(b)', 3),
              ('((a)a(b))', 1),
              ('((a)(a)(b))', 1),
              ('(a(a)(b))', 1),
              ('(a(a)?(b))', 1),
              ('(aa(a)?(b))', 1),
              ('(aa(b))', 1),
              ('(a(ab))', 1),
              ('(a)?ab', 1),
              ('a(a)?ab', None),
              ('a(a)?(a)?b', 1),
              ('a(a)?(a)?(b)', 3),
              ('a(a)b', 1),
              ('(a(a))(b)', 3),
              ('(a(a))b', 1),
              ('((a)(a))(b)', 4),
              ('((a)(a))b', 1),
            ]:
        AreEqual(re.match(pat, 'aab').lastindex, index)

def test_empty_split():
    cases =[
            ('', ['']),
            ('*', ['*']),
            (':', ['', '']),
            ('::', ['', '']),
            ('a::', ['a', '']),
            ('::b', ['', 'b']),
            (':c:', ['', 'c', '']),
            (':\t: ', ['', '\t', ' ']),
            ('a:b::c', ['a', 'b', 'c']),
            (':a:b::c', ['', 'a', 'b', 'c']),
            ('::a:b::c:', ['', 'a', 'b', 'c', '']),
            ]
    for expr, result in cases:
        AreEqual(re.split(":*", expr), result)

@skip("silverlight")
def test_cp15298():
    regex = "^" + "\d\.\d\.\d \(IronPython \d\.\d(\.\d)? ((Alpha )|(Beta )|())\(\d\.\d\.\d\.\d{3,4}\) on \.NET \d(\.\d{1,5}){3}\)" * 15 + "$"
    match_str = "2.5.0 (IronPython 2.0 Beta (2.0.0.1000) on .NET 2.0.50727.1433)" * 15
    compiled_regex = re.compile(regex)
    
    retval = compiled_regex.match(match_str)
    Assert(retval != None)
    
    retval = re.match(regex, match_str)
    Assert(retval != None)

def test_cp11136():
    regex = re.compile(r"^(?P<msg>NMAKE[A-Za-z0-9]*)'\"?(?P<file>[\\A-Za-z0-9/:_\.\+]+)" )
    Assert(regex.search(r"NMAKE0119'adirectory\afile.txt")!=None)

def test_cp17111():
    test_cases = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#%&_+-=]{};':,.//<>" + '"'
    for x in test_cases:
        regex = re.compile(r".*\\%s" % x)
        Assert(regex.search(r"\\%s" % x)!=None)
        Assert(regex.search(r"")==None)
        
def test_cp1089():
    test_cases = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#%&_+-=]{};':,.//<>" + '"'
    for x in test_cases:
        #Just make sure they don't throw
        temp = re.compile('\\\\' + x)

def test_cp16657():
    Assert(re.compile(r'^bar', re.M).search('foo\nbar') != None)
    Assert(re.compile(r'^bar(?m)').search('foo\nbar') != None)
    Assert(re.compile(r'^bar', re.M).search('foo\nbaar') == None)
    Assert(re.compile(r'^bar(?m)').search('foo\nbaar') == None)
    
    Assert(re.compile(r'^bar', re.U).search('bar') != None)
    Assert(re.compile(r'^bar(?u)').search('bar') != None)
    Assert(re.compile(r'^bar', re.U).search('baar') == None)
    Assert(re.compile(r'^bar(?u)').search('baar') == None)
    
    Assert(re.compile(r'     b ar   ', re.X).search('bar') != None)
    Assert(re.compile(r'b ar(?x)').search('bar') != None)
    Assert(re.compile(r'     b ar   ', re.X).search('baar') == None)
    Assert(re.compile(r'b ar(?x)').search('baar') == None)
    Assert(re.compile(r'b ar').search('bar') == None)    

def test_n_m_quantifier():
    AreEqual(re.search('ab{,2}a', 'abba').span(), (0, 4))
    AreEqual(re.search('ab{,2}a', 'aba').span(), (0, 3))
    AreEqual(re.search('ab{,2}a', 'abbba'), None)
    AreEqual(re.search('ab{,2}a', 'abba').span(), re.search('ab{0,2}a', 'abba').span())
    AreEqual(re.search('ab{0,2}a', 'abbba'), None)
    AreEqual(re.search('ab{2,}a', 'abba').span(), (0,4))
    AreEqual(re.search('ab{2,}a', 'abbba').span(), (0,5))
    AreEqual(re.search('ab{2,}a', 'aba'), None)

def test_mixed_named_and_unnamed_groups():
    example1=r"(?P<one>Blah)"
    example2=r"(?P<one>(Blah))"
    RegExsToTest=[example1,example2]
    
    for regString in RegExsToTest:
        g=re.compile(regString)
        AreEqual(g.groupindex, {'one' : 1})

def test__pickle():
    '''
    TODO: just a sanity test for now.  Needs far more testing.
    '''
    regex = re.compile(r"^(?P<msg>NMAKE[A-Za-z0-9]*)'\"?(?P<file>[\\A-Za-z0-9/:_\.\+]+)" )
    pickled_regex = re._pickle(regex)
    AreEqual(len(pickled_regex), 2)
    AreEqual(pickled_regex[1],
             ('^(?P<msg>NMAKE[A-Za-z0-9]*)\'\\"?(?P<file>[\\\\A-Za-z0-9/:_\\.\\+]+)', 0))


def test_conditional():
    p = re.compile(r'(a)?(b)((?(1)c))')
    AreEqual(p.match('abc').groups(), ('a', 'b', 'c'))
    p = re.compile(r'(?P<first>a)?(b)((?(first)c))')
    AreEqual(p.match('abc').groups(), ('a', 'b', 'c'))
    s = r'((?(a)ab|cd))'
    if is_cli or is_silverlight:
        p = re.compile(s)
        AreEqual(p.match('ab').groups(), ('ab',))
    else:
        AssertError(re.error, re.compile, s)

def test_cp35146():
    # re.compile returns cached instances
    AreEqual(re.compile('cp35146'), re.compile('cp35146'))

def test_cp35135():
    AreEqual(re.match(r"(?iu)aA", "aa").string, "aa")
    AreEqual(re.match(r"(?iu)Aa", "aa").string, "aa")
    AreEqual(re.match(r"(?iLmsux)Aa", "aa").string, "aa")

def test_issue506():
    AreEqual(re.compile("^a", re.M).search("ba", 1), None)

def test_issue1370():
    AreEqual(re.compile("\Z").match("\n"), None)
    AreEqual(re.compile("\Z").match("").group(0), "")

#--MAIN------------------------------------------------------------------------        
run_test(__name__)
