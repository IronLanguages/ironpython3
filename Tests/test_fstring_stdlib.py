# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_fstring from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_fstring

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_fstring.TestCase('test__format__lookup'))
        #suite.addTest(test.test_fstring.TestCase('test_arguments')) # TODO: f-string in format spec
        suite.addTest(test.test_fstring.TestCase('test_assignment'))
        suite.addTest(test.test_fstring.TestCase('test_ast'))
        suite.addTest(test.test_fstring.TestCase('test_ast_compile_time_concat'))
        suite.addTest(unittest.expectedFailure(test.test_fstring.TestCase('test_ast_line_numbers'))) # TODO: ast line numbers
        suite.addTest(unittest.expectedFailure(test.test_fstring.TestCase('test_ast_line_numbers_duplicate_expression'))) # TODO: ast line numbers
        suite.addTest(unittest.expectedFailure(test.test_fstring.TestCase('test_ast_line_numbers_multiline_fstring'))) # TODO: ast line numbers
        suite.addTest(unittest.expectedFailure(test.test_fstring.TestCase('test_ast_line_numbers_multiple_formattedvalues'))) # TODO: ast line numbers
        suite.addTest(unittest.expectedFailure(test.test_fstring.TestCase('test_ast_line_numbers_nested'))) # TODO: ast line numbers
        suite.addTest(test.test_fstring.TestCase('test_backslash_char'))
        suite.addTest(test.test_fstring.TestCase('test_backslashes_in_string_part'))
        suite.addTest(test.test_fstring.TestCase('test_call'))
        suite.addTest(test.test_fstring.TestCase('test_closure'))
        suite.addTest(test.test_fstring.TestCase('test_comments'))
        suite.addTest(test.test_fstring.TestCase('test_compile_time_concat'))
        suite.addTest(test.test_fstring.TestCase('test_compile_time_concat_errors'))
        suite.addTest(test.test_fstring.TestCase('test_conversions'))
        suite.addTest(test.test_fstring.TestCase('test_del'))
        suite.addTest(test.test_fstring.TestCase('test_dict'))
        suite.addTest(test.test_fstring.TestCase('test_docstring'))
        suite.addTest(test.test_fstring.TestCase('test_double_braces'))
        suite.addTest(test.test_fstring.TestCase('test_empty_format_specifier'))
        suite.addTest(test.test_fstring.TestCase('test_errors'))
        suite.addTest(test.test_fstring.TestCase('test_expressions_with_triple_quoted_strings'))
        #suite.addTest(test.test_fstring.TestCase('test_format_specifier_expressions')) # TODO: f-string in format spec
        suite.addTest(test.test_fstring.TestCase('test_global'))
        suite.addTest(test.test_fstring.TestCase('test_if_conditional'))
        suite.addTest(test.test_fstring.TestCase('test_invalid_string_prefixes'))
        suite.addTest(test.test_fstring.TestCase('test_lambda'))
        suite.addTest(test.test_fstring.TestCase('test_leading_trailing_spaces'))
        suite.addTest(test.test_fstring.TestCase('test_literal'))
        suite.addTest(test.test_fstring.TestCase('test_literal_eval'))
        suite.addTest(test.test_fstring.TestCase('test_locals'))
        suite.addTest(test.test_fstring.TestCase('test_loop'))
        suite.addTest(test.test_fstring.TestCase('test_many_expressions')) # TODO: f-string in format spec
        suite.addTest(test.test_fstring.TestCase('test_misformed_unicode_character_name')) # TODO: error is thrown in the parser instead of the tokenizer
        suite.addTest(test.test_fstring.TestCase('test_mismatched_braces'))
        suite.addTest(test.test_fstring.TestCase('test_mismatched_parens'))
        suite.addTest(test.test_fstring.TestCase('test_missing_expression'))
        suite.addTest(test.test_fstring.TestCase('test_missing_format_spec'))
        suite.addTest(test.test_fstring.TestCase('test_missing_variable'))
        suite.addTest(test.test_fstring.TestCase('test_multiple_vars'))
        suite.addTest(test.test_fstring.TestCase('test_nested_fstrings'))
        #suite.addTest(test.test_fstring.TestCase('test_newlines_in_expressions')) # TODO: newlines
        suite.addTest(test.test_fstring.TestCase('test_no_backslashes_in_expression_part'))
        suite.addTest(test.test_fstring.TestCase('test_no_escapes_for_braces'))
        #suite.addTest(test.test_fstring.TestCase('test_not_equal')) # TODO: special case not equal
        suite.addTest(test.test_fstring.TestCase('test_parens_in_expressions'))
        suite.addTest(test.test_fstring.TestCase('test_shadowed_global'))
        suite.addTest(test.test_fstring.TestCase('test_side_effect_order'))
        suite.addTest(test.test_fstring.TestCase('test_str_format_differences'))
        suite.addTest(test.test_fstring.TestCase('test_unterminated_string'))
        #suite.addTest(test.test_fstring.TestCase('test_yield')) # TODO: yield
        #suite.addTest(test.test_fstring.TestCase('test_yield_send')) # TODO: yield
        return suite

    else:
        return loader.loadTestsFromModule(test.test_fstring, pattern)

run_test(__name__)
