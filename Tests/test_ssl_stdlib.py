# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ssl from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_posix

import test.test_ssl

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_ssl, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_ssl.BasicSocketTests('test_enum_crls'), # AssertionError: [] is not true
            test.test_ssl.BasicSocketTests('test_errors_sslwrap'), # NotImplementedError: keyfile
            test.test_ssl.BasicSocketTests('test_get_default_verify_paths'), # AttributeError: 'module' object has no attribute 'get_default_verify_paths'
            test.test_ssl.BasicSocketTests('test_parse_all_sans'), # AssertionError
            test.test_ssl.BasicSocketTests('test_parse_cert'), # KeyError: OCSP
            test.test_ssl.BasicSocketTests('test_parse_cert_CVE_2013_4238'), # AssertionError: Tuples differ
            test.test_ssl.BasicSocketTests('test_parse_cert_CVE_2019_5010'), # AssertionError
            test.test_ssl.BasicSocketTests('test_timeout'), # AssertionError: 0.0 != None
            test.test_ssl.ContextTests('test_cert_store_stats'), # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
            test.test_ssl.ContextTests('test_ciphers'), # AssertionError: SSLError not raised
            test.test_ssl.ContextTests('test_get_ca_certs'), # AssertionError: Lists differ
            test.test_ssl.ContextTests('test_load_cert_chain'), # AssertionError: OSError not raised
            test.test_ssl.ContextTests('test_load_default_certs_env_windows'), # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
            test.test_ssl.ContextTests('test_load_dh_params'), # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
            test.test_ssl.ContextTests('test_load_verify_cadata'), # AttributeError: 'SSLContext' object has no attribute 'cert_store_stats'
            test.test_ssl.ContextTests('test_load_verify_locations'), # AssertionError: "PEM lib" does not match ...
            test.test_ssl.ContextTests('test_options'), # AssertionError: -2097150977 != 50331648
            test.test_ssl.ContextTests('test_session_stats'), # AttributeError: 'SSLContext' object has no attribute 'session_stats'
            test.test_ssl.ContextTests('test_sni_callback'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ContextTests('test_sni_callback_refcycle'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ContextTests('test_verify_mode'), # AssertionError: ValueError not raised
            test.test_ssl.SSLErrorTests('test_lib_reason'), # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
            test.test_ssl.SSLErrorTests('test_str'), # AssertionError: '[Errno 1] foo' != 'foo'
            test.test_ssl.ThreadedTests('test_dh_params'), # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
            test.test_ssl.ThreadedTests('test_sni_callback'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ThreadedTests('test_sni_callback_alert'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ThreadedTests('test_sni_callback_raising'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ThreadedTests('test_sni_callback_wrong_return_type'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
        ]
        if is_posix:
            failing_tests += [
                test.test_ssl.ContextTests('test_load_default_certs_env'), # 'SSLContext' object has no attribute 'cert_store_stats'
            ]

        skip_tests = [
            test.test_ssl.SSLErrorTests('test_subclass'), # hangs indefinitely: wrapped SSLSocket resets timeout to None
            test.test_ssl.SimpleBackgroundTests('test_bio_handshake'),
            test.test_ssl.SimpleBackgroundTests('test_bio_read_write_data'),
            test.test_ssl.SimpleBackgroundTests('test_ciphers'),
            test.test_ssl.SimpleBackgroundTests('test_connect'),
            test.test_ssl.SimpleBackgroundTests('test_connect_cadata'),
            test.test_ssl.SimpleBackgroundTests('test_connect_capath'),
            test.test_ssl.SimpleBackgroundTests('test_connect_ex'),
            test.test_ssl.SimpleBackgroundTests('test_connect_fail'),
            test.test_ssl.SimpleBackgroundTests('test_connect_with_context'),
            test.test_ssl.SimpleBackgroundTests('test_connect_with_context_fail'),
            test.test_ssl.SimpleBackgroundTests('test_context_setget'),
            test.test_ssl.SimpleBackgroundTests('test_get_ca_certs_capath'),
            test.test_ssl.SimpleBackgroundTests('test_get_server_certificate'),
            test.test_ssl.SimpleBackgroundTests('test_get_server_certificate_fail'),
            test.test_ssl.SimpleBackgroundTests('test_makefile_close'),
            test.test_ssl.SimpleBackgroundTests('test_non_blocking_connect_ex'),
            test.test_ssl.SimpleBackgroundTests('test_non_blocking_handshake'),
            test.test_ssl.ThreadedTests('test_alpn_protocols'),
            test.test_ssl.ThreadedTests('test_asyncore_server'), # blocking
            test.test_ssl.ThreadedTests('test_check_hostname'),
            test.test_ssl.ThreadedTests('test_compression'),
            test.test_ssl.ThreadedTests('test_default_ecdh_curve'),
            test.test_ssl.ThreadedTests('test_echo'),
            test.test_ssl.ThreadedTests('test_getpeercert'), # blocking
            test.test_ssl.ThreadedTests('test_handshake_timeout'), # hangs indefinitely: wrapped SSLSocket resets timeout to None
            test.test_ssl.ThreadedTests('test_no_shared_ciphers'),
            test.test_ssl.ThreadedTests('test_nonblocking_send'),
            test.test_ssl.ThreadedTests('test_npn_protocols'),
            test.test_ssl.ThreadedTests('test_protocol_sslv2'),
            test.test_ssl.ThreadedTests('test_protocol_sslv23'),
            test.test_ssl.ThreadedTests('test_protocol_sslv3'),
            test.test_ssl.ThreadedTests('test_protocol_tlsv1'),
            test.test_ssl.ThreadedTests('test_protocol_tlsv1_1'),
            test.test_ssl.ThreadedTests('test_protocol_tlsv1_2'),
            test.test_ssl.ThreadedTests('test_read_write_after_close_raises_valuerror'), # blocking
            test.test_ssl.ThreadedTests('test_recv_send'), # NotImplementedError: keyfile
            test.test_ssl.ThreadedTests('test_recv_zero'),
            test.test_ssl.ThreadedTests('test_selected_alpn_protocol'),
            test.test_ssl.ThreadedTests('test_selected_alpn_protocol_if_server_uses_alpn'),
            test.test_ssl.ThreadedTests('test_selected_npn_protocol'),
            test.test_ssl.ThreadedTests('test_sendfile'),
            test.test_ssl.ThreadedTests('test_server_accept'),
            test.test_ssl.ThreadedTests('test_session'),
            test.test_ssl.ThreadedTests('test_session_handling'),
            test.test_ssl.ThreadedTests('test_shared_ciphers'),
            test.test_ssl.ThreadedTests('test_socketserver'),
            test.test_ssl.ThreadedTests('test_starttls'), # blocking
            test.test_ssl.ThreadedTests('test_version_basic'),
            test.test_ssl.ThreadedTests('test_wrong_cert'),
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
