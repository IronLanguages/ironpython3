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
from iptest.warning_util import warning_trapper
import sys
if not is_silverlight:
    from iptest.process_util import *

year = 2005
month = 3
day = 16
hour = 14
minute = 53
second = 24

if 1900 < year < 2100 and 1 <= month <= 12 \
   and 1 <= day <= 31 and 0 <= hour < 24 \
   and 0 <= minute < 60 and 0 <= second < 60:   # Looks like a valid date
        pass

# Testing the (expr) support

x = 10
AreEqual(x, 10)
del x
try: y = x
except NameError: pass
else: Fail("x not deleted")

(x) = 20
AreEqual((x), 20)
del (x)
try: y = x
except NameError: pass
else: Fail("x not deleted")

# this is comment \
a=10
AreEqual(a, 10)

x = "Th\
\
e \
qu\
ick\
 br\
ow\
\
n \
fo\
\
x\
 ju\
mp\
s \
ove\
\
r \
th\
e l\
az\
\
y d\
og\
.\
\
 \
\
12\
34\
567\
89\
0"

y="\
The\
 q\
ui\
\
c\
k b\
\
r\
o\
w\
n\
 \
fo\
x\
 \
jum\
ps\
 ov\
er \
t\
he\
 la\
\
\
zy\
\
\
 d\
og\
. 1\
2\
\
3\
\
\
\
\
4\
567\
\
8\
\
90\
"

AreEqual(x, y)

AreEqual("\101", "A")
x='\a\b\c\d\e\f\g\h\i\j\k\l\m\n\o\p\q\r\s\t\u\v\w\y\z'
y=u'\u0007\u0008\\\u0063\\\u0064\\\u0065\u000C\\\u0067\\\u0068\\\u0069\\\u006a\\\u006b\\\u006c\\\u006d\u000A\\\u006f\\\u0070\\\u0071\u000D\\\u0073\u0009\\\u0075\u000B\\\u0077\\\u0079\\\u007a'

Assert(x == y)
AreEqual(x, y)

for a,b in zip(x,y):
    AreEqual(a,b)

Assert((10==20)==(20==10))
AreEqual(10==20, 20==10)
AreEqual(4e4-4, 4e4 - 4)

c = compile("071 + 1", "Error", "eval")

AssertError(SyntaxError, compile, "088 + 1", "Error", "eval")
AssertError(SyntaxError, compile, "099 + 1", "Error", "eval")
AssertError(SyntaxError, compile, """
try:
    pass
""", "Error", "single")

AssertError(SyntaxError, compile, "x=10\ny=x.", "Error", "exec")

def run_compile_test(code, msg, lineno, skipCpy):
    if skipCpy and not is_cli:
        return
    filename = "the file name"
    try:
        compile(code, filename, "exec")
    except SyntaxError, e:
        AreEqual(e.msg, msg)
        AreEqual(e.lineno, lineno)
        AreEqual(e.filename, filename)
    else:
        Assert(False, "Expected exception, got none")

if is_ironpython:
    _yield_msg = "can't assign to yield expression"
else:
    _yield_msg = "assignment to yield expression not possible"
compile_tests = [
    ("for x notin []:\n    pass", "unexpected token 'notin'", 1, True),
    ("global 1", "unexpected token '1'", 1, True),
    ("x=10\nyield x\n", "'yield' outside function", 2, False),
    ("return\n", "'return' outside function", 1, False),
    #("print >> 1 ,\n", "unexpected token '<eof>'", 1, False),
    ("def f(x=10, y):\n    pass", "default value must be specified here", 1, True),
    ("def f(for):\n    pass", "unexpected token 'for'", 1, True),
    ("f(3 = )", "expected name", 1, True),
    ("dict(a=1,a=2)", "duplicate keyword argument", 1, True),
    ("def f(a,a): pass", "duplicate argument 'a' in function definition", 1, False),
    ("def f((a,b),(c,b)): pass", "duplicate argument 'b' in function definition", 1, False),
    ("x = 10\nx = x[]", "unexpected token ']'", 2, True),
    ("break", "'break' outside loop", 1, False),
    ("if 1:\n\tbreak", "'break' outside loop", 2, False),
    ("if 1:\n\tx+y=22", "can't assign to operator", 2, False),
    ("if 1:\n\tdel f()", "can't delete function call", 2, False),
    ("def a(x):\n    def b():\n        print x\n    del x", "can not delete variable 'x' referenced in nested scope", 2, True),
    ("if 1:\nfoo()\n", "expected an indented block", 2, False),
    ("'abc'.1", "invalid syntax", 1, True),
    ("'abc'.1L", "invalid syntax", 1, False),
    ("'abc'.1j", "invalid syntax", 1, True),
    ("'abc'.0xFFFF", "invalid syntax", 1, False),
    ("'abc' 1L", "invalid syntax", 1, True),
    ("'abc' 1.0", "invalid syntax", 1, True),
    ("'abc' 0j", "invalid syntax", 1, True),
    ("x = 'abc'\nx.1", "invalid syntax", 2, False),
    ("x = 'abc'\nx 1L", "invalid syntax", 2, False),
    ("x = 'abc'\nx 1.0", "invalid syntax", 2, False),
    ("x = 'abc'\nx 0j", "invalid syntax", 2, False),
    ('def f():\n    del (yield 5)\n', "can't delete yield expression", 2, False),
    ('a,b,c += 1,2,3', "illegal expression for augmented assignment", 1, False),
    ('def f():\n    a = yield 3 = yield 4', _yield_msg, 2, False),
    ('((yield a), 2,3) = (2,3,4)', "can't assign to yield expression", 1, False),
    ('(2,3) = (3,4)', "can't assign to literal", 1, False),
    ("def e():\n    break", "'break' outside loop", 2, False),
    ("def g():\n    for x in range(10):\n        print x\n    break\n", "'break' outside loop", 4, False),
    ("def g():\n    for x in range(10):\n        print x\n    if True:\n        break\n", "'break' outside loop", 5, False),
    ("def z():\n    if True:\n        break\n", "'break' outside loop", 3, False),
    ('from import abc', "invalid syntax", 1, False),
    ('() = 1', "can't assign to ()", 1, False),
    ("""for x in range(100):\n"""
     """    try:\n"""
     """        [1,2][3]\n"""
     """    except IndexError:\n"""
     """        pass\n"""
     """    finally:\n"""
     """        continue\n""", "'continue' not supported inside 'finally' clause", 7, False)

    #CodePlex 15428
    #("'abc'.", "invalid syntax", 1),
]

compile_tests.append(("None = 2", "cannot assign to None", 1, False))

# different error messages, ok
for test in compile_tests:
    run_compile_test(*test)
            
AreEqual(float(repr(2.5)), 2.5)

AreEqual(eval("1, 2, 3,"), (1, 2, 3))

# eval validates end of input
AssertError(SyntaxError, compile, "1+2 1", "Error", "eval")

# empty test list in for expression
AssertError(SyntaxError, compile, "for x in : print x", "Error", "exec")
AssertError(SyntaxError, compile, "for x in : print x", "Error", "eval")
AssertError(SyntaxError, compile, "for x in : print x", "Error", "single")

# empty backquote
AssertError(SyntaxError, compile, "``", "Error", "exec")
AssertError(SyntaxError, compile, "``", "Error", "eval")
AssertError(SyntaxError, compile, "``", "Error", "single")

# empty assignment expressions
AssertError(SyntaxError, compile, "x = ", "Error", "exec")
AssertError(SyntaxError, compile, "x = ", "Error", "eval")
AssertError(SyntaxError, compile, "x = ", "Error", "single")
AssertError(SyntaxError, compile, "x = y = ", "Error", "exec")
AssertError(SyntaxError, compile, "x = y = ", "Error", "eval")
AssertError(SyntaxError, compile, "x = y = ", "Error", "single")
AssertError(SyntaxError, compile, " = ", "Error", "exec")
AssertError(SyntaxError, compile, " = ", "Error", "eval")
AssertError(SyntaxError, compile, " = ", "Error", "single")
AssertError(SyntaxError, compile, " = 4", "Error", "exec")
AssertError(SyntaxError, compile, " = 4", "Error", "eval")
AssertError(SyntaxError, compile, " = 4", "Error", "single")
AssertError(SyntaxError, compile, "x <= ", "Error", "exec")
AssertError(SyntaxError, compile, "x <= ", "Error", "eval")
AssertError(SyntaxError, compile, "x <= ", "Error", "single")
#indentation errors - BUG 864
AssertError(IndentationError, compile, "class C:\nx=2\n", "Error", "exec")
AssertError(IndentationError, compile, "class C:\n\n", "Error", "single")

#allow \f
compile('\f\f\f\f\fclass C:\f\f\f pass', 'ok', 'exec')
compile('\f\f\f\f\fclass C:\n\f\f\f    print "hello"\n\f\f\f\f\f\f\f\f\f\f    print "goodbye"', 'ok', 'exec')
compile('class C:\n\f\f\f    print "hello"\n\f\f\f\f\f\f\f\f\f\f    print "goodbye"', 'ok', 'exec')
compile('class \f\f\f\fC:\n\f    print "hello"\n\f\f\f\f\f\f\f\f\f\f    print "goodbye"', 'ok', 'exec')

# multiline expression passed to exec (positive test)
s = """
title = "The Cat"
Assert(title.istitle())
x = 2 + 5
AreEqual(x, 7)
"""
exec s

if is_cpython:
    # this seems to be a CPython bug, Guido says:
    #   I usually append some extra newlines before passing a string to compile(). That's the usual work-around. 
    #   There's probably a subtle bug in the tokenizer when reading from a string -- if you find it, 
    #   please upload a patch to the tracker!
    # http://mail.python.org/pipermail/python-dev/2009-May/089793.html
    AssertError(SyntaxError, compile, "def f(a):\n\treturn a\n\t", "", "single")

AssertError(SyntaxError, compile, "def f(a):\n\treturn a\n\t", "", "single", 0x200)
# should work
s = "def f():\n\treturn 3"
compile(s, "<string>", "single")

AssertError(SyntaxError, compile, s, "<string>", "single", 0x200)


# Assignment to None and constant

def NoneAssign():
    exec 'None = 2'
def LiteralAssign():
    exec "'2' = '3'"

AssertError(SyntaxError, NoneAssign)
AssertError(SyntaxError, LiteralAssign)

# beginning of the file handling

c = compile("     # some comment here   \nprint 10", "", "exec")
c = compile("    \n# some comment\n     \nprint 10", "", "exec")

AssertError(SyntaxError, compile, "    x = 10\n\n", "", "exec")
AssertError(SyntaxError, compile, "    \n   #comment\n   x = 10\n\n", "", "exec")

if sys.platform == 'cli':
    c = compile(u"\u0391 = 10\nif \u0391 != 10: 1/0", "", "exec")
    exec c

# from __future__ tests
AssertError(SyntaxError, compile, "def f():\n    from __future__ import division", "", "exec")
AssertError(SyntaxError, compile, "'doc'\n'doc2'\nfrom __future__ import division", "", "exec")

# del x
AssertError(SyntaxError, compile, "def f():\n    del x\n    def g():\n        return x\n", "", "exec")
AssertError(SyntaxError, compile, "def f():\n    def g():\n        return x\n    del x\n", "", "exec")
AssertError(SyntaxError, compile, "def f():\n    class g:\n        def h(self):\n            print x\n        pass\n    del x\n", "", "exec")
# add global to the picture
c = compile("def f():\n    x=10\n    del x\n    def g():\n        global x\n        return x\n    return g\nf()()\n", "", "exec")
AssertError(NameError, eval, c)
c = compile("def f():\n    global x\n    x=10\n    del x\n    def g():\n        return x\n    return g\nf()()\n", "", "exec")
AssertError(NameError, eval, c)

# global following definition test

# affected by bug# 1145

c = compile("def f():\n    global a\n    global a\n    a = 1\n", "", "exec")

# unqualified exec in nested function
AssertError(SyntaxError, compile, "def f():\n    x = 1\n    def g():\n        exec 'pass'\n        print x", "", "exec")
# correct case - qualified exec in nested function
c = compile("def f():\n    x = 10\n    def g():\n        exec 'pass' in {}\n        print x\n", "", "exec")

# private names test

class C:
    __x = 10
    class ___:
        __y = 20
    class D:
        __z = 30

AreEqual(C._C__x, 10)
AreEqual(C.___.__y, 20)
AreEqual(C.D._D__z, 30)

class B(object):
    def method(self, __a):
        return __a

AreEqual(B().method("__a passed in"), "__a passed in")

class B(object):
    def method(self, (__a, )):
        return __a

AreEqual(B().method(("__a passed in", )), "__a passed in")

class B(object):
    def __f(self):
        pass


Assert('_B__f' in dir(B))

class B(object):
    class __C(object): pass

Assert('_B__C' in dir(B))

class B(object):
	x = lambda self, __a : __a

AreEqual(B.x(B(), _B__a='value'), 'value')


#Hit negative case of 'sublist' in http://www.python.org/doc/2.5.1/ref/grammar.txt.
AssertError(SyntaxError, compile, "def f((1)): pass", "", "exec")

#
# Make sure that augmented assignment also binds in the given scope
#

augassign_code = """
x = 10
def f():
    x %s 10
f()
"""

def test_augassign_binding():
    for op in ["+=", "-=", "**=", "*=", "//=", "/=", "%=", "<<=", ">>=", "&=", "|=", "^="]:
        code = augassign_code % op
        try:
            exec code in {}, {}
        except:
            pass
        else:
            Assert(False, "augassign binding test didn't raise exception")
    return True

Assert(test_augassign_binding())

# tests for multiline compound statements
class MyException(Exception): pass
def test_multiline_compound_stmts():
    tests = [
                "if False: print 'In IF'\nelse: x = 2; raise MyException('expected')",
                "if False: print 'In IF'\nelif True: x = 2;raise MyException('expected')\nelse: print 'In ELSE'",
                "for i in (1,2): x = i\nelse: x = 5; raise MyException('expected')",
                "while 5 in (1,2): print i\nelse:x = 2;raise MyException('expected')",
                "try: x = 2\nexcept: print 'In EXCEPT'\nelse: x=20;raise MyException('expected')",
            ]
    for test in tests:
        try:
            c = compile(test,"","exec")
            exec c
        except MyException:
            pass
        else:
            Assert(False, "multiline_compound stmt test did not raise exception. test = " + test)

test_multiline_compound_stmts()

# Generators cannot have return statements with values in them. SyntaxError is thrown in those cases.
def test_generator_with_nonempty_return():
    tests = [
        "def f():\n     return 42\n     yield 3",
        "def f():\n     yield 42\n     return 3",
        "def f():\n     yield 42\n     return None",
        "def f():\n     if True:\n          return 42\n     yield 42",
        "def f():\n     try:\n          return 42\n     finally:\n          yield 23"
        ]

    for test in tests:
        #Merlin 148614 - Change it to AssertErrorWithMessage once bug is fixed.
        AssertErrorWithPartialMessage(SyntaxError, "'return' with argument inside generator", compile, test, "", "exec")
        
    #Verify that when there is no return value error is not thrown.
    def f():
        yield 42
        return
    
test_generator_with_nonempty_return()

# compile function which returns from finally, but does not yield from finally.
c = compile("def f():\n    try:\n        pass\n    finally:\n        return 1", "", "exec")

def ret_from_finally():
    try:
        pass
    finally:
        return 1
    return 2
    
AreEqual(ret_from_finally(), 1)

def ret_from_finally2(x):
    if x:
        try:
            pass
        finally:
            return 1
    else:
        return 2

AreEqual(ret_from_finally2(True), 1)
AreEqual(ret_from_finally2(False), 2)

def ret_from_finally_x(x):
    try:
        1/0
    finally:
        return x

AreEqual(ret_from_finally_x("Hi"), "Hi")

def ret_from_finally_x2():
    try:
        1/0
    finally:
        raise AssertionError("This one")

try:
    ret_from_finally_x2()
except AssertionError, e:
    AreEqual(e.args[0], "This one")
else:
    Fail("Expected AssertionError, got none")

try:
    pass
finally:
    pass

# The try block can only have one default except clause, and it must be last

try_syntax_error_tests = [
"""
try:
    pass
except:
    pass
except Exception, e:
    pass
""",
"""
try:
    pass
except Exception, e:
    pass
except:
    pass
except:
    pass
""",
"""
try:
    pass
except:
    pass
except:
    pass
"""
]

for code in try_syntax_error_tests:
    AssertError(SyntaxError, compile, code, "code", "exec")

def test_break_in_else_clause():
    def f():
       exec ('''
       while i >= 0:
           pass
       else:
           break''')
            
    AssertError(SyntaxError, f)

#Just make sure these don't throw
print "^L"
temp = 7
print temp

print "No ^L's..."

# keep this at the end of the file, do not insert anything below this line

def endoffile():
    return "Hi" # and some comment here

def test_syntaxerror_text():       
    method_missing_colon = ("    def MethodTwo(self)\n", """
class HasASyntaxException:
    def MethodOne(self):
        print 'hello'
        print 'world'
        print 'again'
    def MethodTwo(self)
        print 'world'""")

    if is_cpython: #http://ironpython.codeplex.com/workitem/28380
        function_missing_colon1 = ("def f()\n", "def f()")
    else:        
        function_missing_colon1 = ("def f()", "def f()")
    function_missing_colon2 = ("def f()\n", "def f()\n")
    if is_cpython: #http://ironpython.codeplex.com/workitem/28380
        function_missing_colon3 = ("def f()\n", "def f()\r\n")
        function_missing_colon4 = ("def f()\n", "def f()\r")
    else:
        function_missing_colon3 = ("def f()\r\n", "def f()\r\n")
        function_missing_colon4 = ("def f()\r", "def f()\r")
        
        
    function_missing_colon2a = ("def f()\n", "print 1\ndef f()\nprint 3")
    if is_cpython: #http://ironpython.codeplex.com/workitem/28380
        function_missing_colon3a = ("def f()\n", "print 1\ndef f()\r\nprint 3")
        function_missing_colon4a = ("def f()\n", "print 1\ndef f()\rprint 3")
    else:
        function_missing_colon3a = ("def f()\r\n", "print 1\ndef f()\r\nprint 3")
        function_missing_colon4a = ("def f()\rprint 3", "print 1\ndef f()\rprint 3")
    
    tests = (
        method_missing_colon,
        #function_missing_body,
        function_missing_colon1,
        function_missing_colon2,
        function_missing_colon3,
        function_missing_colon4,

        function_missing_colon2a,
        function_missing_colon3a,
        function_missing_colon4a,
    )
    
    for expectedText, testCase in tests:            
        try:
            exec testCase
        except SyntaxError, e:
            AreEqual(e.text, expectedText)

def test_error_parameters():
    tests = [#("if 1:", 0x200, ('unexpected EOF while parsing', ('dummy', 1, 5, 'if 1:')) ),
             ("if 1:\n", 0x200, ('unexpected EOF while parsing', ('dummy', 1, 6, 'if 1:\n')) ),
             #("if 1:", 0x000, ('unexpected EOF while parsing', ('dummy', 1, 5, 'if 1:')) ),
             ("if 1:\n", 0x000, ('unexpected EOF while parsing', ('dummy', 1, 6, 'if 1:\n')) ),
             ("if 1:\n\n", 0x200, ('expected an indented block', ('dummy', 2, 1, '\n')) ),
             ("if 1:\n\n", 0x000, ('expected an indented block', ('dummy', 2, 1, '\n')) ),
             #("if 1:\n  if 1:", 0x200, ('expected an indented block', ('dummy', 2, 7, '  if 1:')) ),
             ("if 1:\n  if 1:\n", 0x200, ('expected an indented block', ('dummy', 2, 8, '  if 1:\n')) ),
             #("if 1:\n  if 1:", 0x000, ('expected an indented block', ('dummy', 2, 7, '  if 1:')) ),
             ("if 1:\n  if 1:\n", 0x000, ('expected an indented block', ('dummy', 2, 8, '  if 1:\n')) ),
             ("if 1:\n  if 1:\n\n", 0x200, ('expected an indented block', ('dummy', 3, 1, '\n')) ),
             ("if 1:\n  if 1:\n\n", 0x000, ('expected an indented block', ('dummy', 3, 1, '\n')) ),
             ("class MyClass(object):\n\tabc = 42\n\tdef __new__(cls):\n", 0x200, ('expected an indented block', ('dummy', 3, 19, '\tdef __new__(cls):\n')) ),
             ("class MyClass(object):\n\tabc = 42\n\tdef __new__(cls):\n", 0x000, ('expected an indented block', ('dummy', 3, 19, '\tdef __new__(cls):\n')) ),
             ("def  Foo():\n\n    # comment\n\n    Something = -1\n\n\n\n  ", 0x000, ('unindent does not match any outer indentation level', ('dummy', 9, 2, '  '))),
             ("def  Foo():\n\n    # comment\n\n    Something = -1\n\n\n\n  ", 0x200, ('unindent does not match any outer indentation level', ('dummy', 9, 2, '  '))),
             ("def  Foo():\n\n    # comment\n\n    Something = -1\n\n\n\n   ", 0x000, ('unindent does not match any outer indentation level', ('dummy', 9, 3, '   '))),
             ("def  Foo():\n\n    # comment\n\n    Something = -1\n\n\n\n   ", 0x200, ('unindent does not match any outer indentation level', ('dummy', 9, 3, '   '))),
             ]
    
    for input, flags, res in tests:
        #print repr(input), flags
        try:
            code3 = compile(input, "dummy", "single", flags, 1)
            AssertUnreachable()
        except SyntaxError, err:
            AreEqual(err.args, res)
        

try:
    exec("""
    def f():
        x = 3
            y = 5""")
    AssertUnreachable()
except IndentationError, e:
    AreEqual(e.lineno, 2)

@skip("win32", "silverlight") # no encoding.Default
def test_parser_recovery():
    # bunch of test infrastructure...
    
    import clr
    clr.AddReference('IronPython')
    clr.AddReference('Microsoft.Scripting')
    clr.AddReference('Microsoft.Dynamic')
    
    from Microsoft.Scripting import (
        TextContentProvider, SourceCodeKind, SourceUnit, ErrorSink,  
        SourceCodeReader
        )
    from Microsoft.Scripting.Runtime import CompilerContext

    from IronPython import PythonOptions
    from IronPython.Compiler import Parser, Tokenizer, PythonCompilerOptions, Ast
    from System.IO import StringReader
    from System.Text import Encoding

    class MyErrorSink(ErrorSink):    
        def __init__(self):
            self.Errors = []
        
        def Add(self, *args):
            if type(args[0]) is str:
                self.AddWithPath(*args)
            else:
                self.AddWithSourceUnit(*args)
        
        def AddWithPath(self, message, path, code, line, span, error, severity):
            err = (
                message,
                path,
                span,
                error
            )
            self.Errors.append(err)
        
        def AddWithSourceUnit(self, source, message, span, errorCode, severity):
            err = (
                message, 
                source.Path,
                span,
                errorCode
            )
            
            self.Errors.append(err)
    
    class MyTextContentProvider(TextContentProvider):
        def __init__(self, text):
            self.text = text
        def GetReader(self):
            return SourceCodeReader(StringReader(self.text), Encoding.Default)

    def parse_text(text):
        errorSink = MyErrorSink()
        sourceUnit = SourceUnit(
                clr.GetCurrentRuntime().GetLanguageByName('python'),
                MyTextContentProvider(text),
                'foo',
                SourceCodeKind.File
            )
           
        parser = Parser.CreateParser(
            CompilerContext(sourceUnit, PythonCompilerOptions(), errorSink),
            PythonOptions()
        )
        parser.ParseFile(True)
        return errorSink

    def TestErrors(text, errors):
        res = parse_text(text)
        AreEqual(len(res.Errors), len(errors))
        for curErr, expectedMsg in zip(res.Errors, errors):
            AreEqual(curErr[0], expectedMsg)
    
    def PrintErrors(text):
        """helper for creating new tests"""
        errors = parse_text(text)
        print
        for err in errors.Errors:
            print err
    
    TestErrors("""class 

def x(self):
    pass""", ["unexpected token '<newline>'"])

    
    TestErrors("""class x

def x(self):
    pass
""", ["unexpected token '<newline>'"])

    TestErrors("""class x(

def x(self):
    pass""", ["unexpected token 'def'"])

    TestErrors("""class X:
	if x:

	def x(): pass""", ['expected an indented block'])

    TestErrors("""class X:
	if x is None:
		x = 

	def x(self): pass""", ["unexpected token '<newline>'"])

    TestErrors("""class X:
    
    def f(
    
    def g(self): pass""", ["unexpected token 'def'"])

    TestErrors("""class X:
    
    def f(*
    
    def g(self): pass""", ["unexpected token 'def'"])

    TestErrors("""class X:
    
    def f(**
    
    def g(self): pass""", ["unexpected token 'def'"])

    TestErrors("""class X:
    
    def f(*a, **
    
    def g(self): pass""", ["unexpected token 'def'"])
    
    TestErrors("""f() += 1""", ["illegal expression for augmented assignment"])

def test_syntax_warnings():
    # syntax error warnings are outputted using warnings.showwarning.  our own warning trapper therefore
    # doesn't see them.  So we trap stderr here instead.  We could use CPython's warning trapper if we
    # checked for the presence of the stdlib.
    with stderr_trapper() as trapper:
        compile("def f():\n    a = 1\n    global a\n", "", "exec")
    AreEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is assigned to before global declaration"])

    with stderr_trapper() as trapper:
        compile("def f():\n    def a(): pass\n    global a\n", "", "exec")
    AreEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is assigned to before global declaration"])

    with stderr_trapper() as trapper:   
        compile("def f():\n    for a in []: pass\n    global a\n", "", "exec")
    AreEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is assigned to before global declaration"])

    with stderr_trapper() as trapper:   
        compile("def f():\n    global a\n    a = 1\n    global a\n", "", "exec")
    AreEqual(trapper.messages, [":4: SyntaxWarning: name 'a' is assigned to before global declaration"])

    with stderr_trapper() as trapper:   
        compile("def f():\n    print a\n    global a\n", "", "exec")
    AreEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is used prior to global declaration"])

    with stderr_trapper() as trapper:   
        compile("def f():\n    a = 1\n    global a\n    global a\n    a = 1", "", "exec")
    AreEqual(trapper.messages, 
             [":3: SyntaxWarning: name 'a' is assigned to before global declaration", 
              ":4: SyntaxWarning: name 'a' is assigned to before global declaration"])

    with stderr_trapper() as trapper:   
        compile("x = 10\nglobal x\n", "", "exec")
    AreEqual(trapper.messages, [":2: SyntaxWarning: name 'x' is assigned to before global declaration"])

#--MAIN------------------------------------------------------------------------
run_test(__name__)
