# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_super from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_super

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_super.TestSuper('test___class___classmethod'))
        suite.addTest(test.test_super.TestSuper('test___class___delayed'))
        suite.addTest(test.test_super.TestSuper('test___class___instancemethod'))
        suite.addTest(unittest.expectedFailure(test.test_super.TestSuper('test___class___mro'))) # NotImplementedError: Overriding type.mro is not implemented
        suite.addTest(test.test_super.TestSuper('test___class___new'))
        suite.addTest(test.test_super.TestSuper('test___class___staticmethod'))
        suite.addTest(test.test_super.TestSuper('test___classcell___expected_behaviour'))
        suite.addTest(test.test_super.TestSuper('test___classcell___missing'))
        suite.addTest(test.test_super.TestSuper('test___classcell___overwrite'))
        suite.addTest(test.test_super.TestSuper('test___classcell___wrong_cell'))
        suite.addTest(test.test_super.TestSuper('test_basics_working'))
        suite.addTest(test.test_super.TestSuper('test_cell_as_self'))
        suite.addTest(test.test_super.TestSuper('test_class_getattr_working'))
        suite.addTest(test.test_super.TestSuper('test_class_methods_still_working'))
        suite.addTest(test.test_super.TestSuper('test_obscure_super_errors'))
        suite.addTest(test.test_super.TestSuper('test_subclass_no_override_working'))
        suite.addTest(test.test_super.TestSuper('test_super_in_class_methods_working'))
        suite.addTest(test.test_super.TestSuper('test_super_init_leaks'))
        suite.addTest(test.test_super.TestSuper('test_super_with_closure'))
        suite.addTest(test.test_super.TestSuper('test_unbound_method_transfer_working'))
        suite.addTest(test.test_super.TestSuper('test_various___class___pathologies'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_super, pattern)

run_test(__name__)
