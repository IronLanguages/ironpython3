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
import sys

class stdout_reader:
    def __init__(self):
        self.text = ''
    def write(self, text):
        self.text += text
    
@skip('win32')
def test_z_cli_tests():    # runs last to prevent tainting the module w/ CLR names
    import clr
    import System
    load_iron_python_test()
    from IronPythonTest import WriteOnly

    sys.stdout = stdout_reader()
    help(WriteOnly)
    if is_silverlight==False:
        help(System.IO.Compression)
    help(System.Int64)
    sys.stdout = sys.__stdout__

    sys.stdout = stdout_reader()
        
    help(System.String.Format)
    x = sys.stdout.text
        
    sys.stdout = sys.__stdout__
    Assert(x.find('Format(format: str, arg0: object) -> str') != -1)
    
    sys.stdout = stdout_reader()
    help('u.u'.Split('u'))
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    if not is_silverlight:
        # requires std lib
        Assert('Help on Array[str] object' in x)
        if not is_net40: #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24508
            Assert('Clear(...)' in x)

    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=4190
    from System.IO import MemoryStream
    try:
        sys.stdout = stdout_reader() 
        help(MemoryStream.Write)
        x_class = sys.stdout.text
    finally:
        sys.stdout = sys.__stdout__

    try:
        sys.stdout = stdout_reader() 
        help(MemoryStream().Write)
        x_instance = sys.stdout.text
    finally:
        sys.stdout = sys.__stdout__        

    if not is_silverlight:        
        AreEqual(x_class, x_instance.replace("built-in function Write", "method_descriptor"))
    else:
        AreEqual(x_class.replace(" |", "|"), 
                 x_instance.replace("built-in function", "method-descriptor").replace(" |", "|"))

    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=11883
    AreEqual(dir(System).count('Action'), 1)
    try:
        sys.stdout = stdout_reader() 
        help(System.Action)
    finally:
        sys.stdout = sys.__stdout__     
    AreEqual(dir(System).count('Action'), 1)
        
def test_module():
    import time
    sys.stdout = stdout_reader()
    
    help(time)
    
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    
    if is_silverlight==False:
        Assert(x.find('clock(...)') != -1)      # should have help for our stuff
    Assert(x.find('unichr(...)') == -1)     # shouldn't have bulit-in help
    Assert(x.find('AscTime') == -1)         # shouldn't display CLI names
    Assert(x.find('static') == -1)          # methods shouldn't be displayed as static

def test_userfunction():
    def foo():
        """my help is useful"""
        
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    
    Assert(x.find('my help is useful') != -1)
    
def test_splat():
    def foo(*args): pass
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(*args)') != -1)
    if not is_silverlight:
        Assert(x.find('Help on function foo in module __main__:') != -1)
    
    def foo(**kwargs): pass
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(**kwargs)') != -1)

    def foo(*args, **kwargs): pass
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(*args, **kwargs)') != -1)
    
    def foo(a, *args): pass
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(a, *args)') != -1)
    
    def foo(a, *args, **kwargs): pass
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(a, *args, **kwargs)') != -1)
    
    def foo(a=7): pass
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(a=7') != -1)
    
    def foo(a=[3]): a.append(7)
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(a=[3]') != -1)
    foo()
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    Assert(x.find('foo(a=[3, 7]') != -1)
    
def test_builtinfunction():
    sys.stdout = stdout_reader()
    help(abs)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    
    Assert(x.find('Return the absolute value of the argument') != -1)

def test_user_class():
    class foo(object):
        """this documentation is going to make the world a better place"""
                        
    sys.stdout = stdout_reader()
    
    class TClass(object):
        '''
        Some TClass doc...
        '''
        def __init__(self, p1, *args, **argkw):
            '''
            Some constructor doc...
            
            p1 does something
            args does something
            argkw does something
            '''
            self.member = 1
            
        def plainMethod(self):
            '''
            Doc here
            '''
            pass
            
        def plainMethodEmpty(self):
            '''
            '''
            pass
            
        def plainMethodNone(self):
            pass
    
    help(foo)
    
    #sanity checks. just make sure no execeptions
    #are thrown
    help(TClass)
    help(TClass.__init__)
    help(TClass.plainMethod)
    help(TClass.plainMethodEmpty)
    help(TClass.plainMethodNone)
    
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
        
    Assert(x.find('this documentation is going to make the world a better place') != -1)
    

def test_methoddescriptor():
    sys.stdout = stdout_reader()
    help(list.append)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    
    Assert(x.find('append(...)') != -1)

def test_oldstyle_class():
    class foo:
        """the slow lazy fox jumped over the quick brown dog..."""
                        
    sys.stdout = stdout_reader()
    help(foo)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
        
    Assert(x.find('the slow lazy fox jumped over the quick brown dog...') != -1)

def test_nodoc():
    sys.stdout = stdout_reader()
    class foo(object): pass
    
    help(foo)
    
    sys.stdout = sys.__stdout__

    sys.stdout = stdout_reader()
    class foo: pass
    
    help(foo)
    
    sys.stdout = sys.__stdout__

    sys.stdout = stdout_reader()
    def foo(): pass
    
    help(foo)
    
    sys.stdout = sys.__stdout__

def test_help_instance():
    class foo(object):
        """my documentation"""
        
    a = foo()
    
    sys.stdout = stdout_reader()
    
    help(a)
    
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
        
    Assert(x.find('my documentation') != -1)
    
def test_str():
    sys.stdout = stdout_reader()
    help('abs')
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    
    Assert(x.find('Return the absolute value of the argument.') != -1)
    
def run_help(o):
    sys.stdout = stdout_reader()
    help(o)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    return x

def test_user_function():
    def f(): pass
    out = run_help(f)
    
    Assert(out.find('f()') != -1)
    if is_silverlight==False:
        Assert(out.find('in module __main__:') != -1)
    
    # list
    def f(*args): pass
    out = run_help(f)
    Assert(out.find('f(*args)') != -1)
    
    # kw dict
    def f(**kw): pass
    out = run_help(f)
    Assert(out.find('f(**kw)') != -1)
    
    # list & kw dict
    def f(*args, **kwargs): pass
    out = run_help(f)
    Assert(out.find('f(*args, **kwargs)') != -1)
    
    # default
    def f(abc = 3): pass
    out = run_help(f)
    Assert(out.find('f(abc=3)') != -1)
    
    # mutating default value
    def f(a = [42]):
        a.append(23)
    out = run_help(f)
    Assert(out.find('f(a=[42])') != -1)
    f()
    out = run_help(f)
    Assert(out.find('f(a=[42, 23])') != -1)
    
    # str vs repr
    class foo(object):
        def __repr__(self): return 'abc'
        def __str__(self): return 'def'

    def f(a = foo()): pass
    out = run_help(f)
    Assert(out.find('f(a=abc)') != -1)

def test_user_method():
    class x:
        def f(): pass
    out = run_help(x.f)
    
    Assert(out.find('f()') != -1)
    if is_silverlight==False:
        Assert(out.find('in module __main__:') != -1)
    
    # list
    class x:
        def f(*args): pass
    out = run_help(x.f)
    Assert(out.find('f(*args)') != -1)
    
    # kw dict
    class x:
        def f(**kw): pass
    out = run_help(x.f)
    Assert(out.find('f(**kw)') != -1)
    
    # list & kw dict
    class x:
        def f(*args, **kwargs): pass
    out = run_help(x.f)
    Assert(out.find('f(*args, **kwargs)') != -1)
    
    # default
    class x:
        def f(abc = 3): pass
    out = run_help(x.f)
    Assert(out.find('f(abc=3)') != -1)
    
    # str vs repr
    class foo(object):
        def __repr__(self): return 'abc'
        def __str__(self): return 'def'

    class x:
        def f(a = foo()): pass
    out = run_help(x.f)
    Assert(out.find('f(a=abc)') != -1)
    
    if is_silverlight==False:
        Assert(out.find('unbound __main__.x method') != -1)
    
def test_bound_user_method():
    class x:
        def f(): pass
    out = run_help(x().f)
    
    Assert(out.find('f()') != -1)
    if is_silverlight==False:
        Assert(out.find('in module __main__:') != -1)
    
    # list
    class x:
        def f(*args): pass
    out = run_help(x().f)
    Assert(out.find('f(*args)') != -1)
    
    # kw dict
    class x:
        def f(**kw): pass
    out = run_help(x().f)
    Assert(out.find('f(**kw)') != -1)
    
    # list & kw dict
    class x:
        def f(*args, **kwargs): pass
    out = run_help(x().f)
    Assert(out.find('f(*args, **kwargs)') != -1)
    
    # default
    class x:
        def f(abc = 3): pass
    out = run_help(x().f)
    Assert(out.find('f(abc=3)') != -1)
    
    # str vs repr
    class foo(object):
        def __repr__(self): return 'abc'
        def __str__(self): return 'def'

    class x:
        def f(a = foo()): pass
    out = run_help(x().f)
    Assert(out.find('f(a=abc)') != -1)

    if is_silverlight==False:
        Assert(out.find('method of __main__.x instance') != -1)

@skip("stdlib") #CodePlex 17577
def test_bound_builtin_func():
    x = []
    
    out = run_help(x.append)
    Assert(out.find('Help on built-in function append') != -1)
    if is_cli:   # Cpython and IronPython display different help
        Assert(out.find('append(self: ') != -1)

@skip("win32")
def test_clr_addreference():
    sys.stdout = stdout_reader()
    help(clr.AddReference)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
    
    Assert(x.find("Adds a reference to a .NET assembly.") != -1)

@skip("win32", "silverlight", "cli64")
def test_paramrefs():
    # System.DateTime.Parse (for example) has a paramrefs in its help text which get substitued
    # by paramnames.
    import System
    x = run_help(System.DateTime.Parse)

    # help output is dependant on the console width so get rid of formatting
    x = " ".join(y.strip() for y in x.splitlines())
    
    Assert(x.find("equivalent to the date and time contained in s") != -1)

@skip("silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=20236#
def test_type():        
    sys.stdout = stdout_reader()
    help(type)
    x = sys.stdout.text
    sys.stdout = sys.__stdout__
        
    Assert("CodeContext" not in x)
    Assert(x.count("type(") > 1)

def test_builtinfunc_module():
    AreEqual([].append.__module__, None)
    AreEqual(min.__module__, '__builtin__')
    
#--MAIN------------------------------------------------------------------------
run_test(__name__)
