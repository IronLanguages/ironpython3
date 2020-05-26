# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

class IsInstanceTest(unittest.TestCase):

    def test_isinstance_metaclass(self):
        class AlwaysFalse(type):
            def __instancecheck__(cls, instance):
                return False

        class A(metaclass=AlwaysFalse):
            pass

        self.assertFalse(isinstance(int, A))
        self.assertTrue(isinstance(A(), A)) # does not call __instancecheck__

        class AlwaysTrue(type):
            def __instancecheck__(cls, instance):
                return True

        class B(metaclass=AlwaysTrue):
            pass

        self.assertTrue(isinstance(int, B))
        self.assertTrue(isinstance(B(), B)) # does not call __instancecheck__

    def test_isinstance_bigint(self):
        # check that isinstance(x, int) returns True on BigInteger values
        l = sys.maxsize + 1
        if sys.implementation.name == "ironpython":
            # https://github.com/IronLanguages/ironpython3/issues/52
            self.assertNotEqual(type(0), type(l))
        self.assertTrue(isinstance(l, int))

if __name__ == '__main__':
    unittest.main()
