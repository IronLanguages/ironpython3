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

import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netstandard, run_test, skipUnlessIronPython
from types import FunctionType

global init

# defined globally because unqualified exec isn't allowed in
# a nested function.
def unqualified_exec():
    print x
    exec ""

def copyfunc(f, name):
    return FunctionType(f.func_code, f.func_globals, name, f.func_defaults, f.func_closure)


def substitute_globals(f, name, globals):
    return FunctionType(f.func_code, globals, name, f.func_defaults, f.func_closure)

global_variable = 13


def create_fn_with_closure():
    x=13
    def f():
        return x
    return f

def x(a,b,c):
    z = 8
    if a < b:
        return c
    elif c < 5 :
        return a + b
    else:
        return z

class C1:
    def f0(self): return 0
    def f1(self, a): return 1
    def f2(self, a, b): return 2
    def f3(self, a, b, c): return 3
    def f4(self, a, b, c, d): return 4
    def f5(self, a, b, c, d, e): return 5
    def f6(self, a, b, c, d, e, f): return 6
    def f7(self, a, b, c, d, e, f, g): return 7

class FunctionTest(IronPythonTestCase):
    def test_basics(self):
        self.assertTrue(x(1,2,10) == 10)
        self.assertTrue(x(2,1,4) == 3)
        self.assertTrue(x(1,1,10) == 8)

        def f():
            pass

        f.a = 10

        self.assertTrue(f.a == 10)
        self.assertEqual(f.__module__, __name__)

        def g():
            g.a = 20

        g()

        self.assertTrue(g.a == 20)


        def foo(): pass

        self.assertEqual(foo.func_code.co_filename.lower().endswith('test_function.py'), True)
        self.assertEqual(foo.func_code.co_firstlineno, 86)  # if you added lines to the top of this file you need to update this number.

    def test_inherit_function(self):
        def foo(): pass

        # Cannot inherit from a function
        def CreateSubType(t):
            class SubType(t): pass
            return SubType
            
        self.assertRaisesRegexp(TypeError, ".*\n?.* is not an acceptable base type", CreateSubType, type(foo))

    def test_varargs(self):
        def a(*args): return args
        def b(*args): return a(*args)
        self.assertEqual(b(1,2,3), (1,2,3))

    def test_default_values(self):
        def xwd(a=0,b=1,c=3):
            z = 8
            if a < b:
                return c
            elif c < 5 :
                return a + b
            else:
                return z
                
        self.assertEqual(x,x)
        self.assertEqual(xwd(), 3)
        self.assertRaises(TypeError, (lambda:x()))
        self.assertEqual(xwd(2), 3)
        self.assertRaises(TypeError, (lambda:x(1)))
        self.assertEqual(xwd(0,5), 3)
        self.assertRaises(TypeError, (lambda:x(0,5)))
        self.assertEqual( (x == "not-a-Function3"), False)

    def test_missin_params(self):
        def y(a,b,c,d):
            return a+b+c+d

        def ywd(a=0, b=1, c=2, d=3):
            return a+b+c+d

        self.assertEqual(y, y)
        self.assertEqual(ywd(), 6)
        self.assertRaises(TypeError, y)
        self.assertEqual(ywd(4), 10)
        self.assertRaises(TypeError, y, 4)
        self.assertEqual(ywd(4,5), 14)
        self.assertRaises(TypeError, y, 4, 5)
        self.assertEqual(ywd(4,5,6), 18)
        self.assertRaises(TypeError, y, 4,5,6)
        self.assertEqual( (y == "not-a-Function4"), False)

    def test__doc__(self):
        def foo(): "hello world"
        self.assertEqual(foo.__doc__, 'hello world')

    def test_coverage(self):

        # function5
        def f1(a=1, b=2, c=3, d=4, e=5):    return a * b * c * d * e
        def f2(a, b=2, c=3, d=4, e=5):    return a * b * c * d * e
        def f3(a, b, c=3, d=4, e=5):    return a * b * c * d * e
        def f4(a, b, c, d=4, e=5):    return a * b * c * d * e
        def f5(a, b, c, d, e=5):    return a * b * c * d * e
        def f6(a, b, c, d, e):    return a * b * c * d * e

        for f in (f1, f2, f3, f4, f5, f6):
            self.assertRaises(TypeError, f, 1, 1, 1, 1, 1, 1)             # 6 args
            self.assertEqual(f(10,11,12,13,14), 10 * 11 * 12 * 13 * 14)     # 5 args

        for f in (f1, f2, f3, f4, f5):
            self.assertEqual(f(10,11,12,13), 10 * 11 * 12 * 13 * 5)         # 4 args
        for f in (f6,):    
            self.assertRaises(TypeError, f, 1, 1, 1, 1)

        for f in (f1, f2, f3, f4):
            self.assertEqual(f(10,11,12), 10 * 11 * 12 * 4 * 5)             # 3 args
        for f in (f5, f6):    
            self.assertRaises(TypeError, f, 1, 1, 1)

        for f in (f1, f2, f3):
            self.assertEqual(f(10,11), 10 * 11 * 3 * 4 * 5)                 # 2 args
        for f in (f4, f5, f6):    
            self.assertRaises(TypeError, f, 1, 1)

        for f in (f1, f2):
            self.assertEqual(f(10), 10 * 2 * 3 * 4 * 5)                     # 1 args
        for f in (f3, f4, f5, f6):    
            self.assertRaises(TypeError, f, 1)

        for f in (f1,):
            self.assertEqual(f(), 1 * 2 * 3 * 4 * 5)                        # no args
        for f in (f2, f3, f4, f5, f6):    
            self.assertRaises(TypeError, f)


    def test_class_method(self):
        # method
        
        class C2: pass

        c1, c2 = C1(), C2()

        line = ""
        for i in range(8):
            args = ",".join(['1'] * i)
            line += "self.assertEqual(c1.f%d(%s), %d)\n" % (i, args, i)
            line +=  "self.assertEqual(C1.f%d(c1,%s), %d)\n" % (i, args, i)
            #line +=  "try: C1.f%d(%s) \nexcept TypeError: pass \nelse: raise AssertionError\n" % (i, args)
            #line +=  "try: C1.f%d(c2, %s) \nexcept TypeError: pass \nelse: raise AssertionError\n" % (i, args)

        #print line
        exec line

    def test_set_attr_instance_method(self):
        def SetAttrOfInstanceMethod():
            C1.f0.attr = 1
        self.assertRaises(AttributeError, SetAttrOfInstanceMethod)

        C1.f0.im_func.attr = 1
        self.assertEqual(C1.f0.attr, 1)
        self.assertEqual(dir(C1.f0).__contains__("attr"), True)

        self.assertEqual(C1.f0.__module__, __name__)

    def test_kwargs(self):

        def f(x=0, y=10, z=20, *args, **kws):
            return (x, y, z), args, kws

        self.assertTrue(f(10, l=20) == ((10, 10, 20), (), {'l': 20}))

        self.assertTrue(f(1, *(2,), **{'z':20}) == ((1, 2, 20), (), {}))

        self.assertTrue(f(*[1,2,3]) == ((1, 2, 3), (), {}))

        def a(*args, **kws): return args, kws

        def b(*args, **kws):
            return a(*args, **kws)

        self.assertTrue(b(1,2,3, x=10, y=20) == ((1, 2, 3), {'y': 20, 'x': 10}))

        def b(*args, **kws):
            return a(**kws)

        self.assertTrue(b(1,2,3, x=10, y=20) == ((), {'y': 20, 'x': 10}))

        try:
            b(**[])
            self.assertTrue(False)
        except TypeError:
            pass

        def f(x, *args):
            return (x, args)

        self.assertEqual(f(1, *[2]), (1, (2,)))
        self.assertEqual(f(7, *(i for i in range(3))), (7, (0, 1, 2,)))
        self.assertEqual(f(9, *range(11, 13)), (9, (11, 12)))

    def test_sorted_kwargs(self):
        """verify we can call sorted w/ keyword args"""
        import operator
        inventory = [('apple', 3), ('banana', 2), ('pear', 5), ('orange', 1)]
        getcount = operator.itemgetter(1)

        sorted_inventory = sorted(inventory, key=getcount)

    def test_kwargs2(self):
        """verify proper handling of keyword args for python functions"""
        def kwfunc(a,b,c):  pass

        try:
            kwfunc(10, 20, b=30)
            self.assertTrue(False)
        except TypeError:
            pass

        try:
            kwfunc(10, None, b=30)
            self.assertTrue(False)
        except TypeError:
            pass


        try:
            kwfunc(10, None, 40, b=30)
            self.assertTrue(False)
        except TypeError:
            pass

        if is_cli:
            import System
            
            # Test Hashtable and Dictionary on desktop, and just Dictionary in Silverlight
            # (Hashtable is not available)
            htlist = [System.Collections.Generic.Dictionary[System.Object, System.Object]()]
            if is_netstandard:
                import clr
                clr.AddReference("System.Collections.NonGeneric")
            htlist += [System.Collections.Hashtable()]

            for ht in htlist:
                def foo(**kwargs):
                    return kwargs['key']
                    
                ht['key'] = 'xyz'
                
                self.assertEqual(foo(**ht), 'xyz')

        def foo(a,b):
            return a-b
            
        self.assertEqual(foo(b=1, *(2,)), 1)

        # kw-args passed to init through method instance
        s = self
        class foo:
            def __init__(self, group=None, target=None):
                    s.assertEqual(group, None)
                    s.assertEqual(target,'baz')

        a = foo(target='baz')

        foo.__init__(a, target='baz')


    @skipUnlessIronPython()
    def test_params_method_no_params(self):
        """call a params method w/ no params"""
        import clr
        import System
        self.assertEqual('abc\ndef'.Split()[0], 'abc') 
        self.assertEqual('abc\ndef'.Split()[1], 'def')
        x = 'a bc   def'.Split()
        self.assertEqual(x[0], 'a')
        self.assertEqual(x[1], 'bc')
        self.assertEqual(x[2], '')
        self.assertEqual(x[3], '')
        self.assertEqual(x[4], 'def')
        
        # calling Double.ToString(...) should work - Double is
        # an OpsExtensibleType and doesn't define __str__ on this
        # overload
        
        self.assertEqual(System.Double.ToString(1.0, 'f'), '1.00')


    def test_incorrect_number_of_args(self):
        """Incorrect number of arguments"""

        def f(a): pass

        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", f)
        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (3 given)", f, 1, 2, 3)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
        #self.assertRaises calls f(*args), which generates a different AST than f(1,2,3)
        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f())
        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (3 given)", lambda:f(1, 2, 3))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=2))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=2))

        def f(a,b,c,d,e,f,g,h,i,j): pass

        self.assertRaisesMessage(TypeError, "f() takes exactly 10 arguments (0 given)", f)
        self.assertRaisesMessage(TypeError, "f() takes exactly 10 arguments (3 given)", f, 1, 2, 3)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
        self.assertRaisesMessage(TypeError, "f() takes exactly 10 arguments (0 given)", lambda:f())
        self.assertRaisesMessage(TypeError, "f() takes exactly 10 arguments (3 given)", lambda:f(1, 2, 3))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=2))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=2))

        def f(a, b=2): pass

        self.assertRaisesMessage(TypeError, "f() takes at least 1 argument (0 given)", f)
        self.assertRaisesMessage(TypeError, "f() takes at most 2 arguments (3 given)", f, 1, 2, 3)
        if is_cli: #CPython bug 9326
            self.assertRaisesMessage(TypeError, "f() takes at least 1 non-keyword argument (0 given)", f, b=2)
        else:
            self.assertRaisesMessage(TypeError, "f() takes at least 1 argument (1 given)", f, b=2)
            
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=3)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, b=2, dummy=3)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, 1, dummy=3)
        self.assertRaisesMessage(TypeError, "f() takes at least 1 argument (0 given)", lambda:f())
        self.assertRaisesMessage(TypeError, "f() takes at most 2 arguments (3 given)", lambda:f(1, 2, 3))
        if is_cli: #CPython bug 9326
            self.assertRaisesMessage(TypeError, "f() takes at least 1 non-keyword argument (0 given)", lambda:f(b=2))
        else:
            self.assertRaisesMessage(TypeError, "f() takes at least 1 argument (1 given)", lambda:f(b=2))
            
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=3))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(b=2, dummy=3))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=3))

        def f(a, *argList): pass

        self.assertRaisesMessage(TypeError, "f() takes at least 1 argument (0 given)", f)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, dummy=2)
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", f, 1, dummy=2)
        self.assertRaisesMessage(TypeError, "f() takes at least 1 argument (0 given)", lambda:f())
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(dummy=2))
        self.assertRaisesMessage(TypeError, "f() got an unexpected keyword argument 'dummy'", lambda:f(1, dummy=2))

        def f(a, **keywordDict): pass

        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", f)
        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (3 given)", f, 1, 2, 3)
        if is_cli: #CPython bug 9326
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", f, dummy=2)
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", f, dummy=2, dummy2=3)
        else:
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", f, dummy=2)
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", f, dummy=2, dummy2=3)
        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f())
        self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (3 given)", lambda:f(1, 2, 3))
        if is_cli: #CPython bug 9326
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", lambda:f(dummy=2))
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 non-keyword argument (0 given)", lambda:f(dummy=2, dummy2=3))
        else:
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f(dummy=2))
            self.assertRaisesMessage(TypeError, "f() takes exactly 1 argument (0 given)", lambda:f(dummy=2, dummy2=3))

        self.assertRaisesMessages(TypeError, "abs() takes exactly 1 argument (0 given)",
                                        "abs() takes exactly one argument (0 given)",   abs)
        self.assertRaisesMessages(TypeError, "abs() takes exactly 1 argument (3 given)",
                                        "abs() takes exactly one argument (3 given)",   abs, 1, 2, 3)
        self.assertRaisesMessages(TypeError, "abs() got an unexpected keyword argument 'dummy'",
                                        "abs() takes no keyword arguments",             abs, dummy=2)
        self.assertRaisesMessages(TypeError, "abs() takes exactly 1 argument (2 given)",
                                        "abs() takes no keyword arguments",             abs, 1, dummy=2)
        self.assertRaisesMessages(TypeError, "abs() takes exactly 1 argument (0 given)",
                                        "abs() takes exactly one argument (0 given)",   lambda:abs())
        self.assertRaisesMessages(TypeError, "abs() takes exactly 1 argument (3 given)",
                                        "abs() takes exactly one argument (3 given)",   lambda:abs(1, 2, 3))
        self.assertRaisesMessages(TypeError, "abs() got an unexpected keyword argument 'dummy'",
                                        "abs() takes no keyword arguments",             lambda:abs(dummy=2))
        self.assertRaisesMessages(TypeError, "abs() takes exactly 1 argument (2 given)",
                                        "abs() takes no keyword arguments",             lambda:abs(1, dummy=2))

        # list([m]) has one default argument (built-in type)
        #self.assertRaisesMessage(TypeError, "list() takes at most 1 argument (2 given)", list, 1, 2)
        #self.assertRaisesMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, list, [], dict({"dummy":2}))

        #======== BUG 697 ===========
        #self.assertRaisesMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, list, [1], dict({"dummy":2}))

        # complex([x,y]) has two default argument (OpsReflectedType type)
        #self.assertRaisesMessage(TypeError, "complex() takes at most 2 arguments (3 given)", complex, 1, 2, 3)
        #self.assertRaisesMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, complex, [], dict({"dummy":2}))
        #self.assertRaisesMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, complex, [1], dict({"dummy":2}))

        # bool([x]) has one default argument (OpsReflectedType and valuetype type)
        #self.assertRaisesMessage(TypeError, "bool() takes at most 1 argument (2 given)", bool, 1, 2)
        #self.assertRaisesMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, bool, [], dict({"dummy":2}))
        #self.assertRaisesMessage(TypeError, "'dummy' is an invalid keyword argument for this function", apply, bool, [1], dict({"dummy":2}))

        class UserClass(object): pass
        self.assertRaisesMessage(TypeError, "object.__new__() takes no parameters", UserClass, 1)
        self.assertRaisesMessage(TypeError, "object.__new__() takes no parameters", apply, UserClass, [], dict({"dummy":2}))
            
        class OldStyleClass: pass
        self.assertRaisesMessage(TypeError, "this constructor takes no arguments", OldStyleClass, 1)
        self.assertRaisesMessage(TypeError, "this constructor takes no arguments", apply, OldStyleClass, [], dict({"dummy":2}))

    @skipUnlessIronPython()
    def test_runtime_type_checking(self):
        """accepts / returns runtype type checking tests"""

        import clr
        
        @clr.accepts(object)
        def foo(x): 
            return x

        self.assertEqual(foo('abc'), 'abc')
        self.assertEqual(foo(2), 2)
        self.assertEqual(foo(2L), 2L)
        self.assertEqual(foo(2.0), 2.0)
        self.assertEqual(foo(True), True)


        @clr.accepts(str)
        def foo(x):
            return x
            
        self.assertEqual(foo('abc'), 'abc')
        self.assertRaises(AssertionError, foo, 2)
        self.assertRaises(AssertionError, foo, 2L)
        self.assertRaises(AssertionError, foo, 2.0)
        self.assertRaises(AssertionError, foo, True)

        @clr.accepts(str, bool)
        def foo(x, y):
            return x, y

        self.assertEqual(foo('abc', True), ('abc', True))
        self.assertRaises(AssertionError, foo, ('abc',2))
        self.assertRaises(AssertionError, foo, ('abc',2L))
        self.assertRaises(AssertionError, foo, ('abc',2.0))


        class bar:  
            @clr.accepts(clr.Self(), str)
            def foo(self, x):
                return x


        a = bar()
        self.assertEqual(a.foo('xyz'), 'xyz')
        self.assertRaises(AssertionError, a.foo, 2)
        self.assertRaises(AssertionError, a.foo, 2L)
        self.assertRaises(AssertionError, a.foo, 2.0)
        self.assertRaises(AssertionError, a.foo, True)

        @clr.returns(str)
        def foo(x):
            return x


        self.assertEqual(foo('abc'), 'abc')
        self.assertRaises(AssertionError, foo, 2)
        self.assertRaises(AssertionError, foo, 2L)
        self.assertRaises(AssertionError, foo, 2.0)
        self.assertRaises(AssertionError, foo, True)

        @clr.accepts(bool)
        @clr.returns(str)
        def foo(x):
            if x: return str(x)
            else: return 0
            
        self.assertEqual(foo(True), 'True')

        self.assertRaises(AssertionError, foo, 2)
        self.assertRaises(AssertionError, foo, 2)
        self.assertRaises(AssertionError, foo, False)

        @clr.returns(None)
        def foo(): pass

        self.assertEqual(foo(), None)

    def test_error_message(self):
        try:
            buffer()
        except TypeError, e:
            # make sure we get the right type name when calling w/ wrong # of args
            self.assertEqual(str(e)[:8], 'buffer()')
    
        #try:
        #    list(1,2,3)
        #except TypeError, e:
            # make sure we get the right type name when calling w/ wrong # of args
        #    self.assertEqual(str(e)[:6], 'list()')    

        # oldinstance
        class foo:
            def bar(self): pass
            def bar1(self, xyz): pass
            
        class foo2: pass
        class foo3(object): pass

        self.assertRaises(TypeError, foo.bar)
        self.assertRaises(TypeError, foo.bar1, None, None)
        self.assertRaises(TypeError, foo.bar1, None, 'abc')
        self.assertRaises(TypeError, foo.bar1, 'xyz', 'abc')
        self.assertRaises(TypeError, foo.bar, foo2())
        self.assertRaises(TypeError, foo.bar, foo3())

        # usertype
        class foo(object):
            def bar(self): pass
            def bar1(self, xyz): pass

        self.assertRaises(TypeError, foo.bar)
        self.assertRaises(TypeError, foo.bar1, None, None)
        self.assertRaises(TypeError, foo.bar1, None, 'abc')
        self.assertRaises(TypeError, foo.bar1, 'xyz', 'abc')
        self.assertRaises(TypeError, foo.bar, foo2())
        self.assertRaises(TypeError, foo.bar, foo3())

    def test_caller_context(self):
        # access a method w/ caller context w/ an args parameter.
        def foo(*args):
            return hasattr(*args)
            
        self.assertEqual(foo('', 'index'), True)

    @skipUnlessIronPython()
    def test_dispatch_to_ReflectOptimized(self):
        """dispatch to a ReflectOptimized method"""

        from iptest.console_util import IronPythonInstance
        from System import Environment
        from sys import executable
        
        wkdir = self.test_dir
        
        if "-X:LightweightScopes" in Environment.GetCommandLineArgs():
            ipi = IronPythonInstance(executable, wkdir, "-X:LightweightScopes", "-X:BasicConsole")
        else:
            ipi = IronPythonInstance(executable, wkdir, "-X:BasicConsole")

        if (ipi.Start()):
            try:
                result = ipi.ExecuteLine("from iptest.ipunittest import load_ironpython_test")
                result = ipi.ExecuteLine("load_ironpython_test()")
                result = ipi.ExecuteLine("from IronPythonTest import DefaultParams")
                response = ipi.ExecuteLine("DefaultParams.FuncWithDefaults(1100, z=82)")
                self.assertEqual(response, '1184')
            finally:
                ipi.End()

    def test_zip(self):
        p = ((1, 2),)

        self.assertEqual(zip(*(p * 10)), [(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), (2, 2, 2, 2, 2, 2, 2, 2, 2, 2)])
        self.assertEqual(zip(*(p * 10)), [(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), (2, 2, 2, 2, 2, 2, 2, 2, 2, 2)])

    def test_super(self):

        class A(object): pass

        class B(A): pass

        #unbound super
        for x in [super(B), super(B,None)]:
            self.assertEqual(x.__thisclass__, B)
            self.assertEqual(x.__self__, None)
            self.assertEqual(x.__self_class__, None)

        # super w/ both types
        x = super(B,B)

        self.assertEqual(x.__thisclass__,B)
        self.assertEqual(x.__self_class__, B)
        self.assertEqual(x.__self__, B)

        # super w/ type and instance
        b = B()
        x = super(B, b)

        self.assertEqual(x.__thisclass__,B)
        self.assertEqual(x.__self_class__, B)
        self.assertEqual(x.__self__, b)

        # super w/ mixed types
        x = super(A,B)
        self.assertEqual(x.__thisclass__,A)
        self.assertEqual(x.__self_class__, B)
        self.assertEqual(x.__self__, B)

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
        self.assertEqual(super(B,b).__thisclass__.__name__, 'B')
        self.assertEqual(super(B,b).__self__.__name__, 'derived')
        self.assertEqual(super(B,b).__self_class__.__name__, 'B')

        self.assertEqual(super(B,b).classmeth(), 'B')


        # descriptor supper
        class A(object):
            def meth(self): return 'A'
            
        class B(A):
            def meth(self):    
                return 'B' + self.__super.meth()
                
        B._B__super = super(B)
        b = B()
        self.assertEqual(b.meth(), 'BA')

    def test_class_method_calls(self):
        """class method should get correct meta class."""

        class D(object):
            @classmethod
            def classmeth(cls): pass
            
        self.assertEqual(D.classmeth.im_class, type)

        class MetaType(type): pass

        class D(object):
            __metaclass__ = MetaType
            @classmethod
            def classmeth(cls): pass
            
        self.assertEqual(D.classmeth.im_class, MetaType)

    def test_cases(self):
        def runTest(testCase):
            class foo(testCase.subtype):
                def __new__(cls, param):
                    ret = testCase.subtype.__new__(cls, param)
                    self.assertTrue(ret == testCase.newEq)
                    self.assertTrue((ret != testCase.newEq) != True)
                    return ret
                def __init__(self, param):
                    testCase.subtype.__init__(self, param)
                    self.assertTrue(self == testCase.initEq)
                    self.assertTrue((self != testCase.initEq) != True)

            a = foo(testCase.param)
            self.assertTrue((type(a) == foo) == testCase.match)

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

    @unittest.skipIf(is_netstandard or is_mono, 'missing System.Windows.Forms support')
    @skipUnlessIronPython()
    def test_call_base_init(self):
        """verify we can call the base init directly"""

        import clr
        clr.AddReferenceByPartialName('System.Windows.Forms')
        from System.Windows.Forms import Form

        class MyForm(Form):
            def __init__(self, title):
                Form.__init__(self)
                self.Text = title

        a = MyForm('abc')
        self.assertEqual(a.Text, 'abc')

#TestCase(bool, True, True),                    # not an acceptable base type

    def test_func_flags(self):
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
        
        self.assertEqual(foo0.func_code.co_flags & 12, 0)
        self.assertEqual(foo1.func_code.co_flags & 12, 4)
        self.assertEqual(foo2.func_code.co_flags & 12, 8)
        self.assertEqual(foo3.func_code.co_flags & 12, 12)
        self.assertEqual(foo4.func_code.co_flags & 12, 0)
        self.assertEqual(foo5.func_code.co_flags & 12, 4)
        self.assertEqual(foo6.func_code.co_flags & 12, 8)
        self.assertEqual(foo7.func_code.co_flags & 12, 12)
        self.assertEqual(foo8.func_code.co_flags & 12, 0)
        self.assertEqual(foo9.func_code.co_flags & 12, 0)
        
        self.assertEqual(foo0.func_code.co_argcount, 0)
        self.assertEqual(foo1.func_code.co_argcount, 0)
        self.assertEqual(foo2.func_code.co_argcount, 0)
        self.assertEqual(foo3.func_code.co_argcount, 0)
        self.assertEqual(foo4.func_code.co_argcount, 1)
        self.assertEqual(foo5.func_code.co_argcount, 1)
        self.assertEqual(foo6.func_code.co_argcount, 1)
        self.assertEqual(foo7.func_code.co_argcount, 1)
        self.assertEqual(foo8.func_code.co_argcount, 6)
        self.assertEqual(foo9.func_code.co_argcount, 2)

    def test_big_calls(self):
        # check various function call sizes and boundaries
        sizes = [3, 4, 5, 7, 8, 9, 13, 15, 16, 17, 23, 24, 25, 31, 32, 33, 47, 48, 49, 63, 64, 65, 127, 128, 129, 254, 255, 256, 257, 258, 511, 512, 513]


        # mono has a limitation of < 1023
        if not is_mono:
            sizes.extend([1023, 1024, 1025, 2047, 2048, 2049])

        for size in sizes:
            # w/o defaults
            exec 'def f(' + ','.join(['a' + str(i) for i in range(size)]) + '): return ' + ','.join(['a' + str(i) for i in range(size)])
            # w/ defaults
            exec 'def g(' + ','.join(['a' + str(i) + '=' + str(i) for i in range(size)]) + '): return ' + ','.join(['a' + str(i) for i in range(size)])
            if (size <= 255 or is_cli) and not is_netstandard or size <= 13: # PlatformNotSupportedException when > 13
                # CPython allows function definitions > 255, but not calls w/ > 255 params.
                exec 'a = f(' + ', '.join([str(x) for x in xrange(size)]) + ')'
                self.assertEqual(a, tuple(xrange(size)))
                exec 'a = g()'
                self.assertEqual(a, tuple(xrange(size)))
                exec 'a = g(' + ', '.join([str(x) for x in xrange(size)]) + ')'
                self.assertEqual(a, tuple(xrange(size)))
            
            exec 'a = f(*(' + ', '.join([str(x) for x in xrange(size)]) + '))'
            self.assertEqual(a, tuple(xrange(size)))
    
    def test_compile(self):
        x = compile("print 2/3", "<string>", "exec", 8192)
        self.assertTrue((x.co_flags & 8192) == 8192)
        
        x = compile("2/3", "<string>", "eval", 8192)
        self.assertEqual(eval(x), 2.0 / 3.0)

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
            self.assertEqual(compile("print 2/3", name, "exec", 8192).co_filename,
                    name)

    def test_filename(self):
        c = compile("x = 2", "test", "exec")
        self.assertEqual(c.co_filename, 'test')
    
    def test_name(self):
        def f(): pass
        
        f.__name__ = 'g'
        self.assertEqual(f.__name__, 'g')
        self.assertTrue(repr(f).startswith('<function g'))
        
        f.func_name = 'x'
        self.assertEqual(f.__name__, 'x')
        self.assertTrue(repr(f).startswith('<function x'))

    def test_argcount(self):
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
        
        self.assertEqual(foo0.func_code.co_argcount, 0)
        self.assertEqual(foo1.func_code.co_argcount, 0)
        self.assertEqual(foo2.func_code.co_argcount, 0)
        self.assertEqual(foo3.func_code.co_argcount, 0)
        self.assertEqual(foo4.func_code.co_argcount, 1)
        self.assertEqual(foo5.func_code.co_argcount, 1)
        self.assertEqual(foo6.func_code.co_argcount, 1)
        self.assertEqual(foo7.func_code.co_argcount, 1)
        self.assertEqual(foo8.func_code.co_argcount, 6)
        self.assertEqual(foo9.func_code.co_argcount, 2)

    def test_defaults(self):
        defaults = [None, object, int, [], 3.14, [3.14], (None,), "a string"]
        for default in defaults:
            def helperFunc(): pass
            self.assertEqual(helperFunc.func_defaults, None)
            self.assertEqual(helperFunc.func_defaults, None)
        
            def helperFunc1(a): pass
            self.assertEqual(helperFunc1.func_defaults, None)
            self.assertEqual(helperFunc1.func_defaults, None)
            
            
            def helperFunc2(a=default): pass
            self.assertEqual(helperFunc2.func_defaults, (default,))
            helperFunc2(a=7)
            self.assertEqual(helperFunc2.func_defaults, (default,))
            
            
            def helperFunc3(a, b=default, c=[42]): c.append(b)
            self.assertEqual(helperFunc3.func_defaults, (default, [42]))
            helperFunc3("stuff")
            self.assertEqual(helperFunc3.func_defaults, (default, [42, default]))

    def test_splat_defaults(self):
        def g(a, b, x=None):
            return a, b, x
            
        def f(x, *args):        
            return g(x, *args)
            
        self.assertEqual(f(1, *(2,)), (1,2,None))

    def test_argument_eval_order(self):
        """Check order of evaluation of function arguments"""
        x = [1]
        def noop(a, b, c):
            pass
        noop(x.append(2), x.append(3), x.append(4))
        self.assertEqual(x, [1,2,3,4])

    def test_method_attr_access(self):
        class foo(object):
            def f(self): pass
            abc = 3
        
        method = type(foo.f)    
        
        self.assertEqual(method(foo, 'abc').abc, 3)

    #TODO: @skip("interpreted")  # we don't have FuncEnv's in interpret modes so this always returns None
    def test_function_closure_negative(self):
        def f(): pass
        
        for assignment_val in [None, 1, "a string"]:
            try:
                f.func_closure = assignment_val
                AssertUnreachable("func_closure is a read-only attribute of functions")
            except TypeError, e:
                pass

    def test_paramless_function_call_error(self):
        def f(): pass
        
        try:
            f(*(1, ))
            AssertUnreachable()
        except TypeError: pass
            
        try:
            f(**{'abc':'def'})
            AssertUnreachable()
        except TypeError: pass


    def test_function_closure(self):
        def f(): pass
        
        self.assertEqual(f.func_closure, None)
        
        def f():    
            def g(): pass
            return g
            
        self.assertEqual(f().func_closure, None)

        def f():
            x = 4
            def g(): return x
            return g
            
        self.assertEqual(sorted([x.cell_contents for x in f().func_closure]), [4])
        
        def f():
            x = 4
            def g():
                y = 5
                def h(): return x,y
                return h
            return g()

        self.assertEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5])

        # don't use z
        def f():
            x = 4
            def g():
                y = 5
                z = 7
                def h(): return x,y
                return h
            return g()
        
        self.assertEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5])
        
        def f():
            x = 4
            def g():
                y = 5
                z = 7
                def h(): return x,y,z
                return h
            return g()

        self.assertEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5, 7])

        def f():
            x = 4
            a = 9
            def g():
                y = 5
                z = 7
                def h(): return x,y
                return h
            return g()

        self.assertEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5])
        
        # closure cells are not recreated
        callRes = f()
        a = sorted([id(x) for x in callRes.func_closure])
        b = sorted([id(x) for x in callRes.func_closure])
        self.assertEqual(a, b)

        def f():
            x = 4
            a = 9
            def g():
                y = 5
                z = 7
                def h(): return x,y,a,z
                return h
            return g()

        self.assertEqual(sorted([x.cell_contents for x in f().func_closure]), [4, 5, 7, 9])

        self.assertRaises(TypeError, hash, f().func_closure[0])
        
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

        self.assertEqual(f().func_closure[0], h().func_closure[0])
        self.assertTrue(f().func_closure[0] != j().func_closure[0])

        # <cell at 45: int object at 44>
        self.assertTrue(repr(f().func_closure[0]).startswith('<cell at '))
        self.assertTrue(repr(f().func_closure[0]).find(': int object at ') != -1)
    
    
    def test_func_code(self):
        def foo(): pass
        def assign(): foo.func_code = None
        self.assertRaises(TypeError, assign)
    
    def def_func_doc(self):
        foo.func_doc = 'abc'
        self.assertEqual(foo.__doc__, 'abc')
        foo.__doc__ = 'def'
        self.assertEqual(foo.func_doc, 'def')
        foo.func_doc = None
        self.assertEqual(foo.__doc__, None)
        self.assertEqual(foo.func_doc, None)

    def test_func_defaults(self):
        def f(a, b): return (a, b)

        f.func_defaults = (1,2)
        self.assertEqual(f(), (1,2))
        
        f.func_defaults = (1,2,3,4)
        self.assertEqual(f(), (3,4))

        f.func_defaults = None
        self.assertRaises(TypeError, f)

        f.func_defaults = (1,2)
        self.assertEqual(f.func_defaults, (1,2))
        
        del f.func_defaults
        self.assertEqual(f.func_defaults, None)
        del f.func_defaults
        self.assertEqual(f.func_defaults, None)
        
        def func_with_many_args(one, two, three, four, five, six, seven, eight, nine, ten, eleven=None, twelve=None, thirteen=None, fourteen=None, fifteen=None, sixteen=None, seventeen=None, eighteen=None, nineteen=None):
            print 'hello'
            
        func_with_many_args(None, None, None, None, None, None, None, None, None, None)


    def test_func_dict(self):
        def f(): pass
        
        f.abc = 123
        self.assertEqual(f.func_dict, {'abc': 123})
        f.func_dict = {'def': 'def'}
        self.assertEqual(hasattr(f, 'def'), True)
        self.assertEqual(getattr(f, 'def'), 'def')
        f.func_dict = {}
        self.assertEqual(hasattr(f, 'abc'), False)
        self.assertEqual(hasattr(f, 'def'), False)
            
        self.assertRaises(TypeError, lambda : delattr(f, 'func_dict'))
        self.assertRaises(TypeError, lambda : delattr(f, '__dict__'))
    
    def test_method(self):
        class C:
            def method(self): pass
        
        method = type(C.method)(id, None, 'abc')
        self.assertEqual(method.im_class, 'abc')
        
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
        self.assertEqual(m1, m2)
        
        self.assertTrue('eq' in func1.called)
        self.assertTrue('eq' in inst1.called)

        hash(m1)   
        self.assertTrue('hash' in func1.called)
        self.assertTrue('hash' in inst1.called)
    
    def test_function_type(self):
        def f1(): pass
        def f2(a): pass
        def f3(a, b, c): pass
        def f4(*a, **b): pass
        
        def decorator(f): return f
        @decorator
        def f5(a): pass
        
        for x in [ f2, f3, f4, f5]:
            self.assertEqual(type(f1), type(x))

    def test_name_mangled_params(self):
        def f1(__a): pass
        def f2(__a): return __a
        def f3(a, __a): return __a
        def f4(_a, __a): return _a + __a
        
        f1("12")
        self.assertEqual(f2("hello"), "hello")
        self.assertEqual(f3("a","b"), "b")
        self.assertEqual(f4("a","b"), "ab")

    def test_splat_none(self):
        def f(*args): pass
        def g(**kwargs): pass
        def h(*args, **kwargs): pass
        
        #CodePlex 20250
        self.assertRaisesMessage(TypeError, "f() argument after * must be a sequence, not NoneType", 
                            lambda : f(*None))
        self.assertRaisesMessage(TypeError, "g() argument after ** must be a mapping, not NoneType", 
                            lambda : g(**None))
        self.assertRaisesMessage(TypeError, "h() argument after ** must be a mapping, not NoneType",
                            lambda : h(*None, **None))

    def test_exec_funccode(self):
        # can't exec a func code w/ parameters
        def f(a, b, c): print a, b, c
        
        self.assertRaises(TypeError, lambda : eval(f.func_code))
        
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
        
        self.assertRaises(TypeError, lambda : eval(f()))
    
    def test_exec_funccode_filename(self):
        import sys
        mod = type(sys)('fake_mod_name')
        mod.__file__ = 'some file'
        exec "def x(): pass" in mod.__dict__
        self.assertEqual(mod.x.func_code.co_filename, '<string>')


    def test_func_code_variables(self):
        def CompareCodeVars(code, varnames, names, freevars, cellvars):
            self.assertEqual(code.co_varnames, varnames)
            self.assertEqual(code.co_names, names)
            self.assertEqual(code.co_freevars, freevars)
            self.assertEqual(code.co_cellvars, cellvars)
        
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
            
        self.assertEqual(unqualified_exec.func_code.co_names, ('x', ))

    def test_delattr(self):
        def f(): pass
        f.abc = 42
        del f.abc
        def g(): f.abc
        self.assertRaises(AttributeError, g)


    def test_cp35180(self):
        def foo():
            return 13
        def bar():
            return 42
        dpf = copyfunc(foo, "dpf")
        self.assertEqual(dpf(), 13)
        foo.func_code = bar.func_code
        self.assertEqual(foo(), 42)
        self.assertEqual(dpf(), 13)
        self.assertEqual(foo.__module__, '__main__')
        self.assertEqual(dpf.__module__, '__main__')

    def test_cp34932(self):
        def get_global_variable():
            return global_variable
        def set_global_variable(v):
            global global_variable
            global_variable = v

        alt_globals = {'global_variable' : 66 }
        get_global_variable_x = substitute_globals(get_global_variable, "get_global_variable_x", alt_globals)
        set_global_variable_x = substitute_globals(set_global_variable, "set_global_variable_x", alt_globals)
        self.assertEqual(get_global_variable(), 13)
        self.assertEqual(get_global_variable_x(), 66)
        self.assertEqual(get_global_variable(), 13)
        set_global_variable_x(7)
        self.assertEqual(get_global_variable_x(), 7)
        self.assertEqual(get_global_variable(), 13)

        self.assertEqual(get_global_variable_x.__module__, None)
        self.assertEqual(set_global_variable_x.__module__, None)

        get_global_variable_y = substitute_globals(get_global_variable, "get_global_variable_x", globals())
        self.assertEqual(get_global_variable_y(), 13)
        self.assertEqual(get_global_variable_y.__module__, '__main__')

    def test_issue1351(self):
        class X(object):
            def __init__(self, res):
                self.called = []
                self.res = res
            def __eq__(self, other):
                self.called.append('eq')
                return self.res
            def foo(self):
                pass

        a = X(True)
        b = X(False)

        self.assertEqual(a.foo, a.foo)
        self.assertNotIn('eq', a.called)
        self.assertEqual(a.foo, b.foo)
        self.assertIn('eq', a.called)

        self.assertEqual(b.foo, b.foo)
        self.assertNotIn('eq', b.called)
        self.assertNotEqual(b.foo, a.foo)
        self.assertIn('eq', b.called)

    def test_function_type(self):
        fn_with_closure = create_fn_with_closure()
        def fn_no_closure():
            pass
        self.assertRaises(NotImplementedError, copyfunc, fn_with_closure, "new_fn_name")
        self.assertRaises(NotImplementedError, FunctionType, fn_with_closure.func_code,
                fn_with_closure.func_globals, "name", fn_with_closure.func_defaults)
        self.assertRaises(NotImplementedError, FunctionType, fn_with_closure.func_code,
                fn_with_closure.func_globals, "name", fn_with_closure.func_defaults,
                fn_with_closure.func_closure)
        self.assertRaises(NotImplementedError, FunctionType, fn_no_closure.func_code,
                fn_no_closure.func_globals, "name", fn_no_closure.func_defaults,
                fn_with_closure.func_closure)
   
run_test(__name__)
