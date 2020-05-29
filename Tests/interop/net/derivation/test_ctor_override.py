# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

# http://docs.python.org/ref/customization.html

@skipUnlessIronPython()
class CtorOverrideTest(IronPythonTestCase):
    def setUp(self):
        super(CtorOverrideTest, self).setUp()
        from System import Array
        from clr import StrongBox
        self.box_int = StrongBox[int]
        self.array_int = Array[int]

        self.add_clr_assemblies("baseclasscs", "typesamples")

    def test_0(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor10
        class C(CCtor10): pass
        C()
        Flag.Check(42)
        self.assertRaisesMessage(TypeError, "object() takes no parameters", C, 1)

        class C(CCtor10): 
            def __new__(cls, arg1, arg2):
                return super(C, cls).__new__(cls)
        C(1, 2)
        Flag.Check(42)

    def test_1_normal(self): 
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor20
        class C(CCtor20): pass
        C(1)
        Flag.Check(1)
        
        self.assertRaises(TypeError, C)
        self.assertRaises(TypeError, C, 1, 2)
        
        class C(CCtor20):
            def __new__(cls, arg):
                return super(C, cls).__new__(cls, arg)
    
        C(2)
        Flag.Check(2)
        
        class C(CCtor20):
            def __new__(cls, arg):
                return super(C, cls).__new__(cls)
                
        self.assertRaises(TypeError, C, 2)
        
        class C(CCtor20):
            def __new__(cls):
                return super(C, cls).__new__(cls, 3)
        
        C()
        Flag.Check(3)

    def test_1_ref(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor21
        class C(CCtor21): pass
        #x, y = C(1)
        #Flag.Check(1)
        #AreEqual(y, 1)   # 313045
        
        #y = box_int(2)
        #C(y)
        #Flag.Check(2)
        #AreEqual(y.Value, 2)  # 313045
        
        # TODO
        class C(CCtor21):
            def __new__(cls, arg):
                return super(C, cls).__new__(cls, arg)
        C(3)    
    
    def test_1_array(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor30
        class C1(CCtor30): pass
        class C2(CCtor30):
            def __new__(cls, arg):
                return super(cls, C2).__new__(cls, arg)

        for C in [C1, C2]:
            self.assertRaises(TypeError, C)
            self.assertRaises(TypeError, C, 1)
            
            C(self.array_int([]))
            Flag.Check(0)
            
            C(self.array_int([1, 2]))
            Flag.Check(3)
            
            #C(None)  # 374293
            #Flag.Check(-10)
    
    def test_1_param_array(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor31
        class C1(CCtor31): pass
        class C2(CCtor31):
            def __new__(cls, *arg):
                return super(cls, C2).__new__(cls, *arg)

        for C in [C1, C2]:
            C(); Flag.Check(-20)
            #C(None); Flag.Check(-40)
            C(1); Flag.Check(1)
            C(2, 3); Flag.Check(5)
            
            C(self.array_int([])); Flag.Check(-20)
            C(self.array_int([4, 5, 6])); Flag.Check(15)

            self.assertRaises(TypeError, lambda: C([4, 5, 6]))

        class C3(CCtor31):
            def __new__(cls, arg):
                return super(cls, C3).__new__(cls, *arg)
                
        C3([1, 2]); Flag.Check(3)
        C(self.array_int([4, 5, 6])); Flag.Check(15)


    def test_5_args(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor40
        class C(CCtor40): pass
        C(1, 2, arg4=4, *(3, ), **{'arg5':5})
        Flag.Check(12345)
        
        class C(CCtor40):
            def __new__(cls, *args): 
                return super(cls, C).__new__(cls, *args)

        #self.assertRaisesMessage(TypeError, "CCtor40() takes exactly 6 arguments (2 given)", C, 2) # bug 374515
        C(2, 1, 3, 5, 4)
        Flag.Check(21354)
        
        class C(CCtor40):
            def __new__(cls, arg1, arg2, *arg3, **arg4): 
                return super(cls, C).__new__(cls, arg1, arg2, *arg3, **arg4)
        
        self.assertRaisesMessage(TypeError, "__new__() got multiple values for keyword argument 'arg2'", eval, "C(3, arg2=1, *(2, 5), **{'arg5' : 4})", globals(), locals())
        
        C(3, 1, *(2, 5), **{'arg5' : 4})
        Flag.Check(31254)

    def test_overload1(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor50
        class C(CCtor50):
            def __new__(cls, arg):
                return super(C, cls).__new__(cls, arg)
        C(1)
        Flag.Check(1)

        self.assertRaises(TypeError, C, 1, 2)
        
        class C(CCtor50):
            def __new__(cls, arg):
                return super(C, cls).__new__(cls, arg, 10)
        C(2)
        Flag.Check(12)
        
        class C(CCtor50):
            def __new__(cls, arg1, arg2):
                return super(C, cls).__new__(cls, arg1 + arg2)
                
        C(3, 4)
        Flag.Check(7)
        
        self.assertRaises(TypeError, C, 3)
        
        class C(CCtor50):
            def __new__(cls, arg1, arg2):
                return super(C, cls).__new__(cls, arg1, arg2)
        C(5, 6)
        Flag.Check(11)
   
    def test_overload2(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor51
        class C1(CCtor51):
            def __new__(cls, *args): 
                return super(cls, C1).__new__(cls, *args)
        class C2(CCtor51):
            def __new__(cls, arg1, *arg2): 
                return super(cls, C2).__new__(cls, arg1, *arg2)

        # more?
        
        for C in [C1, C2]:
            C(1);         Flag.Check(10)
            C(1, 2);      Flag.Check(20)
            C(self.array_int([1, 2, 3]));        Flag.Check(20)
        
    
    def test_related_to_init(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import CCtor20
        class C(CCtor20):
            def __new__(cls, arg):
                x = super(C, cls).__new__(cls, arg)
                Flag.Check(arg)
                Flag.Set(arg * 2)
                return x
            def __init__(self, arg):
                Flag.Check(arg * 2)
                Flag.Set(arg * 3)
        
        C(4)
        Flag.Check(12)
        
        C.__init__ = lambda self, arg1, arg2: None
        self.assertRaises(TypeError, C, 4)
        
        C.__init__ = lambda self, arg: arg
        #self.assertRaises(TypeError, C, 4) # bug 374136
        
        class C(CCtor20):
            def __new__(cls, arg):
                super(C, cls).__new__(cls, arg)  # no return
            def __init__(self, arg):
                Flag.Set(2)                      # then not called here
        
        C(5)
        Flag.Check(5)

        class C(CCtor20):                        # no explicit __new__
            def __init__(self, arg):
                Flag.Check(6)
                Flag.Set(7)
        C(6)
        Flag.Check(7)
    
run_test(__name__)

