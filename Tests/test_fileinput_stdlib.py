# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_fileinput from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_fileinput

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_fileinput.BufferSizesTests('test_buffer_sizes'))
        suite.addTest(test.test_fileinput.FileInputTests('test__getitem__'))
        suite.addTest(test.test_fileinput.FileInputTests('test__getitem__eof'))
        suite.addTest(test.test_fileinput.FileInputTests('test__getitem__invalid_key'))
        suite.addTest(test.test_fileinput.FileInputTests('test_close_on_exception'))
        suite.addTest(test.test_fileinput.FileInputTests('test_context_manager'))
        suite.addTest(test.test_fileinput.FileInputTests('test_detached_stdin_binary_mode'))
        suite.addTest(test.test_fileinput.FileInputTests('test_empty_files_list_specified_to_constructor'))
        suite.addTest(test.test_fileinput.FileInputTests('test_file_opening_hook'))
        suite.addTest(test.test_fileinput.FileInputTests('test_fileno'))
        suite.addTest(test.test_fileinput.FileInputTests('test_fileno_when_ValueError_raised'))
        suite.addTest(test.test_fileinput.FileInputTests('test_files_that_dont_end_with_newline'))
        suite.addTest(test.test_fileinput.FileInputTests('test_iteration_buffering'))
        suite.addTest(test.test_fileinput.FileInputTests('test_nextfile_oserror_deleting_backup'))
        suite.addTest(test.test_fileinput.FileInputTests('test_opening_mode'))
        suite.addTest(test.test_fileinput.FileInputTests('test_readline'))
        suite.addTest(test.test_fileinput.FileInputTests('test_readline_binary_mode'))
        suite.addTest(test.test_fileinput.FileInputTests('test_readline_buffering'))
        suite.addTest(test.test_fileinput.FileInputTests('test_readline_os_chmod_raises_OSError'))
        suite.addTest(test.test_fileinput.FileInputTests('test_readline_os_fstat_raises_OSError'))
        suite.addTest(test.test_fileinput.FileInputTests('test_stdin_binary_mode'))
        suite.addTest(test.test_fileinput.FileInputTests('test_zero_byte_files'))
        suite.addTest(test.test_fileinput.MiscTest('test_all'))
        suite.addTest(test.test_fileinput.Test_fileinput_close('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_close('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_filelineno('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_filelineno('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_filename('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_filename('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_fileno('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_fileno('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_input('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_input('test_state_is_not_None_and_state_file_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_input('test_state_is_not_None_and_state_file_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_isfirstline('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_isfirstline('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_isstdin('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_isstdin('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_lineno('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_lineno('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_nextfile('test_state_is_None'))
        suite.addTest(test.test_fileinput.Test_fileinput_nextfile('test_state_is_not_None'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_blah_ext'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_bz2_ext_builtin'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_bz2_ext_fake'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_empty_string'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_gz_ext_builtin'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_gz_ext_fake'))
        suite.addTest(test.test_fileinput.Test_hook_compressed('test_no_ext'))
        suite.addTest(test.test_fileinput.Test_hook_encoded('test'))
        suite.addTest(unittest.expectedFailure(test.test_fileinput.Test_hook_encoded('test_errors'))) # https://github.com/IronLanguages/ironpython3/issues/1452
        suite.addTest(test.test_fileinput.Test_hook_encoded('test_modes'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_fileinput, pattern)

run_test(__name__)
