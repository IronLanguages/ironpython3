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

import sys
import unittest

from iptest import IronPythonTestCase, is_cli, run_test

@unittest.skipUnless(is_cli, 'IronPython specific test')
class AttrInjectorTestCase(IronPythonTestCase):
    def setUp(self):
        super(AttrInjectorTestCase, self).setUp()
        self.load_iron_python_test()

    def test_attributes_injector(self):
        from IronPythonTest import AttrInjectorTest
        # load XML Dom
        x = AttrInjectorTest.LoadXml('<root><foo>foo text</foo><bar><baz>baz text</baz></bar></root>')

        # access injected attributes

        self.assertEqual(x.GetType().Name, 'XmlElement')
        self.assertEqual(x.foo.GetType().Name, 'String')
        self.assertEqual(x.foo, 'foo text')

        self.assertEqual(x.GetType().Name, 'XmlElement')
        self.assertEqual(x.bar.GetType().Name, 'XmlElement')
        self.assertEqual(x.bar.baz.GetType().Name, 'String')
        self.assertEqual(x.bar.baz, 'baz text')

    def operator_test(self, a):
        self.assertEqual(a.doesnotexist, 42)
        
        # GetBoundMember shouldn't be called for pre-existing attributes, and we verify the name we got before.
        self.assertEqual(a.Name, 'doesnotexist')
        
        # SetMember should be called for sets
        a.somethingelse = 123
        self.assertEqual(a.Name, 'somethingelse')
        self.assertEqual(a.Value, 123)

    def test_get_set_extended(self):
        from IronPythonTest import ExtendedClass
        self.operator_test(ExtendedClass())

    def test_get_set_instance(self):
        from IronPythonTest import OperatorTest
        self.operator_test(OperatorTest())

    #
    # test_overload_*
    # http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=26277
    #

    def test_overload_0_GetCustomMember(self):
        from IronPythonTest import MemberOverloadTest
        x = MemberOverloadTest()
        self.assertEqual(x.Prop, 0)

    def test_overload_1_GetBoundMember(self):
        from IronPythonTest import MemberOverloadTest
        x = MemberOverloadTest()
        self.assertRaises(AttributeError, getattr, x, "Prop_Nonexistent")

    def test_overload_2_SetMember(self):
        from IronPythonTest import MemberOverloadTest
        x = MemberOverloadTest()
        x.Prop = 5
        self.assertEqual(x.Prop, 5)

    def test_overload_3_DeleteMember(self):
        from IronPythonTest import MemberOverloadTest
        x = MemberOverloadTest()
        def f():
            del x.Prop
        self.assertRaises(AttributeError, f)

run_test(__name__)
