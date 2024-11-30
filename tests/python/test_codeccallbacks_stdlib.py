# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_codeccallbacks from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono

import test.test_codeccallbacks

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_codeccallbacks)

    if is_ironpython:
        failing_tests = [
            test.test_codeccallbacks.CodecCallbackTest('test_backslashescape'), # UTF-16 vs. UTF-32
            test.test_codeccallbacks.CodecCallbackTest('test_badandgoodbackslashreplaceexceptions'), # UTF-16 vs. UTF-32
            test.test_codeccallbacks.CodecCallbackTest('test_badandgoodreplaceexceptions'), # UTF-16 vs. UTF-32
            test.test_codeccallbacks.CodecCallbackTest('test_badandgoodxmlcharrefreplaceexceptions'), # UTF-16 vs. UTF-32
            test.test_codeccallbacks.CodecCallbackTest('test_badhandlerresults'), # TypeError not raised by decode
            test.test_codeccallbacks.CodecCallbackTest('test_callbacks'), # Moving cursor not implemented
            test.test_codeccallbacks.CodecCallbackTest('test_decodehelper'), # Moving cursor not implemented
            test.test_codeccallbacks.CodecCallbackTest('test_decoding_callbacks'), # Moving cursor not implemented
            test.test_codeccallbacks.CodecCallbackTest('test_encodehelper'), # Moving cursor not implemented
            test.test_codeccallbacks.CodecCallbackTest('test_mutatingdecodehandler'), # Moving cursor not implemented
            test.test_codeccallbacks.CodecCallbackTest('test_unencodablereplacement'), # UnicodeEncodeError not raised by encode
            test.test_codeccallbacks.CodecCallbackTest('test_unicodedecodeerror'), # TypeError not raised by UnicodeDecodeError
            test.test_codeccallbacks.CodecCallbackTest('test_unicodeencodeerror'), # TypeError not raised by UnicodeEncodeError
            test.test_codeccallbacks.CodecCallbackTest('test_unicodetranslateerror'),  # UTF-16 vs. UTF-32
            test.test_codeccallbacks.CodecCallbackTest('test_uninamereplace'), # bug?
        ]
        if is_mono:
            failing_tests += [
                test.test_codeccallbacks.CodecCallbackTest('test_longstrings'), # https://github.com/mono/mono/issues/20445
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
