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
Test cases try to access a .NET type.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("loadtypesample")

keywords = ['pass', 'import', 'def', 'exec', 'except']
bultin_funcs = ['abs', 'type', 'file']
bultin_types = ['complex', 'StandardError']
bultin_constants = ['None', 'False']
modules = ['__builtin__', 'datetime', '_collections', 'site']

def test_interesting_names_as_namespace():
    # import
    for x in keywords + ['None']: 
        AssertError(SyntaxError, compile, "import %s" % x, "", "exec")
    
    import False; AreEqual(str(False.A), "<type 'A'>")    
    
    import abs; AreEqual(str(abs.A), "<type 'A'>")
    import type; AreEqual(str(type.A), "<type 'A'>")
    import file; AreEqual(str(file.A), "<type 'A'>")
    
    import complex; AreEqual(str(complex.A), "<type 'A'>")
    import Exception; AreEqual(str(Exception.A), "<type 'A'>")
    
    # !!! no way to get clr types which have the same name as builtin modules
    import builtins; AssertError(AttributeError, lambda: __builtin__.A)
    import datetime; AssertError(AttributeError, lambda: datetime.A)
    import _collections; AssertError(AttributeError, lambda: _collections.A)
    
    # __import__
    for x in keywords + bultin_constants + bultin_funcs + bultin_types:
        mod = __import__(x)
        AreEqual(str(mod.A), "<type 'A'>")
    
    for x in modules:
        mod = __import__(x)
        AssertError(AttributeError, lambda: mod.A)

def test_interesting_names_as_class_name():
    # from a import b
    for x in keywords: 
        AssertError(SyntaxError, compile, "from NSwInterestingClassName import %s" % x, "", "exec")

    # !!! special None    
    AssertError(SyntaxError, compile, "from NSwInterestingClassName import None", "", "exec")
    from NSwInterestingClassName import False; AreEqual(False.A, 10)
    
    from NSwInterestingClassName import abs; AreEqual(abs.A, 10)
    from NSwInterestingClassName import type; AreEqual(type.A, 10)
    from NSwInterestingClassName import file; AreEqual(file.A, 10)
    
    from NSwInterestingClassName import complex; AreEqual(complex.A, 10)
    from NSwInterestingClassName import Exception; AreEqual(Exception.A, 10)
    
    from NSwInterestingClassName import __builtin__; AreEqual(builtins.A, 10)
    from NSwInterestingClassName import datetime; AreEqual(datetime.A, 10)
    from NSwInterestingClassName import _collections; AreEqual(_collections.A, 10)
    
    # import a
    import NSwInterestingClassName
    for x in keywords: 
        AssertError(SyntaxError, compile, "NSwInterestingClassName.%s" % x, "", "exec")
        
    for x in bultin_constants + bultin_funcs + bultin_types + modules:
        x = eval("NSwInterestingClassName.%s" % x)
        AreEqual(x.A, 10)

def test_nothing_public():
    try: 
        import NothingPublic
        AssertUnreachable()
    except ImportError:
        pass
     
def test_generic_types():
    from NSwGeneric import G1, G2, G3, G4

    AreEqual(G1.A, 10)
    AreEqual(G1[int, int].A, 20)
    AreEqual(G1[G1, G1].A, 20)          # G1
    
    AreEqual(G1[()].A, 10)              # empty tuple
    AreEqual(G1[(int, str)].A, 20)
    AssertErrorWithMessage(ValueError, "could not find compatible generic type for 1 type arguments", lambda: G1[int].A)

    AssertErrorWithMessage(SystemError, "The operation requires a non-generic type for G2, but this represents generic types only", lambda: G2.A)
    AssertErrorWithMessage(SystemError, "The operation requires a non-generic type for G2, but this represents generic types only", lambda: G2[()])
    AreEqual(G2[int].A, 30)
    AreEqual(G2[int, int].A, 40)

    if not is_net40:
        AssertErrorWithMessage(ValueError, 
                               "The type or method has 1 generic parameter(s), but 0 generic argument(s) were provided. A generic argument must be provided for each generic parameter.", lambda: G3[()])
    else: #.NET changed the error message with .NET 4.0
        AssertErrorWithMessage(ValueError, 
                               "The number of generic arguments provided doesn't equal the arity of the generic type definition.\nParameter name: instantiation", 
                               lambda: G3[()])
    
    if is_posix:
        AssertErrorWithMessage(ValueError, "Invalid generic arguments\nParameter name: typeArguments", lambda: G3[System.Exception])
    else:
        AssertErrorWithMessage(ValueError, "GenericArguments[0], 'System.Exception', on 'NSwGeneric.G3`1[T]' violates the constraint of type 'T'.", lambda: G3[System.Exception])
    AreEqual(G3[int].A, 50)
    
    AssertErrorWithMessage(SystemError, "MakeGenericType on non-generic type", lambda: G4[()])
    AssertErrorWithMessage(SystemError, "MakeGenericType on non-generic type", lambda: G4[int])
    
def test_type_without_namespace():
    try:
        from PublicRefTypeWithoutNS import *    # non static type, should fail
        AssertUnreachable()
    except ImportError:
        pass
        
    from PublicStaticRefTypeWithoutNS import *
    AreEqual(Nested.A, 10)
    AreEqual(A, 20)
    AreEqual(B, 20)
    Assert(not 'C' in dir())
    AreEqual(SM(), 30)

    import PublicRefTypeWithoutNS
    AreEqual(PublicRefTypeWithoutNS.Nested.A, 10)
    AreEqual(PublicRefTypeWithoutNS.A, 20)
    AreEqual(PublicRefTypeWithoutNS.SM(), 30)
    
    Assert(hasattr(PublicRefTypeWithoutNS, 'B')) # instance field
    Assert(hasattr(PublicRefTypeWithoutNS, 'IM')) # instance method
    
    # internal type
    try:
        import InternalRefTypeWithoutNS
        AssertUnreachable()
    except ImportError:
        pass

def test_generic_type_without_namespace():
    import PublicValueTypeWithoutNS
    AssertError(SystemError, lambda: PublicValueTypeWithoutNS.A)
    AreEqual(60, PublicValueTypeWithoutNS[int].A)

def test_various_types():
    import NSwVarious
    AreEqual(dir(NSwVarious.NestedNS), ['A', 'B', 'C', 'D', 'E'])   # F should not be seen

import System
if '-X:SaveAssemblies' not in System.Environment.GetCommandLineArgs():
    # snippets.dll (if saved) has the reference to temp.dll, which is not saved.
    @runonly("orcas")
    def test_type_from_reflection_emit():
        sr = System.Reflection
        sre = System.Reflection.Emit
        array = System.Array
        cab = array[sre.CustomAttributeBuilder]([sre.CustomAttributeBuilder(clr.GetClrType(System.Security.SecurityTransparentAttribute).GetConstructor(System.Type.EmptyTypes), array[object]([]))])
        ab = System.AppDomain.CurrentDomain.DefineDynamicAssembly(sr.AssemblyName("temp"), sre.AssemblyBuilderAccess.RunAndSave, "temp", None, None, None, None, True, cab)  # tracking: 291888

        mb = ab.DefineDynamicModule("temp", "temp.dll")
        tb = mb.DefineType("EmittedNS.EmittedType", sr.TypeAttributes.Public)
        tb.CreateType()
            
        clr.AddReference(ab)
        import EmittedNS
        EmittedNS.EmittedType()
    
def test_type_forward1():
    add_clr_assemblies("typeforwarder1")
    from NSwForwardee1 import Foo, Bar        #!!!
    AreEqual(Foo.A, 120)
    AreEqual(Bar.A, -120)
    
    import NSwForwardee1
    AreEqual(NSwForwardee1.Foo.A, 120)
    AreEqual(NSwForwardee1.Bar.A, -120)

@skip("multiple_execute", "posix")
def test_type_forward2():    
    add_clr_assemblies("typeforwarder2")
    from NSwForwardee2 import *      
    Assert('Foo_SPECIAL' not in dir())      # !!!
    Assert('Bar_SPECIAL' in dir())
    
    import NSwForwardee2
    AreEqual(NSwForwardee2.Foo_SPECIAL.A, 620)
    AreEqual(NSwForwardee2.Bar_SPECIAL.A, 64)
    
def test_type_forward3():    
    add_clr_assemblies("typeforwarder3")
    #import NSwForwardee3                   # TRACKING BUG: 291692
    #AreEqual(NSwForwardee3.Foo.A, 210)
    
@skip("posix")
def test_type_causing_load_exception():
    add_clr_assemblies("loadexception")
    from PossibleLoadException import A, C
    AreEqual(A.F, 10)
    AreEqual(C.F, 30)

    B = 10    
    try:
        from PossibleLoadException import B
        AssertUnreachable()
    except ImportError:
        pass

    import PossibleLoadException
    AreEqual(PossibleLoadException.A.F, 10)
    AssertError(AttributeError, lambda: PossibleLoadException.B)
    AreEqual(PossibleLoadException.C.F, 30)
    AreEqual(B, 10)

def test_digits_in_ns8074():
    import NSWithDigitsCase1
    AreEqual(str(NSWithDigitsCase1.Z), "<type 'Z'>")
    AreEqual(NSWithDigitsCase1.Z.A, 10)
    AreEqual(str(NSWithDigitsCase1.Z0), "<type 'Z0'>")
    AreEqual(NSWithDigitsCase1.Z0.A, 0)
    
    import NSWithDigits.Case2
    AreEqual(str(NSWithDigits.Case2.Z), "<type 'Z'>")
    AreEqual(NSWithDigits.Case2.Z.A, 10)
    AreEqual(str(NSWithDigits.Case2.Z0), "<type 'Z0'>")
    AreEqual(NSWithDigits.Case2.Z0.A, 0)


run_test(__name__)

