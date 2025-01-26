# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import run_test

@unittest.skipIf(sys.flags.optimize, "should be run without optimize")
class AssertTest(unittest.TestCase):

    def test_positive(self):
        try:
            assert True
        except AssertionError as e:
            raise "Should have been no exception!"

        try:
            assert True, 'this should always pass'
        except AssertionError as e:
            raise "Should have been no exception!"
            
    def test_negative(self):
        ok = False
        try:
            assert False
        except AssertionError as e:
            ok = True
            self.assertEqual(str(e), "")
        self.assertTrue(ok)
        
        ok = False
        try:
            assert False
        except AssertionError as e:
            ok = True
            self.assertEqual(str(e), "")
        self.assertTrue(ok)
        
        ok = False
        try:
            assert False, 'this should never pass'
        except AssertionError as e:
            ok = True
            self.assertEqual(str(e), "this should never pass")
        self.assertTrue(ok)
        
        ok = False
        try:
            assert None, 'this should never pass'
        except AssertionError as e:
            ok = True
            self.assertEqual(str(e), "this should never pass")
        self.assertTrue(ok)
            
    def test_doesnt_fail_on_curly(self):
        """Ensures that asserting a string with a curly brace doesn't choke up the
        string formatter."""

        ok = False
        try:
            assert False, '}'
        except AssertionError:
            ok = True
        self.assertTrue(ok)

    def test_custom_assertionerror(self):
        """https://github.com/IronLanguages/ironpython2/issues/107"""
        class MyAssertionError(Exception):
            def __init__(self, msg):
                super(MyAssertionError, self).__init__(msg)

        def test():
            assert False, 'You are here'

        import builtins
        old = builtins.AssertionError
        builtins.AssertionError = MyAssertionError
        try:
            self.assertRaises(MyAssertionError, test)
        finally:
            builtins.AssertionError = old
  
  
#--Main------------------------------------------------------------------------
# if is_cli and '-O' in System.Environment.GetCommandLineArgs():
#     from iptest.process_util import *
#     self.assertEqual(0, launch_ironpython_changing_extensions(__file__, remove=["-O"]))
# else:
#     run_test(__name__)

run_test(__name__)

