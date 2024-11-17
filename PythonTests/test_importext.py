# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest

from iptest import IronPythonTestCase, run_test


class ImportExtant(IronPythonTestCase):
    def test_gh936(self):
        # https://github.com/IronLanguages/ironpython3/issues/936

        extra_import_path = os.path.join(self.test_dir, "import_extant") 
        sys.path.insert(0, extra_import_path)
        try:
            m = __import__('extern.packaging.version')
            self.assertEqual(m.__name__, "extern")
            self.assertEqual(m.__package__, "extern")
            self.assertTrue(hasattr(m, "VendorImporter"))
            self.assertTrue(hasattr(m, "packaging"))

            self.assertEqual(m.packaging.__name__, "_vendor.packaging")
            self.assertEqual(m.packaging.__package__, "_vendor.packaging")

            self.assertEqual(m.packaging.version.__name__, "extern.packaging.version")
            self.assertEqual(m.packaging.version.__package__, "extern.packaging")

            self.assertIn("extern", sys.modules.keys())
            self.assertIs(m, sys.modules["extern"])

            self.assertIn("extern.packaging", sys.modules.keys())
            self.assertIs(m.packaging, sys.modules["extern.packaging"])

            self.assertIn("extern.packaging.version", sys.modules.keys())
            self.assertIs(m.packaging.version, sys.modules["extern.packaging.version"])

            self.assertIsNone(m.packaging.version.Infinity)
        finally:
            sys.path.remove(extra_import_path)

run_test(__name__)

