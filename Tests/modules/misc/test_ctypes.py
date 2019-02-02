# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Pawel Jasinski
#

import unittest

from iptest import is_posix, run_test

import ctypes
import ctypes.wintypes

class CtypesTest(unittest.TestCase):

    def test_cp34892(self):
        src = b''
        buf = ctypes.create_string_buffer(0)
        try:
            ctypes.memmove(ctypes.addressof(buf), src, 0)
        except Exception as ex:
            # there should be no exception of any kind
            self.fail("Unexpected exception: %s" % ex)

    @unittest.skipIf(is_posix, 'Windows specific test')
    def test_cp35326(self):
        GetStdHandle = ctypes.windll.kernel32.GetStdHandle
        GetStdHandle.argtypes = [ ctypes.wintypes.DWORD, ]
        GetStdHandle.restype = ctypes.wintypes.HANDLE
        try:
            GetStdHandle(-11)
        except Exception as ex:
            self.fail("Unexpected exception: %s" % ex)

    @unittest.skipIf(is_posix, 'Windows specific test')
    def test_gh951(self):
        try:
            res = ctypes.cast(ctypes.windll.kernel32.GetCurrentProcess, ctypes.c_void_p)
            if not isinstance(res, ctypes.c_void_p):
                self.fail("c_void_p expected in test_cfuncptr")
        except Exception as ex:
            self.fail("Unexpected exception: %s" % ex)


run_test(__name__)
