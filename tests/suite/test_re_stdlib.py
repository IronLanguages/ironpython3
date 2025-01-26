# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_re from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_re

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_re, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_re.ExternalTests('test_re_tests'),
            test.test_re.ReTests('test_ascii_and_unicode_flag'),
            test.test_re.ReTests('test_backref_group_name_in_exception'),
            test.test_re.ReTests('test_bug_1661'),
            test.test_re.ReTests('test_bug_29444'), # AssertionError
            test.test_re.ReTests('test_bug_581080'),
            test.test_re.ReTests('test_bug_764548'),
            test.test_re.ReTests('test_character_set_errors'), # AttributeError: 'error' object has no attribute 'msg'
            test.test_re.ReTests('test_compile'),
            test.test_re.ReTests('test_dealloc'),
            test.test_re.ReTests('test_debug_flag'),
            test.test_re.ReTests('test_enum'), # AssertionError: 'ASCII' not found in '256'
            test.test_re.ReTests('test_error'), # AttributeError: 'error' object has no attribute 'pattern'
            test.test_re.ReTests('test_group_name_in_exception'),
            test.test_re.ReTests('test_ignore_case'),
            test.test_re.ReTests('test_ignore_case_range'),
            test.test_re.ReTests('test_ignore_case_set'),
            test.test_re.ReTests('test_keep_buffer'),
            test.test_re.ReTests('test_keyword_parameters'),
            test.test_re.ReTests('test_locale_caching'), # fails on .NET Core linux/macos
            test.test_re.ReTests('test_lookbehind'),
            test.test_re.ReTests('test_match_getitem'), # TypeError: expected dict, got Match
            test.test_re.ReTests('test_misc_errors'), # AttributeError: 'error' object has no attribute 'msg'
            test.test_re.ReTests('test_multiple_repeat'), # AttributeError: 'error' object has no attribute 'msg'
            test.test_re.ReTests('test_nothing_to_repeat'), # AttributeError: 'error' object has no attribute 'msg'
            test.test_re.ReTests('test_other_escapes'), # AssertionError: error not raised by compile
            test.test_re.ReTests('test_pickling'),
            test.test_re.ReTests('test_qualified_re_split'), # AssertionError: FutureWarning not triggered
            test.test_re.ReTests('test_re_escape'),
            test.test_re.ReTests('test_re_escape_byte'),
            test.test_re.ReTests('test_re_escape_non_ascii_bytes'),
            test.test_re.ReTests('test_re_groupref'), # AssertionError: error not raised
            test.test_re.ReTests('test_re_groupref_exists'), # AssertionError: error not raised
            test.test_re.ReTests('test_re_groupref_overflow'), # IndexError: unknown group reference
            test.test_re.ReTests('test_re_split'), # AssertionError: FutureWarning not triggered
            test.test_re.ReTests('test_repeat_minmax'), # AttributeError: 'error' object has no attribute 'msg'
            test.test_re.ReTests('test_repeat_minmax_overflow'),
            test.test_re.ReTests('test_scoped_flags'), # https://github.com/IronLanguages/ironpython3/issues/1419
            test.test_re.ReTests('test_sre_byte_class_literals'),
            test.test_re.ReTests('test_sre_byte_literals'),
            test.test_re.ReTests('test_sre_character_class_literals'),
            test.test_re.ReTests('test_sre_character_literals'),
            test.test_re.ReTests('test_string_boundaries'),
            test.test_re.ReTests('test_sub_template_numeric_escape'),
            test.test_re.ReTests('test_symbolic_groups'),
            test.test_re.ReTests('test_symbolic_refs'),
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
