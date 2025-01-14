# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests the _mmap standard module.
'''

import sys
import os
import errno
import mmap

from iptest import IronPythonTestCase, is_cli, is_posix, is_windows, run_test

class MmapTest(IronPythonTestCase):

    def setUp(self):
        super(MmapTest, self).setUp()

        self.temp_file = os.path.join(self.temporary_dir, "temp_%d.dat" % os.getpid())

    def tearDown(self):
        self.delete_files(self.temp_file)
        return super().tearDown()


    def test_constants(self):
        self.assertTrue(hasattr(mmap, "PAGESIZE"))
        self.assertTrue(hasattr(mmap, "ALLOCATIONGRANULARITY"))

        self.assertEqual(mmap.ACCESS_READ, 1)
        self.assertEqual(mmap.ACCESS_WRITE, 2)
        self.assertEqual(mmap.ACCESS_COPY, 3)
        if sys.version_info >= (3, 7) or is_cli:
            self.assertEqual(mmap.ACCESS_DEFAULT, 0)
        self.assertFalse(hasattr(mmap, "ACCESS_NONE"))

        if is_posix:
            self.assertEqual(mmap.MAP_SHARED, 1)
            self.assertEqual(mmap.MAP_PRIVATE, 2)
            self.assertEqual(mmap.PROT_READ, 1)
            self.assertEqual(mmap.PROT_WRITE, 2)
            self.assertEqual(mmap.PROT_EXEC, 4)


    def test_resize_errors(self):
        with open(self.temp_file, "wb+") as f:
            f.write(b"x" * mmap.ALLOCATIONGRANULARITY * 2)

        with open(self.temp_file, "rb+") as f:
            m = mmap.mmap(f.fileno(), 0, offset=0)
            with self.assertRaises(OSError) as cm:
                m.resize(0)

            self.assertEqual(cm.exception.errno, errno.EINVAL)  # 22
            if is_windows:
                self.assertEqual(cm.exception.winerror, 1006)  #  ERROR_FILE_INVALID
                self.assertEqual(cm.exception.strerror, "The volume for a file has been externally altered so that the opened file is no longer valid")
            else:
                self.assertEqual(cm.exception.strerror, "Invalid argument")


    def test_resize_errors_negative(self):
        with open(self.temp_file, "wb+") as f:
            f.write(b"x" * mmap.ALLOCATIONGRANULARITY * 2)

        with open(self.temp_file, "rb+") as f:
            m = mmap.mmap(f.fileno(), 0, offset=0)
            if is_cli or sys.version_info >= (3, 5):
                self.assertRaises(ValueError, m.resize, -1)
            else:
                self.assertRaises(OSError, m.resize, -1)

            m.close()


    def test_resize_errors_offset(self):
        with open(self.temp_file, "wb+") as f:
            f.write(b"x" * mmap.ALLOCATIONGRANULARITY * 2)

        with open(self.temp_file, "rb+") as f:
            m = mmap.mmap(f.fileno(), 0, offset=mmap.ALLOCATIONGRANULARITY)

            if is_windows:
                with self.assertRaises(PermissionError) as cm:
                    m.resize(0)
                self.assertEqual(cm.exception.errno, errno.EACCES)  # 13
                self.assertEqual(cm.exception.winerror, 5)  # ERROR_ACCESS_DENIED
                self.assertEqual(cm.exception.strerror, "Access is denied")
            else:
                with self.assertRaises(OSError) as cm:
                    m.resize(0)
                self.assertEqual(cm.exception.errno, errno.EINVAL)  # 22
                self.assertEqual(cm.exception.strerror, "Invalid argument")
            m.close()


run_test(__name__)

