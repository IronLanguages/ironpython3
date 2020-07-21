# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
How to re-define a method in Python.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

# from Merlin.Testing import *
# from Merlin.Testing.BaseClass import *

# import System


@skipUnlessIronPython()
class MethodOverrideTest(IronPythonTestCase):
    def setUp(self):
        super(MethodOverrideTest, self).setUp()
        self.add_clr_assemblies("baseclasscs", "typesamples")

    def test_interface_simple_defined(self):
        from Merlin.Testing.BaseClass import Class43, Class44, IInterface22, IInterface24, IInterface25, IInterface26
        class C(IInterface24): # IInterface24 < IInterface21
            def m21(self): return 1
            # m24 is not defined

        x = C()
        self.assertEqual(IInterface24.m21(x), 1)
        f = IInterface24.m24
        self.assertRaisesMessage(AttributeError, "'C' object has no attribute 'm24'", f, x)

        class C(IInterface24):
            def m21(self): return 2
            def m24(self): return 3

        x = C()
        self.assertEqual(IInterface24.m21(x), 2)
        self.assertEqual(IInterface24.m24(x), 3)

        #public class Class43 : IInterface26 { public int m26() { return 43; } }
        class C(Class43):
            def m26(self): return 4
        x = C()
        self.assertEqual(IInterface26.m26(x), 43)

        #public class Class44 : IInterface26 { public virtual int m26() { return 44; } }
        class C(Class44):
            def m26(self): return 5
        x = C()
        self.assertEqual(IInterface26.m26(x), 5)
        self.assertEqual(Class44.m26(x), 44)

        class C(IInterface25, IInterface22): # repeated interfaces
            def m21(self): return 6
            def m22(self): return 7
            def m24(self): return 8
            def m25(self): return 9
        x = C()
        self.assertEqual(IInterface25.m24(x), 8)
        self.assertEqual(IInterface24.m21(x), 6)
        self.assertEqual(IInterface25.m22(x), 7)

    def test_class_simple_method(self):
        from Merlin.Testing.BaseClass import Class41, Class42
        # public class Class41 { public int m41() { return 41; } }
        class C(Class41):
            def m41(self): return 1

        x = C()
        self.assertEqual(x.m41(), 1)
        self.assertEqual(Class41.m41(x), 41)
        self.assertEqual(C.m41(x), 1)

        # public class Class42 { public virtual int m42() { return 42; } }
        class C(Class42):
            def m42(self): return 2

        x, y = C(), Class42()
        self.assertEqual(x.m42(), 2)
        self.assertEqual(Class42.m42(x), 42)
        self.assertEqual(C.m42(x), 2)
        self.assertEqual(Class42.m42(y), 42)  # !!!

    def test_interface_methods(self):
        from Merlin.Testing.BaseClass import IInterface110a, IInterface110b, IInterface110c
        #public interface IInterface110a { int m110(); }
        #public interface IInterface110b { int m110(int arg); }
        #public interface IInterface110c : IInterface110a, IInterface110b { }

        # m110 not defined
        class C1(IInterface110a, IInterface110b):
            pass
        class C2(IInterface110c):
            pass

        # interfaces defined explicitly are included in the MRO so we can find their methods...
        self.assertEqual(C1.__mro__, (C1, IInterface110a, IInterface110b, object))
        self.assertEqual(C2.__mro__, (C2, IInterface110c, IInterface110a, IInterface110b, object))

        for C in [C1, C2]:
            x = C()
            self.assertRaises(AttributeError, IInterface110a.m110, x)
            self.assertRaises(TypeError, IInterface110b.m110, x)
            self.assertRaises(AttributeError, IInterface110c.m110, x)
            f = C.m110  # no AttributeError

            # we find the implementation through the interface which does a
            # lookup for the member dynamically...
            self.assertEqual(repr(f), "<method 'm110' of 'IInterface110a' objects>")

            # this fails w/ attribute error because we can find the method
            # on the interface and dispatch through it and then the lookup
            # on the type fails...
            self.assertRaises(AttributeError, f, x)

            # this fails because we find IInterface110a's method not B's and
            # we don't have the right number of arguments
            self.assertRaises(TypeError, f, x, 1)

            ## !! it's strange that this behaves differently...
            f = x.m110  # no AttributeError
            self.assertRaises(AttributeError, f)
            self.assertRaises(AttributeError, f, 1)
        #
        # m110 is defined
        #
        class C(IInterface110a, IInterface110b):
            def m110(self): return 11
        x = C()
        self.assertEqual(IInterface110a.m110(x), 11)
        self.assertRaises(TypeError, IInterface110b.m110, x)
        self.assertEqual(IInterface110c.m110(x), 11)

        class C(IInterface110a, IInterface110b):
            def m110(self, arg): return 12
        x = C()
        self.assertRaises(TypeError, IInterface110a.m110, x, 1)
        #self.assertEqual(IInterface110b.m110(x, 1), 12)
        #self.assertEqual(IInterface110c.m110(x, 1), 12)

        class C(IInterface110c):
            def m110(self): return 13
        x = C()
        self.assertEqual(IInterface110a.m110(x), 13)
        self.assertRaises(TypeError, IInterface110b.m110, x)
        self.assertEqual(IInterface110c.m110(x), 13)

        class C(IInterface110c):
            def m110(self, arg): return 14
        x = C()
        self.assertRaises(TypeError, IInterface110a.m110, x, 1)
        self.assertEqual(IInterface110b.m110(x, 1), 14)
        self.assertRaises(TypeError, IInterface110b.m110, x, "string")
        #self.assertEqual(IInterface110c.m110(x, 1), 14)

        f = C.m110
        self.assertEqual(f(x, 1), 14)
        self.assertEqual(f(x, "string"), 14)
        self.assertRaises(TypeError, f, x)

        self.assertEqual(x.m110(1), 14)
        self.assertEqual(x.m110("string"), 14)
        self.assertRaises(TypeError, x.m110)

    @unittest.expectedFailure
    def test_explicit_implementation_required_in_csharp(self):
        from Merlin.Testing.BaseClass import Class42, IInterface42
        #public class Class42 { public virtual int m42() { return 42; } }
        #public interface IInterface42 { int m42(); }

        class C1(Class42, IInterface42): pass
        class C2(IInterface42, Class42): pass

        for C in [ C1, C2, ]:
            x = C()
            self.assertEqual(x.m42(), 42)
            self.assertRaises(AttributeError, IInterface42.m42, x)

        class C1(Class42, IInterface42):
            def m42(self): return 10
        class C2(IInterface42, Class42):
            def m42(self): return 10

        for C in [ C1, C2 ]:
            x = C()
            self.assertEqual(x.m42(), 10)
            self.assertEqual(IInterface42.m42(x), 10)
            self.assertEqual(Class42.m42(x), 42)

    def test_abstract_methods(self):
        from Merlin.Testing.BaseClass import Class200a, Class200b
        class C(Class200a): pass
        x = C()
        f = x.m200
        self.assertRaises(AttributeError, f)

        C.m200 = lambda self: 100
        self.assertEqual(f(), 100)
        self.assertEqual(C.m200(x), 100)
        self.assertEqual(Class200a.m200(x), 100)

        class C(Class200a):
            def m200(self): return 100
        x = C()
        self.assertEqual(x.m200(), 100)
        self.assertEqual(Class200a.m200(x), 100)

        class C(Class200a):
            def m200(self, arg): return 111  # unmatched signature
        x = C()
        self.assertRaises(TypeError, Class200a.m200, x)
        self.assertRaises(TypeError, Class200a.m200, x, 1)
        self.assertEqual(x.m200(1), 111)

        class C(Class200b): pass
        x = C()
        self.assertEqual(x.m200(), 200)
        self.assertEqual(Class200b.m200(x), 200)
        self.assertEqual(Class200a.m200(x), 200)
        self.assertEqual(C.m200(x), 200)

        class C(Class200b):
            def m200(self): return 300
        x = C()
        # CodePlex bug 21222 http://www.codeplex.com/IronPython/WorkItem/View.aspx?WorkItemId=21222
        # this should be an error.
        self.assertEqual(Class200a.m200(x), 200)
        self.assertEqual(Class200b.m200(x), 200)

    def test_virtual_override_method(self):
        from Merlin.Testing.BaseClass import Callback, Class210a, Class210b, Class210c, Class210d
        class C(Class210b): pass
        x = C()
        self.assertEqual(Class210a.m210(x), 210)
        self.assertEqual(Class210b.m210(x), 211)
        self.assertEqual(x.m210(), 211)
        self.assertEqual(C.m210(x), 211)
        self.assertEqual(Callback.On(x), 210)

        C.m210 = lambda self: 400
        self.assertEqual(Class210a.m210(x), 210)
        self.assertEqual(Class210b.m210(x), 211)
        self.assertEqual(x.m210(), 400)
        self.assertEqual(C.m210(x), 400)
        self.assertEqual(Callback.On(x), 210)

        class C(Class210c):
            def m210(self): return 500
        x = C()
        #self.assertEqual(Class210a.m210(x), 210) # bug 368813
        self.assertEqual(Class210c.m210(x), 212)
        self.assertEqual(x.m210(), 500)
        self.assertEqual(C.m210(x), 500)
        self.assertEqual(Callback.On(x), 500)

        del C.m210
        #self.assertEqual(Class210a.m210(x), 210) # bug 368813
        self.assertEqual(Class210c.m210(x), 212)
        self.assertEqual(x.m210(), 212)
        self.assertEqual(C.m210(x), 212)
        self.assertEqual(Callback.On(x), 212)

        class C(Class210d): pass
        x = C()
        self.assertEqual(Class210a.m210(x), 210)
        self.assertEqual(Class210d.m210(x), 210)
        self.assertEqual(x.m210(), 210)
        self.assertEqual(C.m210(x), 210)
        self.assertEqual(Callback.On(x), 210)

        C.m210 = lambda self: 600
        #self.assertEqual(Class210a.m210(x), 210) # bug 368813
        self.assertEqual(Class210d.m210(x), 210)
        self.assertEqual(x.m210(), 600)
        self.assertEqual(C.m210(x), 600)
        self.assertEqual(Callback.On(x), 600)

    def test_final_methods(self):
        from Merlin.Testing.BaseClass import Callback, Class210e
        class C(Class210e):
            def m210(self): return 700
        x = C()
        self.assertEqual(x.m210(), 700)
        #self.assertEqual(Class210a.m210(x), 210) # bug 368813
        self.assertEqual(Class210e.m210(x), 214)
        self.assertEqual(Callback.On(x), 214)

    #@disabled("bug 369539")
    def test_generic_method_inside_interface(self):
        from Merlin.Testing.BaseClass import IInterface250, IInterface251
        class C(IInterface250): pass

        # TODO: define m250...

        class C(IInterface251): pass


    def test_method_inside_generic_interfaces(self):
        from Merlin.Testing.BaseClass import IInterface260
        class C(IInterface260[int]): pass
        x = C()
        self.assertRaises(AttributeError, IInterface260[int].m260, x)

        class C(IInterface260[int]):
            def m260(self): return 700
        x = C()
        self.assertEqual(IInterface260[int].m260(x), 700)
        self.assertRaises(TypeError, IInterface260[str].m260, x)
        # expected IInterface260[str], got Object#IInterface260[] !!!

        class C: pass

    #@disabled("bug 369539")
    def test_generic_method_from_abstract_class(self):
        from Merlin.Testing.BaseClass import Class300
        class C(Class300):
            def m300(self): return 310
        x = C()
        # Class300.m300(x)

    def test_generic_method_from_class(self):
        from Merlin.Testing.BaseClass import Class310
        class C(Class310): pass
        x = C()

        self.assertEqual(x.m310a[int](1), 1)
        self.assertEqual(x.m310b[int, str](1, "a"), 2)
        self.assertEqual(x.m310c(1), 3)
        self.assertEqual(x.m310c[str](1), 4)

        # bug 369616

        #C.m310a[int]

        #Class310.m310a[str](x, 'a')
        #Class310.m310c[str]

        class C(Class310):
            def m310a(self, x): return 10
            def m310b(self): return 11
            def m310c(self): return 12

        x = C()
        self.assertEqual(x.m310a(1), 10)
        self.assertRaisesMessage(TypeError,
            "'method' object is not subscriptable",
            lambda: x.m310a[int])

        self.assertEqual(C.m310a(x, 1), 10)
        #Class310.m310a[int](x, 1)

    def test_methods_from_generic_class(self):
        import System
        from Merlin.Testing.BaseClass import Class320
        class C(Class320[int]): pass
        x = C()
        self.assertEqual(x.m320(1), 11)
        self.assertRaises(TypeError, x.m320, 'abc')

        self.assertEqual(C.m320(x, 1), 11)
        self.assertEqual(Class320[int].m320(x, 1), 11)

        C.m320 = lambda self, arg: 100
        self.assertEqual(x.m320(1), 100)
        self.assertEqual(C.m320(x, 1), 100)
        self.assertEqual(Class320[int].m320(x, 1), 11)

        # ref type
        class C(Class320[System.Type]): pass
        x = C()
        self.assertRaises(TypeError, x.m320, 1)
        self.assertEqual(x.m320(None), 11)

        C.m320 = lambda self: 200
        self.assertEqual(x.m320(), 200)

    def test_static_methods(self):
        from Merlin.Testing.BaseClass import Class500
        class C(Class500): pass
        x = C()

        for s in [x, C]:
            self.assertEqual(s.m500a(), 501)
            self.assertEqual(s.m500b[int](), 502)
            #self.assertRaisesMessage(TypeError, "xxx", s.m500b)  # bug 369706

            self.assertEqual(s.m500c(), 503)
            self.assertEqual(s.m500c[int](), 504)
            self.assertRaisesMessage(TypeError,
                "bad type args to this generic method m500c",
                lambda: s.m500c[int, int])

            #self.assertRaisesMessage(TypeError, "xxx", s.m500d) # bug 369706
            self.assertEqual(s.m500d[int](), 505)
            self.assertEqual(s.m500d[int, str](), 506)


        class C(Class500):
            @staticmethod  # classmethod??
            def m500a(): return 105
            @staticmethod
            def m500b(): return 205
            @staticmethod
            def m500d(): return 505

        x = C()
        for s in [x, C]:
            self.assertEqual(s.m500a(), 105)

            self.assertEqual(s.m500b(), 205)
            self.assertRaisesMessage(TypeError,
                "'function' object is not subscriptable",
                lambda: s.m500b[int])

            self.assertEqual(s.m500c(), 503)
            self.assertEqual(s.m500c[str](), 504)

            self.assertEqual(s.m500d(), 505)
            self.assertRaisesMessage(TypeError,
                "'function' object is not subscriptable",
                lambda: s.m500d[int])

    def test_super(self):
        from Merlin.Testing.BaseClass import Class210a, Class210d, Class210e
        class C(Class210a):
            def m210(self):
                return super(C, self).m210() + 100
            def m220(self):
                return super(C, self).m220() + 100
        x = C()
        self.assertEqual(x.m210(), 310)
        self.assertRaisesMessage(AttributeError,
            "'super' object has no attribute 'm220'",
            x.m220)

        class C(Class210d):  # m210 is not defined in Class210d
            def m210(self):
                return super(C, self).m210() + 200
        x = C()
        self.assertEqual(x.m210(), 410)

        class C(Class210e):
            def m210(self):
                return super(C, self).m210() + 300
        x = C()
        self.assertEqual(x.m210(), 514)

    def test_super_protected(self):
        from Merlin.Testing.BaseClass import Class215d
        class KNet(Class215d):
            def m215(self):
                return super(KNet, self).m215()

        k = KNet()
        self.assertEqual(k.m215(), 215)

    # incomplete...
    def test_long_hierarchy(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.BaseClass import Callback, CType11, CType21
        class C(CType11): pass

        x = C()
        x.m1()
        Flag.Check(10)

        Flag.Reset()
        Callback.On(x)
        Flag.Check(10)

        class C(CType11):
            def m1(self): Flag.Set(20)

        x = C()
        Callback.On(x)
        Flag.Check(20)

        class C(CType21):
            pass
        x = C()
        self.assertRaises(AttributeError, Callback.On, x)

        class C(CType21):
            def m1(self): Flag.Set(30)
        x = C()
        Callback.On(x)
        Flag.Check(30)

run_test(__name__)
