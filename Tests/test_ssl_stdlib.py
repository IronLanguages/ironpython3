# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ssl from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_ssl

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_ssl.BasicSocketTests('test_DER_to_PEM'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_asn1object'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_constants'))) # AttributeError: 'module' object has no attribute 'OP_CIPHER_SERVER_PREFERENCE'
        suite.addTest(test.test_ssl.BasicSocketTests('test_dealloc_warn'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_enum_certificates'))) # AssertionError: OSError not raised by enum_certificates
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_enum_crls'))) # AssertionError: [] is not true
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_errors'))) # AssertionError: OSError not raised
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_get_default_verify_paths'))) # AttributeError: 'module' object has no attribute 'get_default_verify_paths'
        suite.addTest(test.test_ssl.BasicSocketTests('test_match_hostname'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_openssl_version'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_parse_cert'))) # KeyError: OCSP
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_parse_cert_CVE_2013_4238'))) # AssertionError: Tuples differ
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_parse_cert_CVE_2019_5010'))) # AssertionError
        suite.addTest(test.test_ssl.BasicSocketTests('test_purpose_enum'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_random'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_random_fork'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_refcycle'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_server_side'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_timeout'))) # AssertionError: 0.0 != None
        suite.addTest(test.test_ssl.BasicSocketTests('test_tls_unique_channel_binding'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_unknown_channel_binding'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_unsupported_dtls'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_wrapped_unconnected'))
        suite.addTest(test.test_ssl.ContextTests('test__create_stdlib_context'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_cert_store_stats'))) # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_check_hostname'))) # AssertionError: ValueError not raised
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_ciphers'))) # AssertionError: SSLError not raised
        suite.addTest(test.test_ssl.ContextTests('test_constructor'))
        suite.addTest(test.test_ssl.ContextTests('test_create_default_context'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_get_ca_certs'))) # AttributeError: 'SSLContext' object has no attribute 'get_ca_certs'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_cert_chain'))) # AssertionError: OSError not raised
        suite.addTest(test.test_ssl.ContextTests('test_load_default_certs'))
        suite.addTest(test.test_ssl.ContextTests('test_load_default_certs_env'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_default_certs_env_windows'))) # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_dh_params'))) # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_verify_cadata'))) # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_verify_locations'))) # TypeError: expected str, got bytes
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_options'))) # AssertionError: 2197818367 != 50331648
        suite.addTest(test.test_ssl.ContextTests('test_protocol'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_session_stats'))) # AttributeError: 'SSLContext' object has no attribute 'session_stats'
        suite.addTest(test.test_ssl.ContextTests('test_set_default_verify_paths'))
        suite.addTest(test.test_ssl.ContextTests('test_set_ecdh_curve'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_sni_callback'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_sni_callback_refcycle'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(test.test_ssl.ContextTests('test_verify_flags'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_verify_mode'))) # AssertionError: ValueError not raised
        suite.addTest(test.test_ssl.NetworkedTests('test_algorithms'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_ciphers'))) # AssertionError: SSLError not raised
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_connect'))) # AssertionError: {} != None
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_connect_cadata'))) # ssl.SSLError: [Errno 'errors while validating certificate chain: '] RemoteCertificateChainErrors
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_connect_capath'))) # ssl.SSLError: [Errno 'errors while validating certificate chain: '] RemoteCertificateChainErrors
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_connect_ex'))) # ssl.SSLError: [Errno 'errors while validating certificate chain: '] RemoteCertificateChainErrors
        #suite.addTest(test.test_ssl.NetworkedTests('test_connect_ex_error')) # slow
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_connect_with_context'))) # AssertionError: {} != None
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_context_setget'))) # AttributeError: '_SSLSocket' object has no attribute 'context'
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_get_ca_certs_capath'))) # AttributeError: 'SSLContext' object has no attribute 'get_ca_certs'
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_get_server_certificate'))) # TypeError: Value cannot be null.
        suite.addTest(test.test_ssl.NetworkedTests('test_makefile_close'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_non_blocking_connect_ex'))) # OSError: [Errno -2146232800] The operation is not allowed on a non-blocking Socket.
        suite.addTest(unittest.expectedFailure(test.test_ssl.NetworkedTests('test_non_blocking_handshake'))) # TypeError: Value cannot be null.
        suite.addTest(test.test_ssl.NetworkedTests('test_timeout_connect_ex'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.SSLErrorTests('test_lib_reason'))) # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
        suite.addTest(unittest.expectedFailure(test.test_ssl.SSLErrorTests('test_str'))) # AssertionError: '[Errno 1] foo' != 'foo'
        suite.addTest(unittest.expectedFailure(test.test_ssl.SSLErrorTests('test_subclass'))) # TypeError: Value cannot be null.
        #suite.addTest(test.test_ssl.ThreadedTests('test_asyncore_server'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_check_hostname'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_compression'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_compression_disabled'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_crl_check'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_default_ciphers'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_default_ecdh_curve'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_dh_params'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_do_handshake_enotconn'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_echo'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_empty_cert'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_getpeercert'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_getpeercert_enotconn'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_handshake_timeout'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_malformed_cert'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_malformed_key'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_nonexisting_cert'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_npn_protocols'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_sslv2'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_sslv23'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_sslv3'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_tlsv1'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_tlsv1_1'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_tlsv1_2'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_read_write_after_close_raises_valuerror'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_recv_send'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_rude_shutdown'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_selected_npn_protocol'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_server_accept'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_sni_callback'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_sni_callback_alert'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_sni_callback_raising'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_sni_callback_wrong_return_type'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_socketserver'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_starttls'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_tls_unique_channel_binding'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_ssl, pattern)

run_test(__name__)
