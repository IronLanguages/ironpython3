# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_sax from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_sax

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_sax.BytesXmlgenTest('test_1463026_1'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_1463026_1_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_1463026_2'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_1463026_2_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_1463026_3'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_1463026_3_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_5027_1'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_5027_2'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_no_close_file'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_attr_escape'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_basic'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_basic_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_content'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_content_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_content_escape'))
        suite.addTest(unittest.expectedFailure(test.test_sax.BytesXmlgenTest('test_xmlgen_encoding'))) # AssertionError
        suite.addTest(unittest.expectedFailure(test.test_sax.BytesXmlgenTest('test_xmlgen_encoding_bytes'))) # AssertionError
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_fragment'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_ignorable'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_ignorable_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_ns'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_ns_empty'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_pi'))
        suite.addTest(test.test_sax.BytesXmlgenTest('test_xmlgen_unencodable'))
        suite.addTest(unittest.expectedFailure(test.test_sax.ErrorReportingTest('test_expat_incomplete'))) # AttributeError: 'xmlparser' object has no attribute 'ErrorColumnNumber'
        suite.addTest(unittest.expectedFailure(test.test_sax.ErrorReportingTest('test_expat_inpsource_location'))) # AttributeError: 'xmlparser' object has no attribute 'ErrorColumnNumber'
        suite.addTest(test.test_sax.ErrorReportingTest('test_sax_parse_exception_str'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_attrs_empty'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_attrs_wattr'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_binary_file'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_binary_file_bytes_name'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_binary_file_int_name'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_binary_file_nonascii'))
        suite.addTest(unittest.expectedFailure(test.test_sax.ExpatReaderTest('test_expat_dtdhandler'))) # AssertionError
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_entityresolver_default'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_entityresolver_enabled'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_external_dtd_default'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_external_dtd_enabled'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_incremental'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_incremental_reset'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_inpsource_byte_stream'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_inpsource_character_stream'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_inpsource_filename'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_inpsource_sysid'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_inpsource_sysid_nonascii'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_locator_noinfo'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_locator_withinfo'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_locator_withinfo_nonascii'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_nsattrs_empty'))
        suite.addTest(test.test_sax.ExpatReaderTest('test_expat_nsattrs_wattr'))
        suite.addTest(unittest.expectedFailure(test.test_sax.ExpatReaderTest('test_expat_text_file'))) # AssertionError
        suite.addTest(test.test_sax.MakeParserTest('test_make_parser2'))
        suite.addTest(unittest.expectedFailure(test.test_sax.ParseTest('test_parseString_bytes'))) # UnicodeEncodeError
        suite.addTest(test.test_sax.ParseTest('test_parseString_text'))
        suite.addTest(unittest.expectedFailure(test.test_sax.ParseTest('test_parse_InputSource'))) # AttributeError: 'xmlparser' object has no attribute 'ErrorColumnNumber'
        suite.addTest(unittest.expectedFailure(test.test_sax.ParseTest('test_parse_bytes'))) # UnicodeEncodeError
        suite.addTest(test.test_sax.ParseTest('test_parse_close_source'))
        suite.addTest(unittest.expectedFailure(test.test_sax.ParseTest('test_parse_text'))) # UnicodeEncodeError
        suite.addTest(test.test_sax.PrepareInputSourceTest('test_binary_file'))
        suite.addTest(test.test_sax.PrepareInputSourceTest('test_byte_stream'))
        suite.addTest(test.test_sax.PrepareInputSourceTest('test_character_stream'))
        suite.addTest(test.test_sax.PrepareInputSourceTest('test_string'))
        suite.addTest(test.test_sax.PrepareInputSourceTest('test_system_id'))
        suite.addTest(test.test_sax.PrepareInputSourceTest('test_text_file'))
        suite.addTest(test.test_sax.SaxutilsTest('test_double_quoteattr'))
        suite.addTest(test.test_sax.SaxutilsTest('test_escape_all'))
        suite.addTest(test.test_sax.SaxutilsTest('test_escape_basic'))
        suite.addTest(test.test_sax.SaxutilsTest('test_escape_extra'))
        suite.addTest(test.test_sax.SaxutilsTest('test_make_parser'))
        suite.addTest(test.test_sax.SaxutilsTest('test_quoteattr_basic'))
        suite.addTest(test.test_sax.SaxutilsTest('test_single_double_quoteattr'))
        suite.addTest(test.test_sax.SaxutilsTest('test_single_quoteattr'))
        suite.addTest(test.test_sax.SaxutilsTest('test_unescape_all'))
        suite.addTest(test.test_sax.SaxutilsTest('test_unescape_amp_extra'))
        suite.addTest(test.test_sax.SaxutilsTest('test_unescape_basic'))
        suite.addTest(test.test_sax.SaxutilsTest('test_unescape_extra'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_1463026_1'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_1463026_1_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_1463026_2'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_1463026_2_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_1463026_3'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_1463026_3_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_5027_1'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_5027_2'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_no_close_file'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_attr_escape'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_basic'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_basic_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_content'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_content_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_content_escape'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_encoding'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_encoding_bytes'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_fragment'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_ignorable'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_ignorable_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_ns'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_ns_empty'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_pi'))
        suite.addTest(test.test_sax.StreamReaderWriterXmlgenTest('test_xmlgen_unencodable'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_1463026_1'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_1463026_1_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_1463026_2'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_1463026_2_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_1463026_3'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_1463026_3_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_5027_1'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_5027_2'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_no_close_file'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_attr_escape'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_basic'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_basic_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_content'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_content_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_content_escape'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_encoding'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_encoding_bytes'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_fragment'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_ignorable'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_ignorable_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_ns'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_ns_empty'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_pi'))
        suite.addTest(test.test_sax.StreamWriterXmlgenTest('test_xmlgen_unencodable'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_1463026_1'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_1463026_1_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_1463026_2'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_1463026_2_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_1463026_3'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_1463026_3_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_5027_1'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_5027_2'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_no_close_file'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_attr_escape'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_basic'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_basic_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_content'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_content_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_content_escape'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_encoding'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_encoding_bytes'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_fragment'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_ignorable'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_ignorable_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_ns'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_ns_empty'))
        suite.addTest(test.test_sax.StringXmlgenTest('test_xmlgen_pi'))
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_1463026_1'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_1463026_1_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_1463026_2'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_1463026_2_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_1463026_3'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_1463026_3_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_5027_1'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_5027_2'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_no_close_file'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_attr_escape'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_basic'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_basic_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_content'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_content_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_content_escape'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_encoding'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_encoding_bytes'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_fragment'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_ignorable'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_ignorable_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_ns'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_ns_empty'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_pi'))) # TypeError: expected long, got NoneType
        suite.addTest(unittest.expectedFailure(test.test_sax.WriterXmlgenTest('test_xmlgen_unencodable'))) # TypeError: expected long, got NoneType
        suite.addTest(test.test_sax.XMLFilterBaseTest('test_filter_basic'))
        suite.addTest(test.test_sax.XmlReaderTest('test_attrs_empty'))
        suite.addTest(test.test_sax.XmlReaderTest('test_attrs_wattr'))
        suite.addTest(test.test_sax.XmlReaderTest('test_nsattrs_empty'))
        suite.addTest(test.test_sax.XmlReaderTest('test_nsattrs_wattr'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_sax, pattern)

run_test(__name__)
