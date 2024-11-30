# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_functools from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono

import test.test_functools

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_functools)

    if is_ironpython:
        failing_tests = [
            test.test_functools.TestCmpToKeyC('test_bad_cmp'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_cmp_to_key'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_cmp_to_key_arguments'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_hash'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_obj_field'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_sort_int'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestCmpToKeyC('test_sort_int_str'), # TypeError: cmp_to_key() takes exactly 1 argument (2 given)
            test.test_functools.TestPartialC('test_setstate_refcount'), # AttributeError: 'partial' object has no attribute '__setstate__'
            test.test_functools.TestPartialCSubclass('test_attributes_unwritable'), # AssertionError: AttributeError not raised by setattr
            test.test_functools.TestPartialCSubclass('test_repr'), # AssertionError
            test.test_functools.TestPartialCSubclass('test_setstate_refcount'), # AttributeError: 'PartialSubclass' object has no attribute '__setstate__'
        ]

        skip_tests = []
        if is_mono:
            skip_tests += [
                test.test_functools.TestPartialPy('test_weakref'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
