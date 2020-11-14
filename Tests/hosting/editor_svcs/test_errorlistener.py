# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Testing ErrorListener
##

import unittest

from iptest import IronPythonTestCase, skipUnlessIronPython, is_cli, run_test

if is_cli:
    import clr
    clr.AddReference("Microsoft.Scripting")

@skipUnlessIronPython()
class ErrorListenerTest(IronPythonTestCase):
    def setUp(self):
        super(ErrorListenerTest, self).setUp()
        self.load_iron_python_test()

        from Microsoft.Scripting import Severity, SourceLocation
        from IronPython.Hosting import Python
        self.engine = Python.CreateEngine()

        self.From, self.To = SourceLocation, SourceLocation
        self.Warning, self.Error, self.FatalError = Severity.Warning, Severity.Error, Severity.FatalError

    from Microsoft.Scripting.Hosting import ErrorListener
    class MyErrorListener(ErrorListener):
        def __init__(self):
            self.__errors= []

        errors = property(lambda obj: obj.__errors)

        def ErrorReported(self, src, msg, span, errorCode, severity):
            line = src.GetCodeLine(span.Start.Line)
            if line \
                and span.Start.Line == span.End.Line \
                and span.Start.Column != span.End.Column:
                bad = line[span.Start.Column - 1 : span.End.Column - 1]
            else:
                bad = (span.Start.Line, span.Start.Column)
            self.__errors.append((msg, bad, errorCode, severity))

    def compile_expression(self, expression):
        from Microsoft.Scripting import SourceCodeKind
        source = self.engine.CreateScriptSourceFromString(expression, SourceCodeKind.Expression)
        return self.compile_source(source)

    def compile_file(self, stmts):
        from Microsoft.Scripting import SourceCodeKind
        source = self.engine.CreateScriptSourceFromString(stmts, SourceCodeKind.File)
        return self.compile_source(source)

    def compile_source(self, source):
        import System
        errorlistener = ErrorListenerTest.MyErrorListener()
        try:
            source.Compile(errorlistener)
        except System.Exception as e:
            pass
        return errorlistener.errors

    def test_no_errors(self):
        self.assertEqual([], self.compile_expression("1+1"))

    def test_empty(self):
        self.assertEqual([], self.compile_file(""))
        expected = [
            ("unexpected EOF while parsing", (1,1), 17, self.FatalError),
        ]
        actual = self.compile_expression("")
        self.assertEqual(expected, actual)

    def test_unexpected_token(self):
        expected = [
            ("invalid syntax", "foo", 16, self.FatalError)
        ]
        actual = self.compile_expression("1.foo")
        self.assertEqual(expected, actual)

    def test_multiple_errors(self):
        expected = [
            ("invalid syntax", "assert", 16, self.FatalError),
            ("EOL while scanning string literal", '"hello', 16, self.FatalError),
            ("invalid syntax", "assert", 16, self.FatalError),
        ]
        actual = self.compile_expression("""assert "hello""")
        self.assertEqual(expected, actual)

    def test_not_indented_class(self):
        expected = [
            ("expected an indented block", "pass", 32, self.FatalError),
        ]
        code = """\
class Foo:
pass"""
        self.assertEqual(expected, self.compile_file(code))

    def test_bad_indentation(self):
        expected = [
            ("unindent does not match any outer indentation level", ' ', 32, self.FatalError),
        ]
        code = """\
class Foo:
  pass
 pass"""
        self.assertEqual(expected, self.compile_file(code))

    def test_non_fatal_error(self):
        expected = [
            ("'break' outside loop", "break", 16, self.FatalError),
        ]
        code = """\
1+1
break"""
        self.assertEqual(expected, self.compile_file(code))

    def test_assignment_to_none(self):
        expected = [
            ("can't assign to keyword", "None", 80, self.FatalError),
        ]
        actual = self.compile_file("None = 42")
        self.assertEqual(expected, actual)

    def test_multiple_erroneous_statements(self):
        expected = [
            ("can't assign to keyword", "None", 80, self.FatalError),
            ("can't assign to keyword", "None", 80, self.FatalError),
        ]
        code = """\
None = 2
None = 3"""
        self.assertEqual(expected, self.compile_file(code))

    def test_warning(self):
        expected = [
            ("name 'a' is assigned to before global declaration", "global a", -1, self.FatalError), # reports as error since 3.6
        ]
        code = """\
def foo():
    a=2
    global a"""
        self.assertEqual(expected, self.compile_file(code))

    def test_should_report_multiple_warnings_negative(self):
        "Bug #17541, http://www.codeplex.com/IronPython/WorkItem/View.aspx?WorkItemId=17541"
        expected = [
            ("Variable a assigned before global declaration", "global a", -1, self.Warning),
            ("Variable b assigned before global declaration", "global b", -1, self.Warning),
        ]
        code = """\
def foo():
    a=2
    global a

def bar():
    b=2
    global b"""
        self.assertNotEqual(expected, self.compile_file(code))

    def test_should_report_both_errors_and_warnings_negative(self):
        "Bug #17541, http://www.codeplex.com/IronPython/WorkItem/View.aspx?WorkItemId=17541"
        expected = [
            ("can't assign to keyword", "None", -1, self.Error),
            ("Variable a assigned before global declaration", "global a", -1, self.Warning),
        ]
        code = """\
None = 2
def foo():
    a=2
    global a"""
        self.assertNotEqual(expected, self.compile_file(code))

    def test_all_together(self):
        expected = [
            ("can't assign to keyword", "None", 80, self.FatalError),
        ]
        code = """\
None = 2
dict={foo}
def foo():
    a=2
    global a"""
        self.assertEqual(expected, self.compile_file(code))

run_test(__name__)
