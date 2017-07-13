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

