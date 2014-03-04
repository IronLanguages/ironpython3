######################################################################################
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

def x(a,b,c):
    z = 8
    if a < b:
        return c
    elif c < 5 :
        return a + b
    else:
        return z


Assert(x(1,2,10) == 10)
Assert(x(2,1,4) == 3)
Assert(x(1,1,10) == 8)

def f():
    pass

f.a = 10

Assert(f.a == 10)
AreEqual(f.__module__, __name__)

def g():
    g.a = 20

g()

Assert(g.a == 20)


def foo(): pass

AreEqual(foo.func_code.co_filename.lower().endswith('test_function.py'), True)
AreEqual(foo.func_code.co_firstlineno, 48)  # if you added lines to the top of this file you need to update this number.


# Cannot inherit from a function
def CreateSubType(t):
    class SubType(t): pass
    return SubType
    
if is_silverlight==False:
    AssertErrorWithMatch(TypeError, ".*\n?.* is not an acceptable base type", CreateSubType, type(foo))
else:
    try:
        CreateSubType(type(foo))
    except TypeError, e:
        Assert(e.message.find("is not an acceptable base type") != -1)

def a(*args): return args
def b(*args): return a(*args)
AreEqual(b(1,2,3), (1,2,3))

# some coverage for Function3 code
def xwd(a=0,b=1,c=3):
    z = 8
    if a < b:
        return c
    elif c < 5 :
        return a + b
    else:
        return z
        
AreEqual(x,x)
AreEqual(xwd(), 3)
AssertError(TypeError, (lambda:x()))
AreEqual(xwd(2), 3)
AssertError(TypeError, (lambda:x(1)))
AreEqual(xwd(0,5), 3)
AssertError(TypeError, (lambda:x(0,5)))
AreEqual( (x == "not-a-Function3"), False)

def y(a,b,c,d):
	return a+b+c+d

def ywd(a=0, b=1, c=2, d=3):
	return a+b+c+d

AreEqual(y, y)
AreEqual(ywd(), 6)
AssertError(TypeError, y)
AreEqual(ywd(4), 10)
AssertError(TypeError, y, 4)
AreEqual(ywd(4,5), 14)
AssertError(TypeError, y, 4, 5)
AreEqual(ywd(4,5,6), 18)
AssertError(TypeError, y, 4,5,6)
AreEqual( (y == "not-a-Function4"), False)

def foo(): "hello world"
AreEqual(foo.__doc__, 'hello world')

############# coverage ############# 

# function5
def f1(a=1, b=2, c=3, d=4, e=5):    return a * b * c * d * e
def f2(a, b=2, c=3, d=4, e=5):    return a * b * c * d * e
def f3(a, b, c=3, d=4, e=5):    return a * b * c * d * e
def f4(a, b, c, d=4, e=5):    return a * b * c * d * e
def f5(a, b, c, d, e=5):    return a * b * c * d * e
def f6(a, b, c, d, e):    return a * b * c * d * e

for f in (f1, f2, f3, f4, f5, f6):
    AssertError(TypeError, f, 1, 1, 1, 1, 1, 1)             # 6 args
    AreEqual(f(10,11,12,13,14), 10 * 11 * 12 * 13 * 14)     # 5 args

for f in (f1, f2, f3, f4, f5):
    AreEqual(f(10,11,12,13), 10 * 11 * 12 * 13 * 5)         # 4 args
for f in (f6,):    
    AssertError(TypeError, f, 1, 1, 1, 1)

for f in (f1, f2, f3, f4):
    AreEqual(f(10,11,12), 10 * 11 * 12 * 4 * 5)             # 3 args
for f in (f5, f6):    
    AssertError(TypeError, f, 1, 1, 1)

for f in (f1, f2, f3):
    AreEqual(f(10,11), 10 * 11 * 3 * 4 * 5)                 # 2 args
for f in (f4, f5, f6):    
    AssertError(TypeError, f, 1, 1)

for f in (f1, f2):
    AreEqual(f(10), 10 * 2 * 3 * 4 * 5)                     # 1 args
for f in (f3, f4, f5, f6):    
    AssertError(TypeError, f, 1)

for f in (f1,):
    AreEqual(f(), 1 * 2 * 3 * 4 * 5)                        # no args
for f in (f2, f3, f4, f5, f6):    
    AssertError(TypeError, f)


# method
class C1:
    def f0(self): return 0
    def f1(self, a): return 1
    def f2(self, a, b): return 2
    def f3(self, a, b, c): return 3
    def f4(self, a, b, c, d): return 4
    def f5(self, a, b, c, d, e): return 5
    def f6(self, a, b, c, d, e, f): return 6
    def f7(self, a, b, c, d, e, f, g): return 7

class C2: pass

c1, c2 = C1(), C2()

line = ""
for i in range(8):
    args = ",".join(['1'] * i)
    line += "AreEqual(c1.f%d(%s), %d)\n" % (i, args, i)
    line +=  "AreEqual(C1.f%d(c1,%s), %d)\n" % (i, args, i)
    #line +=  "try: C1.f%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n" % (i, args)
    #line +=  "try: C1.f%d(c2, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n" % (i, args)

#print line
exec line    

def SetAttrOfInstanceMethod():
    C1.f0.attr = 1
AssertError(AttributeError, SetAttrOfInstanceMethod)

C1.f0.im_func.attr = 1
AreEqual(C1.f0.attr, 1)
AreEqual(dir(C1.f0).__contains__("attr"), True)

AreEqual(C1.f0.__module__, __name__)

######################################################################################

from iptest.assert_util import *

def f(x=0, y=10, z=20, *args, **kws):
    return (x, y, z), args, kws

Assert(f(10, l=20) == ((10, 10, 20), (), {'l': 20}))

Assert(f(1, *(2,), **{'z':20}) == ((1, 2, 20), (), {}))

Assert(f(*[1,2,3]) == ((1, 2, 3), (), {}))

def a(*args, **kws): return args, kws

def b(*args, **kws):
    return a(*args, **kws)

Assert(b(1,2,3, x=10, y=20) == ((1, 2, 3), {'y': 20, 'x': 10}))

def b(*args, **kws):
    return a(**kws)

Assert(b(1,2,3, x=10, y=20) == ((), {'y': 20, 'x': 10}))

try:
    b(**[])
    Assert(False)
except TypeError:
    pass

def f(x, *args):
    return (x, args)

AreEqual(f(1, *[2]), (1, (2,)))
AreEqual(f(7, *(i for i in range(3))), (7, (0, 1, 2,)))
AreEqual(f(9, *range(11, 13)), (9, (11, 12)))

#verify we can call sorted w/ keyword args

import operator
inventory = [('apple', 3), ('banana', 2), ('pear', 5), ('orange', 1)]
getcount = operator.itemgetter(1)

sorted_inventory = sorted(inventory, key=getcount)


# verify proper handling of keyword args for python functions
def kwfunc(a,b,c):  pass

try:
    kwfunc(10, 20, b=30)
    Assert(False)
except TypeError:
    pass

try:
    kwfunc(10, None, b=30)
    Assert(False)
except TypeError:
    pass


try:
    kwfunc(10, None, 40, b=30)
    Assert(False)
except TypeError:
    pass

if is_cli or is_silverlight:
    import System    
    
    # Test Hashtable and Dictionary on desktop, and just Dictionary in Silverlight
    # (Hashtable is not available)
    htlist = [System.Collections.Generic.Dictionary[System.Object, System.Object]()]
    if not is_silverlight:
        htlist += [System.Collections.Hashtable()]

    for ht in htlist:
        def foo(**kwargs):
            return kwargs['key']
            
        ht['key'] = 'xyz'
        
        AreEqual(foo(**ht), 'xyz')

def foo(a,b):
    return a-b
    
AreEqual(foo(b=1, *(2,)), 1)

# kw-args passed to init through method instance

class foo:
    def __init__(self, group=None, target=None):
            AreEqual(group, None)
            AreEqual(target,'baz')

a = foo(target='baz')

foo.__init__(a, target='baz')


# call a params method w/ no params

if is_cli or is_silverlight:
    import clr
    AreEqual('abc\ndef'.Split()[0], 'abc') 
    AreEqual('abc\ndef'.Split()[1], 'def')
    x = 'a bc   def'.Split()
    AreEqual(x[0], 'a')
    AreEqual(x[1], 'bc')
    AreEqual(x[2], '')
    AreEqual(x[3], '')
    AreEqual(x[4], 'def')
    
    # calling Double.ToString(...) should work - Double is
    # an OpsExtensibleType and doesn't define __str__ on this
    # overload
    
    AreEqual('1.00', System.Double.ToString(1.0, 'f'))

######################################################################################
# Incorrect number of arguments

def f(a): pass

AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", f)
AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (3 given)", f, 1, 2, 3)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
#AssertError calls f(*args), which generates a different AST than f(1,2,3)
AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f())
AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (3 given)", lambda:f(1, 2, 3))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=2))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=2))

def f(a,b,c,d,e,f,g,h,i,j): pass

AssertErrorWithMessage(TypeError, "f() takes exactly 10 arguments (0 given)", f)
AssertErrorWithMessage(TypeError, "f() takes exactly 10 arguments (3 given)", f, 1, 2, 3)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
AssertErrorWithMessage(TypeError, "f() takes exactly 10 arguments (0 given)", lambda:f())
AssertErrorWithMessage(TypeError, "f() takes exactly 10 arguments (3 given)", lambda:f(1, 2, 3))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=2))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=2))

def f(a, b=2): pass

AssertErrorWithMessage(TypeError, "f() takes at least 1 argument (0 given)", f)
AssertErrorWithMessage(TypeError, "f() takes at most 2 arguments (3 given)", f, 1, 2, 3)
if is_cpython: #CPython bug 9326
    AssertErrorWithMessage(TypeError, "f() takes at least 1 argument (1 given)", f, b=2)
else:
    AssertErrorWithMessage(TypeError, "f() takes at least 1 non-keyword argument (0 given)", f, b=2)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=3)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, b=2, dummy=3)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, 1, dummy=3)
AssertErrorWithMessage(TypeError, "f() takes at least 1 argument (0 given)", lambda:f())
AssertErrorWithMessage(TypeError, "f() takes at most 2 arguments (3 given)", lambda:f(1, 2, 3))
if is_cpython: #CPython bug 9326
    AssertErrorWithMessage(TypeError, "f() takes at least 1 argument (1 given)", lambda:f(b=2))
else:
    AssertErrorWithMessage(TypeError, "f() takes at least 1 non-keyword argument (0 given)", lambda:f(b=2))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=3))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(b=2, dummy=3))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=3))

def f(a, *argList): pass

AssertErrorWithMessage(TypeError, "f() takes at least 1 argument (0 given)", f)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, 1, dummy=2)
AssertErrorWithMessage(TypeError, "f() takes at least 1 argument (0 given)", lambda:f())
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=2))
AssertErrorWithMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=2))

def f(a, **keywordDict): pass

AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", f)
AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (3 given)", f, 1, 2, 3)
if is_cpython: #CPython bug 9326
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", f, dummy=2)
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", f, dummy=2, dummy2=3)
else:
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", f, dummy=2)
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", f, dummy=2, dummy2=3)
AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f())
AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (3 given)", lambda:f(1, 2, 3))
if is_cpython: #CPython bug 9326
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f(dummy=2))
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f(dummy=2, dummy2=3))
else:
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", lambda:f(dummy=2))
    AssertErrorWithMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", lambda:f(dummy=2, dummy2=3))

AssertErrorWithMessages(TypeError, "abs() takes exactly 1 argument (0 given)",
                                   "abs() takes exactly one argument (0 given)",   abs)
AssertErrorWithMessages(TypeError, "abs() takes exactly 1 argument (3 given)",
                                   "abs() takes exactly one argument (3 given)",   abs, 1, 2, 3)
AssertErrorWithMessages(TypeError, "abs() got an unexpected keyword argument 'dummy'",
                                   "abs() takes no keyword arguments",             abs, dummy=2)
AssertErrorWithMessages(TypeError, "abs() takes exactly 1 argument (2 given)",
                                   "abs() takes no keyword arguments",             abs, 1, dummy=2)
AssertErrorWithMessages(TypeError, "abs() takes exactly 1 argument (0 given)",
                                   "abs() takes exactly one argument (0 given)",   lambda:abs())
AssertErrorWithMessages(TypeError, "abs() takes exactly 1 argument (3 given)",
                                   "abs() takes exactly one argument (3 given)",   lambda:abs(1, 2, 3))
AssertErrorWithMessages(TypeError, "abs() got an unexpected keyword argument 'dummy'",
                                   "abs() takes no keyword arguments",             lambda:abs(dummy=2))
AssertErrorWithMessages(TypeError, "abs() takes exactly 1 argument (2 given)",
                                   "abs() takes no keyword arguments",             lambda:abs(1, dummy=2))

# list([m]) has one default argument (built-in type)
#AssertErrorWithMessage(TypeError, "list() takes at most 1 argument (2 given)", list, 1, 2)
#AssertErrorWithMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, list, [], dict({"dummy":2}))

#======== BUG 697 ===========
#AssertErrorWithMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, list, [1], dict({"dummy":2}))

# complex([x,y]) has two default argument (OpsReflectedType type)
#AssertErrorWithMessage(TypeError, "complex() takes at most 2 arguments (3 given)", complex, 1, 2, 3)
#AssertErrorWithMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, complex, [], dict({"dummy":2}))
#AssertErrorWithMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, complex, [1], dict({"dummy":2}))

# bool([x]) has one default argument (OpsReflectedType and valuetype type)
#AssertErrorWithMessage(TypeError, "bool() takes at most 1 argument (2 given)", bool, 1, 2)
#AssertErrorWithMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, bool, [], dict({"dummy":2}))
#AssertErrorWithMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, bool, [1], dict({"dummy":2}))

class UserClass(object): pass
AssertErrorWithMessage(TypeError, "object.__new__() takes no parameters", UserClass, 1)
AssertErrorWithMessage(TypeError, "object.__new__() takes no parameters", apply, UserClass, [], dict({"dummy":2}))
    
class OldStyleClass: pass
AssertErrorWithMessage(TypeError, "this constructor takes no arguments", OldStyleClass, 1)
AssertErrorWithMessage(TypeError, "this constructor takes no arguments", apply, OldStyleClass, [], dict({"dummy":2}))

###############################################################################################
# accepts / returns runtype type checking tests

if is_cli or is_silverlight:
    import clr
    
    @clr.accepts(object)
    def foo(x): 
        return x

    AreEqual(foo('abc'), 'abc')
    AreEqual(foo(2), 2)
    AreEqual(foo(2L), 2L)
    AreEqual(foo(2.0), 2.0)
    AreEqual(foo(True), True)


    @clr.accepts(str)
    def foo(x):
        return x
        
    AreEqual(foo('abc'), 'abc')
    AssertError(AssertionError, foo, 2)
    AssertError(AssertionError, foo, 2L)
    AssertError(AssertionError, foo, 2.0)
    AssertError(AssertionError, foo, True)

    @clr.accepts(str, bool)
    def foo(x, y):
        return x, y

    AreEqual(foo('abc', True), ('abc', True))
    AssertError(AssertionError, foo, ('abc',2))
    AssertError(AssertionError, foo, ('abc',2L))
    AssertError(AssertionError, foo, ('abc',2.0))


    class bar:  
        @clr.accepts(clr.Self(), str)
        def foo(self, x):
            return x


    a = bar()
    AreEqual(a.foo('xyz'), 'xyz')
    AssertError(AssertionError, a.foo, 2)
    AssertError(AssertionError, a.foo, 2L)
    AssertError(AssertionError, a.foo, 2.0)
    AssertError(AssertionError, a.foo, True)

    @clr.returns(str)
    def foo(x):
        return x


    AreEqual(foo('abc'), 'abc')
    AssertError(AssertionError, foo, 2)
    AssertError(AssertionError, foo, 2L)
    AssertError(AssertionError, foo, 2.0)
    AssertError(AssertionError, foo, True)

    @clr.accepts(bool)
    @clr.returns(str)
    def foo(x):
        if x: return str(x)
        else: return 0
        
    AreEqual(foo(True), 'True')

    AssertError(AssertionError, foo, 2)
    AssertError(AssertionError, foo, 2)
    AssertError(AssertionError, foo, False)

    @clr.returns(None)
    def foo(): pass

    AreEqual(foo(), None)

try:
    buffer()
except TypeError, e:
    # make sure we get the right type name when calling w/ wrong # of args
    AreEqual(str(e)[:8], 'buffer()')
    
#try:
#    list(1,2,3)
#except TypeError, e:
    # make sure we get the right type name when calling w/ wrong # of args
#    AreEqual(str(e)[:6], 'list()')    

# oldinstance
class foo:
    def bar(self): pass
    def bar1(self, xyz): pass
    
class foo2: pass
class foo3(object): pass

AssertError(TypeError, foo.bar)
AssertError(TypeError, foo.bar1, None, None)
AssertError(TypeError, foo.bar1, None, 'abc')
AssertError(TypeError, foo.bar1, 'xyz', 'abc')
AssertError(TypeError, foo.bar, foo2())
AssertError(TypeError, foo.bar, foo3())

# usertype
class foo(object):
    def bar(self): pass
    def bar1(self, xyz): pass

AssertError(TypeError, foo.bar)
AssertError(TypeError, foo.bar1, None, None)
AssertError(TypeError, foo.bar1, None, 'abc')
AssertError(TypeError, foo.bar1, 'xyz', 'abc')
AssertError(TypeError, foo.bar, foo2())
AssertError(TypeError, foo.bar, foo3())

# access a method w/ caller context w/ an args parameter.
def foo(*args):
    return hasattr(*args)
    
AreEqual(foo('', 'index'), True)

# dispatch to a ReflectOptimized method

if is_cli and not is_silverlight:
    from iptest.console_util import IronPythonInstance
    from System import Environment
    from sys import executable
    
    wkdir = testpath.public_testdir
    
    if "-X:LightweightScopes" in Environment.GetCommandLineArgs():
        ipi = IronPythonInstance(executable, wkdir, "-X:LightweightScopes", "-X:BasicConsole")
    else:
        ipi = IronPythonInstance(executable, wkdir, "-X:BasicConsole")

    if (ipi.Start()):
        result = ipi.ExecuteLine("from iptest.assert_util import *")
        result = ipi.ExecuteLine("load_iron_python_test()")
        result = ipi.ExecuteLine("from IronPythonTest import DefaultParams")
        response = ipi.ExecuteLine("DefaultParams.FuncWithDefaults(1100, z=82)")
        AreEqual(response, '1184')
        ipi.End()

p = ((1, 2),)

AreEqual(zip(*(p * 10)), [(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), (2, 2, 2, 2, 2, 2, 2, 2, 2, 2)])
AreEqual(zip(*(p * 10)), [(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), (2, 2, 2, 2, 2, 2, 2, 2, 2, 2)])




class A(object): pass

class B(A): pass

#unbound super
for x in [super(B), super(B,None)]:
    AreEqual(x.__thisclass__, B)
    AreEqual(x.__self__, None)
    AreEqual(x.__self_class__, None)

# super w/ both types
x = super(B,B)

AreEqual(x.__thisclass__,B)
AreEqual(x.__self_class__, B)
AreEqual(x.__self__, B)

# super w/ type and instance
b = B()
x = super(B, b)

AreEqual(x.__thisclass__,B)
AreEqual(x.__self_class__, B)
AreEqual(x.__self__, b)

# super w/ mixed types
x = super(A,B)
AreEqual(x.__thisclass__,A)
AreEqual(x.__self_class__, B)
AreEqual(x.__self__, B)

# invalid super cases
try:
    x = super(B, 'abc')
    AssertUnreachable()
except TypeError:
    pass
    
try:
    super(B,A)
    AssertUnreachable()
except TypeError:
    pass
    

class A(object):
    def __init__(self, name):
        self.__name__ = name
    def meth(self):
        return self.__name__
    classmeth = classmethod(meth)
    
class B(A): pass

b = B('derived')
AreEqual(super(B,b).__thisclass__.__name__, 'B')
AreEqual(super(B,b).__self__.__name__, 'derived')
AreEqual(super(B,b).__self_class__.__name__, 'B')

AreEqual(super(B,b).classmeth(), 'B')


# descriptor supper
class A(object):
    def meth(self): return 'A'
    
class B(A):
    def meth(self):    
        return 'B' + self.__super.meth()
        
B._B__super = super(B)
b = B()
AreEqual(b.meth(), 'BA')


#################################
# class method calls - class method should get
# correct meta class.

class D(object):
	@classmethod
	def classmeth(cls): pass
	
AreEqual(D.classmeth.im_class, type)

class MetaType(type): pass

class D(object):
	__metaclass__ = MetaType
	@classmethod
	def classmeth(cls): pass
	
AreEqual(D.classmeth.im_class, MetaType)

#####################################################################################

from iptest.assert_util import *
if is_cli or is_silverlight:
    from _collections import *
else:
    from collections import *

global init

def Assert(val):
    if val == False:
        raise TypeError, "assertion failed"

def runTest(testCase):
    global typeMatch
    global init

    class foo(testCase.subtype):
        def __new__(cls, param):
            ret = testCase.subtype.__new__(cls, param)
            Assert(ret == testCase.newEq)
            Assert((ret != testCase.newEq) != True)
            return ret
        def __init__(self, param):
            testCase.subtype.__init__(self, param)
            Assert(self == testCase.initEq)
            Assert((self != testCase.initEq) != True)

    a = foo(testCase.param)
    Assert((type(a) == foo) == testCase.match)

class TestCase(object):
    __slots__ = ['subtype', 'newEq', 'initEq', 'match', 'param']
    def __init__(self, subtype, newEq, initEq, match, param):
        self.match = match
        self.subtype = subtype
        self.newEq = newEq
        self.initEq = initEq
        self.param = param


cases = [TestCase(int, 2, 2, True, 2),
         TestCase(list, [], [2,3,4], True, (2,3,4)),
         TestCase(deque, deque(), deque((2,3,4)), True, (2,3,4)),
         TestCase(set, set(), set((2,3,4)), True, (2,3,4)),
         TestCase(frozenset, frozenset((2,3,4)), frozenset((2,3,4)), True, (2,3,4)),
         TestCase(tuple, (2,3,4), (2,3,4), True, (2,3,4)),
         TestCase(str, 'abc', 'abc', True, 'abc'),
         TestCase(float, 2.3, 2.3, True, 2.3),
         TestCase(type, type(object), type(object), False, object),
         TestCase(long, 10000000000L, 10000000000L, True, 10000000000L),
         #TestCase(complex, complex(2.0, 0), complex(2.0, 0), True, 2.0),        # complex is currently a struct w/ no extensibel, we fail here
        # TestCase(file, 'abc', True),      # ???
        ]


for case in cases:
    runTest(case)

# verify we can call the base init directly

if is_cli and not is_silverlight:
    import clr
    clr.AddReferenceByPartialName('System.Windows.Forms')
    from System.Windows.Forms import *

    class MyForm(Form):
        def __init__(self, title):
            Form.__init__(self)
            self.Text = title

    a = MyForm('abc')
    AreEqual(a.Text, 'abc')

#TestCase(bool, True, True),                    # not an acceptable base type

def test_func_flags():
    def foo0(): pass
    def foo1(*args): pass
    def foo2(**args): pass
    def foo3(*args, **kwargs): pass
    def foo4(a): pass
    def foo5(a, *args): pass
    def foo6(a, **args): pass
    def foo7(a, *args, **kwargs): pass
    def foo8(a,b,c,d,e,f): pass
    def foo9(a,b): pass
    
    AreEqual(foo0.func_code.co_flags & 12, 0)
    AreEqual(foo1.func_code.co_flags & 12, 4)
    AreEqual(foo2.func_code.co_flags & 12, 8)
    AreEqual(foo3.func_code.co_flags & 12, 12)
    AreEqual(foo4.func_code.co_flags & 12, 0)
    AreEqual(foo5.func_code.co_flags & 12, 4)
    AreEqual(foo6.func_code.co_flags & 12, 8)
    AreEqual(foo7.func_code.co_flags & 12, 12)
    AreEqual(foo8.func_code.co_flags & 12, 0)
    AreEqual(foo9.func_code.co_flags & 12, 0)
    
    AreEqual(foo0.func_code.co_argcount, 0)
    AreEqual(foo1.func_code.co_argcount, 0)
    AreEqual(foo2.func_code.co_argcount, 0)
    AreEqual(foo3.func_code.co_argcount, 0)
    AreEqual(foo4.func_code.co_argcount, 1)
    AreEqual(foo5.func_code.co_argcount, 1)
    AreEqual(foo6.func_code.co_argcount, 1)
    AreEqual(foo7.func_code.co_argcount, 1)
    AreEqual(foo8.func_code.co_argcount, 6)
    AreEqual(foo9.func_code.co_argcount, 2)

    
def test_big_calls():
    # check various function call sizes and boundaries
    for size in [3,4,5, 7,8,9, 15,16,17, 23, 24, 25, 31,32,33, 47,48,49, 63,64,65, 127, 128, 129, 254, 255, 256, 257, 258, 511,512,513, 1023,1024,1025, 2047, 2048, 2049]:
        # w/o defaults
        exec 'def f(' + ','.join(['a' + str(i) for i in range(size)]) + '): return ' + ','.join(['a' + str(i) for i in range(size)])
        # w/ defaults
        exec 'def g(' + ','.join(['a' + str(i) + '=' + str(i) for i in range(size)]) + '): return ' + ','.join(['a' + str(i) for i in range(size)])
        if size <= 255 or is_cli:
            # CPython allows function definitions > 255, but not calls w/ > 255 params.
            exec 'a = f(' + ', '.join([str(x) for x in xrange(size)]) + ')'
            AreEqual(a, tuple(xrange(size)))
            exec 'a = g()'
            AreEqual(a, tuple(xrange(size)))
            exec 'a = g(' + ', '.join([str(x) for x in xrange(size)]) + ')'
            AreEqual(a, tuple(xrange(size)))
        
        exec 'a = f(*(' + ', '.join([str(x) for x in xrange(size)]) + '))'
        AreEqual(a, tuple(xrange(size)))
    
def test_compile():
    x = compile("print 2/3", "<string>", "exec", 8192)
    Assert((x.co_flags & 8192) == 8192)
    
    x = compile("2/3", "<string>", "eval", 8192)
    AreEqual(eval(x), 2.0 / 3.0)

    names = [   "", ".", "1", "\n", " ", "@", "%^",
                "a", "A", "Abc", "aBC", "filename.py",
                "longlonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglong",
                """
                stuff
                more stuff
                last stuff
                """
                ]
    for name in names:
        AreEqual(compile("print 2/3", name, "exec", 8192).co_filename,
                 name)

def test_filename():
    c = compile("x = 2", "test", "exec")
    AreEqual(c.co_filename, 'test')
    
def test_name():
    def f(): pass
    
    f.__name__ = 'g'
    AreEqual(f.__name__, 'g')
    Assert(repr(f).startswith('<function g'))
    
    f.func_name = 'x'
    AreEqual(f.__name__, 'x')
    Assert(repr(f).startswith('<function x'))

def test_argcount():
    def foo0(): pass
    def foo1(*args): pass
    def foo2(**args): pass
    def foo3(*args, **kwargs): pass
    def foo4(a): pass
    def foo5(a, *args): pass
    def foo6(a, **args): pass
    def foo7(a, *args, **kwargs): pass
    def foo8(a,b,c,d,e,f): pass
    def foo9(a,b): pass
    
    AreEqual(foo0.func_code.co_argcount, 0)
    AreEqual(foo1.func_code.co_argcount, 0)
    AreEqual(foo2.func_code.co_argcount, 0)
    AreEqual(foo3.func_code.co_argcount, 0)
    AreEqual(foo4.func_code.co_argcount, 1)
    AreEqual(foo5.func_code.co_argcount, 1)
    AreEqual(foo6.func_code.co_argcount, 1)
    AreEqual(foo7.func_code.co_argcount, 1)
    AreEqual(foo8.func_code.co_argcount, 6)
    AreEqual(foo9.func_code.co_argcount, 2)

def test_defaults():
    defaults = [None, object, int, [], 3.14, [3.14], (None,), "a string"]
    for default in defaults:
        def helperFunc(): pass
        AreEqual(helperFunc.func_defaults, None)
        AreEqual(helperFunc.func_defaults, None)
    
        def helperFunc1(a): pass
        AreEqual(helperFunc1.func_defaults, None)
        AreEqual(helperFunc1.func_defaults, None)
        
        
        def helperFunc2(a=default): pass
        AreEqual(helperFunc2.func_defaults, (default,))
        helperFunc2(a=7)
        AreEqual(helperFunc2.func_defaults, (default,))
        
        
        def helperFunc3(a, b=default, c=[42]): c.append(b)
        AreEqual(helperFunc3.func_defaults, (default, [42]))
        helperFunc3("stuff")
        AreEqual(helperFunc3.func_defaults, (default, [42, default]))

def test_splat_defaults():
    def g(a, b, x=None):
        return a, b, x
        
    def f(x, *args):        
        return g(x, *args)
        
    AreEqual(f(1, *(2,)), (1,2,None))

def test_argument_eval_order():
    """Check order of evaluation of function arguments"""
    x = [1]
    def noop(a, b, c):
        pass
    noop(x.append(2), x.append(3), x.append(4))
    AreEqual(x, [1,2,3,4])

def test_method_attr_access():
    class foo(object):
        def f(self): pass
        abc = 3
    
    method = type(foo.f)    
    
    AreEqual(method(foo, 'abc').abc, 3)

@skip("interpreted")  # we don't have FuncEnv's in interpret modes so this always returns None
def test_function_closure_negative():
    def f(): pass
    
    for assignment_val in [None, 1, "a string"]:
        try:
            f.func_closure = assignment_val
            AssertUnreachable("func_closure is a read-only attribute of functions")
        except TypeError, e:
            pass

def test_paramless_function_call_error():
    def f(): pass
    
    try:
        f(*(1, ))
        AssertUnreachable()
    except TypeError: pass
        
    try:
        f(**{'abc':'def'})
        AssertUnreachable()
    except TypeError: pass


def test_function_closure():
    def f(): pass
    
    AreEqual(f.func_closure, None)
    
    def f():    
        def g(): pass
        return g
        
    AreEqual(f().func_closure, None)

    def f():
        x = 4
        def g(): return x
        return g
        
    AreEqual(sorted([x.cell_contents for x in f().func_closure]), [4])
    
    def f():
        x = 4
        def g():
            y = 5
            def h(): return x,y
            return h
        return g()

    AreEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5])

    # don't use z
    def f():
        x = 4
        def g():
            y = 5
            z = 7
            def h(): return x,y
            return h
        return g()
    
    AreEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5])
    
    def f():
        x = 4
        def g():
            y = 5
            z = 7
            def h(): return x,y,z
            return h
        return g()

    AreEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5, 7])

    def f():
        x = 4
        a = 9
        def g():
            y = 5
            z = 7
            def h(): return x,y
            return h
        return g()

    AreEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5])
    
    # closure cells are not recreated
    callRes = f()
    a = sorted([id(x) for x in callRes.func_closure])
    b = sorted([id(x) for x in callRes.func_closure])
    AreEqual(a, b)

    def f():
        x = 4
        a = 9
        def g():
            y = 5
            z = 7
            def h(): return x,y,a,z
            return h
        return g()

    AreEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5, 7, 9])

    AssertError(TypeError, hash, f().func_closure[0])
    
    def f():
        x = 5
        def g():
            return x
        return g
        
    def h():
        x = 5
        def g():
            return x
        return g

    def j():
        x = 6
        def g():
            return x
        return g

    AreEqual(f().func_closure[0], h().func_closure[0])
    Assert(f().func_closure[0] != j().func_closure[0])

    # <cell at 45: int object at 44>
    Assert(repr(f().func_closure[0]).startswith('<cell at '))
    Assert(repr(f().func_closure[0]).find(': int object at ') != -1)
    
    
def test_func_code():
    def foo(): pass
    def assign(): foo.func_code = None
    AssertError(TypeError, assign)
    
def def_func_doc():
    foo.func_doc = 'abc'
    AreEqual(foo.__doc__, 'abc')
    foo.__doc__ = 'def'
    AreEqual(foo.func_doc, 'def')
    foo.func_doc = None
    AreEqual(foo.__doc__, None)
    AreEqual(foo.func_doc, None)

def test_func_defaults():
    def f(a, b): return (a, b)

    f.func_defaults = (1,2)
    AreEqual(f(), (1,2))
    
    f.func_defaults = (1,2,3,4)
    AreEqual(f(), (3,4))

    f.func_defaults = None
    AssertError(TypeError, f)

    f.func_defaults = (1,2)
    AreEqual(f.func_defaults, (1,2))
    
    del f.func_defaults
    AreEqual(f.func_defaults, None)
    del f.func_defaults
    AreEqual(f.func_defaults, None)
    
    def func_with_many_args(one, two, three, four, five, six, seven, eight, nine, ten, eleven=None, twelve=None, thirteen=None, fourteen=None, fifteen=None, sixteen=None, seventeen=None, eighteen=None, nineteen=None):
        print 'hello'
        
    func_with_many_args(None, None, None, None, None, None, None, None, None, None)


def test_func_dict():
    def f(): pass
    
    f.abc = 123
    AreEqual(f.func_dict, {'abc': 123})
    f.func_dict = {'def': 'def'}
    AreEqual(hasattr(f, 'def'), True)
    AreEqual(getattr(f, 'def'), 'def')
    f.func_dict = {}
    AreEqual(hasattr(f, 'abc'), False)
    AreEqual(hasattr(f, 'def'), False)
        
    AssertError(TypeError, lambda : delattr(f, 'func_dict'))
    AssertError(TypeError, lambda : delattr(f, '__dict__'))
    
def test_method():
    class C:
        def method(self): pass
    
    method = type(C.method)(id, None, 'abc')
    AreEqual(method.im_class, 'abc')
    
    class myobj:
        def __init__(self, val):            
            self.val = val
            self.called = []
        def __hash__(self):
            self.called.append('hash')
            return hash(self.val)        
        def __eq__(self, other): 
            self.called.append('eq')
            return self.val == other.val
        def __call__(*args): pass
    
    func1, func2 = myobj(2), myobj(2)
    inst1, inst2 = myobj(3), myobj(3)
    
    method = type(C().method)
    m1 = method(func1, inst1)
    m2 = method(func2, inst2)
    AreEqual(m1, m2)
    
    Assert('eq' in func1.called)
    Assert('eq' in inst1.called)

    hash(m1)   
    Assert('hash' in func1.called)
    Assert('hash' in inst1.called)
    
def test_function_type():
    def f1(): pass
    def f2(a): pass
    def f3(a, b, c): pass
    def f4(*a, **b): pass
    
    def decorator(f): return f
    @decorator
    def f5(a): pass
    
    for x in [ f2, f3, f4, f5]:
        AreEqual(type(f1), type(x))

def test_name_mangled_params():
    def f1(__a): pass
    def f2(__a): return __a
    def f3(a, __a): return __a
    def f4(_a, __a): return _a + __a
    
    f1("12")
    AreEqual(f2("hello"), "hello")
    AreEqual(f3("a","b"), "b")
    AreEqual(f4("a","b"), "ab")

def test_splat_none():
    def f(*args): pass
    def g(**kwargs): pass
    def h(*args, **kwargs): pass
    
    #CodePlex 20250
    AssertErrorWithMessage(TypeError, "f() argument after * must be a sequence, not NoneType", 
                           lambda : f(*None))
    AssertErrorWithMessage(TypeError, "g() argument after ** must be a mapping, not NoneType", 
                           lambda : g(**None))
    AssertErrorWithMessage(TypeError, "h() argument after ** must be a mapping, not NoneType",
                           lambda : h(*None, **None))

def test_exec_funccode():
    # can't exec a func code w/ parameters
    def f(a, b, c): print a, b, c
    
    AssertError(TypeError, lambda : eval(f.func_code))
    
    # can exec *args/**args
    def f(*args): pass
    exec f.func_code in {}, {}
    
    def f(*args, **kwargs): pass
    exec f.func_code in {}, {}

    # can't exec function which closes over vars
    def f():
        x = 2
        def g():
                print x
        return g.func_code
    
    AssertError(TypeError, lambda : eval(f()))
    
def test_exec_funccode_filename():
    import sys
    mod = type(sys)('fake_mod_name')
    mod.__file__ = 'some file'
    exec "def x(): pass" in mod.__dict__
    AreEqual(mod.x.func_code.co_filename, '<string>')


# defined globally because unqualified exec isn't allowed in
# a nested function.
def unqualified_exec():
    print x
    exec ""

def test_func_code_variables():
    def CompareCodeVars(code, varnames, names, freevars, cellvars):
        AreEqual(code.co_varnames, varnames)
        AreEqual(code.co_names, names)
        AreEqual(code.co_freevars, freevars)
        AreEqual(code.co_cellvars, cellvars)
    
    # simple local
    def f():
        a = 2
    
    CompareCodeVars(f.func_code, ('a', ), (), (), ())    
    
    # closed over var
    def f():
        a = 2
        def g():
            print a
        return g
        
    CompareCodeVars(f.func_code, ('g', ), (), (), ('a', ))
    CompareCodeVars(f().func_code, (), (), ('a', ), ())

    # tuple parameters
    def f((a, b)): pass
    
    CompareCodeVars(f.func_code, ('.0', 'a', 'b'), (), (), ())
    
    def f((a, b), (c, d)): pass
    
    CompareCodeVars(f.func_code, ('.0', '.1', 'a', 'b', 'c', 'd'), (), (), ())
    
    # explicitly marked global
    def f():
        global a
        a = 2
        
    CompareCodeVars(f.func_code, (), ('a', ), (), ())
    
    # implicit global
    def f():
        print some_global
        
    CompareCodeVars(f.func_code, (), ('some_global', ), (), ())

    # global that's been "closed over"
    def f():
        global a
        a = 2
        def g():
            print a
        return g
        
    CompareCodeVars(f.func_code, ('g', ), ('a', ), (), ())
    CompareCodeVars(f().func_code, (), ('a', ), (), ())

    # multi-depth closure
    def f():
        a = 2
        def g():
            x = a
            def h():
                y = a
            return h
        return g
                
    CompareCodeVars(f.func_code, ('g', ), (), (), ('a', ))
    CompareCodeVars(f().func_code, ('x', 'h'), (), ('a', ), ())
    CompareCodeVars(f()().func_code, ('y', ), (), ('a', ), ())

    # multi-depth closure 2
    def f():
        a = 2
        def g():
            def h():
                y = a
            return h
        return g
                
    CompareCodeVars(f.func_code, ('g', ), (), (), ('a', ))
    CompareCodeVars(f().func_code, ('h', ), (), ('a', ), ())
    CompareCodeVars(f()().func_code, ('y', ), (), ('a', ), ())
    
    # closed over parameter
    def f(a):
        def g():
            return a
        return g
    
    CompareCodeVars(f.func_code, ('a', 'g'), (), (), ('a', ))    
    CompareCodeVars(f(42).func_code, (), (), ('a', ), ())
        
    AreEqual(unqualified_exec.func_code.co_names, ('x', ))

def test_delattr():
    def f(): pass
    f.abc = 42
    del f.abc
    def g(): f.abc
    AssertError(AttributeError, g)
   
run_test(__name__)
