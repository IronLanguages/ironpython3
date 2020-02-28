# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, is_posix, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class MethodDispatchTest(IronPythonTestCase):
    def setUp(self):
        super(MethodDispatchTest, self).setUp()
        self.load_iron_python_test()

    def test_sanity(self):
        """A few minimal checks for error messages - using bad exact match rules"""
        from IronPythonTest import Cmplx, BindingTestClass, Infinite, InheritedBindingSub, MixedDispatch

        md = MixedDispatch("hi")
        self.assertRaisesMessage(TypeError, "Combine() takes exactly 2 arguments (1 given)", MixedDispatch.Combine, md)
        self.assertRaisesMessage(TypeError, "Combine() takes at least 1 argument (0 given)", md.Combine)
        md.Combine(md)

        x = BindingTestClass.Bind("Hello")
        self.assertTrue(x == "Hello")
        x = BindingTestClass.Bind(10)
        self.assertTrue(x == 10)
        x = BindingTestClass.Bind(False)
        self.assertTrue(x == False)

        b = InheritedBindingSub()
        self.assertTrue(b.Bind(True) == "Subclass bool")
        self.assertTrue(b.Bind("Hi") == "Subclass string")
        self.assertTrue(b.Bind(10) == "Subclass int")

        x = "this is a string"
        y = x.Split(' ')
        self.assertTrue(y[0] == "this")
        self.assertTrue(y[1] == "is")
        self.assertTrue(y[2] == "a")
        self.assertTrue(y[3] == "string")

        def verify_complex(x, xx):
            self.assertTrue(x.Real == xx.real)
            self.assertTrue(x.Imag == xx.imag)

        i  = Cmplx(3, 4)
        ii = (3 + 4j)
        j  = Cmplx(2, 1)
        jj = (2 + 1j)

        verify_complex(i, ii)
        verify_complex(j, jj)
        
        verify_complex(i + j, ii + jj)
        verify_complex(i - j, ii - jj)
        verify_complex(i * j, ii * jj)
        verify_complex(i / j, ii / jj)

        verify_complex(i + 2.5, ii + 2.5)
        verify_complex(i - 2.5, ii - 2.5)
        verify_complex(i * 2.5, ii * 2.5)
        verify_complex(i / 2.5, ii / 2.5)
        
        verify_complex(2.5 + j, 2.5 + jj)
        verify_complex(2.5 - j, 2.5 - jj)
        verify_complex(2.5 * j, 2.5 * jj)
        verify_complex(2.5 / j, 2.5 / jj)
        
        verify_complex(i + 2, ii + 2)
        verify_complex(i - 2, ii - 2)
        verify_complex(i * 2, ii * 2)
        verify_complex(i / 2, ii / 2)

        verify_complex(2 + j, 2 + jj)
        verify_complex(2 - j, 2 - jj)
        verify_complex(2 * j, 2 * jj)
        verify_complex(2 / j, 2 / jj)
        
        verify_complex(-i, -ii)
        verify_complex(-j, -jj)

        i *= j
        ii *= jj
        verify_complex(i, ii)

        i /= j
        ii /= jj
        verify_complex(i, ii)

        i += j
        ii += jj
        verify_complex(i, ii)

        i -= j
        ii -= jj
        verify_complex(i, ii)

        i -= 2
        ii -= 2
        verify_complex(i, ii)

        i += 2
        ii += 2
        verify_complex(i, ii)
        
        i *= 2
        ii *= 2
        verify_complex(i, ii)

        i /= 2
        ii /= 2
        verify_complex(i, ii)

        class D(Infinite):
            pass

        class E(D):
            def __cmp__(self, other):
                return super(E, self).__cmp__(other)

        e = E()
        result = E.__cmp__(e, 20)
        retuls = e.__cmp__(20)

        class F(Infinite):
            def __cmp__(self, other):
                return super(F, self).__cmp__(other)

        f = F()
        result = F.__cmp__(f, 20)
        result = f.__cmp__(20)


    def test_system_drawing(self):
        import clr
        if is_netcoreapp:
            clr.AddReference("System.Drawing.Primitives")
        else:
            clr.AddReference("System.Drawing")
        from System.Drawing import Rectangle
        r = Rectangle(0, 0, 3, 7)
        s = Rectangle(3, 0, 8, 14)
        
        # calling the static method
        i = Rectangle.Intersect(r, s)
        self.assertEqual(i, Rectangle(3, 0, 0, 7))
        self.assertEqual(r, Rectangle(0, 0, 3, 7))
        self.assertEqual(s, Rectangle(3, 0, 8, 14))
        
        # calling the instance
        i = r.Intersect(s)
        self.assertEqual(i, None)
        self.assertEqual(r, Rectangle(3, 0, 0, 7))
        self.assertEqual(s, Rectangle(3, 0, 8, 14))
        
        # calling instance w/ StrongBox
        r = Rectangle(0, 0, 3, 7)
        box = clr.StrongBox[Rectangle](r)
        i = box.Intersect(s)
        self.assertEqual(i, None)
        self.assertEqual(box.Value, Rectangle(3, 0, 0, 7))
        self.assertEqual(s, Rectangle(3, 0, 8, 14))
        
        # should be able to access properties through the box
        self.assertEqual(box.X, 3)
        
        # multiple sites should produce the same function
        i = box.Intersect
        j = box.Intersect

    def test_io_memorystream(self):
        import System
        s = System.IO.MemoryStream()
        a = System.Array.CreateInstance(System.Byte, 10)
        b = System.Array.CreateInstance(System.Byte, a.Length)
        for i in range(a.Length):
            a[i] = a.Length - i
        s.Write(a, 0, a.Length)
        result = s.Seek(0, System.IO.SeekOrigin.Begin)
        r = s.Read(b, 0, b.Length)
        self.assertTrue(r == b.Length)
        for i in range(a.Length):
            self.assertEqual(a[i], b[i])

    def test_types(self):
        global type
        import System
        from IronPythonTest import BindResult, BindTest
        BoolType    = (System.Boolean, BindTest.BoolValue,    BindResult.Bool)
        ByteType    = (System.Byte,    BindTest.ByteValue,    BindResult.Byte)
        CharType    = (System.Char,    BindTest.CharValue,    BindResult.Char)
        DecimalType = (System.Decimal, BindTest.DecimalValue, BindResult.Decimal)
        DoubleType  = (System.Double,  BindTest.DoubleValue,  BindResult.Double)
        FloatType   = (System.Single,  BindTest.FloatValue,   BindResult.Float)
        IntType     = (System.Int32,   BindTest.IntValue,     BindResult.Int)
        LongType    = (System.Int64,   BindTest.LongValue,    BindResult.Long)
        ObjectType  = (System.Object,  BindTest.ObjectValue,  BindResult.Object)
        SByteType   = (System.SByte,   BindTest.SByteValue,   BindResult.SByte)
        ShortType   = (System.Int16,   BindTest.ShortValue,   BindResult.Short)
        StringType  = (System.String,  BindTest.StringValue,  BindResult.String)
        UIntType    = (System.UInt32,  BindTest.UIntValue,    BindResult.UInt)
        ULongType   = (System.UInt64,  BindTest.ULongValue,   BindResult.ULong)
        UShortType  = (System.UInt16,  BindTest.UShortValue,  BindResult.UShort)
        
        saveType = type

        for binding in [BoolType, ByteType, CharType, DecimalType, DoubleType,
                        FloatType, IntType, LongType, ObjectType, SByteType,
                        ShortType, StringType, UIntType, ULongType, UShortType]:
            type   = binding[0]
            value  = binding[1]
            expect = binding[2]

            # Select using System.Type object
            select = BindTest.Bind.Overloads[type]
            result = select(value)
            self.assertEqual(expect, result)
        
            # Select using ReflectedType object
            select = BindTest.Bind.Overloads[type]
            result = select(value)
            self.assertEqual(expect, result)

            # Make simple call
            result = BindTest.Bind(value)
            if not binding is CharType:
                self.assertEqual(expect, result)

            result, output = BindTest.BindRef(value)
            if not binding is CharType:
                self.assertEqual(expect | BindResult.Ref, result)
        
            # Select using Array type
            arrtype = System.Type.MakeArrayType(type)
            select = BindTest.Bind.Overloads[arrtype]
            array  = System.Array.CreateInstance(type, 1)
            array[0] = value
            result = select(array)
            self.assertEqual(expect | BindResult.Array, result)

            # Select using ByRef type
            reftype = System.Type.MakeByRefType(type)
            select = BindTest.Bind.Overloads[reftype]
            result, output = select()
            self.assertEqual(expect | BindResult.Out, result)

            select = BindTest.BindRef.Overloads[reftype]
            result, output = select(value)
            self.assertEqual(expect | BindResult.Ref, result)

        type = saveType

        select = BindTest.Bind.Overloads[()]
        result = select()
        self.assertEqual(getattr(BindResult, "None"), result)


    def test_enum(self):
        import System
        from IronPythonTest import DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong, EnumTest

        class MyEnumTest(EnumTest):
            def TestDaysInt(self):
                return DaysInt.Weekdays

            def TestDaysShort(self):
                return DaysShort.Weekdays

            def TestDaysLong(self):
                return DaysLong.Weekdays

            def TestDaysSByte(self):
                return DaysSByte.Weekdays

            def TestDaysByte(self):
                return DaysByte.Weekdays

            def TestDaysUShort(self):
                return DaysUShort.Weekdays
        
            def TestDaysUInt(self):
                return DaysUInt.Weekdays

            def TestDaysULong(self):
                return DaysULong.Weekdays

        et = MyEnumTest()

        self.assertEqual(et.TestDaysInt(), DaysInt.Weekdays)
        self.assertEqual(et.TestDaysShort(), DaysShort.Weekdays)
        self.assertEqual(et.TestDaysLong(), DaysLong.Weekdays)
        self.assertEqual(et.TestDaysSByte(), DaysSByte.Weekdays)
        self.assertEqual(et.TestDaysByte(), DaysByte.Weekdays)
        self.assertEqual(et.TestDaysUShort(), DaysUShort.Weekdays)
        self.assertEqual(et.TestDaysUInt(), DaysUInt.Weekdays)
        self.assertEqual(et.TestDaysULong(), DaysULong.Weekdays)

        for l in range(10):
            a = System.Array.CreateInstance(str, l)
            r = []
            for i in range(l):
                a[i] = "ip" * i
                r.append("IP" * i)
            m = list(map(str.upper, a))
            self.assertEqual(m, r)

        methods = [
            MyEnumTest.TestEnumInt,
            MyEnumTest.TestEnumShort,
            MyEnumTest.TestEnumLong,
            MyEnumTest.TestEnumSByte,
            MyEnumTest.TestEnumUInt,
            MyEnumTest.TestEnumUShort,
            MyEnumTest.TestEnumULong,
            MyEnumTest.TestEnumByte,
            #MyEnumTest.TestEnumBoolean,
        ]

        parameters = [
            DaysInt.Weekdays,
            DaysShort.Weekdays,
            DaysLong.Weekdays,
            DaysSByte.Weekdays,
            DaysByte.Weekdays,
            DaysUShort.Weekdays,
            DaysUInt.Weekdays,
            DaysULong.Weekdays,
        ]

        for p in parameters:
            #No implicit conversions from enum to numeric types are allowed
            for m in methods:
                self.assertRaises(TypeError, m, p)
            x = int(p)
            x = bool(p)

    def test_dispatch(self):
        import System
        from IronPythonTest import Dispatch, DispatchHelpers, DispatchDerived

        def Check(flagValue, func, *args):
            Dispatch.Flag = 0
            func(*args)
            self.assertTrue(Dispatch.Flag == flagValue)

        d = Dispatch()

        #======================================================================
        #        public void M1(int arg) { Flag = 101; }
        #        public void M1(DispatchHelpers.Color arg) { Flag = 201; }
        #======================================================================

        Check(101, d.M1, 1)
        Check(201, d.M1, DispatchHelpers.Color.Red)
        self.assertRaises(TypeError, d.M1, None)

        #======================================================================
        #        public void M2(int arg) { Flag = 102; }
        #        public void M2(int arg, params int[] arg2) { Flag = 202; }
        #======================================================================

        Check(102, d.M2, 1)
        Check(202, d.M2, 1, 1)
        Check(202, d.M2, 1, 1, 1)
        Check(202, d.M2, 1, None)
        self.assertRaises(TypeError, d.M2, 1, 1, "string", 1)
        self.assertRaises(TypeError, d.M2, None, None)
        self.assertRaises(TypeError, d.M2, None)

        #======================================================================
        #        public void M3(int arg) { Flag = 103; }
        #        public void M3(int arg, int arg2) { Flag = 203; }
        #======================================================================

        self.assertRaises(TypeError, d.M3, DispatchHelpers.Color.Red)
        Check(103, d.M3, 1)
        Check(203, d.M3, 1, 1)
        self.assertRaises(TypeError, d.M3, None, None)
        self.assertRaises(TypeError, d.M4, None)

        #======================================================================
        #        public void M4(int arg) { Flag = 104; }
        #        public void M4(int arg, __arglist) { Flag = 204; }
        #======================================================================

        self.assertRaises(TypeError, DispatchHelpers.Color.Red)
        Check(104, d.M4, 1)
        #VarArgs methods can not be called from IronPython by design
        self.assertRaises(TypeError, d.M4, 1, 1)
        self.assertRaises(TypeError, d.M4, None, None)
        self.assertRaises(TypeError, d.M4, None)

        #======================================================================
        #        public void M5(float arg) { Flag = 105; }
        #        public void M5(double arg) { Flag = 205; }
        #======================================================================

        #!!! easy way to get M5(float) invoked
        Check(105, d.M5, System.Single.Parse("3.14"))
        Check(205, d.M5, 3.14)
        self.assertRaises(TypeError, d.M5, None)

        #======================================================================
        #        public void M6(char arg) { Flag = 106; }
        #        public void M6(string arg) { Flag = 206; }
        #======================================================================

        #!!! no way to invoke M6(char)
        Check(206, d.M6, 'a')
        Check(206, d.M6, 'hello')
        Check(206, d.M6, 'hello'[0])
        Check(206, d.M6, None)

        #======================================================================
        #        public void M7(int arg) { Flag = 107; }
        #        public void M7(params int[] args) { Flag = 207; }
        #======================================================================
        Check(207, d.M7)
        Check(107, d.M7, 1)
        Check(207, d.M7, 1, 1)
        Check(207, d.M7, None)

        #======================================================================
        #        public void M8(int arg) { Flag = 108; }
        #        public void M8(ref int arg) { Flag = 208; arg = 999; }
        #        public void M10(ref int arg) { Flag = 210; arg = 999; }
        #======================================================================
        Check(108, d.M8, 1)

        self.assertTrue(d.M10(1) == 999)
        Check(210, d.M10, 1)
        self.assertRaises(TypeError, d.M10, None)

        #======================================================================
        #        public void M11(int arg, int arg2) { Flag = 111; }
        #        public void M11(DispatchHelpers.Color arg, int arg2) { Flag = 211; }
        #======================================================================

        Check(111, d.M11, 1, 1)
        self.assertRaises(TypeError, d.M11, 1, DispatchHelpers.Color.Red)
        Check(211, d.M11, DispatchHelpers.Color.Red, 1)
        self.assertRaises(TypeError, d.M11, DispatchHelpers.Color.Red, DispatchHelpers.Color.Red)

        #======================================================================
        #        public void M12(int arg, DispatchHelpers.Color arg2) { Flag = 112; }
        #        public void M12(DispatchHelpers.Color arg, int arg2) { Flag = 212; }
        #======================================================================

        self.assertRaises(TypeError, d.M12, 1, 1)
        Check(112, d.M12, 1, DispatchHelpers.Color.Red)
        Check(212, d.M12, DispatchHelpers.Color.Red, 1)
        self.assertRaises(TypeError, d.M12, DispatchHelpers.Color.Red, DispatchHelpers.Color.Red)

        #======================================================================
        #        public void M20(DispatchHelpers.B arg) { Flag = 120; }
        #======================================================================

        Check(120, d.M20, None)

        #======================================================================
        #        public void M22(DispatchHelpers.B arg) { Flag = 122; }
        #        public void M22(DispatchHelpers.D arg) { Flag = 222; }
        #======================================================================

        Check(222, d.M22, None)
        Check(122, d.M22, DispatchHelpers.B())
        Check(222, d.M22, DispatchHelpers.D())

        #======================================================================
        #        public void M23(DispatchHelpers.I arg) { Flag = 123; }
        #        public void M23(DispatchHelpers.C2 arg) { Flag = 223; }
        #======================================================================

        Check(123, d.M23, DispatchHelpers.C1())
        Check(223, d.M23, DispatchHelpers.C2())

        #======================================================================
        # Bug 20 - public void M50(params DispatchHelpers.B[] args) { Flag = 150; }
        #======================================================================

        Check(150, d.M50, DispatchHelpers.B())
        Check(150, d.M50, DispatchHelpers.D())
        Check(150, d.M50, DispatchHelpers.B(), DispatchHelpers.B())
        Check(150, d.M50, DispatchHelpers.B(), DispatchHelpers.D())
        Check(150, d.M50, DispatchHelpers.D(), DispatchHelpers.D())

        #======================================================================
        #        public void M51(params DispatchHelpers.B[] args) { Flag = 151; }
        #        public void M51(params DispatchHelpers.D[] args) { Flag = 251; }
        #======================================================================

        Check(151, d.M51, DispatchHelpers.B())
        Check(251, d.M51, DispatchHelpers.D())
        Check(151, d.M51, DispatchHelpers.B(), DispatchHelpers.B())
        Check(151, d.M51, DispatchHelpers.B(), DispatchHelpers.D())
        Check(251, d.M51, DispatchHelpers.D(), DispatchHelpers.D())

        #======================================================================
        #        public void M60(int? arg) { Flag = 160; }
        #======================================================================
        Check(160, d.M60, 1)
        Check(160, d.M60, None)

        #======================================================================
        #        public void M70(Dispatch arg) { Flag = 170; }
        #======================================================================
        Check(170, d.M70, d)
        self.assertRaises(TypeError, Dispatch.M70, d)
        self.assertRaises(TypeError, d.M70, d, d)
        Check(170, Dispatch.M70, d, d)

        #======================================================================
        #        public static void M71(Dispatch arg) { Flag = 171; }
        #======================================================================
        Check(171, d.M71, d)
        Check(171, Dispatch.M71, d)
        self.assertRaises(TypeError, d.M71, d, d)
        self.assertRaises(TypeError, Dispatch.M71, d, d)

        #======================================================================
        #        public static void M81(Dispatch arg, int arg2) { Flag = 181; }
        #        public void M81(int arg) { Flag = 281; }
        #======================================================================
        self.assertRaises(TypeError, d.M81, d, 1)
        Check(181, Dispatch.M81, d, 1)
        Check(281, d.M81, 1)
        self.assertRaises(TypeError, Dispatch.M81, 1)

        #======================================================================
        #        public static void M82(bool arg) { Flag = 182; }
        #        public static void M82(string arg) { Flag = 282; }
        #======================================================================
        Check(182, d.M82, True)
        Check(282, d.M82, "True")
        Check(182, Dispatch.M82, True)
        Check(282, Dispatch.M82, "True")

        #======================================================================
        #        public void M83(bool arg) { Flag = 183; }
        #        public void M83(string arg) { Flag = 283; }
        #======================================================================
        Check(183, d.M83, True)
        Check(283, d.M83, "True")
        self.assertRaises(TypeError, Dispatch.M83, True)
        self.assertRaises(TypeError, Dispatch.M83, "True")
        self.assertRaises(TypeError, d.M83, d, True)
        self.assertRaises(TypeError, d.M83, d, "True")
        Check(183, Dispatch.M83, d, True)
        Check(283, Dispatch.M83, d, "True")


        #======================================================================
        #        public void M90<T>(int arg) { Flag = 190; }
        #======================================================================
        self.assertRaises(TypeError, d.M90, 1)
        Check(191, d.M91, 1)

        #======================================================================
        #======================================================================

        d = DispatchDerived()
        Check(201, d.M1, 1)

        Check(102, d.M2, 1)
        Check(202, d.M2, DispatchHelpers.Color.Red)

        Check(103, d.M3, 1)
        Check(203, d.M3, "hello")

        Check(104, d.M4, 100)
        Check(204, d.M4, "python")

        Check(205, d.M5, 1)
        Check(106, d.M6, 1)


    def test_conversion_dispatch(self):
        """ConversionDispatch - Test binding List / Tuple to array/enum/IList/ArrayList/etc..."""
        import System
        from IronPythonTest import Cmplx, Cmplx2, ConversionDispatch, FieldTest, MixedDispatch
        cd = ConversionDispatch()

        ###########################################
        # checker functions - verify the result of the test

        def Check(res, orig):
            if hasattr(res, "__len__"):
                self.assertEqual(len(res), len(orig))
            i = 0
            for a in res:
                self.assertEqual(a, orig[i])
                i = i+1
            self.assertEqual(i, len(orig))

        def len_helper(o):
            if hasattr(o, 'Count'): return o.Count
            return len(o)

        def clear_helper(o):
            if hasattr(o, 'Clear'):
                o.Clear()
            else:
                del o[:]

        def CheckModify(res, orig):
            Check(res, orig)

            index = len_helper(res)
            res.Add(orig[0])
            Check(res, orig)

            res.RemoveAt(index)
            Check(res, orig)

            x = res[0]
            res.Remove(orig[0])
            Check(res, orig)

            res.Insert(0, x)
            Check(res, orig)

            if(hasattr(res, "sort")):
                res.sort()
                Check(res, orig)

            clear_helper(res)
            Check(res, orig)

        def keys_helper(o):
            if hasattr(o, 'keys'): return list(o.keys())
            
            return o.Keys
            
        def CheckDict(res, orig):
            if hasattr(res, "__len__"):
                self.assertEqual(len(res), len(orig))
            i = 0
            
            for a in keys_helper(res):
                self.assertEqual(res[a], orig[a])
                i = i+1
            self.assertEqual(i, len(orig))


        ###################################
        # test data sets used for all the checks


        # list/tuple data
        inttuple = (2,3,4,5)
        strtuple = ('a', 'b', 'c', 'd')
        othertuple = (['a', 2], ['c', 'd', 3], 5)


        intlist = [2,3,4,5]
        strlist = ['a', 'b', 'c', 'd']
        otherlist = [('a', 2), ('c', 'd', 3), 5]

        intdict = {2:5, 7:8, 9:10}
        strdict = {'abc': 'def', 'xyz':'abc', 'mno':'prq'}
        objdict = { (2,3) : (4,5), (1,2):(3,4), (8,9):(1,4)}
        mixeddict = {'abc': 2, 'def': 9, 'qrs': 8}

        objFunctions = [cd.Array,cd.ObjIList, cd.Enumerable]
        objData = [inttuple, strtuple, othertuple]

        intFunctions = [cd.IntEnumerable, cd.IntIList]
        intData = [inttuple, intlist]

        intTupleFunctions = [cd.IntArray]
        intTupleData = [inttuple]

        strFunctions = [cd.StringEnumerable, cd.StringIList]
        strData = [strtuple, strlist]

        strTupleFunctions = [cd.StringArray]
        strTupleData = [strtuple]

        # dictionary data

        objDictFunctions = [cd.DictTest]
        objDictData = [intdict, strdict, objdict, mixeddict]

        intDictFunctions = [cd.IntDictTest]
        intDictData = [intdict]

        strDictFunctions = [cd.StringDictTest]
        strDictData = [strdict]

        mixedDictFunctions = [cd.MixedDictTest]
        mixedDictData = [mixeddict]

        modCases = [ (cd.ObjIList, (intlist, strlist, otherlist)),
                    ( cd.IntIList, (intlist,) ),
                    ( cd.StringIList, (strlist,) ),
                    ]

        testCases = [ [objFunctions, objData],
                    [intFunctions, intData],
                    [strFunctions, strData],
                    [intTupleFunctions, intTupleData],
                    [strTupleFunctions, strTupleData] ]

        dictTestCases = ( (objDictFunctions, objDictData ),
                        (intDictFunctions, intDictData ),
                        (strDictFunctions, strDictData),
                        (mixedDictFunctions, mixedDictData) )

        ############################################3
        # run the test cases:

        # verify all conversions succeed properly

        for cases in testCases:
            for func in cases[0]:
                for data in cases[1]:
                    Check(func(data), data)


        # verify that modifications show up as appropriate.

        for case in modCases:
            for data in case[1]:
                newData = list(data)
                CheckModify(case[0](newData), newData)


        # verify dictionary test cases

        for case in dictTestCases:
            for data in case[1]:
                for func in case[0]:
                    newData = dict(data)
                    CheckDict(func(newData), newData)


        x = FieldTest()
        y = System.Collections.Generic.List[System.Type]()
        x.Field = y

        # verify we can bind w/ add & radd
        self.assertEqual(x.Field, y)

        a = Cmplx(2, 3)
        b = Cmplx2(3, 4)

        x = a + b
        y = b + a


        #############################################################
        # Verify combinaions of instance / no instance

        a = MixedDispatch("one")
        b = MixedDispatch("two")
        c = MixedDispatch("three")
        d = MixedDispatch("four")

        x= a.Combine(b)
        y = MixedDispatch.Combine(a,b)

        self.assertEqual(x.called, "instance")
        self.assertEqual(y.called, "static")

        x= a.Combine2(b)
        y = MixedDispatch.Combine2(a,b)
        z = MixedDispatch.Combine2(a,b,c,d)
        v = a.Combine2(b,c,d)

        self.assertEqual(x.called, "instance")
        self.assertEqual(y.called, "static")
        self.assertEqual(z.called, "instance_three")
        self.assertEqual(v.called, "instance_three")


        ###########################################################
        # verify non-instance built-in's don't get bound

        class C:
            mycmp = cmp
            
        a = C()
        self.assertEqual(a.mycmp(0,0), 0)

    def test_default_value(self):
        import clr
        from IronPythonTest import BindResult, BigEnum, DefaultValueTest
        tst = DefaultValueTest()

        self.assertEqual(tst.Test_Enum(), BindResult.Bool)
        self.assertEqual(tst.Test_BigEnum(), BigEnum.BigValue)
        self.assertEqual(tst.Test_String(), 'Hello World')
        self.assertEqual(tst.Test_Int(), 5)
        self.assertEqual(tst.Test_UInt(), 4294967295)
        self.assertEqual(tst.Test_Bool(), True)
        self.assertEqual(str(tst.Test_Char()), 'A')
        self.assertEqual(tst.Test_Byte(), 2)
        self.assertEqual(tst.Test_SByte(), 2)
        self.assertEqual(tst.Test_Short(), 2)
        self.assertEqual(tst.Test_UShort(), 2)
        self.assertEqual(tst.Test_Long(), 9223372036854775807)
        self.assertEqual(tst.Test_ULong(), 18446744073709551615)

        r = clr.Reference[object]("Hi")
        s = clr.Reference[object]("Hello")
        t = clr.Reference[object]("Ciao")

        self.assertEqual(tst.Test_ByRef_Object(), "System.Reflection.Missing; System.Reflection.Missing; System.Reflection.Missing")
        self.assertEqual(tst.Test_ByRef_Object(r), "Hi; System.Reflection.Missing; System.Reflection.Missing")
        self.assertEqual(tst.Test_ByRef_Object(r, s), "Hi; Hello; System.Reflection.Missing")
        self.assertEqual(tst.Test_ByRef_Object(r, s, t), "Hi; Hello; Ciao")
        self.assertEqual(tst.Test_ByRef_Object("Hi", "Hello", "Ciao"), ("Hi; Hello; Ciao"))
        self.assertRaises(TypeError, tst.Test_ByRef_Object, "Hi")
        
        self.assertEqual(tst.Test_Default_Cast(), "1")
        self.assertEqual(tst.Test_Default_Cast("Hello"), ("Hello", "Hello"))
        self.assertEqual(tst.Test_Default_Cast(None), ("(null)", None))

        self.assertEqual(tst.Test_Default_ValueType(), "1")
        self.assertEqual(tst.Test_Default_ValueType("Hello"), "Hello")
        self.assertEqual(tst.Test_Default_ValueType(None), "(null)")

    def test_missing_value(self):
        from IronPythonTest import MissingValueTest
        tst = MissingValueTest()

        self.assertEqual(tst.Test_1(), "(bool)False")
        self.assertEqual(tst.Test_2(), "(bool)False")
        self.assertEqual(tst.Test_3(), "(sbyte)0")
        self.assertEqual(tst.Test_4(), "(sbyte)0")
        self.assertEqual(tst.Test_5(), "(byte)0")
        self.assertEqual(tst.Test_6(), "(byte)0")
        self.assertEqual(tst.Test_7(), "(short)0")
        self.assertEqual(tst.Test_8(), "(short)0")
        self.assertEqual(tst.Test_9(), "(ushort)0")
        self.assertEqual(tst.Test_10(), "(ushort)0")
        self.assertEqual(tst.Test_11(), "(int)0")
        self.assertEqual(tst.Test_12(), "(int)0")
        self.assertEqual(tst.Test_13(), "(uint)0")
        self.assertEqual(tst.Test_14(), "(uint)0")
        self.assertEqual(tst.Test_15(), "(long)0")
        self.assertEqual(tst.Test_16(), "(long)0")
        self.assertEqual(tst.Test_17(), "(ulong)0")
        self.assertEqual(tst.Test_18(), "(ulong)0")
        self.assertEqual(tst.Test_19(), "(decimal)0")
        self.assertEqual(tst.Test_20(), "(decimal)0")
        self.assertEqual(tst.Test_21(), "(float)0")
        self.assertEqual(tst.Test_22(), "(float)0")
        self.assertEqual(tst.Test_23(), "(double)0")
        self.assertEqual(tst.Test_24(), "(double)0")
        self.assertEqual(tst.Test_25(), "(DaysByte)None")
        self.assertEqual(tst.Test_26(), "(DaysByte)None")
        self.assertEqual(tst.Test_27(), "(DaysSByte)None")
        self.assertEqual(tst.Test_28(), "(DaysSByte)None")
        self.assertEqual(tst.Test_29(), "(DaysShort)None")
        self.assertEqual(tst.Test_30(), "(DaysShort)None")
        self.assertEqual(tst.Test_31(), "(DaysUShort)None")
        self.assertEqual(tst.Test_32(), "(DaysUShort)None")
        self.assertEqual(tst.Test_33(), "(DaysInt)None")
        self.assertEqual(tst.Test_34(), "(DaysInt)None")
        self.assertEqual(tst.Test_35(), "(DaysUInt)None")
        self.assertEqual(tst.Test_36(), "(DaysUInt)None")
        self.assertEqual(tst.Test_37(), "(DaysLong)None")
        self.assertEqual(tst.Test_38(), "(DaysLong)None")
        self.assertEqual(tst.Test_39(), "(DaysULong)None")
        self.assertEqual(tst.Test_40(), "(DaysULong)None")
        self.assertEqual(tst.Test_41(), "(char)\x00")
        self.assertEqual(tst.Test_42(), "(char)\x00")
        self.assertEqual(tst.Test_43(), "(Structure)IronPythonTest.Structure")
        self.assertEqual(tst.Test_44(), "(Structure)IronPythonTest.Structure")
        self.assertEqual(tst.Test_45(), "(EnumSByte)Zero")
        self.assertEqual(tst.Test_46(), "(EnumSByte)Zero")
        # TODO: determine if this is a mono bug or not
        # both Zero and MinByte have the value of 0, Mono
        # https://github.com/IronLanguages/main/issues/1596
        if not is_mono:
            self.assertEqual(tst.Test_47(), "(EnumByte)Zero")
            self.assertEqual(tst.Test_48(), "(EnumByte)Zero")
            self.assertEqual(tst.Test_49(), "(EnumShort)Zero")
            self.assertEqual(tst.Test_50(), "(EnumShort)Zero")
            self.assertEqual(tst.Test_51(), "(EnumUShort)Zero")
            self.assertEqual(tst.Test_52(), "(EnumUShort)Zero")
        self.assertEqual(tst.Test_53(), "(EnumInt)MinUShort")
        self.assertEqual(tst.Test_54(), "(EnumInt)MinUShort")
        if not is_mono:
            self.assertEqual(tst.Test_55(), "(EnumUInt)MinUInt")
            self.assertEqual(tst.Test_56(), "(EnumUInt)MinUInt")
            self.assertEqual(tst.Test_57(), "(EnumLong)MinUInt")
            self.assertEqual(tst.Test_58(), "(EnumLong)MinUInt")
            self.assertEqual(tst.Test_59(), "(EnumULong)MinUInt")
            self.assertEqual(tst.Test_60(), "(EnumULong)MinUInt")
        self.assertEqual(tst.Test_61(), "(string)(null)")
        self.assertEqual(tst.Test_62(), "(string)(null)")
        self.assertEqual(tst.Test_63(), "(object)System.Reflection.Missing")
        self.assertEqual(tst.Test_64(), "(object)System.Reflection.Missing")
        self.assertEqual(tst.Test_65(), "(MissingValueTest)(null)")
        self.assertEqual(tst.Test_66(), "(MissingValueTest)(null)")

    def test_function(self):
        import System
        from IronPythonTest import BindTest, DispatchAgain

        def testfunctionhelper(c, o):
            def assertEqual(first, second):
                self.assertEqual(first,second)

            ############ OptimizedFunctionX ############
            line = ""
            for i in range(6):
                args = ",".join(['1'] * i)
                line += 'assertEqual(o.IM%d(%s), "IM%d")\n' % (i, args, i)
                line += 'assertEqual(c.IM%d(o,%s), "IM%d")\n' % (i, args, i)
                if i > 0:
                    line += 'try: o.IM%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i-1)))
                    line += 'try: c.IM%d(o, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i-1)))
                line += 'try: o.IM%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i+1)))
                line += 'try: c.IM%d(o, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i+1)))
                    
                line += 'assertEqual(o.SM%d(%s), "SM%d")\n' % (i, args, i)
                line += 'assertEqual(c.SM%d(%s), "SM%d")\n' % (i, args, i)
                
                if i > 0:
                    line += 'try: o.SM%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i-1)))
                    line += 'try: c.SM%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i-1)))
                line += 'try: o.SM%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i+1)))
                line += 'try: c.SM%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (i, ",".join(['1'] * (i+1)))
            
            #print line
            exec(line, globals(), locals())

            ############ OptimizedFunctionAny ############
            ## 1
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in [0, 3, 4]:
                    line += 'assertEqual(o.IDM0(%s), "IDM0-%d")\n' % (args, i)
                    line += 'assertEqual(c.IDM0(o,%s), "IDM0-%d")\n' % (args, i)
                else:
                    line += 'try: o.IDM0(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                    line += 'try: c.IDM0(o, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                
            #print line
            exec(line, globals(), locals())
        
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in [0, 3]:
                    line += 'assertEqual(o.SDM0(%s), "SDM0-%d")\n' % (args, i)
                    line += 'assertEqual(c.SDM0(%s), "SDM0-%d")\n' % (args, i)
                else:
                    line += 'try: o.SDM0(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                    line += 'try: c.SDM0(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                
            #print line
            exec(line, globals(), locals())

            ## 2
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in [1]:
                    line += 'assertEqual(o.IDM1(%s), "IDM1-%d")\n' % (args, i)
                    line += 'assertEqual(c.IDM1(o,%s), "IDM1-%d")\n' % (args, i)
                    line += 'assertEqual(o.SDM1(%s), "SDM1-%d")\n' % (args, i)
                    line += 'assertEqual(c.SDM1(%s), "SDM1-%d")\n' % (args, i)
                else:
                    line += 'assertEqual(o.IDM1(%s), "IDM1-x")\n' % (args)
                    line += 'assertEqual(c.IDM1(o,%s), "IDM1-x")\n' % (args)
                    line += 'assertEqual(o.SDM1(%s), "SDM1-x")\n' % (args)
                    line += 'assertEqual(c.SDM1(%s), "SDM1-x")\n' % (args)
                    
            #print line
            exec(line, globals(), locals())
        
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in [2]:
                    line += 'assertEqual(o.IDM4(%s), "IDM4-%d")\n' % (args, i)
                    line += 'assertEqual(c.IDM4(o,%s), "IDM4-%d")\n' % (args, i)
                    line += 'assertEqual(o.SDM4(%s), "SDM4-%d")\n' % (args, i)
                    line += 'assertEqual(c.SDM4(%s), "SDM4-%d")\n' % (args, i)
                else:
                    line += 'assertEqual(o.IDM4(%s), "IDM4-x")\n' % (args)
                    line += 'assertEqual(c.IDM4(o,%s), "IDM4-x")\n' % (args)
                    line += 'assertEqual(o.SDM4(%s), "SDM4-x")\n' % (args)
                    line += 'assertEqual(c.SDM4(%s), "SDM4-x")\n' % (args)
                
            #print line
            exec(line, globals(), locals())

            ## 3
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in range(5):
                    line += 'assertEqual(o.IDM2(%s), "IDM2-%d")\n' % (args, i)
                    line += 'assertEqual(c.IDM2(o,%s), "IDM2-%d")\n' % (args, i)
                else:
                    line += 'try: o.IDM2(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                    line += 'try: c.IDM2(o, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
            #print line
            exec(line, globals(), locals())
        
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in range(6):
                    line += 'assertEqual(o.SDM2(%s), "SDM2-%d")\n' % (args, i)
                    line += 'assertEqual(c.SDM2(%s), "SDM2-%d")\n' % (args, i)
                else:
                    line += 'try: o.SDM2(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                    line += 'try: c.SDM2(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)

            #print line
            exec(line, globals(), locals())
        
            ## 4
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in [0, 5]:
                    line += 'assertEqual(o.IDM5(%s), "IDM5-%d")\n' % (args, i)
                    line += 'assertEqual(c.IDM5(o,%s), "IDM5-%d")\n' % (args, i)
                else:
                    line += 'try: o.IDM5(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                    line += 'try: c.IDM5(o, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)

            #print line
            exec(line, globals(), locals())

            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in [0, 6]:
                    line += 'assertEqual(o.SDM5(%s), "SDM5-%d")\n' % (args, i)
                    line += 'assertEqual(c.SDM5(%s), "SDM5-%d")\n' % (args, i)
                else:
                    line += 'try: o.SDM5(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)
                    line += 'try: c.SDM5(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n' % (args)

            #print line
            exec(line, globals(), locals())

            ## 5
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in range(5):
                    line += 'assertEqual(o.IDM3(%s), "IDM3-%d")\n' % (args, i)
                    line += 'assertEqual(c.IDM3(o,%s), "IDM3-%d")\n' % (args, i)
                else:
                    line += 'self.assertEqual(o.IDM3(%s), "IDM3-x")\n' % (args)
                    line += 'self.assertEqual(c.IDM3(o,%s), "IDM3-x")\n' % (args)
                
            #print line
            exec(line, globals(), locals())
        
            line = ""
            for i in range(7):
                args = ",".join(['1'] * i)
                if i in range(6):
                    line += 'assertEqual(o.SDM3(%s), "SDM3-%d")\n' % (args, i)
                    line += 'assertEqual(c.SDM3(%s), "SDM3-%d")\n' % (args, i)
                else:
                    line += 'assertEqual(o.SDM3(%s), "SDM3-x")\n' % (args)
                    line += 'assertEqual(c.SDM3(%s), "SDM3-x")\n' % (args)
                
            #print line
            exec(line, globals(), locals())

            ############ OptimizedFunctionN ############
            line = ""
            for i in range(6):
                args = ",".join(['1'] * i)
                line +=  'assertEqual(o.IPM0(%s), "IPM0-%d")\n' % (args, i)
                line +=  'assertEqual(o.SPM0(%s), "SPM0-%d")\n' % (args, i)
                line +=  'assertEqual(c.IPM0(o,%s), "IPM0-%d")\n' % (args, i)
                line +=  'assertEqual(c.SPM0(%s), "SPM0-%d")\n' % (args, i)
                
                line +=  'assertEqual(o.SPM1(0,%s), "SPM1-%d")\n' % (args, i)
                line +=  'assertEqual(o.IPM1(0,%s), "IPM1-%d")\n' % (args, i)
                line +=  'assertEqual(c.IPM1(o, 0,%s), "IPM1-%d")\n' % (args, i)
                line +=  'assertEqual(c.SPM1(0,%s), "SPM1-%d")\n' % (args, i)

            #print line
            exec(line, globals(), locals())
            
        class DispatchAgain2(DispatchAgain): pass
        
        testfunctionhelper(DispatchAgain, DispatchAgain())
        testfunctionhelper(DispatchAgain2, DispatchAgain2())
        
        self.assertEqual(type(BindTest.ReturnTest('char')), System.Char)
        self.assertEqual(type(BindTest.ReturnTest('null')), type(None))
        self.assertEqual(type(BindTest.ReturnTest('object')), object)
        if not is_posix and not is_netcoreapp:
            self.assertTrue(repr(BindTest.ReturnTest("com")).startswith('<System.__ComObject'))

    def test_multicall_generator(self):
        import clr
        from IronPythonTest import Dispatch, DispatchHelpers, MultiCall
        c = MultiCall()

        def AllEqual(exp, meth, passins):
            for arg in passins:
                #print meth, arg
                self.assertEqual(meth(*arg), exp)

        def AllAssert(type, meth, passins):
            for arg in passins:
                #print meth, arg
                self.assertRaises(type, meth, arg)

        import sys
        maxint = sys.maxsize
        import System
        maxlong1 = System.Int64.MaxValue
        maxlong2 = int(str(maxlong1))

        class MyInt(int):
            def __repr__(self):
                return "MyInt(%s)" % super(MyInt, self).__repr__()

        myint = MyInt(10)

        #############################################################################################
        #        public int M0(int arg) { return 1; }
        #        public int M0(long arg) { return 2; }

        func = c.M0
        AllEqual(1, func, [(0,), (1,), (maxint,), (myint,)])
        AllEqual(2, func, [(maxint + 1,), (-maxint-10,), (10,)])
        AllAssert(TypeError, func, [
                                    (-10.2,),
                                    (1+2j,),
                                    ("10",),
                                    (System.Byte.Parse("2"),),
                    ])
        
        #############################################################################################
        #        public int M1(int arg) { return 1; }
        #        public int M1(long arg) { return 2; }
        #        public int M1(object arg) { return 3; }

        func = c.M1
        AllEqual(1, func, [
                            (0,),
                            (1,),
                            (maxint,),
                            (System.Byte.Parse("2"),),
                            (myint, ),
                            #(10L,),
                            #(-1234.0,),
                ])
        AllEqual(2, func, [
                            #(maxint + 1,),
                            #(-maxint-10,),
                            ])
        AllEqual(3, func, [(-10.2,), (1+2j,), ("10",),])

        #############################################################################################
        #        public int M2(int arg1, int arg2) { return 1; }
        #        public int M2(long arg1, int arg2) { return 2; }
        #        public int M2(int arg1, long arg2) { return 3; }
        #        public int M2(long arg1, long arg2) { return 4; }
        #        public int M2(object arg1, object arg2) { return 5; }
        
        func = c.M2
        AllEqual(1, func, [
            (0, 0), (1, maxint), (maxint, 1), (maxint, maxint),
            #(10L, 0),
            ])
        
        AllEqual(2, func, [
            #(maxint+1, 0),
            #(maxint+10, 10),
            #(maxint+10, 10L),
            #(maxlong1, 0),
            #(maxlong2, 0),
            ])

        AllEqual(3, func, [
            #(0, maxint+1),
            #(10, maxint+10),
            #(10L, maxint+10),
            ])
        
        AllEqual(4, func, [
            #(maxint+10, maxint+1),
            #(-maxint-10, maxint+10),
            #(-maxint-10L, maxint+100),
            #(maxlong1, maxlong1),
            #(maxlong2, maxlong1),
            ])
        
        AllEqual(5, func, [
            (maxlong1 + 1, 1),
            (maxlong2 + 1, 1),
            (maxint, maxlong1 + 10),
            (maxint, maxlong2 + 10),
            (1, "100L"),
            (10.2, 1),
            ])
        
        #############################################################################################
        #        public int M4(int arg1, int arg2, int arg3, int arg4) { return 1; }
        #        public int M4(object arg1, object arg2, object arg3, object arg4) { return 2; }
        
        one = [t.Parse("5") for t in [System.Byte, System.SByte, System.UInt16, System.Int16, System.UInt32, System.Int32,
                System.UInt32, System.Int32, System.UInt64, System.Int64,
                System.Char, System.Decimal, System.Single, System.Double] ]
        
        one.extend([True, False, 5, DispatchHelpers.Color.Red ])
        
        two = [t.Parse("5.5") for t in [ System.Decimal, System.Single, System.Double] ]
        
        two.extend([None, "5", "5.5", maxint * 2, ])
        
        together = []
        together.extend(one)
        together.extend(two)
        
        ignore = '''
        for a1 in together:
            for a2 in together:
                for a3 in together:
                    for a4 in together:
                        # print a1, a2, a3, a4, type(a1), type(a1), type(a2), type(a3), type(a4)
                        if a1 in two or a2 is two or a3 in two or a4 in two:
                            self.assertEqual(c.M4(a1, a2, a3, a4), 2)
                        else :
                            self.assertEqual(c.M4(a1, a2, a3, a4), 1)
        '''

        #############################################################################################
        #        public int M5(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 1; }
        #        public int M5(DispatchHelpers.D arg1, DispatchHelpers.B args) { return 2; }
        #        public int M5(object arg1, object args) { return 3; }
        b = DispatchHelpers.B()
        d = DispatchHelpers.D()
        
        func = c.M5
        
        AllEqual(1, func, [(b, b), (b, d)])
        AllEqual(2, func, [(d, b), (d, d)])
        AllEqual(3, func, [(1, 2)])
        
        #############################################################################################
        #        public int M6(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 1; }
        #        public int M6(DispatchHelpers.B arg1, DispatchHelpers.D args) { return 2; }
        #        public int M6(object arg1, DispatchHelpers.D args) { return 3; }
        
        func = c.M6

        AllEqual(1, func, [(b, b), (d, b)])
        AllEqual(2, func, [(b, d), (d, d)])
        AllEqual(3, func, [(1, d), (6, d)])
        AllAssert(TypeError, func, [(1,1), (None, None), (None, d), (3, b)])
        
        #############################################################################################
        #        public int M7(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 1; }
        #        public int M7(DispatchHelpers.B arg1, DispatchHelpers.D args) { return 2; }
        #        public int M7(DispatchHelpers.D arg1, DispatchHelpers.B args) { return 3; }
        #        public int M7(DispatchHelpers.D arg1, DispatchHelpers.D args) { return 4; }

        func = c.M7
        AllEqual(1, func, [(b, b)])
        AllEqual(2, func, [(b, d)])
        AllEqual(3, func, [(d, b)])
        AllEqual(4, func, [(d, d)])
        AllAssert(TypeError, func, [(1,1), (None, None), (None, d)])

        #############################################################################################
        #        public int M8(int arg1, int arg2) { return 1;}
        #        public int M8(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 2; }
        #        public int M8(object arg1, object arg2) { return 3; }
        
        func = c.M8
        AllEqual(1, func, [(1, 2), ]) #(maxint, 2L)])
        AllEqual(2, func, [(b, b), (b, d), (d, b), (d, d)])
        AllEqual(3, func, [(5.1, b), (1, d), (d, 1), (d, maxlong2), (maxlong1, d), (None, 3), (3, None)])

        #############################################################################################
        # public static int M92(out int i, out int j, out int k, bool boolIn)
        self.assertEqual(Dispatch.M92(True), (4, 1,2,3))
        self.assertEqual(Dispatch.Flag, 192)

        #############################################################################################
        # public int M93(out int i, out int j, out int k, bool boolIn)
        self.assertEqual(Dispatch().M93(True), (4, 1,2,3))
        self.assertEqual(Dispatch.Flag, 193)

        #############################################################################################
        # public int M94(out int i, out int j, bool boolIn, out int k)
        self.assertEqual(Dispatch().M94(True), (4, 1,2,3))
        self.assertEqual(Dispatch.Flag, 194)

        #############################################################################################
        # public static int M95(out int i, out int j, bool boolIn, out int k)
        self.assertEqual(Dispatch.M95(True), (4, 1,2,3))
        self.assertEqual(Dispatch.Flag, 195)

        #############################################################################################
        # public static int M96(out int x, out int j, params int[] extras)
        self.assertEqual(Dispatch.M96(), (0, 1,2))
        self.assertEqual(Dispatch.Flag, 196)
        self.assertEqual(Dispatch.M96(1,2), (3, 1,2))
        self.assertEqual(Dispatch.Flag, 196)
        self.assertEqual(Dispatch.M96(1,2,3), (6, 1,2))
        self.assertEqual(Dispatch.Flag, 196)

        #############################################################################################
        # public int M97(out int x, out int j, params int[] extras)
        self.assertEqual(Dispatch().M97(), (0, 1,2))
        self.assertEqual(Dispatch.Flag, 197)
        self.assertEqual(Dispatch().M97(1,2), (3, 1,2))
        self.assertEqual(Dispatch.Flag, 197)
        self.assertEqual(Dispatch().M97(1,2,3), (6, 1,2))
        self.assertEqual(Dispatch.Flag, 197)


        #############################################################################################
        # public void M98(string a, string b, string c, string d, out int x, ref Dispatch di)
        a = Dispatch()
        x = a.M98('1', '2', '3', '4', a)
        self.assertEqual(x[0], 10)
        self.assertEqual(x[1], a)
        # doc for this method should have the out & ref params as return values
        self.assertEqual(a.M98.__doc__, 'M98(self: Dispatch, a: str, b: str, c: str, d: str, di: Dispatch) -> (int, Dispatch)%s' % os.linesep)
        
        # call type.InvokeMember on String.ToString - all methods have more arguments than max args.
        res = clr.GetClrType(str).InvokeMember('ToString',
                                                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.InvokeMethod,
                                                None,
                                                'abc',
                                                ())
        self.assertEqual(res, 'abc')


    def test_missing_generic_args(self):
        """verify calling a generic method w/o args throws a reasonable exception"""
        import System
        #TODO specify clearly which exception is appropriate here
        self.assertRaises(Exception, System.Collections.Generic.List)

    def test_explicit(self):
        """verify calls to explicit interface implementations"""
        from IronPythonTest import ExplicitTestArg, ExplicitTest, IExplicitTest1, IExplicitTest2, IExplicitTest3, IExplicitTest4
        x = ExplicitTest()
        self.assertTrue(not hasattr(x, "A"))
        try:
            x.A()
        except AttributeError:
            pass
        else:
            self.fail("Expected AttributeError, got none")
            
        self.assertEqual(x.B(), "ExplicitTest.B")
        
        self.assertTrue(not hasattr(x, "C"))
        try:
            x.C()
        except AttributeError:
            pass
        else:
            self.fail("Expected AttributeError, got none")
        
        self.assertEqual(x.D(), "ExplicitTest.D")
        
        self.assertEqual(IExplicitTest1.A(x), "ExplicitTest.IExplicitTest1.A")
        self.assertEqual(IExplicitTest1.B(x), "ExplicitTest.IExplicitTest1.B")
        self.assertEqual(IExplicitTest1.C(x), "ExplicitTest.IExplicitTest1.C")
        self.assertEqual(IExplicitTest1.D(x), "ExplicitTest.D")
        self.assertEqual(IExplicitTest2.A(x), "ExplicitTest.IExplicitTest2.A")
        self.assertEqual(IExplicitTest2.B(x), "ExplicitTest.B")
        
        x = ExplicitTestArg()
        try:
            x.M()
        except AttributeError:
            pass
        else:
            self.fail("Expected AttributeError, got none")

        self.assertEqual(IExplicitTest3.M(x), 3)
        self.assertEqual(IExplicitTest4.M(x, 7), 4)


    def test_security_crypto(self):
        import System
        if is_netcoreapp:
            import clr
            clr.AddReference("System.Security.Cryptography.Algorithms")
            self.assertTrue(issubclass(type(System.Security.Cryptography.MD5.Create()),
                    System.Security.Cryptography.MD5))
        else:
            self.assertEqual(type(System.Security.Cryptography.MD5.Create("MD5")),
                    System.Security.Cryptography.MD5CryptoServiceProvider)
            self.assertEqual(type(System.Security.Cryptography.MD5.Create()),
                    System.Security.Cryptography.MD5CryptoServiceProvider)

    def test_array_error_message(self):
        import System
        from IronPythonTest import BinderTest
        
        x = BinderTest.CNoOverloads()
        self.assertRaisesMessage(TypeError, 'expected Array[int], got Array[Byte]', x.M500, System.Array[System.Byte]([1,2,3]))

    def test_max_args(self):
        """verify the correct number of max args are reported, this may need to be updated if file ever takes more args"""
        self.assertRaisesRegex(TypeError, '.*takes at most 4 arguments.*', file, 2, 3, 4, 5, 6, 7, 8, 9)


    def test_enumerator_conversions(self):
        from IronPythonTest import ConversionDispatch
        cd = ConversionDispatch()
        self.assertRaises(TypeError, cd.StringEnumerator, 'abc')
        for seq in ((2,3,4), [2,3,4]):
            self.assertRaises(TypeError, cd.ObjectEnumerator, seq)
            self.assertRaises(TypeError, cd.NonGenericEnumerator, seq)

    def test_property_conversions(self):
        from IronPythonTest import Dispatch
        d = Dispatch()
        d.P01 = 2.0
        self.assertEqual(d.P01, 2.0)


run_test(__name__)
