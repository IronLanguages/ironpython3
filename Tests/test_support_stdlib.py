# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_support from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_support

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_support.TestSupport('test_CleanImport'))
        suite.addTest(test.test_support.TestSupport('test_DirsOnSysPath'))
        suite.addTest(test.test_support.TestSupport('test_HOST'))
        suite.addTest(unittest.expectedFailure(test.test_support.TestSupport('test_args_from_interpreter_flags'))) # https://github.com/IronLanguages/ironpython3/issues/1541
        suite.addTest(test.test_support.TestSupport('test_bind_port'))
        suite.addTest(test.test_support.TestSupport('test_captured_stderr'))
        suite.addTest(test.test_support.TestSupport('test_captured_stdin'))
        suite.addTest(test.test_support.TestSupport('test_captured_stdout'))
        suite.addTest(test.test_support.TestSupport('test_change_cwd'))
        suite.addTest(test.test_support.TestSupport('test_change_cwd__chdir_warning'))
        suite.addTest(test.test_support.TestSupport('test_change_cwd__non_existent_dir'))
        suite.addTest(test.test_support.TestSupport('test_change_cwd__non_existent_dir__quiet_true'))
        suite.addTest(test.test_support.TestSupport('test_check__all__'))
        suite.addTest(test.test_support.TestSupport('test_check_syntax_error'))
        suite.addTest(test.test_support.TestSupport('test_detect_api_mismatch'))
        suite.addTest(test.test_support.TestSupport('test_detect_api_mismatch__ignore'))
        suite.addTest(test.test_support.TestSupport('test_fd_count'))
        suite.addTest(test.test_support.TestSupport('test_find_unused_port'))
        suite.addTest(test.test_support.TestSupport('test_forget'))
        suite.addTest(test.test_support.TestSupport('test_gc_collect'))
        suite.addTest(test.test_support.TestSupport('test_get_attribute'))
        suite.addTest(test.test_support.TestSupport('test_get_original_stdout'))
        suite.addTest(test.test_support.TestSupport('test_import_fresh_module'))
        suite.addTest(test.test_support.TestSupport('test_import_module'))
        suite.addTest(test.test_support.TestSupport('test_make_bad_fd'))
        suite.addTest(test.test_support.TestSupport('test_match_test'))
        suite.addTest(unittest.expectedFailure(test.test_support.TestSupport('test_optim_args_from_interpreter_flags'))) # https://github.com/IronLanguages/ironpython3/issues/1541
        suite.addTest(test.test_support.TestSupport('test_python_is_optimized'))
        suite.addTest(test.test_support.TestSupport('test_rmtree'))
        suite.addTest(test.test_support.TestSupport('test_sortdict'))
        suite.addTest(test.test_support.TestSupport('test_swap_attr'))
        suite.addTest(test.test_support.TestSupport('test_swap_item'))
        suite.addTest(test.test_support.TestSupport('test_temp_cwd'))
        suite.addTest(test.test_support.TestSupport('test_temp_cwd__name_none'))
        suite.addTest(test.test_support.TestSupport('test_temp_dir'))
        suite.addTest(test.test_support.TestSupport('test_temp_dir__existing_dir__quiet_default'))
        suite.addTest(test.test_support.TestSupport('test_temp_dir__existing_dir__quiet_true'))
        suite.addTest(test.test_support.TestSupport('test_temp_dir__forked_child'))
        suite.addTest(test.test_support.TestSupport('test_temp_dir__path_none'))
        suite.addTest(test.test_support.TestSupport('test_unlink'))
        suite.addTest(test.test_support.TestSupport('test_unload'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_support, pattern)

run_test(__name__)
