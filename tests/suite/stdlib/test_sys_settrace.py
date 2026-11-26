# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_sys_settrace from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_sys_settrace

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_sys_settrace, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_sys_settrace.TraceTestCase('test_10_ireturn'), # CPython 3.4 reports the return on the line of `else: pass`, which its bytecode ends the if statement with
            test.test_sys_settrace.TraceTestCase('test_13_genexp'), # the generator abandoned by any() is closed by the garbage collector, not at once by reference counting
        ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
