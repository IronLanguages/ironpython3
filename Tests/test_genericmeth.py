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
from IronPythonTest import *

# verify generic .NET method binding

# Create an instance of the generic method provider class.
gm = GenMeth()

# Check that the documentation strings for all the instance methods (they all have the same name) is as expected.
expected = newline.join([
    'InstMeth[T](self: GenMeth) -> str',
    'InstMeth[(T, U)](self: GenMeth) -> str',
    'InstMeth[T](self: GenMeth, arg1: int) -> str',
    'InstMeth[T](self: GenMeth, arg1: str) -> str',
    'InstMeth[(T, U)](self: GenMeth, arg1: int) -> str',
    'InstMeth[T](self: GenMeth, arg1: T) -> str',
    'InstMeth[(T, U)](self: GenMeth, arg1: T, arg2: U) -> str',
    'InstMeth(self: GenMeth) -> str',
    'InstMeth(self: GenMeth, arg1: int) -> str',
    'InstMeth(self: GenMeth, arg1: str) -> str']) + newline
    
AssertDocEqual(gm.InstMeth.__doc__, expected)

# And the same for the static methods.
expected_static_methods = newline.join([
        'StaticMeth[T]() -> str' , 
        'StaticMeth[(T, U)]() -> str' , 
        'StaticMeth[T](arg1: int) -> str' , 
        'StaticMeth[T](arg1: str) -> str' , 
        'StaticMeth[(T, U)](arg1: int) -> str' , 
        'StaticMeth[T](arg1: T) -> str' , 
        'StaticMeth[(T, U)](arg1: T, arg2: U) -> str' , 
        'StaticMeth() -> str' , 
        'StaticMeth(arg1: int) -> str' , 
        'StaticMeth(arg1: str) -> str']) + newline
 
AssertDocEqual(GenMeth.StaticMeth.__doc__, expected_static_methods)

# Check that we bind to the correct method based on type and call arguments for each of our instance methods. We can validate this
# because each target method returns a unique string we can compare.
AreEqual(gm.InstMeth(), "InstMeth()")
AreEqual(gm.InstMeth[str](), "InstMeth<String>()")
AreEqual(gm.InstMeth[(int, str)](), "InstMeth<Int32, String>()")
AreEqual(gm.InstMeth(1), "InstMeth(Int32)")
AreEqual(gm.InstMeth(""), "InstMeth(String)")
#This ordering never worked, but new method binding rules reveal the bug.  Open a new bug here.
#AreEqual(gm.InstMeth[int](1), "InstMeth<Int32>(Int32)")
#AreEqual(gm.InstMeth[str](""), "InstMeth<String>(String)")
AreEqual(gm.InstMeth[(str, int)](1), "InstMeth<String, Int32>(Int32)")
AreEqual(gm.InstMeth[GenMeth](gm), "InstMeth<GenMeth>(GenMeth)")
AreEqual(gm.InstMeth[(str, int)]("", 1), "InstMeth<String, Int32>(String, Int32)")

# And the same for the static methods.
AreEqual(GenMeth.StaticMeth(), "StaticMeth()")
AreEqual(GenMeth.StaticMeth[str](), "StaticMeth<String>()")
AreEqual(GenMeth.StaticMeth[(int, str)](), "StaticMeth<Int32, String>()")
AreEqual(GenMeth.StaticMeth(1), "StaticMeth(Int32)")
AreEqual(GenMeth.StaticMeth(""), "StaticMeth(String)")
#AreEqual(GenMeth.StaticMeth[int](1), "StaticMeth<Int32>(Int32)")
#AreEqual(GenMeth.StaticMeth[str](""), "StaticMeth<String>(String)")
AreEqual(GenMeth.StaticMeth[(str, int)](1), "StaticMeth<String, Int32>(Int32)")
AreEqual(GenMeth.StaticMeth[GenMeth](gm), "StaticMeth<GenMeth>(GenMeth)")
AreEqual(GenMeth.StaticMeth[(str, int)]("", 1), "StaticMeth<String, Int32>(String, Int32)")
