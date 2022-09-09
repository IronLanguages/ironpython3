# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_class from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_class

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_class.ClassTests('testBadTypeReturned'))
        suite.addTest(test.test_class.ClassTests('testBinaryOps'))
        suite.addTest(test.test_class.ClassTests('testDel'))
        suite.addTest(unittest.expectedFailure(test.test_class.ClassTests('testForExceptionsRaisedInInstanceGetattr2'))) # https://github.com/IronLanguages/ironpython3/issues/1530
        suite.addTest(test.test_class.ClassTests('testGetSetAndDel'))
        suite.addTest(test.test_class.ClassTests('testHashComparisonOfMethods'))
        suite.addTest(test.test_class.ClassTests('testHashStuff'))
        suite.addTest(test.test_class.ClassTests('testInit'))
        suite.addTest(test.test_class.ClassTests('testListAndDictOps'))
        suite.addTest(test.test_class.ClassTests('testMisc'))
        suite.addTest(test.test_class.ClassTests('testSFBug532646')) # requires MaxRecursion set
        suite.addTest(test.test_class.ClassTests('testSetattrNonStringName'))
        suite.addTest(test.test_class.ClassTests('testSetattrWrapperNameIntern'))
        suite.addTest(test.test_class.ClassTests('testUnaryOps'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_class, pattern)

run_test(__name__)
