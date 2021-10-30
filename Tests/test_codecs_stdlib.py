# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_codecs from StdLib
##

import unittest
import codecs
import sys

from iptest import run_test, is_mono, is_netcoreapp31, is_net50, is_net60

import test.test_codecs

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(unittest.expectedFailure(test.test_codecs.BasicUnicodeTest('test_bad_decode_args'))) # unknown encoding: big5
        suite.addTest(unittest.expectedFailure(test.test_codecs.BasicUnicodeTest('test_bad_encode_args'))) # unknown encoding: big5
        suite.addTest(unittest.expectedFailure(test.test_codecs.BasicUnicodeTest('test_basics'))) # unknown encoding: big5
        suite.addTest(test.test_codecs.BasicUnicodeTest('test_basics_capi'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.BasicUnicodeTest('test_decoder_state'))) # unknown encoding: big5
        suite.addTest(test.test_codecs.BasicUnicodeTest('test_encoding_map_type_initialized'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.BasicUnicodeTest('test_seek'))) # unknown encoding: big5
        suite.addTest(test.test_codecs.BomTest('test_seek0'))
        suite.addTest(test.test_codecs.CP65001Test('test_bug1098990_a'))
        suite.addTest(test.test_codecs.CP65001Test('test_bug1098990_b'))
        suite.addTest(test.test_codecs.CP65001Test('test_bug1175396'))
        if is_netcoreapp31 or is_net50 or is_net60:
            suite.addTest(test.test_codecs.CP65001Test('test_decode'))
        else:
            suite.addTest(unittest.expectedFailure(test.test_codecs.CP65001Test('test_decode'))) # '[��]' != '[���]' (bug in .NET: dotnet/corefx#36163, fixed in .NET Core 3.x)
        suite.addTest(test.test_codecs.CP65001Test('test_encode'))
        suite.addTest(test.test_codecs.CP65001Test('test_lone_surrogates'))
        suite.addTest(test.test_codecs.CP65001Test('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.CP65001Test('test_readline'))
        suite.addTest(test.test_codecs.CP65001Test('test_readlinequeue'))
        suite.addTest(test.test_codecs.CP65001Test('test_surrogatepass_handler'))
        suite.addTest(test.test_codecs.CharmapTest('test_decode_with_int2int_map'))
        suite.addTest(test.test_codecs.CharmapTest('test_decode_with_int2str_map'))
        suite.addTest(test.test_codecs.CharmapTest('test_decode_with_string_map'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_code_page_name'))) # "CP_UTF8" does not match "'cp65001'""
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_cp1252'))) # b'?' != b'L' for "Ł" with 'replace'
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_cp932'))) # b'[?]' != b'[y]' for "ÿ" with 'replace'
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_cp_utf7'))) # Unable to decode b'[+/]' from "cp65000"
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_incremental'))) # incremental codepage decoding not implemented yet
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_invalid_code_page'))) # SystemError raised iso OSError
        suite.addTest(unittest.expectedFailure(test.test_codecs.CodePageTest('test_multibyte_encoding'))) # .NET cp932 does not resynchronize cursor after '\x84' in '\x84\xe9\x80'
        suite.addTest(test.test_codecs.CodecsModuleTest('test_all'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_decode'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_encode'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_getdecoder'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_getencoder'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_getreader'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_getwriter'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_lookup'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_lookup_issue1813'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_open'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_register'))
        suite.addTest(test.test_codecs.CodecsModuleTest('test_undefined'))
        suite.addTest(test.test_codecs.EncodedFileTest('test_basic'))
        suite.addTest(test.test_codecs.EscapeDecodeTest('test_empty'))
        suite.addTest(test.test_codecs.EscapeDecodeTest('test_errors'))
        suite.addTest(test.test_codecs.EscapeDecodeTest('test_escape'))
        suite.addTest(test.test_codecs.EscapeDecodeTest('test_raw'))
        suite.addTest(test.test_codecs.ExceptionChainingTest('test_codec_lookup_failure_not_wrapped'))
        suite.addTest(test.test_codecs.ExceptionChainingTest('test_init_override_is_not_wrapped'))
        suite.addTest(test.test_codecs.ExceptionChainingTest('test_instance_attribute_is_not_wrapped'))
        suite.addTest(test.test_codecs.ExceptionChainingTest('test_multiple_args_is_not_wrapped'))
        suite.addTest(test.test_codecs.ExceptionChainingTest('test_new_override_is_not_wrapped'))
        suite.addTest(test.test_codecs.ExceptionChainingTest('test_non_str_arg_is_not_wrapped'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.ExceptionChainingTest('test_raise_by_type'))) # wrong exception
        suite.addTest(unittest.expectedFailure(test.test_codecs.ExceptionChainingTest('test_raise_by_value'))) # wrong exception
        suite.addTest(unittest.expectedFailure(test.test_codecs.ExceptionChainingTest('test_raise_grandchild_subclass_exact_size'))) # wrong exception
        suite.addTest(unittest.expectedFailure(test.test_codecs.ExceptionChainingTest('test_raise_subclass_with_weakref_support'))) # # wrong exception
        suite.addTest(unittest.expectedFailure(test.test_codecs.ExceptionChainingTest('test_unflagged_non_text_codec_handling'))) # wrong exception
        suite.addTest(test.test_codecs.IDNACodecTest('test_builtin_decode'))
        suite.addTest(test.test_codecs.IDNACodecTest('test_builtin_encode'))
        suite.addTest(test.test_codecs.IDNACodecTest('test_errors'))
        suite.addTest(test.test_codecs.IDNACodecTest('test_incremental_decode'))
        suite.addTest(test.test_codecs.IDNACodecTest('test_incremental_encode'))
        suite.addTest(test.test_codecs.IDNACodecTest('test_stream'))
        if is_mono:
            suite.addTest(test.test_codecs.NameprepTest('test_nameprep'))
        else:
            suite.addTest(unittest.expectedFailure(test.test_codecs.NameprepTest('test_nameprep'))) # Invalid Unicode code point found at index 0
        suite.addTest(test.test_codecs.PunycodeTest('test_decode'))
        suite.addTest(test.test_codecs.PunycodeTest('test_encode'))
        suite.addTest(test.test_codecs.RawUnicodeEscapeTest('test_decode_errors'))
        suite.addTest(test.test_codecs.RawUnicodeEscapeTest('test_empty'))
        suite.addTest(test.test_codecs.RawUnicodeEscapeTest('test_escape_encode'))
        suite.addTest(test.test_codecs.RawUnicodeEscapeTest('test_raw_decode'))
        suite.addTest(test.test_codecs.RawUnicodeEscapeTest('test_raw_encode'))
        suite.addTest(test.test_codecs.ReadBufferTest('test_array'))
        suite.addTest(test.test_codecs.ReadBufferTest('test_bad_args'))
        suite.addTest(test.test_codecs.ReadBufferTest('test_empty'))
        suite.addTest(test.test_codecs.RecodingTest('test_recoding'))
        suite.addTest(test.test_codecs.StreamReaderTest('test_readlines'))
        suite.addTest(test.test_codecs.SurrogateEscapeTest('test_ascii'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.SurrogateEscapeTest('test_charmap'))) # .NET iso-8859-3 decodes b'\xa5' to 'uf7f5' rather than undefined
        suite.addTest(test.test_codecs.SurrogateEscapeTest('test_latin1'))
        suite.addTest(test.test_codecs.SurrogateEscapeTest('test_utf8'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_aliases'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_basics'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_binary_to_text_blacklists_binary_transforms'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_binary_to_text_blacklists_text_transforms'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_buffer_api_usage'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.TransformCodecTest('test_custom_hex_error_is_wrapped'))) # "^decoding with 'hex_codec' codec failed" does not match "Odd-length string"
        suite.addTest(unittest.expectedFailure(test.test_codecs.TransformCodecTest('test_custom_zlib_error_is_wrapped'))) # "^decoding with 'zlib_codec' codec failed" does not match "Error -3 while decompressing data: incorrect header check"
        suite.addTest(test.test_codecs.TransformCodecTest('test_quopri_stateless'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.TransformCodecTest('test_read'))) # TypeError: expected str, got bytes
        suite.addTest(unittest.expectedFailure(test.test_codecs.TransformCodecTest('test_readline'))) # Exception: BZ_DATA_ERROR
        suite.addTest(test.test_codecs.TransformCodecTest('test_text_to_binary_blacklists_binary_transforms'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_text_to_binary_blacklists_text_transforms'))
        suite.addTest(test.test_codecs.TransformCodecTest('test_uu_invalid'))
        suite.addTest(test.test_codecs.TypesTest('test_decode_unicode'))
        suite.addTest(test.test_codecs.TypesTest('test_unicode_escape'))
        suite.addTest(test.test_codecs.UTF16BETest('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF16BETest('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF16BETest('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF16BETest('test_errors'))
        suite.addTest(test.test_codecs.UTF16BETest('test_lone_surrogates'))
        suite.addTest(test.test_codecs.UTF16BETest('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF16BETest('test_nonbmp'))
        suite.addTest(test.test_codecs.UTF16BETest('test_partial'))
        suite.addTest(test.test_codecs.UTF16BETest('test_readline'))
        suite.addTest(test.test_codecs.UTF16BETest('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF16ExTest('test_bad_args'))
        suite.addTest(test.test_codecs.UTF16ExTest('test_errors'))
        suite.addTest(test.test_codecs.UTF16LETest('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF16LETest('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF16LETest('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF16LETest('test_errors'))
        suite.addTest(test.test_codecs.UTF16LETest('test_lone_surrogates'))
        suite.addTest(test.test_codecs.UTF16LETest('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF16LETest('test_nonbmp'))
        suite.addTest(test.test_codecs.UTF16LETest('test_partial'))
        suite.addTest(test.test_codecs.UTF16LETest('test_readline'))
        suite.addTest(test.test_codecs.UTF16LETest('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF16Test('test_badbom'))
        suite.addTest(test.test_codecs.UTF16Test('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF16Test('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF16Test('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF16Test('test_bug691291'))
        suite.addTest(test.test_codecs.UTF16Test('test_decoder_state'))
        suite.addTest(test.test_codecs.UTF16Test('test_errors'))
        suite.addTest(test.test_codecs.UTF16Test('test_handlers'))
        suite.addTest(test.test_codecs.UTF16Test('test_lone_surrogates'))
        suite.addTest(test.test_codecs.UTF16Test('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF16Test('test_only_one_bom'))
        suite.addTest(test.test_codecs.UTF16Test('test_partial'))
        suite.addTest(test.test_codecs.UTF16Test('test_readline'))
        suite.addTest(test.test_codecs.UTF16Test('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF32BETest('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF32BETest('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF32BETest('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF32BETest('test_errors'))
        suite.addTest(test.test_codecs.UTF32BETest('test_issue8941'))
        suite.addTest(test.test_codecs.UTF32BETest('test_lone_surrogates'))
        suite.addTest(test.test_codecs.UTF32BETest('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF32BETest('test_partial'))
        suite.addTest(test.test_codecs.UTF32BETest('test_readline'))
        suite.addTest(test.test_codecs.UTF32BETest('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF32BETest('test_simple'))
        suite.addTest(test.test_codecs.UTF32LETest('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF32LETest('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF32LETest('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF32LETest('test_errors'))
        suite.addTest(test.test_codecs.UTF32LETest('test_issue8941'))
        suite.addTest(test.test_codecs.UTF32LETest('test_lone_surrogates'))
        suite.addTest(test.test_codecs.UTF32LETest('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF32LETest('test_partial'))
        suite.addTest(test.test_codecs.UTF32LETest('test_readline'))
        suite.addTest(test.test_codecs.UTF32LETest('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF32LETest('test_simple'))
        suite.addTest(test.test_codecs.UTF32Test('test_badbom'))
        suite.addTest(test.test_codecs.UTF32Test('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF32Test('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF32Test('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF32Test('test_decoder_state'))
        suite.addTest(test.test_codecs.UTF32Test('test_errors'))
        suite.addTest(test.test_codecs.UTF32Test('test_handlers'))
        suite.addTest(test.test_codecs.UTF32Test('test_issue8941'))
        suite.addTest(test.test_codecs.UTF32Test('test_lone_surrogates'))
        suite.addTest(test.test_codecs.UTF32Test('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF32Test('test_only_one_bom'))
        suite.addTest(test.test_codecs.UTF32Test('test_partial'))
        suite.addTest(test.test_codecs.UTF32Test('test_readline'))
        suite.addTest(test.test_codecs.UTF32Test('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF7Test('test_ascii'))
        suite.addTest(test.test_codecs.UTF7Test('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF7Test('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF7Test('test_bug1175396'))
        suite.addTest(unittest.expectedFailure(test.test_codecs.UTF7Test('test_errors'))) # AssertionError: UnicodeDecodeError not raised by utf_7_decode
        suite.addTest(unittest.expectedFailure(test.test_codecs.UTF7Test('test_lone_surrogates'))) # UnicodeEncodeError: 'utf_8' codec can't encode character '\ud801' in position 503: Unable to translate Unicode character \uD801 at index 503 to specified code page.
        suite.addTest(test.test_codecs.UTF7Test('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF7Test('test_nonbmp'))
        suite.addTest(test.test_codecs.UTF7Test('test_partial'))
        suite.addTest(test.test_codecs.UTF7Test('test_readline'))
        suite.addTest(test.test_codecs.UTF7Test('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_bom'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_bug1601501'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_decoder_state'))
        if is_netcoreapp31 or is_net50 or is_net60:
            suite.addTest(test.test_codecs.UTF8SigTest('test_lone_surrogates'))
        else:
            suite.addTest(unittest.expectedFailure(test.test_codecs.UTF8SigTest('test_lone_surrogates'))) # AssertionError: '\ud803\udfff��A' != '\ud803\udfff���A'
        suite.addTest(test.test_codecs.UTF8SigTest('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_partial'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_readline'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_stream_bare'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_stream_bom'))
        suite.addTest(test.test_codecs.UTF8SigTest('test_surrogatepass_handler'))
        suite.addTest(test.test_codecs.UTF8Test('test_bug1098990_a'))
        suite.addTest(test.test_codecs.UTF8Test('test_bug1098990_b'))
        suite.addTest(test.test_codecs.UTF8Test('test_bug1175396'))
        suite.addTest(test.test_codecs.UTF8Test('test_decoder_state'))
        if is_netcoreapp31 or is_net50 or is_net60:
            suite.addTest(test.test_codecs.UTF8Test('test_lone_surrogates'))
        else:
            suite.addTest(unittest.expectedFailure(test.test_codecs.UTF8Test('test_lone_surrogates'))) # AssertionError: '\ud803\udfff��A' != '\ud803\udfff���A'
        suite.addTest(test.test_codecs.UTF8Test('test_mixed_readline_and_read'))
        suite.addTest(test.test_codecs.UTF8Test('test_partial'))
        suite.addTest(test.test_codecs.UTF8Test('test_readline'))
        suite.addTest(test.test_codecs.UTF8Test('test_readlinequeue'))
        suite.addTest(test.test_codecs.UTF8Test('test_surrogatepass_handler'))
        suite.addTest(test.test_codecs.UnicodeEscapeTest('test_decode_errors'))
        suite.addTest(test.test_codecs.UnicodeEscapeTest('test_empty'))
        suite.addTest(test.test_codecs.UnicodeEscapeTest('test_escape_decode'))
        suite.addTest(test.test_codecs.UnicodeEscapeTest('test_escape_encode'))
        suite.addTest(test.test_codecs.UnicodeEscapeTest('test_raw_decode'))
        suite.addTest(test.test_codecs.UnicodeEscapeTest('test_raw_encode'))
        suite.addTest(test.test_codecs.UnicodeInternalTest('test_bug1251300'))
        suite.addTest(test.test_codecs.UnicodeInternalTest('test_decode_callback'))
        suite.addTest(test.test_codecs.UnicodeInternalTest('test_decode_error_attributes'))
        suite.addTest(test.test_codecs.UnicodeInternalTest('test_encode_length'))
        suite.addTest(test.test_codecs.WithStmtTest('test_encodedfile'))
        suite.addTest(test.test_codecs.WithStmtTest('test_streamreaderwriter'))
        return suite
        
    else:
        return loader.loadTestsFromModule(test.test_codecs, pattern)

run_test(__name__)
