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

# make sure the peverify logic works

from iptest.assert_util import *
from iptest.process_util import *

skiptest("silverlight")
skiptest("win32")

import sys, System

# ipy.exe should be DEBUG version
ipy_exe = System.Reflection.Assembly.LoadFile(sys.executable)
ca = ipy_exe.GetCustomAttributes(System.Diagnostics.DebuggableAttribute, False)
if int(ca[0].DebuggingFlags & System.Diagnostics.DebuggableAttribute.DebuggingModes.DisableOptimizations) == 0:
    print("not debug version of ipy.exe: skip")
    sys.exit(0)

switches = ['-D', '-X:SaveAssemblies']

for x in switches:
    if x not in System.Environment.GetCommandLineArgs():
        print('%s not found in sys.argv: skip' % x)
        sys.exit(0)

process = System.Diagnostics.Process()
process.StartInfo.FileName = sys.executable
process.StartInfo.Arguments = '-D -X:SaveAssemblies badil.py'
process.StartInfo.CreateNoWindow = True
process.StartInfo.UseShellExecute = False
process.StartInfo.RedirectStandardInput = True
process.StartInfo.RedirectStandardOutput = True
process.StartInfo.RedirectStandardError = True
process.Start()
output1 = process.StandardOutput.ReadToEnd()
output2 = process.StandardError.ReadToEnd()
process.WaitForExit()
ret = process.ExitCode

print("ExitCode:", ret)
print("Output: ", output1)
print("Error:  ", output2)
print()

if ret == 1 and ("Error Verifying" in output1 or "Error(s) Verifying" in output1):
    print("caught verification failure: pass")
    sys.exit(0)
else:
    print("did not see Verification failure: fail")
    sys.exit(1)
