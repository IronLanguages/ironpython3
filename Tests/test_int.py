# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import is_cli

class IntSubclass(int): pass

class IntTest(unittest.TestCase):
    def test_from_bytes(self):
        self.assertEqual(type(int.from_bytes(b"abc", "big")), int)
        self.assertEqual(type(IntSubclass.from_bytes(b"abc", "big")), IntSubclass) # https://github.com/IronLanguages/ironpython3/pull/973

    def test_int(self):
        from iptest import long, myint, mylong

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

        for t in (bool, int, long, myint, mylong):
            # int()
            v = int(t(0))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int)

            # __trunc__
            v = int(MyTrunc(t(0)))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int if is_cli or sys.version_info >= (3,6) else t)

            # __int__
            if t in (bool, myint, mylong):
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

if __name__ == "__main__":
    unittest.main()
