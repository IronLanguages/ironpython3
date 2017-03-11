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

# msagent COM Interop tests

from iptest.assert_util import skiptest
skiptest("win32", "silverlight", "cli64", "posix")
from iptest.cominterop_util import *

import clr
from System import Type, Activator

print("Disabling this test due to Codeplex 18525")
from sys import exit
exit(0)

if not file_exists_in_path("tlbimp.exe"):
    from sys import exit
    print("tlbimp.exe is not in the path!")
    exit(1)


#------------------------------------------------------------------------------
#--HELPERS
def _test_common_on_object(o):
    for x in ['GetHashCode', 'GetPassword', '__repr__', 'ToString']:
        Assert(hasattr(o, x), x + " not in dir(o)")

    for x in ['__doc__', '__init__', '__module__']:
        AreEqual(dir(o).count(x), 1)
    
    Assert(o.GetHashCode()) # not zero
    try: del o.GetHashCode
    except AttributeError: pass
    else: Fail("attribute 'GetHashCode' of 'xxx' object is read-only")

    try: o[3] = "something"
    except AttributeError: pass
    else: Fail("__setitem__")
    try: something = o[3]
    except AttributeError: pass
    else: Fail("__getitem__")

    AssertError(TypeError, (lambda:o+3))
    AssertError(TypeError, (lambda:o-3))
    AssertError(TypeError, (lambda:o*3))
    AssertError(TypeError, (lambda:o/3))
    AssertError(TypeError, (lambda:o >> 3))
    AssertError(TypeError, (lambda:o << 3))

#------------------------------------------------------------------------------
#--TESTS
#BUG: http://tkbgitvstfat01:8080/WorkItemTracking/WorkItem.aspx?artifactMoniker=177188
@skip("orcas")
def test__1_registered_nopia():
    # Check to see that namespace 'spwLib' isn't accessible
    Assert('spwLib' not in dir(), "spwLib is already registered")

    run_register_com_component(scriptpw_path)
    
    pwcType = Type.GetTypeFromProgID('ScriptPW.Password.1')
    
    pwcInst = Activator.CreateInstance(pwcType)
    
    AreEqual('System.__ComObject', pwcInst.ToString())
        
    try: del pwcInst.GetPassword
    except AttributeError: pass
    else: Fail("'__ComObject' object has no attribute 'GetPassword'")
        
    _test_common_on_object(pwcInst)
        
    # looks like: "<System.__ComObject  (TypeInfo : IPassword)>"
    types = ['__ComObject', 'IPassword']
    
    for x in types:
        Assert(x in repr(pwcInst), x + " not in repr(pwcInst)")
    
#Merlin Work Item #203712
@skip("cli")
def test__3_registered_with_pia():
    run_tlbimp(scriptpw_path, "spwLib")
    run_register_com_component(scriptpw_path)
    clr.AddReference("spwLib.dll")
    
    from spwLib import PasswordClass    
    pc = PasswordClass()
        
    Assert('PasswordClass' in repr(pc))
    Assert('spwLib.PasswordClass' in pc.ToString())
    AreEqual(pc.__class__, PasswordClass)
        
    try: del pc.GetPassword
    except AttributeError: pass
    else: Fail("attribute 'GetPassword' of 'PasswordClass' object is read-only")
        
    _test_common_on_object(pc)
        
def test__2_unregistered_nopia():
    # Check to see that namespace 'spwLib' isn't accessible
    Assert('spwLib' not in dir(), "spwLib is already registered")
    
    run_unregister_com_component(scriptpw_path)
    pwcType = Type.GetTypeFromProgID('ScriptPW.Password.1')
    AreEqual(pwcType, None)
    
    # Registration-free COM activation
    import IronPythonTest
    password = IronPythonTest.ScriptPW.CreatePassword()
    AreEqual('System.__ComObject', password.ToString())


#------------------------------------------------------------------------------
if not file_exists(scriptpw_path):
    print("Cannot test scriptpw.dll when it doesn't exist.")
else:
    run_com_test(__name__, __file__)