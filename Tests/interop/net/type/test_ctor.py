# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
Calls to constructor.
'''

import unittest 

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class CtorTest(IronPythonTestCase):
    def setUp(self):
        super(CtorTest, self).setUp()
        self.add_clr_assemblies("methodargs", "typesamples")

#// 1 argument 
#public class Ctor100 {
    #public Ctor100(int arg) { }
#}
#public class Ctor101 {
    #public Ctor101([DefaultParameterValue(10)]int arg) { }
#}
#public class Ctor102 {
    #public Ctor102([Optional]int arg) { }
#}
    
    def test_ctor_1_arg(self):
        from clr import StrongBox
        from Merlin.Testing.Call import Ctor101, Ctor103, Ctor105, Ctor110
        box_int = StrongBox[int]
        Ctor101()
        
        #public class Ctor103 {
        #   public Ctor103(params int[] arg) { }
        #}
        Ctor103()
        Ctor103(1)
        Ctor103(1, 2, 3)
        Ctor105(a=1,b=2,c=3)
        #public class Ctor110 {
        #   public Ctor110(ref int arg) { arg = 10; }
        #}
        
        
        x, y = Ctor110(2)

        x = box_int()
        Ctor110(x)
        self.assertEqual(x.Value, 10)  # bug 313045
        

        #public class Ctor111 {
        #   public Ctor111(out int arg) { arg = 10; }
        #}

        #Ctor111() # bug 312989

        #x = box_int()
        #Ctor111(x)
        #self.assertEqual(x.Value, 10)   # bug 313045
    
    def test_object_array_as_ctor_args(self):
        from System import Array
        from Merlin.Testing.Call import Ctor104
        Ctor104(Array[object]([1,2]))
    
    def test_ctor_keyword(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import Ctor610
        def check(o):
            Flag[int, int, int].Check(1, 2, 3)
            self.assertEqual(o.Arg4, 4)
            Flag[int, int, int].Reset()
            
        x = 4
        o = Ctor610(1, arg2 = 2, Arg3 = 3, Arg4 = x); check(o)
        o = Ctor610(Arg3 = 3, Arg4 = x, arg1 = 1, arg2 = 2); check(o)
        #o = Ctor610(Arg3 = 3, Arg4 = x, *(1, 2)); check(o)

    def test_ctor_keyword2(self):
        """parameter name is same as property"""
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import Ctor620
        Ctor620(arg1 = 1)
        f = Flag[int, int, int, str]
        o = Ctor620(arg1 = 1, arg2 = 2); f.Check(1, 2, 0, None); f.Reset()
        o = Ctor620(arg1 = 1, arg2 = "hello"); f.Check(1, 0, 0, "hello"); f.Reset()
        #Ctor620(arg1 = 1, arg2 = 2, **{ 'arg1' : 3})
        pass

    def test_ctor_bad_property_field(self):
        from Merlin.Testing.Call import Ctor700, Ctor720, Ctor730, Ctor760
        self.assertRaisesMessage(AttributeError, "Property ReadOnlyProperty is read-only", lambda: Ctor700(1, ReadOnlyProperty = 1))
        self.assertRaisesMessage(AttributeError, "Field ReadOnlyField is read-only", lambda: Ctor720(ReadOnlyField = 2))
        self.assertRaisesMessage(AttributeError, "Field LiteralField is read-only", lambda: Ctor730(LiteralField = 3))
        #self.assertRaisesMessage(AttributeError, "xxx", lambda: Ctor710(StaticField = 10))
        #self.assertRaisesMessage(AttributeError, "xxx", lambda: Ctor750(StaticProperty = 10))
        self.assertRaisesMessage(TypeError, "Ctor760() takes no arguments (1 given)", lambda: Ctor760(InstanceMethod = 1))
        self.assertRaisesMessage(TypeError, "expected EventHandler, got int", lambda: Ctor760(MyEvent = 1))

    def test_set_field_for_value_type_in_ctor(self):
        from Merlin.Testing.Call import Struct
        # with all fields set
        x = Struct(IntField = 2, StringField = "abc", ObjectField = 4)
        self.assertEqual(x.IntField, 2)
        self.assertEqual(x.StringField, "abc")
        self.assertEqual(x.ObjectField, 4)

        # with partial field set
        x = Struct(StringField = "def")
        self.assertEqual(x.IntField, 0)
        self.assertEqual(x.StringField, "def")
        self.assertEqual(x.ObjectField, None)
        
        # with not-existing field as keyword
        # bug: 361389
        self.assertRaisesMessage(TypeError, 
            "CreateInstance() takes no arguments (2 given)", 
            lambda: Struct(IntField = 2, FloatField = 3.4))
        
        # set with value of "wrong" type
        # bug: 361389
        self.assertRaisesMessage(TypeError, 
            "expected str, got int", 
            lambda: Struct(StringField = 2))

    def test_cp14861(self):
        from Merlin.Testing.Call import Struct
        def foo():
            x = Struct(IntField = 2, StringField = "abc", ObjectField = 4)
            self.assertEqual(x.IntField, 2)
            self.assertEqual(x.StringField, "abc")
            self.assertEqual(x.ObjectField, 4)
        for i in xrange(2):
            foo()
            exec "foo()" in globals(), locals() 

run_test(__name__)

