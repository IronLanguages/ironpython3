# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os

from iptest import IronPythonTestCase, is_cli, run_test

tc = IronPythonTestCase('__init__')
tc.setUp()

##
## to test how exec related to globals/locals
##

def _contains(large, small):
    for (key, value) in small.items():
        tc.assertEqual(large[key], value)

def _not_contains(dict, *keylist):
    for key in keylist:
        tc.assertTrue(key not in dict)

## exec without in something
x = 1
y = "hello"
_contains(globals(), {"x":1, "y":"hello"})
_contains(locals(),  {"x":1, "y":"hello"})

exec("x, y")
_contains(globals(), {"x":1, "y":"hello"})
_contains(locals(),  {"x":1, "y":"hello"})

## exec with custom globals
# -- use global x, y; assign
g1 = {'x':2, 'y':'world'}
exec("global x; x, y; x = 4", g1)
_contains(globals(), {"x":1, "y":"hello"})
_contains(locals(),  {"x":1, "y":"hello"})
_contains(g1, {"x":4, "y":"world"})

exec("global x; x, y; x = x + 4", g1)
_contains(g1, {"x":8})

# -- declare global
exec("global z", g1)
_not_contains(globals(), 'z')
_not_contains(locals(), 'z')
_not_contains(g1, 'z')

# -- new global
exec("global z; z = -1", g1)
_not_contains(globals(), 'z')
_not_contains(locals(), 'z')
_contains(g1, {'z':-1})

# y is missing in g2
g2 = {'x':3}
try:
    exec("x, y", g2)
except NameError: pass
else:  tc.assertTrue(False, "should throw NameError exception")

exec("y = 'ironpython'", g2)
_contains(g2, {"x":3, "y":"ironpython"})
_contains(globals(), {"y":"hello"})
_contains(locals(),  {"y":"hello"})

## exec with custom globals, locals
g = {'x': -1, 'y': 'python' }
l = {}

# use global
exec("if x != -1: throw", g, l)
exec("if y != 'python': throw", g, l)
_not_contains(l, 'x', 'y')

# new local
exec("x = 20; z = 2", g, l)
_contains(g, {"x":-1, "y":"python"})
_contains(l, {"x":20, "z":2})

# changes
exec("global y; y = y.upper(); z = -2", g, l)
_contains(g, {'x': -1, 'y': 'PYTHON'})
_contains(l, {'x': 20, 'z': -2})

# new global
exec("global w; w = -2", g, l)
_contains(g, {'x': -1, 'y': 'PYTHON', 'w': -2})
_contains(l, {'x': 20, 'z': -2})

# x in both g and l; use it
exec("global x; x = x - 1", g, l)
_contains(g, {'x': -2, 'y': 'PYTHON', 'w': -2})
_contains(l, {'x': 20, 'z': -2})

exec("x = x + 1", g, l)
_contains(g, {'x': -2, 'y': 'PYTHON', 'w': -2})
_contains(l, {'x': 21, 'z': -2})


## Inside Function: same as last part of previous checks
def InsideFunc():
    g = {'x': -1, 'y': 'python' }
    l = {}

    # use global
    exec("if x != -1: throw", g, l)
    exec("if y != 'python': throw", g, l)
    _not_contains(l, 'x', 'y')

    # new local
    exec("x = 20; z = 2", g, l)
    _contains(g, {"x":-1, "y":"python"})
    _contains(l, {"x":20, "z":2})

    # changes
    exec("global y; y = y.upper(); z = -2", g, l)
    _contains(g, {'x': -1, 'y': 'PYTHON'})
    _contains(l, {'x': 20, 'z': -2})

    # new global
    exec("global w; w = -2", g, l)
    _contains(g, {'x': -1, 'y': 'PYTHON', 'w': -2})
    _contains(l, {'x': 20, 'z': -2})

    # x in both g and l; use it
    exec("global x; x = x - 1", g, l)
    _contains(g, {'x': -2, 'y': 'PYTHON', 'w': -2})
    _contains(l, {'x': 20, 'z': -2})

    exec("x = x + 1", g, l)
    _contains(g, {'x': -2, 'y': 'PYTHON', 'w': -2})
    _contains(l, {'x': 21, 'z': -2})


InsideFunc()


unique_global_name = 987654321
class C:
    exec('a = unique_global_name')
    exec("if unique_global_name != 987654321: raise AssertionError('cannott see unique_global_name')")

tc.assertEqual(C.a, 987654321)

def f():
    exec("if unique_global_name != 987654321: raise AssertionError('cannot see unique_global_name')")
    def g(): exec("if unique_global_name != 987654321: raise AssertionError('cannot see unique_global_name')")
    g()

f()


# exec tests

# verify passing a bad value throws...

try:
    exec(3)
except TypeError:    pass
else: tc.fail("Should already thrown (3)")

# verify exec(...) takes a code object
codeobj = compile ('1+1', '<compiled code>', 'exec')
exec(codeobj)

# verify that exec'd code has access to existing locals
qqq = 3
exec('qqq+1')

# and not to *non*-existing locals..
del qqq
try:   exec('qqq+1')
except NameError:   pass
else:  tc.fail("should already thrown (qqq+1)")

exec('qqq+1', {'qqq':99})

# Test passing alternative local and global scopes to exec.

# Explicit global and local scope.

# Functional form of exec.
myloc = {}
myglob = {}
exec("a = 1; global b; b = 1", myglob, myloc)
tc.assertTrue("a" in myloc)
tc.assertTrue("a" not in myglob)
tc.assertTrue("b" in myglob)
tc.assertTrue("b" not in myloc)

# Statement form of exec.
myloc = {}
myglob = {}
exec("a = 1; global b; b = 1", myglob, myloc)
tc.assertTrue("a" in myloc)
tc.assertTrue("a" not in myglob)
tc.assertTrue("b" in myglob)
tc.assertTrue("b" not in myloc)


# Explicit global scope implies the same local scope.

# Functional form of exec.
myloc = {}
myglob = {}
exec("a = 1; global b; b = 1", myglob)
tc.assertTrue("a" in myglob)
tc.assertTrue("a" not in myloc)
tc.assertTrue("b" in myglob)
tc.assertTrue("b" not in myloc)

# Statement form of exec.
myloc = {}
myglob = {}
exec("a = 1; global b; b = 1", myglob)
tc.assertTrue("a" in myglob)
tc.assertTrue("a" not in myloc)
tc.assertTrue("b" in myglob)
tc.assertTrue("b" not in myloc)

# Testing interesting exec cases

x = "global_x"

def TryExecG(what, glob):
    exec(what, glob)

def TryExecGL(what, glob, loc):
    exec(what, glob, loc)

class Nothing:
    pass

def MakeDict(value):
    return { 'AreEqual' : tc.assertEqual, 'AssertError' : tc.assertRaises, 'x' : value, 'str' : str }

class Mapping:
    def __init__(self, value = None):
        self.values = MakeDict(value)
    def __getitem__(self, item):
        return self.values[item]

class MyDict(dict):
    def __init__(self, value = None):
        self.values = MakeDict(value)
    def __getitem__(self, item):
        return self.values[item]

TryExecG("tc.assertEqual(x, 'global_x')", None)
TryExecGL("tc.assertEqual(x, 'global_x')", None, None)

tc.assertRaises(TypeError, TryExecG, "print(x)", Nothing())
tc.assertRaises(TypeError, TryExecGL, "print(x)", Nothing(), None)

tc.assertRaises(TypeError, TryExecG, "print(x)", Mapping())
tc.assertRaises(TypeError, TryExecGL, "print(x)", Mapping(), None)

TryExecG("AreEqual(x, 17)", MakeDict(17))
TryExecGL("AreEqual(x, 19)", MakeDict(19), None)

#TryExecG("AreEqual(x, 23)", MyDict(23))
#TryExecGL("AreEqual(x, 29)", MyDict(29), None)

TryExecGL("AreEqual(x, 31)", None, MakeDict(31))
tc.assertRaises(TypeError, TryExecGL, "print(x)", None, Nothing())

TryExecGL("AreEqual(x, 37)", None, Mapping(37))
#TryExecGL("AreEqual(x, 41)", None, MyDict(41))

# Evaluating the "in" expressions in exec statement

def f(l):
    l.append("called f")
    return {}

l = []
exec("pass", f(l))
tc.assertEqual(l, ["called f"])

def g(l):
    l.append("called g")
    return {}

l = []
exec("pass", f(l), g(l))
tc.assertEqual(l, ["called f", "called g"])

class ExecTestCase(IronPythonTestCase):

    # testing exec accepts \n, \r, \r\n as eolns
    def test_eolns(self):
        def f1(sep): exec('x = 2$y=4$'.replace('$', sep))
        def f2(sep): exec('''x = 3$y = 5$'''.replace('$', sep))
        def f3(sep): exec("exec('''x = 3$y = 5$''')".replace('$', sep))

        for x in [f1, f2, f3]:
            # http://ironpython.codeplex.com/workitem/27991
            x('\r\n')
            x('\r')

            self.assertRaises(SyntaxError, x, '\a')
            x('\n')

    def test_set_builtins(self):
        g = {}
        exec("", g, None)
        self.assertTrue('__builtins__' in g.keys())

    def test_builtins_type(self):
        x, y = {}, {}
        exec('abc = 42', x, y)
        self.assertEqual(type(x['__builtins__']), dict)

    def test_exec_locals(self):
        tc = self
        exec("""
def body():
    tc.assertEqual('anythingatall' in locals(), False)

body()
foozbab = 2
def body():
    tc.assertEqual('foozbab' in locals(), False)

body()

""")

run_test(__name__)
