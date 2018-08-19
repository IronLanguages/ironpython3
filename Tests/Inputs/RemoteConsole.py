# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
        print("Remote runtime terminated with exit code : %d" % self.RemoteRuntimeProcess.ExitCode)
        print("Press Enter to nudge the old thread out of Console.Readline...")        

class TestConsoleRestartManager(ConsoleRestartManager):
    def CreateRemoteConsoleHost(self):
        return AutoAbortableConsoleHost()
    
if __name__ == "__main__":
    print("TestConsoleRestartManager procId is %d" % Process.GetCurrentProcess().Id)
    console = TestConsoleRestartManager(exitOnNormalExit = True)
    console.Start()
    console.ConsoleThread.Join()
