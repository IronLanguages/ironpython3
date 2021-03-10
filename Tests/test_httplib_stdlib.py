# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_httplib from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_httplib

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_httplib.BasicTest('test_bad_status_repr'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked'))
        suite.addTest(test.test_httplib.BasicTest('test_chunked_head'))
        suite.addTest(test.test_httplib.BasicTest('test_delayed_ack_opt'))
        suite.addTest(test.test_httplib.BasicTest('test_early_eof'))
        suite.addTest(test.test_httplib.BasicTest('test_epipe'))
        suite.addTest(test.test_httplib.BasicTest('test_error_leak'))
        suite.addTest(test.test_httplib.BasicTest('test_host_port'))
        suite.addTest(test.test_httplib.BasicTest('test_incomplete_read'))
        suite.addTest(test.test_httplib.BasicTest('test_negative_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_chunked_line'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_header_line'))
        suite.addTest(test.test_httplib.BasicTest('test_overflowing_status_line'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_readintos'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_readintos_incomplete_body'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_readintos_no_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_reads'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_reads_incomplete_body'))
        suite.addTest(test.test_httplib.BasicTest('test_partial_reads_no_content_length'))
        suite.addTest(test.test_httplib.BasicTest('test_read_head'))
        suite.addTest(test.test_httplib.BasicTest('test_readinto_chunked'))
        suite.addTest(test.test_httplib.BasicTest('test_readinto_chunked_head'))
        suite.addTest(test.test_httplib.BasicTest('test_readinto_head'))
        suite.addTest(test.test_httplib.BasicTest('test_response_headers'))
        suite.addTest(test.test_httplib.BasicTest('test_send'))
        suite.addTest(test.test_httplib.BasicTest('test_send_file'))
        suite.addTest(test.test_httplib.BasicTest('test_send_iter'))
        suite.addTest(test.test_httplib.BasicTest('test_send_type_error'))
        suite.addTest(test.test_httplib.BasicTest('test_send_updating_file'))
        suite.addTest(test.test_httplib.BasicTest('test_status_lines'))
        suite.addTest(test.test_httplib.BasicTest('test_too_many_headers'))
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
        suite.addTest(unittest.expectedFailure(test.test_httplib.HTTPSTest('test_networked_good_cert'))) # ssl.SSLError: [Errno 'errors while validating certificate chain: '] RemoteCertificateChainErrors
        suite.addTest(test.test_httplib.HTTPSTest('test_networked_noverification'))
        suite.addTest(test.test_httplib.HTTPSTest('test_networked_trusted_by_default_cert'))
        suite.addTest(test.test_httplib.HeaderTests('test_auto_headers'))
        suite.addTest(test.test_httplib.HeaderTests('test_content_length_0'))
        suite.addTest(test.test_httplib.HeaderTests('test_invalid_headers'))
        suite.addTest(test.test_httplib.HeaderTests('test_ipv6host_header'))
        suite.addTest(test.test_httplib.HeaderTests('test_malformed_headers_coped_with'))
        suite.addTest(unittest.expectedFailure(test.test_httplib.HeaderTests('test_putheader'))) # https://github.com/IronLanguages/ironpython3/issues/1100
        suite.addTest(test.test_httplib.OfflineTest('test_all'))
        suite.addTest(test.test_httplib.OfflineTest('test_responses'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_ascii_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_binary_file_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_bytes_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_file_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_latin1_body'))
        suite.addTest(test.test_httplib.RequestBodyTest('test_manual_content_length'))
        suite.addTest(test.test_httplib.SourceAddressTest('testHTTPConnectionSourceAddress'))
        suite.addTest(test.test_httplib.SourceAddressTest('testHTTPSConnectionSourceAddress'))
        suite.addTest(test.test_httplib.TimeoutTest('testTimeoutAttribute'))
        suite.addTest(test.test_httplib.TunnelTests('test_connect_put_request'))
        suite.addTest(test.test_httplib.TunnelTests('test_connect_with_tunnel'))
        suite.addTest(test.test_httplib.TunnelTests('test_disallow_set_tunnel_after_connect'))
        suite.addTest(test.test_httplib.TunnelTests('test_set_tunnel_host_port_headers'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_httplib, pattern)

run_test(__name__)
