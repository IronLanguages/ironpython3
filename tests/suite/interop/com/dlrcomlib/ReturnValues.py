# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
