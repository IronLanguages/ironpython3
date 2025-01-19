# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Testing IronPython Engine
##

from iptest.assert_util import *
skiptest("win32")
import sys

remove_ironpython_dlls(testpath.public_testdir)
load_iron_python_dll()

# setup Scenario tests in module from EngineTest.cs
# this enables us to see the individual tests that pass / fail
load_iron_python_test()

import IronPython
import IronPythonTest

et = IronPythonTest.EngineTest()
multipleexecskips = [ ]
for s in dir(et):
    if s.startswith("Scenario"):
        if s in multipleexecskips:
            exec('@skip("multiple_execute") \ndef test_Engine_%s(): getattr(et, "%s")()' % (s, s))
        else :
            exec('def test_Engine_%s(): getattr(et, "%s")()' % (s, s))

#Rowan Work Item 312902
@disabled("The ProfileDrivenCompilation feature is removed from DLR")
def test_deferred_compilation():
    save1 = IronPythonTest.TestHelpers.GetContext().Options.InterpretedMode
    save2 = IronPythonTest.TestHelpers.GetContext().Options.ProfileDrivenCompilation
    modules = sys.modules.copy()
    IronPythonTest.TestHelpers.GetContext().Options.ProfileDrivenCompilation = True # this will enable interpreted mode
    Assert(IronPythonTest.TestHelpers.GetContext().Options.InterpretedMode)
    try:
        # Just import some modules to make sure we can switch to compilation without blowing up
        import test_namebinding
        import test_function
        import test_tcf
    finally:
        IronPythonTest.TestHelpers.GetContext().Options.InterpretedMode = save1
        IronPythonTest.TestHelpers.GetContext().Options.ProfileDrivenCompilation = save2
        sys.modules = modules

def CreateOptions():
    import sys
    import clr
    
    o = IronPython.PythonEngineOptions()
    if sys.argv.count('-X:ExceptionDetail') > 0: o.ExceptionDetail = True
    return o

def a():
    raise System.Exception()

def b():
    try:
        a()
    except System.Exception as e:
        raise System.Exception("second", e)

def c():
    try:
        b()
    except System.Exception as e:
        x = System.Exception("first", e)
    return x

#Rowan Work Item 312902
@skip("multiple_execute")
def test_formatexception():
    try:
        import Microsoft.Scripting
        from IronPython.Hosting import Python
        pe = Python.CreateEngine()
        
        service = pe.GetService[Microsoft.Scripting.Hosting.ExceptionOperations]()
        AssertError(TypeError, service.FormatException, None)
    
        exc_string = service.FormatException(System.Exception("first",
                                                        System.Exception("second",
                                                                         System.Exception())))
        AreEqual(exc_string, 'Traceback (most recent call last):[NEWLINE]Exception: first[NEWLINE]'.replace('[NEWLINE]',  System.Environment.NewLine))
        exc_string = service.FormatException(c())
        AreEqual(exc_string.count(" File "), 4)
        AreEqual(exc_string.count(" line "), 4)
    finally:
        pass

#Rowan Work Item 31290
def test_formatexception_showclrexceptions():
    import Microsoft.Scripting
    from IronPython.Hosting import Python
    pe = Python.CreateEngine({'ShowClrExceptions': True})

    exc_string = pe.GetService[Microsoft.Scripting.Hosting.ExceptionOperations]().FormatException(System.Exception("first",
                                                    System.Exception("second",
                                                                     System.Exception())))
    AreEqual(exc_string, "Traceback (most recent call last):[NEWLINE]Exception: first[NEWLINE]CLR Exception: [NEWLINE]    Exception[NEWLINE]: [NEWLINE]first[NEWLINE]    Exception[NEWLINE]: [NEWLINE]second[NEWLINE]    Exception[NEWLINE]: [NEWLINE]Exception of type 'System.Exception' was thrown.[NEWLINE]".replace("[NEWLINE]", System.Environment.NewLine))
    exc_string = pe.GetService[Microsoft.Scripting.Hosting.ExceptionOperations]().FormatException(c())
    
    AreEqual(exc_string.count(" File "), 4)
    AreEqual(exc_string.count(" line "), 4)
    Assert(exc_string.endswith("CLR Exception: [NEWLINE]    Exception[NEWLINE]: [NEWLINE]first[NEWLINE]    Exception[NEWLINE]: [NEWLINE]second[NEWLINE]    Exception[NEWLINE]: [NEWLINE]Exception of type 'System.Exception' was thrown.[NEWLINE]".replace("[NEWLINE]", System.Environment.NewLine)))

@skip("multiple_execute") #CodePlex 20636 - multi-execute
def test_formatexception_exceptiondetail():
    import Microsoft.Scripting
    from IronPython.Hosting import Python
            
    pe = Python.CreateEngine({'ExceptionDetail': True})

    try:
        x = System.Collections.Generic.Dictionary[object, object]()
        x[None] = 42
    except System.Exception as e:
        pass
    
    exc_string = pe.GetService[Microsoft.Scripting.Hosting.ExceptionOperations]().FormatException(System.Exception("first", e))
    Assert(exc_string.startswith("first"))
    Assert(exc_string.find('Insert') >= 0) 
    exc_string = pe.GetService[Microsoft.Scripting.Hosting.ExceptionOperations]().FormatException(c())
    Assert(exc_string.endswith("Exception: first[NEWLINE]".replace("[NEWLINE]", System.Environment.NewLine)))

def test_engine_access_from_within():
    import clr
    from Microsoft.Scripting.Hosting import ScriptEngine
    pc = clr.GetCurrentRuntime().GetLanguageByName('python')
    engine = pc.GetModuleState(clr.GetClrType(ScriptEngine))
    Assert(engine is not None)

def test_import_clr():
    from IronPython.Hosting import Python
    eng = Python.CreateEngine()
    mod = Python.ImportModule(eng, 'clr')
    Assert('ToString' not in eng.Operations.GetMemberNames(42))

def test_cp6703():
    import clr
    clr.AddReference("IronPython")
    import IronPython
    pe = IronPython.Hosting.Python.CreateEngine()
    
    stuff = '''
import System
a = 2
globals()["b"] = None
globals().Add("c", "blah")
joe = System.Collections.Generic.KeyValuePair[object,object]("d", int(3))
globals().Add(joe)
count = 0
if globals().Contains(System.Collections.Generic.KeyValuePair[object,object]("b", None)): count += 1
if globals().Contains(System.Collections.Generic.KeyValuePair[object,object]("c", "blah")): count += 1
if globals().Contains(System.Collections.Generic.KeyValuePair[object,object]("d", int(3))): count += 1
if globals().Contains(System.Collections.Generic.KeyValuePair[object,object]("d", 3)): count += 1
if globals().Contains(System.Collections.Generic.KeyValuePair[object,object]("a", 2)): count += 1
'''
    s = pe.CreateScope()
    pe.Execute(stuff, s)
    AreEqual(s.count, 5)
    

def test_cp20594():
    import IronPython
    AreEqual(IronPython.Runtime.PythonContext.GetIronPythonAssembly("IronPython").split(",", 1)[1],
             IronPython.Runtime.PythonContext.GetIronPythonAssembly("IronPython.Modules").split(",", 1)[1])
            

def test_cp27547():
    import clr
    clr.AddReference('IronPython')
    clr.AddReference('Microsoft.Scripting')
    from IronPython.Hosting import Python
    from Microsoft.Scripting import SourceCodeKind, ScriptCodeParseResult
    engine = Python.CreateEngine()
    scope = engine.CreateScope()
    text = 'lambda'
    source = engine.CreateScriptSourceFromString(text, 'stdin',
    SourceCodeKind.InteractiveCode)
    result = source.GetCodeProperties()
    AreEqual(result, ScriptCodeParseResult.IncompleteToken)

def test_hidden_base():
    from IronPythonTest import DerivedFromHiddenBase
    a = DerivedFromHiddenBase()
    AreEqual(a.Accessible(), 42)
    AssertError(AttributeError, lambda: a.Inaccessible)

def test_cp27150():
	from IronPythonTest import GenericProperty
	from System import DateTime
	
	wrapper = GenericProperty[DateTime]()
	def f():
		wrapper.Value = None
	AssertError(TypeError, f)
	
	
#--MAIN------------------------------------------------------------------------        
run_test(__name__)


#Make sure this runs last
#test_dispose()
