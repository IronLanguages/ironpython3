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

import clr
import sys
clr.AddReference("IronPython")
from IronPython.Runtime import PythonContext
clr.AddReference("Microsoft.Scripting")
clr.AddReference("Microsoft.Dynamic")
from Microsoft.Scripting.Hosting.Shell.Remote import RemoteConsoleHost, ConsoleRestartManager
from System.Reflection import Assembly
from System.Diagnostics import Process

# Sending Ctrl-C is hard to automate in testing. This class tests the AbortCommand functionality
# without relying on Ctrl-C
class AutoAbortableConsoleHost(RemoteConsoleHost):
    def get_Provider(self):
        return PythonContext
    
    def CustomizeRemoteRuntimeStartInfo(self, processInfo):
        fileName = Assembly.GetEntryAssembly().Location
        assert(fileName.endswith("ipy.exe"))
        processInfo.FileName = fileName
        # Pass along any command-line arguments
        if len(sys.argv) > 1:
            args = ""
            for arg in sys.argv[1:]:
                args += arg + " "
            processInfo.Arguments += " " + args

    def OnOutputDataReceived(self, sender, eventArgs):
        super(AutoAbortableConsoleHost, self).OnOutputDataReceived(sender, eventArgs);
        if eventArgs.Data == None:
            return
        if "ABORT ME!!!" in eventArgs.Data:
            self.AbortCommand()
    
    def OnRemoteRuntimeExited(self, sender, eventArgs):
        super(AutoAbortableConsoleHost, self).OnRemoteRuntimeExited(sender, eventArgs)
        print "Remote runtime terminated with exit code : %d" % self.RemoteRuntimeProcess.ExitCode
        print "Press Enter to nudge the old thread out of Console.Readline..."        

class TestConsoleRestartManager(ConsoleRestartManager):
    def CreateRemoteConsoleHost(self):
        return AutoAbortableConsoleHost()
    
if __name__ == "__main__":
    print "TestConsoleRestartManager procId is %d" % Process.GetCurrentProcess().Id
    console = TestConsoleRestartManager(exitOnNormalExit = True)
    console.Start()
    console.ConsoleThread.Join()
