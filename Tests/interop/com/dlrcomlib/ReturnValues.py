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

# COM Interop tests for IronPython
from iptest.assert_util import skiptest
skiptest("win32")
from iptest.cominterop_util import *
from System import NullReferenceException
from System.Runtime.InteropServices import COMException

com_type_name = "DlrComLibrary.ReturnValues"

#------------------------------------------------------------------------------
# Create a COM object
com_obj = getRCWFromProgID(com_type_name)

#------------------------------------------------------------------------------

#Test making calls to COM methods which return non-HRESULT values
def test_nonHRESULT_retvals():
    com_obj.mNoRetVal()  #void
    AreEqual(com_obj.mIntRetVal(), 42) #int
    #The method with two return values is a signature that tlbimp cant handle so it skips it.
    try:
        AreEqual(com_obj.mTwoRetVals(), 42) #Todo: What should be the expected behaviour for the IDispatch mode - 42 or [3,42]
    except AttributeError:
        pass    
    
#Test making calls to COM methods which return error values of HRESULT.
def test_HRESULT_Error():
    AssertErrorWithPartialMessage(NullReferenceException, "Custom error message for E_POINTER", com_obj.mNullRefException)
    
    # The CLR COM interop support does not use the custom error message provided by the user.
    #AssertErrorWithPartialMessage(NullReferenceException, "Object reference not set to an instance of an object.", com_obj.mNullRefException, bugid="NEEDED?")
    
    if is_vista:
        AssertErrorWithPartialMessage(COMException, "Migration source incorrect. (Exception from HRESULT: 0x8028005E)", com_obj.mGenericCOMException)
    else:
        print("By Design: 409994")
    
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)
