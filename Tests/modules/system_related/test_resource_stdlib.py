# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_resource from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_resource

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_resource.ResourceTest('test_args'))
        suite.addTest(test.test_resource.ResourceTest('test_freebsd_contants'))
        #suite.addTest(test.test_resource.ResourceTest('test_fsize_enforced')) # TODO: handle SIGXFSZ
        suite.addTest(test.test_resource.ResourceTest('test_fsize_ismax'))
        suite.addTest(test.test_resource.ResourceTest('test_fsize_toobig'))
        suite.addTest(test.test_resource.ResourceTest('test_getrusage'))
        suite.addTest(test.test_resource.ResourceTest('test_linux_constants'))
        suite.addTest(test.test_resource.ResourceTest('test_pagesize'))
        suite.addTest(test.test_resource.ResourceTest('test_prlimit'))
        suite.addTest(test.test_resource.ResourceTest('test_prlimit_refcount'))
        suite.addTest(test.test_resource.ResourceTest('test_setrusage_refcount'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_resource, pattern)

run_test(__name__)
