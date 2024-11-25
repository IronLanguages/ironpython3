# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import imp
import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_posix, path_modifier, run_test, skipUnlessIronPython
import collections

def get_builtins_dict():
    if type(__builtins__) is type(sys):
        return __builtins__.__dict__
    return __builtins__

class meta_loader(object):
    def __init__(self, value):
        self.value = value
    def load_module(self, fullname):
        if type(self.value) is Exception: raise self.value

        return self.value

class meta_importer(object):
    def __init__(self, s):
        self.s = s
    def find_module(self, fullname, path=None):
        self.s.assertIsNone(path)
        if fullname == 'does_not_exist_throw': raise Exception('hello')
        elif fullname == 'does_not_exist_abc': return meta_loader('abc')
        elif fullname == 'does_not_exist_loader_throw': return meta_loader(Exception('loader'))
        elif fullname == 'does_not_exist_None': return meta_loader(None)
        elif fullname == 'does_not_exist_X':
            class X(object):
                abc = 3
            return meta_loader(X)

class ImpTest(IronPythonTestCase):
    def setUp(self):
        super(ImpTest, self).setUp()

        self._testdir = "ImpTest"
        self._imptestdir = os.path.join(self.test_dir, self._testdir)
        self._f_init = os.path.join(self._imptestdir, "__init__.py")
        self._f_module  = os.path.join(self._imptestdir, "imptestmod.py")

        self.temp_name = ["os",
                    "os.P_WAIT",
                    "os.chmod",
                    "sys.path",
                    "xxxx"
                    ]

        # backup path values
        self.__path = sys.path[:]
        self.__path_hooks = sys.path_hooks[:]
        self.__meta_path = sys.meta_path[:]

    def tearDown(self):
        super(ImpTest, self).tearDown()
        self.clean_directory(self._imptestdir)

        # restore path values
        sys.path_importer_cache = {}
        sys.path = self.__path
        sys.path_hooks = self.__path_hooks
        sys.meta_path = self.__meta_path

    def test_imp_new_module(self):
        x = imp.new_module('abcd')
        sys.modules['abcd'] = x
        try:
            x.foo = 'bar'
            import abcd
            self.assertEqual(abcd.foo, 'bar')
        finally:
            del sys.modules['abcd']

        y = imp.new_module('\r\n')
        sys.modules['xyz'] = y
        try:
            y.foo = 'foo'
            import xyz
            self.assertEqual(xyz.foo, 'foo')
        finally:
            del sys.modules['xyz']

    def test_imp_in_exec(self):
        _imfp = 'impmodfrmpkg'
        _f_imfp_init = os.path.join(self.test_dir, _imfp, "__init__.py")
        _f_imfp_mod  = os.path.join(self.test_dir, _imfp, "mod.py")
        _f_imfp_start = os.path.join(self.test_dir, "imfpstart.tpy")

        self.write_to_file(_f_imfp_init, "")
        self.write_to_file(_f_imfp_mod, "value = 10")
        self.write_to_file(_f_imfp_start, """
try:
    from impmodfrmpkg.mod import mod
except ImportError as e:
    pass
else:
    raise AssertionError("Import of mod from pkg.mod unexpectedly succeeded")
        """)

        try:
            # import a package
            import impmodfrmpkg

            # create a dictionary like that package
            glb = {'__name__' : impmodfrmpkg.__name__, '__path__' : impmodfrmpkg.__path__}
            loc = {}

            exec('from . import mod', glb, loc)
            self.assertIn('mod', loc)

            glb = {'__name__' : impmodfrmpkg.__name__, '__path__' : impmodfrmpkg.__path__}
            loc = {}
            exec('from .mod import *', glb, loc)
            self.assertIn('value', loc)

            if is_cli:
                loc = {}
                exec('from System import *', globals(), loc)

                self.assertIn('Int32', loc)
                self.assertNotIn('Int32', globals())

                exec('from System import *')
                self.assertIn('Int32', dir())

        finally:
            self.delete_files(_f_imfp_start)
            self.clean_directory(os.path.join(self.test_dir, _imfp))

    def test_imp_basic(self):
        magic = imp.get_magic()
        suffixes = imp.get_suffixes()
        self.assertTrue(isinstance(suffixes, list))
        for suffix in suffixes:
            self.assertTrue(isinstance(suffix, tuple))
            self.assertEqual(len(suffix), 3)
        self.assertTrue((".py", "r", 1) in suffixes)

    def test_imp_package(self):
        self.write_to_file(self._f_init, "my_name = 'imp package test'")
        pf, pp, (px, pm, pt) = imp.find_module(self._testdir, [self.test_dir])
        self.assertEqual(pt, imp.PKG_DIRECTORY)
        self.assertEqual(pf, None)
        self.assertEqual(px, "")
        self.assertEqual(pm, "")
        module = imp.load_module(self._testdir, pf, pp, (px, pm, pt))
        self.assertTrue(self._testdir in sys.modules)
        self.assertEqual(module.my_name, 'imp package test')

        with path_modifier(self.test_dir):
            fm = imp.find_module(self._testdir)
        # unpack the result obtained above
        pf, pp, (px, pm, pt) = fm
        self.assertEqual(pt, imp.PKG_DIRECTORY)
        self.assertEqual(pf, None)
        self.assertEqual(px, "")
        self.assertEqual(pm, "")
        module = imp.load_module(self._testdir, pf, pp, (px, pm, pt))
        self.assertEqual(module.my_name, 'imp package test')

    def test_imp_module(self):
        self.write_to_file(self._f_module, "value = 'imp test module'")
        pf, pp, (px, pm, pt) = imp.find_module("imptestmod", [self._imptestdir])
        self.assertEqual(pt, imp.PY_SOURCE)
        self.assertTrue(pf is not None)
        module = imp.load_module("imptestmod", pf, pp, (px, pm, pt))
        self.assertEqual(module.value, 'imp test module')
        pf.close()

        with path_modifier(self._imptestdir) as p:
            fm = imp.find_module("imptestmod")
        # unpack the result obtained above
        pf, pp, (px, pm, pt) = fm
        self.assertEqual(pt, imp.PY_SOURCE)
        self.assertTrue(pf != None)
        self.assertEqual(px, ".py")
        self.assertEqual(pm, "r")
        module = imp.load_module("imptestmod", pf, pp, (px, pm, pt))
        self.assertEqual(module.value, 'imp test module')
        pf.close()

    def test_direct_module_creation(self):
        import math

        for baseMod in math, sys:
            module = type(baseMod)

            x = module.__new__(module)
            self.assertEqual(repr(x), "<module '?' (built-in)>" if is_cli else "<module '?'>")
            #self.assertEqual(x.__dict__, None)

            x.__init__('abc', 'def')
            self.assertEqual(repr(x), "<module 'abc' (built-in)>" if is_cli else "<module 'abc'>")
            self.assertEqual(x.__doc__, 'def')

            x.__init__('aaa', 'zzz')
            self.assertEqual(repr(x), "<module 'aaa' (built-in)>" if is_cli else "<module 'aaa'>")
            self.assertEqual(x.__doc__, 'zzz')

            # can't assign to module __dict__
            with self.assertRaises(AttributeError):
                x.__dict__ = {}

            # can't delete __dict__
            with self.assertRaises(AttributeError):
                del(x.__dict__)

            # init doesn't clobber dict, it just re-initializes values

            x.__dict__['foo'] = 'xyz'
            x.__init__('xyz', 'nnn')

            self.assertEqual(x.foo, 'xyz')

            # dict is lazily created on set
            x = module.__new__(module)
            x.foo = 23
            self.assertEqual(x.__dict__, {'foo':23})

            self.assertEqual(repr(x), "<module '?' (built-in)>" if is_cli else "<module '?'>")

            # can't pass wrong sub-type to new
            with self.assertRaises(TypeError):
                module.__new__(str)

            # dir on non-initialized module raises TypeError
            x = module.__new__(module)

            x.__name__ = 'module_does_not_exist_in_sys_dot_modules'
            self.assertRaises(ImportError, imp.reload, x)

    def test_redefine_import(self):
        # redefining global __import__ shouldn't change import semantics
        global __import__
        global called
        called = False
        def __import__(*args):
            global called
            called = True

        self.assertEqual(called, False)
        del __import__
        called = False

        self.assertEqual(called, False)

    def test_module_dict(self):
        currentModule = sys.modules[__name__]
        self.assertEqual(isinstance(currentModule.__dict__, collections.Mapping), True)
        self.assertEqual(type({}), type(currentModule.__dict__))
        self.assertEqual(isinstance(currentModule.__dict__, dict), True)

    def test_lock(self):
        i=0
        while i<5:
            i+=1
            if not imp.lock_held():
                self.assertRaises(RuntimeError,imp.release_lock)
                imp.acquire_lock()
            else:
                imp.release_lock()

    def test_is_frozen(self):
        for name in self.temp_name:
            f = imp.is_frozen(name)
            self.assertFalse(f)

    def test_init_frozen(self):
        for name in self.temp_name:
            f = imp.init_frozen(name)
            self.assertIsNone(f)

    def test_is_builtin(self):

        self.assertEqual(imp.is_builtin("xxx"),0)
        self.assertEqual(imp.is_builtin("12324"),0)
        self.assertEqual(imp.is_builtin("&*^^"),0)

        self.assertEqual(imp.is_builtin("dir"),0)
        self.assertEqual(imp.is_builtin("__doc__"),0)
        self.assertEqual(imp.is_builtin("__name__"),0)

        self.assertEqual(imp.is_builtin("_locle"),0)

        if is_cli:
            self.assertEqual(imp.is_builtin("_pickle"),0)
        else:
            self.assertEqual(imp.is_builtin("_pickle"),1)
        self.assertEqual(imp.is_builtin("_random"),1)

        # nt module disabled in Silverlight
        if is_posix:
            self.assertEqual(imp.is_builtin("posix"),1)
        else:
            self.assertEqual(imp.is_builtin("nt"),1)

        self.assertEqual(imp.is_builtin("_thread"),1)

        # used to be a builtin but no longer is
        self.assertEqual(imp.is_builtin("copyreg"),0)

        # supposedly you can't re-init these
        self.assertEqual(imp.is_builtin("sys"), -1)
        self.assertEqual(imp.is_builtin("builtins"), -1)

        imp.init_builtin("sys")
        imp.init_builtin("builtins")

    def test_sys_path_none_builtins(self):
        prevPath = sys.path

        #import some builtin modules not previously imported
        try:
            sys.path = [None] + prevPath
            self.assertNotIn('array', sorted(sys.modules.keys()))
            import array
            self.assertIn('array', sys.modules)

            sys.path = prevPath + [None]
            self.assertNotIn('cmath', sys.modules)
            import array
            import cmath
            self.assertIn('array', sys.modules)
            self.assertIn('cmath', sys.modules)

            sys.path = [None]
            self.assertNotIn('_bisect', sys.modules)
            import array
            import cmath
            import _bisect
            self.assertIn('array', sys.modules)
            self.assertIn('cmath', sys.modules)
            self.assertIn('_bisect', sys.modules)

        finally:
            sys.path = prevPath

    def test_sys_path_none_userpy(self):
        prevPath = sys.path

        #import a *.py file
        temp_syspath_none = os.path.join(self.test_dir, "temp_syspath_none.py")
        self.write_to_file(temp_syspath_none, "stuff = 3.14")

        try:
            sys.path = [None] + prevPath
            import temp_syspath_none
            self.assertEqual(temp_syspath_none.stuff, 3.14)

        finally:
            sys.path = prevPath
            self.delete_files(os.path.join(self.test_dir, "temp_syspath_none.py"))

    def test_sys_path_none_negative(self):
        prevPath = sys.path
        test_paths = [  [None] + prevPath,
                        prevPath + [None],
                        [None],
                    ]

        try:
            for temp_path in test_paths:

                sys.path = temp_path
                with self.assertRaises(ImportError):
                    import does_not_exist
        finally:
            sys.path = prevPath

    def test_init_builtin(self):
        r  = imp.init_builtin("c_Pickle")
        self.assertEqual(r,None)

        r  = imp.init_builtin("2345")
        self.assertEqual(r,None)
        r  = imp.init_builtin("xxxx")
        self.assertEqual(r,None)
        r  = imp.init_builtin("^$%$#@")
        self.assertEqual(r,None)

        r  = imp.init_builtin("_locale")
        self.assertTrue(r!=None)

    def test_flags(self):
        self.assertEqual(imp.SEARCH_ERROR,0)
        self.assertEqual(imp.PY_SOURCE,1)
        self.assertEqual(imp.PY_COMPILED,2)
        self.assertEqual(imp.C_EXTENSION,3)
        self.assertEqual(imp.PY_RESOURCE,4)
        self.assertEqual(imp.PKG_DIRECTORY,5)
        self.assertEqual(imp.C_BUILTIN,6)
        self.assertEqual(imp.PY_FROZEN,7)
        self.assertEqual(imp.PY_CODERESOURCE,8)

    def test_user_defined_modules(self):
        """test the importer using user-defined module types"""
        class MockModule(object):
            def __init__(self, name): self.__name__ = name
            def __repr__(self): return 'MockModule("' + self.__name__ + '")'

        TopModule = MockModule("TopModule")
        sys.modules["TopModule"] = TopModule

        SubModule = MockModule("SubModule")
        theObj = object()
        SubModule.Object = theObj
        TopModule.SubModule = SubModule
        sys.modules["TopModule.SubModule"] = SubModule

        # clear the existing names from our namespace...
        x, y = TopModule, SubModule
        del TopModule, SubModule

        # verify we can import TopModule w/ TopModule.SubModule name
        import TopModule.SubModule
        self.assertEqual(TopModule, x)
        self.assertTrue('SubModule' not in dir())

        # verify we can import Object from TopModule.SubModule
        from TopModule.SubModule import Object
        self.assertEqual(Object, theObj)

        # verify we short-circuit the lookup in TopModule if
        # we have a sys.modules entry...
        SubModule2 = MockModule("SubModule2")
        SubModule2.Object2 = theObj
        sys.modules["TopModule.SubModule"] = SubModule2
        from TopModule.SubModule import Object2
        self.assertEqual(Object2, theObj)

        del sys.modules['TopModule']
        del sys.modules['TopModule.SubModule']

    def test_constructed_module(self):
        """verify that we don't load arbitrary modules from modules, only truly nested modules"""
        ModuleType = type(sys)

        TopModule = ModuleType("TopModule")
        sys.modules["TopModule"] = TopModule

        SubModule = ModuleType("SubModule")
        SubModule.Object = object()
        TopModule.SubModule = SubModule

        with self.assertRaises(ImportError):
            import TopModule.SubModule

        del sys.modules['TopModule']

    #TODO: @skip("multiple_execute")
    def test_import_from_custom(self):
        import builtins
        try:
            class foo(object):
                b = 'abc'
            def __import__(name, globals, locals, fromlist, level):
                global received
                received = name, fromlist
                return foo()

            saved = builtins.__import__
            builtins.__import__ = __import__

            from a import b
            self.assertEqual(received, ('a', ('b', )))
        finally:
            builtins.__import__ = saved

    def test_module_name(self):
        import imp
        m = imp.new_module('foo')
        self.assertEqual(m.__str__(), "<module 'foo' (built-in)>" if is_cli else "<module 'foo'>")
        m.__name__ = 'bar'
        self.assertEqual(m.__str__(), "<module 'bar' (built-in)>" if is_cli else "<module 'bar'>")
        m.__name__ = None
        self.assertEqual(m.__str__(), "<module '?' (built-in)>" if is_cli else "<module None>")
        m.__name__ = []
        self.assertEqual(m.__str__(), "<module '?' (built-in)>" if is_cli else "<module []>")
        m.__file__ = None
        self.assertEqual(m.__str__(), "<module '?' (built-in)>" if is_cli else "<module [] from None>")
        m.__file__ = []
        self.assertEqual(m.__str__(), "<module '?' (built-in)>" if is_cli else "<module [] from []>")
        m.__file__ = 'foo.py'
        self.assertEqual(m.__str__(), "<module '?' from 'foo.py'>" if is_cli else "<module [] from 'foo.py'>")

    def test_cp7007(self):
        file_contents = '''
called = 3.14
    '''

        strange_module_names = [    "+",
                                    "+a",
                                    "a+",
                                    "++",
                                    "+++",
                                    "-",
                                    "=",
                                    "$",
                                    "^",
                                ]

        strange_file_names = [ os.path.join(self.test_dir, "cp7007", x + ".py") for x in strange_module_names ]

        for x in strange_file_names: self.write_to_file(x, file_contents)

        try:
            with path_modifier(os.path.join(self.test_dir, 'cp7007')) as p:
                for x in strange_module_names:
                    temp_mod = __import__(x)
                    self.assertEqual(temp_mod.called, 3.14)
        finally:
            self.clean_directory(os.path.join(self.test_dir, "cp7007"))

    def test_relative_control(self):
        """test various flavors of relative/absolute import and ensure the right
        arguments are delivered to __import__"""
        def myimport(*args):
            global importArgs
            importArgs = list(args)
            importArgs[1] = None    # globals, we don't care about this
            importArgs[2] = None    # locals, we don't care about this either

            # we'll pull values out of this class on success, but that's not
            # the important part
            class X:
                abc = 3
                absolute_import = 2
                bar = 5
            return X
        old_import = get_builtins_dict()['__import__']
        try:
            get_builtins_dict()['__import__'] = myimport

            import abc
            self.assertEqual(importArgs, ['abc', None, None, None, 0])

            from . import abc
            self.assertEqual(importArgs, ['', None, None, ('abc',), 1])

            from .. import abc
            self.assertEqual(importArgs, ['', None, None, ('abc',), 2])

            from ... import abc
            self.assertEqual(importArgs, ['', None, None, ('abc',), 3])

            from ...d import abc
            self.assertEqual(importArgs, ['d', None, None, ('abc',), 3])

            from ...d import (abc, bar)
            self.assertEqual(importArgs, ['d', None, None, ('abc', 'bar'), 3])

            from d import (abc, bar)
            self.assertEqual(importArgs, ['d', None, None, ('abc', 'bar'), 0])

            code = """from __future__ import absolute_import\nimport abc"""
            exec(code, globals(), locals())
            self.assertEqual(importArgs, ['abc', None, None, None, 0])

            def f():exec("from import abc")
            self.assertRaises(SyntaxError, f)

        finally:
            get_builtins_dict()['__import__'] = old_import

    #TODO:@skip("multiple_execute") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=26829
    def test_import_relative_error(self):
        with self.assertRaises(SystemError):
            exec('from . import *')

    @unittest.skip('No access to CPython stdlib')
    def test_import_hooks_import_precence(self):
        """__import__ takes precedence over import hooks"""
        global myimpCalled
        myimpCalled = None
        class myimp(object):
            def find_module(self, fullname, path=None):
                global myimpCalled
                myimpCalled = fullname, path

        def myimport(*args):
            return 'myimport'

        import distutils
        import distutils.command
        mi = myimp()
        sys.meta_path.append(mi)
        builtinimp = get_builtins_dict()['__import__']
        try:
            get_builtins_dict()['__import__'] = myimport

            import abc
            self.assertEqual(abc, 'myimport')
            self.assertEqual(myimpCalled, None)

            # reload on a built-in hits the loader protocol
            imp.reload(distutils)
            self.assertEqual(myimpCalled, ('distutils', None))

            imp.reload(distutils.command)
            self.assertEqual(myimpCalled[0], 'distutils.command')
            self.assertEqual(myimpCalled[1][0][-7:], 'distutils')
        finally:
            get_builtins_dict()['__import__'] = builtinimp
            sys.meta_path.remove(mi)

    def test_import_hooks_bad_importer(self):
        class bad_importer(object): pass

        mi = bad_importer()
        sys.path.append(mi)
        try:
            def f(): import does_not_exist

            self.assertRaises(ImportError, f)
        finally:
            sys.path.remove(mi)

        sys.path.append(None)
        try:
            def f(): import does_not_exist

            self.assertRaises(ImportError, f)
        finally:
            sys.path.remove(None)

        class inst_importer(object): pass

        mi = inst_importer()
        def f(*args): raise Exception()
        mi.find_module = f
        sys.path.append(mi)
        try:
            def f(): import does_not_exist

            self.assertRaises(ImportError, f)
        finally:
            sys.path.remove(mi)

    def test_import_hooks_importer(self):
        """importer tests - verify the importer gets passed correct values, handles
        errors coming back out correctly"""
        global myimpCalled
        myimpCalled = None

        class myimp(object):
            def find_module(self, fullname, path=None):
                global myimpCalled
                myimpCalled = fullname, path
                if fullname == 'does_not_exist_throw':
                    raise Exception('hello')

        mi = myimp()
        sys.meta_path.append(mi)
        try:
            with self.assertRaises(ImportError):
                import does_not_exist

            self.assertEqual(myimpCalled, ('does_not_exist', None))

            with self.assertRaises(ImportError):
                from testpkg1 import blah

            self.assertEqual(type(myimpCalled[1]), list)
            self.assertEqual(myimpCalled[0], 'testpkg1.blah')
            self.assertEqual(myimpCalled[1][0][-8:], 'testpkg1')

            def f(): import does_not_exist_throw

            self.assertRaisesMessage(Exception, 'hello', f)
        finally:
            sys.meta_path.remove(mi)

    #TODO: @skip("multiple_execute")
    def test_import_hooks_loader(self):
        """loader tests - verify the loader gets the right values, handles errors correctly"""
        global myimpCalled
        myimpCalled = None

        moduleType = type(sys)

        class myloader(object):
            loadcount = 0
            def __init__(self, fullname, path):
                self.fullname = fullname
                self.path = path
            def load_module(self, fullname):
                if fullname == 'does_not_exist_throw':
                    raise Exception('hello again')
                elif fullname == 'does_not_exist_return_none':
                    return None
                else:
                    myloader.loadcount += 1
                    module = sys.modules.setdefault(fullname, moduleType(fullname))
                    module.__file__ = '<myloader file ' + str(myloader.loadcount) + '>'
                    module.fullname = self.fullname
                    module.path = self.path
                    module.__loader__ = self
                    if fullname[-3:] == 'pkg':
                        # create a package
                        module.__path__ = [fullname]

                    return module

        class myimp(object):
            def find_module(self, fullname, path=None):
                return myloader(fullname, path)

        mi = myimp()
        sys.meta_path.append(mi)
        try:
            def f(): import does_not_exist_throw

            self.assertRaisesMessage(Exception, 'hello again', f)

            if is_cli:
                def f(): import does_not_exist_return_none
                self.assertRaises(ImportError, f)

            import does_not_exist_create
            self.assertEqual(does_not_exist_create.__file__, '<myloader file 1>')
            self.assertEqual(does_not_exist_create.fullname, 'does_not_exist_create')
            self.assertEqual(does_not_exist_create.path, None)

            imp.reload(does_not_exist_create)
            self.assertEqual(does_not_exist_create.__file__, '<myloader file 2>')
            self.assertEqual(does_not_exist_create.fullname, 'does_not_exist_create')
            self.assertEqual(does_not_exist_create.path, None)

            import testpkg1.does_not_exist_create_sub
            self.assertEqual(testpkg1.does_not_exist_create_sub.__file__, '<myloader file 3>')
            self.assertEqual(testpkg1.does_not_exist_create_sub.fullname, 'testpkg1.does_not_exist_create_sub')
            self.assertEqual(testpkg1.does_not_exist_create_sub.path[0][-8:], 'testpkg1')

            imp.reload(testpkg1.does_not_exist_create_sub)
            self.assertEqual(testpkg1.does_not_exist_create_sub.__file__, '<myloader file 4>')
            self.assertEqual(testpkg1.does_not_exist_create_sub.fullname, 'testpkg1.does_not_exist_create_sub')
            self.assertEqual(testpkg1.does_not_exist_create_sub.path[0][-8:], 'testpkg1')

            import does_not_exist_create_pkg.does_not_exist_create_subpkg
            self.assertEqual(does_not_exist_create_pkg.__file__, '<myloader file 5>')
            self.assertEqual(does_not_exist_create_pkg.fullname, 'does_not_exist_create_pkg')
        finally:
            sys.meta_path.remove(mi)

    def test_path_hooks(self):
        import toimport
        def prepare(f):
            sys.path_importer_cache = {}
            sys.path_hooks = [f] + old_path_hooks
            if 'toimport' in sys.modules: del sys.modules['toimport']

        old_path_hooks = sys.path_hooks[:]
        try:
            def hook(*args):  raise Exception('hello')
            prepare(hook)
            def f(): import toimport
            self.assertRaisesMessage(Exception, 'hello', f)

            # ImportError shouldn't propagate out
            def hook(*args):  raise ImportError('foo')
            prepare(hook)
            f()

            # returning None
            def hook(*args): pass
            prepare(hook)
            if is_cli:
                f()
            else:
                self.assertRaises(ImportError, f)
        finally:
            sys.path_hooks = old_path_hooks

    def common_meta_import_tests(self):
        def f(): import does_not_exist_throw
        self.assertRaisesMessage(Exception, 'hello', f)

        if is_cli:
            import does_not_exist_abc
            self.assertEqual(does_not_exist_abc, 'abc')

        def f(): import does_not_exist_loader_throw
        self.assertRaisesMessage(Exception, 'loader', f)

        def f(): import does_not_exist_loader_None
        self.assertRaisesMessage(ImportError, "No module named 'does_not_exist_loader_None'", f)

        if is_cli:
            from does_not_exist_X import abc
            self.assertEqual(abc, 3)

    def test_path_hooks_importer_and_loader(self):
        path = list(sys.path)
        hooks = list(sys.path_hooks)
        try:
            sys.path.append('<myname>')
            def hook(name):
                if name == "<myname>":
                    return meta_importer(self)
            sys.path_hooks.append(hook)

            self.common_meta_import_tests()
        finally:
            sys.path = path
            sys.path_hooks = hooks

    def test_meta_path(self):
        mi = meta_importer(self)
        sys.meta_path.append(mi)
        try:
            self.common_meta_import_tests()
        finally:
            sys.meta_path.remove(mi)

    def test_custom_meta_path(self):
        """most special methods invoked by the runtime from Python only invoke on the type, not the instance.
        the import methods will invoke on instances including using __getattribute__ for resolution or on
        old-style classes.   This test verifies we do a full member lookup to find these methods"""
        finder = None
        loader = None
        class K(object):
            def __init__(self):
                self.calls = []
            def __getattribute__(self, name):
                if name != 'calls': self.calls.append(name)
                if name == 'find_module': return finder
                if name == 'load_module': return loader
                return object.__getattribute__(self, name)

        loaderInst = K()
        sys.meta_path.insert(0, loaderInst)

        def ok_finder(name, path=None):
            loaderInst.calls.append( (name, path) )
            return loaderInst

        def ok_loader(name):
            loaderInst.calls.append(name)
            return sys.modules.setdefault('xyz', 'abc')

        try:
            # dynamically resolve find_module to None
            with self.assertRaises(TypeError):
                import xyz
            self.assertEqual(loaderInst.calls, ['find_module'] if is_cli else ['find_spec', 'find_module'])
            loaderInst.calls = []

            # dynamically resolve find_module to a function,
            # and load_module to None.
            finder = ok_finder
            with self.assertRaises(TypeError):
                import xyz
            self.assertEqual(loaderInst.calls, ['find_module', ('xyz', None), 'load_module'] if is_cli else ['find_spec', 'find_module', ('xyz', None), 'get_filename', 'is_package', 'exec_module', 'load_module'])
            loaderInst.calls = []

            loader = ok_loader
            import xyz

            self.assertEqual(xyz, 'abc')
            self.assertEqual(loaderInst.calls, ['find_module', ('xyz', None), 'load_module', 'xyz'] if is_cli else ['find_spec', 'find_module', ('xyz', None), 'get_filename', 'is_package', 'exec_module', 'load_module', 'xyz'])
        finally:
            sys.meta_path.remove(loaderInst)

    def test_import_kw_args(self):
        self.assertEqual(__import__(name = 'sys', globals = globals(), locals = locals(), fromlist = [], level = 0), sys)

    def test_import_list_empty_string(self):
        """importing w/ an empty string in the from list should be ignored"""
        x = __import__('testpkg1', {}, {}, [''])
        self.assertTrue(not '' in dir(x))

    def test_cp7050(self):
        '''
        This test case complements CPython's test_import.py
        '''
        with self.assertRaises(ImportError):
            import Nt

        self.assertRaises(ImportError, __import__, "Nt")
        if is_cli:
            self.assertRaises(ImportError, __import__, "Lib")
        self.assertRaises(ImportError, __import__, "iptest.Assert_Util")

    def test_meta_path_before_builtins(self):
        """the meta path should be consulted before builtins are loaded"""
        class MyException(Exception): pass

        class K:
            def find_module(self, name, path=None):
                if name == "time": return self
                return None
            def load_module(self, name):
                raise MyException

        if 'time' in sys.modules:
            del sys.modules["time"]

        loader = K()
        sys.meta_path.insert(0, loader)
        try:
            with self.assertRaises(MyException):
                import time
        finally:
            sys.meta_path.remove(loader)

        import time

    def test_file_coding(self):
        try:
            import os
            with open('test_coding_mod.py', 'wb+') as f:
                f.write(b"# coding: utf-8\nx = '\xc3\xa6ble'\n")
            with path_modifier('.'):
                import test_coding_mod
                self.assertEqual(test_coding_mod.x[0], '\xe6')
        finally:
            os.unlink('test_coding_mod.py')

        try:
            with open('test_coding_2.py', 'wb+') as f:
                f.write(b"\xef\xbb\xbf# -*- coding: utf-8 -*-\n")
                f.write(b"x = u'ABCDE'\n")
            with path_modifier('.'):
                import test_coding_2
                self.assertEqual(test_coding_2.x, 'ABCDE')
        finally:
            os.unlink('test_coding_2.py')

        try:
            with open('test_coding_3.py', 'wb+') as f:
                f.write(b"# -*- coding: utf-8 -*-\n")
                f.write(b"raise Exception()")
            f.close()
            try:
                with path_modifier('.'):
                    import test_coding_3
            except Exception as e:
                tb = sys.exc_info()[2].tb_next
                if is_cli:
                    while tb.tb_next is not None: tb = tb.tb_next # importlib has a longer traceback
                self.assertEqual(tb.tb_lineno, 2)
        finally:
            os.unlink('test_coding_3.py')

    def test_module_subtype(self):
        class x(type(sys)):
            def __init__(self): self.baz = 100
            def __getattr__(self, name):
                if name == 'qux': raise AttributeError
                return 42
            def __getattribute__(self, name):
                if name == 'foo' or name == 'qux': raise AttributeError
                if name == 'baz': return type(sys).__getattribute__(self, name)
                return 23


        a = x()
        self.assertEqual(a.foo, 42)
        self.assertEqual(a.bar, 23)
        self.assertEqual(a.baz, 100)
        self.assertRaises(AttributeError, lambda : a.qux)

        #Real *.py file
        import testpkg1.mod1
        class x(type(testpkg1.mod1)):
            def __init__(self): self.baz = 100
            def __getattr__(self, name):
                if name == 'qux': raise AttributeError
                return 42
            def __getattribute__(self, name):
                if name == 'foo' or name == 'qux': raise AttributeError
                if name == 'baz': return type(sys).__getattribute__(self, name)
                return 23

        a = x()
        self.assertEqual(a.foo, 42)
        self.assertEqual(a.bar, 23)
        self.assertEqual(a.baz, 100)
        self.assertRaises(AttributeError, lambda : a.qux)

        #Package
        import testpkg1
        class x(type(testpkg1)):
            def __init__(self): self.baz = 100
            def __getattr__(self, name):
                if name == 'qux': raise AttributeError
                return 42
            def __getattribute__(self, name):
                if name == 'foo' or name == 'qux': raise AttributeError
                if name == 'baz': return type(sys).__getattribute__(self, name)
                return 23

        a = x()
        self.assertEqual(a.foo, 42)
        self.assertEqual(a.bar, 23)
        self.assertEqual(a.baz, 100)
        self.assertRaises(AttributeError, lambda : a.qux)

    #TODO:@runonly("stdlib")
    def test_cp13736(self):
        import os
        _f_imp_cp13736 = os.path.join(self.test_dir, "impcp13736.py")
        shortName = _f_imp_cp13736.rsplit(os.sep, 1)[1].split(".")[0]

        self.write_to_file(_f_imp_cp13736, """
class Test(object):
    def a(self):
        return 34
""")

        try:
            import sys
            import imp

            moduleInfo = imp.find_module(shortName)
            module = imp.load_module(shortName, moduleInfo[0], moduleInfo[1], moduleInfo[2])
            t = type('Test1', (getattr(module, 'Test'),), {})
            i = t()
            self.assertEqual(i.a(), 34)

            moduleInfo[0].close()
        finally:
            self.delete_files(_f_imp_cp13736)

    def test_import_path_seperator(self):
        """verify using the path seperator in a direct call will result in an ImportError"""
        self.assertRaises(ImportError, __import__, 'iptest\\type_util')
        __import__('iptest.type_util')

    def test_load_package(self):
        import testpkg1
        pkg = imp.load_package('libcopy', testpkg1.__path__[0])
        self.assertEqual(sys.modules['libcopy'], pkg)

        with self.assertRaises(AttributeError): # AttributeError: 'NoneType' object has no attribute 'name'
            imp.load_package('some_new_pkg', 'some_path_that_does_not_and_never_will_exist')

    def test_NullImporter(self):
        self.assertEqual(imp.NullImporter.__module__, 'imp')

        sys.path.append('directory_that_does_not_exist')
        try:
            with self.assertRaises(ImportError):
                import SomeFileThatDoesNotExist

            # Changed in version 3.3: None is inserted into sys.path_importer_cache instead of an instance of NullImporter.
            self.assertTrue(sys.path_importer_cache['directory_that_does_not_exist'] is None)
        finally:
            sys.path.remove('directory_that_does_not_exist')

    def test_get_frozen_object(self):
        # frozen objects not supported, this always fails
        self.assertRaises(ImportError, imp.get_frozen_object, 'foo')

    def test_cp17459(self):
        self.assertEqual(imp.IMP_HOOK, 9)

    def test_module_getattribute(self):
        mymod = type(sys)('foo', 'bar')
        attrs = ['__delattr__', '__doc__', '__hash__', '__init__', '__new__', '__reduce__', '__reduce_ex__', '__str__']
        for attr in attrs:
            d = mymod.__dict__
            d[attr] = 42
            self.assertEqual(getattr(mymod, attr), 42)
            self.assertEqual(mymod.__getattribute__(attr), 42)
            self.assertEqual(mymod.__getattribute__(attr), getattr(mymod, attr))
            del d[attr]

        for x in dir(type(sys)):
            self.assertEqual(mymod.__getattribute__(x), getattr(mymod, x))

    @skipUnlessIronPython()
    def test_import_lookup_after(self):
        import os
        try:
            _x_mod = os.path.join(self.test_dir, "x.py")
            _y_mod = os.path.join(self.test_dir, "y.py")

            self.write_to_file(_x_mod, """
import sys
oldmod = sys.modules['y']
newmod = object()
sys.modules['y'] = newmod
""")
            self.write_to_file(_y_mod, "import x")
            import y
            self.assertEqual(type(y), object)
        finally:
            os.unlink(_x_mod)
            os.unlink(_y_mod)

    @skipUnlessIronPython()
    def test_imp_load_source(self):
        import os
        try:
            _x_mod = os.path.join(self.test_dir, "x.py")
            self.write_to_file(_x_mod, """
'''some pydoc'''
X = 3.14
    """)
            with open(_x_mod, "r") as f:
                x = imp.load_source("test_imp_load_source_x",
                                    _x_mod,
                                    f)
            self.assertEqual(x.__name__, "test_imp_load_source_x")
            self.assertEqual(x.X, 3.14)
            self.assertEqual(x.__doc__, '''some pydoc''')
        finally:
            os.unlink(_x_mod)

    @skipUnlessIronPython()
    def test_imp_load_compiled(self):
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=17459
        with self.assertRaises(FileNotFoundError):
            imp.load_compiled("", "")

    @skipUnlessIronPython()
    def test_imp_load_dynamic(self):
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=17459
        self.assertEqual(imp.load_dynamic, None)

    def test_override_dict(self):
        class M(type(sys)):
            @property
            def __dict__(self):
                return 'not a dict'
            @__dict__.setter
            def __dict__(self, value):
                global setCalled
                setCalled = True

        a = M('foo')
        self.assertEqual(a.__dict__, 'not a dict')
        a.__dict__ = 42
        self.assertEqual(setCalled, True)

        class MyDesc(object):
            def __get__(self, instance, context):
                return 'abc'

        class M(type(sys)):
            __dict__ = MyDesc()

        a = M('foo')
        self.assertEqual(a.__dict__, 'abc')

        # instance members won't be found
        class M(type(sys)): pass

        a = M('foo')
        a.__dict__['__dict__'] = 42
        self.assertEqual(type(a.__dict__), dict)

        self.assertEqual(a.__getattribute__('__dict__'), a.__dict__)

        class M(type(sys)):
            def baz(self):
                return 'hello'
            @property
            def foo(self):
                return 'hello'
            @foo.setter
            def foo(self, value):
                self.bar = value
            @foo.deleter
            def foo(self):
                del self.bar

        a = M('hello')
        self.assertEqual(a.__getattribute__('baz'), a.baz)
        self.assertEqual(a.baz(), 'hello')

        a.__setattr__('foo', 42)
        self.assertEqual(a.__dict__['bar'], 42)

        a.__delattr__('foo')
        self.assertTrue('bar' not in a.__dict__)

        # mix-in an old-style class
        class old_class:
            def old_method(self):
                return 42
            @property
            def old_prop(self):
                return 'abc'
            @old_prop.setter
            def old_prop(self, value):
                self.op = value
            @old_prop.deleter
            def old_prop(self):
                del self.op

        M.__bases__ += (old_class, )
        self.assertEqual(a.old_method(), 42)

        a.__setattr__('old_prop', 42)
        self.assertEqual(a.__dict__['op'], 42)

        a.__delattr__('old_prop')
        self.assertTrue('op' not in a.__dict__)

        # change the class
        class M2(type(sys)): pass
        a.__setattr__('__class__', M2)
        self.assertEqual(type(a), M2)
        self.assertRaisesMessage(AttributeError, "readonly attribute", a.__setattr__, '__dict__', int)
        self.assertRaisesMessage(AttributeError, "readonly attribute", a.__delattr__, '__dict__')

        # __setattr__/__delattr__ no non-derived type
        m = type(sys)('foo')
        self.assertRaisesMessage(TypeError, "__class__ assignment: only for heap types", m.__setattr__, '__class__', int)
        self.assertRaisesMessage(AttributeError, "readonly attribute", m.__setattr__, '__dict__', int)
        self.assertRaisesMessage(TypeError, "can't delete __class__ attribute", m.__delattr__, '__class__')
        self.assertRaisesMessage(AttributeError, "readonly attribute", m.__delattr__, '__dict__')

    def test_ximp_load_module(self):
        mod = imp.new_module('my_module_test')
        mod.__file__ = 'does_not_exist.py'
        sys.modules['my_module_test'] = mod

        with open('test.py', 'w+') as f:
            f.write('x = 42')

        try:
            with open('test.py') as inp_file:
                imp.load_module('my_module_test', inp_file, 'does_not_exist.py', ('', 'U', 1))
        finally:
            os.unlink('test.py')

        self.assertEqual(mod.x, 42)

    def test_import_string_from_list_cp26098(self):
        self.assertEqual(__import__('email.mime.application', globals(), locals(), 'MIMEApplication').__name__, 'email.mime.application')

    @skipUnlessIronPython()
    def test_new_builtin_modules(self):
        import clr
        clr.AddReference('IronPythonTest')
        import test_new_module
        dir(test_new_module)

        # static members should still be accessible
        self.assertEqual(test_new_module.StaticMethod(), 42)
        self.assertEqual(test_new_module.StaticField, 42)
        self.assertEqual(test_new_module.StaticProperty, 42)

        # built-in functions shouldn't appear to be bound
        self.assertEqual(test_new_module.test_method.__doc__, 'test_method() -> object%s' % os.linesep)
        self.assertEqual(test_new_module.test_method.__self__, None)

        # unassigned attributes should throw as if the callee failed to look them up
        self.assertRaises(NameError, lambda : test_new_module.get_test_attr())

        # unassigned builtins should return the built-in as if the caller looked them up
        self.assertEqual(test_new_module.get_min(), min)

        # we should be able to assign to values
        test_new_module.test_attr = 42

        # and the built-in module should see them
        self.assertEqual(test_new_module.get_test_attr(), 42)
        self.assertEqual(test_new_module.test_attr, 42)

        # static members take precedence over things in globals
        self.assertEqual(test_new_module.test_overlap_method(), 42)
        self.assertEqual(type(test_new_module.test_overlap_type), type)

        test_new_module.inc_value()
        self.assertEqual(test_new_module.get_value(), 1)
        test_new_module.inc_value()
        self.assertEqual(test_new_module.get_value(), 2)

        # can't access private fields
        self.assertRaises(AttributeError, lambda : test_new_module._value)

run_test(__name__)
if __name__ == '__main__':
    from iptest.file_util import delete_all_f
    delete_all_f(__name__, remove_folders=True)
