# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Sanity tests for exceptions automatically generated by generate_exceptions.py (that
cannot be hit under normal circumstances).
"""

import os
import sys
import unittest

from iptest import is_cli, path_modifier, run_test, skipUnlessIronPython, source_root

def gen_testcase(exc_name):
    def test(self):
        import IronPython.Runtime.Exceptions as IRE
        e0 = eval("IRE.%s()" % exc_name)
        e2 = eval("IRE.%s('msg', e0)" % exc_name)
        self.assertEqual(e0.Message, e2.InnerException.Message)
        self.assertEqual("msg", e2.Message)
    return test

@skipUnlessIronPython()
class ExceptionsGeneratedTest(unittest.TestCase):
    def setUp(self):
        super(ExceptionsGeneratedTest, self).setUp()
        import clr
        clr.AddReference("IronPython")

if is_cli:
    with path_modifier(os.path.join(source_root(), 'Src', 'Scripts')):
        from generate_exceptions import pythonExcs as test_cases
    test_cases = [x.replace('Error', '') + 'Exception' for x in test_cases]
    for exc_name in test_cases:
        test_name = 'test_%s' % exc_name
        test = gen_testcase(exc_name)
        setattr(ExceptionsGeneratedTest, test_name, test)

run_test(__name__)