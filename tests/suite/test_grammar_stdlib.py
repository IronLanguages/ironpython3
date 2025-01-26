# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_grammar from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_grammar

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_grammar, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_grammar.GrammarTests('test_former_statements_refer_to_builtins'), # https://github.com/IronLanguages/ironpython3/issues/374
            test.test_grammar.GrammarTests('test_var_annot_basic_semantics'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_var_annot_custom_maps'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_var_annot_metaclass_semantics'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_var_annot_module_semantics'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_var_annot_refleak'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_var_annot_simple_exec'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_var_annot_syntax_errors'), # https://github.com/IronLanguages/ironpython3/issues/106
            test.test_grammar.GrammarTests('test_yield'), # NotImplementedError: The method or operation is not implemented.
            test.test_grammar.TokenTests('test_underscore_literals'), # https://github.com/IronLanguages/ironpython3/issues/105
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
