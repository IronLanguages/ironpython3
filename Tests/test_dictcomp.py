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
