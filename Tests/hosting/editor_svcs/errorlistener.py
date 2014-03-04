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

##
## Testing ErrorListener
##
from iptest.assert_util import *
skiptest("win32")

load_iron_python_test()

import Microsoft.Scripting.Hosting
from Microsoft.Scripting import Severity, SourceCodeKind, SourceSpan, SourceLocation
from Microsoft.Scripting.Hosting import ErrorListener, ScriptSource, ScriptRuntime
from IronPython.Hosting import Python

From, To = SourceLocation, SourceLocation
Warning, Error, FatalError = Severity.Warning, Severity.Error, Severity.FatalError

#------------------------------------------------------------------------------
# Globals
engine = Python.CreateEngine()

#------------------------------------------------------------------------------
# Utils
class MyErrorListener(ErrorListener):
    def __init__(self):
        self.__errors= []    
    
    errors = property(lambda obj: obj.__errors)
    
    def ErrorReported(self, src, msg, span, errorCode, severity):        
        line = src.GetCodeLine(span.Start.Line);        
        if line \
            and span.Start.Line == span.End.Line \
            and span.Start.Column != span.End.Column:
            bad = line[span.Start.Column - 1 : span.End.Column - 1]
        else:
            bad = (span.Start.Line, span.Start.Column)                
        self.__errors.append((msg, bad, errorCode, severity))

def compile_expression(expression):
    source = engine.CreateScriptSourceFromString(expression, SourceCodeKind.Expression) 
    return compile_source(source)
    
def compile_file(stmts):
    source = engine.CreateScriptSourceFromString(stmts, SourceCodeKind.File) 
    return compile_source(source)

def compile_source(source):
    errorlistener = MyErrorListener()
    try:
        source.Compile(errorlistener)
    except System.Exception, e:        
        pass
    return errorlistener.errors

#------------------------------------------------------------------------------
# Tests
def test_no_errors():
    AreEqual([], compile_expression("1+1"))
    
def test_empty():
    AreEqual([], compile_file(""))    
    expected = [
        ("unexpected EOF while parsing", (1,1), 17, FatalError),
    ]
    actual = compile_expression("")    
    AreEqual(expected, actual)    

def test_unexpected_token():
    expected = [
        ("unexpected token 'foo'", "foo", 16, FatalError)
    ]
    actual = compile_expression("1.foo")
    AreEqual(expected, actual)
    
def test_multiple_errors():
    expected = [
        ("unexpected token 'print'", "print", 16, FatalError),
        ("EOL while scanning single-quoted string", '"hello', 16, FatalError),
        ("unexpected token 'print'", "print", 16, FatalError),
    ]
    actual = compile_expression("""print "hello""")    
    AreEqual(expected, actual)   
    
def test_not_indented_class():
    expected = [
        ("expected an indented block", "pass", 32, FatalError),
    ]
    code = """\
class Foo:
pass"""       
    AreEqual(expected, compile_file(code))
    
def test_bad_indentation():
    expected = [
        ("unindent does not match any outer indentation level", ' ', 32, FatalError),
    ]
    code = """\
class Foo:
  pass
 pass"""    
    AreEqual(expected, compile_file(code))        

def test_non_fatal_error():
    expected = [   
        ("'break' outside loop", "break", 16, FatalError),     
    ]
    code = """\
1+1
break"""
    AreEqual(expected, compile_file(code))        
    
def test_assignment_to_none():
    expected = [   
        ("cannot assign to None", "None", 80, FatalError),        
    ]
    actual = compile_file("None = 42")        
    AreEqual(expected, actual)

def test_multiple_erroneous_statements():
    expected = [
        ("cannot assign to None", "None", 80, FatalError),
        ("cannot assign to None", "None", 80, FatalError),
    ]
    code = """\
None = 2
None = 3"""            
    AreEqual(expected, compile_file(code))              
    
def test_warning():
    expected = [   
        ("name 'a' is assigned to before global declaration", "global a", -1, Warning),
    ]    
    code = """\
def foo():
  a=2
  global a"""
    AreEqual(expected, compile_file(code))                       

def test_should_report_multiple_warnings_negative():
    "Bug #17541, http://www.codeplex.com/IronPython/WorkItem/View.aspx?WorkItemId=17541"
    expected = [   
        ("Variable a assigned before global declaration", "global a", -1, Warning),
        ("Variable b assigned before global declaration", "global b", -1, Warning),
    ]    
    code = """\
def foo():
  a=2
  global a

def bar():
  b=2
  global b"""
    AssertError(AssertionError, AreEqual, expected, compile_file(code))

def test_should_report_both_errors_and_warnings_negative():    
    "Bug #17541, http://www.codeplex.com/IronPython/WorkItem/View.aspx?WorkItemId=17541"
    expected = [   
        ("cannot assign to None", "None", -1, Error),
        ("Variable a assigned before global declaration", "global a", -1, Warning),
    ]    
    code = """\
None = 2
def foo():
  a=2
  global a"""
    AssertError(AssertionError, AreEqual, expected, compile_file(code))
    
def test_all_together():        
    expected = [   
        ('cannot assign to None', 'None', 80,FatalError),
    ]        
    code = """\
None = 2
dict={foo}
def foo():
  a=2
  global a"""
    AreEqual(expected, compile_file(code))                 
    
run_test(__name__)
