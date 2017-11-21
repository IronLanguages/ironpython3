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

import sys
import unittest

from iptest import run_test
import collections
from functools import reduce

#-----------------------------------------------------------------------------------
#These have to be run first: importing iptest.assert_util masked a bug. Just make sure
#these do not throw
for stuff in [bool, True, False]:
    temp = dir(stuff)

items = list(globals().items()) #4716

class BuiltinsTest1(unittest.TestCase):
    def cp946(self):
        builtins = __builtins__
        if type(builtins) is type(sys):
            builtins = builtins.__dict__
        
        self.assertIn('hasattr', builtins, 'hasattr should be in __builtins__')
        self.assertNotIn('HasAttr', builtins, 'HasAttr should not be in __builtins__')

    def test_00_no_clr_import(self):
        self.cp946()

    @unittest.skipUnless(sys.platform=='cli', 'IronPython specific test')
    def test_01_with_clr_import(self):
        import clr
        self.cp946()

top_level_dir = dir()

x = 10
y = 20

from iptest import IronPythonTestCase, is_cli, is_netcoreapp

class BuiltinsTest2(IronPythonTestCase):

    def test_name_error(self):
        self.assertRaises(NameError, lambda: __new__)

    def test_callable(self):
        class C: x=1

        self.assertTrue(isinstance(min, collections.Callable))
        self.assertTrue(not isinstance("a", collections.Callable))
        self.assertTrue(isinstance(callable, collections.Callable))
        self.assertTrue(isinstance(lambda x, y: x + y, collections.Callable))
        self.assertTrue(isinstance(C, collections.Callable))
        self.assertTrue(not isinstance(C.x, collections.Callable))
        self.assertTrue(not isinstance(__builtins__, collections.Callable))

    def test_callable_oldclass(self):
        # for class instances, callable related to whether the __call__ attribute is defined.
        # This can be mutated at runtime.
        class Dold:
            pass
        d=Dold()
        #
        self.assertEqual(isinstance(d, collections.Callable), False)
        #
        d.__call__ = None # This defines the attr, even though it's None
        self.assertEqual(isinstance(d, collections.Callable), True) # True for oldinstance, False for new classes.
        #
        del (d.__call__) # now remove the attr, no longer callable
        self.assertEqual(isinstance(d, collections.Callable), False)

    def test_callable_newclass(self):
        class D(object):
            pass
        self.assertEqual(isinstance(D, collections.Callable), True)
        d=D()
        self.assertEqual(isinstance(d, collections.Callable), False)
        #
        # New class with a __call__ defined is callable()
        class D2(object):
            def __call__(self): pass
        d2=D2()
        self.assertEqual(isinstance(d2, collections.Callable), True)
        # Inherit callable
        class D3(D2):
            pass
        d3=D3()
        self.assertEqual(isinstance(d3, collections.Callable), True)

    def test_cmp(self):
        x = {}
        x['foo'] = x
        y = {}
        y['foo'] = y

        self.assertRaises(RuntimeError, cmp, x, y)

    def test_reversed(self):
        class ToReverse:
            a = [1,2,3,4,5,6,7,8,9,0]
            def __len__(self): return len(self.a)
            def __getitem__(self, i): return self.a[i]

        x = []
        for i in reversed(ToReverse()):
            x.append(i)

        self.assertTrue(x == [0,9,8,7,6,5,4,3,2,1])

        # more tests for 'reversed'
        self.assertRaises(TypeError, reversed, 1) # arg to reversed must be a sequence
        self.assertRaises(TypeError, reversed, None)
        self.assertRaises(TypeError, reversed, ToReverse)
        
        # no __len__ on class, reversed should throw
        class x(object):
            def __getitem__(self, index): return 2
        
        def __len__(): return 42

        a = x()
        a.__len__ = __len__
        self.assertRaises(TypeError, reversed, a)

        # no __len__ on class, reversed should throw
        class x(object):
            def __len__(self): return 42

        def __getitem__(index): return 2    
        a = x()
        a.__getitem__ = __getitem__
        self.assertRaises(TypeError, reversed, a)


    def test_reduce(self):
        def add(x,y): return x+y;
        self.assertTrue(reduce(add, [2,3,4]) == 9)
        self.assertTrue(reduce(add, [2,3,4], 1) == 10)

        self.assertRaises(TypeError, reduce, None, [1,2,3]) # arg 1 must be callable
        self.assertRaises(TypeError, reduce, None, [2,3,4], 1)
        self.assertRaises(TypeError, reduce, add, None) # arg 2 must support iteration
        self.assertRaises(TypeError, reduce, add, None, 1)
        self.assertRaises(TypeError, reduce, add, []) # arg 2 must not be empty sequence with no initial value
        self.assertRaises(TypeError, reduce, add, "") # empty string sequence
        self.assertRaises(TypeError, reduce, add, ()) # empty tuple sequence
        
        self.assertTrue(reduce(add, [], 1), 1) # arg 2 has initial value through arg 3 so no TypeError for this
        self.assertTrue(reduce(add, [], None) == None)
        self.assertRaises(TypeError, reduce, add, [])
        self.assertRaises(TypeError, reduce, add, "")
        self.assertRaises(TypeError, reduce, add, ())

    def test_apply(self):
        def foo(): return 42
        
        self.assertEqual(apply(foo), 42)
        
    def test_map(self):
        def cat(x,y):
            ret = ""
            if x != None: ret += x
            if y != None: ret += y
            return ret

        self.assertTrue(list(map(cat, ["a","b"],["c","d", "e"])) == ["ac", "bd", "e"])
        self.assertTrue(list(map(lambda x,y: x+y, [1,1],[2,2])) == [3,3])
        self.assertTrue(list([1,2,3]) == [1,2,3])
        self.assertTrue(map(None, [1,2,3], [4,5,6]) == [(1,4),(2,5),(3,6)])
        self.assertEqual(['a' + x + 'c' for x in 'b'], ['abc'])
        
    def test_range(self):
        self.assertRaisesMessage(TypeError, "range() integer end argument expected, got float.",
                            range, 2, 5.0)
        self.assertRaisesMessage(TypeError, "range() integer step argument expected, got float.",
                            range, 3, 10, 2.0)

        self.assertRaisesMessage(TypeError, "range() integer end argument expected, got float.", 
                            range, float(-2<<32))
        self.assertRaisesMessage(TypeError, "range() integer end argument expected, got float.", 
                            range, 0, float(-2<<32))
        self.assertRaisesMessage(TypeError, "range() integer start argument expected, got float.", 
                            range, float(-2<<32), 100)
        self.assertRaisesMessage(TypeError, "range() integer step argument expected, got float.", 
                            range, 0, 100, float(-2<<32))
        self.assertRaisesMessage(TypeError, "range() integer end argument expected, got float.", 
                            range, float(-2<<32), float(-2<<32), float(-2<<32))

    def test_sorted(self):
        a = [6,9,4,5,3,1,2,7,8]
        self.assertTrue(sorted(a) == [1,2,3,4,5,6,7,8,9])
        self.assertTrue(a == [6,9,4,5,3,1,2,7,8])
        self.assertTrue(sorted(a, None, None, True) == [9,8,7,6,5,4,3,2,1])

        def invcmp(a,b): return -cmp(a,b)

        self.assertTrue(sorted(list(range(10)), None, None, True) == list(range(10))[::-1])
        self.assertTrue(sorted(list(range(9,-1,-1)), None, None, False) == list(range(10)))
        self.assertTrue(sorted(list(range(10)), invcmp, None, True) == sorted(list(range(9,-1,-1)), None, None, False))
        self.assertTrue(sorted(list(range(9,-1,-1)),invcmp, None, True) == sorted(list(range(9,-1,-1)), None, None, False))

        class P:
            def __init__(self, n, s):
                self.n = n
                self.s = s

        def equal_p(a,b):      return a.n == b.n and a.s == b.s

        def key_p(a):          return a.n.lower()

        def cmp_s(a,b):        return cmp(a.s, b.s)

        def cmp_n(a,b):        return cmp(a.n, b.n)

        a = [P("John",6),P("Jack",9),P("Gary",4),P("Carl",5),P("David",3),P("Joe",1),P("Tom",2),P("Tim",7),P("Todd",8)]
        x = sorted(a, cmp_s)
        y = [P("Joe",1),P("Tom",2),P("David",3),P("Gary",4),P("Carl",5),P("John",6),P("Tim",7),P("Todd",8),P("Jack",9)]

        for i,j in zip(x,y): self.assertTrue(equal_p(i,j))

        # case sensitive compariso is the default one

        a = [P("John",6),P("jack",9),P("gary",4),P("carl",5),P("David",3),P("Joe",1),P("Tom",2),P("Tim",7),P("todd",8)]
        x = sorted(a, cmp_n)
        y = [P("David",3),P("Joe",1),P("John",6),P("Tim",7),P("Tom",2),P("carl",5),P("gary",4),P("jack",9),P("todd",8)]

        for i,j in zip(x,y): self.assertTrue(equal_p(i,j))

        # now compare using keys - case insensitive

        x = sorted(a,None,key_p)
        y = [P("carl",5),P("David",3),P("gary",4),P("jack",9),P("Joe",1),P("John",6),P("Tim",7),P("todd",8),P("Tom",2)]
        
        for i,j in zip(x,y): self.assertTrue(equal_p(i,j))

        d = {'John': 6, 'Jack': 9, 'Gary': 4, 'Carl': 5, 'David': 3, 'Joe': 1, 'Tom': 2, 'Tim': 7, 'Todd': 8}
        x = sorted([(v,k) for k,v in list(d.items())])
        self.assertTrue(x == [(1, 'Joe'), (2, 'Tom'), (3, 'David'), (4, 'Gary'), (5, 'Carl'), (6, 'John'), (7, 'Tim'), (8, 'Todd'), (9, 'Jack')])

    def test_sum(self):
        class user_object(object):
            def __add__(self, other):
                return self
            def __radd__(self, other):
                return self
            
        def gen(x):
            for a in x: yield a
            
        def sumtest(values, expected):
            for value in values, tuple(values), gen(values):
                res = sum(values)
                self.assertEqual(res, expected)
                self.assertEqual(type(res), type(expected))
        
                res = sum(values, 0)
                self.assertEqual(res, expected)
                self.assertEqual(type(res), type(expected))

        uo = user_object()
        # int + other
        sumtest([1, 1], 2)
        sumtest([2147483647, 1], 2147483648)
        sumtest([1, 1.0], 2.0)
        sumtest([1, 1], 2)
        sumtest([1, uo], uo)

        # double and other
        sumtest([1.0, 1], 2.0)
        sumtest([2147483647.0, 1], 2147483648.0)
        sumtest([1.0, 1.0], 2.0)
        sumtest([1.0, 1], 2.0)
        sumtest([1.0, uo], uo)

        # long and other
        sumtest([1, 1], 2)
        sumtest([2147483647, 1], 2147483648)
        sumtest([1, 1.0], 2.0)
        sumtest([1, 1], 2)
        sumtest([1, uo], uo)

        # corner cases
        sumtest([1, 2.0, 3], 6.0)
        sumtest([2147483647, 1, 1.0], 2147483649.0)    
        inf = 1.7976931348623157e+308*2
        sumtest([1.7976931348623157e+308, int(1.7976931348623157e+308)], inf)        
        self.assertRaises(OverflowError, sum, [1.0, 100000000<<2000])
        
    def test_unichr(self):

        #Added the following to resolve Codeplex WorkItem #3220.
        max_uni = sys.maxunicode
        self.assertTrue(max_uni==0xFFFF or max_uni==0x10FFFF)
        max_uni_plus_one = max_uni + 1
        
        huger_than_max = 100000
        max_ok_value = '\uffff'
        
        #special case for WorkItem #3220
        if max_uni==0x10FFFF:
            huger_than_max = 10000000
            max_ok_value = '\u0010FFFF' #OK representation for UCS4???
            
        self.assertRaises(ValueError, chr, -1) # arg must be in the range [0...65535] or [0...1114111] inclusive
        self.assertRaises(ValueError, chr, max_uni_plus_one)
        self.assertRaises(ValueError, chr, huger_than_max)
        self.assertTrue(chr(0) == '\x00')
        self.assertTrue(chr(max_uni) == max_ok_value)

    def test_max_min(self):
        self.assertTrue(max([1,2,3]) == 3)
        self.assertTrue(max((1,2,3)) == 3)
        self.assertTrue(max(1,2,3) == 3)
        
        self.assertTrue(min([1,2,3]) == 1)
        self.assertTrue(min((1,2,3)) == 1)
        self.assertTrue(min(1,2,3) == 1)
        
        self.assertEqual(max((1,2), None), (1, 2))
        self.assertEqual(min((1,2), None), None)

    def test_abs(self):
        self.assertRaises(TypeError,abs,None)

        #long integer passed to abs
        self.assertEqual(22, abs(22))
        self.assertEqual(22, abs(-22))

        #bool passed to abs
        self.assertEqual(1, abs(True))
        self.assertEqual(0, abs(False))

        #__abs__ defined on user type
        class myclass:
            def __abs__(self):
                return "myabs"
        c = myclass()
        self.assertEqual("myabs", abs(c))
        
    def test_coerce(self):
        self.assertEqual(coerce(None, None), (None, None))
        self.assertRaises(TypeError, coerce, None, 1)
        self.assertRaises(TypeError, coerce, 1, None)
        
        class x(object):
            def __init__(self, value):
                self.value = value
            def __int__(self):
                return self.value
            def __coerce__(self, other):
                return self, x(other)
            def __eq__(self, other):
                return self.value == other.value
            def __repr__(self):
                return 'x(' + repr(self.value) + ')'

        for value in (x(42), 42., 42):
            self.assertEqual(int.__coerce__(0, value), NotImplemented)
            l, r = coerce(0, value)
            self.assertEqual((r, l), (value, type(value)(0)))
            self.assertEqual((type(l), type(r)), (type(value), type(value)))
    
    def test_zip(self):
        def foo(): yield 2

        def bar():
            yield 2
            yield 3

        self.assertEqual(list(zip(foo())), [(2,)])
        self.assertEqual(list(zip(foo(), foo())), [(2,2)])
        self.assertEqual(list(zip(foo(), foo(), foo())), [(2,2,2)])

        self.assertEqual(list(zip(bar(), foo())), [(2,2)])
        self.assertEqual(list(zip(foo(), bar())), [(2,2)])
        
        # test passing the same object for multiple iterables
        self.assertEqual(list(zip(*[iter([])])), [])
        self.assertEqual(list(zip(*[iter([])] * 2)), [])
        self.assertEqual(list(zip(*[list(range(3))] * 2)), [(0, 0), (1, 1), (2, 2)])
        self.assertEqual(list(zip(*[iter(["0", "1"])] * 2)), [('0', '1')])
        self.assertEqual(list(zip(*[iter(["0", "1", "2"])] * 3)), [('0', '1', '2')])
        self.assertEqual(list(zip(*'abc')), [('a', 'b', 'c')])

    def test_dir(self):
        local_var = 10
        self.assertEqual(dir(), ['local_var', 'self'])
        
        def f():
            local_var = 10
            self.assertEqual(dir(*()), ['local_var', 'self'])
        f()
        
        def f():
            local_var = 10
            self.assertEqual(dir(**{}), ['local_var', 'self'])
        f()

        def f():
            local_var = 10
            self.assertEqual(dir(*(), **{}), ['local_var', 'self'])
        f()

        class A(object):
            def __dir__(self):
                    return ['foo']
            def __init__(self):
                    self.abc = 3
        
        self.assertEqual(dir(A()), ['foo'])

        class C:
            a = 1
        
        class D(object, C):
            b = 2
        
        self.assertTrue('a' in dir(D()))
        self.assertTrue('b' in dir(D()))
        self.assertTrue('__hash__' in dir(D()))
        
        if is_cli:
            import clr
            if is_netcoreapp:
                clr.AddReference("System.Linq.Expressions")
            else:
                clr.AddReference("System.Core")
            
            from System.Dynamic import ExpandoObject
            eo = ExpandoObject()
            eo.bill = 5
            self.assertTrue('bill' in dir(eo))

        
    def test_ord(self):
        # ord of extensible string
        class foo(str): pass
        
        self.assertEqual(ord(foo('A')), 65)
        
    def test_top_level_dir(self):
        self.assertTrue("__name__" in top_level_dir)
        self.assertTrue("__builtins__" in top_level_dir)

    def test_eval(self):
        d1 = { 'y' : 3 }
        d2 = { 'x' : 4 }

        self.assertEqual(eval("x + y", None, d1), 13)
        self.assertEqual(eval("x + y", None, None), 30)
        self.assertEqual(eval("x + y", None), 30)
        self.assertEqual(eval("x + y", None, d2), 24)

        self.assertRaises(NameError, eval, "x + y", d1)
        self.assertRaises(NameError, eval, "x + y", d1, None)

        try:
            eval('1+')
            AssertUnreachable()
        except Exception as exp:
            pass
        else:
            AssertUnreachable()

        # gh1636 - don't use self.assertRaises since it hides the error
        zzz = 1
        try:
            eval("zzz", {})
            AssertUnreachable()
        except NameError:
            pass
        try:
            eval("zzz", {}, None)
            AssertUnreachable()
        except NameError:
            pass

        # test one of each expression in all sorts of combinations
        foo = 1
        bar = 2
        def f(): return 42
        exprs = ['foo', 
                '23', 
                '$inp and $inp', 
                '$inp or $inp', 
                '`42`', 
                '$inp + $inp', 
                'f()', 
                'lambda :42',  
                '$inp if $inp else $inp', 
                '[$inp]', 
                '{$inp:$inp}', 
                '($inp).__class__', 
                '{$inp}', 
                '($inp, )', 
                '($inp)', 
                '[x for x in (2, 3, 4)]']
        
        def process(depth):
            if(depth > 2):
                yield '42'
            else:
                for x in exprs:
                    processors = [process(depth + 1)] * x.count('$inp')
                    if processors:
                        while 1:
                            try:
                                newx = x
                                for i in range(len(processors)):
                                    new = next(processors[i])
                                    newx = newx.replace('$inp', new, 1)
                                yield newx
                            except StopIteration:
                                break
                        
                    else:
                        yield x
                
        for x in process(0):
            try:
                print(eval(x))
            except SyntaxError: pass            
            except TypeError: pass

    def test_len(self):
        # old-style classes throw AttributeError, new-style classes throw
        # TypeError
        self.assertRaises(TypeError, len, 2)
        class foo: pass
        
        self.assertRaises(AttributeError, len, foo())
        
        class foo(object): pass
        
        self.assertRaises(TypeError, len, foo())

    def test_int_ctor(self):
        self.assertEqual(int('0x10', 16), 16)
        self.assertEqual(int('0X10', 16), 16)
        self.assertEqual(int('0x10', 16), 16)
        self.assertEqual(int('0X10', 16), 16)
    
    def test_type(self):
        self.assertEqual(len(type.__bases__), 1)
        self.assertEqual(type.__bases__[0], object)
        
    def test_globals(self):
        self.assertTrue("_" not in globals())
        self.assertEqual(list(globals().keys()).count("_"), 0)

    def test_vars(self):
        """vars should look for user defined __dict__ value and directly return the provided value"""
        
        class foo(object):
            def getDict(self):
                    return {'a':2}
            __dict__ = property(fget=getDict)
        
        self.assertEqual(vars(foo()), {'a':2})
        
        class foo(object):
            def __getattribute__(self, name):
                if name == "__dict__":
                        return {'a':2}
                return object.__getattribute__(self, name)

        self.assertEqual(vars(foo()), {'a':2})
        
        class foo(object):
            def getDict(self):
                    return 'abc'
            __dict__ = property(fget=getDict)
        
        self.assertEqual(vars(foo()), 'abc')
        
        class foo(object):
            def __getattribute__(self, name):
                if name == "__dict__":
                        return 'abc'
                return object.__getattribute__(self, name)

        self.assertEqual(vars(foo()), 'abc')

        def f():
            local_var = 10
            self.assertEqual(vars(*()), {'self': self, 'local_var': 10})
        f()

        def f():
            local_var = 10
            self.assertEqual(vars(**{}), {'self': self, 'local_var': 10})
        f()

        def f():
            local_var = 10
            self.assertEqual(vars(*(), **{}), {'self': self, 'local_var': 10})
        f()

    def test_compile(self):
        for x in ['exec', 'eval', 'single']:
            c = compile('2', 'foo', x)
            self.assertEqual(c.co_filename, 'foo')
            
        class mystdout(object):
            def __init__(self):
                self.data = []
            def write(self, data):
                self.data.append(data)

        out = mystdout()
        sys.stdout = out
        try:
            c = compile('2', 'test', 'single')
            exec(c)
            self.assertEqual(out.data, ['2', '\n'])
        finally:
            sys.stdout = sys.__stdout__
            
        for code in ["abc" + chr(0) + "def", chr(0) + "def", "def" + chr(0)]:
            self.assertRaises(TypeError, compile, code, 'f', 'exec')

    def test_str_none(self):
        class foo(object):
            def __str__(self):
                    return None
        
        self.assertEqual(foo().__str__(), None)
        self.assertRaises(TypeError, str, foo())

    def test_not_in_globals(self):
        self.assertRaises(NameError, lambda: __dict__)
        self.assertRaises(NameError, lambda: __module__)
        self.assertRaises(NameError, lambda: __class__)
        self.assertRaises(NameError, lambda: __init__)

    # Regress bug 319126: __int__ on long should return long, not overflow
    def test_long_int(self):
        l=int(1.23e300)
        i = l.__int__()
        self.assertTrue(type(l) == type(i))
        self.assertTrue(i == l)
        
    def test_round(self):
        self.assertEqual(round(number=3.4), 3.0)
        self.assertEqual(round(number=3.125, ndigits=3), 3.125)
        self.assertEqual(round(number=3.125, ndigits=0), 3)

    def test_cp16000(self):
        class K(object):
            FOO = 39
            def fooFunc():
                return K.FOO
            def memberFunc(self):
                return K.FOO * 3.14


        temp_list = [   None, str, int, int, K,
                        "", "abc", "abc", 34, 1111111111111, 3.14, K(), K.FOO,
                        id, hex, K.fooFunc, K.memberFunc, K().memberFunc,
                    ]

        if is_cli:
            import System
            temp_list += [  System.Exception, System.InvalidOperationException(),
                            System.Single, System.UInt16(5), System.Version(0, 0)]

        for x in temp_list:
            self.assertTrue(type(id(x)) in [int, int], 
                str(type(id(x))))


    def test_locals_contains(self):
        global locals_globals
        locals_globals = 2
        def func():
            self.assertTrue(not 'locals_globals' in locals())
        func()

# def in_main():
#     self.assertTrue(not 'locals_globals' in locals())
#     self.assertTrue(not 'locals_globals' in globals())
    
#     def in_main_sub1():
#         self.assertTrue(not 'locals_globals' in locals())
#         self.assertTrue(not 'locals_globals' in globals())
    
#     def in_main_sub2():
#         global local_globals
#         self.assertTrue(not 'locals_globals' in locals())
#         self.assertTrue('locals_globals' in globals())
        
#         def in_main_sub3():
#             local_globals = 42
#             self.assertTrue(not 'locals_globals' in locals())
#             self.assertTrue('locals_globals' in globals())
        
#         in_main_sub3()
    
#     in_main_sub1()
#     return in_main_sub2


    def test_error_messages(self):
        self.assertRaisesMessages(TypeError, "join() takes exactly 1 argument (2 given)", "join() takes exactly one argument (2 given)", "".join, ["a", "b"], "c")
    
    def test_enumerate(self):
        class MyIndex(object):
            def __init__(self, value):
                self.value = value
            def __index__(self):
                return self.value

        for value_maker in MyIndex, lambda x: x:
            self.assertEqual([(10, 2), (11, 3), (12, 4)], list(enumerate([2,3,4], value_maker(10))))
            self.assertEqual([(10, 2), (11, 3), (12, 4)], list(enumerate([2,3,4], start=value_maker(10))))
            self.assertEqual([(2147483647, 2), (2147483648, 3), (2147483649, 4)], list(enumerate([2,3,4], value_maker(int((1<<31) - 1)))))
            self.assertEqual([(2147483648, 2), (2147483649, 3), (2147483650, 4)], list(enumerate([2,3,4], value_maker(1<<31))))
    
# temp_func = in_main()
# locals_globals = 7
# temp_func()

run_test(__name__)

