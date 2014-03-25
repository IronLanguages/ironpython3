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