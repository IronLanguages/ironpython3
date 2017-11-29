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
This tests what CPythons test_types.py does not hit.
'''

import types
import unittest

from iptest import run_test

class TypesTest(unittest.TestCase):
    def test_cp24741(self):
        m = types.ModuleType('m')
        self.assertEqual(m.__doc__, None)

run_test(__name__)
