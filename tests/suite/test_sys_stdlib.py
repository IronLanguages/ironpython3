# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_sys from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono

import test.test_sys

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_sys)

    if is_ironpython:
        failing_tests = [
            test.test_sys.SysModuleTest('test_excepthook'), # TypeError: Exception expected for value, str found
            test.test_sys.SysModuleTest('test_lost_displayhook'), # TypeError: NoneType is not callable
            test.test_sys.SysModuleTest('test_setcheckinterval'), # NotImplementedError: IronPython does not support sys.getcheckinterval
            test.test_sys.SysModuleTest('test_switchinterval'), # AttributeError: 'module' object has no attribute 'setswitchinterval'
        ]

        skip_tests = [
            test.test_sys.SysModuleTest('test_43581'), # TODO: figure out - failing in CI
            test.test_sys.SysModuleTest('test_current_frames'), # TODO: slow and fails
            test.test_sys.SysModuleTest('test_executable'), # TODO: figure out - failing in CI
            test.test_sys.SysModuleTest('test_exit'), # TODO: slow and fails
            test.test_sys.SysModuleTest('test_ioencoding'), # AssertionError: b'\x9b' != b'J\r%'
            test.test_sys.SysModuleTest('test_ioencoding_nonascii'), # AssertionError: b'\x91' != b'\xe6'
            test.test_sys.SysModuleTest('test_recursionlimit_fatalerror'), # StackOverflowException
            test.test_sys.SysModuleTest('test_recursionlimit_recovery'), # StackOverflowException
        ]
        if is_mono:
            skip_tests += [
                test.test_sys.SysModuleTest('test_intern'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
