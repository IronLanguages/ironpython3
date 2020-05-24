# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_weakset from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_weakset

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_weakset.TestWeakSet('test_add'))
        suite.addTest(test.test_weakset.TestWeakSet('test_and'))
        suite.addTest(test.test_weakset.TestWeakSet('test_clear'))
        suite.addTest(test.test_weakset.TestWeakSet('test_constructor_identity'))
        suite.addTest(test.test_weakset.TestWeakSet('test_contains'))
        suite.addTest(test.test_weakset.TestWeakSet('test_copy'))
        suite.addTest(test.test_weakset.TestWeakSet('test_difference'))
        suite.addTest(test.test_weakset.TestWeakSet('test_difference_update'))
        suite.addTest(test.test_weakset.TestWeakSet('test_discard'))
        suite.addTest(test.test_weakset.TestWeakSet('test_eq'))
        suite.addTest(test.test_weakset.TestWeakSet('test_gc'))
        suite.addTest(test.test_weakset.TestWeakSet('test_gt'))
        suite.addTest(test.test_weakset.TestWeakSet('test_hash'))
        suite.addTest(test.test_weakset.TestWeakSet('test_iand'))
        suite.addTest(test.test_weakset.TestWeakSet('test_init'))
        suite.addTest(test.test_weakset.TestWeakSet('test_inplace_on_self'))
        suite.addTest(test.test_weakset.TestWeakSet('test_intersection'))
        suite.addTest(test.test_weakset.TestWeakSet('test_intersection_update'))
        suite.addTest(test.test_weakset.TestWeakSet('test_ior'))
        suite.addTest(test.test_weakset.TestWeakSet('test_isdisjoint'))
        suite.addTest(test.test_weakset.TestWeakSet('test_isub'))
        suite.addTest(test.test_weakset.TestWeakSet('test_ixor'))
        suite.addTest(test.test_weakset.TestWeakSet('test_len'))
        #suite.addTest(test.test_weakset.TestWeakSet('test_len_cycles'))
        suite.addTest(test.test_weakset.TestWeakSet('test_len_race'))
        suite.addTest(test.test_weakset.TestWeakSet('test_lt'))
        suite.addTest(test.test_weakset.TestWeakSet('test_methods'))
        suite.addTest(test.test_weakset.TestWeakSet('test_ne'))
        suite.addTest(test.test_weakset.TestWeakSet('test_new_or_init'))
        suite.addTest(test.test_weakset.TestWeakSet('test_or'))
        suite.addTest(test.test_weakset.TestWeakSet('test_pop'))
        suite.addTest(test.test_weakset.TestWeakSet('test_remove'))
        suite.addTest(test.test_weakset.TestWeakSet('test_sub'))
        suite.addTest(test.test_weakset.TestWeakSet('test_sub_and_super'))
        suite.addTest(test.test_weakset.TestWeakSet('test_subclass_with_custom_hash'))
        suite.addTest(test.test_weakset.TestWeakSet('test_symmetric_difference'))
        suite.addTest(test.test_weakset.TestWeakSet('test_symmetric_difference_update'))
        suite.addTest(test.test_weakset.TestWeakSet('test_union'))
        suite.addTest(test.test_weakset.TestWeakSet('test_update'))
        suite.addTest(test.test_weakset.TestWeakSet('test_update_set'))
        #suite.addTest(test.test_weakset.TestWeakSet('test_weak_destroy_and_mutate_while_iterating'))
        suite.addTest(test.test_weakset.TestWeakSet('test_weak_destroy_while_iterating'))
        suite.addTest(test.test_weakset.TestWeakSet('test_xor'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_weakset, pattern)

run_test(__name__)
