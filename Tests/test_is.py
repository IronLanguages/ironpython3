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

from iptest import run_test, skipUnlessIronPython


class IsTest(unittest.TestCase):
    def test_object(self):
        a = object()
        b = object()

        self.assertTrue(a is a)
        self.assertFalse(a is b)
        self.assertFalse(a is not a)
        self.assertTrue(a is not b)
    
    @skipUnlessIronPython()
    def test_bool_nullablebool(self):
        from System import Nullable
        tc = [
            # (a, b, a is b)
            (True, True, True), 
            (True, False, False), 
            (Nullable[bool](True), True, True), # https://github.com/IronLanguages/main/issues/1299
            (Nullable[bool](True), False, False),
            (Nullable[bool](False), True, False), # dito
            (Nullable[bool](False), False, True),
            (None, True, False), 
            (None, False, False),
            ]
            
        for a, b, result in tc:
            self.assertEqual(result, a is b)
            self.assertEqual(result, b is a)
            self.assertEqual(not result, a is not b)
            self.assertEqual(not result, b is not a)

run_test(__name__)