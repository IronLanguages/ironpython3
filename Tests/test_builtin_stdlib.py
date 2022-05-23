# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_builtin from StdLib
##

import unittest
import sys

from iptest import run_test, is_netcoreapp

import builtins
import doctest
import test.test_builtin

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_builtin.BuiltinTest('test_abs'))
        suite.addTest(test.test_builtin.BuiltinTest('test_all'))
        suite.addTest(test.test_builtin.BuiltinTest('test_any'))
        suite.addTest(test.test_builtin.BuiltinTest('test_ascii'))
        suite.addTest(test.test_builtin.BuiltinTest('test_bin'))
        suite.addTest(test.test_builtin.BuiltinTest('test_bug_27936'))
        suite.addTest(test.test_builtin.BuiltinTest('test_bytearray_translate'))
        suite.addTest(test.test_builtin.BuiltinTest('test_callable'))
        suite.addTest(test.test_builtin.BuiltinTest('test_chr'))
        suite.addTest(test.test_builtin.BuiltinTest('test_cmp'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.BuiltinTest('test_compile')))
        suite.addTest(test.test_builtin.BuiltinTest('test_construct_singletons'))
        suite.addTest(test.test_builtin.BuiltinTest('test_delattr'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.BuiltinTest('test_dir')))
        suite.addTest(test.test_builtin.BuiltinTest('test_divmod'))
        suite.addTest(test.test_builtin.BuiltinTest('test_eval'))
        suite.addTest(test.test_builtin.BuiltinTest('test_exec'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.BuiltinTest('test_exec_globals')))
        suite.addTest(test.test_builtin.BuiltinTest('test_exec_redirected'))
        suite.addTest(test.test_builtin.BuiltinTest('test_filter'))
        suite.addTest(test.test_builtin.BuiltinTest('test_filter_pickle'))
        suite.addTest(test.test_builtin.BuiltinTest('test_format'))
        suite.addTest(test.test_builtin.BuiltinTest('test_general_eval'))
        suite.addTest(test.test_builtin.BuiltinTest('test_getattr'))
        suite.addTest(test.test_builtin.BuiltinTest('test_hasattr'))
        suite.addTest(test.test_builtin.BuiltinTest('test_hash'))
        suite.addTest(test.test_builtin.BuiltinTest('test_hex'))
        suite.addTest(test.test_builtin.BuiltinTest('test_id'))
        suite.addTest(test.test_builtin.BuiltinTest('test_import'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.BuiltinTest('test_input')))
        suite.addTest(test.test_builtin.BuiltinTest('test_isinstance'))
        suite.addTest(test.test_builtin.BuiltinTest('test_issubclass'))
        suite.addTest(test.test_builtin.BuiltinTest('test_iter'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.BuiltinTest('test_len')))
        suite.addTest(test.test_builtin.BuiltinTest('test_map'))
        suite.addTest(test.test_builtin.BuiltinTest('test_map_pickle'))
        suite.addTest(test.test_builtin.BuiltinTest('test_max'))
        suite.addTest(test.test_builtin.BuiltinTest('test_min'))
        suite.addTest(test.test_builtin.BuiltinTest('test_neg'))
        suite.addTest(test.test_builtin.BuiltinTest('test_next'))
        suite.addTest(test.test_builtin.BuiltinTest('test_oct'))
        suite.addTest(test.test_builtin.BuiltinTest('test_open'))
        suite.addTest(test.test_builtin.BuiltinTest('test_open_default_encoding'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.BuiltinTest('test_open_non_inheritable'))) # https://github.com/IronLanguages/ironpython3/issues/1225
        suite.addTest(test.test_builtin.BuiltinTest('test_ord'))
        suite.addTest(test.test_builtin.BuiltinTest('test_pow'))
        suite.addTest(test.test_builtin.BuiltinTest('test_repr'))
        suite.addTest(test.test_builtin.BuiltinTest('test_round'))
        suite.addTest(test.test_builtin.BuiltinTest('test_round_large'))
        suite.addTest(test.test_builtin.BuiltinTest('test_setattr'))
        suite.addTest(test.test_builtin.BuiltinTest('test_sum'))
        suite.addTest(test.test_builtin.BuiltinTest('test_type'))
        suite.addTest(test.test_builtin.BuiltinTest('test_vars'))
        suite.addTest(test.test_builtin.BuiltinTest('test_zip'))
        suite.addTest(test.test_builtin.BuiltinTest('test_zip_pickle'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.PtyTests('test_input_no_stdout_fileno')))
        suite.addTest(unittest.expectedFailure(test.test_builtin.PtyTests('test_input_tty')))
        suite.addTest(unittest.expectedFailure(test.test_builtin.PtyTests('test_input_tty_non_ascii')))
        suite.addTest(unittest.expectedFailure(test.test_builtin.PtyTests('test_input_tty_non_ascii_unicode_errors')))
        if not is_netcoreapp:
            suite.addTest(test.test_builtin.ShutdownTest('test_cleanup'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestSorted('test_bad_arguments'))) # AssertionError: TypeError not raised
        suite.addTest(test.test_builtin.TestSorted('test_baddecorator'))
        suite.addTest(test.test_builtin.TestSorted('test_basic'))
        suite.addTest(test.test_builtin.TestSorted('test_inputtypes'))
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_bad_args'))) # AssertionError: TypeError not raised
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_bad_slots'))) # AssertionError: TypeError not raised
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_namespace_order'))) # https://github.com/IronLanguages/ironpython3/issues/1468
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_new_type'))) # AssertionError: <class 'test.test_builtin.B'> is not <class 'int'>
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_type_doc'))) # AssertionError: UnicodeEncodeError not raised
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_type_name'))) # AssertionError: ValueError not raised
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_type_nokwargs'))) # AssertionError: TypeError not raised
        suite.addTest(unittest.expectedFailure(test.test_builtin.TestType('test_type_qualname'))) # https://github.com/IronLanguages/ironpython3/issues/30
        suite.addTest(doctest.DocTestSuite(builtins))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_builtin, pattern)

run_test(__name__)
