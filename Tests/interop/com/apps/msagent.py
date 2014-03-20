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

# MSAgent COM Interop tests

from iptest.assert_util import skiptest
skiptest("win32", "silverlight", "cli64")
from iptest.cominterop_util import *
from clr import StrongBox
from System.Runtime.InteropServices import DispatchWrapper

if is_win7:
    import sys
    print "MSAgent is unavailable on Windows 7!"
    sys.exit(0)
#------------------------------------------------------------------------------
#--GLOBALS
com_obj = CreateAgentServer()

#------------------------------------------------------------------------------
#--TESTS
def test_merlin():
    from time import sleep
    
    Assert('Equals' in dir(com_obj))
    charID = StrongBox[int](0)
    reqID = StrongBox[int](0)
    com_obj.Load('Merlin.acs', charID, reqID)
    cid = charID.Value

    character = StrongBox[object](DispatchWrapper(None))
    com_obj.GetCharacter(cid, character)
    c = character.Value.WrappedObject
    sleep(1)
    
    if is_snap or testpath.basePyDir.lower()=='src':
        c.Show(0, reqID)
        sleep(1)
        visible = StrongBox[int](0)
        while visible.Value == 0:
            c.GetVisible(visible)
            sleep(1)
            
    c.Think('IronPython...', reqID)
    c.Play('Read', reqID)
    c.GestureAt(True, False, reqID)
    c.GestureAt(100, 200, reqID)
    AssertError(OverflowError, c.GestureAt, 65537.34, 32) # It should be an error to convert a float to Int16 since it will not fit

    c.Speak('hello world', None, reqID)

    c.StopAll(0)
    c.Hide(0, reqID)
    sleep(1)
    com_obj.Unload(cid)
        
    delete_files("AgentServerObjects.dll")
    
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)