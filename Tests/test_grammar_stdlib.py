# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_grammar from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_grammar

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_grammar.GrammarTests('testAssert2'))
        suite.addTest(test.test_grammar.GrammarTests('test_additive_ops'))
        suite.addTest(test.test_grammar.GrammarTests('test_assert'))
        suite.addTest(test.test_grammar.GrammarTests('test_atoms'))
        suite.addTest(test.test_grammar.GrammarTests('test_binary_mask_ops'))
        suite.addTest(test.test_grammar.GrammarTests('test_break_continue_loop'))
        suite.addTest(test.test_grammar.GrammarTests('test_break_in_finally'))
        suite.addTest(test.test_grammar.GrammarTests('test_break_stmt'))
        suite.addTest(test.test_grammar.GrammarTests('test_classdef'))
        suite.addTest(test.test_grammar.GrammarTests('test_comparison'))
        suite.addTest(test.test_grammar.GrammarTests('test_comprehension_specials'))
        suite.addTest(test.test_grammar.GrammarTests('test_continue_stmt'))
        suite.addTest(test.test_grammar.GrammarTests('test_del_stmt'))
        suite.addTest(test.test_grammar.GrammarTests('test_dictcomps'))
        suite.addTest(test.test_grammar.GrammarTests('test_eval_input'))
        suite.addTest(test.test_grammar.GrammarTests('test_expr_stmt'))
        suite.addTest(test.test_grammar.GrammarTests('test_for'))
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_former_statements_refer_to_builtins'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(test.test_grammar.GrammarTests('test_funcdef'))
        suite.addTest(test.test_grammar.GrammarTests('test_genexps'))
        suite.addTest(test.test_grammar.GrammarTests('test_global'))
        suite.addTest(test.test_grammar.GrammarTests('test_if'))
        suite.addTest(test.test_grammar.GrammarTests('test_if_else_expr'))
        suite.addTest(test.test_grammar.GrammarTests('test_import'))
        suite.addTest(test.test_grammar.GrammarTests('test_lambdef'))
        suite.addTest(test.test_grammar.GrammarTests('test_listcomps'))
        suite.addTest(test.test_grammar.GrammarTests('test_matrix_mul'))
        suite.addTest(test.test_grammar.GrammarTests('test_multiplicative_ops'))
        suite.addTest(test.test_grammar.GrammarTests('test_nonlocal'))
        suite.addTest(test.test_grammar.GrammarTests('test_paren_evaluation'))
        suite.addTest(test.test_grammar.GrammarTests('test_pass_stmt'))
        suite.addTest(test.test_grammar.GrammarTests('test_raise'))
        suite.addTest(test.test_grammar.GrammarTests('test_return'))
        suite.addTest(test.test_grammar.GrammarTests('test_return_in_finally'))
        suite.addTest(test.test_grammar.GrammarTests('test_selectors'))
        suite.addTest(test.test_grammar.GrammarTests('test_shift_ops'))
        suite.addTest(test.test_grammar.GrammarTests('test_simple_stmt'))
        suite.addTest(test.test_grammar.GrammarTests('test_suite'))
        suite.addTest(test.test_grammar.GrammarTests('test_test'))
        suite.addTest(test.test_grammar.GrammarTests('test_try'))
        suite.addTest(test.test_grammar.GrammarTests('test_unary_ops'))
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_basic_semantics'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(test.test_grammar.GrammarTests('test_var_annot_basics'))
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_custom_maps'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(test.test_grammar.GrammarTests('test_var_annot_in_module'))
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_metaclass_semantics'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_module_semantics'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_refleak'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_simple_exec'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_var_annot_syntax_errors'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(test.test_grammar.GrammarTests('test_while'))
        suite.addTest(test.test_grammar.GrammarTests('test_with_statement'))
        suite.addTest(unittest.expectedFailure(test.test_grammar.GrammarTests('test_yield'))) # NotImplementedError: The method or operation is not implemented.
        suite.addTest(test.test_grammar.TokenTests('test_backslash'))
        suite.addTest(test.test_grammar.TokenTests('test_ellipsis'))
        suite.addTest(test.test_grammar.TokenTests('test_eof_error'))
        suite.addTest(test.test_grammar.TokenTests('test_float_exponent_tokenization'))
        suite.addTest(test.test_grammar.TokenTests('test_floats'))
        suite.addTest(test.test_grammar.TokenTests('test_long_integers'))
        suite.addTest(test.test_grammar.TokenTests('test_plain_integers'))
        suite.addTest(test.test_grammar.TokenTests('test_string_literals'))
        suite.addTest(unittest.expectedFailure(test.test_grammar.TokenTests('test_underscore_literals'))) # https://github.com/IronLanguages/ironpython3/issues/105
        return suite

    else:
        return loader.loadTestsFromModule(test.test_grammar, pattern)

run_test(__name__)
