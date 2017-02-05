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
# PART 2. how IronPython choose the overload methods
#

from iptest.assert_util import *
from iptest.type_util import *
skiptest("win32")
load_iron_python_test()
from IronPythonTest.BinderTest import *

class PT_I(I): pass

class PT_C1(C1): pass

class PT_C3_int(C3):
    def __int__(self): return 1

class PT_I_int(I):
    def __int__(self): return 100

class PT_int_old:
    def __int__(self): return 200

class PT_int_new(object):
    def __int__(self): return 300

UInt32Max = System.UInt32.MaxValue
Byte10   = System.Byte.Parse('10')
SBytem10 = System.SByte.Parse('-10')
Int1610  = System.Int16.Parse('10')
Int16m20 = System.Int16.Parse('-20')
UInt163  = System.UInt16.Parse('3')

pt_i = PT_I()
pt_c1 = PT_C1()
pt_i_int = PT_I_int()
pt_int_old = PT_int_old()
pt_int_new = PT_int_new()

arrayInt = array_int((10, 20))
tupleInt = ((10, 20), )
listInt  = ([10, 20], )
tupleLong1, tupleLong2  = ((10, 20), ), ((System.Int64.MaxValue, System.Int32.MaxValue * 2),)
arrayByte = array_byte((10, 20))
arrayObj = array_object(['str', 10])


def _self_defined_method(name): return len(name) == 4 and name[0] == "M"

def _result_pair(s, offset=0):
    fn = s.split()
    val = [int(x[1:]) + offset for x in fn]
    return dict(list(zip(fn, val)))

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
    
def _try_arg(target, arg, mapping, funcTypeError, funcOverflowError, verbose=False):
    '''try the pass-in argument 'arg' on all methods 'target' has.
       mapping specifies (method-name, flag-value)
       funcOverflowError contains method-name, which will cause OverflowError when passing in 'arg'
    '''
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
                Fail("unexpected exception %s when func %s with arg %s (%s)\n%s" % (e, funcname, arg, type(arg), func.__doc__))

            if funcname in list(mapping.keys()):  # No exception expected:
                Fail("unexpected exception %s when func %s with arg %s (%s)\n%s" % (e, funcname, arg, type(arg), func.__doc__))

            if not isinstance(e, expectError):
                Fail("expect '%s', but got '%s' (flag %s) when func %s with arg %s (%s)\n%s" % (expectError, e, Flag.Value, funcname, arg, type(arg), func.__doc__))
        else:
            if not funcname in list(mapping.keys()): # Expecting exception
                Fail("expect %s, but got no exception (flag %s) when func %s with arg %s (%s)\n%s" % (expectError, Flag.Value, funcname, arg, type(arg), func.__doc__))
            
            left, right = Flag.Value, mapping[funcname]
            if left != right:
                Fail("left %s != right %s when func %s on arg %s (%s)\n%s" % (left, right, funcname, arg, type(arg), func.__doc__))
            Flag.Value = -99           # reset
    if verbose: print()
    
def test_other_concerns():
    target = COtherOverloadConcern()
    
    # the one asking for Int32 is private
    target.M100(100)
    AreEqual(Flag.Value, 200); Flag.Value = 99
    
    # static / instance
    target.M110(target, 100)
    AreEqual(Flag.Value, 110); Flag.Value = 99
    COtherOverloadConcern.M110(100)
    AreEqual(Flag.Value, 210); Flag.Value = 99
    
    AssertError(TypeError, COtherOverloadConcern.M110, target, 100)
    
    # static / instance 2
    target.M111(100)
    AreEqual(Flag.Value, 111); Flag.Value = 99
    COtherOverloadConcern.M111(target, 100)
    AreEqual(Flag.Value, 211); Flag.Value = 99
        
    AssertError(TypeError, target.M111, target, 100)
    AssertError(TypeError, COtherOverloadConcern.M111, 100)
    
    # statics
    target.M120(target, 100)
    AreEqual(Flag.Value, 120); Flag.Value = 99
    target.M120(100)
    AreEqual(Flag.Value, 220); Flag.Value = 99

    COtherOverloadConcern.M120(target, 100)
    AreEqual(Flag.Value, 120); Flag.Value = 99
    COtherOverloadConcern.M120(100)
    AreEqual(Flag.Value, 220); Flag.Value = 99
    
    # generic
    target.M130(100)
    AreEqual(Flag.Value, 130); Flag.Value = 99

    target.M130(100.1234)
    AreEqual(Flag.Value, 230); Flag.Value = 99

    target.M130(C1())
    AreEqual(Flag.Value, 230); Flag.Value = 99

    for x in [100, 100.1234]:
        target.M130[int](x)
        AreEqual(Flag.Value, 230); Flag.Value = 99
    
    # narrowing levels and __int__ conversion
    target.M140(PT_C3_int(), PT_C3_int())
    AreEqual(Flag.Value, 140); Flag.Value = 99

import clr
clrRefInt = clr.Reference[int](0)

######### generated python code below #########

def test_arg_ClrReference():
    target = COverloads_ClrReference()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(lambda :                         None, _merge(_first('M100 M101 M107 '), _second('M102 M104 M105 M106 ')), 'M103 ', '', ),
(lambda :      clr.Reference[object](None), _second('M100 M104 M105 M107 '), 'M101 M102 M103 M104 M106 ', '', ),
(lambda :  clr.Reference[object](None), _second('M100 M104 M105 M107 '), 'M101 M102 M103 M106 ', '', ),
(lambda :        clr.Reference[int](9), _merge(_first('M100 M102 M103 M104 '), _second('M105 M107 ')), 'M101 M106 ', '', ),
(lambda :    clr.Reference[bool](True), _merge(_first('M100 M105 '), _second('M101 M102 M104 M107 ')), 'M103 M106 ', '', ),
(lambda : clr.Reference[type](complex), _merge(_first('M100 '), _second('M104 M105 M107 ')), 'M101 M102 M103 M106 ', '', ),
(lambda :      clr.Reference[C1](C1()), _merge(_first('M100 M106 M107 '), _second('M104 M105 ')), 'M101 M102 M103 ', '', ),
(lambda :      clr.Reference[C1](C2()), _merge(_first('M100 M106 M107 '), _second('M104 M105 ')), 'M101 M102 M103 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_NoArgNecessary():
    target = COverloads_NoArgNecessary()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), _merge(_first('M100 M101 M102 M105 '), _second('M103 M104 M106 ')), '', '', ),
(         100, _merge(_first('M105 M106 '), _second('M101 M102 M103 M104 ')), 'M100 ', '', ),
(  (100, 200), _second('M102 M104 M105 M106 '), 'M100 M101 M103 ', '', ),
(   clrRefInt, _merge(_first('M103 M104 '), _second('M100 ')), 'M101 M102 M105 M106 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_NormalArg():
    target = COverloads_OneArg_NormalArg()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 ', '', ),
(         100, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 '), '', '', ),
(  (100, 200), _second('M102 M107 M108 '), 'M100 M101 M103 M104 M105 M106 M109 ', '', ),
(   clrRefInt, _second('M100 '), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_RefArg():
    target = COverloads_OneArg_RefArg()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 M106 M107 M108 ', '', ),
(         100, _merge(_first('M100 M101 M103 M105 M108 '), _second('M106 M107 ')), 'M102 M104 ', '', ),
(  (100, 200), _second('M101 M106 M107 '), 'M100 M102 M103 M104 M105 M108 ', '', ),
(   clrRefInt, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 '), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_NullableArg():
    target = COverloads_OneArg_NullableArg()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 M106 M107 ', '', ),
(         100, _merge(_first('M100 M107 '), _second('M101 M102 M103 M104 M105 M106 ')), '', '', ),
(  (100, 200), _second('M100 M105 M106 '), 'M101 M102 M103 M104 M107 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_TwoArgs():
    target = COverloads_OneArg_TwoArgs()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 ', '', ),
(         100, _second('M100 M101 M102 M103 M104 '), 'M105 ', '', ),
(  (100, 200), _first('M100 M101 M102 M103 M104 M105 '), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_NormalOut():
    target = COverloads_OneArg_NormalOut()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 M104 M105 ', '', ),
(         100, _merge(_first('M100 M102 M105 '), _second('M103 M104 ')), 'M101 ', '', ),
(  (100, 200), _second('M103 M104 '), 'M100 M101 M102 M105 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_RefOut():
    target = COverloads_OneArg_RefOut()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 ', '', ),
(         100, _merge(_first('M103 '), _second('M100 M101 M102 ')), '', '', ),
(  (100, 200), _second('M101 M102 '), 'M100 M103 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_OutNormal():
    target = COverloads_OneArg_OutNormal()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 M103 ', '', ),
(         100, _merge(_first('M100 M103 '), _second('M101 M102 ')), '', '', ),
(  (100, 200), _second('M101 M102 '), 'M100 M103 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_OutRef():
    target = COverloads_OneArg_OutRef()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 M102 ', '', ),
(         100, _merge(_first('M102 '), _second('M100 M101 ')), '', '', ),
(  (100, 200), _second('M100 M101 '), 'M102 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_OneArg_NormalDefault():
    target = COverloads_OneArg_NormalDefault()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(     tuple(), dict(), 'M100 M101 ', '', ),
(         100, _first('M100 M101 '), '', '', ),
(  (100, 200), _first('M100 M101 '), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_String():
    target = COverloads_String()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(         'a', _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
(       'abc', _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
(  mystr('a'), _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
(mystr('abc'), _merge(_first('M100 M101 '), _second('M102 ')), '', '', ),
(           1, _first('M101 M102 '), 'M100 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_Enum():
    target = COverloads_Enum()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        E1.A, _first('M100 '), 'M101 ', '', ),
(        E2.A, _first('M101 '), 'M100 ', '', ),
(           1, _second('M100 M101 '), '', '', ),
(     UInt163, _second('M101 '), 'M100 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_UserDefined():
    target = COverloads_UserDefined()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        C1(), _merge(_first('M101 M102 M103 M104 '), _second('M100 ')), 'M105 ', '', ),
(        C2(), _merge(_first('M102 M103 '), _second('M100 M101 M104 ')), 'M105 ', '', ),
(        C3(), _second('M103 '), 'M100 M101 M102 M104 M105 ', '', ),
(        S1(), _first('M100 M101 M102 M103 '), 'M104 M105 ', '', ),
(        C6(), _second('M103 M105 '), 'M100 M101 M102 M104 ', '', ),
(        pt_i, _first('M100 M101 M102 M103 '), 'M104 M105 ', '', ),
(       pt_c1, _merge(_first('M101 M102 M103 M104 '), _second('M100 ')), 'M105 ', '', ),
(    pt_i_int, _first('M100 M101 M102 M103 '), 'M104 M105 ', '', ),
(  pt_int_old, _second('M102 M103 '), 'M100 M101 M104 M105 ', '', ),
(  pt_int_new, _second('M102 M103 '), 'M100 M101 M104 M105 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_Derived_Number():
    target = COverloads_Derived_Number()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        None, _merge(_first('M106 '), _second('M102 M103 ')), 'M100 M101 M104 M105 ', '', ),
(        True, _merge(_first('M100 M103 '), _second('M104 M105 M106 ')), 'M101 M102 ', '', ),
(        -100, _merge(_first('M100 '), _second('M104 M105 M106 ')), 'M101 M102 M103 ', '', ),
(        200, _merge(_first('M106 M105 '), _second('M100 M102 M101 ')), 'M103 M104 ', '', ),
(      Byte10, _merge(_first('M103 '), _second('M100 M105 M106 ')), 'M101 M102 M104 ', '', ),
(       12.34, _merge(_first('M105 M106 '), _second('M101 M102 M100 ')), 'M103 M104 ', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_Collections():
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
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

#------------------------------------------------------------------------------
#--Boolean
def test_arg_boolean_overload():
    '''
    TODO:
    In addition to test_arg_boolean_overload, we need to split up test_arg_Boolean
    into two more functions as well - test_arg_boolean_overload_typeerror and
    test_arg_boolean_overload_overflowerror.  This should be done for all of these
    types of tests to make them more readable and maintainable.
    '''
    o = COverloads_Boolean()
    
    param_method_map = {
        None : [        o.M100, o.M101, o.M102, o.M103, o.M104, o.M105, o.M106, 
                        o.M107, o.M108, o.M109, o.M110, o.M111],
        True : [        o.M100, o.M101, o.M102, o.M103, o.M104, o.M105, o.M106, o.M107, o.M108, o.M109, o.M110, o.M111, o.M112],
        False : [       o.M100, o.M101, o.M102, o.M103, o.M104, o.M105, o.M106, o.M107, o.M108, o.M109, o.M110, o.M111, o.M112],
        100 : [         o.M100],
        myint(100): [   o.M100],
        -100 : [        o.M100],
        UInt32Max: [    o.M100, o.M106],
        200 : [        o.M100, o.M106, o.M109],
        -200 : [       o.M100, o.M106, o.M109],
        Byte10 : [      o.M100],
        SBytem10 : [    o.M100],
        Int1610 : [     o.M100],
        Int16m20 : [    o.M100],
        12.34 : [       o.M100, o.M101, o.M102, o.M103, o.M104, o.M105, o.M106, o.M107, o.M108, o.M109, o.M110],
        
    }
    
    for param in list(param_method_map.keys()):
        for meth in param_method_map[param]:
            expected_flag = int(meth.__name__[1:])
            meth(param)
            AreEqual(expected_flag, Flag.Value)
    

def test_arg_Boolean():
    target = COverloads_Boolean()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        None, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 '), _second('M112 ')), '', '', ),
(        True, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
(       False, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('')), '', '', ),
(         100, _merge(_first('M100 '), _second('M106 M108 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M107 ', '', ),
(  myint(100), _merge(_first('M100 '), _second('M106 M108 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M107 ', '', ),
(        -100, _merge(_first('M100 '), _second('M106 M108 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 M105 M107 ', '', ),
(   UInt32Max, _merge(_first('M100 M106 '), _second('M105 M107 M108 M109 M110 M111 M112 ')), 'M101 M102 M103 M104 ', '', ),
(        200, _merge(_first('M100 M106 M109 '), _second('M108 M112 M110 M111 ')), 'M101 M102 M103 M104 M105 M107 ', '', ),
(       -200, _merge(_first('M100 M106 M109 '), _second('M108 M112 M110 M111 ')), 'M101 M102 M103 M104 M105 M107 ', '', ),
(      Byte10, _merge(_first('M100 '), _second('M101 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 ')), 'M102 ', '', ),
(    SBytem10, _merge(_first('M100 '), _second('M102 M104 M106 M108 M109 M110 M111 M112 ')), 'M101 M103 M105 M107 ', '', ),
(     Int1610, _merge(_first('M100 '), _second('M104 M106 M108 M109 M110 M111 M112 ')), 'M101 M102 M103 M105 M107 ', '', ),
(    Int16m20, _merge(_first('M100 '), _second('M104 M106 M108 M109 M110 M111 M112 ')), 'M101 M102 M103 M105 M107 ', '', ),
(       12.34, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 '), _second('M111 M112 ')), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)


def test_arg_Byte():
    target = COverloads_Byte()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
(        True, _merge(_first('M101 M102 M103 M104 M105 M107 '), _second('M100 M106 M108 M109 M110 M112 M111 ')), '', '', ),
(        False, _merge(_first('M101 M102 M103 M104 M105 M107 '), _second('M100 M106 M108 M109 M110 M112 M111 ')), '', '', ),
(         100, _merge(_first('M101 M102 M103 M104 M105 M107 '), _second('M106 M108 M109 M110 M111 M112 ')), 'M100 ', '', ),
(  myint(100), _merge(_first('M101 M102 M103 M104 M105 M107 '), _second('M106 M108 M109 M110 M111 M112 ')), 'M100 ', '', ),
(        -100, _merge(_first(''), _second('M106 M108 M109 M110 M111 M112 ')), 'M100 ', 'M101 M102 M103 M104 M105 M107 ', ),
(   UInt32Max, _merge(_first(''), _second('M105 M107 M108 M109 M110 M111 M112 ')), 'M100 ', 'M101 M102 M103 M104 M106 ', ),
(        200, _merge(_first('M101 M102 M103 M104 M105 M106 M107 M109 '), _second('M108 M112 M110 M111 ')), 'M100 ', '', ),
(       -200, _merge(_first(''), _second('M108 M112 M110 M111 ')), 'M100 ', 'M101 M102 M103 M104 M105 M106 M107 M109 ', ),
(      Byte10, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
(    SBytem10, _merge(_first(''), _second('M102 M104 M106 M108 M109 M110 M111 M112 ')), 'M100 ', 'M101 M103 M105 M107 ', ),
(     Int1610, _merge(_first('M101 M102 M103 M105 M107 '), _second('M104 M106 M108 M109 M110 M111 M112 ')), 'M100 ', '', ),
(    Int16m20, _merge(_first(''), _second('M104 M106 M108 M109 M110 M111 M112 ')), 'M100 ', 'M101 M102 M103 M105 M107 ', ),
(       12.34, _merge(_first('M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 '), _second('M100 M111 M112 ')), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_Int16():
    target = COverloads_Int16()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
(        True, _merge(_first('M101 '), _second('M100 M102 M103 M104 M105 M107 M106 M108 M109 M110 M112 M111 ')), '', '', ),
(       False, _merge(_first('M101 '), _second('M100 M102 M103 M104 M105 M107 M106 M108 M109 M110 M112 M111 ')), '', '', ),
(         100, _merge(_first('M101 '), _second('M102 M103 M104 M105 M107 M106 M108 M109 M110 M111 M112 ')), 'M100 ', '', ),
(  myint(100), _merge(_first('M101 '), _second('M102 M103 M104 M105 M107 M106 M108 M109 M110 M111 M112 ')), 'M100 ', '', ),
(        -100, _merge(_first('M101 '), _second('M103 M106 M108 M109 M110 M111 M112 ')), 'M100 ', 'M102 M104 M105 M107 ', ),
(   UInt32Max, _merge(_first(''), _second('M105 M107 M108 M109 M110 M111 M112 ')), 'M100 ', 'M101 M102 M103 M104 M106 ', ),
(        200, _merge(_first('M101 M106 M109 '), _second('M102 M104 M105 M107 M108 M110 M111 M112 ')), 'M100 ', 'M103 ', ),
(       -200, _merge(_first('M101 M106 M109 '), _second('M108 M110 M111 M112 ')), 'M100 ', 'M102 M103 M104 M105 M107 ', ),
(      Byte10, _merge(_first('M100 M101 M103 M106 M108 M109 M110 M111 M112'), _second('M102 M104 M105 M107 ')), '', '', ),
(    SBytem10, _merge(_first('M100 M101 M102 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), _second('M103 ')), '', '', ),
(     Int1610, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
(    Int16m20, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
(       12.34, _merge(_first('M101 M106 M108 M109 M110 '), _second('M100 M111 M112 M102 M103 M104 M105 M107 ')), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_Int32():
    target = COverloads_Int32()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
(        True, _merge(_first('M101 M102 M103 M104 M105 M107 M106 M108 M109 M110 M112 M111 '), _second('M100 ')), '', '', ),
(       False, _merge(_first('M101 M102 M103 M104 M105 M107 M106 M108 M109 M110 M112 M111 '), _second('M100 ')), '', '', ),
(         100, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
(  myint(100), _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
(        -100, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
(    UInt32Max, _merge(_first(''), _second('M100 M106 M107 M108 M109 M110 M111 M112 ')), '', 'M101 M102 M103 M104 M105 ', ),
(        200, _merge(_first('M101 M109 '), _second('M100 M102 M104 M105 M106 M107 M108 M110 M111 M112 ')), '', 'M103 ', ),
(       -200, _merge(_first('M101 M109 '), _second('M100 M105 M108 M110 M111 M112 ')), '', 'M102 M103 M104 M106 M107 ', ),
(      Byte10, _merge(_first('M100 M101 M103 M108 M109 M110 M111 M112'), _second('M102 M104 M105 M106 M107 ')), '', '', ),
(    SBytem10, _merge(_first('M100 M101 M102 M104 M106 M107 M108 M109 M110 M111 M112 '), _second('M103 M105 ')), '', '', ),
(     Int1610, _merge(_first('M100 M101 M102 M103 M104 M106 M107 M108 M109 M110 M111 M112 '), _second('M105 ')), '', '', ),
(    Int16m20, _merge(_first('M100 M101 M102 M103 M104 M106 M107 M108 M109 M110 M111 M112 '), _second('M105 ')), '', '', ),
(       12.34, _merge(_first('M101 M108 M109 M110 '), _second('M100 M106 M111 M112 M102 M103 M104 M105 M107 ')), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

def test_arg_Double():
    target = COverloads_Double()
    for (arg, mapping, funcTypeError, funcOverflowError) in [
(        None, _merge(_first(''), _second('M100 M112 ')), 'M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 ', '', ),
(        True, _merge(_first('M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M100 M107 M109 M111 ')), 'M110 ', '', ),
(       False, _merge(_first('M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M100 M107 M109 M111 ')), 'M110 ', '', ),
(         100, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M107 M109 M111 ')), 'M110 ', '', ),
(  myint(100), _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M107 M109 M111 ')), 'M110 ', '', ),
(        -100, _merge(_first('M100 M101 M102 M103 M104 M105 M106 M108 M112 '), _second('M107 M109 M111 ')), 'M110 ', '', ),
(   UInt32Max, _merge(_first('M100 M101 M102 M103 M104 M105 M107 M112 '), _second('M106 M108 M109 M111 ')), 'M110 ', '', ),
(        200, _merge(_first('M101 M100 M102 M103 M104 M105 M106 M107 M108 M109 M110 M112 '), _second('M111 ')), '', '', ),
(       -200, _merge(_first('M101 M100 M102 M103 M104 M105 M106 M107 M108 M109 M110 M112 '), _second('M111 ')), '', '', ),
(      Byte10, _merge(_first('M100 M101 M103 M112 '), _second('M102 M104 M105 M106 M107 M108 M109 M111 ')), 'M110 ', '', ),
(    SBytem10, _merge(_first('M100 M101 M102 M104 M106 M108 M112 '), _second('M103 M105 M107 M109 M111 ')), 'M110 ', '', ),
(     Int1610, _merge(_first('M100 M101 M102 M103 M104 M106 M108 M112 '), _second('M105 M107 M109 M111 ')), 'M110 ', '', ),
(    Int16m20, _merge(_first('M100 M101 M102 M103 M104 M106 M108 M112 '), _second('M105 M107 M109 M111 ')), 'M110 ', '', ),
(       12.34, _first('M100 M101 M102 M103 M104 M105 M106 M107 M108 M109 M110 M111 M112 '), '', '', ),
    ]:
        _try_arg(target, arg, mapping, funcTypeError, funcOverflowError)

run_test(__name__)
