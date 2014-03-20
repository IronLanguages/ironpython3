#####################################################################################
#
#  Copyright (c) Michael van der Kolff. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

##
## Test whether super() behaves as expected
##

from iptest.assert_util import *

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

def test_classSupermethods():
    for cls in (A,B,C,D):
        AreEqual(cls.cls_getDoc(), cls.__doc__)

def test_instanceSupermethods():
    for cls in (A,B,C,D):
        AreEqual(cls().inst_getDoc(), cls.__doc__)

run_test(__name__)
