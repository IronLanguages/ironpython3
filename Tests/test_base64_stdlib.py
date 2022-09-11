# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_base64 from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_base64

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_base64.BaseXYTestCase('test_ErrorHeritage'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_a85_padding'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_a85decode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_a85decode_errors'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_a85encode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b16decode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b16encode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b32decode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b32decode_casefold'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b32decode_error'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b32encode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b64decode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b64decode_invalid_chars'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b64decode_padding_error'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b64encode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b85_padding'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b85decode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b85decode_errors'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_b85encode'))
        suite.addTest(test.test_base64.BaseXYTestCase('test_decode_nonascii_str'))
        suite.addTest(test.test_base64.LegacyBase64TestCase('test_decode'))
        suite.addTest(test.test_base64.LegacyBase64TestCase('test_decodebytes'))
        suite.addTest(test.test_base64.LegacyBase64TestCase('test_decodestring_warns'))
        suite.addTest(test.test_base64.LegacyBase64TestCase('test_encode'))
        suite.addTest(test.test_base64.LegacyBase64TestCase('test_encodebytes'))
        suite.addTest(test.test_base64.LegacyBase64TestCase('test_encodestring_warns'))
        suite.addTest(test.test_base64.TestMain('test_decode'))
        suite.addTest(test.test_base64.TestMain('test_encode_decode'))
        suite.addTest(test.test_base64.TestMain('test_encode_file'))
        suite.addTest(unittest.expectedFailure(test.test_base64.TestMain('test_encode_from_stdin'))) # https://github.com/IronLanguages/ironpython3/issues/1135
        return suite

    else:
        return loader.loadTestsFromModule(test.test_base64, pattern)

run_test(__name__)
