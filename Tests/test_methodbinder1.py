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
# PART 1. how IronPython choose the CLI method, treat parameters WHEN NO OVERLOADS PRESENT
#

from iptest.assert_util import *
from iptest.type_util import *
skiptest("win32")

load_iron_python_test()
from IronPythonTest.BinderTest import *

myint1,     myint2      = myint(20),    myint(-20)
mylong1,    mylong2     = mylong(3),    mylong(-4)
myfloat1,   myfloat2    = myfloat(4.5), myfloat(-4.5)
mycomplex1              = mycomplex(3)

funcs = '''
M100   M201   M202   M203   M204   M205   M301   M302   M303   M304
M310   M311   M312   M313   M320   M321   M400   M401   M402   M403
M404   M410   M411   M450   M451
M500   M510   M600   M610   M611   M620   M630
M650   M651   M652   M653
M680   M700   M701
M710   M715
'''.split()

args  = '''
NoArg  Int32  Double BigInt Bool   String SByte  Int16  Int64  Single
Byte   UInt16 UInt32 UInt64 Char   Decml  Object I      C1     C2
S1     A      C6     E1     E2
ArrInt32  ArrI   ParamArrInt32  ParamArrI       ParamArrS   Int32ParamArrInt32  IParamArrI
IListInt  Array  IEnumerableInt IEnumeratorInt
NullableInt RefInt32  OutInt32
DefValInt32 Int32DefValInt32
'''.split()

arg2func = dict(zip(args, funcs))
func2arg = dict(zip(funcs, args))

TypeE = TypeError
OverF = OverflowError

def _get_funcs(args): return [arg2func[x] for x in args.split()]
def _self_defined_method(name): return len(name) == 4 and name[0] == "M"

def _my_call(func, arg):
    if isinstance(arg, tuple):
        l = len(arg)
        # by purpose
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
    
def _helper(func, positiveArgs, flagValue, negativeArgs, exceptType):
    for arg in positiveArgs:
        try:
            _my_call(func, arg)
        except Exception, e:
            Fail("unexpected exception %s when calling %s with %s\n%s" % (e, func, arg, func.__doc__))
        else:
            AreEqual(Flag.Value, flagValue)
            Flag.Value = -188
    
    for arg in negativeArgs:
        try:
            _my_call(func, arg)
        except Exception, e:
            if not isinstance(e, exceptType):
                Fail("expected '%s', but got '%s' when calling %s with %s\n%s" % (exceptType, e, func, arg, func.__doc__))
        else:
            Fail("expected exception (but didn't get one) when calling func %s on args %s\n%s" % (func, arg, func.__doc__))

def test_this_matrix():
    '''
    This will test the full matrix.
    To print the matrix, enable the following flag
    '''
    print_the_matrix = False


    funcnames =     "M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400".split()
    matrix = (
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
(        "SByteMax", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(         "ByteMax", True,  True,  True,  True,  True,  TypeE, OverF, True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(        "Int16Max", True,  True,  True,  True,  True,  TypeE, OverF, True,  True,  True,  OverF, True,  True,  True,  TypeE, True,  True,  ),
(       "UInt16Max", True,  True,  True,  True,  True,  TypeE, OverF, OverF, True,  True,  OverF, True,  True,  True,  TypeE, True,  True,  ),
(          "intMax", True,  True,  True,  True,  True,  TypeE, OverF, OverF, True,  True,  OverF, OverF, True,  True,  TypeE, True,  True,  ),
(       "UInt32Max", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, True,  True,  OverF, OverF, True,  True,  TypeE, True,  True,  ),
(        "Int64Max", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, True,  True,  OverF, OverF, OverF, True,  TypeE, True,  True,  ),
(       "UInt64Max", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, True,  TypeE, True,  True,  ),
(      "DecimalMax", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(       "SingleMax", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, OverF, TypeE, OverF, True,  ),
(        "floatMax", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, OverF, TypeE, OverF, True,  ),
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
(        "SByteMin", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(         "ByteMin", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(        "Int16Min", True,  True,  True,  True,  True,  TypeE, OverF, True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(       "UInt16Min", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(          "intMin", True,  True,  True,  True,  True,  TypeE, OverF, OverF, True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(       "UInt32Min", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(        "Int64Min", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(       "UInt64Min", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(      "DecimalMin", OverF, OverF, True , True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, OverF, TypeE, True , True,  ),
(       "SingleMin", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, OverF, TypeE, OverF, True,  ),
(        "floatMin", OverF, OverF, True,  True,  True,  TypeE, OverF, OverF, OverF, True,  OverF, OverF, OverF, OverF, TypeE, OverF, True,  ),
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
(    "SBytePlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(     "BytePlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(    "Int16PlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(   "UInt16PlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(      "intPlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(            myint1, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(   "UInt32PlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(    "Int64PlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(   "UInt64PlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(  "DecimalPlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE,  True,  True,  ),
(   "SinglePlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(    "floatPlusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(          myfloat1, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
(   "SByteMinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(   "Int16MinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(     "intMinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(            myint2, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(   "Int64MinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
( "DecimalMinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(  "SingleMinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(   "floatMinusOne", True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
(          myfloat2, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
##################################################   pass in bool   #########################################################
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
(              True, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(             False, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
##################################################  pass in BigInt #########################################################
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
(               10L, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(              -10L, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
( 1234567890123456L, OverF, OverF, True , True,  True,  TypeE, OverF, OverF, True,  True,  OverF, OverF, OverF, True,  TypeE, True,  True,  ),
(           mylong1, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  True,  True,  True,  True,  TypeE, True,  True,  ),
(           mylong2, True,  True,  True,  True,  True,  TypeE, True,  True,  True,  True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ),
##################################################  pass in Complex #########################################################
####                 M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
####                 int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
(            (3+0j), TypeE, TypeE, TypeE, TypeE, True,  TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, True,  ),
(            (3+1j), TypeE, TypeE, TypeE, TypeE, True,  TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, True,  ),
(        mycomplex1, TypeE, TypeE, TypeE, TypeE, True,  TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, True,  )
    )

    
    if is_silverlight==False:
        InvariantCulture = System.Globalization.CultureInfo.InvariantCulture
        matrix = list(matrix)
        ##################################################  pass in char    #########################################################
        ####                                     M201   M680   M202   M203   M204   M205   M301   M302   M303   M304   M310   M311   M312   M313   M320   M321   M400
        ####                                     int    int?   double bigint bool   str    sbyte  i16    i64    single byte   ui16   ui32   ui64   char   decm   obj
        matrix.append((System.Char.Parse('A'), TypeE, TypeE, TypeE, TypeE, True,  True,  TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, TypeE, True,  True, True,  ))
        
        ##################################################  pass in float   #########################################################
        ####    single/double becomes Int32, but this does not apply to other primitive types
        ####                                                           int    int?  double  bigint bool   str    sbyte i16   i64   single byte   ui16   ui32   ui64   char   decm   obj
        matrix.append((System.Single.Parse("8.01", InvariantCulture), True,  True, True,  True,  True,  TypeE, True, True, True, True,  True,  True,  True,  True,  TypeE, True,  True,  ))
        matrix.append((System.Double.Parse("10.2", InvariantCulture), True,  True, True,  True,  True,  TypeE, True, True, True, True,  True,  True,  True,  True,  TypeE, True,  True,  ))
        matrix.append((System.Single.Parse("-8.1", InvariantCulture), True,  True, True,  True,  True,  TypeE, True, True, True, True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ))
        matrix.append((System.Double.Parse("-1.8", InvariantCulture), True,  True, True,  True,  True,  TypeE, True, True, True, True,  OverF, OverF, OverF, OverF, TypeE, True,  True,  ))
        matrix = tuple(matrix)
    
    for scenario in matrix:
        if isinstance(scenario[0], str):
            value = clr_numbers[scenario[0]]
            if print_the_matrix: print '(%18s,' % ('"'+ scenario[0] +'"'),
        else:
            value = scenario[0]
            if print_the_matrix: print '(%18s,' % value ,
        
        for i in range(len(funcnames)):
            funcname = funcnames[i]
            func = getattr(target, funcname)
            
            if print_the_matrix:
                try:
                    func(value)
                    print "True, ",
                except TypeError:
                    print "TypeE,",
                except OverflowError:
                    print "OverF,",
                print "),"
            else:
                try:
                    func(value)
                except Exception,e:
                    if scenario[i+1] not in [TypeE, OverF]:
                        Fail("unexpected exception %s, when func %s on arg %s (%s)\n%s" % (e, funcname, scenario[0], type(value), func.__doc__))
                    if isinstance(e, scenario[i+1]): pass
                    else: Fail("expect %s, but got %s when func %s on arg %s (%s)\n%s" % (scenario[i+1], e, funcname, scenario[0], type(value), func.__doc__))
                else:
                    if scenario[i+1] in [TypeE, OverF]:
                        Fail("expect %s, but got none when func %s on arg %s (%s)\n%s" % (scenario[i+1], funcname, scenario[0], type(value), func.__doc__))

                    left = Flag.Value ; Flag.Value = -99           # reset

                    right = int(funcname[1:])
                    if left != right:
                        Fail("left %s != right %s when func %s on arg %s (%s)\n%s" % (left, right, funcname, scenario[0], type(value), func.__doc__))

    # these funcs should behavior same as M201(Int32)
    # should have NullableInt too ?
    for funcname in _get_funcs('RefInt32   ParamArrInt32   Int32ParamArrInt32'):
        for scenario in matrix:
            if isinstance(scenario[0], str): value = clr_numbers[scenario[0]]
            else: value = scenario[0]

            func = getattr(target, funcname)
            if scenario[1] not in [TypeE, OverF]:
                func(value)
                left = Flag.Value
                right = int(funcname[1:])
                if left != right:
                    Fail("left %s != right %s when func %s on arg %s" % (left, right, funcname, scenario[0]))
                Flag.Value = -99           # reset
            else:
                try:   func(value)
                except scenario[1]: pass   # 1 is M201
                else:  Fail("expect %s, but got none when func %s on arg %s" % (scenario[1], funcname, scenario[0]))

def test_char_string_asked():
    # char asked
    _helper(target.M320, ['a', System.Char.MaxValue, System.Char.MinValue, 'abc'[2]], 320, ['abc', ('a  b')], TypeError)
    # string asked
    _helper(target.M205, ['a', System.Char.MaxValue, System.Char.MinValue, 'abc'[2], 'abc', 'a b' ], 205, [('a', 'b'), 23, ], TypeError)
    
def test_pass_extensible_types():
    # number covered by that matrix
    # string or char
    mystr1, mystr2 = mystr('a'), mystr('abc')
    _helper(target.M205, [mystr1, mystr2, ], 205, [], TypeError)  # String
    _helper(target.M320, [mystr1, ], 320, [mystr2, ], TypeError)  # Char

# check the bool conversion result
def test_bool_asked():
    for arg in ['a', 3, object(), True]:
        target.M204(arg)
        Assert(Flag.BValue, "argument is %s" % arg)
        Flag.BValue = False
        
    if is_silverlight==False:
        for arg in [0, System.Byte.Parse('0'), System.UInt64.Parse('0'), 0.0, 0L, False, None, tuple(), list()]:
            target.M204(arg)
            Assert(not Flag.BValue, "argument is %s" % (arg,))
            Flag.BValue = True

def test_user_defined_conversion():
    class CP1:
        def __int__(self): return 100
    
    class CP2(object):
        def __int__(self): return 99
    
    class CP3: pass
    cp1, cp2, cp3 = CP1(), CP2(), CP3()

    ### 1. not work for Nullable<Int32> required (?)
    ### 2. (out int): should pass in nothing
    ###      int  params int int?          ref int   defVal  int+defVal
    works = 'M201 M600       M680     M620   M700      M710    M715'
    for fn in works.split():
        _helper(getattr(target, fn), [cp1, cp2, ], int(fn[1:]), [cp3, ], TypeError)
    
    for fn in dir(target):
    ###                                                     bool  obj
        if _self_defined_method(fn) and fn not in (works + 'M204  M400 '):
            _helper(getattr(target, fn), [], 0, [cp1, cp2, cp3, ], TypeError)

def test_pass_in_derived_python_types():
    class CP1(I): pass
    class CP2(C1): pass
    class CP3(C2): pass
    class CP4(C6, I): pass
    cp1, cp2, cp3, cp4 = CP1(), CP2(), CP3(), CP4()

    # I asked
    _helper(target.M401, [C1(), C2(), S1(), cp1, cp2, cp3, cp4,], 401,[C3(), object()], TypeError)
    # C2 asked
    _helper(target.M403, [C2(), cp3, ], 403, [C3(), object(), C1(), cp1, cp2, cp4, ], TypeError)
    
    class CP1(A): pass
    class CP2(C6): pass
    cp1, cp2 = CP1(), CP2()
    
    # A asked
    _helper(target.M410, [C6(), cp1, cp2, cp4,], 410, [C3(), object(), C1(), cp3, ], TypeError)
    # C6 asked
    _helper(target.M411, [C6(), cp2, cp4, ], 411, [C3(), object(), C1(), cp1, cp3,], TypeError)
    
def test_nullable_int():
    _helper(target.M680, [None, 100, 100L, System.Byte.MaxValue, System.UInt32.MinValue, myint1, mylong2, 3.6, ], 680, [(), 3+1j], TypeError)
    
def test_out_int():
    if is_silverlight==False:
        _helper(target.M701, [], 701, [1, 10L, None, System.Byte.Parse('3')], TypeError)    # not allow to pass in anything
    
def test_collections():
    arrayInt = array_int((10, 20))
    tupleInt = ((10, 20), )
    listInt  = ([10, 20], )
    tupleBool = ((True, False, True, True, False), )
    tupleLong1, tupleLong2  = ((10L, 20L), ), ((System.Int64.MaxValue, System.Int32.MaxValue * 2),)
    arrayByte = array_byte((10, 20))
    arrayObj = array_object(['str', 10])
    
    # IList<int>
    _helper(target.M650, [arrayInt, tupleInt, listInt, arrayObj, tupleLong1, tupleLong2, ], 650, [arrayByte, ], TypeError)
    # arrayObj, tupleLong1, tupleLong2 : conversion happens late

    # Array
    _helper(target.M651, [arrayInt, arrayObj, arrayByte, ], 651, [listInt, tupleInt, tupleLong1, tupleLong2, ], TypeError)
    
    # IEnumerable[int]
    _helper(target.M652, [arrayInt, arrayObj, arrayByte, listInt, tupleInt, tupleLong1, tupleLong2, ], 652, [], TypeError)
    
    # IEnumerator[int]
    _helper(target.M653, [], 653, [arrayInt, arrayObj, arrayByte, listInt, tupleInt, tupleLong1, tupleLong2, ], TypeError)
    
    # Int32[]
    _helper(target.M500, [arrayInt, tupleInt, tupleLong1, tupleBool, ], 500, [listInt, arrayByte, arrayObj, ], TypeError)
    _helper(target.M500, [], 500, [tupleLong2, ], OverflowError)
    # params Int32[]
    _helper(target.M600, [arrayInt, tupleInt, tupleLong1, tupleBool, ], 600, [listInt, arrayByte, arrayObj, ], TypeError)
    _helper(target.M600, [], 600, [tupleLong2, ], OverflowError)
    
    # Int32, params Int32[]
    _helper(target.M620, [(10, 10), (10L, 10), (10L, 10L), (10, 10L), (10, arrayInt), (10, (10, 20)), ], 620, [(10, [10, 20]), ], TypeError)
    _helper(target.M620, [], 620, [(10, 123456789101234L), ], OverflowError)
    
    arrayI1 = System.Array[I]( (C1(), C2()) )
    arrayI2 = System.Array[I]( () )
    arrayObj3 = System.Array[object]( (C1(), C2()) )
    tupleI = ((C1(), C2()),)
    listI =  ([C1(), C2()],)
    _helper(target.M510, [arrayI1, arrayI2, tupleI, ], 510, [arrayObj3, listI, ], TypeError)     # I[]
    _helper(target.M610, [arrayI1, arrayI2, tupleI, ], 610, [arrayObj3, listI, ], TypeError)     # params I[]

def test_no_arg_asked():
    # no args asked
    _helper(target.M100, [()], 100, [2, None, (2, None)], TypeError)

def test_enum():
    # E1 asked
    _helper(target.M450, [E1.A, ], 450, [10, E2.A], TypeError)
    # E2: ushort asked
    if is_silverlight==False:
        _helper(target.M451, [E2.A, ], 451, [10, E1.A, System.UInt16.Parse("3")], TypeError)

def _repeat_with_one_arg(goodStr, getArg):
    passSet = _get_funcs(goodStr)
    skipSet = []

    for fn in passSet:
        if fn in skipSet: continue
        
        arg = getArg()
        getattr(target, fn)(arg)
        left = Flag.Value
        right = int(fn[1:])
        if left != right:
            Fail("left %s != right %s when func %s on arg %s" % (left, right, fn, arg))
    
    for fn in dir(target):
        if _self_defined_method(fn) and (fn not in passSet) and (fn not in skipSet):
            arg = getArg()
            try:   getattr(target, fn)(arg)
            except TypeError : pass
            else:  Fail("expect TypeError, but got none when func %s on arg %s" % (fn, arg))

def test_pass_in_none():
    test_str = '''
Bool String Object I C1 C2 A C6
ArrInt32 ArrI ParamArrInt32 ParamArrI ParamArrS IParamArrI
IListInt Array IEnumerableInt IEnumeratorInt NullableInt
'''
    # Big integers are only nullable in CLR 2
    if not is_net40:
        test_str = "BigInt " + test_str
    
    _repeat_with_one_arg(test_str, lambda : None)

def test_pass_in_clrReference():
    import clr
    _repeat_with_one_arg('Object RefInt32  OutInt32', lambda : clr.Reference[int](0))
    _repeat_with_one_arg('Object', lambda : clr.Reference[object](None))
    _repeat_with_one_arg('Object RefInt32  OutInt32', lambda : clr.Reference[int](10))
    _repeat_with_one_arg('Object ', lambda : clr.Reference[float](123.123))
    _repeat_with_one_arg('Object', lambda : clr.Reference[type](str)) # ref.Value = (type)

def test_pass_in_nothing():
    passSet = _get_funcs('NoArg ParamArrInt32 ParamArrS ParamArrI OutInt32 DefValInt32')
    skipSet = [ ]  # be empty before release
    
    for fn in passSet:
        if fn in skipSet: continue
        
        getattr(target, fn)()
        left = Flag.Value
        right = int(fn[1:])
        if left != right:
            Fail("left %s != right %s when func %s on arg Nothing" % (left, right, fn))
    
    for fn in dir(target):
        if _self_defined_method(fn) and (fn not in passSet) and (fn not in skipSet):
            try:   getattr(target, fn)()
            except TypeError : pass
            else:  Fail("expect TypeError, but got none when func %s on arg Nothing" % fn)
    
def test_other_concern():
    target = COtherConcern()
    
    # static void M100()
    target.M100()
    AreEqual(Flag.Value, 100); Flag.Value = 99
    COtherConcern.M100()
    AreEqual(Flag.Value, 100); Flag.Value = 99
    AssertError(TypeError, target.M100, target)
    AssertError(TypeError, COtherConcern.M100, target)
    
    # static void M101(COtherConcern arg)
    target.M101(target)
    AreEqual(Flag.Value, 101); Flag.Value = 99
    COtherConcern.M101(target)
    AreEqual(Flag.Value, 101); Flag.Value = 99
    AssertError(TypeError, target.M101)
    AssertError(TypeError, COtherConcern.M101)
    
    # void M102(COtherConcern arg)
    target.M102(target)
    AreEqual(Flag.Value, 102); Flag.Value = 99
    COtherConcern.M102(target, target)
    AreEqual(Flag.Value, 102); Flag.Value = 99
    AssertError(TypeError, target.M102)
    AssertError(TypeError, COtherConcern.M102, target)
    
    # generic method
    target.M200[int](100)
    AreEqual(Flag.Value, 200); Flag.Value = 99
    target.M200[int](100.1234)
    AreEqual(Flag.Value, 200); Flag.Value = 99
    target.M200[long](100)
    AreEqual(Flag.Value, 200); Flag.Value = 99
    AssertError(OverflowError, target.M200[System.Byte], 300)
    AssertError(OverflowError, target.M200[int], 12345678901234)
    
    # We should ignore Out attribute on non-byref.
    # It's used in native interop scenarios to designate a buffer (StringBUilder, arrays, etc.) 
    # the caller allocates, passes to the method and expects the callee to populate it with data.
    AssertError(TypeError, target.M222)
    AreEqual(target.M222(0), None)
    AreEqual(Flag.Value, 222)
    
    # what does means when passing in None
    target.M300(None)
    AreEqual(Flag.Value, 300); Flag.Value = 99
    AreEqual(Flag.BValue, True)
    target.M300(C1())
    AreEqual(Flag.BValue, False)
    
    # void M400(ref Int32 arg1, out Int32 arg2, Int32 arg3) etc...
    AreEqual(target.M400(1, 100), (100, 100))
    AreEqual(target.M401(1, 100), (100, 100))
    AreEqual(target.M402(100, 1), (100, 100))
    
    # default Value
    target.M450()
    AreEqual(Flag.Value, 80); Flag.Value = 99
    
    # 8 args
    target.M500(1,2,3,4,5,6,7,8)
    AreEqual(Flag.Value, 500)
    AssertError(TypeError, target.M500)
    AssertError(TypeError, target.M500, 1)
    AssertError(TypeError, target.M500, 1,2,3,4,5,6,7,8,9)
    
    # IDictionary
    for x in [ {1:1}, {"str": 3} ]:
        target.M550(x)
        AreEqual(Flag.Value, 550); Flag.Value = 99
    AssertError(TypeError, target.M550, [1, 2])
    
    # not supported
    for fn in (target.M600, target.M601, target.M602):
        for l in ( {1:'a'}, [1,2], (1,2) ):
            AssertError(TypeError, fn, l)
            
    # delegate
    def f(x): return x * x
    AssertError(TypeError, target.M700, f)

    from IronPythonTest import IntIntDelegate
    for x in (lambda x: x, lambda x: x*2, f):
        target.M700(IntIntDelegate(x))
        AreEqual(Flag.Value, x(10)); Flag.Value = 99
    
    target.M701(lambda x: x*2)
    AreEqual(Flag.Value, 20); Flag.Value = 99
    AssertError(TypeError, target.M701, lambda : 10)
    
    # keywords
    x = target.M800(arg1 = 100, arg2 = 200L, arg3 = 'this'); AreEqual(x, 'THIS')
    x = target.M800(arg3 = 'Python', arg1 = 100, arg2 = 200L); AreEqual(x, 'PYTHON')
    x = target.M800(100, arg3 = 'iron', arg2 = C1()); AreEqual(x, 'IRON')
    
    try: target.M800(100, 'Yes', arg2 = C1())
    except TypeError: pass
    else: Fail("expect: got multiple values for keyword argument arg2")
    
    # more ref/out sanity check
    import clr
    def f1(): return clr.Reference[object](None)
    def f2(): return clr.Reference[int](10)
    def f3(): return clr.Reference[S1](S1())
    def f4(): return clr.Reference[C1](C2()) # C2 inherits C1

    for (f, a, b, c, d) in [
        ('M850', False, False, True, False),
        ('M851', False, False, False, True),
        ('M852', False, False, True, False),
        ('M853', False, False, False, True),
    ]:
        expect = (f in 'M850 M852') and S1 or C1
        func = getattr(target, f)
        
        for i in range(4):
            ref = (f1, f2, f3, f4)[i]()
            if (a,b,c,d)[i]:
                func(ref); AreEqual(type(ref.Value), expect)
            else:
                AssertError(TypeError, func, ref)

    # call 854
    AssertError(TypeError, target.M854, clr.Reference[object](None))
    AssertError(TypeError, target.M854, clr.Reference[int](10))
    
    # call 855
    AssertError(TypeError, target.M855, clr.Reference[object](None))
    AssertError(TypeError, target.M855, clr.Reference[int](10))
    
    # call 854 and 855 with Reference[bool]
    target.M854(clr.Reference[bool](True)); AreEqual(Flag.Value, 854)
    target.M855(clr.Reference[bool](True)); AreEqual(Flag.Value, 855)
    
    # practical
    ref = clr.Reference[int](0)
    ref2 = clr.Reference[int](0)
    ref.Value = 300
    ref2.Value = 100
    ## M860(ref arg1, arg2, out arg3): arg3 = arg1 + arg2; arg1 = 100;
    x = target.M860(ref, 200, ref2)
    AreEqual(x, None)
    AreEqual(ref.Value, 100)
    AreEqual(ref2.Value, 500)
    
    # pass one clr.Reference(), and leave the other one open
    ref.Value = 300
    AssertError(TypeError, target.M860, ref, 200)
    
    # the other way
    x = target.M860(300, 200)
    AreEqual(x, (100, 500))
    
    # GOtherConcern<T>
    target = GOtherConcern[int]()
    for x in [100, 200L, 4.56, myint1]:
        target.M100(x)
        AreEqual(Flag.Value, 100); Flag.Value = 99
    
    GOtherConcern[int].M100(target, 200)
    AreEqual(Flag.Value, 100); Flag.Value = 99
    AssertError(TypeError, target.M100, 'abc')
    AssertError(OverflowError, target.M100, 12345678901234)
    
def test_iterator_sequence():
    class C:
        def __init__(self):  self.x = 0
        def __iter__(self):  return self
        def next(self):
            if self.x < 10:
                y = self.x
                self.x += 1
                return y
            else:
                self.x = 0
                raise StopIteration
        def __len__(self): return 10
        
    # different size
    c = C()
    list1 = [1, 2, 3]
    tuple1 = [4, 5, 6, 7]
    str1 = "890123"
    all = (list1, tuple1, str1, c)
    
    target = COtherConcern()
    
    for x in all:
        # IEnumerable / IEnumerator
        target.M620(x)
        AreEqual(Flag.Value, len(x)); Flag.Value = 0
        
        # built in types are not IEnumerator, they are enumerable
        if not isinstance(x, C):
            AssertError(TypeError, target.M621, x)
        else:
            target.M621(x)
            AreEqual(Flag.Value, len(x))

        # IEnumerable<char> / IEnumerator<char>
        target.M630(x)
        AreEqual(Flag.Value, len(x)); Flag.Value = 0
        AssertError(TypeError, target.M631, x)

        # IEnumerable<int> / IEnumerator<int>
        target.M640(x)
        AreEqual(Flag.Value, len(x)); Flag.Value = 0
        AssertError(TypeError, target.M641, x)

    # IList / IList<char> / IList<int>
    for x in (list1, tuple1):
        target.M622(x)
        AreEqual(Flag.Value, len(x))

        target.M632(x)
        AreEqual(Flag.Value, len(x))

        target.M642(x)
        AreEqual(Flag.Value, len(x))

    for x in (str1, c):
        AssertError(TypeError, target.M622, x)
        AssertError(TypeError, target.M632, x)
        AssertError(TypeError, target.M642, x)
       
def test_explicit_inheritance():
    target = CInheritMany1()
    Assert(hasattr(target, "M"))
    target.M()
    AreEqual(Flag.Value, 100)
    I1.M(target); AreEqual(Flag.Value, 100); Flag.Value = 0
    
    target = CInheritMany2()
    target.M(); AreEqual(Flag.Value, 201)
    I1.M(target); AreEqual(Flag.Value, 200)
    
    target = CInheritMany3()
    Assert(not hasattr(target, "M"))
    try: target.M()
    except AttributeError: pass
    else: Fail("Expected AttributeError, got none")
    I1.M(target); AreEqual(Flag.Value, 300)
    I2.M(target); AreEqual(Flag.Value, 301)
    
    target = CInheritMany4()
    target.M(); AreEqual(Flag.Value, 401)
    I3[object].M(target); AreEqual(Flag.Value, 400)
    AssertError(TypeError, I3[int].M, target)
    
    target = CInheritMany5()
    I1.M(target); AreEqual(Flag.Value, 500)
    I2.M(target); AreEqual(Flag.Value, 501)
    I3[object].M(target); AreEqual(Flag.Value, 502)
    target.M(); AreEqual(Flag.Value, 503)
    
    target = CInheritMany6[int]()
    target.M(); AreEqual(Flag.Value, 601)
    I3[int].M(target); AreEqual(Flag.Value, 600)
    AssertError(TypeError, I3[object].M, target)

    target = CInheritMany7[int]()
    Assert(hasattr(target, "M"))
    target.M(); AreEqual(Flag.Value, 700)
    I3[int].M(target); AreEqual(Flag.Value, 700)
    
    target = CInheritMany8()
    Assert(not hasattr(target, "M"))
    try: target.M()
    except AttributeError: pass
    else: Fail("Expected AttributeError, got none")
    I1.M(target); AreEqual(Flag.Value, 800); Flag.Value = 0
    I4.M(target, 100); AreEqual(Flag.Value, 801)
    # target.M(100) ????
    
    # original repro
    from System.Collections.Generic import Dictionary
    d = Dictionary[object,object]()
    d.GetEnumerator() # not throw

def test_nullable_property_double():
    from IronPythonTest import NullableTest
    nt = NullableTest()
    nt.DProperty = 1
    AreEqual(nt.DProperty, 1.0)
    nt.DProperty = 2.0
    AreEqual(nt.DProperty, 2.0)
    nt.DProperty = None
    AreEqual(nt.DProperty, None)
    
@disabled("Merlin 309716")
def test_nullable_property_long():
    from IronPythonTest import NullableTest
    nt = NullableTest()
    nt.LProperty = 1
    AreEqual(nt.LProperty, 1L)
    nt.LProperty = 2L
    AreEqual(nt.LProperty, 2L)
    nt.LProperty = None
    AreEqual(nt.LProperty, None)

def test_nullable_property_bool():
    from IronPythonTest import NullableTest
    nt = NullableTest()
    nt.BProperty = 1.0
    AreEqual(nt.BProperty, True)
    nt.BProperty = 0.0
    AreEqual(nt.BProperty, False)
    nt.BProperty = True
    AreEqual(nt.BProperty, True)
    nt.BProperty = None
    AreEqual(nt.BProperty, None)
    
def test_nullable_property_enum():
    from IronPythonTest import NullableTest
    nt = NullableTest()
    nt.EProperty = NullableTest.NullableEnums.NE1
    AreEqual(nt.EProperty, NullableTest.NullableEnums.NE1)
    nt.EProperty = None
    AreEqual(nt.EProperty, None)

def test_nullable_parameter():
    from IronPythonTest import NullableTest
    nt = NullableTest()
    result = nt.Method(1)
    AreEqual(result, 1.0)
    result = nt.Method(2.0)
    AreEqual(result, 2.0)
    result = nt.Method(None)
    AreEqual(result, None)

# Skip on silverlight because the System.Configuration is not available
@skip("silverlight")
def test_xequals_call_for_optimization():
    """
    Testing specifically for System.Configuration.ConfigurationManager
    because currently its .Equals method will throw null reference
    exception when called with null argument. This is a case that could
    slip through our dynamic site checks.
    """

    import clr
    clr.AddReference("System.Configuration");
    from System.Configuration import ConfigurationManager
    c = ConfigurationManager.ConnectionStrings
    
    #Invoke tests multiple times to make sure DynamicSites are utilized
    for i in xrange(3):
        AreEqual(1, c.Count)
        
    for i in xrange(3):
        count = c.Count
        AreEqual(1, count)
        AreEqual(c.Count, count)
            
    for i in xrange(3):
        #just ensure it doesn't throw
        c[0].Name
        
    #Just to be sure this doesn't throw...
    c.Count
    c.Count
        

def test_interface_only_access():
    pc = InterfaceOnlyTest.PrivateClass
    
    # property set
    pc.Hello = InterfaceOnlyTest.PrivateClass
    # property get
    AreEqual(pc.Hello, pc)
    # method call w/ interface param
    pc.Foo(pc)
    # method call w/ interface ret val
    AreEqual(pc.RetInterface(), pc)
    
    # events
    global fired
    fired = False
    def fired(*args):
        global fired
        fired = True
        return args[0]
    # add event
    pc.MyEvent += fired
    # fire event
    AreEqual(pc.FireEvent(pc.GetEventArgs()), pc)
    AreEqual(fired, True)
    # remove event
    pc.MyEvent -= fired

def test_ref_bytearr():
    target = COtherConcern()
    
    arr = System.Array[System.Byte]((2,3,4))
    
    res = target.M702(arr)
    AreEqual(Flag.Value, 702)
    AreEqual(type(res), System.Array[System.Byte])
    AreEqual(len(res), 0)
    
    i, res = target.M703(arr)
    AreEqual(Flag.Value, 703)
    AreEqual(i, 42)
    AreEqual(type(res), System.Array[System.Byte])
    AreEqual(len(res), 0)
    
    i, res = target.M704(arr, arr)
    AreEqual(Flag.Value, 704)
    AreEqual(i, 42)
    AreEqual(arr, res)
    
    sarr = clr.StrongBox[System.Array[System.Byte]](arr)
    res = target.M702(sarr)
    AreEqual(Flag.Value, 702)
    AreEqual(res, None)
    res = sarr.Value
    AreEqual(type(res), System.Array[System.Byte])
    AreEqual(len(res), 0)
    
    sarr.Value = arr
    i = target.M703(sarr)
    AreEqual(Flag.Value, 703)
    AreEqual(i, 42)
    AreEqual(len(sarr.Value), 0)
    
    i = target.M704(arr, sarr)
    AreEqual(Flag.Value, 704)
    AreEqual(i, 42)
    AreEqual(sarr.Value, arr)

def test_struct_prop_assign():
    from IronPythonTest.BinderTest import SOtherConcern
    a = SOtherConcern()
    a.P100 = 42
    AreEqual(a.P100, 42)
    
def test_generic_type_inference():
    from IronPythonTest import GenericTypeInference, GenericTypeInferenceInstance, SelfEnumerable    
    from System import Array, Exception, ArgumentException
    from System.Collections.Generic import IEnumerable, List
    from System.Collections.Generic import Dictionary as Dict

    class UserGenericType(GenericTypeInferenceInstance): pass

    # public PythonType MInst<T>(T x) -> pytype(T)
    AreEqual(UserGenericType().MInst(42), int)

    class UserObject(object): pass
        
    userInst = UserObject()
    userInt, userLong, userFloat, userComplex, userStr = myint(), mylong(), myfloat(), mycomplex(), mystr()
    userTuple, userList, userDict = mytuple(), mylist(), mydict()
    objArray = System.Array[object]( (1,2,3) )
    doubleArray = System.Array[float]( (1.0,2.0,3.0) )
    

    for target in [GenericTypeInference, GenericTypeInferenceInstance(), UserGenericType()]:    
        tests = [     
         # simple single type tests, no constraints
         # public static PythonType M0<T>(T x) -> pytypeof(T)
         # target method,   args,                            Result,     KeywordCall,      Exception
         (target.M0,        (1, ),                          int,        True,             None),
         (target.M0,        (userInst, ),                   object,     True,             None),
         (target.M0,        (userInt, ),                    object,     True,             None),
         (target.M0,        (userStr, ),                    object,     True,             None),
         (target.M0,        (userLong, ),                   object,     True,             None),
         (target.M0,        (userFloat, ),                  object,     True,             None),
         (target.M0,        (userComplex, ),                object,     True,             None),
         (target.M0,        (userTuple, ),                  tuple,      True,             None),
         (target.M0,        (userList, ),                   list,       True,             None),
         (target.M0,        (userDict, ),                   dict,       True,             None),
         (target.M0,        ((), ),                         tuple,      True,             None),
         (target.M0,        ([], ),                         list,       True,             None),
         (target.M0,        ({}, ),                         dict,       True,             None),
         
         # multiple arguments
         # public static PythonType M1<T>(T x, T y) -> pytypeof(T)
         # public static PythonType M2<T>(T x, T y, T z) -> pytypeof(T)
         (target.M1,        (1, 2),                         int,        True,             None),
         (target.M2,        (1, 2, 3),                      int,        True,             None),
         (target.M1,        (userInst, userInst),           object,     True,             None),
         (target.M2,        (userInst, userInst, userInst), object,     True,             None),
         (target.M1,        (1, 2.0),                       None,       True,             TypeError),
         (target.M1,        (1, 'abc'),                     None,       True,             TypeError),
         (target.M1,        (object(), userInst),           object,     True,             None),
         (target.M1,        ([], userList),                 list,       True,             None),
    
         # params arguments
         # public static PythonType M3<T>(params T[] args) -> pytypeof(T)
         (target.M3,        (),                             None,       False,           TypeError),
         (target.M3,        (1, ),                          int,        False,            None),
         (target.M3,        (1, 2),                         int,        False,            None),
         (target.M3,        (1, 2, 3),                      int,        False,            None),
         (target.M3,        (1, 2.0),                       object,     False,            TypeError),
         (target.M3,        (1, 'abc'),                     object,     False,            TypeError),
         (target.M3,        (object(), userInst),           object,     False,            None),
         (target.M3,        ([], userList),                 list,       False,            None),
         
         # public static PythonType M4<T>(T x, params T[] args) -> pytypeof(T)
         (target.M4,        (1, 2),                         int,        False,            None),
         (target.M4,        (1, 2.0),                       object,     False,            TypeError),
         (target.M4,        (1, 'abc'),                     object,     False,            TypeError),
         (target.M4,        (object(), userInst),           object,     False,            None),
         (target.M4,        ([], userList),                 list,       False,            None),
         
         # simple constraints
         # public static PythonType M5<T>(T x) where T : class -> pytype(T)
         # public static PythonType M6<T>(T x) where T : struct -> pytype(T)
         # public static PythonType M7<T>(T x) where T : IList -> pytype(T)
         (target.M5,        (1, ),                           None,      False,           TypeError),
         (target.M6,        ('abc', ),                       None,      False,           TypeError),
         (target.M7,        (object(), ),                    None,      False,           TypeError),
         (target.M7,        (2, ),                           None,      False,           TypeError),
         (target.M5,        ('abc', ),                       str,        False,           None),
         (target.M5,        (object(), ),                    object,     False,           None),
         (target.M6,        (1, ),                           int,        False,           None),
         (target.M7,        ([], ),                          list,       False,           None),
         (target.M7,        (objArray, ),                    type(objArray),False,        None),
    
         # simple dependent constraints
         # public static PythonTuple M8<T0, T1>(T0 x, T1 y) where T0 : T1 -> (pytype(T0), pytype(T1))     
         (target.M8,        (1, 2),                         (int, int),   False,           None),
         (target.M8,        ('abc', object()),              (str, object),False,           None),
         (target.M8,        (object(), 'abc'),              None,         False,           TypeError),
         (target.M8,        (1, object()),                  (int, object),False,           None),
         (target.M8,        (object(), 1),                  None,         False,           TypeError),
    
         # no types can be inferred, error
         # public static PythonTuple M9<T0, T1>(object x, T1 y) where T0 : T1
         # public static PythonTuple M9b<T0, T1>(T0 x, object y) where T0 : T1
         # public static PythonType M11<T>(object x)
         # public static PythonType M12<T0, T1>(T0 x, object y)     
         (target.M9,        (1, 2),                         None,         False,           TypeError),
         (target.M9b,       (1, 2),                         None,         False,           TypeError),
         (target.M9,        (object(), object()),           None,        True,             TypeError),
         (target.M9b,       (object(), object()),           None,        True,             TypeError),
         (target.M11,       (1, ),                          None,         False,           TypeError),
         (target.M12,       (1, 2),                         None,         False,           TypeError),
         
         # multiple dependent constraints
         # public static PythonTuple M10<T0, T1, T2>(T0 x, T1 y, T2 z) where T0 : T1 where T1 : T2 -> (pytype(T0), pytype(T1), pytype(T2))
         (target.M10,        (ArgumentException(), Exception(), object()), (ArgumentException, Exception, object),False,           None),
         (target.M10,        (Exception(), ArgumentException(), object()), None,False,           TypeError),
         (target.M10,        (ArgumentException(), object(), Exception()), None,False,           TypeError),
         (target.M10,        (object(), ArgumentException(), Exception()), None,False,           TypeError),
         (target.M10,        (object(), Exception(), ArgumentException()), None,False,           TypeError),     
         
         # public static PythonType M11<T>(object x) -> pytypeof(T)
         # public static PythonType M12<T0, T1>(T0 x, object y) -> pytypeof(T0)
         (target.M11,       (object(), ),                   None,       True,             TypeError),
         (target.M12,       (3, object()),                  None,       True,             TypeError),     
         
         # public static PythonType M13<T>(T x, Func<T> y) -> pytype(T), func()
         # public static PythonType M14<T>(T x, Action<T> y) -> pytype(T)
         # public static PythonTuple M15<T>(T x, IList<T> y) -> pytype, list...
         # public static PythonType M16<T>(T x, Dictionary<T, IList<T>> list) -> pytype, listKeys...
         (target.M13,       (1, lambda: 42),               (object, 42),    False,           None),
         (target.M14,       (1, lambda x: None),           object,          False,           None),
         (target.M15,       (1, [2, ]),                     (object, 2),     True,           None),
         (target.M15,       (1, (2, )),                     (object, 2),     True,           None),
         (target.M15,       (1, objArray),                  (object, 1,2,3), True,           None),
         (target.M15,       (1, doubleArray),               None,            True,           TypeError),
         (target.M16,       (1, {1: [1,2]}),               None,             False,         TypeError),
    
         # public static PythonType M17<T>(T x, IEnumerable<T> y) -> pytype(T)
         (target.M17,       (SelfEnumerable(), SelfEnumerable()), SelfEnumerable,    True,  None),
         (target.M17,       (1, [1,2,3]),                  object,           True,         None),
         (target.M17,       (1.0, [1,2,3]),                object,           True,         None),
         (target.M17,       (object(), [1,2,3]),           object,           True,         None),
         
         # public static PythonType M18<T>(T x) where T : IEnumerable<T> -> pytype(T)
         (target.M18,       (SelfEnumerable(), ),          SelfEnumerable,    True,         None),
         
         # public static PythonType M19<T0, T1>(T0 x, T1 y) where T0 : IList<T1> -> pytype(T0), pytype(T1)
         (target.M19,       ([], 1),                       None,             True,         TypeError),
         (target.M19,       (List[int](), 1),              (List[int], int), True,         None),
         
         # public static PythonType M20<T0, T1>(T0 x, T1 y) -> pytype(T0), pytype(T1)
         (target.M20,       ([], 1),                       (list, int),      True,         None),
         (target.M20,       (List[int](), 1),              (List[int], int), True,         None),
         
         # constructed types
         # public static PythonType M21<T>(IEnumerable<T> enumerable)
         (target.M21,       ([1,2,3], ),                   object,   False,         None),
         
         # overloaded by function
         # public static PythonTuple M22<T>(IEnumerable<T> enumerable, Func<T, bool> predicate) -> pytype(T), True
         # public static PythonTuple M22<T>(IEnumerable<T> enumerable, Func<T, int, bool> predicate) -> pytype(T), False
         (target.M22,       ([1,2,3], lambda x:True),     (object, True),   True,         None),
         (target.M22,       ([1,2,3], lambda x,y:True),   (object, False),  True,         None),
         
         # public static PythonType M23<T>(List<T> x) -> pytype(T)
         # public static PythonType M24<T>(List<List<T>> x) -> pytype(T)
         # public static PythonType M25<T>(Dictionary<T, T> x) -> pytype(T)
         (target.M23,       (List[int](), ),               int,              True,         None),
         (target.M24,       (List[List[int]](), ),         int,              True,         None),     
         (target.M25,       (Dict[int, int](), ),          int,              True,         None),
         (target.M25,       (Dict[int, str](), ),          None,              True,         TypeError),
         
         # constructed types and constraints
         # public static PythonType M26<T>(List<T> x) where T : class -> pytype(T)
         # public static PythonType M27<T>(List<T> x) where T : struct -> pytype(T)
         # public static PythonType M28<T>(List<T> x) where T : new() -> pytype(T)
         (target.M26,       (List[int](), ),                None,      False,           TypeError),
         (target.M27,       (List[str](), ),                None,      False,           TypeError),
         (target.M28,       (List[str](), ),                None,      False,           TypeError),
         (target.M26,       (List[str](), ),                str,       False,           None),
         (target.M27,       (List[int](), ),                int,       False,           None),
         (target.M28,       (List[List[str]](), ),          List[str], False,           None),
         
         # public static PythonType M29<T>(Dictionary<Dictionary<T, T>, Dictionary<T, T>> x)
         (target.M29,       (Dict[Dict[int, int], Dict[int, int]](), ),          int,              True,         None),
         
         # constraints and constructed types
         # public static PythonType M30<T>(Func<T, bool> y) where T : struct -> pytype(T)
         # public static PythonType M31<T>(Func<T, bool> y) where T : IList -> pytype(T)
         # public static PythonType M32<T>(List<T> y) where T : new() -> pytype(T)
         # public static PythonType M33<T>(List<T> y) where T : class -> pytype(T)
         (target.M30,       (lambda x: False, ),            int,              True,         TypeError),
         (target.M31,       (lambda x: False, ),            int,              True,         TypeError),
         (target.M32,       (List[str](), ),                 int,              True,         TypeError),
         (target.M33,       (List[int](), ),                 int,              True,         TypeError),
         
         # public static PythonType M34<T>(IList<T> x, IList<T> y) -> pytype(T)
         (target.M34,       ((), [], ),                     object,            True,         None),
         
         # T[] and IList<T> overloads:
         (target.M35,       (objArray, ),                    System.Array[object], False,    None),
        ]
        
        # TODO: more by-ref and arrays tests:
        x = Array.Resize(Array.CreateInstance(int, 10), 20)
        AreEqual(x.Length, 20)
        
        for method, args, res, kwArgs, excep in tests:
            generic_method_tester(method, args, res, kwArgs, excep)

def generic_method_tester(method, args, res, kwArgs, excep):
    #print method, args, res, excep
    if excep is None:
        # test method w/ multiple calling conventions
        if len(args) == 1:
            AreEqual(method(args[0]), res)
            if kwArgs:
                AreEqual(method(x = args[0]), res)
                AreEqual(method(**{'x' : args[0]}), res)
        elif len(args) == 2:
            AreEqual(method(args[0], args[1]), res)
            if kwArgs:
                AreEqual(method(x = args[0], y = args[1]), res)
                AreEqual(method(args[0], y = args[1]), res)
                AreEqual(method(y = args[1], x = args[0]), res)
                AreEqual(method(*(args[0], ), **{'y' : args[1]}), res)
                AreEqual(method(**{'x' : args[0], 'y' : args[1]}), res)
        elif len(args) == 3:
            AreEqual(method(args[0], args[1], args[2]), res)
            if kwArgs:
                AreEqual(method(x = args[0], y = args[1], z = args[2]), res)
                AreEqual(method(args[0], y = args[1], z = args[2]), res)
                AreEqual(method(args[0], args[1], z = args[2]), res)
                AreEqual(method(z = args[2], y = args[1], x = args[0]), res)
                AreEqual(method(*(args[0], args[1]), **{'z' : args[2]}), res)
                AreEqual(method(*(args[0], ), **{'y': args[1], 'z' : args[2]}), res)
                AreEqual(method(**{'x' : args[0], 'y' : args[1], 'z' : args[2]}), res)
        else:
            raise Exception, "need to add new case for len %d " % len(args)
            
        AreEqual(method(*args), res)
        AreEqual(method(args[0], *args[1:]), res)
    else:            
        # test error method w/ multiple calling conventions
        if len(args) == 0:
            f = lambda : method()
            fkw, fkw2 = None, None
        elif len(args) == 1:
            f = lambda : method(args[0])
            fkw = lambda : method(x = args[0])
            fkw2 = lambda : method(**{'x' : args[0]})
        elif len(args) == 2:
            f = lambda : method(args[0], args[1])
            fkw = lambda : method(x = args[0], y = args[1])
            fkw2 = lambda : method(**{'x' : args[0], 'y' : args[1]})
        elif len(args) == 3:
            f = lambda : method(args[0], args[1], args[2])
            fkw = lambda : method(x = args[0], y = args[1], z = args[2])
            fkw2 = lambda : method(**{'x' : args[0], 'y' : args[1], 'z' : args[2]})
        else:
            raise Exception, "need to add new case for len %d " % len(args)
    
        if not kwArgs:
            fkw = None
            fkw2 = None
        
        # test w/o splatting
        AssertError(excep, f)    
        if fkw: AssertError(excep, fkw)
        if fkw2: AssertError(excep, fkw2)
        # test with splatting
        AssertError(excep, method, *args)    
    
print '>>>> methods in reference type'
target = CNoOverloads()
run_test(__name__)

