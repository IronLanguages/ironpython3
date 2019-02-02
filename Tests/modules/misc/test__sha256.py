# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
This tests what CPythons test_sha.py does not hit.
'''

import _sha256
import unittest

from iptest import is_cli, run_test

class _Sha256Test(unittest.TestCase):

    def test_sanity(self):
        self.assertTrue("__doc__" in dir(_sha256))
        if is_cli:
            self.assertEqual(_sha256.__doc__, "SHA256 hash algorithm")
        self.assertTrue("__name__" in dir(_sha256))
        self.assertTrue("__loader__" in dir(_sha256))
        self.assertTrue("__spec__" in dir(_sha256))
        self.assertTrue("sha224" in dir (_sha256))
        self.assertTrue("sha256" in dir(_sha256))
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21920
        self.assertEqual(len(dir(_sha256)), 7, "there should be 7 attributes in the _sha256 module")

    def test_sha256_sanity(self):
        x = _sha256.sha256()
        self.assertEqual(x.block_size, 64)
        self.assertEqual(x.digest(),
                "\xe3\xb0\xc4B\x98\xfc\x1c\x14\x9a\xfb\xf4\xc8\x99o\xb9$'\xaeA\xe4d\x9b\x93L\xa4\x95\x99\x1bxR\xb8U")
        self.assertEqual(x.digest_size, 32)
        self.assertEqual(x.digest_size, x.digestsize)
        self.assertEqual(x.hexdigest(),
                'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855')
        self.assertEqual(x.name, "SHA256")
        x.update("abc")
        self.assertEqual(x.hexdigest(),
                'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad')

        x_copy = x.copy()
        self.assertTrue(x!=x_copy)
        self.assertEqual(x.hexdigest(), x_copy.hexdigest())

    def test_sha224_sanity(self):
        x = _sha256.sha224()
        self.assertEqual(x.block_size, 64)
        self.assertEqual(x.digest(),
                '\xd1J\x02\x8c*:+\xc9Ga\x02\xbb(\x824\xc4\x15\xa2\xb0\x1f\x82\x8e\xa6*\xc5\xb3\xe4/')
        self.assertEqual(x.digest_size, 28)
        self.assertEqual(x.digest_size, x.digestsize)
        self.assertEqual(x.hexdigest(),
                'd14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f')
        self.assertEqual(x.name, "SHA224")
        x.update("abc")
        self.assertEqual(x.hexdigest(),
                '23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7')

        x_copy = x.copy()
        self.assertTrue(x!=x_copy)
        self.assertEqual(x.hexdigest(), x_copy.hexdigest())

run_test(__name__)
