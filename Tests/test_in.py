#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

import unittest

from iptest import run_test

class C:
    x = "Hello"
    def __contains__(self, y):
        return self.x == y;


class D:
    x = (1,2,3,4,5,6,7,8,9,10)
    def __getitem__(self, y):
        return self.x[y];

class InTest(unittest.TestCase):
    def test_basic(self):
        self.assertTrue('abc' in 'abcd')

    def test_class(self):
        h = "Hello"
        c = C()
        self.assertTrue(c.__contains__("Hello"))
        self.assertTrue(c.__contains__(h))
        self.assertTrue(not (c.__contains__('abc')))

        self.assertTrue(h in c)
        self.assertTrue("Hello" in c)

        d = D()
        self.assertTrue(1 in d)
        self.assertTrue(not(11 in d))

run_test(__name__)
