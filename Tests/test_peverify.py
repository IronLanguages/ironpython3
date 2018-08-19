# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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