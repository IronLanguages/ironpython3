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
import System
import System.IO
import System.ComponentModel
import System.Text
import System.Collections.Generic

# ExceptionMapping tests
# See exception mapping in the IP Wiki at http://channel9.msdn.com/wiki/default.aspx/IronPython.ExceptionModel

def clr_to_py_positive(clrExcep, pyExcep, excepMsg = None, msg = "CLR exception not mapped to specified Python exception"):
    try:
        raise clrExcep(excepMsg)
    except pyExcep:
        Assert(True)
    except:
        Assert(False, msg)

def py_to_clr_positive(pyExcep, clrExcep, msg = "Python exception not mapped to specified CLR exception"):
    try:
        raise pyExcep
    except clrExcep:
        Assert(True)
    except:
        Assert(False, msg)

def py_to_clr_positive_with_args(pyExcep, clrExcep, args, msg = "Python exception not mapped to specified CLR exception"):
    try:
        raise pyExcep(*args)
    except clrExcep:
        Assert(True)
    except Exception as e:
        print(e)
        Assert(False, msg)

# Tests for exact mappings

# BUG 515 System.SystemException not mapped to StandardError
#clr_to_py_positive(System.SystemException, StandardError)
# /BUG

clr_to_py_positive(System.IO.IOException, IOError, msg = "System.IO.IOException -> IOError")
py_to_clr_positive(IOError, System.IO.IOException, msg = "IOError -> System.IO.IOException")

clr_to_py_positive(System.MissingMemberException, AttributeError, msg ="System.MissingMemberException -> AttributeError")
py_to_clr_positive(AttributeError, System.MissingMemberException, msg = "AttributeError -> System.MissingMemberException")

if not is_silverlight:
    clr_to_py_positive(System.ComponentModel.Win32Exception, WindowsError, msg ="System.ComponentModel.Win32Exception -> WindowsError")
    py_to_clr_positive(WindowsError, System.ComponentModel.Win32Exception, msg = "WindowsError -> System.ComponentModel.Win32Exception")

clr_to_py_positive(System.IO.EndOfStreamException, EOFError, msg="System.IO.EndOfStreamException -> EOFError")
py_to_clr_positive(EOFError, System.IO.EndOfStreamException, msg = "EOFError -> System.IO.EndOfStreamException")

clr_to_py_positive(System.NotImplementedException, NotImplementedError, msg = "System.NotImplementedException -> NotImplementedError")
py_to_clr_positive(NotImplementedError, System.NotImplementedException, msg = "NotImplementedError -> System.NotImplementedException")

clr_to_py_positive(System.IndexOutOfRangeException, IndexError, msg = "System.IndexOutOfRangeException -> IndexError")
py_to_clr_positive(IndexError, System.IndexOutOfRangeException, msg = "IndexError -> System.IndexOutOfRangeException")

clr_to_py_positive(System.ArithmeticException, ArithmeticError, msg = "System.ArithmeticException -> ArithmeticError")
py_to_clr_positive(ArithmeticError, System.ArithmeticException, msg = "ArithmeticError -> System.ArithmeticException")

clr_to_py_positive(System.OverflowException, OverflowError, msg= "System.OverflowException -> OverflowError")
py_to_clr_positive(OverflowError, System.OverflowException, msg = "OverflowError -> System.OverflowException")

clr_to_py_positive(System.DivideByZeroException, ZeroDivisionError, msg ="System.DivideByZeroException -> ZeroDivisionError")
py_to_clr_positive(ZeroDivisionError, System.DivideByZeroException, msg = "ZeroDivisionError -> System.DivideByZeroException")

clr_to_py_positive(System.ArgumentException, ValueError, msg = "System.ArgumentException -> ValueError")
py_to_clr_positive(ValueError, System.ArgumentException, msg = "ValueError-> System.ArgumentException")

if not is_silverlight:
    clr_to_py_positive(System.Text.DecoderFallbackException, UnicodeDecodeError)
    py_to_clr_positive_with_args(UnicodeDecodeError, System.Text.DecoderFallbackException, ('abc','abc',3,4,'abc'))

    clr_to_py_positive(System.Text.EncoderFallbackException, UnicodeEncodeError)
    py_to_clr_positive_with_args(UnicodeEncodeError, System.Text.EncoderFallbackException, ('abc','abc',3,4,'abc'))

    clr_to_py_positive(System.ComponentModel.WarningException, Warning, msg = "System.ComponentModel.WarningException -> Warning")
    py_to_clr_positive(Warning, System.ComponentModel.WarningException, msg = "Warning -> System.ComponentModel.WarningException")

clr_to_py_positive(System.OutOfMemoryException, MemoryError, msg = "System.OutOfMemoryException -> MemoryError")
py_to_clr_positive(MemoryError, System.OutOfMemoryException, msg = "MemoryError -> System.OutOfMemoryException")

clr_to_py_positive(System.Collections.Generic.KeyNotFoundException, KeyError, msg = "System.Collections.Generic.KeyNotFoundException -> KeyError")
py_to_clr_positive(KeyError, System.Collections.Generic.KeyNotFoundException, msg = "KeyError -> System.Collections.Generic.KeyNotFoundException")
# Tests for inexact mappings

# Let CLR exception class A map to Python error type B;
#  let A' be a direct subclass of A, with *no* direct mapping to a Python error type;
# then A' should be caught in Iron Python as an instance of B.
# In other words, A' should be mapped using the *first* available mapping found while
#  searching upwards in its type hierarchy.
clr_to_py_positive(System.NotFiniteNumberException, ArithmeticError, msg = "System.NotFiniteNumberException -> ArithmeticError")

# StopIteration can be caught as an Exception
py_to_clr_positive(StopIteration, System.Exception, msg = "StopIteration -> System.Exception")

