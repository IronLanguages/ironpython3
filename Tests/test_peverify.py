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

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, is_mono, run_test, skipUnlessIronPython

if is_cli:
    import clr
else:
    class clr(object):
        IsDebug=False

@unittest.skipIf(is_mono, 'mono does not add a debuggable attribute')
@unittest.skipIf(is_netcoreapp, 'no assembly saving')
@skipUnlessIronPython()
@unittest.skipUnless(clr.IsDebug, 'Need debug mode assemblies')
class PEVerifyTest(IronPythonTestCase):
    def test_badil(self):
        import System
        process = System.Diagnostics.Process()
        process.StartInfo.FileName = sys.executable
        process.StartInfo.Arguments = '-D -X:SaveAssemblies "%s"'  % os.path.join(self.test_dir, 'badil.py')
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

        print "ExitCode:", ret
        print "Output: ", output1
        print "Error:  ", output2
        print

        self.assertEqual(ret, 1)
        self.assertTrue("Error Verifying" in output1 or "Error(s) Verifying" in output1)

run_test(__name__)