# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
Designed to be run exclusively by ipyw.exe (or pythonw.exe).
Offers minimal verification that ipyw.exe works as expected.

In the future, this test or some derivative of it should probably import all 
IronPython tests.
'''

import sys
from   _thread import start_new_thread
from   time   import sleep

TEMP_FILE = "verify_ipyw.log"
BAD_OUTPUT = "FAILED"
ERRORS = 0

#Write to a file that Consoleless.ps1 can read
f = open(TEMP_FILE, "w")

#------------------------------------------------------------------------------------
#--Helper functions
def logError(msg):
    global ERRORS
    ERRORS = ERRORS + 1
    f.write(BAD_OUTPUT + ": " + msg + "\n")
    
def logInfo(msg):
    f.write("INFO: " + msg + "\n")     

def logFinished():
    if ERRORS==0:
        f.write("PASSED\n")
    f.close()
    sys.stdout.flush()
    sys.stderr.flush()

def print_bad():
    '''
    If any of this makes it to stdout/stderr, ipyw.exe is broken
    '''
    print(BAD_OUTPUT)
    print(BAD_OUTPUT, file=sys.stdout)
    print(BAD_OUTPUT, file=sys.stderr)
    sys.stdout.write(BAD_OUTPUT)
    sys.stdout.writelines([BAD_OUTPUT])    
    sys.stderr.write(BAD_OUTPUT)
    sys.stderr.writelines([BAD_OUTPUT])
        

#-------------------------------------------------------------------------------------

print_bad()

if __name__!="__main__":
    logError("__name__")

if sys.argv.count("some_param")!= 1 or sys.argv[1]!="some_param":
    logError("some_param")

#spawn a thread to run print_bad...
start_new_thread(print_bad, ())

#one more attempt
print_bad()

#run for just a bit longer...
sleep(5)

logFinished()
