# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Try different python calls to clr method with different signatures.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ArgumentsTest(IronPythonTestCase):
    def setUp(self):
        super(ArgumentsTest, self).setUp()
        self.add_clr_assemblies("methodargs", "typesamples")

        from clr import StrongBox
        from Merlin.Testing.Call import VariousParameters
        self.box_int = StrongBox[int]

        self.o = VariousParameters()

    def test_0_1_args(self):
        import System
        from Merlin.Testing import Flag

        # public void M100() { Flag.Set(10); }
        f = self.o.M100
        f()
        self.assertRaisesMessage(TypeError, 'M100() takes no arguments (1 given)', lambda: f(1))
        f(*())
        self.assertRaisesMessage(TypeError, 'M100() takes no arguments (2 given)', lambda: f(*(1,2)))
        self.assertRaisesMessage(TypeError, 'M100() takes no arguments (1 given)', lambda: f(x = 10))
        self.assertRaisesMessage(TypeError, 'M100() takes no arguments (2 given)',lambda: f(x = 10, y = 20))
        f(**{})
        self.assertRaisesMessage(TypeError, 'M100() takes no arguments (1 given)', lambda: f(**{'x':10}))
        f(*(), **{})

        # public void M200(int arg) { Flag.Set(arg); }
        f = self.o.M200
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (0 given)", lambda: f())
        f(1)
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, 2))
        f(*(1,))
        f(1, *())
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, *(2,)))
        f(arg = 1); self.assertRaises(NameError, lambda: arg)
        f(arg = 1, *())
        f(arg = 1, **{})
        f(**{"arg" : 1})
        f(*(), **{"arg" : 1})
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, arg = 1))
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(arg = 1, *(1,)))
        self.assertRaisesMessage(TypeError, "M200() got an unexpected keyword argument 'other'", lambda: f(other = 1))
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, other = 1))
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(other = 1, arg = 2))
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(arg = 1, other = 2))
        self.assertRaisesMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(arg = 1, **{'arg' : 2})) # msg

        # public void M201([DefaultParameterValue(20)] int arg) { Flag.Set(arg); }
        f = self.o.M201
        f()
        f(1)
        self.assertRaisesMessage(TypeError, 'M201() takes at most 1 argument (2 given)', lambda: f(1, 2))# msg
        f(*())
        f(1, *())
        f(*(1,))
        self.assertRaisesMessage(TypeError, 'M201() takes at most 1 argument (3 given)', lambda: f(1, *(2, 3)))# msg
        self.assertRaisesMessage(TypeError, 'M201() takes at most 1 argument (2 given)', lambda: f(*(1, 2)))# msg
        f(arg = 1)
        f(arg = 1, *())
        f(arg = 1, **{})
        f(**{"arg" : 1})
        f(*(), **{"arg" : 1})
        self.assertRaisesMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(1, arg = 1))# msg
        self.assertRaisesMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(arg = 1, *(1,)))# msg
        self.assertRaisesMessage(TypeError, "M201() got an unexpected keyword argument 'other'", lambda: f(other = 1))
        self.assertRaisesMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(1, other = 1))
        self.assertRaisesMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(**{ "other" : 1, "arg" : 2}))
        self.assertRaisesMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(arg = 1, other = 2))
        self.assertRaisesMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(arg1 = 1, other = 2))
        self.assertRaisesMessage(TypeError, "M201() got an unexpected keyword argument 'arg1'", lambda: f(**{ "arg1" : 1}))

        # public void M202(params int[] arg) { Flag.Set(arg.Length); }
        f = self.o.M202
        f()
        f(1)
        f(1,2)
        f(*())
        f(1, *(), **{})
        f(1, *(2, 3))
        f(*(1, 2, 3, 4))
        self.assertRaisesMessage(TypeError, "M202() got an unexpected keyword argument 'arg'", lambda: f(arg = 1))# msg
        self.assertRaisesMessage(TypeError, "M202() takes at least 0 arguments (2 given)", lambda: f(1, arg = 2))# msg
        self.assertRaisesMessage(TypeError, "M202() got an unexpected keyword argument 'arg'", lambda: f(**{'arg': 3}))# msg
        self.assertRaisesMessage(TypeError, "M202() got an unexpected keyword argument 'other'", lambda: f(**{'other': 4}))

        ar_int = System.Array[System.Int32](3)
        f(ar_int); Flag.Check(3)

        # public void M203([ParamDictionaryAttribute] IDictionary<object, object> arg) { Flag.Set(arg.Count); }
        f = self.o.M203
        f()
        self.assertRaisesMessage(TypeError, "M203() takes no arguments (1 given)", lambda: f(1))
        self.assertRaisesMessage(TypeError, "M203() takes no arguments (1 given)", lambda: f({'a':1}))
        f(a=1)
        f(a=1, b=2)
        f(**{})
        f(**{'a':2, 'b':3})
        f(a=1, **{'b':2, 'c':5})
        self.assertRaisesMessage(TypeError, "M203() got multiple values for keyword argument 'a'", lambda: f(a=1, **{'a':2, 'c':5}))
        self.assertRaisesMessage(TypeError, "M203() takes no arguments (3 given)", lambda: f(*(1,2,3))) # msg: no positional arguments
        dict_obj = System.Collections.Generic.Dictionary[System.Object, System.Object]()
        self.assertRaisesMessage(TypeError, "M203() takes no arguments (1 given)", lambda: f(dict_obj)) # msg: no positional arguments

        # public void M204(params object[] arg)
        f = self.o.M204

        ar_obj = System.Array[System.Object](3)
        f(ar_obj); Flag.Check(3)
        f(ar_obj, ar_obj); Flag.Check(2)
        f(ar_int); Flag.Check(1)

    def test_optional(self):
        import System
        from Merlin.Testing import Flag
        from Merlin.Testing.TypeSample import EnumInt32, SimpleClass, SimpleStruct
        #public void M231([Optional] int arg) { Flag.Set(arg); }  // not reset any
        #public void M232([Optional] bool arg) { Flag<bool>.Set(arg); }
        #public void M233([Optional] object arg) { Flag<object>.Set(arg); }
        #public void M234([Optional] string arg) { Flag<string>.Set(arg); }
        #public void M235([Optional] EnumInt32 arg) { Flag<EnumInt32>.Set(arg); }
        #public void M236([Optional] SimpleClass arg) { Flag<SimpleClass>.Set(arg); }
        #public void M237([Optional] SimpleStruct arg) { Flag<SimpleStruct>.Set(arg); }

        ## testing the passed in value, and the default values
        self.o.M231(12); Flag.Check(12)
        self.o.M231(); Flag.Check(0)

        self.o.M232(True); Flag[bool].Check(True)
        self.o.M232(); Flag[bool].Check(False)

        def t(): pass
        self.o.M233(t); Flag[object].Check(t)
        self.o.M233(); Flag[object].Check(System.Type.Missing.Value)

        self.o.M234("ironpython"); Flag[str].Check("ironpython")
        self.o.M234(); Flag[str].Check(None)

        self.o.M235(EnumInt32.B); Flag[EnumInt32].Check(EnumInt32.B)
        self.o.M235(); Flag[EnumInt32].Check(EnumInt32.A)

        x = SimpleClass(23)
        self.o.M236(x); Flag[SimpleClass].Check(x)
        self.o.M236(); Flag[SimpleClass].Check(None)

        x = SimpleStruct(24)
        self.o.M237(x); Flag[SimpleStruct].Check(x)
        self.o.M237(); self.assertEqual(Flag[SimpleStruct].Value1.Flag, 0)

        ## testing the argument style
        f = self.o.M231

        f(*()); Flag.Check(0)
        f(*(2, )); Flag.Check(2)
        f(arg = 3); Flag.Check(3)
        f(**{}); Flag.Check(0)
        f(*(), **{'arg':4}); Flag.Check(4)

        self.assertRaisesMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(1, 2))  # msg
        self.assertRaisesMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(1, **{'arg': 2}))  # msg
        self.assertRaisesMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(arg = 3, **{'arg': 4}))  # msg
        self.assertRaisesMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(arg = 3, **{'other': 4}))  # msg

    def test_two_args(self):
        from Merlin.Testing import Flag
        #public void M300(int x, int y) { }
        f = self.o.M300
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (0 given)", lambda: f())
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(1))
        f(1, 2)
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, 2, 3))

        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (0 given)", lambda: f(*()))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(*(1,)))
        f(1, *(2,))
        f(*(3, 4))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, *(2, 3)))

        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(y = 1))
        f(y = 2, x = 1)
        self.assertRaisesMessage(TypeError, "M300() got an unexpected keyword argument 'x2'", lambda: f(y = 1, x2 = 2))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(x = 1, y = 1, z = 3))
        #self.assertRaises(SyntaxError, eval, "f(x=1, y=2, y=3)")

        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(**{"x":1}))  # msg
        f(**{"x":1, "y":2})

        # ...

        # mixed
        # positional/keyword
        f(1, y = 2)
        self.assertRaisesMessage(TypeError, "Argument for M300() given by name ('x') and position (1)", lambda: f(2, x = 1))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, y = 1, x = 2)) # msg
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, 2, y = 1)) # msg

        # positional / **
        f(1, **{'y': 2})
        self.assertRaisesMessage(TypeError, "Argument for M300() given by name ('x') and position (1)", lambda: f(2, ** {'x':1}))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, ** {'y':1, 'x':2}))  # msg

        # keyword / *
        f(y = 2, *(1,))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(y = 2, *(1,2)))
        self.assertRaisesMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(y = 2, x = 1, *(3,)))  # msg

        # keyword / **
        f(y = 2, **{'x' : 1})

        #public void M350(int x, params int[] y) { }

        f = self.o.M350
        self.assertRaisesMessage(TypeError, "M350() takes at least 1 argument (0 given)", lambda: f())
        f(1)
        f(1, 2)
        f(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)

        self.assertRaisesMessage(TypeError, "M350() takes at least 1 argument (0 given)", lambda: f(*()))
        f(*(1,))
        f(1, 2, *(3, 4))
        f(1, 2, *())
        f(1, 2, 3, *(4, 5, 6, 7, 8, 9, 10))

        f(x = 1)
        self.assertRaisesMessage(TypeError, "M350() got an unexpected keyword argument 'y'", lambda: f(x = 1, y = 2))

        f(**{'x' : 1})
        self.assertRaisesMessage(TypeError, "M350() got an unexpected keyword argument 'y'", lambda: f(**{'x' : 1, 'y' : 2}))
        self.assertRaisesMessage(TypeError, "Argument for M350() given by name ('x') and position (1)", lambda: f(2, 3, 4, x = 1))

        # TODO: mixed
        f(x = 1)  # check the value

        #public void M351(int x, [ParamDictionary] IDictionary<object, object> y) { Flag<object>.Set(y); }
        f = self.o.M351
        self.assertRaisesMessage(TypeError, "M351() takes exactly 1 argument (0 given)", lambda: f())
        f(1); self.assertEqual(Flag[object].Value1, {})
        f(1, a=3); self.assertEqual(Flag[object].Value1, {'a':3})
        f(1, a=3,b=4); self.assertEqual(Flag[object].Value1, {'a':3, 'b':4})
        f(1, a=3, **{'b':4, 'd':5}); self.assertEqual(Flag[object].Value1, {'a':3, 'b':4, 'd':5})
        f(x=1); self.assertEqual(Flag[object].Value1, {})
        f(**{'x' : 1}); self.assertEqual(Flag[object].Value1, {})

        #public void M352([ParamDictionary] IDictionary<object, object> x, params object[] y) { Flag<object>.Set(x); }

        f=self.o.M352
        f(); self.assertEqual(Flag[object].Value1, {})
        f(1); self.assertEqual(Flag[object].Value1, {})
        f(1,2,3); self.assertEqual(Flag[object].Value1, {})
        f(a=1,b=2); self.assertEqual(Flag[object].Value1, {'a':1, 'b':2})
        f(1,2,3, a=1); self.assertEqual(Flag[object].Value1, {'a':1})
        f(a=1, *(1,2,3)); self.assertEqual(Flag[object].Value1, {'a':1})
        f(*(1,2,3), **{'a':1, 'b':2}); self.assertEqual(Flag[object].Value1, {'a':1, 'b':2})
        f(*(), **{}); self.assertEqual(Flag[object].Value1, {})

    def test_default_values_2(self):
        from Merlin.Testing import Flag
        # public void M310(int x, [DefaultParameterValue(30)]int y) { Flag.Set(x + y); }
        f = self.o.M310
        self.assertRaisesMessage(TypeError, "M310() takes at least 1 argument (0 given)", f)
        f(1); Flag.Check(31)
        f(1, 2); Flag.Check(3)
        self.assertRaisesMessage(TypeError, "M310() takes at most 2 arguments (3 given)", lambda : f(1, 2, 3))

        f(x = 2); Flag.Check(32)
        f(4, y = 5); Flag.Check(9)
        f(y = 7, x = 10); Flag.Check(17)
        f(*(8,)); Flag.Check(38)
        f(*(9, 10)); Flag.Check(19)

        f(1, **{'y':2}); Flag.Check(3)

        # public void M320([DefaultParameterValue(40)] int x, int y) { Flag.Set(x + y); }
        f = self.o.M320
        self.assertRaisesMessage(TypeError, "M320() takes at least 1 argument (0 given)", f)
        f(1); Flag.Check(41)  # !!!
        f(2, 3); Flag.Check(5)
        self.assertRaisesMessage(TypeError, "M320() takes at most 2 arguments (3 given)", lambda : f(1, 2, 3))

        f(y = 2); Flag.Check(42)
        f(y = 2, x = 3); Flag.Check(5)
        f(x = 2, y = 3); Flag.Check(5)
        f(*(1,)); Flag.Check(41)
        f(*(1, 2)); Flag.Check(3)

        self.assertRaisesMessage(TypeError, "Argument for M320() given by name ('x') and position (1)", lambda : f(5, x = 6)) # !!!
        self.assertRaisesMessage(TypeError, "M320() got an unexpected keyword argument 'x'", lambda : f(x = 6)) # !!!
        f(6, y = 7); Flag.Check(13)

        # public void M330([DefaultParameterValue(50)] int x, [DefaultParameterValue(60)] int y) { Flag.Set(x + y); }
        f = self.o.M330
        f(); Flag.Check(110)
        f(1); Flag.Check(61)
        f(1, 2); Flag.Check(3)

        f(x = 1); Flag.Check(61)
        f(y = 2); Flag.Check(52)
        f(y = 3, x = 4); Flag.Check(7)

        f(*(5,)); Flag.Check(65)
        f(**{'y' : 6}); Flag.Check(56)

    def test_3_args(self):
        from Merlin.Testing import Flag
        # public void M500(int x, int y, int z) { Flag.Set(x * 100 + y * 10 + z); }
        f = self.o.M500
        f(1, 2, 3); Flag.Check(123)
        f(y = 1, z = 2, x = 3); Flag.Check(312)
        f(3, *(2, 1)); Flag.Check(321)
        f(1, z = 2, **{'y':3}); Flag.Check(132)
        f(z = 1, **{'x':2, 'y':3}); Flag.Check(231)
        f(1, z = 2, *(3,)); Flag.Check(132)
        self.assertRaisesMessage(TypeError, "Argument for M500() given by name ('y') and position (2)", lambda: f(1, 2, y = 2)) # msg

        # public void M510(int x, int y, [DefaultParameterValue(70)] int z) { Flag.Set(x * 100 + y * 10 + z); }
        f = self.o.M510
        f(1, 2); Flag.Check(120 + 70)
        f(2, y = 1); Flag.Check(210 + 70)

        f(1, 2, 3); Flag.Check(123)
        self.assertRaisesMessage(TypeError, "Argument for M510() given by name ('y') and position (2)", lambda: f(1, 2, y = 2))

        # public void M520(int x, [DefaultParameterValue(80)]int y, int z) { Flag.Set(x * 100 + y * 10 + z); }
        f = self.o.M520
        f(1, 2); Flag.Check(102 + 800)
        f(2, z = 1); Flag.Check(201 + 800)
        f(z=1, **{'x': 2}); Flag.Check(201 + 800)
        f(2, *(1,)); Flag.Check(201 + 800)

        f(1, z = 2, y = 3); Flag.Check(132)
        f(1, 2, 3); Flag.Check(123)

        # public void M530([DefaultParameterValue(90)]int x, int y, int z) { Flag.Set(x * 100 + y * 10 + z); }
        f = self.o.M530
        f(1, 2); Flag.Check(12 + 9000)
        f(3, z = 4); Flag.Check(34 + 9000)
        f(*(5,), **{'z':6}); Flag.Check(56 + 9000)
        self.assertRaisesMessage(TypeError, "Argument for M530() given by name ('y') and position (1)", lambda: f(2, y = 2)) # !!!
        self.assertRaisesMessage(TypeError, "Argument for M530() given by name ('y') and position (2)", lambda: f(1, 2, y = 2))

        f(1, 2, 3); Flag.Check(123)

        # public void M550(int x, int y, params int[] z) { Flag.Set(x * 100 + y * 10 + z.Length); }
        f = self.o.M550
        f(1, 2); Flag.Check(120)
        f(1, 2, 3); Flag.Check(121)
        f(1, 2, 3, 4, 5, 6, 7, 8, 9, 10); Flag.Check(128)

        f(1, 2, *()); Flag.Check(120)
        f(1, 2, *(3,)); Flag.Check(121)

        # bug 311155
        ##def  f(x, y, *z): print x, y, z
        f(1, y = 2); Flag.Check(120)
        f(x = 2, y = 3); Flag.Check(230)
        f(1, y = 2, *()); Flag.Check(120)

        self.assertRaisesMessage(TypeError, "Argument for M550() given by name ('y') and position (2)", lambda: f(1, y = 2, *(3, )))
        self.assertRaisesMessage(TypeError, "Argument for M550() given by name ('y') and position (2)", lambda: f(y = 2, x = 3, *(3, 4)))
        self.assertRaisesMessage(TypeError, "Argument for M550() given by name ('y') and position (2)", lambda: f(1, *(2, 3), **{'y': 4}))
        self.assertRaisesMessage(TypeError, "Argument for M550() given by name ('y') and position (2)", lambda: f(*(4, 5, 6), **{'y':7}))
        self.assertRaises(TypeError, r"^Argument for M550\(\) given by name \('[xy]'\) and position \([12]\)$", lambda: f(*(1, 2, 0, 1), **{'y':3, 'x':4}))
        #f(1, 2, 3, z=4, y=5, x=6) # FIXME: M550() takes at least 2 arguments (6 given) => e.g. Argument for M550() given by name ('x') and position (1)
        #f(1, 2, **{'z':3, 'y':4}) # FIXME: M550() takes at least 2 arguments (4 given) => e.g. Argument for M550() given by name ('y') and position (2)
        #f(*(1, 2, 0, 1), **{'z':3, 'y':4}) # FIXME: M550() takes at least 2 arguments (6 given) => e.g. Argument for M550() given by name ('x') and position (1)
        #f(*(1, 2, 0, 1), **{'z':3, 'x':4}) # FIXME: M550() takes at least 2 arguments (6 given) => e.g. Argument for M550() given by name ('x') and position (1)

        # public void M560(int x, [ParamDictionary] IDictionary<string, int> y, params int[] z) { Flag.Set(x * 100 + y.Count * 10 + z.Length); }
        f = self.o.M560
        f(2, 3, 4, a=5, b=6, c=7); Flag.Check(2 * 100 + 3 * 10 + 2)
        self.assertRaisesMessage(TypeError, "Argument for M560() given by name ('x') and position (1)", lambda: f(2, 3, 4, x=4, b=6, c=7))
        f(2, 3, 4, y=5, b=6, c=7); Flag.Check(2 * 100 + 3 * 10 + 2)
        f(2, 3, 4, z=5, b=6, c=7); Flag.Check(2 * 100 + 3 * 10 + 2)

    def test_many_args(self):
        from Merlin.Testing import Flag
        #public void M650(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6, int arg7, int arg8, int arg9, int arg10) { }
        f = self.o.M650
        expect = "1 2 3 4 5 6 7 8 9 10"

        f(1, 2, 3, 4, 5, 6, 7, 8, 9, 10); Flag[str].Check(expect)

        #def f(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10): print arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10
        f(arg2 = 2, arg3 = 3, arg4 = 4, arg5 = 5, arg6 = 6, arg7 = 7, arg8 = 8, arg9 = 9, arg10 = 10, arg1 = 1); Flag[str].Check(expect)
        f(1, 2, arg6 = 6, arg7 = 7, arg8 = 8, arg3 = 3, arg4 = 4, arg5 = 5, arg9 = 9, arg10 = 10); Flag[str].Check(expect)

        #self.assertRaisesMessage(TypeError, "M650() got multiple values for keyword argument 'arg5'", lambda: f(1, 2, 3, arg5 = 5, *(4, 6, 7, 8, 9, 10)))
        #self.assertRaisesMessage(TypeError, "M650() got multiple values for keyword argument 'arg1'", lambda: f(arg3 = 3, arg2 = 2, arg1 = 1, *(4, 5, 6, 7, 9, 10), **{'arg8': 8}))
        #self.assertRaisesMessage(TypeError, "M650() got multiple values for keyword argument 'arg3'", lambda: f(1, 2, 4, 5, 6, 7, 8, 9, 10, **{'arg3' : 3}))

        f(1, 2, 3, arg9 = 9, arg10 = 10, *(4, 5, 6, 7, 8)); # Flag[str].Check(expect)  # bug 311195
        f(1, 2, 3, arg10 = 10, *(4, 5, 6, 7, 8), ** {'arg9': 9}); # Flag[str].Check(expect) # bug 311195

        # msg below, should it be pos 4? (positon of the splatee) but 5 matches CPython
        self.assertRaisesMessage(TypeError, "Argument for M650() given by name ('arg5') and position (5)", lambda: f(2, arg5 = 5, arg10 = 10, *(3, 4, 6, 7, 9), **{'arg8': 8, 'arg1': 1}))

        #public void M700(int arg1, string arg2, bool arg3, object arg4, EnumInt16 arg5, SimpleClass arg6, SimpleStruct arg7) { }

        f = self.o.M700

    def test_special_name(self):
        from Merlin.Testing import Flag
        #// keyword argument name, or **dict style
        #public void M800(int True) { }
        #public void M801(int def) { }

        f = self.o.M800
        self.assertRaises(SyntaxError, eval, "f(True=9)")
        f(**{"True": 19}); Flag.Check(19)

        self.assertEqual(str(True), "True")

        f = self.o.M801
        self.assertRaises(SyntaxError, eval, "f(def = 3)")
        f(**{"def": 8}); Flag.Check(8)

    def test_1_byref_arg(self):
        from Merlin.Testing.Call import ByRefParameters
        obj = ByRefParameters()

        #public void M100(ref int arg) { arg = 1; }
        f = obj.M100

        self.assertEqual(f(2), 1)
        self.assertEqual(f(arg = 3), 1)
        self.assertEqual(f(*(4,)), 1)
        self.assertEqual(f(**{'arg': 5}), 1)

        x = self.box_int(6); self.assertEqual(f(x), None); self.assertEqual(x.Value, 1)
        x = self.box_int(7); f(arg = x); self.assertEqual(x.Value, 1)
        x = self.box_int(8); f(*(x,)); self.assertEqual(x.Value, 1)
        x = self.box_int(9); f(**{'arg':x}); self.assertEqual(x.Value, 1)

        #public void M120(out int arg) { arg = 2; }
        f = obj.M120
        self.assertEqual(f(), 2)
        self.assertRaises(TypeError, lambda: f(1))  # bug 311218

        x = self.box_int(); self.assertEqual(f(x), None); self.assertEqual(x.Value, 2)
        x = self.box_int(7); f(arg = x); self.assertEqual(x.Value, 2)
        x = self.box_int(8); f(*(x,)); self.assertEqual(x.Value, 2)
        x = self.box_int(9); f(**{'arg':x}); self.assertEqual(x.Value, 2)

    def test_2_byref_args(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import ByRefParameters
        obj = ByRefParameters()

        #public void M200(int arg1, ref int arg2) { Flag.Set(arg1 * 10 + arg2); arg2 = 10; }
        f = obj.M200
        self.assertEqual(f(1, 2), 10); Flag.Check(12)
        self.assertEqual(f(3, arg2 = 4), 10); Flag.Check(34)
        self.assertEqual(f(arg2 = 6, arg1 = 5), 10); Flag.Check(56)
        self.assertEqual(f(*(7, 8)), 10); Flag.Check(78)
        self.assertEqual(f(9, *(1,)), 10); Flag.Check(91)

        x = self.box_int(5); self.assertEqual(f(1, x), None); self.assertEqual(x.Value, 10); Flag.Check(15)
        x = self.box_int(6); f(2, x); self.assertEqual(x.Value, 10); Flag.Check(26)
        x = self.box_int(7); f(3, *(x,)); self.assertEqual(x.Value, 10); Flag.Check(37)
        x = self.box_int(8); f(**{'arg1': 4, 'arg2' : x}); self.assertEqual(x.Value, 10); Flag.Check(48)

        #public void M201(ref int arg1, int arg2) { Flag.Set(arg1 * 10 + arg2); arg1 = 20; }
        f = obj.M201
        self.assertEqual(f(1, 2), 20)
        x = self.box_int(2); f(x, *(2,)); self.assertEqual(x.Value, 20); Flag.Check(22)

        #public void M202(ref int arg1, ref int arg2) { Flag.Set(arg1 * 10 + arg2); arg1 = 30; arg2 = 40; }
        f = obj.M202
        self.assertEqual(f(1, 2), (30, 40))
        self.assertEqual(f(arg2 = 1, arg1 = 2), (30, 40)); Flag.Check(21)

        self.assertRaisesMessage(TypeError, "expected int, got StrongBox[int]", lambda: f(self.box_int(3), 4))  # bug 311239
        x = self.box_int(3)
        y = self.box_int(4)
        f(arg2 = y, *(x,)); Flag.Check(34) # bug 311169
        self.assertRaisesMessage(TypeError, "Argument for M202() given by name ('arg1') and position (1)", lambda: f(arg1 = x, *(y,)))

        # just curious
        x = y = self.box_int(5)
        f(x, y); self.assertEqual(x.Value, 40); self.assertEqual(y.Value, 40); Flag.Check(55)

    def test_2_out_args(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import ByRefParameters
        obj = ByRefParameters()

        #public void M203(int arg1, out int arg2) { Flag.Set(arg1 * 10); arg2 = 50; }
        f = obj.M203
        self.assertEqual(f(1), 50)
        self.assertEqual(f(*(2,)), 50)
        self.assertRaises(TypeError, lambda: f(1, 2))  # bug 311218

        x = self.box_int(4)
        f(1, x); self.assertEqual(x.Value, 50)

        #public void M204(out int arg1, int arg2) { Flag.Set(arg2); arg1 = 60; }
        # TODO

        #public void M205(out int arg1, out int arg2) { arg1 = 70; arg2 = 80; }
        f = obj.M205
        self.assertEqual(f(), (70, 80))
        self.assertRaisesMessage(TypeError, "M205() takes at most 2 arguments (1 given)", lambda: f(1)) # FIXME: msg
        self.assertRaisesMessage(TypeError, "expected StrongBox[int], got int", lambda: f(1, 2))

        self.assertRaisesMessage(TypeError, "M205() takes at most 2 arguments (1 given)", lambda: f(arg2 = self.box_int(2))) # FIXME: msg
        self.assertRaisesMessage(TypeError, "M205() takes at most 2 arguments (1 given)", lambda: f(arg1 = self.box_int(2))) # FIXME: msg

        for l in [
            lambda: f(*(x, y)),
            lambda: f(x, y, *()),
            lambda: f(arg2 = y, arg1 = x, *()),
            lambda: f(x, arg2 = y, ),
            lambda: f(x, **{"arg2":y})
                ]:
            x, y = self.box_int(1), self.box_int(2)
            #print l
            l()
            self.assertEqual(x.Value, 70)
            self.assertEqual(y.Value, 80)


        #public void M206(ref int arg1, out int arg2) { Flag.Set(arg1 * 10); arg1 = 10; arg2 = 20; }
        f = obj.M206
        self.assertEqual(f(1), (10, 20))
        self.assertEqual(f(arg1 = 2), (10, 20))
        self.assertEqual(f(*(3,)), (10, 20))
        self.assertRaises(TypeError, lambda: f(self.box_int(5)))

        x, y = self.box_int(4), self.box_int(5)
        f(x, y); self.assertEqual(x.Value, 10); self.assertEqual(y.Value, 20);

        #public void M207(out int arg1, ref int arg2) { Flag.Set(arg2); arg1 = 30; arg2 = 40; }

        f = obj.M207
        self.assertEqual(f(1), (30, 40))
        self.assertEqual(f(arg2 = 2), (30, 40)); Flag.Check(2)
        self.assertRaises(TypeError, lambda: f(1, 2))
        self.assertRaises(TypeError, lambda: f(arg2 = 1, arg1 = 2))

        for l in [
                lambda: f(x, y),
                lambda: f(arg2 = y, arg1 = x),
                lambda: f(x, *(y,)),
                lambda: f(*(x, y,)),
                #lambda: f(arg1 = x, *(y,)),
                lambda: f(arg1 = x, **{'arg2': y}),
                ]:
            x, y = self.box_int(1), self.box_int(2)
            #print l
            l()
            self.assertEqual(x.Value, 30)
            self.assertEqual(y.Value, 40)
            Flag.Check(2)

    def test_splatting_errors(self):
        # public void M200(int arg) { Flag.Reset(); Flag.Set(arg); }
        f = self.o.M200

        self.assertRaisesMessage(TypeError, "M200() argument after * must be a sequence, not NoneType", lambda: f(*None))
        self.assertRaisesMessage(TypeError, "expected int, got str", lambda: f(*'x'))
        self.assertRaisesMessage(TypeError, "M200() argument after * must be a sequence, not int", lambda: f(*1))

run_test(__name__)
