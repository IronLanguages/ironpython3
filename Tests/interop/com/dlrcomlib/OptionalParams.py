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
from System import DateTime, TimeSpan, Reflection, Int32, String
from System.Runtime.InteropServices import COMException
from clr import StrongBox

com_type_name = "DlrComLibrary.OptionalParams"

#------------------------------------------------------------------------------
# Create a COM object
com_obj = getRCWFromProgID(com_type_name)

#------------------------------------------------------------------------------

def test_basic_optional_params():
    com_obj.mSingleOptionalParam()
    com_obj.mSingleOptionalParam("a")
    com_obj.mOneOptionalParam("a")
    com_obj.mOneOptionalParam("a", "b")
    com_obj.mTwoOptionalParams("a")
    com_obj.mTwoOptionalParams("a", "b")
    com_obj.mTwoOptionalParams("a", 10, 3)
    
def test_neg_wrong_number_params():
    for meth, params in [   (com_obj.mSingleOptionalParam, ["a", "b", "c"]),
                            (com_obj.mOneOptionalParam, ()), 
                            (com_obj.mOneOptionalParam, [1, 2, 3]),
                            (com_obj.mTwoOptionalParams, ()),
                            (com_obj.mTwoOptionalParams, ["a", "b", "c", "d"]),
                            (com_obj.mOptionalParamWithDefaultValue, ()),
                            (com_obj.mOptionalParamWithDefaultValue, ["a", "b", "c"]), ]:
        AssertError(Exception, meth, bugid="409926", *params)        
    
def test_defaultvalue():   
    AreEqual(com_obj.mOptionalParamWithDefaultValue("a"), 3)
    AreEqual(com_obj.mOptionalParamWithDefaultValue("a", "b"), "b")        
    
def test_optional_out_params():
    b = StrongBox[object]()
    com_obj.mOptionalOutParam("a", b)
    AreEqual(b.Value, "a")
    com_obj.mOptionalOutParam("a")
        
def test_optional_params_types():
    AreEqual('', com_obj.mOptionalStringParam())
    AreEqual(com_obj.mOptionalStringParam("a"), "a")    
    AreEqual(com_obj.mOptionalIntParam(3), 3)
    AreEqual(com_obj.mOptionalIntParam(), 0)

def test_python_keyword_syntax():
    com_obj.mSingleOptionalParam(a="a")
    com_obj.mOneOptionalParam("a", b="b")
    com_obj.mTwoOptionalParams("a", b=32.0)
    com_obj.mTwoOptionalParams("a", c=32.0)
    com_obj.mTwoOptionalParams("a", c=32.0, b="b")
    com_obj.mOptionalParamWithDefaultValue(3, b=5)
    #test out params with the keyword syntax
    strongObject = StrongBox[object]()
    com_obj.mOptionalOutParam("a", b=strongObject)
    AreEqual(strongObject.Value, "a")
    #test types with the keyword syntax
    
    AreEqual(com_obj.mOptionalStringParam(a="a"), "a")
    AreEqual(com_obj.mOptionalIntParam(a=3), 3)
    
def test_optional_kwargs():
    com_obj.mTwoOptionalParams(**{'a':3})
    com_obj.mTwoOptionalParams(**{'a':3, 'b':32})
    com_obj.mTwoOptionalParams(**{'a':3, 'c':33})
    com_obj.mTwoOptionalParams(**{'a':3, 'b':12, 'c':33})
    AssertError(Exception, com_obj.mTwoOptionalParams, **{'b':3, 'c':33, 'bugid':"TODO"})
    
    
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)
    
    