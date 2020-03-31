# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import shutil
import tempfile
import time
import unittest

from iptest import is_mono, is_osx

class ShutilTest(unittest.TestCase):
    def setUp(self):
        self.test_dir = tempfile.mkdtemp()
        self.test_data = b"qwerty"

    def tearDown(self):
        shutil.rmtree(self.test_dir)

    def test_copyfileobj(self):
        # TODO: implement me!
        pass

    def test_copyfile(self):
        from_filename = os.path.join(self.test_dir, "test_copyfile_from")
        with open(from_filename, "wb") as f: f.write(self.test_data)

        with self.assertRaises(shutil.SameFileError):
            shutil.copyfile(from_filename, from_filename)

        to_filename = os.path.join(self.test_dir, "test_copyfile_to")
        shutil.copyfile(from_filename, to_filename)
        with open(to_filename, "rb") as f:
            self.assertEqual(f.read(), self.test_data)

        # make sure we can overwrite an existing file
        with open(to_filename, "wb") as f:
            f.write(self.test_data * 2)
        shutil.copyfile(from_filename, to_filename)
        with open(to_filename, "rb") as f:
            self.assertEqual(f.read(), self.test_data)

    def test_copymode(self):
        from_filename = os.path.join(self.test_dir, "test_copymode_from")
        with open(from_filename, "wb"): pass
        os.chmod(from_filename, 0o642)
        from_stat = os.stat(from_filename)

        to_filename = os.path.join(self.test_dir, "test_copymode_to")
        with open(to_filename, "wb"): pass
        shutil.copymode(from_filename, to_filename)
        to_stat = os.stat(to_filename)

        self.assertEqual(from_stat.st_mode, to_stat.st_mode)

    def test_copystat(self):
        from_filename = os.path.join(self.test_dir, "test_copystat_from")
        with open(from_filename, "wb"): pass
        from_stat = os.stat(from_filename)

        time.sleep(0.1)

        to_filename = os.path.join(self.test_dir, "test_copystat_to")
        with open(to_filename, "wb"): pass
        shutil.copystat(from_filename, to_filename)
        to_stat = os.stat(to_filename)

        self.assertEqual(from_stat.st_mode, to_stat.st_mode)
        if is_mono and is_osx:
            self.assertAlmostEqual(from_stat.st_atime, to_stat.st_atime, places=5)
            self.assertAlmostEqual(from_stat.st_mtime, to_stat.st_mtime, places=5)
            self.assertAlmostEqual(to_stat.st_atime, to_stat.st_atime_ns / 1e9, places=5)
            self.assertAlmostEqual(to_stat.st_mtime, to_stat.st_mtime_ns / 1e9, places=5)
            self.assertEqual(to_stat.st_atime_ns % 1000, 0)
            self.assertEqual(to_stat.st_mtime_ns % 1000, 0)
            self.assertEqual(from_stat.st_atime_ns // 1000, to_stat.st_atime_ns // 1000)
            self.assertEqual(from_stat.st_mtime_ns // 1000, to_stat.st_mtime_ns // 1000)
        else:
            self.assertEqual(from_stat.st_atime, to_stat.st_atime)
            self.assertEqual(from_stat.st_mtime, to_stat.st_mtime)
            self.assertEqual(from_stat.st_atime_ns, to_stat.st_atime_ns)
            self.assertEqual(from_stat.st_mtime_ns, to_stat.st_mtime_ns)

    def test_copy(self):
        # TODO: implement me!
        pass

    def test_copy2(self):
        # TODO: implement me!
        pass

    def test_copytree(self):
        # TODO: implement me!
        pass

    def test_rmtree(self):
        # TODO: implement me!
        pass

    def test_move(self):
        # TODO: implement me!
        pass

    def test_disk_usage(self):
        # TODO: implement me!
        pass

    def test_chown(self):
        # TODO: implement me!
        pass

    def test_which(self):
        # TODO: implement me!
        pass

if __name__ == '__main__':
    unittest.main()
