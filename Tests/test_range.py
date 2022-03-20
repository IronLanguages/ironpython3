# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test range
##
## * sbs_builtin\test_xrange covers many range corner cases
##

import sys
import unittest

from iptest import is_cli, run_test

if is_cli:
    from System import Int64

class RangeTest(unittest.TestCase):

    def test_range(self):
        self.assertTrue(list(range(10)) == [0, 1, 2, 3, 4, 5, 6, 7, 8, 9])
        self.assertTrue(list(range(0)) == [])
        self.assertTrue(list(range(-10)) == [])

        self.assertTrue(list(range(3,10)) == [3, 4, 5, 6, 7, 8, 9])
        self.assertTrue(list(range(10,3)) == [])
        self.assertTrue(list(range(-3,-10)) == [])
        self.assertTrue(list(range(-10,-3)) == [-10, -9, -8, -7, -6, -5, -4])

        self.assertTrue(list(range(3,20,2)) == [3, 5, 7, 9, 11, 13, 15, 17, 19])
        self.assertTrue(list(range(3,20,-2)) == [])
        self.assertTrue(list(range(20,3,2)) == [])
        self.assertTrue(list(range(20,3,-2)) == [20, 18, 16, 14, 12, 10, 8, 6, 4])
        self.assertTrue(list(range(-3,-20,2)) == [])
        self.assertTrue(list(range(-3,-20,-2)) == [-3, -5, -7, -9, -11, -13, -15, -17, -19])
        self.assertTrue(list(range(-20,-3, 2)) == [-20, -18, -16, -14, -12, -10, -8, -6, -4])
        self.assertTrue(list(range(-20,-3,-2)) == [])

    def test_range_collections(self):
        self.assertTrue(range(0, 100, 2).count(10) == 1)
        self.assertTrue(range(0, 100, 2).index(10) == 5)
        self.assertTrue(range(0, 100, 2)[5] == 10)
        self.assertTrue(str(range(0, 100, 2)[0:5]) == 'range(0, 10, 2)')

    def test_range_corner_cases(self):
        x = range(0, sys.maxsize, sys.maxsize-1)
        self.assertEqual(x[0], 0)
        self.assertEqual(x[1], sys.maxsize-1)
        self.assertEqual(len(x), 2)

        x = range(sys.maxsize, 0, -(sys.maxsize-1))
        self.assertEqual(x[0], sys.maxsize)
        self.assertEqual(x[1], 1)
        self.assertEqual(len(x), 2)

        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                x = range(0, Int64.MaxValue, Int64.MaxValue-1)
                self.assertEqual(x[0], 0)
                self.assertEqual(x[1], Int64.MaxValue-1)

                self.assertEqual(len(range(0, Int64.MaxValue, Int64.MaxValue-1)), 2)

                r = range(-Int64.MaxValue, Int64.MaxValue, 2)
                self.assertEqual(len(r), Int64.MaxValue)

    def test_range_coverage(self):
        ## ToString
        self.assertEqual(str(range(0, 3, 1)), "range(0, 3)")
        self.assertEqual(str(range(1, 3, 1)), "range(1, 3)")
        self.assertEqual(str(range(0, 5, 2)), "range(0, 5, 2)")

        ## Long
        self.assertEqual([x for x in range(5)], list(range(5)))
        self.assertEqual([x for x in range(10, 15)], list(range(10, 15)))
        self.assertEqual([x for x in range(10, 15, 2)], list(range(10, 15, 2)))

        ## Ops
        self.assertRaises(TypeError, lambda: range(4) + 4)
        self.assertRaises(TypeError, lambda: range(4) * 4)

    def test_range_equal(self):
         self.assertEqual(range(0, 3, 2), range(0, 4, 2))
         self.assertEqual(range(0), range(1, -1, 1))

# as soon as unittest and stdlib/test are usable, the following tests can be retired
class RangeTestFromStdLib(unittest.TestCase):

    def test_invalid_invocation_from_stdlib(self):
        self.assertRaises(TypeError, range)
        self.assertRaises(TypeError, range, 1, 2, 3, 4)
        self.assertRaises(ValueError, range, 1, 2, 0)
        a = int(10 * sys.maxsize)
        if is_cli:
            self.assertRaises(OverflowError, range, a, a + 1, int(0))
        else:
            self.assertRaises(ValueError, range, a, a + 1, int(0))
        self.assertRaises(TypeError, range, 1., 1., 1.)
        self.assertRaises(TypeError, range, 1e100, 1e101, 1e101)
        self.assertRaises(TypeError, range, 0, "spam")
        self.assertRaises(TypeError, range, 0, 42, "spam")
        # Exercise various combinations of bad arguments, to check
        # refcounting logic
        self.assertRaises(TypeError, range, 0.0)
        self.assertRaises(TypeError, range, 0, 0.0)
        self.assertRaises(TypeError, range, 0.0, 0)
        self.assertRaises(TypeError, range, 0.0, 0.0)
        self.assertRaises(TypeError, range, 0, 0, 1.0)
        self.assertRaises(TypeError, range, 0, 0.0, 1)
        self.assertRaises(TypeError, range, 0, 0.0, 1.0)
        self.assertRaises(TypeError, range, 0.0, 0, 1)
        self.assertRaises(TypeError, range, 0.0, 0, 1.0)
        self.assertRaises(TypeError, range, 0.0, 0.0, 1)
        self.assertRaises(TypeError, range, 0.0, 0.0, 1.0)

    def test_index_from_stdlib(self):
        u = range(2)
        self.assertEqual(u.index(0), 0)
        self.assertEqual(u.index(1), 1)
        self.assertRaises(ValueError, u.index, 2)

        u = range(-2, 3)
        self.assertEqual(u.count(0), 1)
        self.assertEqual(u.index(0), 2)
        self.assertRaises(TypeError, u.index)

        class BadExc(Exception):
            pass

        class BadCmp:
            def __eq__(self, other):
                if other == 2:
                    raise BadExc()
                return False

        a = range(4)
        self.assertRaises(BadExc, a.index, BadCmp())

        a = range(-2, 3)
        self.assertEqual(a.index(0), 2)
        self.assertEqual(range(1, 10, 3).index(4), 1)
        self.assertEqual(range(1, -10, -3).index(-5), 2)

        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                self.assertEqual(range(10**20).index(1), 1)
                self.assertEqual(range(10**20).index(10**20 - 1), 10**20 - 1)

                self.assertRaises(ValueError, range(1, 2**100, 2).index, 2**87)
                self.assertEqual(range(1, 2**100, 2).index(2**87+1), 2**86)
        else:
            self.assertEqual(range(10**20).index(10**20 - 1), 10**20 - 1)

            self.assertRaises(ValueError, range(1, 2**100, 2).index, 2**87)
            self.assertEqual(range(1, 2**100, 2).index(2**87+1), 2**86)

        class AlwaysEqual(object):
            def __eq__(self, other):
                return True
        always_equal = AlwaysEqual()
        self.assertEqual(range(10).index(always_equal), 0)

    def test_count_from_stdlib(self):
        self.assertEqual(range(3).count(-1), 0)
        self.assertEqual(range(3).count(0), 1)
        self.assertEqual(range(3).count(1), 1)
        self.assertEqual(range(3).count(2), 1)
        self.assertEqual(range(3).count(3), 0)
        self.assertIs(type(range(3).count(-1)), int)
        self.assertIs(type(range(3).count(1)), int)
        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                self.assertEqual(range(10**20).count(1), 1)
                self.assertEqual(range(10**20).count(10**20), 0)
        else:
            self.assertEqual(range(10**20).count(1), 1)
            self.assertEqual(range(10**20).count(10**20), 0)

        self.assertEqual(range(3).index(1), 1)
        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                self.assertEqual(range(1, 2**100, 2).count(2**87), 0)
                self.assertEqual(range(1, 2**100, 2).count(2**87+1), 1)
        else:
            self.assertEqual(range(1, 2**100, 2).count(2**87), 0)
            self.assertEqual(range(1, 2**100, 2).count(2**87+1), 1)

        class AlwaysEqual(object):
            def __eq__(self, other):
                return True
        always_equal = AlwaysEqual()
        self.assertEqual(range(10).count(always_equal), 10)

        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                self.assertEqual(len(range(sys.maxsize, sys.maxsize+10)), 10)
        else:
            self.assertEqual(len(range(sys.maxsize, sys.maxsize+10)), 10)
        self.assertEqual(len(range(sys.maxsize-10, sys.maxsize)), 10)

    def test_user_index_method_from_stdlib(self):
        bignum = 2*sys.maxsize
        smallnum = 42

        # User-defined class with an __index__ method
        class I:
            def __init__(self, n):
                self.n = int(n)
            def __index__(self):
                return self.n

        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                self.assertEqual(list(range(I(bignum), I(bignum + 1))), [bignum])
        else:
            self.assertEqual(list(range(I(bignum), I(bignum + 1))), [bignum])
        self.assertEqual(list(range(I(smallnum), I(smallnum + 1))), [smallnum])

        # User-defined class with a failing __index__ method
        class IX:
            def __index__(self):
                raise RuntimeError
        self.assertRaises(RuntimeError, range, IX())

        # User-defined class with an invalid __index__ method
        class IN:
            def __index__(self):
                return "not a number"

        self.assertRaises(TypeError, range, IN())

        # Test use of user-defined classes in slice indices.
        self.assertEqual(range(10)[:I(5)], range(5))

        with self.assertRaises(RuntimeError):
            range(0, 10)[:IX()]

        with self.assertRaises(TypeError):
            range(0, 10)[:IN()]

    def test_slice_from_stdlib(self ):
        def check(start, stop, step=None):
            i = slice(start, stop, step)
            self.assertEqual(list(r[i]), list(r)[i])
            self.assertEqual(len(r[i]), len(list(r)[i]))
        for r in [range(10),
                    range(0),
                    range(1, 9, 3),
                    range(8, 0, -3),
                    #range(sys.maxsize+1, sys.maxsize+10), # https://github.com/IronLanguages/ironpython3/issues/472
                    ]:
            check(0, 2)
            check(0, 20)
            check(1, 2)
            check(20, 30)
            check(-30, -20)
            check(-1, 100, 2)
            check(0, -1)
            check(-1, -3, -1)

    def test_reverse_iteration_from_stdlib(self):
        for r in [range(10),
                    range(0),
                    range(1, 9, 3),
                    range(8, 0, -3),
                    #range(sys.maxsize+1, sys.maxsize+10), # https://github.com/IronLanguages/ironpython3/issues/472
                    ]:
            self.assertEqual(list(reversed(r)), list(r)[::-1])

    def test_comparison_from_stdlib(self):
        test_ranges = [range(0), range(0, -1),
                       range(1, 1, 3),
                       range(1), range(5, 6), range(5, 6, 2),
                       range(5, 7, 2), range(2), range(0, 4, 2),
                       range(0, 5, 2), range(0, 6, 2)]
        test_tuples = list(map(tuple, test_ranges))

        # Check that equality of ranges matches equality of the corresponding
        # tuples for each pair from the test lists above.
        ranges_eq = [a == b for a in test_ranges for b in test_ranges]
        tuples_eq = [a == b for a in test_tuples for b in test_tuples]

        self.assertEqual(ranges_eq, tuples_eq)

        # Check that != correctly gives the logical negation of ==
        ranges_ne = [a != b for a in test_ranges for b in test_ranges]
        self.assertEqual(ranges_ne, [not x for x in ranges_eq])

        # Equal ranges should have equal hashes.
        for a in test_ranges:
            for b in test_ranges:
                if a == b:
                    self.assertEqual(hash(a), hash(b))

        # Ranges are unequal to other types (even sequence types)
        self.assertIs(range(0) == (), False)
        self.assertIs(() == range(0), False)
        self.assertIs(range(2) == [0, 1], False)

        # Huge integers aren't a problem.
        if is_cli:
            # TODO: https://github.com/IronLanguages/ironpython3/issues/472
            with self.assertRaises(OverflowError):
                self.assertEqual(range(0, 2**100 - 1, 2),
                                    range(0, 2**100, 2))
                self.assertEqual(hash(range(0, 2**100 - 1, 2)),
                                    hash(range(0, 2**100, 2)))
                self.assertNotEqual(range(0, 2**100, 2),
                                    range(0, 2**100 + 1, 2))
                self.assertEqual(range(2**200, 2**201 - 2**99, 2**100),
                                    range(2**200, 2**201, 2**100))
                self.assertEqual(hash(range(2**200, 2**201 - 2**99, 2**100)),
                                    hash(range(2**200, 2**201, 2**100)))
                self.assertNotEqual(range(2**200, 2**201, 2**100),
                                    range(2**200, 2**201 + 1, 2**100))
        else:
            self.assertEqual(range(0, 2**100 - 1, 2),
                                range(0, 2**100, 2))
            self.assertEqual(hash(range(0, 2**100 - 1, 2)),
                                hash(range(0, 2**100, 2)))
            self.assertNotEqual(range(0, 2**100, 2),
                                range(0, 2**100 + 1, 2))
            self.assertEqual(range(2**200, 2**201 - 2**99, 2**100),
                                range(2**200, 2**201, 2**100))
            self.assertEqual(hash(range(2**200, 2**201 - 2**99, 2**100)),
                                hash(range(2**200, 2**201, 2**100)))
            self.assertNotEqual(range(2**200, 2**201, 2**100),
                                range(2**200, 2**201 + 1, 2**100))

        # Order comparisons are not implemented for ranges.
        with self.assertRaises(TypeError):
            range(0) < range(0)
        with self.assertRaises(TypeError):
            range(0) > range(0)
        with self.assertRaises(TypeError):
            range(0) <= range(0)
        with self.assertRaises(TypeError):
            range(0) >= range(0)

run_test(__name__)
