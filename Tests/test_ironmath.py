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

#
# test Microsoft.Scripting.Math
#


from iptest.assert_util import *
skiptest("win32")


from System import *
import clr
#silverlight already has this
if is_cli:
    math_assembly = (0j).GetType().Assembly
    clr.AddReference(math_assembly)
load_iron_python_test()
import IronPythonTest

if is_net40:
    from System.Numerics import BigInteger, Complex
else:
    from Microsoft.Scripting.Math import BigInteger
    from Microsoft.Scripting.Math import Complex64 as Complex


class myFormatProvider(IFormatProvider):
    def ToString():pass
    
p = myFormatProvider()


def test_bigint():
    AreEqual(BigInteger.Add(1,99999999999999999999999999999999999999999999999999999999999) ,BigInteger.Subtract(100000000000000000000000000000000000000000000000000000000001,1))
    AreEqual(BigInteger.Multiply(400,500) , BigInteger.Divide(1000000,5))
    AreEqual(BigInteger.Multiply(400,8) , BigInteger.LeftShift(400,3))
    AreEqual(BigInteger.Divide(400,8) , BigInteger.RightShift(400,3))
    AreEqual(BigInteger.RightShift(BigInteger.LeftShift(400,100),100) , 400)
    AreEqual(BigInteger.RightShift(BigInteger.LeftShift(-12345678987654321,100),100) , -12345678987654321)
    if is_net40:
        AssertError(ValueError, BigInteger.RightShift, 400, -100)
        AssertError(ValueError, BigInteger.LeftShift, 400, -100)
        AssertError(ValueError, BigInteger.RightShift, -12345678987654321, -100)
        AssertError(ValueError, BigInteger.LeftShift, -12345678987654321, -100)
    else:
        AreEqual(BigInteger.LeftShift(BigInteger.RightShift(400,-100),-100) , 400)
        AreEqual(BigInteger.LeftShift(BigInteger.RightShift(-12345678987654321,-100),-100) , -12345678987654321)
    AreEqual(BigInteger(-123456781234567812345678123456781234567812345678123456781234567812345678).OnesComplement().OnesComplement() , -123456781234567812345678123456781234567812345678123456781234567812345678)
    AreEqual(BigInteger(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678).OnesComplement() , -(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678 + 1 ))
    Assert(BigInteger.Xor(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678,BigInteger(-1234567812345678123456781234567812345678123456781234567812345678123456781234567812345678).OnesComplement()) , -1)
    AreEqual(BigInteger.BitwiseAnd(0xff00ff00,BigInteger.BitwiseOr(0x00ff00ff,0xaabbaabb)) , BigInteger(0xaa00aa00))
    AreEqual(BigInteger.Mod(BigInteger(-9999999999999999999999999999999999999999),1000000000000000000) , -BigInteger.Mod(9999999999999999999999999999999999999999,BigInteger(-1000000000000000000)))
    
    AreEqual(BigInteger.ToInt64(0x7fffffffffffffff) , 9223372036854775807)
    AssertError(OverflowError, BigInteger.ToInt64, 0x8000000000000000)
    
    AreEqual(BigInteger(-0).ToBoolean(p) , False )
    AreEqual(BigInteger(-1212321.3213).ToBoolean(p) , True )
    AreEqual(BigInteger(1212321384892342394723947).ToBoolean(p) , True )
    
    AreEqual(BigInteger(0).ToChar(p) , Char.MinValue)
    AreEqual(BigInteger(65).ToChar(p) , IConvertible.ToChar('A', p))
    AreEqual(BigInteger(0xffff).ToChar(p) , Char.MaxValue)
    AssertError(OverflowError, BigInteger(-1).ToChar, p)
    
    AreEqual(BigInteger(100).ToDouble(p) , 100.0)
    AreEqual(BigInteger(BigInteger(100).ToDouble(p)).ToSingle(p) , BigInteger(100.1213123).ToFloat())
    
    Assert(BigInteger(100) != 100.32)
    AreEqual(BigInteger(100) , 100.0)
    
    Assert( 100.32 != BigInteger(100))
    AreEqual(100.0 , BigInteger(100) )

def test_big_1():
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
    
        b = BigInteger(-x ** a )
        left = getattr(b, m)(p)
        right = t.MinValue
        AreEqual(left, right)
        
        b = BigInteger(2 ** a -1)
        left = getattr(b, m)(p)
        right = t.MaxValue
        AreEqual(left, right)
    
        b = BigInteger(0)
        left = getattr(b, m)(p)
        right = t.MaxValue - t.MaxValue
        AreEqual(left, 0)

        AssertError(OverflowError,getattr(BigInteger(2 ** a ), m),p)
        AssertError(OverflowError,getattr(BigInteger(-1 - x ** a ), m),p)


def test_big_2():
    for (a, m, t,x) in [
                        (31, "ToInt32",Int32,2),
                        (32, "ToUInt32",UInt32,0),
                        (63, "ToInt64",Int64,2),
                        (64, "ToUInt64",UInt64,0)
                       ]:
    
        b = BigInteger(-x ** a )
        left = getattr(b, m)()
        right = t.MinValue
        AreEqual(left, right)

        b = BigInteger(2 ** a -1)
        left = getattr(b, m)()
        right = t.MaxValue
        AreEqual(left, right)
    
        b = BigInteger(0)
        left = getattr(b, m)()
        right = t.MaxValue - t.MaxValue
        AreEqual(left, right)

        AssertError(OverflowError,getattr(BigInteger(2 ** a ), m))
        AssertError(OverflowError,getattr(BigInteger(-1 - x ** a ), m))


#complex
def test_complex():
    AreEqual(
        Complex.Add(
            Complex(BigInteger(9999), -1234),
            Complex.Conjugate(Complex(9999, -1234)) ),
        Complex.Multiply(BigInteger(9999), 2) )
    AreEqual(
        Complex.Add(
            Complex(99999.99e-200, 12345.88e+100),
            Complex.Negate(Complex(99999.99e-200, 12345.88e+100)) ),
        Complex.Subtract(
            Complex(99999.99e-200, 12345.88e+100),
            Complex(99999.99e-200, 12345.88e+100) ))
    AreEqual(
        Complex.Divide(4+2j,2),
        (2 + 1j) )
    Assert(not hasattr(Complex, "Mod"))  #IP 1.x had limited support for modulo which has been removed

def test_bool_misc():
    if is_net40:
        def is_zero(bigint):
            return bigint.IsZero
    else:
        def is_zero(bigint):
            return bigint.IsZero()
    
    AreEqual(BigInteger(-1234).Sign, -1)
    AreEqual(is_zero(BigInteger(-1234)), False)
    AreEqual(BigInteger(-1234).IsNegative(), True)
    AreEqual(BigInteger(-1234).IsPositive(), False)
    
    AreEqual(BigInteger(0).Sign, 0)
    AreEqual(is_zero(BigInteger(0)), True)
    AreEqual(BigInteger(0).IsNegative(), False)
    AreEqual(BigInteger(0).IsPositive(), False)

    AreEqual(BigInteger(1234).Sign, 1)
    AreEqual(is_zero(BigInteger(1234)), False)
    AreEqual(BigInteger(1234).IsNegative(), False)
    AreEqual(BigInteger(1234).IsPositive(), True)


def test_byte_conversions():

    def CheckByteConversions(bigint, bytes):
        SequencesAreEqual(bigint.ToByteArray(), bytes)
        AreEqual(BigInteger.Create(Array[Byte](bytes)), bigint)

    CheckByteConversions(BigInteger(0x00), [0x00])

    CheckByteConversions(BigInteger(-0x01), [0xff])
    CheckByteConversions(BigInteger(-0x81), [0x7f, 0xff])
    CheckByteConversions(BigInteger(-0x100), [0x00, 0xff])
    CheckByteConversions(BigInteger(-0x1000), [0x00, 0xf0])
    CheckByteConversions(BigInteger(-0x10000), [0x00, 0x00, 0xff])
    CheckByteConversions(BigInteger(-0x100000), [0x00, 0x00, 0xf0])
    CheckByteConversions(BigInteger(-0x10000000), [0x00, 0x00, 0x00, 0xf0])
    CheckByteConversions(BigInteger(-0x100000000), [0x00, 0x00, 0x00, 0x00, 0xff])

    CheckByteConversions(BigInteger(0x7f), [0x7f])
    CheckByteConversions(BigInteger(0xff), [0xff, 0x00])
    CheckByteConversions(BigInteger(0x0201), [0x01, 0x02])
    CheckByteConversions(BigInteger(0xf2f1), [0xf1, 0xf2, 0x00])
    CheckByteConversions(BigInteger(0x03020100), [0x00, 0x01, 0x02, 0x03])
    CheckByteConversions(BigInteger(0x0403020100), [0x00, 0x01, 0x02, 0x03, 0x04])
    CheckByteConversions(BigInteger(0x0706050403020100), [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07])
    CheckByteConversions(BigInteger(0x080706050403020100), [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08])

def test_dword_conversions():
    def CheckDwordConversions(bigint, dwords):
        SequencesAreEqual(bigint.GetWords(), dwords)
        if bigint == BigInteger.Zero:
            AreEqual(
                IronPythonTest.System_Scripting_Math.CreateBigInteger(
                    0,
                    Array[UInt32](dwords),),
                bigint)
        else:
            AreEqual(
                IronPythonTest.System_Scripting_Math.CreateBigInteger(
                    1,
                    Array[UInt32](dwords)),
                bigint)
            AreEqual(
                IronPythonTest.System_Scripting_Math.CreateBigInteger(
                    -1,
                    Array[UInt32](dwords)),
                BigInteger.Negate(bigint))
    
    CheckDwordConversions(BigInteger(0), [0x00000000])
    CheckDwordConversions(BigInteger(1), [0x00000001])
    CheckDwordConversions(BigInteger((1<<31)), [0x80000000])
    CheckDwordConversions(BigInteger(((1<<31) + 9)), [0x80000009])
    CheckDwordConversions(BigInteger((1<<32)), [0x00000000, 0x00000001])

def test_misc():
    AssertError(ArgumentException, IronPythonTest.System_Scripting_Math.CreateBigInteger, 0, (1, 2, 3))
    AssertError(ArgumentNullException, IronPythonTest.System_Scripting_Math.CreateBigInteger, 0, None)

    AreEqual(BigInteger(1).CompareTo(None), 1)
    if is_net40:
        AreEqual(BigInteger(1).CompareTo(True), 0)
    else:
        AssertError(ArgumentException, BigInteger(1).CompareTo, True)

run_test(__name__)
