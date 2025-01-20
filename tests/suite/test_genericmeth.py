# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class GenericMethTest(IronPythonTestCase):
    def setUp(self):
        super(GenericMethTest, self).setUp()

        self.load_iron_python_test()

    def assertDocEqual(self, received, expected):
        expected = expected.split(os.linesep)
        received = received.split(os.linesep)
        for x in received:
            if not x in expected:
                self.fail('Extra doc string: ' + x)
            index = expected.index(x)
            del expected[index]

        if expected: self.fail('Missing doc strings: ' + expected.join(', '))

    def test_generic_method_binding(self):
        from IronPythonTest import GenMeth

        # Create an instance of the generic method provider class.
        gm = GenMeth()

        # Check that the documentation strings for all the instance methods (they all have the same name) is as expected.
        expected = os.linesep.join([
            'InstMeth[T](self: GenMeth) -> str',
            'InstMeth[(T, U)](self: GenMeth) -> str',
            'InstMeth[T](self: GenMeth, arg1: Int32) -> str',
            'InstMeth[T](self: GenMeth, arg1: str) -> str',
            'InstMeth[(T, U)](self: GenMeth, arg1: Int32) -> str',
            'InstMeth[T](self: GenMeth, arg1: T) -> str',
            'InstMeth[(T, U)](self: GenMeth, arg1: T, arg2: U) -> str',
            'InstMeth(self: GenMeth) -> str',
            'InstMeth(self: GenMeth, arg1: Int32) -> str',
            'InstMeth(self: GenMeth, arg1: str) -> str']) + os.linesep

        self.assertDocEqual(gm.InstMeth.__doc__, expected)

        # And the same for the static methods.
        expected_static_methods = os.linesep.join([
                'StaticMeth[T]() -> str' ,
                'StaticMeth[(T, U)]() -> str' ,
                'StaticMeth[T](arg1: Int32) -> str' ,
                'StaticMeth[T](arg1: str) -> str' ,
                'StaticMeth[(T, U)](arg1: Int32) -> str' ,
                'StaticMeth[T](arg1: T) -> str' ,
                'StaticMeth[(T, U)](arg1: T, arg2: U) -> str' ,
                'StaticMeth() -> str' ,
                'StaticMeth(arg1: Int32) -> str' ,
                'StaticMeth(arg1: str) -> str']) + os.linesep

        self.assertDocEqual(GenMeth.StaticMeth.__doc__, expected_static_methods)

        # Check that we bind to the correct method based on type and call arguments for each of our instance methods. We can validate this
        # because each target method returns a unique string we can compare.
        self.assertEqual(gm.InstMeth(), "InstMeth()")
        self.assertEqual(gm.InstMeth[str](), "InstMeth<String>()")
        self.assertEqual(gm.InstMeth[(int, str)](), "InstMeth<BigInteger, String>()")
        self.assertEqual(gm.InstMeth(1), "InstMeth(Int32)")
        self.assertEqual(gm.InstMeth(""), "InstMeth(String)")
        self.assertEqual(gm.InstMeth[int](1), "InstMeth<BigInteger>(Int32)")
        #This ordering never worked, but new method binding rules reveal the bug.  Open a new bug here.
        #self.assertEqual(gm.InstMeth[str](""), "InstMeth<String>(String)")
        self.assertEqual(gm.InstMeth[(str, int)](1), "InstMeth<String, BigInteger>(Int32)")
        self.assertEqual(gm.InstMeth[GenMeth](gm), "InstMeth<GenMeth>(GenMeth)")
        self.assertEqual(gm.InstMeth[(str, int)]("", 1), "InstMeth<String, BigInteger>(String, BigInteger)")

        # And the same for the static methods.
        self.assertEqual(GenMeth.StaticMeth(), "StaticMeth()")
        self.assertEqual(GenMeth.StaticMeth[str](), "StaticMeth<String>()")
        self.assertEqual(GenMeth.StaticMeth[(int, str)](), "StaticMeth<BigInteger, String>()")
        self.assertEqual(GenMeth.StaticMeth(1), "StaticMeth(Int32)")
        self.assertEqual(GenMeth.StaticMeth(""), "StaticMeth(String)")
        self.assertEqual(GenMeth.StaticMeth[int](1), "StaticMeth<BigInteger>(Int32)")
        #self.assertEqual(GenMeth.StaticMeth[str](""), "StaticMeth<String>(String)")
        self.assertEqual(GenMeth.StaticMeth[(str, int)](1), "StaticMeth<String, BigInteger>(Int32)")
        self.assertEqual(GenMeth.StaticMeth[GenMeth](gm), "StaticMeth<GenMeth>(GenMeth)")
        self.assertEqual(GenMeth.StaticMeth[(str, int)]("", 1), "StaticMeth<String, BigInteger>(String, BigInteger)")

run_test(__name__)
