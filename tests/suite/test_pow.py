import test.support, unittest

class PowTest(unittest.TestCase):
    # taken from test_complex.py
    # needed for negative real values with a fractional exponent    
    def assertAlmostEqual(self, a, b):
        if isinstance(a, complex):
            if isinstance(b, complex):
                unittest.TestCase.assertAlmostEqual(self, a.real, b.real)
                unittest.TestCase.assertAlmostEqual(self, a.imag, b.imag)
            else:
                unittest.TestCase.assertAlmostEqual(self, a.real, b)
                unittest.TestCase.assertAlmostEqual(self, a.imag, 0.)
        else:
            if isinstance(b, complex):
                unittest.TestCase.assertAlmostEqual(self, a, b.real)
                unittest.TestCase.assertAlmostEqual(self, 0., b.imag)
            else:
                unittest.TestCase.assertAlmostEqual(self, a, b)

    def pow_exponent_of_type_test(self, type):
        asseq = self.assertEqual
        if type == float:
            asseq = self.assertAlmostEqual

        # sanity check that when the exponent is of the given type the power operation is successful
        asseq(pow(3, type(3)), 27)
        asseq(3 ** type(3), 27)

        asseq(pow(type(3), type(3)), 27)
        asseq(type(3) ** type(3), 27)

    def test_pow_exponent_of_type_test_int(self):
        self.pow_exponent_of_type_test(int)

    def test_pow_exponent_of_type_test_long(self):
        self.pow_exponent_of_type_test(int)

    def test_pow_exponent_of_type_test_float(self):
        self.pow_exponent_of_type_test(float)

    def test_pow_negvaluefractionalexponent(self):
        # Support pow of negative numbers with fractional powers #504
        self.assertAlmostEqual((-1)**0.5, 1j)
        self.assertAlmostEqual(pow(-1, 0.5), 1j)

        self.assertAlmostEqual((-4)**0.5, 2j)
        self.assertAlmostEqual(pow(-4, 0.5), 2j)

        # 0.7071067811865476 = 0.5**0.5
        expected = 0.7071067811865476 + 0.7071067811865476j
        self.assertAlmostEqual((-1)**0.25, expected)
        self.assertAlmostEqual(pow(-1, 0.25), expected)

        self.assertAlmostEqual(pow(-1, 1/3), 0.5 + 0.8660254037844386j)
        self.assertAlmostEqual((-1)**(1/3), 0.5 + 0.8660254037844386j)

def test_main():
    test.support.run_unittest(PowTest)

if __name__ == "__main__":
    test_main()
