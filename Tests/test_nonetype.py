# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, is_cli, run_test

class NoneTypeTest(IronPythonTestCase):
        
    def test_trival(self):
        self.assertEqual(type(None), None.__class__)
        self.assertEqual(str(None), None.__str__())
        self.assertEqual(repr(None), None.__repr__())
        None.__init__('abc')
        if is_cli:
            self.assertRaisesMessage(TypeError, "NoneType is not callable", None)
        else:
            self.assertRaisesMessage(TypeError, "'NoneType' object is not callable", None)
    
run_test(__name__)
