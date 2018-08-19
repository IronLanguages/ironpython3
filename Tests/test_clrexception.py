# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest
from iptest import skipUnlessIronPython, is_netcoreapp, is_cli, run_test

@skipUnlessIronPython()
class ClrExceptionTest(unittest.TestCase):

    def clr_to_py_positive(self, clrExcep, pyExcep, excepMsg = None, msg = "CLR exception not mapped to specified Python exception"):
        try:
            raise clrExcep(excepMsg)
        except pyExcep:
            self.assertTrue(True)
        except:
            self.fail(msg)

    def py_to_clr_positive(self, pyExcep, clrExcep, msg = "Python exception not mapped to specified CLR exception"):
        try:
            raise pyExcep
        except clrExcep:
            self.assertTrue(True)
        except:
            self.fail(msg)

    def py_to_clr_positive_with_args(self, pyExcep, clrExcep, args, msg = "Python exception not mapped to specified CLR exception"):
        try:
            raise pyExcep(*args)
        except clrExcep:
            self.assertTrue(True)
        except Exception as e:
            print(e)
            self.fail(msg)

    def test_mappings(self):
        if is_netcoreapp:
            import clr
            clr.AddReference("Microsoft.Win32.Primitives")
            clr.AddReference("System.ComponentModel.TypeConverter")

        import System
        import System.IO
        import System.ComponentModel
        import System.Text
        import System.Collections.Generic

        # Tests for exact mappings

        # BUG 515 System.SystemException not mapped to StandardError
        self.clr_to_py_positive(System.SystemException, Exception)
        # /BUG

        self.clr_to_py_positive(System.IO.IOException, IOError, msg = "System.IO.IOException -> IOError")
        self.py_to_clr_positive(IOError, System.IO.IOException, msg = "IOError -> System.IO.IOException")

        self.clr_to_py_positive(System.MissingMemberException, AttributeError, msg ="System.MissingMemberException -> AttributeError")
        self.py_to_clr_positive(AttributeError, System.MissingMemberException, msg = "AttributeError -> System.MissingMemberException")

        self.clr_to_py_positive(System.ComponentModel.Win32Exception, WindowsError, msg ="System.ComponentModel.Win32Exception -> WindowsError")
        self.py_to_clr_positive(WindowsError, System.ComponentModel.Win32Exception, msg = "WindowsError -> System.ComponentModel.Win32Exception")

        self.clr_to_py_positive(System.IO.EndOfStreamException, EOFError, msg="System.IO.EndOfStreamException -> EOFError")
        self.py_to_clr_positive(EOFError, System.IO.EndOfStreamException, msg = "EOFError -> System.IO.EndOfStreamException")

        self.clr_to_py_positive(System.NotImplementedException, NotImplementedError, msg = "System.NotImplementedException -> NotImplementedError")
        self.py_to_clr_positive(NotImplementedError, System.NotImplementedException, msg = "NotImplementedError -> System.NotImplementedException")

        self.clr_to_py_positive(System.IndexOutOfRangeException, IndexError, msg = "System.IndexOutOfRangeException -> IndexError")
        self.py_to_clr_positive(IndexError, System.IndexOutOfRangeException, msg = "IndexError -> System.IndexOutOfRangeException")

        self.clr_to_py_positive(System.ArithmeticException, ArithmeticError, msg = "System.ArithmeticException -> ArithmeticError")
        self.py_to_clr_positive(ArithmeticError, System.ArithmeticException, msg = "ArithmeticError -> System.ArithmeticException")

        self.clr_to_py_positive(System.OverflowException, OverflowError, msg= "System.OverflowException -> OverflowError")
        self.py_to_clr_positive(OverflowError, System.OverflowException, msg = "OverflowError -> System.OverflowException")

        self.clr_to_py_positive(System.DivideByZeroException, ZeroDivisionError, msg ="System.DivideByZeroException -> ZeroDivisionError")
        self.py_to_clr_positive(ZeroDivisionError, System.DivideByZeroException, msg = "ZeroDivisionError -> System.DivideByZeroException")

        self.clr_to_py_positive(System.ArgumentException, ValueError, msg = "System.ArgumentException -> ValueError")
        self.py_to_clr_positive(ValueError, System.ArgumentException, msg = "ValueError-> System.ArgumentException")

        self.clr_to_py_positive(System.Text.DecoderFallbackException, UnicodeDecodeError)
        self.py_to_clr_positive_with_args(UnicodeDecodeError, System.Text.DecoderFallbackException, ('abc','abc',3,4,'abc'))

        self.clr_to_py_positive(System.Text.EncoderFallbackException, UnicodeEncodeError)
        self.py_to_clr_positive_with_args(UnicodeEncodeError, System.Text.EncoderFallbackException, ('abc','abc',3,4,'abc'))

        self.clr_to_py_positive(System.ComponentModel.WarningException, Warning, msg = "System.ComponentModel.WarningException -> Warning")
        self.py_to_clr_positive(Warning, System.ComponentModel.WarningException, msg = "Warning -> System.ComponentModel.WarningException")

        self.clr_to_py_positive(System.OutOfMemoryException, MemoryError, msg = "System.OutOfMemoryException -> MemoryError")
        self.py_to_clr_positive(MemoryError, System.OutOfMemoryException, msg = "MemoryError -> System.OutOfMemoryException")

        self.clr_to_py_positive(System.Collections.Generic.KeyNotFoundException, KeyError, msg = "System.Collections.Generic.KeyNotFoundException -> KeyError")
        self.py_to_clr_positive(KeyError, System.Collections.Generic.KeyNotFoundException, msg = "KeyError -> System.Collections.Generic.KeyNotFoundException")
        # Tests for inexact mappings

        # Let CLR exception class A map to Python error type B;
        #  let A' be a direct subclass of A, with *no* direct mapping to a Python error type;
        # then A' should be caught in Iron Python as an instance of B.
        # In other words, A' should be mapped using the *first* available mapping found while
        #  searching upwards in its type hierarchy.
        self.clr_to_py_positive(System.NotFiniteNumberException, ArithmeticError, msg = "System.NotFiniteNumberException -> ArithmeticError")

        # StopIteration can be caught as an Exception
        self.py_to_clr_positive(StopIteration, System.Exception, msg = "StopIteration -> System.Exception")

run_test(__name__)