# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
See how IronPython treats implicit coversion.
'''
#------------------------------------------------------------------------------

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class OpImplicitTest(IronPythonTestCase):
    def setUp(self):
        super(OpImplicitTest, self).setUp()
        self.add_clr_assemblies("userdefinedconversions", "typesamples")

    def test_pass_in_wrapper(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import BooleanWrapperClass, BooleanWrapperStruct, ByteWrapperClass, ByteWrapperStruct, CharWrapperClass, CharWrapperStruct, \
            Consumer, DoubleWrapperClass, DoubleWrapperStruct, DecimalWrapperClass, DecimalWrapperStruct, EnumWrapperClass, EnumWrapperStruct, \
            Int16WrapperClass, Int16WrapperStruct, Int32WrapperClass, Int32WrapperStruct, Int64WrapperClass, Int64WrapperStruct, SByteWrapperClass, SByteWrapperStruct, \
            SingleWrapperClass, SingleWrapperStruct, StringWrapperClass, StringWrapperStruct, UInt16WrapperClass, UInt16WrapperStruct, UInt32WrapperClass, UInt32WrapperStruct, \
            UInt64WrapperClass, UInt64WrapperStruct
        from Merlin.Testing.TypeSample import EnumInt16
        for (call, type1, type2, value, flag) in [ 
                (Consumer.EatByte, ByteWrapperClass, ByteWrapperStruct, 1, 500),
                (Consumer.EatSByte, SByteWrapperClass, SByteWrapperStruct, 11, 510),
                (Consumer.EatUInt16, UInt16WrapperClass, UInt16WrapperStruct, 21, 520),
                (Consumer.EatInt16, Int16WrapperClass, Int16WrapperStruct, 31, 530),
                (Consumer.EatUInt32, UInt32WrapperClass, UInt32WrapperStruct, 41, 540),
                (Consumer.EatInt32, Int32WrapperClass, Int32WrapperStruct, 51, 550),
                (Consumer.EatUInt64, UInt64WrapperClass, UInt64WrapperStruct, 61, 560),
                (Consumer.EatInt64, Int64WrapperClass, Int64WrapperStruct, 71, 570),
                (Consumer.EatDouble, DoubleWrapperClass, DoubleWrapperStruct, 81, 580),
                (Consumer.EatSingle, SingleWrapperClass, SingleWrapperStruct, 91, 590),
                (Consumer.EatDecimal, DecimalWrapperClass, DecimalWrapperStruct, 101, 600),
                (Consumer.EatChar, CharWrapperClass, CharWrapperStruct, 'a', 610),
                (Consumer.EatBoolean, BooleanWrapperClass, BooleanWrapperStruct, True, 620),
                (Consumer.EatString, StringWrapperClass, StringWrapperStruct, 'python', 630),
                (Consumer.EatEnum, EnumWrapperClass, EnumWrapperStruct, EnumInt16.B, 640),
            ]:
            x = type1(value)
            self.assertEqual(call(x), value)
            Flag.Check(flag)
            
            x = type2(value)
            self.assertEqual(call(x), value)
            Flag.Check(flag)

    def test_pass_in_value(self):
        import System
        from Merlin.Testing import Flag
        from Merlin.Testing.TypeSample import EnumInt16
        from Merlin.Testing.Call import Consumer
        for (call1, call2, value, flag) in [
                (Consumer.EatByteClass, Consumer.EatByteStruct, System.Byte.Parse("2"), 100),
                (Consumer.EatSByteClass, Consumer.EatSByteStruct, System.SByte.Parse("12"), 110),
                (Consumer.EatUInt16Class, Consumer.EatUInt16Struct, System.UInt16.Parse("22"), 120),
                (Consumer.EatInt16Class, Consumer.EatInt16Struct, System.Int16.Parse("32"), 130),
                (Consumer.EatUInt32Class, Consumer.EatUInt32Struct, System.UInt32.Parse("42"), 140),
                (Consumer.EatInt32Class, Consumer.EatInt32Struct, System.Int32.Parse("52"), 150),
                (Consumer.EatUInt64Class, Consumer.EatUInt64Struct, System.UInt64.Parse("62"), 160),
                (Consumer.EatInt64Class, Consumer.EatInt64Struct, System.Int64.Parse("72"), 170),
                (Consumer.EatDoubleClass, Consumer.EatDoubleStruct, System.Double.Parse("82"), 180),
                (Consumer.EatSingleClass, Consumer.EatSingleStruct, System.Single.Parse("92"), 190),
                (Consumer.EatDecimalClass, Consumer.EatDecimalStruct, System.Decimal.Parse("102"), 200),
                (Consumer.EatCharClass, Consumer.EatCharStruct, System.Char.Parse("b"), 210),
                (Consumer.EatBooleanClass, Consumer.EatBooleanStruct, False, 220),
                (Consumer.EatStringClass, Consumer.EatStringStruct, "nohtyp", 230),
                (Consumer.EatEnumClass, Consumer.EatEnumStruct, EnumInt16.C, 240),
            ]:
            self.assertEqual(call1(value), value)
            Flag.Check(flag)
            self.assertEqual(call2(value), value)
            Flag.Check(flag)
        
    def test_class_struct(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import Consumer, ClassOne, ClassTwo, MixedClass1, MixedClass2, MixedStruct1, MixedStruct2, StructOne, StructTwo
        for (call, t, value, flag) in [
                (Consumer.EatClassOne, ClassTwo, 1, 100),
                (Consumer.EatClassTwo, ClassOne, 2, 110),
                (Consumer.EatStructOne, StructTwo, 3, 120),
                (Consumer.EatStructTwo, StructOne, 4, 130),
                (Consumer.EatMixedClass1, MixedStruct1, 5, 140),
                (Consumer.EatMixedStruct1, MixedClass1, 6, 150),
                (Consumer.EatMixedClass2, MixedStruct2, 7, 160),
                (Consumer.EatMixedStruct2, MixedClass2, 8, 170),
            ]:
            x = t(value)
            self.assertEqual(call(x), value)
            Flag.Check(flag)

    def test_generic(self):
        from System import Int32
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import Consumer, G1, G2, G3, GInt
        a = G1[Int32](5)
        b = GInt(6)
        c = G2[Int32](7) 
        d = G3[Int32, str](8, 'nine')
        e = G3[Int32, Int32](10, 11)
        
        f = Consumer.EatG1OfInt
        self.assertEqual(f(a), 5); Flag.Check(180)
        self.assertEqual(f(b), 6); Flag.Check(180)
        self.assertEqual(f(c), 7); Flag.Check(180)
        self.assertEqual(f(d), 8); Flag.Check(180)
        self.assertEqual(f(e), 10); Flag.Check(180)
        
        f = Consumer.EatGInt
        self.assertEqual(f(a), 5); Flag.Check(190)
        self.assertEqual(f(b), 6); Flag.Check(190)
        for x in [c, d, e]: self.assertRaises(TypeError, f, x)
        
        f = Consumer.EatG2OfInt
        self.assertEqual(f(a), 5); Flag.Check(200)
        self.assertEqual(f(c), 7); Flag.Check(200)
        for x in [b, d, e]: self.assertRaises(TypeError, f, x)
        
        f = Consumer.EatG3OfIntInt
        self.assertEqual(f(a), 5); Flag.Check(210)
        self.assertEqual(f(e), 10); Flag.Check(210)
        for x in [b, c, d]: self.assertRaises(TypeError, f, x)

    def test_selection(self):
        import System
        from Merlin.Testing.Call import Consumer
        from Merlin.Testing.TypeSample import EnumInt16, SimpleStruct
        f = Consumer.EatOmniTarget
        self.assertEqual(f(3.14), 10)
        self.assertEqual(f(314), 20)
        self.assertEqual(f(EnumInt16.A), 30)
        self.assertEqual(f(SimpleStruct(3)), 40)
        
        for x in [System.Byte.Parse("7"), True]:  self.assertRaises(TypeError, f, x)

    # http://msdn2.microsoft.com/en-us/library/aa691302(VS.71).aspx
    def test_derivation(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import Consumer, S1, SBase1, SDerived1
        a, b, c = SBase1(), S1(), SDerived1()
        
        f = Consumer.EatTBase1
        #for x in [b, c]: f(x); Flag.Check(701) # bug: 314599
        self.assertRaises(TypeError, f, a)
        
        f = Consumer.EatT1
        for x in [b, c]: f(x); Flag.Check(702)
        self.assertRaises(TypeError, f, a)
        
        f = Consumer.EatTDerived1
        for x in [a, b]: self.assertRaises(TypeError, f, x)
        
        # TODO: more 

    # http://msdn2.microsoft.com/en-us/library/aa691284(VS.71).aspx
    def test_implicit_reference_conversions(self):
        import System
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import AnyStruct, AnyReference, ClassBase, ClassDerived, Consumer, \
            First, Int32WrapperClass, Int32WrapperStruct, Second, Source1, StructBase, StructDerived, Third
        from Merlin.Testing.TypeSample import VoidVoidDelegate, Int32Int32Delegate

        for x in [AnyStruct(), AnyReference(), None]:  # boxing conversion for AnyStruct
            Consumer.EatObject(x); Flag.Check(801)
        
        for x in [First(), Second(), Third(), ClassDerived(), None]: 
            Consumer.EatFirst(x); Flag.Check(808)
        
        for x in [ StructBase(), ClassBase(), StructDerived(), ClassDerived() ]:  # boxing conversion for StructXXX
            Consumer.EatIBase(x); Flag.Check(802)
        
        a = System.Array[Source1]([Source1(), Source1()])
        self.assertRaises(TypeError, Consumer.EatTarget1Array, a)
        self.assertRaises(TypeError, Consumer.EatTarget2Array, a)

        c = System.Array[int]([1, 2, 3])
        d = System.Array[Int32WrapperClass]([Int32WrapperClass(1),])
        e = System.Array[Int32WrapperStruct]([Int32WrapperStruct(2),])
        self.assertRaisesMessage(TypeError, "expected Array[Int32WrapperClass], got Array[int]", 
                            Consumer.EatInt32WrapperClassArray, c)
        self.assertRaisesMessage(TypeError, "expected Array[Int32WrapperStruct], got Array[int]", 
                            Consumer.EatInt32WrapperStructArray, c)
        for x in [d, e]:
            self.assertRaises(TypeError, Consumer.EatInt32Array, x)
        
        f = System.Array[Second]([Second(), Second()])
        g = System.Array[Third]([Third(), ])
        for x in [f, g]:
            Consumer.EatFirstArray(f); Flag.Check(815)
            
        for x in [None, a, c, d, e, f, g]:
            Consumer.EatArray(x); Flag.Check(806)
        
        for x in [VoidVoidDelegate(Consumer.MVoidVoid), Int32Int32Delegate(Consumer.MInt32Int32)]:
            Consumer.EatDelegate(x); Flag.Check(807)
        
        a = AnyStruct()
        Consumer.EatAnyStruct(a); Flag.Check(850)
        self.assertRaisesMessage(TypeError, 'expected AnyStruct, got NoneType', Consumer.EatAnyStruct, None)
        
        for x in [a, None]:
            Consumer.EatNullableAnyStruct(a); Flag.Check(851)
    

    # http://msdn2.microsoft.com/en-us/library/aa691158(VS.71).aspx
    def test_boxing_conversion(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import AnyStruct, Consumer, StructBase
        from Merlin.Testing.TypeSample import EnumInt16, EnumUInt32
        for x in [ AnyStruct(), EnumInt16.B, None, StructBase()]:
            Consumer.EatValueType(x)
            Flag.Check(809)
        
        for x in [EnumInt16.A, EnumUInt32.C, None]:
            Consumer.EatEnumType(x)
            Flag.Check(810)

    # http://msdn2.microsoft.com/en-us/library/aa691283(vs.71).aspx
    def test_implicit_enum_conversion(self):
        import System
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import Consumer
        Consumer.EatEnum(System.Int16(0)); Flag.Check(640)  # tracking as 316744
        self.assertRaises(TypeError, Consumer.EatEnum, 1)

    
run_test(__name__)

