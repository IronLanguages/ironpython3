# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import array
import itertools
import gc
import unittest

from iptest import run_test, is_mono

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

    def test_finalizer(self):
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
    def test_exports(self):
        ba = bytearray()

        def f(b):
            mv = memoryview(b)
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

    def test_cast_double(self):
        a = array.array('b', range(8))
        mv = memoryview(a).cast('d')
        mv[0] = 3.4
        self.assertEqual(mv[0], 3.4)

    def test_cast_reshape(self):
        a = array.array('b', range(16))
        mv = memoryview(a).cast('b', (2,2,2,2))

        self.assertEqual(len(mv), 2)

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
        for i in range(2):
            for j in range(2):
                for k in range(2):
                    self.assertEqual(mv[(i + 2, j, k)], mv2[(i, j, k)])

        self.assertEqual(mv2.tolist(), [[[8, 9], [10, 11]], [[12, 13], [14, 15]]])
        self.assertEqual(mv2.tobytes(), b'\x08\x09\x0a\x0b\x0c\x0d\x0e\x0f')

        mv_2 = mv[::2]
        self.assertEqual(len(mv_2), 2)
        for i in range(2):
            for j in range(2):
                for k in range(2):
                    self.assertEqual(mv[(i * 2, j, k)], mv_2[(i, j, k)])

        self.assertEqual(mv_2.tolist(), [[[0, 1], [2, 3]], [[8, 9], [10, 11]]])
        self.assertEqual(mv_2.tobytes(), b'\x00\x01\x02\x03\x08\x09\x0a\x0b')


run_test(__name__)
