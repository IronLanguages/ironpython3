# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import ast
import sys
import types
import unittest
import itertools

from io import StringIO

from iptest import is_cli, run_test

def to_tuple(t):
    if t is None or isinstance(t, (str, int, complex)):
        return t
    elif isinstance(t, list):
        return [to_tuple(e) for e in t]
    result = [t.__class__.__name__]
    if hasattr(t, 'lineno') and hasattr(t, 'col_offset'):
        result.append((t.lineno, t.col_offset))
    if t._fields is None:
        return tuple(result)
    for f in t._fields:
        result.append(to_tuple(getattr(t, f)))
    return tuple(result)

# These tests are compiled through "exec"
# There should be at least one test per statement
exec_tests_stdlib = [
    # None
    "None",
    # FunctionDef
    "def f(): pass",
    # FunctionDef with arg
    "def f(a): pass",
    # FunctionDef with arg and default value
    "def f(a=0): pass",
    # FunctionDef with varargs
    "def f(*args): pass",
    # FunctionDef with kwargs
    "def f(**kwargs): pass",
    # FunctionDef with all kind of args
    "def f(a, b=1, c=None, d=[], e={}, *args, f=42, **kwargs): pass",
    # ClassDef
    "class C:pass",
    # ClassDef, new style class
    "class C(object): pass",
    # Return
    "def f():return 1",
    # Delete
    "del v",
    # Assign
    "v = 1",
    # AugAssign
    "v += 1",
    # For
    "for v in v:pass",
    # While
    "while v:pass",
    # If
    "if v:pass",
    # With
    "with x as y: pass",
    "with x as y, z as q: pass",
    # Raise
    "raise Exception('string')",
    # TryExcept
    "try:\n  pass\nexcept Exception:\n  pass",
    # TryFinally
    "try:\n  pass\nfinally:\n  pass",
    # Assert
    "assert v",
    # Import
    "import sys",
    # ImportFrom
    "from sys import v",
    # Global
    "global v",
    # Expr
    "1",
    # Pass,
    "pass",
    # Break
    # "break", doesn't work outside a loop in IronPython
    # Continue
    # "continue", doesn't work outside a loop in IronPython
    # for statements with naked tuples (see http://bugs.python.org/issue6704)
    "for a,b in c: pass",
    "[(a,b) for a,b in c]",
    "((a,b) for a,b in c)",
    "((a,b) for (a,b) in c)",
    # Multiline generator expression (test for .lineno & .col_offset)
    """(
    (
    Aa
    ,
       Bb
    )
    for
    Aa
    ,
    Bb in Cc
    )""",
    # dictcomp
    "{a : b for w in x for m in p if g}",
    # dictcomp with naked tuple
    "{a : b for v,w in x}",
    # setcomp
    "{r for l in x if g}",
    # setcomp with naked tuple
    "{r for l,m in x}",
]

# extra tests not in stdlib
exec_tests = exec_tests_stdlib + [
    # Break
    # "break", doesn't work outside a loop
    "while x: break",
    # Continue
    # "continue", doesn't work outside a loop
    "while x: continue",
    # If elif else
    "if v:pass\nelif u:pass\nelse: pass",
    # yield makes no sense outside function
    "def f(): yield 1",
    # CP35001
    "def f(): yield",
    # comment
    "#"
]

# These are compiled through "single"
# because of overlap with "eval", it just tests what
# can't be tested with "eval"
single_tests = [
    "1+2"
]

# These are compiled through "eval"
# It should test all expressions
eval_tests_stdlib = [
  # None
  "None",
  # BoolOp
  "a and b",
  # BinOp
  "a + b",
  # UnaryOp
  "not v",
  # Lambda
  "lambda:None",
  # Dict
  "{ 1:2 }",
  # Empty dict
  "{}",
  # Set
  "{None,}",
  # Multiline dict (test for .lineno & .col_offset)
  """{
      1
        :
          2
     }""",
  # ListComp
  "[a for b in c if d]",
  # GeneratorExp
  "(a for b in c if d)",
  # Yield - yield expressions can't work outside a function
  #
  # Compare
  "1 < 2 < 3",
  # Call
  "f(1,2,c=3,*d,**e)",
  # Num
  "10",
  # Str
  "'string'",
  # Attribute
  "a.b",
  # Subscript
  "a[b:c]",
  # Name
  "v",
  # List
  "[1,2,3]",
  # Empty list
  "[]",
  # Tuple
  "1,2,3",
  # Tuple
  "(1,2,3)",
  # Empty tuple
  "()",
  # Combination
  "a.b.c.d(a.b[1:2])",
]

# extra tests not in stdlib
eval_tests = eval_tests_stdlib + [
  # Lambda
  "lambda x: x",
  "lambda x: (yield x)",
  # GeneratorExp
  "(a for b in c for d in e for f in g)",
  "(a for b in c for c in d)",
  # Yield
  "((yield i) for i in range(5))",
  # ellipsis
  "a[...]",
  # index
  "a[1]",
  # set
  "{a,b,c}",
  # DictComp
  "{k:v for k,v in li}",
  # SetComp
  "{e for e in li}",
]

# TODO: expr_context, slice, boolop, operator, unaryop, cmpop, comprehension
# excepthandler, arguments, keywords, alias

class AST_Tests(unittest.TestCase):

    def _assertTrueorder(self, ast_node, parent_pos):
        if not isinstance(ast_node, ast.AST) or ast_node._fields is None:
            return
        if isinstance(ast_node, (ast.expr, ast.stmt, ast.excepthandler)):
            node_pos = (ast_node.lineno, ast_node.col_offset)
            self.assertTrue(node_pos >= parent_pos)
            parent_pos = (ast_node.lineno, ast_node.col_offset)
        for name in ast_node._fields:
            value = getattr(ast_node, name)
            if isinstance(value, list):
                for child in value:
                    self._assertTrueorder(child, parent_pos)
            elif value is not None:
                self._assertTrueorder(value, parent_pos)

    def test_compile_from_ast_001(self):
        p = ast.parse("-1", mode="eval")
        c = compile(p,"<unknown>", mode="eval" )
        self.assertEqual( eval(c), -1)

    def test_compile_from_ast_002(self):
        p = ast.parse("+1", mode="eval")
        c = compile(p,"<unknown>", mode="eval" )
        self.assertEqual( eval(c), 1)

    def test_compile_from_ast_003(self):
        p = ast.parse("not True", mode="eval")
        c = compile(p,"<unknown>", mode="eval" )
        self.assertEqual( eval(c), False)

    def test_compile_from_ast_004(self):
        p = ast.parse("2+2", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 4 )

    def test_compile_from_ast_005(self):
        p = ast.parse("5-1", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 4 )

    def test_compile_from_ast_006(self):
        p = ast.parse("42%13", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 3 )

    def test_compile_from_ast_007(self):
        p = ast.parse("2**8", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 256 )

    def test_compile_from_ast_008(self):
        p = ast.parse("True or False", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), True )

    def test_compile_from_ast_009(self):
        p = ast.parse("True and False", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), False )

    def test_compile_from_ast_010(self):
        p = ast.parse("'a'", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), "a" )

    def test_compile_from_ast_011(self):
        p = ast.parse("42", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 42 )

    def test_compile_from_ast_012(self):
        p = ast.parse("None", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), None )

    def test_compile_from_ast_013(self):
        p = ast.parse("[1,2,3]", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), [1,2,3] )

    def test_compile_from_ast_014(self):
        p = ast.parse("{1,2,3}", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), {1,2,3} )

    def test_compile_from_ast_015(self):
        p = ast.parse("{1:'a', 2:'b'}", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), {1:'a',2:'b'} )

    def test_compile_from_ast_016(self):
        p = ast.parse("1,2", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), (1,2) )

    def test_compile_from_ast_017(self):
        p = ast.parse("dict()", mode="eval") # trivial call expression
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), {} )

    # parenthesis ?

    def test_compile_from_ast_018(self):
        p = ast.parse("(1)", mode="eval")  # failed attempt to generate parenthesis expression
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 1 )

    def test_compile_from_ast_019(self):
        p = ast.parse("[x for x in range(2)]", mode="eval") # list comprehension
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), [0,1] )

    def test_compile_from_ast_020(self):
        # list comprehension
        p = ast.parse("[(x, y, z) for x in [1,2,3] if x!=2 for y in [3,1,4] for z in [7,8,9] if x != y]", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), [(1, 3, 7), (1, 3, 8), (1, 3, 9), (1, 4, 7),
                                    (1, 4, 8), (1, 4, 9), (3, 1, 7), (3, 1, 8),
                                    (3, 1, 9), (3, 4, 7), (3, 4, 8), (3, 4, 9)] )

    def test_compile_from_ast_021(self):
        p = ast.parse("2>1", mode="eval") # Compare
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), True )

    def test_compile_from_ast_022(self):
        p = ast.parse("2>1<3==3", mode="eval")  # All comparisons evaluate to True
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), True )

        p = ast.parse("1>1<3==3", mode="eval")  # False at first position
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), False )

        p = ast.parse("2>1<0==0", mode="eval")  # False at second position
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), False )

        p = ast.parse("2>1<3==1", mode="eval")  # False at third position
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), False )

    def test_compile_from_ast_023(self):
        p = ast.parse("{x for x in range(3) if x!=2 }", mode="eval") # set comprehension
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), {0,1} )

    def test_compile_from_ast_024(self):
        p = ast.parse("{ x : ord(x) for x in ['a','b'] }", mode="eval") # dict comprehension
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), {'a':97, 'b':98 } )

    def test_compile_from_ast_025(self):
        p = ast.parse("'a'.upper()", mode="eval") # attribute
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 'A' )

    def test_compile_from_ast_026(self):
        p = ast.parse("lambda x: x", mode="eval") # lambda
        c = compile(p,"<unknown>", mode="eval")
        f = eval(c)
        self.assertEqual( f(42),42)

    def test_compile_from_ast_027(self):
        p = ast.parse("lambda x=42: x", mode="eval") # default argument
        c = compile(p,"<unknown>", mode="eval")
        f = eval(c)
        self.assertEqual( f(),42)

    def test_compile_from_ast_028(self):
        p = ast.parse("1 if True else 2", mode="eval") # conditional expression
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 1 )

        p = ast.parse("1 if False else 2", mode="eval") # conditional expression
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 2 )

    def test_compile_from_ast_029(self):
        p = ast.parse("(x for x in [1,2,3] if x!=1)", mode="eval") # generator
        c = compile(p,"<unknown>", mode="eval")
        g = eval(c)
        self.assertEqual( next(g), 2 )
        self.assertEqual( next(g), 3 )
        self.assertRaises( StopIteration, g.__next__ )

    def test_compile_from_ast_031(self):
        p = ast.parse("range(13)[10]", mode="eval") # index
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), 10 )

    def test_compile_from_ast_032(self):
        p = ast.parse("range(42)[1:5]", mode="eval") # slice
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), range(1, 5))

    def test_compile_from_ast_033(self):
        p = ast.parse("range(42)[1:5:2]", mode="eval") # extended? slice
        c = compile(p,"<unknown>", mode="eval")
        self.assertEqual( eval(c), range(1, 5, 2))

    def test_compile_from_ast_034(self):
        # plain generator
        page = [ "line1 aaaaa bbb cccccc ddddddddd", "line2 xxxxxxxx yyyyyyy zzzzzz", "line3 ssssss ttttttttttt uuuu" ]
        p = ast.parse("(word  for line in page  for word in line.split())", mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        g = eval(c)
        self.assertEqual( next(g), 'line1' )
        self.assertEqual( next(g), 'aaaaa' )
        self.assertEqual( next(g), 'bbb')
        self.assertEqual( next(g), 'cccccc' )
        self.assertEqual( next(g), 'ddddddddd' )
        self.assertEqual( next(g), 'line2' )
        self.assertEqual( next(g), 'xxxxxxxx' )
        self.assertEqual( next(g), 'yyyyyyy' )
        self.assertEqual( next(g), 'zzzzzz' )
        self.assertEqual( next(g), 'line3' )
        self.assertEqual( next(g), 'ssssss' )
        self.assertEqual( next(g), 'ttttttttttt' )
        self.assertEqual( next(g), 'uuuu' )
        self.assertRaises( StopIteration, g.__next__ )

    def test_compile_from_ast_035(self):
        # generator with multiple ifs
        page = [ "line1 aaaaa bbb cccccc ddddddddd", "short line", "line2 xxxxxxxx yyyyyyy zzzzzz", "line3 ssssss ttttttttttt uuuu" ]
        p = ast.parse("(word  for line in page if len(line)>10 for word in line.split() if word!='ssssss' if word!='zzzzzz')",
                      mode="eval")
        c = compile(p,"<unknown>", mode="eval")
        g = eval(c)
        self.assertEqual( next(g), 'line1' )
        self.assertEqual( next(g), 'aaaaa' )
        self.assertEqual( next(g), 'bbb')
        self.assertEqual( next(g), 'cccccc' )
        self.assertEqual( next(g), 'ddddddddd' )
        self.assertEqual( next(g), 'line2' )
        self.assertEqual( next(g), 'xxxxxxxx' )
        self.assertEqual( next(g), 'yyyyyyy' )
        self.assertEqual( next(g), 'line3' )
        self.assertEqual( next(g), 'ttttttttttt' )
        self.assertEqual( next(g), 'uuuu' )
        self.assertRaises( StopIteration, g.__next__ )

    def test_compile_from_ast_036(self):
        # the results comply 1:1 with cpython 2.7.3 on Linux
        p = ast.parse("((yield i) for i in range(3))", mode="eval") # yield inside generator
        c = compile(p,"<unknown>", mode="eval")
        g = eval(c)
        self.assertEqual(next(g),0)
        self.assertIsNone(next(g))
        self.assertEqual(next(g),1)
        self.assertIsNone(next(g))
        self.assertEqual(next(g),2)
        self.assertIsNone(next(g))
        self.assertRaises(StopIteration, g.__next__ )

    # TODO: more testing for extended slices [1:2, 5:10], [1:2, ...] etc
    # inspiration at: http://ilan.schnell-web.net/prog/slicing/

    def test_compile_from_ast_038(self):
        p = ast.parse("lambda x: (yield x)", mode="eval") # yield inside lambda
        c = compile(p,"<unknown>", mode="eval")
        eval(c)
        f = eval(c)
        g = f(1)
        self.assertEqual(next(g),1)
        self.assertRaises(StopIteration, g.__next__ )

    def test_compile_from_ast_100(self):
        p = ast.parse("pass", mode="exec")
        c = compile(p,"<unknown>", mode="exec")
        self.assertEqual( eval(c), None)

    def test_compile_from_ast_101(self):
        cap = StringIO()
        p = ast.parse("print(1, 2, end='', file=cap)", mode="exec")  # print
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual( cap.getvalue(), "1 2")

    def test_compile_from_ast_102(self):
        p = ast.parse("a=b=42", mode="exec") # assignment
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'a': 42, 'b': 42})

    def test_compile_from_ast_103(self):
        p = ast.parse("a,b=13,42", mode="exec") # assignment
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'a': 13, 'b': 42})

    def test_compile_from_ast_104(self):
        p = ast.parse("a=42\na+=1", mode="exec") # assignment
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'a': 43})

    def test_compile_from_ast_105(self):
        p = ast.parse("assert True", mode="exec") # assert no exception
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        p = ast.parse("assert False", mode="exec") # assert with exception
        c = compile(p,"<unknown>", mode="exec")
        raised=False
        try:
            exec(c)
        except AssertionError:
            raised=True
        self.assertTrue(raised)

    def test_compile_from_ast_106(self):
        p = ast.parse("a=1\ndel a\nprint(a)", mode="exec") # delete statement
        c = compile(p,"<unknown>", mode="exec")
        raised=False
        try:
            exec(c)
        except NameError:
            raised=True
        self.assertTrue(raised)

    def test_compile_from_ast_107(self):
        p = ast.parse("def f(): return\nf()", mode="exec") # return statement
        c = compile(p,"<unknown>", mode="exec")
        exec(c)

    def test_compile_from_ast_108(self):
        cap = StringIO()
        p = ast.parse("def f(): return 42\nprint(f(), end='', file=cap)", mode="exec") # return statement
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(),'42')

    def test_compile_from_ast_109(self):
        p = ast.parse("def f(): yield 42", mode="exec") # yield statement
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, res)
        f = res["f"]
        g = f()
        self.assertEqual(next(g),42)
        self.assertRaises(StopIteration, g.__next__ )

    def test_compile_from_ast_110(self):
        p = ast.parse("raise Exception('expected')", mode="exec") # raise
        c = compile(p,"<unknown>", mode="exec")
        raised=False
        try:
            exec(c)
        except Exception as e:
            raised=True
            self.assertEqual(e.args[0],'expected')
        self.assertTrue(raised)

    def test_compile_from_ast_111(self):
        p = ast.parse("n=0\nwhile n==0:\n n=1\n break\n n=2", mode="exec") # break
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'n': 1})

    def test_compile_from_ast_112(self):
        p = ast.parse("n=0\nwhile n==0:\n n=1\n continue\n raise Exception()", mode="exec") # continue
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'n': 1})

    # TODO: test relative imports
    # TODO: test imports with subpackages structure

    def test_compile_from_ast_113(self):
        p = ast.parse("import dummy_module", mode="exec") # import
        c = compile(p,"<unknown>", mode="exec")
        exec(c, globals())
        from sys import modules
        self.assertNotEqual(modules.get('dummy_module'),None)
        self.assertEqual(modules.get('dummy_module'),dummy_module)

    def test_compile_from_ast_114(self):
        p = ast.parse("import dummy_module as baz", mode="exec") # import as
        c = compile(p,"<unknown>", mode="exec")
        exec(c, globals())
        from sys import modules
        self.assertNotEqual(modules.get('dummy_module'),None)
        self.assertEqual(modules.get('dummy_module'),baz)

    def test_compile_from_ast_115(self):
        p = ast.parse("from dummy_module import *", mode="exec") # from import *
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, globals(), res)
        from sys import modules
        self.assertNotEqual(modules.get('dummy_module'),None)
        self.assertEqual(res, {'foo':1, 'bar': 2, 'foobar': 3})

    def test_compile_from_ast_116(self):
        p = ast.parse("def f():\n global i\n i+=41\nf()", mode="exec") # global
        c = compile(p,"<unknown>", mode="exec")
        v = {'i':1}
        exec(c, v)
        self.assertEqual(v['i'],42)

    def test_compile_from_ast_117(self):
        cap = StringIO()
        p = ast.parse("exec('print(42, end=\\'\\', file=cap)')", mode="exec") # exec
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), '42')

    def test_compile_from_ast_118(self):
        p = ast.parse("if a:\n x=1\nelif b:\n x=2\nelse:\n x=3", mode="exec") # if elif else
        c = compile(p,"<unknown>", mode="exec")

        res = {'a': True}
        exec(c, res)
        self.assertEqual(res['x'], 1)

        res = {'a': False, 'b': True}
        exec(c, res)
        self.assertEqual(res['x'], 2)

        res = {'a': False, 'b': False}
        exec(c, res)
        self.assertEqual(res['x'], 3)

    def test_compile_from_ast_119(self):
        p = ast.parse("a=1; b=2", mode="exec") # statement list?
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'a': 1, 'b': 2})

    def test_compile_from_ast_120(self):
        p = ast.parse("n=1\nwhile n==0:\n n=13\nelse: n=42", mode="exec") # while with else
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'n': 42})

    def test_compile_from_ast_121(self):
        cap = StringIO()
        p = ast.parse("for i in [0,1,2]:\n print(i, end='', file=cap)\nelse:\n print('else', end='', file=cap)", mode="exec") # for with else
        exec(compile(p,"<unknown>", mode="exec"))
        self.assertEqual(cap.getvalue(),"012else")

    def test_compile_from_ast_122(self):
        cap = StringIO()
        tc = """
try:
    print(1, end='', file=cap)
    raise Exception("test")
    print(2, end='', file=cap)
except Exception as e:
    print(3, end='', file=cap)
else:
    print(4, end='', file=cap)
finally:
    print(5, end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # try except
        exec(compile(p,"<unknown>", mode="exec"))
        self.assertEqual(cap.getvalue(), "135")

    def test_compile_from_ast_123(self):
        cap = StringIO()
        tc = """
try:
    print(1, end='', file=cap)
    # raise Exception("test")
    print(2, end='', file=cap)
except Exception as e:
    print(3, end='', file=cap)
else:
    print(4, end='', file=cap)
finally:
    print(5, end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # try except
        exec(compile(p,"<unknown>", mode="exec"))
        self.assertEqual(cap.getvalue(),"1245")

    def test_compile_from_ast_124(self):
        tc = """
with open("dummy_module.py") as dm:
    l = len(dm.read())
            """
        p = ast.parse(tc, mode="exec") # try except
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, res)
        self.assertEqual(res['l'], 20)

    def test_compile_from_ast_125(self):
        tc = """
class Foo(object):
    pass
foo=Foo()
            """
        p = ast.parse(tc, mode="exec") # try except
        c = compile(p,"<unknown>", mode="exec")
        res = {}
        exec(c, res)
        self.assertIsInstance(res['foo'], res['Foo'])

    def test_compile_from_ast_126(self):
        cap = StringIO()
        tc = """
def f(a):
    return a
print(f(22), end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # call function with an argument
        exec(compile(p,"<unknown>", mode="exec"))
        self.assertEqual(cap.getvalue(), "22")

    def test_compile_from_ast_127(self):
        cap = StringIO()
        tc = """
def f(a):
    return a
print(f(a=222), end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # call function with an argument, pass as keyword
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), "222")

    def test_compile_from_ast_128(self):
        cap = StringIO()
        tc = """
def f(a,*args):
    return args[1]
print(f(1,2,42), end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # call function with a variable number of arguments
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), "42")

    def test_compile_from_ast_129(self):
        cap = StringIO()
        tc = """
def f(a,**kwargs):
    return kwargs["two"]
d={ "one":13, "two":42 }
print(f(1, **d), end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # call function with a keyword argument
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), "42")

    def test_compile_from_ast_130(self):
        cap = StringIO()
        tc = """
def f(a,**kwargs):
    return kwargs["two"]
print(f(1, two=42, one=13), end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # call function with a keyword argument
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), "42")

    def test_compile_from_ast_131(self):
        cap = StringIO()
        tc = """
def deco(fx):
    def wrap():
        print("wrapped", file=cap)
        fx()
    return wrap
@deco
def f():
    print("f", file=cap)
f()
            """
        p = ast.parse(tc, mode="exec") # function decorator
        c = compile(p,"<unknown>", mode="exec")
        exec(c, {"cap": cap})
        self.assertEqual(cap.getvalue(), "wrapped\nf\n")

    def test_compile_from_ast_132(self):
        cap = StringIO()
        tc = """
def deco(k):
    k.foo = 42
    return k
@deco
class C:
    pass
c=C()
print(c.foo, end='', file=cap)
            """
        p = ast.parse(tc, mode="exec") # function decorator
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), "42")

    def test_compile_from_ast_133(self):
        cap = StringIO()
        tc = """
def f1():
    yield 1

def fNone():
    yield

for v in f1():
    print(v, file=cap)
for v in fNone():
    print(v, file=cap)

            """
        p = ast.parse(tc, mode="exec") # yield
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        self.assertEqual(cap.getvalue(), "1\nNone\n")

    def test_compile_from_ast_200(self):
        p = ast.parse("a=1; b=2", mode="single") # something with single
        c = compile(p,"<unknown>", mode="single")
        res = {}
        exec(c, {}, res)
        self.assertEqual(res, {'a': 1, 'b': 2})

    def test_compile_from_ast_201(self):
        p = ast.parse("1+1", mode="single") # expression in single should find its way into stdout
        c = compile(p,"<unknown>", mode="single")
        saveStdout = sys.stdout
        sys.stdout = cap = StringIO()
        exec(c)
        sys.stdout = saveStdout
        self.assertEqual(cap.getvalue(), "2\n")

    def test_compile_argument_bytes(self):
        p = ast.parse(b'1+1', "<unknown>", mode="eval")
        c = compile(p, "<unknown>", mode="eval")
        self.assertEqual(eval(c),2)

    def test_compile_argument_memoryview(self):
        p = ast.parse(memoryview(b'1+1'), "<unknown>", mode="eval")
        c = compile(p, "<unknown>", mode="eval")
        self.assertEqual(eval(c),2)

    def test_compile_argument_bytearray(self):
        p = ast.parse(bytearray('1+1','ascii'), "<unknown>", mode="eval")
        c = compile(p, "<unknown>", mode="eval")
        self.assertEqual(eval(c),2)

    def test_compile_argument_error(self):
        self.assertRaises( TypeError, ast.parse, ['1+1'], "<unknown>", mode="eval")

    def test_compile_manual(self):
        # check that expressions which are built manually compile
        for test in eval_tests:
            a = ast.parse(test, "<unknown>", mode="eval")
            b = ast.fix_missing_locations(eval(ast.dump(a, annotate_fields=False), vars(ast)))
            compile(b, "<unknown>", mode="eval")
        for test in exec_tests:
            a = ast.parse(test, "<unknown>", mode="exec")
            b = ast.fix_missing_locations(eval(ast.dump(a, annotate_fields=False), vars(ast)))
            compile(b, "<unknown>", mode="exec")

    def test_snippets(self):
        # Things which diverted from cpython:
        # - col_offset of list comprehension in ironpython uses opening bracket, cpython points to first expr
        # - same for generator
        # - Slice in iron has col_offset and lineno set, in cpython both are not set
        for input, output, kind in ((exec_tests, exec_results, "exec"),
                                    (single_tests, single_results, "single"),
                                    (eval_tests, eval_results, "eval")):
            for i, o in zip(input, output):
                ast_tree = compile(i, "?", kind, ast.PyCF_ONLY_AST)
                self.assertEqual(to_tuple(ast_tree), o)
                self._assertTrueorder(ast_tree, (0, 0))

    def test_slicex(self):
        slc = ast.parse("x[1:2:3]").body[0].value.slice
        self.assertEqual(slc.lower.n, 1)
        self.assertEqual(slc.upper.n, 2)
        self.assertEqual(slc.step.n, 3)

    def test_slice(self):
        slc = ast.parse("x[::]").body[0].value.slice
        self.assertIsNone(slc.upper)
        self.assertIsNone(slc.lower)
        self.assertIsNone(slc.step)

    def test_from_import(self):
        im = ast.parse("from . import y").body[0]
        self.assertIsNone(im.module)

    def test_base_classes(self):
        self.assertTrue(issubclass(ast.For, ast.stmt))
        self.assertTrue(issubclass(ast.Name, ast.expr))
        self.assertTrue(issubclass(ast.stmt, ast.AST))
        self.assertTrue(issubclass(ast.expr, ast.AST))
        self.assertTrue(issubclass(ast.comprehension, ast.AST))
        self.assertTrue(issubclass(ast.Gt, ast.AST))

    def test_nodeclasses(self):
        # IronPyhon performs argument typechecking
        l=ast.Str('A')
        o=ast.Mult()
        r=ast.Num('13')
        x=ast.BinOp(l,o,r,lineno=42)
        self.assertEqual(x.left, l)
        self.assertEqual(x.op, o)
        self.assertEqual(x.right, r)
        self.assertEqual(x.lineno, 42)

        # node raises exception when not given enough arguments
        self.assertRaises(TypeError, ast.BinOp, l, o)

        # can set attributes through kwargs too
        x = ast.BinOp(left=l, op=o, right=r, lineno=42)
        self.assertEqual(x.left, l)
        self.assertEqual(x.op, o)
        self.assertEqual(x.right, r)
        self.assertEqual(x.lineno, 42)

        # this used to fail because Sub._fields was None
        x = ast.Sub()

    def test_docexample(self):
        # used to fail on ironpython for various reason
        node = ast.UnaryOp(ast.USub(), ast.Num(5, lineno=0, col_offset=0),
                            lineno=0, col_offset=0)

        # the same with zero argument constructors
        node = ast.UnaryOp()
        node.op = ast.USub()
        node.operand = ast.Num()
        node.operand.n = 5
        node.operand.lineno = 0
        node.operand.col_offset = 0
        node.lineno = 0
        node.col_offset = 0

    def test_example_from_net(self):
        node = ast.Expression(ast.BinOp(ast.Str('xy'), ast.Mult(), ast.Num(3)))

    def _test_extra_attribute(self):
        n=ast.Num()
        n.extra_attribute=2
        self.assertTrue(hasattr(n,'extra_attribute'))

    def test_operators(self):
        boolop0 = ast.BoolOp()
        boolop1 = ast.BoolOp(ast.And(),
                          [ ast.Name('True', ast.Load()), ast.Name('False', ast.Load()), ast.Name('a',ast.Load())])
        boolop2 = ast.BoolOp(ast.And(),
                          [ ast.Name('True', ast.Load()), ast.Name('False', ast.Load()), ast.Name('a',ast.Load())],
                          lineno=0, col_offset=0)
        binop0 = ast.BinOp()
        binop1 = ast.BinOp(ast.Str('xy'), ast.Mult(), ast.Num(3))
        binop2 = ast.BinOp(ast.Str('xy'), ast.Mult(), ast.Num(3), lineno=0, col_offset=0)

        unaryop0 = ast.UnaryOp()
        unaryop1 = ast.UnaryOp(ast.Not(), ast.Name('True',ast.Load()))
        unaryop2 = ast.UnaryOp(ast.Not(), ast.Name('True',ast.Load()), lineno=0, col_offset=0)

        lambda0 = ast.Lambda()
        lambda1 = ast.Lambda(ast.arguments([ast.Name('x', ast.Param())], None, [], [], None, []), ast.Name('x', ast.Load()))

        ifexp0 = ast.IfExp()
        ifexp1 = ast.IfExp(ast.Name('True',ast.Load()), ast.Num(1), ast.Num(0))
        ifexp2 = ast.IfExp(ast.Name('True',ast.Load()), ast.Num(1), ast.Num(0), lineno=0, col_offset=0)

        dict0 = ast.Dict()
        dict1 = ast.Dict([ast.Num(1), ast.Num(2)], [ast.Str('a'), ast.Str('b')])
        dict2 = ast.Dict([ast.Num(1), ast.Num(2)], [ast.Str('a'), ast.Str('b')], lineno=0, col_offset=0)

        set0 = ast.Set()
        set1 = ast.Set([ast.Num(1), ast.Num(2)])
        set2 = ast.Set([ast.Num(1), ast.Num(2)], lineno=0, col_offset=0)

        lc0 = ast.ListComp()
        lc1 = ast.ListComp( ast.Name('x',ast.Load()),
                   [ast.comprehension(ast.Name('x', ast.Store()),
                                      ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load()), [])])
        lc2 = ast.ListComp( ast.Name('x',ast.Load()),
                   [ast.comprehension(ast.Name('x', ast.Store()),
                                      ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load()), [])], lineno=0, col_offset=0)

        setcomp0 = ast.SetComp()
        setcomp1 = ast.SetComp(ast.Name('x', ast.Load()),
                   [ast.comprehension(ast.Name('x', ast.Store()), ast.Str('abracadabra'),
                                      [ast.Compare(ast.Name('x', ast.Load()), [ast.NotIn()],
                                                   [ast.Str('abc')])])])

        comprehension0 = ast.comprehension()
        comprehension1 = ast.comprehension(ast.Name('x', ast.Store()),
                                           ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load()), [])

        # "{i : chr(65+i) for i in (1,2)}")
        dictcomp0 = ast.DictComp()
        dictcomp1 = ast.DictComp(ast.Name('i', ast.Load()),
                                 ast.Call(ast.Name('chr', ast.Load()),
                                          [ast.BinOp(ast.Num(65), ast.Add(), ast.Name('i', ast.Load()))],
                                          [], None, None),
                                 [ast.comprehension(ast.Name('i', ast.Store()),
                                                    ast.Tuple([ast.Num(1), ast.Num(n=2)], ast.Load()), [])])
        dictcomp2 = ast.DictComp(ast.Name('i', ast.Load()),
                                 ast.Call(ast.Name('chr', ast.Load()),
                                          [ast.BinOp(ast.Num(65), ast.Add(), ast.Name('i', ast.Load()))],
                                          [], None, None),
                                 [ast.comprehension(ast.Name('i', ast.Store()),
                                                    ast.Tuple([ast.Num(1), ast.Num(n=2)], ast.Load()), [])], lineno=0, col_offset=0)

        # (x for x in (1,2))
        genexp0 = ast.GeneratorExp()
        genexp1 = ast.GeneratorExp(ast.Name('x', ast.Load()),
                                   [ast.comprehension(ast.Name('x', ast.Store()),
                                                      ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load()), [])])
        genexp2 = ast.GeneratorExp(ast.Name('x', ast.Load()),
                                   [ast.comprehension(ast.Name('x', ast.Store()),
                                                      ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load()), [])], lineno=0, col_offset=0)

        # yield 2
        yield0 = ast.Yield()
        yield1 = ast.Yield(ast.Num(2))
        yield2 = ast.Yield(ast.Num(2), lineno=0, col_offset=0)
        yield20 = ast.Yield(lineno=0, col_offset=0)

        # a>0
        compare0 = ast.Compare()
        compare1 = ast.Compare(ast.Name('a', ast.Load()), [ast.Gt()], [ast.Num(0)])
        compare2 = ast.Compare(ast.Name('a', ast.Load()), [ast.Gt()], [ast.Num(0)], lineno=0, col_offset=0)

        # chr(65)
        call0 = ast.Call()
        call1 = ast.Call(ast.Name('chr', ast.Load()), [ast.Num(65)], [], None, None)
        call2 = ast.Call(ast.Name('chr', ast.Load()), [ast.Num(65)], [], None, None, lineno=0, col_offset=0)
        call20 = ast.Call(ast.Name('f', ast.Load()), [ast.Num(0)], [], None, None)
        call21 = ast.Call(ast.Name('f', ast.Load()), [ast.Num(0)], [], None, None, lineno=0, col_offset=0)

        # 0
        num0 = ast.Num()
        num1 = ast.Num(0)
        num2 = ast.Num(0, lineno=0, col_offset=0)

        # "foo"
        str0 = ast.Str()
        str1 = ast.Str("foo")
        str2 = ast.Str("foo", lineno=0, col_offset=0)

        # foo.bar
        attr0 = ast.Attribute()
        attr1 = ast.Attribute(ast.Name('foo', ast.Load()), 'bar', ast.Load())
        attr2 = ast.Attribute(ast.Name('foo', ast.Load()), 'bar', ast.Load(), lineno=0, col_offset=0)

        # a[1:2]
        subscript0 = ast.Subscript()
        subscript1 = ast.Subscript(ast.Name('a', ast.Load()), ast.Slice(ast.Num(1), ast.Num(2), None), ast.Load())
        subscript2 = ast.Subscript(ast.Name('a', ast.Load()), ast.ExtSlice([ast.Num(1), ast.Num(2)]), ast.Load(), lineno=0, col_offset=0)

        # name
        name0 = ast.Name()
        name1 = ast.Name("name", ast.Load())
        name2 = ast.Name("name", ast.Load(), lineno=0, col_offset=0)

        # [1,2]
        list0 = ast.List()
        list1 = ast.List([ast.Num(1), ast.Num(2)], ast.Load())
        list2 = ast.List([ast.Num(1), ast.Num(2)], ast.Load(), lineno=0, col_offset=0)

        # (1,2)
        tuple0 = ast.Tuple()
        tuple1 = ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load())
        tuple2 = ast.Tuple([ast.Num(1), ast.Num(2)], ast.Load(), lineno=0, col_offset=0)

    def test_stmt(self):

        # def foo():
        #   pass
        fundef0 = ast.FunctionDef()
        fundef1 = ast.FunctionDef('foo', ast.arguments([], None, [], [], None, []), [ast.Pass()], [], None)
        fundef2 = ast.FunctionDef('foo', ast.arguments([], None, [], [], None, []), [ast.Pass()], [], None, lineno=0, col_offset=0)

        # class foo(object):
        #   pass
        classdef0 = ast.ClassDef()
        classdef1 = ast.ClassDef('foo', [ast.Name('object', ast.Load())], [], None, None, [ast.Pass()], [])
        classdef1 = ast.ClassDef('foo', [ast.Name('object', ast.Load())], [], None, None, [ast.Pass()], [], lineno=0, col_offset=0)

        # return 0
        return0 = ast.Return()
        return1 = ast.Return(ast.Num(0))
        return2 = ast.Return(ast.Num(0), lineno=0, col_offset=0)
        return20 = ast.Return(lineno=0, col_offset=0)

        # del d[1]
        del0 = ast.Delete()
        del1 = ast.Delete([ast.Subscript(ast.Name('d', ast.Load()), ast.Index(ast.Num(1)), ast.Del())])
        del2 = ast.Delete([ast.Subscript(ast.Name('d', ast.Load()), ast.Index(ast.Num(1)), ast.Del())], lineno=0, col_offset=0)

        # a=1
        assign0=ast.Assign()
        assign1=ast.Assign([ast.Name('a', ast.Store())], ast.Num(1))
        assign2=ast.Assign([ast.Name('a', ast.Store())], ast.Num(1), lineno=0, col_offset=0)

        # a+=1
        augassign0=ast.AugAssign()
        augassign1=ast.AugAssign(ast.Name('a', ast.Store()), ast.Add(), ast.Num(1))
        augassign2=ast.AugAssign(ast.Name('a', ast.Store()), ast.Add(), ast.Num(1), lineno=0, col_offset=0)

        # for i in l:
        #   print i
        # else:
        #   pass
        for0 = ast.For()
        for1 = ast.For(ast.Name('i', ast.Store()),
                       ast.Name('l', ast.Load()),
                       [ast.Assert(ast.Name('i', ast.Load()), None)],
                       [ast.Pass()])
        for2 = ast.For(ast.Name('i', ast.Store()),
                       ast.Name('l', ast.Load()),
                       [ast.Assert(ast.Name('i', ast.Load()), None)],
                       [ast.Pass()], lineno=0, col_offset=0)

        # while True:
        #   pass
        # else:
        #   pass
        while0 = ast.While()
        while1 = ast.While(ast.Name('True', ast.Load()), [ast.Pass()], [ast.Pass()])
        while2 = ast.While(ast.Name('True', ast.Load()), [ast.Pass()], [ast.Pass()], lineno=0, col_offset=0)

        # if a:
        #   pass
        # else:
        #   pass
        if0 = ast.If()
        if1 = ast.If(ast.Name('a', ast.Load()), [ast.Pass()], [ast.Pass()])
        if2 = ast.If(ast.Name('a', ast.Load()), [ast.Pass()], [ast.Pass()], lineno=0, col_offset=0)

        # with open("foo") as f:
        #   pass
        with0 = ast.With()
        with0 = ast.With([ast.withitem(ast.Call(ast.Name('open', ast.Load()), [ast.Str('foo')], [], None, None),
                         ast.Name('f', ast.Store()))],
                         [ast.Pass()])

        # raise Exception()
        raise0 = ast.Raise()
        raise1 = ast.Raise(ast.Call(ast.Name('Exception', ast.Load()), [], [], None, None), None)
        raise2 = ast.Raise(ast.Call(ast.Name('Exception', ast.Load()), [], [], None, None), None, lineno=0, col_offset=0)

    def test_attributes(self):
        # assert True, "bad"
        assert0 = ast.Assert()
        self.assertFalse(hasattr(assert0, 'lineno'))
        self.assertFalse(hasattr(assert0, 'col_offset'))
        assert1 = ast.Assert(ast.Name('True', ast.Load()), ast.Str('bad'))
        self.assertFalse(hasattr(assert1, 'lineno'))
        self.assertFalse(hasattr(assert1, 'col_offset'))
        try:
            tmp=assert1.lineno
        except Exception as e:
            self.assertTrue(isinstance(e,AttributeError))
        try:
            tmp=assert1.col_offset
        except Exception as e:
            self.assertTrue(isinstance(e,AttributeError))
        assert2 = ast.Assert(ast.Name('True', ast.Load()), ast.Str('bad'), lineno=2, col_offset=3)
        self.assertEqual(assert2.lineno,2)
        self.assertEqual(assert2.col_offset,3)

    def test_compare(self):
        #
        c0 = to_tuple(ast.parse("a<b>c"))
        c1 = to_tuple(ast.parse("(a<b)>c"))
        c2 = to_tuple(ast.parse("a<(b>c)"))
        self.assertNotEqual(c0,c1)
        self.assertNotEqual(c1,c2)
        self.assertNotEqual(c0,c2)

    def test_pickling(self):
        import pickle
        mods = [pickle]
        try:
            import pickle
            mods.append(pickle)
        except ImportError:
            pass
        protocols = [0, 1, 2]
        for mod in mods:
            for protocol in protocols:
                for ast in (compile(i, "?", "exec", 0x400) for i in exec_tests):
                    ast2 = mod.loads(mod.dumps(ast, protocol))
                    self.assertEqual(to_tuple(ast2), to_tuple(ast))

    def test_compile_comment(self):
        self.assertRaisesRegex( SyntaxError, "unexpected EOF while parsing", compile, "#", "string", "single" )

class ASTHelpers_Test(unittest.TestCase):

    def test_parse(self):
        a = ast.parse('foo(1 + 1)')
        b = compile('foo(1 + 1)', '<unknown>', 'exec', ast.PyCF_ONLY_AST)
        self.assertEqual(ast.dump(a), ast.dump(b))

    def test_dump(self):
        node = ast.parse('spam(eggs, "and cheese")')
        self.assertEqual(ast.dump(node),
            "Module(body=[Expr(value=Call(func=Name(id='spam', ctx=Load()), "
            "args=[Name(id='eggs', ctx=Load()), Str(s='and cheese')], "
            "keywords=[], starargs=None, kwargs=None))])"
        )
        self.assertEqual(ast.dump(node, annotate_fields=False),
            "Module([Expr(Call(Name('spam', Load()), [Name('eggs', Load()), "
            "Str('and cheese')], [], None, None))])"
        )
        self.assertEqual(ast.dump(node, include_attributes=True),
            "Module(body=[Expr(value=Call(func=Name(id='spam', ctx=Load(), "
            "lineno=1, col_offset=0), args=[Name(id='eggs', ctx=Load(), "
            "lineno=1, col_offset=5), Str(s='and cheese', lineno=1, "
            "col_offset=11)], keywords=[], starargs=None, kwargs=None, "
            "lineno=1, col_offset=0), lineno=1, col_offset=0)])"
        )

    def test_copy_location(self):
        src = ast.parse('1 + 1', mode='eval')
        src.body.right = ast.copy_location(ast.Num(2), src.body.right)
        self.assertEqual(ast.dump(src, include_attributes=True),
            'Expression(body=BinOp(left=Num(n=1, lineno=1, col_offset=0), '
            'op=Add(), right=Num(n=2, lineno=1, col_offset=4), lineno=1, '
            'col_offset=0))'
        )

    def test_fix_missing_locations(self):
        src = ast.parse('write("spam")')
        src.body.append(ast.Expr(ast.Call(ast.Name('spam', ast.Load()),
                                          [ast.Str('eggs')], [], None, None)))
        self.assertEqual(src, ast.fix_missing_locations(src))
        self.assertEqual(ast.dump(src, include_attributes=True),
            "Module(body=[Expr(value=Call(func=Name(id='write', ctx=Load(), "
            "lineno=1, col_offset=0), args=[Str(s='spam', lineno=1, "
            "col_offset=6)], keywords=[], starargs=None, kwargs=None, "
            "lineno=1, col_offset=0), lineno=1, col_offset=0), "
            "Expr(value=Call(func=Name(id='spam', ctx=Load(), lineno=1, "
            "col_offset=0), args=[Str(s='eggs', lineno=1, col_offset=0)], "
            "keywords=[], starargs=None, kwargs=None, lineno=1, "
            "col_offset=0), lineno=1, col_offset=0)])"
        )

    def test_increment_lineno(self):
        src = ast.parse('1 + 1', mode='eval')
        self.assertEqual(ast.increment_lineno(src, n=3), src)
        self.assertEqual(ast.dump(src, include_attributes=True),
            'Expression(body=BinOp(left=Num(n=1, lineno=4, col_offset=0), '
            'op=Add(), right=Num(n=1, lineno=4, col_offset=4), lineno=4, '
            'col_offset=0))'
        )
        # issue10869: do not increment lineno of root twice
        src = ast.parse('1 + 1', mode='eval')
        self.assertEqual(ast.increment_lineno(src.body, n=3), src.body)
        self.assertEqual(ast.dump(src, include_attributes=True),
            'Expression(body=BinOp(left=Num(n=1, lineno=4, col_offset=0), '
            'op=Add(), right=Num(n=1, lineno=4, col_offset=4), lineno=4, '
            'col_offset=0))'
        )

    def test_iter_fields(self):
        node = ast.parse('foo()', mode='eval')
        d = dict(ast.iter_fields(node.body))
        self.assertEqual(d.pop('func').id, 'foo')
        self.assertEqual(d, {'keywords': [], 'kwargs': None,
                             'args': [], 'starargs': None})

    def test_iter_child_nodes(self):
        node = ast.parse("spam(23, 42, eggs='leek')", mode='eval')
        self.assertEqual(len(list(ast.iter_child_nodes(node.body))), 4)
        iterator = ast.iter_child_nodes(node.body)
        self.assertEqual(next(iterator).id, 'spam')
        self.assertEqual(next(iterator).n, 23)
        self.assertEqual(next(iterator).n, 42)
        self.assertEqual(ast.dump(next(iterator)),
            "keyword(arg='eggs', value=Str(s='leek'))"
        )

    def test_get_docstring(self):
        node = ast.parse('def foo():\n  """line one\n  line two"""')
        self.assertEqual(ast.get_docstring(node.body[0]),
                         'line one\nline two')

    def test_literal_eval(self):
        self.assertEqual(ast.literal_eval('[1, 2, 3]'), [1, 2, 3])
        self.assertEqual(ast.literal_eval('{"foo": 42}'), {"foo": 42})
        self.assertEqual(ast.literal_eval('(True, False, None)'), (True, False, None))
        self.assertRaises(ValueError, ast.literal_eval, 'foo()')

    def test_literal_eval_issue4907(self):
        self.assertEqual(ast.literal_eval('2j'), 2j)
        self.assertEqual(ast.literal_eval('10 + 2j'), 10 + 2j)
        self.assertEqual(ast.literal_eval('1.5 - 2j'), 1.5 - 2j)
        self.assertEqual(ast.literal_eval('2 + (3 + 4j)'), 5 + 4j)

    def test_literal_eval_cp35572(self):
        self.assertEqual(ast.literal_eval('-1'), -1)
        self.assertEqual(ast.literal_eval('+1'), 1)
        self.assertEqual(ast.literal_eval('-1j'), -1j)
        self.assertEqual(ast.literal_eval('+1j'), 1j)
        self.assertEqual(ast.literal_eval('-1.1'), -1.1)
        self.assertEqual(ast.literal_eval('+1.1'), 1.1)

def main():
    if __name__ != '__main__':
        return
    if sys.argv[1:] == ['-x']:
        import os
        # place for a quick individual test
        p = ast.parse( "dict(a=1)", mode="exec") # call function with an argument
        input("attach to %s and press enter" % os.getpid())
        c = compile(p,"<unknown>", mode="exec")
        exec(c)
        sys.exit()

    if sys.argv[1:] == ['-g']:
        for statements, kind in ((exec_tests, "exec"), (single_tests, "single"),
                                 (eval_tests, "eval")):
            print(kind+"_results = [")
            for s in statements:
                print(repr(to_tuple(compile(s, "?", kind, 0x400)))+",")
            print("]")
        print("main()")
        raise SystemExit
    test_main()

#### GENERATED FOR IRONPYTHON ####
exec_results_stdlib = [
('Module', [('Expr', (1, 0), ('NameConstant', (1, 0), None))]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [], None, [], [], None, []), [('Pass', (1, 9))], [], None)]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [('arg', (1, 6), 'a', None)], None, [], [], None, []), [('Pass', (1, 10))], [], None)]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [('arg', (1, 6), 'a', None)], None, [], [], None, [('Num', (1, 8), 0)]), [('Pass', (1, 12))], [], None)]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [], ('arg', (1, 7), 'args', None), [], [], None, []), [('Pass', (1, 14))], [], None)]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [], None, [], [], ('arg', (1, 8), 'kwargs', None), []), [('Pass', (1, 17))], [], None)]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [('arg', (1, 6), 'a', None), ('arg', (1, 9), 'b', None), ('arg', (1, 14), 'c', None), ('arg', (1, 22), 'd', None), ('arg', (1, 28), 'e', None)], ('arg', (1, 35), 'args', None), [('arg', (1, 41), 'f', None)], [('Num', (1, 43), 42)], ('arg', (1, 49), 'kwargs', None), [('Num', (1, 11), 1), ('NameConstant', (1, 16), None), ('List', (1, 24), [], ('Load',)), ('Dict', (1, 30), [], [])]), [('Pass', (1, 58))], [], None)]),
('Module', [('ClassDef', (1, 0), 'C', [], [], None, None, [('Pass', (1, 8))], [])]),
('Module', [('ClassDef', (1, 0), 'C', [('Name', (1, 8), 'object', ('Load',))], [], None, None, [('Pass', (1, 17))], [])]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [], None, [], [], None, []), [('Return', (1, 8), ('Num', (1, 15), 1))], [], None)]),
('Module', [('Delete', (1, 0), [('Name', (1, 4), 'v', ('Del',))])]),
('Module', [('Assign', (1, 0), [('Name', (1, 0), 'v', ('Store',))], ('Num', (1, 4), 1))]),
('Module', [('AugAssign', (1, 0), ('Name', (1, 0), 'v', ('Store',)), ('Add',), ('Num', (1, 5), 1))]),
('Module', [('For', (1, 0), ('Name', (1, 4), 'v', ('Store',)), ('Name', (1, 9), 'v', ('Load',)), [('Pass', (1, 11))], [])]),
('Module', [('While', (1, 0), ('Name', (1, 6), 'v', ('Load',)), [('Pass', (1, 8))], [])]),
('Module', [('If', (1, 0), ('Name', (1, 3), 'v', ('Load',)), [('Pass', (1, 5))], [])]),
('Module', [('With', (1, 0), [('withitem', ('Name', (1, 5), 'x', ('Load',)), ('Name', (1, 10), 'y', ('Load' if is_cli else 'Store',)))], [('Pass', (1, 13))])]),
('Module', [('With', (1, 0), [('withitem', ('Name', (1, 5), 'x', ('Load',)), ('Name', (1, 10), 'y', ('Load',)))], [('With', (1, 0), [('withitem', ('Name', (1, 13), 'z', ('Load',)), ('Name', (1, 18), 'q', ('Load',)))], [('Pass', (1, 21))])])]) if is_cli else
('Module', [('With', (1, 0), [('withitem', ('Name', (1, 5), 'x', ('Load',)), ('Name', (1, 10), 'y', ('Store',))), ('withitem', ('Name', (1, 13), 'z', ('Load',)), ('Name', (1, 18), 'q', ('Store',)))], [('Pass', (1, 21))])]),
('Module', [('Raise', (1, 0), ('Call', (1, 6), ('Name', (1, 6), 'Exception', ('Load',)), [('Str', (1, 16), 'string')], [], None, None), None)]),
('Module', [('Try', (1, 0), [('Pass', (2, 2))], [('ExceptHandler', (3, 0), ('Name', (3, 7), 'Exception', ('Load',)), None, [('Pass', (4, 2))])], [], [])]),
('Module', [('Try', (1, 0), [('Pass', (2, 2))], [], [], [('Pass', (4, 2))])]),
('Module', [('Assert', (1, 0), ('Name', (1, 7), 'v', ('Load',)), None)]),
('Module', [('Import', (1, 0), [('alias', 'sys', None)])]),
('Module', [('ImportFrom', (1, 0), 'sys', [('alias', 'v', None)], 0)]),
('Module', [('Global', (1, 0), ['v'])]),
('Module', [('Expr', (1, 0), ('Num', (1, 0), 1))]),
('Module', [('Pass', (1, 0))]),
#('Module', [('Break', (1, 0))]), # doesn't work outside a loop in IronPython
#('Module', [('Continue', (1, 0))]), # doesn't work outside a loop in IronPython
('Module', [('For', (1, 0), ('Tuple', (1, 4), [('Name', (1, 4), 'a', ('Store',)), ('Name', (1, 6), 'b', ('Store',))], ('Store',)), ('Name', (1, 11), 'c', ('Load',)), [('Pass', (1, 14))], [])]),
('Module', [('Expr', (1, 0), ('ListComp', (1, 0) if is_cli else (1, 1), ('Tuple', (1, 1) if is_cli else (1, 2), [('Name', (1, 2), 'a', ('Load',)), ('Name', (1, 4), 'b', ('Load',))], ('Load',)), [('comprehension', ('Tuple', (1, 11), [('Name', (1, 11), 'a', ('Store',)), ('Name', (1, 13), 'b', ('Store',))], ('Store',)), ('Name', (1, 18), 'c', ('Load',)), [])]))]),
('Module', [('Expr', (1, 0), ('GeneratorExp', (1, 0) if is_cli else (1, 1), ('Tuple', (1, 1) if is_cli else (1, 2), [('Name', (1, 2), 'a', ('Load',)), ('Name', (1, 4), 'b', ('Load',))], ('Load',)), [('comprehension', ('Tuple', (1, 11), [('Name', (1, 11), 'a', ('Store',)), ('Name', (1, 13), 'b', ('Store',))], ('Store',)), ('Name', (1, 18), 'c', ('Load',)), [])]))]),
('Module', [('Expr', (1, 0), ('GeneratorExp', (1, 0) if is_cli else (1, 1), ('Tuple', (1, 1) if is_cli else (1, 2), [('Name', (1, 2), 'a', ('Load',)), ('Name', (1, 4), 'b', ('Load',))], ('Load',)), [('comprehension', ('Tuple', (1, 11) if is_cli else (1, 12), [('Name', (1, 12), 'a', ('Store',)), ('Name', (1, 14), 'b', ('Store',))], ('Store',)), ('Name', (1, 20), 'c', ('Load',)), [])]))]),
('Module', [('Expr', (1, 0), ('GeneratorExp', (1, 0) if is_cli else (2, 4), ('Tuple', (2, 4) if is_cli else (3, 4), [('Name', (3, 4), 'Aa', ('Load',)), ('Name', (5, 7), 'Bb', ('Load',))], ('Load',)), [('comprehension', ('Tuple', (8, 4), [('Name', (8, 4), 'Aa', ('Store',)), ('Name', (10, 4), 'Bb', ('Store',))], ('Store',)), ('Name', (10, 10), 'Cc', ('Load',)), [])]))]),
('Module', [('Expr', (1, 0), ('DictComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'a', ('Load',)), ('Name', (1, 5), 'b', ('Load',)), [('comprehension', ('Name', (1, 11), 'w', ('Store',)), ('Name', (1, 16), 'x', ('Load',)), []), ('comprehension', ('Name', (1, 22), 'm', ('Store',)), ('Name', (1, 27), 'p', ('Load',)), [('Name', (1, 32), 'g', ('Load',))])]))]),
('Module', [('Expr', (1, 0), ('DictComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'a', ('Load',)), ('Name', (1, 5), 'b', ('Load',)), [('comprehension', ('Tuple', (1, 11), [('Name', (1, 11), 'v', ('Store',)), ('Name', (1, 13), 'w', ('Store',))], ('Store',)), ('Name', (1, 18), 'x', ('Load',)), [])]))]),
('Module', [('Expr', (1, 0), ('SetComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'r', ('Load',)), [('comprehension', ('Name', (1, 7), 'l', ('Store',)), ('Name', (1, 12), 'x', ('Load',)), [('Name', (1, 17), 'g', ('Load',))])]))]),
('Module', [('Expr', (1, 0), ('SetComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'r', ('Load',)), [('comprehension', ('Tuple', (1, 7), [('Name', (1, 7), 'l', ('Store',)), ('Name', (1, 9), 'm', ('Store',))], ('Store',)), ('Name', (1, 14), 'x', ('Load',)), [])]))]),
]
exec_results = exec_results_stdlib + [
('Module', [('While', (1, 0), ('Name', (1, 6), 'x', ('Load',)), [('Break', (1, 9))], [])]),
('Module', [('While', (1, 0), ('Name', (1, 6), 'x', ('Load',)), [('Continue', (1, 9))], [])]),
('Module', [('If', (1, 0), ('Name', (1, 3), 'v', ('Load',)), [('Pass', (1, 5))], [('If', (2, 0) if is_cli else (2, 5), ('Name', (2, 5), 'u', ('Load',)), [('Pass', (2, 7))], [('Pass', (3, 6))])])]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [], None, [], [], None, []), [('Expr', (1, 9), ('Yield', (1, 9), ('Num', (1, 15), 1)))], [], None)]),
('Module', [('FunctionDef', (1, 0), 'f', ('arguments', [], None, [], [], None, []), [('Expr', (1, 9), ('Yield', (1, 9), None))], [], None)]),
('Module', []),
]
single_results = [
('Interactive', [('Expr', (1, 0), ('BinOp', (1, 0), ('Num', (1, 0), 1), ('Add',), ('Num', (1, 2), 2)))]),
]
eval_results_stdlib = [
('Expression', ('NameConstant', (1, 0), None)),
('Expression', ('BoolOp', (1, 0), ('And',), [('Name', (1, 0), 'a', ('Load',)), ('Name', (1, 6), 'b', ('Load',))])),
('Expression', ('BinOp', (1, 0), ('Name', (1, 0), 'a', ('Load',)), ('Add',), ('Name', (1, 4), 'b', ('Load',)))),
('Expression', ('UnaryOp', (1, 0), ('Not',), ('Name', (1, 4), 'v', ('Load',)))),
('Expression', ('Lambda', (1, 0), ('arguments', [], None, [], [], None, []), ('NameConstant', (1, 7), None))),
('Expression', ('Dict', (1, 0), [('Num', (1, 2), 1)], [('Num', (1, 4), 2)])),
('Expression', ('Dict', (1, 0), [], [])),
('Expression', ('Set', (1, 0), [('NameConstant', (1, 1), None)])),
('Expression', ('Dict', (1, 0), [('Num', (2, 6), 1)], [('Num', (4, 10), 2)])),
('Expression', ('ListComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'a', ('Load',)), [('comprehension', ('Name', (1, 7), 'b', ('Store',)), ('Name', (1, 12), 'c', ('Load',)), [('Name', (1, 17), 'd', ('Load',))])])),
('Expression', ('GeneratorExp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'a', ('Load',)), [('comprehension', ('Name', (1, 7), 'b', ('Store',)), ('Name', (1, 12), 'c', ('Load',)), [('Name', (1, 17), 'd', ('Load',))])])),
('Expression', ('Compare', (1, 0), ('Num', (1, 0), 1), [('Lt',), ('Lt',)], [('Num', (1, 4), 2), ('Num', (1, 8), 3)])),
('Expression', ('Call', (1, 0), ('Name', (1, 0), 'f', ('Load',)), [('Num', (1, 2), 1), ('Num', (1, 4), 2)], [('keyword', 'c', ('Num', (1, 8), 3))], ('Name', (1, 11), 'd', ('Load',)), ('Name', (1, 15), 'e', ('Load',)))),
('Expression', ('Num', (1, 0), 10)),
('Expression', ('Str', (1, 0), 'string')),
('Expression', ('Attribute', (1, 0), ('Name', (1, 0), 'a', ('Load',)), 'b', ('Load',))),
('Expression', ('Subscript', (1, 0), ('Name', (1, 0), 'a', ('Load',)), ('Slice', ('Name', (1, 2), 'b', ('Load',)), ('Name', (1, 4), 'c', ('Load',)), None), ('Load',))),
('Expression', ('Name', (1, 0), 'v', ('Load',))),
('Expression', ('List', (1, 0), [('Num', (1, 1), 1), ('Num', (1, 3), 2), ('Num', (1, 5), 3)], ('Load',))),
('Expression', ('List', (1, 0), [], ('Load',))),
('Expression', ('Tuple', (1, 0), [('Num', (1, 0), 1), ('Num', (1, 2), 2), ('Num', (1, 4), 3)], ('Load',))),
('Expression', ('Tuple', (1, 0) if is_cli else (1, 1), [('Num', (1, 1), 1), ('Num', (1, 3), 2), ('Num', (1, 5), 3)], ('Load',))),
('Expression', ('Tuple', (1, 0), [], ('Load',))),
('Expression', ('Call', (1, 0), ('Attribute', (1, 0), ('Attribute', (1, 0), ('Attribute', (1, 0), ('Name', (1, 0), 'a', ('Load',)), 'b', ('Load',)), 'c', ('Load',)), 'd', ('Load',)), [('Subscript', (1, 8), ('Attribute', (1, 8), ('Name', (1, 8), 'a', ('Load',)), 'b', ('Load',)), ('Slice', ('Num', (1, 12), 1), ('Num', (1, 14), 2), None), ('Load',))], [], None, None)),
]
eval_results = eval_results_stdlib + [
('Expression', ('Lambda', (1, 0), ('arguments', [('arg', (1, 7), 'x', None)], None, [], [], None, []), ('Name', (1, 10), 'x', ('Load',)))),
('Expression', ('Lambda', (1, 0), ('arguments', [('arg', (1, 7), 'x', None)], None, [], [], None, []), ('Yield', (1, 10) if is_cli else (1, 11), ('Name', (1, 17), 'x', ('Load',))))),
('Expression', ('GeneratorExp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'a', ('Load',)), [('comprehension', ('Name', (1, 7), 'b', ('Store',)), ('Name', (1, 12), 'c', ('Load',)), []), ('comprehension', ('Name', (1, 18), 'd', ('Store',)), ('Name', (1, 23), 'e', ('Load',)), []), ('comprehension', ('Name', (1, 29), 'f', ('Store',)), ('Name', (1, 34), 'g', ('Load',)), [])])),
('Expression', ('GeneratorExp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'a', ('Load',)), [('comprehension', ('Name', (1, 7), 'b', ('Store',)), ('Name', (1, 12), 'c', ('Load',)), []), ('comprehension', ('Name', (1, 18), 'c', ('Store',)), ('Name', (1, 23), 'd', ('Load',)), [])])),
('Expression', ('GeneratorExp', (1, 0) if is_cli else (1, 1), ('Yield', (1, 1) if is_cli else (1, 2), ('Name', (1, 8), 'i', ('Load',))), [('comprehension', ('Name', (1, 15), 'i', ('Store',)), ('Call', (1, 20), ('Name', (1, 20), 'range', ('Load',)), [('Num', (1, 26), 5)], [], None, None), [])])),
('Expression', ('Subscript', (1, 0), ('Name', (1, 0), 'a', ('Load',)), ('Index', ('Ellipsis', (1, 1) if is_cli else (1, 2))), ('Load',))),
('Expression', ('Subscript', (1, 0), ('Name', (1, 0), 'a', ('Load',)), ('Index', ('Num', (1, 1) if is_cli else (1, 2), 1)), ('Load',))),
('Expression', ('Set', (1, 0), [('Name', (1, 1), 'a', ('Load',)), ('Name', (1, 3), 'b', ('Load',)), ('Name', (1, 5), 'c', ('Load',))])),
('Expression', ('DictComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'k', ('Load',)), ('Name', (1, 3), 'v', ('Load',)), [('comprehension', ('Tuple', (1, 9), [('Name', (1, 9), 'k', ('Store',)), ('Name', (1, 11), 'v', ('Store',))], ('Store',)), ('Name', (1, 16), 'li', ('Load',)), [])])),
('Expression', ('SetComp', (1, 0) if is_cli else (1, 1), ('Name', (1, 1), 'e', ('Load',)), [('comprehension', ('Name', (1, 7), 'e', ('Store',)), ('Name', (1, 12), 'li', ('Load',)), [])])),
]

run_test(__name__)
