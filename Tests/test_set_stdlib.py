# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_set from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_set

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_set, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_set.TestFrozenSet('test_do_not_rehash_dict_keys'), # https://github.com/IronLanguages/ironpython3/issues/848
            test.test_set.TestFrozenSet('test_hash_effectiveness'), # https://github.com/IronLanguages/ironpython3/issues/1538
            test.test_set.TestFrozenSetSubclass('test_do_not_rehash_dict_keys'), # https://github.com/IronLanguages/ironpython3/issues/848
            test.test_set.TestFrozenSetSubclass('test_hash_effectiveness'), # https://github.com/IronLanguages/ironpython3/issues/1538
            test.test_set.TestSet('test_do_not_rehash_dict_keys'), # https://github.com/IronLanguages/ironpython3/issues/848
            test.test_set.TestSetSubclass('test_do_not_rehash_dict_keys'), # https://github.com/IronLanguages/ironpython3/issues/848
            test.test_set.TestSetSubclassWithKeywordArgs('test_do_not_rehash_dict_keys'), # https://github.com/IronLanguages/ironpython3/issues/848
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
