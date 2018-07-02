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
        from ctypes import *
        try:
            res = cast(windll.kernel32.GetCurrentProcess, c_void_p)
            if not isinstance(res, ctypes.c_void_p):
                self.fail("c_void_p expected in test_cfuncptr")
        except Exception as ex:
            self.fail("Unexpected exception: %s" % ex)


run_test(__name__)

