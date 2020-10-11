# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from _collections import deque
import unittest

class DequeTest(unittest.TestCase):
    def test_deque_cmp_empty(self):
        """https://github.com/IronLanguages/ironpython3/pull/973"""
        class AlwaysLessThan:
            def __lt__(self, other):
                return True

        self.assertFalse(deque([AlwaysLessThan()]) < deque())

if __name__ == "__main__":
    unittest.main()
