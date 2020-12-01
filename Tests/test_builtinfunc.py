# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import math
import sys
import unittest

#-----------------------------------------------------------------------------------
#These have to be run first: importing iptest.assert_util masked a bug. Just make sure
#these do not throw
for stuff in [bool, True, False]:
    temp = dir(stuff)

items = globals().items() #4716

class BuiltinsTest1(unittest.TestCase):
    def cp946(self):
        builtins = __builtins__
        if type(builtins) is type(sys):
            builtins = builtins.__dict__

        self.assertIn('hasattr', builtins, 'hasattr should be in __builtins__')
        self.assertNotIn('HasAttr', builtins, 'HasAttr should not be in __builtins__')

    def test_00_no_clr_import(self):
        self.cp946()

    @unittest.skipUnless(sys.implementation.name=='ironpython', 'IronPython specific test')
    def test_01_with_clr_import(self):
        import clr
        self.cp946()

top_level_dir = dir()

x = 10
y = 20

import collections
from functools import cmp_to_key, reduce
from operator import neg
import random

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, long, run_test

if not is_cli:
    long = int

def callable(x):
    return isinstance(x, collections.Callable)

def run_test_not_in_globals(self):
    self.assertRaises(NameError, lambda: __dict__)
    self.assertRaises(NameError, lambda: __module__)
    self.assertRaises(NameError, lambda: __class__)
    self.assertRaises(NameError, lambda: __init__)

class BuiltinsTest2(IronPythonTestCase):
    def test_name_error(self):
        self.assertRaises(NameError, lambda: __new__)

    def test_callable(self):
        class C: x=1

        self.assertTrue(callable(min))
        self.assertTrue(not callable("a"))
        self.assertTrue(callable(callable))
        self.assertTrue(callable(lambda x, y: x + y))
        self.assertTrue(callable(C))
        self.assertTrue(not callable(C.x))
        self.assertTrue(not callable(__builtins__))

    def test_callable_oldclass(self):
        # for class instances, callable related to whether the __call__ attribute is defined.
        # This can be mutated at runtime.
        class Dold:
            pass
        d=Dold()
        #
        self.assertEqual(callable(d), False)
        #
        d.__call__ = None # This defines the attr, even though it's None
        self.assertEqual(callable(d), False) # True for oldinstance, False for new classes.
        #
        del (d.__call__) # now remove the attr, no longer callable
        self.assertEqual(callable(d), False)

    def test_callable_newclass(self):
        class D(object):
            pass
        self.assertEqual(callable(D), True)
        d=D()
        self.assertEqual(callable(d), False)
        #
        # New class with a __call__ defined is callable()
        class D2(object):
            def __call__(self): pass
        d2=D2()
        self.assertEqual(callable(d2), True)
        # Inherit callable
        class D3(D2):
            pass
        d3=D3()
        self.assertEqual(callable(d3), True)

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

    def test_map(self):
        def cat(x,y):
            ret = ""
            if x != None: ret += x
            if y != None: ret += y
            return ret

        self.assertEqual(list(map(cat, ["a","b"], ["c","d", "e"])), ["ac", "bd"])
        self.assertEqual(list(map(lambda x,y: x+y, [1,1],[2,2])), [3,3])
        self.assertEqual(list(map(lambda x: x, [1,2,3])), [1,2,3])
        self.assertEqual(list(map(lambda x, y: (x, y), [1,2,3], [4,5,6])), [(1,4),(2,5),(3,6)])
        self.assertEqual(list(map(lambda x:'a' + x + 'c', 'b')), ['abc'])

    def test_range(self):
        if is_cli:
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, 2, 5.0)
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, 3, 10, 2.0)
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, float(-2<<32))
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, 0, float(-2<<32))
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, float(-2<<32), 100)
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, 0, 100, float(-2<<32))
            self.assertRaisesMessage(TypeError, "expected integer value, got float",
                            range, float(-2<<32), float(-2<<32), float(-2<<32))
        else:
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, 2, 5.0)
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, 3, 10, 2.0)
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, float(-2<<32))
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, 0, float(-2<<32))
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, float(-2<<32), 100)
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, 0, 100, float(-2<<32))
            self.assertRaisesMessage(TypeError, "'float' object cannot be interpreted as an integer",
                            range, float(-2<<32), float(-2<<32), float(-2<<32))


    def test_sorted(self):
        a = [6,9,4,5,3,1,2,7,8]
        self.assertTrue(sorted(a) == [1,2,3,4,5,6,7,8,9])
        self.assertTrue(a == [6,9,4,5,3,1,2,7,8])
        self.assertTrue(sorted(a, reverse=True) == [9,8,7,6,5,4,3,2,1])

        def cmp(a,b): return (a > b) - (a < b)

        def invcmp(a,b): return -cmp(a,b)

        self.assertTrue(sorted(range(10), reverse=True) == list(range(10))[::-1])
        self.assertTrue(sorted(range(9,-1,-1)) == list(range(10)))
        self.assertTrue(sorted(range(10), key=cmp_to_key(invcmp), reverse=True) == sorted(range(9,-1,-1)))
        self.assertTrue(sorted(range(9,-1,-1), key=cmp_to_key(invcmp), reverse=True) == sorted(range(9,-1,-1)))

        class P:
            def __init__(self, n, s):
                self.n = n
                self.s = s

        def equal_p(a,b):      return a.n == b.n and a.s == b.s

        def key_p(a):          return a.n.lower()

        def cmp_s(a,b):        return cmp(a.s, b.s)

        def cmp_n(a,b):        return cmp(a.n, b.n)

        a = [P("John",6),P("Jack",9),P("Gary",4),P("Carl",5),P("David",3),P("Joe",1),P("Tom",2),P("Tim",7),P("Todd",8)]
        x = sorted(a, key=cmp_to_key(cmp_s))
        y = [P("Joe",1),P("Tom",2),P("David",3),P("Gary",4),P("Carl",5),P("John",6),P("Tim",7),P("Todd",8),P("Jack",9)]

        for i,j in zip(x,y): self.assertTrue(equal_p(i,j))

        # case sensitive compariso is the default one

        a = [P("John",6),P("jack",9),P("gary",4),P("carl",5),P("David",3),P("Joe",1),P("Tom",2),P("Tim",7),P("todd",8)]
        x = sorted(a, key=cmp_to_key(cmp_n))
        y = [P("David",3),P("Joe",1),P("John",6),P("Tim",7),P("Tom",2),P("carl",5),P("gary",4),P("jack",9),P("todd",8)]

        for i,j in zip(x,y): self.assertTrue(equal_p(i,j))

        # now compare using keys - case insensitive

        x = sorted(a, key=key_p)
        y = [P("carl",5),P("David",3),P("gary",4),P("jack",9),P("Joe",1),P("John",6),P("Tim",7),P("todd",8),P("Tom",2)]

        for i,j in zip(x,y): self.assertTrue(equal_p(i,j))

        d = {'John': 6, 'Jack': 9, 'Gary': 4, 'Carl': 5, 'David': 3, 'Joe': 1, 'Tom': 2, 'Tim': 7, 'Todd': 8}
        x = sorted([(v,k) for k,v in d.items()])
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
        sumtest([2147483647, 1], long(2147483648))
        sumtest([1, 1.0], 2.0)
        sumtest([1, long(1)], long(2))
        sumtest([1, uo], uo)

        # double and other
        sumtest([1.0, 1], 2.0)
        sumtest([2147483647.0, 1], 2147483648.0)
        sumtest([1.0, 1.0], 2.0)
        sumtest([1.0, long(1)], 2.0)
        sumtest([1.0, uo], uo)

        # long and other
        sumtest([long(1), 1], long(2))
        sumtest([long(2147483647), 1], long(2147483648))
        sumtest([long(1), 1.0], 2.0)
        sumtest([long(1), long(1)], long(2))
        sumtest([long(1), uo], uo)

        # corner cases
        sumtest([long(1), 2.0, 3], 6.0)
        sumtest([2147483647, 1, 1.0], 2147483649.0)
        inf = 1.7976931348623157e+308*2
        sumtest([1.7976931348623157e+308, long(1.7976931348623157e+308)], inf)
        self.assertRaises(OverflowError, sum, [1.0, 100000000<<2000])

    def test_unichr(self):

        #Added the following to resolve Codeplex WorkItem #3220.
        max_uni = sys.maxunicode
        self.assertTrue(max_uni==0xFFFF or max_uni==0x10FFFF)
        max_uni_plus_one = max_uni + 1

        huger_than_max = 100000
        max_ok_value = u'\uffff'

        #special case for WorkItem #3220
        if max_uni==0x10FFFF:
            huger_than_max = 10000000
            max_ok_value = u'\U0010FFFF'

        unichr = chr

        self.assertRaises(ValueError, unichr, -1) # arg must be in the range [0...65535] or [0...1114111] inclusive
        self.assertRaises(ValueError, unichr, max_uni_plus_one)
        self.assertRaises(ValueError, unichr, huger_than_max)
        self.assertTrue(unichr(0) == '\x00')
        self.assertTrue(unichr(max_uni) == max_ok_value)

    def test_max(self):
        self.assertEqual(max('123123'), '3')
        self.assertTrue(max([1,2,3]) == 3)
        self.assertTrue(max((1,2,3)) == 3)
        self.assertTrue(max(1,2,3) == 3)
        self.assertTrue(max((1, 2, 3, 1, 2, 3)) == 3)
        self.assertEqual(max([1, 2, 3, 1, 2, 3]), 3)
        self.assertTrue(max(1,2,3) == 3)
        self.assertTrue(max([], default=1) == 1)
        self.assertTrue(max((), default=1) == 1)
        self.assertTrue(max([], default=None) is None)

        self.assertEqual(max(1, 2, 3.0), 3.0)
        self.assertEqual(max(1, 2.0, 3), 3)
        self.assertEqual(max(1.0, 2, 3), 3)

        self.assertRaises(TypeError, max)
        self.assertRaises(TypeError, max, 42)
        self.assertRaises(ValueError, max, ())
        self.assertRaises(TypeError, max, (1, 2), None)
        class BadSeq:
            def __getitem__(self, index):
                raise ValueError
        self.assertRaises(ValueError, max, BadSeq())

        for stmt in (
            "max(key=int)",                 # no args
            "max(default=None)",
            "max(1, 2, default=None)",      # require container for default
            "max(default=None, key=int)",
            "max(1, key=int)",              # single arg not iterable
            "max(1, 2, keystone=int)",      # wrong keyword
            "max(1, 2, key=int, abc=int)",  # two many keywords
            "max(1, 2, key=1)",             # keyfunc is not callable
            ):
            try:
                exec(stmt, globals())
            except TypeError:
                pass
            else:
                self.fail(stmt)

        self.assertEqual(max((1,), key=neg), 1)     # one elem iterable
        self.assertEqual(max((1,2), key=neg), 1)    # two elem iterable
        self.assertEqual(max(1, 2, key=neg), 1)     # two elems
        self.assertEqual(max((), default=None), None)    # zero elem iterable
        self.assertEqual(min((1,), default=None), 1)     # one elem iterable
        self.assertEqual(min((1,2), default=None), 1)    # two elem iterable
        self.assertEqual(max((), default=1, key=neg), 1)
        self.assertEqual(max((1, 2), default=3, key=neg), 1)

        data = [random.randrange(200) for i in range(100)]
        keys = dict((elem, random.randrange(50)) for elem in data)
        f = keys.__getitem__
        self.assertEqual(max(data, key=f), sorted(reversed(data), key=f)[-1])


    def test_min(self):
        self.assertEqual(min('123123'), '1')
        self.assertEqual(min(1, 2, 3), 1)
        self.assertEqual(min((1, 2, 3, 1, 2, 3)), 1)
        self.assertEqual(min([1, 2, 3, 1, 2, 3]), 1)

        self.assertEqual(min(1, 2, 3.0), 1)
        self.assertEqual(min(1, 2.0, 3), 1)
        self.assertEqual(min(1.0, 2, 3), 1.0)

        self.assertRaises(TypeError, min)
        self.assertRaises(TypeError, min, 42)
        self.assertRaises(ValueError, min, ())
        class BadSeq:
            def __getitem__(self, index):
                raise ValueError
        self.assertRaises(ValueError, min, BadSeq())

        for stmt in (
            "min(key=int)",                 # no args
            "min(default=None)",
            "min(1, 2, default=None)",      # require container for default
            "min(default=None, key=int)",
            "min(1, key=int)",              # single arg not iterable
            "min(1, 2, keystone=int)",      # wrong keyword
            "min(1, 2, key=int, abc=int)",  # two many keywords
            "min(1, 2, key=1)",             # keyfunc is not callable
            ):
            try:
                exec(stmt, globals())
            except TypeError:
                pass
            else:
                self.fail(stmt)

        self.assertEqual(min((1,), key=neg), 1)     # one elem iterable
        self.assertEqual(min((1,2), key=neg), 2)    # two elem iterable
        self.assertEqual(min(1, 2, key=neg), 2)     # two elems

        self.assertEqual(min((), default=None), None)    # zero elem iterable
        self.assertEqual(min((1,), default=None), 1)     # one elem iterable
        self.assertEqual(min((1,2), default=None), 1)    # two elem iterable

        self.assertEqual(min((), default=1, key=neg), 1)
        self.assertEqual(min((1, 2), default=1, key=neg), 2)

        data = [random.randrange(200) for i in range(100)]
        keys = dict((elem, random.randrange(50)) for elem in data)
        f = keys.__getitem__
        self.assertEqual(min(data, key=f), sorted(data, key=f)[0])

    def test_abs(self):
        self.assertRaises(TypeError,abs,None)

        #long integer passed to abs
        self.assertEqual(long(22), abs(long(22)))
        self.assertEqual(long(22), abs(-long(22)))

        #bool passed to abs
        self.assertEqual(1, abs(True))
        self.assertEqual(0, abs(False))

        #__abs__ defined on user type
        class myclass:
            def __abs__(self):
                return "myabs"
        c = myclass()
        self.assertEqual("myabs", abs(c))

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
        self.assertEqual(list(zip(*[range(3)] * 2)), [(0, 0), (1, 1), (2, 2)])
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

        class D(C, object):
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
            self.fail("Unreachable code reached")
        except Exception as exp:
            pass
        else:
            self.fail("Unreachable code reached")

        # gh1636 - don't use self.assertRaises since it hides the error
        zzz = 1
        try:
            eval("zzz", {})
            self.fail("Unreachable code reached")
        except NameError:
            pass
        try:
            eval("zzz", {}, None)
            self.fail("Unreachable code reached")
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
                eval(x)
            except SyntaxError: pass
            except TypeError: pass

    def test_len(self):
        # old-style classes throw AttributeError, new-style classes throw
        # TypeError
        self.assertRaises(TypeError, len, 2)
        class foo: pass

        self.assertRaises(TypeError, len, foo())

        class foo(object): pass

        self.assertRaises(TypeError, len, foo())

        # ensure len doesn't fail when __len__ returns a long
        class LongLength(object):
            def __len__(self): return 11111111111111111

        self.assertEqual(len(LongLength()), 11111111111111111)

    def test_int_ctor(self):
        self.assertEqual(int('0x10', 16), 16)
        self.assertEqual(int('0X10', 16), 16)
        self.assertEqual(long('0x10', 16), long(16))
        self.assertEqual(long('0X10', 16), long(16))

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
        run_test_not_in_globals(self)

    # Regress bug 319126: __int__ on long should return long, not overflow
    def test_long_int(self):
        l=long(1.23e300)
        i = l.__int__()
        self.assertTrue(type(l) == type(i))
        self.assertTrue(i == l)

    class Roundable:
        def __round__(self, ndigits):
            if ndigits == 3: return "circle"
            else: return "ellipse"

    class ParameterlessRoundable:
        def __round__(self):
            return "sphere"

    class NotRoundable:
        def dummy(self):
            pass

    class IntIndex:
        def __init__(self, index):
            self.index = index

        def __index__(self):
            return self.index
    
    class LongIndex:
        def __init__(self, index):
            self.index = index

        def __index__(self):
            return long(self.index)

    class NonIntegralIndex:
        def __index__(self):
            return "bad"

    def assertEqualAndCheckType(self, actual, expected, expectedType):
        self.assertEqual(actual, expected)
        self.assertIsInstance(actual, expectedType, msg="Type: {0}".format(type(actual)))

    def test_round(self):
        self.assertEqual(round(number=3.4), 3.0)
        self.assertEqual(round(number=3.125, ndigits=3), 3.125)
        self.assertEqual(round(number=3.125, ndigits=0), 3)

        # rounds to even as of Python 3.0
        self.assertEqual(round(number=2.5), 2)
        self.assertEqual(round(number=3.5), 4)

        self.assertEqual(round(number=2.5, ndigits=0), 2)
        self.assertEqual(round(number=3.5, ndigits=0), 4)

        self.assertEqual(round(number=25.0, ndigits=-1), 20)
        self.assertEqual(round(number=35.0, ndigits=-1), 40)

        try:
            round(number=2.5, ndigits=1.1)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("'float' object cannot be interpreted as an integer", str(err))
        else:
            self.fail("Unreachable code reached")

        # type implements __round__
        # correct number of arguments
        roundable = self.Roundable()
        self.assertEqual(round(roundable, 3), "circle")
        self.assertEqual(round(number=roundable, ndigits=3), "circle")
        self.assertEqual(round(roundable, 2), "ellipse")
        self.assertEqual(round(number=roundable, ndigits=2), "ellipse")
        self.assertEqual(round(number=roundable, ndigits="blah"), "ellipse")
        self.assertEqual(round(self.ParameterlessRoundable()), "sphere")

        # too few arguments
        with self.assertRaisesMessage(TypeError, "__round__() takes exactly 2 arguments (1 given)" if is_cli else "__round__() missing 1 required positional argument: 'ndigits'"):
            round(roundable)

        # too many arguments
        try:
            round(roundable, 1, 2)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("round() takes at most 2 arguments (3 given)", str(err))
        else:
            self.fail("Unreachable code reached")

        # type does not implement __round__
        # too few arguments
        try:
            round(self.NotRoundable())
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("type NotRoundable doesn't define __round__ method", str(err))
        else:
            self.fail("Unreachable code reached")

        # correct number of arguments
        try:
            round(self.NotRoundable(), 1)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("type NotRoundable doesn't define __round__ method", str(err))
        else:
            self.fail("Unreachable code reached")

        try:
            round(number=self.NotRoundable(), ndigits=1)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("type NotRoundable doesn't define __round__ method", str(err))
        else:
            self.fail("Unreachable code reached")

        # too many arguments
        try:
            round(self.NotRoundable(), 1, 2)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("round() takes at most 2 arguments (3 given)", str(err))
        else:
            self.fail("Unreachable code reached")
        
        self.assertEqualAndCheckType(round(3), 3, int)
        self.assertEqualAndCheckType(round(3, 0), 3, int)
        self.assertEqualAndCheckType(round(3, 1), 3, int)

        self.assertEqualAndCheckType(round(24, -1), 20, int)
        self.assertEqualAndCheckType(round(25, -1), 20, int)
        self.assertEqualAndCheckType(round(26, -1), 30, int)

        self.assertEqualAndCheckType(round(249, -2), 200, int)
        self.assertEqualAndCheckType(round(250, -2), 200, int)
        self.assertEqualAndCheckType(round(251, -2), 300, int)

        self.assertEqualAndCheckType(round(2147483647, -3), 2147484000, long)

        self.assertEqualAndCheckType(
            round(111111111111111111111111111111, 111111111111111111111111111111), 
            111111111111111111111111111111, 
            long)

        self.assertEqualAndCheckType(round(111111111111111111111111111111, 0), 111111111111111111111111111111, long)
        self.assertEqualAndCheckType(round(111111111111111111111111111111, -2), 111111111111111111111111111100, long)

        self.assertEqualAndCheckType(round(111111111111111111111111111124, -1), 111111111111111111111111111120, long)
        self.assertEqualAndCheckType(round(111111111111111111111111111125, -1), 111111111111111111111111111120, long)
        self.assertEqualAndCheckType(round(111111111111111111111111111126, -1), 111111111111111111111111111130, long)

        self.assertEqualAndCheckType(round(111111111111111111111111111249, -2), 111111111111111111111111111200, long)
        self.assertEqualAndCheckType(round(111111111111111111111111111250, -2), 111111111111111111111111111200, long)
        self.assertEqualAndCheckType(round(111111111111111111111111111251, -2), 111111111111111111111111111300, long)

        if is_cli:
            self.assertEqualAndCheckType(round(111111111111111111111111111111, -111111111111111111111111111111), 0, long)
            self.assertEqualAndCheckType(round(-111111111111111111111111111111, -111111111111111111111111111111), 0, long)

        try:
            round(number=2, ndigits=1.1)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("'float' object cannot be interpreted as an integer", str(err))
        else:
            self.fail("Unreachable code reached")

        try:
            round(number=111111111111111111111111111111, ndigits=1.1)
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("'float' object cannot be interpreted as an integer", str(err))
        else:
            self.fail("Unreachable code reached")

        try:
            round(float('nan'))
            self.fail("Unreachable code reached")
        except ValueError as err:
            self.assertEqual("cannot convert float NaN to integer", str(err))
        else:
            self.fail("Unreachable code reached")

        try:
            round(float('inf'))
            self.fail("Unreachable code reached")
        except OverflowError as err:
            self.assertEqual("cannot convert float infinity to integer", str(err))
        else:
            self.fail("Unreachable code reached")

        try:
            round(float('-inf'))
            self.fail("Unreachable code reached")
        except OverflowError as err:
            self.assertEqual("cannot convert float infinity to integer", str(err))
        else:
            self.fail("Unreachable code reached")

        try:
            round(sys.float_info.max, -307)
            self.fail("Unreachable code reached")
        except OverflowError as err:
            self.assertEqual("rounded value too large to represent", str(err))
        else:
            self.fail("Unreachable code reached")

        actual = round(float('inf'), 1)
        self.assertTrue(math.isinf(actual) and actual > 0)

        actual = round(float('-inf'), 1)
        self.assertTrue(math.isinf(actual) and actual < 0)

        actual = round(float('inf'), -1)
        self.assertTrue(math.isinf(actual) and actual > 0)

        actual = round(float('-inf'), -1)
        self.assertTrue(math.isinf(actual) and actual < 0)

        actual = round(float('nan'), 1)
        self.assertTrue(math.isnan(actual))

        actual = round(float('nan'), -1)
        self.assertTrue(math.isnan(actual))
        
        actual = round(float('inf'), 354250895282439122322875506826024599142533926918074193061745122574500)
        self.assertTrue(math.isinf(actual) and actual > 0)

        actual = round(float('-inf'), 354250895282439122322875506826024599142533926918074193061745122574500)
        self.assertTrue(math.isinf(actual) and actual < 0)

        actual = round(float('inf'), -354250895282439122322875506826024599142533926918074193061745122574500)
        self.assertTrue(math.isinf(actual) and actual > 0)

        actual = round(float('-inf'), -354250895282439122322875506826024599142533926918074193061745122574500)
        self.assertTrue(math.isinf(actual) and actual < 0)

        actual = round(float('nan'), 354250895282439122322875506826024599142533926918074193061745122574500)
        self.assertTrue(math.isnan(actual))

        actual = round(float('nan'), -354250895282439122322875506826024599142533926918074193061745122574500)
        self.assertTrue(math.isnan(actual))

        self.assertEqual(round(3.55, self.IntIndex(1)), 3.6 if is_cli else 3.5)
        self.assertEqual(round(35, self.IntIndex(-1)), 40)
        self.assertEqual(round(35.555, self.LongIndex(2)), 35.56 if is_cli else 35.55)
        self.assertEqual(round(355, self.LongIndex(-2)), 400)

        try:
            round(5.5, self.NonIntegralIndex())
            self.fail("Unreachable code reached")
        except TypeError as err:
            self.assertEqual("__index__ returned non-int (type str)", str(err))
        else:
            self.fail("Unreachable code reached")

    def test_cp16000(self):
        class K(object):
            FOO = 39
            def fooFunc():
                return K.FOO
            def memberFunc(self):
                return K.FOO * 3.14


        temp_list = [   None, str, int, long, K,
                        "", "abc", u"abc", 34, long(1111111111111), 3.14, K(), K.FOO,
                        id, hex, K.fooFunc, K.memberFunc, K().memberFunc,
                    ]

        if is_cli:
            import System
            temp_list += [  System.Exception, System.InvalidOperationException(),
                            System.Single, System.UInt16(5), System.Version(0, 0)]

        for x in temp_list:
            self.assertTrue(type(id(x)) in [int, long],
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
        if is_cli:
            self.assertRaisesMessage(TypeError, "join() takes exactly 1 argument (2 given)", "".join, ["a", "b"], "c")
            self.assertRaisesMessage(TypeError, "'NoneType' object is not iterable", "".join, None)
        else:
            self.assertRaisesMessage(TypeError, "join() takes exactly one argument (2 given)", "".join, ["a", "b"], "c")
            self.assertRaisesMessage(TypeError, "can only join an iterable", "".join, None)

        self.assertRaisesMessage(TypeError, "sequence item 0: expected str instance, NoneType found", "".join, (None,))
        self.assertRaisesMessage(TypeError, "sequence item 0: expected str instance, NoneType found", "".join, [None])
        self.assertRaisesMessage(TypeError, "sequence item 1: expected str instance, NoneType found", "".join, ("", None))
        self.assertRaisesMessage(TypeError, "sequence item 1: expected str instance, NoneType found", "".join, ["", None])

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

    def test_hex_long(self):
        """https://github.com/IronLanguages/ironpython3/pull/973"""
        self.assertEqual(hex(0x800000000), '0x800000000')

# temp_func = in_main()
# locals_globals = 7
# temp_func()

run_test(__name__)
