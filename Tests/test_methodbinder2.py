# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# PART 2. how IronPython choose the overload methods
#

from iptest import IronPythonTestCase, is_cli, big, run_test, skipUnlessIronPython
if is_cli:
    from iptest.type_util import array_int, array_byte, array_object, myint, mystr, types

class PT_int_old:
    def __int__(self): return 200

class PT_int_new(object):
    def __int__(self): return 300

def _self_defined_method(name): return len(name) == 4 and name[0] == "M"

def _result_pair(s, offset=0):
    fn = s.split()
    val = [int(x[1:]) + offset for x in fn]
    return dict(zip(fn, val))

def _first(s): return _result_pair(s, 0)
def _second(s): return _result_pair(s, 100)

def _merge(*args):
    ret = {}
    for arg in args:
        for (k, v) in arg.items(): ret[k] = v
    return ret

def _my_call(func, arg):
    if isinstance(arg, tuple):
        l = len(arg)
        if l == 0: func()
        elif l == 1: func(arg[0])
        elif l == 2: func(arg[0], arg[1])
        elif l == 3: func(arg[0], arg[1], arg[2])
        elif l == 4: func(arg[0], arg[1], arg[2], arg[3])
        elif l == 5: func(arg[0], arg[1], arg[2], arg[3], arg[4])
        elif l == 6: func(arg[0], arg[1], arg[2], arg[3], arg[4], arg[5])
        else: func(*arg)
    else:
        func(arg)

if is_cli:
    import clr
    import System
    clrRefInt = clr.Reference[System.Int32](0)
    clrRefBigInt = clr.Reference[int](0)
    UInt32Max = System.UInt32.MaxValue
    Byte10   = System.Byte.Parse('10')
    SBytem10 = System.SByte.Parse('-10')
    Int1610  = System.Int16.Parse('10')
    Int16m20 = System.Int16.Parse('-20')
    UInt163  = System.UInt16.Parse('3')

    arrayInt = array_int((10, 20))
    tupleInt = ((10, 20), )
    listInt  = ([10, 20], )
    tupleLong1, tupleLong2  = ((big(10), big(20)), ), ((System.Int64.MaxValue, System.Int32.MaxValue * 2),)
    arrayByte = array_byte((10, 20))
    arrayObj = array_object(['str', 10])

@skipUnlessIronPython()
class MethodBinder2Test(IronPythonTestCase):
    def setUp(self):
        super(MethodBinder2Test, self).setUp()

        self.load_iron_python_test()
        import System
        from IronPythonTest.BinderTest import I, C1, C3

        class PT_I(I): pass

        class PT_C1(C1): pass

        class PT_C3_int(C3):
            def __int__(self): return 1

        class PT_I_int(I):
            def __int__(self): return 100

        self.pt_i = PT_I()
        self.pt_c1 = PT_C1()
        self.pt_i_int = PT_I_int()
        self.pt_int_old = PT_int_old()
        self.pt_int_new = PT_int_new()


    def _try_arg(self, target, arg, mapping, funcTypeError, funcOverflowError, verbose=False):
        '''try the pass-in argument 'arg' on all methods 'target' has.
        mapping specifies (method-name, flag-value)
        funcOverflowError contains method-name, which will cause OverflowError when passing in 'arg'
        '''
        from IronPythonTest.BinderTest import Flag

        if verbose: print(arg, end=' ')
        for funcname in dir(target):
            if not _self_defined_method(funcname) : continue

            if verbose: print(funcname, end=' ')
            func = getattr(target, funcname)

            if funcname in funcOverflowError: expectError = OverflowError
            elif funcname in funcTypeError:  expectError = TypeError
            else: expectError = None

            if isinstance(arg, types.lambdaType):
                arg = arg()
            try:
                _my_call(func, arg)
            except Exception as e:
                if expectError == None:
                    self.fail("unexpected exception %s when func %s with arg %s (%s)\n%s" % (e, funcname, arg, type(arg), func.__doc__))

                if funcname in mapping.keys():  # No exception expected:
                    self.fail("unexpected exception %s when func %s with arg %s (%s)\n%s" % (e, funcname, arg, type(arg), func.__doc__))

                if not isinstance(e, expectError):
                    self.fail("expect '%s', but got '%s' (flag %s) when func %s with arg %s (%s)\n%s" % (expectError, e, Flag.Value, funcname, arg, type(arg), func.__doc__))
            else:
                if not funcname in mapping.keys(): # Expecting exception
                    self.fail("expect %s, but got no exception (flag %s) when func %s with arg %s (%s)\n%s" % (expectError, Flag.Value, funcname, arg, type(arg), func.__doc__))

                left, right = Flag.Value, mapping[funcname]
                if left != right:
                    self.fail("expect %s, but got %s when func %s on arg %s (%s)\n%s" % (right, left, funcname, arg, type(arg), func.__doc__))
                Flag.Value = -99           # reset
        if verbose: print()

    def test_other_concerns(self):
        from IronPythonTest.BinderTest import C1, C3, COtherOverloadConcern, Flag
        target = COtherOverloadConcern()

        # the one asking for Int32 is private
        target.M100(100)
        self.assertEqual(Flag.Value, 200); Flag.Value = 99

        # static / instance
        target.M110(target, 100)
        self.assertEqual(Flag.Value, 110); Flag.Value = 99
        COtherOverloadConcern.M110(100)
        self.assertEqual(Flag.Value, 210); Flag.Value = 99

        self.assertRaises(TypeError, COtherOverloadConcern.M110, target, 100)

        # static / instance 2
        target.M111(100)
        self.assertEqual(Flag.Value, 111); Flag.Value = 99
        COtherOverloadConcern.M111(target, 100)
        self.assertEqual(Flag.Value, 211); Flag.Value = 99

        self.assertRaises(TypeError, target.M111, target, 100)
        self.assertRaises(TypeError, COtherOverloadConcern.M111, 100)

        # statics
        target.M120(target, 100)
        self.assertEqual(Flag.Value, 120); Flag.Value = 99
        target.M120(100)
        self.assertEqual(Flag.Value, 220); Flag.Value = 99

        COtherOverloadConcern.M120(target, 100)
        self.assertEqual(Flag.Value, 120); Flag.Value = 99
        COtherOverloadConcern.M120(100)
        self.assertEqual(Flag.Value, 220); Flag.Value = 99

        # generic
        target.M130(100)
        self.assertEqual(Flag.Value, 130); Flag.Value = 99

        target.M130(100.1234)
        self.assertEqual(Flag.Value, 230); Flag.Value = 99

        target.M130(C1())
        self.assertEqual(Flag.Value, 230); Flag.Value = 99

        target.M130[System.Int32](100)
        self.assertEqual(Flag.Value, 230); Flag.Value = 99

        with self.assertRaises(TypeError):
            target.M130[System.Int32](100.1234)

        target.M130[int](100)
        self.assertEqual(Flag.Value, 230); Flag.Value = 99

        with self.assertRaises(TypeError):
            target.M130[int](100.1234)

        class PT_C3_int(C3):
            def __int__(self): return 1

        # narrowing levels and __int__ conversion
        target.M140(PT_C3_int(), PT_C3_int())
        self.assertEqual(Flag.Value, 140); Flag.Value = 99

    def test_arg_ClrReference(self):
        import clr
        from IronPythonTest.BinderTest import C1, C2, COverloads_ClrReference
        target = COverloads_ClrReference()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (lambda :                           None, _merge(_first('M100 M101 M107 '), _second('M102 M104 M105 M106 ')), 'M103 ', '', ),
            (lambda :    clr.Reference[object](None), _second('M100 M104 M105 M107 '), 'M101 M102 M103 M104 M106 ', '', ),
            (lambda :    clr.Reference[object](None), _second('M100 M104 M105 M107 '), 'M101 M102 M103 M106 ', '', ),
            (lambda : clr.Reference[System.Int32](9), _merge(_first('M100 M102 M103 M104 '), _second('M105 M107 ')), 'M101 M106 ', '', ),
            (lambda :      clr.Reference[bool](True), _merge(_first('M100 M105 '), _second('M101 M102 M104 M107 ')), 'M103 M106 ', '', ),
            (lambda :   clr.Reference[type](complex), _merge(_first('M100 '), _second('M104 M105 M107 ')), 'M101 M102 M103 M106 ', '', ),
            (lambda :        clr.Reference[C1](C1()), _merge(_first('M100 M106 M107 '), _second('M104 M105 ')), 'M101 M102 M103 ', '', ),
            (lambda :        clr.Reference[C1](C2()), _merge(_first('M100 M106 M107 '), _second('M104 M105 ')), 'M101 M102 M103 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_NoArgNecessary(self):
        from IronPythonTest.BinderTest import COverloads_NoArgNecessary
        target = COverloads_NoArgNecessary()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), _merge(_first('M100 M101 M102 M105 '), _second('M103 M104 M106 ')), '', '', ),
            (         100, _merge(_first('M105 M106 '), _second('M101 M102 M103 M104 ')), 'M100 ', '', ),
            (  (100, 200), _second('M102 M104 M105 M106 '), 'M100 M101 M103 ', '', ),
            (   clrRefInt, _merge(_first('M103 M104 '), _second('M100 ')), 'M101 M102 M105 M106 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_NormalArg(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_NormalArg
        target = COverloads_OneArg_NormalArg()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 ', '', ),
            (         100, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 '), '', '', ),
            (  (100, 200), _second('M102 M107 M108 '), 'M100 M101 M103 M104 M105 M106 M109 ', '', ),
            (   clrRefInt, _second('M100 '), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_RefArg(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_RefArg
        target = COverloads_OneArg_RefArg()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 M106 M107 M108 ', '', ),
            (         100, _merge(_first('M100 M101 M103 M105 M108 '), _second('M106 M107 ')), 'M102 M104 ', '', ),
            (  (100, 200), _second('M101 M106 M107 '), 'M100 M102 M103 M104 M105 M108 ', '', ),
            (   clrRefInt, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 '), '', '', ),
            ]:
            self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_NullableArg(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_NullableArg
        target = COverloads_OneArg_NullableArg()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 M106 M107 ', '', ),
            (         100, _merge(_first('M100 M107 '), _second('M101 M102 M103 M104 M105 M106 ')), '', '', ),
            (  (100, 200), _second('M100 M105 M106 '), 'M101 M102 M103 M104 M107 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_TwoArgs(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_TwoArgs
        target = COverloads_OneArg_TwoArgs()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 ', '', ),
            (         100, _second('M100 M101 M102 M103 M104 '), 'M105 ', '', ),
            (  (100, 200), _first('M100 M101 M102 M103 M104 M105 '), '', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_NormalOut(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_NormalOut
        target = COverloads_OneArg_NormalOut()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 ', '', ),
            (         100, _merge(_first('M100 M102 M105 '), _second('M103 M104 ')), 'M101 ', '', ),
            (  (100, 200), _second('M103 M104 '), 'M100 M101 M102 M105 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_RefOut(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_RefOut
        target = COverloads_OneArg_RefOut()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 ', '', ),
            (         100, _merge(_first('M103 '), _second('M100 M101 M102 ')), '', '', ),
            (  (100, 200), _second('M101 M102 '), 'M100 M103 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_OutNormal(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_OutNormal
        target = COverloads_OneArg_OutNormal()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 M103 ', '', ),
            (         100, _merge(_first('M100 M103 '), _second('M101 M102 ')), '', '', ),
            (  (100, 200), _second('M101 M102 '), 'M100 M103 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_OutRef(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_OutRef
        target = COverloads_OneArg_OutRef()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 M102 ', '', ),
            (         100, _merge(_first('M102 '), _second('M100 M101 ')), '', '', ),
            (  (100, 200), _second('M100 M101 '), 'M102 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_OneArg_NormalDefault(self):
        from IronPythonTest.BinderTest import COverloads_OneArg_NormalDefault
        target = COverloads_OneArg_NormalDefault()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (     tuple(), dict(), 'M100 M101 ', '', ),
            (         100, _first('M100 M101 '), '', '', ),
            (  (100, 200), _first('M100 M101 '), '', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_String(self):
        from IronPythonTest.BinderTest import COverloads_String
        target = COverloads_String()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (         'a', _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
            (       'abc', _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
            (  mystr('a'), _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
            (mystr('abc'), _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
            (           1, _first('M101 M102 '), 'M100 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Enum(self):
        from IronPythonTest.BinderTest import COverloads_Enum, E1, E2
        target = COverloads_Enum()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        E1.A, _first('M100 '), 'M101 ', '', ),
            (        E2.A, _first('M101 '), 'M100 ', '', ),
            (           1, _second('M100 M101 '), '', '', ),
            (     UInt163, _second('M100 M101 '), '', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_UserDefined(self):
        from IronPythonTest.BinderTest import C1, C2, C3, C6, S1, COverloads_UserDefined
        target = COverloads_UserDefined()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (           C1(), _merge(_first('M101 M102 M103 M104 '), _second('M100 ')), 'M105 ', '', ),
            (           C2(), _merge(_first('M102 M103 '), _second('M100 M101 M104 ')), 'M105 ', '', ),
            (           C3(), _second('M103 '), 'M100 M101 M102 M104 M105 ', '', ),
            (           S1(), _first('M100 M101 M102 M103 '), 'M104 M105 ', '', ),
            (           C6(), _second('M103 M105 '), 'M100 M101 M102 M104 ', '', ),
            (     self.pt_i,  _first('M100 M101 M102 M103 '), 'M104 M105 ', '', ),
            (     self.pt_c1, _merge(_first('M101 M102 M103 M104 '), _second('M100 ')), 'M105 ', '', ),
            (  self.pt_i_int, _first('M100 M101 M102 M103 '), 'M104 M105 ', '', ),
            (self.pt_int_old, _second('M102 M103 '), 'M100 M101 M104 M105 ', '', ),
            (self.pt_int_new, _second('M102 M103 '), 'M100 M101 M104 M105 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Derived_Number(self):
        from IronPythonTest.BinderTest import COverloads_Derived_Number
        target = COverloads_Derived_Number()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first('M106 '), _second('M102 M103 ')), 'M100 M101 M104 M105 ', '', ),
            (        True, _merge(_first('M100 M103 '), _second('M104 M105 M106 ')), 'M101 M102 ', '', ),
            (        -100, _merge(_first('M100 '), _second('M104 M105 M106 ')), 'M101 M102 M103 ', '', ),
            (    big(200), _merge(_first('M100 M105 M106 '), _second('M101 M102 M104 ')), 'M103 ', '', ),
            (      Byte10, _merge(_first('M103 '), _second('M100 M105 M106 ')), 'M101 M102 M104 ', '', ),
            (       12.34, _merge(_first('M105 M106 '), _second('M101 M102 ')), 'M100 M103 M104 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Collections(self):
        from IronPythonTest.BinderTest import COverloads_Collections
        target = COverloads_Collections()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (    arrayInt, _merge(_first('M100 '), _second('M101 M102 M103 M104 ')), '', '', ),
            (    tupleInt, _merge(_first(''), _second('M100 M101 M102 M103 M104 ')), '', '', ),
            (     listInt, _merge(_first('M102 M104 '), _second('M100 M103 ')), 'M101 ', '', ),
            (  tupleLong1, _merge(_first(''), _second('M100 M101 M102 M103 M104 ')), '', '', ),
            (  tupleLong2, _merge(_first(''), _second('M100 M103 ')), '', 'M101 M102 M104 ', ),
            (   arrayByte, _first('M101 M103 M104 '), 'M100 M102 ', '', ),
            (    arrayObj, _merge(_first('M101 M102 M104 '), _second('M100 M103 ')), '', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Boolean(self):
        from IronPythonTest.BinderTest import COverloads_Boolean
        target = COverloads_Boolean()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 '), _second('M112 ')), '', '', ),
            (        True, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
            (       False, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
            (         100, _merge(_first('M100 '), _second('M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (        -100, _merge(_first('M100 '), _second('M102 M104 M106 M108 M109 M110 M111 M112 ')), '', 'M101 M103 M105 M107 ', ),
            (  myint(150), _merge(_first('M100 '), _second('M101 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', 'M102 ', ),
            (    big(200), _merge(_first('M100 '), _second('M101 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', 'M102 ', ),
            (   big(-200), _merge(_first('M100 '), _second('M104 M106 M108 M109 M110 M111 M112 ')), '', 'M101 M102 M103 M105 M107 ', ),
            (   UInt32Max, _merge(_first('M100 '), _second('M105 M107 M108 M109 M110 M111 M112 ')), '', 'M101 M102 M103 M104 M106 ', ),
            (      Byte10, _merge(_first('M100 '), _second('M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (    SBytem10, _merge(_first('M100 '), _second('M102 M104 M106 M108 M109 M110 M111 M112 ')), '', 'M101 M103 M105 M107 ', ),
            (     Int1610, _merge(_first('M100 '), _second('M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (    Int16m20, _merge(_first('M100 '), _second('M102 M104 M106 M108 M109 M110 M111 M112 ')), '', 'M101 M103 M105 M107 ', ),
            (       12.34, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 '), _second('M109 M110 M111 M112 ')), '', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)


    def test_arg_Byte(self):
        from IronPythonTest.BinderTest import COverloads_Byte
        target = COverloads_Byte()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
            (        True, _merge(_first('M101 M102 M103 M104 M105 M107 '), _second('M100 M106 M108 M109 M110 M112 M111 ')), '', '', ),
            (       False, _merge(_first('M101 M102 M103 M104 M105 M107 '), _second('M100 M106 M108 M109 M110 M112 M111 ')), '', '', ),
            (         100, _merge(_first('M100 M101 M102 '), _second('M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (        -100, _merge(_first(''), _second('M104 M106 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M102 M103 M105 M107 ', ),
            (  myint(150), _merge(_first('M100 M101 M102 '), _second('M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (    big(200), _merge(_first('M100 M101 M102 '), _second('M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (   big(-200), _merge(_first(''), _second('M104 M106 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M102 M103 M105 M107 ', ),
            (   UInt32Max, _merge(_first(''), _second('M105 M107 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M102 M103 M104 M106 ', ),
            (      Byte10, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
            (    SBytem10, _merge(_first(''), _second('M102 M104 M106 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M103 M105 M107 ', ),
            (     Int1610, _merge(_first('M100 M101 M102 '), _second('M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (    Int16m20, _merge(_first(''), _second('M104 M106 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M102 M103 M105 M107 ', ),
            (       12.34, _merge(_first(''), _second('M100 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Int16(self):
        from IronPythonTest.BinderTest import COverloads_Int16
        target = COverloads_Int16()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
            (        True, _merge(_first('M101 '), _second('M100 M102 M103 M104 M105 M106 M107 M108 M109 M110 M112 M111 ')), '', '', ),
            (       False, _merge(_first('M101 '), _second('M100 M102 M103 M104 M105 M106 M107 M108 M109 M110 M112 M111 ')), '', '', ),
            (         100, _merge(_first('M100 M101 M102 M103 '), _second('M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (        -100, _merge(_first('M100 M101 M102 M103 '), _second('M106 M108 M109 M110 M111 M112 ')), '', 'M104 M105 M107 ', ),
            (  myint(150), _merge(_first('M100 M101 M102 M103 '), _second('M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (    big(200), _merge(_first('M100 M101 M102 M103 '), _second('M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (   big(-200), _merge(_first('M100 M101 M102 M103 '), _second('M106 M108 M109 M110 M111 M112 ')), '', 'M104 M105 M107 ', ),
            (   UInt32Max, _merge(_first(''), _second('M105 M107 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M102 M103 M104 M106 ', ),
            (      Byte10, _merge(_first('M100 M101 M103 M106 M108 M109 M110 M111 M112'), _second('M102 M104 M105 M107 ')), '', '', ),
            (    SBytem10, _merge(_first('M100 M101 M102 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('M103 ')), '', '', ),
            (     Int1610, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
            (    Int16m20, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
            (       12.34, _merge(_first(''), _second('M100 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Int32(self):
        from IronPythonTest.BinderTest import COverloads_Int32
        target = COverloads_Int32()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
            (        True, _merge(_first('M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M112 M111 '), _second('M100 ')), '', '', ),
            (       False, _merge(_first('M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M112 M111 '), _second('M100 ')), '', '', ),
            (         100, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
            (        -100, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
            (  myint(150), _merge(_first('M100 M101 M102 M103 M104 M105 M106 '), _second('M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (    big(200), _merge(_first('M100 M101 M102 M103 M104 M105 M106 '), _second('M107 M108 M109 M110 M111 M112 ')), '', '', ),
            (   big(-200), _merge(_first('M100 M101 M102 M103 M104 M105 M106 '), _second('M108 M109 M110 M111 M112 ')), '', 'M107 ', ),
            (   UInt32Max, _merge(_first(''), _second('M106 M107 M108 M109 M110 M111 M112 ')), '', 'M100 M101 M102 M103 M104 M105 ', ),
            (      Byte10, _merge(_first('M100 M101 M103 M108 M109 M110 M111 M112'), _second('M102 M104 M105 M106 M107 ')), '', '', ),
            (    SBytem10, _merge(_first('M100 M101 M102 M104 M106 M107 M108 M109 M110 M111 M112 '), _second('M103 M105 ')), '', '', ),
            (     Int1610, _merge(_first('M100 M101 M102 M103 M104 M106 M107 M108 M109 M110 M111 M112 '), _second('M105 ')), '', '', ),
            (    Int16m20, _merge(_first('M100 M101 M102 M103 M104 M106 M107 M108 M109 M110 M111 M112 '), _second('M105 ')), '', '', ),
            (       12.34, _merge(_first(''), _second('M100 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Int64(self):
        from IronPythonTest.BinderTest import COverloads_Int64
        target = COverloads_Int64()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
            (        True, _merge(_first('M101 M102 M103 M104 M105 M106 M108 M109 M110 M112 M111 '), _second('M100 M107 ')), '', '', ),
            (       False, _merge(_first('M101 M102 M103 M104 M105 M106 M108 M109 M110 M112 M111 '), _second('M100 M107 ')), '', '', ),
            (         100, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M109 M110 M111 M112 '), _second('M107 ')), '', '', ),
            (        -100, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M109 M110 M111 M112 '), _second('M107 ')), '', '', ),
            (  myint(150), _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 '), _second('M111 M112 ')), '', '', ),
            (    big(200), _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 '), _second('M111 M112 ')), '', '', ),
            (   big(-200), _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 '), _second('M111 M112 ')), '', '', ),
            (   UInt32Max, _merge(_first('M100 M101 M102 M103 M104 M105 M107 M109 M110 M111 M112 '), _second('M106 M108 ')), '', '', ),
            (      Byte10, _merge(_first('M100 M101 M103 M109 M110 M111 M112 '), _second('M102 M104 M105 M106 M107 M108 ')), '', '', ),
            (    SBytem10, _merge(_first('M100 M101 M102 M104 M106 M108 M109 M110 M111 M112 '), _second('M103 M105 M107 ')), '', '', ),
            (     Int1610, _merge(_first('M100 M101 M102 M103 M104 M106 M108 M109 M110 M111 M112 '), _second('M105 M107 ')), '', '', ),
            (    Int16m20, _merge(_first('M100 M101 M102 M103 M104 M106 M108 M109 M110 M111 M112 '), _second('M105 M107 ')), '', '', ),
            (       12.34, _merge(_first(''), _second('M100 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 ', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_arg_Double(self):
        from IronPythonTest.BinderTest import COverloads_Double
        target = COverloads_Double()
        for (arg, mapping, funcTypeError, funcOverflowError) in [
            (        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
            (        True, _merge(_first('M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M100 M107 M109 M111 ')), 'M110 ', '', ),
            (       False, _merge(_first('M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M100 M107 M109 M111 ')), 'M110 ', '', ),
            (         100, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M107 M109 M111 ')), 'M110 ', '', ),
            (        -100, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M107 M109 M111 ')), 'M110 ', '', ),
            (  myint(150), _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
            (    big(200), _merge(_first('M101 M100 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
            (   big(-200), _merge(_first('M101 M100 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
            (   UInt32Max, _merge(_first('M100 M101 M102 M103 M104 M105 M107 M112 '), _second('M106 M108 M109 M111 ')), 'M110 ', '', ),
            (      Byte10, _merge(_first('M100 M101 M103 M112 '), _second('M102 M104 M105 M106 M107 M108 M109 M111 ')), 'M110 ', '', ),
            (    SBytem10, _merge(_first('M100 M101 M102 M104 M106 M108 M112 '), _second('M103 M105 M107 M109 M111 ')), 'M110 ', '', ),
            (     Int1610, _merge(_first('M100 M101 M102 M103 M104 M106 M108 M112 '), _second('M105 M107 M109 M111 ')), 'M110 ', '', ),
            (    Int16m20, _merge(_first('M100 M101 M102 M103 M104 M106 M108 M112 '), _second('M105 M107 M109 M111 ')), 'M110 ', '', ),
            (       12.34, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
            ]:
            with self.subTest(arg=arg):
                self._try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

    def test_compare_int_as_bigint(self):
        from IronPythonTest import BigIntCompare
        self.assertEqual(BigIntCompare(1).CompareTo(big(2)), -1)
        self.assertEqual(BigIntCompare(1).CompareTo(2), -1)

        self.assertTrue(BigIntCompare(1).IsEqual(big(1)))
        self.assertTrue(BigIntCompare(1).IsEqual(1))

run_test(__name__)
