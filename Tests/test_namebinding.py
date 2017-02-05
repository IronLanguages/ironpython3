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
import collections
import imp
if is_cli or is_silverlight:
    from _collections import *
else:
    from collections import *

def nolocals():
    AreEqual(locals(), {})

def singleLocal():
    a = True
    AreEqual(locals(), {'a' : True})

def nolocalsWithArg(a):
    AreEqual(locals(), {'a' : 5})

def singleLocalWithArg(b):
    a = True
    AreEqual(locals(), {'a' : True, 'b': 5})

def delSimple():
    a = 5
    Assert(locals() == {'a' : 5})
    del(a)
    Assert(locals() == {})

def iteratorFunc():
    for i in range(1):
        Assert(locals() == {'i' : 0})
        yield i

def iteratorFuncLocals():
    a = 3
    for i in range(1):
        Assert(locals() == {'a':3, 'i' : 0})
        yield i

def iteratorFuncWithArg(b):
    for i in range(1):
        AreEqual(locals(), {'i' : 0, 'b':5})
        yield i

def iteratorFuncLocalsWithArg(b):
    a = 3
    for i in range(1):
        Assert(locals() == {'a':3, 'i' : 0, 'b':5})
        yield i

def delIter():
    a = 5
    yield 2
    Assert(locals() == {'a': 5})
    del(a)
    yield 3
    Assert(locals() == {})

def execAdd():
    exec('a=2')
    Assert(locals() == {'a': 2})

def execAddExisting():
    b = 5
    exec('a=2')
    Assert(locals() == {'a': 2, 'b':5})

def execAddExistingArgs(c):
    b = 5
    exec('a=2')
    Assert(locals() == {'a': 2, 'b': 5, 'c':7})

def execDel():
    a = 5
    exec('del(a)')
    #AreEqual(locals(), {})

def unassigned():
    Assert(locals() == {})
    a = 5
    AreEqual(locals(), {'a': 5})


def reassignLocals():
    locals = 2
    Assert(locals == 2)

def unassignedIter():
    yield 1
    Assert(locals() == {})
    yield 2
    a = 5
    yield 3
    Assert(locals() == {'a': 5})
    yield 4

def reassignLocalsIter():
    yield 1
    locals = 2
    yield 2
    Assert(locals == 2)

# we used to include _ which got defined during codegen.  Make sure
# we don't crash
def localsAfterExpr():
    exec("pass")
    10
    exec("pass")

def nested_locals():
    # Test locals in nested functions
    x = 5
    def f():
        a = 10
        AreEqual(locals(),{'a':10})
    f()
nested_locals()

def nested_locals2():
    # Test locals in nested functions with locals also used in outer function
    x = 5
    def f():
        a = 10
        AreEqual(locals(),{'a':10})
    AreEqual(sorted(locals().keys()), ['f','x'])
    f()
nested_locals2()

# Test that namebinding uses name lookup when locals() is accessed inside a class
xyz = False
class C:
    locals()["xyz"] = True
    passed = xyz

Assert(C.passed == True)

nolocals()
singleLocal()
for a in iteratorFunc(): pass
for a in iteratorFuncLocals(): pass


nolocalsWithArg(5)
singleLocalWithArg(5)

def modifyingLocal(a):
    AreEqual(a, 10)
    a = 8
    AreEqual(a, 8)
    AreEqual(locals(), { 'a' : 8 })

modifyingLocal(10)


for a in iteratorFuncWithArg(5): pass
for a in iteratorFuncLocalsWithArg(5): pass

execAdd()
execAddExisting()
execAddExistingArgs(7)
execDel()

delSimple()
for a in delIter(): pass

unassigned()
for a in unassignedIter(): pass

reassignLocals()
for a in reassignLocalsIter(): pass

Assert('__builtins__' in locals())
a = 5
Assert('a' in locals())

exec('a = a+1')

Assert(locals()['a'] == 6)

def my_locals():
    Fail("Calling wrong locals")

exec("pass")
locals = my_locals
exec("pass")
import builtins
save_locals = builtins.locals
try:
    builtins.locals = my_locals
    exec("pass")
finally:
    builtins.locals = save_locals

localsAfterExpr()
del locals
# name binding in a function
def f():
    a = 2
    class c:
        exec("a = 42")
        abc = a
    return c

AreEqual(f().abc, 2)

#def f():
#    a = 2
#    class c:
#        locals()['a'] = 42
#        abc = a
#    return c
#
#AreEqual(f().abc, 2)
#
#def f():
#    a = 2
#    class c:
#        exec "a = 43"
#        locals()['a'] = 42 
#        abc = a
#    return c
#
#AreEqual(f().abc, 43)

#def f():
#    def x(): 42
#    class C:
#            print locals()
#            print x
#    return C
#
#AreEqual(f()().x, 42)

#def f():
#    def x(): 42
#    class C:
#            print locals()
#            print x
#    return C
#
#Assert(not hasattr(f()(), 'x'))

######################################################################################

# TODO: weakref support in Silverlight
if not is_silverlight:
    def nop():
        pass
    
    class gc:
        pass
    
    try:
        import System
        from System import GC
    
        gc.collect = GC.Collect
        gc.WaitForPendingFinalizers = GC.WaitForPendingFinalizers
    except ImportError:
        import gc
        gc.collect = gc.collect
        gc.WaitForPendingFinalizers = nop
    
    
    def FullCollect():
        gc.collect()
        gc.WaitForPendingFinalizers()
    
    def Hello():
        global res
        res = 'Hello finalizer'
    
    #########################################
    # class implements finalizer
    
    class Foo:
        def __del__(self):
                global res
                res = 'Foo finalizer'
    
    #########################################
    # class doesn't implement finalizer
    
    class Bar:
        pass
    
    ######################
    # simple case
    
    def SimpleTest():
        global res
    
        res = ''
        f = Foo()
        del(f)
        FullCollect()
    
        Assert(res == 'Foo finalizer')
    
    
    ######################
    # Try to delete a builtin name. This should fail since "del" should not
    # lookup the builtin namespace
    
    def DoDelBuiltin():
        global pow
        del(pow)
    
    def DelBuiltin():
        # Check that "pow" is defined
        global pow
        p = pow
    
        AssertError(NameError, DoDelBuiltin)
        AssertError(NameError, DoDelBuiltin)
    
    ######################
    # Try to delete a builtin name. This should fail since "del" should not
    # lookup the builtin namespace
    
    def DoDelGlobal():
        global glb
        del(glb)
        return True
    
    def DelUndefinedGlobal():
        AssertError(NameError, DoDelGlobal)
        AssertError(NameError, DoDelGlobal)
    
    def DelDefinedGlobal():
        # Check that "glb" is defined
        global glb
        l = glb
    
        Assert(DoDelGlobal() == True)
        AssertError(NameError, DoDelGlobal)
        AssertError(NameError, DoDelGlobal)
    
    ######################
    # Try to delete a name from an enclosing function. This should fail since "del" should not
    # lookup the enclosing namespace
    
    def EnclosingFunction():
        val = 1
        def DelEnclosingName():
            del val
        DelEnclosingName()
    
    ######################
    # per-instance override
    def PerInstOverride():
    
        global res
        res = ''
        def testIt():
            f = Foo()
    
            f.__del__ = Hello
    
            Assert(hasattr(Foo, '__del__'))
    
            Assert(hasattr(f, '__del__'))
    
            del(f)
        
        testIt()
        FullCollect()
    
        Assert(res == 'Hello finalizer')
    
    ##################################
    # per-instance override & remove
    
    def PerInstOverrideAndRemove():
        global res
    
        def testit():
            global res
            global Hello
            res = ''
            f = Foo()
            f.__del__ = Hello
            Assert(hasattr(Foo, '__del__'))
        
            Assert(hasattr(f, '__del__'))
        
            del(f.__del__)
            Assert(hasattr(Foo, '__del__'))
            Assert(hasattr(f, '__del__'))
        
            del(f)
        
        testit()
        FullCollect()
        Assert(res == 'Foo finalizer')
    
    
    ##################################
    # per-instance override & remove both
    
    def PerInstOverrideAndRemoveBoth():
    
        res = ''
        f = Foo()
        Assert(hasattr(Foo, '__del__'))
        Assert(hasattr(f, '__del__'))
    
        f.__del__ = Hello
    
        Assert(hasattr(Foo, '__del__'))
        Assert(hasattr(f, '__del__'))
    
        FullCollect()
        FullCollect()
    
        del(Foo.__del__)
        Assert(hasattr(Foo, '__del__') == False)
        Assert(hasattr(f, '__del__'))
    
        del(f.__del__)
        dir(f)
    
        Assert(hasattr(Foo, '__del__') == False)
        Assert(hasattr(f, '__del__') == False)
    
        FullCollect()
        FullCollect()
    
        del(f)
        FullCollect()
    
        Assert(res == '')
    
    
    ##################################
    # define finalizer after instance creation
    def NoFinAddToInstance():
    
        global res
        res = ''
        
        def inner():
            b = Bar()
            Assert(hasattr(Bar, '__del__') == False)
    
            Assert(hasattr(b, '__del__') == False)
    
            b.__del__ = Hello
            Assert(hasattr(Bar, '__del__') == False)
            Assert(hasattr(b, '__del__'))
    
            del(b)
    
        inner()
        FullCollect()
    
        Assert(res == 'Hello finalizer')
    
    
    ##################################
    # define & remove finalizer after instance creation
    def NoFinAddToInstanceAndRemove():
        global res
        res = ''
        b = Bar()
        Assert(hasattr(Bar, '__del__') == False)
    
        Assert(hasattr(b, '__del__') == False)
    
        b.__del__ = Hello
        Assert(hasattr(Bar, '__del__') == False)
        Assert(hasattr(b, '__del__'))
    
        del(b.__del__)
        Assert(hasattr(Bar, '__del__') == False)
        Assert(hasattr(b, '__del__') == False)
    
        del(b)
        FullCollect()
    
        Assert(res == '')
    
    
    SimpleTest()
    DelBuiltin()
    Assert(UnboundLocalError, EnclosingFunction)
    DelUndefinedGlobal()
    glb = 100
    DelDefinedGlobal()
    PerInstOverride()
    PerInstOverrideAndRemove()
    PerInstOverrideAndRemoveBoth()
    NoFinAddToInstance()
    NoFinAddToInstanceAndRemove()

#####################################################################################

from iptest.assert_util import *

# Some utilities

def get_true():
    return True

def get_false():
    return False
    
def get_n(n):
    return n

def test_undefined(function, *args):
    try:
        function(*args)
    except NameError as n:
        Assert("'undefined'" in str(n), "%s: expected undefined variable exception, but different NameError caught: %s" % (function.__name__, n))
    else:
        Fail("%s: expected undefined variable exception, but no exception caught" % function.__name__)

def test_unassigned(function, *args):
    try:
        function(*args)
    except UnboundLocalError as n:
        pass
    else:
        Fail("%s: expected unassigned variable exception, but no exception caught" % function.__name__)

def test_attribute_error(function, *args):
    try:
        function(*args)
    except AttributeError:
        pass
    else:
        Fail("%s: expected AttributeError, but no exception caught" % function.__name__)

# straightforward undefined local
def test_simple_1():
    x = undefined
    undefined = 1           # create local binding

test_undefined(test_simple_1)

# longer assignment statement
def test_simple_2():
    x = y = z = undefined
    undefined = 1           # create local binding

test_undefined(test_simple_1)

# aug assignment
def test_simple_3():
    undefined += 1          # binds already

test_undefined(test_simple_3)

# assigned to self
def test_simple_4():
    undefined = undefined

test_undefined(test_simple_4)

# explicit deletion
def test_simple_5():
    del undefined

test_undefined(test_simple_5)

# if statement
def test_if_1():
    if get_false():
        undefined = 1       # unreachable
    x = undefined
    
test_undefined(test_if_1)

# if statement
def test_if_2():
    if get_false():
        undefined = 1
    else:
        x = 1
    x = undefined
    
test_undefined(test_if_2)

# if statement
def test_if_3():
    if get_true():
        pass
    else:
        undefined = 1
    x = undefined

test_undefined(test_if_3)

# nested if statements
def test_if_4():
    if get_false():
        if get_true():
            undefined = 1
        else:
            undefined = 1
    x = undefined

test_undefined(test_if_4)

# if elif elif elif
def test_if_5():
    n = get_n(10)
    if n == 1:
        undefined = n
    elif n == 2:
        undefined = n
    elif n == 3:
        undefined = n
    elif n == 4:
        undefined = n
    elif n == 5:
        undefined = n
    else:
        pass
    n = undefined

test_undefined(test_if_5)

# for
def test_for_1():
    for i in range(get_n(0)):
        undefined = i
    x = undefined

test_undefined(test_for_1)

# more for with else that doesn't always bind
def test_for_2():
    for i in range(get_n(0)):
        undefined = i
    else:
        if get_false():
            undefined = 1
        elif get_false():
            undefined = 1
        else:
            pass
    x = undefined

test_undefined(test_for_2)

# for with break
def test_for_3():
    for i in range(get_n(10)):
        break
        undefined = 10
    x = undefined

test_undefined(test_for_3)

# for with break and else
def test_for_4():
    for i in range(get_n(10)):
        break
        undefined = 10
    else:
        undefined = 20
    x = undefined

test_undefined(test_for_4)

# for with break and else
def test_for_5():
    for i in range(get_n(10)):
        if get_true():
            break
        undefined = 10
    else:
        undefined = 20
    x = undefined

test_undefined(test_for_5)

# for with break and else and conditional initialization
def test_for_6():
    for i in range(get_n(10)):
        if get_false():
            undefined = 10
        if get_true():
            break
        undefined = 10
    else:
        undefined = 20
    x = undefined

test_undefined(test_for_6)

# delete somewhere deep
def test_for_7():
    for i in range(get_n(10)):
        undefined = 10
        if get_false():
            del undefined
        if get_true():
            del undefined;
        if get_true():
            break
        undefined = 10
    else:
        undefined = 20
    x = undefined

test_undefined(test_for_7)


# bound by for
def test_for_7():
    for undefined in []:
        pass
    print(undefined)

test_undefined(test_for_7)

# more binding constructs
def test_try_1():
    try:
        1/0
        undefined = 1
    except:
        pass
    x = undefined

test_undefined(test_try_1)

def test_try_2():
    try:
        pass
    except Error as undefined:
        pass
    x = undefined
    
test_undefined(test_try_2)

# bound by import statement
def test_import_1():
    x = undefined
    import undefined

test_undefined(test_import_1)

# same here
def test_import_2():
    x = undefined
    import defined as undefined

test_undefined(test_import_2)

# del
def test_del_1():
    undefined = 1
    del undefined
    x = undefined

test_undefined(test_del_1)

# conditional del
def test_del_2():
    undefined = 10
    if get_true():
        del undefined
    else:
        undefined = 1
    x = undefined

test_undefined(test_del_2)

# del in the loop

def test_del_3():
    undefined = 10
    for i in [1]:
        if i == get_n(1):
            del undefined
    x = undefined
    
test_undefined(test_del_3)

# del in the loop in condition

def test_del_4():
    undefined = 10
    for i in [1,2,3]:
        if i == get_n(1):
            continue
        elif i == get_n(2):
            del undefined
        else:
            break
    x = undefined
    
test_undefined(test_del_4)

def test_params_1(undefined):
    AreEqual(undefined, 1)
    del undefined
    x = undefined

test_undefined(test_params_1, 1)

def test_params_2(undefined):
    AreEqual(undefined, 1)
    x = undefined
    AreEqual(x, 1)
    del undefined
    y = x
    AreEqual(y, 1)
    x = undefined

test_undefined(test_params_2, 1)

def test_params_3(a):
    if get_false(): return
    x = a
    undefined = a
    del undefined
    x = undefined

test_undefined(test_params_3, 1)

def test_default_params():
    if get_false():
        x = 1

    def f(a = x):
        locals()

test_unassigned(test_default_params)

def test_class_bases():
    if get_false():
        x = dict

    class C(x):
        locals()

test_unassigned(test_class_bases)

def test_conditional_member_def():
    class C(object):
        if get_false():
            x = 1
    
    C().x

test_attribute_error(test_conditional_member_def)

def test_member_access_on_undefined():
    undefined.member = 1
    locals()
  
test_undefined(test_member_access_on_undefined)

def test_item_access_on_undefined():
    undefined[1] = 1
    locals()
  
test_undefined(test_item_access_on_undefined)

def test_item_access_on_undefined_in_tuple():
    (undefined[1],x) = 1,2
    locals()
  
test_undefined(test_item_access_on_undefined_in_tuple)

def test_item_access_on_undefined_in_list():
    [undefined[1],x] = 1,2
    locals()
  
test_undefined(test_item_access_on_undefined_in_list)

def test_item_index_undefined():
    dict()[undefined] = 1
    locals()
  
test_undefined(test_item_index_undefined)

def test_nested_scope_variable_access():
    def g():
        def f():
            x = undefined
        return f
    g()()
    undefined = 1
  
test_undefined(test_nested_scope_variable_access)

#####################################################################################

from iptest.assert_util import *
result = "Failed"

a = 2
b = a
del a


try:
    b = a
except NameError:
    result = "Success"

Assert(result == "Success")


class C:
    pass

c = C()
c.a = 10
Assert("a" in dir(c))
del c.a
Assert(not "a" in dir(c))
C.a = 10
Assert("a" in dir(C))
del C.a
Assert(not "a" in dir(C))

for m in dir([]):
    c = isinstance(m, collections.Callable)
    a = getattr([], m)

success = False
try:
    del this_name_is_undefined
except NameError:
    success = True
Assert(success)


# verify __builtin__ is accessed if a global isn't defined
xyz = 'aaa'
AreEqual(xyz, 'aaa')

import builtins
builtins.xyz = 'abc'

xyz = 'def'
AreEqual(xyz, 'def')
del xyz
AreEqual(xyz, 'abc')

del builtins.xyz
try:
    a = xyz
except NameError: pass

## delete builtin func
import builtins

try:
    del pow
    AssertUnreachable("should have thrown")
except NameError: pass

class C(object):
    name = None
    def test(self):
        print(name)

AssertError(NameError, C().test)

try:
    del builtins.pow
    AssertError(NameError, lambda: pow)
    AssertError(AttributeError, lambda: builtins.pow)
finally:
    imp.reload(__builtin__)
    # make sure we still have access to __builtin__'s after reloading
    # AreEqual(pow(2,2), 4) # bug 359890
    dir('abc')

## Overriding __builtin__ method inconsistent with -X:LightweightScopes flag
import builtins
builtins.help = 10
AssertErrorWithPartialMessage(TypeError, "is not callable", lambda: help(dir))

# Test that run time name lookup skips over class scopes
# (because class variables aren't implicitly accessible inside member functions)
a = 123
class C(object):
    a = 456
    def foo(self):
        # this exec statement is here to cause the "a" in the next statement
        # to have its name looked up at run time.
        exec("dummy = 10")
        return a  # a should bind to the global a, not C.a

AreEqual(C().foo(), 123)

class C:
    codeplex_20956 = 3
    def foo(self):
        return codeplex_20956

AssertErrorWithMessage(NameError, "global name 'codeplex_20956' is not defined",
                       C().foo)

class ImportAsInClass:
    import sys as sys_in_class

class ImportInClass:
    import sys

Assert(ImportAsInClass.sys_in_class is ImportInClass.sys)

class FromImportAsInClass:
    from sys import path as sys_path

class FromImportInClass:
    from sys import path
    
Assert(FromImportAsInClass.sys_path is FromImportInClass.path)

global_class_member = "wrong value for the global_class_member"

class ClassWithGlobalMember:
    global global_class_member
    global_class_member = "global class member value"

Assert(not hasattr(ClassWithGlobalMember, "global_class_member"), "invalid binding of global in a class")
AreEqual(global_class_member, "global class member value")


def f():
    abc = eval("locals()")
    abc['foo'] = 42
    print(foo)

AssertError(NameError, f)


def f():
    x = locals()
    x['foo'] = 42
    print(foo)

AssertError(NameError, f)

def f():
    x = vars()
    x['foo'] = 42
    print(foo)

AssertError(NameError, f)

if not is_silverlight:
    def f():
        import nt
        f = file('temptest.py', 'w+')
        f.write('foo = 42')
        f.close()
        try:
            exec(compile(open('temptest.py').read(), 'temptest.py', 'exec'))
        finally:
            nt.unlink('temptest.py')
        return foo
    
    AssertError(NameError, f)

def f():
    exec("foo = 42")
    return foo

AreEqual(f(), 42)

if not is_silverlight:
    def f():
        import nt
        f = file('temptest.py', 'w+')
        f.write('foo = 42')
        f.close()
        try:
            from temptest import *
        finally:
            nt.unlink('temptest.py')
        return foo
    
    AreEqual(f(), 42)


def f():
    exec("foo = 42", {})
    return foo
  
AssertError(NameError, f)



def F():
  a = 4
  class C:
    field=7
    def G(self):
      b = a
      b = 4
      return locals()
  c = C()
  return c.G()

AreEqual(set(F()), set(['self', 'b', 'a']))


def f():
  a = 10
  def g1():
    return a, locals()
  def g2():
    return locals()
  res = [g1()]
  res.append(g2())
  return res

AreEqual(f(), [(10, {'a':10}), {}])

X = (42, 43)
class Y:
    def outer_f(self):
        def f(xxx_todo_changeme=X):     
            (a,b) = xxx_todo_changeme
            return a, b
        return f
        

a = Y()
AreEqual(a.outer_f()(), (42, 43))

