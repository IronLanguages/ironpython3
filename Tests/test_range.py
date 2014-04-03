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
## Test range and xrange
##
## * sbs_builtin\test_xrange covers many xrange corner cases
##

from iptest.assert_util import *

def test_range():
    Assert(range(10) == [0, 1, 2, 3, 4, 5, 6, 7, 8, 9])
    Assert(range(0) == [])
    Assert(range(-10) == [])

    Assert(range(3,10) == [3, 4, 5, 6, 7, 8, 9])
    Assert(range(10,3) == [])
    Assert(range(-3,-10) == [])
    Assert(range(-10,-3) == [-10, -9, -8, -7, -6, -5, -4])

    Assert(range(3,20,2) == [3, 5, 7, 9, 11, 13, 15, 17, 19])
    Assert(range(3,20,-2) == [])
    Assert(range(20,3,2) == [])
    Assert(range(20,3,-2) == [20, 18, 16, 14, 12, 10, 8, 6, 4])
    Assert(range(-3,-20,2) == [])
    Assert(range(-3,-20,-2) == [-3, -5, -7, -9, -11, -13, -15, -17, -19])
    Assert(range(-20,-3, 2) == [-20, -18, -16, -14, -12, -10, -8, -6, -4])
    Assert(range(-20,-3,-2) == [])

def _xrange_eqv_range(r, o):
    Assert(len(r) == len(o))
    for i in range(len(r)):
        Assert(r[i]==o[i])
        if (1 - i) == len(r):
            AssertError(IndexError, lambda: r[1-i])
            AssertError(IndexError, lambda: o[1-i])
        else:
            Assert(r[1-i] == o[1-i])

def test_xrange_based_on_range():
    for x in (10, -1, 0, 1, -10):
        _xrange_eqv_range(xrange(x), range(x))

    for x in (3, -3, 10, -10):
        for y in (3, -3, 10, -10):
            _xrange_eqv_range(xrange(x, y), range(x, y))

    for x in (3, -3, 20, -20):
        for y in (3, -3, 20, -20):
            for z in (2, -2):
                _xrange_eqv_range(xrange(x, y, z), range(x, y, z))

    for x in (7, -7):
        for y in (20, 21, 22, 23, -20, -21, -22, -23):
            for z in (4, -4):
                _xrange_eqv_range(xrange(x, y, z), range(x, y, z))

def test_xrange_corner_cases():
    import sys
    x = xrange(0, sys.maxint, sys.maxint-1)
    AreEqual(x[0], 0)
    AreEqual(x[1], sys.maxint-1)

def test_xrange_coverage():
    ## ToString
    AreEqual(str(xrange(0, 3, 1)), "xrange(3)")
    AreEqual(str(xrange(1, 3, 1)), "xrange(1, 3)")
    AreEqual(str(xrange(0, 5, 2)), "xrange(0, 6, 2)")

    ## Long
    AreEqual([x for x in xrange(5L)], range(5))
    AreEqual([x for x in xrange(10L, 15L)], range(10, 15))
    AreEqual([x for x in xrange(10L, 15L, 2)], range(10, 15,2 ))
    
    ## Ops
    AssertError(TypeError, lambda: xrange(4) + 4)
    AssertError(TypeError, lambda: xrange(4) * 4)
    AssertError(TypeError, lambda: xrange(4)[:2])
    AssertError(TypeError, lambda: xrange(4)[1:2:3])


run_test(__name__)
