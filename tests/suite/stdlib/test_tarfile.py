# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_tarfile from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test, is_exfat, is_windows

import test.test_tarfile

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_tarfile, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_tarfile.ListTest('test_list_verbose'), # AssertionError: Regex didn't match (stat.filemode(0o644) gives '-rw-r--r--' instead of '?rw-r--r--')
            test.test_tarfile.GzipListTest('test_list_verbose'), # AssertionError: Regex didn't match
            test.test_tarfile.Bz2ListTest('test_list_verbose'), # AssertionError: Regex didn't match
            test.test_tarfile.UstarReadTest('test_fileobj_seek'), # AssertionError: 2048 != 0 : seek() to absolute position failed
            test.test_tarfile.GzipUstarReadTest('test_fileobj_seek'), # AssertionError: 2048 != 0 : seek() to absolute position failed
            test.test_tarfile.Bz2UstarReadTest('test_fileobj_seek'), # AssertionError: 2048 != 0 : seek() to absolute position failed
            test.test_tarfile.MiscReadTest('test_bytes_name_attribute'), # AssertionError: '...' is not an instance of <class 'bytes'>
            test.test_tarfile.GzipMiscReadTest('test_bytes_name_attribute'), # AssertionError: '...' is not an instance of <class 'bytes'>
            test.test_tarfile.Bz2PartialReadTest('test_partial_input'), # AssertionError: infinite loop detected in tarfile.open()
            test.test_tarfile.Bz2PartialReadTest('test_partial_input_bz2'), # AssertionError: infinite loop detected in tarfile.open()
            test.test_tarfile.CommandLineTest('test_list_command'), # AssertionError: PYTHONIOENCODING is ignored
            test.test_tarfile.CommandLineTest('test_list_command_verbose'), # AssertionError: PYTHONIOENCODING is ignored
        ]

        skip_tests = []

        hardlink_tests = [
            test.test_tarfile.WriteTest('test_link_size'), # AssertionError: 3 != 0 (os.stat().st_nlink is 0)
            test.test_tarfile.GzipWriteTest('test_link_size'), # AssertionError: 3 != 0
            test.test_tarfile.Bz2WriteTest('test_link_size'), # AssertionError: 3 != 0
            test.test_tarfile.HardlinkTest('test_add_hardlink'), # AssertionError: b'0' != b'1' : add file as hardlink failed
        ]

        if is_windows and is_exfat():
            skip_tests += hardlink_tests + [
                # OSError: [WinError 1] Incorrect function (no hard links on exFAT)
                test.test_tarfile.HardlinkTest('test_add_twice'),
                test.test_tarfile.HardlinkTest('test_dereference_hardlink'),

                # AssertionError: 1041808784.0 != 1041808783
                test.test_tarfile.Bz2MiscReadTest('test_extract_directory'),
                test.test_tarfile.Bz2MiscReadTest('test_extractall'),
                test.test_tarfile.GzipMiscReadTest('test_extract_directory'),
                test.test_tarfile.GzipMiscReadTest('test_extractall'),
                test.test_tarfile.MiscReadTest('test_extract_directory'),
                test.test_tarfile.MiscReadTest('test_extractall'),
            ]

            if sys.version_info >= (3, 6):
                skip_tests += [
                    # AssertionError: 1041808784.0 != 1041808783
                    test.test_tarfile.Bz2MiscReadTest('test_extract_pathlike_name'),
                    test.test_tarfile.Bz2MiscReadTest('test_extractall_pathlike_name'),
                    test.test_tarfile.GzipMiscReadTest('test_extract_pathlike_name'),
                    test.test_tarfile.GzipMiscReadTest('test_extractall_pathlike_name'),
                    test.test_tarfile.MiscReadTest('test_extract_pathlike_name'),
                    test.test_tarfile.MiscReadTest('test_extractall_pathlike_name'),
                ]
        elif is_windows:
            failing_tests += hardlink_tests

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
