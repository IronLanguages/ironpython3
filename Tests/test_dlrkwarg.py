# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, is_posix, long, run_test, skipUnlessIronPython

if is_cli:
    import System
    from iptest.ipunittest import load_ironpython_test
    load_ironpython_test()
    from IronPythonTest import DefaultParams, Variadics


@skipUnlessIronPython()
class DlrKwargTest(IronPythonTestCase):
    def test_defaults(self):
        self.assertEqual(DefaultParams.FuncWithDefaults(1100, z=82), 1184)

    def call_kwargs_func(self, func):
        self.assertEqual(func(), 0)
        self.assertEqual(func(arg1='arg1'), 1)
        self.assertEqual(func(arg1='arg1', arg2='arg2'), 2)
        self.assertEqual(func(arg1='bad', arg2='arg2'), 'arg1')
        self.assertEqual(func(arg1='arg1', arg2='bad'), 'arg2')
        self.assertEqual(func(**{'arg1':'arg1'}), 1)
        self.assertEqual(func(**{'arg1':'arg1', 'arg2':'arg2'}), 2)
        self.assertEqual(func(**{'arg1':'bad', 'arg2':'arg2'}), 'arg1')
        self.assertEqual(func(**{'arg1':'arg1', 'arg2':'bad'}), 'arg2')

    def test_idict(self):
        self.call_kwargs_func(Variadics.FuncWithIDictKwargs)

    def test_odict(self):
        self.call_kwargs_func(Variadics.FuncWithDictGenOOKwargs)

    def test_sdict(self):
        self.call_kwargs_func(Variadics.FuncWithDictGenSOKwargs)

    def test_iodict(self):
        self.call_kwargs_func(Variadics.FuncWithIDictGenOOKwargs)

    def test_isdict(self):
        self.call_kwargs_func(Variadics.FuncWithIDictGenSOKwargs)

    def test_irosdict(self):
        self.call_kwargs_func(Variadics.FuncWithIRoDictGenSOKwargs)

    def test_iroodict(self):
        self.call_kwargs_func(Variadics.FuncWithIRoDictGenOOKwargs)

    def test_isidict(self):
        self.assertEqual(Variadics.FuncWithIDictGenSIKwargs(), 0)
        self.assertEqual(Variadics.FuncWithIDictGenSIKwargs(arg1=1), 1)
        self.assertEqual(Variadics.FuncWithIDictGenSIKwargs(arg1=1, arg2=2), 2)
        self.assertEqual(Variadics.FuncWithIDictGenSIKwargs(arg1=0, arg2=2), 'arg1')
        self.assertEqual(Variadics.FuncWithIDictGenSIKwargs(arg1=1, arg2=0), 'arg2')
        with self.assertRaises(TypeError) as cm:
            Variadics.FuncWithIDictGenSIKwargs(arg1=1.0)
        self.assertRegex(cm.exception.args[0], r"^Unable to cast keyword argument of type System\.Double to System\.Int32\.$")

    def test_irosidict(self):
        self.assertEqual(Variadics.FuncWithIRoDictGenSIKwargs(), 0)
        self.assertEqual(Variadics.FuncWithIRoDictGenSIKwargs(arg1=1), 1)
        self.assertEqual(Variadics.FuncWithIRoDictGenSIKwargs(arg1=1, arg2=2), 2)
        self.assertEqual(Variadics.FuncWithIRoDictGenSIKwargs(arg1=0, arg2=2), 'arg1')
        self.assertEqual(Variadics.FuncWithIRoDictGenSIKwargs(arg1=1, arg2=0), 'arg2')
        with self.assertRaises(TypeError) as cm:
            Variadics.FuncWithIRoDictGenSIKwargs(arg1=1.0)
        self.assertRegex(cm.exception.args[0], r"^Unable to cast keyword argument of type System\.Double to System\.Int32\.$")

    def test_bad(self):
        self.assertRaisesMessage(TypeError, "FuncWithIRoDictGenSOKwargs() takes no arguments (1 given)",
            Variadics.FuncWithIRoDictGenSOKwargs, {})

    def test_keyword_arg(self):
        clrdict = System.Collections.Generic.Dictionary[System.String, System.Object]()
        self.assertEqual(Variadics.FuncWithIRoDictGenSOKwargs(kwargs=clrdict), 1) # no binding by keyword
        self.assertEqual(Variadics.FuncWithIRoDictGenSOKwargs(kwargs={}), 1)

    def test_attribcol(self):
        self.assertRaisesMessage(SystemError, "Unsupported param dictionary type: System.ComponentModel.AttributeCollection",
         Variadics.FuncWithAttribColKwargs)

run_test(__name__)