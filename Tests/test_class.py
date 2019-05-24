# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# from iptest.type_util import *
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, run_test

GETATTRIBUTE_CALLED = False
class myint(int): pass
class mylong(long): pass

class ClassTest(IronPythonTestCase):

    def test_common_attributes(self):
        builtin_type_instances = [None, object(), 1, "Hello", [0,1], {"a":0}]
        builtin_hashable_type_instances = [None, object(), 1, "Hello"]
        builtin_types = [type(None), object, int, str, list, dict]

        for i in builtin_type_instances:
            # Read-only attribute
            self.assertRaises(AttributeError, i.__delattr__, "__doc__")
            # Non-existent attribute
            self.assertRaises(AttributeError, i.__delattr__, "foo")
            # Modifying __class__ causes a TypeError
            self.assertRaises(TypeError, i.__delattr__, "__class__")
        
            # Read-only attribute
            self.assertRaises(TypeError, i.__setattr__, "__doc__")
            # Non-existent attribute
            self.assertRaises(AttributeError, i.__setattr__, "foo", "foovalue")
            # Modifying __class__ causes a TypeError
            self.assertRaises(TypeError, i.__setattr__, "__class__")
            
            self.assertEqual(type(i), i.__getattribute__("__class__"))
            # Non-existent attribute
            self.assertRaises(AttributeError, i.__getattribute__, "foo")
            
            if is_cli and not i: # !!! Need to expose __reduce__ on all types
                self.assertRaises(TypeError, i.__reduce__)
                self.assertRaises(TypeError, i.__reduce_ex__)
        
        for i in builtin_hashable_type_instances:
            self.assertEqual(hash(i), i.__hash__())
                
        for i in builtin_types:
            if is_cli and i == type(None):
                continue
            # __init__ and __new__ are implemented by IronPython.Runtime.Operations.InstanceOps
            # We do repr to ensure that we can map back the functions properly
            repr(getattr(i, "__init__"))
            repr(getattr(i, "__new__"))
            
    def test_set_dict(self):
        class C: pass
        setdict = C.__dict__
        C.__dict__ = setdict
        
        o1 = C()

        class C:
            def m(self):
                return 42
        
        o2 = C()
        self.assertTrue(42 == o2.m())
        
        self.assertTrue(o2.__class__ is C)
        self.assertTrue(o2.__class__ is not o1.__class__)


    def test_attrs(self):
        class C:pass
        
        C.v = 10
        
        self.assertTrue(C.v == 10)
        
        success = 0
        try:
            x = C.x
        except AttributeError:
            success = 1
        self.assertTrue(success == 1)


    def test_type_in(self):
        self.assertEqual(type in (None, True, False, 1, {}, [], (), 1.0, 1, (1+0j)), False)

    def test_init_defaults(self):
        class A:
            def __init__(self, height=20, width=30):
                self.area = height * width
        
        a = A()
        self.assertTrue(a.area == 600)
        a = A(2,3)
        self.assertTrue(a.area == 6)
        a = A(2)
        self.assertTrue(a.area == 60)
        a = A(width = 2)
        self.assertTrue(a.area == 40)

    def test_getattr(self):
        class C:
            def __init__(self, name, flag):
                self.f = file(name, flag)
            def __getattr__(self, name):
                return getattr(self.f, name)
        
        tmpfile = "tmpfile.txt"
        
        c=C(tmpfile, "w")
        c.write("Hello\n")
        c.close()
        c=C(tmpfile, "r")
        self.assertTrue(c.readline() == "Hello\n")
        c.close()

        try:
            import os
            os.unlink(tmpfile)
        except:
            pass
                
        # new-style
        class C(object):
            def __getattr__(self, name):
                raise AttributeError(name)
        
        # old-style
        class D:
            def __getattr__(self, name):
                raise AttributeError(name)

        # new-style __getattribute__
        class E(object):
            def __getattribute__(self, name):
                if name == 'xyz':
                    raise AttributeError(name)
                if name == 'x':
                    return 42
                return object.__getattribute__(self, name)

        # derived new-style type
        class F(E):
            pass

        # verify that base class' __getattribute__ is called.
        self.assertEqual(F().x, 42)
            
        # exception shouldn't propagate out
        for cls in [C, D, E, F]:
            self.assertEqual(getattr(cls(), 'xyz', 'DNE'), 'DNE')
            self.assertEqual(hasattr(cls(), 'xyz'), False)
        
        
        # removing & adding back on __getattribute__ should work
        class foo(object):
            def __getattribute__(self, name): return 42

        x = foo.__getattribute__
        del foo.__getattribute__
        self.assertRaises(AttributeError, getattr, foo(), 'x')
        foo.__getattribute__ = x
        self.assertEqual(foo().x, 42)
        del foo.__getattribute__
        self.assertRaises(AttributeError, getattr, foo(), 'x')

        # check getattr when the property raises
        class C(object):
            def throw(self):
                raise AttributeError
            foo = property(throw)

        self.assertEqual(getattr(C(), 'foo', 'abc'), 'abc')

    def count_elem(self,d,n):
        count = 0
        for e in d:
            if e == n:
                count += 1
        return count

############################################################
    def test_newstyle_oldstyle_dict(self):
        """Dictionary and new style classes"""
        
        class class_n(object):
            val1 = "Value"
            def __init__(self):
                self.val2 = self.val1
        
        inst_n = class_n()
        self.assertTrue(inst_n.val2 == "Value")
        self.assertTrue(not 'val2' in dir(class_n))
        self.assertTrue('val1' in dir(class_n))
        self.assertTrue('val2' in dir(inst_n))
        self.assertTrue('val1' in dir(inst_n))
        self.assertTrue('val2' in inst_n.__dict__)
        self.assertTrue(inst_n.__dict__['val2'] == "Value")
        self.assertTrue(self.count_elem(dir(inst_n), "val1") == 1)
        inst_n.val1 = 20
        self.assertTrue(self.count_elem(dir(inst_n), "val1") == 1)

        # old style classes:
        
        class class_o:
            val1 = "Value"
            def __init__(self):
                self.val2 = self.val1
        
        inst_o = class_o()
        self.assertTrue('val1' in dir(class_o))
        self.assertTrue(not 'val2' in dir(class_o))
        self.assertTrue('val1' in dir(inst_o))
        self.assertTrue('val2' in dir(inst_o))
        self.assertTrue('val2' in inst_o.__dict__)
        self.assertTrue(inst_o.__dict__['val2'] == "Value")
        self.assertTrue(self.count_elem(dir(inst_o), "val1") == 1)
        inst_n.val1 = 20
        self.assertTrue(self.count_elem(dir(inst_o), "val1") == 1)
        self.assertTrue(isinstance(class_o, object))
        self.assertTrue(isinstance(inst_o, object))
        self.assertTrue(isinstance(None, object))


    def test_misc(self):
        class C:
            def x(self):
                return 'C.x'
            def y(self):
                return 'C.y'
        
        class D:
            def z(self):
                return 'D.z'
        
        c = C()
        self.assertEqual(c.x(), "C.x")
        self.assertEqual(c.y(), "C.y")
        
        # verify repr and str on old-style class objects have the right format:
        
        # bug# 795
        self.assertEqual(str(C), __name__+'.C')
        self.assertEqual(repr(C).index('<class '+__name__+'.C at 0x'), 0)
        
        success=0
        try:
            c.z()
        except AttributeError:
            success=1
        self.assertTrue(success==1)
        
        C.__bases__+=(D,)
        
        self.assertEqual(c.z(), "D.z")

        class C:
            def m(self):
                return "IronPython"
            def n(self, parm):
                return parm
        
        c = C()
        
        y = c.m
        y = c.n
        y = C.m
        y = C.n

        self.assertTrue('__dict__' not in str.__dict__)

    def test_dir_in_init(self):
        # both of these shouldn't throw
        
        class DirInInit(object):
            def __init__(self):
                dir(self)
        
        a = DirInInit()


    def test_priv_class(self):
        class _PrivClass(object):
            def __Mangled(self):
                    pass
            def __init__(self):
                    a = self.__Mangled
        
        a = _PrivClass()

    def test_inheritance_attrs_dir(self):
        class foo:
            def foofunc(self):
                return "foofunc"
        
        class bar(foo):
            def barfunc(self):
                return "barfunc"
        
        class baz(foo, bar):
            def bazfunc(self):
                return "bazfunc"
        
        self.assertTrue('foofunc' in dir(foo))
        self.assertTrue(dir(foo).count('__doc__') == 1)
        self.assertTrue(dir(foo).count('__module__') == 1)
        self.assertTrue(len(dir(foo)) == 3)
        self.assertTrue('foofunc' in dir(bar))
        self.assertTrue('barfunc' in dir(bar))
        self.assertTrue(dir(bar).count('__doc__') == 1)
        self.assertTrue(dir(bar).count('__module__') == 1)
        self.assertTrue(len(dir(bar)) == 4)
        self.assertTrue('foofunc' in dir(baz))
        self.assertTrue('barfunc' in dir(baz))
        self.assertTrue('bazfunc' in dir(baz))
        self.assertTrue(dir(baz).count('__doc__') == 1)
        self.assertTrue(dir(baz).count('__module__') == 1)
        self.assertTrue(len(dir(baz)) == 5)
        
        bz = baz()
        self.assertTrue('foofunc' in dir(bz))
        self.assertTrue('barfunc' in dir(bz))
        self.assertTrue('bazfunc' in dir(bz))
        self.assertTrue(dir(bz).count('__doc__') == 1)
        self.assertTrue(dir(bz).count('__module__') == 1)
        self.assertTrue(len(dir(bz)) == 5)
        
        bz.__module__ = "MODULE"
        self.assertTrue(bz.__module__ == "MODULE")
        bz.__module__ = "SOMEOTHERMODULE"
        self.assertTrue(bz.__module__ == "SOMEOTHERMODULE")
        bz.__module__ = 33
        self.assertTrue(bz.__module__ == 33)
        bz.__module__ = [2, 3, 4]
        self.assertTrue(bz.__module__ == [2, 3 , 4])


    def test_oldstyle_setattr(self):
        global called
        class C:
            def __setattr__(self, name, value):
                global called
                called = (self, name, value)
                
        a = C()
        a.abc = 'def'
        self.assertEqual(called, (a, 'abc', 'def'))
        
        del C.__setattr__
        
        a.qrt = 'abc'
        
        self.assertEqual(called, (a, 'abc', 'def'))
        
        def setattr(self, name, value):
            global called
            called = (self, name, value)
        
        C.__setattr__ = setattr
        
        a.qrt = 'abc'
        
        self.assertEqual(called, (a, 'qrt', 'abc'))
    
    def test_oldstyle_getattr(self):
        """verify we don't access __getattr__ while creating an old
        style class."""
        
        class C:
            def __getattr__(self,name):
                return globals()[name]
        
        a = C()

    def test_oldstyle_eq(self):
        """old style __eq__ shouldn't call __cmp__"""
        class x: pass
        
        inst = type(x())
        
        global cmpCalled
        cmpCalled = False
        class C:
            def __init__(self, value):
                self.value = value
            def __cmp__(self, other):
                global cmpCalled
                cmpCalled = True
                return self.value - other.value
        
        class D:
            def __init__(self, value):
                self.value = value
            def __cmp__(self, other):
                global cmpCalled
                cmpCalled = True
                return self.value - other.value
        
        
        class C2(C): pass
        
        class D2(D): pass
        
        
        self.assertEqual(inst.__eq__(C(3.0), C(4.5)), NotImplemented)
        self.assertEqual(inst.__eq__(C(3.0), C2(4.5)), NotImplemented)
        self.assertEqual(inst.__eq__(C(3.0), D(4.5)), NotImplemented)
        self.assertEqual(cmpCalled, False)

    def test_raise_attrerror(self):
        """raising AttributeError from __getattr__ should be ok,
        and shouldn't be seen by the user"""
        
        class A:
            def __getattr__(self, name):
                raise AttributeError('get outta here')
            def __repr__(self):
                return 'foo'
        
        class B:
            def __getattr__(self, name):
                raise AttributeError('get outta here')
            def __str__(self):
                return 'foo'
        
        self.assertEqual(str(A()), 'foo')
        self.assertEqual(repr(A()), 'foo')
        self.assertEqual(str(B()), 'foo')
        self.assertTrue(repr(B()).find('B instance') != -1)

# use exec to define methods on classes:

    def test_exec_namespace(self):
        class oldclasswithexec:
            exec("def oldexecmethod(self): return 'result of oldexecmethod'")
        
        self.assertTrue('oldexecmethod' in dir(oldclasswithexec))
        self.assertEqual(oldclasswithexec().oldexecmethod(), 'result of oldexecmethod')
        
        class newclasswithexec(object):
            exec("def newexecmethod(self): return 'result of newexecmethod'")
        
        self.assertTrue('newexecmethod' in dir(newclasswithexec))
        self.assertEqual(newclasswithexec().newexecmethod(), 'result of newexecmethod')


    def test_module_name(self):
        global __name__
        
        mod = sys.modules[__name__]
        name = __name__
        
        mod.__name__ = None
        
        class C: pass
        
        self.assertEqual(C.__module__, None)
        
        def func1():
            __name__ = "wrong"
            class C: pass
            return C()
        
        def func2():
            class C: pass
            return C()
        
        def func3():
            global __name__
            __name__ = "right"
            class C: pass
            return C()
            
            
        self.assertEqual(func1().__module__, func2().__module__)
        
        __name__ = "fake"
        self.assertEqual(func1().__module__, "fake")
        
        self.assertEqual(func3().__module__, "right")
        mod.__name__ = name
        
        def f(x): x.__module__
        def g(x): getattr(x, '__module__')
        import errno
        for thing in "", 1, errno, 1, 1+2j, (), [], {}:
            self.assertEqual(getattr(thing, '__module__', 'does_not_exist'), 'does_not_exist')
            self.assertEqual(hasattr(thing, '__module__'), False)
            self.assertRaises(AttributeError, f, thing)
            self.assertRaises(AttributeError, g, thing)
        
        self.assertRaisesMessage(TypeError, "can't set function.__module__", type(type(f)).__dict__['__module__'].__set__, type(f), 42)
        
        class x(object): pass
        type(type(x())).__dict__['__module__'].__set__(x, 'fooz')
        self.assertEqual(x.__module__, 'fooz')
        
        self.assertRaisesMessage(TypeError, "can't delete x.__module__", type(type(x())).__dict__['__module__'].__delete__, x)

    def test_check_dictionary(self):
        """tests to verify that Symbol dictionaries do the right thing in dynamic scenarios"""
        def CheckDictionary(C):
            # add a new attribute to the type...
            C.newClassAttr = 'xyz'
            self.assertEqual(C.newClassAttr, 'xyz')
            
            # add non-string index into the class and instance dictionary
            a = C()
            a.__dict__[1] = '1'
            if object in C.__bases__:
                try:
                    C.__dict__[2] = '2'
                    self.assertUnreachable()
                except TypeError: pass
                self.assertEqual(2 in C.__dict__, False)

            self.assertEqual(1 in a.__dict__, True)
            self.assertEqual(dir(a).__contains__(1), True)

            self.assertEqual(repr(a.__dict__), "{1: '1'}")
            
            # replace a class dictionary (containing non-string keys) w/ a normal dictionary
            C.newTypeAttr = 1
            self.assertEqual(hasattr(C, 'newTypeAttr'), True)
            
            class OldClass: pass
            
            if isinstance(C, type(OldClass)):
                C.__dict__ = dict(C.__dict__)
                self.assertEqual(hasattr(C, 'newTypeAttr'), True)
            else:
                try:
                    C.__dict__ = {}
                    self.assertUnreachable()
                except AttributeError:
                    pass
            
            # replace an instance dictionary (containing non-string keys) w/ a new one.
            a.newInstanceAttr = 1
            self.assertEqual(hasattr(a, 'newInstanceAttr'), True)
            a.__dict__  = dict(a.__dict__)
            self.assertEqual(hasattr(a, 'newInstanceAttr'), True)
        
            a.abc = 'xyz'
            self.assertEqual(hasattr(a, 'abc'), True)
            self.assertEqual(getattr(a, 'abc'), 'xyz')
            
        
        class OldClass:
            def __init__(self):  pass
        
        class NewClass(object):
            def __init__(self):  pass
        
        CheckDictionary(OldClass)
        CheckDictionary(NewClass)

    def test_call_type_call(self):
        for stuff in [object, int, str, bool, float]:
            self.assertEqual(type(type.__call__(stuff)), stuff)
        
        self.assertEqual(type.__call__(int, 5), 5)
        self.assertEqual(type.__call__(int), 0)
        
        self.assertEqual(type.__call__(bool, True), True)
        self.assertEqual(type.__call__(bool), False)
        
        #User-defined old/new style classes
        call_mapper = {}
        
        class KOld0:
            def __call__(self):
                return 2
        call_mapper[KOld0] = lambda: [type(KOld0()).__call__(KOld0())]
                
        class KOld1:
            def __call__(self, p):
                return 2
        call_mapper[KOld1] = lambda: [type(KOld1()).__call__(KOld1(), 3.14),
                                    type(KOld1()).__call__(KOld1(), p=3.14),
                                    type(KOld1()).__call__(KOld1(), **{"p":3.14}),
                                    type(KOld1()).__call__(KOld1(), (1, 2, 3))  ]
                
        class KOldArgs:
            def __call__(self, *args):
                return 2
        call_mapper[KOldArgs] = lambda: [type(KOldArgs()).__call__(KOldArgs())]
                
        class KOldKwargs:
            def __call__(self, **kwargs):
                return 2
        call_mapper[KOldKwargs] = lambda: [type(KOldKwargs()).__call__(KOldKwargs())]
                
        class KOldArgsKwargs:
            def __call__(self, *args, **kwargs):
                return 2
        call_mapper[KOldArgsKwargs] = lambda: [type(KOldArgsKwargs()).__call__(KOldArgsKwargs())]
                
        
        
        class KNew0(object):
            def __call__(self):
                return 2
        call_mapper[KNew0] = lambda: [type(KNew0()).__call__(KNew0())]
                
        class KNew1(object):
            def __call__(self, p):
                return 2
        call_mapper[KNew1] = lambda: [type(KNew1()).__call__(KNew1(), 3.14),
                                    type(KNew1()).__call__(KNew1(), p=3.14),
                                    type(KNew1()).__call__(KNew1(), **{"p":3.14}),
                                    type(KNew1()).__call__(KNew1(), []),
                                    ]
                
        class KNewArgs(object):
            def __call__(self, *args):
                return 2
        call_mapper[KNewArgs] = lambda: [type(KNewArgs()).__call__(KNewArgs()) ]
                
        class KNewKwargs(object):
            def __call__(self, **kwargs):
                return 2
        call_mapper[KNewKwargs] = lambda: [type(KNewKwargs()).__call__(KNewKwargs())]
                
        class KNewArgsKwargs(object):
            def __call__(self, *args, **kwargs):
                return 2
        call_mapper[KNewArgsKwargs] = lambda: [type(KNewArgsKwargs()).__call__(KNewArgsKwargs())]

        
        for K in list(call_mapper.keys()):        
            for ret_val in call_mapper[K]():
                self.assertEqual(ret_val, 2)


    def test_cp8246(self):
        
        #...
        class K(object):
            def __call__(self):
                return ((), {})
        self.assertEqual(K()(), ((), {}))
        
        #...
        class K(object):
            def __call__(self, **kwargs):
                return ((), kwargs)
        self.assertEqual(K()(), ((), {}))
        self.assertEqual(K()(**{}), ((), {}))
        self.assertEqual(K()(**{'a':None}), ((), {'a':None}))
        
        #...
        class K(object):
            def __call__(self, *args):
                return (args, {})
        self.assertEqual(K()(), ((), {}))
        self.assertEqual(K()(*()), ((), {}))
        self.assertEqual(K()(*(None,)), ((None,), {}))
        
        #...
        class K(object):
            def __call__(self, *args, **kwargs):
                return (args, kwargs)
        self.assertEqual(K()(), ((), {}))
        self.assertEqual(K()(*()), ((), {}))
        self.assertEqual(K()(**{}), ((), {}))
        self.assertEqual(K()(*(), **{}), ((), {}))
        self.assertEqual(K()(*(None,)), ((None,), {}))
        self.assertEqual(K()(**{'a':None}), ((), {'a':None}))
        self.assertEqual(K()(*(None,), **{'a':None}), ((None,), {'a':None}))
    
    
    def test_mixed_inheritance(self):
        """inheritance from both old & new style classes..."""
        class foo: pass
        
        class bar(object): pass
        
        class baz1(foo, bar): pass
        
        class baz2(bar, foo): pass
        
        self.assertEqual(baz1.__bases__, (foo, bar))
        self.assertEqual(baz2.__bases__, (bar, foo))
        
        class foo:
            abc = 3
        
        class bar(object):
            def get_abc():
                return 42
            def set_abc():
                pass
                
            abc = property(fget=get_abc, fset=set_abc)
        
        class baz(foo, bar): pass
        
        self.assertEqual(baz().abc, 3)

    def test_newstyle_unbound_inheritance(self):
        """verify calling unbound method w/ new-style class on subclass which
        new-style also inherits from works."""
        class foo:
            def func(self): return self
        
        class bar(object, foo):
            def barfunc(self):
                    return foo.func(self)
        
        a = bar()
        self.assertEqual(a.barfunc(), a)

    def test_mro(self):
        """mro (method resolution order) support"""
        class A(object): pass
        
        self.assertEqual(A.__mro__, (A, object))
        
        class B(object): pass
        
        self.assertEqual(B.__mro__, (B, object))
        
        class C(B): pass
        
        self.assertEqual(C.__mro__, (C, B, object))
        
        class N(C,B,A): pass
        
        self.assertEqual(N.__mro__, (N, C, B, A, object))
        
        try:
            class N(A, B,C): pass
            self.assertUnreachable("impossible MRO created")
        except TypeError:
            pass
        
        try:
            class N(A, A): pass
            self.assertUnreachable("can't dervie from the same base type twice")
        except TypeError:
            pass

    def test_mro_bases(self):
        """verify replacing base classes also updates MRO"""
        class C(object):
            def __getattribute__(self, name):
                if(name == 'xyz'): return 'C'
                return super(C, self).__getattribute__(name)
        
        class C1(C):
            def __getattribute__(self, name):
                if(name == 'xyz'):  return 'C1'
                return super(C1, self).__getattribute__(name)
        
        class A(object): pass
        
        class B(object):
            def __getattribute__(self, name):
                if(name == 'xyz'): return 'B'
                return super(B, self).__getattribute__(name)
        
        a = C1()
        self.assertEqual(a.xyz, 'C1')
        
        C1.__bases__ = (A,B)
        self.assertEqual(a.xyz, 'C1')
        
        del(C1.__getattribute__)
        self.assertEqual(a.xyz, 'B')


    def test_dynamic_mro_bases(self):
        class C(object):
            pass
        
        def __getattribute__(self, name):
            if (name == 'xyz'):
                return 'C'
            return super(C, self).__getattribute__(name)
        
        C.__getattribute__ = __getattribute__
        
        class C1(C):
            pass
        
        def __getattribute__(self, name):
            if (name == 'xyz'):
                return 'C1'
            return super(C1, self).__getattribute__(name)
        
        
        C1.__getattribute__ = __getattribute__
        
        
        class B(object):
            pass
        
        def __getattribute__(self, name):
            if (name == 'xyz'):
                return 'B'
            return super(B, self).__getattribute__(name)
        
        B.__getattribute__ = __getattribute__
        
        C1.__bases__ = (B, )
        self.assertEqual(C1().xyz, 'C1')
    
    def test_builtin_mro(self):
        """int mro shouldn't include ValueType"""
        self.assertEqual(int.__mro__, (int, object))


    def test_mixed_inheritance_mro(self):
        """mixed inheritance from old-style & new-style classes"""
        
        # we should use old-style MRO when inheriting w/ a single old-style class
        class A: pass
        
        class B(A): pass
        
        class C(A): pass
        
        class D(B, C):pass
        
        class E(D, object): pass
        
        # old-style MRO of D is D, B, A, C, which should
        # be present  in E's mro
        self.assertEqual(E.__mro__, (E, D, B, A, C, object))
        
        class F(B, C, object): pass
        
        # but when inheriting from multiple old-style classes we switch
        # to new-style MRO, and respect local ordering of classes in the MRO
        self.assertEqual(F.__mro__, (F, B, C, A, object))
        
        class G(B, object, C): pass
        
        self.assertEqual(G.__mro__, (G, B, object, C, A))
        
        class H(E): pass
        
        self.assertEqual(H.__mro__, (H, E, D, B, A, C, object))
        
        try:
            class H(A,B,E): pass
            self.assertUnreachable()
        except TypeError:
            pass
        
        
        class H(E,B,A): pass
        
        self.assertEqual(H.__mro__, (H, E, D, B, A, C, object))

    def test_depth_first_mro_mixed(self):
        """Verify given two large, independent class hierarchies
        that we favor them in the order listed.
        
        w/ old-style
        """
        
        class A: pass
        
        class B(A): pass
        
        class C(A): pass
        
        class D(B,C): pass
        
        class E(D, object): pass
        
        class G: pass
        
        class H(G): pass
        
        class I(G): pass
        
        class K(H,I, object): pass
        
        class L(K,E): pass
        
        self.assertEqual(L.__mro__, (L, K, H, I, G, E, D, B, A, C, object))


    def test_depth_first_mro(self):
        """w/o old-style"""
        
        class A(object): pass
        
        class B(A): pass
        
        class C(A): pass
        
        class D(B,C): pass
        
        class E(D, object): pass
        
        class G(object): pass
        
        class H(G): pass
        
        class I(G): pass
        
        class K(H,I, object): pass
        
        class L(K,E): pass
        
        self.assertEqual(L.__mro__, (L, K, H, I, G, E, D, B, C, A, object))

    
    def test_newstyle_lookup(self):
        """new-style classes should only lookup methods from the class, not from the instance"""
        class Strange(object):
            def uselessMethod(self): pass
        
        global obj
        obj = Strange()
        obj.__nonzero__ = lambda: False
        self.assertEqual(bool(obj), True)
        
        def twoargs(self, other):
            global twoArgsCalled
            twoArgsCalled = True
            return self
        
        def onearg(self):
            return self
            
        def onearg_str(self):
            return 'abc'
        
        # create methods that we can then stick into Strange
        twoargs = type(Strange.uselessMethod)(twoargs, None, Strange)
        onearg = type(Strange.uselessMethod)(onearg, None, Strange)


        class ForwardAndReverseTests:
            testCases = [
                #forward versions
                ('__add__', 'obj + obj'),
                ('__sub__', 'obj - obj'),
                ('__mul__', 'obj * obj'),
                ('__floordiv__', 'obj // obj'),
                ('__mod__', 'obj % obj'),
                ('__divmod__', 'divmod(obj,obj)'),
                ('__pow__', 'pow(obj, obj)'),
                ('__lshift__', 'obj << obj'),
                ('__rshift__', 'obj >> obj'),
                ('__and__', 'obj & obj'),
                ('__xor__', 'obj ^ obj'),
                ('__or__', 'obj | obj'),
                
                # reverse versions
                ('__radd__', '1 + obj'),
                ('__rsub__', '1 - obj'),
                ('__rmul__', '1 * obj'),
                ('__rfloordiv__', '1 // obj'),
                ('__rmod__', '1 % obj'),
                #('__rdivmod__', '1 % obj'), #bug 975
                ('__rpow__', 'pow(1, obj)'),
                ('__rlshift__', '1 << obj'),
                ('__rrshift__', '1 >> obj'),
                ('__rand__', '1  & obj'),
                ('__rxor__', '1 ^ obj'),
                ('__ror__', '1 | obj'),
                ]
            
            @staticmethod
            def NegativeTest(method, testCase):
                setattr(obj, method, twoargs)
                
                try:
                    eval(testCase)
                    self.assertUnreachable()
                except TypeError as e:
                    pass
                
                delattr(obj, method)
            
            @staticmethod
            def PositiveTest(method, testCase):
                setattr(Strange, method, twoargs)
                
                self.assertEqual(eval(testCase), obj)
                
                delattr(Strange, method)
        
        
        class InPlaceTests:
            # in-place versions require exec instead of eval
            testCases = [
                # inplace versions
                ('__iadd__', 'obj += obj'),
                ('__isub__', 'obj -= obj'),
                ('__imul__', 'obj *= obj'),
                ('__ifloordiv__', 'obj //= obj'),
                ('__imod__', 'obj %= obj'),
                ('__ipow__', 'obj **= obj'),
                ('__ilshift__', 'obj <<= obj'),
                ('__irshift__', 'obj >>= obj'),
                ('__iand__', 'obj &= obj'),
                ('__ixor__', 'obj ^= obj'),
                ('__ior__', 'obj |= obj'),
            ]
            
            @staticmethod
            def NegativeTest(method, testCase):
                setattr(obj, method, twoargs)
                
                try:
                    exec(testCase, globals(), locals())
                    self.assertUnreachable()
                except TypeError:
                    pass
                
                delattr(obj, method)
            
            @staticmethod
            def PositiveTest(method, testCase):
                setattr(Strange, method, twoargs)
                
                global twoArgsCalled
                twoArgsCalled = False
                exec(testCase, globals(), locals())
                self.assertEqual(twoArgsCalled, True)
                
                delattr(Strange, method)
        
        
        class SingleArgTests:
            testCases = [
                # one-argument versions
                ('__neg__', '-obj'),
                ('__pos__', '+obj'),
                ('__abs__', 'abs(obj)'),
                ('__invert__', '~obj'),
                ]
            
            @staticmethod
            def NegativeTest(method, testCase):
                setattr(obj, method, onearg)
            
                try:
                    eval(testCase)
                    self.assertUnreachable()
                except TypeError:
                    pass
                
                delattr(obj, method)
            
            @staticmethod
            def PositiveTest(method, testCase):
                setattr(Strange, method, onearg)

                try:
                    self.assertEqual(eval(testCase), obj)
                except TypeError:
                    self.assertTrue(method == '__oct__' or method == '__hex__')
                
                delattr(Strange, method)
        
        class HexOctTests:
            testCases = [
                ('__oct__', 'oct(obj)'),
                ('__hex__', 'hex(obj)'),
                ]

            @staticmethod
            def NegativeTest(method, testCase):
                setattr(obj, method, onearg)
            
                try:
                    eval(testCase)
                    self.assertUnreachable()
                except TypeError:
                    pass
                
                delattr(obj, method)
            
            @staticmethod
            def PositiveTest(method, testCase):
                setattr(Strange, method, onearg_str)

                self.assertEqual(eval(testCase), 'abc')
                
                delattr(Strange, method)

        class ConversionTests:
            testCases = [
                (('__complex__', 2+0j), 'complex(obj)'),
                (('__int__', 1), 'int(obj)'),
                (('__long__', 1), 'long(obj)'),
                (('__float__', 1.0), 'float(obj)'),
            ]
            
            @staticmethod
            def NegativeTest(method, testCase):
                setattr(obj, method[0], onearg)
            
                try:
                    eval(testCase)
                    self.assertUnreachable()
                except (TypeError, ValueError) as e:
                    self.assertEqual(e.args[0].find('returned') == -1, True)    # shouldn't have returned '__complex__ returned ...'

                delattr(obj, method[0])
                
            @staticmethod
            def PositiveTest(method, testCase):
                def testMethod(self):
                    return method[1]
                    
                testMethod = type(Strange.uselessMethod)(testMethod, None, Strange)
                setattr(Strange, method[0], testMethod)
        
                self.assertEqual(eval(testCase), method[1])
                
                delattr(Strange, method[0])
            
        allTests = [ForwardAndReverseTests, InPlaceTests, SingleArgTests, ConversionTests, HexOctTests]
        
        for test in allTests:
            for method,testCase in test.testCases:
                test.NegativeTest(method, testCase)
            for method,testCase in test.testCases:
                test.PositiveTest(method, testCase)
        
        #Verify that the base type's defined special operators get picked up.
        class DerivedStrange(Strange): pass
        
        obj = DerivedStrange()
        for test in allTests:
            for method,testCase in test.testCases:
                test.NegativeTest(method, testCase)
            for method,testCase in test.testCases:
                test.PositiveTest(method, testCase)
    
     
    def test_bad_repr(self):
        # overriding a classes __repr__ and returning a
        # non-string should throw
        
        class C:
            def __repr__(self):
                return None
        
        self.assertRaises(TypeError, repr, C())
        
        class C(object):
            def __repr__(self):
                return None
        
        self.assertRaises(TypeError, repr, C())


    def test_name(self):
        """setting __name__ on a class should work"""
        
        class C(object): pass
        
        C.__name__ = 'abc'
        self.assertEqual(C.__name__, 'abc')

    def test_mro_super(self):
        """super for multiple inheritance we should follow the MRO as we go up the super chain"""
        class F:
            def meth(self):
                return 'F'
        
        class G: pass
        
        def gmeth(self): return 'G'
        
        
        class A(object):
            def meth(self):
                if hasattr(super(A, self), 'meth'):
                    return 'A' + super(A, self).meth()
                else:
                    return "A"
        
        class B(A):
            def __init__(self):
                self.__super = super(B, self)
                super(B, self).__init__()
            def meth(self):
                return "B" + self.__super.meth()
        
        class C(A):
            def __init__(self):
                self.__super = super(C, self)
                super(C, self).__init__()
            def meth(self):
                return "C" + self.__super.meth()
        
        class D(C, B):
            def meth(self):
                return "D" + super(D, self).meth()
        
        self.assertEqual(D().meth(), 'DCBA')
        
        class D(C, F, B):
            def meth(self):
                return "D" + super(D, self).meth()
        
        self.assertEqual(D.__mro__, (D,C,F,B,A,object))
        self.assertEqual(D().meth(), 'DCF')
        
        class D(C, B, F):
            def meth(self):
                return "D" + super(D, self).meth()
        
        self.assertEqual(D.__mro__, (D,C,B,A,object,F))
        self.assertEqual(D().meth(), 'DCBAF')
        
        
        class D(C, B, G):
            def meth(self):
                return "D" + super(D, self).meth()
        
        d = D()
        d.meth = type(F.meth)(gmeth, d, G)
        self.assertEqual(d.meth(), 'G')


    def test_slots(self):
        """slots tests"""
        
        # simple slots, assign, delete, etc...
        
        class foo(object):
            __slots__ = ['abc']
            
        class bar(object):
            __slots__ = 'abc'
        
        class baz(object):
            __slots__ = ('abc', )
        
        for slotType in [foo, bar, baz]:
            a = slotType()
            self.assertRaises(AttributeError, lambda: a.abc)
            self.assertEqual(hasattr(a, 'abc'), False)
            
            a.abc = 'xyz'
            self.assertEqual(a.abc, 'xyz')
            self.assertEqual(hasattr(a, 'abc'), True)

            del(a.abc)
            self.assertEqual(hasattr(a, 'abc'), False)
            self.assertRaises(AttributeError, lambda: a.abc)
            
            # slot classes don't have __dict__
            self.assertEqual(hasattr(a, '__dict__'), False)
            self.assertRaises(AttributeError, lambda: a.__dict__)
        
        # sub-class of slots class, has no slots, has a __dict__
        class foo(object):
            __slots__ = 'abc'
            def __init__(self):
                self.abc = 23
                
        class bar(foo):
            def __init__(self):
                super(bar, self).__init__()
            
        a = bar()
        self.assertEqual(a.abc, 23)
        
        del(a.abc)
        self.assertEqual(hasattr(a, 'abc'), False)
        a.abc = 42
        self.assertEqual(a.abc, 42)
        
        x = a.__dict__
        self.assertEqual('abc' in x, False)
        a.xyz = 'abc'
        self.assertEqual(a.xyz, 'abc')
        
        # subclass of not-slots class defining slots:
        
        class A(object): pass
        class B(A): __slots__ = 'c'
        
        self.assertEqual(hasattr(B(), '__dict__'), True)
        self.assertEqual(hasattr(B, 'c'), True)
        
        # slots & metaclass
        if is_cli:          # INCOMPATBILE: __slots__ not supported for subtype of type
            class foo(type):
                __slots__ = ['abc']
        
            class bar(object, metaclass=foo):
                pass
        
        # complex slots
        
        class foo(object):
            __slots__ = ['abc']
            def __new__(cls, *args, **kw):
                self = object.__new__(cls)
                dict = object.__getattribute__(self, '__dict__')
                return self
        
        class bar(foo): pass
        
        
        a = bar()
        
        self.assertRaises(AttributeError, foo)
        
        # slots & name-mangling
        
        class foo(object):
            __slots__ = '__bar'
            
        self.assertEqual(hasattr(foo, '_foo__bar'), True)
        
        # invalid __slots__ values
        for x in ['', None, '3.5']:
            try:
                class C(object):
                    __slots__ = x
                self.assertUnreachable()
            except TypeError:
                pass
        
        # including __dict__ in slots allows accessing __dict__
        class A(object): __slots__ = '__dict__'
        
        self.assertEqual(hasattr(A(),"__dict__"), True)
        a = A()
        a.abc = 'xyz'
        self.assertEqual(a.abc, 'xyz')
        
        class B(A): pass
        self.assertEqual(hasattr(B(),"__dict__"), True)
        b = A()
        b.abc = 'xyz'
        self.assertEqual(b.abc, 'xyz')
        
        # including __weakref__ explicitly
        class A(object):
            __slots__ = ["__weakref__"]
        
        hasattr(A(), "__weakref__")
        
        class B(A): pass
        
        hasattr(B(), "__weakref__")
        
        # weird case, including __weakref__ and __dict__ and we allow
        # a subtype to inherit from both

        if is_cli: types = [object, dict, tuple]    # INCOMPATBILE: __slots__ not supported for tuple
        else: types = [object,dict]
        
        for x in types:
            class A(x):
                __slots__ = ["__dict__"]
            
            class B(x):
                __slots__ = ["__weakref__"]
            
            class C(A,B):
                __slots__ = []
                
            a = C()
            self.assertEqual(hasattr(a, '__dict__'), True)
            self.assertEqual(hasattr(a, '__weakref__'), True)
        
            class C(A,B):
                __slots__ = ['xyz']
                
            a = C()
            self.assertEqual(hasattr(a, '__dict__'), True)
            self.assertEqual(hasattr(a, '__weakref__'), True)
            self.assertEqual(hasattr(C, 'xyz'), True)
        
        # calling w/ keyword args
        
        class foo(object):
            __slots__ = ['a', 'b']
            def __new__(cls, one='a', two='b'):
                self = object.__new__(cls)
                self.a = one
                self.b = two
                return self
        
        a = foo('x', two='y')
        self.assertEqual(a.a, 'x')
        self.assertEqual(a.b, 'y')
            
        # assign to __dict__
        
        class C(object): pass
        
        a = C()
        a.__dict__ = {'b':1}
        self.assertEqual(a.b, 1)
        
        
        # base, child define slots, grand-child doesn't
        
        class foo(object): __slots__ = ['fooSlot']
        
        class bar(foo): __slots__ = ['barSlot']
        
        class baz(bar): pass   # shouldn't throw
        
        a = baz()
        a.barSlot = 'xyz'
        a.fooSlot = 'bar'
        a.dictEntry = 'foo'
        
        self.assertEqual(a.barSlot, 'xyz')
        self.assertEqual(a.fooSlot, 'bar')
        self.assertEqual(a.dictEntry, 'foo')
        
        # large number of slots works (nested tuple slots)
        class foo(object):
            __slots__ = [ 'a' + str(x) for x in range(256) ]
            
        a = foo()
        
        for x in range(256):
            setattr(a, 'a' + str(x), x)
            
        for x in range(256):
            self.assertEqual(x, getattr(a, 'a' + str(x)))


        # new-style / old-style mixed with slots, slots take precedence
        class foo:
            abc = 3

        class bar(object):
            __slots__ = ['abc']
            def __init__(self): self.abc = 5

        class bar2(object):
            abc = 5

        class baz(foo, bar): pass
        
        class baz2(foo, bar2): pass

        self.assertEqual(baz().abc, 5)
        
        # but if it's just a class member we respect MRO
        self.assertEqual(baz2().abc, 3)
        
        # getattr and __slots__ should mix well
        class Foo(object):
            __slots__ = ['bar']
            def __getattr__(self, name):
                    return name.upper()
        
        self.assertEqual(Foo().bar, 'BAR')
        
        # slot takes precedence over dictionary member
        class Foo(object):
            __slots__ = ['bar', '__dict__']

        a = Foo()
        a.__dict__['bar'] = 'abc'
        self.assertRaises(AttributeError, lambda : a.bar)

        # members defined the class take precedence over slots
        global initCalled
        class Foo(object):
            __slots__ = ["__init__"]
            def __init__(self):
                global initCalled
                initCalled = True
        
        initCalled = False
        a = Foo()
        self.assertEqual(initCalled, True)
        
        # the member is readonly because the member slot is gone.
        def f(): a.__init__ = 'abc'
        self.assertRaises(AttributeError, f)
        
        # make sure __init__ isn't special
        class Foo(object):
            __slots__ = ["abc"]
            abc = 3
            
        a = Foo()
        self.assertEqual(a.abc, 3)
        
        def f(): a.abc = 'abc'
        self.assertRaises(AttributeError, f)

        class Foo(object): __slots__ = 'abc'
        
        self.assertEqual(repr(Foo.abc), "<member 'abc' of 'Foo' objects>")
        self.assertEqual(str(Foo.abc), "<member 'abc' of 'Foo' objects>")
        self.assertRaisesPartialMessage(AttributeError, 'abc', lambda: Foo().abc)    
        
        # again w/ empty __slots__ in C1 (409720)
        class C1(object): 
            __slots__ = []

        class C2(object):
            __slots__ = ['a']    
        
        class D1(C1, C2): pass
        
        # name mangling, slots, and classes which start with __
        class __NameStartsWithUnderscore(object):
            __slots__ = [ '__a' ]
            def __init__(self): self.__a = 'a'
            def geta(self): return self.__a
        
        s = __NameStartsWithUnderscore()
        self.assertEqual(s.geta(), 'a')
    
    def test_slots11457(self):
        class COld:
            __slots__ = ['a']

        class CNew(object):
            __slots__ = ['a']
            
        for C in [COld, CNew]:
            for i in range(2):
                setattr(C, 'a', 5)
                self.assertEqual(C().a, 5)
                
                setattr(C, 'a', 7)
                self.assertEqual(C().a, 7)
    
    def test_inheritance_cycle(self):
        """test for inheritance cycle"""
        class CycleA: pass
        class CycleB: pass
        
        try:
            CycleA.__bases__ = (CycleA,)
            self.self.assertUnreachable()
        except TypeError: pass
        
        try:
            CycleA.__bases__ = (CycleB,)
            CycleB.__bases__ = (CycleA,)
            self.self.assertUnreachable()
        except TypeError: pass


    def test_hexoct(self):
        """returning non-string from hex & oct should throw"""
        
        class foo(object):
            def __hex__(self): return self
            def __oct__(self): return self
            
        class bar:
            def __hex__(self): return self
            def __oct__(self): return self
            
        self.assertRaises(TypeError, hex, foo())
        self.assertRaises(TypeError, oct, foo())
        self.assertRaises(TypeError, hex, bar())
        self.assertRaises(TypeError, oct, bar())

    #TODO: @skip("multiple_execute")
    def test_no_clr_attributes(self):
        """verify types have no CLR attributes"""
        # list, 
        class x: pass
        
        for stuff in [object, int, float, bool, str, int, complex, dict, set, 
                    None, NotImplemented, Ellipsis, type(self.test_no_clr_attributes),
                    classmethod, staticmethod, frozenset, property, sys, 
                    BaseException, type(zip), slice, buffer, enumerate, file,
                    range, xrange, type(x), type(x())]:
            for dir_stuff in dir(stuff):
                if dir_stuff[:1].isalpha():
                    self.assertTrue(dir_stuff[:1].islower(),
                        "%s should not be an attribute of %s" % (dir_stuff, str(stuff)))

    def test_method_correct_name(self):
        # __str__ is an InstanceOps method (ToStringMethod), but we should 
        # report the proper name in __str__
        self.assertTrue(repr(BaseException.__str__).find('__str__') != -1)

    #TODO: @skip("multiple_execute")
    def test_no_clr_attributes_sanity(self):
        self.assertEqual(hasattr(int, 'MaxValue'), False)
        self.assertEqual(hasattr(int, 'MinValue'), False)
        self.assertEqual(hasattr(int, 'Abs'), False)
        self.assertEqual(hasattr(int, 'BitwiseOr'), False)
        self.assertEqual(hasattr(int, 'Equals'), False)
        
        self.assertEqual(hasattr(str, 'Empty'), False)
        self.assertEqual(hasattr(str, 'Compare'), False)
        self.assertEqual(hasattr(str, 'Equals'), False)
        self.assertEqual(hasattr(str, 'IndexOf'), False)

    
    def test_outer_scope(self):
        """do not automatically include outer scopes in closure scenarios"""
        def outer_scope_test():
            class Referenced:
                pass
            class C:
                if Referenced: pass
            self.assertTrue("Referenced" not in list(C.__dict__.keys()))
        
        outer_scope_test()
        
        
        for x in [None, 'abc', 3]:
            class foo(object): pass
            a = foo()
            try:
                a.__dict__ = x
                self.assertUnreachable()
            except TypeError: pass

    def test_default_new_init(self):
        """test cases to verify we do the right thing for the default new & init
        methods"""

        anyInitList = [
                    int,
                    int,
                    float,
                    complex,
                    tuple,
                    ]
        anyNewList  = [list,        # classes that take any set of args to __new__
                    set,
                    ]
                
        self.assertRaises(TypeError, object().__init__, 1, 2, 3)
        for x in anyInitList:
            x().__init__(1, 2, 3)
                
            self.assertRaises(TypeError, x.__new__, x, 1, 2, 3)
            self.assertEqual(isinstance(x.__new__(x), x), True)
        
        for x in anyNewList:
            self.assertEqual(len(x.__new__(x, 1, 2, 3)), 0)
            self.assertRaises(TypeError, x.__new__(x).__init__, 1, 2, 3)


        
        class foo(object): pass
        
        self.assertRaises(TypeError, foo, 1)
        
        class foo(list): pass
        self.assertEqual(list.__new__(list, sequence='abc'), [])
        
        x = list.__new__(foo, 1, 2, 3)
        self.assertEqual(len(x), 0)
        self.assertEqual(type(x), foo)
        
        
        # define only __init__.  __new__ should be the same object
        # for both types, and calling it w/ different types should have
        # different responses.
        class foo(object):
            def __init__(self): pass
                    
        self.assertEqual(id(foo.__new__), id(object.__new__))
        
        self.assertRaises(TypeError, object.__new__, object, 1,2,3)
        self.assertEqual(type(object.__new__(foo, 1, 2, 3)), foo)

        # inheritance / mutating class hierarchy tests
        
        # overrides __new__ w/ object.__new__
        class x(object):
            __new__ = object.__new__

        self.assertRaises(TypeError, x().__init__, 2)

        # inherits __new__, overrides w/ default __new__
        class x(object):
            def __new__(cls, *args):
                return object.__new__(cls)
        
        class y(x):
            __new__ = object.__new__
        
        self.assertEqual(y().__init__(2), None)
        
        # then deletes the base __new__
        del x.__new__
        self.assertEqual(y().__init__(2), None)
        
        # then deletes y.__new__
        del y.__new__
        self.assertEqual(y().__init__(2), None)

        # dynamically add __new__
        class x(object): pass
        
        x.__new__ = staticmethod(lambda cls : object.__new__(x))
        
        self.assertEqual(x().__init__(2), None)
        
        # __init__ versions
        # overrides __init__ w/ object.__init__
        class x(object):
            __init__ = object.__init__

        self.assertRaises(TypeError, x, 2)

        # inherits __init__, overrides w/ default __init__
        class x(object):
            def __init__(cls, *args):
                return object.__init__(cls)
        
        class y(x):
            __init__ = object.__init__
        
        self.assertRaises(TypeError, y, 2)
        
        # then deletes the base __init__
        del x.__init__
        self.assertRaises(TypeError, y, 2)
        
        # then deletes y.__init__
        del y.__init__
        self.assertRaises(TypeError, y, 2)

        # dynamically add __init__
        class x(object): pass
        
        x.__init__ = staticmethod(lambda cls : object.__init__(x))
        
        x(2)
        
        # switching bases doesn't change it either
        class x(object):
            def __new__(cls, *args):
                return object.__new__(cls)
        
        class z(object): pass
        
        class y(x):
            pass
        
        self.assertEqual(y().__init__(2), None)
        y.__bases__ = (z, )
        self.assertEqual(y().__init__(2), None)

    def test_hash(self):
        for x in [tuple, str, str, object, frozenset]:
            inst = x()
            self.assertEqual(inst.__hash__(), hash(inst))
            
        # old style hash can return longs, the result of which is
        # the hash of the long
        class foo:
            def __hash__(self): return 1<<35

    def test_NoneSelf(seld):
        try:
            set.add(None)
            self.self.assertUnreachable()
        except TypeError:
            pass

    def test_builtin_classmethod(self):
        descr = dict.__dict__["fromkeys"]
        self.assertRaises(TypeError, descr.__get__, 42)
        self.assertRaises(TypeError, descr.__get__, None, 42)
        self.assertRaises(TypeError, descr.__get__, None, int)

    def test_classmethod(self):
        if is_cli: #http://ironpython.codeplex.com/workitem/27908
            self.assertRaises(TypeError, classmethod, 1)
        else:
            cm = classmethod(1)
            self.assertRaises(TypeError, cm.__get__, None)
            self.assertRaises(TypeError, cm.__get__, None, None)

        def foo(): pass
            
        cm = classmethod(foo)
        self.assertRaises(TypeError, cm.__get__, None)
        self.assertRaises(TypeError, cm.__get__, None, None)
        

    def test_EmptyTypes(self):
        for x in [None, Ellipsis, NotImplemented]:
            self.assertTrue(type(x) != str)
            
        self.assertEqual(repr(Ellipsis), 'Ellipsis')
        self.assertEqual(repr(NotImplemented), 'NotImplemented')
        self.assertEqual(repr(type(Ellipsis)), "<type 'ellipsis'>")
        self.assertEqual(repr(type(NotImplemented)), "<type 'NotImplementedType'>")
    
    def test_property(self):
        prop = property()
        try: prop.fget = self.test_classmethod
        except TypeError: pass
        else: self.assertUnreachable()
        
        try: prop.fdel = self.test_classmethod
        except TypeError: pass
        else: self.assertUnreachable()
        
        try: prop.__doc__ = 'abc'
        except TypeError: pass
        else: self.assertUnreachable()
        
        try: prop.fset = self.test_classmethod
        except TypeError: pass
        else: self.assertUnreachable()
    
    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_override_mro(self):
        try:
            class C(object):
                def __mro__(self): pass
        except NotImplementedError: pass
        else: self.fail("Expected NotImplementedError, got none")
            
        class C(object):
            def mro(self): pass
        
        try:
            class C(type):
                def mro(self): pass
        except NotImplementedError: pass
        else: self.fail("Expected NotImplementedError, got none")
        
        class D(type): pass
            
        try:
            class E(D):
                def mro(self): pass
        except NotImplementedError: pass
        else: self.fail("Expected NotImplementedError, got none")

    def test_type_mro(self):
        self.assertRaises(TypeError, type.mro)
        self.assertEqual(object.mro(), list(object.__mro__))
        self.assertEqual(type(object.mro()), list)
        self.assertEqual(type(object.__mro__), tuple)

    def test_derived_tuple_eq(self):
        # verify overriding __eq__ on tuple still allows us to call the super version
        class bazbar(tuple):
            def __eq__(self,other):
                other = bazbar(other)
                return super(bazbar,self).__eq__(other)
        self.assertEqual(bazbar('abc'), 'abc')
    
    def test_new_old_slots(self):
        class N(object): pass
        class O: pass
        class C(N, O):
            __slots__ = ['a','b']


    def test_slots_counter(self):
        import gc
        class Counter(object):
            c = 0
            def __init__(self):
                Counter.c += 1
            def __del__(self):
                Counter.c -= 1
        def testit():
            class C(object):
                __slots__ = ['a', 'b', 'c']
        
            x = C()
            x.a = Counter()
            x.b = Counter()
            x.c = Counter()
        
            self.assertEqual(Counter.c, 3)
            del x
        
        testit()
        gc.collect()
        self.assertEqual(Counter.c, 0)

    def test_override_container_contains(self):
        for x in (dict, list, tuple):
            class C(x):
                def __contains__(self, other):
                    return other == "abc"
                    
            self.assertEqual('abc' in C(), True)

    def test_override_container_len(self):
        for x in (dict, list, tuple):
            class C(x):
                def __len__(self): return 2
            
            self.assertEqual(C().__len__(), 2)
            self.assertEqual(len(C()), 2)
            
            self.assertEqual(C(), x())
            
            if x is dict:
                self.assertEqual(C({1:1}), {1:1})
                d = {1:1, 2:2, 3:3}
                self.assertEqual(C(d).__cmp__({0:0, 1:1, 2:2}), 1)
                d[4] = 4
                self.assertEqual(len(list(C(d).keys())), len(list(d.keys())))
            else:
                self.assertEqual(C([1]), x([1]))
                a = list(range(4))
                self.assertEqual(len(list(iter(C(a)))), len(list(iter(x(a)))))

    #TODO:@skip("multiple_execute") #http://www.codeplex.com/IronPython/WorkItem/View.aspx?WorkItemId=17551        
    def test_dictproxy_access(self):
        def f():
            int.__dict__[0] = 0
            
        self.assertRaises(TypeError, f)
        
        class KOld: pass
        class KNew(object): pass
        
        #CodePlex 16001
        self.assertEqual(int.__dict__.get(0, 'abc'), 'abc')
        self.assertEqual(int.__dict__.get('__new__'), int.__new__)
        self.assertEqual(KOld.__dict__.get(0, 'abc'), 'abc')
        self.assertEqual(KNew.__dict__.get(0, 'abc'), 'abc')
        self.assertEqual(KNew.__dict__.get('__new__'), None)
        #CodePlex 15702
        self.assertEqual(int.__dict__.copy(), dict(int.__dict__))
        self.assertEqual(int.__class__.__dict__.copy(), dict(int.__class__.__dict__))
        self.assertEqual(KOld.__dict__.copy(), dict(KOld.__dict__))
        self.assertEqual(KNew.__dict__.copy(), dict(KNew.__dict__))
        #Dev10 4844754
        self.assertEqual(KNew.__class__.__dict__.copy(), dict(KNew.__class__.__dict__))
        
        self.assertEqual(set(KNew.__dict__.items()), set(dict(KNew.__dict__).items()))
        self.assertEqual(set(KNew.__dict__.keys()), set(dict(KNew.__dict__).keys()))
        self.assertEqual(set(KNew.__dict__.values()), set(dict(KNew.__dict__).values()))
        
        for value in [None, 'abc', 1, object(), KNew(), KOld(), KNew, KOld, property(lambda x: x)]:
            class KNew(object):
                abc = value
                
            self.assertEqual(KNew.__dict__['abc'], value)
            self.assertEqual(KNew.__dict__.get('abc'), value)
            self.assertEqual(KNew.__dict__.get('abc', value), value)
            for items in iter(KNew.__dict__.items()), list(KNew.__dict__.items()), list(zip(list(KNew.__dict__.keys()), list(KNew.__dict__.values()))):
                for k, v in items:
                    if k == 'abc':
                        self.assertEqual(v, value)
    
    #@unittest.expectedFailure('Currently throws a StackOverflowException')
    @unittest.skip('Currently throws a StackOverflowException')
    def test_getattribute_getattr(self):
        # verify object.__getattribute__(name) will call __getattr__
        class Base(object):
            def __getattribute__(self, name):
                return object.__getattribute__(self, name)
        
        class Derived(Base):
            def __getattr__(self, name):
                if name == "bar": return 23
                raise AttributeError(name)
            def __getattribute__(self, name):
                return Base.__getattribute__(self, name)
        
        a = Derived()
        self.assertEqual(a.bar, 23)

        # getattr doesn't get called when calling object.__getattribute__
        class x(object):
            def __getattr__(self, name): return 23

        self.assertRaises(AttributeError, object.__getattribute__, x(), 'abc')

        # verify __getattr__ gets called after __getattribute__ fails, not
        # during the base call to object.
        state = []
        class Derived(object):
            def __getattr__(self, name):
                if name == "bar":
                    self.assertEqual(state, [])
                    return 23
                raise AttributeError(name)
            def __getattribute__(self, name):
                try:
                    state.append('getattribute')
                    return object.__getattribute__(self, name)
                finally:
                    self.assertEqual(state.pop(), 'getattribute')

        a = Derived()
        self.assertEqual(a.bar, 23)

    def test_dynamic_getattribute_getattr(self):
        class Base(object):
            pass 
        
        def __getattribute__(self, name):
            return object.__getattribute__(self, name)
        
        Base.__getattribute__ = __getattribute__
        
        class Derived(Base):
            pass
        
        def __getattr__(self, name):
            if name == "bar":
                return 23
            raise AttributeError(name)
        
        
        Derived.__getattr__ = __getattr__
        
        def __getattribute__(self, name):
            return Base.__getattribute__(self, name)
        
        Derived.__getattribute__ = __getattribute__
        
        a = Derived()
        self.assertEqual(a.bar, 23)

    def test_setattr(self):
        # verify defining __setattr__ works
        global setCalled
        
        class KNew(object):
            def __setattr__(self, name, value):
                global setCalled
                setCalled = True
                object.__setattr__(self, name, value)
                
        class KOld:
            def __setattr__(self, name, value):
                global setCalled
                setCalled = True
                self.__dict__[name] = value

        class KNewSub(KNew): pass
        
        class KOldSub(KOld): pass

        for K in [  KOld,
                    KOldSub, #CodePlex 8018
                    KNew,
                    KNewSub]:
            setCalled = False
            x = K()
            x.abc = 23
            self.assertEqual(x.abc, 23)
            self.assertEqual(setCalled, True)

    def test_dynamic_getattribute(self):
        # verify adding / removing __getattribute__ succeeds
        
        # remove
        class foo(object):
            def __getattribute__(self, name):
                raise Exception()
        
        class bar(foo):
            def __getattribute__(self, name):
                return super(bar, self).__getattribute__(name)
        
        del foo.__getattribute__
        a = bar()
        a.xyz = 'abc'
        self.assertEqual(a.xyz, 'abc')
        
        # add
        class foo(object): pass

        def getattr(self, name):
            if name == 'abc': return 42
        
        foo.__getattribute__ = getattr
        
        self.assertEqual(foo().abc, 42)

    def test_nonstring_name(self):
        global __name__
        
        name = __name__
        try:
            __name__ = 3
            class C: pass
            
            self.assertEqual(C.__module__, 3)

            class C(object): pass
            
            self.assertEqual(C.__module__, 3)
            
            class C(type): pass
            
            self.assertEqual(C.__module__, 3)
            
            class D(object, metaclass=C):
                pass
                
            self.assertEqual(D.__module__, 3)
        finally:
            __name__ = name
    
    def test_dictproxy_descrs(self):
        # verify that we get the correct descriptors when we access the dictionary proxy directly
        class foo(object):
            xyz = 'abc'

        self.assertEqual(foo.__dict__['xyz'], 'abc')
        
        class foo(object):
            def __get__(self, instance, context):
                return 'abc'

        class bar(object):
            xyz = foo()

        self.assertEqual(bar.__dict__['xyz'].__class__, foo)

## __int__

    def test_fastnew_int(self):
        class C1:
            def __int__(self): return 100
        class C2:
            def __int__(self): return myint(100)
        class C3:
            def __int__(self): return 100
        class C4:
            def __int__(self): return mylong(100)
        class C5:
            def __int__(self): return -123456789012345678910
        class C6:
            def __int__(self): return C6()
        class C7:
            def __int__(self): return "100"
        
        for x in [C1, C2, C3, C4]:   self.assertEqual(int(x()), 100)
        self.assertEqual(int(C5()), -123456789012345678910)
        for x in [C6, C7]:      self.assertRaises(TypeError, int, x())
            
        class C1(object):
            def __int__(self): return 100
        class C2(object):
            def __int__(self): return myint(100)
        class C3(object):
            def __int__(self): return 100
        class C4(object):
            def __int__(self): return mylong(100)
        class C5(object):
            def __int__(self): return -123456789012345678910
        class C6(object):
            def __int__(self): return C6()
        class C7(object):
            def __int__(self): return "100"
        
        for x in [C1, C2, C3, C4]:   self.assertEqual(int(x()), 100)
        self.assertEqual(int(C5()), -123456789012345678910)
        for x in [C6, C7]:      self.assertRaises(TypeError, int, x())


    def test_type_type_is_type(self):
        class OS: pass
        class NS(object): pass
        
        true_values = [type, NS, int, float, tuple, str]
        if is_cli:
            import System
            true_values += [System.Boolean, System.Int32, System.Version, System.Exception]
        
        for x in true_values:
            self.assertTrue(type(x) is type)
            
        false_values = [OS]
        if is_cli:
            false_values += [ System.Boolean(1), System.Int32(3), System.Version(0, 0), System.Exception() ]
            
        for x in false_values:
            self.assertTrue(type(x) is not type)
    

    def test_hash_return_values(self):
        # new-style classes do conversion to int
        for retval in [1, 1.0, 1.1, 1, 1<<30]:
            for type in [object, int, str, float, int]:
                class foo(object):
                    def __hash__(self): return retval
                
                self.assertEqual(hash(foo()), int(retval))
        
        # old-style classes require int or long return value
        for retval in [1.0, 1.1]:
            class foo:
                def __hash__(self): return retval
            
            self.assertRaises(TypeError, hash, foo())

        tests = {   1:1,
                    2:2,
                }

        for retval in list(tests.keys()):
            class foo:
                def __hash__(self): return retval
            
            self.assertEqual(hash(foo()), tests[retval])

    def test_cmp_notimplemented(self):
        class foo(object):
            def __eq__(self, other):
                ran.append('foo.eq')
                return NotImplemented
            def __ne__(self, other):
                ran.append('foo.ne')
                return NotImplemented
            def __le__(self, other):
                ran.append('foo.le')
                return NotImplemented
            def __lt__(self, other):
                ran.append('foo.lt')
                return NotImplemented
            def __gt__(self, other):
                ran.append('foo.gt')
                return NotImplemented
            def __ge__(self, other):
                ran.append('foo.ge')
                return NotImplemented
            def __cmp__(self, other):
                ran.append('foo.cmp')
                return NotImplemented
        
        
        class bar:
            def __eq__(self, other):
                ran.append('bar.eq')
                return NotImplemented
            def __ne__(self, other):
                ran.append('bar.ne')
                return NotImplemented
            def __le__(self, other):
                ran.append('bar.le')
                return NotImplemented
            def __lt__(self, other):
                ran.append('bar.lt')
                return NotImplemented
            def __gt__(self, other):
                ran.append('bar.gt')
                return NotImplemented
            def __ge__(self, other):
                ran.append('bar.ge')
                return NotImplemented
            def __cmp__(self, other):
                ran.append('bar.cmp')
                return NotImplemented

        ran = []
        cmp(foo(), bar())
        #self.assertEqual(ran, ['foo.eq', 'bar.eq', 'foo.lt', 'bar.gt', 'foo.gt', 'bar.lt', 'bar.cmp'])
        
        ran = []
        cmp(foo(), foo())
        #self.assertEqual(ran, ['foo.cmp', 'foo.cmp'])
        
        ran = []
        cmp(bar(), bar())
        #self.assertEqual(ran, ['bar.cmp', 'bar.cmp', 'bar.eq', 'bar.eq', 'bar.eq', 'bar.eq', 'bar.lt', 'bat.gt', 'bar.gt', 'bar.lt', 'bar.gt', 'bar.lt', 'bar.lt', 'bar.gt', 'bar.cmp', 'bar.cmp'])
        
        ran = []
        cmp(foo(), 1)
        #self.assertEqual(ran, ['foo.eq', 'foo.lt', 'foo.gt', 'foo.cmp'])


    def test_override_repr(self):
        class KOld:
            def __repr__(self):
                return "old"
                
        class KNew(object):
            def __repr__(self):
                return "new"
                
        self.assertEqual(repr(KOld()), "old")
        self.assertEqual(str(KOld()), "old")
        self.assertEqual(repr(KNew()), "new")
        #IP breaks here because __str__ in new style classes does not call __repr__
        self.assertEqual(str(KNew()), "new")


    def test_mutate_base(self):
            class basetype(object):
                xyz = 3
            
            class subtype(basetype): pass
            
            self.assertEqual(subtype.xyz, 3)
            self.assertEqual(subtype().xyz, 3)
            
            basetype.xyz = 7
            
            self.assertEqual(subtype.xyz, 7)
            self.assertEqual(subtype().xyz, 7)

    def test_mixed_newstyle_oldstyle_init(self):
        """mixed new-style & old-style class should run init if its defined in the old-style class"""
        class foo:
            def __init__(self):
                self.x = 3
        
        class bar(foo):
            def __init__(self):
                self.x = 4
        
        class baz(foo): pass
        
        class ns(object): pass
        
        class full(bar, baz, ns): pass
        
        a = full()
        self.assertEqual(a.x, 4)
        
        class full(bar, baz, ns):
            def __init__(self):
                self.x = 5
        
        a = full()
        self.assertEqual(a.x, 5)

        class ns(object):
            def __init__(self):
                self.x = 6
        
        class full(bar, baz, ns): pass
        a = full()
        self.assertEqual(a.x, 4)

    def test_mixed_newstyle_oldstyle_new(self):
        class S:
            pass
        
        class P(S, object):
            def __new__(cls, *a, **kw):
                return object.__new__(cls)
        
        self.assertEqual(type(P()), P)

    def test_mixed_newstyle_oldstyle_descriptor(self):
        class base:
            @classmethod
            def f(cls):
                    return cls
        
        
        class x(base, object):
            pass
        
        self.assertEqual(x.f(), x)

    def test_getattr_exceptions(self):
        """verify the original exception propagates out"""
        class AttributeTest(object):
            def __getattr__(self, name): raise AttributeError('catch me')
            
        x = AttributeTest()
        try:
            y = x.throws
        except AttributeError as ex:
            self.assertEqual(ex.args, ('catch me',))
        else: Fail("should have thrown")

    def test_descriptor_meta_magic(self):
        class valueDescriptor(object):
            def __init__(self,x=None): self.value = x
            def __get__(self,ob,cls):   return self.value
            def __set__(self,ob,x):     self.value = x
        
        class Ameta(type):
            def createShared( cls, nm, initValue=None ):
                o = valueDescriptor(initValue)
                setattr( cls,nm, o )
                setattr( cls.__class__,nm, o )
        
        class A(metaclass=Ameta):
            pass
        
        class B( A ):
            A.createShared("cls2",1)
        
        def test(value):
            self.assertEqual(o.cls2, value)
            self.assertEqual(o2.cls2, value)
            self.assertEqual(A.cls2, value)
            self.assertEqual(B.cls2, value)
            
        o = A()
        o2 = B()
        test(1)
            
        B.cls2 = 2
        test(2)
            
        A.cls2 = 3
        test(3)
        
        o.cls2 = 4
        test(4)
        
        o2.cls2 = 5
        test(5)

    def test_missing_attr(self):
        class foo(object): pass
        
        a = foo()
        def f(): a.dne
        self.assertRaisesMessage(AttributeError, "'foo' object has no attribute 'dne'", f)

    def test_method(self):
        class tst_oc:
            def root(): return 2

        class tst_nc:
            def root(): return 2

        self.assertRaises(TypeError, tst_oc.root)
        self.assertRaises(TypeError, tst_nc.root)
        
        instancemethod = type(tst_oc.root)
        self.assertRaises(TypeError, instancemethod, lambda x:True, None)

    def test_descriptors_custom_attrs(self):
        """verifies the interaction between descriptors and custom attribute access works properly"""
        class mydesc(object):
            def __get__(self, instance, ctx):
                raise AttributeError
        
        class f(object):
            x = mydesc()
            def __getattr__(self, name): return 42
        
        self.assertEqual(f().x, 42)

    def test_cp5801(self):
        class Foo(object):
            __slots__ = ['bar']
            def __getattr__(self, n):
                return n.upper()
            
        foo = Foo()
        self.assertEqual(foo.bar, "BAR")


    def test_property_always_set_descriptor(self):
        """verifies that set descriptors take precedence over dictionary entries and
        properties are always treated as set descriptors, even if they have no
        setter function"""
        
        class C(object):
            x = property(lambda self: self._x)
            def __init__(self):
                self._x = 42
        
        
        c = C()
        c.__dict__['x'] = 43
        self.assertEqual(c.x, 42)

        # now check a user get descriptor
        class MyDescriptor(object):
            def __get__(self, *args): return 42
            
        class C(object):
            x = MyDescriptor()
            
        c = C()
        c.__dict__['x'] = 43
        self.assertEqual(c.x, 43)
        
        # now check a user get/set descriptor
        class MyDescriptor(object):
            def __get__(self, *args): return 42
            def __set__(self, *args): pass
            
        class C(object):
            x = MyDescriptor()
            
        c = C()
        c.__dict__['x'] = 43
        self.assertEqual(c.x, 42)

    def test_object_as_condition(self):
        class C(object):
            def __mod__(self, other): return 1
        o = C()
        flag = 0
        if o % o: flag = 1   # the bug was causing cast error before
        self.assertEqual(flag, 1)

    def test_unbound_class_method(self):
        class C(object):
            def f(): return 1
        
        x = C()
        self.assertRaisesPartialMessage(TypeError, "unbound method f() must be called with", lambda: C.f())
        self.assertRaisesPartialMessage(TypeError, "unbound method f() must be called with", lambda: C.f(C))
        self.assertRaisesPartialMessage(TypeError, "arguments (1 given)", lambda: C.f(x))
        self.assertRaisesPartialMessage(TypeError, "arguments (1 given)", lambda: x.f())

    def test_oldinstance_operator_exceptions(self):
        global called
        def fmaker(name, ex = None):
            def f(self, *args):
                global called
                called.append(name)
                if ex:
                    raise ex(name)
                def g(*args):
                    return NotImplemented
                return g
            return f

        def fthrowingmaker(name, ex):
            def f(self):
                global called
                called.append(name)
                def g(*args):
                    raise ex
                return g
            return f

        class OC:
            __eq__ = property(fmaker('oc_eq', AttributeError))
            __ne__ = property(fmaker('oc_ne', AttributeError))
        
        class OC2:
            __eq__ = property(fthrowingmaker('oc_eq', AttributeError))
            __ne__ = property(fthrowingmaker('oc_ne', AttributeError))

        class OC3:
            def __getattr__(self, name):
                return property(fmaker(name, AttributeError)).__get__(self, OC3)
        
        called = []
        self.assertTrue(not OC() == OC())
        self.assertEqual(called, ['oc_eq']*4)
        
        called = []
        type(OC()).__eq__(OC(), OC())
        self.assertEqual(called, ['oc_eq']*2)
        
        called =[]
        self.assertTrue(OC() != OC())
        self.assertEqual(called, ['oc_ne']*4)

        called = []
        type(OC()).__ne__(OC(), OC())
        self.assertEqual(called, ['oc_ne']*2)


        called = []
        self.assertRaises(AttributeError, lambda : not OC2() == OC2())
        self.assertEqual(called, ['oc_eq'])
        
        called = []
        self.assertRaises(AttributeError, lambda : type(OC2()).__eq__(OC2(), OC2()))
        self.assertEqual(called, ['oc_eq'])
        
        called =[]
        self.assertRaises(AttributeError, lambda : OC2() != OC2())
        self.assertEqual(called, ['oc_ne'])

        called = []
        self.assertRaises(AttributeError, lambda : type(OC2()).__ne__(OC2(), OC2()))
        self.assertEqual(called, ['oc_ne'])
        
        called = []
        self.assertRaises(AttributeError, lambda : type(OC()).__getattribute__(OC(), '__eq__'))
        self.assertEqual(called, ['oc_eq'])
        
        self.assertTrue(not hasattr(OC(), '__eq__'))

        # IronPython still differs on these from CPython:
        # verify other attributes work correctly
        #for x in ['__abs__', '__float__', '__long__', '__int__', '__hex__', '__oct__', '__pos__', '__neg__', '__invert__']:
        #    # unary operators which pass on AttributeError
        #    print x
        #    self.assertRaises(AttributeError, getattr(type(OC3()), x), OC3())
        
        for x in ['__hash__', '__nonzero__', '__str__', '__repr__']:
            # unary operators that catch AttributeError
            getattr(type(OC3()), x)(OC3())
        
        # IronPython still differs on these from CPython:
        #for x in ['__iter__']:
        #    # unary operators that call, catch, and report another error
        #    called = []
        #    self.assertRaises(TypeError, getattr(type(OC3()), x), OC3())
        #    self.assertTrue(x in called)
        
        for x in ['__add__', '__iadd__', '__radd__', '__cmp__', '__coerce__']:
            # binary operators catch AttributeError
            getattr(type(OC3()), x)(OC3(), OC3())
        

    def test_cp10291(self):
        class K1(object):
            def __call__(self):
                return "K1"
        
        class K2(K1):
            def __call__(self):
                return "K2" + K1.__call__(self)

        class K1Old:
            def __call__(self):
                return "K1"
        
        class K2Old(K1Old):
            def __call__(self):
                return "K2" + K1Old.__call__(self)


        for k1, k2 in [ (K1, K2),
                        (K1Old, K2Old),
                        ]:
            self.assertEqual(k1()(), "K1")
            self.assertEqual(k2()(), "K2K1")

    @unittest.skipUnless(is_cli, 'IronPython specific test') # should be reenabled against CPython26
    def test_cp11760(self):
        class KNew(object):
            def __str__(self): return "KNew"
        
        class KOld:
            def __str__(self): return "KOld"
        
        for K in [KNew, KOld]:
            dir_str = dir(K().__str__)
            for x in [  '__class__', '__delattr__', '__doc__',
                        '__get__', '__getattribute__', '__hash__', '__init__',
                        '__new__', '__reduce__', '__reduce_ex__', '__repr__',
                        '__setattr__', '__str__', 'im_class',
                        'im_func', '__func__', 'im_self', '__self__',
                        #'__call__', '__cmp__',
                        ]:
                self.assertTrue(x in dir_str, x + " is not in dir(K().__str__)")

    def test_delattr(self):
        global called
        class X(object):
            def __delattr__(self, name):
                global called
                called = True
        
        del X().abc
        
        self.assertTrue(called)
  
    def test_cp10709(self):
        class KNew(object):
            p1 = property(lambda self: 3.14)
            m1 = lambda self: 3.15
            f1 = lambda: 3.16
            def m2(self, a):
                x = lambda: 3.17
                return x()
                
        class KOld:
            p1 = property(lambda self: 3.14)
            m1 = lambda self: 3.15
            f1 = lambda: 3.16
            def m2(self, a):
                x = lambda: 3.17
                return x()
                
        for temp in dir(KNew) + dir(KNew()) + dir(KOld) + dir(KOld()):
            self.assertTrue("lambda" not in temp)
        
    def test_oldstyle_fancycallable(self):
        class C : pass
            
        x = C(*())
        self.assertTrue(x.__class__ is C)
        x = C(**{})
        self.assertTrue(x.__class__ is C)
        x = C(*(), **{})
        self.assertTrue(x.__class__ is C)
        
        class C:
            def __init__(self, a):
                pass
        
        x = C(*(2,))
        #Merlin 382112
        x = C(*(None,))
        self.assertTrue(x.__class__ is C)
        x = C(**{'a': None})

    def test_oldclass_newclass_construction(self):
        """calling __new__ on and old-class passing in new-classes should result in a new-style type"""
        class nc(object): pass
        
        class oc: pass
        
        newType = type(oc).__new__(type(oc), 'foo', (nc, oc), {})
        self.assertEqual(type(newType), type)

    def test_inherited_getattribute(self):
        """inherited getattribute should be respected"""
        class x(object):
            def __getattribute__(self, name):
                if name == 'abc': return 42
                return object.__getattribute__(self, name)
        
        class y(x): pass

        self.assertEqual(y().abc, 42)


    def test_cp13820(self):
        global GETATTRIBUTE_CALLED
        GETATTRIBUTE_CALLED = False
        
        class KOld:
            def __getattribute__(self, name):
                global GETATTRIBUTE_CALLED
                GETATTRIBUTE_CALLED = True
                print("__getattribute__ was called by:", name)
                return 1
                
            def __init__(self):
                return
                
            def __del__(self):
                return
                
            def __str__(self): return ""
            
            def __cmp__(self, other): return 0
            
            def __hash__(self): return 1
            
            def __bool__(self): return 1
            
            def __get__(self, instance, owner): return 3
            
            def __delete__(self, instance): return
            
            def __len__(self): return 4
            
            def __getitem__(self, key): return 5
            
            def __setitem__(self, key, value): return
            
            def  __getslice__(self, i, j): return 6
            
            def __contains__(self, obj): return True
            
            def __add__(self, other): return 7
                
            def __hex__(self): return hex(9)
            
            def __coerce__(self, other): return None
            
            def __lt__(self, other): return True
        
        class KNew(object):
            def __getattribute__(self, name):
                global GETATTRIBUTE_CALLED
                GETATTRIBUTE_CALLED = True
                print("__getattribute__ was called by:", name)
                return 1
                
            def __init__(self):
                return
                
            def __del__(self):
                return
                
            def __str__(self): return ""
            
            def __cmp__(self, other): return 0
            
            def __hash__(self): return 1
            
            def __bool__(self): return 1
            
            def __get__(self, instance, owner): return 3
            
            def __delete__(self, instance): return
            
            def __len__(self): return 4
            
            def __getitem__(self, key): return 5
            
            def __setitem__(self, key, value): return
            
            def  __getslice__(self, i, j): return 6
            
            def __contains__(self, obj): return True
            
            def __add__(self, other): return 7
                
            def __hex__(self): return hex(9)
            
            def __coerce__(self, other): return None
            
            def __lt__(self, other): return True
            
        for K in [KOld, KNew]:
            obj = K()
            str(obj)
            obj==3
            hash(obj)
            bool(obj)
            obj[3]
            obj[3] = 4
            len(obj)
            obj[1:3]
            if K==KOld: hasattr(obj, "abc")
            obj + 3
            hex(obj)
            obj<9
            del obj
            self.assertTrue(not GETATTRIBUTE_CALLED)

    def test_keyword_type_construction(self):
        """using type.__call__ should accept keyword arguments"""
        class x(object):
            def __new__(cls, *args, **kwargs):
                return object.__new__(cls)
            def __init__(self, *args, **kwargs):
                for x, y in kwargs.items():
                    setattr(self, x, y)
                return object.__init__(self)
        
        obj = type.__call__(x, *(), **{'abc':2})
        self.assertEqual(obj.abc, 2)
        
        obj = x.__call__(*(), **{'abc':3})
        self.assertEqual(obj.abc, 3)

    def test_mixed_mro_respected(self):
        """creates a class with an mro of "MC, NC, OC2, NC2, object, OC" and verifies that we get NC2 member, not OC"""
        class OC:
            abc = 3
        
        class OC2(OC):
            pass
        
        class NC(object):
            pass
        
        class NC2(object):
            abc = 5
        
        class MC(NC, OC2, NC2, OC): pass
        
        self.assertEqual(MC.abc, 5)

    def test_descriptor_object_getattribute_interactions(self):
        class nondata_desc(object):
            def __init__(self, value): self.value = value
            def __get__(self, inst, ctx = None):
                return (self.value, inst, ctx)
        
        class data_desc(object):
            def __init__(self, value): self.value = value
            def __get__(self, inst, ctx = None):
                return (self.value, inst, ctx)
            def __set__(self, inst, value):
                self.value = value
            def __delete__(self, inst):
                del self.value
        
        class readonly_data_desc(object):
            def __init__(self, value): self.value = value
            def __get__(self, inst, ctx = None):
                return (self.value, inst, ctx)
            def __set__(self, inst, value):
                raise AttributeError()
            def __delete__(self, inst):
                raise AttributeError()
        
        class meta(type):
            nondata = nondata_desc(1)
            data = data_desc(2)
            nondata_shadowed_class = nondata_desc(3)
            data_shadowed_class = data_desc(4)
            nondata_shadowed_inst = nondata_desc(5)
            data_shadowed_inst = data_desc(6)
            ro_data = readonly_data_desc(7)
            ro_shadowed_class = readonly_data_desc(8)
            ro_shadowed_inst = readonly_data_desc(9)
        
        class x(object, metaclass=meta):
            def __init__(self):
                self.nondata_shadowed_inst = "nondata_inst"
                self.data_shadowed_inst = "data_inst"
                self.ro_shadowed_inst = 'ro_inst'
            nondata_shadowed_class = 'nondata_shadowed_class'
            data_shadowed_class = 'data_shadowed_class'
            ro_shadowed_class = 'ro_shadowed_class'
            
        a = x()
        self.assertRaises(AttributeError, object.__getattribute__, a, 'nondata')
        self.assertRaises(AttributeError, object.__getattribute__, a, 'data')
        self.assertRaises(AttributeError, object.__getattribute__, a, 'ro_data')
        
        self.assertEqual(object.__getattribute__(a, 'nondata_shadowed_class'), 'nondata_shadowed_class')
        self.assertEqual(object.__getattribute__(a, 'data_shadowed_class'), 'data_shadowed_class')
        self.assertEqual(object.__getattribute__(a, 'ro_shadowed_class'), 'ro_shadowed_class')
        
        self.assertEqual(object.__getattribute__(a, 'nondata_shadowed_inst'), 'nondata_inst')
        self.assertEqual(object.__getattribute__(a, 'data_shadowed_inst'), 'data_inst')
        self.assertEqual(object.__getattribute__(a, 'ro_shadowed_inst'), 'ro_inst')
        
        self.assertEqual(object.__getattribute__(x, 'nondata_shadowed_class'), 'nondata_shadowed_class')
        self.assertEqual(object.__getattribute__(x, 'data_shadowed_class'), (4, x, meta))
        self.assertEqual(object.__getattribute__(x, 'ro_shadowed_class'), (8, x, meta))
        
        self.assertEqual(object.__getattribute__(x, 'nondata_shadowed_inst'), (5, x, meta))
        self.assertEqual(object.__getattribute__(x, 'data_shadowed_inst'), (6, x, meta))
        self.assertEqual(object.__getattribute__(x, 'ro_shadowed_inst'), (9, x, meta))

        self.assertEqual(object.__getattribute__(x, 'ro_data'), (7, x, meta))
        self.assertEqual(object.__getattribute__(x, 'nondata'), (1, x, meta))
        self.assertEqual(object.__getattribute__(x, 'data'), (2, x, meta))

    def test_cp5803(self):

        #--Simple
        class KSimple(object):
            def __radd__(self, other):
                return
                
        for x in ["", 1, 3.14, None, "stuff", object, KSimple]:
            self.assertEqual(x + KSimple(), None)
        self.assertRaisesPartialMessage(TypeError,
                                    "unsupported operand type(s) for +: 'KSimple' and 'str'",
                                    lambda: KSimple() + "")

        #--Addition
        class K(object): pass

        class K0(object):
            def __radd__(self, other):
                return "__radd__:" + str(type(self)) + " " + str(type(other))

        class K1(object):
            def __radd__(self, other):
                return "__radd__:" + str(type(self)) + " " + str(type(other))
                
            def __add__(self, other):
                return "__add__:" + str(type(self)) + " " + str(type(other))
        
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'K' and 'K'", lambda: K() + K())
        self.assertEqual(K() + K0(), "__radd__:<class '" + __name__ + ".K0'> <class '" + __name__ + ".K'>")
        self.assertEqual(K() + K1(), "__radd__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K'>")
        
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'K0' and 'K'", lambda: K0() + K())
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'K0' and 'K0'", lambda: K0() + K0())
        self.assertEqual(K0() + K1(), "__radd__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K0'>")

        self.assertEqual(K1() + K(),  "__add__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K'>")
        self.assertEqual(K1() + K0(), "__add__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K0'>")
        self.assertEqual(K1() + K1(), "__add__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K1'>" )
        
        #--Subtraction
        class K(object): pass

        class K0(object):
            def __rsub__(self, other):
                return "__rsub__:" + str(type(self)) + " " + str(type(other))

        class K1(object):
            def __rsub__(self, other):
                return "__rsub__:" + str(type(self)) + " " + str(type(other))
                
            def __sub__(self, other):
                return "__sub__:" + str(type(self)) + " " + str(type(other))
        
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for -: 'K' and 'K'", lambda: K() - K())
        self.assertEqual(K() - K0(), "__rsub__:<class '" + __name__ + ".K0'> <class '" + __name__ + ".K'>")
        self.assertEqual(K() - K1(), "__rsub__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K'>")
        
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for -: 'K0' and 'K'", lambda: K0() - K())
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for -: 'K0' and 'K0'", lambda: K0() - K0())
        self.assertEqual(K0() - K1(), "__rsub__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K0'>")

        self.assertEqual(K1() - K(),  "__sub__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K'>")
        self.assertEqual(K1() - K0(), "__sub__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K0'>")
        self.assertEqual(K1() - K1(), "__sub__:<class '" + __name__ + ".K1'> <class '" + __name__ + ".K1'>" )

        #--Old style
        class K: pass

        class K0:
            def __radd__(self, other):
                return "__radd__:" + str(type(self)) + " " + str(type(other))

        class K1:
            def __radd__(self, other):
                return "__radd__:" + str(type(self)) + " " + str(type(other))
                
            def __add__(self, other):
                return "__add__:" + str(type(self)) + " " + str(type(other))
        
        self.assertRaises(TypeError, lambda: K() + K())
        self.assertEqual(K() + K0(), "__radd__:<type 'instance'> <type 'instance'>")
        self.assertEqual(K() + K1(), "__radd__:<type 'instance'> <type 'instance'>")
        
        self.assertRaises(TypeError, lambda: K0() + K())
        self.assertEqual(K0() + K0(), "__radd__:<type 'instance'> <type 'instance'>")
        self.assertEqual(K0() + K1(), "__radd__:<type 'instance'> <type 'instance'>")

        self.assertEqual(K1() + K(),  "__add__:<type 'instance'> <type 'instance'>")
        self.assertEqual(K1() + K0(), "__add__:<type 'instance'> <type 'instance'>")
        self.assertEqual(K1() + K1(), "__add__:<type 'instance'> <type 'instance'>")

    def test_special_type_attributes(self):
        # some attributes on new-style class are alwayed retrieved
        # from the type, not the classes dictionary
        class x(object):
            __dict__ = 'abc'
            __class__ = 'abc'
            __bases__ = 'abc'
            __name__ = 'abc'
            
        class y(object): pass
        
        self.assertEqual(type(x.__dict__), type(y.__dict__))
        self.assertEqual(x.__class__, type)
        self.assertEqual(x.__bases__, (object, ))
        self.assertEqual(x.__name__, 'x')


    def test_issubclass(self):
        # first argument doesn't need to be new-style or old-style class if it defines __bases__
        class C(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (object, )
                return object.__getattribute__(self, name)
        
        class S(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (x, )
                return C.__getattribute__(self, name)
        
        x = C()
        self.assertEqual(issubclass(S(), x), True)
        self.assertEqual(issubclass(S(), (x, )), True)

        # if arg 1 doesn't have __bases__ a TypeError is raised
        class S(object):
            pass 
        
        x = C()
        self.assertRaisesMessage(TypeError, "issubclass() arg 1 must be a class", issubclass, S(), x)
        self.assertRaisesMessage(TypeError, "issubclass() arg 1 must be a class", issubclass, S(), (x, ))
    
        # arg 1 __bases__ must be a tuple
        for arg1 in [[2, 3, 4], 23, 'abc']:
            class S(object):
                def __getattribute__(self, name):
                    if name == "__bases__": return arg1
                    return C.__getattribute__(self, name)
            self.assertRaisesMessage(TypeError, "issubclass() arg 1 must be a class", issubclass, S(), x)
            self.assertRaisesMessage(TypeError, "issubclass() arg 1 must be a class", issubclass, S(), (x, ))


        # recursively check members returned from __bases__
        class S(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (y, )
                return C.__getattribute__(self, name)
        
        class A(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (x, )
                return C.__getattribute__(self, name)
        
        y = A()
        self.assertEqual(issubclass(S(), x), True)
        self.assertEqual(issubclass(S(), (x, )), True)
        
        # but ignore members that don't have __bases__ themselves, don't raise a TypeError
        class S(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (123, )
                return C.__getattribute__(self, name)
        
        self.assertEqual(issubclass(S(), x), False)
        self.assertEqual(issubclass(S(), (x, )), False)

        # if __bases__ returns a type we should fallback into subclass(type, typeinfo)
        class C(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (int, )
                return object.__getattribute__(self, name)

        self.assertEqual(issubclass(C(), object), True)
        self.assertEqual(issubclass(C(), (object, )), True)
        
        # if __bases__ returns an old-class we should fallback into subclass(oc, typeinfo)
        class OC1: pass
        
        class OC2(OC1): pass
        
        class C(object):
            def __getattribute__(self, name):
                if name == "__bases__": return (OC2, )
                return object.__getattribute__(self, name)

        self.assertEqual(issubclass(C(), OC1), True)
        self.assertEqual(issubclass(C(), (OC1, )), True)
        
        # raising an exception from __bases__ propagates out
        
        class C(object):
            def getbases(self):
                raise RuntimeError
            __bases__ = property(getbases)

        class S(C): pass

        self.assertRaises(RuntimeError, issubclass, C(), S())

        reclimit = sys.getrecursionlimit()
        if reclimit == sys.maxsize:
            sys.setrecursionlimit(1001)
            
        # Make sure that calling isinstance with a deeply nested tuple for its
        # argument will raise RuntimeError eventually.
        def blowstack(fxn, arg, compare_to):
            tuple_arg = (compare_to,)
            for cnt in range(sys.getrecursionlimit()+5):
                tuple_arg = (tuple_arg,)
                fxn(arg, tuple_arg)
        
        self.assertRaises(RuntimeError, blowstack, issubclass, str, str)

        sys.setrecursionlimit(reclimit)

    def test_isinstance_recursion(self):
        reclimit = sys.getrecursionlimit()
        if reclimit == sys.maxsize:
            sys.setrecursionlimit(1001)

        # Make sure that calling isinstance with a deeply nested tuple for its
        # argument will raise RuntimeError eventually.
        def blowstack(fxn, arg, compare_to):
            tuple_arg = (compare_to,)
            for cnt in range(sys.getrecursionlimit()+5):
                tuple_arg = (tuple_arg,)
                fxn(arg, tuple_arg)
        
        self.assertRaises(RuntimeError, blowstack, isinstance, '', str)

        sys.setrecursionlimit(reclimit)

    def test_call_recursion(self):
        reclimit = sys.getrecursionlimit()
        if reclimit == sys.maxsize:
            sys.setrecursionlimit(1001)
        
        class A(object): pass

        A.__call__ = A()
        self.assertRaises(RuntimeError, A())
        sys.setrecursionlimit(reclimit)
    
    def test_metaclass_base_search(self):
        class MetaClass(type):
            def __init__(cls, clsname, bases, dict):
                setattr(cls, "attr_%s" % clsname, "attribute set on %s by MetaClass" % clsname)
                super(MetaClass, cls).__init__(clsname, bases, dict)
        
        class Mixin(object, metaclass=MetaClass):
            pass
        
        class Parent(object):
            pass
        
        class Child(Parent, Mixin):
            pass
            
        self.assertEqual(Child.attr_Child, 'attribute set on Child by MetaClass')

    def test_binary_operator_subclass(self):
        """subclassing but not overriding shouldn't call __radd__"""
        class A(object):
            def __add__(self, other):
                return ('a', self.__class__.__name__)
            __radd__ = __add__
        
        class B(A):
            def __add__(self, other):
                return ('b', self.__class__.__name__)
            __radd__ = __add__
        
        class C(A): pass
            
        a = A()
        b = B()
        c = C()
        self.assertEqual(a + b, ('b', 'B'))
        self.assertEqual(a + c, ('a', 'A'))

    def test_cp2021(self):
        class KOld:
            def __rmul__(self, other):
                return 7
        
        class KNew(object):
            def __rmul__(self, other):
                return 7
        
        for testdata in [[], [1], [1,2]]:
            self.assertEqual(testdata * KOld(), 7)
            self.assertEqual(testdata * KNew(), 7)
            self.assertRaisesMessage(TypeError, "object cannot be interpreted as an index",
                                testdata.__mul__, KOld())
            self.assertRaisesMessage(TypeError, "'KNew' object cannot be interpreted as an index",
                                testdata.__mul__, KNew())

    def test_redundant_multiple_bases(self):
        """specifying an extra base which is implied by a previous one should work ok"""
        class Foo(list, object):
            pass

        class Bar(Foo):
            pass
            
        self.assertEqual(Bar(), [])

    def test_metaclass_keyword_args(self):
        class MetaType(type):
            def __init__(cls, name, bases, dict):
                super(MetaType, cls).__init__(name, bases, dict)
        
        class Base(object, metaclass=MetaType):
            pass
        
        class A(Base):
            def __init__(self, a, b='b', c=12, d=None, e=None):
                self.d = d
                self.b = b
        
        a = A('empty', *(), **{'d': 'boom'})
        self.assertEqual(a.d, 'boom')

        a = A('empty', *('foo', ), **{'d': 'boom'})
        self.assertEqual(a.b, 'foo')
        self.assertEqual(a.d, 'boom')

    def test_oldinstance_creation(self):
        class C: pass
        
        inst = type(C())
        
        d = {'a': 2}
        i = inst(C, d)
        
        self.assertEqual(id(d), id(i.__dict__))
        self.assertTrue(isinstance(i, C))

        x = inst(C, None)
        self.assertEqual(x.__dict__, {})
    
    def test_metaclass_getattribute(self):
        class mc(type):
            def __getattr__(self, name):
                return 42
        
        class nc_ga(object, metaclass=mc):
            pass
            
        self.assertEqual(nc_ga.x, 42)

    def test_method_call(self):
        class K(object):
            def m(self, *args, **kwargs): 
                return args, kwargs
        
        self.assertEqual(K().m.__call__(), ((), {}))
        self.assertEqual(K().m.__call__(42), ((42, ), {}))
        self.assertEqual(K().m.__call__(42, x = 23), ((42, ), {'x': 23}))
        
        self.assertTrue('__call__' in dir(K().m))
    
    
    def test_metaclass_multiple_bases(self):
        global log
        log = []
        class C(object): pass
        
        class MT1(type):
            def __new__(cls, name, bases, dict):
                log.append('MT1')
                return super(MT1, cls).__new__(cls, name, bases, dict)
        
        class D(object, metaclass=MT1):
            pass
        
        self.assertEqual(log, ['MT1'])
        
        class MT2(type):
            def __new__(cls, name, bases, dict):
                log.append('MT2')
                return super(MT2, cls).__new__(cls, name, bases, dict)
        
        class E(object, metaclass=MT2):
            pass
        
        self.assertEqual(log, ['MT1', 'MT2'])
        class T1(C, D): pass    
        
        self.assertEqual(log, ['MT1', 'MT2', 'MT1'])
        
        def f(): 
            class T2(C, D, E): pass
            
        self.assertRaises(TypeError, f)
        
        self.assertEqual(log, ['MT1', 'MT2', 'MT1'])

    def test_del_getattribute(self): # 409747    
        class B(object): 
            def __getattribute__(self, name): pass
        
        class D(B): pass
        
        def f(): del D.__getattribute__  # AttributeError expected.
        self.assertRaises(AttributeError, f)

    def test_metaclass_oldstyle_only_bases(self):
        class C: pass
        
        self.assertRaises(TypeError, type, 'foo', (C, ), {})

    def test_bad_mro_error_message(self):
        class A(object): pass
        
        class B(A): pass
        
        self.assertRaisesPartialMessage(TypeError, "Cannot create a consistent method resolution\norder (MRO) for bases A, B",
                                    type, "X", (A,B), {})

    def test_finalizer(self):
        """returning the same object from __new__ shouldn't cause it to be finalized"""
        global val, called
        val = None
        called = False
        class X(object):
            def __new__(cls):
                global val
                if val == None:
                    val = object.__new__(cls)
                return val
            def __del__(self):
                called = True
        
        a = X()
        b = X()
        self.assertEqual(id(a), id(b))
        import gc
        gc.collect()
        self.assertEqual(called, False)

    def test_metaclass_attribute_lookup(self):
        class x(type):
            @property
            def Foo(self): return self._foo
            @Foo.setter
            def Foo(self, value): self._foo = value
        
        class y(metaclass=x):
            def Foo(self): return 42
            _foo = 0
        
        # data descriptor should lookup in meta class first.
        self.assertEqual(y.Foo, 0)
        
        class x(type):
            Foo = 42
        
        class y(metaclass=x):
            Foo = 0
            
        # non-data descriptors lookup in the normal class first
        self.assertEqual(y.Foo, 0)

    def test_len(self):
        class l(object):
            def __int__(self):
                return 42

        vals = (l(), 42, 42.0)
        if is_cli:
            from iptest.type_util import clr_all_types
            vals += tuple(t(42) for t in clr_all_types)
        
        for x in vals:
            class C(object):
                def __len__(self):
                    return x
            self.assertEqual(len(C()), 42)


    def test_descriptor_exception(self):
        class desc(object):
            def __get__(self, value, ctx):
                raise AttributeError('foo')
        
        class x(object):
            a = 42
        
        class y(x):
            a = desc()    
        
        self.assertRaisesMessage(AttributeError, 'foo', lambda: y().a)

        class y(object):
            a = desc()    
        
        self.assertRaisesMessage(AttributeError, 'foo', lambda: y().a)

    def test_mutate_descriptor(self):
        class desc(object):
            def __get__(self, value, ctx):
                return 42

        class x(object):
            a = desc()

        self.assertEqual(x().a, 42)
        desc.__get__ = lambda self, value, ctx: 23
        self.assertEqual(x().a, 23)

    def test_method_tuple_type(self):
        """creates a method who's type is declared to be a tuple"""
        class x(object):
            def f(self): pass
        
        def f(self): return self
        
        self.assertEqual(type(x.f)(f, None, (int, str))(42), 42)
        self.assertEqual(type(x.f)(f, None, (int, str))('abc'), 'abc')
        self.assertRaises(TypeError, type(x.f)(f, None, (int, str)), 1)

    def test_mutate_class(self):
        def f(): object.foo = 42
        def g(): type.foo = 42
        def h(): del type.foo
        def i(): del object.foo
        self.assertRaises(TypeError, f)
        self.assertRaises(TypeError, g)
        self.assertRaises(TypeError, h)
        self.assertRaises(TypeError, i)

    def test_wacky_new_init(self):
        global initCalled

        for base in [object, list]:
            for has_finalizer in [True, False]:
                class CustomInit(object):
                    def __get__(self, inst, ctx):
                        return self
                    def __call__(self, *args):
                        global initCalled
                        initCalled = 'CustomInit'
                
                class Base(base):
                    def __new__(self, toCreate):
                        return base.__new__(toCreate)
                    if has_finalizer:
                        def __del__(self): pass
                        
                class Sub(Base):
                    def __init__(self, *args):
                        global initCalled
                        initCalled = 'Sub'
                
                class NotSub(base):
                    def __init__(self, *args):
                        global initCalled
                        initCalled = 'NotSub'
                    
                class OC: pass
                
                class MixedSub(Base, OC):
                    def __init__(self, *args):
                        global initCalled
                        initCalled = 'MixedSub'
                
                class CustomSub(Base):
                    __init__ = CustomInit()
                
                Base(MixedSub)
                self.assertEqual(initCalled, 'MixedSub')
                
                Base(Sub)
                self.assertEqual(initCalled, 'Sub')
                initCalled = None
                
                Base(NotSub)
                self.assertEqual(initCalled, None)
                    
                Base(CustomSub)
                self.assertEqual(initCalled, 'CustomInit')

    def test_new_init_error_combinations(self):
        class X1(object):
            args = ()
            def __init__(self):
                object.__init__(self, *X1.args)
        
        class X2(object):
            args = ()
            def __new__(cls):
                return object.__new__(cls, *X2.args)
        
        
        
        temp = X1()
        temp = X2()
        for args in [(42,), 
                    (None,), #CP19585
                    (42, 43), ("abc",), 
                        ]:
            X1.args = args
            X2.args = args
            self.assertRaises(TypeError, X1)
            self.assertRaises(TypeError, X2)

        #--args plus kwargs
        class X3(object):
            args = ()
            kwargs = {}
            def __init__(self):
                object.__init__(self, *X3.args, **X3.kwargs)
        
        class X4(object):
            args = ()
            kwargs = {}
            def __new__(cls):
                return object.__new__(cls, *X4.args, **X4.kwargs)
        
        temp = X3()
        temp = X4()
        
        for args, kwargs in [
                                [(42,), {}], 
                                [(None,), {'a':3}], 
                                [(42, 43), {'a':3, 'b': 4}], 
                                [("abc",), {'z':None}], 
                                [(), {'a':3}],
                            ]:
            X3.args = args
            X3.kwargs = kwargs
            X4.args = args
            X4.kwargs = kwargs
            self.assertRaises(TypeError, X3)
            self.assertRaises(TypeError, X4)


    def test_oldstyle_splat_dict(self):
        class C: pass
        
        def E(): return {}
        
        self.assertEqual(type(C(*E())), type(C()))
    

    def test_get_dict_once(self):
        class x(object): pass

        class y(x): pass

        self.assertTrue('__dict__' in x.__dict__)
        self.assertTrue('__dict__' not in y.__dict__)

    def test_cp22832(self):
        class KOld:
            KOldStuff = 3
        
        class KNew(object, KOld):
            pass
            
        self.assertTrue("KOldStuff" in dir(KNew))

    @unittest.skipIf(is_mono, "mono's GC behaves different from what this test expects")
    def test_cp23564(self):
        global A
        A = 0
        
        class K1(object):
            def __del__(self):
                global A
                A = 1
        
        class K2(K1):
            pass
            
        k = K2()
        k.__class__ = K1
        del k
        self.force_gc()
        self.assertEqual(A, 1)


    def test_object_delattr(self):
        class A(object):
            def __init__(self):
                self.abc = 42
                
        x = A()
        object.__delattr__(x, 'abc')
        self.assertEqual(hasattr(x, 'abc'), False)

    def test_cp33622(self):
        self.assertEqual(object.__repr__ in (None,), False)
        self.assertEqual(object.__repr__ in (None,object.__repr__), True)
        self.assertEqual(object.__repr__ in (None,object.__cmp__), False)
        self.assertEqual(object.__repr__ in (None,object.__str__), False)

    def test_cp24649_gh120(self):
        import copy

        class Descriptor(object):
            def __get__(self, instance, owner):
                return instance.x

        def clone(cls):
            """Create clone of provided class"""
            attrs = vars(cls).copy()
            skipped = ['__dict__', '__weakref__']
            for attr in skipped:
                try:
                    del attrs[attr]
                except KeyError:
                    pass
            cattrs = copy.deepcopy(attrs)
            return type(cls.__name__, cls.__bases__, cattrs)

        class C(object):
            a = Descriptor()
            
            def __init__(self, x):
                self.x = x

        # make sure all expected keys are present, and only those
        self.assertEqual(set(C.__dict__.keys()),
            {'a', '__module__', '__dict__', '__weakref__', '__doc__', '__init__'})
        
        # make sure .items() is the same as indexing
        for key, value in list(C.__dict__.items()):
            self.assertEqual(C.__dict__[key], value)

        CC = clone(C)
        cc = CC(1)
        self.assertEqual(cc.x, 1)

# # tests w/ special requirements that can't be run in methods..
# #Testing the class attributes backed by globals
    
# x = 10

# class C:
#     x = x
#     del x
#     x = x
    
# self.assertEqual(C.x, 10)
# self.assertEqual(x, 10)

# try:
#     class C:
#         x = x
#         del x
#         del x
# except NameError:
#     pass
# else:
#     self.assertTrue("Expecting name error")

# self.assertEqual(x, 10)

# class C:
#     x = 10
#     del x
#     b = x
#     self.assertEqual(x, 10)

# self.assertEqual(C.b, 10)
# self.assertEqual(x, 10)


# def test_suite():
#     return unittest.makeSuite(ClassTest)

# if __name__ == '__main__':
#     unittest.main(defaultTest='test_suite')


run_test(__name__)