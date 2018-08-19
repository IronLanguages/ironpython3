# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import clr
from System.IO import File, Path

def main():
    if len(sys.argv) != 2:
        print "Usage: ipy run_compiled.py <testfile.py>"
        sys.exit(-1)
    
    testName = sys.argv[1]
    
    print "Compiling ", testName ,"..."
    clr.CompileModules("compiledTest.dll", testName)    
    File.Move(testName, testName+".bak")    
    try:
        print "Running test from compiled binary..."    
        clr.AddReference("compiledTest")    
        __import__(Path.GetFileNameWithoutExtension(testName))    
    finally:
        File.Move(testName+".bak" , testName)
    
#--------------------------------------------------------------------------------------
if __name__ == "__main__":
    main()
#--------------------------------------------------------------------------------------