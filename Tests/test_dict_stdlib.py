# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_dict from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono

import test.test_dict

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_dict)

    if is_ironpython:
        failing_tests = [
            test.test_dict.DictTest('test_equal_operator_modifying_operand'),
            test.test_dict.DictTest('test_errors_in_view_containment_check'),
            test.test_dict.DictTest('test_instance_dict_getattr_str_subclass'),
            test.test_dict.DictTest('test_itemiterator_pickling'),
            test.test_dict.DictTest('test_merge_and_mutate'),
            test.test_dict.DictTest('test_oob_indexing_dictiter_iternextitem'),
            test.test_dict.DictTest('test_setdefault_atomic'),
        ]
        if is_mono:
            failing_tests += [
                test.test_dict.DictTest('test_container_iterator'), # https://github.com/IronLanguages/ironpython3/issues/544
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
