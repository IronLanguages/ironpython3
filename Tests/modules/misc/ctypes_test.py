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

def test_cp34892():
    src = b''
    buf = ctypes.create_string_buffer(0)
    try:
        ctypes.memmove(ctypes.addressof(buf), src, 0)
    except:
        # there should be no exception of any kind
        Assert(False)

run_test(__name__)

