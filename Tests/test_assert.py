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

#
# test assert
#
from iptest.assert_util import *
if is_cli: import System

def test_positive():
    try:
        assert True
    except AssertionError as e:
        raise "Should have been no exception!"

    try:
        assert True, 'this should always pass'
    except AssertionError as e:
        raise "Should have been no exception!"
        
def test_negative():
    ok = False
    try:
        assert False
    except AssertionError as e:
        ok = True
        AreEqual(str(e), "")
    Assert(ok)
    
    ok = False
    try:
        assert False
    except AssertionError as e:
        ok = True
        AreEqual(str(e), "")
    Assert(ok)
    
    ok = False
    try:
        assert False, 'this should never pass'
    except AssertionError as e:
        ok = True
        AreEqual(str(e), "this should never pass")
    Assert(ok)
    
    ok = False
    try:
        assert None, 'this should never pass'
    except AssertionError as e:
        ok = True
        AreEqual(str(e), "this should never pass")
    Assert(ok)
        
def test_doesnt_fail_on_curly():
    """Ensures that asserting a string with a curly brace doesn't choke up the
    string formatter."""

    ok = False
    try:
        assert False, '}'
    except AssertionError:
        ok = True
    Assert(ok)
  
  
#--Main------------------------------------------------------------------------
if is_silverlight:
    run_test(__name__, noOutputPlease=True)
elif is_cli and '-O' in System.Environment.GetCommandLineArgs():
    from iptest.process_util import *
    AreEqual(0, launch_ironpython_changing_extensions(__file__, remove=["-O"]))
else:
    run_test(__name__)
