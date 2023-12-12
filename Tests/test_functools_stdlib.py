# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_functools from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono

import test.test_functools

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_functools, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_functools.TestCmpToKeyC('test_bad_cmp'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_cmp_to_key'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_cmp_to_key_arguments'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_hash'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_obj_field'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_sort_int'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_sort_int_str'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestLRUC('test_lru_cache_threaded'), # unittest.expectedFailure(
            test.test_functools.TestLRUC('test_pickle'), # pickle.PicklingError: Can't pickle
            test.test_functools.TestLRUPy('test_lru_cache_threaded'), # AttributeError: 'module' object has no attribute 'getswitchinterval'
            test.test_functools.TestLRUPy('test_pickle'), # pickle.PicklingError: Can't pickle
            test.test_functools.TestPartialC('test_copy'), # AssertionError: (['asdf'],) is not (['asdf'],)
            test.test_functools.TestPartialC('test_keystr_replaces_value'), # AssertionError: 'astr' not found in '<CPartialSubclass object at 0x00000000000000A8>'
            test.test_functools.TestPartialC('test_nested_optimization'), # AssertionError: Tuples differ: (<partial object at 0x0000000000000066>, (), {'bar': True}, {}) != (<function signature at 0x0000000000000051>, ('asdf',), {'bar': True}, {})
            test.test_functools.TestPartialC('test_setstate'), # AssertionError: Tuples differ: (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {'attr': []}) != (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {})
            test.test_functools.TestPartialC('test_setstate_subclasses'), # AssertionError: <class 'test.test_functools.MyDict'> is not <class 'dict'>
            test.test_functools.TestPartialCSubclass('test_attributes_unwritable'), # AssertionError: AttributeError not raised by setattr
            test.test_functools.TestPartialCSubclass('test_copy'), # AttributeError: 'partial' object has no attribute 'attr'
            test.test_functools.TestPartialCSubclass('test_deepcopy'), # AttributeError: 'partial' object has no attribute 'attr'
            test.test_functools.TestPartialCSubclass('test_keystr_replaces_value'), # AssertionError: 'astr' not found in '<CPartialSubclass object at 0x00000000000000A8>'
            test.test_functools.TestPartialCSubclass('test_recursive_repr'), # AssertionError: 'functools.partial(...)' != 'CPartialSubclass(...)'
            test.test_functools.TestPartialCSubclass('test_repr'), # AssertionError
            test.test_functools.TestPartialCSubclass('test_setstate'), # AssertionError: Tuples differ: (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {'attr': []}) != (<function capture at 0x000000000000008D>, (1,), {'a': 10}, {})
            test.test_functools.TestPartialCSubclass('test_setstate_subclasses'), # AssertionError: <class 'test.test_functools.MyDict'> is not <class 'dict'>
            test.test_functools.TestTotalOrdering('test_pickle'), # pickle.PicklingError: Can't pickle
        ]

        skip_tests = [
            test.test_functools.TestLRUC('test_lru_cache_threaded2'), # intermittent failures
            test.test_functools.TestLRUC('test_lru_cache_threaded3'), # intermittent failures
            test.test_functools.TestLRUPy('test_lru_cache_threaded2'), # intermittent failures
            test.test_functools.TestLRUPy('test_lru_cache_threaded3'), # intermittent failures
            test.test_functools.TestPartialC('test_recursive_pickle'), # StackOverflowException
            test.test_functools.TestPartialCSubclass('test_recursive_pickle'), # StackOverflowException
            test.test_functools.TestPartialPy('test_recursive_pickle'), #  # StackOverflowException
            test.test_functools.TestPartialPySubclass('test_recursive_pickle'), # StackOverflowException
        ]
        if is_mono:
            skip_tests += [
                test.test_functools.TestPartialCSubclass('test_weakref'),
                test.test_functools.TestPartialPy('test_weakref'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
