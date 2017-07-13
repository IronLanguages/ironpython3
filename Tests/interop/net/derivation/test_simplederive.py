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

import unittest

from iptest import IronPythonTestCase, is_netstandard, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class SimpleDeriveTest(IronPythonTestCase):
    def setUp(self):
        super(SimpleDeriveTest, self).setUp()
        self.add_clr_assemblies("baseclasscs", "typesamples")

    def test_simply_derive(self):
        import System
        from Merlin.Testing.BaseClass import EmptyClass, EmptyTypeGroup2, EmptyGenericClass, IEmpty, IGenericEmpty, AbstractEmptyClass, INotEmpty, AbstractNotEmptyClass
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

    def test_multiple_typegroup(self):
        from Merlin.Testing.BaseClass import EmptyClass, EmptyTypeGroup2, IInterfaceGroup1, IInterfaceGroup2
        class C(IInterfaceGroup1, IInterfaceGroup2): pass
        class C(IInterfaceGroup1, IInterfaceGroup2, EmptyClass): pass
        class C(EmptyTypeGroup2, IInterfaceGroup1, IInterfaceGroup2): pass
        class C(EmptyTypeGroup2, IInterfaceGroup1[int], IInterfaceGroup2): pass
    
    def test_negative_simply_derive(self):
        import clr
        import System
        from Merlin.Testing.BaseClass import EmptyClass, EmptyEnum, EmptyGenericClass, EmptyStruct, EmptyTypeGroup1, IEmpty, IGenericEmpty, SealedClass
        # value type, sealed ref type
        def f1():
            class C(EmptyStruct): pass
        def f2():
            class C(EmptyEnum): pass
        def f3():
            class C(SealedClass): pass
        def f4():
            class C(System.Single): pass

        
        self.assertRaisesMessage(TypeError, "cannot derive from Merlin.Testing.BaseClass.EmptyStruct because it is a value type", f1)
        self.assertRaisesMessage(TypeError, "cannot derive from Merlin.Testing.BaseClass.EmptyEnum because it is a value type", f2)
        self.assertRaisesMessage(TypeError, "cannot derive from Merlin.Testing.BaseClass.SealedClass because it is sealed", f3)
        self.assertRaisesMessage(TypeError, "cannot derive from System.Single because it is a value type", f4)

        # open generic
        def f():
            class C(EmptyGenericClass): pass
        self.assertRaisesMessage(TypeError, 
            "C: cannot inhert from open generic instantiation IronPython.Runtime.Types.PythonType. Only closed instantiations are supported.",
            f)
        
        def f():
            class C(IGenericEmpty): pass
        self.assertRaisesMessage(TypeError, 
            "C: cannot inhert from open generic instantiation Merlin.Testing.BaseClass.IGenericEmpty`1[T]. Only closed instantiations are supported.",
            f)
        
        def f():
            class C(EmptyTypeGroup1): pass
        self.assertRaisesMessage(TypeError, 
            "cannot derive from open generic types <types 'EmptyTypeGroup1[T]', 'EmptyTypeGroup1[K, V]'>",
            f)

        # too many base (same or diff)
        def f():    
            class C(EmptyClass, EmptyClass): pass
        self.assertRaisesMessage(TypeError, "duplicate base class EmptyClass", f)
    
        def f():
            class C(IEmpty, EmptyClass, IEmpty): pass
        self.assertRaisesMessage(TypeError, "duplicate base class IEmpty", f)

        assemblyqualifiedname = clr.GetClrType(int).AssemblyQualifiedName
        def f():
            class C(EmptyClass, EmptyGenericClass[int]): pass
        self.assertRaisesMessage(TypeError, 
            "C: can only extend one CLI or builtin type, not both Merlin.Testing.BaseClass.EmptyClass (for IronPython.Runtime.Types.PythonType) and Merlin.Testing.BaseClass.EmptyGenericClass`1[[%s]] (for IronPython.Runtime.Types.PythonType)" % assemblyqualifiedname,
            f)
        
        class B:pass
        b = B()
        def f(): 
            class C(object, b): pass
        self.assertRaisesPartialMessage(TypeError, 
            "metaclass conflict: the metaclass of a derived class must be a (non-strict) subclass of the metaclasses of all its bases",
            f)
        
        def f():
            class C(EmptyGenericClass[()]): pass
        self.assertRaises(ValueError, f)
    
    def test_system_type_cs0644(self):
        # http://msdn2.microsoft.com/en-us/library/hxds244y(VS.80).aspx
        # bug 363984
        import System
        def inheritDelegate():
            class C(System.Delegate): pass

        def inheritArray():
            class C(System.Array): pass

        def inheritValueType():
            class C(System.ValueType): pass

        def inheritEnum():
            class C(System.Enum): pass

        self.assertRaises(TypeError, inheritDelegate)
        self.assertRaises(TypeError, inheritArray)
        self.assertRaises(TypeError, inheritValueType)
        self.assertRaises(TypeError, inheritEnum)


    @unittest.skipIf(is_netstandard, 'no System.MarshalByRefObject in netstandard')
    def test_mbr(self):
        import System
        class C(System.MarshalByRefObject): pass

        #class C('abc'): pass


# scenarios
# C derive from interface I, D derive from C and I (again)

# interface's base types: interfaces (implement them)
# ctor: params/param_dict

run_test(__name__)
