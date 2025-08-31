# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# COM Interop tests for IronPython
from iptest.assert_util import skiptest
skiptest("win32")

from iptest.cominterop_util import *
from System import DateTime, TimeSpan, Reflection, UInt32, String
from System.Runtime.InteropServices import COMException
from clr import StrongBox

com_type_name = "DlrComLibrary.MultipleParams"

#------------------------------------------------------------------------------
# Create a COM object
com_obj = getRCWFromProgID(com_type_name)

#------------------------------------------------------------------------------

test_data =  [
				("mZeroParams", (), None ),
				("mOneParam", ("someString",), "someString"),
				("mTwoParams", (25,25), 50),
				("mThreeParams", (33.33, 33.33, 33.33), 99.99),
				("mFourParams", (True, 10000, "a", 10000), 20000),
				("mFiveParams", ("qwerty", 3.0, "poiuy", 4.5, 3.5), 11)				
			 ]
    
def test_multiple_params():
	AreEqual(com_obj.mZeroParams(), None)
	AreEqual(com_obj.mOneParamNoRetval("a"), None)
	AreEqual(com_obj.mOneParam("iron python"), "iron python")
	AreEqual(com_obj.mTwoParams(25, 25), 50)
	AreEqual(com_obj.mThreeParams(33.33, 33.33, 33.33), 99.99)
	AreEqual(com_obj.mFourParams(True, 10000, "A", 10000), 20000)
	AreEqual(com_obj.mFiveParams("qwerty", 3.0, "poiuy", 4.5, 3.5), 11)
	
def test_tuple_unpacking():
	for funcName, args, expectedValue in test_data:		
		func = getattr(com_obj, funcName)
		AreEqual(func(*args), expectedValue)
		
		
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)