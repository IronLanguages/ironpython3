# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_struct from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_struct

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_struct.StructTest('test_1530559'))
        suite.addTest(unittest.expectedFailure(test.test_struct.StructTest('test_705836'))) # TODO: figure out
        suite.addTest(test.test_struct.StructTest('test_Struct_reinitialization'))
        suite.addTest(test.test_struct.StructTest('test__sizeof__'))
        suite.addTest(unittest.expectedFailure(test.test_struct.StructTest('test_bool'))) # TODO: figure out
        suite.addTest(unittest.expectedFailure(test.test_struct.StructTest('test_calcsize'))) # TODO: figure out
        suite.addTest(test.test_struct.StructTest('test_consistence'))
        suite.addTest(unittest.expectedFailure(test.test_struct.StructTest('test_count_overflow'))) # TODO: figure out
        suite.addTest(test.test_struct.StructTest('test_integers'))
        suite.addTest(test.test_struct.StructTest('test_isbigendian'))
        suite.addTest(test.test_struct.StructTest('test_nN_code'))
        suite.addTest(test.test_struct.StructTest('test_new_features'))
        suite.addTest(test.test_struct.StructTest('test_p_code'))
        suite.addTest(test.test_struct.StructTest('test_pack_into'))
        suite.addTest(test.test_struct.StructTest('test_pack_into_fn'))
        suite.addTest(unittest.expectedFailure(test.test_struct.StructTest('test_trailing_counter'))) # TODO: figure out
        suite.addTest(test.test_struct.StructTest('test_transitiveness'))
        suite.addTest(test.test_struct.StructTest('test_unpack_from'))
        suite.addTest(test.test_struct.StructTest('test_unpack_with_buffer'))
        suite.addTest(test.test_struct.UnpackIteratorTest('test_arbitrary_buffer'))
        suite.addTest(unittest.expectedFailure(test.test_struct.UnpackIteratorTest('test_construct'))) # TODO: figure out
        suite.addTest(test.test_struct.UnpackIteratorTest('test_half_float'))
        suite.addTest(test.test_struct.UnpackIteratorTest('test_iterate'))
        suite.addTest(test.test_struct.UnpackIteratorTest('test_length_hint'))
        suite.addTest(test.test_struct.UnpackIteratorTest('test_module_func'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_struct, pattern)

run_test(__name__)
