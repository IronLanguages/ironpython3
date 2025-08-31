# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Utility script used to determine which CPython tests IronPython can run correctly.

USAGE:
    ipy cmodule.py C:\Python25
    
OUTPUT:
    %CD%\IPY_PASSES.log (tests which IP can run)
    %CD%\IPY_FAILS.log  (tests which IP cannot run)
'''

import sys
import nt
from clr_helpers import Process

#------------------------------------------------------------------------------
CPY_DIR = sys.argv[1]  #E.g., C:\Python25


DISABLED = {
            "test_aepack.py" : "Platform specific test - Mac",
            }
_temp_keys = list(DISABLED.keys())

TEST_LIST = [x for x in nt.listdir(CPY_DIR + r"\Lib\test") if x.startswith("test_") and x.endswith(".py") and _temp_keys.count(x)==0]

#Log containing all tests IP passes
IPY_PASSES = open("IPY_PASSES.log", "w")
#Log containing all tests IP fails
IPY_FAILS = open("IPY_FAILS.log", "w")

#--HELPER FUNCTIONS------------------------------------------------------------
def ip_passes(mod_name):
    print(mod_name)
    IPY_PASSES.write(mod_name + "\n")
    IPY_PASSES.flush() 
    
def ip_fails(mod_name):
    IPY_FAILS.write(mod_name + "\n")
    IPY_FAILS.flush()

#--MAIN-----------------------------------------------------------------------
nt.chdir(CPY_DIR + r"\Lib")

for mod_name in TEST_LIST:
    proc = Process()
    proc.StartInfo.FileName =  sys.executable
    proc.StartInfo.Arguments = "test\\" + mod_name
    proc.StartInfo.UseShellExecute = False
    proc.StartInfo.RedirectStandardOutput = True
    if (not proc.Start()):
        raise Exception("Python process failed to start: " + mod_name)
    else:
        cpymod_dir = proc.StandardOutput.ReadToEnd()
    
    if not proc.HasExited:
        raise Exception("Python process should have exited by now: " + mod_name)
    
    if proc.ExitCode==0:
        ip_passes(mod_name)
    else:
        ip_fails(mod_name)
    
IPY_PASSES.close()
IPY_FAILS.close()