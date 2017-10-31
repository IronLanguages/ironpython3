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

import unittest
import warnings

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython


@skipUnlessIronPython()
class InstanceFieldsTest(IronPythonTestCase):
    def setUp(self):
        super(InstanceFieldsTest, self).setUp()
        self.add_clr_assemblies("fieldtests", "typesamples")

    def _test_get_by_instance(self, o):
        import System
        from Merlin.Testing.TypeSample import EnumInt64
        o.SetInstanceFields()

        self.assertEqual(o.InstanceByteField, 0)
        self.assertEqual(o.InstanceSByteField, 1)
        self.assertEqual(o.InstanceUInt16Field, 2)
        self.assertEqual(o.InstanceInt16Field, 3)
        self.assertEqual(o.InstanceUInt32Field, 4)
        self.assertEqual(o.InstanceInt32Field, 5)
        self.assertEqual(o.InstanceUInt64Field, 6)
        self.assertEqual(o.InstanceInt64Field, 7)
        self.assertEqual(o.InstanceDoubleField, 8)
        self.assertEqual(o.InstanceSingleField, 9)
        self.assertEqual(o.InstanceDecimalField, 10)
        self.assertEqual(o.InstanceCharField, 'a')
        self.assertEqual(o.InstanceBooleanField, True)
        self.assertEqual(o.InstanceStringField, 'testing')
        
        self.assertEqual(o.InstanceObjectField.Flag, 1111)
        self.assertEqual(o.InstanceEnumField, EnumInt64.B)
        self.assertEqual(o.InstanceDateTimeField, System.DateTime(50000))
        self.assertEqual(o.InstanceSimpleStructField.Flag, 1234)
        self.assertEqual(o.InstanceSimpleGenericStructField.Flag, 32) 
        self.assertEqual(o.InstanceNullableStructNotNullField.Flag, 56)
        self.assertEqual(o.InstanceNullableStructNullField, None)
        self.assertEqual(o.InstanceSimpleClassField.Flag, 54)
        self.assertEqual(o.InstanceSimpleGenericClassField.Flag, "string")
        
        self.assertEqual(o.InstanceSimpleInterfaceField.Flag, 87)

    def _test_get_by_type(self, o, vf, t):
        import System
        from Merlin.Testing.TypeSample import EnumInt64
        o.SetInstanceFields()
        
        v = vf and o.Value or o
        
        self.assertEqual(t.InstanceByteField.__get__(v, t), 0)
        self.assertEqual(t.InstanceSByteField.__get__(v, t), 1)
        self.assertEqual(t.InstanceUInt16Field.__get__(v, t), 2)
        self.assertEqual(t.InstanceInt16Field.__get__(v, t), 3)
        self.assertEqual(t.InstanceUInt32Field.__get__(v, t), 4)
        self.assertEqual(t.InstanceInt32Field.__get__(v, t), 5)
        self.assertEqual(t.InstanceUInt64Field.__get__(v, t), 6)
        self.assertEqual(t.InstanceInt64Field.__get__(v, t), 7)
        self.assertEqual(t.InstanceDoubleField.__get__(v, t), 8)
        self.assertEqual(t.InstanceSingleField.__get__(v, t), 9)
        self.assertEqual(t.InstanceDecimalField.__get__(v, t), 10)
        self.assertEqual(t.InstanceCharField.__get__(v, t), 'a')
        self.assertEqual(t.InstanceBooleanField.__get__(v, t), True)
        self.assertEqual(t.InstanceStringField.__get__(v, t), 'testing')
        
        self.assertEqual(t.InstanceObjectField.__get__(v, t).Flag, 1111)
        self.assertEqual(t.InstanceEnumField.__get__(v, t), EnumInt64.B)
        self.assertEqual(t.InstanceDateTimeField.__get__(v, t), System.DateTime(50000))
        self.assertEqual(t.InstanceSimpleStructField.__get__(v, t).Flag, 1234)
        self.assertEqual(t.InstanceSimpleGenericStructField.__get__(v, t).Flag, 32) 
        self.assertEqual(t.InstanceNullableStructNotNullField.__get__(v, t).Flag, 56)
        self.assertEqual(t.InstanceNullableStructNullField.__get__(v, t), None)
        self.assertEqual(t.InstanceSimpleClassField.__get__(v, t).Flag, 54)
        self.assertEqual(t.InstanceSimpleGenericClassField.__get__(v, t).Flag, "string")

        self.assertEqual(t.InstanceSimpleInterfaceField.__get__(v, t).Flag, 87)

    def _test_get_by_descriptor(self, o, vf, t):
        import System
        from Merlin.Testing.TypeSample import EnumInt64
        o.SetInstanceFields()
        v = vf and o.Value or o

        self.assertEqual(t.__dict__['InstanceByteField'].__get__(v, t), 0)
        self.assertEqual(t.__dict__['InstanceSByteField'].__get__(v, t), 1)
        self.assertEqual(t.__dict__['InstanceUInt16Field'].__get__(v, t), 2)
        self.assertEqual(t.__dict__['InstanceInt16Field'].__get__(v, t), 3)
        self.assertEqual(t.__dict__['InstanceUInt32Field'].__get__(v, t), 4)
        self.assertEqual(t.__dict__['InstanceInt32Field'].__get__(v, t), 5)
        self.assertEqual(t.__dict__['InstanceUInt64Field'].__get__(v, t), 6)
        self.assertEqual(t.__dict__['InstanceInt64Field'].__get__(v, t), 7)
        self.assertEqual(t.__dict__['InstanceDoubleField'].__get__(v, t), 8)
        self.assertEqual(t.__dict__['InstanceSingleField'].__get__(v, t), 9)
        self.assertEqual(t.__dict__['InstanceDecimalField'].__get__(v, t), 10)
        self.assertEqual(t.__dict__['InstanceCharField'].__get__(v, t), 'a')
        self.assertEqual(t.__dict__['InstanceBooleanField'].__get__(v, t), True)
        self.assertEqual(t.__dict__['InstanceStringField'].__get__(v, t), 'testing')

        self.assertEqual(t.__dict__['InstanceObjectField'].__get__(v, t).Flag, 1111)
        self.assertEqual(t.__dict__['InstanceEnumField'].__get__(v, t), EnumInt64.B)
        self.assertEqual(t.__dict__['InstanceDateTimeField'].__get__(v, t), System.DateTime(50000))
        self.assertEqual(t.__dict__['InstanceSimpleStructField'].__get__(v, t).Flag, 1234)
        self.assertEqual(t.__dict__['InstanceSimpleGenericStructField'].__get__(v, t).Flag, 32) 
        self.assertEqual(t.__dict__['InstanceNullableStructNotNullField'].__get__(v, t).Flag, 56)
        self.assertEqual(t.__dict__['InstanceNullableStructNullField'].__get__(v, t), None)
        self.assertEqual(t.__dict__['InstanceSimpleClassField'].__get__(v, t).Flag, 54)
        self.assertEqual(t.__dict__['InstanceSimpleGenericClassField'].__get__(v, t).Flag, "string")

        self.assertEqual(t.__dict__['InstanceSimpleInterfaceField'].__get__(v, t).Flag, 87)
        
        # TODO (pass in other values to __get__)

    def _test_verify(self, o):
        import System
        from Merlin.Testing.TypeSample import EnumInt64
        self.assertEqual(o.InstanceByteField, 5)
        self.assertEqual(o.InstanceSByteField, 10)
        self.assertEqual(o.InstanceUInt16Field, 20)
        self.assertEqual(o.InstanceInt16Field, 30)
        self.assertEqual(o.InstanceUInt32Field, 40)
        self.assertEqual(o.InstanceInt32Field, 50)
        self.assertEqual(o.InstanceUInt64Field, 60)
        self.assertEqual(o.InstanceInt64Field, 70)
        self.assertEqual(o.InstanceDoubleField, 80)
        self.assertEqual(o.InstanceSingleField, 90)
        self.assertEqual(o.InstanceDecimalField, 100)
        self.assertEqual(o.InstanceCharField, 'd')
        self.assertEqual(o.InstanceBooleanField, False)
        self.assertEqual(o.InstanceStringField, 'TESTING')
        
        self.assertEqual(o.InstanceObjectField, "number_to_string")
        self.assertEqual(o.InstanceEnumField, EnumInt64.C)
        self.assertEqual(o.InstanceDateTimeField, System.DateTime(500000))
        self.assertEqual(o.InstanceSimpleStructField.Flag, 12340)
        self.assertEqual(o.InstanceSimpleGenericStructField.Flag, 320) 
        self.assertEqual(o.InstanceNullableStructNotNullField, None)
        self.assertEqual(o.InstanceNullableStructNullField.Flag, 650)
        self.assertEqual(o.InstanceSimpleClassField.Flag, 540)
        self.assertEqual(o.InstanceSimpleGenericClassField.Flag, "STRING")

        self.assertEqual(o.InstanceSimpleInterfaceField.Flag, 78)
    
    def _test_set_by_instance(self, o, vf, t):
        import clr
        import System
        from Merlin.Testing.TypeSample import ClassImplementSimpleInterface, EnumInt64, SimpleClass, SimpleStruct, SimpleGenericClass, SimpleGenericStruct
        #from Merlin.Testing.FieldTest import 
        o.SetInstanceFields()
        v = vf and o.Value or o
        
        # pass correct values
        def f1(): v.InstanceByteField =  5
        def f2(): v.InstanceSByteField = 10
        def f3(): v.InstanceUInt16Field = 20
        def f4(): v.InstanceInt16Field = 30
        def f5(): v.InstanceUInt32Field = 40
        def f6(): v.InstanceInt32Field = 50
        def f7(): v.InstanceUInt64Field = 60
        def f8(): v.InstanceInt64Field = 70
        def f9(): v.InstanceDoubleField = 80
        def f10(): v.InstanceSingleField = 90
        def f11(): v.InstanceDecimalField = 100
        def f12(): v.InstanceCharField = 'd'
        def f13(): v.InstanceBooleanField = False
        def f14(): v.InstanceStringField = 'testing'.upper()
        
        def f15(): v.InstanceObjectField = "number_to_string"
        def f16(): v.InstanceEnumField = EnumInt64.C
        def f17(): v.InstanceDateTimeField = System.DateTime(500000)
        def f18(): v.InstanceSimpleStructField = SimpleStruct(12340)
        def f19(): v.InstanceSimpleGenericStructField = SimpleGenericStruct[System.UInt16](320)
        def f20(): v.InstanceNullableStructNotNullField = None
        def f21(): v.InstanceNullableStructNullField = SimpleStruct(650)
        def f22(): v.InstanceSimpleClassField = SimpleClass(540)
        def f23(): v.InstanceSimpleGenericClassField = SimpleGenericClass[str]("string".upper())
        def f24(): v.InstanceSimpleInterfaceField = ClassImplementSimpleInterface(78)

        funcs = [ eval("f%s" % i) for i in range(1, 25) ]
        if clr.GetClrType(t).IsValueType:
            for f in funcs:
                self.assertWarnsPartialMessage(RuntimeWarning, 'may result in updating a copy', f)
        else: 
            for f in funcs: f()
            self._test_verify(o)
            
            # set values which need conversion.
            v.InstanceInt32Field = 100
            self.assertEqual(o.InstanceInt32Field, 100)

            v.InstanceInt32Field = 10.01
            self.assertEqual(o.InstanceInt32Field, 10)

            # set bad values 
            def f1(): v.InstanceInt32Field = "abc"
            def f2(): v.InstanceEnumField = 3
            
            for f in [f1, f2]: self.assertRaises(TypeError, f)

    def _test_set_by_type(self, o, vf, t):
        import clr
        import System
        from Merlin.Testing.TypeSample import ClassImplementSimpleInterface, EnumInt32, EnumInt64, SimpleClass, SimpleStruct, SimpleGenericClass, SimpleGenericStruct

        o.SetInstanceFields()
        v = vf and o.Value or o
        
        # pass correct values
        def f1(): t.InstanceByteField.__set__(v,  5)
        def f2(): t.InstanceSByteField.__set__(v, 10)
        def f3(): t.InstanceUInt16Field.__set__(v, 20)
        def f4(): t.InstanceInt16Field.__set__(v, 30)
        def f5(): t.InstanceUInt32Field.__set__(v, 40)
        def f6(): t.InstanceInt32Field.__set__(v, 50)
        def f7(): t.InstanceUInt64Field.__set__(v, 60)
        def f8(): t.InstanceInt64Field.__set__(v, 70)
        def f9(): t.InstanceDoubleField.__set__(v, 80)
        def f10(): t.InstanceSingleField.__set__(v, 90)
        def f11(): t.InstanceDecimalField.__set__(v, 100)
        def f12(): t.InstanceCharField.__set__(v, 'd')
        def f13(): t.InstanceBooleanField.__set__(v, False)
        def f14(): t.InstanceStringField.__set__(v, 'testing'.upper())
        
        def f15(): t.InstanceObjectField.__set__(v, "number_to_string")
        def f16(): t.InstanceEnumField.__set__(v, EnumInt64.C)
        def f17(): t.InstanceDateTimeField.__set__(v, System.DateTime(500000))
        def f18(): t.InstanceSimpleStructField.__set__(v, SimpleStruct(12340))
        def f19(): t.InstanceSimpleGenericStructField.__set__(v, SimpleGenericStruct[System.UInt16](320))
        def f20(): t.InstanceNullableStructNotNullField.__set__(v, None)
        def f21(): t.InstanceNullableStructNullField.__set__(v, SimpleStruct(650))
        def f22(): t.InstanceSimpleClassField.__set__(v, SimpleClass(540))
        def f23(): t.InstanceSimpleGenericClassField.__set__(v, SimpleGenericClass[str]("string".upper()))
        def f24(): t.InstanceSimpleInterfaceField.__set__(v, ClassImplementSimpleInterface(78))

        funcs = [ eval("f%s" % i) for i in range(1, 25) ]
        if clr.GetClrType(t).IsValueType:
            for f in funcs:
                self.assertWarnsPartialMessage(RuntimeWarning, 'may result in updating a copy', f)
        else: 
            for f in funcs: f()
            self._test_verify(o)
            
            # set values which need conversion.
            t.InstanceInt16Field.__set__(v, 100);        self.assertEqual(o.InstanceInt16Field, 100)
            t.InstanceBooleanField.__set__(v, 0);         self.assertEqual(o.InstanceBooleanField, False)

            # set bad values 
            def f1(): t.InstanceInt16Field.__set__(v, "abc")
            def f2(): t.InstanceCharField.__set__(v, "abc")
            def f3(): t.InstanceEnumField.__set__(v, EnumInt32.B)

            self.assertRaisesRegexp(TypeError, "expected Int16, got str",  f1)
            self.assertRaisesRegexp(TypeError, "expected string of length 1 when converting to char, got 'abc'", f2)
            self.assertRaisesRegexp(TypeError, "expected EnumInt64, got EnumInt32",  f3)

    def _test_set_by_descriptor(self, o, vf, t):
        import clr
        import System
        from Merlin.Testing.FieldTest import Class, GenericClass, GenericStruct, Struct
        from Merlin.Testing.TypeSample import SimpleClass, SimpleGenericClass, SimpleStruct

        o.SetInstanceFields()
        v = vf and o.Value or o

        # pass correct values
        def f1(): t.__dict__['InstanceByteField'].__set__(v, 5)
        def f2(): t.__dict__['InstanceSByteField'].__set__(v, 10)
        def f3(): t.__dict__['InstanceUInt16Field'].__set__(v, 20)
        def f4(): t.__dict__['InstanceInt16Field'].__set__(v, 30)
        def f5(): t.__dict__['InstanceUInt32Field'].__set__(v, 40)
        def f6(): t.__dict__['InstanceInt32Field'].__set__(v, 50)
        def f7(): t.__dict__['InstanceUInt64Field'].__set__(v, 60)
        def f8(): t.__dict__['InstanceInt64Field'].__set__(v, 70)
        def f9(): t.__dict__['InstanceDoubleField'].__set__(v, 80)
        def f10(): t.__dict__['InstanceSingleField'].__set__(v, 90)
        def f11(): t.__dict__['InstanceDecimalField'].__set__(v, 100)
        def f12(): t.__dict__['InstanceCharField'].__set__(v, 'd')
        def f13(): t.__dict__['InstanceBooleanField'].__set__(v, False)
        def f14(): t.__dict__['InstanceStringField'].__set__(v, 'TESTING')

        def f15(): t.__dict__['InstanceObjectField'].__set__(v, "number_to_string")
        def f16(): t.__dict__['InstanceEnumField'].__set__(v, EnumInt64.C)
        def f17(): t.__dict__['InstanceDateTimeField'].__set__(v, System.DateTime(500000))
        def f18(): t.__dict__['InstanceSimpleStructField'].__set__(v, SimpleStruct(12340))
        def f19(): t.__dict__['InstanceSimpleGenericStructField'].__set__(v, SimpleGenericStruct[System.UInt16](320)) 
        def f20(): t.__dict__['InstanceNullableStructNotNullField'].__set__(v, None)
        def f21(): t.__dict__['InstanceNullableStructNullField'].__set__(v, SimpleStruct(650))
        def f22(): t.__dict__['InstanceSimpleClassField'].__set__(v, SimpleClass(540))
        def f23(): t.__dict__['InstanceSimpleGenericClassField'].__set__(v, SimpleGenericClass[str]("STRING"))
        def f24(): t.__dict__['InstanceSimpleInterfaceField'].__set__(v, ClassImplementSimpleInterface(78))

        funcs = [ eval("f%s" % i) for i in range(1, 25) ]
        if clr.GetClrType(t).IsValueType:
            for f in funcs:
                with warning_trapper() as wt:
                    f()
                AssertFieldWarnings(wt)
        else: 
            for f in funcs: f()
            self._test_verify(o)
        
        # set with bad values (TODO)

    def _test_delete_by_type(self, current_type):
        def f1(): del current_type.InstanceByteField 
        def f2(): del current_type.InstanceSByteField
        def f3(): del current_type.InstanceUInt16Field
        def f4(): del current_type.InstanceInt16Field 
        def f5(): del current_type.InstanceUInt32Field
        def f6(): del current_type.InstanceInt32Field 
        def f7(): del current_type.InstanceUInt64Field
        def f8(): del current_type.InstanceInt64Field
        def f9(): del current_type.InstanceDoubleField 
        def f10(): del current_type.InstanceSingleField
        def f11(): del current_type.InstanceDecimalField
        def f12(): del current_type.InstanceCharField 
        def f13(): del current_type.InstanceBooleanField 
        def f14(): del current_type.InstanceStringField
        
        def f15(): del current_type.InstanceObjectField
        def f16(): del current_type.InstanceEnumField 
        def f17(): del current_type.InstanceDateTimeField 
        def f18(): del current_type.InstanceSimpleStructField 
        def f19(): del current_type.InstanceSimpleGenericStructField 
        def f20(): del current_type.InstanceNullableStructNotNullField
        def f21(): del current_type.InstanceNullableStructNullField 
        def f22(): del current_type.InstanceSimpleClassField 
        def f23(): del current_type.InstanceSimpleGenericClassField 
        def f24(): del current_type.InstanceSimpleInterfaceField 
        
        funcs = [ eval("f%s" % i) for i in range(1, 25) ]
        for f in funcs:
            self.assertRaisesRegexp(AttributeError, "cannot delete attribute", f)  

    def _test_delete_by_instance(self, current_type):
        o = current_type()
        def f1(): del o.InstanceByteField 
        def f2(): del o.InstanceSByteField
        def f3(): del o.InstanceUInt16Field
        def f4(): del o.InstanceInt16Field 
        def f5(): del o.InstanceUInt32Field
        def f6(): del o.InstanceInt32Field 
        def f7(): del o.InstanceUInt64Field
        def f8(): del o.InstanceInt64Field
        def f9(): del o.InstanceDoubleField 
        def f10(): del o.InstanceSingleField
        def f11(): del o.InstanceDecimalField
        def f12(): del o.InstanceCharField 
        def f13(): del o.InstanceBooleanField 
        def f14(): del o.InstanceStringField
        
        def f15(): del o.InstanceObjectField
        def f16(): del o.InstanceEnumField 
        def f17(): del o.InstanceDateTimeField 
        def f18(): del o.InstanceSimpleStructField 
        def f19(): del o.InstanceSimpleGenericStructField 
        def f20(): del o.InstanceNullableStructNotNullField
        def f21(): del o.InstanceNullableStructNullField 
        def f22(): del o.InstanceSimpleClassField 
        def f23(): del o.InstanceSimpleGenericClassField 
        def f24(): del o.InstanceSimpleInterfaceField 
        
        funcs = [ eval("f%s" % i) for i in range(1, 25) ]
        for f in funcs:
            self.assertRaisesRegexp(AttributeError, "cannot delete attribute", f) 

    def _test_delete_by_descriptor(self, current_type):
        for x in [
            'Byte',
            'SByte',
            'UInt16',
            'Int16',
            'UInt32',
            'Int32',
            'UInt64',
            'Int64',
            'Double',
            'Single',
            'Decimal',
            'Char',
            'Boolean',
            'String',
            'Object',
            'Enum',
            'DateTime',
            'SimpleStruct',
            'SimpleGenericStruct',
            'NullableStructNotNull',
            'NullableStructNull',
            'SimpleClass',
            'SimpleGenericClass',
            'SimpleInterface',
        ]:
            for o in [None, current_type, current_type()]:
                self.assertRaisesRegexp(AttributeError, "cannot delete attribute", lambda: current_type.__dict__['Instance%sField' % x].__delete__(o))

    def test_types(self):
        import clr
        from Merlin.Testing.FieldTest import Class, GenericClass, GenericStruct, Struct
        from Merlin.Testing.TypeSample import SimpleClass, SimpleStruct
        types = [ 
            Struct, 
            GenericStruct[int],
            GenericStruct[SimpleClass],
            Class,
            GenericClass[SimpleStruct],
            GenericClass[SimpleClass],
            ]

        o1 = clr.StrongBox[Struct](Struct())
        o2 = clr.StrongBox[GenericStruct[int]](GenericStruct[int]())
        o3 = clr.StrongBox[GenericStruct[SimpleClass]](GenericStruct[SimpleClass]())
        o4 = Class()
        o5 = GenericClass[SimpleStruct]()
        o6 = GenericClass[SimpleClass]()
        o7 = clr.StrongBox[Class](o4)
        o8 = clr.StrongBox[GenericClass[SimpleStruct]](o5)
        o9 = clr.StrongBox[GenericClass[SimpleClass]](o6)

        scenarios = [ 
            (o1, True, types[0]), 
            (o2, True, types[1]), 
            (o3, True, types[2]), 
            (o4, False, types[3]), 
            (o5, False, types[4]), 
            (o6, False, types[5]), 
            (o7, True, types[3]), 
            (o8, True, types[4]), 
            (o9, True, types[5]),
        ]

        for i in range(len(scenarios)):
            exec("def test_%s_get_by_instance():   self._test_get_by_instance(scenarios[%s][0])" % (i, i))
            exec("def test_%s_get_by_type():   self._test_get_by_type(scenarios[%s][0], scenarios[%s][1], scenarios[%s][2])" % (i, i, i, i))
            exec("def test_%s_get_by_descriptor():   self._test_get_by_descriptor(scenarios[%s][0], scenarios[%s][1], scenarios[%s][2])" % (i, i, i, i))
            
            exec("def test_%s_set_by_instance():   self._test_set_by_instance(scenarios[%s][0], scenarios[%s][1], scenarios[%s][2])" % (i, i, i, i))
            exec("def test_%s_set_by_type():   self._test_set_by_type(scenarios[%s][0], scenarios[%s][1], scenarios[%s][2])" % (i, i, i, i))
            exec("def test_%s_set_by_descriptor():   self._test_set_by_descriptor(scenarios[%s][0], scenarios[%s][1], scenarios[%s][2])" % (i, i, i, i))
            
            exec("def test_%s_delete_by_type():   self._test_delete_by_type(scenarios[%s][2])" % (i, i))
            exec("def test_%s_delete_by_instance():   self._test_delete_by_instance(scenarios[%s][2])" % (i, i))
            exec("def test_%s_delete_by_descriptor():   self._test_delete_by_descriptor(scenarios[%s][2])" % (i, i))

    def test_nested(self):
        import System
        from Merlin.Testing.FieldTest import Class2, GenericClass2
        for ct in [ Class2, GenericClass2[System.Byte], GenericClass2[object] ]:
            c = ct()
            self.assertEqual(c.InstanceNextField, None)
            c.InstanceNextField = ct()
            
            self.assertEqual(c.InstanceNextField.InstanceNextField, None)
            c.InstanceNextField.InstanceField = 20
            self.assertEqual(c.InstanceNextField.InstanceField, 20)
    
    def test_generic_fields(self):
        from Merlin.Testing.FieldTest import GenericClass2, GenericStruct2
        from Merlin.Testing.TypeSample import SimpleGenericClass, SimpleGenericStruct

        current_type = GenericStruct2[str]
        o = current_type()
        self.assertEqual(o.InstanceTField, None)
        self.assertEqual(o.InstanceClassTField, None)
        self.assertEqual(o.InstanceStructTField.Flag, None)

        def tryUpdate():
            o.InstanceTField="abc"
        
        self.assertWarnsPartialMessage(RuntimeWarning, 'may result in updating a copy', tryUpdate)
        
        current_type = GenericClass2[int]
        o = current_type()
        o.InstanceTField = 30
        o.InstanceClassTField = SimpleGenericClass[int](40)
        o.InstanceStructTField = SimpleGenericStruct[int](50)
        
        self.assertEqual(o.InstanceTField, 30)
        self.assertEqual(o.InstanceClassTField.Flag, 40)
        self.assertEqual(o.InstanceStructTField.Flag, 50)

        current_type = GenericClass2[str]
        o = current_type()
        o.InstanceTField = "30"
        o.InstanceClassTField = SimpleGenericClass[str]("40")
        o.InstanceStructTField = SimpleGenericStruct[str]("50")

        self.assertEqual(o.InstanceTField, '30')
        self.assertEqual(o.InstanceClassTField.Flag, '40')
        self.assertEqual(o.InstanceStructTField.Flag, '50')

    def test_access_from_derived_types(self):
        from Merlin.Testing.FieldTest import DerivedClass, DerivedOpenGenericClass, DerivedGenericClassOfInt32, DerivedGenericClassOfObject
        for current_type in [ 
            DerivedClass, 
            DerivedOpenGenericClass[int], 
            DerivedOpenGenericClass[str], 
            DerivedGenericClassOfInt32,
            DerivedGenericClassOfObject,
        ]:
            o = current_type()
            self._test_get_by_instance(o)
            self._test_get_by_type(o, False, current_type)

            self._test_set_by_instance(o, False, current_type) 
            self._test_set_by_type(o, False, current_type)    

            self._test_delete_by_instance(current_type) 
            self._test_delete_by_type(current_type)    

run_test(__name__)

