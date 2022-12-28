# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_sax from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_sax

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_sax)

    if is_ironpython:
        failing_tests = [
            test.test_sax.BytesXmlgenTest('test_xmlgen_encoding'), # AssertionError
            test.test_sax.BytesXmlgenTest('test_xmlgen_encoding_bytes'), # AssertionError
            test.test_sax.ErrorReportingTest('test_expat_incomplete'), # AttributeError: 'xmlparser' object has no attribute 'ErrorColumnNumber'
            test.test_sax.ErrorReportingTest('test_expat_inpsource_location'), # AttributeError: 'xmlparser' object has no attribute 'ErrorColumnNumber'
            test.test_sax.ExpatReaderTest('test_expat_dtdhandler'), # AssertionError
            test.test_sax.ExpatReaderTest('test_expat_entityresolver'), # AssertionError
            test.test_sax.ExpatReaderTest('test_expat_text_file'), # AssertionError
            test.test_sax.ParseTest('test_parseString_bytes'), # UnicodeEncodeError
            test.test_sax.ParseTest('test_parse_InputSource'), # AttributeError: 'xmlparser' object has no attribute 'ErrorColumnNumber'
            test.test_sax.ParseTest('test_parse_bytes'), # UnicodeEncodeError
            test.test_sax.ParseTest('test_parse_text'), # UnicodeEncodeError
            test.test_sax.WriterXmlgenTest('test_1463026_1'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_1463026_1_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_1463026_2'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_1463026_2_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_1463026_3'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_1463026_3_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_5027_1'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_5027_2'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_no_close_file'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_attr_escape'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_basic'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_basic_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_content'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_content_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_content_escape'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_encoding'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_encoding_bytes'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_fragment'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_ignorable'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_ignorable_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_ns'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_ns_empty'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_pi'), # TypeError: expected long, got NoneType
            test.test_sax.WriterXmlgenTest('test_xmlgen_unencodable'), # TypeError: expected long, got NoneType
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
