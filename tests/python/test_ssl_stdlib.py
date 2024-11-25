# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ssl from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono, is_osx, is_posix

import test.test_ssl

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_ssl)

    if is_ironpython:
        failing_tests = [
            test.test_ssl.BasicSocketTests('test_enum_crls'), # AssertionError: [] is not true
            test.test_ssl.BasicSocketTests('test_errors'), # AssertionError: OSError not raised
            test.test_ssl.BasicSocketTests('test_get_default_verify_paths'), # AttributeError: 'module' object has no attribute 'get_default_verify_paths'
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
            test.test_ssl.NetworkedTests('test_ciphers'), # AssertionError: SSLError not raised
            test.test_ssl.NetworkedTests('test_connect'), # AssertionError: {} != None
            test.test_ssl.NetworkedTests('test_connect_capath'), # ssl.SSLError: [Errno 'errors while validating certificate chain: '] RemoteCertificateChainErrors
            test.test_ssl.NetworkedTests('test_connect_with_context'), # AssertionError: {} != None
            test.test_ssl.NetworkedTests('test_context_setget'), # AttributeError: can't assign to read-only property context of type '_SSLSocket'
            test.test_ssl.NetworkedTests('test_get_ca_certs_capath'), # AttributeError: 'SSLContext' object has no attribute 'get_ca_certs'
            test.test_ssl.NetworkedTests('test_non_blocking_connect_ex'), # OSError: [Errno -2146232800] The operation is not allowed on a non-blocking Socket.
            test.test_ssl.SSLErrorTests('test_lib_reason'), # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
            test.test_ssl.SSLErrorTests('test_str'), # AssertionError: '[Errno 1] foo' != 'foo'
            test.test_ssl.ThreadedTests('test_dh_params'), # AttributeError: 'SSLContext' object has no attribute 'load_dh_params'
            test.test_ssl.ThreadedTests('test_empty_cert'), # NotImplementedError: keyfile
            test.test_ssl.ThreadedTests('test_malformed_cert'), # NotImplementedError: keyfile
            test.test_ssl.ThreadedTests('test_malformed_key'), # NotImplementedError: keyfile
            test.test_ssl.ThreadedTests('test_nonexisting_cert'), # NotImplementedError: keyfile
            test.test_ssl.ThreadedTests('test_recv_send'), # NotImplementedError: keyfile
            test.test_ssl.ThreadedTests('test_sni_callback'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ThreadedTests('test_sni_callback_alert'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ThreadedTests('test_sni_callback_raising'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'
            test.test_ssl.ThreadedTests('test_sni_callback_wrong_return_type'), # AttributeError: 'SSLContext' object has no attribute 'set_servername_callback'

        ]
        if is_posix:
            failing_tests += [
                test.test_ssl.ContextTests('test_load_default_certs_env'), # 'SSLContext' object has no attribute 'cert_store_stats'
                test.test_ssl.NetworkedTests('test_makefile_close'), # OSError: [Errno 9] Bad file descriptor
            ]
        if is_mono and is_osx:
            failing_tests += [
                test.test_ssl.NetworkedTests('test_connect_cadata'), # # https://github.com/IronLanguages/ironpython3/issues/1523
                test.test_ssl.NetworkedTests('test_connect_ex'), # https://github.com/IronLanguages/ironpython3/issues/1523
                test.test_ssl.NetworkedTests('test_get_server_certificate'), # https://github.com/IronLanguages/ironpython3/issues/1523
                test.test_ssl.ThreadedTests('test_rude_shutdown'), # ValueError: Value does not fall within the expected range.
            ]

        skip_tests = [
            test.test_ssl.NetworkedTests('test_connect_ex_error'), # slow
            test.test_ssl.SSLErrorTests('test_subclass'), # blocking
            test.test_ssl.ThreadedTests('test_asyncore_server'), # blocking
            test.test_ssl.ThreadedTests('test_check_hostname'),
            test.test_ssl.ThreadedTests('test_compression'),
            test.test_ssl.ThreadedTests('test_default_ciphers'),
            test.test_ssl.ThreadedTests('test_default_ecdh_curve'),
            test.test_ssl.ThreadedTests('test_echo'),
            test.test_ssl.ThreadedTests('test_getpeercert'), # blocking
            test.test_ssl.ThreadedTests('test_handshake_timeout'), # blocking
            test.test_ssl.ThreadedTests('test_protocol_sslv2'),
            test.test_ssl.ThreadedTests('test_protocol_sslv23'),
            test.test_ssl.ThreadedTests('test_protocol_sslv3'),
            test.test_ssl.ThreadedTests('test_protocol_tlsv1'),
            test.test_ssl.ThreadedTests('test_protocol_tlsv1_1'),
            test.test_ssl.ThreadedTests('test_protocol_tlsv1_2'),
            test.test_ssl.ThreadedTests('test_read_write_after_close_raises_valuerror'), # blocking
            test.test_ssl.ThreadedTests('test_selected_npn_protocol'),
            test.test_ssl.ThreadedTests('test_server_accept'),
            test.test_ssl.ThreadedTests('test_socketserver'),
            test.test_ssl.ThreadedTests('test_starttls'), # blocking
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
