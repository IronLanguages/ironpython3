# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import re
import sys
import time
import unittest

from iptest import IronPythonTestCase, run_test, retryOnFailure, skipUnlessIronPython

def getTestOutput():
    '''
    Returns stdout and stderr output for a console test.
    '''
    #On some platforms 'ip_session.log' is not immediately created after
    #calling the 'close' method of the file object writing to 'ip_session.log'.
    #Give it a few seconds to catch up.
    time.sleep(1)
    for i in xrange(5):
        if "ip_session.log" in os.listdir(os.getcwd()):
            tfile = open('ip_session.log', 'r')
            break
        print "Waiting for ip_session.log to be created..."
        time.sleep(1)
    
    outlines = tfile.readlines()
    tfile.close()
    
    errlines = []
    if os.path.exists('ip_session_stderr.log'):
        with open('ip_session_stderr.log', 'r') as tfile:
            errlines = tfile.readlines()
        
    return (outlines, errlines)

def removePrompts(lines):
    return [line for line in lines if not line.startswith(">>>") and not line.startswith("...")]

@unittest.skip('Requires MAUI to run')
@skipUnlessIronPython()
class SuperConsoleTest(IronPythonTestCase):
    def setUp(self):
        super(SuperConsoleTest, self).setUp()
        
        import clr

        #if this is a debug build and the assemblies are being saved...peverify is run.
        #for the test to pass, Maui assemblies must be in the AssembliesDir
        if is_peverify_run:
            AddReferenceToDlrCore()
            clr.AddReference("Microsoft.Scripting")
            from Microsoft.Scripting.Runtime import ScriptDomainManager
            from System.IO import Path

            tempMauiDir = Path.GetTempPath()
            
            print "Copying Maui.Core.dll to %s for peverify..." % (tempMauiDir)
            if not File.Exists(tempMauiDir + '\\Maui.Core.dll'):
                File.Copy(testpath.rowan_root + '\\Util\\Internal\\Maui_old\\Maui.Core.dll',
                        tempMauiDir + '\\Maui.Core.dll')

        #Cleanup the last run
        for t_name in ['ip_session.log', 'ip_session_stderr.log']:
            if File.Exists(t_name):
                File.Delete(t_name)
            Assert(not File.Exists(t_name))

        sys.path.append(testpath.rowan_root + '\\Util\\Internal\\Maui_old')
        try:
            clr.AddReference('Maui.Core.dll')
        except:
            print "test_superconsole.py failed: cannot load Maui.Core assembly"
            sys.exit(int(is_snap))

        from Maui.Core import App
        proc = Process()
        proc.StartInfo.FileName = sys.executable
        proc.StartInfo.WorkingDirectory = testpath.rowan_root + '\\Languages\\IronPython\\Tests'
        proc.StartInfo.Arguments = '-X:TabCompletion -X:AutoIndent -X:ColorfulConsole'
        proc.StartInfo.UseShellExecute = True
        proc.StartInfo.WindowStyle = ProcessWindowStyle.Normal
        proc.StartInfo.CreateNoWindow = False
        started = proc.Start()

        try:
            superConsole = App(proc.Id)
        except Exception as e:
            print "test_superconsole.py failed: cannot initialize App object (probably running as service, or in minimized remote window)"
            print "On VSLGO-MAUI machines close all remote desktop sessions using EXIT command on desktop!"
            proc.Kill()
            sys.exit(1) 
            
        superConsole.SendKeys('from pretest import *{ENTER}')
    
    def shutDown(self):
        super(SuperConsoleTest, self).shutDown()
        # and finally test that F6 shuts it down
        superConsole.SendKeys('{F6}')
        superConsole.SendKeys('{ENTER}')
        sleep(5)
        Assert(not superConsole.IsRunning)

    def verifyResults(self, lines, testRegex):
        '''
        Verifies that a set of lines match a regular expression.
        '''
        lines = removePrompts(lines)
        chopped = ''.join([line[:-1] for line in lines])
        Assert(re.match(testRegex, chopped),
            "Expected Regular Expression=" + testRegex + "\nActual Lines=" + chopped)


    def test_newlines(self):
        '''
        Ensure empty lines do not break the console.
        '''
        #test
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('{ENTER}')
        superConsole.SendKeys('None{ENTER}')
        superConsole.SendKeys('{ENTER}{ENTER}{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        
        #verification
        for lines in getTestOutput():
            AreEqual(removePrompts(lines), [])

    def test_cp12403(self):
        '''
        An exception thrown should appear in stderr.
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        

        superConsole.SendKeys('raise Exception{(}"Some string exception"{)}{ENTER}')
        expected = [
                    "Traceback (most recent call last):",
                    '  File "<stdin>", line 1, in <module>',
                    "Exception: Some string exception",
                    "",
                    ]

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        #stdout should be empty
        AreEqual(removePrompts(getTestOutput()[0]),
                [])
        #stderr should contain the exception
        errlines = getTestOutput()[1]
        for i in xrange(len(errlines)):
            Assert(errlines[i].startswith(expected[i]), str(errlines) + " != " + str(expected))
    
    def test_unique_prefix_completion(self):
        '''
        Ensure that an attribute with a prefix unique to the dictionary is
        properly completed.
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        testRegex = ""

        superConsole.SendKeys('print z{TAB}{ENTER}')
        testRegex += 'zoltar'
        superConsole.SendKeys('print yo{TAB}{ENTER}')
        testRegex += 'yorick'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)
        AreEqual(removePrompts(getTestOutput()[1]),
                [])

    def test_nonunique_prefix_completion(self):
        '''
        Ensure that tabbing on a non-unique prefix cycles through the available
        options.
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys('print y{TAB}{ENTER}')
        superConsole.SendKeys('print y{TAB}{TAB}{ENTER}')
        testRegex += '(yorickyak|yakyorick)'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)
        AreEqual(removePrompts(getTestOutput()[1]),
                [])

    def test_builtin_completion(self):
        """
        verifies we can complete to builtins.  This tests min() is available
        """
        #setup
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        testRegex = ""

        superConsole.SendKeys('print mi{TAB}{(}1,2,3{)}{ENTER}')
        testRegex += '1'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_member_completion(self):
        '''
        Ensure that tabbing after 'ident.' cycles through the available options.
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""

        # 3.1: identifier is valid, we can get dict
        superConsole.SendKeys('print c.{TAB}{ENTER}')

        # it is *either* __doc__ ('Cdoc') or __module__ ('pretest')
        testRegex += '(Cdoc|pretest)'

        # 3.2: identifier is not valid
        superConsole.SendKeys('try:{ENTER}')

        # autoindent
        superConsole.SendKeys('print f.{TAB}x{ENTER}')

        # backup from autoindent
        superConsole.SendKeys('{BACKSPACE}except:{ENTER}')
        superConsole.SendKeys('print "EXC"{ENTER}{ENTER}{ENTER}')
        testRegex += 'EXC'
        
        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)
    
    def test_member_completion_com(self):
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        superConsole.SendKeys('import clr{ENTER}')
        superConsole.SendKeys('import System{ENTER}')
        superConsole.SendKeys('clr.AddReference{(}"Microsoft.Office.Interop.Word"{)}{ENTER}')
        superConsole.SendKeys('import Microsoft.Office.Interop.Word{ENTER}')
        superConsole.SendKeys('wordapp = Microsoft.Office.Interop.Word.ApplicationClass{(}{)}{ENTER}')
        sleep(10) #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24427
        superConsole.SendKeys('wordapp.Activ{TAB}{ENTER}')
        sleep(15) #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24427
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        
        #Verification
        temp = getTestOutput()
        Assert(len(temp[0])==8, str(temp[0]))
        Assert(temp[0][6].startswith('<Microsoft.Scripting.ComInterop.DispCallable object at '), str(temp[0]))

    def test_cp17797(self):
        #setup
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        testRegex = ""

        superConsole.SendKeys('import clr{ENTER}')

        superConsole.SendKeys('print clr.Comp{TAB}{ENTER}')
        testRegex += '<built-in function CompileModules>'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    @retryOnFailure
    def test_autoindentself():
        '''
        Auto-indent
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys("def f{(}{)}:{ENTER}print 'f!'{ENTER}{ENTER}")
        superConsole.SendKeys('f{(}{)}{ENTER}')
        testRegex += 'f!'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_backspace_and_delete(self):
        '''
        Backspace and delete
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys("print 'IQ{BACKSPACE}P'{ENTER}")
        testRegex += "IP"

        superConsole.SendKeys("print 'FW'{LEFT}{LEFT}{DELETE}X{ENTER}")
        testRegex += "FX"

        # 5.3: backspace over auto-indentation
        #   a: all white space
        #   b: some non-whitespace characters

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_cursor_keys(self):
        '''
        Cursor keys
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys("print 'up'{ENTER}")
        testRegex += 'up'
        superConsole.SendKeys("print 'down'{ENTER}")
        testRegex += 'down'
        superConsole.SendKeys("{UP}{UP}{ENTER}")
        testRegex += 'up'
        superConsole.SendKeys("{DOWN}{ENTER}")
        testRegex += 'down'

        superConsole.SendKeys("print 'up'{ENTER}{UP}{ENTER}")
        testRegex += 'upup'
        superConsole.SendKeys("print 'awy{LEFT}{LEFT}{RIGHT}a{RIGHT}'{ENTER}")
        testRegex += 'away'
        superConsole.SendKeys("print 'bad'{ESC}print 'good'{ENTER}")
        testRegex += 'good'
        superConsole.SendKeys("rint 'hom'{HOME}p{END}{LEFT}e{ENTER}")
        testRegex += 'home'
        
        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_control_character_rendering(self):
        '''
        Control-character rendering
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        testRegex = ""

        # Ctrl-D
        superConsole.SendKeys('print "^(d)^(d){LEFT}{DELETE}"{ENTER}')
        testRegex += chr(4)

        # check that Ctrl-C breaks an infinite loop (the test is that subsequent things actually appear)
        superConsole.SendKeys('while True: pass{ENTER}{ENTER}')
        superConsole.SendKeys('^(c)')
        print "CodePlex Work Item 12401"
        errors = [
                    "Traceback (most recent call last):", #CodePlex Work Item 12401
                    "  File", #CodePlex Work Item 12401
                    "  File", #CodePlex Work Item 12401
                    "KeyboardInterrupt",
                    "", #CodePlex Work Item 12401
                ]

        # check that Ctrl-C breaks an infinite loop (the test is that subsequent things actually appear)
        superConsole.SendKeys('def foo{(}{)}:{ENTER}try:{ENTER}while True:{ENTER}pass{ENTER}')
        superConsole.SendKeys('{BACKSPACE}{BACKSPACE}except KeyboardInterrupt:{ENTER}print "caught"{ENTER}{BACKSPACE}{ENTER}')
        superConsole.SendKeys('print "after"{ENTER}{BACKSPACE}{ENTER}foo{(}{)}{ENTER}')
        sleep(2)
        superConsole.SendKeys('^(c)')
        testRegex += 'caughtafter'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)
        #stderr should contain the exceptions
        errlines = getTestOutput()[1]
        Assert("KeyboardInterrupt" + newline in errlines,
            "KeyboardInterrupt not found in:" + str(errlines))
        #for i in xrange(len(errlines)):
        #    Assert(errlines[i].startswith(errors[i]), str(errlines) + " != " + str(errors))


    def test_hasattr_interrupted(self):
        # hasattr() shouldn't swallow KeyboardInterrupt exceptions
        superConsole.SendKeys("class x{(}object{)}:{ENTER}")
        superConsole.SendKeys("    def __getattr__{(}self, name{)}:{ENTER}")
        superConsole.SendKeys("        while True: pass{ENTER}")
        superConsole.SendKeys("{ENTER}")
        superConsole.SendKeys("a = x{(}{)}{ENTER}")
        
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys("hasattr{(}a, 'abc'{)}{ENTER}")
        superConsole.SendKeys('^(c)')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        Assert("KeyboardInterrupt" + newline in getTestOutput()[1])

    def test_tab_insertion(self):
        '''
        Tab insertion
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys('print "j{TAB}{TAB}y"{ENTER}')
        testRegex += 'j    y'

        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)
    
    def test_noeffect_keys(self):
        '''
        Make sure that home, delete, backspace, etc. at start have no effect
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys('{BACKSPACE}{DELETE}{HOME}{LEFT}print "start"{ENTER}')
        testRegex += 'start'
        
        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_tab_completion_caseinsensitive(self):
        '''
        Tab-completion is case-insensitive (wrt input)
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys('import System{ENTER}')
        superConsole.SendKeys('print System.r{TAB}{ENTER}')
        testRegex += "<type 'Random'>"
        
        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_history(self):
        '''
        Command history
        '''
        #setup
        superConsole.SendKeys('outputRedirectStart{(}True{)}{ENTER}')
        testRegex = ""
        
        superConsole.SendKeys('print "first"{ENTER}')
        testRegex += 'first'
        superConsole.SendKeys('print "second"{ENTER}')
        testRegex += 'second'
        superConsole.SendKeys('print "third"{ENTER}')
        testRegex += 'third'
        superConsole.SendKeys('print "fourth"{ENTER}')
        testRegex += 'fourth'
        superConsole.SendKeys('print "fifth"{ENTER}')
        testRegex += 'fifth'
        superConsole.SendKeys('{UP}{UP}{UP}{ENTER}')
        testRegex += 'third'
        superConsole.SendKeys('{UP}{ENTER}')
        testRegex += 'third'
        superConsole.SendKeys('{UP}{UP}{UP}{DOWN}{ENTER}')
        testRegex += 'second'
        superConsole.SendKeys('{UP}{ENTER}')
        testRegex += 'second'
        superConsole.SendKeys('{DOWN}{ENTER}')
        testRegex += 'third'
        superConsole.SendKeys('{DOWN}{ENTER}')
        testRegex += 'fourth'
        superConsole.SendKeys('{DOWN}{ENTER}')
        testRegex += 'fifth'
        superConsole.SendKeys('{UP}{UP}{ESC}print "sixth"{ENTER}')
        testRegex += 'sixth'
        superConsole.SendKeys('{UP}{ENTER}')
        testRegex += 'sixth'
        superConsole.SendKeys('{UP}{DOWN}{DOWN}{DOWN}{DOWN}{DOWN}{DOWN}{ENTER}')
        testRegex += 'sixth'
        
        #verification
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        verifyResults(getTestOutput()[0], testRegex)

    def test_raw_input(self):
        '''
        '''
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('x = raw_input{(}"foo"{)}{ENTER}')
        superConsole.SendKeys('{ENTER}')
        superConsole.SendKeys('print x{ENTER}')
        
        superConsole.SendKeys('x = raw_input{(}"foo"{)}{ENTER}')
        superConsole.SendKeys('abc{ENTER}')
        superConsole.SendKeys('print x{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        
        #verification
        lines = getTestOutput()[0]
        AreEqual(lines[2], '\n')
        AreEqual(lines[5], 'abc\n')

    #Run this first to corrupt other test cases if it's broken.
    def test_000_unverified_raw_input(self):
        '''
        Intentionally not checking output on this test (based on
        CP14456) as redirecting stdout/stderr will hide the bug.
        '''
        superConsole.SendKeys('x = raw_input{(}"foo:"{)}{ENTER}')
        superConsole.SendKeys('{ENTER}')

    @unittest.skip("CodePlex 4299")
    def test_cp4299(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('import sys{ENTER}')
        superConsole.SendKeys('print sys.ps1{ENTER}')
        superConsole.SendKeys('print sys.ps2{ENTER}')
        
        superConsole.SendKeys('sys.ps1 = "abc "{ENTER}')
        superConsole.SendKeys('sys.ps2 = "xyz "{ENTER}')
        superConsole.SendKeys('def f{(}{)}:{ENTER}    pass{ENTER}{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        lines = getTestOutput()[0]
        expected_lines = ['>>> import sys\n', 
                        '>>> print sys.ps1\n', '>>> \n', 
                        '>>> print sys.ps2\n', '... \n', 
                        '>>> sys.ps1 = "abc "\n', 'abc sys.ps2 = "xyz "\n', 
                        'abc def f():\n', 'xyz         pass\n', 'xyz         \n', 
                        'abc outputRedirectStop()\n']
        
        for i in xrange(len(lines)):
            AreEqual(lines[i], expected_lines[i])
        AreEqual(len(lines), len(expected_lines))
    
    def test_cp16520(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('min{(}2,{ENTER}')
        superConsole.SendKeys('3{)}{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        
        #verification
        lines = getTestOutput()[0]
        expected_lines = [  ">>> min(2,\n",
                            "... 3)\n",
                            "2\n",
                            ">>> outputRedirectStop()\n"]
                    
        AreEqual(len(lines), len(expected_lines))
        for i in xrange(0, len(lines)):
            AreEqual(lines[i], expected_lines[i])
    
    def test_decorator_cp21984(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('{@}property{ENTER}')
        superConsole.SendKeys('def foo{(}{)}: pass{ENTER}{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        
        #verification
        lines = getTestOutput()[0]
        expected_lines = [  ">>> @property\n",
                            "... def foo(): pass\n",
                            "... \n",
                            ">>> outputRedirectStop()\n"]
                    
        AreEqual(len(lines), len(expected_lines))
        for i in xrange(0, len(lines)):
            AreEqual(lines[i], expected_lines[i])


    def test_triple_strings(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('"""{ENTER}')
        superConsole.SendKeys('hello"""{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        #verification
        lines = getTestOutput()[0]
        expected_lines = [  ">>> \"\"\"\n",
                            "... hello\"\"\"\n",
                            "'\\nhello'\n",
                            ">>> outputRedirectStop()\n"]
                    
        AreEqual(len(lines), len(expected_lines))
        for i in xrange(0, len(lines)):
            AreEqual(lines[i], expected_lines[i])

    def test_areraise(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('def foo{(}{)}:{ENTER}{TAB}some(){ENTER}{ENTER}')
        superConsole.SendKeys(    'try:{ENTER}{TAB}foo{(}{)}{ENTER}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}')
        superConsole.SendKeys(    'except:{ENTER}{TAB}raise{ENTER}{ENTER}')
        sleep(3)
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        lines = getTestOutput()[1]
        AreEqual(lines, ['Traceback (most recent call last):\r\n', '  File "<stdin>", line 2, in <module>\r\n', '  File "<stdin>", line 2, in foo\r\n', "NameError: global name 'some' is not defined\r\n"])


    def test_syntax_errors(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('def foo{(}{(}1{)}{)}: pass{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        lines = getTestOutput()[1]
        AreEqual(lines, ['  File "<stdin>", line 1\r\n', '    def foo((1)): pass\n', '\r\n', '             ^\r\n', "SyntaxError: unexpected token '1'\r\n", '\r\n'])

    def test_missing_member_syntax_error_cp15428(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('".".{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        lines = getTestOutput()[1]
        AreEqual(lines, ['  File "<stdin>", line 1\r\n', '    ".".\n', '\r\n', '        ^\r\n', "SyntaxError: syntax error\r\n", '\r\n'])
                    

    def test_a_comment_newline(self):
        superConsole.SendKeys('outputRedirectStart{(}{)}{ENTER}')
        superConsole.SendKeys('def foo{(}{)}:{ENTER}    # hi{ENTER}    pass{ENTER}{ENTER}')
        superConsole.SendKeys('outputRedirectStop{(}{)}{ENTER}')
        lines = getTestOutput()[1]
        AreEqual(lines, [])

    def test_aa_redirect_stdout(self):
        # CodePlex 25861, we should be able to return to the
        # REPL w/ output redirected.  If this doesn't work we
        # get an exception which fails the test.    
        f = file('test_superconsole_input.py', 'w')
        f.write("""
import sys

class _StreamLog(object):
    def __init__(self, ostream):
        self.ostream = ostream
    
    def write(self, *args):
        self.ostream.write("{")
        self.ostream.write(*args)
        self.ostream.write("}")
    
    def flush(self):
        self.ostream.flush()

sys.stderr = _StreamLog(sys.stderr)
sys.stdout = _StreamLog(sys.stdout)

""")
        f.close()
        try:
            superConsole.SendKeys('import test_superconsole_input{ENTER}')
            lines = getTestOutput()[0]
            superConsole.SendKeys('import sys{ENTER}')
            superConsole.SendKeys('sys.stdout = sys.__stdout__{ENTER}')
            superConsole.SendKeys('sys.stderr = sys.__stderr__{ENTER}')
            
        finally:
            os.unlink('test_superconsole_input.py')


run_test(__name__)
