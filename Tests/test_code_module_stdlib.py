# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_code_module from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_code_module

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_banner'))
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_cause_tb'))
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_console_stderr'))
        suite.addTest(unittest.expectedFailure(test.test_code_module.TestInteractiveConsole('test_context_tb'))) # https://github.com/IronLanguages/ironpython3/issues/1557
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_exit_msg'))
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_ps1'))
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_ps2'))
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_syntax_error'))
        suite.addTest(test.test_code_module.TestInteractiveConsole('test_sysexcepthook'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_code_module, pattern)

run_test(__name__)
