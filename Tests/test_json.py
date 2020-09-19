# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import json
import unittest

from iptest import run_test

class JsonTest(unittest.TestCase):

    def test_ipy3_gh926(self):
        """https://github.com/IronLanguages/ironpython3/issues/926"""
        
        self.assertEqual(json.dumps([]), "[]")

run_test(__name__)
