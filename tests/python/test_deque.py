# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from _collections import deque
from iptest import IronPythonTestCase, big, myint, myfloat, mycomplex, run_test

class DequeTest(IronPythonTestCase):
    def test_deque_cmp_empty(self):
        """https://github.com/IronLanguages/ironpython3/pull/973"""
        class AlwaysLessThan:
            def __lt__(self, other):
                return True

        self.assertFalse(deque([AlwaysLessThan()]) < deque())

    def test_deque_equality_issue(self):
        """https://github.com/IronLanguages/ironpython3/issues/1216"""
        self.assertNotEqual(deque(range(8)), deque([1]*8))

    def test_maxlen_type(self):
        self.assertEqual(deque([], 42).maxlen, 42)
        self.assertEqual(deque([], big(42)).maxlen, 42)
        self.assertEqual(deque([], myint(42)).maxlen, 42)

        self.assertRaisesMessage(TypeError, "an integer is required", deque, [], 1.2)
        self.assertRaisesMessage(TypeError, "an integer is required", deque, [], myfloat(1))
        self.assertRaisesMessage(TypeError, "an integer is required", deque, [], mycomplex(1))
        self.assertRaisesMessage(TypeError, "an integer is required", deque, [], "1")

        class int_with_int(int):
            def __int__(self): return 42
        self.assertEqual(deque([], int_with_int(24)).maxlen, 24)

        class int_with_index(int):
            def __index__(self): return 42
        self.assertEqual(deque([], int_with_index(24)).maxlen, 24)

        class object_with_int:
            def __int__(self): return 42
        self.assertRaisesMessage(TypeError, "an integer is required", deque, [], object_with_int())

        class object_with_index:
            def __index__(self): return 42
        self.assertRaisesMessage(TypeError, "an integer is required", deque, [], object_with_index())

    def test_maxlen_value(self):
        self.assertEqual(deque([]).maxlen, None)
        self.assertEqual(deque([], None).maxlen, None)
        self.assertEqual(deque([], 0).maxlen, 0)
        self.assertRaises(OverflowError, deque, [], 1<<64)
        self.assertRaises(OverflowError, deque, [], -1<<64)
        self.assertRaisesMessage(ValueError, "maxlen must be non-negative", deque, [], -1)

run_test(__name__)
