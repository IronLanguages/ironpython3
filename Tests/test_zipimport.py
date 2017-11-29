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

from iptest import IronPythonTestCase, path_modifier, run_test, stdout_trapper, is_netcoreapp

class ZipImportTest(IronPythonTestCase):
    
    @unittest.skipIf(is_netcoreapp, "TODO: figure out")
    def test_encoded_module(self):
        """https://github.com/IronLanguages/ironpython2/issues/129"""
        with path_modifier(os.path.join(self.test_dir, 'gh129.zip')):
            import something
            self.assertEqual(something.test(), u'\u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440!')

run_test(__name__)