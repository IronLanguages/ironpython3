# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test built-in types: set/frozenset
##

import unittest

from iptest import IronPythonTestCase, is_cli, run_test
from iptest.type_util import myset, myfrozenset

#--GLOBALS---------------------------------------------------------------------
s1 = [2, 4, 5]
s2 = [4, 7, 9, 10]
s3 = [2, 4, 5, 6]

class SetTest(IronPythonTestCase):
    def test_equality(self):
        ne_list = [1]

        for z in [s1, s2, s3, []]:
            for x in (set, frozenset, myset, myfrozenset):
                for y in (set, frozenset, myset, myfrozenset):
                    self.assertEqual(x(z), y(z))
                    self.assertEqual(list(x(z)), list(y(z)))
                    self.assertEqual([x(z)], [y(z)])
                    self.assertEqual(tuple(x(z)), tuple(y(z)))
                    self.assertEqual((x(z)), (y(z)))
                self.assertTrue(x(z) != x(ne_list))
                self.assertTrue(list(x(z)) != list(x(ne_list)))
                self.assertTrue([x(z)] != [x(ne_list)])
                self.assertTrue(tuple(x(z)) != tuple(x(ne_list)))
                self.assertTrue((x(z)) != (x(ne_list)))

    def test_sanity(self):
        for x in (set, frozenset, myset, myfrozenset):
            # creating as default
            y = x()
            self.assertEqual(len(y), 0)
            # creating with 2 args
            self.assertRaises(TypeError, x, range(3), 3)
            #!!!self.assertRaises(TypeError, x.__new__, str)
            #!!!self.assertRaises(TypeError, x.__new__, str, 'abc')

            xs1, xs2, xs3 = x(s1), x(s2), x(s3)

            # membership
            self.assertEqual(4 in xs1, True)
            self.assertEqual(6 in xs1, False)

            # relation with another of the same type
            self.assertEqual(xs1.issubset(xs2), False)
            self.assertEqual(xs1.issubset(xs3), True)
            self.assertEqual(xs3.issuperset(xs1), True)
            self.assertEqual(xs3.issuperset(xs2), False)

            # equivalent op
            self.assertEqual(xs1 <= xs2, False)
            self.assertEqual(xs1 <= xs3, True)
            self.assertEqual(xs3 >= xs1, True)
            self.assertEqual(xs3 >= xs2, False)

            self.assertEqual(xs1.union(xs2), x([2, 4, 5, 7, 9, 10]))
            self.assertEqual(xs1.intersection(xs2), x([4]))
            self.assertEqual(xs1.difference(xs2), x([2, 5]))
            self.assertEqual(xs2.difference(xs1), x([7, 9, 10]))
            self.assertEqual(xs2.symmetric_difference(xs1), x([2, 5, 7, 9, 10]))
            self.assertEqual(xs3.symmetric_difference(xs1), x([6]))

            # equivalent op
            self.assertEqual(xs1 | xs2, x([2, 4, 5, 7, 9, 10]))
            self.assertEqual(xs1 & xs2, x([4]))
            self.assertEqual(xs1 - xs2, x([2, 5]))
            self.assertEqual(xs2 - xs1, x([7, 9, 10]))
            self.assertEqual(xs2 ^ xs1, x([2, 5, 7, 9, 10]))
            self.assertEqual(xs3 ^ xs1, x([6]))

            # repeat with list
            self.assertEqual(xs1.issubset(s2), False)
            self.assertEqual(xs1.issubset(s3), True)
            self.assertEqual(xs3.issuperset(s1), True)
            self.assertEqual(xs3.issuperset(s2), False)

            self.assertEqual(xs1.union(s2), x([2, 4, 5, 7, 9, 10]))
            self.assertEqual(xs1.intersection(s2), x([4]))
            self.assertEqual(xs1.difference(s2), x([2, 5]))
            self.assertEqual(xs2.difference(s1), x([7, 9, 10]))
            self.assertEqual(xs2.symmetric_difference(s1), x([2, 5, 7, 9, 10]))
            self.assertEqual(xs3.symmetric_difference(s1), x([6]))

    def test_ops(self):
        s1, s2, s3 = 'abcd', 'be', 'bdefgh'
        for t1 in (set, frozenset, myset, myfrozenset):
            for t2 in (set, frozenset, myset, myfrozenset):
                # set/frozenset creation
                self.assertEqual(t1(t2(s1)), t1(s1))

                # ops
                for (op, exp1, exp2) in [('&', 'b', 'bd'), ('|', 'abcde', 'abcdefgh'), ('-', 'acd', 'ac'), ('^', 'acde', 'acefgh')]:
                    d = dict(locals())

                    d["x1"] = t1(s1)
                    exec("x1   %s= t2(s2)" % op, d)
                    self.assertEqual(d["x1"], t1(exp1))

                    d["x1"] = t1(s1)
                    exec("x1   %s= t2(s3)" % op, d)
                    self.assertEqual(d["x1"], t1(exp2))

                    d["x1"] = t1(s1)
                    exec("y = x1 %s t2(s2)" % op, d)
                    self.assertEqual(d["y"], t1(exp1))

                    d["x1"] = t1(s1)
                    exec("y = x1 %s t2(s3)" % op, d)
                    self.assertEqual(d["y"], t1(exp2))

    def test_none(self):
        x, y = set([None, 'd']), set(['a', 'b', 'c', None])
        self.assertEqual(x | y, set([None, 'a', 'c', 'b', 'd']))
        self.assertEqual(y | x, set([None, 'a', 'c', 'b', 'd']))
        self.assertEqual(x & y, set([None]))
        self.assertEqual(y & x, set([None]))
        self.assertEqual(x - y, set('d'))
        self.assertEqual(y - x, set('abc'))

        a = set()
        a.add(None)
        self.assertEqual(repr(a), 'set([None])')


    def test_cmp(self):
        """Verify we can compare sets that aren't the same type"""

        a = frozenset([1,2])
        b = set([1,2])

        self.assertEqual(a, b)

        class sset(set): pass

        class fset(frozenset): pass

        a = fset([1,2])
        b = sset([1,2])

        self.assertEqual(a, b)

    def test_deque(self):
        if is_cli:
            from _collections import deque
        else:
            from collections import deque
        x = deque([2,3,4,5,6])
        x.remove(2)
        self.assertEqual(x, deque([3,4,5,6]))
        x.remove(6)
        self.assertEqual(x, deque([3,4,5]))
        x.remove(4)
        self.assertEqual(x, deque([3,5]))

        # get a deque w/ head/tail backwards...
        x = deque([1,2,3,4,5,6,7,8])
        x.popleft()
        x.popleft()
        x.popleft()
        x.popleft()
        x.append(1)
        x.append(2)
        x.append(3)
        x.append(4)
        self.assertEqual(x, deque([5,6,7,8, 1, 2, 3, 4]))
        x.remove(5)
        self.assertEqual(x, deque([6,7,8, 1, 2, 3, 4]))
        x.remove(4)
        self.assertEqual(x, deque([6,7,8, 1, 2, 3]))
        x.remove(8)
        self.assertEqual(x, deque([6,7,1, 2, 3]))
        x.remove(2)
        self.assertEqual(x, deque([6,7,1, 3]))

        class BadCmp:
            def __eq__(self, other):
                raise RuntimeError

        d = deque([1,2, BadCmp()])
        self.assertRaises(RuntimeError, d.remove, 3)

        x = deque()
        class y(object):
            def __eq__(self, other):
                return True

        x.append(y())
        self.assertEqual(y() in x, True)

        x = deque({}, None)
        self.assertEqual(x, deque([]))

        self.assertRaisesPartialMessage(TypeError, "takes at most 2 arguments (3 given)", deque, 'abc', 2, 2)

    def test_singleton(self):
        """Verify that an empty frozenset is a singleton"""
        self.assertEqual(frozenset([]) is frozenset([]), True)
        x = frozenset([1, 2, 3])
        self.assertEqual(x is frozenset(x), True)

    # no random
    def test_iteration_no_mutation_bad_hash(self):
        """create a set w/ objects with a bad hash and enumerate through it.  No exceptions should be thrown"""

        import random
        class c(object):
            def __hash__(self):
                return int(random.random()*200)

        l = [c() for i in range(1000)]
        b = set(l)
        for x in b:
            pass

    def test_null_elements(self):
        class SetSubclass(set):
            pass
        class FrozenSetSubclass(frozenset):
            pass

        for thetype in [set, frozenset, SetSubclass, FrozenSetSubclass]:
            s = thetype([None])

            self.assertEqual(s, set([None]))
            self.assertEqual(s.copy(), set([None]))

            self.assertEqual(s.isdisjoint(set()), True)
            self.assertEqual(s.isdisjoint(set([None])), False)
            self.assertEqual(s.isdisjoint(set([42])), True)
            self.assertEqual(s.isdisjoint(set([None, 42])), False)
            self.assertEqual(s.issubset(set()), False)
            self.assertEqual(s.issubset(set([42])), False)
            self.assertEqual(s.issubset(set([None])), True)
            self.assertEqual(s.issubset(set([None, 42])), True)
            self.assertEqual(s.issuperset(set()), True)
            self.assertEqual(s.issuperset(set([42])), False)
            self.assertEqual(s.issuperset(set([None])), True)
            self.assertEqual(s.issuperset(set([None, 42])), False)

            self.assertEqual(s.union(), set([None]))
            self.assertEqual(s.union(set([None])), set([None]))
            self.assertEqual(s.union(set()), set([None]))
            self.assertEqual(s.intersection(), set([None]))
            self.assertEqual(s.intersection(set([None])), set([None]))
            self.assertEqual(s.intersection(set()), set())
            self.assertEqual(s.difference(), set([None]))
            self.assertEqual(s.difference(set([None])), set())
            self.assertEqual(s.difference(set()), set([None]))
            self.assertEqual(s.symmetric_difference(set([None])), set())
            self.assertEqual(s.symmetric_difference(set()), set([None]))

            # Test mutating operations
            if 'add' in dir(s):
                s.remove(None)
                self.assertEqual(s, set())
                s.add(None)
                self.assertEqual(s, set([None]))
                s.discard(None)
                self.assertEqual(s, set())
                s.discard(None) # make sure we don't raise exception
                self.assertRaises(KeyError, s.remove, None)
                s.add(None)
                s.clear()
                self.assertEqual(s, set())
                s.add(None)
                self.assertEqual(s.pop(), None)
                self.assertEqual(s, set())

                s.update(set([None]))
                self.assertEqual(s, set([None]))
                s.intersection_update(set([42]))
                self.assertEqual(s, set())
                s.update(set([None, 42]))
                s.difference_update(set([None]))
                self.assertEqual(s, set([42]))
                s.symmetric_difference_update(set([None, 42]))
                self.assertEqual(s, set([None]))

    def test_frozenness(self):
        s = set([1,2,3])
        f = frozenset(s)
        s.add(4)
        self.assertEqual(4 in f, False)

    def test_gh152(self):
        """https://github.com/IronLanguages/ironpython2/issues/152"""
        s1 = set(['a','b','c','d','e','f','h','i','l','m'])
        s2 = set(['1','2','3','4','5','6','7','8','9'])
        s1.pop()
        s = s1 | s2
        self.assertEqual(len(s), len(s1) + len(s2))

        s = set(range(10))
        s.pop()
        t = set(s)
        self.assertEqual(len(s), len(t))

    def test_gh154(self):
        """https://github.com/IronLanguages/ironpython2/issues/154"""
        s = set([1,17])
        self.assertEqual(s.pop(), 1)
        s.add(17)
        self.assertEqual(tuple(s), (17,))

    def test_ipy2_gh239(self):
        """https://github.com/IronLanguages/ironpython2/issues/239"""
        s = set(range(5))
        s.remove(0)
        s.remove(1)
        s.remove(2)
        s.add(5)
        s.add(6)
        s.add(7)
        s.add(0)
        self.assertEqual(s, set([0, 3, 4, 5, 6, 7]))

        s = set(range(5))
        s.remove(0)
        s.remove(1)
        s.remove(2)
        s.add(5)
        s.add(6)
        s.add(7)
        s.add(8)
        self.assertEqual(s, set([3, 4, 5, 6, 7, 8]))

run_test(__name__)
