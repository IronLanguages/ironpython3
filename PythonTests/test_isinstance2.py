# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
        # check that isinstance(x, int) returns True on both BigInteger and Int32 values
        # and other type equivalences
        # PEP 237: int/long unification: https://www.python.org/dev/peps/pep-0237/
        # https://github.com/IronLanguages/ironpython3/issues/52

        i = 1        # Int32
        j = 1 << 64  # BigInteger

        self.assertTrue(isinstance(i, int))
        self.assertTrue(isinstance(i, int))

        self.assertTrue(isinstance(i, type(i)))
        self.assertTrue(isinstance(j, type(i)))

        self.assertTrue(isinstance(i, type(j)))
        self.assertTrue(isinstance(j, type(j)))

        # 'issubclass' equivalence
        self.assertTrue(issubclass(type(i), type(j)))
        self.assertTrue(issubclass(type(j), type(i)))
        self.assertTrue(issubclass(bool, int))

        # '__eq__' equivalence
        self.assertTrue(type(i) == int)
        self.assertTrue(type(j) == int)
        self.assertTrue(type(i) == type(j))

        # 'is' equivalence
        self.assertTrue(type(i) is int)
        self.assertTrue(type(j) is int)
        self.assertTrue(type(i) is type(j))

        # 'id' equivalence
        self.assertEqual(id(type(i)), id(type(j)))

    def test_isinstance_subint(self):
        # Test int type equivalence involving subclasses
        class SubInt(int): # Extensible<BigInteger>
            pass

        i = 1        # Int32
        j = 1 << 64  # BigInteger

        ki = SubInt(i)
        self.assertTrue(isinstance(ki, SubInt))
        self.assertTrue(isinstance(ki, int))
        self.assertTrue(isinstance(ki, type(i)))
        self.assertTrue(isinstance(ki, type(j)))
        self.assertTrue(isinstance(ki, type(ki)))
        self.assertFalse(isinstance(i, type(ki)))
        self.assertFalse(isinstance(j, type(ki)))

        kj = SubInt(j)
        self.assertTrue(isinstance(kj, SubInt))
        self.assertTrue(isinstance(kj, int))
        self.assertTrue(isinstance(kj, type(i)))
        self.assertTrue(isinstance(kj, type(j)))
        self.assertTrue(isinstance(kj, type(kj)))
        self.assertFalse(isinstance(i, type(kj)))
        self.assertFalse(isinstance(j, type(kj)))

        self.assertTrue(issubclass(SubInt, int))
        self.assertTrue(issubclass(SubInt, type(i)))
        self.assertTrue(issubclass(SubInt, type(j)))
        self.assertTrue(issubclass(SubInt, type(ki)))
        self.assertTrue(issubclass(SubInt, type(kj)))

        self.assertFalse(issubclass(int, SubInt))
        self.assertFalse(issubclass(type(i), SubInt))
        self.assertFalse(issubclass(type(j), SubInt))
        self.assertFalse(issubclass(bool, SubInt))
        self.assertFalse(issubclass(SubInt, bool))

    def test_isinstance_tuple_subclass(self):
        """https://github.com/IronLanguages/ironpython3/issues/1255"""
        class T(tuple):
            def __iter__(self):
                yield self

        # isinstance should not be invoking __iter__ on the subclass
        self.assertFalse(isinstance(3, T()))

if __name__ == '__main__':
    unittest.main()
