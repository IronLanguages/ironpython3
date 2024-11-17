# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import array
import ctypes
import itertools
import sys
import unittest
import warnings

from iptest import IronPythonTestCase, is_cpython, myint, myfloat, mycomplex, run_test, skipUnlessIronPython

types = [bytearray, bytes]

class IndexableOC:
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class Indexable(object):
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class BytesSubclass(bytes): pass
class BytearraySubclass(bytearray): pass

class BytesTest(IronPythonTestCase):

    def test_init(self):
        b = bytes(b'abcd')
        self.assertIs(bytes(b), b)

        sb = BytesSubclass(b)
        self.assertIsNot(BytesSubclass(sb), sb)

        for testType in types:
            self.assertEqual(testType(b), b)
            self.assertEqual(testType(bytearray(b)), b)
            self.assertEqual(testType(memoryview(b)), b)
            self.assertEqual(testType(array.array('B', b)), b)
            self.assertEqual(testType(array.array('H', b)), b)
            self.assertEqual(testType(ctypes.c_int32(0x64636261)), b)
            self.assertEqual(testType(memoryview(b)[::2]), b[::2])
            self.assertEqual(testType(array.array('H', b"abcdefgh")[::2]), b"abef")

            self.assertRaises(TypeError, testType, None, 'ascii')
            self.assertRaises(TypeError, testType, 'abc', None)
            self.assertRaises(TypeError, testType, [None])
            self.assertEqual(testType('abc', 'ascii'), b'abc')
            self.assertEqual(testType(0), b'')
            self.assertEqual(testType(5), b'\x00\x00\x00\x00\x00')
            self.assertRaises(ValueError, testType, [256])
            self.assertRaises(ValueError, testType, [257])
            self.assertRaises(ValueError, testType, -1)

            self.assertEqual(list(testType(list(range(256)))), list(range(256)))

            self.assertEqual(testType(IndexableOC(10)), b"\0" * 10)
            self.assertRaisesRegex(TypeError, "'IndexableOC' object", testType, IndexableOC(IndexableOC(10)))
            self.assertRaisesMessage(OverflowError, "cannot fit 'int' into an index-sized integer", testType, 2<<222)
            self.assertRaisesMessage(OverflowError, "cannot fit 'myint' into an index-sized integer", testType, myint(2<<222))
            self.assertRaisesMessage(OverflowError, "cannot fit 'IndexableOC' into an index-sized integer", testType, IndexableOC(2<<222))

        def f():
            yield 42

        self.assertEqual(bytearray(f()), b'*')

        ba = bytearray()
        def gen():
            for i in range(251, 256):
                yield IndexableOC(i)
                # modify the array being initialized
                ba.append(100)
            for i in range(1, 6):
                # yield some plain good bytes
                yield i
                # modify the array being initialized
                ba.append(101)

        expected = list(itertools.chain(
            itertools.chain.from_iterable(zip(range(251, 256), [100] * 5)),
            itertools.chain.from_iterable(zip(range(1, 6), [101] * 5))
        ))

        ba.__init__(gen())
        self.assertEqual(list(ba), expected)
        ba.__init__(gen())
        self.assertEqual(list(ba), expected)

        self.assertRaises(TypeError, ba.__init__, 1.0)
        self.assertEqual(ba, bytearray())

        self.assertRaises(TypeError, ba.__init__, "abc")
        self.assertEqual(ba, bytearray())

        mv = memoryview(ba)
        self.assertRaises(TypeError, ba.__init__, "abc")
        self.assertRaises(BufferError, ba.__init__, "abc", 'ascii')

    def test_hints(self):
        global test_hint_called

        class BadHint:
            def __length_hint__(self):
                global test_hint_called
                test_hint_called = True
                return "abc"
            def __iter__(self):
                self.x = 0
                return self
            def __next__(self):
                self.x += 1
                if self.x > 4: raise StopIteration()
                return self.x

        test_hint_called = False
        ba = bytearray(BadHint())
        self.assertEqual(ba, bytearray(b'\x01\x02\x03\x04'))
        self.assertFalse(test_hint_called)

        test_hint_called = False
        self.assertRaises(TypeError, bytes, BadHint())
        self.assertTrue(test_hint_called)

        test_hint_called = False
        self.assertRaises(TypeError, ba.extend, BadHint())
        self.assertTrue(test_hint_called)
        self.assertEqual(ba, bytearray(b'\x01\x02\x03\x04'))

        ba[:] = b"abcd"
        test_hint_called = False
        ba[0:4:1] = BadHint()
        self.assertEqual(ba, bytearray(b'\x01\x02\x03\x04'))
        self.assertFalse(test_hint_called)

        del test_hint_called

    @skipUnlessIronPython()
    def test_init_interop(self):
        import clr
        clr.AddReference("System.Memory")
        from System import Byte, Array, ArraySegment, ReadOnlyMemory, Memory

        arr = Array[Byte](b"abc")
        ars = ArraySegment[Byte](arr)
        rom = ReadOnlyMemory[Byte](arr)
        mem = Memory[Byte](arr)

        for testType in types:
            self.assertEqual(testType(arr), b"abc")
            self.assertEqual(testType(ars), b"abc")
            self.assertEqual(testType(rom), b"abc")
            self.assertEqual(testType(mem), b"abc")

    def test_dunder_bytes(self):
        class A:
            def __bytes__(self):
                return b'abc'

        self.assertEqual(bytes(A()), b'abc')
        self.assertRaisesRegex(TypeError, "^'A' object is not iterable$", bytearray, A())

        class A1:
            def __bytes__(self):
                return bytearray(b'abc')

        self.assertRaisesRegex(TypeError, r"__bytes__ returned non-bytes \(.*bytearray.*\)$", bytes, A1())

        class A2: pass
        self.assertRaisesRegex(TypeError, "^'A2' object is not iterable$", bytearray, A2())

        class A3:
            def __bytes__(self):
                return None

        self.assertRaisesRegex(TypeError, r"__bytes__ returned non-bytes \(.*NoneType.*\)$", bytes, A3())

        class A4:
            def __bytes__(self):
                return b'abc'
            def __index__(self):
                return 42

        self.assertEqual(bytes(A4()), b'abc')
        self.assertEqual(bytearray(A4()), bytearray(42))
        self.assertEqual(int.from_bytes(A4(), 'big'), 0x616263)

        class EmptyClass: pass
        t = EmptyClass()
        t.__bytes__ = lambda: b"1"
        self.assertRaisesRegex(TypeError, "'EmptyClass' object is not iterable", bytes, t)

        class OtherBytesSubclass(bytes): pass

        class SomeClass:
            def __bytes__(self):
                return OtherBytesSubclass(b'SOME CLASS')

        self.assertEqual(bytes(SomeClass()), b'SOME CLASS')
        self.assertIs(type(bytes(SomeClass())), OtherBytesSubclass)
        self.assertEqual(BytesSubclass(SomeClass()), b'SOME CLASS')
        self.assertIs(type(BytesSubclass(SomeClass())), BytesSubclass)
        self.assertEqual(int.from_bytes(bytes(SomeClass()), 'big'), int.from_bytes(b"SOME CLASS", 'big'))

        class BytesBytesSubclass(bytes):
            def __bytes__(self):
                return BytesBytesSubclass(b"BYTES FROM BYTES")

        self.assertEqual(bytes(BytesBytesSubclass(b"JUST BYTES")), b"BYTES FROM BYTES")
        self.assertIs(type(bytes(BytesBytesSubclass(b"JUST BYTES"))), BytesBytesSubclass)
        self.assertEqual(int.from_bytes(bytes(BytesBytesSubclass(b"JUST BYTES")), 'big'), int.from_bytes(b"BYTES FROM BYTES", 'big'))
        self.assertEqual(int.from_bytes(BytesBytesSubclass(b"JUST BYTES"), 'big'), int.from_bytes(b"BYTES FROM BYTES", 'big'))

        class ListSubclass(bytes):
            def __bytes__(self):
                return OtherBytesSubclass(b"BYTES FROM LIST")

        self.assertEqual(bytes(ListSubclass([1, 2, 3])), b"BYTES FROM LIST")
        self.assertIs(type(bytes(ListSubclass([1, 2, 3]))), OtherBytesSubclass)
        self.assertEqual(BytesSubclass(ListSubclass([1, 2, 3])), b"BYTES FROM LIST")
        self.assertIs(type(BytesSubclass(ListSubclass([1, 2, 3]))), BytesSubclass)

        class StrSubclass(str):
            def __bytes__(self):
                return OtherBytesSubclass(b"BYTES FROM STR")

        if sys.version_info >= (3, 5) or sys.implementation.name == 'ironpython':
            self.assertEqual(bytes(StrSubclass("STR")), b"BYTES FROM STR")
            self.assertIs(type(bytes(StrSubclass("STR"))), OtherBytesSubclass)
            self.assertEqual(BytesSubclass(StrSubclass("STR")), b"BYTES FROM STR")
            self.assertIs(type(BytesSubclass(StrSubclass("STR"))), BytesSubclass)
        else:
            self.assertRaises(TypeError, bytes, StrSubclass("STR"))

        self.assertEqual(bytes(StrSubclass("STR"), 'ascii'), b"STR")
        self.assertEqual(bytes(StrSubclass("STR"), 'ascii', 'ignore'), b"STR")

        class IntSubclass(int):
            def __bytes__(self):
                return OtherBytesSubclass(b"BYTES FROM INT")

        self.assertEqual(bytes(IntSubclass(-1)), b"BYTES FROM INT")
        self.assertIs(type(bytes(IntSubclass(-1))), OtherBytesSubclass)
        self.assertEqual(BytesSubclass(IntSubclass(-1)), b"BYTES FROM INT")
        self.assertIs(type(BytesSubclass(IntSubclass(-1))), BytesSubclass)
        self.assertEqual(int.from_bytes(IntSubclass(555), 'big'), int.from_bytes(b"BYTES FROM INT", 'big'))

    def test_dunder_index(self):
        class IndexableBytes(bytes):
            def __init__(self, value):
                self.value = len(value)
            def __index__(self):
                return self.value

        class IndexableBytearray(bytearray):
            def __init__(self, value):
                super().__init__(value)
                self.value = len(value)
            def __index__(self):
                return self.value

        class IndexableStr(str):
            def __init__(self, value):
                self.value = len(value)
            def __index__(self):
                return self.value

        class IndexableInt(int):
            def __init__(self, value):
                self.value = value
            def __index__(self):
                return self.value + 10

        ib = IndexableBytes(b"xyz")
        iba = IndexableBytearray(b"abcd")
        istr = IndexableStr("abcde")
        ii = IndexableInt(2)
        self.assertEqual(ii.__index__(), 12)

        self.assertEqual(ib, b"xyz")
        self.assertEqual(iba, bytearray(b"abcd"))
        self.assertEqual(istr, "abcde")

        self.assertEqual(bytes(ib), bytes(3))
        self.assertEqual(bytes(iba), bytes(4))
        self.assertRaises(TypeError, bytes, istr)
        self.assertEqual(bytes(ii), bytes(2))

        self.assertEqual(bytearray(ib), bytearray(3))
        self.assertEqual(bytearray(iba), bytearray(4))
        self.assertRaises(TypeError, bytearray, istr)
        self.assertEqual(bytearray(ii), bytes(2))

        self.assertEqual(int.from_bytes(IndexableBytes(b"abc"), 'big'), 0x616263)
        self.assertEqual(int.from_bytes(IndexableBytearray(b"abc"), 'big'), 0x616263)
        self.assertRaises(TypeError, int.from_bytes, IndexableStr("abc"), 'big')
        self.assertRaises(TypeError, int.from_bytes, IndexableInt(2), 'big')
        self.assertRaises(TypeError, int.from_bytes, 2, 'big')

    @unittest.skipUnless(sys.flags.bytes_warning, "Run Python with the '-b' flag on command line for this test")
    def test_byteswarning(self):
        with warnings.catch_warnings(record=True) as ws:
            warnings.simplefilter("always")

            with self.assertWarnsRegex(BytesWarning, r"^str\(\) on a bytes instance$"):
                self.assertEqual(str(b'abc'), "b'abc'")
            self.assertEqual(str(b'abc', 'ascii'), 'abc')

            with self.assertWarnsRegex(BytesWarning, r"^str\(\) on a bytearray instance$"):
                self.assertEqual(str(bytearray(b'abc')), "bytearray(b'abc')")
            self.assertEqual(str(bytearray(b'abc'), 'ascii'), 'abc')

            class B(bytes):
                def __str__(self):
                    return "This is B"

            self.assertEqual(str(B(b'abc')), "This is B") # no warning

            class B2(bytes): pass
            with self.assertWarnsRegex(BytesWarning, r"^str\(\) on a bytes instance$"):
                self.assertEqual(str(B2(b'abc')), "b'abc'")

        self.assertEqual(len(ws), 0) # no unchecked warnings

    def test_byteswarning_user(self):
        with warnings.catch_warnings(record=True) as ws:
            warnings.simplefilter("always")

            with self.assertWarnsRegex(BytesWarning, r"^test warning$"):
                warnings.warn("test warning", BytesWarning)

        self.assertEqual(len(ws), 0) # no unchecked warnings

    def test_capitalize(self):
        tests = [(b'foo', b'Foo'),
                (b' foo', b' foo'),
                (b'fOO', b'Foo'),
                (b' fOO BAR', b' foo bar'),
                (b'fOO BAR', b'Foo bar'),
                ]

        for testType in types:
            for data, result in tests:
                self.assertEqual(testType(data).capitalize(), result)

        y = b''
        x = y.capitalize()
        self.assertEqual(id(x), id(y))
        self.assertIs(type(BytesSubclass(y).capitalize()), bytes)

        y = bytearray(b'')
        x = y.capitalize()
        self.assertTrue(id(x) != id(y), "bytearray.capitalize returned self")
        self.assertIs(type(BytearraySubclass(y).capitalize()), bytearray)

    def test_center(self):
        for testType in types:
            self.assertEqual(testType(b'aa').center(4), b' aa ')
            self.assertEqual(testType(b'aa').center(4, b'*'), b'*aa*')
            self.assertEqual(testType(b'aa').center(2), b'aa')
            self.assertEqual(testType(b'aa').center(2, b'*'), b'aa')
            self.assertRaises(TypeError, testType(b'abc').center, 3, [2, ])
            self.assertRaises(TypeError, testType(b'abc').center, 3, ' ')

        x = b'aa'
        self.assertEqual(id(x.center(2, b'*')), id(x))
        self.assertIs(type(BytesSubclass(x).center(2, b'*')), bytes)

        x = bytearray(b'aa')
        self.assertTrue(id(x.center(2, b'*')) != id(x))
        self.assertIs(type(BytearraySubclass(x).center(2, b'*')), bytearray)

    def test_count(self):
        for testType in types:
            self.assertEqual(testType(b"adadad").count(b"d"), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad"), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad", 1, 8), 2)
            self.assertEqual(testType(b"adbaddads").count(b"ad", -1, -1), 0)
            self.assertEqual(testType(b"adbaddads").count(b"ad", 0, -1), 3)
            self.assertEqual(testType(b"adbaddads").count(b"", 0, -1), 9)
            self.assertEqual(testType(b"adbaddads").count(b"", 27), 0)

            self.assertRaises(TypeError, testType(b"adbaddads").count, [2,])
            self.assertRaises(TypeError, testType(b"adbaddads").count, [2,], 0)
            self.assertRaises(TypeError, testType(b"adbaddads").count, [2,], 0, 1)

            self.assertEqual(testType(b"adbaddads").count(b"ad", -10), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad", -2<<222), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad", -2<<222, 2<<222), 3)
            self.assertFalse(testType(b"adbaddads").count(b"ad", None, -2<<222), 0)
            self.assertFalse(testType(b"adbaddads").count(b"ad", 2<<222, None), 0)

            self.assertEqual(testType(b"adbaddads").count(b"ad", IndexableOC(1), IndexableOC(8)), 2)
            self.assertEqual(testType(b"adbaddads").count(b"ad", IndexableOC(-10)), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad", IndexableOC(-2<<222)), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad", IndexableOC(-2<<222), IndexableOC(2<<222)), 3)
            self.assertFalse(testType(b"adbaddads").count(b"ad", None, IndexableOC(-2<<222)), 0)
            self.assertFalse(testType(b"adbaddads").count(b"ad", IndexableOC(2<<222), None), 0)

            self.assertEqual(testType(b"adbaddads").count((ord('a')<<222>>221) // 2), 3)
            self.assertRaises(ValueError, testType(b"adbaddads").count, ord('a')<<222)

    def test_decode(self):
        for testType in types:
            self.assertEqual(testType(b'\xff\xfea\x00b\x00c\x00').decode('utf-16'), 'abc')

    def test_endswith(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abcdef').endswith, ([], ))
            self.assertRaises(TypeError, testType(b'abcdef').endswith, [])
            self.assertRaises(TypeError, testType(b'abcdef').endswith, [], 0)
            self.assertRaises(TypeError, testType(b'abcdef').endswith, [], 0, 1)
            self.assertEqual(testType(b'abcdef').endswith(b'def'), True)
            self.assertEqual(testType(b'abcdef').endswith(b'def', -1, -2), False)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 0, 42), True)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 0, -7), False)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 42, -7), False)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 42), False)
            self.assertEqual(testType(b'abcdef').endswith(b'bar'), False)
            self.assertEqual(testType(b'abcdef').endswith((b'def', )), True)
            self.assertEqual(testType(b'abcdef').endswith((b'baz', )), False)
            self.assertEqual(testType(b'abcdef').endswith((b'baz', ), 0, 42), False)
            self.assertEqual(testType(b'abcdef').endswith((b'baz', ), 0, -42), False)

            self.assertTrue(testType(b'===abc').endswith(b'abc', -2<<222))
            self.assertFalse(testType(b'===abc').endswith(b'abc', 2<<222))
            self.assertFalse(testType(b'===abc').endswith(b'abc', None, -2<<222))
            self.assertTrue(testType(b'===abc').endswith(b'abc', None, 2<<222))
            self.assertTrue(testType(b'===abc').endswith(b'abc', None, None))

            self.assertTrue(testType(b'===abc').endswith((b'xyz', b'abc'), -2<<222))
            self.assertFalse(testType(b'===abc').endswith((b'xyz', b'abc'), 2<<222))
            self.assertFalse(testType(b'===abc').endswith((b'xyz', b'abc'), None, -2<<222))
            self.assertTrue(testType(b'===abc').endswith((b'xyz', b'abc'), None, 2<<222))
            self.assertTrue(testType(b'===abc').endswith((b'xyz', b'abc'), None, None))

            self.assertTrue(testType(b'===abc').endswith((b'xyz', b'abc'), IndexableOC(-2<<222)))
            self.assertFalse(testType(b'===abc').endswith((b'xyz', b'abc'), IndexableOC(2<<222)))
            self.assertFalse(testType(b'===abc').endswith((b'xyz', b'abc'), None, IndexableOC(-2<<222)))
            self.assertTrue(testType(b'===abc').endswith((b'xyz', b'abc'), None, (2<<222)))
            self.assertTrue(testType(b'===abc').endswith((b'xyz', b'b'), IndexableOC(4), IndexableOC(5)))
            self.assertTrue(testType(b'===abc').endswith((b'xyz', memoryview(b'b')), IndexableOC(4), IndexableOC(5)))
            self.assertTrue(testType(b'===abc').endswith((b'xyz', memoryview(bytearray(b'b'))), IndexableOC(4), IndexableOC(5)))

            for x in (0, 1, 2, 3, -10, -3, -4):
                self.assertEqual(testType(b"abcdef").endswith(b"def", x), True)
                self.assertEqual(testType(b"abcdef").endswith(b"de", x, 5), True)
                self.assertEqual(testType(b"abcdef").endswith(b"de", x, -1), True)
                self.assertEqual(testType(b"abcdef").endswith((b"def", ), x), True)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, 5), True)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, -1), True)

            for x in (4, 5, 6, 10, -1, -2):
                self.assertEqual(testType(b"abcdef").endswith((b"def", ), x), False)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, 5), False)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, -1), False)

            ans = [
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = -5
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = -4
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = -3
                [False, False, False, False, True, True, False, True, True, True, True, True], # start = -2
                [False, False, False, False, False, True, False, False, True, True, True, True], # start = -1
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = 0
                [False, False, False, False, True, True, False, True, True, True, True, True], # start = 1
                [False, False, False, False, False, True, False, False, True, True, True, True], # start = 2
                [False, False, False, False, False, False, False, False, False, True, True, True], # start = 3
                [False, False, False, False, False, False, False, False, False, False, False, False], # start = 4
            ]
            seq = testType(b"abc")
            for start in range(-5, 5):
                for end in range(-6, 6):
                    self.assertEqual(seq.endswith(b"", start, end), ans[start+5][end+6], "for start={0}, end={1}".format(start,end))

    def test_expandtabs(self):
        for testType in types:
            self.assertTrue(testType(b"\ttext\t").expandtabs(0) == b"text")
            self.assertTrue(testType(b"\ttext\t").expandtabs(-10) == b"text")
            self.assertEqual(testType(b"\r\ntext\t").expandtabs(-10), b"\r\ntext")

            self.assertEqual(len(testType(b"aaa\taaa\taaa").expandtabs()), 19)
            self.assertEqual(testType(b"aaa\taaa\taaa").expandtabs(), b"aaa     aaa     aaa")
            self.assertRaises(OverflowError, bytearray(b'\t\t').expandtabs, sys.maxsize)

        x = b''
        self.assertEqual(id(x.expandtabs()), id(x))
        self.assertIs(type(BytesSubclass(x).expandtabs()), bytes)

        x = bytearray(b'')
        self.assertTrue(id(x.expandtabs()) != id(x))
        self.assertIs(type(BytearraySubclass(x).expandtabs()), bytearray)

    def test_extend(self):
        b = bytearray(b'abc')
        b.extend(b'def')
        self.assertEqual(b, b'abcdef')
        b.extend(bytearray(b'ghi'))
        self.assertEqual(b, b'abcdefghi')

        b = bytearray(b'abc')
        b.extend([2,3,4])
        self.assertEqual(b, b'abc' + b'\x02\x03\x04')

        b = bytearray(b'abc')
        b.extend(memoryview(b"def"))
        self.assertEqual(b, b'abcdef')

    def test_find(self):
        for testType in types:
            self.assertEqual(testType(b"abcdbcda").find(b"cd", 1), 2)
            self.assertEqual(testType(b"abcdbcda").find(b"cd", 3), 5)
            self.assertEqual(testType(b"abcdbcda").find(b"cd", 7), -1)
            self.assertEqual(testType(b'abc').find(b'abc', -1, 1), -1)
            self.assertEqual(testType(b'abc').find(b'abc', 25), -1)
            self.assertEqual(testType(b'abc').find(b'add', 0, 3), -1)

            self.assertEqual(testType(b'abc').find(b'add', 0, None), -1)
            self.assertEqual(testType(b'abc').find(b'add', None, None), -1)
            self.assertEqual(testType(b'abc').find(b'', None, 0), 0)
            self.assertEqual(testType(b'x').find(b'x', None, 0), -1)

            self.assertEqual(testType(b'').find(b'', 0, 4), 0)

            self.assertEqual(testType(b'x').find(b'x', -2, -1), -1)
            self.assertEqual(testType(b'x').find(b'x', -2, 1), 0)
            self.assertEqual(testType(b'x').find(b'x', -1, 1), 0)
            self.assertEqual(testType(b'x').find(b'x', 0, 0), -1)
            self.assertEqual(testType(b'x').find(b'x', 3, 0), -1)
            self.assertEqual(testType(b'x').find(b'', 3, 0), -1)

            self.assertRaises(TypeError, testType(b'x').find, [1])
            self.assertRaises(TypeError, testType(b'x').find, [1], 0)
            self.assertRaises(TypeError, testType(b'x').find, [1], 0, 1)

            ans = [
                [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], # start = -5
                [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], # start = -4
                [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], # start = -3
                [-1, -1, -1, -1, 1, 1, -1, 1, 1, 1, 1, 1], # start = -2
                [-1, -1, -1, -1, -1, 2, -1, -1, 2, 2, 2, 2], # start = -1
                [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], # start = 0
                [-1, -1, -1, -1, 1, 1, -1, 1, 1, 1, 1, 1], # start = 1
                [-1, -1, -1, -1, -1, 2, -1, -1, 2, 2, 2, 2], # start = 2
                [-1, -1, -1, -1, -1, -1, -1, -1, -1, 3, 3, 3], # start = 3
                [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], # start = 4
            ]
            seq = testType(b"abc")
            for start in range(-5, 5):
                for end in range(-6, 6):
                    self.assertEqual(seq.find(b"", start, end), ans[start+5][end+6], "for start={0}, end={1}".format(start,end))

    def test_fromhex(self):
        for testType in types:
            if testType != str:
                self.assertRaises(ValueError, testType.fromhex, '0')
                self.assertRaises(ValueError, testType.fromhex, 'A')
                self.assertRaises(ValueError, testType.fromhex, 'a')
                self.assertRaises(ValueError, testType.fromhex, 'aG')
                self.assertRaises(ValueError, testType.fromhex, 'Ga')

                self.assertEqual(testType.fromhex('00'), b'\x00')
                self.assertEqual(testType.fromhex('00 '), b'\x00')
                self.assertEqual(testType.fromhex('00  '), b'\x00')
                self.assertEqual(testType.fromhex('00  01'), b'\x00\x01')
                self.assertEqual(testType.fromhex('00  01 0a'), b'\x00\x01\x0a')
                self.assertEqual(testType.fromhex('00  01 0a 0B'), b'\x00\x01\x0a\x0B')
                self.assertEqual(testType.fromhex('00  a1 Aa 0B'), b'\x00\xA1\xAa\x0B')

    def test_index(self):
        for testType in types:
            self.assertRaises(ValueError, testType(b'abc').index, 257)
            self.assertEqual(testType(b'abc').index(b'a'), 0)
            self.assertEqual(testType(b'abc').index(b'a', 0, -1), 0)

            self.assertRaises(ValueError, testType(b'abc').index, b'c', 0, -1)
            self.assertRaises(ValueError, testType(b'abc').index, b'a', -1)

            self.assertEqual(testType(b'abc').index(b'ab'), 0)
            self.assertEqual(testType(b'abc').index(b'bc'), 1)
            self.assertRaises(ValueError, testType(b'abc').index, b'abcd')
            self.assertRaises(ValueError, testType(b'abc').index, b'e')

            self.assertRaises(TypeError, testType(b'x').index, [1])
            self.assertRaises(TypeError, testType(b'x').index, [1], 0)
            self.assertRaises(TypeError, testType(b'x').index, [1], 0, 1)

    def test_insert(self):
        b = bytearray(b'abc')
        b.insert(0, ord('d'))
        self.assertEqual(b, b'dabc')

        b.insert(1000, ord('d'))
        self.assertEqual(b, b'dabcd')

        b.insert(-1, ord('d'))
        self.assertEqual(b, b'dabcdd')

        self.assertRaises(ValueError, b.insert, 0, 256)

    def test_iterator_length_hint(self):
        for testType in types:
            b = testType(b"abc")

            it = iter(b)
            self.assertEqual(it.__length_hint__(), 3)
            self.assertEqual(next(it), ord("a"))
            self.assertEqual(it.__length_hint__(), 2)
            self.assertEqual(next(it), ord("b"))
            self.assertEqual(it.__length_hint__(), 1)
            self.assertEqual(next(it), ord("c"))
            self.assertEqual(it.__length_hint__(), 0)

            self.assertRaises(StopIteration, next, it)
            self.assertEqual(it.__length_hint__(), 0)

    def test_iterator_reduce(self):
        for testType in types:
            b = testType(b"abc")

            it = iter(b)
            self.assertEqual(it.__reduce__(), (iter, (b"abc",), 0))
            self.assertEqual(next(it), ord("a"))
            self.assertEqual(it.__reduce__(), (iter, (b"abc",), 1))
            self.assertEqual(next(it), ord("b"))
            self.assertEqual(it.__reduce__(), (iter, (b"abc",), 2))
            self.assertEqual(next(it), ord("c"))
            self.assertEqual(it.__reduce__(), (iter, (b"abc",), 3))

            self.assertRaises(StopIteration, next, it)
            empty_reduce = it.__reduce__()
            self.assertEqual(len(empty_reduce), 2)
            self.assertEqual(empty_reduce[0], iter)
            self.assertEqual(len(empty_reduce[1]), 1)
            self.assertEqual(len(empty_reduce[1][0]), 0)

            it = iter(testType(b""))
            self.assertEqual(it.__reduce__(), (iter, (b"",), 0))

    def test_iterator_setstate(self):
        for testType in types:
            b = testType(b"abc")

            it = iter(b)
            self.assertEqual(next(it), ord("a"))
            self.assertEqual(next(it), ord("b"))
            self.assertEqual(next(it), ord("c"))
            self.assertRaises(StopIteration, next, it)

            it = iter(b)
            self.assertEqual(next(it), ord("a"))
            it.__setstate__(0)
            self.assertEqual(next(it), ord("a"))
            it.__setstate__(-10)
            self.assertEqual(next(it), ord("a"))
            it.__setstate__(2)
            self.assertEqual(next(it), ord("c"))
            it.__setstate__(1)
            self.assertEqual(next(it), ord("b"))
            it.__setstate__(3)
            self.assertRaises(StopIteration, next, it)
            it.__setstate__(0)
            self.assertRaises(StopIteration, next, it)

            it = iter(b)
            it.__setstate__(10)
            self.assertRaises(StopIteration, next, it)
            it.__setstate__(0)
            self.assertRaises(StopIteration, next, it)

    def check_is_method(self, methodName, result):
        for testType in types:
            self.assertEqual(getattr(testType(b''), methodName)(), False)
            for i in range(256):
                data = bytearray()
                data.append(i)

                self.assertTrue(getattr(testType(data), methodName)() == result(i), chr(i) + " (" + str(i) + ") should be " + str(result(i)))

    def test_isalnum(self):
        self.check_is_method('isalnum', lambda i : i >= ord('a') and i <= ord('z') or i >= ord('A') and i <= ord('Z') or i >= ord('0') and i <= ord('9'))

    def test_isalpha(self):
        self.check_is_method('isalpha', lambda i : i >= ord('a') and i <= ord('z') or i >= ord('A') and i <= ord('Z'))

    def test_isdigit(self):
        self.check_is_method('isdigit', lambda i : (i >= ord('0') and i <= ord('9')))

    def test_islower(self):
        self.check_is_method('islower', lambda i : i >= ord('a') and i <= ord('z'))
        for testType in types:
            for i in range(256):
                if not chr(i).isupper():
                    self.assertEqual((testType(b'a') + testType([i])).islower(), True)

    def test_isspace(self):
        self.check_is_method('isspace', lambda i : i in [ord(' '), ord('\t'), ord('\f'), ord('\n'), ord('\r'), 11])
        for testType in types:
            for i in range(256):
                if not chr(i).islower():
                    self.assertEqual((testType(b'A') + testType([i])).isupper(), True)

    def test_istitle(self):
        for testType in types:
            self.assertEqual(testType(b'').istitle(), False)
            self.assertEqual(testType(b'Foo').istitle(), True)
            self.assertEqual(testType(b'Foo Bar').istitle(), True)
            self.assertEqual(testType(b'FooBar').istitle(), False)
            self.assertEqual(testType(b'foo').istitle(), False)

    def test_isupper(self):
        self.check_is_method('isupper', lambda i : i >= ord('A') and i <= ord('Z'))

    def test_join(self):
        x = b''
        self.assertEqual(id(x.join(b'')), id(x))

        x = bytearray(x)
        self.assertTrue(id(x.join(b'')) != id(x))

        self.assertEqual(id(b'foo'.join([])), id(b'bar'.join([])))

        x = b'abc'
        self.assertEqual(id(b'foo'.join([x])), id(x))

        self.assertRaises(TypeError, b'foo'.join, [42])

        x = bytearray(b'foo')
        self.assertTrue(id(bytearray(b'foo').join([x])) != id(x), "got back same object on single arg join w/ bytearray")

        for testType in types:
            self.assertEqual(testType(b'x').join([b'd', b'e', b'f']), b'dxexf')
            self.assertEqual(testType(b'x').join([b'd', b'e', b'f']), b'dxexf')
            self.assertEqual(type(testType(b'x').join([b'd', b'e', b'f'])), testType)
            if str != bytes:
                # works in Py3k/Ipy, not in Py2.6
                self.assertEqual(b'x'.join([testType(b'd'), testType(b'e'), testType(b'f')]), b'dxexf')
            self.assertEqual(bytearray(b'x').join([testType(b'd'), testType(b'e'), testType(b'f')]), b'dxexf')
            self.assertEqual(testType(b'').join([]), b'')
            self.assertEqual(testType(b'').join((b'abc', )), b'abc')
            self.assertEqual(testType(b'').join((b'abc', b'def')), b'abcdef')
            self.assertRaises(TypeError, testType(b'').join, (42, ))

    def test_ljust(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').ljust, 42, ' ')
            self.assertRaises(TypeError, testType(b'').ljust, 42, '  ')
            self.assertRaises(TypeError, testType(b'').ljust, 42, b'  ')
            self.assertRaises(TypeError, testType(b'').ljust, 42, '\u0100')
            self.assertEqual(testType(b'abc').ljust(4), b'abc ')
            self.assertEqual(testType(b'abc').ljust(4, b'x'), b'abcx')
            self.assertEqual(testType(b'abc').ljust(-4), b'abc')

        x = b'abc'
        self.assertEqual(id(x.ljust(2)), id(x))
        self.assertIs(type(BytesSubclass(x).ljust(2)), bytes)

        x = bytearray(x)
        self.assertTrue(id(x.ljust(2)) != id(x))
        self.assertIs(type(BytearraySubclass(x).ljust(2)), bytearray)

    def test_lower(self):
        expected = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f'  \
        b'\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%' \
        b'&\'()*+,-./0123456789:;<=>?@abcdefghijklmnopqrstuvwxyz[\\]^_`'          \
        b'abcdefghijklmnopqrstuvwxyz{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88' \
        b'\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99'   \
        b'\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa'   \
        b'\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb'   \
        b'\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc'   \
        b'\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd'   \
        b'\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee'   \
        b'\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'

        data = bytearray()
        for i in range(256):
            data.append(i)

        for testType in types:
            self.assertEqual(testType(data).lower(), expected)

        x = b''
        self.assertEqual(id(x.lower()), id(x))
        self.assertIs(type(BytesSubclass(x).lower()), bytes)

        x = bytearray(b'')
        self.assertTrue(id(x.lower()) != id(x))
        self.assertIs(type(BytearraySubclass(x).lower()), bytearray)

    def test_lstrip(self):
        for testType in types:
            self.assertEqual(testType(b' abc').lstrip(), b'abc')
            self.assertEqual(testType(b' abc ').lstrip(), b'abc ')
            self.assertEqual(testType(b' ').lstrip(), b'')

        x = b'abc'
        self.assertEqual(id(x.lstrip()), id(x))
        self.assertIs(type(BytesSubclass(x).lstrip()), bytes)
        self.assertIs(type(BytesSubclass(x).lstrip(b'x')), bytes)

        x = bytearray(x)
        self.assertTrue(id(x.lstrip()) != id(x))
        self.assertIs(type(BytearraySubclass(x).lstrip()), bytearray)
        self.assertIs(type(BytearraySubclass(x).lstrip(b'x')), bytearray)

    def test_partition(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').partition, None)
            self.assertRaises(ValueError, testType(b'').partition, b'')
            self.assertRaises(ValueError, testType(b'').partition, b'')

            if testType == bytearray and is_cpython and sys.version_info < (3,6): # https://bugs.python.org/issue20047
                self.assertEqual(testType(b'a\x01c').partition([1]), (b'a', b'\x01', b'c'))
            else:
                self.assertRaises(TypeError, testType(b'a\x01c').partition, [1])

            self.assertEqual(testType(b'abc').partition(b'b'), (b'a', b'b', b'c'))
            self.assertEqual(testType(b'abc').partition(b'd'), (b'abc', b'', b''))

            x = testType(b'abc')
            one, two, three = x.partition(b'd')
            if testType == bytearray:
                self.assertTrue(id(one) != id(x))
            else:
                self.assertEqual(id(one), id(x))

        one, two, three = b''.partition(b'abc')
        self.assertEqual(id(one), id(two))
        self.assertEqual(id(two), id(three))

        one, two, three = bytearray().partition(b'abc')
        self.assertTrue(id(one) != id(two))
        self.assertTrue(id(two) != id(three))
        self.assertTrue(id(three) != id(one))

    def test_pop(self):
        b = bytearray()
        self.assertRaises(IndexError, b.pop)
        self.assertRaises(IndexError, b.pop, 0)

        b = bytearray(b'abc')
        self.assertEqual(b.pop(), ord('c'))
        self.assertEqual(b, b'ab')

        b = bytearray(b'abc')
        b.pop(1)
        self.assertEqual(b, b'ac')

        b = bytearray(b'abc')
        b.pop(-1)
        self.assertEqual(b, b'ab')

    def test_replace(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'abc')
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None)
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'abc', 1)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None, 1)
            self.assertRaises(TypeError, testType(b'abc').replace, [1], b'abc')
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', [1])
            self.assertRaises(TypeError, testType(b'abc').replace, [1], b'abc', 1)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', [1], 1)

            self.assertEqual(testType(b'abc').replace(b'b', b'foo'), b'afooc')
            self.assertEqual(testType(b'abc').replace(b'b', b''), b'ac')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', 1), b'afoocb')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', 2), b'afoocfoo')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', 3), b'afoocfoo')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', -1), b'afoocfoo')
            self.assertEqual(testType(b'abcb').replace(b'', b'foo', 100), b'fooafoobfoocfoobfoo')
            self.assertEqual(testType(b'abcb').replace(b'', b'foo', 0), b'abcb')
            self.assertEqual(testType(b'abcb').replace(b'', b'foo', 1), b'fooabcb')

            self.assertEqual(testType(b'ooooooo').replace(b'o', b'u'), b'uuuuuuu')

        x = b'abc'
        self.assertEqual(id(x.replace(b'foo', b'bar', 0)), id(x))
        self.assertIs(type(BytesSubclass(x).replace(b'foo', b'bar', 0)), bytes)

        # CPython bug in 2.6 - http://bugs.python.org/issue4348
        x = bytearray(b'abc')
        self.assertTrue(id(x.replace(b'foo', b'bar', 0)) != id(x))
        self.assertIs(type(BytearraySubclass(x).replace(b'foo', b'bar', 0)), bytearray)

    def test_remove(self):
        for toremove in (ord('a'), b'a', Indexable(ord('a')), IndexableOC(ord('a'))):
            b = bytearray(b'abc')
            b.remove(ord('a'))
            self.assertEqual(b, b'bc')

        self.assertRaises(ValueError, b.remove, ord('x'))

        b = bytearray(b'abc')
        self.assertRaises(TypeError, b.remove, bytearray(b'a'))

    def test_reverse(self):
        b = bytearray(b'abc')
        b.reverse()
        self.assertEqual(b, b'cba')
        mv = memoryview(b)
        b.reverse()
        self.assertEqual(b, b'abc')
        self.assertEqual(mv, b'abc')

    # CoreCLR bug xxxx found in build 30324 from silverlight_w2
    def test_rfind(self):
        for testType in types:
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", 1), 5)
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", 3), 5)
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", 7), -1)
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", -1, -2), -1)
            self.assertEqual(testType(b"abc").rfind(b"add", 3, 0), -1)
            self.assertEqual(testType(b'abc').rfind(b'bd'), -1)
            self.assertRaises(TypeError, testType(b'abc').rfind, [1])
            self.assertRaises(TypeError, testType(b'abc').rfind, [1], 1)
            self.assertRaises(TypeError, testType(b'abc').rfind, [1], 1, 2)

            self.assertEqual(testType(b"abc").rfind(b"add", None, 0), -1)
            self.assertEqual(testType(b"abc").rfind(b"add", 3, None), -1)
            self.assertEqual(testType(b"abc").rfind(b"add", None, None), -1)

            self.assertEqual(testType(b'x').rfind(b'x', -2, -1), -1)
            self.assertEqual(testType(b'x').rfind(b'x', -2, 1), 0)
            self.assertEqual(testType(b'x').rfind(b'x', -1, 1), 0)
            self.assertEqual(testType(b'x').rfind(b'x', 0, 0), -1)
            self.assertEqual(testType(b'x').rfind(b'x', 3, 0), -1)

            ans = [
                [0, 0, 0, 0, 1, 2, 0, 1, 2, 3, 3, 3], # start = -5
                [0, 0, 0, 0, 1, 2, 0, 1, 2, 3, 3, 3], # start = -4
                [0, 0, 0, 0, 1, 2, 0, 1, 2, 3, 3, 3], # start = -3
                [-1, -1, -1, -1, 1, 2, -1, 1, 2, 3, 3, 3], # start = -2
                [-1, -1, -1, -1, -1, 2, -1, -1, 2, 3, 3, 3], # start = -1
                [0, 0, 0, 0, 1, 2, 0, 1, 2, 3, 3, 3], # start = 0
                [-1, -1, -1, -1, 1, 2, -1, 1, 2, 3, 3, 3], # start = 1
                [-1, -1, -1, -1, -1, 2, -1, -1, 2, 3, 3, 3], # start = 2
                [-1, -1, -1, -1, -1, -1, -1, -1, -1, 3, 3, 3], # start = 3
                [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1], # start = 4
            ]
            seq = testType(b"abc")
            for start in range(-5, 5):
                for end in range(-6, 6):
                    self.assertEqual(seq.rfind(b"", start, end), ans[start+5][end+6], "for start={0}, end={1}".format(start,end))

    def test_rindex(self):
        for testType in types:
            self.assertRaises(ValueError, testType(b'abc').rindex, 257)
            self.assertEqual(testType(b'abc').rindex(b'a'), 0)
            self.assertEqual(testType(b'abc').rindex(b'a', 0, -1), 0)
            self.assertRaises(TypeError, testType(b'abc').rindex, [1])
            self.assertRaises(TypeError, testType(b'abc').rindex, [1], 1)
            self.assertRaises(TypeError, testType(b'abc').rindex, [1], 1, 2)

            self.assertRaises(ValueError, testType(b'abc').rindex, b'c', 0, -1)
            self.assertRaises(ValueError, testType(b'abc').rindex, b'a', -1)

    def test_rjust(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').rjust, 42, ' ')
            self.assertRaises(TypeError, testType(b'').rjust, 42, '  ')
            self.assertRaises(TypeError, testType(b'').rjust, 42, b'  ')
            self.assertRaises(TypeError, testType(b'').rjust, 42, '\u0100')
            self.assertRaises(TypeError, testType(b'').rjust, 42, [2])
            self.assertEqual(testType(b'abc').rjust(4), b' abc')
            self.assertEqual(testType(b'abc').rjust(4, b'x'), b'xabc')
            self.assertEqual(testType(b'abc').rjust(-4), b'abc')

        x = b'abc'
        self.assertEqual(id(x.rjust(2)), id(x))
        self.assertIs(type(BytesSubclass(x).rjust(2)), bytes)

        x = bytearray(x)
        self.assertTrue(id(x.rjust(2)) != id(x))
        self.assertIs(type(BytearraySubclass(x).rjust(2)), bytearray)

    def test_rpartition(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').rpartition, None)
            self.assertRaises(ValueError, testType(b'').rpartition, b'')

            if testType == bytearray and is_cpython and sys.version_info < (3,6): # https://bugs.python.org/issue20047
                self.assertEqual(testType(b'a\x01c').rpartition([1]), (b'a', b'\x01', b'c'))
            else:
                self.assertRaises(TypeError, testType(b'a\x01c').rpartition, [1])

            self.assertEqual(testType(b'abc').rpartition(b'b'), (b'a', b'b', b'c'))
            self.assertEqual(testType(b'abc').rpartition(b'd'), (b'', b'', b'abc'))

            x = testType(b'abc')
            one, two, three = x.rpartition(b'd')
            if testType == bytearray:
                self.assertTrue(id(three) != id(x))
            else:
                self.assertEqual(id(three), id(x))

            b = testType(b'mississippi')
            self.assertEqual(b.rpartition(b'i'), (b'mississipp', b'i', b''))
            self.assertEqual(type(b.rpartition(b'i')[0]), testType)
            self.assertEqual(type(b.rpartition(b'i')[1]), testType)
            self.assertEqual(type(b.rpartition(b'i')[2]), testType)

            b = testType(b'abcdefgh')
            self.assertEqual(b.rpartition(b'a'), (b'', b'a', b'bcdefgh'))

        one, two, three = b''.rpartition(b'abc')
        self.assertEqual(id(one), id(two))
        self.assertEqual(id(two), id(three))

        one, two, three = bytearray().rpartition(b'abc')
        self.assertTrue(id(one) != id(two))
        self.assertTrue(id(two) != id(three))
        self.assertTrue(id(three) != id(one))

    def test_rsplit(self):
        for testType in types:
            x=testType(b"Hello Worllds")
            self.assertEqual(x.rsplit(), [b'Hello', b'Worllds'])
            s = x.rsplit(b"ll")
            self.assertTrue(s[0] == b"He")
            self.assertTrue(s[1] == b"o Wor")
            self.assertTrue(s[2] == b"ds")

            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").rsplit(b"--", 2) == [b'1--2--3--4--5--6--7--8', b'9', b'0'])

            for temp_string in [b"", b"  ", b"   ", b"\t", b" \t", b"\t ", b"\t\t", b"\n", b"\n\n", b"\n \n"]:
                self.assertEqual(temp_string.rsplit(None), [])

            self.assertEqual(testType(b"ab").rsplit(None), [b"ab"])
            self.assertEqual(testType(b"a b").rsplit(None), [b"a", b"b"])

            self.assertRaises(TypeError, testType(b'').rsplit, [2])
            self.assertRaises(TypeError, testType(b'').rsplit, [2], 2)

    def test_rstrip(self):
        for testType in types:
            self.assertEqual(testType(b'abc ').rstrip(), b'abc')
            self.assertEqual(testType(b' abc ').rstrip(), b' abc')
            self.assertEqual(testType(b' ').rstrip(), b'')

            self.assertEqual(testType(b'abcx').rstrip(b'x'), b'abc')
            self.assertEqual(testType(b'xabc').rstrip(b'x'), b'xabc')
            self.assertEqual(testType(b'x').rstrip(b'x'), b'')

            self.assertRaises(TypeError, testType(b'').rstrip, [2])

        x = b'abc'
        self.assertEqual(id(x.rstrip()), id(x))
        self.assertIs(type(BytesSubclass(x).rstrip()), bytes)
        self.assertIs(type(BytesSubclass(x).rstrip(b'x')), bytes)

        x = bytearray(x)
        self.assertTrue(id(x.rstrip()) != id(x))
        self.assertIs(type(BytearraySubclass(x).rstrip()), bytearray)
        self.assertIs(type(BytearraySubclass(x).rstrip(b'x')), bytearray)

    def test_split(self):
        for testType in types:

            x=testType(b"Hello Worllds")
            self.assertRaises(ValueError, x.split, b'')
            self.assertEqual(x.split(None, 0), [b'Hello Worllds'])
            self.assertEqual(x.split(None, -1), [b'Hello', b'Worllds'])
            self.assertEqual(x.split(None, 2), [b'Hello', b'Worllds'])
            self.assertEqual(x.split(), [b'Hello', b'Worllds'])
            self.assertEqual(testType(b'abc').split(b'c'), [b'ab', b''])
            self.assertEqual(testType(b'abcd').split(b'c'), [b'ab', b'd'])
            self.assertEqual(testType(b'abccdef').split(b'c'), [b'ab', b'', b'def'])
            s = x.split(b"ll")
            self.assertTrue(s[0] == b"He")
            self.assertTrue(s[1] == b"o Wor")
            self.assertTrue(s[2] == b"ds")

            self.assertTrue(testType(b"1,2,3,4,5,6,7,8,9,0").split(b",") == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1,2,3,4,5,6,7,8,9,0").split(b",", -1) == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1,2,3,4,5,6,7,8,9,0").split(b",", 2) == [b'1',b'2',b'3,4,5,6,7,8,9,0'])
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").split(b"--") == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").split(b"--", -1) == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").split(b"--", 2) == [b'1', b'2', b'3--4--5--6--7--8--9--0'])

            self.assertEqual(testType(b"").split(None), [])
            self.assertEqual(testType(b"ab").split(None), [b"ab"])
            self.assertEqual(testType(b"a b").split(None), [b"a", b"b"])
            self.assertEqual(bytearray(b' a bb c ').split(None, 1), [bytearray(b'a'), bytearray(b'bb c ')])

            self.assertEqual(testType(b'    ').split(), [])

            self.assertRaises(TypeError, testType(b'').split, [2])
            self.assertRaises(TypeError, testType(b'').split, [2], 2)

    def test_splitlines(self):
        for testType in types:
            self.assertEqual(testType(b'foo\nbar\n').splitlines(), [b'foo', b'bar'])
            self.assertEqual(testType(b'foo\nbar\n').splitlines(True), [b'foo\n', b'bar\n'])
            self.assertEqual(testType(b'foo\r\nbar\r\n').splitlines(True), [b'foo\r\n', b'bar\r\n'])
            self.assertEqual(testType(b'foo\r\nbar\r\n').splitlines(), [b'foo', b'bar'])
            self.assertEqual(testType(b'foo\rbar\r').splitlines(True), [b'foo\r', b'bar\r'])
            self.assertEqual(testType(b'foo\nbar\nbaz').splitlines(), [b'foo', b'bar', b'baz'])
            self.assertEqual(testType(b'foo\nbar\nbaz').splitlines(True), [b'foo\n', b'bar\n', b'baz'])
            self.assertEqual(testType(b'foo\r\nbar\r\nbaz').splitlines(True), [b'foo\r\n', b'bar\r\n', b'baz'])
            self.assertEqual(testType(b'foo\rbar\rbaz').splitlines(True), [b'foo\r', b'bar\r', b'baz'])

    def test_startswith(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abcdef').startswith, [])
            self.assertRaises(TypeError, testType(b'abcdef').startswith, [], 0)
            self.assertRaises(TypeError, testType(b'abcdef').startswith, [], 0, 1)

            self.assertEqual(testType(b"abcde").startswith(b'c', 2, 6), True)
            self.assertEqual(testType(b"abc").startswith(b'c', 4, 6), False)
            self.assertEqual(testType(b"abcde").startswith(b'cde', 2, 9), True)
            self.assertEqual(testType(b'abc').startswith(b'abcd', 4), False)
            self.assertEqual(testType(b'abc').startswith(b'abc', -3), True)
            self.assertEqual(testType(b'abc').startswith(b'abc', -10), True)
            self.assertEqual(testType(b'abc').startswith(b'abc', -3, 0), False)
            self.assertEqual(testType(b'abc').startswith(b'abc', -10, 0), False)
            self.assertEqual(testType(b'abc').startswith(b'abc', -10, -10), False)
            self.assertEqual(testType(b'abc').startswith(b'ab', 0, -1), True)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), -10), True)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 10), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), -10, 0), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 10, 0), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 1, -10), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 1, -1), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), -1, -2), False)

            self.assertEqual(testType(b'abc').startswith((b'abc', b'def')), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def')), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), -3), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), -3), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), 0), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), 0), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), -3, 3), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), -3, 3), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), 0, 3), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), 0, 3), False)

            self.assertTrue(testType(b'abc===').startswith(b'abc', -2<<222))
            self.assertFalse(testType(b'abc===').startswith(b'abc', 2<<222))
            self.assertFalse(testType(b'abc===').startswith(b'abc', None, -2<<222))
            self.assertTrue(testType(b'abc===').startswith(b'abc', None, 2<<222))
            self.assertTrue(testType(b'abc===').startswith(b'abc', None, None))

            self.assertTrue(testType(b'abc===').startswith((b'xyz', b'abc'), -2<<222))
            self.assertFalse(testType(b'abc===').startswith((b'xyz', b'abc'), 2<<222))
            self.assertFalse(testType(b'abc===').startswith((b'xyz', b'abc'), None, -2<<222))
            self.assertTrue(testType(b'abc===').startswith((b'xyz', b'abc'), None, 2<<222))
            self.assertTrue(testType(b'abc===').startswith((b'xyz', b'abc'), None, None))

            self.assertTrue(testType(b'abc===').startswith((b'xyz', b'abc'), IndexableOC(-2<<222)))
            self.assertFalse(testType(b'abc===').startswith((b'xyz', b'abc'), IndexableOC(2<<222)))
            self.assertFalse(testType(b'abc===').startswith((b'xyz', b'abc'), None, IndexableOC(-2<<222)))
            self.assertTrue(testType(b'abc===').startswith((b'xyz', b'abc'), None, (2<<222)))
            self.assertTrue(testType(b'abc===').startswith((b'xyz', b'b'), IndexableOC(1), IndexableOC(2)))
            self.assertTrue(testType(b'abc===').startswith((b'xyz', memoryview(b'b')), IndexableOC(1), IndexableOC(2)))
            self.assertTrue(testType(b'abc===').startswith((b'xyz', memoryview(bytearray(b'b'))), IndexableOC(1), IndexableOC(2)))

            hw = testType(b"hello world")
            self.assertTrue(hw.startswith(b"hello"))
            self.assertTrue(not hw.startswith(b"heloo"))
            self.assertTrue(hw.startswith(b"llo", 2))
            self.assertTrue(not hw.startswith(b"lno", 2))
            self.assertTrue(hw.startswith(b"wor", 6, 9))
            self.assertTrue(not hw.startswith(b"wor", 6, 7))
            self.assertTrue(not hw.startswith(b"wox", 6, 10))
            self.assertTrue(not hw.startswith(b"wor", 6, 2))

            ans = [
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = -5
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = -4
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = -3
                [False, False, False, False, True, True, False, True, True, True, True, True], # start = -2
                [False, False, False, False, False, True, False, False, True, True, True, True], # start = -1
                [True, True, True, True, True, True, True, True, True, True, True, True], # start = 0
                [False, False, False, False, True, True, False, True, True, True, True, True], # start = 1
                [False, False, False, False, False, True, False, False, True, True, True, True], # start = 2
                [False, False, False, False, False, False, False, False, False, True, True, True], # start = 3
                [False, False, False, False, False, False, False, False, False, False, False, False], # start = 4
            ]
            seq = testType(b"abc")
            for start in range(-5, 5):
                for end in range(-6, 6):
                    self.assertEqual(seq.startswith(b"", start, end), ans[start+5][end+6], "for start={0}, end={1}".format(start,end))

    def test_strip(self):
        for testType in types:
            self.assertEqual(testType(b'abc ').strip(), b'abc')
            self.assertEqual(testType(b' abc').strip(), b'abc')
            self.assertEqual(testType(b' abc ').strip(), b'abc')
            self.assertEqual(testType(b' ').strip(), b'')

            self.assertEqual(testType(b'abcx').strip(b'x'), b'abc')
            self.assertEqual(testType(b'xabc').strip(b'x'), b'abc')
            self.assertEqual(testType(b'xabcx').strip(b'x'), b'abc')
            self.assertEqual(testType(b'x').strip(b'x'), b'')

        x = b'abc'
        self.assertEqual(id(x.strip()), id(x))
        self.assertIs(type(BytesSubclass(x).strip()), bytes)
        self.assertIs(type(BytesSubclass(x).strip(b'x')), bytes)

        x = bytearray(x)
        self.assertTrue(id(x.strip()) != id(x))
        self.assertIs(type(BytearraySubclass(x).strip()), bytearray)
        self.assertIs(type(BytearraySubclass(x).strip(b'x')), bytearray)

    def test_swapcase(self):
        expected = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f'  \
        b'\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%' \
        b'&\'()*+,-./0123456789:;<=>?@abcdefghijklmnopqrstuvwxyz[\\]^_`'          \
        b'ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88' \
        b'\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99'   \
        b'\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa'   \
        b'\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb'   \
        b'\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc'   \
        b'\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd'   \
        b'\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee'   \
        b'\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'

        data = bytearray()
        for i in range(256):
            data.append(i)

        for testType in types:
            self.assertEqual(testType(b'123').swapcase(), b'123')
            b = testType(b'123')
            self.assertTrue(id(b.swapcase()) != id(b))

            self.assertEqual(testType(b'abc').swapcase(), b'ABC')
            self.assertEqual(testType(b'ABC').swapcase(), b'abc')
            self.assertEqual(testType(b'ABc').swapcase(), b'abC')

            self.assertEqual(testType(data).swapcase(), expected)

        x = b''
        self.assertEqual(id(x.swapcase()), id(x))
        self.assertIs(type(BytesSubclass(x).swapcase()), bytes)

        x = bytearray(b'')
        self.assertTrue(id(x.swapcase()) != id(x))
        self.assertIs(type(BytearraySubclass(x).swapcase()), bytearray)

    def test_title(self):
        for testType in types:
            self.assertEqual(testType(b'').title(), b'')
            self.assertEqual(testType(b'foo').title(), b'Foo')
            self.assertEqual(testType(b'Foo').title(), b'Foo')
            self.assertEqual(testType(b'foo bar baz').title(), b'Foo Bar Baz')

            for i in range(256):
                b = bytearray()
                b.append(i)

                if (b >= b'a' and b <= b'z') or (b >= b'A' and b <= b'Z'):
                    continue

                inp = testType(b.join([b'foo', b'bar', b'baz']))
                exp = b.join([b'Foo', b'Bar', b'Baz'])
                self.assertEqual(inp.title(), exp)

        x = b''
        self.assertEqual(id(x.title()), id(x))
        self.assertIs(type(BytesSubclass(x).title()), bytes)

        x = bytearray(b'')
        self.assertTrue(id(x.title()) != id(x))
        self.assertIs(type(BytearraySubclass(x).title()), bytearray)

    def test_translate(self):
        identTable = bytearray()
        for i in range(256):
            identTable.append(i)

        repAtable = bytearray(identTable)
        repAtable[ord('A')] = ord('B')

        for testType in types:
            self.assertRaises(TypeError, testType(b'').translate, {})
            self.assertRaises(ValueError, testType(b'foo').translate, b'')
            self.assertRaises(ValueError, testType(b'').translate, b'')
            self.assertEqual(testType(b'AAA').translate(repAtable), b'BBB')
            self.assertEqual(testType(b'AAA').translate(repAtable, b'A'), b'')
            self.assertRaises(TypeError, b''.translate, identTable, None)

        self.assertEqual(b'AAA'.translate(None, b'A'), b'')
        self.assertEqual(b'AAABBB'.translate(None, b'A'), b'BBB')
        self.assertEqual(b'AAA'.translate(None), b'AAA')
        self.assertEqual(bytearray(b'AAA').translate(None, b'A'),
                b'')
        self.assertEqual(bytearray(b'AAA').translate(None),
                b'AAA')

        b = b'abc'
        self.assertEqual(id(b.translate(None)), id(b))
        self.assertIs(type(BytesSubclass(b).translate(None)), bytes)

        b = b''
        self.assertEqual(id(b.translate(identTable)), id(b))
        self.assertIs(type(BytesSubclass(b).translate(identTable)), bytes)

        b = b''
        self.assertEqual(id(b.translate(identTable, b'')), id(b))
        self.assertIs(type(BytesSubclass(b'').translate(identTable, b'')), bytes)

        b = b''
        self.assertEqual(id(b.translate(identTable, b'')), id(b))

        # CPython bug 4348 - http://bugs.python.org/issue4348
        b = bytearray(b'')
        self.assertTrue(id(b.translate(identTable)) != id(b))
        self.assertIs(type(BytearraySubclass(b).translate(identTable)), bytearray)

        self.assertRaises(TypeError, testType(b'').translate, [])
        self.assertRaises(TypeError, testType(b'').translate, [], [])

    def test_upper(self):
        expected = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f'  \
        b'\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%' \
        b'&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`'          \
        b'ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88' \
        b'\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99'   \
        b'\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa'   \
        b'\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb'   \
        b'\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc'   \
        b'\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd'   \
        b'\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee'   \
        b'\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'

        data = bytearray()
        for i in range(256):
            data.append(i)

        for testType in types:
            self.assertEqual(testType(data).upper(), expected)

        x = b''
        self.assertEqual(id(x.upper()), id(x))
        self.assertIs(type(BytesSubclass(x).upper()), bytes)

        x = bytearray(b'')
        self.assertTrue(id(x.upper()) != id(x))
        self.assertIs(type(BytearraySubclass(x).upper()), bytearray)

    def test_zfill(self):
        for testType in types:
            self.assertEqual(testType(b'abc').zfill(0), b'abc')
            self.assertEqual(testType(b'abc').zfill(4), b'0abc')
            self.assertEqual(testType(b'+abc').zfill(5), b'+0abc')
            self.assertEqual(testType(b'-abc').zfill(5), b'-0abc')
            self.assertEqual(testType(b'').zfill(2), b'00')
            self.assertEqual(testType(b'+').zfill(2), b'+0')
            self.assertEqual(testType(b'-').zfill(2), b'-0')

        b = b'abc'
        self.assertEqual(id(b.zfill(0)), id(b))
        self.assertIs(type(BytesSubclass(b).zfill(0)), bytes)

        b = bytearray(b)
        self.assertTrue(id(b.zfill(0)) != id(b))
        self.assertIs(type(BytearraySubclass(b).zfill(0)), bytearray)

    def test_none(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').replace, b"new")
            self.assertRaises(TypeError, testType(b'abc').replace, b"new", 2)
            self.assertRaises(TypeError, testType(b'abc').center, 0, None)
            if str != bytes:
                self.assertRaises(TypeError, testType(b'abc').fromhex, None)
            self.assertRaises(TypeError, testType(b'abc').decode, 'ascii', None)

            for fn in ['find', 'index', 'rfind', 'count', 'startswith', 'endswith']:
                f = getattr(testType(b'abc'), fn)
                self.assertRaises(TypeError, f, None)
                self.assertRaises(TypeError, f, None, 0)
                self.assertRaises(TypeError, f, None, 0, 2)

            self.assertRaises(TypeError, testType(b'abc').replace, None, b'ef')
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'ef', 1)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None, 1)

    def test_add_mul(self):
        for testType in types:
            self.assertRaises(TypeError, lambda: testType(b"a") + 3)
            self.assertRaises(TypeError, lambda: 3 + testType(b"a"))

            self.assertRaises(TypeError, lambda: "a" * "3")
            self.assertRaises(OverflowError, lambda: "a" * (sys.maxsize + 1))
            self.assertRaises(OverflowError, lambda: (sys.maxsize + 1) * "a")

            # multiply
            self.assertEqual("aaaa", "a" * 4)
            self.assertEqual("aaaa", "a" * myint(4))
            self.assertEqual("aaa", "a" * 3)
            self.assertEqual("a", "a" * True)
            self.assertEqual("", "a" * False)

            self.assertEqual("aaaa", 4 * "a")
            self.assertEqual("aaaa", myint(4) * "a")
            self.assertEqual("aaa", 3 * "a")
            self.assertEqual("a", True * "a")
            self.assertEqual("", False * "a" )

    # zero-length string
    def test_empty_bytes(self):
        for testType in types:
            self.assertEqual(testType(b'').title(), b'')
            self.assertEqual(testType(b'').capitalize(), b'')
            self.assertEqual(testType(b'').count(b'a'), 0)
            table = testType(b'10') * 128
            self.assertEqual(testType(b'').translate(table), b'')
            self.assertEqual(testType(b'').replace(b'a', b'ef'), b'')
            self.assertEqual(testType(b'').replace(b'bc', b'ef'), b'')
            self.assertEqual(testType(b'').split(), [])
            self.assertEqual(testType(b'').split(b' '), [b''])
            self.assertEqual(testType(b'').split(b'a'), [b''])

    def test_encode_decode(self):
        for testType in types:
            self.assertEqual(testType(b'abc').decode(), 'abc')

    def test_encode_decode_error(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').decode, None)

    def test_bytes_subclass(self):
        for testType in types:
            class customstring(testType):
                def __str__(self):  return 'xyz'
                def __repr__(self): return 'foo'
                def __hash__(self): return 42
                def __mul__(self, count): return b'multiplied'
                def __add__(self, other): return 23
                def __len__(self): return 2300
                def __contains__(self, value): return False

            o = customstring(b'abc')
            self.assertEqual(str(o), "xyz")
            self.assertEqual(repr(o), "foo")
            self.assertEqual(hash(o), 42)
            self.assertEqual(o * 3, b'multiplied')
            self.assertEqual(o + b'abc', 23)
            self.assertEqual(len(o), 2300)
            self.assertEqual(b'a' in o, False)

        class custombytearray(bytearray):
            def __init__(self, value):
                bytearray.__init__(self)

        self.assertEqual(custombytearray(42), bytearray())

        class custombytearray(bytearray):
            def __init__(self, value, **args):
                bytearray.__init__(self)

        self.assertEqual(custombytearray(42, x=42), bytearray())

    def test_bytes_equals(self):
        for testType in types:
            x = testType(b'abc') == testType(b'abc')
            y = testType(b'def') == testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(True))

            x = testType(b'abc') != testType(b'abc')
            y = testType(b'def') != testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(False))

            x = testType(b'abcx') == testType(b'abc')
            y = testType(b'defx') == testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(False))

            x = testType(b'abcx') != testType(b'abc')
            y = testType(b'defx') != testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(True))

    def test_bytes_dict(self):
        self.assertNotIn('__init__', bytes.__dict__.keys())
        self.assertIn('__init__', bytearray.__dict__.keys())

        for testType in types:
            #It's OK that __getattribute__ does not show up in the __dict__.  It is
            #implemented.
            self.assertTrue(hasattr(testType, "__getattribute__"), str(testType) + " has no __getattribute__ method")

            extra_str_dict_keys = ["isdecimal", "isnumeric"]
            for temp_key in extra_str_dict_keys:
                self.assertNotIn(temp_key, testType.__dict__.keys())

    def test_bytes_to_numeric(self):
        for testType in types:
            class substring(testType):
                def __int__(self): return 1
                def __complex__(self): return 1j
                def __float__(self): return 1.0

            v = substring(b"123")

            self.assertEqual(float(v), 1.0)
            self.assertEqual(myfloat(v), 1.0)
            self.assertEqual(type(myfloat(v)), myfloat)

            self.assertEqual(int(v), 1)
            self.assertEqual(myint(v), 1)
            self.assertEqual(type(myint(v)), myint)

            self.assertEqual(int(v), 1)
            self.assertEqual(myint(v), 1)
            self.assertEqual(type(myint(v)), myint)

            self.assertEqual(complex(v), 1j)
            self.assertEqual(mycomplex(v), 1j)

            class substring(testType): pass

            v = substring(b"123")

            self.assertEqual(int(v), 123)
            self.assertEqual(float(v), 123.0)

            self.assertEqual(myint(v), 123)
            self.assertEqual(type(myint(v)), myint)

            if testType == str:
                # 2.6 allows this, 3.0 disallows this.
                self.assertEqual(complex(v), 123+0j)
                self.assertEqual(mycomplex(v), 123+0j)
            else:
                self.assertRaises(TypeError, complex, v)
                self.assertRaises(TypeError, mycomplex, v)

    def test_compares(self):
        a = b'A'
        b = b'B'
        bb = b'BB'
        aa = b'AA'
        ab = b'AB'
        ba = b'BA'

        for testType in types:
            for otherType in types:
                self.assertEqual(testType(a) > otherType(b), False)
                self.assertEqual(testType(a) < otherType(b), True)
                self.assertEqual(testType(a) <= otherType(b), True)
                self.assertEqual(testType(a) >= otherType(b), False)
                self.assertEqual(testType(a) == otherType(b), False)
                self.assertEqual(testType(a) != otherType(b), True)

                self.assertEqual(testType(b) > otherType(a), True)
                self.assertEqual(testType(b) < otherType(a), False)
                self.assertEqual(testType(b) <= otherType(a), False)
                self.assertEqual(testType(b) >= otherType(a), True)
                self.assertEqual(testType(b) == otherType(a), False)
                self.assertEqual(testType(b) != otherType(a), True)

                self.assertEqual(testType(a) > otherType(a), False)
                self.assertEqual(testType(a) < otherType(a), False)
                self.assertEqual(testType(a) <= otherType(a), True)
                self.assertEqual(testType(a) >= otherType(a), True)
                self.assertEqual(testType(a) == otherType(a), True)
                self.assertEqual(testType(a) != otherType(a), False)

                self.assertEqual(testType(aa) > otherType(b), False)
                self.assertEqual(testType(aa) < otherType(b), True)
                self.assertEqual(testType(aa) <= otherType(b), True)
                self.assertEqual(testType(aa) >= otherType(b), False)
                self.assertEqual(testType(aa) == otherType(b), False)
                self.assertEqual(testType(aa) != otherType(b), True)

                self.assertEqual(testType(bb) > otherType(a), True)
                self.assertEqual(testType(bb) < otherType(a), False)
                self.assertEqual(testType(bb) <= otherType(a), False)
                self.assertEqual(testType(bb) >= otherType(a), True)
                self.assertEqual(testType(bb) == otherType(a), False)
                self.assertEqual(testType(bb) != otherType(a), True)

                self.assertEqual(testType(ba) > otherType(b), True)
                self.assertEqual(testType(ba) < otherType(b), False)
                self.assertEqual(testType(ba) <= otherType(b), False)
                self.assertEqual(testType(ba) >= otherType(b), True)
                self.assertEqual(testType(ba) == otherType(b), False)
                self.assertEqual(testType(ba) != otherType(b), True)

                self.assertEqual(testType(ab) > otherType(a), True)
                self.assertEqual(testType(ab) < otherType(a), False)
                self.assertEqual(testType(ab) <= otherType(a), False)
                self.assertEqual(testType(ab) >= otherType(a), True)
                self.assertEqual(testType(ab) == otherType(a), False)
                self.assertEqual(testType(ab) != otherType(a), True)

                self.assertEqual(testType(ab) == [], False)

                self.assertRaises(TypeError, lambda: testType(a) > None)
                self.assertRaises(TypeError, lambda: testType(a) < None)
                self.assertRaises(TypeError, lambda: testType(a) <= None)
                self.assertRaises(TypeError, lambda: testType(a) >= None)
                self.assertRaises(TypeError, lambda: None > testType(a))
                self.assertRaises(TypeError, lambda: None < testType(a))
                self.assertRaises(TypeError, lambda: None <= testType(a))
                self.assertRaises(TypeError, lambda: None >= testType(a))


    def test_bytearray(self):
        self.assertRaises(TypeError, hash, bytearray(b'abc'))
        self.assertRaises(TypeError, bytearray(b'').__setitem__, None, b'abc')
        self.assertRaises(TypeError, bytearray(b'').__delitem__, None)
        x = bytearray(b'abc')
        del x[-1]
        self.assertEqual(x, b'ab')

        def f():
            x = bytearray(b'abc')
            x[0:2] = [1j]
        self.assertRaises(TypeError, f)

        x = bytearray(b'abc')
        x[0:1] = [ord('d')]
        self.assertEqual(x, b'dbc')

        x = bytearray(b'abc')
        x[0:3] = x
        self.assertEqual(x, b'abc')

        x = bytearray(b'abc')

        del x[0]
        self.assertEqual(x, b'bc')

        x = bytearray(b'abc')
        x += b'foo'
        self.assertEqual(x, b'abcfoo')

        b = bytearray(b"abc")
        b1 = b
        b += b"def"
        self.assertEqual(b1, b)

        x = bytearray(b'abc')
        x += bytearray(b'foo')
        self.assertEqual(x, b'abcfoo')

        x = bytearray(b'abc')
        x *= 2
        self.assertEqual(x, b'abcabc')

        x = bytearray(b'abcdefghijklmnopqrstuvwxyz')
        x[25:1] = b'x' * 24
        self.assertEqual(x, b'abcdefghijklmnopqrstuvwxyxxxxxxxxxxxxxxxxxxxxxxxxz')

        x = bytearray(b'abcdefghijklmnopqrstuvwxyz')
        x[25:0] = b'x' * 25
        self.assertEqual(x, b'abcdefghijklmnopqrstuvwxyxxxxxxxxxxxxxxxxxxxxxxxxxz')

        tests = ( ((0, 3, None), b'abc', b''),
                ((0, 2, None), b'abc', b'c'),
                ((4, 0, 2),    b'abc', b'abc'),
                ((3, 0, 2),    b'abc', b'abc'),
                ((3, 0, -2),   b'abc', b'ab'),
                ((0, 3, 1),    b'abc', b''),
                ((0, 2, 1),    b'abc', b'c'),
                ((0, 3, 2),    b'abc', b'b'),
                ((0, 2, 2),    b'abc', b'bc'),
                ((0, 3, -1),   b'abc', b'abc'),
                ((0, 2, -1),   b'abc', b'abc'),
                ((3, 0, -1),   b'abc', b'a'),
                ((2, 0, -1),   b'abc', b'a'),
                ((4, 2, -1),   b'abcdef', b'abcf'),
                )

        for indexes, input, result in tests:
            x = bytearray(input)
            if indexes[2] == None:
                del x[indexes[0] : indexes[1]]
                self.assertEqual(x, result)
            else:
                del x[indexes[0] : indexes[1] : indexes[2]]
                self.assertEqual(x, result)

        class myint(int): pass
        class intobj(object):
            def __int__(self):
                return 42

        x = bytearray(b'abe')
        x[-1] = ord('a')
        self.assertEqual(x, b'aba')

        x[-1] = IndexableOC(ord('r'))
        self.assertEqual(x, b'abr')

        x[-1] = Indexable(ord('s'))
        self.assertEqual(x, b'abs')

        def f(): x[-1] = IndexableOC(256)
        self.assertRaises(ValueError, f)

        def f(): x[-1] = Indexable(256)
        self.assertRaises(ValueError, f)

        x[-1] = ord(b'b')
        self.assertEqual(x, b'abb')
        x[-1] = myint(ord('c'))
        self.assertEqual(x, b'abc')

        with self.assertRaises(TypeError):
            x[0:1] = 2

        x = bytearray(b'abc')
        x[0:1] = [2]*2
        self.assertEqual(x, b'\x02\x02bc')
        x[0:2] = b'a'
        self.assertEqual(x, b'abc')
        x[0:1] = b'd'
        self.assertEqual(x, b'dbc')
        x[0:1] = [myint(3)]*3
        self.assertEqual(x, b'\x03\x03\x03bc')
        x[0:3] = [ord('a'), ord('b'), ord('c')]
        self.assertEqual(x, b'abcbc')

        with self.assertRaises(TypeError):
            x[0:1] = [intobj()]

        for setval in [[b'b', b'a', b'r'], (b'b', b'a', b'r'), (98, b'a', b'r'), (Indexable(98), b'a', b'r'), (IndexableOC(98), b'a', b'r')]:
            with self.assertRaises(TypeError):
                x[0:3] = setval

        for setval in [b'bar', bytearray(b'bar'), [98, 97, 114], (98, 97, 114), (Indexable(98), 97, 114), (IndexableOC(98), 97, 114), memoryview(b'bar')]:
            x = bytearray(b'abc')
            x[0:3] = setval
            self.assertEqual(x, b'bar')

            x = bytearray(b'abc')
            x[1:4] = setval
            self.assertEqual(x, b'abar')

            x = bytearray(b'abc')
            x[0:2] = setval
            self.assertEqual(x, b'barc')

            x = bytearray(b'abc')
            x[4:0:2] = setval[-1:-1]
            self.assertEqual(x, b'abc')

            x = bytearray(b'abc')
            x[3:0:2] = setval[-1:-1]
            self.assertEqual(x, b'abc')

            x = bytearray(b'abc')
            x[3:0:-2] = setval[-1:-1]
            self.assertEqual(x, b'ab')

            x = bytearray(b'abc')
            x[3:0:-2] = setval[0:-2]
            self.assertEqual(x, b'abb')

            x = bytearray(b'abc')
            x[0:3:1] = setval
            self.assertEqual(x, b'bar')

            x = bytearray(b'abc')
            x[0:2:1] = setval
            self.assertEqual(x, b'barc')

            x = bytearray(b'abc')
            x[0:3:2] = setval[0:-1]
            self.assertEqual(x, b'bba')

            x = bytearray(b'abc')
            x[0:2:2] = setval[0:-2]
            self.assertEqual(x, b'bbc')

            x = bytearray(b'abc')
            x[0:3:-1] = setval[-1:-1]
            self.assertEqual(x, b'abc')

            x = bytearray(b'abc')
            x[0:2:-1] = setval[-1:-1]
            self.assertEqual(x, b'abc')

            x = bytearray(b'abc')
            x[3:0:-1] = setval[0:-1]
            self.assertEqual(x, b'aab')

            x = bytearray(b'abc')
            x[2:0:-1] = setval[0:-1]
            self.assertEqual(x, b'aab')

        x = bytearray(b'abcd')
        with self.assertRaisesRegex(ValueError, "^attempt to assign bytes of size 1 to extended slice of size 2$"):
            x[0:6:2] = b'a'
        with self.assertRaisesRegex(ValueError, "^attempt to assign bytes of size 3 to extended slice of size 2$"):
            x[0:6:2] = b'abc'

        # lock size by exporting the byte buffer
        mv = memoryview(x)
        with self.assertRaisesRegex(BufferError, "^Existing exports of data: object cannot be re-sized$"):
            x[0:1] = b'ab'
        with self.assertRaisesRegex(BufferError, "^Existing exports of data: object cannot be re-sized$"):
            x[0:3] = b'ab'

        self.assertEqual(bytearray(source=b'abc'), bytearray(b'abc'))
        self.assertEqual(bytearray(source=2), bytearray(b'\x00\x00'))

        self.assertEqual(bytearray(b'abc').__alloc__(), 4)
        self.assertEqual(bytearray().__alloc__(), 0)

        #copying over itself
        x = bytearray(b'abc')
        x[1:4:1] = x
        self.assertEqual(x, b'aabc')

        x = bytearray(b'abcd')
        x[-2:3:1] = x
        self.assertEqual(x, b'ababcdd')

        x = bytearray(b'xyz')
        mv = memoryview(x)
        mv0 = mv[:2]
        mv1 = mv[1:]
        self.assertEqual(bytes(mv0), b'xy')
        self.assertEqual(bytes(mv1), b'yz')
        x[:] = b'abc'
        self.assertEqual(bytes(mv0), b'ab')
        self.assertEqual(bytes(mv1), b'bc')

        x[0:2:1] = mv0
        self.assertEqual(x, b'abc')
        x[1:3:1] = mv0
        self.assertEqual(x, b'aab')
        x[:] = b'abc'
        self.assertEqual(bytes(mv0), b'ab')
        self.assertEqual(bytes(mv1), b'bc')

        x[1:3:1] = mv1
        self.assertEqual(x, b'abc')
        x[0:2:1] = mv1
        self.assertEqual(x, b'bcc')
        x[:] = b'abc'
        self.assertEqual(bytes(mv0), b'ab')
        self.assertEqual(bytes(mv1), b'bc')

        mv.release()
        mv0.release()
        mv1.release()

    def test_bytes(self):
        self.assertEqual(hash(b'abc'), hash(b'abc'))
        self.assertEqual(b'abc', B'abc')

    def test_operators(self):
        for testType in types:
            self.assertRaises(TypeError, lambda : testType(b'abc') * None)
            self.assertRaises(TypeError, lambda : testType(b'abc') + None)
            self.assertRaises(TypeError, lambda : None * testType(b'abc'))
            self.assertRaises(TypeError, lambda : None + testType(b'abc'))
            self.assertEqual(testType(b'abc') * 2, b'abcabc')

            self.assertEqual(testType(b'abc')[0], ord('a'))
            self.assertEqual(testType(b'abc')[-1], ord('c'))

            for otherType in types:

                self.assertEqual(testType(b'abc') + otherType(b'def'), b'abcdef')
                resType = type(testType(b'abc') + otherType(b'def'))
                if testType == bytearray:
                    self.assertEqual(resType, bytearray)
                else:
                    self.assertEqual(resType, bytes)

            self.assertEqual(b'ab' in testType(b'abcd'), True)

            # 2.6 doesn't allow this for testType=bytes, so test for 3.0 in this case
            if testType is not bytes or hasattr(bytes, '__iter__'):
                self.assertEqual(ord(b'a') in testType(b'abcd'), True)

                self.assertRaises(ValueError, lambda : 256 in testType(b'abcd'))

        x = b'abc'
        self.assertEqual(x * 1, x)
        self.assertEqual(1 * x, x)
        self.assertEqual(id(x), id(x * 1))
        self.assertEqual(id(x), id(1 * x))

        x = bytearray(b'abc')
        self.assertEqual(x * 1, x)
        self.assertEqual(1 * x, x)
        self.assertTrue(id(x) != id(x * 1))
        self.assertTrue(id(x) != id(1 * x))

        x = bytearray(b'abc')
        x *= 2
        self.assertEqual(x, b'abcabc')
        mv = memoryview(x)
        with self.assertRaisesRegex(BufferError, "^Existing exports of data: object cannot be re-sized$"):
            x *= 2

        x = bytearray(b'abc')
        x += b'def'
        self.assertEqual(x, b'abcdef')
        mv = memoryview(x)
        with self.assertRaisesRegex(BufferError, "^Existing exports of data: object cannot be re-sized$"):
            x += b'ghi'

    def test_slicing(self):
        for testType in types:
            self.assertEqual(testType(b'abc')[0:3], b'abc')
            self.assertEqual(testType(b'abc')[0:2], b'ab')
            self.assertEqual(testType(b'abc')[3:0:2], b'')
            self.assertEqual(testType(b'abc')[3:0:2], b'')
            self.assertEqual(testType(b'abc')[3:0:-2], b'c')
            self.assertEqual(testType(b'abc')[3:0:-2], b'c')
            self.assertEqual(testType(b'abc')[0:3:1], b'abc')
            self.assertEqual(testType(b'abc')[0:2:1], b'ab')
            self.assertEqual(testType(b'abc')[0:3:2], b'ac')
            self.assertEqual(testType(b'abc')[0:2:2], b'a')
            self.assertEqual(testType(b'abc')[0:3:-1], b'')
            self.assertEqual(testType(b'abc')[0:2:-1], b'')
            self.assertEqual(testType(b'abc')[3:0:-1], b'cb')
            self.assertEqual(testType(b'abc')[2:0:-1], b'cb')

            self.assertRaises(TypeError, testType(b'abc').__getitem__, None)

    def test_ord(self):
        for testType in types:
            self.assertEqual(ord(testType(b'a')), 97)
            self.assertRaisesPartialMessage(TypeError, "expected a character, but string of length 2 found", ord, testType(b'aa'))

    def test_pickle(self):
        import pickle

        for testType in types:
            self.assertEqual(pickle.loads(pickle.dumps(testType(list(range(256))))), testType(list(range(256))))

    @skipUnlessIronPython()
    def test_zzz_cli_features(self):
        import System
        import clr
        clr.AddReference('Microsoft.Dynamic')
        import Microsoft

        for testType in types:
            self.assertEqual(testType(b'abc').Count, 3)
            self.assertEqual(bytearray(b'abc').Contains(ord('a')), True)
            self.assertEqual(list(System.Collections.IEnumerable.GetEnumerator(bytearray(b'abc'))), [ord('a'), ord('b'), ord('c')])
            self.assertEqual(testType(b'abc').IndexOf(ord('a')), 0)
            self.assertEqual(testType(b'abc').IndexOf(ord('d')), -1)

            myList = System.Collections.Generic.List[System.Byte]()
            myList.Add(ord('a'))
            myList.Add(ord('b'))
            myList.Add(ord('c'))

            self.assertEqual(testType(b'').join([myList]), b'abc')

        # bytearray
        '''
        self.assertEqual(bytearray(b'abc') == 'abc', False)
        if not is_net40:
            self.assertEqual(Microsoft.Scripting.IValueEquality.ValueEquals(bytearray(b'abc'), 'abc'), False)
        '''
        self.assertEqual(bytearray(b'abc') == b'abc', True)
        self.assertEqual(b'abc'.IsReadOnly, True)
        self.assertEqual(bytearray(b'abc').IsReadOnly, False)

        self.assertEqual(bytearray(b'abc').Remove(ord('a')), True)
        self.assertEqual(bytearray(b'abc').Remove(ord('d')), False)

        x = bytearray(b'abc')
        x.Clear()
        self.assertEqual(x, b'')

        x.Add(ord('a'))
        self.assertEqual(x, b'a')

        self.assertEqual(x.IndexOf(ord('a')), 0)
        self.assertEqual(x.IndexOf(ord('b')), -1)

        x.Insert(0, ord('b'))
        self.assertEqual(x, b'ba')

        x.RemoveAt(0)
        self.assertEqual(x, b'a')

        System.Collections.Generic.IList[System.Byte].__setitem__(x, 0, ord('b'))
        self.assertEqual(x, b'b')

        # bytes
        self.assertRaises(System.InvalidOperationException, b'abc'.Remove, ord('a'))
        self.assertRaises(System.InvalidOperationException, b'abc'.Remove, ord('d'))
        self.assertRaises(System.InvalidOperationException, b'abc'.Clear)
        self.assertRaises(System.InvalidOperationException, b'abc'.Add, ord('a'))
        self.assertRaises(System.InvalidOperationException, b'abc'.Insert, 0, ord('b'))
        self.assertRaises(System.InvalidOperationException, b'abc'.RemoveAt, 0)
        self.assertRaises(System.InvalidOperationException, System.Collections.Generic.IList[System.Byte].__setitem__, b'abc', 0, ord('b'))

        lst = System.Collections.Generic.List[System.Byte]()
        lst.Add(42)
        self.assertEqual(ord(lst), 42)
        lst.Add(42)
        self.assertRaisesMessage(TypeError, "expected a character, but string of length 2 found", ord, lst)

    def test_bytes_hashing(self):
        """test interaction of bytes w/ hashing modules"""
        import hashlib

        for hashLib in (hashlib.sha1, hashlib.sha256, hashlib.sha512, hashlib.sha384, hashlib.md5):
            x = hashLib(b'abc')
            x.update(b'abc')

            #For now just make sure this doesn't throw
            temp = hashLib(bytearray(b'abc'))
            x.update(bytearray(b'abc'))

    def test_add(self):
        self.assertEqual(bytearray(b"abc") + memoryview(b"def"), b"abcdef")

    def test_literals(self):
        s = "'ÿ'"
        for prefix in ["b", "rb"]:
            self.assertRaises(SyntaxError, eval, prefix + s)
            self.assertRaises(SyntaxError, eval, prefix + "\\" + s)
            self.assertRaises(SyntaxError, eval, prefix + "\\\\" + s)

run_test(__name__)
