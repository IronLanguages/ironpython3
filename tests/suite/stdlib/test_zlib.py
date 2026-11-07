# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_zlib from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_zlib

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_zlib, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_zlib.CompressObjectTestCase('test_dictionary'), # compressobj() has no zdict argument
            test.test_zlib.CompressObjectTestCase('test_dictionary_streaming'), # compressobj() has no zdict argument
        ]
        if sys.version_info >= (3, 6):
            failing_tests += [
                test.test_zlib.CompressObjectTestCase('test_decompress_raw_with_dictionary'), # compressobj() has no zdict argument
                test.test_zlib.CompressObjectTestCase('test_keywords'), # compressobj() has no zdict argument
                test.test_zlib.CompressObjectTestCase('test_wbits'), # compressobj() can't produce gzip output
                test.test_zlib.CompressTestCase('test_keywords'), # compress() data argument isn't positional-only
            ]
        else:
            failing_tests += [
                test.test_zlib.CompressObjectTestCase('test_compressoptions'), # compressobj() has no zdict argument
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
