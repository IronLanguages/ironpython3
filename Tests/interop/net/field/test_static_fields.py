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
    from Merlin.Testing.FieldTest import *
    from Merlin.Testing.TypeSample import *

def _test_get_by_instance(current_type):
    o = current_type()
    o.SetStaticFields()

    AreEqual(o.StaticByteField, 0)
    AreEqual(o.StaticSByteField, 1)
    AreEqual(o.StaticUInt16Field, 2)
    AreEqual(o.StaticInt16Field, 3)
    AreEqual(o.StaticUInt32Field, 4)
    AreEqual(o.StaticInt32Field, 5)
    AreEqual(o.StaticUInt64Field, 6)
    AreEqual(o.StaticInt64Field, 7)
    AreEqual(o.StaticDoubleField, 8)
    AreEqual(o.StaticSingleField, 9)
    AreEqual(o.StaticDecimalField, 10)
    AreEqual(o.StaticCharField, 'a')
    AreEqual(o.StaticBooleanField, True)
    AreEqual(o.StaticStringField, 'testing')
    
    AreEqual(o.StaticObjectField.Flag, 1111)
    AreEqual(o.StaticEnumField, EnumInt64.B)
    AreEqual(o.StaticDateTimeField, System.DateTime(50000))
    AreEqual(o.StaticSimpleStructField.Flag, 1234)
    AreEqual(o.StaticSimpleGenericStructField.Flag, 32) 
    AreEqual(o.StaticNullableStructNotNullField.Flag, 56)
    AreEqual(o.StaticNullableStructNullField, None)
    AreEqual(o.StaticSimpleClassField.Flag, 54)
    AreEqual(o.StaticSimpleGenericClassField.Flag, "string")
    AreEqual(o.StaticSimpleInterfaceField.Flag, 87)
    
def _test_get_by_type(current_type):    
    current_type.SetStaticFields()

    AreEqual(current_type.StaticByteField, 0)
    AreEqual(current_type.StaticSByteField, 1)
    AreEqual(current_type.StaticUInt16Field, 2)
    AreEqual(current_type.StaticInt16Field, 3)
    AreEqual(current_type.StaticUInt32Field, 4)
    AreEqual(current_type.StaticInt32Field, 5)
    AreEqual(current_type.StaticUInt64Field, 6)
    AreEqual(current_type.StaticInt64Field, 7)
    AreEqual(current_type.StaticDoubleField, 8)
    AreEqual(current_type.StaticSingleField, 9)
    AreEqual(current_type.StaticDecimalField, 10)
    AreEqual(current_type.StaticCharField, 'a')
    AreEqual(current_type.StaticBooleanField, True)
    AreEqual(current_type.StaticStringField, 'testing')
    
    AreEqual(current_type.StaticObjectField.Flag, 1111)
    AreEqual(current_type.StaticEnumField, EnumInt64.B)
    AreEqual(current_type.StaticDateTimeField, System.DateTime(50000))
    AreEqual(current_type.StaticSimpleStructField.Flag, 1234)
    AreEqual(current_type.StaticSimpleGenericStructField.Flag, 32) 
    AreEqual(current_type.StaticNullableStructNotNullField.Flag, 56)
    AreEqual(current_type.StaticNullableStructNullField, None)
    AreEqual(current_type.StaticSimpleClassField.Flag, 54)
    AreEqual(current_type.StaticSimpleGenericClassField.Flag, "string")
    AreEqual(current_type.StaticSimpleInterfaceField.Flag, 87)

def _test_get_by_descriptor(current_type):
    current_type.SetStaticFields()
    o = current_type()
    AreEqual(current_type.__dict__['StaticByteField'].__get__(None, current_type), 0)
    AreEqual(current_type.__dict__['StaticSByteField'].__get__(o, current_type), 1)
    AreEqual(current_type.__dict__['StaticUInt16Field'].__get__(None, current_type), 2)
    AreEqual(current_type.__dict__['StaticInt16Field'].__get__(o, current_type), 3)
    AreEqual(current_type.__dict__['StaticUInt32Field'].__get__(None, current_type), 4)
    AreEqual(current_type.__dict__['StaticInt32Field'].__get__(o, current_type), 5)
    AreEqual(current_type.__dict__['StaticUInt64Field'].__get__(None, current_type), 6)
    AreEqual(current_type.__dict__['StaticInt64Field'].__get__(o, current_type), 7)
    AreEqual(current_type.__dict__['StaticDoubleField'].__get__(None, current_type), 8)
    AreEqual(current_type.__dict__['StaticSingleField'].__get__(o, current_type), 9)
    AreEqual(current_type.__dict__['StaticDecimalField'].__get__(None, current_type), 10)
    AreEqual(current_type.__dict__['StaticCharField'].__get__(o, current_type), 'a')
    AreEqual(current_type.__dict__['StaticBooleanField'].__get__(None, current_type), True)
    AreEqual(current_type.__dict__['StaticStringField'].__get__(o, current_type), 'testing')

    AreEqual(current_type.__dict__['StaticObjectField'].__get__(None, current_type).Flag, 1111)
    AreEqual(current_type.__dict__['StaticEnumField'].__get__(o, current_type), EnumInt64.B)
    AreEqual(current_type.__dict__['StaticDateTimeField'].__get__(None, current_type), System.DateTime(50000))
    AreEqual(current_type.__dict__['StaticSimpleStructField'].__get__(o, current_type).Flag, 1234)
    AreEqual(current_type.__dict__['StaticSimpleGenericStructField'].__get__(None, current_type).Flag, 32) 
    AreEqual(current_type.__dict__['StaticNullableStructNotNullField'].__get__(o, current_type).Flag, 56)
    AreEqual(current_type.__dict__['StaticNullableStructNullField'].__get__(None, current_type), None)
    AreEqual(current_type.__dict__['StaticSimpleClassField'].__get__(o, current_type).Flag, 54)
    AreEqual(current_type.__dict__['StaticSimpleGenericClassField'].__get__(None, current_type).Flag, "string")

    AreEqual(current_type.__dict__['StaticSimpleInterfaceField'].__get__(o, current_type).Flag, 87)
    # TODO (pass in other values to __get__)

def _test_verify(current_type):
    AreEqual(current_type.StaticByteField, 5)
    AreEqual(current_type.StaticSByteField, 10)
    AreEqual(current_type.StaticUInt16Field, 20)
    AreEqual(current_type.StaticInt16Field, 30)
    AreEqual(current_type.StaticUInt32Field, 40)
    AreEqual(current_type.StaticInt32Field, 50)
    AreEqual(current_type.StaticUInt64Field, 60)
    AreEqual(current_type.StaticInt64Field, 70)
    AreEqual(current_type.StaticDoubleField, 80)
    AreEqual(current_type.StaticSingleField, 90)
    AreEqual(current_type.StaticDecimalField, 100)
    AreEqual(current_type.StaticCharField, 'd')
    AreEqual(current_type.StaticBooleanField, False)
    AreEqual(current_type.StaticStringField, 'TESTING')
    
    AreEqual(current_type.StaticObjectField, "number_to_string")
    AreEqual(current_type.StaticEnumField, EnumInt64.C)
    AreEqual(current_type.StaticDateTimeField, System.DateTime(500000))
    AreEqual(current_type.StaticSimpleStructField.Flag, 12340)
    AreEqual(current_type.StaticSimpleGenericStructField.Flag, 320) 
    AreEqual(current_type.StaticNullableStructNotNullField, None)
    AreEqual(current_type.StaticNullableStructNullField.Flag, 650)
    AreEqual(current_type.StaticSimpleClassField.Flag, 540)
    AreEqual(current_type.StaticSimpleGenericClassField.Flag, "STRING")

    AreEqual(current_type.StaticSimpleInterfaceField.Flag, 78)

def _test_set_by_instance(current_type):
    current_type.SetStaticFields()
    o = current_type()

    # pass correct values
    def f1(): o.StaticByteField =  5
    def f2(): o.StaticSByteField = 10
    def f3(): o.StaticUInt16Field = 20
    def f4(): o.StaticInt16Field = 30
    def f5(): o.StaticUInt32Field = 40
    def f6(): o.StaticInt32Field = 50
    def f7(): o.StaticUInt64Field = 60
    def f8(): o.StaticInt64Field = 70
    def f9(): o.StaticDoubleField = 80
    def f10(): o.StaticSingleField = 90
    def f11(): o.StaticDecimalField = 100
    def f12(): o.StaticCharField = 'd'
    def f13(): o.StaticBooleanField = False
    def f14(): o.StaticStringField = 'testing'.upper()
    
    def f15(): o.StaticObjectField = "number_to_string"
    def f16(): o.StaticEnumField = EnumInt64.C
    def f17(): o.StaticDateTimeField = System.DateTime(500000)
    def f18(): o.StaticSimpleStructField = SimpleStruct(12340)
    def f19(): o.StaticSimpleGenericStructField = SimpleGenericStruct[System.UInt16](320)
    def f20(): o.StaticNullableStructNotNullField = None
    def f21(): o.StaticNullableStructNullField = SimpleStruct(650)
    def f22(): o.StaticSimpleClassField = SimpleClass(540)
    def f23(): o.StaticSimpleGenericClassField = SimpleGenericClass[str]("string".upper())
    def f24(): o.StaticSimpleInterfaceField = ClassImplementSimpleInterface(78)

    funcs = [ eval("f%s" % i) for i in range(1, 25) ]
    for f in funcs: f()
    _test_verify(current_type)
    
    # set values which need conversion.
    o.StaticInt32Field = 100L
    AreEqual(current_type.StaticInt32Field, 100)

    o.StaticInt32Field = 10.01
    AreEqual(current_type.StaticInt32Field, 10)

    # set bad values 
    def f1(): o.StaticInt32Field = "abc"
    def f2(): o.StaticEnumField = 3
    
    for f in [f1, f2]: AssertError(TypeError, f)

def _test_set_by_type(current_type):
    current_type.SetStaticFields()

    # pass correct values
    current_type.StaticByteField =  5
    current_type.StaticSByteField = 10
    current_type.StaticUInt16Field = 20
    current_type.StaticInt16Field = 30
    current_type.StaticUInt32Field = 40
    current_type.StaticInt32Field = 50
    current_type.StaticUInt64Field = 60
    current_type.StaticInt64Field = 70
    current_type.StaticDoubleField = 80
    current_type.StaticSingleField = 90
    current_type.StaticDecimalField = 100
    current_type.StaticCharField = 'd'
    current_type.StaticBooleanField = False
    current_type.StaticStringField = 'testing'.upper()
    
    current_type.StaticObjectField = "number_to_string"
    current_type.StaticEnumField = EnumInt64.C
    current_type.StaticDateTimeField = System.DateTime(500000)
    current_type.StaticSimpleStructField = SimpleStruct(12340)
    current_type.StaticSimpleGenericStructField = SimpleGenericStruct[System.UInt16](320)
    current_type.StaticNullableStructNotNullField = None
    current_type.StaticNullableStructNullField = SimpleStruct(650)
    current_type.StaticSimpleClassField = SimpleClass(540)
    current_type.StaticSimpleGenericClassField = SimpleGenericClass[str]("string".upper())
    current_type.StaticSimpleInterfaceField = ClassImplementSimpleInterface(78)

    # verify
    _test_verify(current_type)

    # set values which need conversion.
    current_type.StaticInt16Field = 100L
    AreEqual(current_type.StaticInt16Field, 100)

    current_type.StaticBooleanField = 0
    AreEqual(current_type.StaticBooleanField, False)

    # set bad values 
    def f1(): current_type.StaticInt16Field = "abc"
    def f2(): current_type.StaticCharField = "abc"
    def f3(): current_type.StaticEnumField = EnumInt32.B

    for f in [f1, f2, f3]: AssertError(TypeError, f)

def _test_set_by_descriptor(current_type):
    current_type.SetStaticFields()
    o = current_type()

    # pass correct values
    current_type.__dict__['StaticByteField'].__set__(None, 5)
    current_type.__dict__['StaticSByteField'].__set__(None, 10)
    #current_type.__dict__['StaticSByteField'].__set__(o, 10)  
    current_type.__dict__['StaticUInt16Field'].__set__(None, 20)
    current_type.__dict__['StaticInt16Field'].__set__(None, 30)
    current_type.__dict__['StaticUInt32Field'].__set__(None, 40)
    current_type.__dict__['StaticInt32Field'].__set__(None, 50)
    current_type.__dict__['StaticUInt64Field'].__set__(None, 60)
    current_type.__dict__['StaticInt64Field'].__set__(None, 70)
    current_type.__dict__['StaticDoubleField'].__set__(None, 80)
    current_type.__dict__['StaticSingleField'].__set__(None, 90)
    current_type.__dict__['StaticDecimalField'].__set__(None, 100)
    current_type.__dict__['StaticCharField'].__set__(None, 'd')
    current_type.__dict__['StaticBooleanField'].__set__(None, False)
    current_type.__dict__['StaticStringField'].__set__(None, 'TESTING')

    current_type.__dict__['StaticObjectField'].__set__(None, "number_to_string")
    current_type.__dict__['StaticEnumField'].__set__(None, EnumInt64.C)
    current_type.__dict__['StaticDateTimeField'].__set__(None, System.DateTime(500000))
    current_type.__dict__['StaticSimpleStructField'].__set__(None, SimpleStruct(12340))
    current_type.__dict__['StaticSimpleGenericStructField'].__set__(None, SimpleGenericStruct[System.UInt16](320)) 
    current_type.__dict__['StaticNullableStructNotNullField'].__set__(None, None)
    current_type.__dict__['StaticNullableStructNullField'].__set__(None, SimpleStruct(650))
    current_type.__dict__['StaticSimpleClassField'].__set__(None, SimpleClass(540))
    current_type.__dict__['StaticSimpleGenericClassField'].__set__(None, SimpleGenericClass[str]("STRING"))
    current_type.__dict__['StaticSimpleInterfaceField'].__set__(None, ClassImplementSimpleInterface(78))

    # verify
    _test_verify(current_type)
    
    # set with bad values (TODO)

def _test_delete_by_type(current_type):
    def f1(): del current_type.StaticByteField 
    def f2(): del current_type.StaticSByteField
    def f3(): del current_type.StaticUInt16Field
    def f4(): del current_type.StaticInt16Field 
    def f5(): del current_type.StaticUInt32Field
    def f6(): del current_type.StaticInt32Field 
    def f7(): del current_type.StaticUInt64Field
    def f8(): del current_type.StaticInt64Field
    def f9(): del current_type.StaticDoubleField 
    def f10(): del current_type.StaticSingleField
    def f11(): del current_type.StaticDecimalField
    def f12(): del current_type.StaticCharField 
    def f13(): del current_type.StaticBooleanField 
    def f14(): del current_type.StaticStringField
    
    def f15(): del current_type.StaticObjectField
    def f16(): del current_type.StaticEnumField 
    def f17(): del current_type.StaticDateTimeField 
    def f18(): del current_type.StaticSimpleStructField 
    def f19(): del current_type.StaticSimpleGenericStructField 
    def f20(): del current_type.StaticNullableStructNotNullField
    def f21(): del current_type.StaticNullableStructNullField 
    def f22(): del current_type.StaticSimpleClassField 
    def f23(): del current_type.StaticSimpleGenericClassField 
    def f24(): del current_type.StaticSimpleInterfaceField

    funcs = [ eval("f%s" % i) for i in range(1, 25) ]
    for f in funcs:
        AssertError(AttributeError, f)  # ???

def _test_delete_by_instance(current_type):
    o = current_type()
    def f1(): del o.StaticByteField 
    def f2(): del o.StaticSByteField
    def f3(): del o.StaticUInt16Field
    def f4(): del o.StaticInt16Field 
    def f5(): del o.StaticUInt32Field
    def f6(): del o.StaticInt32Field 
    def f7(): del o.StaticUInt64Field
    def f8(): del o.StaticInt64Field
    def f9(): del o.StaticDoubleField 
    def f10(): del o.StaticSingleField
    def f11(): del o.StaticDecimalField
    def f12(): del o.StaticCharField 
    def f13(): del o.StaticBooleanField 
    def f14(): del o.StaticStringField
    
    def f15(): del o.StaticObjectField
    def f16(): del o.StaticEnumField 
    def f17(): del o.StaticDateTimeField 
    def f18(): del o.StaticSimpleStructField 
    def f19(): del o.StaticSimpleGenericStructField 
    def f20(): del o.StaticNullableStructNotNullField
    def f21(): del o.StaticNullableStructNullField 
    def f22(): del o.StaticSimpleClassField 
    def f23(): del o.StaticSimpleGenericClassField 
    def f24(): del o.StaticSimpleInterfaceField

    funcs = [ eval("f%s" % i) for i in range(1, 25) ]
    for f in funcs:
        AssertError(AttributeError, f)  # ???

def _test_delete_by_descriptor(current_type):
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
            AssertError(AttributeError, lambda: current_type.__dict__['Static%sField' % x].__delete__(o))
        
types = [ 
    Struct, 
    GenericStruct[int],
    GenericStruct[SimpleClass],
    Class,
    GenericClass[SimpleStruct],
    GenericClass[SimpleClass],
    ]

for i in range(len(types)):
    exec("def test_%s_get_by_instance():   _test_get_by_instance(types[%s])" % (i, i))
    exec("def test_%s_get_by_type():   _test_get_by_type(types[%s])" % (i, i))
    exec("def test_%s_get_by_descriptor():   _test_get_by_descriptor(types[%s])" % (i, i))
    exec("def test_%s_set_by_instance():   _test_set_by_instance(types[%s])" % (i, i))
    exec("def test_%s_set_by_type():   _test_set_by_type(types[%s])" % (i, i))
    exec("def test_%s_set_by_descriptor():   _test_set_by_descriptor(types[%s])" % (i, i))
    exec("def test_%s_delete_by_type():   _test_delete_by_type(types[%s])" % (i, i))
    exec("def test_%s_delete_by_instance():   _test_delete_by_instance(types[%s])" % (i, i))
    exec("def test_%s_delete_by_descriptor():   _test_delete_by_descriptor(types[%s])" % (i, i))

@skip("multiple_execute")
def test_nested():
    for s in [ Struct2, GenericStruct2[int], GenericStruct2[str] ]:
        AreEqual(s.StaticNextField.StaticNextField.StaticNextField.StaticField, 10)
        s.StaticNextField.StaticNextField.StaticNextField.StaticField = -10
        AreEqual(s.StaticNextField.StaticNextField.StaticNextField.StaticField, -10)        

    for c in [ Class2, GenericClass2[System.Byte], GenericClass2[object] ]:
        AreEqual(c.StaticNextField, None)
        c.StaticNextField = c()
        AreEqual(c.StaticNextField.StaticNextField.StaticNextField.StaticField, 10)
        c.StaticNextField.StaticNextField.StaticNextField.StaticField = 20
        AreEqual(c.StaticField, 20)
    
def test_generic_fields():
    for gt in [GenericStruct2, GenericClass2]:
        current_type = gt[int]
        o = current_type()
        current_type.StaticTField = 30
        current_type.StaticClassTField = SimpleGenericClass[int](40)
        current_type.StaticStructTField = SimpleGenericStruct[int](50)
        
        AreEqual(o.StaticTField, 30)
        AreEqual(current_type.StaticClassTField.Flag, 40)
        AreEqual(o.StaticStructTField.Flag, 50)

        def f(): o.StaticStructTField = SimpleGenericStruct[int](60)
        f()
        AreEqual(current_type.StaticStructTField.Flag, 60)

        current_type = gt[str]
        o = current_type()
        current_type.StaticTField = "30"
        current_type.StaticClassTField = SimpleGenericClass[str]("40")
        current_type.StaticStructTField = SimpleGenericStruct[str]("50")

        AreEqual(o.StaticTField, '30')
        AreEqual(current_type.StaticClassTField.Flag, '40')
        AreEqual(o.StaticStructTField.Flag, '50')

        def f(): o.StaticClassTField = SimpleGenericClass[str]("60")
        f()
        AreEqual(current_type.StaticClassTField.Flag, "60")

def test_access_from_derived_types():
    for current_type in [ 
        DerivedClass, 
        DerivedOpenGenericClass[int], 
        DerivedOpenGenericClass[str], 
        DerivedGenericClassOfInt32,
        DerivedGenericClassOfObject,
    ]:
        _test_get_by_instance(current_type)
        _test_get_by_type(current_type)

        #
        # the behavior for derived type is different from that for the base type. 
        # I have to write seperate tests as below, instead of using the 2 lines
        #
        
        #_test_set_by_instance(current_type) 
        #_test_set_by_type(current_type)
        
        o = current_type()
        def f1(): o.StaticByteField = 1
        def f2(): current_type.StaticByteField = 1

        AssertErrorWithMatch(AttributeError, "'.*' object has no attribute 'StaticByteField'", f1)
        AssertErrorWithMatch(AttributeError, "'.*' object has no attribute 'StaticByteField'", f2)
        
        Assert('StaticByteField' not in current_type.__dict__)


run_test(__name__)
