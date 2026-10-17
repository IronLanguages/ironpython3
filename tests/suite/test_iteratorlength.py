# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import operator
import sys
import unittest

from iptest import IronPythonTestCase, run_test


class StringLengthHint(object):
    def __iter__(self):
        return iter(range(10))

    def __length_hint__(self):
        return "This isn't a valid length hint"

class Sequence(object):
    def __init__(self, n):
        self.n = n

    def __getitem__(self, i):
        if i >= self.n:
            raise IndexError
        return i

    def __len__(self):
        return self.n

class SequenceWithoutLen(object):
    def __getitem__(self, i):
        if i >= 3:
            raise IndexError
        return i

class SequenceWithBadLen(Sequence):
    def __len__(self):
        raise ZeroDivisionError

class LengthHintTest(IronPythonTestCase):
    def test_invalid_hint(self):
        # It's a TypeError if __length_hint__ returns something other than an int or NotImplemented
        self.assertRaises(TypeError, list, StringLengthHint())
        self.assertRaises(TypeError, [].extend, StringLengthHint())
        b = bytearray(range(10))
        self.assertRaises(TypeError, b.extend, StringLengthHint())

    def test_str_iterator(self):
        it = iter('abc')
        self.assertEqual(it.__length_hint__(), 3)
        next(it)
        self.assertEqual(it.__length_hint__(), 2)
        self.assertEqual(operator.length_hint(it), 2)
        self.assertEqual(list(it), ['b', 'c'])
        self.assertEqual(it.__length_hint__(), 0)
        self.assertEqual(next(it, None), None)
        self.assertEqual(it.__length_hint__(), 0)

    def test_str_iterator_setstate(self):
        for index, expected in [(-5, 'ab'), (0, 'ab'), (1, 'b'), (2, ''), (10, '')]:
            it = iter('ab')
            it.__setstate__(index)
            self.assertEqual(it.__length_hint__(), len(expected))
            self.assertEqual(''.join(it), expected)

    def test_dict_iterators(self):
        d = {1: 2, 3: 4}
        for it in (iter(d), iter(d.keys()), iter(d.values()), iter(d.items())):
            self.assertRaises(TypeError, len, it)
            self.assertEqual(it.__length_hint__(), 2)
            next(it)
            self.assertEqual(it.__length_hint__(), 1)
            self.assertEqual(operator.length_hint(it), 1)
            list(it)
            self.assertEqual(it.__length_hint__(), 0)

    def test_sequence_iterator(self):
        s = Sequence(3)
        it = iter(s)
        self.assertEqual(it.__length_hint__(), 3)
        next(it)
        self.assertEqual(it.__length_hint__(), 2)
        self.assertEqual(operator.length_hint(it), 2)

        # the hint follows the length of the sequence
        s.n = 5
        self.assertEqual(it.__length_hint__(), 4)
        s.n = 0
        self.assertEqual(it.__length_hint__(), 0)

        s.n = 3
        self.assertEqual(list(it), [1, 2])
        self.assertEqual(it.__length_hint__(), 0)
        s.n = 5
        self.assertEqual(it.__length_hint__(), 0)

        it = iter(Sequence(3))
        it.__setstate__(2)
        self.assertEqual(it.__length_hint__(), 1)

    def test_sequence_iterator_without_len(self):
        it = iter(SequenceWithoutLen())
        self.assertIs(it.__length_hint__(), NotImplemented)
        self.assertEqual(operator.length_hint(it, 42), 42)
        self.assertEqual(list(it), [0, 1, 2])
        self.assertEqual(it.__length_hint__(), 0)

    def test_sequence_iterator_bad_len(self):
        it = iter(SequenceWithBadLen(3))
        self.assertRaises(ZeroDivisionError, it.__length_hint__)
        self.assertRaises(ZeroDivisionError, operator.length_hint, it)

run_test(__name__)