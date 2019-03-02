# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Pawel Jasinski
#

import os
import unittest

from iptest import IronPythonTestCase, is_cli, run_test
from threading import Timer

flags = os.O_CREAT | os.O_TRUNC | os.O_RDWR

def is_open(fd):
    try:
        os.fstat(fd)
        return True
    except OSError:
        return False

def is_open_nul(fd):
    try:
        os.read(fd, 1)
        return True
    except OSError:
        return False

def xtest_unlink():
    fd = os.open("tmp.unlink.test-1", flags)
    os.close(fd)
    os.unlink("tmp.unlink.test-1")

class FdTest(IronPythonTestCase):
    def test_dup2(self):
        test_filename = "tmp.dup2.test-1"
        # test inspired by gsasl dup2 test cases
        fd = os.open(test_filename, flags)

        # make sure fd+1 and fd+2 are closed
        os.closerange(fd + 1, fd + 2)

        # Assigning to self must be a no-op.
        self.assertEqual(os.dup2(fd, fd), fd)
        self.assertTrue(is_open(fd))

        # The source must be valid.
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, -1, fd)
        self.assertTrue(is_open(fd))
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, 99, fd)
        self.assertTrue(is_open(fd))

        # If the source is not open, then the destination is unaffected.
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd + 1, fd + 1)
        self.assertTrue(not is_open(fd + 1))

        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd + 1, fd)
        self.assertTrue(is_open(fd))

        # The destination must be valid.
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd, -2)
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd, 10000000)

        # Using dup2 can skip fds.
        self.assertEqual(os.dup2(fd, fd + 2), fd + 2)
        self.assertTrue(is_open(fd))
        self.assertTrue(not is_open(fd + 1))
        self.assertTrue(is_open(fd + 2))

        # Verify that dup2 closes the previous occupant of a fd.
        if is_cli:
            self.assertEqual(os.open(os.devnull, os.O_WRONLY, 0o600), fd + 1)
        else:
            self.assertEqual(os.open(os.devnull, os.O_RDWR, 0o600), fd + 1)
        self.assertEqual(os.dup2(fd + 1, fd), fd)
        # null can not be stated on windows - but writes are ok
        self.assertTrue(is_open_nul(fd))
        self.assertTrue(is_open_nul(fd + 1))
        os.close(fd + 1)
        self.assertTrue(is_open_nul(fd))
        self.assertEqual(os.write(fd, b"1"), 1)

        self.assertEqual(os.dup2(fd + 2, fd), fd)
        self.assertEqual(os.lseek(fd, 0, os.SEEK_END), 0)
        self.assertEqual(os.write(fd + 2, b"2"), 1)
        self.assertEqual(os.lseek(fd, 0, os.SEEK_SET), 0)
        self.assertEqual(os.read(fd, 1), b"2")

        os.close(fd)
        # fd+1 is already closed
        os.close(fd + 2)
        os.unlink(test_filename)

    def test_dup(self):
        test_filename = "tmp.dup.test-1"

        fd1 = os.open(test_filename, flags)

        # make sure fd+1 is closed
        os.closerange(fd1 + 1, fd1 + 1)

        # The source must be valid.
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup, -1)
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.dup, 99)

        # Test basic functionality.
        fd2 = os.dup(fd1)
        self.assertTrue(fd2 == fd1 + 1)
        self.assertTrue(is_open(fd2))
        self.assertTrue(is_open(fd1))
        os.close(fd1)
        self.assertTrue(not is_open(fd1))
        self.assertTrue(is_open(fd2))

        # dup uses the lowest-numbered unused descriptor for the new descriptor.
        fd3 = os.dup(fd2)
        self.assertEqual(fd3, fd1)

        # cleanup
        os.close(fd3)
        os.close(fd2)
        os.unlink(test_filename)

    def test_open(self):
        test_filename = "tmp.open.test"
        fd1 = os.open(test_filename + "1", flags)

        # make sure fd+1 and fd+2 are closed
        os.closerange(fd1 + 1, fd1 + 2)

        # open should return the lowest-numbered file descriptor not currently open
        # for the process
        fd2 = os.open(test_filename + "2", flags)
        fd3 = os.open(test_filename + "3", flags)

        os.close(fd2)
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.lseek, fd2, os.SEEK_SET, 0)

        fd4 = os.open(test_filename + "4", flags)
        self.assertEqual(fd4, fd2)

        os.close(fd1)
        os.close(fd3)
        os.close(fd4)

        for i in range(1, 5):
            os.unlink(test_filename + str(i))

    def test_write(self):
        test_filename = "tmp.write.test"

        # trivial write
        fd = os.open(test_filename, flags)
        self.assertEqual(os.write(fd, b"42"), 2)
        os.close(fd)
        os.unlink(test_filename)

        # write to closed file
        fd = os.open(test_filename, flags)
        os.close(fd)
        self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.write, fd, b"42")
        os.unlink(test_filename)

        # write to file with wrong permissions
        fd = os.open(test_filename, os.O_CREAT | os.O_TRUNC | os.O_RDONLY)
        if is_cli:
            self.assertRaisesMessage(ValueError, "File not open for writing", os.write, fd, b"42")
        else:
            self.assertRaisesMessage(OSError, "[Errno 9] Bad file descriptor", os.write, fd, b"42")
        os.close(fd)
        os.unlink(test_filename)

    def test_pipe(self):
        # basic
        r, w = os.pipe()
        self.assertEqual(os.write(w, b"hello"), 5)
        self.assertEqual(os.read(r, 5), b"hello")
        os.close(w)
        self.assertEqual(os.read(r, 1), b'')
        os.close(r)

    def test_pipe_write_closed(self):
        r, w = os.pipe()
        os.close(r)
        self.assertRaises(OSError, os.write, w, b"x")
        os.close(w)

    @unittest.skipIf(is_cli, "this will block")
    def test_pipe_block(self):
        r, w = os.pipe()

        def delayed_write():
            os.write(w, b"x")

        Timer(1, delayed_write).start()

        # this will block
        self.assertEqual(os.read(r, 1), b"x")

        os.close(r)
        os.close(w)

    def test_pipe_fds(self):
        # make sure the fds assigned by os.open, os.pipe and file do not collide
        # part of cp7267
        r, w = os.pipe()
        test_filename = "tmp.pipe_fds.test"
        f = open(test_filename + "1", "w")
        self.assertNotEqual(f.fileno(), r)
        self.assertNotEqual(f.fileno(), w)
        fd = os.open(test_filename + "2", flags)
        self.assertNotEqual(fd, f.fileno())
        self.assertNotEqual(fd, r)
        self.assertNotEqual(fd, w)
        os.close(r)
        os.close(w)
        os.close(fd)
        f.close()
        for i in range(1, 3):
            os.unlink(test_filename + str(i))

run_test(__name__)
