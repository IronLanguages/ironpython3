# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import re
import unittest

from iptest import IronPythonTestCase, is_cli, run_test

class ReTest(IronPythonTestCase):

    def test_none(self):
        for x in 'compile search match split findall finditer'.split():
            y = getattr(re, x)
            self.assertRaises(TypeError, y, None)
            self.assertRaises(TypeError, y, None, None)
            self.assertRaises(TypeError, y, None, 'abc')
            self.assertRaises(TypeError, y, 'abc', None)

        # Other exceptional input tests
        for x in (re.sub, re.subn):
            self.assertRaises(TypeError, x, 'abc', None, 'abc')
            self.assertRaises(TypeError, x, 'abc', None, None)
            self.assertRaises(TypeError, x, None, 'abc', 'abc')
            self.assertRaises(TypeError, x, 'abc', 'abc', None)

        self.assertRaises(TypeError, re.escape, None)

    def test_sanity_re(self):
        '''
        Basic sanity tests for the re module.  Each module member is
        used at least once.
        '''
        #compile
        self.assertTrue(hasattr(re.compile("(abc){1}"), "pattern"))
        self.assertTrue(hasattr(re.compile("(abc){1}", re.L), "pattern"))
        self.assertTrue(hasattr(re.compile("(abc){1}", flags=re.L), "pattern"))

        #I IGNORECASE L LOCAL MMULTILINE S DOTALL U UNICODE X VERBOSE
        flags = ["I", "IGNORECASE",
                    "L", "LOCALE",
                    "M", "MULTILINE",
                    "S", "DOTALL",
                    "U", "UNICODE",
                    "X", "VERBOSE"]

        for f in flags:
            self.assertTrue(hasattr(re, f))

        #search
        self.assertEqual(re.search("(abc){1}", ""), None)
        self.assertEqual(re.search("(abc){1}", "abcxyz").span(), (0,3))
        self.assertEqual(re.search("(abc){1}", "abcxyz", re.L).span(), (0,3))
        self.assertEqual(re.search("(abc){1}", "abcxyz", flags=re.L).span(), (0,3))
        self.assertEqual(re.search("(abc){1}", "xyzabc").span(), (3,6))

        #match
        self.assertEqual(re.match("(abc){1}", ""), None)
        self.assertEqual(re.match("(abc){1}", "abcxyz").span(), (0,3))
        self.assertEqual(re.match("(abc){1}", "abcxyz", re.L).span(), (0,3))
        self.assertEqual(re.match("(abc){1}", "abcxyz", flags=re.L).span(), (0,3))

        #split
        self.assertEqual(re.split("(abc){1}", ""), [''])
        self.assertEqual(re.split("(abc){1}", "abcxyz"), ['', 'abc', 'xyz'])
        #maxsplit
        self.assertEqual(re.split("(abc){1}", "abc", 0), ['', 'abc', ''])
        for i in range(3):
            self.assertEqual(re.split("(abc){1}", "abc", maxsplit=i), ['', 'abc', ''])
            self.assertEqual(re.split("(abc){1}", "", maxsplit=i), [''])
            self.assertEqual(re.split("(abc){1}", "abcxyz", maxsplit=i), ['', 'abc', 'xyz'])
        self.assertEqual(re.split("(abc){1}", "abcxyzabc", maxsplit=0), ['', 'abc', 'xyz', 'abc', ''])
        self.assertEqual(re.split("(abc){1}", "abcxyzabc", maxsplit=1), ['', 'abc', 'xyzabc'])
        self.assertEqual(re.split("(abc){1}", "abcxyzabc", maxsplit=2), ['', 'abc', 'xyz', 'abc', ''])

        #findall
        self.assertEqual(re.findall("(abc){1}", ""), [])
        self.assertEqual(re.findall("(abc){1}", "abcxyz"), ['abc'])
        self.assertEqual(re.findall("(abc){1}", "abcxyz", re.L), ['abc'])
        self.assertEqual(re.findall("(abc){1}", "abcxyz", flags=re.L), ['abc'])
        self.assertEqual(re.findall("(abc){1}", "xyzabcabc"), ['abc', 'abc'])

        #finditer
        self.assertEqual([x.group() for x in re.finditer("(abc){1}", "")], [])
        self.assertEqual([x.group() for x in re.finditer("(abc){1}", "abcxyz")], ['abc'])
        self.assertEqual([x.group() for x in re.finditer("(abc){1}", "abcxyz", re.L)], ['abc'])
        self.assertEqual([x.group() for x in re.finditer("(abc){1}", "abcxyz", flags=re.L)], ['abc'])
        self.assertEqual([x.group() for x in re.finditer("(abc){1}", "xyzabcabc")], ['abc', 'abc'])
        rex = re.compile("foo")
        for m in rex.finditer("this is a foo and a foo bar"):
            self.assertEqual((m.pos, m.endpos), (0, 27))
        for m in rex.finditer(""):
            self.assertEqual((m.pos, m.endpos), (0, 1))
        for m in rex.finditer("abc"):
            self.assertEqual((m.pos, m.endpos), (0, 4))
        for m in rex.finditer("foo foo foo foo foo"):
            self.assertEqual((m.pos, m.endpos), (0, 19))

        #sub
        self.assertEqual(re.sub("(abc){1}", "9", "abcd"), "9d")
        self.assertEqual(re.sub("(abc){1}", "abcxyz",'abcd'), "abcxyzd")
        self.assertEqual(re.sub("(abc){1}", "1", "abcd", 0), "1d")
        self.assertEqual(re.sub("(abc){1}", "1", "abcd", count=0), "1d")
        self.assertEqual(re.sub("(abc){1}", "1", "abcdabcd", 1), "1dabcd")
        self.assertEqual(re.sub("(abc){1}", "1", "abcdabcd", 2), "1d1d")
        self.assertEqual(re.sub("(abc){1}", "1", "ABCdabcd", 2, flags=re.I), "1d1d")

        #subn
        self.assertEqual(re.subn("(abc){1}", "9", "abcd"), ("9d", 1))
        self.assertEqual(re.subn("(abc){1}", "abcxyz",'abcd'), ("abcxyzd",1))
        self.assertEqual(re.subn("(abc){1}", "1", "abcd", 0), ("1d",1))
        self.assertEqual(re.subn("(abc){1}", "1", "abcd", count=0), ("1d",1))
        self.assertEqual(re.subn("(abc){1}", "1", "abcdabcd", 1), ("1dabcd",1))
        self.assertEqual(re.subn("(abc){1}", "1", "abcdabcd", 2), ("1d1d",2))
        self.assertEqual(re.subn("(abc){1}", "1", "ABCdabcd", 2, flags=re.I), ("1d1d",2))

        #escape
        self.assertEqual(re.escape("abc"), "abc")
        self.assertEqual(re.escape(""), "")
        self.assertEqual(re.escape("_"), "_")
        self.assertEqual(re.escape("a_c"), "a_c")

        #error
        exc = re.error()
        exc = re.error("some args")

        #purge
        re.purge()

    def test_sanity_re_pattern(self):
        '''
        Basic sanity tests for the re module's Regular Expression
        objects (i.e., Pattern in CPython).  Each method/member is
        utilized at least once.
        '''
        pattern = re.compile("(abc){1}")

        #match
        self.assertEqual(pattern.match(""), None)
        self.assertEqual(pattern.match("abcxyz").span(), (0,3))
        self.assertEqual(pattern.match("abc", 0).span(), (0,3))
        self.assertEqual(pattern.match("abc", 0, 3).span(), (0,3))
        self.assertEqual(pattern.match("abc", pos=0, endpos=3).span(), (0,3))
        for i in [-1, -2, -5, -7, -8, -65536]:
            for j in [3, 4, 5, 7, 8, 65536]:
                self.assertEqual(pattern.match("abc", i, j).span(), (0,3))
        self.assertRaises(OverflowError, lambda: pattern.match("abc", 0, 2**64).span())
        self.assertRaises(OverflowError, lambda: pattern.match("abc", -(2**64), 3).span())

        #search
        self.assertEqual(pattern.search(""), None)
        self.assertEqual(pattern.search("abcxyz").span(), (0,3))
        self.assertEqual(pattern.search("abc", 0).span(), (0,3))
        self.assertEqual(pattern.search("abc", 0, 3).span(), (0,3))
        self.assertEqual(pattern.search("abc", pos=0, endpos=3).span(), (0,3))
        self.assertEqual(pattern.search("xyzabc").span(), (3,6))

        #split
        self.assertEqual(pattern.split(""), [''])
        self.assertEqual(pattern.split("abcxyz"), ['', 'abc', 'xyz'])
        self.assertEqual(pattern.split("abc", 0), ['', 'abc', ''])
        self.assertEqual(pattern.split("abc", maxsplit=0), ['', 'abc', ''])
        self.assertEqual(pattern.split("abcxyzabc", maxsplit=1), ['', 'abc', 'xyzabc'])

        #findall
        self.assertEqual(pattern.findall(""), [])
        self.assertEqual(pattern.findall("abcxyz"), ['abc'])
        self.assertEqual(pattern.findall("abc", 0), ['abc'])
        self.assertEqual(pattern.findall("abc", 0, 3), ['abc'])
        self.assertEqual(pattern.findall("abc", pos=0, endpos=3), ['abc'])
        self.assertEqual(pattern.findall("xyzabcabc"), ['abc', 'abc'])

        #sub
        self.assertEqual(pattern.sub("9", "abcd"), "9d")
        self.assertEqual(pattern.sub("abcxyz",'abcd'), "abcxyzd")
        self.assertEqual(pattern.sub("1", "abcd", 0), "1d")
        self.assertEqual(pattern.sub("1", "abcd", count=0), "1d")
        self.assertEqual(pattern.sub("1", "abcdabcd", 1), "1dabcd")
        self.assertEqual(pattern.sub("1", "abcdabcd", 2), "1d1d")

        #subn
        self.assertEqual(pattern.subn("9", "abcd"), ("9d", 1))
        self.assertEqual(pattern.subn("abcxyz",'abcd'), ("abcxyzd",1))
        self.assertEqual(pattern.subn("1", "abcd", 0), ("1d",1))
        self.assertEqual(pattern.subn("1", "abcd", count=0), ("1d",1))
        self.assertEqual(pattern.subn("1", "abcdabcd", 1), ("1dabcd",1))
        self.assertEqual(pattern.subn("1", "abcdabcd", 2), ("1d1d",2))

        #flags
        self.assertEqual(pattern.flags, 0 if is_cli else 32)
        self.assertEqual(re.compile("(abc){1}", re.L).flags, re.L | (0 if is_cli else 32))

        #groupindex
        self.assertEqual(pattern.groupindex, {})
        self.assertEqual(re.compile("(?P<abc>)(?P<bcd>)").groupindex, {'bcd': 2, 'abc': 1})

        #pattern
        self.assertEqual(pattern.pattern, "(abc){1}")
        self.assertEqual(re.compile("").pattern, "")

    def test_groupindex_empty(self):
        test_list = [   ".", "^", "$", "1*", "2+", "3?", "4*?", "5+?", "6??", "7{1}", "8{1,2}",
                        "9{1,2}?", "[a-z]", "|", "(...)", "(?:abc)",
                        "\(\?P\<Blah\>abc\)", "(?#...)", "(?=...)", "(?!...)", "(?<=...)",
                        "(?<!...)", "\1", "\A", "\d"
                        ]
        for x in test_list:
            self.assertEqual(re.compile(x).groupindex, {})

    def test_sanity_re_match(self):
        '''
        Basic sanity tests for the re module's Match objects.  Each method/member
        is utilized at least once.
        '''
        pattern = re.compile("(abc){1}")
        match_obj = pattern.match("abcxyzabc123 and some other words...")

        #expand
        self.assertEqual(match_obj.expand("\1\g<1>.nt"), '\x01abc.nt')

        #group
        self.assertEqual(match_obj.group(), 'abc')
        self.assertEqual(match_obj.group(1), 'abc')

        #groups
        self.assertEqual(match_obj.groups(), ('abc',))
        self.assertEqual(match_obj.groups(1), ('abc',))
        self.assertEqual(match_obj.groups(99), ('abc',))

        #groupdict
        self.assertEqual(match_obj.groupdict(), {})
        self.assertEqual(match_obj.groupdict(None), {})
        self.assertEqual(re.compile("(abc)").match("abcxyzabc123 and...").groupdict(), {})

        #start
        self.assertEqual(match_obj.start(), 0)
        self.assertEqual(match_obj.start(1), 0)

        #end
        self.assertEqual(match_obj.end(), 3)
        self.assertEqual(match_obj.end(1), 3)

        #span
        self.assertEqual(match_obj.span(), (0,3))
        self.assertEqual(match_obj.span(1), (0,3))

        #pos
        self.assertEqual(match_obj.pos, 0)

        #endpos
        self.assertEqual(match_obj.endpos, 36)

        #lastindex
        self.assertEqual(match_obj.lastindex, 1)

        #lastgroup
        #CodePlex Work Item 5518
        #self.assertEqual(match_obj.lastgroup, None)

        #re
        self.assertTrue(match_obj.re==pattern)

        #string
        self.assertEqual(match_obj.string, "abcxyzabc123 and some other words...")

    def test_comment(self):
        '''
        (?#...)
        '''
        pattern = "a(?#foo)bc"
        c = re.compile(pattern)
        self.assertEqual(c.findall("abc"), ['abc'])

        pattern = "a(?#)bc"
        c = re.compile(pattern)
        self.assertEqual(c.findall("abc"), ['abc'])

        pattern = "a(?#foo)bdc"
        c = re.compile(pattern)
        self.assertEqual(len(c.findall("abc")), 0)

    def test_optional_paren(self):
        pattern = r"""\(?\w+\)?"""
        c = re.compile(pattern, re.X)
        self.assertEqual(c.findall('abc'), ['abc'])

    def test_back_match(self):
        p = re.compile('(?P<grp>.+?)(?P=grp)')
        self.assertEqual(p.match('abcabc').groupdict(), {'grp':'abc'})
        p = re.compile(r'(?P<delim>[%$])(?P<escaped>(?P=delim))')
        self.assertEqual(p.match('$$').groupdict(), {'escaped': '$', 'delim': '$'})
        self.assertEqual(p.match('$%'), None)
        p = re.compile(r'(?P<grp>ab)(a(?P=grp)b)')
        self.assertEqual(p.match('abaabb').groups(), ('ab', 'aabb'))

    def test_expand(self):
        self.assertEqual(re.match("(a)(b)", "ab").expand("blah\g<1>\g<2>"), "blahab")
        self.assertEqual(re.match("(a)()", "ab").expand("blah\g<1>\g<2>\n\r\t\\\\"),'blaha\n\r\t\\')
        self.assertEqual(re.match("(a)()", "ab").expand(""),'')

    def test_sub(self):
        x = '\n   #region Generated Foo\nblah\nblah#end region'
        a = re.compile("^([ \t]+)#region Generated Foo.*?#end region", re.MULTILINE|re.DOTALL)

        self.assertEqual(a.sub("xx", x), "\nxx")            # should match successfully
        self.assertEqual(a.sub("\\x12", x), "\n\\x12")      # should match, but shouldn't un-escape for \x

        #if optional count arg is 0 then all occurrences should be replaced
        self.assertEqual('bbbb', re.sub("a","b","abab", 0))

        self.assertEqual(re.sub(r'(?P<id>b)', '\g<id>\g<id>yadayada', 'bb'), 'bbyadayadabbyadayada')
        self.assertEqual(re.sub(r'(?P<id>b)', '\g<1>\g<id>yadayada', 'bb'), 'bbyadayadabbyadayada')
        self.assertRaises(IndexError, re.sub, r'(?P<id>b)', '\g<1>\g<i2>yadayada', 'bb')
        # the native implementation just gives a sre_constants.error instead indicating an invalid
        # group reference
        if is_cli:
            self.assertRaises(IndexError, re.sub, r'(?P<id>b)', '\g<1>\g<30>yadayada', 'bb')

        self.assertEqual(re.sub('x*', '-', 'abc'), '-a-b-c-')
        self.assertEqual(re.subn('x*', '-', 'abc'), ('-a-b-c-', 4))
        self.assertEqual(re.sub('a*', '-', 'abc'), '-b-c-')
        self.assertEqual(re.subn('a*', '-', 'abc'), ('-b-c-', 3))
        self.assertEqual(re.sub('a*', '-', 'a'), '-')
        self.assertEqual(re.subn('a*', '-', 'a'), ('-', 1))
        self.assertEqual(re.sub("a*", "-", "abaabb"), '-b-b-b-')
        self.assertEqual(re.subn("a*", "-", "abaabb"), ('-b-b-b-', 4))
        self.assertEqual(re.sub("(a*)b", "-", "abaabb"), '---')
        self.assertEqual(re.subn("(a*)b", "-", "abaabb"), ('---', 3))

        self.assertEqual(re.subn("(ab)*", "cd", "abababababab", 10), ('cd', 1))

        self.assertEqual(re.sub('x*', '-', 'abxd'), '-a-b-d-')
        self.assertEqual(re.subn('x*', '-', 'abxd'), ('-a-b-d-', 4))

        self.assertTrue(re.sub('([^aeiou])y$', r'\lies', 'vacancy') == 'vacan\\lies')
        self.assertTrue(re.sub('([^aeiou])y$', r'\1ies', 'vacancy') == 'vacancies')

        self.assertEqual(re.sub("a+", "\n\t\\\?\"\b", "abc"), '\n\t\\?"\x08bc')
        self.assertEqual(re.sub("a+", r"\n\t\\\?\"\b", "abc"), '\n\t\\\\?\\"\x08bc')
        self.assertEqual(re.sub("a+", "\n\t\\\\\\?\"\b", "abc"), '\n\t\\\\?"\x08bc')

    def test_dot(self):
        a = re.compile('.')
        self.assertEqual(a.groupindex, {})

        p = re.compile('.')
        z = []
        for c in p.finditer('abc'):  z.append((c.start(), c.end()))
        z.sort()
        self.assertEqual(z, [(0,1), (1,2), (2,3)])

    def test_x(self):
        nonmatchingp = re.compile('x')
        self.assertEqual(nonmatchingp.search('ecks', 1, 4), None)

    def test_match(self):
        p = re.compile('.')
        self.assertEqual(p.match('bazbar', 1,2).span(), (1,2))

    def test_span(self):
        self.assertEqual(re.match('(baz)(bar)(m)', "bazbarmxyz").span(2),(3, 6))

    def test_regs(self):
        self.assertEqual(re.match('(baz)(bar)(m)', "bazbarmxyz").regs,
                ((0, 7), (0, 3), (3, 6), (6, 7)))

        self.assertEqual(re.match('bazbar(mm)+(abc)(xyz)', "bazbarmmmmabcxyz123456abc").regs,
                ((0, 16), (8, 10), (10, 13), (13, 16)))

    def test_endpos(self):
        self.assertEqual(re.match('(baz)(bar)(m)', "bazbarmx").endpos, 8)
        pass

    def test_re(self):
        #Just ensure it's there for now
        stuff = re.match('a(baz)(bar)(m)', "abazbarmx")
        self.assertTrue(hasattr(stuff, "re"))
        self.assertTrue(hasattr(stuff.re, "sub"))

    def test_pos(self):
        self.assertEqual(re.match('(baz)(bar)(m)', "bazbarmx").pos, 0)

    def test_startandend(self):
        m = re.match(r'(a)|(b)', 'b')
        self.assertEqual(m.groups(), (None, 'b'))
        self.assertEqual(m.group(0), "b")
        self.assertEqual(m.start(0), 0)
        self.assertEqual(m.end(0), 1)
        self.assertEqual(m.start(1), -1)
        self.assertEqual(m.end(1), -1)
        m = re.match(".*", '')
        self.assertEqual(m.groups(), ())
        self.assertEqual(m.start(0), 0)
        self.assertEqual(m.end(0), 0)
        self.assertRaises(IndexError, m.group, "112")
        self.assertRaises(IndexError, m.group, 112)
        self.assertRaises(IndexError, m.group, "-1")
        self.assertRaises(IndexError, m.group, -1)
        self.assertRaises(IndexError, m.start, 112)
        self.assertRaises(IndexError, m.start, -1)
        self.assertRaises(IndexError, m.end, "112")
        self.assertRaises(IndexError, m.end, 112)
        self.assertRaises(IndexError, m.end, "-1")
        self.assertRaises(IndexError, m.end, -1)

        match = re.match(r'(?P<test>test)', 'test')
        self.assertEqual(match.start('test'), 0)
        self.assertEqual(match.end('test'), 4)

    def test_start_of_str(self):
        startOfStr = re.compile('^')
        self.assertEqual(startOfStr.match('bazbar', 1), None)
        self.assertEqual(startOfStr.match('bazbar', 0,0).span(), (0,0))
        self.assertEqual(startOfStr.match('bazbar', 1,2), None)
        self.assertEqual(startOfStr.match('bazbar', endpos=3).span(), (0,0))

        self.assertEqual(re.sub('^', 'x', ''), 'x')
        self.assertEqual(re.sub('^', 'x', ' '), 'x ')
        self.assertEqual(re.sub('^', 'x', 'abc'), 'xabc')

    # check that groups in split RE are added properly
    def test_split(self):
        self.assertEqual(re.split('{(,)?}', '1 {} 2 {,} 3 {} 4'), ['1 ', None, ' 2 ', ',', ' 3 ', None, ' 4'])

        pnogrp = ','

        ptwogrp = '((,))'
        csv = '0,1,1,2,3,5,8,13,21,44'
        self.assertEqual(re.split(pnogrp, csv, 1), ['0', csv[2:]])
        self.assertEqual(re.split(pnogrp, csv, 2), ['0','1', csv[4:]])
        self.assertEqual(re.split(pnogrp, csv, 1000), re.split(pnogrp, csv))
        self.assertEqual(re.split(pnogrp, csv, 0), re.split(pnogrp, csv))
        self.assertEqual(re.split(pnogrp, csv, -1), [csv])

        ponegrp = '(,)'
        self.assertEqual(re.split(ponegrp, csv, 1), ['0', ',', csv[2:]])

    def test_escape(self):
        compiled = re.compile(re.escape("hi_"))

        all = re.compile('(.*)')
        self.assertEqual(all.search('abcdef', 3).group(0), 'def')

        self.assertRaises(IndexError, re.match("a[bcd]*b", 'abcbd').group, 1)
        self.assertEqual(re.match('(a[bcd]*b)', 'abcbd').group(1), 'abcb')

        s = ''
        for i in range(32, 128):
            if not chr(i).isalnum():
                s = s + chr(i)
        x = re.escape(s)
        self.assertEqual(x, '\\ \\!\\"\\#\\$\\%\\&\\\'\\(\\)\\*\\+\\,\\-\\.\\/\\:\\;\\<\\=\\>\\?\\@\\[\\\\\\]\\^_\\`\\{\\|\\}\\~\\\x7f')

        x = re.compile(r'[\\A-Z\.\+]')
        self.assertTrue(x.search('aaaA\\B\\Caaa'))

        # From the docs: "^" matches only at the start of the string, or in MULTILINE mode also immediately
        # following a newline.
        m = re.compile("a").match("ba", 1)  # succeed
        self.assertEqual('a', m.group(0))
        # bug 23668
        #self.assertEqual(re.compile("^a").search("ba", 1), None)   # fails; 'a' not at start
        #self.assertEqual(re.compile("^a").search("\na", 1), None)  # fails; 'a' not at start
        m = re.compile("^a", re.M).search("\na", 1)        # succeed (multiline)
        self.assertEqual('a', m.group(0))

        # bug 938
        #self.assertEqual(re.compile("^a", re.M).search("ba", 1), None) # fails; no preceding \n

    # findall
    def test_findall(self):
        for (x, y, z) in (
                ('\d+', '99 blahblahblah 183 blah 12 blah 7777 yada yada', ['99', '183', '12', '7777']),
                ('^\d+', '0blahblahblah blah blah yada yada1', ['0']),
                ('^\d+', 'blahblahblah blah blah yada yada1', []),
                ("(\d+)|(\w+)", "x = 999y + 23", [('', 'x'), ('999', ''), ('', 'y'), ('23', '')]),
                ("(\d)(\d\d)(\d\d\d)", "123456789123456789", [('1', '23', '456'), ('7', '89', '123'), ('4', '56', '789')]),
                (r"(?i)(\w+)\s+fish\b", "green fish black fish red fish blue fish", ['green', 'black', 'red', 'blue']),
                ('(a)(b)', 'abab', [('a', 'b'), ('a', 'b')]),
            ):
            self.assertEqual(re.findall(x, y), z)
            self.assertEqual(re.compile(x).findall(y), z)

    def test_match_groups(self):
        m = re.match('(?P<test>a)(b)', 'ab')
        self.assertTrue(m.groups() == ('a', 'b'))
        m = re.match('(u)(?P<test>v)(b)(?P<Named2>w)(x)(y)', 'uvbwxy')
        self.assertTrue(m.groups() == ('u', 'v', 'b', 'w', 'x', 'y'))

    def test_options(self):
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
            self.assertEqual(re.findall(x, y), z)
            self.assertEqual(re.compile(x).findall(y), z)

    def test_bug858(self):
        pattern = r"""\(? #optional paren
        \)? #optional paren
        \d+ """
        c = re.compile(pattern, re.X)
        l = c.findall("989")
        self.assertTrue(l == ['989'])

    def test_finditer(self):
        # finditer
        matches = re.finditer("baz","barbazbarbazbar")
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertTrue(num == 2)

        matches = re.finditer("baz","barbazbarbazbar", re.L)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertTrue(num == 2)

        matches = re.compile("baz").finditer("barbazbarbazbar", 0)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertTrue(num == 2)

        matches = re.compile("baz").finditer("barbazbarbazbar", 14)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertTrue(num == 0)

        matches = re.compile("baz").finditer("barbazbarbazbar", 0, 14)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertTrue(num == 2)

        matches = re.compile("baz").finditer("barbazbarbazbar", 9, 12)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertEqual(num, 1)

        matches = re.compile("baz").finditer("barbazbarbazbar", 9, 11)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertEqual(num, 0)

        matches = re.compile("baz").finditer("barbazbarbazbar", 10, 12)
        num = 0
        for m in matches:
            num = num + 1
            self.assertEqual("baz", m.group(0))
        self.assertEqual(num, 0)

    def test_search(self):
        # search
        sp = re.search('super', 'blahsupersuper').span()
        self.assertTrue(sp == (4, 9))

        sp = re.search('super', 'superblahsuper').span()
        self.assertTrue(sp == (0, 5))

        #re.search.group() index error

        self.assertEqual(re.search("z.*z", "az123za").group(),'z123z')
        self.assertEqual(re.search("z.*z", "az12za").group(),'z12z')
        self.assertEqual(re.search("z.*z", "azza").group(),'zz')
        self.assertEqual(re.search("z123p+z", "az123ppppppppppza").group(),'z123ppppppppppz')
        self.assertEqual(re.search("z123p+z", "az123pza").group(),'z123pz')
        self.assertEqual(re.search("z123p?z", "az123pza").group(),'z123pz')
        self.assertEqual(re.search("z123p?z", "az123za").group(),'z123z')

        self.assertEqual(re.search('b', 'abc').string, 'abc')

    def test_subn(self):
        # subn
        tup = re.subn("ab", "cd", "abababababab")
        self.assertTrue(tup == ('cdcdcdcdcdcd', 6))
        tup = re.subn("ab", "cd", "abababababab", 0)
        self.assertTrue(tup == ('cdcdcdcdcdcd', 6))
        tup = re.subn("ab", "cd", "abababababab", 1)
        self.assertTrue(tup == ('cdababababab', 1))
        tup = re.subn("ab", "cd", "abababababab", 10)
        self.assertTrue(tup == ('cdcdcdcdcdcd', 6))
        tup = re.subn("ababab", "cd", "ab", 10)
        self.assertTrue(tup == ('ab', 0))
        tup = re.subn("ababab", "cd", "ab")
        self.assertTrue(tup == ('ab', 0))

        tup = re.subn("(ab)*", "cd", "abababababab", 10)
        self.assertTrue(tup == ('cd', 1))
        tup = re.subn("(ab)?", "cd", "abababababab", 10)
        self.assertTrue(tup == ('cdcdcdcdcdcd', 6))

    def test_groups(self):
        reg = re.compile("\[(?P<header>.*?)\]")
        m = reg.search("[DEFAULT]")
        self.assertTrue( m.groups() == ('DEFAULT',))
        self.assertTrue( m.group('header') == 'DEFAULT' )

        reg2 = re.compile("(?P<grp>\S+)?")
        m2 = reg2.search("")
        self.assertTrue ( m2.groups() == (None,))
        self.assertTrue ( m2.groups('Default') == ('Default',))

    def test_locale_flags(self):
        self.assertEqual(re.compile(r"^\#[ \t]*(\w[\d\w]*)[ \t](.*)").flags, 0 if is_cli else re.U)
        self.assertEqual(re.compile(r"^\#[ \t]*(\w[\d\w]*)[ \t](.*)", re.L).flags, re.L | (0 if is_cli else re.U))
        self.assertEqual(re.compile(r"(?L)^\#[ \t]*(\w[\d\w]*)[ \t](.*)").flags, re.L | (0 if is_cli else re.U))

    def test_end(self):
        ex = re.compile(r'\s+')
        m = ex.match('(object Petal', 7)
        self.assertTrue (m.end(0) == 8)

    def test_lone_hat(self):
        """Single ^ reg-ex shouldn't match w/ a sub-set of a string"""
        sol = re.compile('^')
        self.assertEqual(sol.match('bazbar', 1, 2), None)

        self.assertEqual(sol.match('foobar', 1, 2), None)

    def test_escape_backslash(self):
        x = re.compile (r"[\\A-Z\.\+]")
        self.assertEqual(x.search('aaaA\\B\\Caaa').span(), (3,4))

    def test_eol(self):
        r = re.compile(r'<(/|\Z)')
        s = r.search("<", 0)
        self.assertTrue(s != None)
        self.assertEqual(s.span(), (0, 1))
        self.assertEqual(s.group(0), '<')
        self.assertEqual(r.search("<Z", 0), None)

    def test_lastindex(self):
        for (pat, index) in [
                ('(a)b', 1), ('((a)(b))', 1), ('((ab))', 1),
                ('(a)(b)', 2),
                ('(a)?ab', None),
                ('(a)?b', 1),
                ]:
            self.assertEqual(re.match(pat, 'ab').lastindex, index)

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
            self.assertEqual(re.match(pat, 'aab').lastindex, index)

    def test_empty_split(self):
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
            self.assertEqual(re.split(":*", expr), result)

    def test_cp15298(self):
        regex = "^" + "\d\.\d\.\d \(IronPython \d\.\d(\.\d)? ((Alpha )|(Beta )|())\(\d\.\d\.\d\.\d{3,4}\) on \.NET \d(\.\d{1,5}){3}\)" * 15 + "$"
        match_str = "2.5.0 (IronPython 2.0 Beta (2.0.0.1000) on .NET 2.0.50727.1433)" * 15
        compiled_regex = re.compile(regex)

        retval = compiled_regex.match(match_str)
        self.assertTrue(retval != None)

        retval = re.match(regex, match_str)
        self.assertTrue(retval != None)

    def test_cp11136(self):
        regex = re.compile(r"^(?P<msg>NMAKE[A-Za-z0-9]*)'\"?(?P<file>[\\A-Za-z0-9/:_\.\+]+)" )
        self.assertTrue(regex.search(r"NMAKE0119'adirectory\afile.txt")!=None)

    def test_cp17111(self):
        test_cases = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#%&_+-=]{};':,.//<>" + '"'
        for x in test_cases:
            regex = re.compile(r".*\\%s" % x)
            self.assertTrue(regex.search(r"\\%s" % x)!=None)
            self.assertTrue(regex.search(r"")==None)

    def test_cp1089(self):
        test_cases = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#%&_+-=]{};':,.//<>" + '"'
        for x in test_cases:
            #Just make sure they don't throw
            temp = re.compile('\\\\' + x)

    def test_cp16657(self):
        self.assertTrue(re.compile(r'^bar', re.M).search('foo\nbar') != None)
        self.assertTrue(re.compile(r'^bar(?m)').search('foo\nbar') != None)
        self.assertTrue(re.compile(r'^bar', re.M).search('foo\nbaar') == None)
        self.assertTrue(re.compile(r'^bar(?m)').search('foo\nbaar') == None)

        self.assertTrue(re.compile(r'^bar', re.U).search('bar') != None)
        self.assertTrue(re.compile(r'^bar(?u)').search('bar') != None)
        self.assertTrue(re.compile(r'^bar', re.U).search('baar') == None)
        self.assertTrue(re.compile(r'^bar(?u)').search('baar') == None)

        self.assertTrue(re.compile(r'     b ar   ', re.X).search('bar') != None)
        self.assertTrue(re.compile(r'b ar(?x)').search('bar') != None)
        self.assertTrue(re.compile(r'     b ar   ', re.X).search('baar') == None)
        self.assertTrue(re.compile(r'b ar(?x)').search('baar') == None)
        self.assertTrue(re.compile(r'b ar').search('bar') == None)

    def test_n_m_quantifier(self):
        self.assertEqual(re.search('ab{,2}a', 'abba').span(), (0, 4))
        self.assertEqual(re.search('ab{,2}a', 'aba').span(), (0, 3))
        self.assertEqual(re.search('ab{,2}a', 'abbba'), None)
        self.assertEqual(re.search('ab{,2}a', 'abba').span(), re.search('ab{0,2}a', 'abba').span())
        self.assertEqual(re.search('ab{0,2}a', 'abbba'), None)
        self.assertEqual(re.search('ab{2,}a', 'abba').span(), (0,4))
        self.assertEqual(re.search('ab{2,}a', 'abbba').span(), (0,5))
        self.assertEqual(re.search('ab{2,}a', 'aba'), None)

    def test_mixed_named_and_unnamed_groups(self):
        example1=r"(?P<one>Blah)"
        example2=r"(?P<one>(Blah))"
        RegExsToTest=[example1,example2]

        for regString in RegExsToTest:
            g=re.compile(regString)
            self.assertEqual(g.groupindex, {'one' : 1})

    def test__pickle(self):
        '''
        TODO: just a sanity test for now.  Needs far more testing.
        '''
        regex = re.compile(r"^(?P<msg>NMAKE[A-Za-z0-9]*)'\"?(?P<file>[\\A-Za-z0-9/:_\.\+]+)" )
        pickled_regex = re._pickle(regex)
        self.assertEqual(len(pickled_regex), 2)
        self.assertEqual(pickled_regex[1],
                ('^(?P<msg>NMAKE[A-Za-z0-9]*)\'\\"?(?P<file>[\\\\A-Za-z0-9/:_\\.\\+]+)', 0 if is_cli else re.U))

    def test_conditional(self):
        p = re.compile(r'(a)?(b)((?(1)c))')
        self.assertEqual(p.match('abc').groups(), ('a', 'b', 'c'))
        p = re.compile(r'(?P<first>a)?(b)((?(first)c))')
        self.assertEqual(p.match('abc').groups(), ('a', 'b', 'c'))
        s = r'((?(a)ab|cd))'
        if is_cli:
            p = re.compile(s)
            self.assertEqual(p.match('ab').groups(), ('ab',))
        else:
            self.assertRaises(re.error, re.compile, s)

    def test_cp35146(self):
        # re.compile returns cached instances
        self.assertEqual(re.compile('cp35146'), re.compile('cp35146'))

    def test_cp35135(self):
        self.assertEqual(re.match(r"(?iu)aA", "aa").string, "aa")
        self.assertEqual(re.match(r"(?iu)Aa", "aa").string, "aa")
        self.assertEqual(re.match(r"(?iLmsux)Aa", "aa").string, "aa")

    def test_issue506(self):
        self.assertEqual(re.compile("^a", re.M).search("ba", 1), None)

    def test_issue1370(self):
        self.assertEqual(re.compile("\Z").match("\n"), None)
        self.assertEqual(re.compile("\Z").match("").group(0), "")

    def test_gh21(self):
        """https://github.com/IronLanguages/ironpython2/issues/21"""
        self.assertRaisesMessage(re.error, "redefinition of group name 'hoge' as group 2; was group 1", re.compile, r'(?P<hoge>\w+):(?P<hoge>\w+)')
        self.assertRaisesMessage(re.error, "redefinition of group name 'hoge' as group 3; was group 2", re.compile, r'(abc)(?P<hoge>\w+):(?P<hoge>\w+)')
        self.assertRaisesMessage(re.error, "redefinition of group name 'hoge' as group 4; was group 2", re.compile, r'(abc)(?P<hoge>\w+):(abc)(?P<hoge>\w+)')

run_test(__name__)
