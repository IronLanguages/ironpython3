# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
This test module verifies that properties of COM object are identical to those
of Python object.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import skiptest
from iptest.cominterop_util import *

#------------------------------------------------------------------------------
#--GLOBALS
com_obj = getRCWFromProgID("DlrComLibrary.DlrUniversalObj")

def test_sanity_check():
    Assert("m0" in dir(com_obj))
    # Assert("m0" in vars(com_obj).keys())

#------------------------------------------------------------------------------
run_com_test(__name__, __file__)
