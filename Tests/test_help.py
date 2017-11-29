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

import os
import sys
import unittest

from iptest import IronPythonTestCase, skipUnlessIronPython, is_cli, is_mono, is_netcoreapp, run_test, stdout_trapper

class HelpTest(IronPythonTestCase):
    def run_help(self, o):
        with stdout_trapper() as output:
            help(o)
        return os.linesep.join(output.messages)

    @skipUnlessIronPython()
    def test_z_cli_tests(self):    # runs last to prevent tainting the module w/ CLR names
        import clr
        import System
        self.load_iron_python_test()
        from IronPythonTest import WriteOnly
        if is_netcoreapp:
            clr.AddReference("System.IO.Compression")

        with stdout_trapper() as output:
            help(WriteOnly)
            help(System.IO.Compression)
            help(System.Int64)
        
        x = self.run_help(System.String.Format)
        self.assertTrue(x.find('Format(format: str, arg0: object) -> str') != -1)
        
        x = self.run_help('u.u'.Split('u'))
        # requires std lib
        self.assertTrue('Help on Array[str] object' in x, x)

        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=4190
        from System.IO import MemoryStream
        x_class = self.run_help(MemoryStream.Write)

        x_instance = self.run_help(MemoryStream().Write)

        self.assertEqual(x_class, x_instance.replace("built-in function Write", "method_descriptor"))

        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=11883
        self.assertEqual(dir(System).count('Action'), 1)
        with stdout_trapper() as output: 
            help(System.Action)
        
        self.assertEqual(dir(System).count('Action'), 1)
        
    def test_module(self):
        import time
        
        x = self.run_help(time)

        self.assertTrue(x.find('clock(...)') != -1)      # should have help for our stuff
        self.assertTrue(x.find('unichr(...)') == -1)     # shouldn't have bulit-in help
        self.assertTrue(x.find('AscTime') == -1)         # shouldn't display CLI names
        self.assertTrue(x.find('static') == -1)          # methods shouldn't be displayed as static

    def test_userfunction(self):
        def foo():
            """my help is useful"""
            
        x = self.run_help(foo)
        
        self.assertTrue(x.find('my help is useful') != -1)
    
    def test_splat(self):
        def foo(*args): pass
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(*args)') != -1)
        self.assertTrue(x.find('Help on function foo in module __main__:') != -1)
        
        def foo(**kwargs): pass
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(**kwargs)') != -1)

        def foo(*args, **kwargs): pass
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(*args, **kwargs)') != -1)
        
        def foo(a, *args): pass
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(a, *args)') != -1)
        
        def foo(a, *args, **kwargs): pass
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(a, *args, **kwargs)') != -1)
        
        def foo(a=7): pass
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(a=7') != -1)
        
        def foo(a=[3]): a.append(7)
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(a=[3]') != -1)
        foo()
        x = self.run_help(foo)
        self.assertTrue(x.find('foo(a=[3, 7]') != -1)
    
    def test_builtinfunction(self):
        x = self.run_help(abs)
        self.assertTrue(x.find('Return the absolute value of the argument') != -1)

    def test_user_class(self):
        class foo(object):
            """this documentation is going to make the world a better place"""

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
        
        with stdout_trapper() as output:
            help(foo)
            
            #sanity checks. just make sure no execeptions
            #are thrown
            help(TClass)
            help(TClass.__init__)
            help(TClass.plainMethod)
            help(TClass.plainMethodEmpty)
            help(TClass.plainMethodNone)
        
        x = os.linesep.join(output.messages)
            
        self.assertTrue(x.find('this documentation is going to make the world a better place') != -1)
    

    def test_methoddescriptor(self):
        x = self.run_help(list.append)
        self.assertTrue(x.find('append(...)') != -1)

    def test_oldstyle_class(self):
        class foo:
            """the slow lazy fox jumped over the quick brown dog..."""

        x = self.run_help(foo)            
        self.assertTrue(x.find('the slow lazy fox jumped over the quick brown dog...') != -1)

    def test_nodoc(self):
        class foo(object): pass

        with stdout_trapper() as output:
            help(foo)
        
        class foo: pass
        
        with stdout_trapper() as output:
            help(foo)
        
        def foo(): pass
        with stdout_trapper() as output:
            help(foo)
        
    def test_help_instance(self):
        class foo(object):
            """my documentation"""
            
        a = foo()
        x = self.run_help(a)
        self.assertTrue(x.find('my documentation') != -1)
    
    def test_str(self):
        x = self.run_help('abs')
        self.assertTrue(x.find('Return the absolute value of the argument.') != -1)

    def test_user_function(self):
        def f(): pass
        out = self.run_help(f)
        
        self.assertTrue(out.find('f()') != -1)
        self.assertTrue(out.find('in module __main__:') != -1)
        
        # list
        def f(*args): pass
        out = self.run_help(f)
        self.assertTrue(out.find('f(*args)') != -1)
        
        # kw dict
        def f(**kw): pass
        out = self.run_help(f)
        self.assertTrue(out.find('f(**kw)') != -1)
        
        # list & kw dict
        def f(*args, **kwargs): pass
        out = self.run_help(f)
        self.assertTrue(out.find('f(*args, **kwargs)') != -1)
        
        # default
        def f(abc = 3): pass
        out = self.run_help(f)
        self.assertTrue(out.find('f(abc=3)') != -1)
        
        # mutating default value
        def f(a = [42]):
            a.append(23)
        out = self.run_help(f)
        self.assertTrue(out.find('f(a=[42])') != -1)
        f()
        out = self.run_help(f)
        self.assertTrue(out.find('f(a=[42, 23])') != -1)
        
        # str vs repr
        class foo(object):
            def __repr__(self): return 'abc'
            def __str__(self): return 'def'

        def f(a = foo()): pass
        out = self.run_help(f)
        self.assertTrue(out.find('f(a=abc)') != -1)

    def test_user_method(self):
        class x:
            def f(): pass
        out = self.run_help(x.f)
        
        self.assertTrue(out.find('f()') != -1)
        self.assertTrue(out.find('in module __main__:') != -1)
        
        # list
        class x:
            def f(*args): pass
        out = self.run_help(x.f)
        self.assertTrue(out.find('f(*args)') != -1)
        
        # kw dict
        class x:
            def f(**kw): pass
        out = self.run_help(x.f)
        self.assertTrue(out.find('f(**kw)') != -1)
        
        # list & kw dict
        class x:
            def f(*args, **kwargs): pass
        out = self.run_help(x.f)
        self.assertTrue(out.find('f(*args, **kwargs)') != -1)
        
        # default
        class x:
            def f(abc = 3): pass
        out = self.run_help(x.f)
        self.assertTrue(out.find('f(abc=3)') != -1)
        
        # str vs repr
        class foo(object):
            def __repr__(self): return 'abc'
            def __str__(self): return 'def'

        class x:
            def f(a = foo()): pass
        out = self.run_help(x.f)
        self.assertTrue(out.find('f(a=abc)') != -1)
        
        self.assertTrue(out.find('unbound __main__.x method') != -1)
    
    def test_bound_user_method(self):
        class x:
            def f(): pass
        out = self.run_help(x().f)
        
        self.assertTrue(out.find('f()') != -1)
        self.assertTrue(out.find('in module __main__:') != -1)
        
        # list
        class x:
            def f(*args): pass
        out = self.run_help(x().f)
        self.assertTrue(out.find('f(*args)') != -1)
        
        # kw dict
        class x:
            def f(**kw): pass
        out = self.run_help(x().f)
        self.assertTrue(out.find('f(**kw)') != -1)
        
        # list & kw dict
        class x:
            def f(*args, **kwargs): pass
        out = self.run_help(x().f)
        self.assertTrue(out.find('f(*args, **kwargs)') != -1)
        
        # default
        class x:
            def f(abc = 3): pass
        out = self.run_help(x().f)
        self.assertTrue(out.find('f(abc=3)') != -1)
        
        # str vs repr
        class foo(object):
            def __repr__(self): return 'abc'
            def __str__(self): return 'def'

        class x:
            def f(a = foo()): pass
        out = self.run_help(x().f)
        self.assertTrue(out.find('f(a=abc)') != -1)

        self.assertTrue(out.find('method of __main__.x instance') != -1)

    #TODO: @skip("stdlib") #CodePlex 17577
    def test_bound_builtin_func(self):
        x = []
        
        out = self.run_help(x.append)
        self.assertTrue(out.find('Help on built-in function append') != -1)
        if is_cli:   # Cpython and IronPython display different help
            self.assertTrue(out.find('append(self: ') != -1)

    @skipUnlessIronPython()
    def test_clr_addreference(self):
        import clr
        x = self.run_help(clr.AddReference)
        self.assertTrue(x.find("Adds a reference to a .NET assembly.") != -1)

    @unittest.skipIf(is_mono, 'doc different on Mono')
    @unittest.skipIf(is_netcoreapp, 'TODO: figure out')
    @skipUnlessIronPython()
    def test_paramrefs(self):
        # System.DateTime.Parse (for example) has a paramrefs in its help text which get substitued
        # by paramnames.
        import System
        x = self.run_help(System.DateTime.Parse)

        # help output is dependant on the console width so get rid of formatting
        x = " ".join(y.strip() for y in x.splitlines())
        
        self.assertTrue(x.find("equivalent to the date and time contained in s") != -1)

    def test_type(self):
        """https://github.com/IronLanguages/main/issues/397"""
        x = self.run_help(type)
            
        self.assertTrue("CodeContext" not in x)
        self.assertTrue(x.count("type(") > 1, x)

    def test_builtinfunc_module(self):
        self.assertEqual([].append.__module__, None)
        self.assertEqual(min.__module__, '__builtin__')
    
run_test(__name__)