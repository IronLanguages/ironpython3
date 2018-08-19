# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
