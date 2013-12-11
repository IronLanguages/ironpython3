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

"""
This module contains a single class, IronPythonInstance, which
encapsulates a separately-spawned, interactive Iron Python process.
Its purpose is to enable testing behaviour of the top-level console,
when that differs from behaviour while importing a module and executing
its statements.
"""
from System.Diagnostics import Process, ProcessStartInfo
from System.IO import StreamReader, StreamWriter
from System.Threading import Thread

class IronPythonInstance:
    """
    Class to hold a single instance of the Iron Python interactive
console for testing purposes, and direct input to and from the instance.

    Example usage:
    from sys import exec_prefix
    ip = IronPythonInstance(sys.executable, exec_prefix)
    AreEqual(ip.Start(), True)
    if ip.ExecuteLine("1+1") != "2":
        raise "Bad console!"
    else:
        print "Console passes sanity check."
    ip.End()

    """
    def __init__(self, pathToBin, wkDir, *parms):
        self.proc = Process()
        self.proc.StartInfo.FileName = pathToBin
        self.proc.StartInfo.WorkingDirectory = wkDir
        self.proc.StartInfo.Arguments = " ".join(parms)
        self.proc.StartInfo.UseShellExecute = False
        self.proc.StartInfo.RedirectStandardOutput = True
        self.proc.StartInfo.RedirectStandardInput = True
        self.proc.StartInfo.RedirectStandardError = True

    def Start(self):
        if (not self.proc.Start()):
            return False
        else:
            self.reader = self.proc.StandardOutput
            self.reader2 = self.proc.StandardError
            self.writer = self.proc.StandardInput
            self.InitializeErrorWatcher()
            self.EatToPrompt()
            return True

    def StartAndRunToCompletion(self):
        if (not self.proc.Start()):
            return (False, None, None)
        else:
            self.reader = self.proc.StandardOutput
            self.reader2 = self.proc.StandardError
            self.writer = self.proc.StandardInput
            # This will hang if the output exceeds the buffer size
            output = self.reader.ReadToEnd()
            output2 = self.reader2.ReadToEnd()
            return (True, output, output2, self.proc.ExitCode)

    def EnsureInteractive(self):
        twoPlusTwoResult = self.ExecuteLine("2 + 2", True)
        if "4" <> twoPlusTwoResult: 
            raise AssertionError, 'EnsureInteractive failed. 2+2 returned ' + twoPlusTwoResult

    # Note that the prompt text could occur in the middle of other output.
    # However, it is important to read all the output from the child process
    # to avoid deadlocks. Hence, we assume that ">>> " will only occur
    # as the prompt.
    def EatToPrompt(self, readError=False):
        result = self.EatToMarker(">>> ", readError)
        return result[0 : -len(">>> ")]

    def EatToMarker(self, marker, readError=False):
        slurped = ""
        while not marker in slurped:
            nextChar = self.reader.Read()
            if nextChar == -1: raise ValueError("unexpected end of input after reading '%s'" % slurped)
            slurped += chr(nextChar)
            if slurped == '...': raise ValueError("found ... instead of %s, after reading %s" % (marker, slurped))
        
        assert(slurped.endswith(marker))

        if readError:
            # This should be returned as separate return values, instead of being appended together
            return self.ReadError() + slurped
        else: 
            return slurped

    # Execute a single-line command, and return the output
    def ExecuteLine(self, line, readError=False):
        self.writer.Write(line+"\n")
        return self.EatToPrompt(readError)[0:-2]

    def ExecuteAndExit(self, line):
        self.writer.Write(line+"\n")
        i = 0
        while i < 40 and not self.proc.HasExited:
            Thread.CurrentThread.Join(100)
            i += 1
        return self.proc.ExitCode

    # Submit one line of a multi-line command to the console. There can be 
    # multiple calls to ExecutePartialLine before a final call to ExecuteLine
    def ExecutePartialLine(self, line):
        self.writer.Write(line+"\n")
        ch = self.reader.Read()
        if ch == -1 or chr(ch) <> '.' : raise AssertionError, 'missing the first dot'
        ch = self.reader.Read()
        if ch == -1 or chr(ch) <> '.' : raise AssertionError, 'missing the second dot'
        ch = self.reader.Read()
        if ch == -1 or chr(ch) <> '.' : raise AssertionError, 'missing the third dot'
        ch = self.reader.Read()
        if ch == -1 or chr(ch) <> ' ' : raise AssertionError, 'missing the last space char'

    def End(self):
        if 'writer' in dir(self) and 'Close' in dir(self.writer):
            self.writer.Close()
    
    # Functions for the remote console
    
    def EnsureInteractiveRemote(self, readError=True):
        """Sometimes remote output can become available after the prompt is printed locally."""

        twoPlusTwoResult = self.ExecuteLine("2 + 2", readError)
        if "4" == twoPlusTwoResult: 
            return
        if "" <> twoPlusTwoResult:
            raise AssertionError, 'EnsureInteractive failed. 2+2 returned ' + twoPlusTwoResult
        twoPlusTwoResult = self.EatToMarker("4\r\n")
        if "4\r\n" <> twoPlusTwoResult:
            raise AssertionError, 'EnsureInteractive failed. 2+2 returned ' + twoPlusTwoResult

    def ExecuteLineRemote(self, line, expectedOutputLines=1):
        """Sometimes remote output can become available after the prompt is printed locally."""

        result = self.ExecuteLine(line)
        if "" <> result:
            return result
        for i in xrange(expectedOutputLines):
            output = self.EatToMarker("\r\n")
            if output == "":
                raise AssertionError, 'ExecuteLineRemote failed. Returned empty after %s. Error is %s' % (result, self.ReadError())
            result += output
        return result[0:-2]
    
    # Functions to read stderr
    
    def InitializeErrorWatcher(self):
        from System.Threading import Thread, ThreadStart
        import thread            
        self.errorLock = thread.allocate_lock()
        self.errorString = ""
        th = Thread(ThreadStart(self.WatchErrorStream))
        th.IsBackground = True
        th.Start()

    def WatchErrorStream(self):
        while True:
            nextChar = self.reader2.Read()
            if (nextChar == -1): break
            self.errorLock.acquire()
            self.errorString += chr(nextChar)
            self.errorLock.release()
            
    def GetUnreadError(self):
        self.errorLock.acquire()
        result = self.errorString
        self.errorString = ""
        self.errorLock.release()
        return result
            
    def ReadError(self):
        # For reading the error stream, there is no marker to know when to stop reading.
        # Instead, we keep reading from the error stream until there is no text written
        # to it for 1 second.
                    
        from time import sleep
        
        result = self.GetUnreadError()
        prev = result
        while True:
            sleep(1)
            result += self.GetUnreadError()
            if (result == prev): break
            prev = result

        return result
