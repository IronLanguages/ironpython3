# -*- coding: utf-8 -*-
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Pawel Jasinski
#

##
## Test str/byte equivalence for built-in string methods
##

import sys
import unittest
import itertools

from iptest import run_test

class ExtensibleStringClass(str):
    pass

long = type(sys.maxsize + 1)

class StrBytesTest(unittest.TestCase):
    def run_permutations(self, expr, inputs, str_eq, bytes_eq):
        for params in itertools.product(*((x, ExtensibleStringClass(x), bytes(x, "ascii"), bytearray(x, "ascii")) for x in inputs)):
            if all(not isinstance(z, str) for z in params):
                self.assertEqual(expr(*params), bytes_eq)
            elif any(not isinstance(z, str) for z in params):
                with self.assertRaises(TypeError):
                    expr(*params)
            else:
                self.assertEqual(expr(*params), str_eq)

    def test_contains(self):
        self.run_permutations(lambda a1, a2: a1.__contains__(a2), ("a", "a"), True, True)

    def test_format(self):
        self.assertEqual("%s" % b"a", "b'a'")
        # self.assertEqual(b"%s" % b"a", b"a")
        # self.assertEqual("%s" % b"a", b"%s" % "a")

    def test_count(self):
        self.run_permutations(lambda aa, a: aa.count(a), ("aa", "a"), 2, 2)
        self.run_permutations(lambda aa, a: aa.count(a, 0), ("aa", "a"), 2, 2)
        self.run_permutations(lambda aa, a: aa.count(a, 0, 1), ("aa", "a"), 1, 1)

    def test_find(self):
        self.run_permutations(lambda abc, b: abc.find(b), ("abc", "b"), 1, 1)
        self.run_permutations(lambda abc, b: abc.find(b, 1), ("abc", "b"), 1, 1)
        self.run_permutations(lambda abc, b: abc.find(b, 1, 2), ("abc", "b"), 1, 1)
        self.run_permutations(lambda abc, b: abc.find(b, long(1)), ("abc", "b"), 1, 1)
        self.run_permutations(lambda abc, b: abc.find(b, long(1), long(2)), ("abc", "b"), 1, 1)

    def test_lstrip(self):
        self.run_permutations(lambda xa, x: xa.lstrip(x), ("xa", "x"), "a", b"a")

    def test_partition(self):
        self.run_permutations(lambda abc, b: abc.partition(b), ("abc", "b"), ("a", "b", "c"), (b"a", b"b", b"c"))

    def test_replace(self):
        self.run_permutations(lambda abc, a, x: abc.replace(a, x), ("abc", "a", "x"), "xbc", b"xbc")
        self.run_permutations(lambda abc, a, x: abc.replace(a, x, 1), ("abc", "a", "x"), "xbc", b"xbc")

        self.assertEqual(b"abc".replace(b"a", memoryview(b"x")), b"xbc")
        self.assertEqual(b"abc".replace(memoryview(b"a"), b"x"), b"xbc")
        self.assertEqual(b"abc".replace(memoryview(b"a"), memoryview(b"x")), b"xbc")

        # str/bytes return the original object
        x = "abc"
        self.assertIs(x.replace("d", "e"), x)

        x = b"abc"
        self.assertIs(x.replace(b"d", b"e"), x)

    def test_rfind(self):
        self.run_permutations(lambda abc, c: abc.rfind(c), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rfind(c, 1), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rfind(c, 1, 3), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rfind(c, long(1)), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rfind(c, long(1), long(3)), ("abc", "c"), 2, 2)

    def test_rindex(self):
        self.run_permutations(lambda abc, c: abc.rindex(c), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rindex(c, 1), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rindex(c, 1, 3), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rindex(c, long(1)), ("abc", "c"), 2, 2)
        self.run_permutations(lambda abc, c: abc.rindex(c, long(1), long(3)), ("abc", "c"), 2, 2)

    def test_rpartition(self):
        self.run_permutations(lambda abc, b: abc.rpartition(b), ("abc", "b"), ("a", "b", "c"), (b"a", b"b", b"c"))

    def test_rsplit(self):
        self.run_permutations(lambda abc, b: abc.rsplit(b), ("abc", "b"), ["a", "c"], [b"a", b"c"])
        self.run_permutations(lambda abc, b: abc.rsplit(b, 1), ("abc", "b"), ["a", "c"], [b"a", b"c"])

    def test_rstrip(self):
        self.run_permutations(lambda ax, x: ax.rstrip(x), ("ax", "x"), "a", b"a")

    def test_split(self):
        self.run_permutations(lambda abc, b: abc.split(b), ("abc", "b"), ["a", "c"], [b"a", b"c"])
        self.run_permutations(lambda abc, b: abc.split(b, 1), ("abc", "b"), ["a", "c"], [b"a", b"c"])

    def test_strip(self):
        self.run_permutations(lambda xax, x: xax.strip(x), ("xax", "x"), "a", b"a")

    def test_startswith(self):
        self.run_permutations(lambda abc, a: abc.startswith(a), ("abc", "a"), True, True)
        self.run_permutations(lambda abc, a: abc.startswith(a, 0), ("abc", "a"), True, True)
        self.run_permutations(lambda abc, a: abc.startswith(a, 0, 1), ("abc", "a"), True, True)

    def test_endswith(self):
        self.run_permutations(lambda abc, c: abc.endswith(c), ("abc", "c"), True, True)
        self.run_permutations(lambda abc, c: abc.endswith(c, 0), ("abc", "c"), True, True)
        self.run_permutations(lambda abc, c: abc.endswith(c, 0, 3), ("abc", "c"), True, True)

    def test_join(self):
        self.run_permutations(lambda b, a, c: b.join([a, c]), ("b", "a", "c"), "abc", b"abc")
        self.run_permutations(lambda a, b: a.join([b]), ("a", "b"), "b", b"b")

run_test(__name__)
