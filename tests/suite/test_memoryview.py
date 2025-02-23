# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import array
import itertools
import gc
import sys
import unittest
import warnings

from iptest import run_test, is_mono, is_cli, is_32, is_64, is_windows

is_long32bit = is_32 or is_windows

class SliceTests(unittest.TestCase):
    def testGet(self):
        m = memoryview(bytearray(range(5)))
        self.assertEqual(bytearray([3, 4]), m[3:5].tobytes())

    def testGetNested(self):
        m = memoryview(bytearray(range(5)))
        self.assertEqual(bytearray([2, 3]), m[1:-1][1:3].tobytes())

    def testUpdate(self):
        b = bytearray(range(4))
        m = memoryview(b)
        m[1:3] = bytes([11, 22])
        self.assertEqual(bytearray([0,11,22,3]), b)

    def testUpdateNested(self):
        b = bytearray(range(4))
        m = memoryview(b)
        m[1:][1:3] = bytes([11, 22])
        self.assertEqual(bytearray([0,1,11,22]), b)

    def testUpdateStrided(self):
        b = bytearray(range(8))
        m = memoryview(b)
        m[::2][1:3] = bytes([11, 22])
        self.assertEqual(bytearray([0, 1, 11, 3, 22, 5, 6, 7]), b)

class TestGH1387(unittest.TestCase):
    # from https://github.com/IronLanguages/main/issues/1387
    data = bytearray(range(10))
    mview = memoryview(data)

    def testMemoryViewEmptySliceData(self):
        chunk = self.mview[-1:2]
        chunk_data = bytearray(chunk)
        self.assertEqual(chunk_data, bytearray(0))

    def testMemoryViewEmptySliceLength(self):
        chunk = self.mview[-1:2]
        self.assertEqual(len(chunk), 0)

    def testMemoryViewTruncatedSliceData(self):
        chunk = self.mview[8:12]
        chunk_data = bytearray(chunk)
        self.assertEqual(chunk_data, bytearray([8,9]))

    def testMemoryViewTruncatedSliceLength(self):
        chunk = self.mview[8:12]
        self.assertEqual(len(chunk), 2)

class MemoryViewTests(unittest.TestCase):
    def test_init(self):
        x = memoryview(b"abc")
        self.assertEqual(x, b"abc")

        y = memoryview(x)
        self.assertEqual(x, y)
        self.assertIsNot(x, y)

    def test_exports(self):
        ba = bytearray()

        mv = memoryview(ba)
        self.assertRaises(BufferError, ba.append, 1)
        mv.release()
        ba.append(0)
        self.assertEqual(len(ba), 1)

        with memoryview(ba) as mv:
            self.assertRaises(BufferError, ba.append, 1)
        ba.append(0)
        self.assertEqual(len(ba), 2)

    @unittest.skipIf(is_mono, "gc.collect() implementation not synchronous")
    def test_finalizer(self):
        ba = bytearray()

        def f(b):
            memoryview(b)
            mv = memoryview(b)
        f(ba)
        gc.collect()
        ba.append(0)
        self.assertEqual(len(ba), 1)

        mv = memoryview(ba)
        del mv
        gc.collect()
        ba.append(0)
        self.assertEqual(len(ba), 2)

    def test_equality(self):
        b = b"aaa"
        ba = bytearray(b)
        mv = memoryview(b)
        a = array.array("b", b)

        # check __eq__
        # DO NOT USE assertTrue SINCE IT DOES NOT FAIL ON NotImplemented
        self.assertEqual(b.__eq__(b), True)
        self.assertEqual(b.__eq__(ba), NotImplemented)
        self.assertEqual(b.__eq__(mv), NotImplemented)
        self.assertEqual(b.__eq__(a), NotImplemented)
        self.assertEqual(ba.__eq__(b), True)
        self.assertEqual(ba.__eq__(ba), True)
        self.assertEqual(ba.__eq__(mv), True)
        self.assertEqual(ba.__eq__(a), True)
        self.assertEqual(mv.__eq__(b), True)
        self.assertEqual(mv.__eq__(ba), True)
        self.assertEqual(mv.__eq__(mv), True)
        self.assertEqual(mv.__eq__(a), True)
        self.assertEqual(a.__eq__(b), NotImplemented)
        self.assertEqual(a.__eq__(ba), NotImplemented)
        self.assertEqual(a.__eq__(mv), NotImplemented)
        self.assertEqual(a.__eq__(a), True)

        # check that equality works for all combinations
        for x, y in itertools.product((b, ba, mv), repeat=2):
            self.assertTrue(x == y, "{!r} {!r}".format(x, y))

        for x, y in itertools.product((a, mv), repeat=2):
            self.assertTrue(x == y, "{!r} {!r}".format(x, y))

        # check __ne__
        self.assertFalse(b.__ne__(b))
        self.assertEqual(b.__ne__(ba), NotImplemented)
        self.assertEqual(b.__ne__(mv), NotImplemented)
        self.assertEqual(b.__ne__(a), NotImplemented)
        self.assertFalse(ba.__ne__(b))
        self.assertFalse(ba.__ne__(ba))
        self.assertFalse(ba.__ne__(mv))
        self.assertFalse(mv.__ne__(b))
        self.assertFalse(mv.__ne__(ba))
        self.assertFalse(mv.__ne__(mv))
        self.assertFalse(mv.__ne__(a))
        self.assertEqual(a.__ne__(b), NotImplemented)
        self.assertEqual(a.__ne__(ba), NotImplemented)
        self.assertEqual(a.__ne__(mv), NotImplemented)
        self.assertFalse(a.__ne__(a))

        # check that inequality works for all combinations
        for x, y in itertools.product((b, ba, mv), repeat=2):
            self.assertFalse(x != y, "{!r} {!r}".format(x, y))

        for x, y in itertools.product((a, mv), repeat=2):
            self.assertFalse(x != y, "{!r} {!r}".format(x, y))

    def test_equality_structural(self):
        # check strided memoryview
        self.assertTrue(memoryview(b'axc')[::2] == memoryview(b'ayc')[::2])

        # check shape differences
        mv = memoryview(b"x")
        self.assertEqual(mv.format, "B")
        self.assertFalse(mv.cast('B', ()) == mv)
        self.assertTrue(mv.cast('B', (1,)) == mv)
        self.assertFalse(mv.cast('B', (1,1)) == mv)

        b = bytes(range(8))
        mv = memoryview(b)
        self.assertEqual(mv.format, 'B')

        # check different typecodes
        mv_b = mv.cast('b')
        self.assertTrue(mv_b == mv)

        # 'b' to 'B' equivalence does not hold for values out of range
        mv_B = memoryview(b'\x80\x81')
        mv_b1 = mv_b.cast('b')
        self.assertFalse(mv_B == mv_b1)

        mv_H = mv.cast('H')
        self.assertFalse(mv_H == mv)
        mv_h = mv.cast('h')
        self.assertTrue(mv_H == mv_h)

        mv_i = mv.cast('i')
        self.assertFalse(mv_i == mv)
        self.assertFalse(mv_i == mv_h)
        mv_L = mv.cast('L')
        self.assertEquals(mv_i == mv_L, is_long32bit)
        mv_f = mv.cast('f')
        self.assertFalse(mv_i == mv_f)

        mv_q = mv.cast('q')
        self.assertFalse(mv_q == mv)
        self.assertFalse(mv_q == mv_i)
        mv_Q = mv.cast('Q')
        self.assertTrue(mv_q == mv_Q)
        mv_d = mv.cast('d')
        self.assertFalse(mv_d == mv_q)
        self.assertFalse(mv_d == mv_f)

        mv_P = mv.cast('P')
        self.assertFalse(mv_P == mv_i)
        self.assertEqual(mv_P == mv_L, not is_long32bit)
        self.assertTrue(mv_P == mv_q)
        self.assertTrue(mv_P == mv_Q)

        # Comparing different formats works if the values are the same
        b = bytes(range(8))
        mv = memoryview(b)
        self.assertEqual(mv.format, 'B')

        mv_h = memoryview(array.array('h', [0,1,2,3,4,5,6,7]))
        self.assertEqual(mv_h.format, 'h')
        self.assertTrue(mv == mv_h)

        mv_L = memoryview(array.array('L', [0,1,2,3,4,5,6,7]))
        self.assertEqual(mv_L.format, 'L')
        self.assertTrue(mv == mv_L)
        self.assertTrue(mv_h == mv_L)

        mv_d = memoryview(array.array('d', [0,1,2,3,4,5,6,7]))
        self.assertEqual(mv_d.format, 'd')
        self.assertTrue(mv == mv_d)
        self.assertTrue(mv_h == mv_d)
        self.assertTrue(mv_L == mv_d)
        self.assertTrue(mv_L[::2] == mv_d[::2])

        # check released memoryview
        ba = bytearray(b)
        mv1 = memoryview(b)
        mv2 = memoryview(ba)
        self.assertTrue(mv1 == mv2)
        mv2.release()
        self.assertFalse(mv1 == mv2)
        mv1.release()
        self.assertFalse(mv1 == mv2)
        self.assertTrue(mv1 == mv1)

        # check nans
        z = array.array('f', [float('nan')])
        mv = memoryview(z)
        self.assertFalse(mv == mv)
        mv.release()
        self.assertTrue(mv == mv)

    @unittest.skipUnless(sys.flags.bytes_warning, "Run Python with the '-b' flag on command line for this test")
    def test_equality_warnings(self):
        with warnings.catch_warnings(record=True) as ws:
            warnings.simplefilter("always")

            b = bytes(range(8))
            mv = memoryview(b)
            self.assertEqual(mv.format, 'B')

            mv_b = mv.cast('b')
            self.assertTrue(mv_b == mv)
            mv_i = mv.cast('i')
            self.assertFalse(mv_b == mv_i)

            mv_c = mv.cast('c')
            if sys.version_info >= (3, 5):
                with self.assertWarnsRegex(BytesWarning, r"^Comparison between bytes and int$"):
                    self.assertFalse(mv_c == mv)
                with self.assertWarnsRegex(BytesWarning, r"^Comparison between bytes and int$"):
                    self.assertFalse(mv_c == mv_b)
                with self.assertWarnsRegex(BytesWarning, r"^Comparison between bytes and int$"):
                    self.assertFalse(mv == mv_c)
                with self.assertWarnsRegex(BytesWarning, r"^Comparison between bytes and int$"):
                    self.assertFalse(mv == mv_c)
            else:
                self.assertFalse(mv_c == mv)
                self.assertFalse(mv_c == mv_b)
                self.assertFalse(mv == mv_c)
                self.assertFalse(mv == mv_c)

        self.assertEqual(len(ws), 0) # no unchecked warnings

    def test_overflow(self):
        def setitem(m, value):
            m[0] = value
        mv = memoryview(array.array('b', range(8)))
        self.assertRaises(ValueError, lambda: setitem(mv, 128))
        self.assertRaises(ValueError, lambda: setitem(mv, 129))
        mv = memoryview(array.array('i', range(8)))
        self.assertRaises(ValueError, lambda: setitem(mv, 9223372036854775807))
        self.assertRaises(ValueError, lambda: setitem(mv, -9223372036854775807))
        mv = memoryview(array.array('I', range(8)))
        self.assertRaises(ValueError, lambda: setitem(mv, 9223372036854775807))
        self.assertRaises(ValueError, lambda: setitem(mv, -1))
        mv = mv.cast('b').cast('q')
        self.assertRaises(ValueError, lambda: setitem(mv, 9223372036854775808))
        self.assertRaises(ValueError, lambda: setitem(mv, -9223372036854775809))
        mv = mv.cast('b').cast('Q')
        self.assertRaises(ValueError, lambda: setitem(mv, 18446744073709551616))
        self.assertRaises(ValueError, lambda: setitem(mv, -1))

    def test_numeric_value_check(self):
        def setitem(m, value):
            m[0] = value
        mv = memoryview(array.array('d', [1.0, 2.0, 3.0]))
        mv  = mv.cast('b').cast('i')
        self.assertRaises(TypeError, lambda: setitem(mv, 2.5))

    def test_scalar(self):
        scalar = memoryview(b'a').cast('B', ())

        self.assertEqual(len(scalar), 1)
        self.assertEqual(scalar.ndim, 0)
        self.assertEqual(scalar.format, 'B')
        self.assertEqual(scalar.itemsize, 1)
        self.assertEqual(scalar.nbytes, 1)
        self.assertEqual(scalar.shape, ())
        self.assertEqual(scalar.strides, ())
        self.assertEqual(scalar.suboffsets, ())
        self.assertTrue(scalar.c_contiguous)
        self.assertTrue(scalar.f_contiguous)
        self.assertTrue(scalar.contiguous)
        self.assertEqual(scalar.tobytes(), b'a')
        self.assertEqual(scalar.tolist(), ord('a'))

    def test_hash(self):
        b = bytes(range(8))
        h = hash(b)
        mv = memoryview(b)
        self.assertEqual(hash(mv), h)
        mvc = mv.cast('B')
        self.assertEqual(hash(mvc), h)
        mvc = mv.cast('@B')
        self.assertEqual(hash(mvc), h)
        mvc = mv.cast('b')
        self.assertEqual(hash(mvc), h)
        mvc = mv.cast('@b')
        self.assertEqual(hash(mvc), h)
        mvc = mv.cast('c')
        self.assertEqual(hash(mvc), h)
        mvc = mv.cast('@c')
        self.assertEqual(hash(mvc), h)
        mvc = mv.cast('h')
        self.assertRaisesRegex(ValueError, "^memoryview: hashing is restricted to formats 'B', 'b' or 'c'$", hash, mvc)

    def test_unicode_array(self):
        a = array.array('u', "abcd")
        mv = memoryview(a)

        self.assertEqual(mv.format, 'u')
        self.assertEqual(mv.tobytes(), a.tobytes())

        self.assertRaisesRegex(NotImplementedError, "^memoryview: format u not supported$", mv.tolist)

        with self.assertRaises(NotImplementedError):
            mv[0]

        with self.assertRaises(NotImplementedError):
            mv[0] = 0

        if is_cli or sys.version_info.minor > 4:
            self.assertEqual(mv.cast('B').tobytes(), a.tobytes())

            with self.assertRaisesRegex(ValueError, "^memoryview: destination format must be a native single character format prefixed with an optional '@'$"):
                mv.cast('u')

            with self.assertRaisesRegex(TypeError, "^memoryview: cannot cast between two non-byte formats$"):
                mv.cast('H')

    def test_conv(self):
        self.assertEqual(int.from_bytes(memoryview(b'abcd'), 'big'), 0x61626364)
        self.assertEqual(int.from_bytes(memoryview(b'abcd')[::2], 'big'), 0x6163)
        self.assertEqual(int.from_bytes(memoryview(b'abcd')[::-2], 'big'), 0x6462)

class CastTests(unittest.TestCase):
    def test_get_int_alignment(self):
        a = array.array('b', range(8))
        mv = memoryview(a)
        slices_answers = [
            (slice(0,4,1), 50462976),
            (slice(1,5,1), 67305985),
            (slice(2,6,1), 84148994),
            (slice(3,7,1), 100992003),
            (slice(4,8,1), 117835012)
            ]

        for (s, answer) in slices_answers:
            mv2 = mv[s].cast('i')
            self.assertEqual(mv2[0], answer)

    def test_set_int_alignment(self):
        a = array.array('i', [50462976, 117835012])
        mv = memoryview(a)

        slice_newval_highint_lowint = [
            (slice(0,4,1), 105491832, 105491832,   117835012),
            (slice(1,5,1), 105491832, 1236105216,  117835014),
            (slice(2,6,1), 105491832, -1384644352, 117835337),
            (slice(3,7,1), 105491832, 2013397248,  117852589),
            (slice(4,8,1), 105491832, 50462976,    105491832)
            ]

        for (s, newval, highint, lowint) in slice_newval_highint_lowint:
            a[0] = 50462976
            a[1] = 117835012
            mv2 = (memoryview(a).cast('b'))[s].cast('i')
            mv2[0] = newval
            self.assertEqual(mv2[0], newval)
            self.assertEqual(mv[0], highint)
            self.assertEqual(mv[1], lowint)

    def test_alignment_inside_item(self):
        a = array.array('i', [50462976])
        mv = memoryview(a).cast('b')[1:3].cast('h')
        self.assertEqual(mv[0], 513)
        mv[0] = 32767
        self.assertEqual(mv[0], 32767)
        self.assertEqual(a[0], 58720000)

    def test_cast_byteorder_typecode(self):
        a = array.array('h', [100, 200])
        mv = memoryview(a)
        self.assertEqual(mv.format, 'h')

        mv2 = mv.cast('@B')
        self.assertEqual(mv2.format, '@B')
        self.assertEqual(len(mv2), 4)
        self.assertEqual(mv2[0], 100)
        self.assertEqual(mv2[2], 200)

        mv3 = mv2.cast('@h')
        self.assertEqual(mv3.format, '@h')
        self.assertEqual(len(mv3), 2)
        self.assertEqual(mv3[0], 100)
        self.assertEqual(mv3[1], 200)
        self.assertEqual(mv3, a)

        mv4 = mv.cast('B')
        self.assertEqual(mv4.format, 'B')
        self.assertRaises(ValueError, mv4.cast, '<B')
        self.assertRaises(ValueError, mv4.cast, '>B')
        self.assertRaises(ValueError, mv4.cast, '=B')
        self.assertRaises(ValueError, mv4.cast, '!B')
        self.assertRaises(ValueError, mv4.cast, ' B')
        self.assertRaises(ValueError, mv4.cast, 'B ')
        self.assertRaises(ValueError, mv4.cast, '<h')
        self.assertRaises(ValueError, mv4.cast, '>h')
        self.assertRaises(ValueError, mv4.cast, '=h')
        self.assertRaises(ValueError, mv4.cast, '!h')
        self.assertRaises(ValueError, mv4.cast, ' h')
        self.assertRaises(ValueError, mv4.cast, 'h ')

    def test_cast_wrong_size(self):
        a = array.array('b', [1,2,3,4,5])
        mv = memoryview(a)
        typecodes = ['h', 'H', 'i', 'I', 'L', 'f', 'P', 'q', 'Q', 'd']
        for tc in typecodes:
            self.assertRaises(TypeError, lambda: mv.cast(tc))

    def test_cast_wrong_shape(self):
        a = array.array('b', range(16))
        mv = memoryview(a)
        self.assertRaises(TypeError, lambda: mv.cast('b', (2,2,2)))
        self.assertRaises(TypeError, lambda: mv.cast('i', (2,2,2)))
        mv.cast('h', (2,2,2))

    def test_cast_wrong_code(self):
        mv = memoryview(bytes(range(16)))
        self.assertRaises(ValueError, mv.cast, 'x')
        self.assertRaises(ValueError, mv.cast, 'u')
        self.assertRaises(ValueError, mv.cast, 's')
        self.assertRaises(ValueError, mv.cast, 'p')

    def test_cast_q_typecode_cast(self):
        a = array.array('b', range(8))
        mv = memoryview(a).cast('q')
        mv[0] = 9223372036854775807
        self.assertEqual(a[0], -1)
        self.assertEqual(a[1], -1)
        self.assertEqual(a[2], -1)
        self.assertEqual(a[3], -1)
        self.assertEqual(a[4], -1)
        self.assertEqual(a[5], -1)
        self.assertEqual(a[6], -1)
        self.assertEqual(a[7], 127)
        self.assertIs(type(mv[0]), type(9223372036854775807))
        mv = memoryview(a).cast('Q')
        mv[0] = 18446744073709551615
        self.assertEqual(mv[0], 18446744073709551615)
        self.assertIs(type(mv[0]), type(18446744073709551615))

    def test_cast_index_typecode(self):
        b = b'\xFF' * 8
        ba = bytearray(b)
        mv = memoryview(ba)

        mvn = mv.cast('n')
        self.assertEqual(mvn[0], -1)

        mvn[0] = sys.maxsize
        self.assertEqual(mvn[0], sys.maxsize)

        mvn[0] = -sys.maxsize - 1
        self.assertEqual(mvn[0], -sys.maxsize - 1)

        with self.assertRaises(ValueError):
            mvn[0] = sys.maxsize + 1
        with self.assertRaises(ValueError):
            mvn[0] = -sys.maxsize - 2
        mvn.release()

        ba[:] = b
        mvn = mv.cast('N')
        self.assertEqual(mvn[0], sys.maxsize * 2 + 1)

        mvn[0] = 0
        self.assertEqual(mvn[0], 0)

        mvn[0] = sys.maxsize * 2 + 1
        self.assertEqual(mvn[0], sys.maxsize * 2 + 1)
        self.assertEqual(mvn.tobytes(), b)

        with self.assertRaises(ValueError):
            mvn[0] = -1
        with self.assertRaises(ValueError):
            mvn[0] = sys.maxsize * 2 + 2
        mvn.release()

    def test_cast_bool(self):
        ba = bytearray(range(4))
        mv = memoryview(ba)
        mvb = mv.cast('?')
        self.assertIs(mvb[0], False)
        self.assertIs(mvb[1], True)
        self.assertIs(mvb[2], True)

        mvb[0] = 3.14
        self.assertIs(mvb[0], True)
        mvb[0] = 0.0
        self.assertIs(mvb[0], False)

        mvb[0] = [3.14]
        self.assertIs(mvb[0], True)
        mvb[0] = []
        self.assertIs(mvb[0], False)

        mvb[0] = "3.14"
        self.assertIs(mvb[0], True)
        mvb[0] = ""
        self.assertIs(mvb[0], False)

    def test_cast_bytechar(self):
        ba = bytearray(range(4))
        mv = memoryview(ba)
        mvc = mv.cast('c')

        for i in range(len(ba)):
            self.assertEqual(mvc[i], bytes(chr(i), 'latin-1'))

        mvc[1] = b"a"
        self.assertEqual(mvc[1], b"a")

        for val in [b"", b"abc"]:
            with self.assertRaisesRegex(ValueError, "^memoryview: invalid value for format 'c'$"):
                mvc[1] = val

        class DunderBytes:
            def __bytes__(self):
                return b"z"

        for val in ["", "ab", None, 0, False, 1.1, DunderBytes(), bytearray(b"z"), array.array('B', [100])]:
            with self.assertRaisesRegex(TypeError, "^memoryview: invalid type for format 'c'$"):
                mvc[1] = val

        self.assertEqual(mvc[1], b"a")

    @unittest.skipUnless(is_64, "assumes 64-bit pointers")
    def test_cast_pointer(self):
        ba = bytearray(range(16))
        mv = memoryview(ba)
        mvp = mv.cast('P')

        self.assertEqual(mvp[0], 0x0706050403020100)
        self.assertEqual(mvp[1], 0x0F0E0D0C0B0A0908)

        mvp[0] = 0xFFFFFFFFFFFFFFFF
        mvp[1] = -1
        self.assertEqual(mvp[0], 0xFFFFFFFFFFFFFFFF)
        self.assertEqual(mvp[1], 0xFFFFFFFFFFFFFFFF)

        mvp[0] = -0x0706050403020100
        mvp[1] = -0x8000000000000000
        self.assertEqual(mvp[0], 0xF8F9FAFBFCFDFF00)
        self.assertEqual(mvp[1], 0x8000000000000000)

        with self.assertRaises(ValueError):
            mvp[0] = 0x10000000000000000
        with self.assertRaises(ValueError):
            mvp[0] = -0x8000000000000001

    @unittest.skipUnless(is_64 and is_cli, "CLI interop, assumes 64-bit pointers")
    def test_cast_netpointer(self):
        import System
        ba = bytearray(range(16))
        mv = memoryview(ba)
        mvr = mv.cast('R')

        mvr[0] = System.UIntPtr(0xFFFFFFFFFFFFFFFF)
        self.assertIsInstance(mvr[1], System.UIntPtr)
        self.assertEqual(mvr[0].ToUInt64(), 0xFFFFFFFFFFFFFFFF)
        mvr[1] = System.IntPtr(-1)
        self.assertIsInstance(mvr[1], System.UIntPtr)
        self.assertEqual(mvr[1].ToUInt64(), 0xFFFFFFFFFFFFFFFF)

        with self.assertRaises(TypeError):
            mvr[0] = 0

        mvr = mv.cast('r')
        mvr[0] = System.UIntPtr(0xFFFFFFFFFFFFFFFF)
        self.assertIsInstance(mvr[1], System.IntPtr)
        self.assertEqual(mvr[0].ToInt64(), -1)
        mvr[1] = System.IntPtr(-1)
        self.assertIsInstance(mvr[1], System.IntPtr)
        self.assertEqual(mvr[1].ToInt64(), -1)

        with self.assertRaises(TypeError):
            mvr[0] = 0

    def test_cast_double(self):
        a = array.array('b', range(8))
        mv = memoryview(a).cast('d')
        mv[0] = 3.4
        self.assertEqual(mv[0], 3.4)

    def test_cast_reshape(self):
        a = array.array('b', range(16))
        mv = memoryview(a).cast('b', (2,2,2,2))

        self.assertEqual(len(mv), 2)

        if is_cli or sys.version_info.minor > 4:
            self.assertEqual(mv[(0,0,0,0)], 0)
            self.assertEqual(mv[(0,0,0,1)], 1)
            self.assertEqual(mv[(0,0,1,0)], 2)
            self.assertEqual(mv[(0,0,1,1)], 3)
            self.assertEqual(mv[(0,1,0,0)], 4)
            self.assertEqual(mv[(0,1,0,1)], 5)
            self.assertEqual(mv[(0,1,1,0)], 6)
            self.assertEqual(mv[(0,1,1,1)], 7)
            self.assertEqual(mv[(1,0,0,0)], 8)
            self.assertEqual(mv[(1,0,0,1)], 9)
            self.assertEqual(mv[(1,0,1,0)], 10)
            self.assertEqual(mv[(1,0,1,1)], 11)
            self.assertEqual(mv[(1,1,0,0)], 12)
            self.assertEqual(mv[(1,1,0,1)], 13)
            self.assertEqual(mv[(1,1,1,0)], 14)
            self.assertEqual(mv[(1,1,1,1)], 15)

        self.assertEqual(mv.tolist(), [[[[0, 1], [2, 3]], [[4, 5], [6, 7]]], [[[8, 9], [10, 11]], [[12, 13], [14, 15]]]])
        self.assertEqual(mv.tobytes(), b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0a\x0b\x0c\x0d\x0e\x0f')

    def test_cast_reshape_then_slice(self):
        a = array.array('b', range(16))
        mv = memoryview(a).cast('b', (4,2,2))
        mv2 = mv[2:]
        self.assertEqual(len(mv2), 2)
        if is_cli or sys.version_info.minor > 4:
            for i in range(2):
                for j in range(2):
                    for k in range(2):
                        self.assertEqual(mv[(i + 2, j, k)], mv2[(i, j, k)])

        self.assertEqual(mv2.tolist(), [[[8, 9], [10, 11]], [[12, 13], [14, 15]]])
        self.assertEqual(mv2.tobytes(), b'\x08\x09\x0a\x0b\x0c\x0d\x0e\x0f')

        mv_2 = mv[::2]
        self.assertEqual(len(mv_2), 2)
        if is_cli or sys.version_info.minor > 4:
            for i in range(2):
                for j in range(2):
                    for k in range(2):
                        self.assertEqual(mv[(i * 2, j, k)], mv_2[(i, j, k)])

        self.assertEqual(mv_2.tolist(), [[[0, 1], [2, 3]], [[8, 9], [10, 11]]])
        self.assertEqual(mv_2.tobytes(), b'\x00\x01\x02\x03\x08\x09\x0a\x0b')

    def test_cast_empty(self):
        memoryview(b'').cast('b')
        memoryview(bytearray(b'')).cast('b')
        self.assertRaises(TypeError, memoryview(b'').cast, 'b', object()) # fails with any 2nd argument


run_test(__name__)
