# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from ctypes.test.test_frombuffer from StdLib
##

import unittest
import sys

from iptest import run_test

import ctypes.test.test_frombuffer

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(ctypes.test.test_frombuffer.Test('test_fortran_contiguous'))
        suite.addTest(unittest.expectedFailure(ctypes.test.test_frombuffer.Test('test_from_buffer')))
        suite.addTest(ctypes.test.test_frombuffer.Test('test_from_buffer_copy'))
        suite.addTest(ctypes.test.test_frombuffer.Test('test_from_buffer_copy_with_offset'))
        suite.addTest(unittest.expectedFailure(ctypes.test.test_frombuffer.Test('test_from_buffer_memoryview')))
        suite.addTest(ctypes.test.test_frombuffer.Test('test_from_buffer_with_offset'))
        return suite

    else:
        return loader.loadTestsFromModule(ctypes.test.test_frombuffer, pattern)

run_test(__name__)
