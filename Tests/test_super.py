# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Michael van der Kolff
#

##
## Test whether super() behaves as expected
##

import unittest
import warnings
from test.support import check_warnings

from iptest import run_test

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

class SuperTest(unittest.TestCase):
    def test_classSupermethods(self):
        for cls in (A,B,C,D):
            self.assertEqual(cls.cls_getDoc(), cls.__doc__)

    def test_instanceSupermethods(self):
        for cls in (A,B,C,D):
            self.assertEqual(cls().inst_getDoc(), cls.__doc__)

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
