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

    def test_replaced_in_sys_modules(self):
        # a module that replaced itself in sys.modules must not be executed again
        extra_import_path = os.path.join(self.test_dir, "import_extant")
        sys.path.insert(0, extra_import_path)
        try:
            import replaced_by_class
            first = replaced_by_class
            self.assertIsInstance(first, type)
            import replaced_by_class
            self.assertIs(replaced_by_class, first)
            self.assertIs(sys.modules["replaced_by_class"], first)

            import replaced_by_builtin
            self.assertIs(replaced_by_builtin, len)
            import replaced_by_builtin
            self.assertIs(replaced_by_builtin, len)
            self.assertIs(sys.modules["replaced_by_builtin"], len)
        finally:
            sys.path.remove(extra_import_path)
            sys.modules.pop("replaced_by_class", None)
            sys.modules.pop("replaced_by_builtin", None)

    def test_fromlist_path_attribute(self):
        # fromlist submodules are imported for anything with a __path__, not only modules
        extra_import_path = os.path.join(self.test_dir, "import_extant")
        sys.path.insert(0, extra_import_path)
        try:
            from replaced_by_namespace import sub
            self.assertEqual(sub.value, "sub")
            self.assertIs(sys.modules["replaced_by_namespace.sub"], sub)
        finally:
            sys.path.remove(extra_import_path)
            sys.modules.pop("replaced_by_namespace", None)
            sys.modules.pop("replaced_by_namespace.sub", None)

    def test_relative_replaced_submodule(self):
        # the top-level module of a relative import comes from the resolved name, not the submodule's __name__
        extra_import_path = os.path.join(self.test_dir, "import_extant")
        sys.path.insert(0, extra_import_path)
        try:
            import replaced_sub
            m = __import__("a.b", {"__name__": "replaced_sub", "__package__": "replaced_sub"}, None, (), 1)
            self.assertIs(m, sys.modules["replaced_sub.a"])
            self.assertIsInstance(sys.modules["replaced_sub.a.b"], type)
        finally:
            sys.path.remove(extra_import_path)
            for name in ("replaced_sub", "replaced_sub.a", "replaced_sub.a.b"):
                sys.modules.pop(name, None)

run_test(__name__)

