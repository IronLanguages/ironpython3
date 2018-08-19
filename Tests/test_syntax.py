# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_cpython, run_test, skipUnlessIronPython, stderr_trapper

def run_compile_test(self, code, msg, lineno):
    filename = "the file name"
    try:
        compile(code, filename, "exec")
    except SyntaxError as e:
        self.assertEqual(e.msg, msg)
        self.assertEqual(e.lineno, lineno)
        self.assertEqual(e.filename, filename)
    else:
        self.assertUnreachable("Expected exception, got none")

def test_compile(self):

    c = compile("0o71 + 1", "Error", "eval")

    self.assertRaises(SyntaxError, compile, "0o88 + 1", "Error", "eval")
    self.assertRaises(SyntaxError, compile, "0o99 + 1", "Error", "eval")
    self.assertRaises(SyntaxError, compile, """
    try:
        pass
    """, "Error", "single")

    self.assertRaises(SyntaxError, compile, "x=10\ny=x.", "Error", "exec")

    compile_tests = [
        ("for x notin []:\n    pass", "unexpected token 'notin'" if is_cli else "invalid syntax", 1),
        ("global 1", "unexpected token '1'" if is_cli else "invalid syntax", 1),
        ("x=10\nyield x\n", "'yield' outside function", 2),
        ("return\n", "'return' outside function", 1),
        ("print >> 1 ,\n", "unexpected token '<newline>'" if is_cli else "invalid syntax", 1),
        ("def f(x=10, y):\n    pass", "non-default argument follows default argument", 1),
        ("def f(for):\n    pass", "unexpected token 'for'" if is_cli else "invalid syntax", 1),
        ("f(3 = )", "expected name" if is_cli else "invalid syntax", 1),
        ("dict(a=1,a=2)", "keyword argument repeated", 1),
        ("def f(a,a): pass", "duplicate argument 'a' in function definition", 1),
        ("def f((a,b),(c,b)): pass", "duplicate argument 'b' in function definition", 1),
        ("x = 10\nx = x[]", "unexpected token ']'" if is_cli else "invalid syntax", 2),
        ("break", "'break' outside loop", 1),
        ("if 1:\n\tbreak", "'break' outside loop", 2),
        ("if 1:\n\tx+y=22", "can't assign to operator", 2),
        ("if 1:\n\tdel f()", "can't delete function call", 2),
        ("if 1:\nfoo()\n", "expected an indented block", 2),
        ("'abc'.1", "invalid syntax", 1),
        ("'abc'.1L", "invalid syntax", 1),
        ("'abc'.1j", "invalid syntax", 1),
        ("'abc'.0xFFFF", "invalid syntax", 1),
        ("'abc' 1L", "invalid syntax", 1),
        ("'abc' 1.0", "invalid syntax", 1),
        ("'abc' 0j", "invalid syntax", 1),
        ("x = 'abc'\nx.1", "invalid syntax", 2),
        ("x = 'abc'\nx 1L", "invalid syntax", 2),
        ("x = 'abc'\nx 1.0", "invalid syntax", 2),
        ("x = 'abc'\nx 0j", "invalid syntax", 2),
        ('def f():\n    del (yield 5)\n', "can't delete yield expression", 2),
        ('a,b,c += 1,2,3', "illegal expression for augmented assignment", 1),
        ('def f():\n    a = yield 3 = yield 4', "can't assign to yield expression" if is_cli else "assignment to yield expression not possible", 2),
        ('((yield a), 2,3) = (2,3,4)', "can't assign to yield expression", 1),
        ('(2,3) = (3,4)', "can't assign to literal", 1),
        ("def e():\n    break", "'break' outside loop", 2),
        ("def g():\n    for x in range(10):\n        print(x)\n    break\n", "'break' outside loop", 4),
        ("def g():\n    for x in range(10):\n        print(x)\n    if True:\n        break\n", "'break' outside loop", 5),
        ("def z():\n    if True:\n        break\n", "'break' outside loop", 3),
        ('from import abc', "invalid syntax", 1),
        ('() = 1', "can't assign to ()", 1),
        ("""for x in range(100):\n"""
        """    try:\n"""
        """        [1,2][3]\n"""
        """    except IndexError:\n"""
        """        pass\n"""
        """    finally:\n"""
        """        continue\n""", "'continue' not supported inside 'finally' clause", 7),
        ("'abc'.", "syntax error" if is_cli else "invalid syntax", 1),
        ("None = 2", "cannot assign to None", 1),
    ]

    if is_cli:
        # CPython does no have a filename and line number
        compile_tests.append(("def a(x):\n    def b():\n        print(x)\n    del x", "can not delete variable 'x' referenced in nested scope", 2))

    # different error messages, ok
    for test in compile_tests:
        run_compile_test(self, *test)

    self.assertEqual(float(repr(2.5)), 2.5)

    self.assertEqual(eval("1, 2, 3,"), (1, 2, 3))

    # eval validates end of input
    self.assertRaises(SyntaxError, compile, "1+2 1", "Error", "eval")

    # empty test list in for expression
    self.assertRaises(SyntaxError, compile, "for x in : print(x)", "Error", "exec")
    self.assertRaises(SyntaxError, compile, "for x in : print(x)", "Error", "eval")
    self.assertRaises(SyntaxError, compile, "for x in : print(x)", "Error", "single")

    # empty backquote
    self.assertRaises(SyntaxError, compile, "``", "Error", "exec")
    self.assertRaises(SyntaxError, compile, "``", "Error", "eval")
    self.assertRaises(SyntaxError, compile, "``", "Error", "single")

    # empty assignment expressions
    self.assertRaises(SyntaxError, compile, "x = ", "Error", "exec")
    self.assertRaises(SyntaxError, compile, "x = ", "Error", "eval")
    self.assertRaises(SyntaxError, compile, "x = ", "Error", "single")
    self.assertRaises(SyntaxError, compile, "x = y = ", "Error", "exec")
    self.assertRaises(SyntaxError, compile, "x = y = ", "Error", "eval")
    self.assertRaises(SyntaxError, compile, "x = y = ", "Error", "single")
    self.assertRaises(SyntaxError, compile, " = ", "Error", "exec")
    self.assertRaises(SyntaxError, compile, " = ", "Error", "eval")
    self.assertRaises(SyntaxError, compile, " = ", "Error", "single")
    self.assertRaises(SyntaxError, compile, " = 4", "Error", "exec")
    self.assertRaises(SyntaxError, compile, " = 4", "Error", "eval")
    self.assertRaises(SyntaxError, compile, " = 4", "Error", "single")
    self.assertRaises(SyntaxError, compile, "x <= ", "Error", "exec")
    self.assertRaises(SyntaxError, compile, "x <= ", "Error", "eval")
    self.assertRaises(SyntaxError, compile, "x <= ", "Error", "single")
    #indentation errors - BUG 864
    self.assertRaises(IndentationError, compile, "class C:\nx=2\n", "Error", "exec")
    self.assertRaises(IndentationError, compile, "class C:\n\n", "Error", "single")

    #allow \f
    compile('\f\f\f\f\fclass C:\f\f\f pass', 'ok', 'exec')
    compile('\f\f\f\f\fclass C:\n\f\f\f    print("hello")\n\f\f\f\f\f\f\f\f\f\f    print("goodbye")', 'ok', 'exec')
    compile('class C:\n\f\f\f    print("hello")\n\f\f\f\f\f\f\f\f\f\f    print("goodbye")', 'ok', 'exec')
    compile('class \f\f\f\fC:\n\f    print("hello")\n\f\f\f\f\f\f\f\f\f\f    print("goodbye")', 'ok', 'exec')

    # multiline expression passed to exec (positive test)
    s = """
title = "The Cat"
self.assertTrue(title.istitle())
x = 2 + 5
self.assertEqual(x, 7)
    """
    exec(s)

    if is_cpython:
        # this seems to be a CPython bug, Guido says:
        #   I usually append some extra newlines before passing a string to compile(). That's the usual work-around.
        #   There's probably a subtle bug in the tokenizer when reading from a string -- if you find it,
        #   please upload a patch to the tracker!
        # http://mail.python.org/pipermail/python-dev/2009-May/089793.html
        self.assertRaises(SyntaxError, compile, "def f(a):\n\treturn a\n\t", "", "single")

    self.assertRaises(SyntaxError, compile, "def f(a):\n\treturn a\n\t", "", "single", 0x200)
    # should work
    s = "def f():\n\treturn 3"
    compile(s, "<string>", "single")

    self.assertRaises(SyntaxError, compile, s, "<string>", "single", 0x200)


    # Assignment to None and constant

    def NoneAssign():
        exec('None = 2')
    def LiteralAssign():
        exec("'2' = '3'")

    self.assertRaises(SyntaxError, NoneAssign)
    self.assertRaises(SyntaxError, LiteralAssign)

    # beginning of the file handling

    c = compile("     # some comment here   \nprint(10)", "", "exec")
    c = compile("    \n# some comment\n     \nprint(10)", "", "exec")

    self.assertRaises(SyntaxError, compile, "    x = 10\n\n", "", "exec")
    self.assertRaises(SyntaxError, compile, "    \n   #comment\n   x = 10\n\n", "", "exec")

    if is_cli:
        c = compile(u"\u0391 = 10\nif \u0391 != 10: 1/0", "", "exec")
        exec(c)

    # from __future__ tests
    self.assertRaises(SyntaxError, compile, "def f():\n    from __future__ import division", "", "exec")
    self.assertRaises(SyntaxError, compile, "'doc'\n'doc2'\nfrom __future__ import division", "", "exec")

    # del x
    self.assertRaises(SyntaxError, compile, "def f():\n    del x\n    def g():\n        return x\n", "", "exec")
    self.assertRaises(SyntaxError, compile, "def f():\n    def g():\n        return x\n    del x\n", "", "exec")
    self.assertRaises(SyntaxError, compile, "def f():\n    class g:\n        def h(self):\n            print(x)\n        pass\n    del x\n", "", "exec")
    # add global to the picture
    c = compile("def f():\n    x=10\n    del x\n    def g():\n        global x\n        return x\n    return g\nf()()\n", "", "exec")
    self.assertRaises(NameError, eval, c)
    c = compile("def f():\n    global x\n    x=10\n    del x\n    def g():\n        return x\n    return g\nf()()\n", "", "exec")
    self.assertRaises(NameError, eval, c)

    # global following definition test

    # affected by bug# 1145

    c = compile("def f():\n    global a\n    global a\n    a = 1\n", "", "exec")

    # unqualified exec in nested function
    self.assertRaises(SyntaxError, compile, "def f():\n    x = 1\n    def g():\n        exec('pass')\n        print(x)", "", "exec")
    # correct case - qualified exec in nested function
    c = compile("def f():\n    x = 10\n    def g():\n        exec('pass') in {}\n        print(x)\n", "", "exec")

def test_expr_support_impl(self):
    x = 10
    self.assertEqual(x, 10)
    del x
    try: y = x
    except NameError: pass
    else: self.fail("x not deleted")

    (x) = 20
    self.assertEqual((x), 20)
    del (x)
    try: y = x
    except NameError: pass
    else: self.fail("x not deleted")

    # this is comment \
    a=10
    self.assertEqual(a, 10)

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

    self.assertEqual(x, y)

    self.assertEqual("\101", "A")
    x=b'\a\b\c\d\e\f\g\h\i\j\k\l\m\n\o\p\q\r\s\t\u\v\w\y\z'
    y=u'\u0007\u0008\\\u0063\\\u0064\\\u0065\u000C\\\u0067\\\u0068\\\u0069\\\u006a\\\u006b\\\u006c\\\u006d\u000A\\\u006f\\\u0070\\\u0071\u000D\\\u0073\u0009\\\u0075\u000B\\\u0077\\\u0079\\\u007a'

    self.assertTrue(x == y)
    self.assertEqual(x, y)

    for a,b in zip(x,y):
        self.assertEqual(a,b)

    self.assertTrue((10==20)==(20==10))
    self.assertEqual(10==20, 20==10)
    self.assertEqual(4e4-4, 4e4 - 4)

def test_private_names(self):
    class C:
        __x = 10
        class ___:
            __y = 20
        class D:
            __z = 30

    self.assertEqual(C._C__x, 10)
    self.assertEqual(C.___.__y, 20)
    self.assertEqual(C.D._D__z, 30)

    class B(object):
        def method(self, __a):
            return __a

    self.assertEqual(B().method("__a passed in"), "__a passed in")

    class B(object):
        def method(self, (__a, )):
            return __a

    self.assertEqual(B().method(("__a passed in", )), "__a passed in")

    class B(object):
        def __f(self):
            pass


    self.assertTrue('_B__f' in dir(B))

    class B(object):
        class __C(object): pass

    self.assertTrue('_B__C' in dir(B))

    class B(object):
        x = lambda self, __a : __a

    self.assertEqual(B.x(B(), _B__a='value'), 'value')


    #Hit negative case of 'sublist' in http://www.python.org/doc/2.5.1/ref/grammar.txt.
    self.assertRaises(SyntaxError, compile, "def f((1)): pass", "", "exec")

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
                exec(code, {}, {})
            except:
                pass
            else:
                Assert(False, "augassign binding test didn't raise exception")
        return True

    self.assertTrue(test_augassign_binding())

class SyntaxTest(IronPythonTestCase):

    def test_compile_method(self):
        test_compile(self)

    def test_expr_support_method(self):
        test_expr_support_impl(self)

    def test_private_names_method(self):
        test_private_names(self)

    def test_date_check(self):
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

    def test_multiline_compound_stmts(self):
        class MyException(Exception): pass
        tests = [
                    "if False: print('In IF')\nelse: x = 2; raise MyException('expected')",
                    "if False: print('In IF')\nelif True: x = 2;raise MyException('expected')\nelse: print('In ELSE')",
                    "for i in (1,2): x = i\nelse: x = 5; raise MyException('expected')",
                    "while 5 in (1,2): print(i)\nelse:x = 2;raise MyException('expected')",
                    "try: x = 2\nexcept: print('In EXCEPT')\nelse: x=20;raise MyException('expected')",
                ]
        for test in tests:
            try:
                c = compile(test,"","exec")
                exec(c)
            except MyException:
                pass
            else:
                self.fail("multiline_compound stmt test did not raise exception. test = " + test)

    # Generators cannot have return statements with values in them. SyntaxError is thrown in those cases.
    def test_generator_with_nonempty_return(self):
        tests = [
            "def f():\n     return 42\n     yield 3",
            "def f():\n     yield 42\n     return 3",
            "def f():\n     yield 42\n     return None",
            "def f():\n     if True:\n          return 42\n     yield 42",
            "def f():\n     try:\n          return 42\n     finally:\n          yield 23"
            ]

        for test in tests:
            self.assertRaisesMessage(SyntaxError, "'return' with argument inside generator", compile, test, "", "exec")

        #Verify that when there is no return value error is not thrown.
        def f():
            yield 42
            return

    def test_return_from_finally(self):
        # compile function which returns from finally, but does not yield from finally.
        c = compile("def f():\n    try:\n        pass\n    finally:\n        return 1", "", "exec")

        def ret_from_finally():
            try:
                pass
            finally:
                return 1
            return 2

        self.assertEqual(ret_from_finally(), 1)

        def ret_from_finally2(x):
            if x:
                try:
                    pass
                finally:
                    return 1
            else:
                return 2

        self.assertEqual(ret_from_finally2(True), 1)
        self.assertEqual(ret_from_finally2(False), 2)

        def ret_from_finally_x(x):
            try:
                1/0
            finally:
                return x

        self.assertEqual(ret_from_finally_x("Hi"), "Hi")

        def ret_from_finally_x2():
            try:
                1/0
            finally:
                raise AssertionError("This one")

        try:
            ret_from_finally_x2()
        except AssertionError as e:
            self.assertEqual(e.args[0], "This one")
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
            self.assertRaises(SyntaxError, compile, code, "code", "exec")

    def test_break_in_else_clause(self):
        def f():
            exec('''
            while i >= 0:
                pass
            else:
                break''')

        self.assertRaises(SyntaxError, f)

    def test_no_throw(self):
        #Just make sure these don't throw
        print("^L")
        temp = 7
        print(temp)

        print("No ^L's...")

    def test_syntaxerror_text(self):
        method_missing_colon = ("    def MethodTwo(self)\n", """
class HasASyntaxException:
    def MethodOne(self):
        print('hello')
        print('world')
        print('again')
    def MethodTwo(self)
        print('world')""")

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


        function_missing_colon2a = ("def f()\n", "print(1)\ndef f()\nprint(3)")
        if is_cpython: #http://ironpython.codeplex.com/workitem/28380
            function_missing_colon3a = ("def f()\n", "print(1)\ndef f()\r\nprint(3)")
            function_missing_colon4a = ("def f()\n", "print(1)\ndef f()\rprint(3)")
        else:
            function_missing_colon3a = ("def f()\r\n", "print(1)\ndef f()\r\nprint(3)")
            function_missing_colon4a = ("def f()\rprint(3)", "print(1)\ndef f()\rprint(3)")

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
                exec(testCase)
            except SyntaxError as e:
                self.assertEqual(e.text, expectedText)

    def test_error_parameters(self):
        tests = [("if 1:", 0x200, ('unexpected EOF while parsing', ('dummy', 1, 6 if is_cli else 5, 'if 1:')) ),
                ("if 1:\n", 0x200, ('unexpected EOF while parsing', ('dummy', 1, 6, 'if 1:\n')) ),
                ("if 1:", 0x000, ('unexpected EOF while parsing', ('dummy', 1, 6 if is_cli else 5, 'if 1:')) ),
                ("if 1:\n", 0x000, ('unexpected EOF while parsing', ('dummy', 1, 6, 'if 1:\n')) ),
                ("if 1:\n\n", 0x200, ('expected an indented block', ('dummy', 2, 1, '\n')) ),
                ("if 1:\n\n", 0x000, ('expected an indented block', ('dummy', 2, 1, '\n')) ),
                ("if 1:\n  if 1:", 0x200, ('unexpected EOF while parsing' if is_cli else 'expected an indented block', ('dummy', 2, 8 if is_cli else 7, '  if 1:')) ),
                ("if 1:\n  if 1:\n", 0x200, ('expected an indented block', ('dummy', 2, 8, '  if 1:\n')) ),
                ("if 1:\n  if 1:", 0x000, ('expected an indented block', ('dummy', 2, 8 if is_cli else 7, '  if 1:')) ),
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
            except SyntaxError as err:
                self.assertEqual(err.args, res)


        try:
            exec("""
            def f():
                x = 3
                    y = 5""")
            AssertUnreachable()
        except IndentationError as e:
            self.assertEqual(e.lineno, 2)

    @skipUnlessIronPython()
    def test_parser_recovery(self):
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
                return SourceCodeReader(StringReader(self.text), Encoding.GetEncoding(0))

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
            self.assertEqual(len(res.Errors), len(errors))
            for curErr, expectedMsg in zip(res.Errors, errors):
                self.assertEqual(curErr[0], expectedMsg)

        def PrintErrors(text):
            """helper for creating new tests"""
            errors = parse_text(text)
            print
            for err in errors.Errors:
                print(err)

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

        TestErrors("""f() += 1""", ["can't assign to function call"])

    def test_syntax_warnings(self):
        # syntax error warnings are outputted using warnings.showwarning.  our own warning trapper therefore
        # doesn't see them.  So we trap stderr here instead.  We could use CPython's warning trapper if we
        # checked for the presence of the stdlib.
        with stderr_trapper() as trapper:
            compile("def f():\n    a = 1\n    global a\n", "", "exec")
        self.assertEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is assigned to before global declaration"])

        with stderr_trapper() as trapper:
            compile("def f():\n    def a(): pass\n    global a\n", "", "exec")
        self.assertEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is assigned to before global declaration"])

        with stderr_trapper() as trapper:
            compile("def f():\n    for a in []: pass\n    global a\n", "", "exec")
        self.assertEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is assigned to before global declaration"])

        with stderr_trapper() as trapper:
            compile("def f():\n    global a\n    a = 1\n    global a\n", "", "exec")
        self.assertEqual(trapper.messages, [":4: SyntaxWarning: name 'a' is assigned to before global declaration"])

        with stderr_trapper() as trapper:
            compile("def f():\n    print(a)\n    global a\n", "", "exec")
        self.assertEqual(trapper.messages, [":3: SyntaxWarning: name 'a' is used prior to global declaration"])

        with stderr_trapper() as trapper:
            compile("def f():\n    a = 1\n    global a\n    global a\n    a = 1", "", "exec")
        self.assertEqual(trapper.messages,
                [":3: SyntaxWarning: name 'a' is assigned to before global declaration",
                ":4: SyntaxWarning: name 'a' is assigned to before global declaration"])

        with stderr_trapper() as trapper:
            compile("x = 10\nglobal x\n", "", "exec")
        self.assertEqual(trapper.messages, [":2: SyntaxWarning: name 'x' is assigned to before global declaration"])


run_test(__name__)