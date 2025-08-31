import unittest
import array

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test

class HashTest(IronPythonTestCase):
    def test_hash_before_eq(self):
        class HashBeforeEq:
            def __hash__(self):
                return 1
            def __eq__(self, other):
                return self is other

        x = HashBeforeEq()
        self.assertNotEqual(x.__hash__, None)
        self.assertEqual(hash(x), 1)

    def test_eq_before_hash(self):
        class EqBeforeHash:
            def __eq__(self, other):
                return self is other
            def __hash__(self):
                return 1

        x = EqBeforeHash()
        self.assertNotEqual(x.__hash__, None)
        self.assertEqual(hash(x), 1)

    def test_hash_writable_memoryviews(self):
        buffer = array.array('b', [1,2,3])
        self.assertRaises(ValueError, hash, memoryview(buffer))

run_test(__name__)
