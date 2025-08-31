# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
Various method signatures to override.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

global expected

# from Merlin.Testing import *
# from Merlin.Testing.BaseClass import *

@skipUnlessIronPython()
class MethodSignatureTest(IronPythonTestCase):
    def setUp(self):
        super(MethodSignatureTest, self).setUp()
        from clr import StrongBox
        from System import Int32

        self.add_clr_assemblies("baseclasscs", "typesamples")
        self.box_int = StrongBox[Int32]

    def test_one_ref(self):
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_a
        
        class C(IInterface600): 
            def m_a(self, arg):
                arg.Value = arg.Value + 3
                
        x = C()
        a = self.box_int(10)
        self.assertEqual(f(x, a), None)
        self.assertEqual(a.Value, 13)
        
        a = 20
        self.assertEqual(f(x, a), 23)
        self.assertEqual(a, 20)
        
        a = self.box_int(10)
        x.m_a(a)
        self.assertEqual(a.Value, 13)
        
        a = 20
        self.assertRaises(AttributeError, x.m_a, a)
        
        # inproper usage ...
        class C(IInterface600):
            def m_a(self, arg):
                arg = 20
                
        x = C()
        a = self.box_int(10)
        self.assertEqual(f(x, a), None)
        self.assertEqual(a.Value, 10)
        
        a = 11
        self.assertEqual(f(x, a), 11)
        self.assertEqual(a, 11)
        
        class C(IInterface600):
            def m_a(self):
                pass
        x = C()
        a = self.box_int(10)
        self.assertRaises(TypeError, f, x, a)



    #TODO: @skip("multiple_execute")
    def test_one_out(self):
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_b

        def checkEqual(first, second):
            self.assertEqual(first, second)
        
        class C(IInterface600):
            def m_b(self, arg):
                checkEqual(arg.Value, expected)
                arg.Value = 14
        x = C()
        a = self.box_int(10)
        expected = 10
        self.assertEqual(f(x, a), None)
        self.assertEqual(a.Value, 14)
        
        a = self.box_int(10)
        expected = 10
        x.m_b(a)
        self.assertEqual(a.Value, 14)
        
        self.assertRaisesMessage(TypeError, 
            "expected StrongBox[Int32], got Int32", 
            f, x, 10)
        self.assertRaisesMessage(TypeError, 
            "expected StrongBox[Int32], got NoneType", 
            f, x, None)
        
        class C(IInterface600):
            def m_b(self, arg): pass    # do not assign arg in the body
        x = C()
        a = self.box_int(10)
        f(x, a)
        self.assertEqual(a.Value, 10)

        class C(IInterface600):
            def m_b(self): return 16    # omit "out" arg
        x = C()
        self.assertRaisesMessage(TypeError, 
            "m_b() takes exactly 1 argument (2 given)", 
            f, x)                       # bug 370002
        self.assertRaisesMessage(TypeError, 
            "expected StrongBox[Int32], got Int32", 
            f, x, 1)
        a = self.box_int(10)
        self.assertRaisesMessage(TypeError, 
            "m_b() takes exactly 1 argument (2 given)", 
            f, x, a)

    def test_one_array(self):
        import System
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_c

        def checkEqual(first, second):
            self.assertEqual(first, second)
        
        class C(IInterface600):
            def m_c(self, arg):
                if arg:
                    checkEqual(sum(arg), expected)
                    arg[0] = 10

        x = C()
        a = System.Array[System.Int32]([1,2])
        expected = 3
        f(x, a)
        self.assertEqual(a[0], 10)
        
        f(x, None)
    
    def test_one_param_array(self):
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_d
        
        def checkEqual(first, second):
            self.assertEqual(first, second)

        class C(IInterface600):
            def m_d(self, *arg): 
                checkEqual(len(arg), expected)

        x = C()
        expected = 0
        f(x)
        
        expected = 1
        f(x, 1)
        
        expected = 3
        f(x, *(1, 2, 3))
        
        class C(IInterface600):
            def m_d(self):
                pass
        x = C()
        f(x)
        self.assertRaises(TypeError, f, x, 1)
        
        class C(IInterface600):
            def m_d(self, arg1, arg2, arg3):
                pass
        x = C()
        f(x, 1, 2, 3)
        self.assertRaises(TypeError, f, x, 1)

        class C(IInterface600):
            def m_d(self, arg1, *arg2):
                checkEqual(len(arg2) + arg1, expected)
                
        x = C()
        expected = 1; f(x, 1)
        expected = 2; f(x, 1, 2)
        expected = 3; f(x, 1, 2, 3)
        expected = 4; f(x, 1, 2, 3, 4)
        expected = 5; f(x, 1, 2, 3, 4, 5)

    def test_return_something(self):
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_e
        
        class C(IInterface600):
            def m_e(self): 
                pass
        x = C()
        self.assertRaisesMessage(TypeError,
            "expected int, got NoneType", 
            f, x)
        
        C.m_e = lambda self: 10
        self.assertEqual(f(x), 10)
        
        C.m_e = lambda self: "abc"
        self.assertRaisesMessage(TypeError,
            "expected int, got str", 
            f, x)
            
            
        f = IInterface600.m_f
        class C(IInterface600):
            def m_f(self): 
                return 10
        x = C()            
        self.assertEqual(f(x), None)

    #TODO:@skip("multiple_execute")
    def test_two_args(self):
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_g
        class C(IInterface600):
            def m_g(self, arg1, arg2):
                temp = arg1.Value + 9
                arg1.Value = arg2 + 8
                return temp
        
        x = C()
        
        a = self.box_int(10)
        b = 20
        
        self.assertEqual(f(x, a, b), 19)
        self.assertEqual(a.Value, 28)
        
        self.assertEqual(f(x, 1, 2), (10, 10))
        
        f = IInterface600.m_h

        def checkEqual(first, second):
            self.assertEqual(first, second)

        class C(IInterface600):
            def m_h(self, arg1, arg2): 
                checkEqual(arg1.Value, 10)
                arg1.Value = arg2.Value + 7
                arg2.Value = 6
                return 5
                
        x = C()
        
        a = self.box_int(10)
        b = self.box_int(20)
        self.assertEqual(f(x, a, b), 5)
        self.assertEqual(a.Value, 27)
        self.assertEqual(b.Value, 6)
        
        self.assertRaises(TypeError, f, x, a, 20)
        self.assertRaises(TypeError, f, x, 10, b)
        self.assertRaises(TypeError, f, x, 10, 20)

    def test_ref_out_normal(self):
        import System
        from Merlin.Testing.BaseClass import IInterface600
        f = IInterface600.m_l
        
        class C(IInterface600):
            def m_l(self, arg1, arg2, arg3): 
                return 1
        
        x = C()
        a = self.box_int(10)
        b = self.box_int(20)
        c = 30
        
        #f(x, a, b, c) # bug 370075
        
        C.m_l = lambda self, arg1, arg2, arg3, *arg4: 2
        #f(x, a, b, c, 1, 2)

        C.m_l = lambda self, arg1, arg2, arg3, arg4, arg5: 3
        #f(x, a, b, c, 1, 2)
        
        f = IInterface600.m_k
        
        class C(IInterface600):
            def m_k(self, arg1, arg2, arg3, arg4):
                arg2.Value = arg1.Value * 3
                arg1.Value = arg3 * 5
                temp = sum(arg4)
                arg4[0] = 100            
                return temp

        x = C()
        a = self.box_int(10)
        b = self.box_int(20)
        c = 3
        d = System.Array[System.Int32]([40, 50])
        self.assertEqual(f(x, a, b, c, d), 90)
        self.assertEqual(a.Value, 15)
        self.assertEqual(b.Value, 30)
        self.assertEqual(d[0], 100)
    

run_test(__name__)
