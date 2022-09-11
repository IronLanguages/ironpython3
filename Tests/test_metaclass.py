# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, is_cli, big, run_test

# ref: http://docs.python.org/ref/metaclasses.html

class SomeClass(object):
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
            class C(object, metaclass=f((SomeClass,), "D")):
                pass
            _check(C)

            class C(metaclass=f((SomeClass,), "D")):
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
        # A1 hits a slightly different code path in some places than A2, same for B1, B2, etc.
        class A1: pass
        class A2(object): pass
        self.assertEqual(A1.__class__, type)
        self.assertEqual(A2.__class__, type)

        class B1(metaclass=dash_attributes): pass
        class B2(object, metaclass=dash_attributes): pass

        meta32 = lambda *args: 100

        class C1(metaclass=meta32): pass
        self.assertEqual(C1, 100)
        self.assertEqual(C1.__class__, int)

        class C2(object,metaclass=meta32): pass
        self.assertEqual(C2, 100)
        self.assertEqual(C2.__class__, int)

        meta = lambda *args: big(100)

        class D1(metaclass=meta): pass
        self.assertEqual(D1, 100)
        self.assertEqual(D1.__class__, int)

        class D2(object,metaclass=meta): pass
        self.assertEqual(D2, 100)
        self.assertEqual(D2.__class__, int)

        # base order: how to see the effect of the order???
        for x in [
                    A1,
                    A2,
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
        self.assertEqual(flag, 1)
        flag = 0
        class D(C2, C1): pass
        self.assertEqual(flag, 1)
        flag = 0
        class D(C3, C4): pass  # C4 derive from C3
        self.assertEqual(flag, 110)
        flag = 0
        class D(C3, C1, C4): pass
        self.assertEqual(flag, 110)
        flag = 0
        class D(C4, C1): pass
        self.assertEqual(flag, 110)

        def f1():
            class D(C2, C3): pass
        def f2():
            class D(C1, C2, C3): pass
        def f3():
            class D(C2, C1, C3): pass

        for f in [
                f1,
                f2,
                f3,
            ]:
            self.assertRaises(TypeError, f)

    def test_bad_choices(self):
        def create(x):
            class C(object, metaclass=x):
                pass

        for x in [
                    None,
                    1,
                    [],
                    lambda name, bases, dict, extra: 1,
                    lambda name, bases: 1,
                    SomeClass,
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

    def test_class_attribute_order(self):
        import collections
        attributes = []
        class MyDict(collections.OrderedDict):
            def __setitem__(self, key, value):
                attributes.append(key)
                super().__setitem__(key, value)
            def __delitem__(self, key):
                raise NotImplementedError
            def __iter__(self):
                return iter(attributes)

        class MetaClass(type):
            @classmethod
            def __prepare__(metacls, name, bases):
                d = MyDict()
                d['prepared'] = True
                return d

            def __new__(metacls, name, bases, attrdict):
                attrdict['created'] = attrdict['executed']
                t = type.__new__(metacls, name, bases, attrdict)
                self.assertNotIn('__class__', attrdict)
                return t

        class MyClass(metaclass=MetaClass):
            """DOCSTRING"""
            def getclass(self):
                return __class__
            executed = True

        if is_cli:
            self.assertEqual(attributes, ['prepared', '__module__', '__doc__', 'getclass', 'executed', '__classcell__', 'created'])
        else:
            self.assertEqual(attributes, ['prepared', '__module__', '__qualname__', '__doc__', 'getclass', 'executed', '__classcell__', 'created'])

    def test_prepare_mapping(self):
        import collections.abc

        def makeclass(name, bases, attrs):
            return type(name, bases, dict(attrs))

        class MyMutableMapping(collections.abc.MutableMapping):
            """A non-dict subclass that does not allow deletions."""
            def __init__(self):
                self.dict = {}
            def __getitem__(self, key):
                return self.dict[key]
            def __setitem__(self, key, item):
                self.dict[key] = item
            def __delitem__(self, key):
                raise NotImplementedError
            def __iter__(self):
                return iter(self.dict)
            def __len__(self):
                return len(self.dict)

        md = None
        def my_prepare(*args, **kwargs):
            nonlocal md
            md = MyMutableMapping()
            return md

        makeclass.__prepare__ = my_prepare

        class A(metaclass=makeclass):
            """DOCSTRING"""
            @staticmethod
            def getclass():
                return __class__

        self.assertIn('__module__', md)
        self.assertIn('__doc__', md)
        self.assertIn('__classcell__', md)
        self.assertNotIn('__class__', md)
        self.assertIn('getclass', md)
        self.assertEqual(A.getclass(), A)

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
        with self.assertRaisesRegex(TypeError, r"\(\) takes .* arguments"):
            class Foo(test=True): pass

        with self.assertRaisesRegex(TypeError, r"\(\) takes .* arguments"):
            class Foo(test=True, **{}): pass

        with self.assertRaisesRegex(TypeError, r"\(\) takes .* arguments"):
            class Foo(test=True, metaclass=type): pass

        class MetaP(type):
            @classmethod
            def __prepare__(metacls, name, bases, **kwargs):
                return type.__prepare__(metacls, name, bases)

        with self.assertRaisesRegex(TypeError, r"\(\) takes .* arguments"):
            class Foo(test=True, metaclass=MetaP): pass

        class MetaN(type):
            def __new__(metacls, name, bases, attrdict, **kwargs):
                nonlocal recv_new_args, recv_new_kwargs 
                recv_new_args = metacls, name, bases, attrdict
                recv_new_kwargs = kwargs
                return type.__new__(metacls, name, bases, attrdict)

        if (sys.version_info < (3,6) and not is_cli):
            with self.assertRaisesMessage(TypeError, "type.__init__() takes no keyword arguments"):
                class Foo(test=True, metaclass=MetaN): pass
        else:
            recv_new_args = recv_new_kwargs = None

            class Foo(test=True, metaclass=MetaN): pass

            self.assertEqual(recv_new_args[1:3], ('Foo', ()))
            self.assertEqual(recv_new_kwargs, {'test':True})

        class MetaI(type):
            def __init__(metacls, name, bases, attrdict, **kwargs):
                return type.__init__(metacls, name, bases, attrdict)

        with self.assertRaisesRegex(TypeError, r"\(\) takes .* arguments"):
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

        self.assertEqual(prep_cnt, 1)

        self.assertEqual(recv_prep_args[1:3], ('Foo', ()))
        self.assertEqual(recv_prep_kwargs, {'test':True})
        self.assertEqual(recv_new_args[1:3], ('Foo', ()))
        self.assertEqual(recv_new_kwargs, {'test':True})
        self.assertEqual(recv_init_args[1:3], ('Foo', ()))
        self.assertEqual(recv_init_kwargs, {'test':True})

    def test_keyword_arguments_duplicated(self):
        with self.assertRaisesPartialMessage(TypeError, "got multiple values for keyword argument 'test'"):
            class X(test=1, **{'test':2}): pass

        # SyntaxError in CPython 3.4, but works in CPython 3.5 and IronPython
        #with self.assertRaisesPartialMessage(TypeError, "got multiple values for keyword argument 'test'"):
        #    class X(**{'test':1}, **{'test':2}): pass

    def test_mixed_metaclass(self):
        from collections import defaultdict, OrderedDict

        class MetaClass(type):
            @classmethod
            def __prepare__(metacls, name, bases, **kwargs):
                flags['MetaClass.__prepare__'] += 1
                return OrderedDict(MetaClass_prep="prepared by MetaClass")

            def __new__(metacls, name, bases, attrdict, **kwargs):
                flags['MetaClass.__new__'] += 1
                attrdict['MetaClass_new']="created by MetaClass"
                return type.__new__(metacls, name, bases, attrdict)

            def __init__(cls, name, bases, attrdict, **kwargs):
                flags['MetaClass.__init__'] += 1
                attrdict['MetaClass_init']="initialized by MetaClass"
                type.__init__(cls, name, bases, attrdict)

        class SubMeta(MetaClass): pass

        def metafactory(metabase):
            def meta(name, bases, classdict, **kwargs):
                flags['meta'] += 1
                classdict['meta_func'] = "processed by function meta(" + name + ", ...) using metabase " + str(metabase)
                cls = metabase.__new__(metabase, name, bases, classdict)
                metabase.__init__(cls, name, bases, classdict)
                return cls
            return meta

        def my_prepare(*args, **kwargs):
            flags['my_prepare'] += 1
            return {'my_prepare':"prepared by my_prepare function"}

        meta_prep = metafactory(type)
        meta_prep.__prepare__ = my_prepare

        # Using a class as a metaclass
        flags = defaultdict(int)
        class C1(metaclass=MetaClass, private=True):
            self.assertIn('MetaClass_prep', dir())

        self.assertEqual(flags['MetaClass.__prepare__'], 1)
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 1)

        # Using a function as a metaclass
        flags = defaultdict(int)
        class C2(metaclass=metafactory(type), private=True):
            self.assertEqual([], [x for x in dir() if not x.startswith("__")])

        self.assertEqual(flags['meta'], 1)
        self.assertEqual(C2.meta_func, "processed by function meta(C2, ...) using metabase <class 'type'>")

        # Metaclass as function with __prepare__
        flags = defaultdict(int)
        class C3(metaclass=meta_prep, private=True):
            self.assertIn('my_prepare', dir())

        self.assertEqual(flags['meta'], 1)
        self.assertEqual(flags['my_prepare'], 1) # !!!
        self.assertEqual(C3.meta_func, "processed by function meta(C3, ...) using metabase <class 'type'>")

        # Derived from a metaclassed class but metaclass overriden with meta-as-function
        flags = defaultdict(int)
        class C2_C1(C1, metaclass=metafactory(type), private=True):
            self.assertEqual([], [x for x in dir() if not x.startswith("__")])

        self.assertEqual(flags['meta'], 1)
        self.assertEqual(flags['MetaClass.__prepare__'], 0) # !!!
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 0) # !!!
        self.assertEqual(C2_C1.meta_func, "processed by function meta(C2_C1, ...) using metabase <class 'type'>")

        # Derived from a metaclassed class but overriden with meta-as-function w/ __prepare__
        flags = defaultdict(int)
        class C3_C1(C1, metaclass=meta_prep, private=True):
            self.assertIn('my_prepare', dir())
            self.assertNotIn('MetaClass_prep', dir())

        self.assertEqual(flags['meta'], 1)
        self.assertEqual(flags['my_prepare'], 1) # !!!
        self.assertEqual(flags['MetaClass.__prepare__'], 0) # !!!
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 0) # !!!
        self.assertEqual(C3_C1.meta_func, "processed by function meta(C3_C1, ...) using metabase <class 'type'>")

        # Derived from two classes with meta-as-function, but sharing a common base with meta-as-class
        flags = defaultdict(int)
        class X(C2_C1, C3_C1):
            self.assertIn('MetaClass_prep', dir())
            self.assertNotIn('my_prepare', dir())

        self.assertEqual(flags['meta'], 0) # !!!
        self.assertEqual(flags['my_prepare'], 0) # !!!
        self.assertEqual(flags['MetaClass.__prepare__'], 1)
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 1)
        self.assertEqual(X.meta_func, "processed by function meta(C2_C1, ...) using metabase <class 'type'>")

        # Derived from two classes with meta-as-function, sharing a common base with meta-as-class, but again overriden here with meta-as-function w/ __prepare__
        flags = defaultdict(int)
        class XM(C2_C1, C3_C1, metaclass=meta_prep):
            self.assertIn('my_prepare', dir())
            self.assertNotIn('MetaClass_prep', dir())
        self.assertEqual(flags['meta'], 1)
        self.assertEqual(flags['my_prepare'], 1)
        self.assertEqual(flags['MetaClass.__prepare__'], 0) # !!!
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 0) # !!!
        self.assertEqual(XM.meta_func, "processed by function meta(XM, ...) using metabase <class 'type'>")

        # Derived from two classes with meta-as-function, w/o common metaclass
        flags = defaultdict(int)
        class XC(C2, C3):
            self.assertNotIn('my_prepare', dir())

        self.assertEqual(flags['meta'], 0) # !!!
        self.assertEqual(flags['my_prepare'], 0) # !!!
        self.assertEqual(XC.meta_func, "processed by function meta(C2, ...) using metabase <class 'type'>")

        # Simple metaclass deriving from another metaclass
        flags = defaultdict(int)
        class C5(metaclass=SubMeta):
            self.assertIn('MetaClass_prep', dir())

        self.assertEqual(flags['MetaClass.__prepare__'], 1)
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 1)

        # Derived from two metaclassed class, one having a subclassed metaclass, but local metaclass overriden with meta-as-function
        flags = defaultdict(int)
        class XM2(C2_C1, C5, metaclass=metafactory(type)):
            self.assertNotIn('my_prepare', dir())
            self.assertNotIn('MetaClass_prep', dir())
        self.assertEqual(flags['meta'], 1)
        self.assertEqual(flags['my_prepare'], 0)
        self.assertEqual(flags['MetaClass.__prepare__'], 0) # !!!
        self.assertEqual(flags['MetaClass.__new__'], 1)
        self.assertEqual(flags['MetaClass.__init__'], 0) # !!!
        self.assertEqual(XM2.meta_func, "processed by function meta(XM2, ...) using metabase <class 'type'>")

    def test_setter_on_metaclass(self):
        """https://github.com/IronLanguages/ironpython3/issues/1208"""
        class MetaClass(type):
            @property
            def test(self):
                return 1

            @test.setter
            def test(self, value):
                raise Exception

        class Generic(metaclass=MetaClass):
            pass

        self.assertEqual(Generic.test, 1)

        with self.assertRaises(Exception):
            Generic.test = 2

    def test_repr_on_metaclass(self):
        class MetaClass(type):
            def __repr__(self):
                return "qwerty"

        class test(metaclass=MetaClass):
            pass

        self.assertEqual(repr(test), "qwerty")

run_test(__name__)
