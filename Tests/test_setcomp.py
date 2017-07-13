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

import unittest

from iptest import run_test

class SetCompTest(unittest.TestCase):

    def test_set_comp(self):
        self.assertEqual({locals()['x'] for x in (2,3,4)}, set([2, 3, 4]))
        
        x = 100
        {x for x in (2,3,4)}
        self.assertEqual(x, 100)
        
        class C:
            {x for x in (2,3,4)}
        
        self.assertEqual(hasattr(C, 'x'), False)
        
        class C:
            abc = {locals()['x'] for x in (2,3,4)}
        
        self.assertEqual(C.abc, set([2,3,4]))

        d = {}
        exec compile("abc = {locals()['x'] for x in (2,3,4)}", 'exec', 'exec') in d, d
        self.assertEqual(d['abc'], set([2,3,4]))
        
        d = {'y':42}
        exec compile("abc = {y for x in (2,3,4)}", 'exec', 'exec') in d, d
        self.assertEqual(d['abc'], set([42]))

        d = {'y':42, 't':(2,3,42)}
        exec compile("abc = {y for x in t if x == y}", 'exec', 'exec') in d, d
        self.assertEqual(d['abc'], set([42]))

        t = (2,3,4)
        v = 2
        abc = {v for x in t}
        self.assertEqual(abc, set([2]))

        abc = {x for x in t if x == v}
        self.assertEqual(abc, set([2]))
        
        def f():
            abc = {x for x in t if x == v}
            self.assertEqual(abc, set([2]))
            
        f()
        
        def f():
            abc = {v for x in t}
            self.assertEqual(abc, set([2]))
            
            
        class C:
            abc = {v for x in t}
            self.assertEqual(abc, set([2]))
            
        class C:
            abc = {x for x in t if x == v}
            self.assertEqual(abc, set([2]))

    def test_scope_mixing(self):
        k = 1
        v = 3

        # in source
        r = {k for k in xrange(v)} # TODO: "xrange(v + k)" fails in IPY, but not in CPython
        self.assertEqual(r, set([0, 1, 2]))

        # in condition
        r = {k for k in xrange(4) if k < v}
        self.assertEqual(r, set([0, 1, 2]))

        # in item generation
        r = {k+v for k in xrange(2)}
        self.assertEqual(r, set([3, 4]))

    def test_scope_mixing_closures(self):
        # see also: GitHub issue #1196

        def eval(f, i):
            return f(i)

        v = 2

        # in source
        r = {k for k in eval(lambda i: xrange(i+v), v)}
        self.assertEqual(r, set([0, 1, 2, 3]))

        # in condition
        r = {k for k in xrange(4) if eval(lambda i: i>=v, k)}
        self.assertEqual(r, set([2, 3]))

        # in item generation
        r = {eval(lambda i: i+v, k+v) for k in xrange(2)}
        self.assertEqual(r, set([4, 5]))


run_test(__name__)