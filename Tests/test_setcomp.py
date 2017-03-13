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
## Testing set comprehension
##

from iptest.assert_util import *

## moved from test_set.py:
def test_set_comp():
    AreEqual({locals()['x'] for x in (2,3,4)}, set([2, 3, 4]))
    
    x = 100
    {x for x in (2,3,4)}
    AreEqual(x, 100)
    
    class C:
        {x for x in (2,3,4)}
    
    AreEqual(hasattr(C, 'x'), False)
    
    class C:
        abc = {locals()['x'] for x in (2,3,4)}
    
    AreEqual(C.abc, set([2,3,4]))

    d = {}
    exec(compile("abc = {locals()['x'] for x in (2,3,4)}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], set([2,3,4]))
    
    d = {'y':42}
    exec(compile("abc = {y for x in (2,3,4)}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], set([42]))

    d = {'y':42, 't':(2,3,42)}
    exec(compile("abc = {y for x in t if x == y}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], set([42]))

    t = (2,3,4)
    v = 2
    abc = {v for x in t}
    AreEqual(abc, set([2]))

    abc = {x for x in t if x == v}
    AreEqual(abc, set([2]))
    
    def f():
        abc = {x for x in t if x == v}
        AreEqual(abc, set([2]))
        
    f()
    
    def f():
        abc = {v for x in t}
        AreEqual(abc, set([2]))
        
        
    class C:
        abc = {v for x in t}
        AreEqual(abc, set([2]))
        
    class C:
        abc = {x for x in t if x == v}
        AreEqual(abc, set([2]))

def test_scope_mixing():
    k = 1
    v = 3

    # in source
    r = {k for k in range(v)} # TODO: "xrange(v + k)" fails in IPY, but not in CPython
    AreEqual(r, set([0, 1, 2]))

    # in condition
    r = {k for k in range(4) if k < v}
    AreEqual(r, set([0, 1, 2]))

    # in item generation
    r = {k+v for k in range(2)}
    AreEqual(r, set([3, 4]))

def test_scope_mixing_closures():
    # see also: GitHub issue #1196

    def eval(f, i):
        return f(i)

    v = 2

    # in source
    r = {k for k in eval(lambda i: range(i+v), v)}
    AreEqual(r, set([0, 1, 2, 3]))

    # in condition
    r = {k for k in range(4) if eval(lambda i: i>=v, k)}
    AreEqual(r, set([2, 3]))

    # in item generation
    r = {eval(lambda i: i+v, k+v) for k in range(2)}
    AreEqual(r, set([4, 5]))

run_test(__name__)