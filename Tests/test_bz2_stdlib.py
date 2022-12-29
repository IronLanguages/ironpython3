# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_bz2 from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_bz2

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_bz2)

    if is_ironpython:
        failing_tests = [
            test.test_bz2.BZ2CompressorTest('testPickle'), # AssertionError: TypeError not raised
            test.test_bz2.BZ2DecompressorTest('testPickle'), # AssertionError: TypeError not raised
            test.test_bz2.BZ2FileTest('test_read_truncated'), # EOFError: Compressed file ended before the end-of-stream marker was reached
        ]

        skip_tests = [
            test.test_bz2.BZ2FileTest('testOpenDel'), # PermissionError: [WinError 32] The process cannot access the file because it is being used by another process
            test.test_bz2.BZ2FileTest('testThreading'), # unstable
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
