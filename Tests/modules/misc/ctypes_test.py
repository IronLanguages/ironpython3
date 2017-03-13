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
import ctypes
import ctypes.wintypes

def test_cp34892():
    src = b''
    buf = ctypes.create_string_buffer(0)
    try:
        ctypes.memmove(ctypes.addressof(buf), src, 0)
    except Exception as ex:
        # there should be no exception of any kind
        Fail("Unexpected exception: %s" % ex)

@skip("posix")
def test_cp35326():
    GetStdHandle = ctypes.windll.kernel32.GetStdHandle
    GetStdHandle.argtypes = [ ctypes.wintypes.DWORD, ]
    GetStdHandle.restype = ctypes.wintypes.HANDLE
    try:
        GetStdHandle(-11)
    except Exception as ex:
        Fail("Unexpected exception: %s" % ex)

@skip("posix")
def test_gh951():
    from ctypes import *
    try:
        res = cast(windll.kernel32.GetCurrentProcess, c_void_p)
        if not isinstance(res, ctypes.c_void_p):
            Fail("c_void_p expected in test_cfuncptr")
    except Exception as ex:
        Fail("Unexpected exception: %s" % ex)


run_test(__name__)

