# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import marshal
import os
import unittest

from iptest import IronPythonTestCase, is_cli, is_64, is_osx, big, run_test

class MarshalTest(IronPythonTestCase):
    def setUp(self):
        super(MarshalTest, self).setUp()
        self.tfn = os.path.join(self.temporary_dir, 'tempfile.bin')

    def test_functionality(self):
        objects = [ None,
                    True, False,
                    '', 'a', 'abc',
                    -3, 0, 10,
                    254, -255, 256, 257,
                    65534, 65535, -65536,
                    3.1415926,

                    big(0),
                    -1234567890123456789,
                    2**33,
                    [],
                    [ [] ], [ [], [] ],
                    ['abc'], [1, 2],
                    tuple(),
                    (), ( (), (), ),
                    (1,), (1,2,3),
                    {},
                    { 'abc' : {} },
                    {1:2}, {1:2, 4:'4', 5:None},
                    0+1j, 2-3.23j,
                    set(),
                    set(['abc', -5]),
                    set([1, (2.1, big(3)), frozenset([5]), 'x']),
                    frozenset(),
                    frozenset(['abc', -5]),
                    frozenset([1, (2.1, big(3)), frozenset([5]), 'x'])
                ]

        if is_cli:
            import System
            objects.extend(
                [
                System.Single.Parse('-2345678'),
                System.Int64.Parse('2345678'),
                ])

        # dumps / loads
        for x in objects:
            s = marshal.dumps(x)
            x2 = marshal.loads(s)
            self.assertEqual(x, x2)

            # on 64-bit the order in set/frozenset isn't the same after dumps/loads
            if (is_64 or is_osx) and isinstance(x, (set, frozenset)): continue

            s2 = marshal.dumps(x2)
            self.assertEqual(s, s2)

        # dump / load
        for x in objects:
            with open(self.tfn, 'wb') as f:
                marshal.dump(x, f)

            with open(self.tfn, 'rb') as f:
                x2 = marshal.load(f)

            self.assertEqual(x, x2)

    def test_memoryview(self):
        for s in [b'', b' ', b'abc ', b'abcdef']:
            x = marshal.dumps(memoryview(s))
            self.assertEqual(marshal.loads(x), s)

        for s in [b'', b' ', b'abc ', b'abcdef']:
            with open(self.tfn, 'wb') as f:
                marshal.dump(memoryview(s), f)

            with open(self.tfn, 'rb') as f:
                x2 = marshal.load(f)

            self.assertEqual(s, x2)

    def test_negative(self):
        self.assertRaises(TypeError if is_cli else AttributeError, marshal.dump, 2, None)
        self.assertRaises(TypeError, marshal.load, '-1', None)

        l = [1, 2]
        l.append(l)
        for v in range(3):
            self.assertRaises(ValueError, marshal.dumps, l, v) ## infinite loop
        if marshal.version > 2:
            self.assertEqual(marshal.dumps(l), b'\xdb\x03\x00\x00\x00\xe9\x01\x00\x00\x00\xe9\x02\x00\x00\x00r\x00\x00\x00\x00')

        class my: pass
        self.assertRaises(ValueError, marshal.dumps, my())  ## unmarshallable object

    def test_file_multiple_reads(self):
        """calling load w/ a file should only advance the length of the file"""
        l = []
        for i in range(10):
            l.append(marshal.dumps({i:i}))

        data = b''.join(l)
        with open('tempfile.txt', 'wb') as f:
            f.write(data)

        with open('tempfile.txt', 'rb') as f:
            for i in range(10):
                obj = marshal.load(f)
                self.assertEqual(obj, {i:i})

        self.delete_files('tempfile.txt')

    def test_string_interning(self):
        self.assertEqual(marshal.dumps(['abc', 'abc'], 0), b'[\x02\x00\x00\x00u\x03\x00\x00\x00abcu\x03\x00\x00\x00abc')
        self.assertEqual(marshal.dumps(['abc', 'abc'], 1), b'[\x02\x00\x00\x00u\x03\x00\x00\x00abcu\x03\x00\x00\x00abc')
        self.assertEqual(marshal.dumps(['abc', 'abc'], 2), b'[\x02\x00\x00\x00u\x03\x00\x00\x00abcu\x03\x00\x00\x00abc')
        self.assertEqual(marshal.dumps(['abc', 'abc', 'abc', 'def', 'def'], 1), b'[\x05\x00\x00\x00u\x03\x00\x00\x00abcu\x03\x00\x00\x00abcu\x03\x00\x00\x00abcu\x03\x00\x00\x00defu\x03\x00\x00\x00def')
        self.assertEqual(marshal.loads(marshal.dumps(['abc', 'abc'], 1)), ['abc', 'abc'])
        self.assertEqual(marshal.loads(marshal.dumps(['abc', 'abc'], 0)), ['abc', 'abc'])
        self.assertEqual(marshal.loads(marshal.dumps(['abc', 'abc', 'abc', 'def', 'def'], 1)), ['abc', 'abc', 'abc', 'def', 'def'])

    def test_binary_floats(self):
        self.assertEqual(marshal.dumps(2.0, 2), b'g\x00\x00\x00\x00\x00\x00\x00@')
        if marshal.version > 2:
            self.assertEqual(marshal.dumps(2.0), b'\xe7\x00\x00\x00\x00\x00\x00\x00@')
        else:
            self.assertEqual(marshal.dumps(2.0), b'g\x00\x00\x00\x00\x00\x00\x00@')
        if is_cli: #https://github.com/IronLanguages/main/issues/854
            self.assertEqual(marshal.dumps(2.0, 1), b'f\x032.0')
        else:
            self.assertEqual(marshal.dumps(2.0, 1), b'f\x012')
        self.assertEqual(marshal.loads(marshal.dumps(2.0, 2)), 2.0)

    def test_cp24547(self):
        if marshal.version > 2:
            self.assertEqual(marshal.dumps(2**33), b'\xec\x03\x00\x00\x00\x00\x00\x00\x00\x08\x00')
        else:
            self.assertEqual(marshal.dumps(2**33), b"l\x03\x00\x00\x00\x00\x00\x00\x00\x08\x00")

run_test(__name__)
