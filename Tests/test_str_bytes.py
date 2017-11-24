# -*- coding: utf-8 -*-
#####################################################################################
#
#  Copyright (c) Pawel Jasinski. All rights reserved.
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
## Test str/byte equivalence for built-in string methods
##
## Please, Note:
## i) All commented test cases are for bytes/extensible string combination
## ii) For version 3.x the str/byte mixing below become "Raises" test cases
##
##

import sys
import unittest

from iptest import run_test

class ExtensibleStringClass(str):
    pass

esa = ExtensibleStringClass("a")
esb = ExtensibleStringClass("b")
esc = ExtensibleStringClass("c")
esx = ExtensibleStringClass("x")

class StrBytesTest(unittest.TestCase):

    def test_contains(self):
        self.assertTrue(esa.__contains__("a"))
        self.assertTrue(esa.__contains__(b"a"))
        self.assertTrue(esa.__contains__(esa))
        self.assertTrue("a".__contains__("a"))
        self.assertTrue("a".__contains__(b"a"))
        self.assertTrue("a".__contains__(esa))
        self.assertTrue(b"a".__contains__("a"))
        self.assertTrue(b"a".__contains__(b"a"))
        self.assertTrue(b"a".__contains__(esa))

    def test_format(self):
        self.assertEqual("%s" % b"a", "a")
        # self.assertEqual(b"%s" % b"a", b"a")
        # self.assertEqual("%s" % b"a", b"%s" % "a")

    def test_count(self):
        self.assertEqual("aa".count(b"a"), 2)
        self.assertEqual("aa".count(b"a", 0), 2)
        self.assertEqual("aa".count(b"a", 0, 1), 1)

        self.assertEqual("aa".count(esa), 2)
        self.assertEqual("aa".count(esa, 0), 2)
        self.assertEqual("aa".count(esa, 0, 1), 1)

        self.assertEqual(b"aa".count("a"), 2)
        self.assertEqual(b"aa".count("a", 0), 2)
        self.assertEqual(b"aa".count("a", 0, 1), 1)

        # self.assertEqual(b"aa".count(esa), 2)
        # self.assertEqual(b"aa".count(esa, 0), 2)
        # self.assertEqual(b"aa".count(esa, 0, 1), 1)


    def test_find(self):
        self.assertTrue("abc".find(b"b"))
        self.assertTrue("abc".find(b"b", 1))
        self.assertTrue("abc".find(b"b", 1, 2))
        self.assertTrue("abc".find(b"b", 1L))
        self.assertTrue("abc".find(b"b", 1L, 2L))

        self.assertTrue("abc".find(esb))
        self.assertTrue("abc".find(esb, 1))
        self.assertTrue("abc".find(esb, 1, 2))
        self.assertTrue("abc".find(esb, 1L))
        self.assertTrue("abc".find(esb, 1L, 2L))

        self.assertTrue(b"abc".find("b"))
        self.assertTrue(b"abc".find("b", 1))
        self.assertTrue(b"abc".find("b", 1, 2))

        # self.assertTrue(b"abc".find(esb))
        # self.assertTrue(b"abc".find(esb, 1))
        # self.assertTrue(b"abc".find(esb, 1, 2))

    def test_lstrip(self):
        self.assertEqual("xa".lstrip(b"x"), "a")
        self.assertEqual("xa".lstrip(esx), "a")
        self.assertEqual(b"xa".lstrip("x"), b"a")
        # self.assertEqual(b"xa".lstrip(esx), b"a")

    def test_partition(self):
        self.assertEqual("abc".partition(b"b"), ("a", "b", "c"))
        self.assertEqual("abc".partition(esb), ("a", "b", "c"))
        self.assertEqual(b"abc".partition("b"), (b"a", b"b", b"c"))
        # self.assertEqual(b"abc".partition(esb), (b"a", b"b", b"c"))

    def test_replace(self):
        self.assertEqual("abc".replace(b"a", "x"), "xbc")
        self.assertEqual("abc".replace(b"a", b"x"), "xbc")
        self.assertEqual("abc".replace("a", b"x"), "xbc")
        self.assertEqual("abc".replace(b"a", "x", 1), "xbc")
        self.assertEqual("abc".replace(b"a", b"x", 1), "xbc")
        self.assertEqual("abc".replace("a", b"x", 1), "xbc")

        self.assertEqual("abc".replace(b"a", buffer("x")), "xbc")
        self.assertEqual("abc".replace(buffer("a"), "x"), "xbc")
        self.assertEqual("abc".replace(buffer("a"), buffer("x")), "xbc")
        self.assertEqual("abc".replace(b"a", bytearray(b"x")), "xbc")
        self.assertEqual("abc".replace(bytearray(b"a"), "x"), "xbc")
        self.assertEqual("abc".replace(bytearray(b"a"), bytearray(b"x")), "xbc")

        self.assertEqual("abc".replace("a", esx), "xbc")
        self.assertEqual("abc".replace(b"a", esx), "xbc")
        self.assertEqual("abc".replace(esa, esx), "xbc")
        self.assertEqual("abc".replace(esa, b"x"), "xbc")

        self.assertEqual("abc".replace("a", esx, 1), "xbc")
        self.assertEqual("abc".replace(b"a", esx, 1), "xbc")
        self.assertEqual("abc".replace(esa, esx, 1), "xbc")
        self.assertEqual("abc".replace("a", esx, 1), "xbc")

        self.assertEqual(b"abc".replace(b"a", "x"), "xbc")
        self.assertEqual(b"abc".replace("a", "x"), "xbc")
        self.assertEqual(b"abc".replace("a", b"x"), "xbc")
        self.assertEqual(b"abc".replace(b"a", "x", 1), "xbc")
        self.assertEqual(b"abc".replace("a", "x", 1), "xbc")
        self.assertEqual(b"abc".replace("a", b"x", 1), "xbc")

        # self.assertEqual(b"abc".replace("a", esx), "xbc")
        # self.assertEqual(b"abc".replace(b"a", esx), "xbc")
        # self.assertEqual(b"abc".replace(esa, esx), "xbc")
        # self.assertEqual(b"abc".replace(esa, b"x"), "xbc")

        # self.assertEqual(b"abc".replace("a", esx, 1), "xbc")
        # self.assertEqual(b"abc".replace(b"a", esx, 1), "xbc")
        # self.assertEqual(b"abc".replace(esa, esx, 1), "xbc")
        # self.assertEqual(b"abc".replace("a", esx, 1), "xbc")


    def test_rfind(self):
        self.assertEqual("abc".rfind(b"c"), 2)
        self.assertEqual("abc".rfind(b"c", 1), 2)
        self.assertEqual("abc".rfind(b"c", 1, 3), 2)
        self.assertEqual("abc".rfind(b"c", 1L), 2)
        self.assertEqual("abc".rfind(b"c", 1L, 3L), 2)

        self.assertEqual("abc".rfind(esc), 2)
        self.assertEqual("abc".rfind(esc, 1), 2)
        self.assertEqual("abc".rfind(esc, 1, 3), 2)
        self.assertEqual("abc".rfind(esc, 1L), 2)
        self.assertEqual("abc".rfind(esc, 1L, 3L), 2)

        self.assertEqual(b"abc".rfind("c"), 2)
        self.assertEqual(b"abc".rfind("c", 1), 2)
        self.assertEqual(b"abc".rfind("c", 1, 3), 2)

        # self.assertEqual(b"abc".rfind(esc), 2)
        # self.assertEqual(b"abc".rfind(esc, 1), 2)
        # self.assertEqual(b"abc".rfind(esc, 1, 3), 2)


    def test_rindex(self):
        self.assertEqual("abc".rindex(b"c"), 2)
        self.assertEqual("abc".rindex(b"c", 1), 2)
        self.assertEqual("abc".rindex(b"c", 1, 3), 2)
        self.assertEqual("abc".rindex(b"c", 1L), 2)
        self.assertEqual("abc".rindex(b"c", 1L, 3L), 2)

        self.assertEqual("abc".rindex(esc), 2)
        self.assertEqual("abc".rindex(esc, 1), 2)
        self.assertEqual("abc".rindex(esc, 1, 3), 2)
        self.assertEqual("abc".rindex(esc, 1L), 2)
        self.assertEqual("abc".rindex(esc, 1L, 3L), 2)

        self.assertEqual(b"abc".rindex("c"), 2)
        self.assertEqual(b"abc".rindex("c", 1), 2)
        self.assertEqual(b"abc".rindex("c", 1, 3), 2)

        # self.assertEqual(b"abc".rindex(esc), 2)
        # self.assertEqual(b"abc".rindex(esc, 1), 2)
        # self.assertEqual(b"abc".rindex(esc, 1, 3), 2)

    def test_rpartition(self):
        self.assertEqual("abc".rpartition(b"b"), ("a", "b", "c"))
        self.assertEqual("abc".rpartition(esb), ("a", "b", "c"))
        self.assertEqual(b"abc".rpartition("b"), (b"a", b"b", b"c"))
        # self.assertEqual(b"abc".rpartition(esb), (b"a", b"b", b"c"))

    def test_rsplit(self):
        self.assertEqual("abc".rsplit(b"b"), ["a", "c"])
        self.assertEqual("abc".rsplit(b"b", 1), ["a", "c"])
        self.assertEqual("abc".rsplit(esb), ["a", "c"])
        self.assertEqual("abc".rsplit(esb, 1), ["a", "c"])
        self.assertEqual(b"abc".rsplit("b"), [b"a", b"c"])
        self.assertEqual(b"abc".rsplit("b", 1), [b"a", b"c"])
        # self.assertEqual(b"abc".rsplit(esb), [b"a", b"c"])
        # self.assertEqual(b"abc".rsplit(esb, 1), [b"a", b"c"])

    def test_rstrip(self):
        self.assertEqual("ax".rstrip(b"x"), "a")
        self.assertEqual("ax".rstrip(esx), "a")
        self.assertEqual(b"ax".rstrip("x"), b"a")
        # self.assertEqual(b"ax".rstrip(esx), b"a")

    def test_split(self):
        self.assertEqual("abc".split(b"b"), ["a", "c"])
        self.assertEqual("abc".split(b"b", 1), ["a", "c"])
        self.assertEqual("abc".split(esb), ["a", "c"])
        self.assertEqual("abc".split(esb, 1), ["a", "c"])
        self.assertEqual(b"abc".split("b"), [b"a", b"c"])
        self.assertEqual(b"abc".split("b", 1), [b"a", b"c"])
        # self.assertEqual(b"abc".split(esb), [b"a", b"c"])
        # self.assertEqual(b"abc".split(esb, 1), [b"a", b"c"])

    def test_strip(self):
        self.assertEqual("xax".strip(b"x"), "a")
        self.assertEqual("xax".strip(esx), "a")
        self.assertEqual(b"xax".strip("x"), b"a")
        # self.assertEqual(b"xax".strip(esx), b"a")

    def test_startswith(self):
        self.assertTrue("abc".startswith(b"a"))
        self.assertTrue("abc".startswith(b"a", 0))
        self.assertTrue("abc".startswith(b"a", 0, 1))
        self.assertTrue("abc".startswith(esa))
        self.assertTrue("abc".startswith(esa, 0))
        self.assertTrue("abc".startswith(esa, 0, 1))
        self.assertTrue(b"abc".startswith("a"))
        self.assertTrue(b"abc".startswith("a", 0))
        self.assertTrue(b"abc".startswith("a", 0, 1))
        # self.assertTrue(b"abc".startswith(esa))
        # self.assertTrue(b"abc".startswith(esa, 0))
        # self.assertTrue(b"abc".startswith(esa, 0, 1))


    def test_endswith(self):
        self.assertTrue("abc".endswith(b"c"))
        self.assertTrue("abc".endswith(b"c", 0))
        self.assertTrue("abc".endswith(b"c", 0, 3))
        self.assertTrue("abc".endswith(esc))
        self.assertTrue("abc".endswith(esc, 0))
        self.assertTrue("abc".endswith(esc, 0, 3))
        self.assertTrue(b"abc".endswith("c"))
        self.assertTrue(b"abc".endswith("c", 0))
        self.assertTrue(b"abc".endswith("c", 0, 3))
        # self.assertTrue(b"abc".endswith(esc))
        # self.assertTrue(b"abc".endswith(esc, 0))
        # self.assertTrue(b"abc".endswith(esc, 0, 3))

    def test_join(self):
        self.assertEqual("abc", "b".join([b"a", b"c"]))
        self.assertEqual("b", "a".join([b"b"]))
        self.assertEqual("abc", "b".join([esa, esc]))
        self.assertEqual("b", "a".join([esb]))
        self.assertEqual(b"abc", b"b".join(["a", "c"]))
        self.assertEqual(b"b", b"a".join(["b"]))
        #self.assertEqual(b"abc", b"b".join([esb, esc]))
        # self.assertEqual(b"b", b"a".join([esb]))


run_test(__name__)
