# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_os from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test, is_posix

import test.test_os

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_os, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_os.DeviceEncodingTests('test_bad_fd'), # AttributeError: 'module' object has no attribute 'device_encoding'
            test.test_os.DeviceEncodingTests('test_device_encoding'), # AttributeError: 'module' object has no attribute 'device_encoding'
            test.test_os.ExecTests('test_execvpe_with_bad_arglist'), # NameError: name 'execv' is not defined
            test.test_os.ExecTests('test_execvpe_with_bad_program'), # NameError: name 'execv' is not defined
            test.test_os.ExecTests('test_internal_execvpe_str'), # NameError: name 'execv' is not defined
            test.test_os.FDInheritanceTests('test_dup'), # AttributeError: 'module' object has no attribute 'get_inheritable'
            test.test_os.FDInheritanceTests('test_dup2'), # AttributeError: 'module' object has no attribute 'get_inheritable'
            test.test_os.FDInheritanceTests('test_get_set_inheritable'), # AttributeError: 'module' object has no attribute 'get_inheritable'
            test.test_os.FDInheritanceTests('test_open'), # AttributeError: 'module' object has no attribute 'get_inheritable'
            test.test_os.FDInheritanceTests('test_pipe'), # AttributeError: 'module' object has no attribute 'get_inheritable'
            test.test_os.FileTests('test_open_keywords'), # IndexError: Index was outside the bounds of the array.
            test.test_os.FileTests('test_symlink_keywords'), # IndexError: Index was outside the bounds of the array.
            test.test_os.OSErrorTests('test_oserror_filename'), # AssertionError: '@test_732_tmp' is not b'@test_732_tmp'
            test.test_os.StatAttributeTests('test_stat_result_pickle'), # AssertionError
            test.test_os.UtimeTests('test_utime'), # OSError: [WinError 87] The parameter is incorrect.
            test.test_os.UtimeTests('test_utime_by_indexed'), # OSError: [WinError 87] The parameter is incorrect.
            test.test_os.UtimeTests('test_utime_by_times'), # OSError: [WinError 87] The parameter is incorrect.
            test.test_os.UtimeTests('test_utime_directory'), # OSError: [WinError 87] The parameter is incorrect.
            test.test_os.Win32KillTests('test_kill_int'), # AssertionError
            test.test_os.Win32KillTests('test_kill_sigterm'), # AssertionError
        ]
        if is_posix:
            failing_tests += [
                test.test_os.EnvironTests('test_unset_error'), # ValueError: Environment variable name cannot contain equal character. (Parameter 'variable')
                test.test_os.FDInheritanceTests('test_get_inheritable_cloexec'), # AttributeError: 'module' object has no attribute 'get_inheritable'
                test.test_os.FDInheritanceTests('test_set_inheritable_cloexec'), # AssertionError
            ]
        else:
            failing_tests += [
                test.test_os.UtimeTests('test_utime_current'), # AssertionError
                test.test_os.UtimeTests('test_utime_current_old'), # AssertionError
            ]
        if sys.version_info < (3, 6):
            failing_tests += [
                test.test_os.Win32DeprecatedBytesAPI('test_deprecated'), # AttributeError: 'module' object has no attribute '_isdir'
            ]
        if sys.version_info >= (3, 6):
            failing_tests += [
                test.test_os.BytesWalkTests('test_walk_bottom_up'), # AssertionError
                test.test_os.ExecTests('test_execv_with_bad_arglist'), # NameError: name 'execv' is not defined
                test.test_os.ExecTests('test_execve_invalid_env'), # NameError: name 'execve' is not defined
                test.test_os.ExecTests('test_execve_with_empty_path'), # NameError: name 'execve' is not defined
                test.test_os.PathTConverterTests('test_path_t_converter'), # AssertionError
                test.test_os.StatAttributeTests('test_file_attributes'), # AssertionError
                test.test_os.TestInvalidFD('test_inheritable'), # AttributeError: 'module' object has no attribute 'get_inheritable'
                test.test_os.TestScandir('test_attributes'), # OSError: [WinError 1] Incorrect function
                test.test_os.TestScandir('test_resource_warning'), # AssertionError: ResourceWarning not triggered
                test.test_os.UtimeTests('test_utime_invalid_arguments'), # OSError: [WinError 87] The parameter is incorrect.
                test.test_os.WalkTests('test_walk_bottom_up'), # AssertionError
                test.test_os.Win32JunctionTests('test_create_junction'), # AttributeError: 'module' object has no attribute 'CreateJunction'
                test.test_os.Win32JunctionTests('test_unlink_removes_junction'), # AttributeError: 'module' object has no attribute 'CreateJunction'
            ]
            if is_posix:
                failing_tests += [
                    test.test_os.TestScandir('test_removed_dir'), # FileNotFoundError
                    test.test_os.TestScandir('test_removed_file'), # FileNotFoundError
                ]

        skip_tests = [
            # these require symlink support on drive
            test.test_os.LinkTests('test_link'),
            test.test_os.LinkTests('test_link_bytes'),
            test.test_os.LinkTests('test_unicode_name'),
        ]
        if sys.version_info >= (3, 6):
            skip_tests += [
                # SpawnTests seem to create a console
                test.test_os.SpawnTests('test_nowait'),
                test.test_os.SpawnTests('test_spawnl'),
                test.test_os.SpawnTests('test_spawnl_noargs'),
                test.test_os.SpawnTests('test_spawnle'),
                test.test_os.SpawnTests('test_spawnle_noargs'),
                test.test_os.SpawnTests('test_spawnlp'),
                test.test_os.SpawnTests('test_spawnlpe'),
                test.test_os.SpawnTests('test_spawnv'),
                test.test_os.SpawnTests('test_spawnv_noargs'),
                test.test_os.SpawnTests('test_spawnve'),
                test.test_os.SpawnTests('test_spawnve_bytes'),
                test.test_os.SpawnTests('test_spawnve_invalid_env'),
                test.test_os.SpawnTests('test_spawnve_noargs'),
                test.test_os.SpawnTests('test_spawnvp'),
                test.test_os.SpawnTests('test_spawnvpe'),
                test.test_os.SpawnTests('test_spawnvpe_invalid_env'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
