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
'''
#------------------------------------------------------------------------------
from iptest import *
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("fieldtests", "typesamples")

if options.RUN_TESTS: #TODO - bug when generating Pydoc
    from Merlin.Testing.FieldTest.InitOnly import *
    from Merlin.Testing.TypeSample import *

def _test_get_by_instance(current_type):
    o = current_type()

    AreEqual(o.InitOnlyByteField, 0)
    AreEqual(o.InitOnlySByteField, 1)
    AreEqual(o.InitOnlyUInt16Field, 2)
    AreEqual(o.InitOnlyInt16Field, 3)
    AreEqual(o.InitOnlyUInt32Field, 4)
    AreEqual(o.InitOnlyInt32Field, 5)
    AreEqual(o.InitOnlyUInt64Field, 6)
    AreEqual(o.InitOnlyInt64Field, 7)
    AreEqual(o.InitOnlyDoubleField, 8)
    AreEqual(o.InitOnlySingleField, 9)
    AreEqual(o.InitOnlyDecimalField, 10)
    
    AreEqual(o.InitOnlyCharField, 'P')
    AreEqual(o.InitOnlyBooleanField, True)
    
    AreEqual(o.InitOnlyEnumField, EnumInt16.B)
    AreEqual(o.InitOnlyStringField, 'ruby')

    AreEqual(o.InitOnlyDateTimeField, System.DateTime(5))
    AreEqual(o.InitOnlySimpleStructField.Flag, 10)
    AreEqual(o.InitOnlySimpleGenericStructField.Flag, 20)
    AreEqual(o.InitOnlyNullableStructField_NotNull.Flag, 30)
    AreEqual(o.InitOnlyNullableStructField_Null, None)
    AreEqual(o.InitOnlySimpleClassField.Flag, 40)
    AreEqual(o.InitOnlySimpleGenericClassField.Flag, "ironruby")
    AreEqual(o.InitOnlySimpleInterfaceField.Flag, 50)

def _test_get_by_type(current_type):    
    AreEqual(current_type.InitOnlyByteField, 0)
    AreEqual(current_type.InitOnlySByteField, 1)
    AreEqual(current_type.InitOnlyUInt16Field, 2)
    AreEqual(current_type.InitOnlyInt16Field, 3)
    AreEqual(current_type.InitOnlyUInt32Field, 4)
    AreEqual(current_type.InitOnlyInt32Field, 5)
    AreEqual(current_type.InitOnlyUInt64Field, 6)
    AreEqual(current_type.InitOnlyInt64Field, 7)
    AreEqual(current_type.InitOnlyDoubleField, 8)
    AreEqual(current_type.InitOnlySingleField, 9)
    AreEqual(current_type.InitOnlyDecimalField, 10)
    
    AreEqual(current_type.InitOnlyCharField, 'P')
    AreEqual(current_type.InitOnlyBooleanField, True)
    
    AreEqual(current_type.InitOnlyEnumField, EnumInt16.B)
    AreEqual(current_type.InitOnlyStringField, 'ruby')

    AreEqual(current_type.InitOnlyDateTimeField, System.DateTime(5))
    AreEqual(current_type.InitOnlySimpleStructField.Flag, 10)
    AreEqual(current_type.InitOnlySimpleGenericStructField.Flag, 20)
    AreEqual(current_type.InitOnlyNullableStructField_NotNull.Flag, 30)
    AreEqual(current_type.InitOnlyNullableStructField_Null, None)
    AreEqual(current_type.InitOnlySimpleClassField.Flag, 40)
    AreEqual(current_type.InitOnlySimpleGenericClassField.Flag, "ironruby")

    AreEqual(current_type.InitOnlySimpleInterfaceField.Flag, 50)

def _test_get_by_descriptor(current_type):
    o = current_type()
    AreEqual(current_type.__dict__['InitOnlyByteField'].__get__(None, current_type), 0)
    AreEqual(current_type.__dict__['InitOnlySByteField'].__get__(o, current_type), 1)
    AreEqual(current_type.__dict__['InitOnlyUInt16Field'].__get__(None, current_type), 2)
    AreEqual(current_type.__dict__['InitOnlyInt16Field'].__get__(o, current_type), 3)
    AreEqual(current_type.__dict__['InitOnlyUInt32Field'].__get__(None, current_type), 4)
    AreEqual(current_type.__dict__['InitOnlyInt32Field'].__get__(o, current_type), 5)
    AreEqual(current_type.__dict__['InitOnlyUInt64Field'].__get__(None, current_type), 6)
    AreEqual(current_type.__dict__['InitOnlyInt64Field'].__get__(o, current_type), 7)
    AreEqual(current_type.__dict__['InitOnlyDoubleField'].__get__(None, current_type), 8)
    AreEqual(current_type.__dict__['InitOnlySingleField'].__get__(o, current_type), 9)
    AreEqual(current_type.__dict__['InitOnlyDecimalField'].__get__(None, current_type), 10)
    
    AreEqual(current_type.__dict__['InitOnlyCharField'].__get__(o, current_type), "P")
    AreEqual(current_type.__dict__['InitOnlyBooleanField'].__get__(None, current_type), True)
    AreEqual(current_type.__dict__['InitOnlyStringField'].__get__(o, current_type), "ruby")

    AreEqual(current_type.__dict__['InitOnlyEnumField'].__get__(None, current_type), EnumInt16.B)
    AreEqual(current_type.__dict__['InitOnlyDateTimeField'].__get__(o, current_type), System.DateTime(5))
    
    AreEqual(current_type.__dict__['InitOnlySimpleStructField'].__get__(None, current_type).Flag, 10)
    AreEqual(current_type.__dict__['InitOnlySimpleGenericStructField'].__get__(o, current_type).Flag, 20)
    AreEqual(current_type.__dict__['InitOnlyNullableStructField_NotNull'].__get__(None, current_type).Flag, 30)
    AreEqual(current_type.__dict__['InitOnlyNullableStructField_Null'].__get__(o, current_type), None)
    AreEqual(current_type.__dict__['InitOnlySimpleClassField'].__get__(None, current_type).Flag, 40)
    AreEqual(current_type.__dict__['InitOnlySimpleGenericClassField'].__get__(o, current_type).Flag, "ironruby")
    AreEqual(current_type.__dict__['InitOnlySimpleInterfaceField'].__get__(None, current_type).Flag, 50)

    for t in [current_type, SimpleStruct, SimpleClass]:
        AssertErrorWithMatch(TypeError, "(expected .*, got type)", lambda: current_type.__dict__['InitOnlySimpleGenericClassField'].__get__(t, current_type))
    
    for t in [None, o, SimpleClass, SimpleStruct]:
        AreEqual(current_type.__dict__['InitOnlyEnumField'].__get__(None, t), EnumInt16.B)
    
def _test_set_by_instance(current_type):
    o = current_type()
    def f1(): o.InitOnlyByteField = 2
    def f2(): o.InitOnlySByteField = 3
    def f3(): o.InitOnlyUInt16Field = 4
    def f4(): o.InitOnlyInt16Field = 5
    def f5(): o.InitOnlyUInt32Field = 6
    def f6(): o.InitOnlyInt32Field = 7
    def f7(): o.InitOnlyUInt64Field = 8
    def f8(): o.InitOnlyInt64Field = 9
    def f9(): o.InitOnlyDoubleField = 10
    def f10(): o.InitOnlySingleField = 11
    def f11(): o.InitOnlyDecimalField = 12
    
    def f12(): o.InitOnlyCharField = 'L'
    def f13(): o.InitOnlyBooleanField = False
    def f14(): o.InitOnlyStringField = "Python"
    
    def f15(): o.InitOnlyEnumField = EnumInt32.C # different type
    def f16(): o.InitOnlyDateTimeField = System.DateTime(300)
    
    def f17(): o.InitOnlySimpleStructField = SimpleStruct(30)
    def f18(): o.InitOnlySimpleGenericStructField = SimpleGenericStruct[int](30)  # instance has the wrong type
    def f19(): o.InitOnlyNullableStructField_NotNull = None
    def f20(): o.InitOnlyNullableStructField_Null = SimpleStruct(300)
    def f21(): o.InitOnlySimpleClassField = SimpleClass(3000)
    def f22(): o.InitOnlySimpleGenericClassField = None
    def f23(): o.InitOnlySimpleInterfaceField = ClassImplementSimpleInterface(40)

    for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17, f18, f19, f20, f21, f22, f23]:
        AssertErrorWithMatch(AttributeError, "attribute .* of .* object is read-only", f)
    
def _test_set_by_type(current_type, message="attribute '.*' of '.*' object is read-only"):
    def f1(): current_type.InitOnlyByteField = 2
    def f2(): current_type.InitOnlySByteField = 3
    def f3(): current_type.InitOnlyUInt16Field = 4
    def f4(): current_type.InitOnlyInt16Field = 5
    def f5(): current_type.InitOnlyUInt32Field = 6
    def f6(): current_type.InitOnlyInt32Field = 7
    def f7(): current_type.InitOnlyUInt64Field = 8
    def f8(): current_type.InitOnlyInt64Field = 9
    def f9(): current_type.InitOnlyDoubleField = 10
    def f10(): current_type.InitOnlySingleField = 11
    def f11(): current_type.InitOnlyDecimalField = 12

    def f12(): current_type.InitOnlyCharField = 'L'
    def f13(): current_type.InitOnlyBooleanField = False
    def f14(): current_type.InitOnlyStringField = "Python"
    
    def f15(): current_type.InitOnlyEnumField = EnumInt16.C 
    def f16(): current_type.InitOnlyDateTimeField = None  # set a value type to null
    
    def f17(): current_type.InitOnlySimpleStructField = SimpleStruct(30)
    def f18(): current_type.InitOnlySimpleGenericStructField = SimpleGenericStruct[int](30)  # instance has the wrong type
    def f19(): current_type.InitOnlyNullableStructField_NotNull = None
    def f20(): current_type.InitOnlyNullableStructField_Null = SimpleStruct(300)
    def f21(): current_type.InitOnlySimpleClassField = SimpleClass(3000)
    def f22(): current_type.InitOnlySimpleGenericClassField = None
    def f23(): current_type.InitOnlySimpleInterfaceField = ClassImplementSimpleInterface(40)

    for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17, f18, f19, f20, f21, f22, f23]:
        AssertErrorWithMatch(AttributeError, message, f)

def _test_set_by_descriptor(current_type):
    o = current_type()
    for f in [
     lambda : current_type.__dict__['InitOnlyByteField'].__set__(None, 2),
     lambda : current_type.__dict__['InitOnlyUInt16Field'].__set__(None, 4),
     lambda : current_type.__dict__['InitOnlyUInt64Field'].__set__(None, 8),
     lambda : current_type.__dict__['InitOnlyDecimalField'].__set__(None, 12),
     lambda : current_type.__dict__['InitOnlyBooleanField'].__set__(None, False),
     lambda : current_type.__dict__['InitOnlySimpleClassField'].__set__(None, None),
    ]: 
        AssertErrorWithMatch(AttributeError, "'.*' object attribute '.*' is read-only", f)  # ???
        
    for f in [
     lambda : current_type.__dict__['InitOnlySByteField'].__set__(o, 3),
     lambda : current_type.__dict__['InitOnlyInt16Field'].__set__(o, 5),
     lambda : current_type.__dict__['InitOnlyUInt32Field'].__set__(current_type, 6),
     lambda : current_type.__dict__['InitOnlyInt32Field'].__set__(o, 7),
     lambda : current_type.__dict__['InitOnlyInt64Field'].__set__(o, 9),
     lambda : current_type.__dict__['InitOnlyDoubleField'].__set__(current_type, 10),
     lambda : current_type.__dict__['InitOnlySingleField'].__set__(o, 11),
    
     lambda : current_type.__dict__['InitOnlyCharField'].__set__(o, "L"),
     lambda : current_type.__dict__['InitOnlyStringField'].__set__(o, "Python"),

     lambda : current_type.__dict__['InitOnlyEnumField'].__set__(current_type, EnumInt32.C),
     lambda : current_type.__dict__['InitOnlySimpleInterfaceField'].__set__(o, None),
    ]: 
        AssertErrorWithMatch(AttributeError, "'.*' object attribute 'InitOnly.*Field' is read-only", f)
        
def _test_delete_via_type(current_type, message="cannot delete attribute 'InitOnly.*' of builtin type"):
    def f1(): del current_type.InitOnlyByteField
    def f2(): del current_type.InitOnlySByteField
    def f3(): del current_type.InitOnlyUInt16Field
    def f4(): del current_type.InitOnlyInt16Field
    def f5(): del current_type.InitOnlyUInt32Field
    def f6(): del current_type.InitOnlyInt32Field
    def f7(): del current_type.InitOnlyUInt64Field
    def f8(): del current_type.InitOnlyInt64Field
    def f9(): del current_type.InitOnlyDoubleField
    def f10(): del current_type.InitOnlySingleField
    def f11(): del current_type.InitOnlyDecimalField
    
    def f12(): del current_type.InitOnlyCharField
    def f13(): del current_type.InitOnlyBooleanField
    def f14(): del current_type.InitOnlyStringField

    def f15(): del current_type.InitOnlyEnumField
    def f16(): del current_type.InitOnlyDateTimeField
    
    def f17(): del current_type.InitOnlySimpleStructField 
    def f18(): del current_type.InitOnlySimpleGenericStructField 
    def f19(): del current_type.InitOnlyNullableStructField_NotNull 
    def f20(): del current_type.InitOnlyNullableStructField_Null 
    def f21(): del current_type.InitOnlySimpleClassField 
    def f22(): del current_type.InitOnlySimpleGenericClassField 
    def f23(): del current_type.InitOnlySimpleInterfaceField

    for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17, f18, f19, f20, f21, f22, f23]:
        AssertErrorWithMatch(AttributeError, message, f)

def _test_delete_via_instance(current_type, message="cannot delete attribute 'InitOnly.*' of builtin type"):
    o = current_type()
    def f1(): del o.InitOnlyByteField
    def f2(): del o.InitOnlySByteField
    def f3(): del o.InitOnlyUInt16Field
    def f4(): del o.InitOnlyInt16Field
    def f5(): del o.InitOnlyUInt32Field
    def f6(): del o.InitOnlyInt32Field
    def f7(): del o.InitOnlyUInt64Field
    def f8(): del o.InitOnlyInt64Field
    def f9(): del o.InitOnlyDoubleField
    def f10(): del o.InitOnlySingleField
    def f11(): del o.InitOnlyDecimalField
    
    def f12(): del o.InitOnlyCharField
    def f13(): del o.InitOnlyBooleanField
    def f14(): del o.InitOnlyStringField

    def f15(): del o.InitOnlyEnumField
    def f16(): del o.InitOnlyDateTimeField
    
    def f17(): del o.InitOnlySimpleStructField 
    def f18(): del o.InitOnlySimpleGenericStructField 
    def f19(): del o.InitOnlyNullableStructField_NotNull 
    def f20(): del o.InitOnlyNullableStructField_Null 
    def f21(): del o.InitOnlySimpleClassField 
    def f22(): del o.InitOnlySimpleGenericClassField 
    def f23(): del o.InitOnlySimpleInterfaceField

    for f in [f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f16, f17, f18, f19, f20, f21, f22, f23]:
        AssertErrorWithMatch(AttributeError, message, f)

def _test_delete_via_descriptor(current_type):
    o = current_type()
    i = 0 
    for x in ['Byte', 'SByte', 'UInt16', 'Int16', 'UInt32', 'Int32', 'UInt64', 'Int64', 'Double', 'Single', 'Decimal', 
              'Char', 'Boolean', 'String', 'Enum', 'DateTime', 
              'SimpleStruct', 'SimpleGenericStruct', 'SimpleClass', 'SimpleGenericClass',  
              'SimpleInterface',
             ]:
        if i % 4 == 0: arg = o
        if i % 4 == 1: arg = None
        if i % 4 == 2: arg = current_type
        if i % 4 == 3: arg = SimpleStruct
        i += 1

        AssertErrorWithMatch(AttributeError, "cannot delete attribute 'InitOnly.*Field' of builtin type", 
               lambda : current_type.__dict__['InitOnly%sField' % x].__delete__(arg))

types = [
    StructWithInitOnlys, 
    GenericStructWithInitOnlys[int], GenericStructWithInitOnlys[str], 
    ClassWithInitOnlys, GenericClassWithInitOnlys[int], GenericClassWithInitOnlys[object],
    ]
    
for i in range(len(types)):
    exec("def test_%s_get_by_instance():   _test_get_by_instance(types[%s])" % (i, i))
    exec("def test_%s_get_by_type():   _test_get_by_type(types[%s])" % (i, i))
    exec("def test_%s_get_by_descriptor():   _test_get_by_descriptor(types[%s])" % (i, i))
    exec("def test_%s_set_by_instance():   _test_set_by_instance(types[%s])" % (i, i))
    exec("def test_%s_set_by_type():   _test_set_by_type(types[%s])" % (i, i))
    exec("def test_%s_set_by_descriptor():   _test_set_by_descriptor(types[%s])" % (i, i))
    exec("def test_%s_delete_via_type():   _test_delete_via_type(types[%s])" % (i, i))
    exec("def test_%s_delete_via_instance():   _test_delete_via_instance(types[%s])" % (i, i))
    exec("def test_%s_delete_via_descriptor():   _test_delete_via_descriptor(types[%s])" % (i, i))

def test_generic_fields():
    for gt in [ GenericStructWithInitOnlys, GenericClassWithInitOnlys ]:
        cgt = gt[int]
        for x in [ cgt, cgt() ]:
            AreEqual(x.InitOnlyTField, 0)
            AreEqual(x.InitOnlyClassTField.Flag, 0)
            AreEqual(x.InitOnlyStructTField.Flag, 0)
        
        cgt = gt[ClassWithDefaultCtor]
        for x in [ cgt, cgt() ]:
            AreEqual(x.InitOnlyTField, None)
            AreEqual(x.InitOnlyClassTField.Flag, None)
            AreEqual(x.InitOnlyStructTField.Flag, None)
        
        cgt = gt[StructWithDefaultCtor]
        for x in [ cgt, cgt() ]:
            AreEqual(x.InitOnlyTField.Flag, 42)
            AreEqual(x.InitOnlyClassTField.Flag.Flag, 42)
            AreEqual(x.InitOnlyStructTField.Flag.Flag, 42)
         
        # set / delete may not necessary    

def test_accessing_from_derived():
    _test_get_by_instance(DerivedClass)
    _test_get_by_type(DerivedClass)
    _test_set_by_instance(DerivedClass)
    _test_set_by_type(DerivedClass)
    _test_delete_via_instance(DerivedClass)
    _test_delete_via_type(DerivedClass)
    
    Assert('InitOnlyInt32Field' not in DerivedClass.__dict__)
    
run_test(__name__)

