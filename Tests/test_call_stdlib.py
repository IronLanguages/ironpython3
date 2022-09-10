# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_call from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_call

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_0'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_0_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_0_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_1'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_1_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_1_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_2'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_2_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs0_2_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_0'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_0_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_0_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_1'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_1_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_1_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_2'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_2_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_oldargs1_2_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs0'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs0_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs0_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs1'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs1_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs1_kw'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs2'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs2_ext'))
        suite.addTest(test.test_call.CFunctionCalls('test_varargs2_kw'))
        suite.addTest(test.test_call.FastCallTests('test_fastcall'))
        suite.addTest(test.test_call.FastCallTests('test_fastcall_dict'))
        suite.addTest(test.test_call.FastCallTests('test_fastcall_keywords'))
        suite.addTest(unittest.expectedFailure(test.test_call.FunctionCalls('test_kwargs_order'))) # https://github.com/IronLanguages/ironpython3/issues/1460
        return suite

    else:
        return loader.loadTestsFromModule(test.test_call, pattern)

run_test(__name__)
