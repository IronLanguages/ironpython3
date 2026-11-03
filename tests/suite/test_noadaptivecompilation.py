# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Test that functions are compiled up front with -X:NoAdaptiveCompilation.
"""

import unittest

from iptest import IronPythonTestCase, is_cli, run_test

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

run_test(__name__)
