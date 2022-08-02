# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Michael van der Kolff
#

##
## Test whether super() behaves as expected
##

import warnings
from test.support import check_warnings

from iptest import IronPythonTestCase, is_cli, run_test

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

run_test(__name__)
