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

from iptest.assert_util import *
skiptest("win32")

load_iron_python_test()
import IronPythonTest

import System
#from IronPython.Runtime.Exceptions import ExceptionConverter as EC
#from IronPython.Runtime.Exceptions import ExceptionMapping

# CreatePythonException

def test_CreatePythonException_name():
    e = EC.CreatePythonException("foo")
    AreEqual("foo", e.__name__)

def test_CreatePythonException_defaultmodule():
    e = EC.CreatePythonException("bar")
    AreEqual("exceptions", e.__module__)

def test_CreatePythonException_othermodule():
    e = EC.CreatePythonException("baz", "quux")
    AreEqual("quux", e.__module__)

def test_CreatePythonException_otherbase():
    class Base:
        pass

    e = EC.CreatePythonException("abc", "exceptions", Base)
    Assert(issubclass(e, Base), "wrong base type")

def test_CreatePythonException_doublecreate_identity():
    e1 = EC.CreatePythonException("hey")
    e2 = EC.CreatePythonException("hey")

    AreEqual(e1, e2)

def test_CreatePythonException_differentbases():
    class NewBase:
        pass

    e1 = EC.CreatePythonException("hey", "exceptions")
    success = False
    try:
        e2 = EC.CreatePythonException("hey", "exceptions", NewBase)
    except System.InvalidOperationException:
        success = True

    Assert(success, "creation of exception with same name and different base class should have failed")

# GetPythonException

def test_GetPythonException_nonexistant():
    success = False
    try:
        e = EC.GetPythonException("neverneverland")
    except System.Collections.Generic.KeyNotFoundException:
        success = True

    Assert(success, "lookup of nonexistant Python exception should have failed")

def test_GetPythonException_defaultmodule():
    e = EC.CreatePythonException("fiddle")
    AreEqual(e, EC.GetPythonException("fiddle"))
    AreEqual(e, EC.GetPythonException("fiddle", "exceptions"))

def test_GetPythonException_othermodule():
    e = EC.CreatePythonException("qix", "mymodule")
    AreEqual(e, EC.GetPythonException("qix", "mymodule"))

# CreateExceptionMapping

def test_CreateExceptionMapping_Py2CLR_NoMapping():
    pyex1 = EC.CreatePythonException("PythonException1")
    success = False
    try:
        raise pyex1()
    except IronPythonTest.CLRException1:
        success = False
    except pyex1:
        success = True

    Assert(success, "CLR version of Python exception caught even without mapping")

def test_CreateExceptionMapping_Py2CLR_WithMapping():
    pyex2 = EC.CreatePythonException("PythonException2")

    m = ExceptionMapping("PythonException2", IronPythonTest.CLRException2)
    EC.CreateExceptionMapping(EC.GetPythonException("Exception"), m)

    success = False
    try:
        raise pyex2()
    except IronPythonTest.CLRException2:
        success = True

    Assert(success, "CLR version of Python exception not caught, despite mapping")

def test_CreateExceptionMapping_CLR2Py_NoMapping():
    pyex3 = EC.CreatePythonException("PythonException3")
    success = False
    try:
        raise IronPythonTest.CLRException3()
    except pyex3:
        success = False
    except IronPythonTest.CLRException3:
        success = True

    Assert(success, "Python version of CLR exception caught even without mapping")

def test_CreateExceptionMapping_CLR2Py_WithMapping():
    pyex4 = EC.CreatePythonException("PythonException4")

    m = ExceptionMapping("PythonException4", IronPythonTest.CLRException4)
    EC.CreateExceptionMapping(EC.GetPythonException("Exception"), m)

    success = False
    try:
        raise pyex4()
    except IronPythonTest.CLRException4:
        success = True

    Assert(success, "Python version of CLR exception not caught, despite mapping")

#run_test(__name__)


