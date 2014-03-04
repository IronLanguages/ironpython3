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
from System.Runtime.InteropServices import COMException
from System import InvalidOperationException
from System.Reflection import TargetParameterCountException
from Microsoft.Scripting import ArgumentTypeException

com_type_name = "DlrComLibrary.DlrComServer"

#------------------------------------------------------------------------------
# Create a COM object
com_obj = getRCWFromProgID(com_type_name)

def test_DlrComServerArrays():
    dlrComServer = com_obj

    data = dlrComServer.GetObjArray() # returns 2 objects - one is itself, another one is something else
    Assert(data.Length == 2)
    Assert(dlrComServer.Equals(data[0]) == True)
    Assert(dlrComServer.Equals(data[1]) == False)

    data = dlrComServer.GetIntArray() # returns 5 ints - 1, 2, 3, 4, 5
    Assert(data.Length == 5)
    Assert(data[0] == 1)
    Assert(data[1] == 2)
    Assert(data[2] == 3)
    Assert(data[3] == 4)
    Assert(data[4] == 5)

    data = dlrComServer.GetByteArray() # return byte string "GetByteArrayTestData"

    stream = System.IO.MemoryStream(data, False)
    reader = System.IO.StreamReader(stream, System.Text.UnicodeEncoding())
    s = reader.ReadToEnd()
    Assert(s == "GetByteArrayTestData")

def test_perfScenarios():
    AreEqual(com_obj.SimpleMethod(), None)
    AreEqual(com_obj.IntArguments(1, 2), None)
    AreEqual(com_obj.StringArguments("hello", "there"), None)
    AreEqual(com_obj.ObjectArguments(com_obj, com_obj), None)

def test_errorInfo():
    try:
        com_obj.TestErrorInfo()
    except COMException, e:
        # This is commented out to revisit it to see if we want to add coverage for str, or if we are
        # happy to have coverage just for e.Message
        # AreEqual("Test error message" in str(e), True)
        AreEqual("Test error message", e.Message)

@disabled("COM dispatch mode doesn't support documentation")
def test_documentation():
    import IronPython
    ops = IronPython.Hosting.Python.CreateRuntime().GetEngine('py').Operations
    AreEqual(ops.GetDocumentation(com_obj.IntArguments), "void IntArguments(Int32 arg1, Int32 arg2)")

@disabled('CodePlex bug 19282')
def test_method_equality():
    AreEqual(com_obj.SumArgs, com_obj.SumArgs)
    Assert(com_obj.SumArgs != com_obj.IntArguments)
    com_obj2 = getRCWFromProgID(com_type_name)
    Assert(com_obj.SumArgs != com_obj2.SumArgs)
    
    #Use COM methods as dicitonary keys
    d = {}
    d[com_obj.SumArgs] = "SumArgs"
    AreEqual(d[com_obj.SumArgs], "SumArgs")    
    d[com_obj.IntArguments] = "IntArguments"
    AreEqual(d[com_obj.IntArguments], "IntArguments")
    d[com_obj.SumArgs] = "SumArgs2"
    AreEqual(d[com_obj.SumArgs], "SumArgs2")
    d[com_obj2.SumArgs] = "obj2_SumArgs"
    AreEqual(d[com_obj2.SumArgs], "obj2_SumArgs")
    AreEqual(d, {com_obj.SumArgs:"SumArgs2", com_obj.IntArguments:"IntArguments", com_obj2.SumArgs:"obj2_SumArgs"})

def test_namedArgs():
    # Named arguments
    AreEqual(12345, com_obj.SumArgs(1, 2, 3, 4, 5))
    AreEqual(12345, com_obj.SumArgs(1, 2, 3, 4, a5=5))
    AreEqual(12345, com_obj.SumArgs(1, 2, 3, a4=4, a5=5))
    AreEqual(12345, com_obj.SumArgs(a1=1, a2=2, a3=3, a4=4, a5=5))
    AreEqual(12345, com_obj.SumArgs(a5=5, a4=4, a3=3, a2=2, a1=1))
    
    # kwargs
    AreEqual(12345, com_obj.SumArgs(1, 2, 3, 4, **{"a5":5}))
    AreEqual(12345, com_obj.SumArgs(1, 2, 3, **{"a4":4, "a5":5}))
    AreEqual(12345, com_obj.SumArgs(**{"a1":1, "a2":2, "a3":3, "a4":4, "a5":5}))
    AreEqual(12345, com_obj.SumArgs(**{"a5":5, "a4":4, "a3":3, "a2":2, "a1":1}))

    # Named arguments and kwargs
    AreEqual(12345, com_obj.SumArgs(1, 2, a5=5, **{"a4":4, "a3":3}))
    

    # DISP_E_UNKNOWNNAME
    AssertError(COMException, com_obj.SumArgs, 1, 2, 3, 4, 5, **{"a6":6, "bugid":"TODO"})
    
    AssertError(StandardError, com_obj.SumArgs, 1, 2, 3, 4, 5, **{"a5":5, "bugid":"TODO"}) 

#Verify that one is able to enumerate over the object in a loop
#TODO: add more tests for enumerators - bad enumerator, different array sizes, different types.
def test_enumerator():
    AreEqual( [x for x in com_obj.GetEnumerator()] , [ 42, True, "DLR"] )
    AreEqual( [x for x in com_obj] , [ 42, True, "DLR"] )
    
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)
