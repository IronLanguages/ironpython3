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
'''
Operations on property.
'''
#------------------------------------------------------------------------------
import unittest

from iptest import IronPythonTestCase, run_test

# from Merlin.Testing import *
# from Merlin.Testing.Property import *  #Merlin 315120 - please do not remove/modify this line
# from Merlin.Testing.TypeSample import *

class PropertyTest(IronPythonTestCase):
    def setUp(self):
        super(PropertyTest, self).setUp()
        self.add_clr_assemblies("propertydefinitions", "typesamples")

    def test_explicitly_implemented_property(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Property import ClassExplicitlyImplement, ClassExplicitlyReadOnly, IData, IReadOnlyData, IWriteOnlyData, StructExplicitlyImplement, StructExplicitlyWriteOnly
        from Merlin.Testing.TypeSample import SimpleClass, SimpleStruct
        for t in [
                    ClassExplicitlyImplement, 
                    StructExplicitlyImplement,
                ]:
            x = t()

            self.assertTrue(hasattr(x, 'Number'))
            
            d = IData.Number
            d.SetValue(x, 20)
            self.assertEqual(d.GetValue(x), 20)
            
            d.__set__(x, 30)
            self.assertEqual(d.__get__(x), 30)
            
        x = ClassExplicitlyReadOnly()

        d = IReadOnlyData.Number
        self.assertRaisesMessage(SystemError, "cannot set property", lambda: d.SetValue(x, "abc"))
        self.assertEqual(d.GetValue(x), "python")
        #self.assertRaisesMessage(AttributeError, "ddd", lambda: d.__set__(x, "abc"))    # bug 362857
        self.assertEqual(d.__get__(x), "python")
        
        x = StructExplicitlyWriteOnly()
        
        d = IWriteOnlyData.Number
        d.SetValue(x, SimpleStruct(3)); 
        Flag.Check(13)
        self.assertRaisesMessage(AttributeError, "unreadable property", lambda: d.GetValue(x))
        
        d.__set__(x, SimpleStruct(4)); 
        Flag.Check(14)
        self.assertRaisesMessage(AttributeError, "unreadable property", lambda: d.__get__(x))
    
    
    def test_readonly(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Property import ClassWithReadOnly
        x = ClassWithReadOnly()
        
        self.assertEqual(x.InstanceProperty, 9)
        def f(): x.InstanceProperty = 10
        self.assertRaisesMessage(AttributeError, "can't assign to read-only property InstanceProperty of type 'ClassWithReadOnly'", f)
        
        self.assertEqual(ClassWithReadOnly.StaticProperty, "dlr")
        def f(): ClassWithReadOnly.StaticProperty = 'abc'
        self.assertRaisesMessage(AttributeError, "can't assign to read-only property StaticProperty of type 'ClassWithReadOnly'", f)
    

    def test_writeonly(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Property import ClassWithWriteOnly
        x = ClassWithWriteOnly()
        
        self.assertRaisesMessage(AttributeError, "InstanceProperty", lambda: x.InstanceProperty)  # msg
        x.InstanceProperty = 1; Flag.Check(11)
        
        #ClassWithWriteOnly.StaticProperty  # bug 362862
        ClassWithWriteOnly.StaticProperty = "dlr"
        Flag.Check(12)
        
        self.assertRaisesRegexp(AttributeError, 
            "unreadable property", 
            lambda: ClassWithWriteOnly.__dict__['InstanceProperty'].__get__(x))
        self.assertRaisesRegexp(AttributeError, 
            "unreadable property", 
            lambda: ClassWithWriteOnly.__dict__['InstanceProperty'].GetValue(x))
    
    def test_readonly_writeonly_derivation(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Property import ReadOnlyBase, WriteOnlyDerived
        x = WriteOnlyDerived()
        
        x.Number = 100
        Flag.Check(100)
        self.assertRaisesMessage(AttributeError, "Number", lambda: x.Number)
        
        self.assertEqual(ReadOnlyBase.Number.GetValue(x), 21) 
        x.Number = 101
        Flag.Check(101)
        self.assertEqual(ReadOnlyBase.Number.GetValue(x), 21) 
        
        # repeat ReadOnlyDerived?

    #TODO: @skip("multiple_execute") 
    def test_basic(self):
        from Merlin.Testing.Property import ClassWithProperties, StructWithProperties
        from Merlin.Testing.TypeSample import SimpleClass, SimpleStruct
        for t in [
                    ClassWithProperties,
                    StructWithProperties,
                ]:
            # very basic: object.InstanceProperty, Type.StaticProperty
            x, y = t(), t()
            a, b, c = 1234, SimpleStruct(23), SimpleClass(45)
            
            self.assertEqual(x.InstanceInt32Property, 0)
            x.InstanceInt32Property = a
            self.assertEqual(x.InstanceInt32Property, a)
            
            self.assertTrue(x.InstanceSimpleStructProperty.Flag == 0)
            x.InstanceSimpleStructProperty = b
            self.assertTrue(b == x.InstanceSimpleStructProperty)  
            self.assertEqual(b.Flag, x.InstanceSimpleStructProperty.Flag)
            
            self.assertEqual(x.InstanceSimpleClassProperty, None)
            x.InstanceSimpleClassProperty = c
            self.assertEqual(c, x.InstanceSimpleClassProperty)
            
            self.assertEqual(t.StaticInt32Property, 0)
            t.StaticInt32Property = a
            self.assertEqual(t.StaticInt32Property, a)
            
            t.StaticSimpleStructProperty = b
            self.assertEqual(b.Flag, t.StaticSimpleStructProperty.Flag)
            
            t.StaticSimpleClassProperty = c
            self.assertEqual(c, t.StaticSimpleClassProperty)
            
            # Type.InstanceProperty: SetValue/GetValue (on x), __set__/__get__ (on y)
            a, b, c = 34, SimpleStruct(56), SimpleClass(78)
            
            p = t.InstanceInt32Property
            self.assertEqual(p.SetValue(x, a), None)
            self.assertEqual(p.GetValue(x), a)
            
            p.__set__(y, a)
            #self.assertEqual(p.__get__(y), a)
            
            p = t.InstanceSimpleStructProperty
            p.SetValue(x, b)
            self.assertEqual(p.GetValue(x).Flag, b.Flag)
            
            p.__set__(y, b)
            #self.assertEqual(p.__get__(y).Flag, b.Flag)
            
            p = t.InstanceSimpleClassProperty
            p.SetValue(x, c)
            self.assertEqual(p.GetValue(x), c) 
            
            p.__set__(y, c)
            #self.assertEqual(p.__get__(y), c)
            
            # instance.StaticProperty
            a, b, c = 21, SimpleStruct(32), SimpleClass(43)

            # can read static properties through instances...
            self.assertEqual(x.StaticInt32Property, 1234)
            self.assertEqual(type(x.StaticSimpleStructProperty), SimpleStruct)
            self.assertEqual(type(x.StaticSimpleClassProperty), SimpleClass)
            
            def w1(): x.StaticInt32Property = a
            def w2(): x.StaticSimpleStructProperty = b
            def w3(): x.StaticSimpleClassProperty = c
            
            for w in [w1, w2, w3]:
                self.assertRaisesRegexp(AttributeError, 
                    "static property '.*' of '.*' can only be assigned to through a type, not an instance", 
                    w)
            
            #
            # type.__dict__['xxxProperty']
            #
            x = t()
            a, b, c = 8, SimpleStruct(7), SimpleClass(6)
            
            p = t.__dict__['StaticInt32Property']
            #p.SetValue(None, a)                # bug 363241
            #self.assertEqual(a, p.GetValue(None))

            # static property against instance        
            self.assertRaisesRegexp(SystemError, "cannot set property", lambda: p.SetValue(x, a))
            #self.assertRaisesRegexp(SystemError, "cannot get property", lambda: p.GetValue(x))  # bug 363242

            p = t.__dict__['InstanceInt32Property']
            p.SetValue(x, a)
            #self.assertEqual(p.GetValue(x), a)         # value type issue again

            # instance property against None
            self.assertRaisesRegexp(SystemError, "cannot set property", lambda: p.SetValue(None, a))
            #self.assertRaisesRegexp(SystemError, "cannot get property", lambda: p.GetValue(None))  # bug 363247
            
            p = t.__dict__['StaticSimpleStructProperty']
            p.__set__(None, b)
            #self.assertEqual(b.Flag, p.__get__(None).Flag)
            
            # do we care???
            #print p.__set__(x, b)
            #print p.__get__(x)
            
            p = t.__dict__['InstanceSimpleStructProperty']
            p.__set__(x, b)        
            #self.assertEqual(b.Flag, p.__get__(x).Flag)
            
            # do we care?
            #print p.__set__(None, b)
            #print p.__get__(None)
            
            p = t.__dict__['StaticSimpleClassProperty']
            p.__set__(None, c)              # similar to bug 363241
            #self.assertEqual(c, p.__get__(None))
            
            p = t.__dict__['InstanceSimpleClassProperty']
            p.__set__(x, c)        
            #self.assertEqual(c, p.__get__(x))

    def test_delete(self):
        from Merlin.Testing.Property import ClassWithProperties, ClassWithReadOnly
        def del_p(): del ClassWithProperties.InstanceSimpleStructProperty
        self.assertRaisesRegexp(AttributeError, 
            "cannot delete attribute 'InstanceSimpleStructProperty' of builtin type 'ClassWithProperties'",
            del_p)

        def del_p(): del ClassWithReadOnly.InstanceProperty
        self.assertRaisesRegexp(AttributeError, 
            "cannot delete attribute 'InstanceProperty' of builtin type 'ClassWithReadOnly'",
            del_p)

    def test_from_derived_type(self):
        from Merlin.Testing.Property import DerivedClass
        from Merlin.Testing.TypeSample import SimpleClass, SimpleStruct
        t = DerivedClass
        x = t()
        a, b, c = 8, SimpleStruct(7), SimpleClass(6)
        
        t.StaticInt32Property  # read
        def f(): t.StaticInt32Property = a
        self.assertRaisesRegexp(AttributeError, 
            "'DerivedClass' object has no attribute 'StaticInt32Property'", 
            f)   # write

        x.InstanceInt32Property = a
        self.assertEqual(a, x.InstanceInt32Property)

        self.assertTrue('StaticSimpleStructProperty' not in t.__dict__)
        self.assertTrue('InstanceSimpleStructProperty' not in t.__dict__)
        p = t.__bases__[0].__dict__['InstanceSimpleStructProperty']
        p.SetValue(x, b)
        self.assertEqual(b.Flag, p.GetValue(x).Flag)
        
        self.assertTrue('StaticSimpleClassProperty' not in t.__dict__)
        self.assertTrue('InstanceSimpleClassProperty' not in t.__dict__)
        p = t.__bases__[0].__dict__['InstanceSimpleClassProperty']
        p.__set__(x, c)
        self.assertEqual(c, p.__get__(x))


    def test_other_reflected_property_ops(self):
        from Merlin.Testing.Property import ClassWithProperties
        p = ClassWithProperties.InstanceSimpleStructProperty
        self.assertRaises(TypeError, lambda: p())
        self.assertRaises(TypeError, lambda: p[1])
        
    def test_none_as_value(self):
        pass

run_test(__name__)
