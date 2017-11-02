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
