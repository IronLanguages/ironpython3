# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import unittest

from iptest import IronPythonTestCase, path_modifier, run_test, stdout_trapper, is_netcoreapp

class ZipImportTest(IronPythonTestCase):
    
    @unittest.skipIf(is_netcoreapp, "TODO: figure out")
    def test_encoded_module(self):
        """https://github.com/IronLanguages/ironpython2/issues/129"""
        with path_modifier(os.path.join(self.test_dir, 'gh129.zip')):
            import something
            self.assertEqual(something.test(), u'\u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440!')

run_test(__name__)