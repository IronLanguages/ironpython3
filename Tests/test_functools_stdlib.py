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
        suite.addTest(test.test_functools.TestLRUC('test_copy'))
        suite.addTest(test.test_functools.TestLRUC('test_deepcopy'))
        suite.addTest(test.test_functools.TestLRUC('test_early_detection_of_bad_call'))
        #suite.addTest(test.test_functools.TestLRUC('test_kwargs_order')) # intermittent failures - https://github.com/IronLanguages/ironpython3/issues/1460
        suite.addTest(test.test_functools.TestLRUC('test_lru'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_cache_decoration'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestLRUC('test_lru_cache_threaded'))) # unittest.expectedFailure(
        #suite.addTest(test.test_functools.TestLRUC('test_lru_cache_threaded2')) # intermittent failures
        #suite.addTest(test.test_functools.TestLRUC('test_lru_cache_threaded3')) # intermittent failures
        suite.addTest(test.test_functools.TestLRUC('test_lru_method'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_reentrancy_with_len'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_type_error'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_with_exceptions'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_with_keyword_args'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_with_keyword_args_maxsize_none'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_with_maxsize_negative'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_with_maxsize_none'))
        suite.addTest(test.test_functools.TestLRUC('test_lru_with_types'))
        suite.addTest(test.test_functools.TestLRUC('test_need_for_rlock'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestLRUC('test_pickle'))) # pickle.PicklingError: Can't pickle
        suite.addTest(test.test_functools.TestLRUPy('test_copy'))
        suite.addTest(test.test_functools.TestLRUPy('test_deepcopy'))
        suite.addTest(test.test_functools.TestLRUPy('test_early_detection_of_bad_call'))
        #suite.addTest(test.test_functools.TestLRUPy('test_kwargs_order')) # intermittent failures - https://github.com/IronLanguages/ironpython3/issues/1460
        suite.addTest(test.test_functools.TestLRUPy('test_lru'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_cache_decoration'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestLRUPy('test_lru_cache_threaded'))) # AttributeError: 'module' object has no attribute 'getswitchinterval'
        #suite.addTest(test.test_functools.TestLRUPy('test_lru_cache_threaded2')) # intermittent failures
        #suite.addTest(test.test_functools.TestLRUPy('test_lru_cache_threaded3')) # intermittent failures
        suite.addTest(test.test_functools.TestLRUPy('test_lru_method'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_reentrancy_with_len'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_type_error'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_with_exceptions'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_with_keyword_args'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_with_keyword_args_maxsize_none'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_with_maxsize_negative'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_with_maxsize_none'))
        suite.addTest(test.test_functools.TestLRUPy('test_lru_with_types'))
        suite.addTest(test.test_functools.TestLRUPy('test_need_for_rlock'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestLRUPy('test_pickle'))) # pickle.PicklingError: Can't pickle
        suite.addTest(test.test_functools.TestPartialC('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialC('test_argument_checking'))
        suite.addTest(test.test_functools.TestPartialC('test_attributes'))
        suite.addTest(test.test_functools.TestPartialC('test_attributes_unwritable'))
        suite.addTest(test.test_functools.TestPartialC('test_basic_examples'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_copy'))) # AssertionError: (['asdf'],) is not (['asdf'],)
        suite.addTest(test.test_functools.TestPartialC('test_deepcopy'))
        suite.addTest(test.test_functools.TestPartialC('test_error_propagation'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_keystr_replaces_value'))) # AssertionError: 'astr' not found in '<CPartialSubclass object at 0x00000000000000A8>'
        suite.addTest(test.test_functools.TestPartialC('test_keyword'))
        suite.addTest(test.test_functools.TestPartialC('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialC('test_kwargs_copy'))
        suite.addTest(test.test_functools.TestPartialC('test_manually_adding_non_string_keyword'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_nested_optimization'))) # AssertionError: Tuples differ: (<partial object at 0x0000000000000066>, (), {'bar': True}, {}) != (<function signature at 0x0000000000000051>, ('asdf',), {'bar': True}, {})
        suite.addTest(test.test_functools.TestPartialC('test_nested_partial_with_attribute'))
        suite.addTest(test.test_functools.TestPartialC('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialC('test_pickle'))
        suite.addTest(test.test_functools.TestPartialC('test_positional'))
        suite.addTest(test.test_functools.TestPartialC('test_protection_of_callers_dict_argument'))
        #suite.addTest(test.test_functools.TestPartialC('test_recursive_pickle')) # StackOverflowException
        suite.addTest(test.test_functools.TestPartialC('test_recursive_repr'))
        suite.addTest(test.test_functools.TestPartialC('test_repr'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_setstate'))) # AssertionError: Tuples differ: (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {'attr': []}) != (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {})
        suite.addTest(test.test_functools.TestPartialC('test_setstate_errors'))
        suite.addTest(test.test_functools.TestPartialC('test_setstate_refcount'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialC('test_setstate_subclasses'))) # AssertionError: <class 'test.test_functools.MyDict'> is not <class 'dict'>
        suite.addTest(test.test_functools.TestPartialC('test_weakref'))
        suite.addTest(test.test_functools.TestPartialC('test_with_bound_and_unbound_methods'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_argument_checking'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_attributes'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_attributes_unwritable'))) # AssertionError: AttributeError not raised by setattr
        suite.addTest(test.test_functools.TestPartialCSubclass('test_basic_examples'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_copy'))) # AttributeError: 'partial' object has no attribute 'attr'
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_deepcopy'))) # AttributeError: 'partial' object has no attribute 'attr'
        suite.addTest(test.test_functools.TestPartialCSubclass('test_error_propagation'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_keystr_replaces_value'))) # AssertionError: 'astr' not found in '<CPartialSubclass object at 0x00000000000000A8>'
        suite.addTest(test.test_functools.TestPartialCSubclass('test_keyword'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_kwargs_copy'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_manually_adding_non_string_keyword'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_nested_partial_with_attribute'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_pickle'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_positional'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_protection_of_callers_dict_argument'))
        #suite.addTest(test.test_functools.TestPartialCSubclass('test_recursive_pickle')) # StackOverflowException
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_recursive_repr'))) # AssertionError: 'functools.partial(...)' != 'CPartialSubclass(...)'
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_repr'))) # AssertionError
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_setstate'))) # AssertionError: Tuples differ: (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {'attr': []}) != (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {})
        suite.addTest(test.test_functools.TestPartialCSubclass('test_setstate_errors'))
        suite.addTest(test.test_functools.TestPartialCSubclass('test_setstate_refcount'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestPartialCSubclass('test_setstate_subclasses'))) # AssertionError: <class 'test.test_functools.MyDict'> is not <class 'dict'>
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
        suite.addTest(test.test_functools.TestPartialPy('test_copy'))
        suite.addTest(test.test_functools.TestPartialPy('test_deepcopy'))
        suite.addTest(test.test_functools.TestPartialPy('test_error_propagation'))
        suite.addTest(test.test_functools.TestPartialPy('test_keyword'))
        suite.addTest(test.test_functools.TestPartialPy('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialPy('test_kwargs_copy'))
        suite.addTest(test.test_functools.TestPartialPy('test_nested_optimization'))
        suite.addTest(test.test_functools.TestPartialPy('test_nested_partial_with_attribute'))
        suite.addTest(test.test_functools.TestPartialPy('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialPy('test_pickle'))
        suite.addTest(test.test_functools.TestPartialPy('test_positional'))
        suite.addTest(test.test_functools.TestPartialPy('test_protection_of_callers_dict_argument'))
        #suite.addTest(test.test_functools.TestPartialPy('test_recursive_pickle')) #  # StackOverflowException
        suite.addTest(test.test_functools.TestPartialPy('test_recursive_repr'))
        suite.addTest(test.test_functools.TestPartialPy('test_repr'))
        suite.addTest(test.test_functools.TestPartialPy('test_setstate'))
        suite.addTest(test.test_functools.TestPartialPy('test_setstate_errors'))
        suite.addTest(test.test_functools.TestPartialPy('test_setstate_refcount'))
        suite.addTest(test.test_functools.TestPartialPy('test_setstate_subclasses'))
        if not is_mono:
            suite.addTest(test.test_functools.TestPartialPy('test_weakref'))
        suite.addTest(test.test_functools.TestPartialPy('test_with_bound_and_unbound_methods'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_arg_combinations'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_argument_checking'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_attributes'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_basic_examples'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_copy'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_deepcopy'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_error_propagation'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_keyword'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_kw_combinations'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_kwargs_copy'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_nested_optimization'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_nested_partial_with_attribute'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_no_side_effects'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_pickle'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_positional'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_protection_of_callers_dict_argument'))
        #suite.addTest(test.test_functools.TestPartialPySubclass('test_recursive_pickle')) # StackOverflowException
        suite.addTest(test.test_functools.TestPartialPySubclass('test_recursive_repr'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_repr'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_setstate'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_setstate_errors'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_setstate_refcount'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_setstate_subclasses'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_weakref'))
        suite.addTest(test.test_functools.TestPartialPySubclass('test_with_bound_and_unbound_methods'))
        suite.addTest(test.test_functools.TestReduce('test_iterator_usage'))
        suite.addTest(test.test_functools.TestReduce('test_reduce'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_c3_abc'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_c_classes'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_cache_invalidation'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_compose_mro'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_false_meta'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_invalid_positional_argument'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_mro'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_mro_conflicts'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_register_abc'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_register_decorator'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_simple_overloads'))
        suite.addTest(test.test_functools.TestSingleDispatch('test_wrapping_attributes'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_no_operations_defined'))
        suite.addTest(unittest.expectedFailure(test.test_functools.TestTotalOrdering('test_pickle'))) # pickle.PicklingError: Can't pickle
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_ge'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_gt'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_le'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_lt'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_total_ordering_no_overwrite'))
        suite.addTest(test.test_functools.TestTotalOrdering('test_type_error_when_not_implemented'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_builtin_update'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_default_update'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_default_update_doc'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_missing_attributes'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_no_update'))
        suite.addTest(test.test_functools.TestUpdateWrapper('test_selective_update'))
        suite.addTest(test.test_functools.TestWraps('test_builtin_update'))
        suite.addTest(test.test_functools.TestWraps('test_default_update'))
        suite.addTest(test.test_functools.TestWraps('test_default_update_doc'))
        suite.addTest(test.test_functools.TestWraps('test_missing_attributes'))
        suite.addTest(test.test_functools.TestWraps('test_no_update'))
        suite.addTest(test.test_functools.TestWraps('test_selective_update'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_functools, pattern)

run_test(__name__)
