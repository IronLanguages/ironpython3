# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import bz2
import unittest

from iptest import run_test

class BZ2Test(unittest.TestCase):
    def test_bz2file(self):
        """https://github.com/IronLanguages/ironpython2/pull/739"""

        # BZ2File should not fail on invalid files, only on read
        with bz2.BZ2File(__file__, 'r') as f:
            with self.assertRaises(IOError):
                f.read()

run_test(__name__)
