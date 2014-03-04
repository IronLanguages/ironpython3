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
Create a Python class which derives from CLR type(s).
'''
#------------------------------------------------------------------------------

from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("baseclasscs", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.BaseClass import *
import System

def test_simply_derive():
    class C(EmptyClass): pass
    class C(EmptyTypeGroup2): pass
    class C(EmptyGenericClass[int]): pass
    class C(IEmpty): pass
    class C(IGenericEmpty[int]): pass
    class C(AbstractEmptyClass): pass

    class C(INotEmpty): pass
    class C(AbstractNotEmptyClass): pass
    
    #class C(EmptyDelegate): pass
    
    class C(System.Double): pass

def test_multiple_typegroup():
    class C(IInterfaceGroup1, IInterfaceGroup2): pass
    class C(IInterfaceGroup1, IInterfaceGroup2, EmptyClass): pass
    class C(EmptyTypeGroup2, IInterfaceGroup1, IInterfaceGroup2): pass    
    class C(EmptyTypeGroup2, IInterfaceGroup1[int], IInterfaceGroup2): pass    
    
def test_negative_simply_derive():
    # value type, sealed ref type
    def f1():
        class C(EmptyStruct): pass
    def f2():
        class C(EmptyEnum): pass
    def f3():
        class C(SealedClass): pass
    def f4():
        class C(System.Single): pass

    
    AssertErrorWithMessage(TypeError, "cannot derive from Merlin.Testing.BaseClass.EmptyStruct because it is a value type", f1)
    AssertErrorWithMessage(TypeError, "cannot derive from Merlin.Testing.BaseClass.EmptyEnum because it is a value type", f2)
    AssertErrorWithMessage(TypeError, "cannot derive from Merlin.Testing.BaseClass.SealedClass because it is sealed", f3)
    AssertErrorWithMessage(TypeError, "cannot derive from System.Single because it is a value type", f4)

    # open generic
    def f():
        class C(EmptyGenericClass): pass
    AssertErrorWithMessage(TypeError, 
        "C: cannot inhert from open generic instantiation IronPython.Runtime.Types.PythonType. Only closed instantiations are supported.",
        f)
    
    def f():
        class C(IGenericEmpty): pass
    AssertErrorWithMessage(TypeError, 
        "C: cannot inhert from open generic instantiation Merlin.Testing.BaseClass.IGenericEmpty`1[T]. Only closed instantiations are supported.",
        f)
    
    def f():
        class C(EmptyTypeGroup1): pass
    AssertErrorWithMessage(TypeError, 
        "cannot derive from open generic types <types 'EmptyTypeGroup1[T]', 'EmptyTypeGroup1[K, V]'>",
        f)

    # too many base (same or diff)
    def f():    
        class C(EmptyClass, EmptyClass): pass
    AssertErrorWithMessage(TypeError, "duplicate base class EmptyClass", f)
   
    def f():
        class C(IEmpty, EmptyClass, IEmpty): pass
    AssertErrorWithMessage(TypeError, "duplicate base class IEmpty", f)
                            
    def f():
        class C(EmptyClass, EmptyGenericClass[int]): pass
    AssertErrorWithMessage(TypeError, 
        "C: can only extend one CLI or builtin type, not both Merlin.Testing.BaseClass.EmptyClass (for IronPython.Runtime.Types.PythonType) and Merlin.Testing.BaseClass.EmptyGenericClass`1[[System.Int32, mscorlib, Version=%d.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]] (for IronPython.Runtime.Types.PythonType)" % System.Environment.Version.Major,
        f)
    
    class B:pass
    b = B()
    def f(): 
        class C(object, b): pass
    AssertErrorWithPartialMessage(TypeError, 
        "metaclass conflict instance and type",
        f)
    
    def f():
        class C(EmptyGenericClass[()]): pass
    AssertError(ValueError, f)
    
def test_system_type_cs0644():
    # http://msdn2.microsoft.com/en-us/library/hxds244y(VS.80).aspx
    # bug 363984
    #class C(System.Delegate): pass
    #class C(System.Array): pass
    #class C(System.ValueType): pass
    #class C(System.Enum): pass
    pass


def test_mbr():
    class C(System.MarshalByRefObject): pass

    #class C('abc'): pass


# scenarios
# C derive from interface I, D derive from C and I (again)

# interface's base types: interfaces (implement them)
# ctor: params/param_dict



run_test(__name__)

