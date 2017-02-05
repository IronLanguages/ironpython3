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

##
## Test range
##
## * sbs_builtin\test_xrange covers many range corner cases
##


# IronPython range implementation uses int to represent start, stop and step.
# As a consequence tests making use of value out of int range
# will not work. All such a tests are commented out and market with
# "not valid for int" comment


from iptest.assert_util import *

import sys

def test_range():
    Assert(list(range(10)) == [0, 1, 2, 3, 4, 5, 6, 7, 8, 9])
    Assert(list(range(0)) == [])
    Assert(list(range(-10)) == [])

    Assert(list(range(3,10)) == [3, 4, 5, 6, 7, 8, 9])
    Assert(list(range(10,3)) == [])
    Assert(list(range(-3,-10)) == [])
    Assert(list(range(-10,-3)) == [-10, -9, -8, -7, -6, -5, -4])

    Assert(list(range(3,20,2)) == [3, 5, 7, 9, 11, 13, 15, 17, 19])
    Assert(list(range(3,20,-2)) == [])
    Assert(list(range(20,3,2)) == [])
    Assert(list(range(20,3,-2)) == [20, 18, 16, 14, 12, 10, 8, 6, 4])
    Assert(list(range(-3,-20,2)) == [])
    Assert(list(range(-3,-20,-2)) == [-3, -5, -7, -9, -11, -13, -15, -17, -19])
    Assert(list(range(-20,-3, 2)) == [-20, -18, -16, -14, -12, -10, -8, -6, -4])
    Assert(list(range(-20,-3,-2)) == [])

def test_range_collections():
    Assert(list(range(0, 100, 2)).count(10) == 1)
    Assert(list(range(0, 100, 2)).index(10) == 5)
    Assert(list(range(0, 100, 2))[5] == 10)
    Assert(str(list(range(0, 100, 2))[0:5]) == 'range(0, 10, 2)')

def test_range_corner_cases():
    x = list(range(0, sys.maxsize, sys.maxsize-1))
    AreEqual(x[0], 0)
    AreEqual(x[1], sys.maxsize-1)
    AreEqual(len(x), 2)

    x = list(range(sys.maxsize, 0, -(sys.maxsize-1)))
    AreEqual(x[0], sys.maxsize)
    AreEqual(x[1], 1)
    AreEqual(len(x), 2)

    # not valid for int
    # x = range(0, Int64.MaxValue, Int64.MaxValue-1)
    # AreEqual(x[0], 0)
    # AreEqual(x[1], Int64.MaxValue-1)

    # self.assertEqual(len(range(0, Int64.MaxValue, Int64.MaxValue-1)), 2)

    # r = range(-Int64.MaxValue, Int64.MaxValue, 2)
    # self.assertEqual(len(r), Int64.MaxValue)


def test_range_coverage():
    ## ToString
    AreEqual(str(list(range(0, 3, 1))), "range(0, 3)")
    AreEqual(str(list(range(1, 3, 1))), "range(1, 3)")
    AreEqual(str(list(range(0, 5, 2))), "range(0, 5, 2)")

    ## Long
    AreEqual([x for x in range(5)], list(range(5)))
    AreEqual([x for x in range(10, 15)], list(range(10, 15)))
    AreEqual([x for x in range(10, 15, 2)], list(range(10, 15, 2)))

    ## Ops
    AssertError(TypeError, lambda: list(range(4)) + 4)
    AssertError(TypeError, lambda: list(range(4)) * 4)

def test_range_equal():
     AreEqual(list(range(0, 3, 2)), list(range(0, 4, 2)))
     AreEqual(list(range(0)), list(range(1, -1, 1)))


# as soon as unittest ans stdlib/test are usable, the following tests can be retired

# ugly hack to avoid changing assertions style from unittest to iptest
import iptest.assert_util as self
self.assertEqual = self.AreEqual
self.assertNotEqual = self.AreNotEqual
self.assertRaises = self.AssertError
self.assertIs = self.AssertIs
self.assertIn = self.AssertIn
self.assertNotIn = self.AssertNotIn
self.assertRaisesCtx = self.AssertRaisesCtx

def test_range_from_stdlib():
    self.assertEqual(list(range(3)), [0, 1, 2])
    self.assertEqual(list(range(1, 5)), [1, 2, 3, 4])
    self.assertEqual(list(range(0)), [])
    self.assertEqual(list(range(-3)), [])
    self.assertEqual(list(range(1, 10, 3)), [1, 4, 7])
    self.assertEqual(list(range(5, -5, -3)), [5, 2, -1, -4])

    a = 10
    b = 100
    c = 50

    self.assertEqual(list(range(a, a+2)), [a, a+1])
    self.assertEqual(list(range(a+2, a, -1)), [a+2, a+1])
    self.assertEqual(list(range(a+4, a, -2)), [a+4, a+2])

    seq = list(range(a, b, c))
    self.assertIn(a, seq)
    self.assertNotIn(b, seq)
    self.assertEqual(len(seq), 2)

    seq = list(range(b, a, -c))
    self.assertIn(b, seq)
    self.assertNotIn(a, seq)
    self.assertEqual(len(seq), 2)

    seq = list(range(-a, -b, -c))
    self.assertIn(-a, seq)
    self.assertNotIn(-b, seq)
    self.assertEqual(len(seq), 2)

    self.assertRaises(TypeError, range)
    self.assertRaises(TypeError, range, 1, 2, 3, 4)
    self.assertRaises(ValueError, range, 1, 2, 0)

    self.assertRaises(TypeError, range, 0.0, 2, 1)
    self.assertRaises(TypeError, range, 1, 2.0, 1)
    self.assertRaises(TypeError, range, 1, 2, 1.0)
    self.assertRaises(TypeError, range, 1e100, 1e101, 1e101)

    self.assertRaises(TypeError, range, 0, "spam")
    self.assertRaises(TypeError, range, 0, 42, "spam")

    self.assertEqual(len(list(range(0, sys.maxsize, sys.maxsize-1))), 2)

    r = list(range(-sys.maxsize, sys.maxsize, 2))
    self.assertEqual(len(r), sys.maxsize)

def test_invalid_invocation_from_stdlib():
    self.assertRaises(TypeError, range)
    self.assertRaises(TypeError, range, 1, 2, 3, 4)
    self.assertRaises(ValueError, range, 1, 2, 0)

    # not valid for int
    #a = int(10 * sys.maxsize)
    #self.assertRaises(ValueError, range, a, a + 1, int(0))
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

def test_index_from_stdlib():
    u = list(range(2))
    self.assertEqual(u.index(0), 0)
    self.assertEqual(u.index(1), 1)
    self.assertRaises(ValueError, u.index, 2)

    u = list(range(-2, 3))
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

    a = list(range(4))
    self.assertRaises(BadExc, a.index, BadCmp())

    a = list(range(-2, 3))
    self.assertEqual(a.index(0), 2)
    self.assertEqual(list(range(1, 10, 3)).index(4), 1)
    self.assertEqual(list(range(1, -10, -3)).index(-5), 2)

    # not valid for int
    #self.assertEqual(range(10**20).index(1), 1)
    #self.assertEqual(range(10**20).index(10**20 - 1), 10**20 - 1)

    #self.assertRaises(ValueError, range(1, 2**100, 2).index, 2**87)
    #self.assertEqual(range(1, 2**100, 2).index(2**87+1), 2**86)

    class AlwaysEqual(object):
        def __eq__(self, other):
            return True
    always_equal = AlwaysEqual()
    self.assertEqual(list(range(10)).index(always_equal), 0)


def test_count_from_stdlib():
    self.assertEqual(list(range(3)).count(-1), 0)
    self.assertEqual(list(range(3)).count(0), 1)
    self.assertEqual(list(range(3)).count(1), 1)
    self.assertEqual(list(range(3)).count(2), 1)
    self.assertEqual(list(range(3)).count(3), 0)
    self.assertIs(type(list(range(3)).count(-1)), int)
    self.assertIs(type(list(range(3)).count(1)), int)
    # not valid for int
    #self.assertEqual(range(10**20).count(1), 1)
    #self.assertEqual(range(10**20).count(10**20), 0)
    self.assertEqual(list(range(3)).index(1), 1)
    # not valid for int
    #self.assertEqual(range(1, 2**100, 2).count(2**87), 0)
    #self.assertEqual(range(1, 2**100, 2).count(2**87+1), 1)

    class AlwaysEqual(object):
        def __eq__(self, other):
            return True
    always_equal = AlwaysEqual()
    self.assertEqual(list(range(10)).count(always_equal), 10)

    # not valid for int
    #self.assertEqual(len(range(sys.maxsize, sys.maxsize+10)), 10)
    self.assertEqual(len(list(range(sys.maxsize-10, sys.maxsize))), 10)

def test_user_index_method_from_stdlib():
    # not valid for int
    # bignum = 2*sys.maxsize
    smallnum = 42

    # User-defined class with an __index__ method
    class I:
        def __init__(self, n):
            self.n = int(n)
        def __index__(self):
            return self.n

    # not valid for int
    # self.assertEqual(list(range(I(bignum), I(bignum + 1))), [bignum])
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
    self.assertEqual(list(range(10))[:I(5)], list(range(5)))

    with self.assertRaisesCtx(RuntimeError):
        list(range(0, 10))[:IX()]

    with self.assertRaisesCtx(TypeError):
        list(range(0, 10))[:IN()]

def test_repr_from_stdlib():
    self.assertEqual(repr(list(range(1))), 'range(0, 1)')
    self.assertEqual(repr(list(range(1, 2))), 'range(1, 2)')
    self.assertEqual(repr(list(range(1, 2, 3))), 'range(1, 2, 3)')

def _test_pickling_from_stdlib():
    testcases = [(13,), (0, 11), (-22, 10), (20, 3, -1),
                    (13, 21, 3), (-2, 2, 2), (2**65, 2**65+2)]
    for proto in range(pickle.HIGHEST_PROTOCOL + 1):
        for t in testcases:
            with self.subTest(proto=proto, test=t):
                r = list(range(*t))
                self.assertEqual(list(pickle.loads(pickle.dumps(r, proto))),
                                    list(r))

def _test_iterator_pickling_from_stdlib():
    testcases = [(13,), (0, 11), (-22, 10), (20, 3, -1),
                    (13, 21, 3), (-2, 2, 2), (2**65, 2**65+2)]
    for proto in range(pickle.HIGHEST_PROTOCOL + 1):
        for t in testcases:
            it = itorg = iter(list(range(*t)))
            data = list(range(*t))

            d = pickle.dumps(it)
            it = pickle.loads(d)
            self.assertEqual(type(itorg), type(it))
            self.assertEqual(list(it), data)

            it = pickle.loads(d)
            try:
                next(it)
            except StopIteration:
                continue
            d = pickle.dumps(it)
            it = pickle.loads(d)
            self.assertEqual(list(it), data[1:])

def test_odd_bug_from_stdlib():
    # This used to raise a "SystemError: NULL result without error"
    # because the range validation step was eating the exception
    # before NULL was returned.
    with self.assertRaisesCtx(TypeError):
        list(range([], 1, -1))

def test_types_from_stdlib():
    # Non-integer objects *equal* to any of the range's items are supposed
    # to be contained in the range.
    self.assertIn(1.0, list(range(3)))
    self.assertIn(True, list(range(3)))
    self.assertIn(1+0j, list(range(3)))

    class C1:
        def __eq__(self, other): return True
    self.assertIn(C1(), list(range(3)))

    # Objects are never coerced into other types for comparison.
    class C2:
        def __int__(self): return 1
        def __index__(self): return 1
    self.assertNotIn(C2(), list(range(3)))
    # ..except if explicitly told so.
    self.assertIn(int(C2()), list(range(3)))

    # Check that the range.__contains__ optimization is only
    # used for ints, not for instances of subclasses of int.
    class C3(int):
        def __eq__(self, other): return True
    self.assertIn(C3(11), list(range(10)))
    self.assertIn(C3(11), list(range(10)))

def test_strided_limits_from_stdlib():
    r = list(range(0, 101, 2))
    self.assertIn(0, r)
    self.assertNotIn(1, r)
    self.assertIn(2, r)
    self.assertNotIn(99, r)
    self.assertIn(100, r)
    self.assertNotIn(101, r)

    r = list(range(0, -20, -1))
    self.assertIn(0, r)
    self.assertIn(-1, r)
    self.assertIn(-19, r)
    self.assertNotIn(-20, r)

    r = list(range(0, -20, -2))
    self.assertIn(-18, r)
    self.assertNotIn(-19, r)
    self.assertNotIn(-20, r)

def test_empty_from_stdlib():
    r = list(range(0))
    self.assertNotIn(0, r)
    self.assertNotIn(1, r)

    r = list(range(0, -10))
    self.assertNotIn(0, r)
    self.assertNotIn(-1, r)
    self.assertNotIn(1, r)

def _test_range_iterators_from_stdlib():
    # exercise 'fast' iterators, that use a rangeiterobject internally.
    # see issue 7298
    limits = [base + jiggle
                for M in (2**32, 2**64)
                for base in (-M, -M//2, 0, M//2, M)
                for jiggle in (-2, -1, 0, 1, 2)]
    test_ranges = [(start, end, step)
                    for start in limits
                    for end in limits
                    for step in (-2**63, -2**31, -2, -1, 1, 2)]

    for start, end, step in test_ranges:
        iter1 = list(range(start, end, step))
        iter2 = pyrange(start, end, step)
        test_id = "range({}, {}, {})".format(start, end, step)
        # check first 100 entries
        self.assert_iterators_equal(iter1, iter2, test_id, limit=100)

        iter1 = reversed(list(range(start, end, step)))
        iter2 = pyrange_reversed(start, end, step)
        test_id = "reversed(range({}, {}, {}))".format(start, end, step)
        self.assert_iterators_equal(iter1, iter2, test_id, limit=100)

def test_slice_from_stdlib():
    def check(start, stop, step=None):
        i = slice(start, stop, step)
        self.assertEqual(list(r[i]), list(r)[i])
        self.assertEqual(len(r[i]), len(list(r)[i]))
    for r in [list(range(10)),
                list(range(0)),
                list(range(1, 9, 3)),
                list(range(8, 0, -3)),
                # not valid for int
                #range(sys.maxsize+1, sys.maxsize+10),
                ]:
        check(0, 2)
        check(0, 20)
        check(1, 2)
        check(20, 30)
        check(-30, -20)
        check(-1, 100, 2)
        check(0, -1)
        check(-1, -3, -1)

def test_contains_from_stdlib():
    r = list(range(10))
    self.assertIn(0, r)
    self.assertIn(1, r)
    self.assertIn(5.0, r)
    self.assertNotIn(5.1, r)
    self.assertNotIn(-1, r)
    self.assertNotIn(10, r)
    self.assertNotIn("", r)
    r = list(range(9, -1, -1))
    self.assertIn(0, r)
    self.assertIn(1, r)
    self.assertIn(5.0, r)
    self.assertNotIn(5.1, r)
    self.assertNotIn(-1, r)
    self.assertNotIn(10, r)
    self.assertNotIn("", r)
    r = list(range(0, 10, 2))
    self.assertIn(0, r)
    self.assertNotIn(1, r)
    self.assertNotIn(5.0, r)
    self.assertNotIn(5.1, r)
    self.assertNotIn(-1, r)
    self.assertNotIn(10, r)
    self.assertNotIn("", r)
    r = list(range(9, -1, -2))
    self.assertNotIn(0, r)
    self.assertIn(1, r)
    self.assertIn(5.0, r)
    self.assertNotIn(5.1, r)
    self.assertNotIn(-1, r)
    self.assertNotIn(10, r)
    self.assertNotIn("", r)

def test_reverse_iteration_from_stdlib():
    for r in [list(range(10)),
                list(range(0)),
                list(range(1, 9, 3)),
                list(range(8, 0, -3)),
                # not valid for int
                #range(sys.maxsize+1, sys.maxsize+10),
                ]:
        self.assertEqual(list(reversed(r)), list(r)[::-1])

def test_issue11845_from_stdlib():
    r = list(range(*slice(1, 18, 2).indices(20)))
    values = {None, 0, 1, -1, 2, -2, 5, -5, 19, -19,
                20, -20, 21, -21, 30, -30, 99, -99}
    for i in values:
        for j in values:
            for k in values - {0}:
                r[i:j:k]

def test_comparison_from_stdlib():
    test_ranges = [list(range(0)), list(range(0, -1)),
                   list(range(1, 1, 3)),
                   list(range(1)), list(range(5, 6)), list(range(5, 6, 2)),
                   list(range(5, 7, 2)), list(range(2)), list(range(0, 4, 2)),
                   list(range(0, 5, 2)), list(range(0, 6, 2))]
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
    self.assertIs(list(range(0)) == (), False)
    self.assertIs(() == list(range(0)), False)
    self.assertIs(list(range(2)) == [0, 1], False)

    # not valid for int
    # Huge integers aren't a problem.
    #self.assertEqual(range(0, 2**100 - 1, 2),
    #                    range(0, 2**100, 2))
    #self.assertEqual(hash(range(0, 2**100 - 1, 2)),
    #                    hash(range(0, 2**100, 2)))
    #self.assertNotEqual(range(0, 2**100, 2),
    #                    range(0, 2**100 + 1, 2))
    #self.assertEqual(range(2**200, 2**201 - 2**99, 2**100),
    #                    range(2**200, 2**201, 2**100))
    #self.assertEqual(hash(range(2**200, 2**201 - 2**99, 2**100)),
    #                    hash(range(2**200, 2**201, 2**100)))
    #self.assertNotEqual(range(2**200, 2**201, 2**100),
    #                    range(2**200, 2**201 + 1, 2**100))

    # Order comparisons are not implemented for ranges.
    with self.assertRaisesCtx(TypeError):
        list(range(0)) < list(range(0))
    with self.assertRaisesCtx(TypeError):
        list(range(0)) > list(range(0))
    with self.assertRaisesCtx(TypeError):
        list(range(0)) <= list(range(0))
    with self.assertRaisesCtx(TypeError):
        list(range(0)) >= list(range(0))


def test_attributes_from_stdlib():
    # test the start, stop and step attributes of range objects
    self.assert_attrs(list(range(0)), 0, 0, 1)
    self.assert_attrs(list(range(10)), 0, 10, 1)
    self.assert_attrs(list(range(-10)), 0, -10, 1)
    self.assert_attrs(list(range(0, 10, 1)), 0, 10, 1)
    self.assert_attrs(list(range(0, 10, 3)), 0, 10, 3)
    self.assert_attrs(list(range(10, 0, -1)), 10, 0, -1)
    self.assert_attrs(list(range(10, 0, -3)), 10, 0, -3)

def assert_attrs(rangeobj, start, stop, step):
    self.assertEqual(rangeobj.start, start)
    self.assertEqual(rangeobj.stop, stop)
    self.assertEqual(rangeobj.step, step)

    with self.assertRaisesCtx(AttributeError):
        rangeobj.start = 0
    with self.assertRaisesCtx(AttributeError):
        rangeobj.stop = 10
    with self.assertRaisesCtx(AttributeError):
        rangeobj.step = 1

    with self.assertRaisesCtx(AttributeError):
        del rangeobj.start
    with self.assertRaisesCtx(AttributeError):
        del rangeobj.stop
    with self.assertRaisesCtx(AttributeError):
        del rangeobj.step
self.assert_attrs = assert_attrs


run_test(__name__)
