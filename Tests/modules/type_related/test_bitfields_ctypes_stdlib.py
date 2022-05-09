# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_bitfields from StdLib
##

import unittest
import sys

from iptest import run_test

import ctypes.test.test_bitfields

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_anon_bitfields'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_c_wchar'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_longlong'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_mixed_2'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_mixed_3'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_multi_bitfields_size'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_nonint_types'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_signed'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_single_bitfield_size'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_uint32'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_uint32_swap_big_endian'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_uint32_swap_little_endian'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_uint64'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_ulonglong'))
        suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_unsigned'))
        suite.addTest(ctypes.test.test_bitfields.C_Test('test_ints'))
        if sys.platform.startswith('win'):
            suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_mixed_1'))
            suite.addTest(ctypes.test.test_bitfields.BitFieldTest('test_mixed_4'))
            suite.addTest(ctypes.test.test_bitfields.C_Test('test_shorts'))
        else: # https://github.com/IronLanguages/ironpython3/issues/1442
            suite.addTest(unittest.expectedFailure(ctypes.test.test_bitfields.BitFieldTest('test_mixed_1')))
            suite.addTest(unittest.expectedFailure(ctypes.test.test_bitfields.BitFieldTest('test_mixed_4')))
            suite.addTest(unittest.expectedFailure(ctypes.test.test_bitfields.C_Test('test_shorts')))
        return suite

    else:
        return loader.loadTestsFromModule(ctypes.test.test_bitfields, pattern)

run_test(__name__)
