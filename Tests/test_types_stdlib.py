# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_types from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_types

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_types.ClassCreationTests('test_metaclass_derivation'))
        suite.addTest(test.test_types.ClassCreationTests('test_metaclass_override_callable'))
        suite.addTest(test.test_types.ClassCreationTests('test_metaclass_override_function'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_basics'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_defaults'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_exec_body'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_meta'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_meta_with_base'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_metaclass_keywords'))
        suite.addTest(test.test_types.ClassCreationTests('test_new_class_subclass'))
        suite.addTest(test.test_types.ClassCreationTests('test_prepare_class'))
        suite.addTest(unittest.expectedFailure(test.test_types.MappingProxyTests('test_chainmap'))) # TypeError: expected dict, got Object_1$1
        suite.addTest(unittest.expectedFailure(test.test_types.MappingProxyTests('test_constructor'))) # TypeError: expected dict, got Object_1$1
        suite.addTest(test.test_types.MappingProxyTests('test_contains'))
        suite.addTest(test.test_types.MappingProxyTests('test_copy'))
        suite.addTest(unittest.expectedFailure(test.test_types.MappingProxyTests('test_customdict'))) # AssertionError: False is not true
        suite.addTest(test.test_types.MappingProxyTests('test_get'))
        suite.addTest(test.test_types.MappingProxyTests('test_iterators'))
        suite.addTest(test.test_types.MappingProxyTests('test_len'))
        suite.addTest(test.test_types.MappingProxyTests('test_methods'))
        suite.addTest(unittest.expectedFailure(test.test_types.MappingProxyTests('test_missing'))) # AssertionError: 'missing=y' != None
        suite.addTest(test.test_types.MappingProxyTests('test_views'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_as_dict'))
        suite.addTest(unittest.expectedFailure(test.test_types.SimpleNamespaceTests('test_attrdel'))) # KeyError: spam
        suite.addTest(test.test_types.SimpleNamespaceTests('test_attrget'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_attrset'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_constructor'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_equal'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_fake_namespace_compare'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_nested'))
        suite.addTest(unittest.expectedFailure(test.test_types.SimpleNamespaceTests('test_pickle'))) # TypeError: protocol 0
        suite.addTest(test.test_types.SimpleNamespaceTests('test_recursive'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_recursive_repr'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_repr'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_subclass'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_unbound'))
        suite.addTest(test.test_types.SimpleNamespaceTests('test_underlying_dict'))
        suite.addTest(test.test_types.TypesTests('test_boolean_ops'))
        suite.addTest(test.test_types.TypesTests('test_comparisons'))
        suite.addTest(unittest.expectedFailure(test.test_types.TypesTests('test_float__format__'))) # AssertionError: '1.12339e+200' != '1.1234e+200'
        suite.addTest(test.test_types.TypesTests('test_float__format__locale'))
        suite.addTest(test.test_types.TypesTests('test_float_constructor'))
        suite.addTest(test.test_types.TypesTests('test_float_to_string'))
        suite.addTest(test.test_types.TypesTests('test_floats'))
        suite.addTest(test.test_types.TypesTests('test_format_spec_errors'))
        suite.addTest(test.test_types.TypesTests('test_int__format__'))
        suite.addTest(test.test_types.TypesTests('test_int__format__locale'))
        suite.addTest(unittest.expectedFailure(test.test_types.TypesTests('test_internal_sizes'))) # AttributeError: 'type' object has no attribute '__basicsize__'
        suite.addTest(test.test_types.TypesTests('test_normal_integers'))
        suite.addTest(test.test_types.TypesTests('test_numeric_types'))
        suite.addTest(test.test_types.TypesTests('test_strings'))
        suite.addTest(test.test_types.TypesTests('test_truth_values'))
        suite.addTest(test.test_types.TypesTests('test_type_function'))
        suite.addTest(test.test_types.TypesTests('test_zero_division'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_types, pattern)

run_test(__name__)
