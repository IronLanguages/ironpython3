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
skiptest("win32", "silverlight")

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