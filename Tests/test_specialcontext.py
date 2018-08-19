# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import run_test, skipUnlessIronPython
x = None
@skipUnlessIronPython()
class SpecialContextTest(unittest.TestCase):
    def test_special_context(self):
        # our built in types shouldn't show CLS methods

        self.assertEqual(hasattr(object, 'ToString'), False)
        self.assertEqual(dir(object).count('ToString'), 0)
        self.assertEqual(vars(object).keys().count('ToString'), 0)

        self.assertEqual(hasattr('abc', 'ToString'), False)
        self.assertEqual(dir('abc').count('ToString'), 0)
        self.assertEqual(vars(str).keys().count('ToString'), 0)

        self.assertEqual(hasattr([], 'ToString'), False)
        self.assertEqual(dir([]).count('ToString'), 0)
        self.assertEqual(vars(list).keys().count('ToString'), 0)

        import System

        # but CLS types w/o the attribute should....
        self.assertEqual(hasattr(System.Environment, 'ToString'), True)
        self.assertEqual(dir(System.Environment).count('ToString'), 1)
        # vars only shows members declared in the type, so it won't be there either
        self.assertEqual(vars(System.Environment).keys().count('ToString'), 0)

        # and importing clr should show them all...
        import clr

        self.assertEqual(hasattr(object, 'ToString'), True)
        self.assertEqual(dir(object).count('ToString'), 1)
        self.assertEqual(vars(object).keys().count('ToString'), 1)

        self.assertEqual(hasattr('abc', 'ToString'), True)
        self.assertEqual(dir('abc').count('ToString'), 1)
        self.assertEqual(vars(str).keys().count('ToString'), 1) # string overrides ToString

        self.assertEqual(hasattr([], 'ToString'), True)
        self.assertEqual(dir([]).count('ToString'), 1)
        self.assertEqual(vars(list).keys().count('ToString'), 0) # list doesn't override ToString

        # and they should still show up on system.
        self.assertEqual(hasattr(System.Environment, 'ToString'), True)
        self.assertEqual(dir(System.Environment).count('ToString'), 1)
        self.assertEqual(vars(System.Environment).keys().count('ToString'), 0)

def assertEqual(first, second):
    if first != second:
        raise AssertionError('assertion failed')


if __name__ == '__main__':
    run_test(__name__)

    # these have to be tested at the global scope
    a = "hello world"
    c = compile("x = a.Split(' ')", "<string>", "single")
    eval(c)
    assertEqual(x[0], "hello")
    assertEqual(x[1], "world")

    y = eval("a.Split(' ')")
    assertEqual(y[0], "hello")
    assertEqual(y[1], "world")

