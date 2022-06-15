# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import types
import unittest

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython
from functools import reduce

def add(x, y): return x + y

class Flag:  pass
f = Flag()

class InheritanceTest(IronPythonTestCase):
    def setUp(self):
        super(InheritanceTest, self).setUp()
        self.load_iron_python_test()

    @skipUnlessIronPython()
    def test_cli_inheritance(self):
        from IronPythonTest import BaseClass, MySize
        class InheritedClass(BaseClass):
            def ReturnHeight(self):
                return self.Height
            def ReturnWidth(self):
                return self.Width
            def ReturnSize(self):
                return self.Size

        i = InheritedClass()

        self.assertEqual(i.Width, 0)
        self.assertEqual(i.Height, 0)
        self.assertEqual(i.Size.width, 0)
        self.assertEqual(i.Size.height, 0)
        self.assertEqual(i.ReturnHeight(), 0)
        self.assertEqual(i.ReturnWidth(), 0)
        self.assertEqual(i.ReturnSize().width, 0)
        self.assertEqual(i.ReturnSize().height, 0)

        i.Width = 1
        i.Height = 2

        self.assertEqual(i.Width, 1)
        self.assertEqual(i.Height, 2)
        self.assertEqual(i.Size.width, 1)
        self.assertEqual(i.Size.height, 2)
        self.assertEqual(i.ReturnHeight(), 2)
        self.assertEqual(i.ReturnWidth(), 1)
        self.assertEqual(i.ReturnSize().width, 1)
        self.assertEqual(i.ReturnSize().height, 2)

        s = MySize(3, 4)

        i.Size = s

        self.assertEqual(i.Width, 3)
        self.assertEqual(i.Height, 4)
        self.assertEqual(i.Size.width, 3)
        self.assertEqual(i.Size.height, 4)
        self.assertEqual(i.ReturnHeight(), 4)
        self.assertEqual(i.ReturnWidth(), 3)
        self.assertEqual(i.ReturnSize().width, 3)
        self.assertEqual(i.ReturnSize().height, 4)

    @skipUnlessIronPython()
    def test_cli_new_inheritance(self):
        """verifies that we do the right thing with new slot's (e.g. public int new Foo { get; })"""

        from IronPythonTest import NewClass, MySize

        class InheritedClass(NewClass):
            def ReturnHeight(self):
                return self.Height
            def ReturnWidth(self):
                return self.Width
            def ReturnSize(self):
                return self.Size

        for i in (InheritedClass(), NewClass()):
            self.assertEqual(i.Width, 1)
            self.assertEqual(i.Height, 1)
            self.assertEqual(i.Size.width,  1)
            self.assertEqual(i.Size.height,  1)
            if hasattr(i, 'ReturnHeight'):
                self.assertEqual(i.ReturnHeight(),  1)
                self.assertEqual(i.ReturnWidth(),  1)
                self.assertEqual(i.ReturnSize().width,  1)
                self.assertEqual(i.ReturnSize().height,  1)

            i.Width = 1
            i.Height = 2

            self.assertEqual(i.Width,  3)
            self.assertEqual(i.Height,  5)
            self.assertEqual(i.Size.width,  2)
            self.assertEqual(i.Size.height,  3)
            if hasattr(i, 'ReturnHeight'):
                self.assertEqual(i.ReturnHeight(),  5)
                self.assertEqual(i.ReturnWidth(),  3)
                self.assertEqual(i.ReturnSize().width,  2)
                self.assertEqual(i.ReturnSize().height,  3)

            s = MySize(3, 4)

            i.Size = s

            self.assertEqual(i.Width,  7)
            self.assertEqual(i.Height,  9)
            self.assertEqual(i.Size.width,  4)
            self.assertEqual(i.Size.height,  5)

            if not hasattr(i, 'ReturnHeight'):
                # BUGBUG: We need to enable this for the inherited case.  Currently ReflectedTypeBUilder
                # is replacing the derived size w/ the base size.
                self.assertEqual(i.size.width, 3)
                self.assertEqual(i.size.height, 4)

            if hasattr(i, 'ReturnHeight'):
                self.assertEqual(i.ReturnHeight(),  9)
                self.assertEqual(i.ReturnWidth(),  7)
                self.assertEqual(i.ReturnSize().width,  4)
                self.assertEqual(i.ReturnSize().height,  5)

    @skipUnlessIronPython()
    def test_override_hierarchy(self):
        from IronPythonTest import CliVirtualStuff
        self.assertEqual(CliVirtualStuff().VirtualMethod(1), 10)

        # overridden in base, should get base value
        class D1(CliVirtualStuff):
            def VirtualMethod(self, x):
                return 23
        class D2(D1): pass

        self.assertEqual(D2().VirtualMethod(1), 23)

        # multiple new-style classes
        class D3(CliVirtualStuff):
            def VirtualMethod(self, x):
                return 5

        class D4(D1, D3): pass

        self.assertEqual(D4().VirtualMethod(1), 23)

        class D5(D3, D1): pass

        self.assertEqual(D5().VirtualMethod(1), 5)

        class D6(D1, D3):
            def VirtualMethod(self, x):
                return 3

        self.assertEqual(D6().VirtualMethod(1), 3)

        class D7(D1, D3):
            def VirtualMethod(self, x):
                return 4

        self.assertEqual(D7().VirtualMethod(1), 4)

        # old-style class mixed in
        class C1:
            def VirtualMethod(self, x):
                return 42

        class D8(C1, D2): pass

        # old-style comes first, it should take precedence
        self.assertEqual(D8().VirtualMethod(1), 42)

        class D9(D2, C1): pass

        # new-style comes first, it should take precedence
        self.assertEqual(D9().VirtualMethod(1), 23)

        class D10(C1, CliVirtualStuff): pass

        self.assertEqual(D10().VirtualMethod(1), 42)

        class D11(CliVirtualStuff, C1): pass

        self.assertEqual(D11().VirtualMethod(1), 10)

    @skipUnlessIronPython()
    def test_mbr_inheritance(self):
        import System
        class InheritFromMarshalByRefObject(System.MarshalByRefObject):
            pass

    @skipUnlessIronPython()
    def test_static_ctor_inheritance(self):
        from IronPythonTest import BaseClassStaticConstructor
        class StaticConstructorInherit(BaseClassStaticConstructor):
            pass

        sci = StaticConstructorInherit()

        self.assertEqual(sci.Value, 10)

    @skipUnlessIronPython()
    def test_cli_overriding(self):
        from IronPythonTest import Overriding
        class PythonDerived(Overriding):
            def TemplateMethod(self):
                return "From Python"

            def BigTemplateMethod(self, *args):
                return ":".join(str(arg) for arg in args)

            def AbstractTemplateMethod(self):
                return "Overriden"

        o = PythonDerived()
        self.assertEqual(o.TopMethod(), "From Python - and Top")

        del PythonDerived.TemplateMethod
        self.assertEqual(o.TopMethod(), "From Base - and Top")

        def NewTemplateMethod(self):
            return "From Function"

        PythonDerived.TemplateMethod = NewTemplateMethod
        self.assertEqual(o.TopMethod(), "From Function - and Top")

        self.assertEqual(o.BigTopMethod(), "0:1:2:3:4:5:6:7:8:9 - and Top")

        del PythonDerived.BigTemplateMethod
        self.assertEqual(o.BigTopMethod(), "BaseBigTemplate - and Top")


        self.assertEqual(o.AbstractTopMethod(), "Overriden - and Top")
        del PythonDerived.AbstractTemplateMethod

        self.assertRaises(AttributeError, o.AbstractTopMethod)

        class PythonDerived2(PythonDerived):
            def TemplateMethod(self):
                return "Python2"

        o = PythonDerived2()
        self.assertEqual(o.TopMethod(), "Python2 - and Top")


        del PythonDerived2.TemplateMethod
        ##!!! TODO
        ##self.assertEqual(o.TopMethod(), "From Function - and Top")

        del PythonDerived.TemplateMethod
        self.assertEqual(o.TopMethod(), "From Base - and Top")


        self.assertEqual(o.BigTopMethod(), "BaseBigTemplate - and Top")

    @skipUnlessIronPython()
    def test_more_inheritance(self):
        from IronPythonTest import Inherited
        class CTest(Inherited):
            def TopMethod(self):
                return "CTest"

        o = CTest()

    @skipUnlessIronPython()
    def test_interface_inheritance(self):
        from IronPythonTest import ITestIt1, ITestIt2, TestIt
        class C(ITestIt1, ITestIt2):
            def Method(self, x=None):
                if x: return "Python"+repr(x)
                else: return "Python"

        o = C()
        self.assertEqual(TestIt.DoIt1(o), "Python")
        self.assertEqual(TestIt.DoIt2(o), "Python42")

        self.assertTrue(isinstance(o, ITestIt2))
        self.assertTrue(issubclass(C, ITestIt2))
        self.assertTrue(ITestIt2 in C.__bases__)

        # inheritance from a single interface should be verifable

        class SingleInherit(ITestIt1): pass

    @skipUnlessIronPython()
    def test_more_interface_inheritance(self):
        import System
        class Point(System.IFormattable):
            def __init__(self, x, y):
                self.x, self.y = x, y

            def ToString(self, format=None, fp=None):
                if format == 'x': return repr((self.x, self.y))
                return "Point(%r, %r)" % (self.x, self.y)

        p = Point(1,2)
        self.assertEqual(p.ToString(), "Point(1, 2)")
        self.assertEqual(p.ToString('x', None), "(1, 2)")
        #System.Console.WriteLine("{0}", p)

    @skipUnlessIronPython()
    def test_interface_hierarchy(self):
        import System
        self.assertTrue(System.Collections.IEnumerable in System.Collections.IList.__bases__)
        self.assertEqual(System.Collections.IEnumerable.GetEnumerator, System.Collections.IList.GetEnumerator)
        self.assertTrue("GetEnumerator" in dir(System.Collections.IList))
        l = System.Collections.Generic.List[int]()
        l.Add(100)
        self.assertTrue(100 in System.Collections.IList.GetEnumerator(l))

    def test_metaclass(self):
        class MetaClass(type):
            def __new__(metacls, name, bases, vars):
                cls = type.__new__(metacls, name, bases, vars)
                return cls

        MC = MetaClass('Foo', (), {})
        # vs CPython missing: ['__class__', '__delattr__', '__getattribute__', '__hash__', '__reduce__', '__reduce_ex__', '__setattr__', '__str__']
        if is_cli:
            attrs = ['__dict__', '__doc__', '__init__', '__module__', '__new__', '__repr__', '__weakref__']

            has = dir(MC)
            for attr in attrs:
                self.assertTrue(has.index(attr) != -1)

        # metaclass such as defined in string.py
        class MetaClass2(type):
            def __init__(metacls, name, bases, vars):
                super(MetaClass2, name).__init__
        #!!! more meta testing todo


    @skipUnlessIronPython()
    def test_override_param_testing(self):
        from IronPythonTest import MoreOverridding
        class OverrideParamTesting(MoreOverridding):
            def Test1(self, *args):
                return "xx" + args[0] + args[1]
            def Test2(self, x, *args):
                return "xx" + x + args[0] + args[1]
            def Test3(self, xr):
                xr.Value += "xx"
                return "Test3"
            def Test4(self, x, yr):
                yr.Value += x
                return "Test4"
            def Test5(self, sc):
                return "xx" + str(sc)
            def Test6(self, x, sc):
                return "xx" + x + str(sc)

        a = OverrideParamTesting()
        x = a.CallTest1()
        self.assertEqual(x, 'xxaabb')
        x = a.CallTest2()
        self.assertEqual(x, 'xxaabbcc')
        self.assertEqual(a.CallTest3("@"), ("Test3", "@xx"))
        self.assertEqual(a.CallTest4("@"), ("Test4", "@aa"))
        x = a.CallTest5()
        self.assertEqual(x, 'xxOrdinal')
        x = a.CallTest6()
        self.assertEqual(x, 'xxaaOrdinal')

        try:
            a.CallTest3()
            self.assertTrue(False)
        except TypeError:
            pass

        try:
            a.CallTest4()
            self.assertTrue(False)
        except TypeError:
            pass

    def test_mangling(self):
        class _ToMangle(object):
            def getPriv(self):
                return self.__value
            def setPriv(self, val):
                self.__value = val

        class AccessMangled(_ToMangle):
            def inheritGetPriv(self):
                return self._ToMangle__value

        a = AccessMangled()
        a.setPriv('def')
        self.assertEqual(a.inheritGetPriv(), 'def')

    ##############################################################

    @skipUnlessIronPython()
    def test_even_more_overriding(self):
        from IronPythonTest import BaseClass, MoreOverridding
        class Test(BaseClass):
                def __new__(cls):
                    return super(Test, cls).__new__(cls, Width=20, Height=30)

        a = Test()
        self.assertEqual(a.Width, 20)
        self.assertEqual(a.Height, 30)

        class Test(MoreOverridding):
            def Test1(self, *p):
                return "Override!"

        class OuterTest(Test): pass

        a = OuterTest()
        self.assertEqual(a.Test1(), "Override!")

        class OuterTest(Test):
            def Test1(self, *p):
                return "Override Outer!"

        a = OuterTest()
        self.assertEqual(a.Test1(), "Override Outer!")

    def test_oldstyle_inheritance_dir(self):
        # BUG 463
        class PythonBaseClass:
            def Func(self):
                print("PBC::Func")

        class PythonDerivedClass(PythonBaseClass): pass

        self.assertTrue('Func' in dir(PythonDerivedClass))
        self.assertTrue('Func' in dir(PythonDerivedClass()))

    @skipUnlessIronPython()
    def test_cli_inheritance_dir(self):
        import System

        class PythonDerivedFromCLR(System.Collections.Generic.List[int]): pass
        #
        # now check that methods are available, but aren't visible in dir()
        self.assertTrue('Capacity' in dir(PythonDerivedFromCLR))
        self.assertTrue('InsertRange' in dir(PythonDerivedFromCLR()))
        self.assertTrue(hasattr(PythonDerivedFromCLR, 'Capacity'))
        self.assertTrue(hasattr(PythonDerivedFromCLR(), 'InsertRange'))

    @skipUnlessIronPython()
    def test_subclass_into_cli(self):
        """Subclassing once as python class, use it; and pass into CLI again"""
        from IronPythonTest import CliInterface, CliAbstractClass, UseCliClass

        class PythonClass(CliInterface):
            def M1(self):
                f.v = 100
            def M2(self, x):
                f.v = 200 + x

        p = PythonClass()
        p.M1()
        self.assertEqual(f.v, 100)

        p.M2(1)
        self.assertEqual(f.v, 201)

        c = UseCliClass()
        c.AsParam10(p)
        self.assertEqual(f.v, 100)

        #Bug 562
        #c.AsParam11(p, 2)
        #self.assertEqual(f.v, 202)

        p.InstanceAttr = 200
        p2 = c.AsRetVal10(p)
        self.assertEqual(p, p2)
        self.assertEqual(p2.InstanceAttr, 200)

        # normal scenario
        class PythonClass(CliAbstractClass):
            def MV(self, x):
                f.v = 300 + x
            def MA(self, x):
                f.v = 400 + x

        p = PythonClass()
        p.MS(1)
        self.assertEqual(p.helperF, -2 * 1)

        p.MI(2)
        self.assertEqual(p.helperF, -3 * 2)

        p.MV(3)
        self.assertEqual(f.v, 303)

        p.MA(4)
        self.assertEqual(f.v, 404)

        c.AsParam20(p, 3)
        self.assertEqual(f.v, 303)

        c.AsParam21(p, 4)
        self.assertEqual(f.v, 404)

        c.AsParam22(p, 2)
        self.assertEqual(p.helperF,  - 2*2)

        c.AsParam23(p, 1)
        self.assertEqual(p.helperF, - 3*1)

        # virtual function is not overriden in the python class: call locally, and pass back to clr and call.
        class PythonClass(CliAbstractClass): pass
        p = PythonClass()
        p.MV(5)
        self.assertEqual(p.helperF, -4 * 5)

        c.AsParam20(p, 6)
        self.assertEqual(p.helperF, -4 * 6)

        # "override" a  non-virtual method, and pass the object back to clr. This method should not be called
        class PythonClass(CliAbstractClass):
            def MI(self, x):
                    f.v = x * 300
        p = PythonClass()
        f.v = 0
        c.AsParam23(p, 2)
        self.assertEqual(f.v, 0)
        self.assertEqual(p.helperF, -3 * 2)

    @skipUnlessIronPython()
    def test_subclass_twice(self):
        """Subclassing twice"""
        from IronPythonTest import CliInterface, CliAbstractClass
        class PythonClass(CliInterface): pass
        class PythonClass2(PythonClass): pass

        class PythonClass(CliAbstractClass): pass
        class PythonClass2(PythonClass): pass

    @skipUnlessIronPython()
    def test_negative_cli(self):
        """Negative cases: struct, enum, delegate"""
        from IronPythonTest import MySize, DaysInt, VoidDelegate

        try:
            class PythonClass(MySize): pass
            self.fail("should thrown")
        except TypeError:    pass

        try:
            class PythonClass(DaysInt): pass
            self.fail("should thrown")
        except TypeError:    pass

        try:
            class PythonClass(VoidDelegate): pass
            self.fail("should thrown")
        except TypeError:    pass

    @skipUnlessIronPython()
    def test_all_virtuals(self):
        """All Virtual stuff can be overriden? and run"""
        from IronPythonTest import CliVirtualStuff
        class PythonClass(CliVirtualStuff): pass
        class PythonClass2(PythonClass):
            def VirtualMethod(self, x):
                return 20 * x
            def VirtualPropertyGetter(self):
                return 20 * self.InstanceAttr
            def VirtualPropertySetter(self, x):
                self.InstanceAttr = x
            VirtualProperty = property(VirtualPropertyGetter, VirtualPropertySetter)

            def VirtualProtectedMethod(self): return 2000
            VirtualProtectedProperty = property(VirtualPropertyGetter, VirtualPropertySetter)


        p = PythonClass()
        self.assertEqual(p.VirtualMethod(1), 10)
        p.VirtualProperty = 99
        self.assertEqual(p.VirtualProperty, 99)

        p2 = PythonClass2()
        self.assertEqual(p2.VirtualMethod(1), 20)
        p2.VirtualProperty = -1
        self.assertEqual(p2.VirtualProperty, -20)

        self.assertTrue(p2.PublicStuffCheckHelper(-1 * 20,20 * 10))

        p2.VirtualProtectedProperty = 999
        self.assertTrue(p2.ProtectedStuffCheckHelper(999 * 20,2000))


    def test_direct_Type_call(self):
        result = type('a', (list,), dict()) ((1,2))
        result = type('a', (str,), dict()) ('abc')
        result = type('a', (tuple,), dict()) ('abc')
        result = type('a', (dict,), dict()) ()
        result = type('a', (int,), dict()) (0)


    def test_override_tostr(self):
        class foo(object):
            def __str__(self):
                return 'abc'

        a = foo()
        self.assertEqual(str(a), 'abc')

    @skipUnlessIronPython()
    def test_instance_override(self):
        """set virtual override on an instance method"""
        from IronPythonTest import Overriding
        class Foo(Overriding):
            pass

        def MyTemplate(self):
            return "I'm Derived"

        a = Foo()
        a.TemplateMethod = types.MethodType(MyTemplate, a)

        self.assertEqual(a.TopMethod(), "I'm Derived - and Top")

    @unittest.skip("https://github.com/IronLanguages/ironpython3/issues/1413") # @skipUnlessIronPython()
    def test_new_init(self):
        """new / init combos w/ inheritance from CLR class"""
        from IronPythonTest import CtorTest
        # CtorTest has no __init__, so the parameters passed directly
        # to Foo in these tests will always go to the ctor.
        def checkEqual(first, second):
            self.assertEqual(first, second)
        # 3 ints
        class Foo(CtorTest):
            def __init__(self, a, b, c):
                    checkEqual(self.CtorRan, 0)
                    super(CtorTest, self).__init__(a, b, c)

        a = Foo(2,3,4)

        # 3 strings
        class Foo(CtorTest):
            def __init__(self, a, b, c):
                    checkEqual(self.CtorRan, 1)
                    super(CtorTest, self).__init__(a, b, c)

        a = Foo("2","3","4")


        # single int, init adds extra args
        class Foo(CtorTest):
            def __init__(self, a):
                checkEqual(self.CtorRan, 3)
                super(CtorTest, self).__init__(a, 2, 3)

        a = Foo(2)

        # single string, init adds extra args

        class Foo(CtorTest):
            def __init__(self, a):
                checkEqual(self.CtorRan, 2)
                super(CtorTest, self).__init__(a, "2", "3")

        a = Foo("2")


        # single string (shoudl go to string overload)
        class Foo(CtorTest):
            def __init__(self):
                checkEqual(self.CtorRan, -1)
                super(CtorTest, self).__init__("2")

        a = Foo()

        # single int (should go to object overload)
        class Foo(CtorTest):
            def __init__(self):
                checkEqual(self.CtorRan, -1)
                super(CtorTest, self).__init__(2)

        a = Foo()


        # init adds int, we call w/ no args, should go to object
        class Foo(CtorTest):
            def __init__(self):
                checkEqual(self.CtorRan, -1)
                super(CtorTest, self).__init__(2)


        a = Foo()

        class Foo(CtorTest):
            def __init__(self):
                checkEqual(self.CtorRan, -1)
                super(CtorTest, self).__init__(2,3,4)


        a = Foo()


        ########################################################
        # verify we can't call it w/ bad args...

        class Foo(CtorTest):
            def __init__(self):
                super(CtorTest, self).__init__()

        def BadFoo():
            a = Foo(2,3,4,5)

        self.assertRaises(TypeError, BadFoo)


        ########################################################
        # now run the __new__ tests.  Overriding __new__ should
        # allow us to change the parameters that can be passed
        # to create the function


        class Foo(CtorTest):
            def __new__(cls, a, b, c):
                    ret = CtorTest.__new__(CtorTest, a, b, c)
                    return ret

        a = Foo(2,3,4)
        self.assertEqual(a.CtorRan, 0)


        a = Foo("2","3","4")
        self.assertEqual(a.CtorRan, 1)

        # use var-args to invoke arbitrary overloads...

        class Foo(CtorTest):
            def __new__(cls, *args):
                    ret = CtorTest.__new__(CtorTest, *args)
                    return ret


        a = Foo(2,3,4)
        self.assertEqual(a.CtorRan, 0)

        a = Foo("2","3","4")
        self.assertEqual(a.CtorRan, 1)

        a = Foo("abc")
        self.assertEqual(a.CtorRan, 2)

        a = Foo([])
        self.assertEqual(a.CtorRan, 3)

    @unittest.skip("https://github.com/IronLanguages/ironpython3/issues/1413") # @skipUnlessIronPython()
    def test_new_init_combo(self):
        """new/init combo tests..."""
        from IronPythonTest import CtorTest
        class Foo(CtorTest):
            def __new__(cls, *args):
                ret = CtorTest.__new__(CtorTest, *args)
                return ret
            def __init__(self, *args):  pass


        # empty init, we should be able to create any of them...

        a = Foo(2,3,4)
        self.assertEqual(a.CtorRan, 0)

        a = Foo("2","3","4")
        self.assertEqual(a.CtorRan, 1)

        a = Foo("abc")
        self.assertEqual(a.CtorRan, 2)

        a = Foo([])
        self.assertEqual(a.CtorRan, 3)


        class Foo(CtorTest):
            def __new__(cls, *args):
                ret = CtorTest.__new__(Foo, *args)
                return ret
            def __init__(self):
                super(CtorTest, self).__init__(self)

        #ok, we have a compatbile init...
        a = Foo()
        self.assertEqual(a.CtorRan, -1)

        #should all fail due to incompatible init.
        self.assertRaises(TypeError, Foo, 2,3,4)
        self.assertRaises(TypeError, Foo, "2","3","4")
        self.assertRaises(TypeError, Foo, "abc")
        self.assertRaises(TypeError, Foo, [])


        class Foo(CtorTest):
            def __new__(cls, *args):
                ret = CtorTest.__new__(Foo, *args)
                return ret
            def __init__(self, x):
                super(CtorTest, self).__init__(self, x)


        a = Foo("abc")
        self.assertEqual(a.CtorRan, 2)
        a = Foo([])
        self.assertEqual(a.CtorRan, 3)

        self.assertRaises(TypeError, Foo)
        self.assertRaises(TypeError, Foo, 2, 3, 4)
        self.assertRaises(TypeError, Foo, "2", "3", "4")
        self.assertRaises(TypeError, Foo, "2", "3", "4", "5")

    def test_tuple_inheritance(self):
        """verify tuple is ok after deriving from it."""
        class T(tuple):
            pass

        result = 'a' in ('c','d','e')

    def test_str_inheritance(self):
        """inheriting from string should allow us to create extensible strings w/ no params"""

        class MyString(str): pass
        s = MyString()
        self.assertEqual(s, '')

    @skipUnlessIronPython()
    def test_interface_with_property(self):
        """inheritance from an interface w/ a property"""
        from IronPythonTest import ITestIt3
        class foo(ITestIt3):
            def get_Foo(self): return 'abc'
            Name = property(fget=get_Foo)

        a = foo()
        self.assertEqual(a.Name, 'abc')

    @skipUnlessIronPython()
    def test_conversions(self):
        """test converter logics and EmitCastFromObject"""
        import System
        from IronPythonTest import CReturnTypes, UseCReturnTypes, RtEnum

        #############################################
        ## no inherited stuffs, expecting those default value defined in CReturnTypes

        class DReturnTypes(CReturnTypes): pass

        used = UseCReturnTypes(DReturnTypes())
        used.Use_void()
        self.assertEqual(used.Use_Char(), System.Char.MaxValue)
        self.assertEqual(used.Use_Int32(), System.Int32.MaxValue)
        self.assertEqual(used.Use_String(), "string")
        self.assertEqual(used.Use_Int64(), System.Int64.MaxValue)
        self.assertEqual(used.Use_Double(), System.Double.MaxValue)
        self.assertEqual(used.Use_Boolean(), True)
        self.assertEqual(used.Use_Single(), System.Single.MaxValue)
        self.assertEqual(used.Use_Byte(), System.Byte.MaxValue)
        self.assertEqual(used.Use_SByte(), System.SByte.MaxValue)
        self.assertEqual(used.Use_Int16(), System.Int16.MaxValue)
        self.assertEqual(used.Use_UInt32(), System.UInt32.MaxValue)
        self.assertEqual(used.Use_UInt64(), System.UInt64.MaxValue)
        self.assertEqual(used.Use_UInt16(), System.UInt16.MaxValue)
        #See Merlin Work Item 294586 for details on why this isn't supported
        self.assertEqual(used.Use_Type(), System.Type.GetType("System.Int32"))
        self.assertEqual(used.Use_RtEnum(), RtEnum.A)
        self.assertEqual(used.Use_RtDelegate().Invoke(30), 30 * 2)
        self.assertEqual(used.Use_RtStruct().F, 1)
        self.assertEqual(used.Use_RtClass().F, 1)
        self.assertEqual(reduce(add, used.Use_IEnumerator()),  60)

    @skipUnlessIronPython()
    def test_inherit_returntypes(self):
        """inherited all, but with correct return types expect values defined here"""

        import System
        from IronPythonTest import CReturnTypes, UseCReturnTypes, RtEnum, RtStruct, RtClass

        def func(arg): return arg

        global flag
        flag = 10
        class DReturnTypes(CReturnTypes):
            def M_void(self): global flag; flag = 20
            def M_Char(self): return System.Char.MinValue
            def M_Int32(self): return System.Int32.MinValue
            def M_String(self): return "hello"
            def M_Int64(self): return System.Int64.MinValue
            def M_Double(self): return System.Double.MinValue
            def M_Boolean(self): return False
            def M_Single(self): return System.Single.MinValue
            def M_Byte(self): return System.Byte.MinValue
            def M_SByte(self): return System.SByte.MinValue
            def M_Int16(self): return System.Int16.MinValue
            def M_UInt32(self): return System.UInt32.MinValue
            def M_UInt64(self): return System.UInt64.MinValue
            def M_UInt16(self): return System.UInt16.MinValue
            def M_Type(self):
                #See Merlin Work Item 294586 for details on this
                return System.Type.GetType("System.Int64")
            def M_RtEnum(self): return RtEnum.B
            def M_RtDelegate(self): return func
            def M_RtStruct(self): return RtStruct(20)
            def M_RtClass(self): return RtClass(30)
            def M_IEnumerator(self): return iter([1, 2, 3, 4, 5])

        used = UseCReturnTypes(DReturnTypes())
        used.Use_void()
        self.assertEqual(flag, 20)
        self.assertEqual(used.Use_Char(), System.Char.MinValue)
        self.assertEqual(used.Use_Int32(), System.Int32.MinValue)
        self.assertEqual(used.Use_String(), "hello")
        self.assertEqual(used.Use_Int64(), System.Int64.MinValue)
        self.assertEqual(used.Use_Double(), System.Double.MinValue)
        self.assertEqual(used.Use_Boolean(), False)
        self.assertEqual(used.Use_Single(), System.Single.MinValue)
        self.assertEqual(used.Use_Byte(), System.Byte.MinValue)
        self.assertEqual(used.Use_SByte(), System.SByte.MinValue)
        self.assertEqual(used.Use_Int16(), System.Int16.MinValue)
        self.assertEqual(used.Use_UInt32(), System.UInt32.MinValue)
        self.assertEqual(used.Use_UInt64(), System.UInt64.MinValue)
        self.assertEqual(used.Use_UInt16(), System.UInt16.MinValue)

        #See Merlin Work Item 294586 for details on why this isn't supported
        self.assertEqual(used.Use_Type(), System.Type.GetType("System.Int64"))
        self.assertEqual(used.Use_RtEnum(), RtEnum.B)
        self.assertEqual(used.Use_RtDelegate().Invoke(100), 100)
        self.assertEqual(used.Use_RtStruct().F, 20)
        self.assertEqual(used.Use_RtClass().F, 30)
        self.assertEqual(list(used.Use_IEnumerator()), [1, 2, 3, 4, 5])
        self.assertEqual(reduce(add, used.Use_IEnumerable()), 66)

    def create_class(self,retObj):
        if not is_cli:
            return None

        from IronPythonTest import CReturnTypes
        """return a class whose derived methods returns the same specified object"""
        class NewC(CReturnTypes):
            def M_void(self): return retObj
            def M_Char(self): return retObj
            def M_Int32(self): return retObj
            def M_String(self): return retObj
            def M_Int64(self): return retObj
            def M_Double(self): return retObj
            def M_Boolean(self): return retObj
            def M_Single(self): return retObj
            def M_Byte(self): return retObj
            def M_SByte(self): return retObj
            def M_Int16(self): return retObj
            def M_UInt32(self): return retObj
            def M_UInt64(self): return retObj
            def M_UInt16(self): return retObj
            def M_Type(self): return retObj
            def M_RtEnum(self): return retObj
            def M_RtDelegate(self): return retObj
            def M_RtStruct(self): return retObj
            def M_RtClass(self): return retObj
            def M_IEnumerator(self): return retObj
            def M_IEnumerable(self): return retObj
        return NewC

    @skipUnlessIronPython()
    def test_inherited_returntypes_odd_returns(self):
        """inherited all, but returns with a python old class, or new class, or with explicit ops"""
        from IronPythonTest import UseCReturnTypes
        ## all return None
        DReturnTypes = self.create_class(None)

        used = UseCReturnTypes(DReturnTypes())
        self.assertEqual(used.Use_Type(), None)
        self.assertEqual(used.Use_String(), None)
        self.assertEqual(used.Use_RtDelegate(), None)
        self.assertEqual(used.Use_RtClass(), None)
        self.assertEqual(used.Use_Boolean(), False)
        self.assertEqual(used.Use_IEnumerator(), None)

        for f in [used.Use_Char, used.Use_Int32, used.Use_Int64,
            used.Use_Double, used.Use_Single, used.Use_Byte, used.Use_SByte, used.Use_RtEnum,
            used.Use_Int16, used.Use_UInt32, used.Use_UInt64, used.Use_UInt16, used.Use_RtStruct, ]:
            self.assertRaises(TypeError, f)

        ## return old class instance / user type instance
        class python_old_class: pass
        class python_new_class(object): pass

        def check_behavior(expected_obj):
            DReturnTypes = self.create_class(expected_obj)
            used = UseCReturnTypes(DReturnTypes())

            self.assertEqual(used.Use_void(), None)
            self.assertEqual(used.Use_Boolean(), True)

            for f in [used.Use_Char, used.Use_Int32, used.Use_String, used.Use_Int64,
                used.Use_Double, used.Use_Single, used.Use_Byte, used.Use_SByte,
                used.Use_Int16, used.Use_UInt32, used.Use_UInt64, used.Use_UInt16, used.Use_Type,
                used.Use_RtEnum, used.Use_RtStruct, used.Use_RtClass, used.Use_IEnumerator,
                #used.Use_RtDelegate,
                ]:
                self.assertRaises(TypeError, f)

        check_behavior(python_old_class())
        check_behavior(python_new_class())

    @skipUnlessIronPython()
    def test_extensible_int(self):
        """extensible int"""
        import System
        from IronPythonTest import UseCReturnTypes
        class python_my_int(int): pass

        def check_behavior(expected_obj):
            DReturnTypes = self.create_class(expected_obj)
            used = UseCReturnTypes(DReturnTypes())

            self.assertEqual(used.Use_void(), None)
            self.assertEqual(used.Use_Boolean(), True)

            self.assertEqual(used.Use_Int32(), System.Int32.Parse("10"))
            self.assertEqual(used.Use_Int64(), System.Int64.Parse("10"))
            self.assertEqual(used.Use_Double(), System.Double.Parse("10"))
            self.assertEqual(used.Use_Single(), System.Single.Parse("10"))
            self.assertEqual(used.Use_UInt32(), System.UInt32.Parse("10"))
            self.assertEqual(used.Use_UInt64(), System.UInt64.Parse("10"))
            self.assertEqual(used.Use_Byte(), System.Byte.Parse("10"))
            self.assertEqual(used.Use_SByte(), System.SByte.Parse("10"))
            self.assertEqual(used.Use_Int16(), System.Int16.Parse("10"))
            self.assertEqual(used.Use_UInt16(), System.UInt16.Parse("10"))

            for f in [used.Use_Char, used.Use_String, used.Use_Type, used.Use_RtEnum,
                    used.Use_RtStruct, used.Use_RtClass, used.Use_IEnumerator
                    # used.Use_RtDelegate,
                ]:
                self.assertRaises(TypeError, f)

        check_behavior(python_my_int(10))

    @skipUnlessIronPython()
    def test_custom_number_conversion(self):
        """customized __int__. __float__"""
        import System
        from IronPythonTest import UseCReturnTypes
        class python_old_class:
            def __int__(self): return 100
            def __float__(self): return 12345.6
        class python_new_class(object):
            def __int__(self): return 100
            def __float__(self): return 12345.6

        def check_behavior(expected_obj):
            DReturnTypes = self.create_class(expected_obj)
            used = UseCReturnTypes(DReturnTypes())

            self.assertEqual(used.Use_void(), None)
            self.assertEqual(used.Use_Boolean(), True)
            self.assertEqual(used.Use_Int32(), System.Int32.Parse("100"))
            self.assertEqual(used.Use_Double(), System.Double.Parse("12345.6", System.Globalization.CultureInfo.InvariantCulture))

            for f in [used.Use_Int16, used.Use_UInt32, used.Use_UInt64, used.Use_UInt16, used.Use_Single,
                    used.Use_Byte, used.Use_SByte, used.Use_Int64, used.Use_Char, used.Use_String,
                    used.Use_Type, used.Use_RtEnum, used.Use_RtStruct, used.Use_RtClass, used.Use_IEnumerator
                #used.Use_RtDelegate,
                ]:
                self.assertRaises(TypeError, f)

        check_behavior(python_old_class())
        check_behavior(python_new_class())

    @unittest.skip("https://github.com/IronLanguages/ironpython3/issues/1413") # @skipUnlessIronPython()
    def test_return_interesting(self):
        """inherited all, but with more interesting return types"""

        import System
        from IronPythonTest import CReturnTypes, UseCReturnTypes, RtClass

        class DReturnTypes(CReturnTypes): pass
        drt = DReturnTypes()
        used = UseCReturnTypes(drt)

        def func(self): global flag; flag = 60; return 70
        DReturnTypes.M_void = func
        self.assertEqual(used.Use_void(), None)
        self.assertEqual(flag, 60)

        ## Char
        DReturnTypes.M_Char = lambda self: ord('z')
        self.assertRaises(TypeError, used.Use_Char)

        DReturnTypes.M_Char = lambda self: 'y'
        self.assertEqual(used.Use_Char(), System.Char.Parse('y'))

        DReturnTypes.M_Char = lambda self: ''
        self.assertRaises(TypeError, used.Use_Char)

        DReturnTypes.M_Char = lambda self: 'abc'
        self.assertRaises(TypeError, used.Use_Char)

        ## String
        DReturnTypes.M_String = lambda self: 'z'
        self.assertEqual(used.Use_String(), 'z')

        DReturnTypes.M_String = lambda self: ''
        self.assertEqual(used.Use_String(), System.String.Empty)

        DReturnTypes.M_String = lambda self: ord('z')
        self.assertRaises(TypeError, used.Use_String)

        ## Int32
        DReturnTypes.M_Int32 = lambda self: System.Char.Parse('z')
        self.assertRaises(TypeError, used.Use_Int32)

        DReturnTypes.M_Int32 = lambda self: System.SByte.Parse('-123')
        self.assertEqual(used.Use_Int32(), -123)

        DReturnTypes.M_Int32 = lambda self: 12345678901234
        self.assertRaises(OverflowError, used.Use_Int32)

        ## RtClass
        class MyRtClass(RtClass):
            def __init__(self, value):
                super(MyRtClass, self).__init__(value)

        DReturnTypes.M_RtClass = lambda self: MyRtClass(500)
        self.assertEqual(used.Use_RtClass().F, 500)

        ## IEnumerator
        DReturnTypes.M_IEnumerator = lambda self: iter((2, 20, 200, 2000))
        self.assertEqual(tuple(used.Use_IEnumerator()), (2, 20, 200, 2000))

        ## IEnumerable
        DReturnTypes.M_IEnumerable = lambda self: (2, 20, 200, 2000)
        self.assertEqual(reduce(add, used.Use_IEnumerable()), 2222)

        DReturnTypes.M_IEnumerator = lambda self: iter({ 1 : "one", 10: "two", 100: "three"})
        self.assertEqual(set(used.Use_IEnumerator()), set([1, 10, 100]))

        DReturnTypes.M_IEnumerable = lambda self: { 1 : "one", 10: "two", 100: "three"}
        self.assertEqual(reduce(add, used.Use_IEnumerable()), 111)

        DReturnTypes.M_IEnumerator = lambda self: iter(System.Array[int](list(range(10))))
        self.assertEqual(list(used.Use_IEnumerator()), list(range(10)))

        DReturnTypes.M_IEnumerable = lambda self: System.Array[int](list(range(10)))
        self.assertEqual(reduce(add, used.Use_IEnumerable()), 45)

        ## RtDelegate
        def func2(arg1, arg2): return arg1 * arg2

        DReturnTypes.M_RtDelegate = lambda self : func2
        delegate = used.Use_RtDelegate()
        self.assertRaises(TypeError, delegate, 1)

    @skipUnlessIronPython()
    def test_redefine_non_virtual(self):
        # Redefine non-virtual method:

        from IronPythonTest import CReturnTypes, UseCReturnTypes

        class DReturnTypes(CReturnTypes): pass

        used = UseCReturnTypes(DReturnTypes())
        self.assertEqual(used.Use_NonVirtual(), 100)

        class DReturnTypes(CReturnTypes):
            def M_NonVirtual(self): return 200

        used = UseCReturnTypes(DReturnTypes())
        self.assertEqual(used.Use_NonVirtual(), 100)

    @skipUnlessIronPython()
    def test_interface_abstract_type(self):
        # Similar but smaller set of test for interface/abstract Type

        import System
        from IronPythonTest import RtEnum, RtStruct, RtClass, IReturnTypes, UseIReturnTypes, AReturnTypes, UseAReturnTypes

        def test_returntype(basetype, usetype):
            class derived(basetype): pass

            used = usetype(derived())

            for f in [ used.Use_void,used.Use_Char,used.Use_Int32,used.Use_String,used.Use_Int64,used.Use_Double,used.Use_Boolean,
                used.Use_Single,used.Use_Byte,used.Use_SByte,used.Use_Int16,used.Use_UInt32,used.Use_UInt64,used.Use_UInt16,
                used.Use_Type,used.Use_RtEnum,used.Use_RtDelegate,used.Use_RtStruct,used.Use_RtClass,used.Use_IEnumerator,
                ]:
                self.assertRaises(AttributeError, f)

            class derived(basetype):
                def M_void(self): global flag; flag = 20
                def M_Char(self): return System.Char.MinValue
                def M_Int32(self): return System.Int32.MinValue
                def M_String(self): return "hello"
                def M_Int64(self): return System.Int64.MinValue
                def M_Double(self): return System.Double.MinValue
                def M_Boolean(self): return False
                def M_Single(self): return System.Single.MinValue
                def M_Byte(self): return System.Byte.MinValue
                def M_SByte(self): return System.SByte.MinValue
                def M_Int16(self): return System.Int16.MinValue
                def M_UInt32(self): return System.UInt32.MinValue
                def M_UInt64(self): return System.UInt64.MinValue
                def M_UInt16(self): return System.UInt16.MinValue
                def M_Type(self):
                    return System.Type.GetType("System.Int64")
                def M_RtEnum(self): return RtEnum.B
                def M_RtDelegate(self): return lambda arg: arg * 5
                def M_RtStruct(self): return RtStruct(20)
                def M_RtClass(self): return RtClass(30)
                def M_IEnumerator(self): return iter([1, 2, 3, 4, 5])
                def M_IEnumerable(self): return [7, 8, 9, 10, 11]

            used = usetype(derived())
            used.Use_void()
            self.assertEqual(flag, 20)
            self.assertEqual(used.Use_Char(), System.Char.MinValue)
            self.assertEqual(used.Use_Int32(), System.Int32.MinValue)
            self.assertEqual(used.Use_String(), "hello")
            self.assertEqual(used.Use_Int64(), System.Int64.MinValue)
            self.assertEqual(used.Use_Double(), System.Double.MinValue)
            self.assertEqual(used.Use_Boolean(), False)
            self.assertEqual(used.Use_Single(), System.Single.MinValue)
            self.assertEqual(used.Use_Byte(), System.Byte.MinValue)
            self.assertEqual(used.Use_SByte(), System.SByte.MinValue)
            self.assertEqual(used.Use_Int16(), System.Int16.MinValue)
            self.assertEqual(used.Use_UInt32(), System.UInt32.MinValue)
            self.assertEqual(used.Use_UInt64(), System.UInt64.MinValue)
            self.assertEqual(used.Use_UInt16(), System.UInt16.MinValue)
            self.assertEqual(used.Use_Type(), System.Type.GetType("System.Int64"))
            self.assertEqual(used.Use_RtEnum(), RtEnum.B)
            self.assertEqual(used.Use_RtDelegate().Invoke(100), 100 * 5)
            self.assertEqual(used.Use_RtStruct().F, 20)
            self.assertEqual(used.Use_RtClass().F, 30)
            self.assertEqual(list(used.Use_IEnumerator()), [1, 2, 3, 4, 5])
            self.assertEqual(reduce(add, used.Use_IEnumerable()), 45)

        test_returntype(IReturnTypes, UseIReturnTypes)
        test_returntype(AReturnTypes, UseAReturnTypes)

    @skipUnlessIronPython()
    def test_bigvirtual_derived(self):
        # verify that classes w/ large vtables we get the
        # correct method dispatch when overriding bases

        from IronPythonTest import BigVirtualClass

        class BigVirtualDerived(BigVirtualClass):
            def __init__(self):
                self.funcCalled = False
            for x in range(50):
                exec('def M%d(self):\n    self.funcCalled = True\n    return super(type(self), self).M%d()' % (x, x))

        a = BigVirtualDerived()
        for x in range(50):
            # call from Python
            self.assertEqual(a.funcCalled, False)
            self.assertEqual(x, getattr(a, 'M'+str(x))())
            self.assertEqual(a.funcCalled, True)
            a.funcCalled = False
            # call non-virtual method that calls from C#
            self.assertEqual(x, getattr(a, 'CallM'+str(x))())
            self.assertEqual(a.funcCalled, True)
            a.funcCalled = False

    def test_super_inheritance(self):
        # descriptor for super should return derived class, not a new instance of super
        class foo(super):
            def __init__(self, *args):
                return super(foo, self).__init__(*args)


        class bar(object): pass

        # when lookup comes from super's class it should
        # be from it's type, not from the super base type
        self.assertEqual(foo(bar).__class__, foo)

        bar.x = foo(bar)
        self.assertEqual(type(bar().x), foo)        # once via .
        x = foo(bar)
        self.assertEqual(type(x.__get__(bar, foo) ), foo)   # once by calling descriptor directly

    def test_super_new_init(self):
        x = super.__new__(super)
        self.assertEqual(x.__thisclass__, None)
        self.assertEqual(x.__self__, None)
        self.assertEqual(x.__self_class__, None)

        x.__init__(super, None)

        self.assertEqual(x.__thisclass__, super)
        self.assertEqual(x.__self__, None)
        self.assertEqual(x.__self_class__, None)

        x.__init__(super, x)

        self.assertEqual(x.__thisclass__, super)
        self.assertEqual(x.__self__, x)
        self.assertEqual(x.__self_class__, super)

        self.assertTrue(repr(x.__self__).find('<super object>') != -1)  # __self__'s repr goes recursive and gets it's display tweaked

    def test_super_class(self):
        """verify super on a class passes None for the instance"""
        def checkEqual(first, second):
            self.assertEqual(first,second)

        class custDescr(object):
            def __get__(self, instance, owner):
                checkEqual(instance, None)
                return 'abc'

        class base(object):
            aProp = property(lambda self: 'foo')
            aDescr = custDescr()

        class sub(base):
            def test1(cls): return super(sub, cls).aProp
            def test2(cls): return super(sub, cls).aDescr
            test1 = classmethod(test1)
            test2 = classmethod(test2)

        self.assertEqual(sub.test2(), 'abc')
        self.assertEqual(type(sub.test1()), property)


    def test_super_proxy(self):
        def checkEqual(first,second):
            self.assertEqual(first,second)
        class mydescr(object):
            def __init__(self, func):
                self.func = func
            def __get__(self, instance, context):
                checkEqual(context, C)
                x = self.func.__get__(instance, context)
                return x

        class Proxy(object):
            def __init__(self, obj):
                self.__obj = obj
            def __getattribute__(self, name):
                if name.startswith("_Proxy__"): return object.__getattribute__(self, name)
                else: return getattr(self.__obj, name)

        class B(object):
            def f(self):
                return "B.f"

        class C(B):
            def f(self):
                return super(C, self).f() + "->C.f"

        C.f = mydescr(C.f)
        B.f = mydescr(B.f)
        obj = C()
        p = Proxy(obj)
        self.assertEqual(C.f(p), 'B.f->C.f')

    def test_super_tostringself(self):
        def check(condition):
            self.assertTrue(condition)

        class C(object):
            def __new__(cls):
                x = super(C, cls)
                check("<super" in str(x))
                return x.__new__(cls)

        c = C()

    @skipUnlessIronPython()
    def test_inherit_mixed_properties(self):
        from IronPythonTest import MixedPropertiesInherited, MixedProperties
        def checkEqual(first, second):
            self.assertEqual(first, second)

        for base in (MixedPropertiesInherited, MixedProperties):
            class MyClass(base):
                def __init__(self):
                    checkEqual(self.Foo, None)
                    self.Foo = 42
                    checkEqual(self.Foo, 42)

                    checkEqual(self.Bar, None)
                    self.Bar = 23
                    checkEqual(self.Bar, 23)

                    checkEqual(self.GetFoo(), 42)
                    checkEqual(self.GetBar(), 23)

            a = MyClass()
            self.assertEqual(a.GetFoo(), 42)
            self.assertEqual(a.GetBar(), 23)

    @skipUnlessIronPython()
    def test_partial_property_override(self):
        # https://github.com/IronLanguages/ironpython3/issues/1375
        from IronPythonTest import PartialPropertyOverrideClass
        c = PartialPropertyOverrideClass()

        # only the setter is overridden on Height
        c.Height = 4
        self.assertEqual(c.Height, 8)

        # only the getter is overridden on Width
        c.Width = 4
        self.assertEqual(c.Width, 8)

run_test(__name__)
