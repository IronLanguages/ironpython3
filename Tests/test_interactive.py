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

skiptest("win32")
from iptest.console_util import IronPythonInstance

remove_ironpython_dlls(testpath.public_testdir)

from sys import executable
from System import Environment
from sys import exec_prefix

extraArgs = ""
if "-X:LightweightScopes" in Environment.GetCommandLineArgs():
    extraArgs += " -X:LightweightScopes"

def test_strings():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # String exception
    response = ipi.ExecuteLine("raise 'foo'", True)
    AreEqual(response.replace("\r\r\n", "\n").replace("\r", ""), 
            """Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
TypeError: exceptions must be classes, or instances, not str""")
    
    # Multi-line string literal
    ipi.ExecutePartialLine("\"\"\"Hello")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine("")
    AreEqual("'Hello\\n\\n\\nWorld'", ipi.ExecuteLine("World\"\"\""))
    
    ipi.ExecutePartialLine("if False: print 3")
    ipi.ExecutePartialLine("else: print 'hello'")
    AreEqual(r'hello', ipi.ExecuteLine(""))
    
    # Empty line
    AreEqual("", ipi.ExecuteLine(""))
    
    ipi.End()

def test_exceptions():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # parameterless exception
    response = ipi.ExecuteLine("raise Exception", True)
    AreEqual(response,
             '''Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
Exception'''.replace("\n", "\r\r\n") + "\r")

    ipi.End()
    
def test_exceptions_nested():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)

    ipi.ExecutePartialLine("def a(): return b()")
    ipi.ExecuteLine("")
    ipi.ExecutePartialLine("def b(): return 1/0")
    ipi.ExecuteLine("")
    response = ipi.ExecuteLine("a()", True)
    response = response.replace("\r\r\n", "\n").strip()
    Assert(response.startswith('''Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
  File "<stdin>", line 1, in a
  File "<stdin>", line 1, in b
ZeroDivisionError:'''), response)
            
    ipi.End()


###############################################################################
# Test "ipy.exe -i script.py"

def test_interactive_mode():
    inputScript = testpath.test_inputs_dir + "\\simpleCommand.py"
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -i \"" + inputScript + "\"")
    AreEqual(ipi.Start(), True)
    ipi.EnsureInteractive()
    AreEqual("1", ipi.ExecuteLine("x"))
    ipi.End()
    
    inputScript = testpath.test_inputs_dir + "\\raise.py"
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -i \"" + inputScript + "\"")
    AreEqual(ipi.Start(), True)
    ipi.ReadError()
    ipi.EnsureInteractive()
    AreEqual("1", ipi.ExecuteLine("x"))
    ipi.End()
    
    inputScript = testpath.test_inputs_dir + "\\syntaxError.py"
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -i \"" + inputScript + "\"")
    AreEqual(ipi.Start(), True)
    # ipi.EnsureInteractive()
    AssertContains(ipi.ExecuteLine("x", True), "NameError")
    ipi.End()
    
    inputScript = testpath.test_inputs_dir + "\\exit.py"
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -i \"" + inputScript + "\"")
    (result, output, output2, exitCode) = ipi.StartAndRunToCompletion()
    AreEqual(exitCode, 0)
    ipi.End()
    
    # interactive + -c
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -i -c x=2")
    AreEqual(ipi.Start(), True)
    ipi.EnsureInteractive()
    Assert(ipi.ExecuteLine("x", True).find("2") != -1)
    ipi.End()

    
###############################################################################
# Test sys.exitfunc

def test_sys_exitfunc():
    import clr
        
    inputScript = testpath.test_inputs_dir + "\\exitFuncRuns.py"
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " \"" + inputScript + "\"")
    (result, output, output2, exitCode) = ipi.StartAndRunToCompletion()
    AreEqual(exitCode, 0)
    AreEqual(output.find('hello world') > -1, True)
    ipi.End()
    
    args = extraArgs
    
    if clr.GetCurrentRuntime().Configuration.DebugMode:
        args = "-D " + args

    inputScript = testpath.test_inputs_dir + "\\exitFuncRaises.py"
    ipi = IronPythonInstance(executable, exec_prefix, args + " \"" + inputScript + "\"")
    (result, output, output2, exitCode) = ipi.StartAndRunToCompletion()
    AreEqual(exitCode, 0)
    AreEqual(output2.find('Error in sys.exitfunc:') > -1, True)
    
    AreEqual(output2.find('exitFuncRaises.py", line 19, in foo') > -1, True)
        
    ipi.End()
    
    # verify sys.exit(True) and sys.exit(False) return 1 and 0
    
    ipi = IronPythonInstance(executable, exec_prefix, '-c "import sys; sys.exit(False)"')
    res = ipi.StartAndRunToCompletion()
    AreEqual(res[0], True)  # should have started
    AreEqual(res[1], '')    # no std out
    AreEqual(res[2], '')    # no std err
    AreEqual(res[3], 0)     # should return 0

    ipi = IronPythonInstance(executable, exec_prefix, '-c "import sys; sys.exit(True)"')
    res = ipi.StartAndRunToCompletion()
    AreEqual(res[0], True)  # should have started
    AreEqual(res[1], '')    # no std out
    AreEqual(res[2], '')    # no std err
    AreEqual(res[3], 1)     # should return 0
    
    # and verify it works at the interactive console as well
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # parameterless exception
    ipi.ExecuteLine("import sys")
    AreEqual(ipi.ExecuteAndExit("sys.exit(False)"), 0)

    # and verify it works at the interactive console as well
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # parameterless exception
    ipi.ExecuteLine("import sys")
    AreEqual(ipi.ExecuteAndExit("sys.exit(True)"), 1)


#############################################################################
# verify we need to dedent to a previous valid indentation level

def test_indentation():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("if False:")
    ipi.ExecutePartialLine("    print 'hello'")
    response = ipi.ExecuteLine("  print 'goodbye'", True)
    AreEqual(response.find('IndentationError') > 1, True)
    ipi.End()

#############################################################################
# verify we dump exception details

def test_dump_exception():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -X:ExceptionDetail")
    AreEqual(ipi.Start(), True)
    response = ipi.ExecuteLine("raise 'goodbye'", True)
    AreEqual(response.count("IronPython.Hosting") >= 1, True)
    ipi.End()

#############################################################################
# make sure we can enter try/except blocks

def test_try_except():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("try:")
    ipi.ExecutePartialLine("    raise Exception('foo')")
    ipi.ExecutePartialLine("except Exception, e:")
    ipi.ExecutePartialLine("    if e.message=='foo':")
    ipi.ExecutePartialLine("        print 'okay'")
    response = ipi.ExecuteLine("")
    Assert(response.find('okay') > -1)
    ipi.End()

###########################################################
# Throw on "complete" incomplete syntax bug #864

def test_incomplate_syntax():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("class K:")
    response = ipi.ExecuteLine("", True)
    Assert("IndentationError:" in response)
    ipi.End()

def test_incomplate_syntax_backslash():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
       
    for i in range(4):
        for j in range(i):
            ipi.ExecutePartialLine("\\")
        ipi.ExecutePartialLine("1 + \\")
        for j in range(i):
            ipi.ExecutePartialLine("\\")
        response = ipi.ExecuteLine("2", True)
        Assert("3" in response)
    
    ipi.End()

###########################################################
# if , while, try, for and then EOF.
def test_missing_test():
    for x in ['if', 'while', 'for', 'try']:
        ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
        AreEqual(ipi.Start(), True)
        response = ipi.ExecuteLine(x, True)
        Assert("SyntaxError:" in response)
        ipi.End()

##########################################################
# Support multiple-levels of indentation
def test_indentation_levels():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("class K:")
    ipi.ExecutePartialLine("  def M(self):")
    ipi.ExecutePartialLine("    if 1:")
    ipi.ExecutePartialLine("      pass")
    response = ipi.ExecuteLine("")
    ipi.End()

##########################################################
# Support partial lists
def test_partial_lists():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("[1")
    ipi.ExecutePartialLine("  ,")
    ipi.ExecutePartialLine("    2")
    response = ipi.ExecuteLine("]")
    Assert("[1, 2]" in response)
    
    ipi.ExecutePartialLine("[")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine("")
    response = ipi.ExecuteLine("]")
    Assert("[]" in response)
    ipi.End()
    
def test_partial_lists_cp3530():

    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    try:
        ipi.ExecutePartialLine("[{'a':None},")
        response = ipi.ExecuteLine("]")
        Assert("[{'a': None}]" in response, response)
    
        ipi.ExecutePartialLine("[{'a'")
        response = ipi.ExecutePartialLine(":None},")
        response = ipi.ExecuteLine("]")
        Assert("[{'a': None}]" in response, response)
    
        ipi.ExecutePartialLine("[{'a':None},")
        ipi.ExecutePartialLine("1,")
        response = ipi.ExecuteLine("2]")
        Assert("[{'a': None}, 1, 2]" in response, response)
    
    finally:
        ipi.End()
    
    
##########################################################
# Support partial tuples
def test_partial_tuples():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("(2")
    ipi.ExecutePartialLine("  ,")
    ipi.ExecutePartialLine("    3")
    response = ipi.ExecuteLine(")")
    Assert("(2, 3)" in response)
    
    ipi.ExecutePartialLine("(")
    response = ipi.ExecuteLine(")")
    Assert("()" in response)
    
    ipi.ExecutePartialLine("'abc %s %s %s %s %s' % (")
    ipi.ExecutePartialLine("    'def'")
    ipi.ExecutePartialLine("    ,'qrt',")
    ipi.ExecutePartialLine("    'jkl'")
    ipi.ExecutePartialLine(",'jkl'")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine(",")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine("'123'")
    response = ipi.ExecuteLine(")")
    Assert("'abc def qrt jkl jkl 123'" in response)
    
    ipi.ExecutePartialLine("a = (")
    ipi.ExecutePartialLine("    1")
    ipi.ExecutePartialLine(" , ")
    ipi.ExecuteLine(")")
    response = ipi.ExecuteLine("a")
    Assert("(1,)" in response)
    
    ipi.ExecutePartialLine("(")
    ipi.ExecutePartialLine("'joe'")
    ipi.ExecutePartialLine(" ")
    ipi.ExecutePartialLine("       #")
    ipi.ExecutePartialLine(",")
    ipi.ExecutePartialLine("2")
    response = ipi.ExecuteLine(")")
    Assert("('joe', 2)" in response)
    
    ipi.ExecutePartialLine("(")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine("")
    response = ipi.ExecuteLine(")")
    Assert("()" in response)
    
    ipi.End()

##########################################################
# Support partial dicts
def test_partial_dicts():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("{2:2")
    ipi.ExecutePartialLine("  ,")
    ipi.ExecutePartialLine("    2:2")
    response = ipi.ExecuteLine("}")
    Assert("{2: 2}" in response)
    
    ipi.ExecutePartialLine("{")
    response = ipi.ExecuteLine("}")
    Assert("{}" in response)
    
    ipi.ExecutePartialLine("a = {")
    ipi.ExecutePartialLine("    None:2")
    ipi.ExecutePartialLine(" , ")
    ipi.ExecuteLine("}")
    response = ipi.ExecuteLine("a")
    Assert("{None: 2}" in response)
    
    ipi.ExecutePartialLine("{")
    ipi.ExecutePartialLine("'joe'")
    ipi.ExecutePartialLine(": ")
    ipi.ExecutePartialLine("       42")
    ipi.ExecutePartialLine(",")
    ipi.ExecutePartialLine("3:45")
    response = ipi.ExecuteLine("}")
    Assert(repr({'joe':42, 3:45})  in response)
    
    ipi.ExecutePartialLine("{")
    ipi.ExecutePartialLine("")
    ipi.ExecutePartialLine("")
    response = ipi.ExecuteLine("}")
    Assert("{}" in response)

    ipi.End()

###########################################################
# Some whitespace wackiness
def test_whitespace():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecuteLine("  ")
    response = ipi.ExecuteLine("")
    ipi.End()
    
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecuteLine("  ")
    response = ipi.ExecuteLine("2")
    Assert("2" in response)
    ipi.End()
    
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecuteLine("  ")
    response = ipi.ExecuteLine("  2", True)
    Assert("SyntaxError:" in response)
    ipi.End()


###########################################################
# test the indentation error in the interactive mode
def test_indentation_interactive():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("class C:pass")
    response = ipi.ExecuteLine("")
    AreEqual(response, "")
    ipi.ExecutePartialLine("class D(C):")
    response = ipi.ExecuteLine("", True)
    Assert("IndentationError:" in response)
    ipi.End()

###########################################################
# test /mta w/ no other args

def test_mta():
    ipi = IronPythonInstance(executable, exec_prefix, '-X:MTA')
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("class C:pass")
    response = ipi.ExecuteLine("")
    AreEqual(response, "")
    ipi.ExecutePartialLine("class D(C):")
    response = ipi.ExecuteLine("", True)
    Assert("IndentationError:" in response)
    ipi.End()


###########################################################
# test for comments  in interactive input

def test_comments():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    response = ipi.ExecuteLine("# this is some comment line")
    AreEqual(response, "")
    response = ipi.ExecuteLine("    # this is some comment line")
    AreEqual(response, "")
    response = ipi.ExecuteLine("# this is some more comment line")
    AreEqual(response, "")
    ipi.ExecutePartialLine("if 100:")
    ipi.ExecutePartialLine("    print 100")
    ipi.ExecutePartialLine("# this is some more comment line inside if")
    ipi.ExecutePartialLine("#     this is some indented comment line inside if")
    ipi.ExecutePartialLine("    print 200")
    response = ipi.ExecuteLine("")
    AreEqual(response, "100" + newline + "200")
    ipi.End()
    
def test_global_values():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecuteLine("import clr")
    response = ipi.ExecuteLine("[x for x in globals().values()]")
    Assert(response.startswith('['))
    d = eval(ipi.ExecuteLine("globals().fromkeys(['a', 'b'], 'c')"))
    AreEqual(d, {'a':'c', 'b':'c'})
    
def test_globals8961():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    response = ipi.ExecuteLine("print globals().keys()")
    res = set(eval(response))
    AreEqual(res, set(['__builtins__', '__name__', '__doc__']))
    
    ipi.ExecuteLine("a = None")
    response = ipi.ExecuteLine("print globals().keys()")
    res = set(eval(response))
    AreEqual(res, set(['__builtins__', '__name__', '__doc__', 'a']))
    response = ipi.ExecuteLine("print globals().values()")
    l = eval(response.replace("<module '__builtin__' (built-in)>", '"builtin"'))
    res = set(l)
    AreEqual(len(l), 4)
    AreEqual(res, set(['builtin', '__main__', None]))
    
    ipi.ExecuteLine("b = None")
    response = ipi.ExecuteLine("print globals().values()")
    l = eval(response.replace("<module '__builtin__' (built-in)>", '"builtin"'))
    res = set(l)
    AreEqual(len(l), 5)
    AreEqual(res, set(['builtin', '__main__', None]))
    

def test_console_input_output():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    input_output = [
    ("x=100",""),
    ("x=200\n",""),
    ("\nx=300",""),
    ("\nx=400\n",""),
    ("500","500"),
    ("600\n\n\n\n\n\n\n\n\n\n\n","600"),
    ("valid=3;more_valid=4;valid","3"),
    ("valid=5;more_valid=6;more_valid\n\n\n\n\n","6"),
    ("valid=7;more_valid=8;#valid",""),
    ("valid=9;valid;# more_valid\n","9"),
    ("valid=11;more_valid=12;more_valid# should be valid input\n\n\n\n","12"),
    ]
    
    
    for x in input_output:
        AreEqual(ipi.Start(), True)
        AreEqual(ipi.ExecuteLine(x[0]),x[1])
        ipi.End()
    
# expect a clean exception message/stack from thread
def test_thrown_from_thread():
    inputScript = path_combine(testpath.temporary_dir, "throwingfromthread.py")
    write_to_file(inputScript, '''
def f(): raise AssertionError, 'hello'
import thread, time
thread.start_new_thread(f, tuple())
time.sleep(2)
''')
    
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " " + inputScript)
    (result, output, output2, exitCode) = ipi.StartAndRunToCompletion()
    AreEqual(exitCode, 0)
    Assert("AssertionError: hello" in output2)
    Assert("IronPython." not in output2)     # '.' is necessary here
    ipi.End()

def test_aform_feeds():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    response = ipi.ExecuteLine("\fprint 'hello'")
    AreEqual(response, "hello")
    response = ipi.ExecuteLine("      \fprint 'hello'")
    AreEqual(response, "hello")
    
    ipi.ExecutePartialLine("def f():")
    ipi.ExecutePartialLine("\f    print 'hello'")
    ipi.ExecuteLine('')
    response = ipi.ExecuteLine('f()')
    AreEqual(response, "hello")
    
    # \f resets indent to 0
    ipi.ExecutePartialLine("def f():")
    ipi.ExecutePartialLine("    \f    x = 'hello'")
    ipi.ExecutePartialLine("\f    print x")
    
    ipi.ExecuteLine('')
    response = ipi.ExecuteLine('f()')
    AreEqual(response, "hello")

    # \f resets indent to 0
    ipi.ExecutePartialLine("def f():")
    ipi.ExecutePartialLine("    \f    x = 'hello'")
    ipi.ExecutePartialLine("    print x")
    
    ipi.ExecuteLine('')
    response = ipi.ExecuteLine('f()')
    AreEqual(response, "hello")

def test_ipy_dash_S():
    """ipy -S should still install Lib into sys.path"""
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -S")
    AreEqual(ipi.Start(), True)
    response = ipi.ExecuteLine("import sys")
    response = ipi.ExecuteLine("print sys.path")
    Assert(response.find('Lib') != -1)

def test_startup_dir():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    response = ipi.ExecuteLine("print dir()")
    AreEqual(sorted(eval(response)), sorted(['__builtins__', '__doc__', '__name__']))

def test_ipy_dash_m():
    import sys
    for path in sys.path:
        if path.find('Lib') != -1:
            filename = System.IO.Path.Combine(path, 'somemodule.py')
            break

    try:
        f = file(filename, 'w')
        f.write('print "hello"\n')
        f.write('import sys\n')
        f.write('print sys.argv')
        f.close()

        # need to run these tests where we have access to runpy.py
        path = System.IO.FileInfo(__file__).DirectoryName

        # simple case works
        ipi = IronPythonInstance(executable, path, extraArgs + " -m somemodule")
        res, output, err, exit = ipi.StartAndRunToCompletion()
        AreEqual(res, True) # run should have worked
        AreEqual(exit, 0)   # should have returned 0
        output = output.replace('\r\n', '\n')
        lines = output.split('\n')
        AreEqual(lines[0], 'hello')
        Assert(samefile(eval(lines[1])[0], 
                        filename))
        
        # we receive any arguments in sys.argv
        ipi = IronPythonInstance(executable, path, extraArgs + " -m somemodule foo bar")
        res, output, err, exit = ipi.StartAndRunToCompletion()
        AreEqual(res, True) # run should have worked
        AreEqual(exit, 0)   # should have returned 0
        output = output.replace('\r\n', '\n')
        lines = output.split('\n')
        AreEqual(lines[0], 'hello')
        AreEqual(eval(lines[1]), [filename, 'foo', 'bar'])

        f = file(filename, 'w')
        f.write('print "hello"\n')
        f.write('import sys\n')
        f.write('sys.exit(1)')
        f.close()
        
        # sys.exit works
        ipi = IronPythonInstance(executable, path, extraArgs + " -m somemodule")
        res, output, err, exit = ipi.StartAndRunToCompletion()
        AreEqual(res, True) # run should have worked
        AreEqual(exit, 1)   # should have returned 0
        output = output.replace('\r\n', '\n')
        lines = output.split('\n')
        AreEqual(lines[0], 'hello')
    finally:
        nt.unlink(filename)

@disabled("CodePlex Work Item 10925")
def test_ipy_dash_m_negative():        
    # builtin modules should not work
    for modname in [ "sys", "datetime" ]:
        ipi = IronPythonInstance(executable, exec_prefix,
                                 extraArgs + " -m " + modname)
        res, output, err, exit = ipi.StartAndRunToCompletion()
        AreEqual(exit, -1)

    # Modules within packages should not work
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -m testpkg1.mod1")
    res, output, err, exit = ipi.StartAndRunToCompletion()
    AreEqual(res, True) # run should have worked
    AreEqual(exit, 1)   # should have returned 0
    Assert("SyntaxError: invalid syntax" in err,
           "stderr is:" + str(err))

        
def test_ipy_dash_m_pkgs(): 
    # Python packages work
    import nt
    Assert("testpkg1" in [x.lower() for x in nt.listdir(nt.getcwd())], nt.getcwd())
    
    old_ipy_path = get_environ_variable("IRONPYTHONPATH")
    try:
        nt.environ["IRONPYTHONPATH"] = nt.getcwd()
        ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -m testpkg1")
        res, output, err, exit = ipi.StartAndRunToCompletion()
        AreEqual(res, True) # run should have worked
        AreEqual(exit, 0)   # should have returned 0
        AreEqual(output, "")
    
        # Bad module names should not work
        ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " -m libxyz")
        res, output, err, exit = ipi.StartAndRunToCompletion()
        AreEqual(res, True) # run should have worked
        AreEqual(exit, 1)   # should have returned 0
        Assert("ImportError: No module named libxyz" in err,
               "stderr is:" + str(err))
    finally:
        nt.environ["IRONPYTHONPATH"] = old_ipy_path  

    
def test_ipy_dash_c():
    """verify ipy -c cmd doesn't print expression statements"""
    ipi = IronPythonInstance(executable, exec_prefix, "-c True;False")
    res = ipi.StartAndRunToCompletion()
    AreEqual(res[0], True)  # should have started
    AreEqual(res[1], '')    # no std out
    AreEqual(res[2], '')    # no std err
    AreEqual(res[3], 0)     # should return 0

#############################################################################
# CP11924 - verify 'from __future__ import division' works
def test_future_division():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecuteLine("from __future__ import division")
    response = ipi.ExecuteLine("11/4")
    AreEqual(response, "2.75")
    ipi.End()


#############################################################################
# CP2206
def test_future_with():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    ipi.ExecutePartialLine("class K(object):")
    ipi.ExecutePartialLine("    def __enter__(self): return 3.14")
    ipi.ExecutePartialLine("    def __exit__(self, type, value, tb): return False")
    ipi.ExecuteLine("")
    ipi.ExecutePartialLine("with K() as d:")
    ipi.ExecutePartialLine("    print d")
    response = ipi.ExecuteLine("")
    AreEqual(response, "3.14")
    ipi.End()

#############################################################################
# Merlin 148481
def test_ipy_dash():
    #Verify that typing a - in the arguments starts an interactive session
    ipi = IronPythonInstance(executable, exec_prefix, "-")
    AreEqual(ipi.Start(), True)
    response = ipi.ExecuteLine("42")
    AreEqual(response, "42")
    ipi.End()

#############################################################################
def test_mta():
    ipi = IronPythonInstance(executable, exec_prefix, '-X:MTA')
    AreEqual(ipi.Start(), True)
    ipi.ExecuteLine("import System")
    response = ipi.ExecuteLine("str(System.Threading.Thread.CurrentThread.ApartmentState)")
    AreEqual(response, "'MTA'")
    
    ipi.ExecutePartialLine("class C:pass")
    response = ipi.ExecuteLine("")
    AreEqual(response, "")
    
    response = ipi.ExecuteLine("str(System.Threading.Thread.CurrentThread.ApartmentState)")
    AreEqual(response, "'MTA'")
    ipi.End()

def test_displayhook():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # parameterless exception
    ipi.ExecuteLine("import sys")
    ipi.ExecutePartialLine("def f(x): print 'foo', x")
    ipi.ExecuteLine("")
    response = ipi.ExecuteLine("sys.displayhook = f")
    response = ipi.ExecuteLine("42")
    AreEqual(response, "foo 42")

def test_excepthook():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # parameterless exception
    ipi.ExecuteLine("import sys")
    ipi.ExecutePartialLine("def f(*args): print 'foo', args")
    ipi.ExecuteLine("")
    response = ipi.ExecuteLine("sys.excepthook = f")
    response = ipi.ExecuteLine("raise Exception", True)
    AssertContains(response, "foo (<type 'exceptions.Exception'>, Exception(), <traceback object at")

def test_last_exception():
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    
    # parameterless exception
    ipi.ExecuteLine("import sys")
    response = ipi.ExecuteLine("hasattr(sys, 'last_value')")
    AreEqual(response, 'False')
    AssertContains(ipi.ExecuteLine("x", True), "NameError")
    response = ipi.ExecuteLine("sys.last_value")
    AreEqual(response, "NameError(\"name 'x' is not defined\",)")
    response = ipi.ExecuteLine("sys.last_type")
    AreEqual(response, "<type 'exceptions.NameError'>")
    response = ipi.ExecuteLine("sys.last_traceback")
    AssertContains(response, "<traceback object at ")

def test_sta_sleep_Warning():
    ipi = IronPythonInstance(executable, exec_prefix, '-c "from System.Threading import Thread;Thread.Sleep(100)"')    
    retval, stdouttext, stderrtext, exitcode = ipi.StartAndRunToCompletion()
    Assert(stderrtext.endswith("RuntimeWarning: Calling Thread.Sleep on an STA thread doesn't pump messages.  Use Thread.CurrentThread.Join instead.\r\n"))

def test_newline():
    ipi = IronPythonInstance(executable, exec_prefix, "")
    ipi.proc.Start()
    ipi.reader = ipi.proc.StandardOutput
    output = ipi.EatToPrompt()
    Assert('\r\r\n' not in output)
    Assert('\r\n' in output)
    
#############################################################################
# Remote console tests

from System.Diagnostics import Process

def get_process_ids(ipi):
    ipi.EnsureInteractiveRemote()
    ipi.proc.Refresh()
    consoleProcessId = ipi.proc.Id
    ipi.ExecuteLine("import System")
    remoteRuntimeProcessId = ipi.ExecuteLineRemote("System.Diagnostics.Process.GetCurrentProcess().Id")
    Assert(remoteRuntimeProcessId.isdigit(), "remoteRuntimeProcessId is '%s'" % remoteRuntimeProcessId)
    return consoleProcessId, int(remoteRuntimeProcessId)

def start_remote_console(args = ""):
    inputScript = testpath.test_inputs_dir + "\\RemoteConsole.py"
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs + " \"" + inputScript + "\" -X:ExceptionDetail " + args)
    AreEqual(ipi.Start(), True)
    return ipi

# Basic check that the remote console actually uses two processes
def test_remote_console_processes():
    # First check that a simple local console uses a single process
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)    
    consoleProcessId, remoteRuntimeProcessId = get_process_ids(ipi)
    AreEqual(consoleProcessId, remoteRuntimeProcessId)
    ipi.End()
        
    # Now use the remote console
    ipi = start_remote_console()
    consoleProcessId, remoteRuntimeProcessId = get_process_ids(ipi)
    AreNotEqual(consoleProcessId, remoteRuntimeProcessId)
    ipi.End()

# The remote runtime should terminate when the console terminates
def test_remote_runtime_normal_exit():
    ipi = start_remote_console()
    consoleProcessId, remoteRuntimeProcessId = get_process_ids(ipi)
    runtimeProcess = Process.GetProcessById(remoteRuntimeProcessId)
    Assert(not runtimeProcess.HasExited)
    ipi.End()
    runtimeProcess.WaitForExit() # The test is that this wait succeeds
    
# Stress the input-output streams
def test_remote_io():
    ipi = start_remote_console()
    for i in range(100):
        AreEqual(ipi.ExecuteLineRemote("2+2"), "4")
    ipi.End()
    
# Kill the remote runtime and ensure that another process starts up again
def test_remote_server_restart():
    ipi = start_remote_console()
    consoleProcessId, remoteRuntimeProcessId = get_process_ids(ipi)
    runtimeProcess = Process.GetProcessById(remoteRuntimeProcessId)
    AreNotEqual(runtimeProcess, consoleProcessId)
    
    runtimeProcess.Kill()
    runtimeProcess.WaitForExit()
    # The Process.Exited event is fired asynchronously, and might take sometime to fire. 
    # Hence, we need to block for a known marker
    ipi.EatToMarker("Remote runtime terminated")

    # We need to press Enter to nudge the old console out of the ReadLine...
    restartMessage = ipi.ExecuteLine("", True)
    ipi.ReadError()
    
    consoleProcessId2, remoteRuntimeProcessId2 = get_process_ids(ipi)
    AreEqual(consoleProcessId, consoleProcessId2)
    # This is technically not a 100% correct as there is a small chance the the process id might get reused
    AreNotEqual(remoteRuntimeProcessId, remoteRuntimeProcessId2)
    ipi.End()

# Check that an exception can be remoted back over the reverse channel
# Note that exceptions are not written to stdout by the remote process
def test_remote_console_exception():
    ipi = start_remote_console()
    zeroDivisionErrorOutput = ipi.ExecuteLine("1/0", True)
    AssertContains(zeroDivisionErrorOutput, "ZeroDivisionError")
    ipi.End()

def test_remote_startup_script():
    ipi = start_remote_console("-i " + testpath.test_inputs_dir + "\\simpleCommand.py")
    AreEqual(ipi.ExecuteLine("x"), "1")
    ipi.End()

def get_abort_command_output():
    ipi = start_remote_console()
    ipi.ExecuteLine("import System")
    
    ipi.ExecutePartialLine  ("def Hang():")
    ipi.ExecutePartialLine  ("    print 'ABORT ME!!!' # This string token should trigger an abort...")
    ipi.ExecutePartialLine  ("    infinite = System.Threading.Timeout.Infinite")
    ipi.ExecutePartialLine  ("    System.Threading.Thread.CurrentThread.Join(infinite)")
    ipi.ExecuteLine         ("")

    result = ipi.ExecuteLine("Hang()", True)
    ipi.End()
    return result

def test_remote_abort_command():
    for i in range(10):
        output = get_abort_command_output()
        if "KeyboardInterrupt" in output:
            AssertDoesNotContain(output, "Thread was being aborted.") # ThreadAbortException
            return
        else:
            # Rarely, under stress conditions, ThreadAbortException leaks through.
            # Keep retrying until we actually get KeyboardInterrupt
            AssertContains(output, "Thread was being aborted.") # ThreadAbortException
            continue
    Assert(False, "KeyboardInterrupt not thrown. Only KeyboardInterrupt was thrown")

def test_exception_slicing_warning():
    ipi = IronPythonInstance(executable, exec_prefix, '-c "print Exception(*range(2))[1]"')
    res = ipi.StartAndRunToCompletion()
    AreEqual(res[0], True)  # should have started
    AreEqual(res[1], '1\r\n')   # some std out
    AreEqual(res[2], '')    # no std err
    AreEqual(res[3], 0)     # should return 0
    
    ipi = IronPythonInstance(executable, exec_prefix,
        '-3 -c "import warnings;'
        'warnings.filters.reverse();'
        'warnings.filters.pop();'
        'print Exception(*range(2))[1]"')
    res = ipi.StartAndRunToCompletion()
    AreEqual(res[0], True)  # should have started
    AreEqual(res[1], '1\r\n')   # std out
    Assert(res[2].endswith('DeprecationWarning: __getitem__ not supported for exception classes in 3.x; use args attribute\r\n')) #std err
    AreEqual(res[3], 0)     # should return 0

#------------------------------------------------------------------------------

run_test(__name__)
