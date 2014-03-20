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
skiptest("win32", "silverlight", "cli64")
from iptest.cominterop_util import *
from System import *
from System.Runtime.InteropServices import COMException, DispatchWrapper, UnknownWrapper, CurrencyWrapper
from clr import StrongBox

com_type_name = "DlrComLibrary.OutParams"

#------------------------------------------------------------------------------
# Create a COM object
com_obj = getRCWFromProgID(com_type_name)

#------------------------------------------------------------------------------
#-------------------------------Helpers----------------------------------------
#------------------------------------------------------------------------------

def testhelper(function, type, values, equality_func=AreEqual):
    for i in xrange(len(values)):
        try:
            strongVar1 = StrongBox[type]()
            function(values[i], strongVar1)
        except Exception, e:
            print "FAILED trying to pass", values[i], "of type", type(values[i]) ,"to", function#.__name__
            raise e
        
        for j in xrange(i, len(values)):
            equality_func(values[i], values[j]) #Make sure no test issues first
        
            try:
                strongVar2 = StrongBox[type]()                
                function(values[j], strongVar2)
            except Exception, e:
                print "FAILED trying to pass", values[j], "of type", type(values[j]) ,"to", function#.__name__
                raise e
            
            equality_func(strongVar1.Value, strongVar2.Value)
            
def callMethodWithStrongBox(func, arg, outparamType):
    strongArg = StrongBox[outparamType]()
    func(arg, strongArg)
    return strongArg.Value
#---------------------------------------------------------------------------------    
    
simple_primitives_data = [
	 ("mVariantBool",bool,	 True),
     ("mByte",       Byte,   Byte.MaxValue),
     ("mChar",       SByte,  SByte.MaxValue),
     ("mUShort",     UInt16, UInt16.MaxValue),
     ("mShort",      Int16,  Int16.MaxValue),
     ("mUlong",      UInt32, UInt32.MaxValue),
     ("mLong",       int,    Int32.MaxValue),
     ("mULongLong",  UInt64, UInt64.MaxValue),
     ("mLongLong",   Int64,  Int64.MaxValue),
     ("mFloat",      Single, Single.MaxValue),
     ("mDouble",     float,  Double.MaxValue)]

def test_sanity():
    for test_data in simple_primitives_data:
        testFunctionName = test_data[0]
        testType = test_data[1]
        testValue = test_data[2]

        testFunction = getattr(com_obj, testFunctionName)
        strongBox = StrongBox[testType]()
        testFunction(testValue, strongBox)
        AreEqual(strongBox.Value, testValue)
        
        testFunction(1, strongBox)
        AreEqual(strongBox.Value, 1)
        
        testFunction(0, strongBox)
        AreEqual(strongBox.Value, 0)
    
    AreEqual(callMethodWithStrongBox(com_obj.mBstr, "Hello", str), "Hello")
    
    #Interface Types    
    strongVar = StrongBox[object](DispatchWrapper(None))
    com_obj.mIDispatch(com_obj, strongVar)
    AreEqual(strongVar.Value.WrappedObject, com_obj)

    strongVar = StrongBox[object](UnknownWrapper(None))
    com_obj.mIUnknown(com_obj, strongVar)
    AreEqual(strongVar.Value.WrappedObject, com_obj)    
    
    #Complex Types
    strongVar = StrongBox[object](CurrencyWrapper(444))
    com_obj.mCy(CurrencyWrapper(123), strongVar)
    AreEqual(strongVar.Value.WrappedObject, 123)    
    
    now = DateTime.Now
    AreEqual(str(callMethodWithStrongBox(com_obj.mDate, now, DateTime)), str(now))        
        
    AreEqual(callMethodWithStrongBox(com_obj.mVariant, Single(2.0), object), 2.0)    

def test_negative():
        
    com_obj.mDouble(Double.MaxValue, Double.MaxValue)

    #See Dev10 409981 for more info            
    AssertError(StandardError, com_obj.mDouble, Double.MaxValue)     
    AssertError(StandardError, com_obj.mCy, Decimal(0)) 

#------------------------------------------------------------------------------
def test_variant_bool():
    for test_list in pythonToCOM("VARIANT_BOOL"):
        testhelper(com_obj.mVariantBool, bool, test_list)
        
#------------------------------------------------------------------------------
def test_byte():
    for test_list in pythonToCOM("BYTE"):
        testhelper(com_obj.mByte, Byte, test_list)

#------------------------------------------------------------------------------
def test_bstr():
    for test_list in pythonToCOM("BSTR"):
        testhelper(com_obj.mBstr, str, test_list)

#------------------------------------------------------------------------------
def test_char():
    for test_list in pythonToCOM("CHAR"):
        testhelper(com_obj.mChar, SByte, test_list)

#------------------------------------------------------------------------------
def test_float():
    for test_list in pythonToCOM("FLOAT"):
        testhelper(com_obj.mFloat, Single, test_list, equality_func=AlmostEqual)

    #Min/Max float values
    AssertError(OverflowError, com_obj.mFloat, 3.402823e+039)

#------------------------------------------------------------------------------
def test_double():
    for test_list in pythonToCOM("DOUBLE"):
        testhelper(com_obj.mDouble, float, test_list, equality_func=AlmostEqual)

    #Min/Max double values
    Assert(str(callMethodWithStrongBox(com_obj.mDouble, -1.797693134864e+309, float)), "-1.#INF") 
    Assert(str(callMethodWithStrongBox(com_obj.mDouble, 1.797693134862313e309, float)), "1.#INF")

#------------------------------------------------------------------------------
def test_ushort():
    for test_list in pythonToCOM("USHORT"):
        testhelper(com_obj.mUShort, UInt16, test_list)
        
#------------------------------------------------------------------------------
def test_ulong():
    for test_list in pythonToCOM("ULONG"):
        testhelper(com_obj.mUlong, UInt32, test_list)

#------------------------------------------------------------------------------
#@disabled("Dev10 409933")
def test_ulonglong():
    for test_list in pythonToCOM("ULONG"):  #Hack:  See bug on why we're using ULong instead of ULongLong
        testhelper(com_obj.mULongLong, UInt64, test_list)

#------------------------------------------------------------------------------
def test_short():
    for test_list in pythonToCOM("SHORT"):
        testhelper(com_obj.mShort, Int16, test_list)

#------------------------------------------------------------------------------
def test_long():
    for test_list in pythonToCOM("LONG"):
        testhelper(com_obj.mLong, int, test_list)

#------------------------------------------------------------------------------
#@disabled("Dev10 409933")
def test_longlong():
    for test_list in pythonToCOM("LONG"): #Hack:  See bug on why we're using Long instead of LongLong
        testhelper(com_obj.mLongLong, Int64, test_list)

#------------------------------------------------------------------------------
run_com_test(__name__, __file__)
