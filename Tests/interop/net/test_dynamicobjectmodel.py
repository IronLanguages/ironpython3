# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class DynamicObjectModelTest(IronPythonTestCase):
    def setUp(self):
        super(DynamicObjectModelTest, self).setUp()
        self.add_clr_assemblies("dynamicobjmodel")

    def test_before_members(self):
        from Merlin.Testing.DynamicObjectModel import  TestBeforeMembers
        obj = TestBeforeMembers()

        self.assertEqual(obj.foo, "custom string")
        self.assertEqual(obj.normal, None)
        self.assertRaises(AttributeError, lambda: getattr(obj, "bar"))

        obj.spam = "hello"
        self.assertEqual(obj.spam, "hello")
        obj.normal = "normalString"
        self.assertEqual(obj.normal, "normalString")
        self.assertRaises(AttributeError, lambda: setattr(obj, "foo" , "somestring"))

        self.assertRaises(AttributeError, lambda: delattr(obj,"foo"))	

    def test_before_members_hijack(self):
        from Merlin.Testing.DynamicObjectModel import  TestBeforeMemberHijack
        obj = TestBeforeMemberHijack()

        #Although foo exists on the class we can't access it
        self.assertTrue("foo" in dir(TestBeforeMemberHijack))
        self.assertRaises(KeyError, lambda: obj.foo)
            
        self.assertEqual(dir(obj), [])	
        obj.foo = 42
        self.assertEqual(obj.foo, 42)

        self.assertEqual(dir(obj), ["foo"])

        del obj.foo
        self.assertEqual(dir(obj), [])
        # The error is a KeyError and not AttributeError that would have been thrown normally
        self.assertRaises(KeyError, lambda: obj.foo)

        #set attributes to functions.
        obj.bar = lambda: "newstring"
        self.assertEqual(obj.bar(), "newstring")	

        #try accessing constants/statics. GetMember should be called before that as well.
        self.assertTrue("PI" in  dir(TestBeforeMemberHijack))
        self.assertRaises(KeyError, lambda: obj.PI)
	
    def test_after_members(self):
        from Merlin.Testing.DynamicObjectModel import TestAfterMembers
        obj = TestAfterMembers()

        self.assertEqual(obj.foo, "original string")
        self.assertEqual(obj.bar, "custom string")
        self.assertRaises(AttributeError, lambda: getattr(obj, "x"))

        obj.spamsetter = "hello"
        self.assertEqual(obj.spam, "hello")
        self.assertRaises(AttributeError, lambda: setattr(obj, "bar" , "somestring"))

        self.assertRaises(AttributeError, lambda: delattr(obj,"foo"))
	
    def test_after_members_hijack(self):
        from Merlin.Testing.DynamicObjectModel import  TestAfterMemberHijack
        obj = TestAfterMemberHijack()

        #Although foo exists on the class we can't access it
        self.assertTrue("foo" not in dir(obj))
        self.assertEqual(obj.foo, "original string")
            
        self.assertEqual(dir(obj), [])	
        obj.bar = "newstring"
        self.assertEqual(obj.bar, "newstring")

        self.assertEqual(dir(obj), ["bar"])

        del obj.bar
        self.assertEqual(dir(obj), [])
        # The error is a KeyError and not AttributeError that would have been thrown normally
        self.assertRaises(KeyError, lambda: obj.bar)

        #set attributes to functions.
        obj.bar = lambda: "newstring"
        self.assertEqual(obj.bar(), "newstring")	
	

run_test(__name__)