# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test the NumPy-like interface of CLR arrays (System.Array)
##
## Tests ported from NumPy are in ndarray/.
##

import unittest

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    import clr
    import System

def array2d(elemType, rows, bases=None):
    a = System.Array.CreateInstance(elemType, (len(rows), len(rows[0])), bases or (0, 0))
    for i, row in enumerate(rows):
        for j, item in enumerate(row):
            a[i + a.GetLowerBound(0), j + a.GetLowerBound(1)] = item
    return a

@skipUnlessIronPython()
class NDArrayFunctionsTest(IronPythonTestCase):

    def test_add_multiply_issue_examples(self):
        # https://github.com/IronLanguages/ironpython3/issues/1887
        add, multiply = System.Array.add, System.Array.multiply
        Array = System.Array

        self.assertEqual(add(Array[int]([1, 2]), 2), Array[int]([3, 4]))
        self.assertEqual(add(Array[int]([1, 2]), Array[int]([3, 4])), Array[int]([4, 6]))
        self.assertEqual(add(array2d(int, [[1, 2], [3, 4]]), Array[int]([10, 20])), array2d(int, [[11, 22], [13, 24]]))

        self.assertEqual(multiply(Array[int]([1, 2]), 2), Array[int]([2, 4]))
        self.assertEqual(multiply(Array[int]([1, 2]), Array[int]([3, 4])), Array[int]([3, 8]))
        self.assertEqual(multiply(array2d(int, [[1, 2], [3, 4]]), Array[int]([10, 20])), array2d(int, [[10, 40], [30, 80]]))

    def test_add_broadcasting(self):
        add, Array = System.Array.add, System.Array

        col = array2d(int, [[1], [2]])
        self.assertEqual(add(col, Array[int]([10, 20, 30])), array2d(int, [[11, 21, 31], [12, 22, 32]]))
        self.assertEqual(add(Array[int]([10, 20, 30]), col), array2d(int, [[11, 21, 31], [12, 22, 32]]))
        self.assertEqual(add(1, Array[int]([1, 2])), Array[int]([2, 3]))

        a3 = System.Array.CreateInstance(int, 2, 1, 3)
        r = add(a3, array2d(int, [[1, 2, 3], [4, 5, 6]]))
        self.assertEqual([r.GetLength(d) for d in range(r.Rank)], [2, 2, 3])
        self.assertEqual(r[1, 1, 2], 6)

        with self.assertRaisesRegex(ValueError, r"^operands could not be broadcast together with shapes \(2,\) \(3,\) $"):
            add(Array[int]([1, 2]), Array[int]([1, 2, 3]))
        with self.assertRaisesRegex(ValueError, r"shapes \(2,1\) \(3,2\)"):
            add(col, array2d(int, [[1, 2], [3, 4], [5, 6]]))

        empty = System.Array.CreateInstance(int, 0)
        self.assertEqual(add(empty, 1), empty)
        self.assertEqual(add(System.Array.CreateInstance(int, 0, 2), Array[int]([1, 2])).GetLength(1), 2)

    def test_add_scalars(self):
        add = System.Array.add

        self.assertEqual(add(2, 3), 5)
        self.assertEqual(add(2.5, 1), 3.5)
        self.assertEqual(add('a', 'b'), 'ab')
        # big Python ints are not converted
        self.assertEqual(add(2**100, 1), 2**100 + 1)
        # a CLR scalar of a type that is not a Python scalar type determines the result type
        r = add(System.Int64(1), 2)
        self.assertEqual(r, 3)
        self.assertIsInstance(r, System.Int64)
        self.assertIsInstance(add(System.Int16(1), System.Single(2)), System.Single)
        self.assertIsInstance(add(2, 3, dtype=float), float)

    def test_add_result_type(self):
        add, multiply, Array = System.Array.add, System.Array.multiply, System.Array
        BigInteger, Complex = int, complex  # their CLR types are System.Numerics.BigInteger and Complex

        def elemType(a):
            return a.GetType().GetElementType()

        for x1, x2, expected in [
                (Array[System.Int16]([1]), Array[System.Single]([1]), System.Single),
                (Array[System.Int32]([1]), Array[System.Single]([1]), System.Double),
                (Array[System.UInt64]([1]), Array[System.Int32]([1]), System.Double),
                (Array[System.UInt64]([1]), Array[BigInteger]([1]), BigInteger),
                (Array[System.Byte]([1]), Array[System.SByte]([1]), System.Int16),
                (Array[System.UInt32]([1]), Array[System.Int32]([1]), System.Int64),
                (Array[bool]([True]), Array[System.Byte]([1]), System.Byte),
                (Array[System.Single]([1]), Array[Complex]([1]), Complex),
                (Array[System.Byte]([1]), 1, System.Byte),          # weak int
                (Array[System.Int32]([1]), 2**20, System.Int32),    # weak int
                (Array[System.Single]([1]), 1.5, System.Single),    # weak float
                (Array[System.Int16]([1]), 1.5, System.Double),     # weak float of higher kind
                (Array[System.Single]([1]), 1j, Complex),           # weak complex of higher kind
                (Array[bool]([True]), 1, BigInteger),               # weak int of higher kind: Python int
                (Array[bool]([True]), True, System.Boolean),
                (Array[str](['a']), 'b', System.String),
                (Array[System.Decimal]([1]), Array[System.Decimal]([1]), System.Decimal),
                (Array[System.Decimal]([1]), 1, object),
                (Array[System.Decimal]([1]), Array[int]([1]), object),
                (Array[object]([1]), 1, object),
                (Array[System.TimeSpan]([System.TimeSpan.Zero]), Array[System.TimeSpan]([System.TimeSpan.Zero]), object),
            ]:
            with self.subTest(x1=x1, x2=x2):
                self.assertEqual(elemType(add(x1, x2)), clr.GetClrType(expected))
                self.assertEqual(elemType(add(x2, x1)), clr.GetClrType(expected))

        # NumPy: add is logical or, multiply is logical and on bools
        t, f = Array[bool]([True, True, False, False]), Array[bool]([True, False, True, False])
        self.assertEqual(add(t, f), Array[bool]([True, True, True, False]))
        self.assertEqual(multiply(t, f), Array[bool]([True, False, False, False]))

        self.assertEqual(add(Array[str](['a', 'b']), 'c'), Array[str](['ac', 'bc']))
        self.assertEqual(add(Array[object]([[1], 'a']), Array[object]([[2], 'b'])), Array[object]([[1, 2], 'ab']))

    @unittest.skipUnless(is_cli and hasattr(System, 'Half'), "System.Half requires .NET 7 or later")
    def test_add_half(self):
        add, multiply, Array = System.Array.add, System.Array.multiply, System.Array
        Half = System.Half

        def elemType(a):
            return a.GetType().GetElementType()

        # NumPy's float16
        for x2, expected in [
                (Array[Half]([1]), Half),
                (Array[System.SByte]([1]), Half),
                (Array[System.Byte]([1]), Half),
                (Array[System.Int16]([1]), System.Single),
                (Array[System.Int32]([1]), System.Double),
                (Array[System.Single]([1]), System.Single),
                (Array[bool]([True]), Half),
                (1, Half),      # weak int
                (1.5, Half),    # weak float
                (1j, complex),  # weak complex of higher kind
            ]:
            with self.subTest(x2=x2):
                self.assertEqual(elemType(add(Array[Half]([1]), x2)), clr.GetClrType(expected))
                self.assertEqual(elemType(add(x2, Array[Half]([1]))), clr.GetClrType(expected))

        # operands are cast to Half before the operation, which IronPython supports only between Half values
        self.assertEqual(add(Array[Half]([1.5, 2]), 1), Array[Half]([2.5, 3]))
        self.assertEqual(multiply(Array[Half]([1.5, 2]), Array[System.Byte]([2, 3])), Array[Half]([3, 6]))
        self.assertEqual(add(Array[Half]([1.5]), 1, dtype=float), Array[float]([2.5]))
        self.assertIsInstance(add(clr.Convert(1.5, Half), 1), Half)

    def test_add_overflow(self):
        add, multiply, Array = System.Array.add, System.Array.multiply, System.Array

        # unlike NumPy, values that do not fit into the result type raise an error rather than wrap around
        self.assertRaises(OverflowError, add, Array[System.Int32]([2**31 - 1]), 1)
        self.assertRaises(OverflowError, add, Array[System.Byte]([200]), 100)
        self.assertRaises(OverflowError, add, Array[System.Byte]([0]), -1)
        self.assertRaises(OverflowError, multiply, Array[System.Int64]([2**62]), 2)
        self.assertEqual(add(Array[int]([2**31 - 1]), 1), Array[int]([2**31]))

    def test_add_dtype(self):
        add, Array = System.Array.add, System.Array

        self.assertEqual(add(Array[int]([1, 2]), 1, dtype=float), Array[float]([2.0, 3.0]))
        self.assertEqual(add(Array[int]([1, 2]), 1, dtype=System.Int16), Array[System.Int16]([2, 3]))
        self.assertEqual(add(Array[int]([1, 2]), 1, dtype=None), Array[int]([2, 3]))
        self.assertEqual(add(Array[int]([1, 2]), 1, dtype=object), Array[object]([2, 3]))
        self.assertRaises(OverflowError, add, Array[int]([1, 2]), 255, dtype=System.Byte)

        # NumPy's casting rule 'same_kind'
        with self.assertRaisesRegex(TypeError, r"^Cannot cast ufunc 'add' input 0 from float to int with casting rule 'same_kind'$"):
            add(Array[float]([1.5]), 1, dtype=int)
        with self.assertRaisesRegex(TypeError, r"input 1 from float to int"):
            add(Array[int]([1]), 1.5, dtype=int)
        self.assertRaises(TypeError, add, Array[int]([1]), 1, dtype=bool)
        self.assertRaises(TypeError, add, Array[str](['a']), 'b', dtype=int)
        self.assertRaises(TypeError, add, Array[int]([1]), 1, dtype=str)

        self.assertRaises(TypeError, add, Array[int]([1]), 1, typo=None)

    def test_add_out(self):
        add, Array = System.Array.add, System.Array

        out = Array[float]([0.0, 0.0])
        self.assertIs(add(Array[int]([1, 2]), 1, out), out)
        self.assertEqual(out, Array[float]([2.0, 3.0]))
        self.assertIs(add(Array[int]([1, 2]), 2, out=out), out)
        self.assertEqual(out, Array[float]([3.0, 4.0]))
        self.assertIs(add(Array[int]([1, 2]), 3, out=(out,)), out)
        self.assertEqual(out, Array[float]([4.0, 5.0]))
        self.assertIs(add(1, 2, out=out), out)
        self.assertEqual(out, Array[float]([3.0, 3.0]))
        self.assertEqual(add(Array[int]([1, 2]), 1, out=None), Array[int]([2, 3]))
        self.assertEqual(add(Array[int]([1, 2]), 1, out=(None,)), Array[int]([2, 3]))

        # out may be of a lower type of the same kind, and broadcasts the operands
        out = System.Array.CreateInstance(System.Int16, 2, 2)
        add(Array[int]([1, 2]), 1, out=out)
        self.assertEqual(out, array2d(System.Int16, [[2, 3], [2, 3]]))

        with self.assertRaisesRegex(TypeError, r"^Cannot cast ufunc 'add' output from float to Int16 with casting rule 'same_kind'$"):
            add(Array[int]([1, 2]), 1.5, out=out)
        with self.assertRaisesRegex(ValueError, r"^non-broadcastable output operand with shape \(1,\) doesn't match the broadcast shape \(2,\)$"):
            add(Array[int]([1, 2]), 1, out=Array[int]([0]))
        self.assertRaises(TypeError, add, Array[int]([1, 2]), 1, out=[0, 0])
        self.assertRaises(ValueError, add, Array[int]([1, 2]), 1, out=(None, None))

    def test_add_lower_bounds(self):
        add, Array = System.Array.add, System.Array

        a = Array[int]((1, 2), base=1)
        self.assertEqual(add(a, 1), Array[int]((2, 3), base=1))
        self.assertEqual(add(a, a), Array[int]((2, 4), base=1))
        self.assertEqual(add(a, Array[int]((10,), base=1)), Array[int]((11, 12), base=1))

        m = array2d(int, [[1, 2], [3, 4]], bases=(5, 1))
        r = add(m, a)
        self.assertEqual([r.GetLowerBound(0), r.GetLowerBound(1)], [5, 1])
        self.assertEqual(r, array2d(int, [[2, 4], [4, 6]], bases=(5, 1)))

        with self.assertRaisesRegex(ValueError, "lower bounds"):
            add(a, Array[int]([1, 2]))
        with self.assertRaisesRegex(ValueError, "lower bounds"):
            add(a, 1, out=Array[int]([0, 0]))

    def test_add_unsupported_operands(self):
        add, Array = System.Array.add, System.Array

        # NumPy converts sequences to arrays; this is not supported yet
        self.assertRaises(TypeError, add, Array[int]([1, 2]), [1, 2])
        self.assertRaises(TypeError, add, (1, 2), Array[int]([1, 2]))
        self.assertRaises(TypeError, add, Array[int]([1]), None)
        self.assertRaises(TypeError, add, Array[int]([1]))

    def test_operators_unchanged(self):
        # operators + and * keep their legacy semantics (concatenation and repetition)
        a = System.Array[int]([1, 2])
        self.assertEqual(a + a, System.Array[int]([1, 2, 1, 2]))
        self.assertEqual(a * 2, System.Array[int]([1, 2, 1, 2]))

run_test(__name__)
