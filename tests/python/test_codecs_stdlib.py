# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_codecs from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono, is_netcoreapp, is_netcoreapp21

import test.test_codecs

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_codecs)

    if is_ironpython:
        failing_tests = [
            test.test_codecs.BasicUnicodeTest('test_bad_decode_args'), # unknown encoding: big5
            test.test_codecs.BasicUnicodeTest('test_bad_encode_args'), # unknown encoding: big5
            test.test_codecs.BasicUnicodeTest('test_basics'), # unknown encoding: big5
            test.test_codecs.BasicUnicodeTest('test_decoder_state'), # unknown encoding: big5
            test.test_codecs.BasicUnicodeTest('test_seek'), # unknown encoding: big5
            test.test_codecs.CodePageTest('test_code_page_name'), # "CP_UTF8" does not match "'cp65001'""
            test.test_codecs.CodePageTest('test_cp1252'), # b'?' != b'L' for "Ł" with 'replace'
            test.test_codecs.CodePageTest('test_cp932'), # b'[?]' != b'[y]' for "ÿ" with 'replace'
            test.test_codecs.CodePageTest('test_cp_utf7'), # Unable to decode b'[+/]' from "cp65000"
            test.test_codecs.CodePageTest('test_incremental'), # incremental codepage decoding not implemented yet
            test.test_codecs.CodePageTest('test_invalid_code_page'), # SystemError raised iso OSError
            test.test_codecs.CodePageTest('test_multibyte_encoding'), # .NET cp932 does not resynchronize cursor after '\x84' in '\x84\xe9\x80'
            test.test_codecs.ExceptionChainingTest('test_raise_by_type'), # wrong exception
            test.test_codecs.ExceptionChainingTest('test_raise_by_value'), # wrong exception
            test.test_codecs.ExceptionChainingTest('test_raise_grandchild_subclass_exact_size'), # wrong exception
            test.test_codecs.ExceptionChainingTest('test_raise_subclass_with_weakref_support'), # # wrong exception
            test.test_codecs.ExceptionChainingTest('test_unflagged_non_text_codec_handling'), # wrong exception
            test.test_codecs.SurrogateEscapeTest('test_charmap'), # .NET iso-8859-3 decodes b'\xa5' to 'uf7f5' rather than undefined
            test.test_codecs.TransformCodecTest('test_custom_hex_error_is_wrapped'), # "^decoding with 'hex_codec' codec failed" does not match "Odd-length string"
            test.test_codecs.TransformCodecTest('test_custom_zlib_error_is_wrapped'), # "^decoding with 'zlib_codec' codec failed" does not match "Error -3 while decompressing data: incorrect header check"
            test.test_codecs.TransformCodecTest('test_read'), # TypeError: expected str, got bytes
            test.test_codecs.TransformCodecTest('test_readline'), # Exception: BZ_DATA_ERROR
            test.test_codecs.UTF7Test('test_errors'), # AssertionError: UnicodeDecodeError not raised by utf_7_decode
            test.test_codecs.UTF7Test('test_lone_surrogates'), # UnicodeEncodeError: 'utf_8' codec can't encode character '\ud801' in position 503: Unable to translate Unicode character \uD801 at index 503 to specified code page.
        ]
        if not is_netcoreapp or is_netcoreapp21:
            failing_tests += [
                test.test_codecs.CP65001Test('test_decode'), # '[��]' != '[���]' (bug in .NET: dotnet/corefx#36163, fixed in .NET Core 3.x)
                test.test_codecs.UTF8SigTest('test_lone_surrogates'), # AssertionError: '\ud803\udfff��A' != '\ud803\udfff���A'
                test.test_codecs.UTF8Test('test_lone_surrogates'), # AssertionError: '\ud803\udfff��A' != '\ud803\udfff���A'
            ]
        if not is_mono:
            failing_tests += [
                test.test_codecs.NameprepTest('test_nameprep'), # Invalid Unicode code point found at index 0
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
