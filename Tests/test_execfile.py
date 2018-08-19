# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import unittest

from iptest import IronPythonTestCase, run_test


class ExecFileTest(IronPythonTestCase):
    def test_sanity(self):
        root = self.test_dir
        execfile(os.path.join(root, "Inc", "toexec.py"))
        execfile(os.path.join(root, "Inc", "toexec.py"))
        #execfile(root + "/doc.py")
        execfile(os.path.join(root, "Inc", "toexec.py"))

    def test_negative(self):
        self.assertRaises(TypeError, execfile, None) # arg must be string
        self.assertRaises(TypeError, execfile, [])
        self.assertRaises(TypeError, execfile, 1)
        self.assertRaises(TypeError, execfile, "somefile", "")

    def test_scope(self):
        root = self.test_dir
        z = 10
        execfile(os.path.join(root, "Inc", "execfile_scope.py"))
    
run_test(__name__)
