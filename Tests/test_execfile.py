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
