# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_plistlib from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_plistlib

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_plistlib.MiscTestCase('test__all__'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_cycles'))
        #suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_deep_nesting')) # StackOverflowException
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_dump_duplicates'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_identity'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_invalid_binary'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_large_timestamp'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_load_int'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_load_singletons'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_nonstandard_refs_size'))
        suite.addTest(test.test_plistlib.TestBinaryPlistlib('test_unsupported'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_appleformatting'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_appleformattingfromliteral'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_bytearray'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_bytes'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_bytesio'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_controlcharacters'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_create'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_dict_members'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_indentation_array'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_indentation_dict'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_indentation_dict_mix'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_int'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_invalid_type'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_invalidarray'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_invaliddict'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_invalidinteger'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_invalidreal'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_io'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_keys_no_string'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_keysort'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_keysort_bytesio'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_list_members'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_lone_surrogates'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_non_bmp_characters'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_nondictroot'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_skipkeys'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_tuple_members'))
        suite.addTest(test.test_plistlib.TestPlistlib('test_xml_encodings'))
        suite.addTest(unittest.expectedFailure(test.test_plistlib.TestPlistlib('test_xml_plist_with_entity_decl'))) # https://github.com/IronLanguages/ironpython2/issues/464
        suite.addTest(test.test_plistlib.TestPlistlibDeprecated('test_bytes_deprecated'))
        suite.addTest(test.test_plistlib.TestPlistlibDeprecated('test_dataobject_deprecated'))
        suite.addTest(test.test_plistlib.TestPlistlibDeprecated('test_io_deprecated'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_plistlib, pattern)

run_test(__name__)
