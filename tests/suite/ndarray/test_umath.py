# Ported from NumPy v2.5.4, numpy/_core/tests/test_umath.py, and adapted to CLR arrays.
# Copyright (c) 2005-2025, NumPy Developers.
# Licensed under the BSD 3-Clause License; see LICENSE_NUMPY.txt in this directory.
#
# Adaptations:
# * pytest-style classes derive from IronPythonTestCase.
# * 0-d arrays are replaced with 1-element arrays.
# * Parameters that IronPython does not support (subok) and functions not implemented yet (frexp)
#   are left out, as are the checks of read-only arrays (CLR arrays are always writeable).

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    from iptest import numpy_shim as np
    assert_ = np.testing.assert_
    assert_raises = np.testing.assert_raises


@skipUnlessIronPython()
class TestOut(IronPythonTestCase):
    def test_out_subok(self):
        a = np.array([0.5])
        o = np.empty((1,))

        r = np.add(a, 2, o)
        assert_(r is o)
        r = np.add(a, 2, out=o)
        assert_(r is o)
        r = np.add(a, 2, out=(o,))
        assert_(r is o)

        assert_raises(TypeError, np.add, a, 2, o, o)
        assert_raises(TypeError, np.add, a, 2, o, out=o)
        assert_raises(TypeError, np.add, a, 2, None, out=o)
        assert_raises(ValueError, np.add, a, 2, out=(o, o))
        assert_raises(ValueError, np.add, a, 2, out=())
        assert_raises(TypeError, np.add, a, 2, [])
        assert_raises(TypeError, np.add, a, 2, out=[])
        assert_raises(TypeError, np.add, a, 2, out=([],))


run_test(__name__)
