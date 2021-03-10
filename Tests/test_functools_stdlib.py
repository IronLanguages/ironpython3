# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_functools from StdLib
##

import unittest
import sys

from iptest import run_test, is_mono

import test.test_functools

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_bad_cmp'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_cmp_to_key'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_cmp_to_key_arguments'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_hash'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_obj_field'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_sort_int'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(unittest.expectedFailure(test.test_functools.TestCmpToKeyC('test_sort_int_str'))) # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_bad_cmp'))
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_cmp_to_key'))
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_cmp_to_key_arguments'))
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_hash'))
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_obj_field'))
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_sort_int'))
        suite.addTest(test.test_functools.TestCmpToKeyPy('test_sort_int_str'))
        suite.addTest(test.test_functools.TestLRU('test_early_detection_of_bad_call'))
        suite.addTest(test.test_functools.TestLRU('test_lru'))
        suite.addTest(test.test_functools.TestLRU('test_lru_with_exceptions'))
        suite.addTest(test.test_functools.TestLRU('test_lru_with_keyword_args'))
        suite.addTest(test.test_functools.TestLRU('test_lru_with_keyword_args_maxsize_none'))
        suite.addTest(test.test_functools.TestLRU('test_lru_with_maxsize_none'))
        suite.addTest(test.test_functools.TestLRU('test_lru_with_types'))
        suite.addTest(test.test_functools.TestLRU('test_need_for_rlock'))
        suite.addTest(test.test_functools.TestPartialC('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialC('test_argument_checking'))
        suite.addTest(test.test_functools.TestPartialC('test_attributes'))
        suite.addTest(test.test_functools.TestPartialC('test_attributes_unwritable'))
        suite.addTest(test.test_functools.TestPartialC('test_basic_examples'))
        suite.addTest(test.test_functools.TestPartialC('test_error_propagation'))
        suite.addTest(test.test_functools.TestPartialC('test_keyword'))
        suite.addTest(test.test_functools.TestPartialC('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialC('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialC('test_pickle'))
        suite.addTest(test.test_functools.TestPartialC('test_positional'))
        suite.addTest(test.test_functools.TestPartialC('test_protection_of_callers_dict_argument'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_repr'))) # AssertionError
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_setstate_refcount'))) # AttributeError: 'partial' object has no attribute '__setstate__'
        suite.addTest(test.test_functools.TestPartialC('test_weakref'))
        suite.addTest(test.test_functools.TestPartialC('test_with_bound_and_unbound_methods'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_argument_checking'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_attributes'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_attributes_unwritable'))) # AssertionError: AttributeError not raised by setattr
        suite.addTest(test.test_functools.TestPartialCSubclass('test_basic_examples'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_error_propagation'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_keyword'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_pickle'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_positional'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_protection_of_callers_dict_argument'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_repr'))) # AssertionError
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_setstate_refcount'))) # AttributeError: 'PartialSubclass' object has no attribute '__setstate__'
        suite.addTest(test.test_functools.TestPartialCSubclass('test_weakref'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_with_bound_and_unbound_methods'))
        suite.addTest(test.test_functools.TestPartialMethod('test_abstract'))
        suite.addTest(test.test_functools.TestPartialMethod('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialMethod('test_bound_method_introspection'))
        suite.addTest(test.test_functools.TestPartialMethod('test_descriptors'))
        suite.addTest(test.test_functools.TestPartialMethod('test_invalid_args'))
        suite.addTest(test.test_functools.TestPartialMethod('test_nested'))
        suite.addTest(test.test_functools.TestPartialMethod('test_over_partial'))
        suite.addTest(test.test_functools.TestPartialMethod('test_overriding_keywords'))
        suite.addTest(test.test_functools.TestPartialMethod('test_repr'))
        suite.addTest(test.test_functools.TestPartialMethod('test_unbound_method_retrieval'))
        suite.addTest(test.test_functools.TestPartialPy('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialPy('test_argument_checking'))
        suite.addTest(test.test_functools.TestPartialPy('test_attributes'))
        suite.addTest(test.test_functools.TestPartialPy('test_basic_examples'))
        suite.addTest(test.test_functools.TestPartialPy('test_error_propagation'))
        suite.addTest(test.test_functools.TestPartialPy('test_keyword'))
        suite.addTest(test.test_functools.TestPartialPy('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialPy('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialPy('test_positional'))
        suite.addTest(test.test_functools.TestPartialPy('test_protection_of_callers_dict_argument'))
        if not is_mono:
            suite.addTest(test.test_functools.TestPartialPy('test_weakref'))
        suite.addTest(test.test_functools.TestPartialPy('test_with_bound_and_unbound_methods'))
        suite.addTest(test.test_functools.TestReduce('test_iterator_usage'))
        suite.addTest(test.test_functools.TestReduce('test_reduce'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_c3_abc'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_c_classes'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_cache_invalidation'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_compose_mro'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_false_meta'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_mro'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_mro_conflicts'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_register_abc'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_register_decorator'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_simple_overloads'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_wrapping_attributes'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_no_operations_defined'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_ge'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_gt'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_le'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_lt'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_no_overwrite'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_type_error_when_not_implemented'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_builtin_update'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestUpdateWrapper('test_default_update'))) # AttributeError: 'function' object has no attribute '__qualname__'
        suite.addTest(test.test_functools.TestUpdateWrapper('test_default_update_doc'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_missing_attributes'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestUpdateWrapper('test_no_update'))) # AttributeError: 'function' object has no attribute '__qualname__'
        suite.addTest(unittest.expectedFailure(test.test_functools.TestUpdateWrapper('test_selective_update'))) # AttributeError: 'function' object has no attribute '__qualname__'
        suite.addTest(test.test_functools.TestWraps('test_builtin_update'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestWraps('test_default_update'))) # AttributeError: 'function' object has no attribute '__qualname__'
        suite.addTest(test.test_functools.TestWraps('test_default_update_doc'))
        suite.addTest(test.test_functools.TestWraps('test_missing_attributes'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestWraps('test_no_update'))) # AttributeError: 'function' object has no attribute '__qualname__'
        suite.addTest(unittest.expectedFailure(test.test_functools.TestWraps('test_selective_update'))) # AttributeError: 'function' object has no attribute '__qualname__'
        return suite

    else:
        return loader.loadTestsFromModule(test.test_functools, pattern)

run_test(__name__)
