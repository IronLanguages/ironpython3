# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ordered_dict from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_ordered_dict

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_ordered_dict, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_468'), # https://github.com/IronLanguages/ironpython3/issues/1460
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_copying'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_free_after_iterating'), # AssertionError
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_iterators_pickling'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_key_change_during_iteration'), # AssertionError: RuntimeError not raised
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_pickle_recursive'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictTests('test_468'), # https://github.com/IronLanguages/ironpython3/issues/1460
            test.test_ordered_dict.CPythonOrderedDictTests('test_free_after_iterating'), # AssertionError
            test.test_ordered_dict.CPythonOrderedDictTests('test_iterators_pickling'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictTests('test_key_change_during_iteration'), # AssertionError: RuntimeError not raised
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_468'), # https://github.com/IronLanguages/ironpython3/issues/1460
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_copying'), # pickle.PicklingError
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_free_after_iterating'), # AssertionError
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_pickle_recursive'), # pickle.PicklingError
            test.test_ordered_dict.PurePythonOrderedDictTests('test_468'), # https://github.com/IronLanguages/ironpython3/issues/1460
            test.test_ordered_dict.PurePythonOrderedDictTests('test_free_after_iterating'), # AssertionError
        ]

        skip_tests = [
            # https://github.com/IronLanguages/ironpython3/issues/1556
            test.test_ordered_dict.CPythonBuiltinDictTests('test_abc'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_clear'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_delitem'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_delitem_hash_collision'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_detect_deletion_during_iteration'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_highly_nested'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_highly_nested_subclass'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_init'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_override_update'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_popitem'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_reinsert'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_setitem'),
            test.test_ordered_dict.CPythonBuiltinDictTests('test_update'),

            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
            test.test_ordered_dict.CPythonOrderedDictTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
            test.test_ordered_dict.PurePythonOrderedDictTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
