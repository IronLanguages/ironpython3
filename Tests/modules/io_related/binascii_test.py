#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
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

##
## Test the binascii module
##

from iptest.assert_util import *

import binascii

# verify extra characters are ignored, and that we require padding.
@skip('win32')
def test_negative():
    for x in ('A', 'AB', '%%%A', 'A%%%', '%A%%', '%AA%' ):
            AssertError(binascii.Error, binascii.a2b_base64, x) # binascii.Error, incorrect padding

def test_positive():
    AreEqual(binascii.a2b_base64(''), '')
    AreEqual(binascii.a2b_base64('AAA='), '\x00\x00')
    AreEqual(binascii.a2b_base64('%%^^&&A%%&&**A**#%&A='), '\x00\x00')
    AreEqual(binascii.a2b_base64('w/A='), '\xc3\xf0')

def test_zeros():
    """verify zeros don't show up as being only a single character"""
    AreEqual(binascii.b2a_hex('\x00\x00\x10\x00'), '00001000')

@skip("win32")
def test_not_implemented():
    test_cases = [
                    lambda: binascii.a2b_qp(None),
                    lambda: binascii.a2b_qp(None, None),
                    lambda: binascii.a2b_hqx(None),
                    lambda: binascii.rledecode_hqx(None),
                    lambda: binascii.rlecode_hqx(None),
                    lambda: binascii.b2a_hqx(None),
                    lambda: binascii.crc_hqx(None, None),
                    ]
    for temp_func in test_cases:
        AssertError(NotImplementedError, temp_func)

run_test(__name__)
