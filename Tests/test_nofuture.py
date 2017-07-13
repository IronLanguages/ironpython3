#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

##
## To test __future__ related areas where __future__ is NOT enabled
## in the module scope
##

import os
import unittest

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test

def always_true(self):
    exec "self.assertEqual(1 / 2, 0)"
    exec "from __future__ import division; self.assertEqual(1 / 2, 0.5)"
    self.assertEqual(1/2, 0)
    self.assertEqual(eval("1/2"), 0)

assert_code = '''
def CustomAssert(c):
    if not c: raise AssertionError("Assertion Failed")

'''

code1  = assert_code + '''
exec "CustomAssert(1/2 == 0)"
exec "from __future__ import division; CustomAssert(1/2 == 0.5)"
CustomAssert(1/2 == 0)
CustomAssert(eval('1/2') == 0)
'''

code2 = '''
from __future__ import division
''' + assert_code + '''
exec "CustomAssert(1/2 == 0.5)"
exec "from __future__ import division; CustomAssert(1/2 == 0.5)"
CustomAssert(1/2 == 0.5)
CustomAssert(eval('1/2') == 0.5)
'''

def f1(tempfile): execfile(tempfile)
def f2(code, tempfile): exec(compile(code, tempfile, "exec"))
def f3(code): exec(code)
def f4():
    if is_cli:
        # import IronPython
        #pe = IronPython.Hosting.PythonEngine()
        #issue around py hosting py again.
        pass

class NoFutureTest(IronPythonTestCase):
    def setUp(self):
        super(NoFutureTest, self).setUp()
        self.tempfile = os.path.join(self.temporary_dir, "temp_future.py")

    def test_simple(self):
        always_true(self)
        try:
            import sys
            with path_modifier(self.temporary_dir):
                for code in (code1, code2) :
                    self.write_to_file(self.tempfile, code)
                    
                    f1(self.tempfile)
                    always_true(self)
                    f2(code, self.tempfile)
                    always_true(self)
                    f3(code)
                    always_true(self)
        finally:
            self.delete_files(self.tempfile)

run_test(__name__)