import test.support, unittest

class UnpackTest(unittest.TestCase):
    def assertRaisesSyntaxError(self, func, expectedMessage, lineno):
        with self.assertRaises(SyntaxError) as context:
            func()
        self.assertEqual(context.exception.msg, expectedMessage, "Error raised, but wrong message")
        self.assertEqual(context.exception.lineno, lineno, "Error raised, but on the wrong line")
        
    def test_unpack_into_list_1(self):
        [*a] = range(2)
        self.assertSequenceEqual(a, range(2))

    def test_unpack_into_list_2(self):
        [*a, b] = range(2)
        self.assertSequenceEqual(a, [0])
        self.assertEqual(b, 1)

    def test_unpack_into_list_3(self):
        [a, *b, c] = range(5)
        self.assertEqual(a, 0)
        self.assertEqual(b, [1, 2, 3])
        self.assertEqual(c, 4)

def test_main():
    test.support.run_unittest(UnpackTest)

if __name__ == "__main__":
    test_main()
