# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_socket from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_linux, is_mono, is_posix

import test.test_socket

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_socket)

    if is_ironpython:
        failing_tests = [
            test.test_socket.BasicTCPTest('testDetach'), # https://github.com/IronLanguages/ironpython3/issues/1224
            test.test_socket.BasicTCPTest('testDup'), # https://github.com/IronLanguages/ironpython3/issues/1223
            test.test_socket.BasicTCPTest('testFromFd'), # https://github.com/IronLanguages/ironpython3/issues/1223
            test.test_socket.BasicTCPTest2('testDetach'), # https://github.com/IronLanguages/ironpython3/issues/1224
            test.test_socket.BasicTCPTest2('testDup'), # https://github.com/IronLanguages/ironpython3/issues/1223
            test.test_socket.BasicTCPTest2('testFromFd'), # https://github.com/IronLanguages/ironpython3/issues/1223
            test.test_socket.GeneralModuleTests('testSendtoErrors'), # TypeError messages all different
            test.test_socket.GeneralModuleTests('test_csocket_repr'), # https://github.com/IronLanguages/ironpython3/issues/1221
            test.test_socket.GeneralModuleTests('test_uknown_socket_family_repr'), # TODO: figure out
            test.test_socket.InheritanceTest('test_default_inheritable'), # https://github.com/IronLanguages/ironpython3/issues/1225
            test.test_socket.InheritanceTest('test_dup'), # https://github.com/IronLanguages/ironpython3/issues/1223
            test.test_socket.InheritanceTest('test_get_inheritable_cloexec'), # https://github.com/IronLanguages/ironpython3/issues/1225
            test.test_socket.InheritanceTest('test_set_inheritable'), # https://github.com/IronLanguages/ironpython3/issues/1225
            test.test_socket.InheritanceTest('test_set_inheritable_cloexec'), # https://github.com/IronLanguages/ironpython3/issues/1225
            test.test_socket.InheritanceTest('test_socketpair'), # https://github.com/IronLanguages/ironpython3/issues/1225
            test.test_socket.TestSocketSharing('testShare'), # https://github.com/IronLanguages/ironpython3/issues/1226
            test.test_socket.TestSocketSharing('testShareLength'), # https://github.com/IronLanguages/ironpython3/issues/1226
            test.test_socket.TestSocketSharing('testShareLocal'), # https://github.com/IronLanguages/ironpython3/issues/1226
            test.test_socket.TestSocketSharing('testTypes'), # https://github.com/IronLanguages/ironpython3/issues/1226
            test.test_socket.UnbufferedFileObjectClassTestCase('testSmallReadNonBlocking'), # TODO: figure out

        ]
        if is_posix:
            failing_tests += [
                test.test_socket.NonBlockingTCPTests('testRecv'), # TODO: figure out
            ]
        if not is_mono:
            failing_tests += [
                test.test_socket.GeneralModuleTests('test_getnameinfo'), # https://github.com/IronLanguages/ironpython3/issues/1222
            ]
        if is_linux:
            failing_tests += [
                test.test_socket.GeneralModuleTests('test_idna'), # TODO: figure out
            ]

        skip_tests = [
            test.test_socket.UnbufferedFileObjectClassTestCase('testWriteNonBlocking') # fails intermittently during CI
        ]
        if is_posix: # TODO: figure out - failure in setup
            skip_tests += [
                test.test_socket.BasicSocketPairTest('testDefaults'),
                test.test_socket.BasicSocketPairTest('testRecv'),
                test.test_socket.BasicSocketPairTest('testSend'),
            ]
        if is_posix: # TODO: figure out - failure in teardown
            skip_tests += [
                test.test_socket.NonBlockingTCPTests('testAccept')
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
