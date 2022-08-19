# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Michael van der Kolff and contributors
#

##
## Test whether super() behaves as expected
##

import sys
import warnings
from test.support import check_warnings

from iptest import IronPythonTestCase, is_cli, run_test

def naked_super():
    """super() call without an encompassing class in a parameterless function"""
    super()

def naked_super_w_arg(x):
    """super() call without an encompassing class in a function with one positional parameter"""
    super()

def naked_super_w_del_arg(x):
    """super() call without an encompassing class in a function with one positional parameter that gets deleted"""
    del x
    super()

class A(object):
    """Doc string A"""
    @classmethod
    def cls_getDoc(cls):
        return cls.__doc__
    
    def inst_getDoc(self):
        return self.__doc__

class B(A):
    """Doc string B"""
    @classmethod
    def cls_getDoc(cls):
        return super(B,cls).cls_getDoc()
    
    def inst_getDoc(self):
        return super(B,self).inst_getDoc()

class C(B):
    """Doc string C"""
    pass

class D(B):
    """Doc string D"""
    @classmethod
    def cls_getDoc(cls):
        return super(D,cls).cls_getDoc()
    
    def inst_getDoc(self):
        return super(D,self).inst_getDoc()

class SuperTest(IronPythonTestCase):
    def test_classSupermethods(self):
        for cls in (A,B,C,D):
            self.assertEqual(cls.cls_getDoc(), cls.__doc__)

    def test_instanceSupermethods(self):
        for cls in (A,B,C,D):
            self.assertEqual(cls().inst_getDoc(), cls.__doc__)

    def test_class_variable(self):
        class C:
            def f(self):
                return self.__class__
            def g(self):
                return __class__
            def h():
                return __class__
            @staticmethod
            def j():
                return __class__
            @classmethod
            def k(cls):
                return __class__

        self.assertEqual(C().f(), C)
        self.assertEqual(C().g(), C)
        self.assertEqual(C.h(), C)
        self.assertEqual(C.j(), C)
        self.assertEqual(C.k(), C)
        self.assertEqual(C().k(), C)

        # Test that a metaclass implemented as a function sets __class__ at a proper moment.
        def makeclass(name, bases, attrs):
            attrNames = set(attrs.keys())
            self.assertRaisesMessage(NameError, "free variable '__class__' referenced before assignment in enclosing scope", attrs['getclass'], None)
            if (is_cli or sys.version_info >= (3, 6)):
                self.assertIn('__classcell__', attrNames)

            t = type(name, bases, attrs)

            if (is_cli or sys.version_info >= (3, 6)):
                self.assertEqual(t.getclass(None), t) # __class__ is set right after the type is created
            else: # CPython 3.5-
                self.assertRaisesMessage(NameError, "free variable '__class__' referenced before assignment in enclosing scope", attrs['getclass'], None)

            t.some_data = True
            self.assertEqual(set(attrs.keys()), attrNames) # set of attrs is not modified by type creation

            return t

        class A(metaclass=makeclass):
            def getclass(self):
                return __class__

        self.assertEqual(A.getclass(None), A)
        self.assertEqual(A().getclass(), A)

        dirA = dir(A)
        self.assertIn('getclass', dirA)
        self.assertIn('getclass', A.__dict__)
        self.assertIn('__class__', dirA)
        self.assertNotIn('__class__', A.__dict__)
        self.assertNotIn('__classcell__', dirA)
        self.assertNotIn('__classcell__', A.__dict__)

        # Variable __class__ can be deleted by a member method.
        class CD:
            def f(self):
                return __class__
            def d(self):
                nonlocal __class__  # __class__ is local to class CD, so nonlocal here
                __class__ = None
                return __class__
        cd = CD()
        self.assertEqual(cd.f(), CD)
        self.assertEqual(cd.d(), None)
        self.assertEqual(cd.f(), None)
        self.assertEqual(cd.__class__, CD) # atribute __class__ is not changed

        # Variable __class__ cannot be deleted in the body of class lambda.
        with self.assertRaisesMessage(NameError, "name '__class__' is not defined"):
            class CDXX:
                def f(self):
                    return __class__
                del __class__

        # Variable __class__ can be deleted in the body of class lambda
        # if it was first initialized, but it will be set again by type__.new__
        class CDX:
            def f(self):
                return __class__
            __class__ = None
            del __class__
        self.assertEqual(CDX().f(), CDX)

        # __class__ is not local to a function
        class CE:
            def f(self):
                return __class__
            def e(self):
                return eval('__class__')
            def ecl(self):
                return eval('__class__', globals(), CE.class_locals)
            def l(self):
                return locals()
            class_locals = locals()
            has_class_yet = '__class__' in class_locals # False, __class__ not set until type.__new__ is called

        self.assertEqual(CE().f(), CE)
        self.assertRaisesMessage(NameError, "name '__class__' is not defined", CE().e)
        self.assertNotIn('__class__', CE().l())
        self.assertFalse(CE.has_class_yet)
        if (is_cli):
            self.assertIn('__class__', CE.class_locals)
            self.assertEqual(CE().ecl(), CE)
        else:
            self.assertNotIn('__class__', CE.class_locals)
            self.assertRaisesMessage(NameError, "name '__class__' is not defined", CE().ecl)

    def test_classcell_propagation(self):
        # If a class method uses __class__, the class namespace contains __classcell__ which has to reach type.__new__
        # The test below uses MyDict for namespace, which discards all attributes, including __classcell__
        # This triggers a warning in Python 3.6 and an error in Python 3.8
        with warnings.catch_warnings(record=True) as ws:
            warnings.simplefilter("always")

            with self.assertWarnsRegex(DeprecationWarning, r"^__class__ not set defining 'MyClass' as <class '.*\.MyClass'>\. Was __classcell__ propagated to type\.__new__\?$"):
                class MyDict(dict):
                    def __setitem__(self, key, value):
                        pass

                class MetaClass(type):
                    @classmethod
                    def __prepare__(metacls, name, bases):
                        return MyDict()

                class MyClass(metaclass=MetaClass):
                    def test(self):
                        return __class__

        self.assertEqual(len(ws), 0) # no unchecked warnings

        # Here the warning is triggered because type.__new__ is not called at all.
        with warnings.catch_warnings(record=True) as ws:
            warnings.simplefilter("always")

            with self.assertWarnsRegex(DeprecationWarning, r"^__class__ not set defining 'bar' as <class '.*\.gez'>\. Was __classcell__ propagated to type\.__new__\?$"):
                class gez: pass

                def foo(*args):
                    return gez

                class bar(metaclass=foo):
                    def barfun(self):
                        return __class__

        self.assertEqual(len(ws), 0) # no unchecked warnings

        # This class uses a __classcell__ set after the end of class lambda.
        # It owerwrites the value assigned to __classcell__ in the body of the class lambda.
        class COK:
            def f(self):
                return __class__
            __classcell__ = None

        self.assertEqual(COK().f(), COK)

        # This class does not need a __classcell__ (because __class__ is not used) but defines it anyway in the body of the class lambda.
        # __classcell__ gets propagated to type.__new__ but its value is of a wrong type.
        with self.assertRaisesMessage(TypeError, "__classcell__ must be a nonlocal cell, not <class 'NoneType'>"):
            class CXX:
                __classcell__ = None

        class MetaXX(type):
            def __new__(cls, name, bases, attrs):
                attrs['__classcell__'] = True
                return super().__new__(cls, name, bases, attrs)

        # This class uses a __classcell__ that gets clobbered by MetaXX
        with self.assertRaisesMessage(TypeError, "__classcell__ must be a nonlocal cell, not <class 'bool'>"):
            class CXX(metaclass=MetaXX):
                def f(self):
                    return __class__

        # Pointing the cell reference at the wrong class is also prohibited
        # See also StdLib 3.6: test_super.TestSuper.test___classcell___wrong_cell
        class Meta(type):
            def __new__(cls, name, bases, namespace):
                cls = super().__new__(cls, name, bases, namespace)
                B = type("B", (), namespace)
                return cls

        # CPython's message is slightly incorrect
        with self.assertRaisesRegex(TypeError, r"^__class__ set to <class '.*'> defining 'A' as <class '.*\.A'>"):
            class A(metaclass=Meta):
                def f(self):
                    return __class__

    def test_classcell_access(self):
        # __classcell__, if used, should be defined only after the body of the class lambda
        with self.assertRaisesMessage(NameError, "name '__classcell__' is not defined"):
            class CXX:
                def f(self):
                    return __class__ # makes __classcell__ needed
                del __classcell__    # it should fail here...
            #CXX().f()               # ...not here

    def test___class___delayed(self):
        """Copy of Python 3.6: test_super.TestSuper.test___class___delayed"""
        test_namespace = None

        class Meta(type):
            def __new__(cls, name, bases, namespace):
                nonlocal test_namespace
                test_namespace = namespace
                return None

        # This case shouldn't trigger the __classcell__ deprecation warning
        with check_warnings() as w:
            warnings.simplefilter("always", DeprecationWarning)
            class A(metaclass=Meta):
                @staticmethod
                def f():
                    return __class__
        self.assertEqual(w.warnings, [])

        self.assertIs(A, None)

        B = type("B", (), test_namespace)
        self.assertIs(B.f(), B)

    def test_classcell_generation(self):
        # Test that __classcell__ is generated only when needed

        # Helper metaclasses
        class AssertHasClasscell(type):
            def __new__(cls, name, bases, namespace):
                self.assertTrue('__classcell__' in namespace)
                return type.__new__(cls, name, bases, namespace)

        class AssertHasNoClasscell(type):
            def __new__(cls, name, bases, namespace):
                self.assertFalse('__classcell__' in namespace)
                return type.__new__(cls, name, bases, namespace)

        # A regular class has no classcell by default
        class C(metaclass=AssertHasNoClasscell):
            def f(self):
                pass

        # A class which method uses __class__ has a classcell
        class C(metaclass=AssertHasClasscell):
            def f(self):
                return __class__

        # A class using super() has a classcell
        class C(metaclass=AssertHasClasscell):
            def f(self):
                return super()
        self.assertEqual(C().f().__thisclass__, C)
        self.assertEqual(C().f().__self_class__, C)

        # A class using suped in a parameterles method still has a classcell
        class C(metaclass=AssertHasClasscell):
            def f():
                return super()
        # though super() call fails
        with self.assertRaisesMessage(RuntimeError, "super(): no arguments"):
            C.f()

        # super() may be something else than the super type constructor, but it still triggers classcell generation
        def subtest():
            super = lambda: 42
            class C(metaclass=AssertHasClasscell):
                def f():
                    return super()
            self.assertEqual(C.f(), 42)
        subtest()

        # Calling super() in a class body does not trigger classcell generation.
        # Neither in the class of the call nor in the encompassing class.
        class C(metaclass=AssertHasNoClasscell):
            class D(metaclass=AssertHasNoClasscell):
                try: # This obviously fails...
                    super()
                except Exception as ex:
                    # ...because a class body has no arguments
                    self.assertEqual(str(ex), "super(): no arguments")

        # super() in a comprehension does trigger classcell generation
        # because it is in a subscope of the class
        class C(metaclass=AssertHasClasscell):
            try: # This obviously fails ...
                test = [super() for _ in range(1)]
            except RuntimeError as e:
                if is_cli:
                    # ...because a comprehension has no arguments
                    self.assertEqual(str(e), "super(): no arguments")
                else:
                    # though it does have in CPython (range(1)), however it fails anyway
                    # because the comprehension is evaluated before __class__ is set
                    self.assertEqual(str(e), "super(): empty __class__ cell")
        # This difference highlights a difference in the way IronPython and CPython handle comprehensions.
        # In CPython the for-expression is assigned to a parameter of the comprehension context.
        # IronPython optimizes that parameter away, and the for-expression is more like
        # an anonymous free variable from the outer context.
        # See also: https://github.com/IronLanguages/ironpython3/pull/1130

        # Similarly, super() in a generator expression does trigger classcell generation
        class C(metaclass=AssertHasClasscell):
            test = (super() for _ in range(1))
        # The class creation succeeds because the generator is nor evaluated yet
        # but the generator fails when it is evaluated
        msg = "super(type, obj): obj must be an instance or subtype of type"
        if is_cli:
            msg += " C, not range"
        with self.assertRaisesMessage(TypeError, msg):
            next(C.test)
        # Note that it raises TypeError, not RuntimeError

        # A regular super(cls, self) call also triggers classcell generation
        # though __class__ is not used
        class C(metaclass=AssertHasClasscell):
            def f(self):
                return super(C, self)
        self.assertEqual(C().f().__thisclass__, C)
        self.assertEqual(C().f().__self_class__, C)

        # Using super though an alias does not trigger classcell generation, although the alias works OK
        s_p_r = super
        class C(metaclass=AssertHasNoClasscell):
            def f(self):
                return s_p_r(C, self)
        self.assertEqual(C().f().__thisclass__, C)
        self.assertEqual(C().f().__self_class__, C)

        # Using the alias as a parameterless super call does not end well, since the classcell is not generated
        class C(metaclass=AssertHasNoClasscell):
            def f0():
                return s_p_r()
            def f1(self):
                return s_p_r()
        with self.assertRaisesMessage(RuntimeError, "super(): no arguments"):
            C.f0()

        if is_cli:
            # IronPython optimizes arguments by converting them to Expression parameters
            # if full access is deemed not needed (unless run with -X FullFrames)
            # so the arguments error gets reported first.
            # This difference from CPython is acceptable since PEP 3135 warns against aliased usage explicitly:
            # "calling a global alias of super without arguments will not necessarily work"
            msg = "super(): no arguments"
        else:
            msg = "super(): __class__ cell not found"
        with self.assertRaisesMessage(RuntimeError, msg):
            C().f1()

    def test_super_runtime_errors(self):
        # Test that RuntimeError is raised, (rather than TypeError, NameError, UnboundLocalError, or SystemError)

        # "no arguments" means no arguments to the encompasing function
        # "no arguments" gets reported before "__class__ cell not found", if both are missing
        with self.assertRaisesMessage(RuntimeError, "super(): no arguments"):
            naked_super()

        # Now that arg0 is provided, mising __class__ cell is reported
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ cell not found"):
            naked_super_w_arg(None)

        # The parameter must not be deleted
        with self.assertRaisesMessage(RuntimeError, "super(): arg[0] deleted"):
            naked_super_w_del_arg(None)

        # Only the first **positional** parameter is used, *args are ignored
        with self.assertRaisesMessage(RuntimeError, "super(): no arguments"):
            def f(*args):
                super()
            f(1, 2, 3)

        # Local __class__ variable is not enough
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ cell not found"):
            def f(x):
                __class__ = int
                return super()
            f(None)

        # Nonlocal __class__ variable is sufficient, though it does not come from a class cell
        def f():
            __class__ = int
            def g(x):
                return super()
            return g(None)
        self.assertEqual(f().__thisclass__, int)

        # Such nonlocal __class__ must be of type "type" though
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ is not a type (NoneType)"):
            def f():
                __class__ = None
                def g(x):
                    return super()
                return g(None)
            f()

        # Even if a class cell is found, it must be initialized
        with self.assertRaisesMessage(RuntimeError, "super(): empty __class__ cell"):
            class X:
                def f(x):
                    nonlocal __class__
                    del __class__
                    super()
            X().f()

        # ... and of type "type"
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ is not a type (NoneType)"):
            class X:
                def f(x):
                    nonlocal __class__
                    __class__ = None
                    super()
            X().f()

        # A local variable __class__ is not considered, but it shades the actual class cell from the class
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ cell not found"):
            class X:
                def f(self):
                    __class__ = X
                    super()
            X().f()

        # ...which makes super() different than super(__class__, self)
        class X:
            def f(self):
                __class__ = X
                return super(__class__, self)
        self.assertEqual(X().f().__thisclass__, X)

        # It also shades any nonlocal variable named __class__
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ cell not found"):
            class X:
                def f(self):
                    __class__ = X
                    def g(x):
                        __class__ = X
                        super()
                    g(None)
            X().f()

        # This also applies to __class__ as a class atribute variable (not a class cell)
        with self.assertRaisesMessage(RuntimeError, "super(): __class__ cell not found"):
            class X:
                __class__ = X
                def f(self):
                    __class__ = X
                    super()
            X().f()

run_test(__name__)
