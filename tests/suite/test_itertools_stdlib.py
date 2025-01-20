# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_itertools from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_itertools

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_itertools)

    if is_ironpython:
        failing_tests = [
            test.test_itertools.TestBasicOps('test_combinations'), # pickling
            test.test_itertools.TestBasicOps('test_combinations_with_replacement'), # pickling
            test.test_itertools.TestBasicOps('test_compress'), # pickling
            test.test_itertools.TestBasicOps('test_cycle'), # pickling
            test.test_itertools.TestBasicOps('test_dropwhile'), # pickling
            test.test_itertools.TestBasicOps('test_filterfalse'), # pickling
            test.test_itertools.TestBasicOps('test_groupby'), # pickling
            test.test_itertools.TestBasicOps('test_islice'), # pickling
            test.test_itertools.TestBasicOps('test_permutations'), # pickling
            test.test_itertools.TestBasicOps('test_product_issue_25021'),
            test.test_itertools.TestBasicOps('test_product_pickling'),
            test.test_itertools.TestBasicOps('test_starmap'), # pickling
            test.test_itertools.TestBasicOps('test_takewhile'), # pickling
            test.test_itertools.TestBasicOps('test_tee'),
            test.test_itertools.TestBasicOps('test_zip_longest_pickling'),
            test.test_itertools.TestVariousIteratorArgs('test_compress'),
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
