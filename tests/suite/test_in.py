# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from iptest import IronPythonTestCase, is_cli, run_test

if is_cli:
    import clr

class C:
    x = "Hello"
    def __contains__(self, y):
        return self.x == y


class D:
    x = (1,2,3,4,5,6,7,8,9,10)
    def __getitem__(self, y):
        return self.x[y]

class InTest(IronPythonTestCase):
    def test_basic(self):
        self.assertTrue('abc' in 'abcd')

    def test_class(self):
        h = "Hello"
        c = C()
        self.assertTrue(c.__contains__("Hello"))
        self.assertTrue(c.__contains__(h))
        self.assertTrue(not (c.__contains__('abc')))

        self.assertTrue(h in c)
        self.assertTrue("Hello" in c)

        d = D()
        self.assertTrue(1 in d)
        self.assertTrue(not(11 in d))

    def test_contains(self):
        nan = float('nan')
        nan2 = float('nan')

        # lists

        l = [nan]
        self.assertTrue(nan in l)
        self.assertTrue(nan2 not in l)
        self.assertTrue(l == [nan])
        self.assertTrue(l.index(nan) == 0)
        self.assertTrue(l.count(nan) == 1)

        if is_cli:
            self.assertTrue(l.IndexOf(nan) == 0)

        # tuples

        t = (nan,)
        self.assertTrue(nan in t)
        self.assertTrue(nan2 not in t)
        self.assertTrue(t == (nan,))
        self.assertTrue(t.index(nan) == 0)
        self.assertTrue(t.count(nan) == 1)

        if is_cli:
            self.assertTrue(l.IndexOf(nan) == 0)

        # sets

        s = {nan}
        self.assertTrue(nan in s)
        self.assertTrue(nan2 not in s)
        self.assertTrue(s == {nan})
        s.add(nan)
        self.assertTrue(len(s) == 1)
        s.remove(nan)
        s.add(nan)
        s.discard(nan)
        self.assertTrue(len(s) == 0)
        s.add(nan)
        s.symmetric_difference_update({nan})
        self.assertTrue(len(s) == 0)

        # dictionaries

        d = {nan: nan2}
        self.assertTrue(nan in d)
        self.assertTrue(nan2 not in d)
        self.assertTrue(d == {nan: nan2})
        self.assertTrue((nan, nan2) in d.items())
        self.assertTrue(nan in d.keys())
        self.assertTrue(nan2 in d.values())

        if is_cli:
            from System.Collections.Generic import Dictionary, KeyValuePair
            d2 = Dictionary[object, object]()
            d2.Add(nan, nan2)
            self.assertTrue(d == d2)

            self.assertTrue(d.Contains(KeyValuePair[object, object](nan, nan2)))

        # deque

        from _collections import deque

        q = deque([nan])
        self.assertTrue(nan in q)
        self.assertTrue(nan2 not in q)
        self.assertTrue(q == deque([nan]))
        q.remove(nan)
        self.assertTrue(len(q) == 0)

        # operator

        import operator

        self.assertTrue(operator.contains([nan], nan))
        self.assertTrue(operator.indexOf([nan], nan) == 0)
        self.assertTrue(operator.countOf([nan], nan) == 1)

run_test(__name__)
