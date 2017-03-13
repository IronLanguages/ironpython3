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
    from Merlin.Testing.FieldTest.Literal import *
    from Merlin.Testing.TypeSample import *

def _test_get_by_instance(current_type):
    o = current_type()

    AreEqual(o.LiteralByteField, 1)
    AreEqual(o.LiteralSByteField, 2)
    AreEqual(o.LiteralUInt16Field, 3)
    AreEqual(o.LiteralInt16Field, 4)
    AreEqual(o.LiteralUInt32Field, 5)
    AreEqual(o.LiteralInt32Field, 6)
    AreEqual(o.LiteralUInt64Field, 7)
    AreEqual(o.LiteralInt64Field, 8)
    AreEqual(o.LiteralDoubleField, 9)
    AreEqual(o.LiteralSingleField, 10)
    AreEqual(o.LiteralDecimalField, 11)
    
    AreEqual(o.LiteralCharField, 'K')
    AreEqual(o.LiteralBooleanField, True)
    AreEqual(o.LiteralStringField, 'DLR')
    
    AreEqual(o.LiteralEnumField, EnumInt32.B)
    AreEqual(o.LiteralClassField, None)
    AreEqual(o.LiteralInterfaceField, None)

def _test_get_by_type(current_type):    
    AreEqual(current_type.LiteralByteField, 1)
    AreEqual(current_type.LiteralSByteField, 2)
    AreEqual(current_type.LiteralUInt16Field, 3)
    AreEqual(current_type.LiteralInt16Field, 4)
    AreEqual(current_type.LiteralUInt32Field, 5)
    AreEqual(current_type.LiteralInt32Field, 6)
    AreEqual(current_type.LiteralUInt64Field, 7)
    AreEqual(current_type.LiteralInt64Field, 8)
    AreEqual(current_type.LiteralDoubleField, 9)
    AreEqual(current_type.LiteralSingleField, 10)
    AreEqual(current_type.LiteralDecimalField, 11)
    
    AreEqual(current_type.LiteralCharField, 'K')
    AreEqual(current_type.LiteralBooleanField, True)
    AreEqual(current_type.LiteralStringField, 'DLR')

    AreEqual(current_type.LiteralEnumField, EnumInt32.B)
    AreEqual(current_type.LiteralClassField, None)
    AreEqual(current_type.LiteralInterfaceField, None)

def _test_get_by_descriptor(current_type):
    o = current_type()
    AreEqual(current_type.__dict__['LiteralByteField'], 1)
    AreEqual(current_type.__dict__['LiteralSByteField'], 2)
    AreEqual(current_type.__dict__['LiteralUInt16Field'], 3)
    AreEqual(current_type.__dict__['LiteralInt16Field'], 4)
    AreEqual(current_type.__dict__['LiteralUInt32Field'], 5)
    AreEqual(current_type.__dict__['LiteralInt32Field'], 6)
    AreEqual(current_type.__dict__['LiteralUInt64Field'], 7)
    AreEqual(current_type.__dict__['LiteralInt64Field'], 8)
    AreEqual(current_type.__dict__['LiteralDoubleField'], 9)
    AreEqual(current_type.__dict__['LiteralSingleField'], 10)
    AreEqual(current_type.__dict__['LiteralDecimalField'].__get__(o, current_type), 11)
    
    AreEqual(current_type.__dict__['LiteralCharField'], "K")
    AreEqual(current_type.__dict__['LiteralBooleanField'], True)
    AreEqual(current_type.__dict__['LiteralStringField'], "DLR")

    AreEqual(current_type.__dict__['LiteralEnumField'], EnumInt32.B)
    AreEqual(current_type.__dict__['LiteralClassField'], None)
    AreEqual(current_type.__dict__['LiteralInterfaceField'], None)

def _test_set_by_instance(current_type):
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
        AssertErrorWithMatch(AttributeError, "attribute .* of .* object is read-only", f)
    
def _test_set_by_type(current_type):
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
        AssertErrorWithMatch(AttributeError, "attribute '.*' of '.*' object is read-only", f)

        
def _test_delete_via_type(current_type):
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
        AssertErrorWithMatch(AttributeError, "cannot delete attribute 'Literal.*Field' of builtin type", f)

def _test_delete_via_instance(current_type, message="cannot delete attribute 'Literal.*Field' of builtin type"):
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
        AssertErrorWithMatch(AttributeError, message, f)

types = [
    StructWithLiterals, 
    GenericStructWithLiterals[int], 
    GenericStructWithLiterals[str], 
    ClassWithLiterals, 
    GenericClassWithLiterals[int], 
    GenericClassWithLiterals[object],
    ]
for i in range(len(types)):
    exec("def test_%s_get_by_instance():   _test_get_by_instance(types[%s])" % (i, i))
    exec("def test_%s_get_by_type():   _test_get_by_type(types[%s])" % (i, i))
    exec("def test_%s_get_by_descriptor():   _test_get_by_descriptor(types[%s])" % (i, i))
    exec("def test_%s_set_by_instance():   _test_set_by_instance(types[%s])" % (i, i))
    exec("def test_%s_set_by_type():   _test_set_by_type(types[%s])" % (i, i))
    exec("def test_%s_delete_via_type():   _test_delete_via_type(types[%s])" % (i, i))
    exec("def test_%s_delete_via_instance():   _test_delete_via_instance(types[%s])" % (i, i))

def test_accessing_from_derived():
    _test_get_by_instance(DerivedClass)
    _test_get_by_type(DerivedClass)
    _test_set_by_instance(DerivedClass)
    _test_set_by_type(DerivedClass) 
    _test_delete_via_instance(DerivedClass)
    _test_delete_via_type(DerivedClass)
    
    Assert('LiteralInt32Field' not in DerivedClass.__dict__)
    
run_test(__name__)

