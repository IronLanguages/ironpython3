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

'''
This is a thin wrapper designed to run all Python tests distributed with IronPython
which do not have external dependencies.

The following options are available:

  -M option (Extension mode)
    -M:0 : launch ip with ""
           this is the default choice if -M: is not specified
    -M:1 : launch ip with "-O -D -X:LightweightScopes -X:MaxRecursion 300"
    -M:2 : launch ip with "-O -D -X:SaveAssemblies"
    -M:D : launch ip with all supported modes (i.e., -M:0, -M:1, ...)
    -M:"-O -D -X:SaveAssemblies" (use DOUBLE QUOTE)

  -O option (to specify output detail)
    -O:min : try to keep the output in one screen
             '.' for pass, 'X' for fail (followed by test name)
    -O:med : show 'PASS' and 'FAIL' for each test
    -O:max : besides showing 'PASS' and 'FAIL' for each test,
             print the exception message at the end
    
  -T option (to specify time related options)
    -T:min :
    -T:med : this is default
    -T:max :
      
  -h or -? : to show this help message

Assumptions:
- we start out in the "Tests" directory
- all tests we wish to run are in "Tests"
- the tests are named using the pattern test_*.py
- this is run under IronPython
'''

from sys import argv
from sys import exit
from sys import executable
import sys

from os  import environ
from os  import listdir
from os  import getcwd
import os
from System import Threading
import System

#--GLOBALS---------------------------------------------------------------------

#Flags to be used to run tests
MODES_TO_RUN = []

#Supported test modes
M0 = ""
M1 = "-O -D -X:LightweightScopes -X:MaxRecursion 300"
M2 = "-O -D -X:SaveAssemblies -X:AssembliesDir %s" % environ["TMP"]

#Supported verbosity levels
OUTPUT_MIN = 0
OUTPUT_MED = 1
OUTPUT_MAX = 2

OUTPUT_LVL = OUTPUT_MED

#Supported time levels
TIME_MIN = 0
TIME_MED = 1
TIME_MAX = 2

TIME_LVL = TIME_MED

#list of slow tests that we disable unless -T:max is supplied
SLOW_LIST = [
            "test_numtypes.py",
            "test_fuzz_parser.py"
            ]

#tests we do not wish to run. These should be in the "Tests" directory
EXCLUDE_LIST = [
                "test_dllsite.py",    #disabled for 2.0 branch
                "test_builds.py"      #Silverlight-only
                ]

#List of extra tests in "Tests" which do not follow the "test_*.py" pattern.
#These WILL be run.
EXTRA_INCLUDE_LIST = []

#Debugging...
DEBUG = False
def debug(*args):
    if DEBUG:
        print(args[0], ":", args[1:])

EXCLUDE_LIST = [x.lower() for x in EXCLUDE_LIST]
SLOW_LIST = [x.lower() for x in SLOW_LIST]
EXTRA_INCLUDE_LIST = [x.lower() for x in EXTRA_INCLUDE_LIST]

OUTPUT_COUNT = 0

#------------------------------------------------------------------------------
#--Process sys.argv

#--HELP -h/-?
if '-h' in argv or '-H' in argv or '-?' in argv:
    print(__doc__)
    exit(0)

#--MODES
for temp in argv[1:]:
    #Check if it's user defined
    if temp=="-M:0" or temp=="-M:1" or temp=="-M:2":
        MODES_TO_RUN.append(eval("M" + temp[3:]))
    #All standard modes
    elif temp=="-M:D":
        MODES_TO_RUN.append(M0)
        MODES_TO_RUN.append(M1)
        MODES_TO_RUN.append(M2)
    #Custom modes
    elif temp.startswith("-M:"):
        MODES_TO_RUN += [temp[3:len(temp)]]
#throw away all -M: flags
argv = [x for x in argv if not x.startswith("-M:")]

#If nothing was specified, just use M0
if len(MODES_TO_RUN)==0: MODES_TO_RUN.append(M0)
debug("MODES_TO_RUN", MODES_TO_RUN)


#--OUTPUT LEVEL
for temp in argv[1:]:
    if temp.startswith("-O:"):
        OUTPUT_LVL = eval("OUTPUT_" + temp[3:].upper())
        #take the first instance of -O: only
        break
#throw away all -O: flags
argv = [x for x in argv if not x.startswith("-O:")]
debug("OUTPUT_LVL", OUTPUT_LVL)


#--TIME LEVEL
for temp in argv[1:]:
    if temp.startswith("-T:"):
        TIME_LVL = eval("TIME_" + temp[3:].upper())
        #take the first instance of -T: only
        break
#throw away all -T: flags
argv = [x for x in argv if not x.startswith("-T:")]
debug("TIME_LVL", TIME_LVL)


#get a list of all test_*.py files in the CWD
TEST_LIST = [ x.lower() for x in listdir(getcwd()) if x.startswith("test_") and x.endswith(".py") ]


#add the extra tests
EXTRA_INCLUDE_LIST = [ x for x in EXTRA_INCLUDE_LIST if not x in TEST_LIST ]
TEST_LIST = EXTRA_INCLUDE_LIST + TEST_LIST

#get rid of the skipped tests
TEST_LIST = [ x for x in TEST_LIST if not x in EXCLUDE_LIST ]

#get rid of the slow tests
if TIME_LVL!=TIME_MAX:
    TEST_LIST = [ x for x in TEST_LIST if not x in SLOW_LIST]

TEST_LIST.sort()
debug("TEST_LIST", TEST_LIST)

#List of tests that failed
FAILED_LIST = []


#--FUNCTIONS-------------------------------------------------------------------
def test_exit_code():
    '''
    Verify if we were to fail we would get a good exit code back.
    '''
    
    exitcode_py = 'exitcode.py'
    f = open(exitcode_py, "w")
    f.writelines(['import sys\n', 'sys.exit(99)\n'])
    f.close()
    
    process = System.Diagnostics.Process()
    process.StartInfo.FileName = executable
    process.StartInfo.Arguments = exitcode_py
    process.StartInfo.CreateNoWindow = True
    process.StartInfo.UseShellExecute = False
    process.Start()
    process.WaitForExit()
    
    if process.ExitCode != 99:
        print('SEVERE FAILURE: sys.exit test failed, cannot run tests!')
        System.Environment.Exit(1)


def multireader(*streams):
    """creates multiple threads to read std err/std out at the same time to avoid blocking"""
    class reader(object):
        def __init__(self, stream):
            self.stream = stream
        def __call__(self):
            self.text = self.stream.readlines()
    
    threads = []
    readers = []
    for stream in streams:
        curReader = reader(stream)
        thread = Threading.Thread(Threading.ThreadStart(curReader))
        readers.append(curReader)
        threads.append(thread)
        thread.Start()
        
    for thread in threads:
        thread.Join()
        
    return [curReader.text for curReader in readers]

def run_one_command(*args):
    """runs a single command, exiting if it doesn't return 0, redirecting std out"""
    cmd_line = '"' + executable + '" '.join(args)
    inp, out, err = os.popen3(cmd_line)
    print(cmd_line)
    output, err = multireader(out, err)
    res = out.close()
    if res:
        print('%d running %s failed' % (res, cmd_line))
        print('output was', output)
        print('err', err)
    return output, err, res

def runTestSlow(test_name, mode):
    '''
    Helper function runs a test as a separate process.
    '''
    #run the actual test
    output, errors, ec = run_one_command(mode, test_name)

    return ec, errors

def runTestFast(test_name):
    '''
    Helper function simply imports a test as a module.
    '''
    #backup sys.stderr/sys.stdout
    old_stderr = sys.stderr
    old_stdout = sys.stdout
    
    #temporary sys.stdout/sys.stderr
    f = open("temp.log", "w")
    
    #return values
    ec = 0
    errors = ""
      
    
    try:
        #take over sys.stdout/sys.stderr and import the test module
        sys.stdout = f
        sys.stderr = f
        __import__(test_name.split(".py")[0])
    except SystemExit:
        ec = int(str(sys.exc_info()[1]))
    except:
        ec = 1
    finally:
        sys.stderr = old_stderr
        sys.stdout = old_stdout
        f.close()

    if ec!=0:
        f = open("temp.log", "r")
        for line in f.readlines():
            errors = errors + line
        f.close()

    return (ec, errors)
        


def runTest(test_name, mode):
    '''
    Runs an individual IronPython test.
    '''
    global OUTPUT_COUNT
    OUTPUT_COUNT = OUTPUT_COUNT + 1
    
    if TIME_LVL==TIME_MIN:
        (ec, errors) = runTestFast(test_name)
    else:
        (ec, errors) = runTestSlow(test_name, mode)
    
    #Error handling
    if ec:
        if OUTPUT_LVL==OUTPUT_MIN:
            print("X" + test_name, end=' ')
        else:
            print("\t", test_name, "FAIL")
            if OUTPUT_LVL==OUTPUT_MAX:
                print(errors, file=sys.stderr)
                print()
        FAILED_LIST.append(test_name + "; Exit Code=" + str(ec))
    else:
        if OUTPUT_LVL==OUTPUT_MIN:
            print(".", end=' ')
        else:
            print("\t", test_name, "PASS")
    
    if OUTPUT_LVL==OUTPUT_MIN and OUTPUT_COUNT%25==0:
        print()


#--MAIN------------------------------------------------------------------------

#Sanity check.  If sys.exit fails to work properly, there's little point in
#running all the tests
test_exit_code()

#Using each set of flags supplied to ipy.exe...
for mode in MODES_TO_RUN:
    print("-------------------------------------------------------------------")
    print("RUNNING IRONPYTHON TESTS USING THESE FLAGS:", mode)

    #...run all tests.
    for test_name in TEST_LIST:
        runTest(test_name, mode)
    print()


print("-------------------------------------------------------------------")
print("SUMMARY")
print()

if len(EXCLUDE_LIST)!=0:
    print("The following tests were excluded due to bugs:")
    for test_name in EXCLUDE_LIST: print("\t" + test_name)
    print()

if len(SLOW_LIST)!=0 and TIME_LVL!=TIME_MAX:
    print("The following tests were excluded due to slow execution time:")
    for test_name in SLOW_LIST: print("\t" + test_name)
    print()

if  len(FAILED_LIST)==0:
    print("Everything passed!")
    exit(0)
else:
    print("The following tests failed:")
    for test_name in FAILED_LIST: print("\t" + test_name)
    exit(1)
    