import unittest

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test

class HashTest(IronPythonTestCase):
    def test_hash_before_eq(self):
        class HashBeforeEq:
            def __hash__(self):
                return 1
            def __eq__(self, other):
                return self is other

        x = HashBeforeEq()
        self.assertNotEquals(x.__hash__, None)
        self.assertEquals(hash(x), 1)

    def test_eq_before_hash(self):
        class EqBeforeHash:
            def __eq__(self, other):
                return self is other
            def __hash__(self):
                return 1

        x = EqBeforeHash()
        self.assertNotEquals(x.__hash__, None)
        self.assertEquals(hash(x), 1)

run_test(__name__)
