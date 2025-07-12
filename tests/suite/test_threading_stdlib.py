# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_threading from StdLib
##

from iptest import is_ironpython, is_mono, generate_suite, run_test

import test.test_threading

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_threading)

    if is_ironpython:
        failing_tests = [
            test.test_threading.CRLockTests('test_weakref_deleted'), # TypeError: cannot create weak reference to 'RLock' object
            test.test_threading.CRLockTests('test_weakref_exists'), # TypeError: cannot create weak reference to 'RLock' object
            test.test_threading.LockTests('test_weakref_deleted'), # TypeError: cannot create weak reference to 'lock' object
            test.test_threading.LockTests('test_weakref_exists'), # TypeError: cannot create weak reference to 'lock' object
            test.test_threading.ConditionAsRLockTests('test_weakref_deleted'), # AssertionError
            test.test_threading.PyRLockTests('test_weakref_deleted'), # AssertionError
            test.test_threading.ThreadTests('test_main_thread_during_shutdown'), # AssertionError
            test.test_threading.ThreadingExceptionTests('test_bare_raise_in_brand_new_thread'), # AssertionError: TypeError('exceptions must derive from BaseException',) is not an instance of <class 'RuntimeError'>
        ]

        skip_tests = [
            test.test_threading.SubinterpThreadingTests('test_threads_join'), # ImportError: No module named '_testcapi'
            test.test_threading.SubinterpThreadingTests('test_threads_join_2'), # ImportError: No module named '_testcapi'
            test.test_threading.ThreadTests('test_PyThreadState_SetAsyncExc'), # AttributeError: function PyThreadState_SetAsyncExc is not defined
            test.test_threading.ThreadTests('test_enumerate_after_join'), # AttributeError: 'module' object has no attribute 'getswitchinterval'
            test.test_threading.ThreadTests('test_finalize_runnning_thread'), # AssertionError: 1 != 42
            test.test_threading.ThreadTests('test_finalize_with_trace'), # AssertionError
            test.test_threading.ThreadTests('test_no_refcycle_through_target'), # AttributeError: 'module' object has no attribute 'getrefcount'
        ]

        if is_mono:
            skip_tests += [
                test.test_threading.ThreadJoinOnShutdown('test_4_daemon_threads'), # SystemError: Thread was being aborted
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
