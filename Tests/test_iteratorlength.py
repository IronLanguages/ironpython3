# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, run_test


class StringLengthHint(object):
    def __iter__(self):
        return iter(range(10))

    def __length_hint__(self):
        return "This isn't a valid length hint"

class LengthHintTest(IronPythonTestCase):
    def test_invalid_hint(self):
        # It's a TypeError if __length_hint__ returns something other than an int or NotImplemented
        self.assertRaises(TypeError, list, StringLengthHint())
        self.assertRaises(TypeError, [].extend, StringLengthHint())
        b = bytearray(range(10))
        self.assertRaises(TypeError, b.extend, StringLengthHint())

run_test(__name__)