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

from iptest.assert_util import *

from System import Nullable

def test_object():
    a = object()
    b = object()

    AssertEqual(True, a is a)
    AssertEqual(False, a is b)
    AssertEqual(False, a is not a)
    AssertEqual(True, a is not b)

def test_bool_nullablebool():
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
        AssertEqual(result, a is b)
        AssertEqual(result, b is a)
        AssertEqual(not result, a is not b)
        AssertEqual(not result, b is not a)