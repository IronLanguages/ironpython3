# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Test cases try to access a .NET type.
'''

import unittest
import re

from iptest import IronPythonTestCase, skipUnlessIronPython, is_netcoreapp, is_mono, run_test

keywords = ['pass', 'import', 'def', 'except', 'None', 'False']
bultins = ['abs', 'type', 'complex']
modules = ['datetime', '_collections', 'site']

@skipUnlessIronPython()
class ReachTypeTest(IronPythonTestCase):
    def setUp(self):
        super(ReachTypeTest, self).setUp()
        self.add_clr_assemblies("loadtypesample")

    def test_interesting_names_as_namespace(self):
        # import
        for x in keywords:
            self.assertRaises(SyntaxError, compile, "import %s" % x, "", "exec")

        import abs; self.assertEqual(str(abs.A), "<class 'A'>")
        import type; self.assertEqual(str(type.A), "<class 'A'>")
        import file; self.assertEqual(str(file.A), "<class 'A'>")

        import complex; self.assertEqual(str(complex.A), "<class 'A'>")
        import StandardError; self.assertEqual(str(StandardError.A), "<class 'A'>")

        # !!! no way to get clr types which have the same name as builtin modules
        import datetime; self.assertRaises(AttributeError, lambda: datetime.A)
        import _collections; self.assertRaises(AttributeError, lambda: _collections.A)

        # __import__
        for x in keywords + bultins:
            mod = __import__(x)
            self.assertEqual(str(mod.A), "<class 'A'>")

        for x in modules:
            mod = __import__(x)
            self.assertRaises(AttributeError, lambda: mod.A)

    def test_interesting_names_as_class_name(self):
        # from a import b
        for x in keywords:
            self.assertRaises(SyntaxError, compile, "from NSwInterestingClassName import %s" % x, "", "exec")

        from NSwInterestingClassName import abs; self.assertEqual(abs.A, 10)
        from NSwInterestingClassName import type; self.assertEqual(type.A, 10)
        from NSwInterestingClassName import file; self.assertEqual(file.A, 10)

        from NSwInterestingClassName import complex; self.assertEqual(complex.A, 10)
        from NSwInterestingClassName import StandardError; self.assertEqual(StandardError.A, 10)

        from NSwInterestingClassName import datetime; self.assertEqual(datetime.A, 10)
        from NSwInterestingClassName import _collections; self.assertEqual(_collections.A, 10)

        # import a
        import NSwInterestingClassName
        for x in keywords:
            self.assertRaises(SyntaxError, compile, "NSwInterestingClassName.%s" % x, "", "exec")

        for x in bultins + modules:
            x = eval("NSwInterestingClassName.%s" % x)
            self.assertEqual(x.A, 10)

    def test_nothing_public(self):
        try:
            import NothingPublic
            self.fail("Unreachable code reached")
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

        self.assertRaisesRegex(ValueError,
                            re.compile(r"(?s)The number of generic arguments provided doesn't equal the arity of the generic type definition\..*Parameter.*instantiation", re.M),
                            lambda: G3[()])

        if is_mono:
            self.assertRaisesMessage(ValueError, "Invalid generic arguments\nParameter name: typeArguments", lambda: G3[System.Exception])
        else:
            self.assertRaisesMessage(ValueError, "GenericArguments[0], 'System.Exception', on 'NSwGeneric.G3`1[T]' violates the constraint of type 'T'.", lambda: G3[System.Exception])
        self.assertEqual(G3[int].A, 50)

        self.assertRaisesMessage(SystemError, "MakeGenericType on non-generic type", lambda: G4[()])
        self.assertRaisesMessage(SystemError, "MakeGenericType on non-generic type", lambda: G4[int])

    def test_type_without_namespace(self):
        self.assertRaises(ImportError, exec, 'from PublicRefTypeWithoutNS import *')    # non static type, should fail

        g = {}; l = {}
        exec('from PublicStaticRefTypeWithoutNS import *', g, l)
        self.assertEqual(l['Nested'].A, 10)
        self.assertEqual(l['A'], 20)
        self.assertEqual(l['B'], 20)
        self.assertTrue(not 'C' in l)
        self.assertEqual(l['SM'](), 30)

        import PublicRefTypeWithoutNS
        self.assertEqual(PublicRefTypeWithoutNS.Nested.A, 10)
        self.assertEqual(PublicRefTypeWithoutNS.A, 20)
        self.assertEqual(PublicRefTypeWithoutNS.SM(), 30)

        self.assertTrue(hasattr(PublicRefTypeWithoutNS, 'B')) # instance field
        self.assertTrue(hasattr(PublicRefTypeWithoutNS, 'IM')) # instance method

        # internal type
        try:
            import InternalRefTypeWithoutNS
            self.fail("Unreachable code reached")
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
        if is_netcoreapp:
            clr.AddReference("System.Reflection.Emit")

        sr = System.Reflection
        sre = System.Reflection.Emit
        array = System.Array
        cab = array[sre.CustomAttributeBuilder]([sre.CustomAttributeBuilder(clr.GetClrType(System.Security.SecurityTransparentAttribute).GetConstructor(System.Type.EmptyTypes), array[object]([]))])
        if is_netcoreapp: # no System.AppDomain.DefineDynamicAssembly
            ab = sre.AssemblyBuilder.DefineDynamicAssembly(sr.AssemblyName("temp"), sre.AssemblyBuilderAccess.Run, cab)  # tracking: 291888
            mb = ab.DefineDynamicModule("temp")
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
    def test_type_forward2(self):
        self.add_clr_assemblies("typeforwarder2")
        g = {}; l = {}
        exec('from NSwForwardee2 import *', g, l)
        if is_netcoreapp:
            self.assertTrue('Foo_SPECIAL' in l)
        else:
            self.assertTrue('Foo_SPECIAL' not in l)      # !!!
        self.assertTrue('Bar_SPECIAL' in l)

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
            self.fail("Unreachable code reached")
        except ImportError:
            pass

        import PossibleLoadException
        self.assertEqual(PossibleLoadException.A.F, 10)
        self.assertRaises(AttributeError, lambda: PossibleLoadException.B)
        self.assertEqual(PossibleLoadException.C.F, 30)
        self.assertEqual(B, 10)

    def test_digits_in_ns8074(self):
        import NSWithDigitsCase1
        self.assertEqual(str(NSWithDigitsCase1.Z), "<class 'Z'>")
        self.assertEqual(NSWithDigitsCase1.Z.A, 10)
        self.assertEqual(str(NSWithDigitsCase1.Z0), "<class 'Z0'>")
        self.assertEqual(NSWithDigitsCase1.Z0.A, 0)

        import NSWithDigits.Case2
        self.assertEqual(str(NSWithDigits.Case2.Z), "<class 'Z'>")
        self.assertEqual(NSWithDigits.Case2.Z.A, 10)
        self.assertEqual(str(NSWithDigits.Case2.Z0), "<class 'Z0'>")
        self.assertEqual(NSWithDigits.Case2.Z0.A, 0)


run_test(__name__)
