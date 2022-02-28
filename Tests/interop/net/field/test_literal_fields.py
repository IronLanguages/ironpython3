# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class LiteralFieldsTest(IronPythonTestCase):
    def setUp(self):
        super(LiteralFieldsTest, self).setUp()
        self.add_clr_assemblies("fieldtests", "typesamples")

    def _test_get_by_instance(self, current_type):
        from Merlin.Testing.TypeSample import EnumInt32
        o = current_type()

        self.assertEqual(o.LiteralByteField, 1)
        self.assertEqual(o.LiteralSByteField, 2)
        self.assertEqual(o.LiteralUInt16Field, 3)
        self.assertEqual(o.LiteralInt16Field, 4)
        self.assertEqual(o.LiteralUInt32Field, 5)
        self.assertEqual(o.LiteralInt32Field, 6)
        self.assertEqual(o.LiteralUInt64Field, 7)
        self.assertEqual(o.LiteralInt64Field, 8)
        self.assertEqual(o.LiteralDoubleField, 9)
        self.assertEqual(o.LiteralSingleField, 10)
        self.assertEqual(o.LiteralDecimalField, 11)
        
        self.assertEqual(o.LiteralCharField, 'K')
        self.assertEqual(o.LiteralBooleanField, True)
        self.assertEqual(o.LiteralStringField, 'DLR')
        
        self.assertEqual(o.LiteralEnumField, EnumInt32.B)
        self.assertEqual(o.LiteralClassField, None)
        self.assertEqual(o.LiteralInterfaceField, None)

    def _test_get_by_type(self, current_type):
        from Merlin.Testing.TypeSample import EnumInt32
        self.assertEqual(current_type.LiteralByteField, 1)
        self.assertEqual(current_type.LiteralSByteField, 2)
        self.assertEqual(current_type.LiteralUInt16Field, 3)
        self.assertEqual(current_type.LiteralInt16Field, 4)
        self.assertEqual(current_type.LiteralUInt32Field, 5)
        self.assertEqual(current_type.LiteralInt32Field, 6)
        self.assertEqual(current_type.LiteralUInt64Field, 7)
        self.assertEqual(current_type.LiteralInt64Field, 8)
        self.assertEqual(current_type.LiteralDoubleField, 9)
        self.assertEqual(current_type.LiteralSingleField, 10)
        self.assertEqual(current_type.LiteralDecimalField, 11)
        
        self.assertEqual(current_type.LiteralCharField, 'K')
        self.assertEqual(current_type.LiteralBooleanField, True)
        self.assertEqual(current_type.LiteralStringField, 'DLR')

        self.assertEqual(current_type.LiteralEnumField, EnumInt32.B)
        self.assertEqual(current_type.LiteralClassField, None)
        self.assertEqual(current_type.LiteralInterfaceField, None)

    def _test_get_by_descriptor(self, current_type):
        from Merlin.Testing.TypeSample import EnumInt32
        o = current_type()
        self.assertEqual(current_type.__dict__['LiteralByteField'], 1)
        self.assertEqual(current_type.__dict__['LiteralSByteField'], 2)
        self.assertEqual(current_type.__dict__['LiteralUInt16Field'], 3)
        self.assertEqual(current_type.__dict__['LiteralInt16Field'], 4)
        self.assertEqual(current_type.__dict__['LiteralUInt32Field'], 5)
        self.assertEqual(current_type.__dict__['LiteralInt32Field'], 6)
        self.assertEqual(current_type.__dict__['LiteralUInt64Field'], 7)
        self.assertEqual(current_type.__dict__['LiteralInt64Field'], 8)
        self.assertEqual(current_type.__dict__['LiteralDoubleField'], 9)
        self.assertEqual(current_type.__dict__['LiteralSingleField'], 10)
        self.assertEqual(current_type.__dict__['LiteralDecimalField'].__get__(o, current_type), 11)
        
        self.assertEqual(current_type.__dict__['LiteralCharField'], "K")
        self.assertEqual(current_type.__dict__['LiteralBooleanField'], True)
        self.assertEqual(current_type.__dict__['LiteralStringField'], "DLR")

        self.assertEqual(current_type.__dict__['LiteralEnumField'], EnumInt32.B)
        self.assertEqual(current_type.__dict__['LiteralClassField'], None)
        self.assertEqual(current_type.__dict__['LiteralInterfaceField'], None)

    def _test_set_by_instance(self, current_type):
        from Merlin.Testing.TypeSample import EnumInt32
        o = current_type()
        def f1(): o.LiteralByteField = 2
        def f2(): o.LiteralSByteField = 3
        def f3(): o.LiteralUInt16Field = 4
        def f4(): o.LiteralInt16Field = 5
        def f5(): o.LiteralUInt32Field = 6
        def f6(): o.LiteralInt32Field = 7
        def f7(): o.LiteralUInt64Field = 8
        def f8(): o.LiteralInt64Field = 9
        def f9(): o.LiteralDoubleField = 10
        def f10(): o.LiteralSingleField = 11
        def f11(): o.LiteralDecimalField = 12
        
        def f12(): o.LiteralCharField = 'L'
        def f13(): o.LiteralBooleanField = False
        def f14(): o.LiteralStringField = "Python"
        
        def f15(): o.LiteralEnumField = EnumInt32.C
        def f16(): o.LiteralClassField = None
        def f17(): o.LiteralInterfaceField = None

        for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17]:
            self.assertRaisesRegex(AttributeError, "attribute .* of .* object is read-only", f)
    
    def _test_set_by_type(self, current_type):
        from Merlin.Testing.TypeSample import EnumUInt32
        def f1(): current_type.LiteralByteField = 2
        def f2(): current_type.LiteralSByteField = 3
        def f3(): current_type.LiteralUInt16Field = 4
        def f4(): current_type.LiteralInt16Field = 5
        def f5(): current_type.LiteralUInt32Field = 6
        def f6(): current_type.LiteralInt32Field = 7
        def f7(): current_type.LiteralUInt64Field = 8
        def f8(): current_type.LiteralInt64Field = 9
        def f9(): current_type.LiteralDoubleField = 10
        def f10(): current_type.LiteralSingleField = 11
        def f11(): current_type.LiteralDecimalField = 12 

        def f12(): current_type.LiteralCharField = 'L'
        def f13(): current_type.LiteralBooleanField = False
        def f14(): current_type.LiteralStringField = "Python"
        
        def f15(): current_type.LiteralEnumField = EnumUInt32.C  # try set with wrong type value
        def f16(): current_type.LiteralClassField = None
        def f17(): current_type.LiteralInterfaceField = None

        for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17]:
            self.assertRaisesRegex(AttributeError, "attribute '.*' of '.*' object is read-only", f)

        
    def _test_delete_via_type(self, current_type):
        def f1(): del current_type.LiteralByteField
        def f2(): del current_type.LiteralSByteField
        def f3(): del current_type.LiteralUInt16Field
        def f4(): del current_type.LiteralInt16Field
        def f5(): del current_type.LiteralUInt32Field
        def f6(): del current_type.LiteralInt32Field
        def f7(): del current_type.LiteralUInt64Field
        def f8(): del current_type.LiteralInt64Field
        def f9(): del current_type.LiteralDoubleField
        def f10(): del current_type.LiteralSingleField
        def f11(): del current_type.LiteralDecimalField
        
        def f12(): del current_type.LiteralCharField
        def f13(): del current_type.LiteralBooleanField
        def f14(): del current_type.LiteralStringField

        def f15(): del current_type.LiteralEnumField
        def f16(): del current_type.LiteralClassField
        def f17(): del current_type.LiteralInterfaceField
        
        for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17]:
            self.assertRaisesRegex(AttributeError, "cannot delete attribute 'Literal.*Field' of builtin type", f)

    def _test_delete_via_instance(self, current_type, message="cannot delete attribute 'Literal.*Field' of builtin type"):
        o = current_type()
        def f1(): del o.LiteralByteField
        def f2(): del o.LiteralSByteField
        def f3(): del o.LiteralUInt16Field
        def f4(): del o.LiteralInt16Field
        def f5(): del o.LiteralUInt32Field
        def f6(): del o.LiteralInt32Field
        def f7(): del o.LiteralUInt64Field
        def f8(): del o.LiteralInt64Field
        def f9(): del o.LiteralDoubleField
        def f10(): del o.LiteralSingleField
        def f11(): del o.LiteralDecimalField
        
        def f12(): del o.LiteralCharField
        def f13(): del o.LiteralBooleanField
        def f14(): del o.LiteralStringField

        def f15(): del o.LiteralEnumField
        def f16(): del o.LiteralClassField
        def f17(): del o.LiteralInterfaceField
        
        for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17]:
            self.assertRaisesRegex(AttributeError, message, f)

    def test_types(self):
        from Merlin.Testing.FieldTest.Literal import ClassWithLiterals, GenericClassWithLiterals, StructWithLiterals, GenericStructWithLiterals
        types = [
            StructWithLiterals, 
            GenericStructWithLiterals[int], 
            GenericStructWithLiterals[str], 
            ClassWithLiterals, 
            GenericClassWithLiterals[int], 
            GenericClassWithLiterals[object],
            ]
        for i in range(len(types)):
            exec("def test_%s_get_by_instance():   self._test_get_by_instance(types[%s])" % (i, i))
            exec("def test_%s_get_by_type():   self._test_get_by_type(types[%s])" % (i, i))
            exec("def test_%s_get_by_descriptor():   self._test_get_by_descriptor(types[%s])" % (i, i))
            exec("def test_%s_set_by_instance():   self._test_set_by_instance(types[%s])" % (i, i))
            exec("def test_%s_set_by_type():   self._test_set_by_type(types[%s])" % (i, i))
            exec("def test_%s_delete_via_type():   self._test_delete_via_type(types[%s])" % (i, i))
            exec("def test_%s_delete_via_instance():   self._test_delete_via_instance(types[%s])" % (i, i))

    def test_accessing_from_derived(self):
        from Merlin.Testing.FieldTest.Literal import DerivedClass
        self._test_get_by_instance(DerivedClass)
        self._test_get_by_type(DerivedClass)
        self._test_set_by_instance(DerivedClass)
        self._test_set_by_type(DerivedClass) 
        self._test_delete_via_instance(DerivedClass)
        self._test_delete_via_type(DerivedClass)
        
        self.assertTrue('LiteralInt32Field' not in DerivedClass.__dict__)
    
run_test(__name__)

