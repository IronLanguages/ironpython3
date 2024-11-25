# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os

from iptest import IronPythonTestCase, is_osx, is_linux, is_windows, run_test

class OsTest(IronPythonTestCase):
    def test_strerror(self):
        if is_windows:
            self.assertEqual(os.strerror(0), "No error")
        elif is_linux:
            self.assertEqual(os.strerror(0), "Success")
        elif is_osx:
            self.assertEqual(os.strerror(0), "Undefined error: 0")

        if is_windows:
            self.assertEqual(os.strerror(39), "No locks available")
        elif is_linux:
            self.assertEqual(os.strerror(39), "Directory not empty")
        elif is_osx:
            self.assertEqual(os.strerror(39), "Destination address required")

        if is_windows:
            self.assertEqual(os.strerror(40), "Function not implemented")
        elif is_linux:
            self.assertEqual(os.strerror(40), "Too many levels of symbolic links")
        elif is_osx:
            self.assertEqual(os.strerror(40), "Message too long")

run_test(__name__)
