import test.support, unittest

class UnpackTest(unittest.TestCase):
    def assertRaisesSyntaxError(self, func, expectedMessage, lineno):
        with self.assertRaises(SyntaxError) as context:
            func()
        self.assertEqual(context.exception.msg, expectedMessage, "Error raised, but wrong message")
        self.assertEqual(context.exception.lineno, lineno, "Error raised, but on the wrong line")

    def test_unpack_into_exprlist_1(self):
        *a, = range(2)
        self.assertEqual(a, [0, 1])

    def test_unpack_into_exprlist_2(self):
        *a, b, c = range(6)
        self.assertEqual(a, [0, 1, 2, 3])
        self.assertEqual(b, 4)
        self.assertEqual(c, 5)
    
    def test_unpack_into_exprlist_3(self):
        a, *b, c = range(6)
        self.assertEqual(a, 0)
        self.assertEqual(b, [1, 2, 3, 4])
        self.assertEqual(c, 5)

    def test_unpack_into_exprlist_4(self):
        a, b, *c = range(6)
        self.assertEqual(a, 0)
        self.assertEqual(b, 1)
        self.assertEqual(c, [2, 3, 4, 5])

    def test_unpack_into_exprlist_5(self):
        a, b, *c, d = e, *f, g, h = range(6)
        self.assertEqual(a, 0)
        self.assertEqual(b, 1)
        self.assertEqual(c, [2, 3, 4])
        self.assertEqual(d, 5)

        self.assertEqual(e, 0)
        self.assertEqual(f, [1, 2, 3])
        self.assertEqual(g, 4)
        self.assertEqual(h, 5)
        
    def test_unpack_into_list_1(self):
        [*a] = range(2)
        self.assertEqual(a, [0, 1])

    def test_unpack_into_list_2(self):
        [*a, b] = range(2)
        self.assertEqual(a, [0])
        self.assertEqual(b, 1)

    def test_unpack_into_list_3(self):
        [a, *b, c] = range(5)
        self.assertEqual(a, 0)
        self.assertEqual(b, [1, 2, 3])
        self.assertEqual(c, 4)

    def test_unpack_into_for_target_1(self):
        index = 0
        for a, *b in enumerate(range(3)):
            self.assertEqual(a, index)
            self.assertEqual(b, [index])
            index = index + 1

    def test_unpack_into_for_target_2(self):
        index = 0
        for *a, b in enumerate(range(3)):
            self.assertEqual(b, index)
            self.assertEqual(a, [index])
            index = index + 1
    
    def test_unpack_into_for_target_3(self):
        index = 0
        expected_a = [1, 4]
        expected_b = [[2], [8, 3]]
        expected_c = [3, 1]

        for a, *b, c in [(1, 2, 3), (4, 8, 3, 1)]:
            self.assertEqual(a, expected_a[index])
            self.assertEqual(b, expected_b[index])
            self.assertEqual(c, expected_c[index])
            index = index + 1

    def test_too_many_starred_assignments(self):
        self.assertRaisesSyntaxError(lambda: exec("*x, *k = range(5)"), "two starred expressions in assignment", 1)
        self.assertRaisesSyntaxError(lambda: exec("*x, *k, *r = range(5)"), "two starred expressions in assignment", 1)
        self.assertRaisesSyntaxError(lambda: exec("v, *x, n, *k = range(5)"), "two starred expressions in assignment", 1)
        self.assertRaisesSyntaxError(lambda: exec("h, t, *g, s, *x, m, *k = range(5)"), "two starred expressions in assignment", 1)

        self.assertRaisesSyntaxError(lambda: exec("[*x, *k] = range(5)"), "two starred expressions in assignment", 1)
        self.assertRaisesSyntaxError(lambda: exec("[*x, *k, *r] = range(5)"), "two starred expressions in assignment", 1)
        self.assertRaisesSyntaxError(lambda: exec("[v, *x, n, *k] = range(5)"), "two starred expressions in assignment", 1)
        self.assertRaisesSyntaxError(lambda: exec("[h, t, *g, s, *x, m, *k] = range(5)"), "two starred expressions in assignment", 1)

def test_main():
    test.support.run_unittest(UnpackTest)

if __name__ == "__main__":
    test_main()
