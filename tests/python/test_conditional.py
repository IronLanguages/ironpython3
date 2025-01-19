# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import run_test

class ConditionalTest(unittest.TestCase):
    
    def test_simple(self):
        # Simple conditional true case
        self.assertTrue(100 if 1 else 200 == 100)

        # Simple conditional false case
        self.assertTrue(0 if 0 else 200 == 200)

    def test_multiple_assignment(self):
        # Conditional multiple assignment
        x, y, z, w, u  = 1 if 0 else 2, 2 if 1 else 3, 3 if 10 else 4, 1 & 0 if 0 and 3 or 4 else 100, 1 and 0 if 0 and 3 or 4 & 0 else 100
        self.assertEqual((x,y,z,w,u), (2,2,3,0,100))

    def test_in_expressions(self):
        # combination of operators and conditional
        self.assertTrue(100 + 1 & 3 if 1 else 2 == 1)
        self.assertTrue(100 + (1 & 3 if 1 else 2) == 101)

    def test_if_else(self):
        # conditional in if-else
        x, y, z = 0,1,2
        if x if y else z:
            p = 100
        else:
            p = 200
        self.assertTrue(p == 200)

    def test_nested_conditionals(self):
        # nested conditionals
        if 0 if (0 if 100 else 1 ) else 10:
            x = 300
        else:
            x = 400
        self.assertTrue(x == 300)

    def test_conditionals_with_test_list_1(self):
        # conditionals with test-list #test1
        x,y,z = 1,2,3
        if 20 if (x,y,z == 1,2,3) else 0:
            x = 300
        else:
            x = 400
        self.assertTrue(x == 300)


    def test_conditionals_with_test_list_2(self):
        # conditionals with test-list #test2
        list = [[1 if 1 else 0,0 if 0 else 2,3],[4,5 if 1 and 1 else 0,8 if 0 and 1 else 6 & 7]]
        if 20 if (list == [[1,2,3],[4,5,7]]) else 0 if 1 else 200 :
            x = 300
        else:
            x = 400
        self.assertTrue(x == 400)

    def test_generator_expressions(self):
        #test for gen_for
        self.assertTrue(sum(x*x for x in range(10) if not x%2 if not x%3) == sum([x*x for x in range(10) if not x%6]))
        
        #test for gen_for gen_if combined
        self.assertTrue(sum(x*x for x in range(10) for x in range(5) if not x %2) == 200)

    def test_list_for(self):
        #test for list_for
        list = [10,20,30,40,50,60,70,80,90,100,110,120,130]
        mysum = 0
        for i in (0,1,2,3,4,5,6,7,8,9,10,11,12):
            mysum += list[i]
        self.assertTrue(mysum == 910)

        #test for list_for list_if combined
        list = [10,20,30,40,50,60,70,80,90,100,110,120,130]
        self.assertTrue(sum(list[i] if not i%2 else 0 for i in (0,1,2,3,4,5,6,7,8,9,10,11,12) if not i %3 if not 0) == 210)

    def test_errors(self):
        #test for null list
        self.assertRaises(SyntaxError, compile, "mysum = 0;for i in 10:pass", "Error", "exec")
        # test for lambda function
        self.assertRaises(SyntaxError, compile, "[f for f in 1, lambda x: x if x >= 0 else -1]", "", "exec")

    def test_conditional_in_lambda(self):
        try:
            list = [f for f in (1, lambda x: x if x >= 0 else -1)]
            list = [f for f in (1, lambda x: (x if x >= 0 else -1))]
            list = [f for f in (1, (lambda x: x if x >= 0 else -1))]
        except e:
            self.fail(e.message)

    def test_conditional_return_types(self):
        '''11491'''
        class OldK: pass
        
        class NewK(object): pass
        
        for x in [
                    -2, -1, 0, 1, 2, 2**16,
                    -2, -1, 0, 1, 2, 2**32,
                    3.14,
                    2j,
                    "", "abc",
                    {}, {'a':'b'}, {'a':'b', 'c':'d'},
                    [], [1], [1, 2],
                    range(0), range(1), range(2),
                    OldK, NewK, OldK(), NewK(),
                    None, str, object,
                    ]:
            temp = 0 if 0 else x
            self.assertEqual(temp, x)

    def test_conversions(self):
        self.assertEqual(1 if False else "Hello", "Hello")
        self.assertEqual("Hello" if False else 1, 1)
        self.assertEqual(1 if True else "Hello", 1)
        self.assertEqual("Hello" if False else "Goodbye", "Goodbye")

        if (1 if True else False):
            pass
        else:
            self.fail("Expression incorrectly evaluated")

    def test_cp13299(self):
        true_conditions = [ 1, 1, -1, -1, True, 1.1, -1.1, "abc", int, 0.1, -0.1]
        false_conditions = [ 0, 0, None, 0.0, -0, -0.0, False, (), [], ""]
        
        for condition in true_conditions:
            x = condition if condition else False
            self.assertEqual(x, condition)

        for condition in false_conditions:
            x = True if condition else condition
            self.assertEqual(x, condition)
                
        self.assertEqual(3.14 if True else 1, 3.14)
        self.assertEqual(3.14 if True else 1, 3.14)
        self.assertEqual(3.14 if True else -1, 3.14)
        self.assertEqual(3.14 if True else True, 3.14)
        self.assertEqual(3.14 if True else 1.1, 3.14)
        self.assertEqual(3.14 if True else -1.1, 3.14)
        self.assertEqual(3.14 if True else "abc", 3.14)
        self.assertEqual(3.14 if True else int, 3.14)
        self.assertEqual(3.14 if True else 0, 3.14)
        self.assertEqual(3.14 if True else 0, 3.14)
        self.assertEqual(3.14 if True else None, 3.14)
        self.assertEqual(3.14 if True else False, 3.14)
        self.assertEqual(3.14 if True else (), 3.14)
        self.assertEqual(3.14 if True else [[1]], 3.14)
        self.assertEqual(3.14 if True else "", 3.14)
        
        self.assertEqual(1 if False else 3.14, 3.14)
        self.assertEqual(1 if False else 3.14, 3.14)
        self.assertEqual(-1 if False else 3.14, 3.14)
        self.assertEqual(True if False else 3.14, 3.14)
        self.assertEqual(1.1 if False else 3.14, 3.14)
        self.assertEqual(-1.1 if False else 3.14, 3.14)
        self.assertEqual("abc" if False else 3.14, 3.14)
        self.assertEqual(int if False else 3.14, 3.14)
        self.assertEqual(0 if False else 3.14, 3.14)
        self.assertEqual(0 if False else 3.14, 3.14)
        self.assertEqual(None if False else 3.14, 3.14)
        self.assertEqual(False if False else 3.14, 3.14)
        self.assertEqual(() if False else 3.14, 3.14)
        self.assertEqual([[1]] if False else 3.14, 3.14)
        self.assertEqual("" if False else 3.14, 3.14)
    
    def test_large_if(self):
        def f(value):
            if value:
                return 42
            elif value:
                raise Exception()
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif value:
                pass
            elif True:
                return 23

        for i in range(10000):
            self.assertEqual(f(True), 42)

        for i in range(10000):
            self.assertEqual(f(False), 23)


run_test(__name__)
