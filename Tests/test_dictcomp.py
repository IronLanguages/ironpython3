# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import run_test

class DictCompTest(unittest.TestCase):

    def test_dict_comp(self):
        self.assertEqual({locals()['x'] : locals()['x'] for x in (2,3,4)}, {2:2, 3:3, 4:4})
        
        x = 100
        {x:x for x in (2,3,4)}
        self.assertEqual(x, 100)
        
        class C:
            {x:x for x in (2,3,4)}
        
        self.assertEqual(hasattr(C, 'x'), False)
        
        class C:
            abc = {locals()['x']:locals()['x'] for x in (2,3,4)}
        
        self.assertEqual(C.abc, {2:2,3:3,4:4})

        d = {}
        exec compile("abc = {locals()['x']:locals()['x'] for x in (2,3,4)}", 'exec', 'exec') in d, d
        self.assertEqual(d['abc'], {2:2,3:3,4:4})
        
        d = {'y':42}
        exec compile("abc = {y:y for x in (2,3,4)}", 'exec', 'exec') in d, d
        self.assertEqual(d['abc'], {42:42})

        d = {'y':42, 't':(2,3,42)}
        exec compile("abc = {y:y for x in t if x == y}", 'exec', 'exec') in d, d
        self.assertEqual(d['abc'], {42:42})

        t = (2,3,4)
        v = 2
        abc = {v:v for x in t}
        self.assertEqual(abc, {2:2})

        abc = {x:x for x in t if x == v}
        self.assertEqual(abc, {2:2})
        
        def f():
            abc = {x:x for x in t if x == v}
            self.assertEqual(abc, {2:2})
            
        f()
        
        def f():
            abc = {v:v for x in t}
            self.assertEqual(abc, {2:2})
            
            
        class C:
            abc = {v:v for x in t}
            self.assertEqual(abc, {2:2})
            
        class C:
            abc = {x:x for x in t if x == v}
            self.assertEqual(abc, {2:2})

    def test_scope_mixing(self):
        k = 1
        v = 3

        # in source
        r = {k:k for k in xrange(v)} # TODO: "xrange(v + k)" fails in IPY, but not in CPython
        self.assertEqual(r, {0:0, 1:1, 2:2})

        # in condition
        r = {k:k for k in xrange(4) if k < v}
        self.assertEqual(r, {0:0, 1:1, 2:2})

        # in item generation
        r = {k:(k+v) for k in xrange(2)}
        self.assertEqual(r, {0:3, 1:4})

    def test_scope_mixing_closures(self):
        # see also: GitHub issue #1196

        def eval(f, i):
            return f(i)

        v = 2

        # in source
        r = {k:k for k in eval(lambda i: xrange(i+v), v)}
        self.assertEqual(r, {0:0, 1:1, 2:2, 3:3})

        # in condition
        r = {k:k for k in xrange(4) if eval(lambda i: i>=v, k)}
        self.assertEqual(r, {2:2, 3:3})

        # in item generation
        r = {k:eval(lambda i: i+v, k+v) for k in xrange(2)}
        self.assertEqual(r, {0:4, 1:5})

run_test(__name__)
