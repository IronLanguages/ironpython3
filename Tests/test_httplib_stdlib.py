# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_httplib from StdLib
##

import unittest
import sys

from iptest import is_mono, is_osx, run_test

import test.test_httplib

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_httplib.BasicTest('test_bad_status_repr'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked_extension'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked_head'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked_missing_end'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked_sync'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked_trailers'))
        suite.addTest(test.test_httplib.BasicTest('test_content_length_sync'))
        suite.addTest(test.test_httplib.BasicTest('test_early_eof'))
        suite.addTest(test.test_httplib.BasicTest('test_epipe'))
        suite.addTest(test.test_httplib.BasicTest('test_error_leak'))
        suite.addTest(test.test_httplib.BasicTest('test_host_port'))
        suite.addTest(test.test_httplib.BasicTest('test_incomplete_read'))
        suite.addTest(test.test_httplib.BasicTest('test_mixed_reads'))
        suite.addTest(test.test_httplib.BasicTest('test_negative_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_chunked_line'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_header_limit_after_100'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_header_line'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_status_line'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_readintos'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_readintos_incomplete_body'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_readintos_no_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_reads'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_reads_incomplete_body'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_reads_no_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_putrequest_override_domain_validation'))
        suite.addTest(test.test_httplib.BasicTest('test_putrequest_override_encoding'))
        suite.addTest(test.test_httplib.BasicTest('test_putrequest_override_host_validation'))
        suite.addTest(test.test_httplib.BasicTest('test_read1_bound_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_read1_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_read_head'))
        suite.addTest(test.test_httplib.BasicTest('test_readinto_chunked'))
        suite.addTest(test.test_httplib.BasicTest('test_readinto_chunked_head'))
        suite.addTest(test.test_httplib.BasicTest('test_readinto_head'))
        suite.addTest(test.test_httplib.BasicTest('test_readline_bound_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_readlines_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_response_fileno'))
        suite.addTest(test.test_httplib.BasicTest('test_response_headers'))
        suite.addTest(test.test_httplib.BasicTest('test_send'))
        suite.addTest(test.test_httplib.BasicTest('test_send_file'))
        suite.addTest(test.test_httplib.BasicTest('test_send_iter'))
        suite.addTest(test.test_httplib.BasicTest('test_send_type_error'))
        suite.addTest(test.test_httplib.BasicTest('test_send_updating_file'))
        suite.addTest(test.test_httplib.BasicTest('test_status_lines'))
        suite.addTest(test.test_httplib.BasicTest('test_too_many_headers'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_peek'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_peek_0'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_read1'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_read1_0'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_read1_bounded'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_read1_unbounded'))
        suite.addTest(test.test_httplib.ExtendedReadTest('test_readline'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_peek'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_peek_0'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_read1'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_read1_0'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_read1_bounded'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_read1_unbounded'))
        suite.addTest(test.test_httplib.ExtendedReadTestChunked('test_readline'))
        suite.addTest(test.test_httplib.HTTPResponseTest('test_getting_header'))
        suite.addTest(test.test_httplib.HTTPResponseTest('test_getting_header_defaultint'))
        suite.addTest(test.test_httplib.HTTPResponseTest('test_getting_nonexistent_header_with_iterable_default'))
        suite.addTest(test.test_httplib.HTTPResponseTest('test_getting_nonexistent_header_with_string_default'))
        suite.addTest(test.test_httplib.HTTPResponseTest('test_getting_nonexistent_header_without_default'))
        suite.addTest(test.test_httplib.HTTPSTest('test_attributes'))
        suite.addTest(test.test_httplib.HTTPSTest('test_host_port'))
        #suite.addTest(test.test_httplib.HTTPSTest('test_local_bad_hostname')) # StackOverflowException
        #suite.addTest(test.test_httplib.HTTPSTest('test_local_good_hostname')) # StackOverflowException
        #suite.addTest(test.test_httplib.HTTPSTest('test_local_unknown_cert')) # StackOverflowException
        suite.addTest(unittest.expectedFailure(test.test_httplib.HTTPSTest('test_networked'))) # AttributeError: 'SSLError' object has no attribute 'reason'
        suite.addTest(unittest.expectedFailure(test.test_httplib.HTTPSTest('test_networked_bad_cert'))) # AttributeError: 'SSLError' object has no attribute 'reason'
        if is_mono and is_osx:
            suite.addTest(unittest.expectedFailure(test.test_httplib.HTTPSTest('test_networked_good_cert'))) # https://github.com/IronLanguages/ironpython3/issues/1523
        else:
            suite.addTest(test.test_httplib.HTTPSTest('test_networked_good_cert'))
        suite.addTest(test.test_httplib.HTTPSTest('test_networked_noverification'))
        suite.addTest(test.test_httplib.HTTPSTest('test_networked_trusted_by_default_cert'))
        suite.addTest(test.test_httplib.HeaderTests('test_auto_headers'))
        suite.addTest(test.test_httplib.HeaderTests('test_content_length_0'))
        suite.addTest(test.test_httplib.HeaderTests('test_headers_debuglevel'))
        suite.addTest(test.test_httplib.HeaderTests('test_invalid_headers'))
        suite.addTest(test.test_httplib.HeaderTests('test_ipv6host_header'))
        suite.addTest(test.test_httplib.HeaderTests('test_malformed_headers_coped_with'))
        suite.addTest(test.test_httplib.HeaderTests('test_parse_all_octets'))
        suite.addTest(unittest.expectedFailure(test.test_httplib.HeaderTests('test_putheader'))) # https://github.com/IronLanguages/ironpython3/issues/1100
        suite.addTest(test.test_httplib.HttpMethodTests('test_invalid_method_names'))
        suite.addTest(test.test_httplib.OfflineTest('test_all'))
        suite.addTest(test.test_httplib.OfflineTest('test_client_constants'))
        suite.addTest(test.test_httplib.OfflineTest('test_responses'))
        suite.addTest(test.test_httplib.PersistenceTest('test_100_close'))
        suite.addTest(test.test_httplib.PersistenceTest('test_disconnected'))
        suite.addTest(test.test_httplib.PersistenceTest('test_reuse_reconnect'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_ascii_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_binary_file_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_bytes_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_latin1_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_list_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_manual_content_length'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_text_file_body'))
        suite.addTest(test.test_httplib.SourceAddressTest('testHTTPConnectionSourceAddress'))
        suite.addTest(test.test_httplib.SourceAddressTest('testHTTPSConnectionSourceAddress'))
        suite.addTest(test.test_httplib.TimeoutTest('testTimeoutAttribute'))
        suite.addTest(test.test_httplib.TransferEncodingTest('test_empty_body'))
        suite.addTest(test.test_httplib.TransferEncodingTest('test_endheaders_chunked'))
        suite.addTest(test.test_httplib.TransferEncodingTest('test_explicit_headers'))
        suite.addTest(test.test_httplib.TransferEncodingTest('test_request'))
        suite.addTest(test.test_httplib.TunnelTests('test_connect_put_request'))
        suite.addTest(test.test_httplib.TunnelTests('test_connect_with_tunnel'))
        suite.addTest(test.test_httplib.TunnelTests('test_disallow_set_tunnel_after_connect'))
        suite.addTest(test.test_httplib.TunnelTests('test_set_tunnel_host_port_headers'))
        suite.addTest(test.test_httplib.TunnelTests('test_tunnel_debuglog'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_httplib, pattern)

run_test(__name__)
