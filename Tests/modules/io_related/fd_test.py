#####################################################################################
#
#  Copyright (c) Pawel Jasinski. All rights reserved.
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

from iptest.assert_util import *

import os
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

def test_dup2():
    test_filename = "tmp.dup2.test-1"
    # test inspired by gsasl dup2 test cases
    fd = os.open(test_filename, flags)

    # make sure fd+1 and fd+2 are closed
    os.closerange(fd + 1, fd + 2)

    # Assigning to self must be a no-op.
    AreEqual(os.dup2(fd, fd), fd)
    Assert(is_open(fd))
   
    # The source must be valid.
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, -1, fd)
    Assert(is_open(fd))
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, 99, fd)
    Assert(is_open(fd))

    # If the source is not open, then the destination is unaffected.
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd + 1, fd + 1)
    Assert(not is_open(fd + 1))
    
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd + 1, fd)
    Assert(is_open(fd))

    # The destination must be valid.
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd, -2)
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup2, fd, 10000000)

    # Using dup2 can skip fds.
    AreEqual(os.dup2(fd, fd + 2), fd + 2)
    Assert(is_open(fd))
    Assert(not is_open(fd + 1))
    Assert(is_open(fd + 2))

    # Verify that dup2 closes the previous occupant of a fd.
    AreEqual(os.open(os.devnull, os.O_WRONLY, 0o600), fd + 1)
    AreEqual(os.dup2(fd + 1, fd), fd)
    # null can not be stated on windows - but writes are ok
    Assert(is_open_nul(fd))
    Assert(is_open_nul(fd + 1))
    os.close(fd + 1)
    Assert(is_open_nul(fd))
    AreEqual(os.write(fd, "1"), 1)

    AreEqual(os.dup2(fd + 2, fd), fd)
    AreEqual(os.lseek(fd, 0, os.SEEK_END), None)
    AreEqual(os.write(fd + 2, "2"), 1)
    AreEqual(os.lseek(fd, 0, os.SEEK_SET), None)
    AreEqual(os.read(fd, 1), "2")

    os.close(fd)
    # fd+1 is already closed
    os.close(fd + 2)
    os.unlink(test_filename)

def test_dup():
    test_filename = "tmp.dup.test-1"

    fd1 = os.open(test_filename, flags)

    # make sure fd+1 is closed
    os.closerange(fd1 + 1, fd1 + 1)

    # The source must be valid.
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup, -1)
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.dup, 99)

    # Test basic functionality.
    fd2 = os.dup(fd1)
    Assert(fd2 == fd1 + 1)
    Assert(is_open(fd2))
    Assert(is_open(fd1))
    os.close(fd1)
    Assert(not is_open(fd1))
    Assert(is_open(fd2))

    # dup uses the lowest-numbered unused descriptor for the new descriptor.
    fd3 = os.dup(fd2)
    AreEqual(fd3, fd1)

    # cleanup
    os.close(fd3)
    os.close(fd2)
    os.unlink(test_filename)

def test_open():
    test_filename = "tmp.open.test"
    fd1 = os.open(test_filename + "1", flags)

    # make sure fd+1 and fd+2 are closed
    os.closerange(fd1 + 1, fd1 + 2)

    # open should return the lowest-numbered file descriptor not currently open
    # for the process
    fd2 = os.open(test_filename + "2", flags)
    fd3 = os.open(test_filename + "3", flags)

    os.close(fd2)
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.lseek, fd2, os.SEEK_SET, 0)

    fd4 = os.open(test_filename + "4", flags)
    AreEqual(fd4, fd2)

    os.close(fd1)
    os.close(fd3)
    os.close(fd4)

    for i in range(1, 5):
        os.unlink(test_filename + str(i))

def test_write():
    test_filename = "tmp.write.test"
    
    # trivial write
    fd = os.open(test_filename, flags)
    AreEqual(os.write(fd, "42"), 2)
    os.close(fd)
    os.unlink(test_filename)

    # write to closed file
    fd = os.open(test_filename, flags)
    os.close(fd)
    AssertErrorWithMessage(OSError, "[Errno 9] Bad file descriptor", os.write, fd, "42")
    os.unlink(test_filename)
    
    # write to file with wrong permissions
    fd = os.open(test_filename, os.O_CREAT | os.O_TRUNC | os.O_RDONLY)
    AssertErrorWithMessage(OSError, "[Errno -2146232800] Can not write to " + test_filename,  os.write, fd, "42")
    os.close(fd)
    os.unlink(test_filename)

def test_pipe():
    # basic
    r, w = os.pipe()
    AreEqual(os.write(w, "hello"), 5)
    AreEqual(os.read(r, 5), "hello")
    os.close(w)
    AreEqual(os.read(r, 1), '')
    os.close(r)

def test_pipe_write_closed():
    r, w = os.pipe()
    os.close(r)
    AssertError(OSError, os.write, w, "x")
    os.close(w)

def test_pipe_block():
    r, w = os.pipe()

    def delayed_write():
        os.write(w, "x")

    Timer(1, delayed_write).start()

    # this will block
    AreEqual(os.read(r, 1), "x")

    os.close(r)
    os.close(w)

def test_pipe_fds():
    # make sure the fds assigned by os.open, os.pipe and file do not collide
    # part of cp7267
    r, w = os.pipe()
    test_filename = "tmp.pipe_fds.test"
    f = file(test_filename + "1", "w")
    AreNotEqual(f.fileno(), r)
    AreNotEqual(f.fileno(), w)
    fd = os.open(test_filename + "2", flags)
    AreNotEqual(fd, f.fileno())
    AreNotEqual(fd, r)
    AreNotEqual(fd, w)
    os.close(r)
    os.close(w)
    os.close(fd)
    f.close()
    for i in range(1, 3):
        os.unlink(test_filename + str(i))


#--MAIN------------------------------------------------------------------------
run_test(__name__)
