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

from iptest.assert_util import *

if sys.platform!="win32":
    load_iron_python_test()
    from IronPythonTest import *
    
    import clr
    if not is_silverlight:
        clr.AddReferenceByPartialName("System.Drawing")
    

@skip("win32 silverlight")
def test_sys_drawing():
    from System.Drawing import Point, Size, PointF, SizeF, Rectangle, RectangleF
    x = Point()
    Assert(x == Point(0,0))
    x = Size()
    Assert(x == Size(0,0))
    x = PointF()
    Assert(x == PointF(0,0))
    x = SizeF()
    Assert(x == SizeF(0,0))
    x = Rectangle()
    Assert(x == Rectangle(0,0,0,0))
    x = RectangleF()
    Assert(x == RectangleF(0,0,0,0))

    p = Point(3,4)
    s = Size(2,9)
    
    q = p + s
    Assert(q == Point(5,13))
    Assert(q != Point(13,5))
    q = p - s
    Assert(q == Point(1,-5))
    Assert(q != Point(0,4))
    q += s
    Assert(q == Point(3,4))
    Assert(q != Point(2,4))
    q -= Size(1,2)
    Assert(q == Point(2,2))
    Assert(q != Point(1))
    
    t = s
    Assert(t == s)
    Assert(t != s - Size(1,0))
    t += Size(3,1)
    Assert(t == Size(5,10))
    Assert(t != Size(5,0))
    t -= Size(2,8)
    Assert(t == Size(3,2))
    Assert(t != Size(0,2))
    t = s + Size(-1,-2)
    Assert(t == Size(1,7))
    Assert(t != Size(1,5))
    t = s - Size(1,2)
    Assert(t == Size(1,7))
    Assert(t != Size(1,3))

    def weekdays(enum):
        return enum.Mon|enum.Tue|enum.Wed|enum.Thu|enum.Fri

    def weekend(enum):
        return enum.Sat|enum.Sun

    def enum_helper(enum):
        days = [enum.Mon,enum.Tue,enum.Wed,enum.Thu,enum.Fri,enum.Sat,enum.Sun]
        x = enum.Mon|enum.Tue|enum.Wed|enum.Thu|enum.Fri|enum.Sat|enum.Sun
        y = enum.Mon
        for day in days:
            y |= day
        Assert(x == y)
        Assert((x <> y) == False)
        if x == y:  # EqualRetBool
            b = True
        else :
            b = False
        Assert(b)
    
        Assert(x == weekdays(enum)|weekend(enum))
        Assert(x == (weekdays(enum)^weekend(enum)))
        Assert((weekdays(enum)&weekend(enum)) == enum.None)
        Assert(weekdays(enum) == enum.Weekdays)
        Assert(weekend(enum) == enum.Weekend)
        Assert(weekdays(enum) != enum.Weekend)
        Assert(weekdays(enum) != weekend(enum))
    
    for e in [DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong]:
        enum_helper(e)

    for e in [DaysInt, DaysShort, DaysLong, DaysSByte]:
        z = operator.inv(e.Mon)
        AreEqual(type(z), e)
        AreEqual(z.ToString(), "-2")

    for (e, v) in [ (DaysByte,254), (DaysUShort,65534), (DaysUInt,4294967294), (DaysULong,18446744073709551614) ]:
        z = operator.inv(e.Mon)
        AreEqual(type(z), e)
        AreEqual(z.ToString(), str(v))
    
    AssertError(ValueError, lambda: DaysInt.Mon & DaysShort.Mon)
    AssertError(ValueError, lambda: DaysInt.Mon | DaysShort.Mon)
    AssertError(ValueError, lambda: DaysInt.Mon ^ DaysShort.Mon)
    AssertError(ValueError, lambda: DaysInt.Mon & 1)
    AssertError(ValueError, lambda: DaysInt.Mon | 1)
    AssertError(ValueError, lambda: DaysInt.Mon ^ 1)
    
    def f():
        if DaysInt.Mon == DaysShort.Mon: return True
        return False
    
    AreEqual(f(), False)
    
    Assert(not DaysInt.Mon == None)
    Assert(DaysInt.Mon != None)

@skip("win32 silverlight")
def test_cp3982():
    from System.Drawing import Color
    test_funcs = [  lambda x: x,
                    lambda x: [x],
                    lambda x: (x),
                    lambda x: [[x]],
                    lambda x: [(x)],
                    lambda x: ((x)),
                    lambda x: ([x]),
                    lambda x: [[[x]]],
                    lambda x: (((x))),
                    lambda x: [x, x],
                    lambda x: (x, x),
                    lambda x: [(x), [x, x]],
                    lambda x: ([x, x], (x)),
                   ]

    for test_func in test_funcs:
        Assert(test_func(Color.Red)==test_func(Color.Red))
        Assert(test_func(Color.Red)!=test_func(Color.Green))
        Assert(test_func(Color.Green)!=test_func(Color.Red))

    Assert( [Color.Green, Color.Red]  == [Color.Green, Color.Red])
    Assert([(Color.Green, Color.Red)] == [(Color.Green, Color.Red)])
    Assert( [Color.Green, Color.Red]  != (Color.Green, Color.Red))
    Assert( [Color.Green, Color.Red]  != [Color.Green, Color.Black])

#------------------------------------------------------------------------------
import operator

def test_operator_module():
    x = ['a','b','c','d']
    g = operator.itemgetter(2)
    AreEqual(g(x), 'c')
    
    class C:
        a = 10
    g = operator.attrgetter("a")
    AreEqual(g(C), 10)
    AreEqual(g(C()), 10)
    
    a = { 'k' : 'v' }
    g = operator.itemgetter('x')
    AssertError(KeyError, g, a)
    
    x = True
    AreEqual(x, True)
    AreEqual(not x, False)
    x = False
    AreEqual(x, False)
    AreEqual(not x, True)
    
    
    class C:
        def func(self):
           pass
    
    a = C.func
    b = C.func
    AreEqual(a, b)
    
    c = C()
    a = c.func
    b = c.func
    AreEqual(a, b)
    
    # __setitem__
    x = {}
    operator.__setitem__(x, 'abc', 'def')
    AreEqual(x, {'abc':'def'})
    
    # __not__
    x = True
    AreEqual(operator.__not__(x), False)

########################
# string multiplication
def test_string_mult():
    class foo(int): pass
    
    fooInst = foo(3)
    
    AreEqual('aaa', 'a' * 3)
    AreEqual('aaa', 'a' * 3L)
    AreEqual('aaa', 'a' * fooInst)
    
    AreEqual('', 'a' * False)
    AreEqual('a', 'a' * True)


###############################
# (not)equals overloading semantics
def test_eq_ne_overloads():
    class CustomEqual:
        def __eq__(self, other):
            return 7
    
    AreEqual((CustomEqual() == 1), 7)

    for base_type in [
                        dict, list, tuple,
                        float, long, int, complex,
                        str, unicode,
                        object,
                      ]:

        class F(base_type):
            def __eq__(self, other):
                return other == 'abc'
            def __ne__(self, other):
                return other == 'def'
        
        AreEqual(F() == 'abc', True)
        AreEqual(F() != 'def', True)
        AreEqual(F() == 'qwe', False)
        AreEqual(F() != 'qwe', False)



# Test binary operators for all numeric types and types inherited from them
def test_num_binary_ops():
    class myint(int): pass
    class mylong(long): pass
    class myfloat(float): pass
    class mycomplex(complex): pass
    
    l = [2, 10L, (1+2j), 3.4, myint(7), mylong(5), myfloat(2.32), mycomplex(3, 2), True]
    
    if is_cli or is_silverlight:
        l.append(System.Int64.Parse("5"))
    
    def add(a, b): return a + b
    def sub(a, b): return a - b
    def mul(a, b): return a * b
    def div(a, b): return a / b
    def mod(a, b): return a % b
    def truediv(a,b): return a / b
    def floordiv(a,b): return a // b
    def pow(a,b): return a ** b
    
    op = [
        ('+', add, True),
        ('-', sub, True),
        ('*', mul, True),
        ('/', div, True),
        ('%', mod, False),
        ('//', floordiv, False),
        ('**', pow, True)
        ]
    
    for a in l:
        for b in l:
            for sym, fnc, cmp in op:
                if cmp or (not isinstance(a, complex) and not isinstance(b, complex)):
                    try:
                        r = fnc(a,b)
                    except:
                        (exc_type, exc_value, exc_traceback) = sys.exc_info()
                        Fail("Binary operator failed: %s, %s: %s %s %s (Message=%s)" % (type(a).__name__, type(b).__name__, str(a), sym, str(b), str(exc_value)))
    
    
    threes = [ 3, 3L, 3.0 ]
    zeroes = [ 0, 0L, 0.0 ]
    
    if is_cli or is_silverlight:
        threes.append(System.Int64.Parse("3"))
        zeroes.append(System.Int64.Parse("0"))
    
    for i in threes:
        for j in zeroes:
            for fnc in [div, mod, truediv, floordiv]:
                try:
                    r = fnc(i, j)
                except ZeroDivisionError:
                    pass
                else:
                    (exc_type, exc_value, exc_traceback) = sys.exc_info()
                    Fail("Didn't get ZeroDivisionError %s, %s, %s, %s, %s (Message=%s)" % (str(func), type(i).__name__, type(j).__name__, str(i), str(j), str(exc_value)))

#------------------------------------------------------------------------------
def test_unary_ops():
    if is_cli or is_silverlight:
        unary = UnaryClass(9)
        AreEqual(-(unary.value), (-unary).value)
        AreEqual(~(unary.value), (~unary).value)
    
    # testing customized unary op
    class C1:
        def __pos__(self):
            return -10
        def __neg__(self):
            return 10
        def __invert__(self):
            return 20
        def __abs__(self):
            return 30
    
    class C2(object):
        def __pos__(self):
            return -10
        def __neg__(self):
            return 10
        def __invert__(self):
            return 20
        def __abs__(self):
            return 30
    
    for x in C1(), C2():
        AreEqual(+x, -10)
        AreEqual(-x, 10)
        AreEqual(~x, 20)
        AreEqual(abs(x), 30)

#------------------------------------------------------------------------------
# testing custom divmod operator
def test_custom_divmod():
    class DM:
        def __divmod__(self, other):
            return "__divmod__"
    
    class NewDM(int): pass
    
    class Callable:
        def __call__(self, other):
            return "__call__"
    
    class CallDM:
        __divmod__ = Callable()
    
    AreEqual(divmod(DM(), DM()), "__divmod__")
    AreEqual(divmod(DM(), 10), "__divmod__")
    AreEqual(divmod(NewDM(10), NewDM(5)), (2, 0))
    AreEqual(divmod(CallDM(), 2), "__call__")

#####################################################################
# object identity of booleans - __ne__ should return "True" or "False", not a new boxed bool
def test_bool_obj_id():
    AreEqual(id(complex.__ne__(1+1j, 1+1j)), id(False))
    AreEqual(id(complex.__ne__(1+1j, 1+2j)), id(True))

#------------------------------------------------------------------------------
def test_sanity():
    '''
    Performs a set of simple sanity checks on most operators.
    '''
    
    #__abs__
    AreEqual(operator.__abs__(0), 0)
    AreEqual(operator.__abs__(1), 1)
    AreEqual(operator.__abs__(-1), 1)
    AreEqual(operator.__abs__(0.0), 0.0)
    AreEqual(operator.__abs__(1.1), 1.1)
    AreEqual(operator.__abs__(-1.1), 1.1)
    AreEqual(operator.__abs__(0L), 0L)
    AreEqual(operator.__abs__(1L), 1L)
    AreEqual(operator.__abs__(-1L), 1L)
    
    #__neg__
    AreEqual(operator.__neg__(0), 0)
    AreEqual(operator.__neg__(1), -1)
    AreEqual(operator.__neg__(-1), 1)
    AreEqual(operator.__neg__(0.0), 0.0)
    AreEqual(operator.__neg__(1.1), -1.1)
    AreEqual(operator.__neg__(-1.1), 1.1)
    AreEqual(operator.__neg__(0L), 0L)
    AreEqual(operator.__neg__(1L), -1L)
    AreEqual(operator.__neg__(-1L), 1L)
    
    #__pos__
    AreEqual(operator.__pos__(0), 0)
    AreEqual(operator.__pos__(1), 1)
    AreEqual(operator.__pos__(-1), -1)
    AreEqual(operator.__pos__(0.0), 0.0)
    AreEqual(operator.__pos__(1.1), 1.1)
    AreEqual(operator.__pos__(-1.1), -1.1)
    AreEqual(operator.__pos__(0L), 0L)
    AreEqual(operator.__pos__(1L), 1L)
    AreEqual(operator.__pos__(-1L), -1L)
    
    #__add__
    AreEqual(operator.__add__(0, 0), 0)
    AreEqual(operator.__add__(1, 2), 3)
    AreEqual(operator.__add__(-1, 2), 1)
    AreEqual(operator.__add__(0.0, 0.0), 0.0)
    AreEqual(operator.__add__(1.1, 2.1), 3.2)
    AreEqual(operator.__add__(-1.1, 2.1), 1.0)
    AreEqual(operator.__add__(0L, 0L), 0L)
    AreEqual(operator.__add__(1L, 2L), 3L)
    AreEqual(operator.__add__(-1L, 2L), 1L)
    
    #__sub__
    AreEqual(operator.__sub__(0, 0), 0)
    AreEqual(operator.__sub__(1, 2), -1)
    AreEqual(operator.__sub__(-1, 2), -3)
    AreEqual(operator.__sub__(0.0, 0.0), 0.0)
    AreEqual(operator.__sub__(1.1, 2.1), -1.0)
    AreEqual(operator.__sub__(-1.1, 2.1), -3.2)
    AreEqual(operator.__sub__(0L, 0L), 0L)
    AreEqual(operator.__sub__(1L, 2L), -1L)
    AreEqual(operator.__sub__(-1L, 2L), -3L)
    
    #__mul__
    AreEqual(operator.__mul__(0, 0), 0)
    AreEqual(operator.__mul__(1, 2), 2)
    AreEqual(operator.__mul__(-1, 2), -2)
    AreEqual(operator.__mul__(0.0, 0.0), 0.0)
    AreEqual(operator.__mul__(2.0, 3.0), 6.0)
    AreEqual(operator.__mul__(-2.0, 3.0), -6.0)
    AreEqual(operator.__mul__(0L, 0L), 0L)
    AreEqual(operator.__mul__(1L, 2L), 2L)
    AreEqual(operator.__mul__(-1L, 2L), -2L)
    
    #__div__
    AreEqual(operator.__div__(0, 1), 0)
    AreEqual(operator.__div__(4, 2), 2)
    AreEqual(operator.__div__(-1, 2), -1)
    AreEqual(operator.__div__(0.0, 1.0), 0.0)
    AreEqual(operator.__div__(4.0, 2.0), 2.0)
    AreEqual(operator.__div__(-4.0, 2.0), -2.0)
    AreEqual(operator.__div__(0L, 1L), 0L)
    AreEqual(operator.__div__(4L, 2L), 2L)
    AreEqual(operator.__div__(-4L, 2L), -2L)
    
    #__floordiv__
    AreEqual(operator.__floordiv__(0, 1), 0)
    AreEqual(operator.__floordiv__(4, 2), 2)
    AreEqual(operator.__floordiv__(-1, 2), -1)
    AreEqual(operator.__floordiv__(0.0, 1.0), 0.0)
    AreEqual(operator.__floordiv__(4.0, 2.0), 2.0)
    AreEqual(operator.__floordiv__(-4.0, 2.0), -2.0)
    AreEqual(operator.__floordiv__(0L, 1L), 0L)
    AreEqual(operator.__floordiv__(4L, 2L), 2L)
    AreEqual(operator.__floordiv__(-4L, 2L), -2L)

    #__truediv__
    AreEqual(operator.__truediv__(0, 1), 0)
    AreEqual(operator.__truediv__(4, 2), 2)
    AreEqual(operator.__truediv__(-1, 2), -0.5)
    AreEqual(operator.__truediv__(0.0, 1.0), 0.0)
    AreEqual(operator.__truediv__(4.0, 2.0), 2.0)
    AreEqual(operator.__truediv__(-1.0, 2.0), -0.5)
    AreEqual(operator.__truediv__(0L, 1L), 0L)
    AreEqual(operator.__truediv__(4L, 2L), 2L)
    AreEqual(operator.__truediv__(-4L, 2L), -2L)
    
    #__mod__
    AreEqual(operator.__mod__(0, 1), 0)
    AreEqual(operator.__mod__(4, 2), 0)
    AreEqual(operator.__mod__(-1, 2), 1)
    AreEqual(operator.__mod__(0.0, 1.0), 0.0)
    AreEqual(operator.__mod__(4.0, 2.0), 0.0)
    AreEqual(operator.__mod__(-1.0, 2.0), 1.0)
    AreEqual(operator.__mod__(0L, 1L), 0L)
    AreEqual(operator.__mod__(4L, 2L), 0L)
    AreEqual(operator.__mod__(-4L, 2L), 0L)
    
    #__inv__
    AreEqual(operator.__inv__(0), -1)
    AreEqual(operator.__inv__(1), -2)
    AreEqual(operator.__inv__(-1), 0)
    AreEqual(operator.__inv__(0L), -1L)
    AreEqual(operator.__inv__(1L), -2L)
    AreEqual(operator.__inv__(-1L), 0L)

    #__invert__
    AreEqual(operator.__invert__(0), -1)
    AreEqual(operator.__invert__(1), -2)
    AreEqual(operator.__invert__(-1), 0)
    AreEqual(operator.__invert__(0L), -1L)
    AreEqual(operator.__invert__(1L), -2L)
    AreEqual(operator.__invert__(-1L), 0L)

    #__lshift__
    AreEqual(operator.__lshift__(0, 1), 0)
    AreEqual(operator.__lshift__(1, 1), 2)
    AreEqual(operator.__lshift__(-1, 1), -2)
    AreEqual(operator.__lshift__(0L, 1), 0L)
    AreEqual(operator.__lshift__(1L, 1), 2L)
    AreEqual(operator.__lshift__(-1L, 1), -2L)
    
    #__rshift__
    AreEqual(operator.__rshift__(1, 1), 0)
    AreEqual(operator.__rshift__(2, 1), 1)
    AreEqual(operator.__rshift__(-1, 1), -1)
    AreEqual(operator.__rshift__(1L, 1), 0L)
    AreEqual(operator.__rshift__(2L, 1), 1L)
    AreEqual(operator.__rshift__(-1L, 1), -1L)
    
    #__not__
    AreEqual(operator.__not__(0), 1)
    AreEqual(operator.__not__(1), 0)
    AreEqual(operator.__not__(-1), 0)
    AreEqual(operator.__not__(0L), 1)
    AreEqual(operator.__not__(1L), 0)
    AreEqual(operator.__not__(-1L), 0)
    
    #__and__
    AreEqual(operator.__and__(0, 0), 0)
    AreEqual(operator.__and__(1, 1), 1)
    AreEqual(operator.__and__(0, 1), 0)
    AreEqual(operator.__and__(1, 0), 0)
    
    #__xor__
    AreEqual(operator.__xor__(0, 0), 0)
    AreEqual(operator.__xor__(1, 1), 0)
    AreEqual(operator.__xor__(0, 1), 1)
    AreEqual(operator.__xor__(1, 0), 1)
    
    #__or__
    AreEqual(operator.__or__(0, 0), 0)
    AreEqual(operator.__or__(1, 1), 1)
    AreEqual(operator.__or__(0, 1), 1)
    AreEqual(operator.__or__(1, 0), 1)

    #__concat__
    AreEqual(operator.__concat__([0], [1]), [0,1])
    AreEqual(operator.__concat__([2], [1]), [2,1])
    AreEqual(operator.__concat__([-1], [1]), [-1,1])
    
    #__contains__
    Assert(operator.__contains__("abc", "c"))
    Assert(not operator.__contains__("abc", "d"))
    Assert(operator.__contains__("abc", ""))
    Assert(not operator.__contains__("", "c"))
    Assert(operator.__contains__([1,2,3], 1))
    Assert(not operator.__contains__([1,2,3], 4))
    
    #__getitem__
    AreEqual(operator.__getitem__("abc", 2), "c")
    AssertError(IndexError, operator.__getitem__, "abc", 3)
    AreEqual(operator.__getitem__([1,2,3], 2), 3)
    AssertError(IndexError, operator.__getitem__, [1,2,3], 3)
    
    #__setitem__
    AssertError(TypeError, operator.__setitem__, "abc", 2, "d")
    t_list = [1,2,3]
    operator.__setitem__(t_list, 2, 4)
    AreEqual(t_list, [1,2,4])
    AssertError(IndexError, operator.__setitem__, [1,2,3], 4, 9)
    
    #__delitem__
    #UNIMPLEMENTED
    #AssertError(TypeError, operator.__delitem__, "abc", 2)
    t_list = [1,2,3]
    operator.__delitem__(t_list, 2)
    AreEqual(t_list, [1,2])
    AssertError(IndexError, operator.__delitem__, [1,2,3], 4)
    
    #__repeat__
    AreEqual(operator.__repeat__("abc", 2), "abcabc")
    AreEqual(operator.__repeat__("", 2), "")
    AreEqual(operator.__repeat__([1,2,3], 2), [1,2,3,1,2,3])
    
    #__getslice__
    AreEqual(operator.__getslice__("abc", 1, 2), "b")
    AreEqual(operator.__getslice__("abc", 0, 3), "abc")
    AreEqual(operator.__getslice__("", 0, 0), "")
    AreEqual(operator.__getslice__([1,2,3], 1, 2), [2])
    AreEqual(operator.__getslice__([1,2,3], 0, 3), [1,2,3])
    AreEqual(operator.__getslice__([], 0, 0), [])
    
    #__delslice__
    t_list = [1,2,3]
    operator.__delslice__(t_list, 1, 2)
    AreEqual(t_list, [1,3])
    
    t_list = [1,2,3]
    operator.__delslice__(t_list, 0, 3)
    AreEqual(t_list, [])
    
    t_list = [1,2,3]
    operator.__delslice__(t_list, 0, 0)
    AreEqual(t_list, [1,2,3])
    
    #__setslice__
    t_list = [1,2,3]
    operator.__setslice__(t_list, 1, 2, [9])
    AreEqual(t_list, [1,9,3])
    
    t_list = [1,2,3]
    operator.__setslice__(t_list, 0, 3, [9, 8])
    AreEqual(t_list, [9, 8])
    
    t_list = [1,2,3]
    operator.__setslice__(t_list, 0, 0, [9])
    AreEqual(t_list, [9,1, 2,3])

def test_py25_operator():
    ops = ['iadd', 'isub', 'idiv', 'ilshift', 'imod', 'imul', 'ior', 'ipow', 'irshift', 'isub', 'itruediv', 'ifloordiv', 'ixor']
   
    class foo(object):
        for x in ops:
            exec 'def __%s__(self, other): return "%s", other' % (x, x)

    for x in ops:
        AreEqual(getattr(operator, x)(foo(), 42), (x, 42))
        AreEqual(getattr(operator, '__' + x + '__')(foo(), 42), (x, 42))

def test_concat_repeat():
    AssertError(TypeError, operator.concat, 2, 3)
    AssertError(TypeError, operator.repeat, 2, 3)

def test_addition_error():
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for +: 'int' and 'str'", lambda : 2 + 'abc')
        
run_test(__name__)
