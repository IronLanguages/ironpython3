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

from iptest.assert_util import *
skiptest("silverlight")
skiptest("win32")
from iptest.console_util import IronPythonInstance
import sys
import nt
import re
from System import *
import os

# Test that IronPython console behaves as expected (command line argument processing etc.).

# Get a temporary directory in which the tests can scribble.
tmpdir = Environment.GetEnvironmentVariable("TEMP")
tmpdir = IO.Path.Combine(tmpdir, "IronPython")
if not os.path.exists(tmpdir):
    nt.mkdir(tmpdir)

# Name of a temporary file used to capture console output.
tmpfile = IO.Path.Combine(tmpdir, "tmp_output.txt")

# Name of a batch file used to execute the console to workaround the fact we have no way to redirect stdout
# from nt.spawnl.
batfile = IO.Path.Combine(tmpdir, "__runconsole.bat")

f = file(batfile, "w")
f.write("@" + sys.executable + " >" + tmpfile + " 2>&1 %*\n")
f.close()

############################################################
# Runs the console with the given tuple of arguments and verifies that the output and exit code are as
# specified. The expected_output argument can be specified in various ways:
#   None        : No output comparison is performed
#   a string    : Full output is compared (remember to include newlines where appropriate)
#   a tuple     : A tuple of the form (optionstring, valuestring), valid optionstrings are:
#       "firstline" : valuestring is compared against the first line of the output
#       "lastline"  : valuestring is compared against the last line of the output
#       "regexp"    : valuestring is a regular expression compared against the entire output
def TestCommandLine(args, expected_output, expected_exitcode = 0):
    realargs = [batfile]
    realargs.extend(args)
    exitcode = nt.spawnv(0, batfile, realargs)
    cmdline = "ipy " + ' '.join(args)
    
    print('')
    print('    ', cmdline)
    
    Assert(exitcode == expected_exitcode, "'" + cmdline + "' generated unexpected exit code " + str(exitcode))
    if (expected_output != None):
        f = file(tmpfile)
        if isinstance(expected_output, str):
            output = f.read()
        else:
            output = f.readlines()
        f.close()
        
        # normalize \r\n to \n
        if type(output) == list:
            for i in range(len(output)):
                output[i] = output[i].replace('\r\n', '\n')
        else:
            output = output.replace('\r\n', '\n')
        
        # then check the output
        if isinstance(expected_output, str):
            Assert(output == expected_output, "'" + cmdline + "' generated unexpected output:\n" + output)
        elif isinstance(expected_output, tuple):
            if expected_output[0] == "firstline":
                Assert(output[0] == expected_output[1], "'" + cmdline + "' generated unexpected first line of output:\n" + repr(output[0]))
            elif expected_output[0] == "lastline":
                Assert(output[-1] == expected_output[1], "'" + cmdline + "' generated unexpected last line of output:\n" + repr(output[-1]))
            elif expected_output[0] == "regexp":
                output = ''.join(output)
                Assert(re.match(expected_output[1], output, re.M | re.S), "'" + cmdline + "' generated unexpected output:\n" + repr(output))
            else:
                Assert(False, "Invalid type for expected_output")
        else:
            Assert(False, "Invalid type for expected_output")

############################################################
# Runs the console with the given argument string with the expectation that it should enter interactive mode.
# Meaning, for one, no -c parameter.  This is useful for catching certain argument parsing errors.
def TestInteractive(args, expected_exitcode = 0):
    ipi = IronPythonInstance(sys.executable, sys.exec_prefix, args, '-X:BasicConsole')
    AreEqual(ipi.Start(), True)
    
    #Verify basic behavior
    AreEqual("4", ipi.ExecuteLine("2+2"))
    
    ipi.End()

############################################################
def TestScript(commandLineArgs, script, expected_output, expected_exitcode = 0):
    scriptFileName = "script_" + str(hash(script)) + ".py"
    tmpscript = IO.Path.Combine(tmpdir, scriptFileName)
    f = file(tmpscript, "w")
    f.write(script)
    f.close()
    args = commandLineArgs + (tmpscript,)
    TestCommandLine(args, expected_output, expected_exitcode)

############################################################
def test_exit():
    # Test exit code with sys.exit(int)
    TestCommandLine(("-c", "import sys; sys.exit(0)"),          "",         0)
    TestCommandLine(("-c", "import sys; sys.exit(200)"),        "",         200)
    TestScript((), "import sys\nclass C(int): pass\nc = C(200)\nsys.exit(c)\n", "", 200)

    # Test exit code with sys.exit(non-int)
    TestCommandLine(("-c", "import sys; sys.exit(None)"),       "",         0)
    TestCommandLine(("-c", "import sys; sys.exit('goodbye')"),  "goodbye\n",1)
    TestCommandLine(("-c", "import sys; sys.exit(200L)"),       "200\n",    1)

############################################################
def test_nt__exit():
    TestCommandLine(("-c", "import nt; nt._exit(0)"),          "",         0)
    TestCommandLine(("-c", "import nt; nt._exit(200)"),        "",         200)
    TestScript((), "import nt\nclass C(int): pass\nc = C(200)\nnt._exit(c)\n", "", 200)

############################################################
@disabled("TODO: this test spawns UI about ipy.exe failing abnormally")
def test_nt_abort():
    # Positive
    TestCommandLine(("-c", "import nt; nt.abort()"),          "",         1)
    TestScript((), "import nt\nnt.abort()", "", 1)

############################################################
# Test the -c (command as string) option.

def test_c():
    TestCommandLine(("-c", "print 'foo'"), "foo\n")
    TestCommandLine(("-c", "raise Exception('foo')"), ("lastline", "Exception: foo"), 1)
    TestCommandLine(("-c", "import sys; sys.exit(123)"), "", 123)
    TestCommandLine(("-c", "import sys; print sys.argv", "foo", "bar", "baz"), "['-c', 'foo', 'bar', 'baz']\n")
    TestCommandLine(("-c",), "Argument expected for the -c option.\n", 1)

############################################################
# Test the -S (suppress site initialization) option.

def test_S():
    # Create a local site.py that sets some global context. Do this in a temporary directory to avoid accidently
    # overwriting a real site.py or creating confusion. Use the IRONPYTHONPATH environment variable to point
    # IronPython at this version of site.py.
    f = file(tmpdir + "\\site.py", "w")
    f.write("import sys\nsys.foo = 123\n")
    f.close()
    Environment.SetEnvironmentVariable("IRONPYTHONPATH", tmpdir)
    
    # Verify that the file gets loaded by default.
    TestCommandLine(("-c", "import sys; print sys.foo"), "123\n")
    
    # CP778 - verify 'site' does not show up in dir()
    TestCommandLine(("-c", "print 'site' in dir()"), "False\n")
    
    # Verify that Lib remains in sys.path.
    TestCommandLine(("-S", "-c", "import sys; print str(sys.exec_prefix + '\\lib').lower() in [x.lower() for x in sys.path]"), "True\n")
    
    # Now check that we can suppress this with -S.
    TestCommandLine(("-S", "-c", "import sys; print sys.foo"), ("lastline", "AttributeError: 'module' object has no attribute 'foo'"), 1)

def test_cp24720():
    f = file(nt.getcwd() + "\\site.py", "w")
    f.write("import sys\nsys.foo = 456\n")
    f.close()
    orig_ipy_path = Environment.GetEnvironmentVariable("IRONPYTHONPATH")
    
    try:
        Environment.SetEnvironmentVariable("IRONPYTHONPATH", "")
        TestCommandLine(("-c", "import site;import sys;print hasattr(sys, 'foo')"), "False\n")
        Environment.SetEnvironmentVariable("IRONPYTHONPATH", ".")
        TestCommandLine(("-c", "import site;import sys;print hasattr(sys, 'foo')"), "True\n")
        
    finally:
        Environment.SetEnvironmentVariable("IRONPYTHONPATH", orig_ipy_path)
        nt.remove(nt.getcwd() + "\\site.py")

def test_V():
    # Test the -V (print version and exit) option.
    TestCommandLine(("-V",), ("regexp", "IronPython ([0-9.]+)(.*) on .NET ([0-9.]+)\n"))

############################################################
# Test the -OO (suppress doc string optimization) option.
def test_OO():
    foo_doc = "def foo():\n\t'OK'\nprint foo.__doc__\n"
    TestScript((),       foo_doc, "OK\n")
    TestScript(("-OO",), foo_doc, "None\n")

############################################################
# Test the -t and -tt (warnings/errors on inconsistent tab usage) options.

def test_t():
    # Write a script containing inconsistent use fo tabs.
    tmpscript = tmpdir + "\\tabs.py"
    f = file(tmpscript, "w")
    f.write("if (1):\n\tpass\n        pass\nprint 'OK'\n")
    f.close()
    
    TestCommandLine((tmpscript, ), "OK\n")
    msg = "inconsistent use of tabs and spaces in indentation"
    TestCommandLine(("-t", tmpscript), ("firstline", "%s:3: SyntaxWarning: %s\n"  % (tmpscript, msg, )), 0)
    TestCommandLine(("-tt", tmpscript), ("lastline", "TabError: " + msg + "\n"), 1)

    tmpscript = tmpdir + "\\funcdef.py"
    f = file(tmpscript, "w")
    f.write("""def f(a,
    b,
    c): pass""")
    f.close()

    TestCommandLine(("-tt", tmpscript, ), "")

def test_E():
    # Test the -E (suppress use of environment variables) option.
    
    # Re-use the generated site.py from above and verify that we can stop it being picked up from IRONPYTHONPATH
    # using -E.
    TestCommandLine(("-E", "-c", "import sys; print sys.foo"), ("lastline", "AttributeError: 'module' object has no attribute 'foo'"), 1)
    
    # Create an override startup script that exits right away
    tmpscript = tmpdir + "\\startupdie.py"
    f = file(tmpscript, "w")
    f.write("from System import Environment\nprint 'Boo!'\nEnvironment.Exit(27)\n")
    f.close()
    Environment.SetEnvironmentVariable("IRONPYTHONSTARTUP", tmpscript)
    TestCommandLine((), None, 27)
    
    tmpscript2 = tmpdir + "\\something.py"
    f = file(tmpscript2, "w")
    f.write("print 2+2\n")
    f.close()
    TestCommandLine(('-E', tmpscript2), "4\n")
    
    tmpscript3 = tmpdir + "\\startupdie.py"
    f = file(tmpscript3, "w")
    f.write("import sys\nprint 'Boo!'\nsys.exit(42)\n")
    f.close()
    Environment.SetEnvironmentVariable("IRONPYTHONSTARTUP", tmpscript3)
    TestCommandLine((), None, 42)
    
    Environment.SetEnvironmentVariable("IRONPYTHONSTARTUP", "")
    nt.unlink(tmpscript)
    nt.unlink(tmpscript2)

# Test -W (set warning filters) option.
def test_W():
    TestCommandLine(("-c", "import sys; print sys.warnoptions"), "[]\n")
    TestCommandLine(("-W", "foo", "-c", "import sys; print sys.warnoptions"), "['foo']\n")
    TestCommandLine(("-W", "foo", "-W", "bar", "-c", "import sys; print sys.warnoptions"), "['foo', 'bar']\n")
    TestCommandLine(("-W",), "Argument expected for the -W option.\n", 1)

# Test -?
# regexp for the output of PrintUsage    
# usageRegex = "Usage.*"
# TestCommandLine(("-?",), ("regexp", usageRegex))

# Test -X:FastEval
def test_X_Interpret():
    TestCommandLine(("-X:Interpret", "-c", "2+2"), "")
    TestCommandLine(("-X:Interpret", "-c", "eval('2+2')"), "")
    TestCommandLine(("-X:Interpret", "-c", "x = 3; eval('x+2')"), "")

# Test -X:TrackPerformance
def test_X_TrackPerformance():
    if not is_debug: return #Mode not supported in Release
    
    TestCommandLine(("-X:TrackPerformance", "-c", "2+2"), "")

# Test -u (Unbuffered stdout & stderr): only test this can be passed in
def test_u():
    TestCommandLine(('-u', '-c', 'print 2+2'), "4\n")

# Test -X:MaxRecursion
def test_X_MaxRecursion():
    TestCommandLine(("-X:MaxRecursion", "10", "-c", "2+2"), "")
    TestCommandLine(("-X:MaxRecursion", "3.14159265", "-c", "2+2"), "The argument for the -X:MaxRecursion option must be an integer >= 10.\n", 1)
    TestCommandLine(("-X:MaxRecursion",), "Argument expected for the -X:MaxRecursion option.\n", 1)
    TestCommandLine(("-X:MaxRecursion", "2"), "The argument for the -X:MaxRecursion option must be an integer >= 10.\n", 1)

# Test -x (ignore first line)
def test_x():
    tmpxoptscript = tmpdir + '\\xopt.py'
    f = file(tmpxoptscript, "w")
    f.write("first line is garbage\nprint 2+2\n")
    f.close()
    TestCommandLine(('-x', tmpxoptscript), "4\n")
    nt.unlink(tmpxoptscript)

def test_nonexistent_file():
    # Test invocation of a nonexistent file
    try:
        nt.unlink("nonexistent.py")
    except OSError:
        pass
    TestCommandLine(("nonexistent.py",), "File nonexistent.py does not exist.\n", 1)

# Test -X:MTA
def test_MTA():
    TestCommandLine(("-X:MTA", "-c", "print 'OK'"), "OK\n")
    TestInteractive("-X:MTA")

# Test -Q
def test_Q():
    TestCommandLine(("-Q", "warnall", "-c", "print 3/2.0"), """<string>:1: DeprecationWarning: classic float division\n1.5\n""")
    TestCommandLine(("-Q", "warn", "-c", "print 3/2.0"), "1.5\n")
    TestCommandLine(("-Q", "warn", "-c", "print 3j/2.0"), "1.5j\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3/2.0"), "<string>:1: DeprecationWarning: classic float division\n1.5\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3L/2.0"), "<string>:1: DeprecationWarning: classic float division\n1.5\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3.0/2L"), "<string>:1: DeprecationWarning: classic float division\n1.5\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3j/2.0"), "<string>:1: DeprecationWarning: classic complex division\n1.5j\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3j/2"), "<string>:1: DeprecationWarning: classic complex division\n1.5j\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3j/2L"), "<string>:1: DeprecationWarning: classic complex division\n1.5j\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3.0/2j"), "<string>:1: DeprecationWarning: classic complex division\n-1.5j\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3/2j"), "<string>:1: DeprecationWarning: classic complex division\n-1.5j\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3L/2j"), "<string>:1: DeprecationWarning: classic complex division\n-1.5j\n")
    TestCommandLine(("-Qwarn", "-c", "print 3/2L"), "<string>:1: DeprecationWarning: classic long division\n1\n")
    TestCommandLine(("-Qwarnall", "-c", "print 3/2L"), "<string>:1: DeprecationWarning: classic long division\n1\n")
    TestCommandLine(("-Qwarn", "-c", "print 3L/2"), "<string>:1: DeprecationWarning: classic long division\n1\n")
    TestCommandLine(("-Qwarnall", "-c", "print 3L/2"), "<string>:1: DeprecationWarning: classic long division\n1\n")

    TestCommandLine(("-Qnew", "-c", "print 3/2"), "1.5\n")
    TestCommandLine(("-Qold", "-c", "print 3/2"), "1\n")
    TestCommandLine(("-Qwarn", "-c", "print 3/2"), "<string>:1: DeprecationWarning: classic int division\n1\n")
    TestCommandLine(("-Qwarnall", "-c", "print 3/2"), "<string>:1: DeprecationWarning: classic int division\n1\n")
    TestCommandLine(("-Q", "new", "-c", "print 3/2"), "1.5\n")
    TestCommandLine(("-Q", "old", "-c", "print 3/2"), "1\n")
    TestCommandLine(("-Q", "warn", "-c", "print 3/2"), "<string>:1: DeprecationWarning: classic int division\n1\n")
    TestCommandLine(("-Q", "warnall", "-c", "print 3/2"), "<string>:1: DeprecationWarning: classic int division\n1\n")

def test_doc():
    TestCommandLine(("", "-c", "print __doc__"), "None\n", 0)
    
def test_cp11922():
    TestCommandLine(("-c", "assert False"), '''Traceback (most recent call last):
  File "<string>", line 1, in <module>
AssertionError''',
                    1)

def test_cp798():
    TestCommandLine(("", "-c", "dir();print '_' in dir()"), "False\n", 0)

def test_logo():
    i = IronPythonInstance(sys.executable, sys.exec_prefix, "")
    AreEqual(i.proc.Start(), True)
    i.reader = i.proc.StandardOutput
    x = i.EatToPrompt()
    Assert(x.find('\r\r\n') == -1)

def test_isatty():
    # cp33123
    # this test assumes to be run from cmd.exe without redirecting stdout/stderr/stdin
    isattycmd="import sys; print sys.stdout.isatty(),; print sys.stderr.isatty(),; print sys.stdin.isatty(),"
    isattycmd2="import sys; print >> sys.stderr, sys.stdout.isatty(),; print >> sys.stderr, sys.stderr.isatty(),; print >> sys.stderr, sys.stdin.isatty(),"
    # batch file used by TestCommandLine redirects stdout and stderr
    TestCommandLine(("-c", isattycmd), "False False True", 0)

    hideDefaultBatch = batfile
    try:
        global batfile
        batfile = IO.Path.Combine(tmpdir, "__runconsole-isatty.bat")

        f = file(batfile, "w")
        f.write("@" + sys.executable + " >" + tmpfile + " 2>&1 <nul %*\n")
        f.close()
        TestCommandLine(("-c", isattycmd), "False False False", 0)

        f = file(batfile, "w")
        f.write("@" + sys.executable + " >" + tmpfile + " %*\n")
        f.close()
        TestCommandLine(("-c", isattycmd), "False True True", 0)

        f = file(batfile, "w")
        f.write("@" + sys.executable + " 2>" + tmpfile + " %*\n")
        f.close()
        TestCommandLine(("-c", isattycmd2), "True False True", 0)
    finally:
        batfile = hideDefaultBatch

run_test(__name__)

