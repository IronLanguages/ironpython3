# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# test Microsoft.Scripting.Math
#

import unittest

from iptest import IronPythonTestCase, big, is_mono, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class IronMathTest(IronPythonTestCase):
    def setUp(self):
        super(IronMathTest, self).setUp()

        import clr, System
        from System import IFormatProvider
        class myFormatProvider(IFormatProvider):
            def ToString():
                pass
            def GetFormat(self, formatType):
                return System.Globalization.NumberFormatInfo.InvariantInfo

        self.p = myFormatProvider()

        clr.AddReference("System.Numerics")
        self.load_iron_python_test()

    def test_bigint(self):
        from System import Int64, Boolean, Char, Double, Single, IConvertible
        from System.Numerics import BigInteger
        self.assertEqual(BigInteger.Add(big(1),99999999999999999999999999999999999999999999999999999999999) ,BigInteger.Subtract(100000000000000000000000000000000000000000000000000000000001,big(1)))
        self.assertEqual(BigInteger.Multiply(big(400),big(500)) , BigInteger.Divide(big(1000000),big(5)))
        self.assertEqual(BigInteger.Multiply(big(400),big(8)) , big(400) <<big(3))
        self.assertEqual(BigInteger.Divide(big(400),big(8)) , big(400) >>big(3))
        self.assertEqual((big(400) << big(100)) >> big(100) , big(400))
        self.assertEqual((-12345678987654321 << big(100)) >>big(100) , -12345678987654321)
        self.assertRaises(ValueError, lambda x, y: x >> y, big(400), -big(100))
        self.assertRaises(ValueError, lambda x, y: x << y, big(400), -big(100))
        self.assertRaises(ValueError, lambda x, y: x >> y, -12345678987654321, -big(100))
        self.assertRaises(ValueError, lambda x, y: x << y, -12345678987654321, -big(100))
        self.assertEqual(~(~big(-123456781234567812345678123456781234567812345678123456781234567812345678)) , -123456781234567812345678123456781234567812345678123456781234567812345678)
        self.assertEqual(~big(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678) , -(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678 + big(1) ))
        self.assertTrue(big(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678) ^ (~big(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678)) , -big(1))
        self.assertEqual(big(0xff00ff00) & (big(0x00ff00ff) | big(0xaabbaabb)) , big(0xaa00aa00))
        self.assertEqual(big(-9999999999999999999999999999999999999999) % 1000000000000000000 , -(9999999999999999999999999999999999999999 % big(-1000000000000000000)))

        self.assertEqual(Int64(big(0x7fffffffffffffff)) , 9223372036854775807)
        self.assertRaises(OverflowError, Int64, big(0x8000000000000000))

        self.assertEqual(Boolean(big(-0)) , False )
        self.assertEqual(Boolean(big(int(-1212321.3213))) , True )
        self.assertEqual(Boolean(big(1212321384892342394723947)) , True )

        self.assertEqual(Char(big(0)) , Char.MinValue)
        self.assertEqual(Char(big(65)) , IConvertible.ToChar('A', self.p))
        self.assertEqual(Char(big(0xffff)) , Char.MaxValue)
        self.assertRaises(OverflowError, Char, big(-1))

        self.assertEqual(Double(big(100)) , 100.0)
        self.assertEqual(Single(big(100)) , 100.0)
        self.assertEqual(Single(big(100)) , IConvertible.ToSingle(int(100.1213123), self.p))

        self.assertTrue(big(100) != 100.32)
        self.assertEqual(big(100) , 100.0)

        self.assertTrue(100.32 != big(100))
        self.assertEqual(100.0 , big(100) )

    def test_big_to_conversion(self):
        from System import SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Char
        for (a, t, x) in [
                            (7,  SByte,  2),
                            (8,  Byte,   0),
                            (15, Int16,  2),
                            (16, UInt16, 0),
                            (16, Char,   0),
                            (31, Int32,  2),
                            (32, UInt32, 0),
                            (63, Int64,  2),
                            (64, UInt64, 0)
                        ]:

            b = big(-x ** a)
            left = t(b)
            right = t.MinValue
            self.assertEqual(left, right)

            b = big(2 ** a -1)
            left = t(b)
            right = t.MaxValue
            self.assertEqual(left, right)

            if t is not Char:
                b = big(0)
                left = t(b)
                right = t.MaxValue - t.MaxValue
                self.assertEqual(left, right)

            self.assertRaises(OverflowError, t, big(2 ** a))
            self.assertRaises(OverflowError, t, big(-1 - x ** a))

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
        from System.Numerics import Complex
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
        self.assertEqual(big(-1234) < 0, True)
        self.assertEqual(big(-1234) > 0, False)

        self.assertEqual(big(0).Sign, 0)
        self.assertEqual(is_zero(big(0)), True)
        self.assertEqual(big(0) < 0, False)
        self.assertEqual(big(0) > 0, False)

        self.assertEqual(big(1234).Sign, 1)
        self.assertEqual(is_zero(big(1234)), False)
        self.assertEqual(big(1234) < 0, False)
        self.assertEqual(big(1234) > 0, True)


    def test_byte_conversions(self):
        def CheckByteConversions(bigint, bytes_list):
            self.assertSequenceEqual(bigint.ToByteArray(), bytes_list)
            self.assertSequenceEqual(bigint.to_bytes(len(bytes_list), 'little', signed=True), bytes_list)
            self.assertEqual(int.from_bytes(bytes(bytes_list), 'little', signed=True), bigint)

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
        import clr
        from System import Array, UInt32
        from System.Numerics import BigInteger
        from IronPythonTest import System_Scripting_Math
        import Microsoft.Scripting.Utils.MathUtils
        clr.ImportExtensions(Microsoft.Scripting.Utils.MathUtils)

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

    def test_to_type_conversions(self):
        from System import Decimal, Double, Single
        from System import Int64, UInt64, Int32, UInt32, Int16, UInt16, Byte, SByte
        from System import Boolean, Char, DateTime, Object, Enum, DateTimeKind

        val = 1
        for i in  [big(val), Int32(val)]:
            self.assertEqual(i.ToDecimal(self.p), val)
            self.assertIsInstance(i.ToDecimal(self.p), Decimal)
            self.assertEqual(i.ToDouble(self.p), val)
            self.assertIsInstance(i.ToDouble(self.p), Double)
            self.assertEqual(i.ToSingle(self.p), val)
            self.assertIsInstance(i.ToSingle(self.p), Single)
            self.assertEqual(i.ToInt64(self.p), val)
            self.assertIsInstance(i.ToInt64(self.p), Int64)
            self.assertEqual(i.ToUInt64(self.p), val)
            self.assertIsInstance(i.ToUInt64(self.p), UInt64)
            self.assertEqual(i.ToInt32(self.p), val)
            self.assertIsInstance(i.ToInt32(self.p), Int32)
            self.assertEqual(i.ToUInt32(self.p), val)
            self.assertIsInstance(i.ToUInt32(self.p), UInt32)
            self.assertEqual(i.ToInt16(self.p), val)
            self.assertIsInstance(i.ToInt16(self.p), Int16)
            self.assertEqual(i.ToUInt16(self.p), val)
            self.assertIsInstance(i.ToUInt16(self.p), UInt16)
            self.assertEqual(i.ToByte(self.p), val)
            self.assertIsInstance(i.ToByte(self.p), Byte)
            self.assertEqual(i.ToSByte(self.p), val)
            self.assertIsInstance(i.ToSByte(self.p), SByte)
            self.assertEqual(i.ToBoolean(self.p), val)
            self.assertIsInstance(i.ToBoolean(self.p), Boolean)
            self.assertEqual(i.ToChar(self.p), Char(val))
            self.assertEqual(i.ToString(self.p), str(val))
            self.assertRaisesRegex(TypeError, r"Invalid cast from '\w+' to 'DateTime'", i.ToDateTime, self.p)

            for t in [Decimal, Double, Single, Int64, UInt64, Int32, UInt32, Int16, UInt16, Byte, SByte, Boolean, Char, str]:
                self.assertEqual(i.ToType(t, self.p), t(i))

            self.assertEqual(i.ToType(Object, self.p), i)
            self.assertIsInstance(i.ToType(Object, self.p), Object)
            self.assertRaisesRegex(TypeError, r"Invalid cast from '\w+' to 'DateTime'", i.ToType, DateTime, self.p)
            self.assertRaisesRegex(TypeError, r"Invalid cast from '[\w.]+' to 'System.DateTimeKind'\.", i.ToType, DateTimeKind, self.p)
            if not is_mono:
                self.assertRaisesRegex(TypeError, r"Unable to cast object of type '[\w.]+' to type 'System.Enum'\.", i.ToType, Enum, self.p)

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
