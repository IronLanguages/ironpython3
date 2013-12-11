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

import sys
from iptest.util import get_env_var, get_temp_dir

#------------------------------------------------------------------------------

#--IronPython or something else?
is_silverlight = sys.platform == 'silverlight'
is_cli =         sys.platform == 'cli'
is_ironpython =  is_silverlight or is_cli
is_cpython    =  sys.platform == 'win32'

if is_ironpython:
    #We'll use System, if available, to figure out more info on the test
    #environment later
    import System
    import clr


#--The bittedness of the Python implementation
is_cli32, is_cli64 = False, False
if is_ironpython: 
    is_cli32, is_cli64 = (System.IntPtr.Size == 4), (System.IntPtr.Size == 8)

is_32, is_64 = is_cli32, is_cli64
if not is_ironpython:
    cpu = get_env_var("PROCESSOR_ARCHITECTURE")
    if cpu.lower()=="x86":
        is_32 = True
    elif cpu.lower()=="amd64":
        is_64 = True
    

#--CLR version we're running on (if any)
is_orcas = False
if is_cli:
    is_orcas = len(clr.GetClrType(System.Reflection.Emit.DynamicMethod).GetConstructors()) == 8

is_net40 = False
if is_cli:
    is_net40 = System.Environment.Version.Major==4

is_dlr_in_ndp = False
if is_net40:
    try:
        clr.AddReference("Microsoft.Scripting.Core")
    except:
        is_dlr_in_ndp = True    

#--Newlines
if is_ironpython:
    newline = System.Environment.NewLine
else:
    import os
    newline = os.linesep


#--Build flavor of Python being tested
is_debug = False
if is_cli:
    is_debug = sys.exec_prefix.lower().endswith("debug")


#--Are we using peverify to check that all IL generated is valid?
is_peverify_run = False
if is_cli:    
    is_peverify_run = is_debug and "-X:SaveAssemblies" in System.Environment.CommandLine    


#--Internal checkin system used for IronPython
is_snap = False
if not is_silverlight and get_env_var("THISISSNAP")!=None: 
    is_snap = True


#--We only run certain time consuming test cases in the stress lab
is_stress = False
if not is_silverlight and get_env_var("THISISSTRESS")!=None: 
    is_stress = True


#--Are we running tests under the Vista operating system?
is_vista = False
if not is_silverlight and get_env_var("IS_VISTA")=="1":
    is_vista = True

is_win7 = False
if is_ironpython:  #TODO - what about CPython?
    is_win7 = System.Environment.OSVersion.Version.Major==6 and System.Environment.OSVersion.Version.Minor==1

#------------------------------------------------------------------------------
