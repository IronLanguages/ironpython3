# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ordered_dict from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono

import test.test_ordered_dict

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_ordered_dict, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_copying'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_free_after_iterating'), # AssertionError
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_iterators_pickling'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_key_change_during_iteration'), # AssertionError: RuntimeError not raised
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_pickle_recursive'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictTests('test_free_after_iterating'), # AssertionError
            test.test_ordered_dict.CPythonOrderedDictTests('test_iterators_pickling'), # pickle.PicklingError
            test.test_ordered_dict.CPythonOrderedDictTests('test_key_change_during_iteration'), # AssertionError: RuntimeError not raised
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_copying'), # pickle.PicklingError
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_free_after_iterating'), # AssertionError
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_pickle_recursive'), # pickle.PicklingError
            test.test_ordered_dict.PurePythonOrderedDictTests('test_free_after_iterating'), # AssertionError
        ]

        skip_tests = [
            test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
            test.test_ordered_dict.CPythonOrderedDictTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
            test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
            test.test_ordered_dict.PurePythonOrderedDictTests('test_highly_nested_subclass'), # intermittent AssertionError - GC related?
        ]
        if is_mono: # maybe https://github.com/IronLanguages/ironpython3/issues/544
            skip_tests += [
                test.test_ordered_dict.PurePythonOrderedDictSubclassTests('test_reference_loop'),
                test.test_ordered_dict.PurePythonOrderedDictTests('test_reference_loop'),
                test.test_ordered_dict.CPythonOrderedDictSubclassTests('test_reference_loop'),
                test.test_ordered_dict.CPythonOrderedDictTests('test_reference_loop'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
