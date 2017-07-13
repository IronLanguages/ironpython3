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

import unittest

from iptest import IronPythonTestCase, run_test

class NoneTypeTest(IronPythonTestCase):
        
    def test_trival(self):
        self.assertEqual(type(None), None.__class__)
        self.assertEqual(str(None), None.__str__())
        self.assertEqual(repr(None), None.__repr__())
        None.__init__('abc')
        self.assertRaisesMessage(TypeError, 'NoneType', lambda : None())
    
run_test(__name__)
