# Ported from NumPy v2.5.4, numpy/_core/tests/test_numeric.py, and adapted to CLR arrays.
# Copyright (c) 2005-2025, NumPy Developers.
# Licensed under the BSD 3-Clause License; see LICENSE_NUMPY.txt in this directory.
#
# Adaptations:
# * pytest-style classes derive from IronPythonTestCase.
# * `x.dtype` is spelled `np.dtype_of(x)`; element-wise `*` is spelled `np.multiply`.
# * Types without a CLR equivalent (longdouble, complex64, clongdouble) are left out; float16 (System.Half)
#   is tested only where available, and its values are created with clr.Convert.
# * CLR values of types Boolean, Int32, Double and Complex are Python scalars, which are weak in type
#   promotion, unlike NumPy scalars of the same types. Where the original test uses such a NumPy
#   scalar as a strong operand, a 1-element array is used instead.
# * np.result_type is not implemented yet, so test_result_type is left out.

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    import clr
    from iptest import numpy_shim as np
    assert_equal = np.testing.assert_equal


@skipUnlessIronPython()
class TestTypes(IronPythonTestCase):
    def check_promotion_cases(self, promote_func):
        # tests that the scalars get coerced correctly.
        b = np.array([False])  # np.bool(0)
        i8, i16, i32, i64 = np.int8(0), np.int16(0), np.array([0], np.int32), np.int64(0)
        u8, u16, u32, u64 = np.uint8(0), np.uint16(0), np.uint32(0), np.uint64(0)
        f32, f64 = np.float32(0), np.array([0], np.float64)
        c128 = np.array([0], np.complex128)

        # coercion within the same kind
        assert_equal(promote_func(i8, i16), np.dtype(np.int16))
        assert_equal(promote_func(i32, i8), np.dtype(np.int32))
        assert_equal(promote_func(i16, i64), np.dtype(np.int64))
        assert_equal(promote_func(u8, u32), np.dtype(np.uint32))
        assert_equal(promote_func(f32, f64), np.dtype(np.float64))

        # coercion between kinds
        assert_equal(promote_func(b, i32), np.dtype(np.int32))
        assert_equal(promote_func(b, u8), np.dtype(np.uint8))
        assert_equal(promote_func(i8, u8), np.dtype(np.int16))
        assert_equal(promote_func(u8, i32), np.dtype(np.int32))
        assert_equal(promote_func(i64, u32), np.dtype(np.int64))
        assert_equal(promote_func(u64, i32), np.dtype(np.float64))
        assert_equal(promote_func(i32, f32), np.dtype(np.float64))
        assert_equal(promote_func(i64, f32), np.dtype(np.float64))
        assert_equal(promote_func(f32, i16), np.dtype(np.float32))
        assert_equal(promote_func(f32, u32), np.dtype(np.float64))
        assert_equal(promote_func(c128, f32), np.dtype(np.complex128))

        # coercion between scalars and 1-D arrays
        assert_equal(promote_func(np.array([b]), i8), np.dtype(np.int8))
        assert_equal(promote_func(np.array([b]), u8), np.dtype(np.uint8))
        assert_equal(promote_func(np.array([b]), i32), np.dtype(np.int32))
        assert_equal(promote_func(np.array([b]), u32), np.dtype(np.uint32))
        assert_equal(promote_func(np.array([i8]), i64), np.dtype(np.int64))
        # unsigned and signed unfortunately tend to promote to float64:
        assert_equal(promote_func(u64, np.array([i32])), np.dtype(np.float64))
        assert_equal(promote_func(i64, np.array([u32])), np.dtype(np.int64))
        assert_equal(promote_func(np.array([u16]), i32), np.dtype(np.int32))
        assert_equal(promote_func(np.array([-1], np.int32), np.array([u64])),
                     np.dtype(np.float64))
        assert_equal(promote_func(f64, np.array([f32])), np.dtype(np.float64))
        assert_equal(promote_func(np.array([f32]), c128),
                     np.dtype(np.complex128))

        # coercion between scalars and 1-D arrays, where
        # the scalar has greater kind than the array
        assert_equal(promote_func(np.array([b]), f64), np.dtype(np.float64))
        assert_equal(promote_func(np.array([b]), i64), np.dtype(np.int64))
        assert_equal(promote_func(np.array([b]), u64), np.dtype(np.uint64))
        assert_equal(promote_func(np.array([i8]), f64), np.dtype(np.float64))
        assert_equal(promote_func(np.array([u16]), f64), np.dtype(np.float64))

    def test_coercion(self):
        def res_type(a, b):
            return np.dtype_of(np.add(a, b))

        self.check_promotion_cases(res_type)

        # Use-case: float/complex scalar * bool/int8 array
        #           shouldn't narrow the float/complex type
        for a in [np.array([True, False]), np.array([-3, 12], dtype=np.int8)]:
            b = np.multiply(1.234, a)
            assert_equal(np.dtype_of(b), np.dtype('f8'), f"array type {np.dtype_of(a)}")
            b = np.multiply(np.float64(1.234), a)
            assert_equal(np.dtype_of(b), np.dtype('f8'), f"array type {np.dtype_of(a)}")
            b = np.multiply(np.float32(1.234), a)
            assert_equal(np.dtype_of(b), np.dtype('f4'), f"array type {np.dtype_of(a)}")
            if np.HAS_FLOAT16:
                b = np.multiply(clr.Convert(1.234, np.float16), a)
                assert_equal(np.dtype_of(b), np.dtype('f2'), f"array type {np.dtype_of(a)}")

            b = np.multiply(1.234j, a)
            assert_equal(np.dtype_of(b), np.dtype('c16'), f"array type {np.dtype_of(a)}")
            b = np.multiply(np.complex128(1.234j), a)
            assert_equal(np.dtype_of(b), np.dtype('c16'), f"array type {np.dtype_of(a)}")


run_test(__name__)
