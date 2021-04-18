# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, is_cli, run_test

# ref: http://docs.python.org/ref/metaclasses.html

class Old:
    def method(self): return 10

class New(object):
    def method(self): return 10

def g_f_modify(new_base=None, new_name=None):
    def f_modify(name, bases, dict):
        if new_name:
            name = new_name
        if new_base:
            bases = new_base + bases

        dict['version'] = 2.4
        return type(name, bases, dict)

    return f_modify

def g_c_modify(new_base=None, new_name=None):
    class c_modify(type):
        def __new__(cls, name, bases, dict):
            if new_name:
                name = new_name
            if new_base:
                bases = new_base + bases

            dict['version'] = 2.4
            return super(c_modify, cls).__new__(cls, name, bases, dict)

    return c_modify

class dash_attributes(type):
    def __new__(metaclass, name, bases, dict):
        new_dict = {}
        for key, val in dict.items():
            new_key = key[0].lower()

            for x in key[1:]:
                if not x.islower():
                    new_key += "_" + x.lower()
                else:
                    new_key += x

            new_dict[new_key] = val

        return super(dash_attributes, metaclass).__new__(metaclass, name, bases, new_dict)


global flag  # to track which __metaclass__'es get invoked
flag = 0

class sub_type1(type):
    def __new__(cls, name, bases, dict):
        global flag
        flag += 1
        return super(sub_type1, cls).__new__(cls, name, bases, dict)

class sub_type2(type):
    def __new__(cls, name, bases, dict):
        global flag
        flag += 10
        return super(sub_type2, cls).__new__(cls, name, bases, dict)

class sub_type3(sub_type2): # subclass
    def __new__(cls, name, bases, dict):
        global flag
        flag += 100
        return super(sub_type3, cls).__new__(cls, name, bases, dict)


class MetaclassTest(IronPythonTestCase):

    def test_modify(self):
        """Modifying the class dictionary prior to the class being created."""
        def _check(T):
            x = T()
            self.assertEqual(x.version, 2.4)
            self.assertEqual(x.method(), 10)
            self.assertEqual(x.__class__.__name__, "D")

        for f in [ g_f_modify, g_c_modify ]:
            class C(object, metaclass=f((New,), "D")):
                pass
            _check(C)

            class C(metaclass=f((New,), "D")):
                pass
            _check(C)

    def test_dash_attribute(self):
        class C(object, metaclass=dash_attributes):
            def WriteLine(self, *arg): return 4

        x = C()
        self.assertFalse(hasattr(C, "WriteLine"))
        self.assertEqual(x.write_line(), 4)

    def test_basic(self):
        def try_metaclass(t):
            class C(object, metaclass=t):
                def method(self): return 10

            x = C()
            self.assertEqual(x.method(), 10)

        try_metaclass(type)
        #try_metaclass(type(Old))  # bug 364938
        try_metaclass(dash_attributes)
        try_metaclass(sub_type1)

        ## subclassing
        class C1(object, metaclass=g_c_modify()):
            pass
        class C2(metaclass=g_f_modify()):
            pass

        # not defining __metaclass__
        for C in [C1, C2]:
            class D(C): pass

            self.assertTrue(hasattr(D, "version"))
            self.assertEqual(D().version, 2.4)

        # redefining __metaclass__
        try:
            class D(C1, metaclass=dash_attributes):
                pass
        except TypeError: pass
        else: Fail("metaclass conflict expected")

        class D(C2, metaclass=dash_attributes):
            def StartSomethingToday(self): pass

        self.assertTrue(hasattr(D, "version"))
        self.assertTrue(hasattr(D, "start_something_today"))

    def test_find_metaclass(self):
        class A1: pass
        class A2(object): pass
        self.assertEqual(A2.__class__, type)

        class B1(metaclass=dash_attributes):
            pass
        class B2(object, metaclass=dash_attributes):
            pass

        meta = lambda *args: 100

        class D1(metaclass=meta):
            pass
        self.assertEqual(D1, 100)

        class D2(object,metaclass=meta):
            pass
        self.assertEqual(D2.__class__, int)

        # base order: how to see the effect of the order???
        for x in [
                    A1,
                    #A2,        # bug 364991
                ]:
            for y in [B1, B2]:
                class E(x, y):
                    def PythonMethod(self): pass
                self.assertTrue(hasattr(E, "python_method"))

                class E(y, x):
                    def PythonMethod(self): pass
                self.assertTrue(hasattr(E, "python_method"))


        class F1: pass
        self.assertTrue(F1 != 100)

    def test_conflict(self):
        global flag

        class C1(object): pass
        class C2(object,metaclass=sub_type1):
            pass
        class C3(object, metaclass=sub_type2):
            pass
        class C4(object,metaclass=sub_type3):
            pass

        flag = 0
        class D(C1, C2): pass
        #self.assertEqual(flag, 1)   # bug 364991
        flag = 0
        class D(C2, C1): pass
        #self.assertEqual(flag, 1)
        flag = 0
        class D(C3, C4): pass  # C4 derive from C3
        #self.assertEqual(flag, 120)
        flag = 0
        class D(C3, C1, C4): pass
        #self.assertEqual(flag, 120)
        flag = 0
        class D(C4, C1): pass
        #self.assertEqual(flag, 110)

        def f1():
            class D(C2, C3): pass
        def f2():
            class D(C1, C2, C3): pass
        def f3():
            class D(C2, C1, C3): pass

        for f in [
                f1,
                #f2,   # bug 364991
                f3,
            ]:
            self.assertRaises(TypeError, f)

    def test_bad_choices(self):
        def create(x):
            class C(object, metaclass=x):
                pass

        for x in [
                    #None,   # bug 364967
                    1,
                    [],
                    lambda name, bases, dict, extra: 1,
                    lambda name, bases: 1,
                    Old,
                    New,
                ]:
            self.assertRaises(TypeError, create, x)

    def test_metaclass_call_override(self):
        """overriding __call__ on a metaclass should work"""
        class mytype(type):
            def __call__(self, *args):
                return args

        class myclass(object, metaclass=mytype):
            pass

        self.assertEqual(myclass(1,2,3), (1,2,3))

    def test_metaclass(self):
        global recvArgs

        # verify we can use a function as a metaclass in the dictionary
        recvArgs = None
        def funcMeta(*args):
            global recvArgs
            recvArgs = args

        class foo(metaclass=funcMeta):
            pass

        if is_cli:
            self.assertEqual(recvArgs, ('foo', (), {'__module__': __name__}))
        else:
            self.assertEqual(recvArgs, ('foo', (), {'__module__': __name__, '__qualname__': 'MetaclassTest.test_metaclass.<locals>.foo'}))

        class foo(object,metaclass=funcMeta):
            pass

        if is_cli:
            self.assertEqual(recvArgs, ('foo', (object, ), {'__module__': __name__}))
        else:
            self.assertEqual(recvArgs, ('foo', (object, ), {'__module__': __name__, '__qualname__': 'MetaclassTest.test_metaclass.<locals>.foo'}))

        class classType: pass
        classType = type(classType)     # get classObj for tests

        class c(metaclass=classType):
            pass
        self.assertEqual(type(c), classType)

        class c(metaclass=type):
            pass
        self.assertEqual(type(c), type)

        # try setting it a different way - by getting it from a type
        class c(object, metaclass=type(c)):
            pass

        class xyz: pass
        self.assertEqual(type(xyz), type(c))

    def test_arguments(self):
        class MetaType(type):
            def __init__(cls, name, bases, dict):
                super(MetaType, cls).__init__(name, bases, dict)

        class Base(object, metaclass=MetaType):
            pass

        class A(Base):
            def __init__(self, a, b='b', c='12', d='', e=''):
                self.val = a + b + c + d + e

        a = A('hello')
        self.assertEqual(a.val, 'hellob12')

        b = ('there',)
        a = A('hello', *b)
        self.assertEqual(a.val, 'hellothere12')

        c = ['42','23']
        a = A('hello', *c)
        self.assertEqual(a.val, 'hello4223')

        x = ()
        y = {'d': 'boom'}
        a = A('hello', *x, **y)
        self.assertEqual(a.val, 'hellob12boom')

    def test_getattr_optimized(self):
        class Meta(type):
            def __getattr__(self, attr):
                if attr == 'b':
                    return 'b'
                raise AttributeError(attr)

        class A(metaclass=Meta):
            pass

        for i in range(110):
            self.assertEqual('A', A.__name__) # after 100 iterations: see https://github.com/IronLanguages/main/issues/1269
            self.assertEqual('b', A.b)

    def test_single_line_with_metaclass(self):
        """https://github.com/IronLanguages/ironpython3/issues/272"""
        class metaTest(type):
            def __new__(cls, name, bases, body):
                return "test"

        class test(metaclass=metaTest): pass

        self.assertEqual(test, "test")

    def test_keyword_arguments_func(self):
        def funcMeta(*args):
            nonlocal recvArgs
            recvArgs = args

        recvArgs = None
        class Foo(**{'metaclass':funcMeta}): pass
        self.assertEqual(recvArgs[0:2], ('Foo', ()))

        recvArgs = None
        class Bar(*(Foo,), **{'metaclass':funcMeta}): pass
        self.assertEqual(recvArgs[0:2], ('Bar', (None,)))

        def funcMeta(*args, **kwargs):
            nonlocal recvArgs, recvKwargs
            recvArgs = args
            recvKwargs = kwargs

        recvArgs = recvKwargs = None
        class Foo(**{'metaclass':funcMeta}): pass
        self.assertEqual(recvArgs[0:2], ('Foo', ()))
        self.assertEqual(recvKwargs, {})

        recvArgs = recvKwargs = None
        class Bar(*(Foo,), **{'metaclass':funcMeta}): pass
        self.assertEqual(recvArgs[0:2], ('Bar', (None,)))
        self.assertEqual(recvKwargs, {})

        recvArgs = recvKwargs = None
        class Foo(test=1, **{'metaclass':funcMeta}): pass
        self.assertEqual(recvArgs[0:2], ('Foo', ()))
        self.assertEqual(recvKwargs, {'test':1})

        recvArgs = recvKwargs = None
        class Foo(test=1, **{'metaclass':funcMeta, 'test2':2}): pass
        self.assertEqual(recvArgs[0:2], ('Foo', ()))
        self.assertEqual(recvKwargs, {'test':1, 'test2':2})

    def test_keyword_arguments_class(self):
        # type() does not accept any keyword arguments
        with self.assertRaisesRegex(TypeError, r"^type\(\) takes .* arguments"):
            class Foo(test=True): pass

        with self.assertRaisesRegex(TypeError, r"^type\(\) takes .* arguments"):
            class Foo(test=True, **{}): pass

        with self.assertRaisesRegex(TypeError, r"^type\(\) takes .* arguments"):
            class Foo(test=True, metaclass=type): pass

        class MetaP(type):
            @classmethod
            def __prepare__(metacls, name, bases, **kwargs):
                return type.__prepare__(metacls, name, bases)

        msg = "MetaP() takes at most 4 arguments (5 given)" if is_cli else "type() takes 1 or 3 arguments"
        with self.assertRaisesMessage(TypeError, msg):
            class Foo(test=True, metaclass=MetaP): pass

        class MetaN(type):
            def __new__(metacls, name, bases, attrdict, **kwargs):
                return type.__new__(metacls, name, bases, attrdict)

        msg = "__init__() takes at most 3 arguments (4 given)" if is_cli else "type.__init__() takes no keyword arguments"
        with self.assertRaisesMessage(TypeError, msg):
            class Foo(test=True, metaclass=MetaN): pass

        class MetaI(type):
            def __init__(metacls, name, bases, attrdict, **kwargs):
                return type.__init__(metacls, name, bases, attrdict)

        msg = "MetaI() takes at most 4 arguments (5 given)" if is_cli else "type() takes 1 or 3 arguments"
        with self.assertRaisesMessage(TypeError, msg):
            class Foo(test=True, metaclass=MetaI): pass

        class MetaNI(type):
            def __new__(metacls, name, bases, attrdict, **kwargs):
                nonlocal recv_new_args, recv_new_kwargs 
                recv_new_args = metacls, name, bases, attrdict
                recv_new_kwargs = kwargs
                return type.__new__(metacls, name, bases, attrdict)

            def __init__(metacls, name, bases, attrdict, **kwargs):
                nonlocal recv_init_args, recv_init_kwargs 
                recv_init_args = metacls, name, bases, attrdict
                recv_init_kwargs = kwargs
                return type.__init__(metacls, name, bases, attrdict)

        recv_new_args = recv_new_kwargs = None
        recv_init_args = recv_init_kwargs = None

        class Foo(test=True, metaclass=MetaNI): pass

        self.assertEqual(recv_new_args[1:3], ('Foo', ()))
        self.assertEqual(recv_new_kwargs, {'test':True})
        self.assertEqual(recv_init_args[1:3], ('Foo', ()))
        self.assertEqual(recv_init_kwargs, {'test':True})

        class MetaPNI(type):
            @classmethod
            def __prepare__(metacls, name, bases, **kwargs):
                nonlocal prep_cnt
                prep_cnt += 1
                nonlocal recv_prep_args, recv_prep_kwargs
                if prep_cnt == 1:
                    # FIXME: https://github.com/IronLanguages/ironpython3/issues/1154
                    recv_prep_args = metacls, name, bases
                    recv_prep_kwargs = kwargs
                return type.__prepare__(metacls, name, bases)

            def __new__(metacls, name, bases, attrdict, **kwargs):
                nonlocal recv_new_args, recv_new_kwargs 
                recv_new_args = metacls, name, bases, attrdict
                recv_new_kwargs = kwargs
                return type.__new__(metacls, name, bases, attrdict)

            def __init__(metacls, name, bases, attrdict, **kwargs):
                nonlocal recv_init_args, recv_init_kwargs 
                recv_init_args = metacls, name, bases, attrdict
                recv_init_kwargs = kwargs
                return type.__init__(metacls, name, bases, attrdict)

        prep_cnt = 0
        recv_prep_args = recv_prep_kwargs = None
        recv_new_args = recv_new_kwargs = None
        recv_init_args = recv_init_kwargs = None

        class Foo(test=True, metaclass=MetaPNI): pass

        if is_cli:
            # FIXME: https://github.com/IronLanguages/ironpython3/issues/1154
            self.assertEqual(prep_cnt, 2)

        self.assertEqual(recv_prep_args[1:3], ('Foo', ()))
        self.assertEqual(recv_prep_kwargs, {'test':True})
        self.assertEqual(recv_new_args[1:3], ('Foo', ()))
        self.assertEqual(recv_new_kwargs, {'test':True})
        self.assertEqual(recv_init_args[1:3], ('Foo', ()))
        self.assertEqual(recv_init_kwargs, {'test':True})

    def tesk_keyword_arguments_duplicated(self):
        with self.assertRaisesPartialMessage(TypeError, "got multiple values for keyword argument 'test'"):
            class X(test=1, **{'test':2}): pass

        # SyntaxError in CPython 3.4, but works in CPython 3.5 and IronPython
        #with self.assertRaisesPartialMessage(TypeError, "got multiple values for keyword argument 'test'"):
        #    class X(**{'test':1}, **{'test':2}): pass

run_test(__name__)
