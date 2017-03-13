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
This module consists of test cases which utilize the __clrtype__ method of 
Python callables set as the __metaclass__ member of Python classes.  Doing
this enables one to manually expose Python methods/attributes/classes directly
to the CLR.  For example, a Python class member might be exposed as a static
field on a CLR class.

Most tests found in this module deal with various nuances of __clrtype__
implementations and ensuring instances of Python classes using __clrtype__
via the __metaclass__ member behave the same as normal Python objects.

There are also a few sanity tests used to ensure the primary purpose
of __clrtype__ is actually met.  Namely, providing IronPython users the ability
to code entirely in Python without having to write Csharp code to do things 
like decorate their classes with custom attributes.  It should not be necessary 
to exhaustively test every possible use of __clrtype__ as we already get much 
of this coverage through our .NET interop inheritance tests.
'''


#--PRE-CLR IMPORT TESTS--------------------------------------------------------
if hasattr(type, "__clrtype__"):
    exc_msg = "type.__clrtype__ should not exist until the 'clr/System' module has been imported"
    print(exc_msg)
    raise Exception(exc_msg)


#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("win32")

import System
import clr

if is_posix:
    import posix as _os
else:
    import nt as _os

import os

clr.AddReference("Microsoft.Dynamic")

#--GLOBALS---------------------------------------------------------------------
TYPE_COUNTER = 0

#--HELPERS---------------------------------------------------------------------


#--TEST CASES------------------------------------------------------------------

###SANITY TEST CASES####################
def test_sanity___clrtype___gets_called():
    '''
    Simple case.  Just make sure the __clrtype__ method gets called immediately 
    after the __metaclass__ is set and we can call type.__clrtype__() from our
    __clrtype__ implementation.
    '''
    global called
    called = False
    
    class MyType(type):
        def __clrtype__(self):
            global called
            called = True
            return super(MyType, self).__clrtype__()
    
    class X(object, metaclass=MyType):
        pass

    AreEqual(called, True)
    
    
def test_sanity_override_constructors():
    '''
    Create a new CLR Type and override all of its constructors.
    '''
    AddReferenceToDlrCore()
    
    from System import Reflection
    from Microsoft.Scripting.Generation import AssemblyGen
    from System.Reflection import Emit, FieldAttributes
    from System.Reflection.Emit import OpCodes    
    gen = AssemblyGen(Reflection.AssemblyName('test'), None, '.dll', False)
    
    try:
        class MyType(type):
            def __clrtype__(self):
                baseType = super(MyType, self).__clrtype__()
                t = gen.DefinePublicType(self.__name__, baseType, True)
                
                ctors = baseType.GetConstructors()
                for ctor in ctors:            
                    builder = t.DefineConstructor(
                        Reflection.MethodAttributes.Public, 
                        Reflection.CallingConventions.Standard, 
                        tuple([p.ParameterType for p in ctor.GetParameters()])
                    )
                    ilgen = builder.GetILGenerator()
                    ilgen.Emit(OpCodes.Ldarg, 0)
                    for index in range(len(ctor.GetParameters())):
                        ilgen.Emit(OpCodes.Ldarg, index + 1)
                    ilgen.Emit(OpCodes.Call, ctor)
                    ilgen.Emit(OpCodes.Ret)
                
                newType = t.CreateType()
                return newType
        
        class X(object, metaclass=MyType):
            def __init__(self):
                self.abc = 3
              
        a = X()
        AreEqual(a.abc, 3)
        
    finally:
        #gen.SaveAssembly()
        pass    


def test_sanity_static_dot_net_type():
    '''
    Create a new static CLR Type.
    '''
    import clr
    AddReferenceToDlrCore()
    
    clr.AddReference("IronPythonTest")
    import IronPythonTest.interop.net.type.clrtype as IPT
    
    from System import Reflection
    from Microsoft.Scripting.Generation import AssemblyGen
    from System.Reflection import Emit, FieldAttributes
    from System.Reflection.Emit import OpCodes      
    gen = AssemblyGen(Reflection.AssemblyName('test'), None, '.dll', False)
    
    class MyType(type):
        def __clrtype__(self):
            baseType = super(MyType, self).__clrtype__()
            t = gen.DefinePublicType(self.__name__, baseType, True)
            ctors = baseType.GetConstructors()
            for ctor in ctors:            
                baseParams = ctor.GetParameters()
                newParams = baseParams[1:]
                builder = t.DefineConstructor(Reflection.MethodAttributes.Public, 
                                              Reflection.CallingConventions.Standard, 
                                              tuple([p.ParameterType for p in newParams])
                                              )
                fldAttrs = FieldAttributes.Static | FieldAttributes.Public
                fld = t.DefineField('$$type', type, fldAttrs)
                ilgen = builder.GetILGenerator()
                ilgen.Emit(OpCodes.Ldarg, 0)
                ilgen.Emit(OpCodes.Ldsfld, fld)
                for index in range(len(ctor.GetParameters())):
                    ilgen.Emit(OpCodes.Ldarg, index + 1)
                ilgen.Emit(OpCodes.Call, ctor)
                ilgen.Emit(OpCodes.Ret)
                # keep a ctor which takes Python types as well so we 
                # can be called from Python still.
                builder = t.DefineConstructor(Reflection.MethodAttributes.Public, 
                                              Reflection.CallingConventions.Standard, 
                                              tuple([p.ParameterType for p in ctor.GetParameters()])
                                              )
                ilgen = builder.GetILGenerator()
                ilgen.Emit(OpCodes.Ldarg, 0)
                for index in range(len(ctor.GetParameters())):
                    ilgen.Emit(OpCodes.Ldarg, index + 1)
                ilgen.Emit(OpCodes.Call, ctor)
                ilgen.Emit(OpCodes.Ret)
            newType = t.CreateType()
            newType.GetField('$$type').SetValue(None, self)
            return newType
    
    class MyCreatableDotNetType(object, metaclass=MyType):
        def __init__(self):
            self.abc = 3

    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23426
    # TODO: Test Type.GetType (requires the base class to be non-transient)
    #py_temp = MyCreatableDotNetType()
    #cs_temp = IPT.Factory.Get[MyCreatableDotNetType]()
    #AreEqual(type(py_temp), type(cs_temp))
    

###__CLRTYPE__#########################
def test_type___clrtype__():
    '''
    Tests out type.__clrtype__ directly.
    '''
    #Make sure it exists
    Assert(hasattr(type, "__clrtype__"))
    
    #Make sure the documentation is useful
    Assert("Gets the .NET type which is" in type.__clrtype__.__doc__, type.__clrtype__.__doc__)
    
    AreEqual(type.__clrtype__(type),  type)
    AreEqual(type.__clrtype__(float), float)
    AreEqual(type.__clrtype__(System.Double), float)
    AreEqual(type.__clrtype__(float), System.Double)
    Assert(not type.__clrtype__(float)==type)
    Assert(type.__clrtype__(float)!=type)



def test_clrtype_returns_existing_python_types():
    '''
    Our implementation of __clrtype__ returns pure-Python types
    instead of subclassing from type or System.Type and implementing
    __clrtype__.
    '''
    global called
    
    class PySubType1(type): pass
    class PySubType2(PySubType1): pass
    class PySubType3(PySubType2, PySubType1): pass
    
    for x in [
                bool,
                buffer,
                type(range),
                type("".index),
                type(BaseException),
                dict,
                type(Ellipsis),
                file,
                float,
                type(sys._getframe(0)),
                xrange,
                int,
                int,
                str,
                tuple,
                type(lambda: 3),
                type(None),
                type(object.__str__),
                PySubType1,
                PySubType2,
                PySubType3,
                ]:
        called = False
        
        class MyType(type):
            def __clrtype__(self):
                global called
                called = True
                return x
        
        class X(object, metaclass=MyType):
            pass
        
        AreEqual(called, True)


def test_clrtype_returns_existing_clr_types():
    '''
    Our implementation of __clrtype__ returns existing .NET types
    instead of subclassing from type or System.Type.
    '''
    global called
    clr.AddReference("System.Data")
    
    types = [
                System.Byte,
                System.Int16,
                System.UInt32,
                System.Int32,
                System.Int64,
                System.Double,
                System.Data.CommandType,
                System.Boolean,
                System.Char,
                System.Decimal,
                System.IntPtr,
                System.Object,
                System.String,
                System.Collections.BitArray,
                System.Collections.Generic.List[System.Char],
                ]

    types.append(System.Data.Common.DataAdapter)

    for x in types:
        called = False
        
        class MyType(type):
            def __clrtype__(self):
                global called
                called = True
                return x
        
        class X(object, metaclass=MyType):
            pass
        
        AreEqual(called, True)
        

###TYPE IMPLEMENTATIONS################
def test_interesting_type_implementations():
    '''
    Test types that have been fully implemented in CSharp.
    '''
    global called
    
    clr.AddReference("IronPythonTest")
    import IronPythonTest.interop.net.type.clrtype as IPT
    
    from IronPython.Runtime.Types import PythonType
    
    for x in [  IPT.Sanity,
                IPT.SanityGeneric[int],
                IPT.SanityGenericConstructor[PythonType],
                IPT.SanityDerived,
                IPT.SanityUniqueConstructor,
                IPT.SanityNoIPythonObject,
                ]:
        called = False
        
        class MyType(type):
            def __clrtype__(self):
                global called
                called = True
                return x
        
        class X(object, metaclass=MyType):
            pass
        
        AreEqual(called, True)
        
        if x!=IPT.SanityUniqueConstructor: #Related to http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23419
            temp = X()
            if x==IPT.SanityNoIPythonObject:
                AreEqual(type(temp), x)
            else:
                AreEqual(type(temp), X)
                
                
def test_type_constructor_overloads():
    '''
    A type containing multiple constructors with a PythonType as the first parameter
    should work.
    '''
    global called
    
    import clr
    clr.AddReference("IronPythonTest")
    import IronPythonTest.interop.net.type.clrtype as IPT
    
    called = False
    
    class MyType(type):
        def __clrtype__(self):
            global called
            called = True
            return IPT.SanityConstructorOverloads
    
    class X(object, metaclass=MyType):
        def __init__(self, *args, **kwargs):
            pass #print "(__init__):", args, kwargs
    
    AreEqual(called, True)
    temp = X()
    Assert(str(temp).startswith("<first"), str(temp))
    
    #Once http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23419 gets
    #fixed, we need to check that str(X(1234)).startswith("<second")


###CRITICAL SCENARIOS##################
def test_critical_custom_attributes():
    '''
    Ensure adding custom attributes to a Python class remains supported.
    '''
    global called
    global TYPE_COUNTER
    TYPE_COUNTER += 1
    
    import clr
    from System.Reflection.Emit import CustomAttributeBuilder, OpCodes
    
    clr.AddReference("Microsoft.Scripting")
    from Microsoft.Scripting.Generation import Snippets
    
    clr.AddReference("System.Xml")
    from System.Xml.Serialization import XmlRootAttribute
    
    xmlroot_clrtype = clr.GetClrType(XmlRootAttribute)
    xmlroot_ci      = xmlroot_clrtype.GetConstructor((clr.GetClrType(str),))
    XmlRootInstance = CustomAttributeBuilder(xmlroot_ci, 
                                            ("product",),
                                            (xmlroot_clrtype.GetProperty("Namespace"),),
                                            ("http://ironpython.codeplex.com",),
                                            (),
                                            ()
                                         )
    
    called = False

    class MyType(type):
        def __clrtype__(self):
            global called
            
            baseType = super(MyType, self).__clrtype__()
            typegen = Snippets.Shared.DefineType("faux type" + str(TYPE_COUNTER), baseType, True, False)
            typebld = typegen.TypeBuilder
            
            for ctor in baseType.GetConstructors(): 
                ctorparams = ctor.GetParameters()
                ctorbld = typebld.DefineConstructor(ctor.Attributes,
                                                    ctor.CallingConvention,
                                                    tuple([p.ParameterType for p in ctorparams]))
                ilgen = ctorbld.GetILGenerator()
                ilgen.Emit(OpCodes.Ldarg, 0)
                for index in range(len(ctorparams)):
                    ilgen.Emit(OpCodes.Ldarg, index + 1)
                ilgen.Emit(OpCodes.Call, ctor)
                ilgen.Emit(OpCodes.Ret)
            
            typebld.SetCustomAttribute(XmlRootInstance)
            called = True
            return typebld.CreateType()

    class X(object, metaclass=MyType):
        pass


    #Verification
    AreEqual(called, True)
    
    x = X()
    x_clrtype = clr.GetClrType(X)
    AreEqual(x_clrtype.GetCustomAttributes(XmlRootAttribute, True)[0].ElementName,
             "product")
    AreEqual(x_clrtype.GetCustomAttributes(XmlRootAttribute, True)[0].Namespace,
             "http://ironpython.codeplex.com")
    
    
def test_critical_clr_reflection():
    '''
    Can we use CLR reflection over a Python type?
    Can WPF APIs (e.g., ListBox.ItemTemplate) automatically detect Python properties?
    '''
    global called
    global TYPE_COUNTER
    
    TYPE_COUNTER += 1
    
    import clr
    
    import System
    from System.Reflection import FieldAttributes, MethodAttributes, PropertyAttributes
    from System.Reflection.Emit import CustomAttributeBuilder, OpCodes
    
    clr.AddReference("Microsoft.Scripting")
    from Microsoft.Scripting.Generation import Snippets
    
    clr.AddReference("IronPython")
    from IronPython.Runtime.Types import ReflectedField
    
    called = False

    class MyType(type):
        def __clrtype__(self):
            global called
            
            baseType = super(MyType, self).__clrtype__()
            typegen = Snippets.Shared.DefineType("faux type property" + str(TYPE_COUNTER), baseType, True, False)
            typebld = typegen.TypeBuilder
            
            for ctor in baseType.GetConstructors(): 
                ctorparams = ctor.GetParameters()
                ctorbld = typebld.DefineConstructor(ctor.Attributes,
                                                    ctor.CallingConvention,
                                                    tuple([p.ParameterType for p in ctorparams]))
                ilgen = ctorbld.GetILGenerator()
                ilgen.Emit(OpCodes.Ldarg, 0)
                for index in range(len(ctorparams)):
                    ilgen.Emit(OpCodes.Ldarg, index + 1)
                ilgen.Emit(OpCodes.Call, ctor)
                ilgen.Emit(OpCodes.Ret)
            
            #Add the property
            prop_type = clr.GetClrType(System.UInt64)
            field_builder = typebld.DefineField("NOT_SO_DYNAMIC", prop_type, FieldAttributes.Public)
            prop_method_attribs = (MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig)
            #Property getter
            prop_getter_builder = typebld.DefineMethod("get_NOT_SO_DYNAMIC", prop_method_attribs, prop_type, None)
            getilgen = prop_getter_builder.GetILGenerator()
            getilgen.Emit(OpCodes.Ldarg_0)
            getilgen.Emit(OpCodes.Ldfld, field_builder)
            getilgen.Emit(OpCodes.Ret)
            #Propery setter
            prop_setter_builder = typebld.DefineMethod("set_NOT_SO_DYNAMIC", prop_method_attribs, None, (prop_type,))
            setilgen = prop_setter_builder.GetILGenerator()
            setilgen.Emit(OpCodes.Ldarg_0)
            setilgen.Emit(OpCodes.Ldarg_1)
            setilgen.Emit(OpCodes.Stfld, field_builder)
            setilgen.Emit(OpCodes.Ret)
            #Actual property
            prop_builder = typebld.DefineProperty("NOT_SO_DYNAMIC", PropertyAttributes.None, prop_type, None)
            prop_builder.SetGetMethod(prop_getter_builder)
            prop_builder.SetSetMethod(prop_setter_builder)
            
            #Hook the C# property up to the Python version
            new_type = typebld.CreateType()
            fldinfo = new_type.GetField("NOT_SO_DYNAMIC")
            setattr(self, "NOT_SO_DYNAMIC", ReflectedField(fldinfo))
            
            called = True
            return new_type

    class X(object, metaclass=MyType):
        pass


    #Verification
    AreEqual(called, True)
    Assert(hasattr(X, "NOT_SO_DYNAMIC"))
    AreEqual(str(X.NOT_SO_DYNAMIC),
             "<field# NOT_SO_DYNAMIC on faux type property%s>" % (str(TYPE_COUNTER),))
    AreEqual(X.NOT_SO_DYNAMIC.FieldType,
             System.UInt64)
    #Simple checks
    x = X()
    AreEqual(x.NOT_SO_DYNAMIC, 0)
    x.NOT_SO_DYNAMIC = 3
    AreEqual(x.NOT_SO_DYNAMIC, 3)
    #Shouldn't be able to use non-UInt64s
    try:
        x.NOT_SO_DYNAMIC = "0"
        Fail("TypeError should have been thrown!")
    except TypeError as e:
        AreEqual(e.message,
                 "expected UInt64, got str")
    finally:
        AreEqual(x.NOT_SO_DYNAMIC, 3)
    #Make sure it's not a static property
    y = X()
    AreEqual(y.NOT_SO_DYNAMIC, 0)
    AreEqual(x.NOT_SO_DYNAMIC, 3)
    #As a .NET property
    AreEqual(x.GetType().GetProperty("NOT_SO_DYNAMIC").GetValue(x, None),
             3)
    x.GetType().GetProperty("NOT_SO_DYNAMIC").SetValue(x, System.UInt64(4), None)
    AreEqual(x.NOT_SO_DYNAMIC, 4)
    AreEqual(x.GetType().GetProperty("NOT_SO_DYNAMIC").GetValue(x, None),
             4)
    #WPF interop
    #TODO!
    

@skip("multiple_execute")    
def test_critical_parameterless_constructor():
    '''
    Ensure that CSharp can new up a Python type that has a 
    parameterless constructor.
    '''
    global called
    
    clr.AddReference("IronPythonTest")
    import IronPythonTest.interop.net.type.clrtype as IPT
    
    called = False
    
    class MyType(type):
        def __clrtype__(self):
            global called
            called = True
            return IPT.SanityParameterlessConstructor
    
    class X(object, metaclass=MyType):
        pass
    
    AreEqual(called, True)
    AreEqual(IPT.SanityParameterlessConstructor.WhichConstructor, 0)
    
    py_x = X()
    AreEqual(IPT.SanityParameterlessConstructor.WhichConstructor, 1)
    
    cs_x = IPT.Factory.Get[X]()
    AreEqual(IPT.SanityParameterlessConstructor.WhichConstructor, 2)
    
    AreEqual(type(py_x), type(cs_x))


###PYTHON OBJECT CHARACTERISTIC########
def test_clrtype_metaclass_characteristics():
    '''
    Make sure clrtype is a properly behaved Python metaclass
    '''
    class T(type):
        '''This is our own type'''
        def __clrtype__(self):
            return type.__clrtype__(self)

    class X(object, metaclass=T):
        pass
        
    AreEqual(X.__class__, T)
    AreEqual(X.__metaclass__, X.__class__)
    AreEqual(X.__class__.__base__, type)
    AreEqual(X.__class__.__bases__, (type,))
    AreEqual(X.__class__.__doc__, '''This is our own type''')
    
    x = X()
    Assert(isinstance(x, X))
    Assert(not isinstance(x, T))
    Assert(not issubclass(X, T))
    AreEqual(x.__class__, X)
    AreEqual(x.__metaclass__, T)


###NEGATIVE TEST CASES#################
def test_neg_type___clrtype__():
    '''
    Tests out negative type.__clrtype__ cases.
    '''
    #Number of params
    AssertErrorWithMessage(TypeError, "__clrtype__() takes exactly 1 argument (0 given)", 
                           type.__clrtype__)
    AssertErrorWithMessage(TypeError, "__clrtype__() takes exactly 1 argument (2 given)", 
                           type.__clrtype__, None, None)
    AssertErrorWithMessage(TypeError, "__clrtype__() takes exactly 1 argument (3 given)", 
                           type.__clrtype__, None, None, None)
    
    #Wrong param type                           
    AssertErrorWithPartialMessage(TypeError, ", got NoneType", 
                                  type.__clrtype__, None)
    AssertErrorWithPartialMessage(TypeError, ", got float", 
                                  type.__clrtype__, 3.14)
                           
    for x in [None, [], (None,), Exception("message"), 3.14, 3, 0, 5j, "string", "string",
              True, System, _os, os, exit, lambda: 3.14]:
        AssertError(TypeError, 
                    type.__clrtype__, x)

    #Shouldn't be able to set __clrtype__ to something else
    AssertErrorWithMessage(AttributeError, "attribute '__clrtype__' of 'type' object is read-only",
                           setattr, type, "__clrtype__", None)


def test_neg_clrtype_wrong_case():
    '''
    Define the __clrtype__ function using the wrong case and see what happens.
    '''
    global called
    called = False
    
    class MyType(type):
        def __clrType__(self):
            global called
            called = True
            return super(MyType, self).__clrtype__()
    
    class X(object, metaclass=MyType):
        pass

    AreEqual(called, False)


def test_neg_clrtype_wrong_params():
    '''
    Define the __clrtype__ function which has a bad method signature and see 
    what happens.
    '''
    global called
    
    #__clrtype__()
    called = False
    
    class MyType(type):
        def __clrtype__():
            global called
            called = True
            return super(MyType, self).__clrtype__()
    
    try:
        class X(object, metaclass=MyType):
            pass
        Fail("Bad __clrtype__ signature!")
       
    except TypeError as e:
        AreEqual(e.message,
                 "__clrtype__() takes no arguments (1 given)")
                 
    finally:
        AreEqual(called, False)

    #__clrtype__(not_self)
    called = False
    
    class MyType(type):
        def __clrtype__(not_self):  #Make this a function...not a method
            global called
            called = True
            return super(MyType, not_self).__clrtype__()
    
    class X(object, metaclass=MyType):
        pass
       
    AreEqual(called, True)

    #__clrtype__(self, stuff)
    called = False
    
    class MyType(type):
        def __clrtype__(self, stuff):
            global called
            called = True
            return super(MyType, self).__clrtype__()
    
    try:
        class X(object, metaclass=MyType):
            pass
        Fail("Bad __clrtype__ signature!")
       
    except TypeError as e:
        AreEqual(e.message,
                 "__clrtype__() takes exactly 2 arguments (1 given)")
                 
    finally:
        AreEqual(called, False)


def test_neg_clrtype_returns_nonsense_values():
    '''
    The __clrtype__ implementation returns invalid values.
    '''
    global called
    
    for x, expected_msg in [[[], "expected Type, got list"], 
                            [(None,), "expected Type, got tuple"], 
                            [True, "expected Type, got bool"], 
                            [False, "expected Type, got bool"], 
                            [3.14, "expected Type, got float"], 
                            ["a string", "expected Type, got str"],
                            [System.UInt16(32), "expected Type, got UInt16"],
                            [1, "expected Type, got long"],
                ]:
        called = False
        
        class MyType(type):
            def __clrtype__(self):
                global called
                called = True
                return x

        try:
            class X(object, metaclass=MyType):
                pass
            Fail("Arbitrary return values of __clrtype__ should not be allowed: " + str(x))
        except TypeError as e:
            AreEqual(e.message,
                     expected_msg)
        finally:    
            AreEqual(called, True)
        
        
    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23244
    called = False
    
    class MyType(type):
        def __clrtype__(self):
            global called
            called = True
            return None
    
    try:
        class X(object, metaclass=MyType):
            pass
        Fail("Arbitrary return values of __clrtype__ are not allowed: ", + str(x))
    except ValueError as e:
        AreEqual(e.message, "__clrtype__ must return a type, not None")
    finally:
        AreEqual(called, True)
    

def test_neg_clrtype_raises_exceptions():
    '''
    What happens when the __clrtype__ implementation raises exceptions?
    '''
    global called
    expected_msg = "my msg"

    for x in [
                IOError(expected_msg),
                BaseException(expected_msg),
                Exception(expected_msg),
                KeyboardInterrupt(expected_msg),
                System.NotSupportedException(expected_msg),
                ]:
        called = False
    
        class MyType(type):
            def __clrtype__(self):
                global called
                raise x
                called = True

        try:
            class X(object, metaclass=MyType):
                pass
            Fail("Exception was never thrown from __clrtype__: " + str(x))
        except type(x) as e:
            if (hasattr(e, "message")):
                AreEqual(e.message,
                         expected_msg)
            else: #Must be a CLR exception
                AreEqual(e.Message,
                         expected_msg)
        finally:    
            AreEqual(called, False)
    
    
def test_neg_type___new___args():
    '''
    Make a type that cannot be constructed and see if __clrtype__ still gets 
    called.
    '''
    global called
    
    called = False
    
    class MyType(type):
        def __new__(self):
            pass
        def __clrtype__(self):
            global called
            called = True
            return super(MyType, self).__clrtype__()
    
    try:
        class X(object, metaclass=MyType):
            pass
        Fail("type.__new__ signature is wrong")
    except TypeError as e:
        AreEqual(e.message,
                 "__new__() takes exactly 1 argument (4 given)")
    finally:
        AreEqual(called, False)
    

def test_neg_type_misc():
    '''
    Various scenarios in which the type returned by __clrtype__ is implemented
    purely in Csharp or VB, and is broken in some form or another.
    '''
    global called
    
    clr.AddReference("IronPythonTest")
    import IronPythonTest.interop.net.type.clrtype as IPT
    
    from IronPython.Runtime.Types import PythonType
    called = False
    
    class MyType(type):
        def __clrtype__(self):
            global called
            called = True
            return IPT.NegativeEmpty
    
    class X(object, metaclass=MyType):
        pass

    a = X()
    AreEqual(clr.GetClrType(type(a)), clr.GetClrType(IPT.NegativeEmpty))
    
    class MyType(type):
        def __clrtype__(self):
            global called
            called = True
            return IPT.NegativeNoConstructor

    class X(object, metaclass=MyType):
            pass
    
    a = X()
    AreEqual(clr.GetClrType(type(a)), clr.GetClrType(int))


#--MAIN------------------------------------------------------------------------    
run_test(__name__)
