# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests for the _blake2 module which are not covered by test_hashlib from the StdLib. The StdLib
tests which check salt, person and parameter validation also use tree hashing which is not
supported. Expected values were generated with CPython 3.12.
'''

import _blake2
import unittest

from iptest import is_cli, run_test

def data(n):
    return bytes((i * 7 + 3) & 0xff for i in range(n))

PARAMS = [
    ('blake2b', {'salt': b'salt'}, 0, 'e13acc0cbc033165279da570723a79e4f35d37141b094225b4d26d18e9a6fa7ad1a74ae3ed749b7aa9beb70a25e73d35b2e4a94477a8d54f00330331481c013a'),
    ('blake2b', {'salt': b'salt'}, 200, 'c068456b3c55510c2766676e8ea00122628ce5cef06e82a1007c25c940e4fd885464fd2f1b80d395f8fec6c73c52a382b0c508b7f13dc729a912d5198090a439'),
    ('blake2b', {'salt': b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f', 'person': b'me'}, 0, 'ca6aa446967c6f33d49e373173186554863b28ed672f4ed8a3641f23d66a4412c97f6b481fadace0560af4975125c7b40195eceef5508d560b5e0b5a72ee6fe3'),
    ('blake2b', {'salt': b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f', 'person': b'me'}, 200, '50624555fe019e59e6c1bfb37ccf4a85579d71020d6422159477b5df2c9886ca8ff6b4c090c4f0b201db05907a84b37bc69bbb06f47a4744ddfdbd38f8a41ecf'),
    ('blake2b', {'key': b'k', 'salt': b's', 'person': b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f', 'digest_size': 33}, 0, 'eefb2381791f179994c8be25dca7c0877fbe91096b2d9a4670190ad6f61e177297'),
    ('blake2b', {'key': b'k', 'salt': b's', 'person': b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f', 'digest_size': 33}, 200, '764f4fbf33a2afbcafb25a511c6f34b3ea10975684c4e67036c922d0bf8c649b32'),
    ('blake2s', {'salt': b'salt'}, 0, '57a3dca94c3e8c943d2007ad780932ff95f585ddc7c9dca3a0d9f2481b9ab4cc'),
    ('blake2s', {'salt': b'salt'}, 200, '3b2edb1052b39c7647ad808e6f7d9ea7b28363f5e874538be7468cf0b2c70170'),
    ('blake2s', {'salt': b'\x00\x01\x02\x03\x04\x05\x06\x07', 'person': b'me'}, 0, 'ced8049ec88e4dd872b5b038161dd8706ce9b386d3d2ab6cca127e16ece2c950'),
    ('blake2s', {'salt': b'\x00\x01\x02\x03\x04\x05\x06\x07', 'person': b'me'}, 200, '195a611949a7860107838cb7366f5940208eb0c05328f97944b3d4959971a5a3'),
    ('blake2s', {'key': b'k', 'salt': b's', 'person': b'\x00\x01\x02\x03\x04\x05\x06\x07', 'digest_size': 17}, 0, 'aae1c76391b9f3e5285a8c1d8bac9f7649'),
    ('blake2s', {'key': b'k', 'salt': b's', 'person': b'\x00\x01\x02\x03\x04\x05\x06\x07', 'digest_size': 17}, 200, 'cfb552122d9f5ada0bc95cece7d44d861d'),
]

class _Blake2Test(unittest.TestCase):

    def test_constants(self):
        self.assertEqual(_blake2.BLAKE2B_SALT_SIZE, 16)
        self.assertEqual(_blake2.BLAKE2B_PERSON_SIZE, 16)
        self.assertEqual(_blake2.BLAKE2B_MAX_KEY_SIZE, 64)
        self.assertEqual(_blake2.BLAKE2B_MAX_DIGEST_SIZE, 64)
        self.assertEqual(_blake2.BLAKE2S_SALT_SIZE, 8)
        self.assertEqual(_blake2.BLAKE2S_PERSON_SIZE, 8)
        self.assertEqual(_blake2.BLAKE2S_MAX_KEY_SIZE, 32)
        self.assertEqual(_blake2.BLAKE2S_MAX_DIGEST_SIZE, 32)

    def test_parameters(self):
        for name, kwargs, n, hexdigest in PARAMS:
            with self.subTest(name=name, kwargs=kwargs, n=n):
                ctor = getattr(_blake2, name)
                self.assertEqual(ctor(data(n), **kwargs).hexdigest(), hexdigest)
                h = ctor(**kwargs)
                h.update(data(n))
                self.assertEqual(h.copy().hexdigest(), hexdigest)

    def test_errors(self):
        for ctor in [_blake2.blake2b, _blake2.blake2s]:
            with self.subTest(ctor=ctor):
                self.assertRaises(TypeError, ctor, None)
                self.assertRaises(ValueError, ctor, digest_size=0)
                self.assertRaises(ValueError, ctor, digest_size=ctor.MAX_DIGEST_SIZE + 1)
                self.assertRaises(ValueError, ctor, key=b'x' * (ctor.MAX_KEY_SIZE + 1))
                self.assertRaises(ValueError, ctor, salt=b'x' * (ctor.SALT_SIZE + 1))
                self.assertRaises(ValueError, ctor, person=b'x' * (ctor.PERSON_SIZE + 1))
                self.assertRaises(ValueError, ctor, fanout=256)
                self.assertRaises(ValueError, ctor, depth=0)
                self.assertRaises(ValueError, ctor, node_depth=256)
                self.assertRaises(ValueError, ctor, inner_size=ctor.MAX_DIGEST_SIZE + 1)
                self.assertRaises((ValueError, OverflowError), ctor, leaf_size=-1)
                self.assertRaises(OverflowError, ctor, leaf_size=1<<32)
                self.assertRaises((ValueError, OverflowError), ctor, node_offset=-1)
        self.assertRaises(OverflowError, _blake2.blake2s, node_offset=1<<48)

    def test_not_picklable(self):
        import copy, pickle
        for ctor in [_blake2.blake2b, _blake2.blake2s]:
            with self.subTest(ctor=ctor):
                h = ctor(b'abc', key=b'key')
                self.assertRaises(TypeError, copy.copy, h)
                self.assertRaises(TypeError, copy.deepcopy, h)
                self.assertRaises(TypeError, pickle.dumps, h)

    @unittest.skipUnless(is_cli, 'IronPython does not support tree hashing')
    def test_tree_hashing_not_supported(self):
        for ctor in [_blake2.blake2b, _blake2.blake2s]:
            with self.subTest(ctor=ctor):
                self.assertRaises(NotImplementedError, ctor, fanout=2)
                self.assertRaises(NotImplementedError, ctor, last_node=True)
                self.assertRaises(NotImplementedError, ctor, node_offset=1)

run_test(__name__)
