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

from iptest.assert_util import *
import _random


#getrandbits
def test_getrandbits():

    #the argument is a random int value
    rand = _random.Random()
    for i1 in xrange(1, 1984, 6):
        Assert(rand.getrandbits(i1) < (2**i1))
        Assert(rand.getrandbits(i1) < (2**i1))
        Assert(rand.getrandbits(i1+1) < (2**(i1+1)))
        Assert(rand.getrandbits(i1+1) < (2**(i1+1)))
    
    temp_list = [ 63, #maxvalue
                  32, #bits less than 32
                  50, #bits greater than 32 and less than 64
                  100 #bits greater than 64
                ]
                
    for x in temp_list:
        Assert(rand.getrandbits(x) < (2**x))
        
    rand = _random.Random()
    
    AssertError(ValueError, rand.getrandbits, 0)
    AssertError(ValueError, rand.getrandbits, -50)
    
    # might raise OverflowError, might not, but shouldn't raise anything else.
    try:
        rand.getrandbits(2147483647)
    except OverflowError:
        pass
    
#jumpahead
def test_jumpahead():
    rand = _random.Random()
    old_state = rand.getstate()
    rand.jumpahead(100)
    #CodePlex Work Item 8294
    #Assert(old_state != rand.getstate())
    

#random
def test_random():
    rand = _random.Random()
    result = rand.random()
    flag = result<1.0 and result >= 0.0
    Assert(flag,
           "Result is not the value as expected,expected the result between 0.0 to 1.0,but the actual is not")
    
#setstate
def test_setstate():
    # state is object which
    random = _random.Random()
    state1 = random.getstate()
    random.setstate(state1)
    state2 = random.getstate()
    AreEqual(state1,state2)
    
    random.jumpahead(1)
    #CodePlex Work Item 8294
    #Assert(state1 != random.getstate())
    
    random.setstate(state1)
    AreEqual(state1, random.getstate())
    
    #state is a int object
    a = 1
    AssertError(Exception,random.setstate,a)
    
    #state is a string object
    b = "stete"
    AssertError(Exception,random.setstate,b)
    
    #state is a random object
    c = _random.Random()
    AssertError(Exception,random.setstate,c)
    

#getstate
def test_getstate():
    random = _random.Random()
    a = random.getstate()
    AreEqual(a, random.getstate())
    
    i = 2
    random = _random.Random(i)
    b = random.getstate()
    AreEqual(b, random.getstate())
    
    str = "state"
    random = _random.Random(str)
    c = random.getstate()
    AreEqual(c, random.getstate())


#seed
def test_seed():
    i= 2
    random = _random.Random(i)
    a = random.getstate()
    
    # parameter is None
    random.seed()
    b =random.getstate()
    if a == b:
        Fail("seed() method can't change the current internal state of the generator.")
  
    
    # parameter is int
    x = 1
    random.seed(x)
    c = random.getstate()
    if b == c or a == c:
        Fail("seed(x) method can't change the current internal state of the generator when x is \
        int type.")
    
    # parameter is string
    x = "seed"
    random.seed(x)
    d = random.getstate()
    if d==c or b==d or a==d:
        Fail("seed(x) method can't change the current internal state of the generator when x is \
        string type.")
     
run_test(__name__)
