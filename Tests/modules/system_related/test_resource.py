# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest
import resource

from iptest import IronPythonTestCase, is_cli, is_linux, path_modifier, run_test, skipUnlessIronPython

class ResourceTest(IronPythonTestCase):
    def test_getrlimit(self):
        RLIM_NLIMITS = 16 if is_linux else 9

        for r in range(RLIM_NLIMITS):
            lims = resource.getrlimit(r)
            self.assertIsInstance(lims, tuple)
            self.assertEqual(len(lims), 2)
            self.assertIsInstance(lims[0], int)
            self.assertIsInstance(lims[1], int)

run_test(__name__)
