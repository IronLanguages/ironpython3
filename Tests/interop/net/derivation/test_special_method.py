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
How to re-define operators methods, __hash__, etc.

NOTES:
- the tests in this module (could) expose some degree of implementation details. 
  Therefore it may be necessary to update the test cases upon failures.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

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
