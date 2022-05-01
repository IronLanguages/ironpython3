# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ssl from StdLib
##

import unittest
import sys

from iptest import is_posix, run_test

import test.test_ssl

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_ssl.BasicSocketTests('test_DER_to_PEM'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_asn1object'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_cert_time_to_seconds'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_cert_time_to_seconds_locale'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_cert_time_to_seconds_timezone'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_connect_ex_error'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_constants'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_dealloc_warn'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_empty_cert'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_enum_certificates'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_enum_crls'))) # AssertionError: [] is not true
        suite.addTest(test.test_ssl.BasicSocketTests('test_errors_sslwrap'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_get_default_verify_paths'))) # AttributeError: 'module' object has no attribute 'get_default_verify_paths'
        suite.addTest(test.test_ssl.BasicSocketTests('test_malformed_cert'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_malformed_key'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_match_hostname'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_openssl_version'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_parse_all_sans'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_parse_cert'))) # KeyError: OCSP
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_parse_cert_CVE_2013_4238'))) # AssertionError: Tuples differ
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_parse_cert_CVE_2019_5010'))) # AssertionError
        suite.addTest(test.test_ssl.BasicSocketTests('test_purpose_enum'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_random'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_random_fork'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_refcycle'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_server_side'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_str_for_enums'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.BasicSocketTests('test_timeout'))) # AssertionError: 0.0 != None
        suite.addTest(test.test_ssl.BasicSocketTests('test_tls_unique_channel_binding'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_unknown_channel_binding'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_unsupported_dtls'))
        suite.addTest(test.test_ssl.BasicSocketTests('test_wrapped_unconnected'))
        suite.addTest(test.test_ssl.ContextTests('test__create_stdlib_context'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_cert_store_stats'))) # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
        suite.addTest(test.test_ssl.ContextTests('test_check_hostname'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_ciphers'))) # AssertionError: SSLError not raised
        suite.addTest(test.test_ssl.ContextTests('test_constructor'))
        suite.addTest(test.test_ssl.ContextTests('test_context_client_server'))
        suite.addTest(test.test_ssl.ContextTests('test_create_default_context'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_get_ca_certs'))) # AssertionError: Lists differ
        suite.addTest(test.test_ssl.ContextTests('test_get_ciphers'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_cert_chain'))) # AssertionError: OSError not raised
        suite.addTest(test.test_ssl.ContextTests('test_load_default_certs'))
        if is_posix:
            suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_default_certs_env'))) # 'SSLContext' object has no attribute 'cert_store_stats'
        else:
            suite.addTest(test.test_ssl.ContextTests('test_load_default_certs_env'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_default_certs_env_windows'))) # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_dh_params'))) # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_verify_cadata'))) # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_load_verify_locations'))) # AssertionError: "PEM lib" does not match ...
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_options'))) # AssertionError: -2097150977 != 50331648
        suite.addTest(test.test_ssl.ContextTests('test_protocol'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_session_stats'))) # AttributeError: 'SSLContext' object has no attribute 'session_stats'
        suite.addTest(test.test_ssl.ContextTests('test_set_default_verify_paths'))
        suite.addTest(test.test_ssl.ContextTests('test_set_ecdh_curve'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_sni_callback'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_sni_callback_refcycle'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(test.test_ssl.ContextTests('test_verify_flags'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ContextTests('test_verify_mode'))) # AssertionError: ValueError not raised
        suite.addTest(test.test_ssl.MemoryBIOTests('test_buffer_types'))
        suite.addTest(test.test_ssl.MemoryBIOTests('test_eof'))
        suite.addTest(test.test_ssl.MemoryBIOTests('test_error_types'))
        suite.addTest(test.test_ssl.MemoryBIOTests('test_pending'))
        suite.addTest(test.test_ssl.MemoryBIOTests('test_read_write'))
        suite.addTest(test.test_ssl.NetworkedTests('test_get_server_certificate_ipv6'))
        suite.addTest(test.test_ssl.NetworkedTests('test_timeout_connect_ex'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.SSLErrorTests('test_lib_reason'))) # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
        suite.addTest(unittest.expectedFailure(test.test_ssl.SSLErrorTests('test_str'))) # AssertionError: '[Errno 1] foo' != 'foo'
        suite.addTest(unittest.expectedFailure(test.test_ssl.SSLErrorTests('test_subclass'))) # TypeError: Value cannot be null.
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_bio_handshake'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_bio_read_write_data'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_ciphers'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect_cadata'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect_capath'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect_ex'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect_fail'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect_with_context'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_connect_with_context_fail'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_context_setget'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_get_ca_certs_capath'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_get_server_certificate'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_get_server_certificate_fail'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_makefile_close'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_non_blocking_connect_ex'))
        suite.addTest(test.test_ssl.SimpleBackgroundTests('test_non_blocking_handshake'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_no_pha_client'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_no_pha_server'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_not_tls13'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_optional'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_optional_nocert'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_required'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_required_nocert'))
        suite.addTest(test.test_ssl.TestPostHandshakeAuth('test_pha_setter'))
        suite.addTest(test.test_ssl.ThreadedTests('test_alpn_protocols'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_asyncore_server')) # blocking
        #suite.addTest(test.test_ssl.ThreadedTests('test_check_hostname'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_compression'))
        suite.addTest(test.test_ssl.ThreadedTests('test_compression_disabled'))
        suite.addTest(test.test_ssl.ThreadedTests('test_crl_check'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_default_ecdh_curve'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_dh_params'))) # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
        suite.addTest(test.test_ssl.ThreadedTests('test_do_handshake_enotconn'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_echo'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_getpeercert')) # blocking
        suite.addTest(test.test_ssl.ThreadedTests('test_getpeercert_enotconn'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_handshake_timeout'))) # TypeError: Value cannot be null.
        suite.addTest(test.test_ssl.ThreadedTests('test_no_shared_ciphers'))
        suite.addTest(test.test_ssl.ThreadedTests('test_nonblocking_send'))
        suite.addTest(test.test_ssl.ThreadedTests('test_npn_protocols'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_sslv2'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_sslv23'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_sslv3'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_tlsv1'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_tlsv1_1'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_protocol_tlsv1_2'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_read_write_after_close_raises_valuerror')) # blocking
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_recv_send'))) # NotImplementedError: keyfile
        suite.addTest(test.test_ssl.ThreadedTests('test_recv_zero'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_rude_shutdown'))) # TypeError: Value cannot be null.
        suite.addTest(test.test_ssl.ThreadedTests('test_selected_alpn_protocol'))
        suite.addTest(test.test_ssl.ThreadedTests('test_selected_alpn_protocol_if_server_uses_alpn'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_selected_npn_protocol'))
        suite.addTest(test.test_ssl.ThreadedTests('test_sendfile'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_server_accept'))
        suite.addTest(test.test_ssl.ThreadedTests('test_session'))
        suite.addTest(test.test_ssl.ThreadedTests('test_session_handling'))
        suite.addTest(test.test_ssl.ThreadedTests('test_shared_ciphers'))
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_sni_callback'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_sni_callback_alert'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_sni_callback_raising'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        suite.addTest(unittest.expectedFailure(test.test_ssl.ThreadedTests('test_sni_callback_wrong_return_type'))) # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        #suite.addTest(test.test_ssl.ThreadedTests('test_socketserver'))
        #suite.addTest(test.test_ssl.ThreadedTests('test_starttls')) # blocking
        suite.addTest(test.test_ssl.ThreadedTests('test_tls1_3'))
        suite.addTest(test.test_ssl.ThreadedTests('test_tls_unique_channel_binding'))
        suite.addTest(test.test_ssl.ThreadedTests('test_version_basic'))
        suite.addTest(test.test_ssl.ThreadedTests('test_wrong_cert'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_ssl, pattern)

run_test(__name__)
