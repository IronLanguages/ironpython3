# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Test that code is compiled, not interpreted, with -X Debug.
"""

import os
import subprocess
import sys
import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

checked_module = '''
import System

def interpreted():
    """Returns whether the caller runs in the System.Linq.Expressions interpreter."""
    for frame in System.Diagnostics.StackTrace().GetFrames():
        method = frame.GetMethod()
        if method is not None and method.DeclaringType is not None and method.DeclaringType.Namespace == 'System.Linq.Expressions.Interpreter':
            return True
    return False

def f():
    return interpreted()

class C:
    in_class = interpreted()

results = [interpreted(), f(), C.in_class]

if __name__ == '__main__':
    print(results)
'''

# runs the module in a new scope of an engine in debug mode; the runner itself is not in debug mode
runner_script = '''
import sys
import clr
clr.AddReference('IronPython')
from IronPython.Hosting import Python

engine = Python.CreateEngine({'Debug': True})
scope = engine.CreateScope()
engine.ExecuteFile(sys.argv[1], scope)
print(scope.GetVariable('results'))
'''

@skipUnlessIronPython()
class DebugCompileTest(IronPythonTestCase):
    def setUp(self):
        super().setUp()
        directory = os.path.join(self.temporary_dir, 'debug_compile_%d' % os.getpid())
        os.makedirs(directory, exist_ok=True)
        self.module = os.path.join(directory, 'dc_checked_module.py')
        self.write_to_file(self.module, checked_module)
        self.runner = os.path.join(directory, 'dc_runner.py')
        self.write_to_file(self.runner, runner_script)

    def assertCompiled(self, *args):
        output = subprocess.check_output([sys.executable, '-S'] + list(args))
        # the module code, the function and the class body
        self.assertEqual(output.strip(), b'[False, False, False]')

    def test_main(self):
        self.assertCompiled('-X', 'Debug', self.module)

    def test_main_tracing(self):
        self.assertCompiled('-X', 'Debug', '-X', 'Tracing', self.module)

    def test_new_scope(self):
        self.assertCompiled(self.runner, self.module)

run_test(__name__)
