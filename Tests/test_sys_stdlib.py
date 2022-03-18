# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_sys from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_sys

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_sys.SizeofTest('test_default'))
        suite.addTest(test.test_sys.SizeofTest('test_errors'))
        suite.addTest(test.test_sys.SizeofTest('test_gc_head_size'))
        suite.addTest(test.test_sys.SizeofTest('test_objecttypes'))
        suite.addTest(test.test_sys.SizeofTest('test_pythontypes'))
        #suite.addTest(test.test_sys.SysModuleTest('test_43581')) # failing in CI
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_attributes'))) # AssertionError: None != 0
        suite.addTest(test.test_sys.SysModuleTest('test_call_tracing'))
        suite.addTest(test.test_sys.SysModuleTest('test_clear_type_cache'))
        # suite.addTest(test.test_sys.SysModuleTest('test_current_frames')) # TODO: slow and fails
        suite.addTest(test.test_sys.SysModuleTest('test_custom_displayhook'))
        suite.addTest(test.test_sys.SysModuleTest('test_debugmallocstats'))
        suite.addTest(test.test_sys.SysModuleTest('test_dlopenflags'))
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_excepthook'))) # TypeError: Exception expected for value, str found
        suite.addTest(test.test_sys.SysModuleTest('test_executable'))
        # suite.addTest(suite.addTest(test.test_sys.SysModuleTest('test_exit'))) # TODO: slow and fails
        suite.addTest(test.test_sys.SysModuleTest('test_getallocatedblocks'))
        suite.addTest(test.test_sys.SysModuleTest('test_getdefaultencoding'))
        suite.addTest(test.test_sys.SysModuleTest('test_getfilesystemencoding'))
        suite.addTest(test.test_sys.SysModuleTest('test_getframe'))
        suite.addTest(test.test_sys.SysModuleTest('test_getwindowsversion'))
        suite.addTest(test.test_sys.SysModuleTest('test_implementation'))
        suite.addTest(test.test_sys.SysModuleTest('test_intern'))
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_ioencoding'))) # AssertionError: b'\x9b' != b'J\r%'
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_ioencoding_nonascii'))) # AssertionError: b'\x91' != b'\xe6'
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_lost_displayhook'))) # TypeError: NoneType is not callable
        suite.addTest(test.test_sys.SysModuleTest('test_original_displayhook'))
        suite.addTest(test.test_sys.SysModuleTest('test_original_excepthook'))
        suite.addTest(test.test_sys.SysModuleTest('test_recursionlimit'))
        #suite.addTest(test.test_sys.SysModuleTest('test_recursionlimit_fatalerror')) # StackOverflowException
        #suite.addTest(test.test_sys.SysModuleTest('test_recursionlimit_recovery')) # StackOverflowException
        suite.addTest(test.test_sys.SysModuleTest('test_refcount'))
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_setcheckinterval'))) # NotImplementedError: IronPython does not support sys.getcheckinterval
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_switchinterval'))) # AttributeError: 'module' object has no attribute 'setswitchinterval'
        suite.addTest(unittest.expectedFailure(test.test_sys.SysModuleTest('test_sys_flags'))) # AssertionError: False is not true : hash_randomization
        suite.addTest(test.test_sys.SysModuleTest('test_sys_flags_no_instantiation'))
        suite.addTest(test.test_sys.SysModuleTest('test_sys_getwindowsversion_no_instantiation'))
        suite.addTest(test.test_sys.SysModuleTest('test_sys_version_info_no_instantiation'))
        suite.addTest(test.test_sys.SysModuleTest('test_thread_info'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_sys, pattern)

run_test(__name__)
