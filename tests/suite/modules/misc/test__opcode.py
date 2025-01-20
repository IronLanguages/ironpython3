# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import _opcode
import sys

from iptest import IronPythonTestCase, is_cli, big, myint, run_test

class object_with_int:
    def __init__(self, value):
        self.value = value
    def __int__(self):
        return self.value

class object_with_index:
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class object_with_int_and_index:
    def __init__(self, int_value, index_value):
        self.int_value = int_value
        self.index_value = index_value
    def __int__(self):
        return self.int_value
    def __index__(self):
        return self.index_value

class OpcodeTest(IronPythonTestCase):

    def test_stack_effect_opcode_type(self):
        self.assertEqual(_opcode.stack_effect(12), 0)
        self.assertEqual(_opcode.stack_effect(big(12)), 0)
        self.assertEqual(_opcode.stack_effect(myint(12)), 0)

        if sys.version_info >= (3, 10):
            self.assertRaises(TypeError, _opcode.stack_effect, object_with_int(12))
        elif sys.version_info >= (3, 8):
            self.assertWarns(DeprecationWarning, _opcode.stack_effect, object_with_int(12))
        else:
            self.assertEqual(_opcode.stack_effect(object_with_int(12)), 0)

        if sys.version_info >= (3, 8):
            self.assertEqual(_opcode.stack_effect(object_with_index(12)), 0)
            # __index__ has priority over __int__
            self.assertEqual(_opcode.stack_effect(object_with_int_and_index(100, 12)), 0)
        else:
            self.assertRaises(TypeError, _opcode.stack_effect, object_with_index(12))

        self.assertRaises(OverflowError, _opcode.stack_effect, 1<<64)

    def test_stack_effect_oparg_type(self):
        self.assertRaises(ValueError, _opcode.stack_effect, 100)
        self.assertRaises(ValueError, _opcode.stack_effect, 100, None)

        self.assertEqual(_opcode.stack_effect(100, 12), 1)
        self.assertEqual(_opcode.stack_effect(100, big(12)), 1)
        self.assertEqual(_opcode.stack_effect(100, myint(12)), 1)

        if sys.version_info >= (3, 10):
            self.assertRaises(TypeError, _opcode.stack_effect, 100, object_with_int(12))
        elif sys.version_info >= (3, 8):
            self.assertWarns(DeprecationWarning, _opcode.stack_effect, 100, object_with_int(12))
        else:
            self.assertEqual(_opcode.stack_effect(100, object_with_int(12)), 1)

        if sys.version_info >= (3, 8) or is_cli:
            self.assertEqual(_opcode.stack_effect(100, object_with_index(12)), 1)
        else:
            self.assertRaises(TypeError, _opcode.stack_effect, 100, object_with_index(12))

        self.assertRaises(OverflowError, _opcode.stack_effect, 100, 1<<64)

run_test(__name__)
