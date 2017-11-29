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
import toimport
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, run_test, skipUnlessIronPython
import imp

def get_local_filename(base):
    import os
    if __file__.count(os.sep):
        return os.path.join(__file__.rsplit(os.sep, 1)[0], base)
    else:
        return base

class ImportPkgTest(IronPythonTestCase):
    def setUp(self):
        super(ImportPkgTest, self).setUp()
        self._testdir = os.path.join(self.test_dir, 'ImportTestDir')

    def tearDown(self):
        super(ImportPkgTest, self).tearDown()
        self.clean_directory(self._testdir, remove=True)

    def test_import_error(self):
        try:
            import this_module_does_not_exist
        except ImportError: pass
        else:  self.fail("should already thrown")

    def test_cp7766(self):
        import os
        if __name__=="__main__":
            self.assertEqual(type(__builtins__), type(sys))
        else:
            self.assertEqual(type(__builtins__), dict)
            
        _t_test = os.path.join(self.test_dir, "cp7766.py")
        try:
            
            self.write_to_file(_t_test, "temp = __builtins__")
        
            import cp7766
            self.assertEqual(type(cp7766.temp), dict)
            self.assertTrue(cp7766.temp != __builtins__)
            
        finally:
            import os
            os.unlink(_t_test)

    def test_generated_files(self):
        """generate test files on the fly"""

        self._testdir    = 'ImportTestDir'
        _f_init     = os.path.join(self.test_dir, self._testdir, '__init__.py')
        _f_error    = os.path.join(self.test_dir, self._testdir, 'Error.py')
        _f_gen      = os.path.join(self.test_dir, self._testdir, 'Gen.py')
        _f_module   = os.path.join(self.test_dir, self._testdir, 'Module.py')

        self.write_to_file(_f_init)
        self.write_to_file(_f_error, 'raise AssertionError()')
        self.write_to_file(_f_gen, '''
def gen():
    try:
        yield "yield inside try"
    except:
        pass
''')

        unique_line = "This is the module to test 'from ImportTestDir import Module'"

        self.write_to_file(_f_module, '''
value = %r

a = 1
b = 2
c = 3
d = 4
e = 5
f = 6
g = 7
h = 8
i = 9
j = 10
''' % unique_line)

        import sys
        result = imp.reload(sys)
        self.assertTrue(not sys.modules.__contains__("Error"))

        try:
            import ImportTestDir.Error
        except AssertionError: pass
        except ImportError:
            self.fail("Should have thrown AssertionError from Error.py")
        else:
            self.fail("Should have thrown AssertionError from Error.py")

        self.assertTrue(not sys.modules.__contains__("Error"))

        from ImportTestDir import Module

        filename = Module.__file__.lower()
        self.assertTrue(filename.endswith("module.py") or filename.endswith("module.pyc"))
        self.assertEqual(Module.__name__.lower(), "importtestdir.module")
        self.assertEqual(Module.value, unique_line)

        from ImportTestDir.Module import (a, b,
        c
        ,
        d, e,
            f, g, h
        , i, j)

        for x in range(ord('a'), ord('j')+1):
            self.assertTrue(chr(x) in dir())

        # testing double import of generators with yield inside try

        from ImportTestDir import Gen
        result = sys.modules
        #u = sys.modules.pop("iptest")
        if "ImportTestDir.Gen" in sys.modules:
            sys.modules.pop("ImportTestDir.Gen")
        from ImportTestDir import Gen
        self.assertEqual(next(Gen.gen()), "yield inside try")

    def test_import_in_nested_blocks(self):
        """using import in nested blocks"""

        #del time  # picked this up from iptest.assert_util

        def f():
            import time
            now = time.time()

        f()

        try:
            print(time)
        except NameError: pass
        else: self.fail("time should be undefined")

        def f():
            import time as t
            now = t.time()
            try:
                now = time
            except NameError: pass
            else: self.fail("time should be undefined")

        f()

        try:
            print(time)
        except NameError:  pass
        else: self.fail("time should be undefined")


        def f():
            from time import clock
            now = clock()
            try:
                now = time
            except NameError: pass
            else: self.fail("time should be undefined")
        f()

        try:
            print(time)
        except NameError:  pass
        else: self.fail("time should be undefined")

        try:
            print(clock)
        except NameError:  pass
        else: self.fail("clock should be undefined")

        def f():
            from time import clock as c
            now = c()
            try:
                now = time
            except NameError:  pass
            else: self.fail("time should be undefined")
            try:
                now = clock
            except NameError:  pass
            else: self.fail("clock should be undefined")

        f()

        try:
            print(time)
        except NameError:  pass
        else: self.fail("time should be undefined")

        try:
            print(clock)
        except NameError:  pass
        else: self.fail("clock should be undefined")


        # with closures
        def f():
            def g(): now = clock_in_closure()
            from time import clock as clock_in_closure
            g()

        f()


    def compileAndRef(self, name, filename, *args):
        if is_cli:
            import clr
            sys.path.append(sys.exec_prefix)
            self.assertEqual(self.run_csc("/nologo /t:library " + ' '.join(args) + " /out:\"" + os.path.join(sys.exec_prefix, name +".dll") + "\" \"" + filename + "\""), 0)
            clr.AddReference(name)


    #TODO: @skip("multiple_execute")
    @skipUnlessIronPython()
    def test_c1cs(self):
        """verify re-loading an assembly causes the new type to show up"""
        if not self.has_csc():
            return
        
        c1cs = get_local_filename('c1.cs')
        outp = sys.exec_prefix
        
        self.compileAndRef('c1', c1cs, '/d:BAR1')
        
        import Foo
        class c1Child(Foo.Bar): pass
        o = c1Child()
        self.assertEqual(o.Method(), "In bar1")
        
        
        self.compileAndRef('c1_b', c1cs)
        import Foo
        class c2Child(Foo.Bar): pass
        o = c2Child()
        self.assertEqual(o.Method(), "In bar2")
        # ideally we would delete c1.dll, c2.dll here so as to keep them from cluttering up
        # /Public; however, they need to be present for the peverify pass.

    #TODO: @skip("multiple_execute")
    @skipUnlessIronPython()
    def test_c2cs(self):
        """verify generic types & non-generic types mixed in the same namespace can
        successfully be used"""
        if not self.has_csc():
            return
        
        c2cs = get_local_filename('c2.cs')
        outp = sys.exec_prefix

        # first let's load Foo<T>
        self.compileAndRef('c2_a', c2cs, '/d:TEST1')
        import ImportTestNS
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo<T>')
        
        # ok, now let's get a Foo<T,Y> going on...
        self.compileAndRef('c2_b', c2cs, '/d:TEST2')
        x = ImportTestNS.Foo[int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y>')
        
        # that worked, let's make sure Foo<T> is still available...
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo<T>')

        # Lets load Foo<T,Y,Z>
        self.compileAndRef('c2_c', c2cs, '/d:TEST3')
        x = ImportTestNS.Foo[int,int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y,Z>')
        
        # make sure Foo<T> and Foo<T,Y> are still available
        x = ImportTestNS.Foo[int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y>')
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo<T>')
        
        # now let's try replacing the Foo<T> and Foo<T,Y>
        self.compileAndRef('c2_replacing_generic_Foos', c2cs, '/d:TEST6,TEST7')
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo2<T>')
        x = ImportTestNS.Foo[int, int]()
        self.assertEqual(x.Test(), 'Foo2<T,Y>')
        # and then we will put them back
        self.compileAndRef('c2_putting_back_original_generic_Foos', c2cs, '/d:TEST1,TEST2')

        # ok, now let's get plain Foo in the picture...
        self.compileAndRef('c2_d', c2cs, '/d:TEST4')
        x = ImportTestNS.Foo()
        self.assertEqual(x.Test(), 'Foo')
        
        # check the generics still work
        x = ImportTestNS.Foo[int,int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y,Z>')
        x = ImportTestNS.Foo[int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y>')
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo<T>')
        
        # now let's try replacing the non-generic Foo
        self.compileAndRef('c2_e', c2cs, '/d:TEST5')
        x = ImportTestNS.Foo()
        self.assertEqual(x.Test(), 'Foo2')
        
        # and make sure all the generics still work
        x = ImportTestNS.Foo[int,int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y,Z>')
        x = ImportTestNS.Foo[int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y>')
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo<T>')
        
        # finally, let's now replace one of the
        # generic overloads...
        self.compileAndRef('c2_f', c2cs, '/d:TEST6')
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo2<T>')

        # and make sure the old ones are still there too..
        x = ImportTestNS.Foo()
        self.assertEqual(x.Test(), 'Foo2')
        x = ImportTestNS.Foo[int,int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y,Z>')
        x = ImportTestNS.Foo[int,int]()
        self.assertEqual(x.Test(), 'Foo<T,Y>')
        
        # Load a namespace called Foo
        self.compileAndRef('c2_with_Foo_namespace', c2cs, '/d:TEST8')
        x = ImportTestNS.Foo.Bar()
        self.assertEqual(x.Test(), 'Bar')
        # Now put back the type Foo
        self.compileAndRef('c2_with_Foo_of_T_type', c2cs, '/d:TEST1')
        x = ImportTestNS.Foo[int]()
        self.assertEqual(x.Test(), 'Foo<T>')
    
    def test_has_main(self):
        self.assertTrue("__main__" in sys.modules)

    def test_multiple_packages(self):
        self._testdir        = 'ImportTestDir'
        _f_init2         = os.path.join(self.test_dir, self._testdir, '__init__.py')
        _f_longpath     = os.path.join(self.test_dir, self._testdir, 'longpath.py')
        _f_recursive    = os.path.join(self.test_dir, self._testdir, 'recursive.py')
        _f_usebuiltin   = os.path.join(self.test_dir, self._testdir, 'usebuiltin.py')

        self.write_to_file(_f_init2, '''
import recursive
import longpath
''')

        self.write_to_file(_f_longpath, '''
from iptest.assert_util import *
import pkg_q.pkg_r.pkg_s.mod_s
self.assertTrue(pkg_q.pkg_r.pkg_s.mod_s.result == "Success")
''')

        self.write_to_file(_f_recursive, '''
from iptest.assert_util import *
import pkg_a.mod_a
self.assertTrue(pkg_a.mod_a.pkg_b.mod_b.pkg_c.mod_c.pkg_d.mod_d.result == "Success")
''')

        self.write_to_file(_f_usebuiltin, '''
x = max(3,5)
x = min(3,5)
min = x

x = cmp(min, x)

cmp = 17
del(cmp)

dir = 'abc'
del(dir)
''')

        _f_pkga_init    = os.path.join(self.test_dir, self._testdir, 'pkg_a', '__init__.py')
        _f_pkga_moda    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'mod_a.py')
        self.write_to_file(_f_pkga_init, '''
import __builtin__

def new_import(a,b,c,d):
    print "* pkg_a.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import
''')
        self.write_to_file(_f_pkga_moda, '''
import __builtin__

def new_import(a,b,c,d):
    print "* mod_a.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import

import pkg_b.mod_b
''')

        _f_pkgb_init    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'pkg_b','__init__.py')
        _f_pkgb_modb    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'pkg_b','mod_b.py')
        self.write_to_file(_f_pkgb_init)
        self.write_to_file(_f_pkgb_modb, 'import pkg_c.mod_c')

        _f_pkgc_init    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'pkg_b', 'pkg_c', '__init__.py')
        _f_pkgc_modc    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'pkg_b', 'pkg_c', 'mod_c.py')
        self.write_to_file(_f_pkgc_init)
        self.write_to_file(_f_pkgc_modc, '''
import __builtin__

def new_import(a,b,c,d):
    print "* mod_c.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import

import pkg_d.mod_d
''')

        _f_pkgd_init    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'pkg_b', 'pkg_c', 'pkg_d', '__init__.py')
        _f_pkgd_modd    = os.path.join(self.test_dir, self._testdir, 'pkg_a', 'pkg_b', 'pkg_c', 'pkg_d', 'mod_d.py')
        self.write_to_file(_f_pkgd_init)
        self.write_to_file(_f_pkgd_modd, '''result="Success"''')

        _f_pkgm_init    = os.path.join(self.test_dir, self._testdir, 'pkg_m', '__init__.py')
        _f_pkgm_moda    = os.path.join(self.test_dir, self._testdir, 'pkg_m', 'mod_a.py')
        _f_pkgm_modb    = os.path.join(self.test_dir, self._testdir, 'pkg_m', 'mod_b.py')
        self.write_to_file(_f_pkgm_init, 'from ImportTestDir.pkg_m.mod_b import value_b')
        self.write_to_file(_f_pkgm_moda, 'from ImportTestDir.pkg_m.mod_b import value_b')
        self.write_to_file(_f_pkgm_modb, 'value_b = "ImportTestDir.pkg_m.mod_b.value_b"')

        _f_pkgq_init    = os.path.join(self.test_dir, self._testdir, 'pkg_q', '__init__.py')
        _f_pkgr_init    = os.path.join(self.test_dir, self._testdir, 'pkg_q', 'pkg_r', '__init__.py')
        _f_pkgs_init    = os.path.join(self.test_dir, self._testdir, 'pkg_q', 'pkg_r', 'pkg_s', '__init__.py')
        _f_pkgs_mods    = os.path.join(self.test_dir, self._testdir, 'pkg_q', 'pkg_r', 'pkg_s', 'mod_s.py')
        self.write_to_file(_f_pkgq_init)
        self.write_to_file(_f_pkgr_init)
        self.write_to_file(_f_pkgs_init)
        self.write_to_file(_f_pkgs_mods, 'result="Success"')

        from ImportTestDir.pkg_m import mod_a
        self.assertEqual(mod_a.value_b, "ImportTestDir.pkg_m.mod_b.value_b")

        import ImportTestDir.usebuiltin as test
        self.assertEqual(dir(test).count('x'), 1)       # defined variable, not a builtin
        self.assertEqual(dir(test).count('min'), 1)     # defined name that overwrites a builtin
        self.assertEqual(dir(test).count('max'), 0)     # used builtin, never assigned to
        self.assertEqual(dir(test).count('cmp'), 0)     # used, assigned to, deleted, shouldn't be visibled
        self.assertEqual(dir(test).count('del'), 0)     # assigned to, deleted, never used


    @unittest.skipIf(is_mono or is_netcoreapp, 'No System.Windows.Forms support')
    @skipUnlessIronPython()
    def test_importwinform(self):
        import clr
        clr.AddReferenceByPartialName("System.Windows.Forms")
        import System.Windows.Forms as TestWinForms
        form = TestWinForms.Form()
        form.Text = "Hello"
        self.assertTrue(form.Text == "Hello")


    @unittest.skip('Merlin 400941')
    def test_copyfrompackages(self):
        _f_pkg1 = os.path.join(self.test_dir, 'StandAlone\\Packages1.py')
        _f_pkg2 = os.path.join(self.test_dir, 'StandAlone\\Packages2.py')
        _f_mod  = os.path.join(self.test_dir, 'StandAlone\\ModPath\\IronPythonTest.py')
        self.write_to_file(_f_pkg1, '''

import sys
sys.path.append(sys.path[0] + '\\..')

from iptest.assert_util import *

import sys
sys.path.append(sys.path[0] +'\\ModPath')
import IronPythonTest

id1 = id(IronPythonTest)
self.assertEqual(dir(IronPythonTest).count('PythonFunc'), 1)

load_iron_python_test()

self.assertEqual(dir(IronPythonTest).count('PythonFunc'), 1)
self.assertEqual(dir(IronPythonTest).count('BindResult'), 0)

import IronPythonTest
id2 = id(IronPythonTest)

self.assertEqual(dir(IronPythonTest).count('PythonFunc'), 1)
self.assertEqual(dir(IronPythonTest).count('BindResult'), 1)

id3 = id(sys.modules['IronPythonTest'])

self.assertEqual(id1, id2)
self.assertEqual(id2, id3)
    ''')
        self.write_to_file(_f_pkg2, '''
import sys
sys.path.append(sys.path[0] + '\\..')
from iptest.assert_util import *

import sys
load_iron_python_test()

import IronPythonTest
id1 = id(IronPythonTest)
self.assertEqual(dir(IronPythonTest).count('BindResult'), 1)
self.assertEqual(dir(IronPythonTest).count('PythonFunc'), 0)

sys.path.append(sys.path[0] + '\\ModPath')
self.assertEqual(dir(IronPythonTest).count('PythonFunc'), 0)
self.assertEqual(dir(IronPythonTest).count('BindResult'), 1)

import IronPythonTest

self.assertEqual(dir(IronPythonTest).count('PythonFunc'), 1)
self.assertEqual(dir(IronPythonTest).count('BindResult'), 1)

id2 = id(IronPythonTest)

id3 = id(sys.modules['IronPythonTest'])

self.assertEqual(id1, id2)
self.assertEqual(id2, id3)
    ''')
    
        self.write_to_file(_f_mod, 'def PythonFunc(): pass')
        
        self.assertEqual(launch_ironpython_changing_extensions(_f_pkg1), 0)
        self.assertEqual(launch_ironpython_changing_extensions(_f_pkg2), 0)
        
        
        _imfp    = 'impmodfrmpkg'
        _f_imfp_init = os.path.join(self.test_dir, _imfp, "__init__.py")
        _f_imfp_mod  = os.path.join(self.test_dir, _imfp, "mod.py")
        _f_imfp_start = os.path.join(self.test_dir, "imfpstart.tpy")
        
        self.write_to_file(_f_imfp_init, "")
        self.write_to_file(_f_imfp_mod, "")
        self.write_to_file(_f_imfp_start, """
try:
    from impmodfrmpkg.mod import mod
except ImportError, e:
    pass
else:
    raise AssertionError("Import of mod from pkg.mod unexpectedly succeeded")
    """)
    
        self.assertEqual(launch_ironpython(_f_imfp_start), 0)
        
        # test import of package module with name bound in __init__.py
        self.write_to_file(_f_imfp_init, """
mod = 10
non_existent_mod = 20
""")
        self.write_to_file(_f_imfp_mod, """
value = "value in module"
""")
        self.write_to_file(_f_imfp_start, """
import impmodfrmpkg.mod as m
if m.value != "value in module":
    raise AssertionError("Failed to import nested module with name bound in __init__.py")
""")
        self.assertEqual(launch_ironpython(_f_imfp_start), 0)

        self.write_to_file(_f_imfp_start, """
try:
    import impmodfrmpkg.non_existent_mod as nm
except ImportError:
    pass
else:
    raise AssertionError("Import of impmodfrmpkg.non_existent_mod unexpectedly succeeded.")
""")
        self.assertEqual(launch_ironpython(_f_imfp_start), 0)

        self.write_to_file(_f_imfp_start, """
import impmodfrmpkg
if impmodfrmpkg.mod != 10:
    raise AssertionError("The value 'mod' in the package was set to module before importing it")
if impmodfrmpkg.non_existent_mod != 20:
    raise AssertionError("The 'non_existent_mod' has wrong value")
import impmodfrmpkg.mod
if impmodfrmpkg.mod.value != "value in module":
    raise AssertionError("Failed to import nested module with name bound in __init__.py")

try:
    import impmodfrmpkg.non_existent_mod
except ImportError:
    pass
else:
    raise AssertionError("Import of impmodfrmpkg.non_existent_mod unexpectedly succeeded")
""")
        self.assertEqual(launch_ironpython(_f_imfp_start), 0)

        _recimp = 'recimp'
        _f_recimp_init = os.path.join(self.test_dir, _recimp, "__init__.py")
        _f_recimp_a = os.path.join(self.test_dir, _recimp, "a.py")
        _f_recimp_b = os.path.join(self.test_dir, _recimp, "b.py")
        _f_recimp_start = os.path.join(self.test_dir, "recimpstart.tpy")
        
        self.write_to_file(_f_recimp_init, "from a import *")
        self.write_to_file(_f_recimp_a, "import b")
        self.write_to_file(_f_recimp_b, "import a")
        self.write_to_file(_f_recimp_start, "import recimp")
        
        self.assertEqual(launch_ironpython(_f_recimp_start), 0)


    def test_import_inside_exec(self):
        _f_module = os.path.join(self.test_dir, 'another.py')
        self.write_to_file(_f_module, 'a1, a2, a3, _a4 = 1, 2, 3, 4')
        
        d = {}
        exec('from another import a2', d)
        self.assertInAndNot(d, ['a2'], ['a1', 'a3', '_a4', 'another'])
        self.assertInAndNot(dir(), [], ['a1', 'a2', 'a3', '_a4', 'another'])
        
        d = {}
        exec('from another import *', d)
        self.assertInAndNot(d, ['a1', 'a2', 'a3'], ['_a4', 'another'])
        self.assertInAndNot(dir(), [], ['a1', 'a2', 'a3', '_a4', 'another'])

        d = {}
        exec('import another', d)
        self.assertInAndNot(d, ['another'], ['a1', 'a2', 'a3', '_a4'])
        
        # Also a precondition for the following tests: ensure a1 a2 a3 are not in dict
        self.assertInAndNot(dir(), [], ['a1', 'a2', 'a3', '_a4', 'another'])
        
        exec('from another import a2')
        self.assertInAndNot(dir(), ['a2'], ['a1', 'a3', '_a4'])
        
        exec('from another import *')
        self.assertInAndNot(dir(), ['a1', 'a2', 'a3'], ['_a4'])

        os.unlink(_f_module)


    def test___import___and_packages(self):
        try:
            mod_backup = dict(sys.modules)
            _f_module = os.path.join(self.test_dir, 'the_test.py')
            _f_dir    = os.path.join(self.test_dir, 'the_dir')
            _f_init   = os.path.join(_f_dir, '__init__.py')
            _f_pkg_y  = os.path.join(_f_dir, 'y.py')
            _f_y      = os.path.join(self.test_dir, 'y.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)
            self.write_to_file(_f_module, 'import the_dir.y\n')
            self.write_to_file(_f_init, '')
            self.write_to_file(_f_pkg_y, 'a=1\ny = __import__("y")\nimport sys\n')
            self.write_to_file(_f_y, 'a=2\n')
            
            import y
            self.assertEqual(y.a, 2)
            
            sys.modules = mod_backup
            mod_backup = dict(sys.modules)
            
            y = __import__('y', globals(), locals())
            self.assertEqual(y.a, 2)
            
        finally:
            sys.modules = mod_backup
            os.unlink(_f_module)
            os.unlink(_f_init)
            os.unlink(_f_pkg_y)
            os.unlink(_f_y)
            os.rmdir(_f_dir)

    #TODO: @skip("multiple_execute")
    def test_relative_imports(self):
        try:
            mod_backup = dict(sys.modules)
            _f_dir      = os.path.join(self.test_dir, 'the_dir')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_pkg_y    = os.path.join(_f_dir, 'abc.py')
            _f_pkg_x    = os.path.join(_f_dir, 'x.py')
            _f_subdir   = os.path.join(_f_dir, 'subdir')
            _f_subinit  = os.path.join(_f_subdir, '__init__.py')
            _f_subpkg_y = os.path.join(_f_subdir, 'abc.py')
            _f_subpkg_x = os.path.join(_f_subdir, 'x.py')
            _f_subpkg_z = os.path.join(_f_subdir, 'z.py')
            _f_subpkg_a = os.path.join(_f_subdir, 'a.py')
            _f_subpkg_b = os.path.join(_f_subdir, 'b.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)
            self.ensure_directory_present(_f_subdir)

            self.write_to_file(_f_init,    '')
            self.write_to_file(_f_subinit, '')
            self.write_to_file(_f_pkg_y,   'import sys\nsys.foo = "pkgy"')
            self.write_to_file(_f_subpkg_y,'import sys\nsys.foo = "subpkgy"')
            self.write_to_file(_f_pkg_x,    'from . import abc\nreload(abc)')
            self.write_to_file(_f_subpkg_x, 'from .. import abc\nreload(abc)')
            self.write_to_file(_f_subpkg_z, 'from . import abc\nreload(abc)')
            self.write_to_file(_f_subpkg_a, 'from __future__ import absolute_import\ntry:\n    import abc\nexcept ImportError:\n    import sys\n    sys.foo="error"')
            self.write_to_file(_f_subpkg_b, 'import abc\nreload(abc)')
                    
            import the_dir.subdir.a
            if sys.winver=="2.5":
                self.assertEqual(sys.foo, 'error')
            else:
                self.assertTrue(not hasattr(sys, "foo"))
            
            import the_dir.x
            self.assertEqual(sys.foo, 'pkgy')
            
            import the_dir.subdir.x
            self.assertEqual(sys.foo, 'pkgy')
                    
            import the_dir.subdir.z
            self.assertEqual(sys.foo, 'subpkgy')
                    
            import the_dir.subdir.b
            self.assertEqual(sys.foo, 'subpkgy')
                    
            del sys.foo
            
            _d_test     = 'OutterDir'
            _subdir     = 'RelTest'
            _f_o_init   = os.path.join(self.test_dir, _d_test, '__init__.py')
            _f_temp     = os.path.join(self.test_dir, _d_test, 'temp.py')
            _f_sub_init = os.path.join(self.test_dir, _d_test, _subdir, '__init__.py')
            
            self.write_to_file(_f_o_init, "from temp import foo1")
            
            
            self.write_to_file(_f_temp, '''
class foo1:
    def bar(self):
        return "foobar"

'''     )
        
            self.write_to_file(_f_sub_init, '''
from ..temp import foo1
'''     )

            from OutterDir import RelTest
            self.assertEqual(RelTest.foo1().bar(), 'foobar')
        finally:
            sys.modules = mod_backup
            os.unlink(_f_init)
            os.unlink(_f_pkg_x)
            os.unlink(_f_pkg_y)
            os.unlink(_f_subinit)
            os.unlink(_f_subpkg_x)
            os.unlink(_f_subpkg_y)
            os.unlink(_f_subpkg_z)
            os.unlink(_f_subpkg_a)
            os.unlink(_f_subpkg_b)
            os.rmdir(_f_subdir)
            os.rmdir(_f_dir)
            os.unlink(_f_o_init)
            os.unlink(_f_temp)
            os.unlink(_f_sub_init)
            os.rmdir(os.path.join(self.test_dir, _d_test, _subdir))
            os.rmdir(os.path.join(self.test_dir, _d_test))


    def test_import_globals(self):
        import os
        _f_dir      = os.path.join(self.test_dir, 'the_dir2')
        _f_x        = os.path.join(_f_dir, 'x')
        _f_init     = os.path.join(_f_x, '__init__.py')
        _f_dir_init = os.path.join(_f_dir, '__init__.py')
        _f_x_y      = os.path.join(_f_x, 'y.py')
        _f_y        = os.path.join(_f_dir, 'y.py')
        _f_test     = os.path.join(_f_dir, 'test.py')
        
        backup = dict(sys.modules)
        try:
            self.write_to_file(_f_init,    '')
            self.write_to_file(_f_dir_init,'')
            self.write_to_file(_f_x_y,   """
import sys
a = 1

class mydict(object):
    def __init__(self, items):
        self.items = items
    def __getitem__(self, index):
        return self.items[index]

sys.test1 = __import__("y").a
sys.test2 = __import__("y", {'__name__' : 'the_dir2.x.y'}).a
sys.test3 = __import__("y", mydict({'__name__' : 'the_dir2.x.y'})).a
sys.test4 = __import__("y", {}, {'__name__' : 'the_dir2.x.y'}).a
""")
            self.write_to_file(_f_y,     'a = 2')
            self.write_to_file(_f_test,  'import x.y\n')
            
            import the_dir2.test
            self.assertEqual(sys.test1, 2)
            self.assertEqual(sys.test2, 1)
            self.assertEqual(sys.test2, 1)
            self.assertEqual(sys.test2, 1)
        finally:
            sys.modules = backup
            import os
            os.unlink(_f_init)
            os.unlink(_f_dir_init)
            os.unlink(_f_x_y)
            os.unlink(_f_y)
            os.unlink(_f_test)
            os.rmdir(_f_x)
            os.rmdir(_f_dir)

    #TODO:@skip("multiple_execute")
    def test_package_back_patching(self):
        """when importing a package item the package should be updated with the child"""
        try:
            mod_backup = dict(sys.modules)
            _f_dir      = os.path.join(self.test_dir, 'the_dir')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_pkg_abc  = os.path.join(_f_dir, 'abc1.py')
            _f_pkg_xyz  = os.path.join(_f_dir, 'xyz1.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)

            self.write_to_file(_f_init,    'import abc1')
            self.write_to_file(_f_pkg_abc, 'import xyz1')
            self.write_to_file(_f_pkg_xyz, 'import sys\nsys.foo = "xyz"')
                    
            import the_dir
            x, y = the_dir.abc1, the_dir.xyz1
            from the_dir import abc1
            from the_dir import xyz1
            
            self.assertEqual(x, abc1)
            self.assertEqual(y, xyz1)
            self.assertEqual(sys.foo, 'xyz')
                    
            del sys.foo
        finally:
            sys.modules = mod_backup
            os.unlink(_f_init)
            os.unlink(_f_pkg_abc)
            os.unlink(_f_pkg_xyz)
        
    
    #TODO:@skip("multiple_execute")
    def test_pack_module_relative_collision(self):
        """when importing a package item the package should be updated with the child"""
        try:
            mod_backup = dict(sys.modules)
            _f_dir      = os.path.join(self.test_dir, 'test_dir')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_foo_dir  = os.path.join(_f_dir, 'foo')
            _f_foo_py   = os.path.join(_f_foo_dir, 'foo.py')
            _f_foo_init = os.path.join(_f_foo_dir, '__init__.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)
            self.ensure_directory_present(_f_foo_dir)

            self.write_to_file(_f_init,    'from foo import bar')
            self.write_to_file(_f_foo_py, 'bar = "BAR"')
            self.write_to_file(_f_foo_init, 'from foo import bar')
                    
            import test_dir
            self.assertEqual(test_dir.bar, 'BAR')
        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_foo_init)
            os.unlink(_f_init)
            os.rmdir(_f_foo_dir)
            os.rmdir(_f_dir)

    #TODO: @skip("multiple_execute")
    def test_from_import_publishes_in_package(self):
        try:
            mod_backup = dict(sys.modules)
            _f_dir      = os.path.join(self.test_dir, 'test_dir2')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_foo_py   = os.path.join(_f_dir, 'foo.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)

            self.write_to_file(_f_init,    'from foo import bar')
            self.write_to_file(_f_foo_py, 'bar = "BAR"')
                    
            import test_dir2
            self.assertEqual(type(test_dir2.foo), type(sys))
        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_init)
            os.rmdir(_f_dir)

    #TODO: @skip("multiple_execute")
    def test_from_import_publishes_in_package_relative(self):
        try:
            mod_backup = dict(sys.modules)
            print(self.test_dir)
            _f_dir      = os.path.join(self.test_dir, 'test_dir3')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_foo_py   = os.path.join(_f_dir, 'foof.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)

            self.write_to_file(_f_init,    'from .foof import bar')
            self.write_to_file(_f_foo_py, 'bar = "BxAR"')
                    
            import test_dir3
            print(test_dir3, dir(test_dir3))
            #print test_dir2.foof, dir(test_dir2.foof), test_dir2.foof.bar
            self.assertEqual(test_dir3.bar, 'BxAR')
            self.assertEqual(type(test_dir3.foof), type(sys))
        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_init)
            os.rmdir(_f_dir)

    #TODO: @skip("multiple_execute")
    def test_from_import_publishes_in_package_relative(self):
        try:
            mod_backup = dict(sys.modules)
            print(self.test_dir)
            _f_dir      = os.path.join(self.test_dir, 'test_dir3')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_foo_py   = os.path.join(_f_dir, 'foof.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)

            self.write_to_file(_f_init,    'from .foof import bar')
            self.write_to_file(_f_foo_py, 'bar = "BxAR"')
                    
            import test_dir3

            self.assertEqual(test_dir3.bar, 'BxAR')
            self.assertEqual(type(test_dir3.foof), type(sys))
        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_init)
            os.rmdir(_f_dir)

    #TODO:@skip("multiple_execute")
    def test_from_import_publishes_in_package_relative_self(self):
        try:
            mod_backup = dict(sys.modules)
            print(self.test_dir)
            _f_dir      = os.path.join(self.test_dir, 'test_dir4')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_foo_py   = os.path.join(_f_dir, 'foof.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)

            self.write_to_file(_f_init,    'from . import foof')
            self.write_to_file(_f_foo_py, 'bar = "BxAR"')

            import test_dir4
            print(test_dir4, dir(test_dir4))
            #self.assertEqual(test_dir4.bar, 'BxAR')
            self.assertEqual(type(test_dir4.foof), type(sys))
        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_init)
            os.rmdir(_f_dir)


    def test_multiple_relative_imports_and_package(self):
        try:
            mod_backup = dict(sys.modules)
            print(self.test_dir)
            _f_dir      = os.path.join(self.test_dir, 'test_dir5')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_foo_py   = os.path.join(_f_dir, 'foo5.py')
            _f_bar_py   = os.path.join(_f_dir, 'bar5.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)

            self.write_to_file(_f_init,    'from .foo5 import x\nfrom .bar5 import y')
            self.write_to_file(_f_foo_py, 'x = 42')
            self.write_to_file(_f_bar_py, 'y = 42')

            import test_dir5
            self.assertEqual(test_dir5.x, 42)
            self.assertEqual(test_dir5.y, 42)
        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_bar_py)
            os.unlink(_f_init)
            os.rmdir(_f_dir)


    def test_cp34551(self):
        try:
            mod_backup = dict(sys.modules)
            _f_dir      = os.path.join(self.test_dir, 'test_dir6')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_subdir   = os.path.join(_f_dir, 'sub')
            _f_subinit  = os.path.join(_f_subdir, '__init__.py')
            _f_foo_py   = os.path.join(_f_subdir, 'foo.py')
                    
            # write the files
            self.ensure_directory_present(_f_dir)
            self.ensure_directory_present(_f_subdir)

            self.write_to_file(_f_init,  """
from .sub.foo import bar
def baz():
    pass
""")
            self.write_to_file(_f_subinit, '')
            self.write_to_file(_f_foo_py, """
def bar():
    pass
""")

            from test_dir6 import baz
            import test_dir6
            self.assertTrue(getattr(test_dir6, 'sub', None) != None)

        finally:
            sys.modules = mod_backup
            os.unlink(_f_foo_py)
            os.unlink(_f_subinit)
            os.unlink(_f_init)
            os.rmdir(_f_subdir)
            os.rmdir(_f_dir)

    def test_cp35116(self):
        try:
            mod_backup = dict(sys.modules)
            _f_dir      = os.path.join(self.test_dir, 'test_dir7')
            _f_init     = os.path.join(_f_dir, '__init__.py')
            _f_pkg1     = os.path.join(_f_dir, 'pkg1')
            _f_pkg2     = os.path.join(_f_dir, 'pkg2')
            _f_pkg1init = os.path.join(_f_pkg1, '__init__.py')
            _f_pkg2init = os.path.join(_f_pkg2, '__init__.py')
            _f_m1       = os.path.join(_f_pkg1, 'm1.py')
            _f_m2       = os.path.join(_f_pkg2, 'pkg1.py')

            # write the files
            self.ensure_directory_present(_f_dir)
            self.ensure_directory_present(_f_pkg1)
            self.ensure_directory_present(_f_pkg2)

            self.write_to_file(_f_init, "from .pkg2 import *")
            self.write_to_file(_f_pkg1init, "")
            self.write_to_file(_f_pkg2init, "from .pkg1 import bar")
            self.write_to_file(_f_m1, "foo = 42")
            self.write_to_file(_f_m2, "bar = 'fourty two'")

            import test_dir7
            self.assertEqual(test_dir7.bar, 'fourty two')

            # The following is not possible, 'sys.modules = mod_backup'
            # obfuscates content of the real sys.modules
            # self.assertEqual(test_dir7.pkg1, sys.modules['test_dir7.pkg2.pkg1'])
            # self.assertEqual(test_dir7.pkg2, sys.modules['test_dir7.pkg2'])

            self.assertEqual(test_dir7.pkg1.__name__, 'test_dir7.pkg2.pkg1')
            self.assertEqual(test_dir7.pkg2.__name__, 'test_dir7.pkg2')

            from test_dir7.pkg1.m1 import foo
            self.assertEqual(foo, 42)

            self.assertEqual(test_dir7.pkg1.__name__, 'test_dir7.pkg1')
            self.assertEqual(test_dir7.pkg2.__name__, 'test_dir7.pkg2')

        except ImportError:
            self.assertTrue(False)

        finally:
            sys.modules = mod_backup
            os.unlink(_f_m2)
            os.unlink(_f_m1)
            os.unlink(_f_pkg2init)
            os.unlink(_f_pkg1init)
            os.unlink(_f_init)
            os.rmdir(_f_pkg1)
            os.rmdir(_f_pkg2)
            os.rmdir(_f_dir)


run_test(__name__)

#TODO: figure out what to do with this
# #This cannot be placed in a test_* function as it uses 'from mod import *'
# try:
#     mod_backup = dict(sys.modules)
#     _f_module2 = os.path.join(self.test_dir, 'the_test.py')
    
#     # write the files
#     self.write_to_file(_f_module2, '''def foo(some_obj): return 3.14''')
    
#     from the_test import *
#     if foo(None) != 3.14:
#         raise AssertionError()
    
#     class Bar:
#         foo = foo
#     self.assertEqual(foo(None), Bar().foo())
    
# finally:
#     sys.modules = mod_backup
#     import os
#     os.unlink(_f_module2)

