#########################
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
#########################

import os
import re
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_posix, run_test, skipUnlessIronPython

if is_cli:
    import clr
else:
    # this in only so CPython does choke on the skipUnless down below
    class clr(object):
        IsDebug = False

class IronPythonVariableContext(object):
    def __init__(self, variable, value, sep=os.pathsep, prepend=False):
        from System import Environment
        self._variable = variable
        self._value = value
        self._prepend = prepend
        self._sep = sep
        self._oldval = Environment.GetEnvironmentVariable(self._variable)
        
    def __enter__(self):
        from System import Environment
        if self._prepend:
            Environment.SetEnvironmentVariable(self._variable, "%s%s%s" % (self._value, self._sep, self._oldval))
        else:
            Environment.SetEnvironmentVariable(self._variable, self._value)

    def __exit__(self, *args):
        from System import Environment
        Environment.SetEnvironmentVariable(self._variable, self._oldval)


@unittest.skipIf(is_posix, 'Relies on batchfiles')
@skipUnlessIronPython()
class StdConsoleTest(IronPythonTestCase):
    """Test that IronPython console behaves as expected (command line argument processing etc.)."""

    def setUp(self):
        super(StdConsoleTest, self).setUp()

        # Get a temporary directory in which the tests can scribble.
        # This is relative to working directory so the path related tests (e.g.'print __name__')
        # return predictable results.

        self.tmpdir = "tmp" #os.path.join(self.temp_dir, "tmp")
        if not os.path.exists(self.tmpdir):
            os.mkdir(self.tmpdir)

        # Name of a temporary file used to capture console output.
        self.tmpfile = os.path.join(self.tmpdir, "tmp_output.txt")

        # Name of a batch file used to execute the console to workaround the fact we have no way to redirect stdout
        # from os.spawnl.
        self.batfile = os.path.join(self.tmpdir, "__runconsole.bat")

        with open(self.batfile, "w") as f:
            f.write("@" + sys.executable + " >" + self.tmpfile + " 2>&1 %*\n")

    # Runs the console with the given tuple of arguments and verifies that the output and exit code are as
    # specified. The expected_output argument can be specified in various ways:
    #   None        : No output comparison is performed
    #   a string    : Full output is compared (remember to include newlines where appropriate)
    #   a tuple     : A tuple of the form (optionstring, valuestring), valid optionstrings are:
    #       "firstline" : valuestring is compared against the first line of the output
    #       "lastline"  : valuestring is compared against the last line of the output
    #       "regexp"    : valuestring is a regular expression compared against the entire output
    def TestCommandLine(self, args, expected_output, expected_exitcode = 0):
        realargs = [self.batfile]
        realargs.extend(args)
        exitcode = os.spawnv(0, self.batfile, realargs)
        cmdline = "ipy " + ' '.join(args)
        
        print ''
        print '    ', cmdline
        
        self.assertTrue(exitcode == expected_exitcode, "'" + cmdline + "' generated unexpected exit code " + str(exitcode))
        if (expected_output != None):
            with open(self.tmpfile) as f:
                if isinstance(expected_output, str):
                    output = f.read()
                else:
                    output = f.readlines()

            # normalize \r\n to \n
            if type(output) == list:
                output = [x.replace('\r\n', '\n') for x in output]
            else:
                output = output.replace('\r\n', '\n')
            
            # then check the output
            if isinstance(expected_output, str):
                self.assertTrue(output == expected_output, "'" + cmdline + "' generated unexpected output:\n" + output)
            elif isinstance(expected_output, tuple):
                if expected_output[0] == "firstline":
                    self.assertTrue(output[0] == expected_output[1], "'" + cmdline + "' generated unexpected first line of output:\n" + repr(output[0]))
                elif expected_output[0] == "lastline":
                    self.assertTrue(output[-1] == expected_output[1], "'" + cmdline + "' generated unexpected last line of output:\n" + repr(output[-1]))
                elif expected_output[0] == "regexp":
                    output = ''.join(output)
                    self.assertTrue(re.match(expected_output[1], output, re.M | re.S), "'" + cmdline + "' generated unexpected output:\n" + repr(output))
                else:
                    self.assertTrue(False, "Invalid type for expected_output")
            else:
                self.assertTrue(False, "Invalid type for expected_output")

    
    # Runs the console with the given argument string with the expectation that it should enter interactive mode.
    # Meaning, for one, no -c parameter.  This is useful for catching certain argument parsing errors.
    def TestInteractive(self, args, expected_exitcode = 0):
        from iptest.console_util import IronPythonInstance
        ipi = IronPythonInstance(sys.executable, sys.exec_prefix, args, '-X:BasicConsole')
        self.assertEqual(ipi.Start(), True)
        
        #Verify basic behavior
        self.assertEqual("4", ipi.ExecuteLine("2+2"))
        ipi.End()


    def TestScript(self, commandLineArgs, script, expected_output, expected_exitcode = 0):
        scriptFileName = "script_" + str(hash(script)) + ".py"
        tmpscript = os.path.join(self.tmpdir, scriptFileName)
        with open(tmpscript, "w") as f:
            f.write(script)

        args = commandLineArgs + (tmpscript,)
        self.TestCommandLine(args, expected_output, expected_exitcode)

    def test_exit(self):
        # Test exit code with sys.exit(int)
        self.TestCommandLine(("-c", "import sys; sys.exit(0)"),          "",         0)
        self.TestCommandLine(("-c", "import sys; sys.exit(200)"),        "",         200)
        self.TestScript((), "import sys\nclass C(int): pass\nc = C(200)\nsys.exit(c)\n", "", 200)

        # Test exit code with sys.exit(non-int)
        self.TestCommandLine(("-c", "import sys; sys.exit(None)"),       "",         0)
        self.TestCommandLine(("-c", "import sys; sys.exit('goodbye')"),  "goodbye\n",1)
        self.TestCommandLine(("-c", "import sys; sys.exit(200L)"),       "200\n",    1)

    
    def test_os__exit(self):
        self.TestCommandLine(("-c", "import os; os._exit(0)"),          "",         0)
        self.TestCommandLine(("-c", "import os; os._exit(200)"),        "",         200)
        self.TestScript((), "import os\nclass C(int): pass\nc = C(200)\nos._exit(c)\n", "", 200)

    
    @unittest.skip("TODO: this test spawns UI about ipy.exe failing abnormally")
    def test_os_abort(self):
        # Positive
        self.TestCommandLine(("-c", "import os; os.abort()"), "", 1)
        self.TestScript((), "import os\nos.abort()", "", 1)

    def test_c(self):
        """Test the -c (command as string) option."""
        self.TestCommandLine(("-c", "print 'foo'"), "foo\n")
        self.TestCommandLine(("-c", "raise Exception('foo')"), ("lastline", "Exception: foo\n"), 1)
        self.TestCommandLine(("-c", "import sys; sys.exit(123)"), "", 123)
        self.TestCommandLine(("-c", "import sys; print sys.argv", "foo", "bar", "baz"), "['-c', 'foo', 'bar', 'baz']\n")
        self.TestCommandLine(("-c",), "Argument expected for the -c option.\n", 1)

    def test_S(self):
        """Test the -S (suppress site initialization) option."""

        # Create a local site.py that sets some global context. Do this in a temporary directory to avoid accidently
        # overwriting a real site.py or creating confusion. Use the IRONPYTHONPATH environment variable to point
        # IronPython at this version of site.py.
        from System import Environment
        with open(os.path.join(self.tmpdir, "site.py"), "w") as f:
            f.write("import sys\nsys.foo = 123\n")
        
        with IronPythonVariableContext("IRONPYTHONPATH", self.tmpdir, prepend=True):
            print Environment.GetEnvironmentVariable("IRONPYTHONPATH")
            # Verify that the file gets loaded by default.
            self.TestCommandLine(("-c", "import sys; print sys.foo"), "123\n")
            
            # CP778 - verify 'site' does not show up in dir()
            self.TestCommandLine(("-c", "print 'site' in dir()"), "False\n")
            
            # Verify that Lib remains in sys.path.
            self.TestCommandLine(("-S", "-c", "import os ; import sys; print str(os.path.join(sys.exec_prefix, 'Lib')).lower() in [x.lower() for x in sys.path]"), "True\n")
            
            # Now check that we can suppress this with -S.
            self.TestCommandLine(("-S", "-c", "import sys; print sys.foo"), ("lastline", "AttributeError: 'module' object has no attribute 'foo'\n"), 1)

    def test_cp24720(self):
        from System import Environment
        with open(os.path.join(self.tmpdir, "site.py"), "w") as f:
            f.write("import sys\nsys.foo = 456\n")
        
        self.TestCommandLine(("-c", "import site;import sys;print hasattr(sys, 'foo')"), "False\n")
        with IronPythonVariableContext("IRONPYTHONPATH", self.tmpdir, prepend=True):
            self.TestCommandLine(("-c", "import site;import sys;print hasattr(sys, 'foo')"), "True\n")
        os.remove(os.path.join(self.tmpdir, "site.py"))

    def test_V(self):
        """Test the -V (print version and exit) option."""
        self.TestCommandLine(("-V",), ("regexp", "IronPython ([0-9.]+)(.*) on .NET ([0-9.]+)\n"))

    def test_OO(self):
        """Test the -OO (suppress doc string optimization) option."""
        foo_doc = "def foo():\n\t'OK'\nprint foo.__doc__\n"
        self.TestScript((),       foo_doc, "OK\n")
        self.TestScript(("-OO",), foo_doc, "None\n")

    def test_t(self):
        """Test the -t and -tt (warnings/errors on inconsistent tab usage) options."""
        # Write a script containing inconsistent use fo tabs.
        tmpscript = os.path.join(self.tmpdir, "tabs.py")
        with open(tmpscript, "w") as f:
            f.write("if (1):\n\tpass\n        pass\nprint 'OK'\n")
        
        self.TestCommandLine((tmpscript, ), "OK\n")
        msg = "inconsistent use of tabs and spaces in indentation"
        self.TestCommandLine(("-t", tmpscript), ("firstline", "%s:3: SyntaxWarning: %s\n"  % (tmpscript, msg, )), 0)
        self.TestCommandLine(("-tt", tmpscript), ("lastline", "TabError: " + msg + "\n"), 1)

        tmpscript = os.path.join(self.tmpdir, "funcdef.py")
        with open(tmpscript, "w") as f:
            f.write("""def f(a,
        b,
        c): pass""")

        self.TestCommandLine(("-tt", tmpscript, ), "")

    def test_E(self):
        """Test the -E (suppress use of environment variables) option."""
        from System import Environment
        
        # Re-use the generated site.py from above and verify that we can stop it being picked up from IRONPYTHONPATH
        # using -E.
        self.TestCommandLine(("-E", "-c", "import sys; print sys.foo"), ("lastline", "AttributeError: 'module' object has no attribute 'foo'\n"), 1)
        
        # Create an override startup script that exits right away
        tmpscript = os.path.join(self.tmpdir, "startupdie.py")
        with open(tmpscript, "w") as f:
            f.write("from System import Environment\nprint 'Boo!'\nEnvironment.Exit(27)\n")
        
        with IronPythonVariableContext("IRONPYTHONSTARTUP", tmpscript):
            self.TestCommandLine((), None, 27)
            
            tmpscript2 = os.path.join(self.tmpdir, "something.py")
            with open(tmpscript2, "w") as f:
                f.write("print 2+2\n")
            
            self.TestCommandLine(('-E', tmpscript2), "4\n")
            
            tmpscript3 = os.path.join(self.tmpdir, "startupdie.py")
            with open(tmpscript3, "w") as f:
                f.write("import sys\nprint 'Boo!'\nsys.exit(42)\n")
        
        with IronPythonVariableContext("IRONPYTHONSTARTUP", tmpscript3):
            self.TestCommandLine((), None, 42)
        
        os.unlink(tmpscript)
        os.unlink(tmpscript2)

    def test_W(self):
        """Test -W (set warning filters) option."""
        self.TestCommandLine(("-c", "import sys; print sys.warnoptions"), "[]\n")
        self.TestCommandLine(("-W", "foo", "-c", "import sys; print sys.warnoptions"), "Invalid -W option ignored: invalid action: 'foo'\n['foo']\n")
        self.TestCommandLine(("-W", "always", "-W", "once", "-c", "import sys; print sys.warnoptions"), "['always', 'once']\n")
        self.TestCommandLine(("-W",), "Argument expected for the -W option.\n", 1)

    def test_X_Interpret(self):
        """Test -X:FastEval"""
        self.TestCommandLine(("-X:Interpret", "-c", "2+2"), "")
        self.TestCommandLine(("-X:Interpret", "-c", "eval('2+2')"), "")
        self.TestCommandLine(("-X:Interpret", "-c", "x = 3; eval('x+2')"), "")

    @unittest.skipUnless(clr.IsDebug, 'Test can only run in debug mode')
    def test_X_TrackPerformance(self):
        """Test -X:TrackPerformance"""
        self.TestCommandLine(("-X:TrackPerformance", "-c", "2+2"), "")

    def test_u(self):
        """Test -u (Unbuffered stdout & stderr): only test this can be passed in"""
        self.TestCommandLine(('-u', '-c', 'print 2+2'), "4\n")

    def test_X_MaxRecursion(self):
        """Test -X:MaxRecursion"""
        self.TestCommandLine(("-X:MaxRecursion", "20", "-c", "2+2"), "")
        self.TestCommandLine(("-X:MaxRecursion", "3.14159265", "-c", "2+2"), "The argument for the -X:MaxRecursion option must be an integer >= 10.\n", 1)
        self.TestCommandLine(("-X:MaxRecursion",), "Argument expected for the -X:MaxRecursion option.\n", 1)
        self.TestCommandLine(("-X:MaxRecursion", "2"), "The argument for the -X:MaxRecursion option must be an integer >= 10.\n", 1)

    def test_x(self):
        """Test -x (ignore first line)"""
        tmpxoptscript = os.path.join(self.tmpdir, 'xopt.py')
        with open(tmpxoptscript, "w") as f:
            f.write("first line is garbage\nprint 2+2\n")

        self.TestCommandLine(('-x', tmpxoptscript), "4\n")
        os.unlink(tmpxoptscript)

    def test_nonexistent_file(self):
        """Test invocation of a nonexistent file"""
        try:
            os.unlink("nonexistent.py")
        except OSError:
            pass
        self.TestCommandLine(("nonexistent.py",), "File nonexistent.py does not exist.\n", 1)

    def test_MTA(self):
        """Test -X:MTA"""
        self.TestCommandLine(("-X:MTA", "-c", "print 'OK'"), "OK\n")
        self.TestInteractive("-X:MTA")

    def test_Q(self):
        """Test -Q"""
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3/2.0"), """-c:1: DeprecationWarning: classic float division\n1.5\n""")
        self.TestCommandLine(("-Q", "warn", "-c", "print 3/2.0"), "1.5\n")
        self.TestCommandLine(("-Q", "warn", "-c", "print 3j/2.0"), "1.5j\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3/2.0"), "-c:1: DeprecationWarning: classic float division\n1.5\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3L/2.0"), "-c:1: DeprecationWarning: classic float division\n1.5\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3.0/2L"), "-c:1: DeprecationWarning: classic float division\n1.5\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3j/2.0"), "-c:1: DeprecationWarning: classic complex division\n1.5j\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3j/2"), "-c:1: DeprecationWarning: classic complex division\n1.5j\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3j/2L"), "-c:1: DeprecationWarning: classic complex division\n1.5j\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3.0/2j"), "-c:1: DeprecationWarning: classic complex division\n-1.5j\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3/2j"), "-c:1: DeprecationWarning: classic complex division\n-1.5j\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3L/2j"), "-c:1: DeprecationWarning: classic complex division\n-1.5j\n")
        self.TestCommandLine(("-Qwarn", "-c", "print 3/2L"), "-c:1: DeprecationWarning: classic long division\n1\n")
        self.TestCommandLine(("-Qwarnall", "-c", "print 3/2L"), "-c:1: DeprecationWarning: classic long division\n1\n")
        self.TestCommandLine(("-Qwarn", "-c", "print 3L/2"), "-c:1: DeprecationWarning: classic long division\n1\n")
        self.TestCommandLine(("-Qwarnall", "-c", "print 3L/2"), "-c:1: DeprecationWarning: classic long division\n1\n")

        self.TestCommandLine(("-Qnew", "-c", "print 3/2"), "1.5\n")
        self.TestCommandLine(("-Qold", "-c", "print 3/2"), "1\n")
        self.TestCommandLine(("-Qwarn", "-c", "print 3/2"), "-c:1: DeprecationWarning: classic int division\n1\n")
        self.TestCommandLine(("-Qwarnall", "-c", "print 3/2"), "-c:1: DeprecationWarning: classic int division\n1\n")
        self.TestCommandLine(("-Q", "new", "-c", "print 3/2"), "1.5\n")
        self.TestCommandLine(("-Q", "old", "-c", "print 3/2"), "1\n")
        self.TestCommandLine(("-Q", "warn", "-c", "print 3/2"), "-c:1: DeprecationWarning: classic int division\n1\n")
        self.TestCommandLine(("-Q", "warnall", "-c", "print 3/2"), "-c:1: DeprecationWarning: classic int division\n1\n")

    def test_doc(self):
        self.TestCommandLine(("", "-c", "print __doc__"), "None\n", 0)
        
    def test_cp11922(self):
        self.TestCommandLine(("-c", "assert False"), '''Traceback (most recent call last):
  File "<string>", line 1, in <module>
AssertionError
''', 1)

    def test_cp798(self):
        self.TestCommandLine(("", "-c", "dir();print '_' in dir()"), "False\n", 0)

    def test_logo(self):
        from iptest.console_util import IronPythonInstance
        i = IronPythonInstance(sys.executable, sys.exec_prefix, "")
        self.assertEqual(i.proc.Start(), True)
        i.reader = i.proc.StandardOutput
        x = i.EatToPrompt()
        self.assertTrue(x.find('\r\r\n') == -1)
        i.End()

    @unittest.skip("When run in a batch mode, the stdout/stderr/stdin are redirected")
    def test_isatty(self):
        # cp33123
        # this test assumes to be run from cmd.exe without redirecting stdout/stderr/stdin
        isattycmd="import sys; print sys.stdout.isatty(),; print sys.stderr.isatty(),; print sys.stdin.isatty(),"
        isattycmd2="import sys; print >> sys.stderr, sys.stdout.isatty(),; print >> sys.stderr, sys.stderr.isatty(),; print >> sys.stderr, sys.stdin.isatty(),"
        # batch file used by self.TestCommandLine redirects stdout and stderr
        self.TestCommandLine(("-c", isattycmd), "False False True", 0)

        hideDefaultBatch = self.batfile
        try:
            self.batfile = os.path.join(self.tmpdir, "__runconsole-isatty.bat")

            with open(self.batfile, "w") as f:
                f.write("@" + sys.executable + " >" + self.tmpfile + " 2>&1 <nul %*\n")
            
            self.TestCommandLine(("-c", isattycmd), "False False False", 0)

            with open(self.batfile, "w") as f:
                f.write("@" + sys.executable + " >" + self.tmpfile + " %*\n")
            
            self.TestCommandLine(("-c", isattycmd), "False True True", 0)

            with open(self.batfile, "w") as f:
                f.write("@" + sys.executable + " 2>" + self.tmpfile + " %*\n")
            
            self.TestCommandLine(("-c", isattycmd2), "True False True", 0)
        finally:
            self.batfile = hideDefaultBatch

    def test_cp34849(self):
        script="""
import sys
def f1():
    raise Exception("test exception")
def t():
    try:
        f1()
    except:
        pt = sys.exc_info()
        raise pt[0], pt[1], pt[2]
t()
"""
        expected = r"""Traceback (most recent call last):
  File "%s", line 7, in t
  File "%s", line 4, in f1
Exception: test exception
""" % (os.path.join(self.tmpdir, "script_cp34849.py"), os.path.join(self.tmpdir, "script_cp34849.py"))

        scriptFileName = os.path.join(self.tmpdir, "script_cp34849.py")
        with open(scriptFileName, "w") as f:
            f.write(script)
        
        self.TestCommandLine((scriptFileName,), expected, 1)

    def test_cp35263(self):
        script = """
import warnings
def foo():
    warnings.warn('warning 1')
warnings.warn('warning 2')
foo()
"""
        expected=r"""tmp\script_cp35263.py:5: UserWarning: warning 2
  warnings.warn('warning 2')
tmp\script_cp35263.py:4: UserWarning: warning 1
  warnings.warn('warning 1')
"""
        scriptFileName = os.path.join(self.tmpdir, "script_cp35263.py")
        with open(scriptFileName, "w") as f:
            f.write(script)
        
        self.TestCommandLine(("-X:Tracing", "-X:FullFrames", scriptFileName,), expected, 0)

    def test_cp35322(self):
        self.TestCommandLine(("-c", "print __name__"), "__main__\n", 0)

    def test_cp35379(self):
        script1 = r"""
print __file__
print __name__"""
        from zipfile import ZipFile
        zipname = os.path.join(self.tmpdir, 'script_cp35379_1.zip')
        with ZipFile(zipname, 'w') as myzip:
            myzip.writestr('__main__.py', script1)

        self.TestCommandLine((zipname,), "tmp\\script_cp35379_1.zip\\__main__.py\n__main__\n", 0)

        script2 = r"""
import sys
print __file__
print __name__
sys.exit(42)"""
        from zipfile import ZipFile
        zipname = os.path.join(self.tmpdir, 'script_cp35379_2.zip')
        with ZipFile(zipname, 'w') as myzip:
            myzip.writestr('__main__.py', script2)

        self.TestCommandLine((zipname,), "tmp\\script_cp35379_2.zip\\__main__.py\n__main__\n", 42)

        zipname = os.path.join(self.tmpdir, 'script_cp35379_3.zip')
        # get some padding in front of 1st zip content
        with open(zipname, "wb") as padded:
            with open(os.path.join(self.test_dir, "cmd.exe"), "rb") as cmdexe:
                padded.write(cmdexe.read())
            with open(os.path.join(self.tmpdir, "script_cp35379_1.zip"), "rb") as firstZip:
                padded.write(firstZip.read())

        self.TestCommandLine((zipname,), "tmp\\script_cp35379_3.zip\\__main__.py\n__main__\n", 0)

        # it should not matter if relative path is given with \ or /
        zipname = zipname.replace('\\', '/')
        self.TestCommandLine((zipname,), "tmp\\script_cp35379_3.zip\\__main__.py\n__main__\n", 0)

run_test(__name__)

