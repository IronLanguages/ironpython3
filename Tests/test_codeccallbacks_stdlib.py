# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_codeccallbacks from StdLib
##

import unittest
import sys

from iptest import run_test, is_mono

import test.test_codeccallbacks

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_backslashescape'))) # UTF-16 vs. UTF-32
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodbackslashreplaceexceptions'))) # UTF-16 vs. UTF-32
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodignoreexceptions'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodnamereplaceexceptions'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodreplaceexceptions'))) # UTF-16 vs. UTF-32
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodstrictexceptions'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodsurrogateescapeexceptions'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodsurrogatepassexceptions'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_badandgoodxmlcharrefreplaceexceptions'))) # UTF-16 vs. UTF-32
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_badhandlerresults'))) # TypeError not raised by decode
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badlookupcall'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_badregistercall'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_bug828737'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_callbacks'))) # Moving cursor not implemented
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_charmapencode'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_crashing_decode_handler'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_decodehelper'))) # Moving cursor not implemented
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_decodeunicodeinternal'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_decoding_callbacks'))) # Moving cursor not implemented
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_encodehelper'))) # Moving cursor not implemented
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_fake_error_class'))
        if is_mono: # https://github.com/mono/mono/issues/20445
            suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_longstrings')))
        else:
            suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_longstrings'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_lookup'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_mutatingdecodehandler'))) # Moving cursor not implemented
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_nameescape'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_translatehelper'))
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_unencodablereplacement'))) # UnicodeEncodeError not raised by encode
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_unicodedecodeerror'))) # TypeError not raised by UnicodeDecodeError
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_unicodeencodeerror'))) # TypeError not raised by UnicodeEncodeError
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_unicodetranslateerror'))) # UTF-16 vs. UTF-32
        suite.addTest(unittest.expectedFailure(test.test_codeccallbacks.CodecCallbackTest('test_uninamereplace'))) # bug?
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_unknownhandler'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_xmlcharnamereplace'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_xmlcharrefreplace'))
        suite.addTest(test.test_codeccallbacks.CodecCallbackTest('test_xmlcharrefvalues'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_codeccallbacks, pattern)

run_test(__name__)
