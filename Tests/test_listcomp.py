# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import run_test, is_cli

class ListCompTest(unittest.TestCase):
    def test_positive(self):
        self.assertEqual([x for x in ""], [])
        self.assertEqual([x for x in range(2)], [0, 1])
        self.assertEqual([x + 10 for x in [-11, 4]], [-1, 14])
        self.assertEqual([x for x in [y for y in range(3)]], [0, 1, 2])
        self.assertEqual([x for x in range(3) if x > 1], [2])
        self.assertEqual([x for x in range(10) if x > 1 if x < 4], [2, 3])
        self.assertEqual([x for x in range(30) for y in range(2) if x > 1 if x < 4], [2, 2, 3, 3])
        self.assertEqual([(x,y) for x in range(30) for y in range(3) if x > 1 if x < 4 if y > 1], [(2, 2), (3, 2)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 for y in range(3) if x < 4 if y > 1], [(2, 2), (3, 2)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 if x < 4 for y in range(3) if y > 1], [(2, 2), (3, 2)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 for y in range(5) if x < 4 if y > x], [(2, 3), (2, 4), (3, 4)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 for y in range(5) if y > x if x < 4], [(2, 3), (2, 4), (3, 4)])
        self.assertEqual([(y, x) for (x, y) in ((1, 2), (2, 4))], [(2, 1), (4, 2)])
        y = 10
        self.assertEqual([y for x in "python"], [y] * 6)
        self.assertEqual([y for y in "python"], list("python"))
        y = 10
        self.assertEqual([x for x in "python" if y > 5], list("python"))
        self.assertEqual([x for x in "python" if y > 15], list())
        self.assertEqual([x for x, in [(1,)]], [1])

    def test_negative(self):
        self.assertRaises(SyntaxError, compile, "[x if x > 1 for x in range(3)]", "", "eval")
        self.assertRaises(SyntaxError, compile, "[x for x in range(3);]", "", "eval")
        self.assertRaises(SyntaxError, compile, "[x for x in range(3) for y]", "", "eval")

        self.assertRaises(NameError, lambda: [z for x in "python"])
        self.assertRaises(NameError, lambda: [x for x in "python" if z > 5])
        self.assertRaises(NameError, lambda: [x for x in "iron" if z > x for z in "python" ])
        self.assertRaises(NameError, lambda: [x for x in "iron" if never_shown_before > x ])
        self.assertRaises(NameError, lambda: [(x, z) for x in "iron" if z > x for z in "python" ])
        self.assertRaises(NameError, lambda: [(i, j) for i in range(10) if j < 'c' for j in ['a', 'b', 'c'] if i % 3 == 0])

    def test_ipy3_gh809(self):
        """https://github.com/IronLanguages/ironpython3/issues/809"""

        # iterable is evaluated in the outer scope
        self.assertIn('self', [x for x in dir()])

        # this rule applies recursively to nested comprehensions
        self.assertIn('self', [x for x in [y for y in dir()]])
        self.assertIn('self', [x for x in [y for y in [z for z in dir()]]])

        # this only applies to the first iterable
        # subsequent iterables are evaluated within the comprehension scope
        self.assertEqual([(0, 'x')], [(x, y) for x in range(1) for y in dir() if not y.startswith('.')]) # (filtering out auxiliary variable staring with a dot, used by CPython)

        # also subsequent conditions are evaluated within the comprehension scope
        a, b, c, d = range(4)
        self.assertTrue(len(dir()) >= 4)
        self.assertEqual([], [dir() for x in range(1) if len(dir()) >= 4])

        # subsequent iterables introduce local variables after the first iteration
        self.assertEqual([(0, 'x'), (1, 'x'), (1, 'y'), (2, 'x'), (2, 'y')],
                         [(x, y) for x in range(3)
                                 for y in dir() if not y.startswith('.')])
        self.assertEqual([(0, 'x', 'x'), (0, 'x', 'y'), 
                          (1, 'x', 'x'), (1, 'x', 'y'), (1, 'x', 'z'), 
                          (1, 'y', 'x'), (1, 'y', 'y'), (1, 'y', 'z'), 
                          (1, 'z', 'x'), (1, 'z', 'y'), (1, 'z', 'z')],
                         [(x, y, z) for x in range(2)
                                    for y in dir() if not y.startswith('.')
                                    for z in dir() if not z.startswith('.')])

        # lambdas create a new scope
        self.assertEqual([], [x for x in (lambda: dir())()])
        self.assertEqual([[]], [x() for x in [lambda: dir()]])

        # first iterable is captured and subsequent assignments do not change it
        self.maxDiff = None
        x, y, z = range(1), range(2), range(3)
        if is_cli:
            _dir = ['x', 'y']
            # TODO: should be: _dir = ['x', 'y', 'z']
            # See: https://github.com/IronLanguages/ironpython3/issues/1132
        else:
            _dir = ['.0', 'x', 'y', 'z'] # adds implementation-level variable '.0'
        # below, x and first/third y is local, second y and z is from the outer scope
        self.assertEqual([(x, y, z, dir()) for x in y for y in z],
                         [(0, 0, range(3), _dir),
                          (0, 1, range(3), _dir),
                          (0, 2, range(3), _dir),
                          (1, 0, range(3), _dir),
                          (1, 1, range(3), _dir),
                          (1, 2, range(3), _dir)])

        # mixing scopes
        def apply(f, i): return f(i)
        x = 2
        res = [x for x in apply(lambda i: range(i+x), x)]
        self.assertEqual(res, [0, 1, 2, 3])

        res = [(x, y) for x in apply(lambda i: range(i+x), x) for y in apply(lambda i: range(i+x//2), x)]
        self.assertEqual(res, [(1, 0), (2, 0), (2, 1), (2, 2), (3, 0), (3, 1), (3, 2), (3, 3)])

        res = [x for x in [y for y in apply(lambda i: range(i+x), x)]]
        self.assertEqual(res, [0, 1, 2, 3])

run_test(__name__)
