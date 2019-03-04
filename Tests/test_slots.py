# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# ref: http://docs.python.org/ref/slots.html

import unittest

from iptest import IronPythonTestCase, run_test

class SlotsTest(IronPythonTestCase):
    def test_basic(self):
        class C(object):
            __slots__ = 'a'

            def __init__(self):
                try: self.b = 2
                except AttributeError: pass
                else: Fail("should have thrown")

        self.assertEqual(str(C.a), "<member 'a' of 'C' objects>")

        x = C()
        self.assertTrue(not hasattr(x, "__dict__"))
        self.assertTrue(not hasattr(x, "__weakref__"))

        self.assertRaises(AttributeError, lambda: x.a)
        x.a = 1
        self.assertEqual(x.a, 1)

        def f(): x.b = 2
        self.assertRaises(AttributeError, f)

        self.assertEqual(C.__dict__['a'], C.a)

        C.__dict__['a'].__set__(x, 3)  # bug 364459
        self.assertEqual(3, C.a.__get__(x))

    def test_remove_slots_afterwards(self):
        class C(object):
            __slots__ = 'a'

        del C.__slots__

        x = C()

        x.a = 3
        self.assertEqual(x.a, 3)

        def f(): x.b = 2
        self.assertRaises(AttributeError, f)
        self.assertRaises(AttributeError, lambda: x.b)

        self.assertTrue(not hasattr(x, "__dict__"))
        self.assertTrue(not hasattr(x, "__slots__"))

    def test_add_slots_afterwards(self):
        class C(object): pass
        C.__slots__ = ['b', 'c']

        x = C()

        self.assertTrue(hasattr(x, "__dict__"))
        x.a = 3
        self.assertEqual(x.a, 3)

        self.assertEqual(x.__slots__, ['b', 'c'])

    def test_change_slots_content(self):
        class C(object):
            __slots__ = ['a', 'b']

        x = C()
        x.a = 1
        x.b = 2
        self.assertEqual([x.a, x.b], [1, 2])

        C.__slots__.remove('a')
        x.a = 3

        C.__slots__.append('c')
        def f(): x.c = 4
        self.assertRaises(AttributeError, f)

        C.__slots__.append('__dict__')
        self.assertRaises(AttributeError, f)

    def test_dict_in_slots(self):
        class C(object):
            __slots__ = ['a', 'b', '__dict__']

            def m(self):
                self.d = 4

        x = C()
        self.assertRaises(AttributeError, lambda: x.c)
        self.assertTrue(not hasattr(x, "__weakref__"))
        x.a = 1; self.assertEqual(x.a, 1)
        x.c = 3; self.assertEqual(x.c, 3)
        x.m();   self.assertEqual(x.d, 4)

    # The action of a __slots__ declaration is limited to the class where it is defined.
    # As a result, subclasses will have a __dict__ unless they also define __slots__.
    def test_subclassing(self):
        class C(object):
            __slots__ = ['a']

        # no __slots__
        class D(C):
            def __init__(self):
                self.a = 1
                self.b = 2

        y = D()
        y.c = 3
        self.assertEqual([y.a, y.b, y.c], [1, 2, 3])

        # set empty __slots__, the instance has __dict__
        class E(D):
            __slots__ = []
        y = E()

        y.d = 4
        self.assertEqual(4, y.d)

        # have new __slots__, and slots are "inherited"
        class D(C):
            __slots__ = ['b']

        y = D()
        y.a = 1; self.assertEqual(1, y.a)    # 'a' is available
        y.b = 2; self.assertEqual(2, y.b)
        def f(): y.c = 3
        self.assertRaises(AttributeError, f)  # bug 364438

        #
        # If a class defines a slot also defined in a base class, the instance variable
        # defined by the base class slot is inaccessible (except by retrieving its descriptor
        # directly from the base class). This renders the meaning of the program undefined.
        # In the future, a check may be added to prevent this.
        #

        class D(C):
            __slots__ = ['a', 'b']  # 'a' is re-defined

        # more after bug 364423 fix
        class C1(object):
            __slots__ = []
        class C2(object):
            __slots__ = ['a']

        class D1(C1, C2): pass

        # https://github.com/IronLanguages/main/issues/1374 (C was marked as not having a dictionary)
        class A(object):
            def __init__(self):
                self.a = 1
        class B(A):
            pass
        class C(B):
            __slots__ = ('c', )
        c = C()

    def test_subclass_with_interesting_slots(self):
        class C1(object):
            __slots__ = []
        class C2(object):
            __slots__ = ['a']
        class C3(object):
            __slots__ = ['b']
        class C4(object): pass

        class D1(C1, C2): pass   # bug 364459
        def f():
            class D2(C2, C3): pass
        self.assertRaises(TypeError, f)  # bug 364423

        class D3(C1, C4):
            pass
        y = D3()
        y.not_in_slots = 1

        class D4(C1, C4):
            __slots__ = []
        y = D4()
        y.not_in_slots = 2

        #############################################

        class C1(object):
            __slots__ = 'a'

        class C2(object):
            __slots__ = ['__dict__']

        class C3(object):
            __slots__ = ['__weakref__']

        # bug 364459

        class D1(C1, C2): pass
        class D2(C2, C1): pass
        class D3(C1, C3): pass
        class D4(C3, C1): pass
        class D5(C2, C3): pass
        class D6(C2, C3):
            __slots__ = 'b'

        # bug 367500
        class D7(C2, C3):
            __slots__ = ['__weakref__', 'c']

        def f():
            class D8(C2, C3): __slots__ = ['__dict__', 'd']

        # bug 367500
        self.assertRaisesPartialMessage(TypeError, "__dict__ slot disallowed: we already got one", f)

        for D in [
                    D1, D2, D3, D4,
                    D5, D6,
                    D7
                ]:
            d = D()
            d.not_in_slots = 20
            self.assertEqual(d.not_in_slots, 20)

        class C1(object):
            __slots__ = 'a'

        class C4(object): __slots__ = [ 'a', '__dict__']

        class C5(object): __slots__ = [ '__weakref__', 'c']

        def f1():
            class D(C1, C4): pass
        def f2():
            class D(C1, C5): pass

        for f in [f1, f2]:
            self.assertRaises(TypeError, f)

    def test_slots_wild_choices(self):
        class B: pass

        def f(x):
            class C(object): __slots__ = x

        for x in [
                    [1, 2],
                    1,
                    'ab cde',
                    [None],
                    None,
                    '',
                    [ type ],
                    [B],
                    ['a', f]
                ]:
            self.assertRaises(TypeError, f, x)

        for x in [
                    [],
                    (),
                    'abc',
                    ('abc', 'def'),
                ]:
            f(x)

    def test_slots_choices(self):
        # guess: all existing attributes should never be overwriten by __slots__ members

        class C(object):
            __slots__ = ['__slots__']

        x = C()
        self.assertEqual(x.__slots__, ['__slots__'])
        def f(): x.a = 1
        self.assertRaises(AttributeError, f)

        class C(object):
            __slots__ = ['m']
            def m(self): return 1

        x = C()
        self.assertEqual(x.m(), 1)

        class C(object):
            __slots__ = ['__dict__', 'c']
            __dict__ = {'a': 1, 'b': 2}

        x = C()
        self.assertRaises(AttributeError, lambda: x.a)
        self.assertEqual(len(x.__dict__), 2)
        x.c = 3
        self.assertEqual(x.c, 3)

    def test_name_mangling(self):
        class C(object):
            __slots__ = ['__a', '__b_', '__c__']

        x = C()
        x._C__a = 1
        x._C__b_ = 2
        x.__c__ = 3
        self.assertEqual([x._C__a, x._C__b_, x.__c__], [1, 2, 3])
        self.assertTrue('__a' not in dir(x))
        self.assertTrue('__b_' not in dir(x))

        # let us try this here too
        class C(object):
            def __init__(self):
                self._C__b_ = -4
                self.__a = -1
                self.__b_ = -2   # overwrite _C__b_
                self.__c__ = -3
        x = C()
        x._C__a = 4
        self.assertEqual([x._C__a, x._C__b_, x.__c__], [4, -2, -3])

    def test_from_sbs_newtype_test(self):
        class C(float): __slots__ = 'a'
        class D(object): pass
        class E(C, D): pass
        class F(E): pass

        class n1(object): pass
        class n2(object): __slots__ = 'a'
        class g(n1, n2): __slots__ = 'b'  # used to throw AttributeError

        class o2: pass
        class n2(object):
            __slots__ = ['a', 'b']
        class n6(n2): pass

        def f():
            class g(o2, n2, n6): pass    # used to throw ValueError: Index
        self.assertRaises(TypeError, f)

    # __slots__ do not work for classes derived from ``variable-length'' built-in types such
    # as long, str and tuple.
    def test_builtin(self):   # TODO
        def f(x):
            class C(x): __slots__ = ['a']

        #f(long)
        #f(tuple)


run_test(__name__)
