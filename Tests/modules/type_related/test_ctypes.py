# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests for CPython's ctypes module.
'''

from ctypes import *
from array import array
import gc

from iptest import IronPythonTestCase, is_cli, is_mono, big, run_test

class CTypesTest(IronPythonTestCase):
    export_error_msg = "Existing exports of data: object cannot be re-sized" if is_cli else "cannot resize an array that is exporting buffers"
    readonly_error_msg = "Object is not writable." if is_cli else "underlying buffer is not writable"

    def test_from_array(self):
        arr = array('i', range(16))
        c = (c_int * 15).from_buffer(arr, sizeof(c_int))

        self.assertEqual(c[:], arr.tolist()[1:])

        arr[9] = -1
        self.assertEqual(c[8], -1)

        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.insert, 10, 100)

        del c
        gc.collect()
        arr.append(100)
        self.assertEqual(arr[-1], 100)

    def test_from_memoryview(self):
        arr = array('i', range(16))
        with memoryview(arr) as mv:
            self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
            mv[10] = -2
            c = (c_int * 15).from_buffer(mv, sizeof(c_int))

        del mv
        gc.collect()

        arr[9] = -1
        self.assertEqual(c[8], -1)
        self.assertEqual(c[9], -2)

        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.insert, 10, 100)
        l = [c]
        l.append(l)
        del c
        gc.collect()

        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
        del l
        gc.collect()
        arr.append(100)
        self.assertEqual(arr[-1], 100)

    def test_from_bytes(self):
        b = b"abcd" * 4
        ba = bytearray(b)

        c = (c_char * 15).from_buffer(ba, sizeof(c_char))
        self.assertEqual(c[:], b[1:])
        self.assertRaisesMessage(TypeError, self.readonly_error_msg, (c_char * 15).from_buffer, b, sizeof(c_char))

run_test(__name__)
