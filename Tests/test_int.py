# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import is_cli, big, myint, skipUnlessIronPython

class IntTest(unittest.TestCase):
    def test_from_bytes(self):
        self.assertEqual(type(int.from_bytes(b"abc", "big")), int)
        self.assertEqual(type(myint.from_bytes(b"abc", "big")), myint) # https://github.com/IronLanguages/ironpython3/pull/973

    def test_int(self):
        class MyTrunc:
            def __init__(self, x):
                self.x = x
            def __trunc__(self):
                return self.x

        class MyInt:
            def __init__(self, x):
                self.x = x
            def __int__(self):
                return self.x

        for value in ("0", b"0"):
            # int(str/bytes)
            v = int(value)
            self.assertEqual(v, 0)
            self.assertIs(type(v), int)

        for t in (bool, int, myint):
            # int()
            v = int(t(0))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int)

            # __trunc__
            v = int(MyTrunc(t(0)))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int if is_cli or sys.version_info >= (3,6) else t)

            # __int__
            if t in (bool, myint):
                with self.assertWarns(DeprecationWarning):
                    v = int(MyInt(t(0)))
            else:
                v = int(MyInt(t(0)))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int if is_cli or sys.version_info >= (3,6) else t)

        if is_cli:
            from iptest import clr_all_types
            for t in clr_all_types:
                # int(System.*)
                v = int(t(0))
                self.assertEqual(v, 0)
                self.assertIs(type(v), int)

    @skipUnlessIronPython()
    def test_type_name(self):
        import System

        i = 1
        j = big(1)

        self.assertEqual(int.__name__, "int")
        self.assertEqual(System.Numerics.BigInteger.__name__, "int")
        self.assertEqual(System.Int32.__name__, "Int32")
        self.assertEqual(System.Int64.__name__, "Int64")

        self.assertEqual(i.__class__.__name__, "int")
        self.assertEqual(j.__class__.__name__, "int")

    def test_type_repr(self):
        i = 1
        j = big(1)

        self.assertEqual(repr(type(i)), repr(int))
        self.assertEqual(repr(type(j)), repr(int))
        self.assertEqual(repr(type(i)), repr(type(j)))

        self.assertEqual(str(type(i)), str(int))
        self.assertEqual(str(type(j)), str(int))
        self.assertEqual(str(type(i)), str(type(j)))

        self.assertEqual(str(i.__class__), "<class 'int'>")
        self.assertEqual(str(j.__class__), "<class 'int'>")

    def test_type_set(self):
        i = 1
        j = big(1)
        k = myint(1)

        self.assertSetEqual({int, type(i), type(j)}, {int})
        self.assertSetEqual({int, type(i), type(j), type(k)}, {int, myint})
        self.assertEqual(len({int, myint}), 2)

        self.assertSetEqual({i, j, k}, {i})
        self.assertSetEqual({i, j, k}, {j})
        self.assertSetEqual({i, j, k}, {k})
        self.assertSequenceEqual({i : 'i', j : 'j', k : 'k'}.keys(), {i})
        self.assertSequenceEqual({i : 'i', j : 'j', k : 'k'}.keys(), {j})
        self.assertSequenceEqual({i : 'i', j : 'j', k : 'k'}.keys(), {k})

if __name__ == "__main__":
    unittest.main()
