# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

class IntSubclass(int): pass

class IntTest(unittest.TestCase):
    def test_from_bytes(self):
        self.assertEqual(type(int.from_bytes(b"abc", "big")), int)
        self.assertEqual(type(IntSubclass.from_bytes(b"abc", "big")), IntSubclass) # https://github.com/IronLanguages/ironpython3/pull/973

if __name__ == "__main__":
    unittest.main()
