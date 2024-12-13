# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os

from iptest import IronPythonTestCase, is_osx, is_linux, is_windows, run_test

class OsTest(IronPythonTestCase):
    def setUp(self):
        super(OsTest, self).setUp()
        self.temp_file = os.path.join(self.temporary_dir, "temp_OSTest_%d.dat" % os.getpid())

    def tearDown(self):
        self.delete_files(self.temp_file)
        return super().tearDown()

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

    def test_open_abplus(self):
        # equivalent to open(self.temp_file, "ab+"), see also test_file.test_open_abplus
        fd = os.open(self.temp_file, os.O_APPEND | os.O_CREAT | os.O_RDWR)
        try:
            f = open(fd, mode="ab+", closefd=False)
            f.write(b"abc")
            f.seek(0)
            self.assertEqual(f.read(2), b"ab")
            f.write(b"def")
            self.assertEqual(f.read(2), b"")
            f.seek(0)
            self.assertEqual(f.read(6), b"abcdef")
            f.close()
        finally:
            os.close(fd)

run_test(__name__)
