# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# test Microsoft.Scripting.Math
#

import unittest

from iptest import IronPythonTestCase, big, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class IronMathTest(IronPythonTestCase):
    def setUp(self):
        super(IronMathTest, self).setUp()

        import clr
        from System import IFormatProvider
        class myFormatProvider(IFormatProvider):
            def ToString():pass

        self.p = myFormatProvider()

        clr.AddReference("System.Numerics")
        self.load_iron_python_test()

    def test_bigint(self):
        from System import Char, IConvertible
        from System.Numerics import BigInteger, Complex
        self.assertEqual(BigInteger.Add(big(1),99999999999999999999999999999999999999999999999999999999999) ,BigInteger.Subtract(100000000000000000000000000000000000000000000000000000000001,big(1)))
        self.assertEqual(BigInteger.Multiply(big(400),big(500)) , BigInteger.Divide(big(1000000),big(5)))
        self.assertEqual(BigInteger.Multiply(big(400),big(8)) , BigInteger.LeftShift(big(400),big(3)))
        self.assertEqual(BigInteger.Divide(big(400),big(8)) , BigInteger.RightShift(big(400),big(3)))
        self.assertEqual(BigInteger.RightShift(BigInteger.LeftShift(big(400),big(100)),big(100)) , big(400))
        self.assertEqual(BigInteger.RightShift(BigInteger.LeftShift(-12345678987654321,big(100)),big(100)) , -12345678987654321)
        self.assertRaises(ValueError, BigInteger.RightShift, big(400), -big(100))
        self.assertRaises(ValueError, BigInteger.LeftShift, big(400), -big(100))
        self.assertRaises(ValueError, BigInteger.RightShift, -12345678987654321, -big(100))
        self.assertRaises(ValueError, BigInteger.LeftShift, -12345678987654321, -big(100))
        self.assertEqual(big(-123456781234567812345678123456781234567812345678123456781234567812345678).OnesComplement().OnesComplement() , -123456781234567812345678123456781234567812345678123456781234567812345678)
        self.assertEqual(big(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678).OnesComplement() , -(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678 + big(1) ))
        self.assertTrue(BigInteger.Xor(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678,big(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678).OnesComplement()) , -big(1))
        self.assertEqual(BigInteger.BitwiseAnd(0xff00ff00,BigInteger.BitwiseOr(0x00ff00ff,0xaabbaabb)) , big(0xaa00aa00))
        self.assertEqual(BigInteger.Mod(big(-9999999999999999999999999999999999999999),1000000000000000000) , -BigInteger.Mod(9999999999999999999999999999999999999999,big(-1000000000000000000)))

        self.assertEqual(BigInteger.ToInt64(0x7fffffffffffffff) , 9223372036854775807)
        self.assertRaises(OverflowError, BigInteger.ToInt64, 0x8000000000000000)

        self.assertEqual(big(-0).ToBoolean(self.p) , False )
        self.assertEqual(big(int(-1212321.3213)).ToBoolean(self.p) , True )
        self.assertEqual(big(1212321384892342394723947).ToBoolean(self.p) , True )

        self.assertEqual(big(0).ToChar(self.p) , Char.MinValue)
        self.assertEqual(big(65).ToChar(self.p) , IConvertible.ToChar('A', self.p))
        self.assertEqual(big(0xffff).ToChar(self.p) , Char.MaxValue)
        self.assertRaises(OverflowError, big(-1).ToChar, self.p)

        self.assertEqual(big(100).ToDouble(self.p) , 100.0)
        self.assertEqual(big(100).ToSingle(self.p) , big(int(100.1213123)).ToFloat())

        self.assertTrue(big(100) != 100.32)
        self.assertEqual(big(100) , 100.0)

        self.assertTrue(100.32 != big(100))
        self.assertEqual(100.0 , big(100) )

    def test_big_to_conversion(self):
        from System import SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64
        from System.Numerics import BigInteger
        for (a, m, t,x) in [
                            (7, "ToSByte",  SByte,2),
                            (8, "ToByte",   Byte, 0),
                            (15, "ToInt16", Int16,2),
                            (16, "ToUInt16", UInt16,0),
                            (31, "ToInt32", Int32,2),
                            (32, "ToUInt32", UInt32,0),
                            (63, "ToInt64", Int64,2),
                            (64, "ToUInt64", UInt64,0)
                        ]:

            b = big(-x ** a)
            left = getattr(b, m)(self.p)
            right = t.MinValue
            self.assertEqual(left, right)

            b = big(2 ** a -1)
            left = getattr(b, m)(self.p)
            right = t.MaxValue
            self.assertEqual(left, right)

            b = big(0)
            left = getattr(b, m)(self.p)
            right = t.MaxValue - t.MaxValue
            self.assertEqual(left, right)

            self.assertRaises(OverflowError,getattr(big(2 ** a), m),self.p)
            self.assertRaises(OverflowError,getattr(big(-1 - x ** a), m),self.p)

    def test_nobig_to_conversion(self):
        from System import SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64
        type_data = [
                            (7,  SByte,  2),
                            (8,  Byte,   0),
                            (15, Int16,  2),
                            (16, UInt16, 0),
                            (31, Int32,  2),
                            (32, UInt32, 0),
                            (63, Int64,  2),
                            (64, UInt64, 0)
                    ]

        def test_extreme_values(a, t, x, wt):
            b = t(-x ** a)
            left = wt(b)
            right = t.MinValue
            self.assertEqual(left, right)

            b = t(2 ** a -1)
            left = wt(b)
            right = t.MaxValue
            self.assertEqual(left, right)

            b = t(0)
            left = wt(b)
            right = t.MaxValue - t.MaxValue
            self.assertEqual(left, right)

        signed, unsigned = 0, 1
        type_data_pairs = list(zip(type_data[signed::2], type_data[unsigned::2]))

        for cur in range(len(type_data_pairs)):
            # signed types fit in any other wider signed type
            a, t, x = type_data_pairs[cur][signed]
            for wider in range(cur, len(type_data_pairs)):
                _, wt, _ = type_data_pairs[wider][signed]
                test_extreme_values(a, t, x, wt)

            # unsigned types fit in any other wider signed/unsigned type
            a, t, x = type_data_pairs[cur][unsigned]
            for wider in range(cur, len(type_data_pairs)):
                _, wt, _ = type_data_pairs[wider][unsigned]
                test_extreme_values(a, t, x, wt)
                if wider > cur:
                    _, wt, _ = type_data_pairs[wider][signed]
                    test_extreme_values(a, t, x, wt)

    def test_complex(self):
        from System.Numerics import BigInteger, Complex
        self.assertEqual(
            Complex.Add(
                Complex(big(9999), -1234),
                Complex.Conjugate(Complex(9999, -1234)) ),
            Complex.Multiply.Overloads[complex, complex](big(9999), 2) )
        self.assertEqual(
            Complex.Add(
                Complex(99999.99e-200, 12345.88e+100),
                Complex.Negate(Complex(99999.99e-200, 12345.88e+100)) ),
            Complex.Subtract(
                Complex(99999.99e-200, 12345.88e+100),
                Complex(99999.99e-200, 12345.88e+100) ))
        self.assertEqual(
            Complex.Divide(4+2j,2),
            (2 + 1j) )
        self.assertTrue(not hasattr(Complex, "Mod"))  #IP 1.x had limited support for modulo which has been removed

    def test_bool_misc(self):
        from System.Numerics import BigInteger
        def is_zero(bigint):
            return bigint.IsZero

        self.assertEqual(big(-1234).Sign, -1)
        self.assertEqual(is_zero(big(-1234)), False)
        self.assertEqual(big(-1234).IsNegative(), True)
        self.assertEqual(big(-1234).IsPositive(), False)

        self.assertEqual(big(0).Sign, 0)
        self.assertEqual(is_zero(big(0)), True)
        self.assertEqual(big(0).IsNegative(), False)
        self.assertEqual(big(0).IsPositive(), False)

        self.assertEqual(big(1234).Sign, 1)
        self.assertEqual(is_zero(big(1234)), False)
        self.assertEqual(big(1234).IsNegative(), False)
        self.assertEqual(big(1234).IsPositive(), True)


    def test_byte_conversions(self):
        from System import Array, Byte
        from System.Numerics import BigInteger

        def CheckByteConversions(bigint, bytes):
            self.assertSequenceEqual(bigint.ToByteArray(), bytes)
            self.assertEqual(BigInteger.Create(Array[Byte](bytes)), bigint)

        CheckByteConversions(big(0x00), [0x00])

        CheckByteConversions(big(-0x01), [0xff])
        CheckByteConversions(big(-0x81), [0x7f, 0xff])
        CheckByteConversions(big(-0x100), [0x00, 0xff])
        CheckByteConversions(big(-0x1000), [0x00, 0xf0])
        CheckByteConversions(big(-0x10000), [0x00, 0x00, 0xff])
        CheckByteConversions(big(-0x100000), [0x00, 0x00, 0xf0])
        CheckByteConversions(big(-0x10000000), [0x00, 0x00, 0x00, 0xf0])
        CheckByteConversions(big(-0x100000000), [0x00, 0x00, 0x00, 0x00, 0xff])

        CheckByteConversions(big(0x7f), [0x7f])
        CheckByteConversions(big(0xff), [0xff, 0x00])
        CheckByteConversions(big(0x0201), [0x01, 0x02])
        CheckByteConversions(big(0xf2f1), [0xf1, 0xf2, 0x00])
        CheckByteConversions(big(0x03020100), [0x00, 0x01, 0x02, 0x03])
        CheckByteConversions(big(0x0403020100), [0x00, 0x01, 0x02, 0x03, 0x04])
        CheckByteConversions(big(0x0706050403020100), [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07])
        CheckByteConversions(big(0x080706050403020100), [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08])

    def test_dword_conversions(self):
        from System import Array, UInt32
        from System.Numerics import BigInteger
        from IronPythonTest import System_Scripting_Math
        def CheckDwordConversions(bigint, dwords):
            self.assertSequenceEqual(bigint.GetWords(), dwords)
            if bigint == BigInteger.Zero:
                self.assertEqual(
                    System_Scripting_Math.CreateBigInteger(
                        0,
                        Array[UInt32](dwords),),
                    bigint)
            else:
                self.assertEqual(
                    System_Scripting_Math.CreateBigInteger(
                        1,
                        Array[UInt32](dwords)),
                    bigint)
                self.assertEqual(
                    System_Scripting_Math.CreateBigInteger(
                        -1,
                        Array[UInt32](dwords)),
                    BigInteger.Negate(bigint))

        CheckDwordConversions(big(0), [0x00000000])
        CheckDwordConversions(big(1), [0x00000001])
        CheckDwordConversions(big(1<<31), [0x80000000])
        CheckDwordConversions(big(1<<31) + 9, [0x80000009])
        CheckDwordConversions(big(1<<32), [0x00000000, 0x00000001])

    def test_misc(self):
        from System import ArgumentException, ArgumentNullException
        from System.Numerics import BigInteger
        from IronPythonTest import System_Scripting_Math
        self.assertRaises(ArgumentException, System_Scripting_Math.CreateBigInteger, 0, (1, 2, 3))
        self.assertRaises(ArgumentNullException, System_Scripting_Math.CreateBigInteger, 0, None)

        self.assertEqual(big(1).CompareTo(None), 1)
        self.assertEqual(big(1).CompareTo(True), 0)

    def test_rightshiftby32_negative_bug(self):
        # test workaround for https://github.com/dotnet/runtime/issues/43396
        from System.Numerics import BigInteger
        self.assertEqual(BigInteger.Parse("-18446744073709543424") >> 32, -4294967296)
        self.assertEqual(BigInteger.Parse("-79228162514264337593543917568") >> 32, -18446744073709551616)

run_test(__name__)
