# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Test that functions are compiled up front with -X NoAdaptiveCompilation.
"""

import os
import subprocess
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    import clr
    from System.Reflection import BindingFlags

    def is_interpreted(func):
        """Returns whether the function, which must have been called at least once, runs in the interpreter."""
        field = clr.GetClrType(type(func.__code__)).GetField('_normalDelegate', BindingFlags.NonPublic | BindingFlags.Instance)
        delegate = field.GetValue(func.__code__)
        return delegate.Target.GetType().Name == 'LightLambda'

    no_adaptive_compilation = clr.GetCurrentRuntime().GetLanguageByName('python').Options.NoAdaptiveCompilation
else:
    no_adaptive_compilation = False

def no_loop(n):
    return n

def with_for(n):
    for i in range(n):
        pass

def with_while(n):
    i = 0
    while i < n:
        i += 1

def generator(n):
    for i in range(n):
        yield i

@unittest.skipUnless(no_adaptive_compilation, 'requires -X NoAdaptiveCompilation')
class NoAdaptiveCompilationTest(IronPythonTestCase):
    def test_functions(self):
        def nested(n):
            return n

        class C:
            def method(self, n):
                return n

        calls = [(no_loop, 1), (with_for, 1), (with_while, 1), (generator, 1), (nested, 1), (lambda n: n, 1), (C.method, C(), 1)]
        for func, *args in calls:
            func(*args) # for a generator, this runs the function that creates the generator object
            self.assertFalse(is_interpreted(func), func.__name__)

    def test_imported_and_exec(self):
        # importlib compiles modules with compile(), like exec, which is compiled adaptively unless the option says otherwise
        import textwrap
        textwrap.indent('text', ' ')
        self.assertFalse(is_interpreted(textwrap.indent))

        ns = {}
        exec('def in_exec(n):\n    return n\n', ns)
        ns['in_exec'](1)
        self.assertFalse(is_interpreted(ns['in_exec']))

child_module = '''
def f(n):
    return n
'''

child_script = '''
import clr
from System.Reflection import BindingFlags
import nac_filtered_module

def is_interpreted(func):
    field = clr.GetClrType(type(func.__code__)).GetField('_normalDelegate', BindingFlags.NonPublic | BindingFlags.Instance)
    return field.GetValue(func.__code__).Target.GetType().Name == 'LightLambda'

def in_main(n):
    return n

ns = {}
exec('def in_exec(n):\\n    return n\\n', ns)
for func in (in_main, nac_filtered_module.f, ns['in_exec']):
    func(1)
print(is_interpreted(in_main), is_interpreted(nac_filtered_module.f), is_interpreted(ns['in_exec']))
'''

@skipUnlessIronPython()
class NoAdaptiveCompilationFilterTest(IronPythonTestCase):
    def test_filter(self):
        directory = os.path.join(self.temporary_dir, 'nac_filter_%d' % os.getpid())
        os.makedirs(directory, exist_ok=True)
        self.write_to_file(os.path.join(directory, 'nac_filtered_module.py'), child_module)
        script = os.path.join(directory, 'nac_main.py')
        self.write_to_file(script, child_script)

        output = subprocess.check_output([sys.executable, '-S', '-X', 'NoAdaptiveCompilation=nac_filtered_module', script])
        # only the module matching the regex is compiled up front: not the script itself, nor code run by exec
        self.assertEqual(output.split(), [b'True', b'False', b'True'])

run_test(__name__)
