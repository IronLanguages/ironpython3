# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
This tests what CPythons test_sha.py does not hit.
'''

import _sha512
import unittest

from iptest import is_cli, run_test

class _Sha512Test(unittest.TestCase):
    def test_sanity(self):
        self.assertTrue("__doc__" in dir(_sha512))
        if is_cli:
            self.assertEqual(_sha512.__doc__, "SHA512 hash algorithm")
        self.assertTrue("__name__" in dir(_sha512))
        self.assertTrue("sha384" in dir (_sha512))
        self.assertTrue("sha512" in dir(_sha512))

    def test_sha512_sanity(self):
        x = _sha512.sha512()
        self.assertEqual(x.block_size, 128)
        self.assertEqual(x.digest(),
                b"\xcf\x83\xe15~\xef\xb8\xbd\xf1T(P\xd6m\x80\x07\xd6 \xe4\x05\x0bW\x15\xdc\x83\xf4\xa9!\xd3l\xe9\xceG\xd0\xd1<]\x85\xf2\xb0\xff\x83\x18\xd2\x87~\xec/c\xb91\xbdGAz\x81\xa582z\xf9'\xda>")
        self.assertEqual(x.digest_size, 64)
        self.assertEqual(x.digest_size, x.digestsize)
        self.assertEqual(x.hexdigest(),
                'cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e')
        self.assertEqual(x.name, "SHA512")
        x.update(b"abc")
        self.assertEqual(x.hexdigest(),
                'ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f')

        x_copy = x.copy()
        self.assertTrue(x!=x_copy)
        self.assertEqual(x.hexdigest(), x_copy.hexdigest())

    def test_sha384_sanity(self):
        x = _sha512.sha384()
        self.assertEqual(x.block_size, 128)
        self.assertEqual(x.digest(),
                b"8\xb0`\xa7Q\xac\x968L\xd92~\xb1\xb1\xe3j!\xfd\xb7\x11\x14\xbe\x07CL\x0c\xc7\xbfc\xf6\xe1\xda'N\xde\xbf\xe7oe\xfb\xd5\x1a\xd2\xf1H\x98\xb9[")
        self.assertEqual(x.digest_size, 48)
        self.assertEqual(x.digest_size, x.digestsize)
        self.assertEqual(x.hexdigest(),
                '38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b')
        self.assertEqual(x.name, "SHA384")
        x.update(b"abc")
        self.assertEqual(x.hexdigest(),
                'cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7')

        x_copy = x.copy()
        self.assertTrue(x!=x_copy)
        self.assertEqual(x.hexdigest(), x_copy.hexdigest())

run_test(__name__)
