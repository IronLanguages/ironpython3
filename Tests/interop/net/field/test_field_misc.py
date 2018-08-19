# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
Operations on enum type and its' members
'''
#------------------------------------------------------------------------------

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class FieldMiscTest(IronPythonTestCase):
    def setUp(self):
        super(FieldMiscTest, self).setUp()
        self.add_clr_assemblies("fieldtests", "typesamples")

    def test_accessibility(self):
        from Merlin.Testing.FieldTest import DerivedMisc, Misc
        o = Misc()
        o.Set()
        self.assertEqual(o.PublicField, 100)
        self.assertTrue(not hasattr(o, 'ProtectedField'))
        self.assertRaisesRegexp(AttributeError, "'Misc' object has no attribute 'PrivateField'", lambda: o.PrivateField)
        self.assertEqual(o.InterfaceField.PublicStaticField, 500)
        
        o = DerivedMisc()
        o.Set()
        self.assertEqual(o.PublicField, 400)
        self.assertTrue(not hasattr(o, 'ProtectedField'))

run_test(__name__)

