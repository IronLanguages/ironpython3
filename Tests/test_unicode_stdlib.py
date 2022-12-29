# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_unicode from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_unicode

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_unicode, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_unicode.UnicodeTest('test_capitalize'),
            test.test_unicode.UnicodeTest('test_case_operation_overflow'),
            test.test_unicode.UnicodeTest('test_casefold'),
            test.test_unicode.UnicodeTest('test_center'),
            test.test_unicode.UnicodeTest('test_codecs_errors'),
            test.test_unicode.UnicodeTest('test_codecs_utf7'),
            test.test_unicode.UnicodeTest('test_compare'),
            test.test_unicode.UnicodeTest('test_constructor_defaults'),
            test.test_unicode.UnicodeTest('test_expandtabs_optimization'),
            test.test_unicode.UnicodeTest('test_expandtabs_overflows_gracefully'),
            test.test_unicode.UnicodeTest('test_format'),
            test.test_unicode.UnicodeTest('test_format_huge_item_number'),
            test.test_unicode.UnicodeTest('test_format_map'),
            test.test_unicode.UnicodeTest('test_format_subclass'),
            test.test_unicode.UnicodeTest('test_formatting'),
            test.test_unicode.UnicodeTest('test_formatting_c_limits'),
            test.test_unicode.UnicodeTest('test_formatting_huge_precision_c_limits'),
            test.test_unicode.UnicodeTest('test_getnewargs'),
            test.test_unicode.UnicodeTest('test_invalid_cb_for_2bytes_seq'),
            test.test_unicode.UnicodeTest('test_invalid_cb_for_3bytes_seq'),
            test.test_unicode.UnicodeTest('test_invalid_cb_for_4bytes_seq'),
            test.test_unicode.UnicodeTest('test_invalid_start_byte'),
            test.test_unicode.UnicodeTest('test_isalnum'),
            test.test_unicode.UnicodeTest('test_isalpha'),
            test.test_unicode.UnicodeTest('test_isdecimal'),
            test.test_unicode.UnicodeTest('test_isdigit'),
            test.test_unicode.UnicodeTest('test_isidentifier'),
            test.test_unicode.UnicodeTest('test_islower'),
            test.test_unicode.UnicodeTest('test_isnumeric'),
            test.test_unicode.UnicodeTest('test_isprintable'),
            test.test_unicode.UnicodeTest('test_issue18183'),
            test.test_unicode.UnicodeTest('test_issue28598_strsubclass_rhs'),
            test.test_unicode.UnicodeTest('test_istitle'),
            test.test_unicode.UnicodeTest('test_isupper'),
            test.test_unicode.UnicodeTest('test_join'),
            test.test_unicode.UnicodeTest('test_join_overflow'), # ValueError: capacity was less than the current size.
            test.test_unicode.UnicodeTest('test_lower'),
            test.test_unicode.UnicodeTest('test_mul'),
            test.test_unicode.UnicodeTest('test_partition'),
            test.test_unicode.UnicodeTest('test_printable_repr'),
            test.test_unicode.UnicodeTest('test_raiseMemError'),
            test.test_unicode.UnicodeTest('test_replace'),
            test.test_unicode.UnicodeTest('test_replace_id'),
            test.test_unicode.UnicodeTest('test_replace_overflow'),
            test.test_unicode.UnicodeTest('test_rpartition'),
            test.test_unicode.UnicodeTest('test_rsplit'),
            test.test_unicode.UnicodeTest('test_split'), # https://github.com/IronLanguages/ironpython3/issues/252
            test.test_unicode.UnicodeTest('test_swapcase'),
            test.test_unicode.UnicodeTest('test_title'),
            test.test_unicode.UnicodeTest('test_unexpected_end_of_data'),
            test.test_unicode.UnicodeTest('test_upper'),
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
