# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
How to re-define operators methods, __hash__, etc.

NOTES:
- the tests in this module (could) expose some degree of implementation details. 
  Therefore it may be necessary to update the test cases upon failures.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class SpecialMethodTest(IronPythonTestCase):
    def setUp(self):
        super(SpecialMethodTest, self).setUp()
        self.add_clr_assemblies("operators", "typesamples")

    def test_basic(self): 
        from Merlin.Testing.BaseClass import Callback, COperator10
        class C(COperator10):
            def __add__(self, other):
                return C(self.Value * other.Value)
                
        x = C(4)
        y = C(5)
        z = x + y
        self.assertEqual(z.Value, 20)
        
        z = Callback.On(x, y)
        #AreEqual(z.Value, 20)  

run_test(__name__)
