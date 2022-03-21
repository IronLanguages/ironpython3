# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_slice from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_slice

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_slice.SliceTest('test_cmp'))
        suite.addTest(test.test_slice.SliceTest('test_constructor'))
        suite.addTest(test.test_slice.SliceTest('test_hash'))
        suite.addTest(unittest.expectedFailure(test.test_slice.SliceTest('test_indices'))) # TODO
        suite.addTest(test.test_slice.SliceTest('test_members'))
        suite.addTest(test.test_slice.SliceTest('test_pickle'))
        suite.addTest(test.test_slice.SliceTest('test_repr'))
        suite.addTest(test.test_slice.SliceTest('test_setslice_without_getslice'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_slice, pattern)

run_test(__name__)
