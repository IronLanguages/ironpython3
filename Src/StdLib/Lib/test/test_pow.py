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

    def powtest(self, type):
        if type != float:
            for i in range(-1000, 1000):
                self.assertEqual(pow(type(i), 0), 1)
                self.assertEqual(pow(type(i), 1), type(i))
                self.assertEqual(pow(type(0), 1), type(0))
                self.assertEqual(pow(type(1), 1), type(1))

            for i in range(-100, 100):
                self.assertEqual(pow(type(i), 3), i*i*i)

            pow2 = 1
            for i in range(0, 31):
                self.assertEqual(pow(2, i), pow2)
                if i != 30 : pow2 = pow2*2

            for othertype in (int,):
                for i in list(range(-10, 0)) + list(range(1, 10)):
                    ii = type(i)
                    for j in range(1, 11):
                        jj = -othertype(j)
                        pow(ii, jj)

        for othertype in int, float:
            for i in range(1, 100):
                zero = type(0)
                exp = -othertype(i/10.0)
                if exp == 0:
                    continue
                self.assertRaises(ZeroDivisionError, pow, zero, exp)

        il, ih = -20, 20
        jl, jh = -5,   5
        kl, kh = -10, 10
        asseq = self.assertEqual
        if type == float:
            il = 1
            asseq = self.assertAlmostEqual
        elif type == int:
            jl = 0
        elif type == int:
            jl, jh = 0, 15
        for i in range(il, ih+1):
            for j in range(jl, jh+1):
                for k in range(kl, kh+1):
                    if k != 0:
                        if type == float or j < 0:
                            self.assertRaises(TypeError, pow, type(i), j, k)
                            continue
                        asseq(
                            pow(type(i),j,k),
                            pow(type(i),j)% type(k)
                        )

        # sanity check that when the exponent is of the given type the power operation is successful
        asseq(pow(3, type(3)), 27)
        asseq(3 ** type(3), 27)

        asseq(pow(type(3), type(3)), 27)
        asseq(type(3) ** type(3), 27)

    def test_powint(self):
        self.powtest(int)

    def test_powlong(self):
        self.powtest(int)

    def test_powfloat(self):
        self.powtest(float)

    def test_other(self):
        # Other tests-- not very systematic
        self.assertEqual(pow(3,3) % 8, pow(3,3,8))
        self.assertEqual(pow(3,3) % -8, pow(3,3,-8))
        self.assertEqual(pow(3,2) % -2, pow(3,2,-2))
        self.assertEqual(pow(-3,3) % 8, pow(-3,3,8))
        self.assertEqual(pow(-3,3) % -8, pow(-3,3,-8))
        self.assertEqual(pow(5,2) % -8, pow(5,2,-8))

        self.assertEqual(pow(3,3) % 8, pow(3,3,8))
        self.assertEqual(pow(3,3) % -8, pow(3,3,-8))
        self.assertEqual(pow(3,2) % -2, pow(3,2,-2))
        self.assertEqual(pow(-3,3) % 8, pow(-3,3,8))
        self.assertEqual(pow(-3,3) % -8, pow(-3,3,-8))
        self.assertEqual(pow(5,2) % -8, pow(5,2,-8))

        for i in range(-10, 11):
            for j in range(0, 6):
                for k in range(-7, 11):
                    if j >= 0 and k != 0:
                        self.assertEqual(
                            pow(i,j) % k,
                            pow(i,j,k)
                        )
                    if j >= 0 and k != 0:
                        self.assertEqual(
                            pow(int(i),j) % k,
                            pow(int(i),j,k)
                        )

    def test_bug643260(self):
        class TestRpow:
            def __rpow__(self, other):
                return None
        None ** TestRpow() # Won't fail when __rpow__ invoked.  SF bug #643260.

    def test_bug705231(self):
        # -1.0 raised to an integer should never blow up.  It did if the
        # platform pow() was buggy, and Python didn't worm around it.
        eq = self.assertEqual
        a = -1.0
        # The next two tests can still fail if the platform floor()
        # function doesn't treat all large inputs as integers
        # test_math should also fail if that is happening
        eq(pow(a, 1.23e167), 1.0)
        eq(pow(a, -1.23e167), 1.0)
        for b in range(-10, 11):
            eq(pow(a, float(b)), b & 1 and -1.0 or 1.0)
        for n in range(0, 100):
            fiveto = float(5 ** n)
            # For small n, fiveto will be odd.  Eventually we run out of
            # mantissa bits, though, and thereafer fiveto will be even.
            expected = fiveto % 2.0 and -1.0 or 1.0
            eq(pow(a, fiveto), expected)
            eq(pow(a, -fiveto), expected)
        eq(expected, 1.0)   # else we didn't push fiveto to evenness

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
