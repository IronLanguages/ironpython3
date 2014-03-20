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
## Testing list comprehension
##

from iptest.assert_util import *

## positive

AreEqual([x for x in ""], [])
AreEqual([x for x in xrange(2)], [0, 1])
AreEqual([x + 10 for x in [-11, 4]], [-1, 14])
AreEqual([x for x in [y for y in range(3)]], [0, 1, 2])
AreEqual([x for x in range(3) if x > 1], [2])
AreEqual([x for x in range(10) if x > 1 if x < 4], [2, 3])
AreEqual([x for x in range(30) for y in range(2) if x > 1 if x < 4], [2, 2, 3, 3])
AreEqual([(x,y) for x in range(30) for y in range(3) if x > 1 if x < 4 if y > 1], [(2, 2), (3, 2)])
AreEqual([(x,y) for x in range(30) if x > 1 for y in range(3) if x < 4 if y > 1], [(2, 2), (3, 2)])
AreEqual([(x,y) for x in range(30) if x > 1 if x < 4 for y in range(3) if y > 1], [(2, 2), (3, 2)])
AreEqual([(x,y) for x in range(30) if x > 1 for y in range(5) if x < 4 if y > x], [(2, 3), (2, 4), (3, 4)])
AreEqual([(x,y) for x in range(30) if x > 1 for y in range(5) if y > x if x < 4], [(2, 3), (2, 4), (3, 4)])
AreEqual([(y, x) for (x, y) in ((1, 2), (2, 4))], [(2, 1), (4, 2)])
y = 10
AreEqual([y for x in "python"], [y] * 6)
AreEqual([y for y in "python"], list("python"))
y = 10
AreEqual([x for x in "python" if y > 5], list("python"))
AreEqual([x for x in "python" if y > 15], list())
AreEqual([x for x, in [(1,)]], [1])

## negative

AssertError(SyntaxError, compile, "[x if x > 1 for x in range(3)]", "", "eval")
AssertError(SyntaxError, compile, "[x for x in range(3);]", "", "eval")
AssertError(SyntaxError, compile, "[x for x in range(3) for y]", "", "eval")

del y
AssertError(NameError, lambda: [y for x in "python"])
AssertError(NameError, lambda: [x for x in "python" if y > 5])
AssertError(NameError, lambda: [x for x in "iron" if y > x for y in "python" ])
AssertError(NameError, lambda: [x for x in "iron" if never_shown_before > x ])
AssertError(NameError, lambda: [(x, y) for x in "iron" if y > x for y in "python" ])
AssertError(NameError, lambda: [(i, j) for i in range(10) if j < 'c' for j in ['a', 'b', 'c'] if i % 3 == 0])

## flow checker
def test_negative():
    try: [y for x in "python"]
    except NameError: pass
    else: Fail()
    try: [x for x in "python" if y > 5]
    except NameError: pass
    else: Fail()
    try: [x for x in "iron" if y > x for y in "python" ]
    except NameError: pass
    else: Fail()
    try: [(x, y) for x in "iron" if y > x for y in "python" ]
    except NameError: pass
    else: Fail()
    try: [(i, j) for i in range(10) if j < 'c' for j in ['a', 'b', 'c'] if i % 3 == 0]
    except NameError: pass
    else: Fail()

test_negative()
