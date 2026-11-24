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

# reports how its code objects run; the stack of imported code also has the frames of importlib, so the stack can't tell
imported_module = '''
import sys
import clr
from System.Reflection import BindingFlags

def kind(code):
    """Returns whether the code object runs compiled or interpreted, from the target of its delegate."""
    field = clr.GetClrType(type(code)).GetField('_normalDelegate', BindingFlags.NonPublic | BindingFlags.Instance)
    delegate = field.GetValue(code)
    if delegate is None:
        return 'none' # code that has only run while tracing
    # on .NET Framework, debug code is compiled to static methods, whose delegates have no target
    return 'interpreted' if delegate.Target is not None and delegate.Target.GetType().Name == 'LightLambda' else 'compiled'

def f():
    return kind(sys._getframe().f_code)

class C:
    in_class = kind(sys._getframe().f_code)

results = [kind(sys._getframe().f_code), f(), C.in_class]
'''

# imports the module, then runs its source again, compiled by compile rather than by importlib;
# with the argument 'tracing', imports the module while tracing, then reports how its function runs once tracing is off
import_script = '''
import sys

tracing = sys.argv[1:] == ['tracing']
if tracing:
    sys.settrace(lambda *args: None)
import dc_imported_module
sys.settrace(None)

if tracing:
    print(dc_imported_module.f())
else:
    ns = {}
    with open(dc_imported_module.__file__) as file:
        exec(compile(file.read(), dc_imported_module.__file__, 'exec'), ns)
    print(*(dc_imported_module.results + ns['results']))
'''

@skipUnlessIronPython()
class DebugImportTest(IronPythonTestCase):
    def setUp(self):
        super().setUp()
        directory = os.path.join(self.temporary_dir, 'debug_import_%d' % os.getpid())
        os.makedirs(directory, exist_ok=True)
        self.write_to_file(os.path.join(directory, 'dc_imported_module.py'), imported_module)
        self.script = os.path.join(directory, 'dc_import_script.py')
        self.write_to_file(self.script, import_script)

    def run_script(self, *options, tracing=False):
        args = ['tracing'] if tracing else []
        return subprocess.check_output([sys.executable, '-S'] + list(options) + [self.script] + args).split()

    def test_import(self):
        # the module code, the function and the class body of the imported module are compiled, as in a script run from a file;
        # the same source compiled by compile is still interpreted
        self.assertEqual(self.run_script('-X', 'Debug'), [b'compiled'] * 3 + [b'interpreted'] * 3)

    def test_import_no_debug(self):
        self.assertEqual(self.run_script('-X', 'Debug', '-X', 'NoDebug=dc_imported_module'), [b'interpreted'] * 6)

    def test_import_while_tracing(self):
        # a module compiled while tracing doesn't compile its functions with the module code, they get their own delegates
        self.assertEqual(self.run_script('-X', 'Debug', tracing=True), [b'compiled'])

run_test(__name__)
