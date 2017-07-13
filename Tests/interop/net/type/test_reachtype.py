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

import unittest

from iptest import IronPythonTestCase, is_netstandard, is_mono, run_test, skipUnlessIronPython

keywords = ['pass', 'import', 'def', 'exec', 'except']
bultin_funcs = ['abs', 'type', 'file']
bultin_types = ['complex', 'StandardError']
bultin_constants = ['None', 'False']
modules = ['__builtin__', 'datetime', '_collections', 'site']

if is_netstandard: SystemError = Exception # TODO: revert this once System.SystemException is added to netstandard (https://github.com/IronLanguages/main/issues/1399)

@skipUnlessIronPython()
class ReachTypeTest(IronPythonTestCase):
    def setUp(self):
        super(ReachTypeTest, self).setUp()
        self.add_clr_assemblies("loadtypesample")

    def test_interesting_names_as_namespace(self):
        # import
        for x in keywords + ['None']: 
            self.assertRaises(SyntaxError, compile, "import %s" % x, "", "exec")
        
        import False; self.assertEqual(str(False.A), "<type 'A'>")    
        
        import abs; self.assertEqual(str(abs.A), "<type 'A'>")
        import type; self.assertEqual(str(type.A), "<type 'A'>")
        import file; self.assertEqual(str(file.A), "<type 'A'>")
        
        import complex; self.assertEqual(str(complex.A), "<type 'A'>")
        import StandardError; self.assertEqual(str(StandardError.A), "<type 'A'>")
        
        # !!! no way to get clr types which have the same name as builtin modules
        import __builtin__; self.assertRaises(AttributeError, lambda: __builtin__.A)
        import datetime; self.assertRaises(AttributeError, lambda: datetime.A)
        import _collections; self.assertRaises(AttributeError, lambda: _collections.A)
        
        # __import__
        for x in keywords + bultin_constants + bultin_funcs + bultin_types:
            mod = __import__(x)
            self.assertEqual(str(mod.A), "<type 'A'>")
        
        for x in modules:
            mod = __import__(x)
            self.assertRaises(AttributeError, lambda: mod.A)

    def test_interesting_names_as_class_name(self):
        # from a import b
        for x in keywords: 
            self.assertRaises(SyntaxError, compile, "from NSwInterestingClassName import %s" % x, "", "exec")

        # !!! special None    
        self.assertRaises(SyntaxError, compile, "from NSwInterestingClassName import None", "", "exec")
        from NSwInterestingClassName import False; self.assertEqual(False.A, 10)
        
        from NSwInterestingClassName import abs; self.assertEqual(abs.A, 10)
        from NSwInterestingClassName import type; self.assertEqual(type.A, 10)
        from NSwInterestingClassName import file; self.assertEqual(file.A, 10)
        
        from NSwInterestingClassName import complex; self.assertEqual(complex.A, 10)
        from NSwInterestingClassName import StandardError; self.assertEqual(StandardError.A, 10)
        
        from NSwInterestingClassName import __builtin__; self.assertEqual(__builtin__.A, 10)
        from NSwInterestingClassName import datetime; self.assertEqual(datetime.A, 10)
        from NSwInterestingClassName import _collections; self.assertEqual(_collections.A, 10)
        
        # import a
        import NSwInterestingClassName
        for x in keywords: 
            self.assertRaises(SyntaxError, compile, "NSwInterestingClassName.%s" % x, "", "exec")
            
        for x in bultin_constants + bultin_funcs + bultin_types + modules:
            x = eval("NSwInterestingClassName.%s" % x)
            self.assertEqual(x.A, 10)

    def test_nothing_public(self):
        try: 
            import NothingPublic
            self.assertUnreachable()
        except ImportError:
            pass
     
    def test_generic_types(self):
        import System
        from NSwGeneric import G1, G2, G3, G4

        self.assertEqual(G1.A, 10)
        self.assertEqual(G1[int, int].A, 20)
        self.assertEqual(G1[G1, G1].A, 20)          # G1
        
        self.assertEqual(G1[()].A, 10)              # empty tuple
        self.assertEqual(G1[(int, str)].A, 20)
        self.assertRaisesMessage(ValueError, "could not find compatible generic type for 1 type arguments", lambda: G1[int].A)

        self.assertRaisesMessage(SystemError, "The operation requires a non-generic type for G2, but this represents generic types only", lambda: G2.A)
        self.assertRaisesMessage(SystemError, "The operation requires a non-generic type for G2, but this represents generic types only", lambda: G2[()])
        self.assertEqual(G2[int].A, 30)
        self.assertEqual(G2[int, int].A, 40)

        self.assertRaisesMessage(ValueError, 
                            "The number of generic arguments provided doesn't equal the arity of the generic type definition.\nParameter name: instantiation", 
                            lambda: G3[()])
        
        if is_mono and not is_netstandard:
            self.assertRaisesMessage(ValueError, "Invalid generic arguments\nParameter name: typeArguments", lambda: G3[System.Exception])
        else:
            self.assertRaisesMessage(ValueError, "GenericArguments[0], 'System.Exception', on 'NSwGeneric.G3`1[T]' violates the constraint of type 'T'.", lambda: G3[System.Exception])
        self.assertEqual(G3[int].A, 50)
        
        self.assertRaisesMessage(SystemError, "MakeGenericType on non-generic type", lambda: G4[()])
        self.assertRaisesMessage(SystemError, "MakeGenericType on non-generic type", lambda: G4[int])
    
    def test_type_without_namespace(self):
        try:
            from PublicRefTypeWithoutNS import *    # non static type, should fail
            self.assertUnreachable()
        except ImportError:
            pass
            
        from PublicStaticRefTypeWithoutNS import *
        self.assertEqual(Nested.A, 10)
        self.assertEqual(A, 20)
        self.assertEqual(B, 20)
        self.assertTrue(not 'C' in dir())
        self.assertEqual(SM(), 30)

        import PublicRefTypeWithoutNS
        self.assertEqual(PublicRefTypeWithoutNS.Nested.A, 10)
        self.assertEqual(PublicRefTypeWithoutNS.A, 20)
        self.assertEqual(PublicRefTypeWithoutNS.SM(), 30)
        
        self.assertTrue(hasattr(PublicRefTypeWithoutNS, 'B')) # instance field
        self.assertTrue(hasattr(PublicRefTypeWithoutNS, 'IM')) # instance method
        
        # internal type
        try:
            import InternalRefTypeWithoutNS
            self.assertUnreachable()
        except ImportError:
            pass

    def test_generic_type_without_namespace(self):
        import PublicValueTypeWithoutNS
        self.assertRaises(SystemError, lambda: PublicValueTypeWithoutNS.A)
        self.assertEqual(60, PublicValueTypeWithoutNS[int].A)

    def test_various_types(self):
        import NSwVarious
        self.assertEqual(dir(NSwVarious.NestedNS), ['A', 'B', 'C', 'D', 'E'])   # F should not be seen

    # snippets.dll (if saved) has the reference to temp.dll, which is not saved.
    def test_type_from_reflection_emit(self):
        import clr
        import System
        if is_netstandard:
            clr.AddReference("System.Reflection.Emit")
        
        sr = System.Reflection
        sre = System.Reflection.Emit
        array = System.Array
        cab = array[sre.CustomAttributeBuilder]([sre.CustomAttributeBuilder(clr.GetClrType(System.Security.SecurityTransparentAttribute).GetConstructor(System.Type.EmptyTypes), array[object]([]))])
        if is_netstandard: # no System.AppDomain in netstandard
            ab = sre.AssemblyBuilder.DefineDynamicAssembly(sr.AssemblyName("temp"), sre.AssemblyBuilderAccess.Run, cab)  # tracking: 291888
        else:
            ab = System.AppDomain.CurrentDomain.DefineDynamicAssembly(sr.AssemblyName("temp"), sre.AssemblyBuilderAccess.RunAndSave, "temp", None, None, None, None, True, cab)  # tracking: 291888

        mb = ab.DefineDynamicModule("temp", "temp.dll")
        tb = mb.DefineType("EmittedNS.EmittedType", sr.TypeAttributes.Public)
        tb.CreateType()
            
        clr.AddReference(ab)
        import EmittedNS
        EmittedNS.EmittedType()
    
    def test_type_forward1(self):
        self.add_clr_assemblies("typeforwarder1")
        from NSwForwardee1 import Foo, Bar        #!!!
        self.assertEqual(Foo.A, 120)
        self.assertEqual(Bar.A, -120)
        
        import NSwForwardee1
        self.assertEqual(NSwForwardee1.Foo.A, 120)
        self.assertEqual(NSwForwardee1.Bar.A, -120)
    
    #@skip("multiple_execute")
    @unittest.skipIf(is_mono, 'https://github.com/IronLanguages/main/issues/1439')
    def test_type_forward2(self):
        self.add_clr_assemblies("typeforwarder2")
        from NSwForwardee2 import *      
        self.assertTrue('Foo_SPECIAL' not in dir())      # !!!
        self.assertTrue('Bar_SPECIAL' in dir())
        
        import NSwForwardee2
        self.assertEqual(NSwForwardee2.Foo_SPECIAL.A, 620)
        self.assertEqual(NSwForwardee2.Bar_SPECIAL.A, 64)
    
    def test_type_forward3(self):
        self.add_clr_assemblies("typeforwarder3")
        #import NSwForwardee3                   # TRACKING BUG: 291692
        #self.assertEqual(NSwForwardee3.Foo.A, 210)
    
    @unittest.skipIf(is_mono, 'https://github.com/IronLanguages/main/issues/1440')
    def test_type_causing_load_exception(self):
        self.add_clr_assemblies("loadexception")
        from PossibleLoadException import A, C
        self.assertEqual(A.F, 10)
        self.assertEqual(C.F, 30)

        B = 10    
        try:
            from PossibleLoadException import B
            self.assertUnreachable()
        except ImportError:
            pass

        import PossibleLoadException
        self.assertEqual(PossibleLoadException.A.F, 10)
        self.assertRaises(AttributeError, lambda: PossibleLoadException.B)
        self.assertEqual(PossibleLoadException.C.F, 30)
        self.assertEqual(B, 10)

    def test_digits_in_ns8074(self):
        import NSWithDigitsCase1
        self.assertEqual(str(NSWithDigitsCase1.Z), "<type 'Z'>")
        self.assertEqual(NSWithDigitsCase1.Z.A, 10)
        self.assertEqual(str(NSWithDigitsCase1.Z0), "<type 'Z0'>")
        self.assertEqual(NSWithDigitsCase1.Z0.A, 0)
        
        import NSWithDigits.Case2
        self.assertEqual(str(NSWithDigits.Case2.Z), "<type 'Z'>")
        self.assertEqual(NSWithDigits.Case2.Z.A, 10)
        self.assertEqual(str(NSWithDigits.Case2.Z0), "<type 'Z0'>")
        self.assertEqual(NSWithDigits.Case2.Z0.A, 0)


run_test(__name__)

