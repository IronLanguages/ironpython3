# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest

from iptest import IronPythonTestCase, skipUnlessIronPython, is_cli, is_mono, is_netcoreapp, run_test, stdout_trapper

class HelpTest(IronPythonTestCase):
    def run_help(self, o):
        with stdout_trapper() as output:
            help(o)
        return os.linesep.join(output.messages)

    @unittest.skipIf(is_mono, "https://github.com/mono/mono/issues/17192")
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
        self.assertIn('Format(format: str, arg0: object) -> str', x)
        
        x = self.run_help('u.u'.Split('u'))
        # requires std lib
        self.assertTrue('Help on Array[str] object' in x, x)

        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=4190
        from System.IO import MemoryStream
        x_class = self.run_help(MemoryStream.Write)

        x_instance = self.run_help(MemoryStream().Write)

        self.assertEqual(x_class, x_instance.replace("built-in function Write", "method_descriptor").replace(" method of {}.MemoryStream instance".format(MemoryStream.__module__), ""))

        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=11883
        self.assertEqual(dir(System).count('Action'), 1)
        with stdout_trapper() as output: 
            help(System.Action)
        
        self.assertEqual(dir(System).count('Action'), 1)
        
    @unittest.skipIf(is_mono, "https://github.com/mono/mono/issues/17192")
    def test_module(self):
        import time
        
        x = self.run_help(time)

        self.assertIn('clock(...)', x)         # should have help for our stuff
        self.assertNotIn('unichr(...)', x)     # shouldn't have bulit-in help
        self.assertNotIn('AscTime', x)         # shouldn't display CLI names
        self.assertNotIn('static', x)          # methods shouldn't be displayed as static

    def test_userfunction(self):
        def foo():
            """my help is useful"""
            
        x = self.run_help(foo)
        
        self.assertIn('my help is useful', x)
    
    def test_splat(self):
        def foo(*args): pass
        x = self.run_help(foo)
        self.assertIn('foo(*args)', x)
        self.assertIn('Help on function foo in module __main__:', x)
        
        def foo(**kwargs): pass
        x = self.run_help(foo)
        self.assertIn('foo(**kwargs)', x)

        def foo(*args, **kwargs): pass
        x = self.run_help(foo)
        self.assertIn('foo(*args, **kwargs)', x)
        
        def foo(a, *args): pass
        x = self.run_help(foo)
        self.assertIn('foo(a, *args)', x)
        
        def foo(a, *args, **kwargs): pass
        x = self.run_help(foo)
        self.assertIn('foo(a, *args, **kwargs)', x)
        
        def foo(a=7): pass
        x = self.run_help(foo)
        self.assertIn('foo(a=7', x)
        
        def foo(a=[3]): a.append(7)
        x = self.run_help(foo)
        self.assertIn('foo(a=[3]', x)
        foo()
        x = self.run_help(foo)
        self.assertIn('foo(a=[3, 7]', x)
    
    def test_builtinfunction(self):
        x = self.run_help(abs)
        self.assertIn('Return the absolute value of the argument', x)

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
            
        self.assertIn('this documentation is going to make the world a better place', x)
    

    def test_methoddescriptor(self):
        x = self.run_help(list.append)
        self.assertIn('append(...)', x)

    def test_oldstyle_class(self):
        class foo:
            """the slow lazy fox jumped over the quick brown dog..."""

        x = self.run_help(foo)            
        self.assertIn('the slow lazy fox jumped over the quick brown dog...', x)

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
        self.assertIn('my documentation', x)
    
    def test_str(self):
        x = self.run_help('abs')
        self.assertIn('Return the absolute value of the argument.', x)

    def test_user_function(self):
        def f(): pass
        out = self.run_help(f)
        
        self.assertIn('f()', out)
        self.assertIn('in module __main__:', out)
        
        # list
        def f(*args): pass
        out = self.run_help(f)
        self.assertIn('f(*args)', out)
        
        # kw dict
        def f(**kw): pass
        out = self.run_help(f)
        self.assertIn('f(**kw)', out)
        
        # list & kw dict
        def f(*args, **kwargs): pass
        out = self.run_help(f)
        self.assertIn('f(*args, **kwargs)', out)
        
        # default
        def f(abc = 3): pass
        out = self.run_help(f)
        self.assertIn('f(abc=3)', out)
        
        # mutating default value
        def f(a = [42]):
            a.append(23)
        out = self.run_help(f)
        self.assertIn('f(a=[42])', out)
        f()
        out = self.run_help(f)
        self.assertIn('f(a=[42, 23])', out)
        
        # str vs repr
        class foo(object):
            def __repr__(self): return 'abc'
            def __str__(self): return 'def'

        def f(a = foo()): pass
        out = self.run_help(f)
        self.assertIn('f(a=abc)', out)

    def test_user_method(self):
        class x:
            def f(): pass
        out = self.run_help(x.f)
        
        self.assertIn('f()', out)
        self.assertIn('in module __main__:', out)
        
        # list
        class x:
            def f(*args): pass
        out = self.run_help(x.f)
        self.assertIn('f(*args)', out)
        
        # kw dict
        class x:
            def f(**kw): pass
        out = self.run_help(x.f)
        self.assertIn('f(**kw)', out)
        
        # list & kw dict
        class x:
            def f(*args, **kwargs): pass
        out = self.run_help(x.f)
        self.assertIn('f(*args, **kwargs)', out)
        
        # default
        class x:
            def f(abc = 3): pass
        out = self.run_help(x.f)
        self.assertIn('f(abc=3)', out)
        
        # str vs repr
        class foo(object):
            def __repr__(self): return 'abc'
            def __str__(self): return 'def'

        class x:
            def f(a = foo()): pass
        out = self.run_help(x.f)
        self.assertIn('f(a=abc)', out)
    
    def test_bound_user_method(self):
        class x:
            def f(): pass
        out = self.run_help(x().f)
        
        self.assertIn('f(...)', out)
        self.assertIn('in module __main__:', out)
        
        # list
        class x:
            def f(*args): pass
        out = self.run_help(x().f)
        self.assertIn('f(*args)', out)
        
        # kw dict
        class x:
            def f(**kw): pass
        out = self.run_help(x().f)
        self.assertIn('f(...)', out)
        
        # list & kw dict
        class x:
            def f(*args, **kwargs): pass
        out = self.run_help(x().f)
        self.assertIn('f(*args, **kwargs)', out)
        
        # default
        class x:
            def f(abc = 3): pass
        out = self.run_help(x().f)
        self.assertIn('f()', out)
        
        # str vs repr
        class foo(object):
            def __repr__(self): return 'abc'
            def __str__(self): return 'def'

        class x:
            def f(a = foo()): pass
        out = self.run_help(x().f)
        self.assertIn('f()', out)

        self.assertIn('method of __main__.x instance', out)

    #TODO: @skip("stdlib") #CodePlex 17577
    def test_bound_builtin_func(self):
        x = []
        
        out = self.run_help(x.append)
        self.assertIn('Help on built-in function append', out)
        if is_cli:   # Cpython and IronPython display different help
            self.assertIn('append(self: ', out)

    @skipUnlessIronPython()
    def test_clr_addreference(self):
        import clr
        x = self.run_help(clr.AddReference)
        self.assertIn("Adds a reference to a .NET assembly.", x)

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
        
        self.assertIn("equivalent to the date and time contained in s", x)

    def test_type(self):
        """https://github.com/IronLanguages/main/issues/397"""
        x = self.run_help(type)
            
        self.assertTrue("CodeContext" not in x)
        self.assertTrue(x.count("type(") > 1, x)

    def test_builtinfunc_module(self):
        self.assertEqual([].append.__module__, None)
        self.assertEqual(min.__module__, 'builtins')
    
run_test(__name__)