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

run_test(__name__)
