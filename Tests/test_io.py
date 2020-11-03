# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import io
import os
import unittest

from iptest import run_test

class BufferedWriterTest(unittest.TestCase):
    def test_flush(self):        
        # calling flush on the BufferedWriter shouldn't flush the underlying raw
        # https://github.com/IronLanguages/ironpython3/issues/392
        b = io.BytesIO()
        f = io.BufferedWriter(io.BufferedWriter(b))
        f.write(b"a")
        f.flush()
        self.assertEqual(b.getvalue(), b"")
        f.raw.flush()
        self.assertEqual(b.getvalue(), b"a")

run_test(__name__)