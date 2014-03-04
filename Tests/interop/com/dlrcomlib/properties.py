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
skiptest("silverlight")
from System import DateTime
from clr import StrongBox
from iptest.cominterop_util import *

com_type_name = "DlrComLibrary.Properties"
com_obj = getRCWFromProgID(com_type_name)

test_sanity_types_data = [
    ("pBstr", "abcd"),
    ("pVariant", 42),
    ("pVariant", "42"),
    ("pLong", 12345),
    ]    
#Verify that types are marshalled properly for properties. This is just a sanity check
#since parmainretval covers marshalling extensively.
def test_sanity_types():
    for propName, val in test_sanity_types_data:
        setattr(com_obj, propName, val)
        AreEqual(getattr(com_obj, propName), val)
    now = DateTime.Now
    com_obj.pDate = now
    AreEqual(str(com_obj.pDate), str(now))        

#Verify properties with propputref act as expected.
@skip("multiple_execute")
def test_ref_properties():    
    com_obj.RefProperty = com_obj
    AreEqual(com_obj.RefProperty, com_obj)
    AreEqual(com_obj.RefProperty, com_obj)
    AreEqual(com_obj.RefProperty, com_obj)
    AreEqual(com_obj.RefProperty, com_obj)
    
    # We'll always prefer a put to a putref
    com_obj.PutAndPutRefProperty = 2.0
    
    # if we call the putref by accident this will end up as 4.0, which is incorrect
    AreEqual(com_obj.PutAndPutRefProperty, 2.0)
    

#Verify that readonly and writeonly properties work as expected.
def test_restricted_properties():
    c = com_obj.ReadOnlyProperty
    AssertError(StandardError, setattr, com_obj, "ReadOnlyProperty", "a")
	
    com_obj.WriteOnlyProperty = DateTime.Now

    # Dev10 409979 - This should work (just return a dispmethod)
    c = com_obj.WriteOnlyProperty
    try:
        com_obj.WriteOnlyProperty()
        Fail('hi')
    except AttributeError:
        pass		

#Validate behaviour of properties which take in parameters.
def test_properties_param():
    com_obj.PropertyWithParam[20] = 42
    AreEqual(com_obj.PropertyWithParam[0], 62)
    AreEqual(com_obj.PropertyWithParam[20], 42)
    
    strongVar = StrongBox[str]("a")
    com_obj.PropertyWithOutParam[strongVar] = "abcd"
    AreEqual(com_obj.PropertyWithOutParam[strongVar], "abcd")
    AreEqual(strongVar.Value, "abcd")
    
    com_obj.PropertyWithTwoParams[2, 2] = 2
    AreEqual(com_obj.PropertyWithTwoParams[0, 0], 6)
    AreEqual(com_obj.PropertyWithTwoParams[2,2], 2)
  
#Validate that one is able to call default properties with indexers.
def test_default_property():
    com_obj[23] = True
    AreEqual(com_obj[23], True)

# Dev10 410003 - not supported
#Call the get_ and set_ methods of the properties.
#def test_propeties_as_methods():
#    for propName, val in test_sanity_types_data:
#        Assert(not hasattr(com_obj, "set_" + propName))
#        Assert(not hasattr(com_obj, "get_" + propName))
    
#    AssertError(AttributeError, getattr, com_obj, "set_ReadOnlyProperty")
#    AssertError(AttributeError, getattr, com_obj, "get_WriteOnlyProperty")
    
#    com_obj.set_PropertyWithParam(20, 42)
#    AreEqual(com_obj.get_PropertyWithParam(0), 62)
        
#------------------------------------------------------------------------------------
run_com_test(__name__, __file__)
#------------------------------------------------------------------------------------