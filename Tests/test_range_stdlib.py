# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_range from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_range

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_range.RangeTest('test_attributes'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_comparison'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_contains'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_count'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_empty'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_exhausted_iterator_pickling'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_index'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_invalid_invocation'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_issue11845'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_iterator_pickling'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_large_exhausted_iterator_pickling'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_large_operands'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_large_range'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_odd_bug'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_pickling'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_range'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_range_iterators'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_repr'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_reverse_iteration'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_slice'))) # https://github.com/IronLanguages/ironpython3/issues/472
        suite.addTest(test.test_range.RangeTest('test_strided_limits'))
        suite.addTest(test.test_range.RangeTest('test_types'))
        suite.addTest(unittest.expectedFailure(test.test_range.RangeTest('test_user_index_method'))) # https://github.com/IronLanguages/ironpython3/issues/472
        return suite

    else:
        return loader.loadTestsFromModule(test.test_range, pattern)

run_test(__name__)
