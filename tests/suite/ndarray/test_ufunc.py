# Ported from NumPy v2.5.4, numpy/_core/tests/test_ufunc.py, and adapted to CLR arrays.
# Copyright (c) 2005-2025, NumPy Developers.
# Licensed under the BSD 3-Clause License; see LICENSE_NUMPY.txt in this directory.
#
# Adaptations:
# * pytest-style classes derive from IronPythonTestCase; parametrized tests are loops with subTest.
# * 0-d arrays are replaced with scalars; cases that need a 0-d output are left out.
# * Element-wise `*` and `==` are spelled with function calls and `assert_array_equal`.
# * Functions not implemented yet (positive, vecdot) are left out.

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    from iptest import numpy_shim as np
    assert_raises = np.testing.assert_raises
    assert_array_equal = np.testing.assert_array_equal


@skipUnlessIronPython()
class TestUfuncKwargs(IronPythonTestCase):
    def test_kwarg_exact(self):
        assert_raises(TypeError, np.add, 1, 2, castingx='safe')
        assert_raises(TypeError, np.add, 1, 2, dtypex=int)
        assert_raises(TypeError, np.add, 1, 2, extobjx=[4096])
        assert_raises(TypeError, np.add, 1, 2, outx=None)
        assert_raises(TypeError, np.add, 1, 2, sigx='ii->i')
        assert_raises(TypeError, np.add, 1, 2, signaturex='ii->i')
        assert_raises(TypeError, np.add, 1, 2, subokx=False)
        assert_raises(TypeError, np.add, 1, 2, wherex=[True])

    def test_sig_signature(self):
        assert_raises(TypeError, np.add, 1, 2, sig='ii->i',
                      signature='ii->i')

    def test_sig_dtype(self):
        assert_raises(TypeError, np.add, 1, 2, sig='ii->i',
                      dtype=int)
        assert_raises(TypeError, np.add, 1, 2, signature='ii->i',
                      dtype=int)


@skipUnlessIronPython()
class TestUfunc(IronPythonTestCase):
    def test_out_broadcasts(self):
        # For ufuncs and gufuncs (not for reductions), we currently allow
        # the output to cause broadcasting of the input arrays.
        # both along dimensions with shape 1 and dimensions which do not
        # exist at all in the inputs.
        arr = np.reshape(np.arange(3), (1, 3))
        out = np.empty((5, 4, 3))
        np.add(arr, arr, out=out)
        assert_array_equal(out, np.broadcast_to(np.multiply(np.arange(3), 2), (5, 4, 3)))

    def test_out_broadcast_errors(self):
        # Output is (currently) allowed to broadcast inputs, but it cannot be
        # smaller than the actual result.
        for arr, out in [
                ([1, 2], np.empty(1)),
                (np.ones((4, 3)), np.empty((4, 1)))]:
            with self.subTest(arr=arr, out=np.shape(out)):
                with self.assertRaisesRegex(ValueError, "non-broadcastable"):
                    np.add(1.0, np.asarray(arr), out=out)


run_test(__name__)
