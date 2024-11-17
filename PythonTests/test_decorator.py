# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import run_test

class dn:
    def d1(self, a):
        a.append("dn.d1")
        return a

    def d2(self, a):
        a.append("dn.d2")
        return a

    def d3(self, a):
        a.append("dn.d3")
        return a

    def d4(self, a):
        a.append("dn.d4")
        return a

    def d5(self, a):
        a.append("dn.d5")
        return a

    def first(self, f):
        return ["dn.first"]

def d1(a):
    a.append("d1")
    return a

def d2(a):
    a.append("d2")
    return a

def d3(a):
    a.append("d3")
    return a

def d4(a):
    a.append("d4")
    return a

def d5(a):
    a.append("d5")
    return a

def first(f):
    return ["first"]


class DecoratorTest(unittest.TestCase):
    def test_simple(self):
        def trick(p):
            return "trick"

        @trick
        def f():
            pass

        self.assertEqual(f, "trick")

    def test_class_as_decorator(self):
        class wrap:
            def __init__(self, fnc):
                self.fnc = fnc
            def __call__(self):
                return "wrapped"

        @wrap
        def f():
            pass
        self.assertTrue(isinstance(f, wrap))
        self.assertEqual(f(), "wrapped")

    def test_parameters(self):
        class eat:
            def __call__(self, fnc):
                return self

        def parm(a,b,c="default c", d="default d"):
            e = eat()
            e.args = a,b,c,d
            return e

        @parm(1,2)
        def f():
            pass

        self.assertTrue(isinstance(f, eat))
        self.assertEqual(f.args, (1,2,"default c", "default d"))

        @parm(1,2,"new c")
        def f():
            pass

        self.assertTrue(isinstance(f, eat))
        self.assertEqual(f.args, (1,2,"new c", "default d"))

    def test_execution_order(self):
        @d1
        @d2
        @d3
        @d4
        @d5
        @first
        def f():
            return 10

        self.assertEqual(f, ["first", "d5", "d4", "d3", "d2", "d1"])

        # More complicated cases

        class capture:
            def __init__(self, *args):
                self.args = args
            def __call__(self, f):
                f.append(self.args)
                return f

        @capture(1,2)
        @d3
        @capture(3,4,5)
        @d4
        @capture("Hello")
        @d5
        @capture("First")
        @first
        def f():
            pass

        self.assertEqual(f, ['first', ('First',), 'd5', ('Hello',), 'd4', (3, 4, 5), 'd3', (1, 2)])

    def test_dotted_names(self):
        x = dn()

        @x.d1
        @d2
        @x.d3
        @d4
        @x.d5
        @first
        def f():
            return 10

        self.assertEqual(f, ['first', 'dn.d5', 'd4', 'dn.d3', 'd2', 'dn.d1'])

run_test(__name__)