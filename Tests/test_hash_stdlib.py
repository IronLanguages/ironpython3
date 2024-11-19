# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_hash from StdLib
##

from iptest import is_ironpython, is_netcoreapp, is_cli32, generate_suite, run_test

import test.test_hash

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_hash)

    if is_ironpython:
        test.test_hash.BytesHashRandomizationTests('test_empty_string')
        test.test_hash.BytesHashRandomizationTests('test_fixed_hash')
        test.test_hash.BytesHashRandomizationTests('test_long_fixed_hash')
        test.test_hash.HashBuiltinsTestCase('test_hashes')
        test.test_hash.HashEqualityTestCase('test_coerced_floats')
        test.test_hash.HashEqualityTestCase('test_coerced_integers')
        test.test_hash.HashEqualityTestCase('test_numeric_literals')
        test.test_hash.HashEqualityTestCase('test_unaligned_buffers')
        test.test_hash.HashInheritanceTestCase('test_default_hash')
        test.test_hash.HashInheritanceTestCase('test_error_hash')
        test.test_hash.HashInheritanceTestCase('test_fixed_hash')
        test.test_hash.HashInheritanceTestCase('test_hashable')
        test.test_hash.HashInheritanceTestCase('test_not_hashable')
        test.test_hash.MemoryviewHashRandomizationTests('test_empty_string')
        test.test_hash.MemoryviewHashRandomizationTests('test_fixed_hash')
        test.test_hash.MemoryviewHashRandomizationTests('test_long_fixed_hash')
        test.test_hash.StrHashRandomizationTests('test_empty_string')
        test.test_hash.StrHashRandomizationTests('test_fixed_hash')
        test.test_hash.StrHashRandomizationTests('test_long_fixed_hash')
        test.test_hash.StrHashRandomizationTests('test_ucs2_string')

        failing_tests = [
            test.test_hash.BytesHashRandomizationTests('test_null_hash'), # KeyError: dotnet
            test.test_hash.MemoryviewHashRandomizationTests('test_null_hash'), # KeyError: dotnet
            test.test_hash.StrHashRandomizationTests('test_null_hash'), # KeyError: dotnet
            test.test_hash.DatetimeDateTests('test_randomized_hash'),
            test.test_hash.DatetimeDatetimeTests('test_randomized_hash'),
            test.test_hash.DatetimeTimeTests('test_randomized_hash'),
        ]

        if not is_netcoreapp:
            failing_tests += [
                test.test_hash.BytesHashRandomizationTests('test_randomized_hash'),
                test.test_hash.MemoryviewHashRandomizationTests('test_randomized_hash'),
                test.test_hash.StrHashRandomizationTests('test_randomized_hash'),
            ]

        if is_cli32:
            failing_tests += [
                test.test_hash.HashDistributionTestCase('test_hash_distribution'),
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
