# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# Test that hash and equality operations cooperate for numbers.
#  cmp(x,y)==0 --> hash(x) == hash(y).
#
# Python has equality comparisons between int, float, long (BigInteger), and Complex

import unittest

from iptest import is_32, is_cli, run_test, skipUnlessIronPython

class NumberHashTest(unittest.TestCase):

    def check(self,x,y):
        """Check the hash invariant for equal objects."""
        self.assertEqual(x, y)
        self.assertTrue(hash(x) == hash(y))

    def test_integer(self):
        i = 123456
        self.check(i, int(i))
        self.check(i, float(i))
        self.check(i, complex(i,0))

    def test_float_long(self):
        """bug 315746"""
        f=float(1.23e300)
        l=int(f)
        self.check(f,l)

    def test_complex_float(self):
        """Test with complex + float + int"""
        for (c, f) in [ (0j+3.5, 3.5), (3e-6 + 0j, 3.0e-6), (4.5e+300 + 0j, 4.5e+300)]:
            self.check(c,f)

    def test_complex_bigint(self):
        """Test with complex and BigInts - Bug 320650"""
        l=5294967296
        c=complex(l,0)
        self.check(l,c)

    def test_floathash_quality(self):
        """Test that floating hash is decent enough to distribute between decimal digits - Bug 320645"""
        f = 1.5
        h1 = hash(f)
        h2 = hash(f +.1)
        self.assertTrue(h1 != h2)

    def test_bigint_hash_quality(self):
        """Ensure that we have a decent hash function that doesn't just map everything to zero - bug 320659"""
        l1=int(1.23e300)
        h1 = hash(l1)
        self.assertTrue(h1 != 0)
        l2 = l1 + 1
        self.assertTrue(l1 != l2)
        h2 = hash(l2)
        self.assertTrue(h1 != h2)

    def test_userhash_result(self):
        class x(object):
            def __init__(self, hash):
                self.__hash = hash
            def __hash__(self):
                return self.__hash
        
        self.assertEqual(hash(x(1)), 1)
        if is_cli or is_32:
            self.assertEqual(hash(x(1<<32)), 2)
        else:
            self.assertEqual(hash(x(1<<63)), 4)

    @skipUnlessIronPython()
    def test_cli_number_hash(self):
        from iptest.type_util import clr_numbers
        
        for name, value in clr_numbers.items():
            if "Decimal" in name: continue # https://github.com/IronLanguages/ironpython2/issues/527
            if "Single" in name:
                self.assertEqual(hash(value), hash(float(value)))
            else:
                self.assertEqual(hash(value), hash(int(value)))

    @unittest.skipIf(is_cli, "https://github.com/IronLanguages/ironpython2/issues/528")
    def test_bigint_hash_subclass(self):
        class x(int):
            def __hash__(self): return 42
            
        self.assertEqual(hash(x()), 42)

run_test(__name__)
