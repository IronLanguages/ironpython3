# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_abc from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_abc

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_abc.TestABC('test_ABC_helper'))
        suite.addTest(test.test_abc.TestABC('test_abstractclassmethod_basics'))
        suite.addTest(test.test_abc.TestABC('test_abstractmethod_basics'))
        suite.addTest(test.test_abc.TestABC('test_abstractmethod_integration'))
        suite.addTest(test.test_abc.TestABC('test_abstractproperty_basics'))
        suite.addTest(test.test_abc.TestABC('test_abstractstaticmethod_basics'))
        suite.addTest(test.test_abc.TestABC('test_all_new_methods_are_called'))
        suite.addTest(test.test_abc.TestABC('test_customdescriptors_with_abstractmethod'))
        suite.addTest(test.test_abc.TestABC('test_descriptors_with_abstractmethod'))
        suite.addTest(test.test_abc.TestABC('test_isinstance_invalidation'))
        suite.addTest(test.test_abc.TestABC('test_metaclass_abc'))
        suite.addTest(test.test_abc.TestABC('test_register_as_class_deco'))
        suite.addTest(test.test_abc.TestABC('test_register_non_class'))
        suite.addTest(test.test_abc.TestABC('test_registration_basics'))
        suite.addTest(test.test_abc.TestABC('test_registration_builtins'))
        suite.addTest(test.test_abc.TestABC('test_registration_edge_cases'))
        suite.addTest(test.test_abc.TestABC('test_registration_transitiveness'))
        suite.addTest(unittest.expectedFailure(test.test_abc.TestABCWithInitSubclass('test_works_with_init_subclass'))) # https://github.com/IronLanguages/ironpython3/issues/1448
        suite.addTest(test.test_abc.TestLegacyAPI('test_abstractclassmethod_basics'))
        suite.addTest(test.test_abc.TestLegacyAPI('test_abstractproperty_basics'))
        suite.addTest(test.test_abc.TestLegacyAPI('test_abstractstaticmethod_basics'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_abc, pattern)

run_test(__name__)
