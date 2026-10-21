# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import unittest

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
            f.raw.write(b"abcxxxx")
            f.raw.seek(0)
            self.assertEqual(f.raw.read(2), b"ab")
            f.raw.seek(0, 2)
            f.raw.write(b"def")
            self.assertEqual(f.raw.read(2), b"")
            f.raw.seek(0)
            self.assertEqual(f.raw.read(), b"abcxxxxdef")
            f.close()
        finally:
            os.close(fd)

    @unittest.skipIf(is_windows, "umask is emulated on Windows")
    def test_umask(self):
        old = os.umask(0o022)
        try:
            self.assertEqual(os.umask(0o077), 0o022)
            self.assertEqual(os.umask(0o022), 0o077)

            os.umask(0o027)
            try:
                fd = os.open(self.temp_file, os.O_CREAT | os.O_WRONLY, 0o666)
                os.close(fd)
                self.assertEqual(os.stat(self.temp_file).st_mode & 0o777, 0o640)
            finally:
                self.delete_files(self.temp_file)
        finally:
            os.umask(old)

run_test(__name__)
