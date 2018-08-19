# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import _random
import unittest
from iptest import is_netcoreapp, run_test

@unittest.skipIf(is_netcoreapp, "https://github.com/IronLanguages/ironpython2/issues/373")
class _RandomTest(unittest.TestCase):
    def test_getrandbits(self):

        #the argument is a random int value
        rand = _random.Random()
        for i1 in range(1, 1984, 6):
            self.assertTrue(rand.getrandbits(i1) < (2**i1))
            self.assertTrue(rand.getrandbits(i1) < (2**i1))
            self.assertTrue(rand.getrandbits(i1+1) < (2**(i1+1)))
            self.assertTrue(rand.getrandbits(i1+1) < (2**(i1+1)))
        
        temp_list = [ 63, #maxvalue
                    32, #bits less than 32
                    50, #bits greater than 32 and less than 64
                    100 #bits greater than 64
                    ]
                    
        for x in temp_list:
            self.assertTrue(rand.getrandbits(x) < (2**x))
            
        rand = _random.Random()
        
        self.assertRaises(ValueError, rand.getrandbits, 0)
        self.assertRaises(ValueError, rand.getrandbits, -50)
        
        # might raise OverflowError, might not, but shouldn't raise anything else.
        try:
            rand.getrandbits(2147483647)
        except OverflowError:
            pass
    
    def test_random(self):
        rand = _random.Random()
        result = rand.random()
        flag = result<1.0 and result >= 0.0
        self.assertTrue(flag,
            "Result is not the value as expected,expected the result between 0.0 to 1.0,but the actual is not")
    
    def test_setstate(self):
        # state is object which
        random = _random.Random()
        state1 = random.getstate()
        random.setstate(state1)
        state2 = random.getstate()
        self.assertEqual(state1,state2)
        
        random.random()
        self.assertTrue(state1 != random.getstate())
        
        random.setstate(state1)
        self.assertEqual(state1, random.getstate())
        
        #state is a int object
        a = 1
        self.assertRaises(Exception,random.setstate,a)
        
        #state is a string object
        b = "stete"
        self.assertRaises(Exception,random.setstate,b)
        
        #state is a random object
        c = _random.Random()
        self.assertRaises(Exception,random.setstate,c)

    def test_getstate(self):
        random = _random.Random()
        a = random.getstate()
        self.assertEqual(a, random.getstate())
        
        i = 2
        random = _random.Random(i)
        b = random.getstate()
        self.assertEqual(b, random.getstate())
        
        str = "state"
        random = _random.Random(str)
        c = random.getstate()
        self.assertEqual(c, random.getstate())

    def test_seed(self):
        i= 2
        random = _random.Random(i)
        a = random.getstate()
        
        # parameter is None
        random.seed()
        b =random.getstate()
        if a == b:
            self.fail("seed() method can't change the current internal state of the generator.")
    
        
        # parameter is int
        x = 1
        random.seed(x)
        c = random.getstate()
        if b == c or a == c:
            self.fail("seed(x) method can't change the current internal state of the generator when x is \
            int type.")
        
        # parameter is string
        x = "seed"
        random.seed(x)
        d = random.getstate()
        if d==c or b==d or a==d:
            self.fail("seed(x) method can't change the current internal state of the generator when x is \
            string type.")

run_test(__name__)