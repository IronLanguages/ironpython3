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


# Test that hash and equality operations cooperate for numbers.
#  cmp(x,y)==0 --> hash(x) == hash(y).
#
# Python has equality comparisons between int, float, long (BigInteger), and Complex

from iptest.assert_util import *

# Check the hash invariant for equal objects.
def check(x,y):
  Assert(cmp(x,y)==0)
  Assert(hash(x) == hash(y))

def test_integer():
  i = 123456
  check(i, long(i))
  check(i, float(i))
  check(i, complex(i,0))


# bug 315746
def test_float_long():
  f=float(1.23e300)
  l=long(f)
  check(f,l)


# Test with complex + float + int
def test_complex_float():
  for (c, f) in [ (0j+3.5, 3.5), (3e-6 + 0j, 3.0e-6), (4.5e+300 + 0j, 4.5e+300)]:
    check(c,f)

# Test with complex and BigInts
# Bug 320650
def test_complex_bigint():
  l=5294967296
  c=complex(l,0)
  check(l,c)


#
# Test hash qualities. We want to ensure that we get different hash results
# for similar, yet different, inputs.
#


# Test that floating hash is decent enough to distribute between decimal digits
# Bug 320645
def test_floathash_quality():
  f = 1.5
  h1 = hash(f)
  h2 = hash(f +.1)
  Assert(h1 != h2)

# Ensure that we have a decent hash function that doesn't just map
# everything to zero.
# bug 320659
def test_bigint_hash_quality():
  l1=long(1.23e300)
  h1 = hash(l1)
  Assert(h1 != 0)
  l2 = l1 + 1
  Assert(l1 != l2)
  h2 = hash(l2)
  Assert(h1 != h2)


def test_userhash_result():
    class x(object):
        def __hash__(self): return 1L
    
    AreEqual(hash(x()), 1)

    class x(object):
        def __hash__(self): return 1<<33L
    
    if not is_net40: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=25894
        AreEqual(hash(x()), 2)

@skip("win32")
def test_cli_number_hash():
    from iptest.type_util import clr_numbers
    
    for name, value in clr_numbers.iteritems():
        if name.find('Single') != -1:
            AreEqual(hash(value), hash(float(value)))
        else:
            AreEqual(hash(value), hash(long(value)))


def test_bigint_hash_subclass():
    class x(long):
        def __hash__(self): return 42
        
    AreEqual(hash(x()), 42)


run_test(__name__)
