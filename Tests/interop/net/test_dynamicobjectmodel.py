#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
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
'''
?
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("dynamicobjmodel")

from Merlin.Testing.DynamicObjectModel import *

def test_before_members():
    obj = TestBeforeMembers()

    AreEqual(obj.foo, "custom string")
    AreEqual(obj.normal, None)
    AssertError(AttributeError, lambda: getattr(obj, "bar"))

    obj.spam = "hello"
    AreEqual(obj.spam, "hello")
    obj.normal = "normalString"
    AreEqual(obj.normal, "normalString")
    AssertError(AttributeError, lambda: setattr(obj, "foo" , "somestring"))

    AssertError(AttributeError, lambda: delattr(obj,"foo"))	

def test_before_members_hijack():
    obj = TestBeforeMemberHijack()

    #Although foo exists on the class we can't access it
    Assert("foo" in dir(TestBeforeMemberHijack))
    AssertError(KeyError, lambda: obj.foo)
    	
    AreEqual(dir(obj), [])	
    obj.foo = 42
    AreEqual(obj.foo, 42)

    AreEqual(dir(obj), ["foo"])

    del obj.foo
    AreEqual(dir(obj), [])
    # The error is a KeyError and not AttributeError that would have been thrown normally
    AssertError(KeyError, lambda: obj.foo)

    #set attributes to functions.
    obj.bar = lambda: "newstring"
    AreEqual(obj.bar(), "newstring")	

    #try accessing constants/statics. GetMember should be called before that as well.
    Assert("PI" in  dir(TestBeforeMemberHijack))
    AssertError(KeyError, lambda: obj.PI)
	
def test_after_members():
    obj = TestAfterMembers()

    AreEqual(obj.foo, "original string")
    AreEqual(obj.bar, "custom string")
    AssertError(AttributeError, lambda: getattr(obj, "x"))

    obj.spamsetter = "hello"
    AreEqual(obj.spam, "hello")
    AssertError(AttributeError, lambda: setattr(obj, "bar" , "somestring"))

    AssertError(AttributeError, lambda: delattr(obj,"foo"))
	
def test_after_members_hijack():
    obj = TestAfterMemberHijack()

    #Although foo exists on the class we can't access it
    Assert("foo" not in dir(obj))
    AreEqual(obj.foo, "original string")
    	
    AreEqual(dir(obj), [])	
    obj.bar = "newstring"
    AreEqual(obj.bar, "newstring")

    AreEqual(dir(obj), ["bar"])

    del obj.bar
    AreEqual(dir(obj), [])
    # The error is a KeyError and not AttributeError that would have been thrown normally
    AssertError(KeyError, lambda: obj.bar)

    #set attributes to functions.
    obj.bar = lambda: "newstring"
    AreEqual(obj.bar(), "newstring")	
	
#####################################################################################
run_test(__name__)
#####################################################################################