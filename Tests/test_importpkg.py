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

import toimport

from iptest.assert_util import *

if not is_silverlight:
    from iptest.file_util import *
    from iptest.process_util import *
    import nt


try:
    import this_module_does_not_exist
except ImportError: pass
else:  Fail("should already thrown")


@skip("silverlight")
def test_cp7766():
    if __name__=="__main__":
        AreEqual(type(__builtins__), type(sys))
    else:
        AreEqual(type(__builtins__), dict)
        
    try:
        _t_test = testpath.public_testdir + "\\cp7766.py"
        write_to_file(_t_test, "temp = __builtins__")
    
        import cp7766
        AreEqual(type(cp7766.temp), dict)
        Assert(cp7766.temp != __builtins__)
        
    finally:
        import nt
        nt.unlink(_t_test)

# generate test files on the fly
if not is_silverlight:
    _testdir    = 'ImportTestDir'
    _f_init     = path_combine(testpath.public_testdir, _testdir, '__init__.py')
    _f_error    = path_combine(testpath.public_testdir, _testdir, 'Error.py')
    _f_gen      = path_combine(testpath.public_testdir, _testdir, 'Gen.py')
    _f_module   = path_combine(testpath.public_testdir, _testdir, 'Module.py')

    write_to_file(_f_init)
    write_to_file(_f_error, 'raise AssertionError()')
    write_to_file(_f_gen, '''
def gen():
    try:
        yield "yield inside try"
    except:
        pass
''')

    unique_line = "This is the module to test 'from ImportTestDir import Module'"

    write_to_file(_f_module, '''
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
result = reload(sys)
Assert(not sys.modules.__contains__("Error"))

try:
    import ImportTestDir.Error
except AssertionError: pass
except ImportError:
    if is_silverlight:
        pass
    else:
        Fail("Should have thrown AssertionError from Error.py")
else:
    Fail("Should have thrown AssertionError from Error.py")

Assert(not sys.modules.__contains__("Error"))

if not is_silverlight:
    from ImportTestDir import Module

    filename = Module.__file__.lower()
    Assert(filename.endswith("module.py") or filename.endswith("module.pyc"))
    AreEqual(Module.__name__.lower(), "importtestdir.module")
    AreEqual(Module.value, unique_line)

    from ImportTestDir.Module import (a, b,
    c
    ,
    d, e,
        f, g, h
    , i, j)

    for x in range(ord('a'), ord('j')+1):
        Assert(chr(x) in dir())

    # testing double import of generators with yield inside try

    from ImportTestDir import Gen
    result = sys.modules
    #u = sys.modules.pop("iptest")
    g = sys.modules.pop("ImportTestDir.Gen")
    from ImportTestDir import Gen
    AreEqual(Gen.gen().next(), "yield inside try")

#########################################################################################
# using import in nested blocks

del time  # picked this up from iptest.assert_util

def f():
    import time
    now = time.time()

f()

try:
    print time
except NameError: pass
else: Fail("time should be undefined")

def f():
    import time as t
    now = t.time()
    try:
        now = time
    except NameError: pass
    else: Fail("time should be undefined")

f()

try:
    print time
except NameError:  pass
else: Fail("time should be undefined")


def f():
    from time import clock
    now = clock()
    try:
        now = time
    except NameError: pass
    else: Fail("time should be undefined")
if not is_silverlight:
    f()

try:
    print time
except NameError:  pass
else: Fail("time should be undefined")

if not is_silverlight:
    try:
        print clock
    except NameError:  pass
    else: Fail("clock should be undefined")

def f():
    from time import clock as c
    now = c()
    try:
        now = time
    except NameError:  pass
    else: Fail("time should be undefined")
    try:
        now = clock
    except NameError:  pass
    else: Fail("clock should be undefined")
if not is_silverlight:
    f()

try:
    print time
except NameError:  pass
else: Fail("time should be undefined")

if not is_silverlight:
    try:
        print clock
    except NameError:  pass
    else: Fail("clock should be undefined")


# with closures
def f():
    def g(): now = clock_in_closure()
    from time import clock as clock_in_closure
    g()

if not is_silverlight:
    f()


#########################################################################################

def get_local_filename(base):
    if __file__.count('\\'):
        return __file__.rsplit("\\", 1)[0] + '\\'+ base
    else:
        return base

def compileAndRef(name, filename, *args):
    if is_cli:
        import clr
        sys.path.append(sys.exec_prefix)
        AreEqual(run_csc("/nologo /t:library " + ' '.join(args) + " /out:\"" + sys.exec_prefix + "\"\\" + name +".dll \"" + filename + "\""), 0)
        clr.AddReference(name)


@skip("silverlight", "multiple_execute", "win32")
def test_c1cs():
    """verify re-loading an assembly causes the new type to show up"""
    if not has_csc():
        return
    
    c1cs = get_local_filename('c1.cs')
    outp = sys.exec_prefix
    
    compileAndRef('c1', c1cs, '/d:BAR1')
    
    import Foo
    class c1Child(Foo.Bar): pass
    o = c1Child()
    AreEqual(o.Method(), "In bar1")
    
    
    compileAndRef('c1_b', c1cs)
    import Foo
    class c2Child(Foo.Bar): pass
    o = c2Child()
    AreEqual(o.Method(), "In bar2")
    # ideally we would delete c1.dll, c2.dll here so as to keep them from cluttering up
    # /Public; however, they need to be present for the peverify pass.

@skip("silverlight", "multiple_execute", "win32")
def test_c2cs():
    """verify generic types & non-generic types mixed in the same namespace can
    successfully be used"""
    if not has_csc():
        return
    
    c2cs = get_local_filename('c2.cs')
    outp = sys.exec_prefix

    # first let's load Foo<T>
    compileAndRef('c2_a', c2cs, '/d:TEST1')
    import ImportTestNS
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo<T>')
    
    # ok, now let's get a Foo<T,Y> going on...
    compileAndRef('c2_b', c2cs, '/d:TEST2')
    x = ImportTestNS.Foo[int,int]()
    AreEqual(x.Test(), 'Foo<T,Y>')
    
    # that worked, let's make sure Foo<T> is still available...
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo<T>')

    # Lets load Foo<T,Y,Z>
    compileAndRef('c2_c', c2cs, '/d:TEST3')
    x = ImportTestNS.Foo[int,int,int]()
    AreEqual(x.Test(), 'Foo<T,Y,Z>')
    
    # make sure Foo<T> and Foo<T,Y> are still available
    x = ImportTestNS.Foo[int,int]()
    AreEqual(x.Test(), 'Foo<T,Y>')
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo<T>')
    
    # now let's try replacing the Foo<T> and Foo<T,Y>
    compileAndRef('c2_replacing_generic_Foos', c2cs, '/d:TEST6,TEST7')
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo2<T>')
    x = ImportTestNS.Foo[int, int]()
    AreEqual(x.Test(), 'Foo2<T,Y>')
    # and then we will put them back
    compileAndRef('c2_putting_back_original_generic_Foos', c2cs, '/d:TEST1,TEST2')

    # ok, now let's get plain Foo in the picture...
    compileAndRef('c2_d', c2cs, '/d:TEST4')
    x = ImportTestNS.Foo()
    AreEqual(x.Test(), 'Foo')
    
    # check the generics still work
    x = ImportTestNS.Foo[int,int,int]()
    AreEqual(x.Test(), 'Foo<T,Y,Z>')
    x = ImportTestNS.Foo[int,int]()
    AreEqual(x.Test(), 'Foo<T,Y>')
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo<T>')
    
    # now let's try replacing the non-generic Foo
    compileAndRef('c2_e', c2cs, '/d:TEST5')
    x = ImportTestNS.Foo()
    AreEqual(x.Test(), 'Foo2')
    
    # and make sure all the generics still work
    x = ImportTestNS.Foo[int,int,int]()
    AreEqual(x.Test(), 'Foo<T,Y,Z>')
    x = ImportTestNS.Foo[int,int]()
    AreEqual(x.Test(), 'Foo<T,Y>')
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo<T>')
    
    # finally, let's now replace one of the
    # generic overloads...
    compileAndRef('c2_f', c2cs, '/d:TEST6')
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo2<T>')

    # and make sure the old ones are still there too..
    x = ImportTestNS.Foo()
    AreEqual(x.Test(), 'Foo2')
    x = ImportTestNS.Foo[int,int,int]()
    AreEqual(x.Test(), 'Foo<T,Y,Z>')
    x = ImportTestNS.Foo[int,int]()
    AreEqual(x.Test(), 'Foo<T,Y>')
    
    # Load a namespace called Foo
    compileAndRef('c2_with_Foo_namespace', c2cs, '/d:TEST8')
    x = ImportTestNS.Foo.Bar()
    AreEqual(x.Test(), 'Bar')
    # Now put back the type Foo
    compileAndRef('c2_with_Foo_of_T_type', c2cs, '/d:TEST1')
    x = ImportTestNS.Foo[int]()
    AreEqual(x.Test(), 'Foo<T>')
    
    
if not is_silverlight:
    Assert(sys.modules.has_key("__main__"))

#########################################################################################
if not is_silverlight:
    _testdir        = 'ImportTestDir'
    _f_init2         = path_combine(testpath.public_testdir, _testdir, '__init__.py')
    _f_longpath     = path_combine(testpath.public_testdir, _testdir, 'longpath.py')
    _f_recursive    = path_combine(testpath.public_testdir, _testdir, 'recursive.py')
    _f_usebuiltin   = path_combine(testpath.public_testdir, _testdir, 'usebuiltin.py')

    write_to_file(_f_init2, '''
import recursive
import longpath
''')

    write_to_file(_f_longpath, '''
from iptest.assert_util import *
import pkg_q.pkg_r.pkg_s.mod_s
Assert(pkg_q.pkg_r.pkg_s.mod_s.result == "Success")
''')

    write_to_file(_f_recursive, '''
from iptest.assert_util import *
import pkg_a.mod_a
Assert(pkg_a.mod_a.pkg_b.mod_b.pkg_c.mod_c.pkg_d.mod_d.result == "Success")
''')

    write_to_file(_f_usebuiltin, '''
x = max(3,5)
x = min(3,5)
min = x

x = cmp(min, x)

cmp = 17
del(cmp)

dir = 'abc'
del(dir)
''')

    _f_pkga_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', '__init__.py')
    _f_pkga_moda    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'mod_a.py')
    write_to_file(_f_pkga_init, '''
import __builtin__

def new_import(a,b,c,d):
    print "* pkg_a.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import
''')
    write_to_file(_f_pkga_moda, '''
import __builtin__

def new_import(a,b,c,d):
    print "* mod_a.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import

import pkg_b.mod_b
''')

    _f_pkgb_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'pkg_b','__init__.py')
    _f_pkgb_modb    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'pkg_b','mod_b.py')
    write_to_file(_f_pkgb_init)
    write_to_file(_f_pkgb_modb, 'import pkg_c.mod_c')

    _f_pkgc_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'pkg_b', 'pkg_c', '__init__.py')
    _f_pkgc_modc    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'pkg_b', 'pkg_c', 'mod_c.py')
    write_to_file(_f_pkgc_init)
    write_to_file(_f_pkgc_modc, '''
import __builtin__

def new_import(a,b,c,d):
    print "* mod_c.py import"
    print a, d
    return old_import(a,b,c,d)

old_import = __builtin__.__import__
#__builtin__.__import__ = new_import

import pkg_d.mod_d
''')

    _f_pkgd_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'pkg_b', 'pkg_c', 'pkg_d', '__init__.py')
    _f_pkgd_modd    = path_combine(testpath.public_testdir, _testdir, 'pkg_a', 'pkg_b', 'pkg_c', 'pkg_d', 'mod_d.py')
    write_to_file(_f_pkgd_init)
    write_to_file(_f_pkgd_modd, '''result="Success"''')

    _f_pkgm_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_m', '__init__.py')
    _f_pkgm_moda    = path_combine(testpath.public_testdir, _testdir, 'pkg_m', 'mod_a.py')
    _f_pkgm_modb    = path_combine(testpath.public_testdir, _testdir, 'pkg_m', 'mod_b.py')
    write_to_file(_f_pkgm_init, 'from ImportTestDir.pkg_m.mod_b import value_b')
    write_to_file(_f_pkgm_moda, 'from ImportTestDir.pkg_m.mod_b import value_b')
    write_to_file(_f_pkgm_modb, 'value_b = "ImportTestDir.pkg_m.mod_b.value_b"')

    _f_pkgq_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_q', '__init__.py')
    _f_pkgr_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_q', 'pkg_r', '__init__.py')
    _f_pkgs_init    = path_combine(testpath.public_testdir, _testdir, 'pkg_q', 'pkg_r', 'pkg_s', '__init__.py')
    _f_pkgs_mods    = path_combine(testpath.public_testdir, _testdir, 'pkg_q', 'pkg_r', 'pkg_s', 'mod_s.py')
    write_to_file(_f_pkgq_init)
    write_to_file(_f_pkgr_init)
    write_to_file(_f_pkgs_init)
    write_to_file(_f_pkgs_mods, 'result="Success"')

    from ImportTestDir.pkg_m import mod_a
    AreEqual(mod_a.value_b, "ImportTestDir.pkg_m.mod_b.value_b")

    import ImportTestDir.usebuiltin as test
    AreEqual(dir(test).count('x'), 1)       # defined variable, not a builtin
    AreEqual(dir(test).count('min'), 1)     # defined name that overwrites a builtin
    AreEqual(dir(test).count('max'), 0)     # used builtin, never assigned to
    AreEqual(dir(test).count('cmp'), 0)     # used, assigned to, deleted, shouldn't be visibled
    AreEqual(dir(test).count('del'), 0)     # assigned to, deleted, never used

#########################################################################################
@skip("silverlight")
@skip("win32")
def test_importwinform():
    import clr
    clr.AddReferenceByPartialName("System.Windows.Forms")
    import System.Windows.Forms as TestWinForms
    form = TestWinForms.Form()
    form.Text = "Hello"
    Assert(form.Text == "Hello")


#@skip("win32", "silverlight")
@disabled("Merlin 400941")
def test_copyfrompackages():
    _f_pkg1 = path_combine(testpath.public_testdir, 'StandAlone\\Packages1.py')
    _f_pkg2 = path_combine(testpath.public_testdir, 'StandAlone\\Packages2.py')
    _f_mod  = path_combine(testpath.public_testdir, 'StandAlone\\ModPath\\IronPythonTest.py')
    write_to_file(_f_pkg1, '''

import sys
sys.path.append(sys.path[0] + '\\..')

from iptest.assert_util import *

import sys
sys.path.append(sys.path[0] +'\\ModPath')
import IronPythonTest

id1 = id(IronPythonTest)
AreEqual(dir(IronPythonTest).count('PythonFunc'), 1)

load_iron_python_test()

AreEqual(dir(IronPythonTest).count('PythonFunc'), 1)
AreEqual(dir(IronPythonTest).count('BindResult'), 0)

import IronPythonTest
id2 = id(IronPythonTest)

AreEqual(dir(IronPythonTest).count('PythonFunc'), 1)
AreEqual(dir(IronPythonTest).count('BindResult'), 1)

id3 = id(sys.modules['IronPythonTest'])

AreEqual(id1, id2)
AreEqual(id2, id3)
    ''')
    write_to_file(_f_pkg2, '''
import sys
sys.path.append(sys.path[0] + '\\..')
from iptest.assert_util import *

import sys
load_iron_python_test()

import IronPythonTest
id1 = id(IronPythonTest)
AreEqual(dir(IronPythonTest).count('BindResult'), 1)
AreEqual(dir(IronPythonTest).count('PythonFunc'), 0)

sys.path.append(sys.path[0] + '\\ModPath')
AreEqual(dir(IronPythonTest).count('PythonFunc'), 0)
AreEqual(dir(IronPythonTest).count('BindResult'), 1)

import IronPythonTest

AreEqual(dir(IronPythonTest).count('PythonFunc'), 1)
AreEqual(dir(IronPythonTest).count('BindResult'), 1)

id2 = id(IronPythonTest)

id3 = id(sys.modules['IronPythonTest'])

AreEqual(id1, id2)
AreEqual(id2, id3)
    ''')
    
    write_to_file(_f_mod, 'def PythonFunc(): pass')
    
    AreEqual(launch_ironpython_changing_extensions(_f_pkg1), 0)
    AreEqual(launch_ironpython_changing_extensions(_f_pkg2), 0)
    
    
    _imfp    = 'impmodfrmpkg'
    _f_imfp_init = path_combine(testpath.public_testdir, _imfp, "__init__.py")
    _f_imfp_mod  = path_combine(testpath.public_testdir, _imfp, "mod.py")
    _f_imfp_start = path_combine(testpath.public_testdir, "imfpstart.tpy")
    
    write_to_file(_f_imfp_init, "")
    write_to_file(_f_imfp_mod, "")
    write_to_file(_f_imfp_start, """
try:
    from impmodfrmpkg.mod import mod
except ImportError, e:
    pass
else:
    raise AssertionError("Import of mod from pkg.mod unexpectedly succeeded")
    """)
    
    AreEqual(launch_ironpython(_f_imfp_start), 0)
    
    # test import of package module with name bound in __init__.py
    write_to_file(_f_imfp_init, """
mod = 10
non_existent_mod = 20
""")
    write_to_file(_f_imfp_mod, """
value = "value in module"
""")
    write_to_file(_f_imfp_start, """
import impmodfrmpkg.mod as m
if m.value != "value in module":
    raise AssertionError("Failed to import nested module with name bound in __init__.py")
""")
    AreEqual(launch_ironpython(_f_imfp_start), 0)

    write_to_file(_f_imfp_start, """
try:
    import impmodfrmpkg.non_existent_mod as nm
except ImportError:
    pass
else:
    raise AssertionError("Import of impmodfrmpkg.non_existent_mod unexpectedly succeeded.")
""")
    AreEqual(launch_ironpython(_f_imfp_start), 0)

    write_to_file(_f_imfp_start, """
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
    AreEqual(launch_ironpython(_f_imfp_start), 0)

    _recimp = 'recimp'
    _f_recimp_init = path_combine(testpath.public_testdir, _recimp, "__init__.py")
    _f_recimp_a = path_combine(testpath.public_testdir, _recimp, "a.py")
    _f_recimp_b = path_combine(testpath.public_testdir, _recimp, "b.py")
    _f_recimp_start = path_combine(testpath.public_testdir, "recimpstart.tpy")
    
    write_to_file(_f_recimp_init, "from a import *")
    write_to_file(_f_recimp_a, "import b")
    write_to_file(_f_recimp_b, "import a")
    write_to_file(_f_recimp_start, "import recimp")
    
    AreEqual(launch_ironpython(_f_recimp_start), 0)

@skip("silverlight")
def test_import_inside_exec():
    _f_module = path_combine(testpath.public_testdir, 'another.py')
    write_to_file(_f_module, 'a1, a2, a3, _a4 = 1, 2, 3, 4')
    
    d = {}
    exec 'from another import a2' in d
    AssertInOrNot(d, ['a2'], ['a1', 'a3', '_a4', 'another'])
    AssertInOrNot(dir(), [], ['a1', 'a2', 'a3', '_a4', 'another'])
    
    d = {}
    exec 'from another import *' in d
    AssertInOrNot(d, ['a1', 'a2', 'a3'], ['_a4', 'another'])
    AssertInOrNot(dir(), [], ['a1', 'a2', 'a3', '_a4', 'another'])

    d = {}
    exec 'import another' in d
    AssertInOrNot(d, ['another'], ['a1', 'a2', 'a3', '_a4'])
    
    # Also a precondition for the following tests: ensure a1 a2 a3 are not in dict
    AssertInOrNot(dir(), [], ['a1', 'a2', 'a3', '_a4', 'another'])
    
    exec 'from another import a2'
    AssertInOrNot(dir(), ['a2'], ['a1', 'a3', '_a4'])
    
    exec 'from another import *'
    AssertInOrNot(dir(), ['a1', 'a2', 'a3'], ['_a4'])

@skip("silverlight")
def test___import___and_packages():
    try:
        mod_backup = dict(sys.modules)
        _f_module = path_combine(testpath.public_testdir, 'the_test.py')
        _f_dir    = path_combine(testpath.public_testdir, 'the_dir')
        _f_init   = path_combine(_f_dir, '__init__.py')
        _f_pkg_y  = path_combine(_f_dir, 'y.py')
        _f_y      = path_combine(testpath.public_testdir, 'y.py')
                
        # write the files
        ensure_directory_present(_f_dir)
        write_to_file(_f_module, 'import the_dir.y\n')
        write_to_file(_f_init, '')
        write_to_file(_f_pkg_y, 'a=1\ny = __import__("y")\nimport sys\n')
        write_to_file(_f_y, 'a=2\n')
        
        import y
        AreEqual(y.a, 2)
        
        sys.modules = mod_backup
        mod_backup = dict(sys.modules)
        
        y = __import__('y', globals(), locals())
        AreEqual(y.a, 2)
        
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_module)
        nt.unlink(_f_init)
        nt.unlink(_f_pkg_y)
        nt.unlink(_f_y)

@skip("silverlight", "multiple_execute")
def test_relative_imports():
    try:
        mod_backup = dict(sys.modules)
        _f_dir      = path_combine(testpath.public_testdir, 'the_dir')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_pkg_y    = path_combine(_f_dir, 'abc.py')
        _f_pkg_x    = path_combine(_f_dir, 'x.py')
        _f_subdir   = path_combine(_f_dir, 'subdir')
        _f_subinit  = path_combine(_f_subdir, '__init__.py')
        _f_subpkg_y = path_combine(_f_subdir, 'abc.py')
        _f_subpkg_x = path_combine(_f_subdir, 'x.py')
        _f_subpkg_z = path_combine(_f_subdir, 'z.py')
        _f_subpkg_a = path_combine(_f_subdir, 'a.py')
        _f_subpkg_b = path_combine(_f_subdir, 'b.py')
                
        # write the files
        ensure_directory_present(_f_dir)
        ensure_directory_present(_f_subdir)

        write_to_file(_f_init,    '')
        write_to_file(_f_subinit, '')
        write_to_file(_f_pkg_y,   'import sys\nsys.foo = "pkgy"')
        write_to_file(_f_subpkg_y,'import sys\nsys.foo = "subpkgy"')
        write_to_file(_f_pkg_x,    'from . import abc\nreload(abc)')
        write_to_file(_f_subpkg_x, 'from .. import abc\nreload(abc)')
        write_to_file(_f_subpkg_z, 'from . import abc\nreload(abc)')
        write_to_file(_f_subpkg_a, 'from __future__ import absolute_import\ntry:\n    import abc\nexcept ImportError:\n    import sys\n    sys.foo="error"')
        write_to_file(_f_subpkg_b, 'import abc\nreload(abc)')
                
        import the_dir.subdir.a
        if sys.winver=="2.5":
            AreEqual(sys.foo, 'error')
        else:
            Assert(not hasattr(sys, "foo"))
        
        import the_dir.x
        AreEqual(sys.foo, 'pkgy')
        
        import the_dir.subdir.x
        AreEqual(sys.foo, 'pkgy')
                
        import the_dir.subdir.z
        AreEqual(sys.foo, 'subpkgy')
                
        import the_dir.subdir.b
        AreEqual(sys.foo, 'subpkgy')
                
        del sys.foo
        
        _d_test     = 'OutterDir'
        _subdir     = 'RelTest'
        _f_o_init   = path_combine(testpath.public_testdir, _d_test, '__init__.py')
        _f_temp     = path_combine(testpath.public_testdir, _d_test, 'temp.py')
        _f_sub_init = path_combine(testpath.public_testdir, _d_test, _subdir, '__init__.py')
        
        write_to_file(_f_o_init, "from temp import foo1")
        
         
        write_to_file(_f_temp, '''
class foo1:
    def bar(self):
        return "foobar"

'''     )
        
        write_to_file(_f_sub_init, '''
from ..temp import foo1
'''     )

        from OutterDir import RelTest
        AreEqual(RelTest.foo1().bar(), 'foobar')
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_init)
        nt.unlink(_f_pkg_y)
        nt.unlink(_f_subinit)
        nt.unlink(_f_subpkg_y)

@skip("silverlight")
def test_import_globals():
    _f_dir      = path_combine(testpath.public_testdir, 'the_dir2')
    _f_x        = path_combine(_f_dir, 'x')
    _f_init     = path_combine(_f_x, '__init__.py')
    _f_dir_init = path_combine(_f_dir, '__init__.py')
    _f_x_y      = path_combine(_f_x, 'y.py')
    _f_y        = path_combine(_f_dir, 'y.py')
    _f_test     = path_combine(_f_dir, 'test.py')
    
    backup = dict(sys.modules)
    try:
        write_to_file(_f_init,    '')
        write_to_file(_f_dir_init,'')
        write_to_file(_f_x_y,   """
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
        write_to_file(_f_y,     'a = 2')
        write_to_file(_f_test,  'import x.y\n')
        
        import the_dir2.test
        AreEqual(sys.test1, 2)
        AreEqual(sys.test2, 1)
        AreEqual(sys.test2, 1)
        AreEqual(sys.test2, 1)
    finally:
        sys.modules = backup
        import nt
        nt.unlink(_f_init)
        nt.unlink(_f_dir_init)
        nt.unlink(_f_x_y)
        nt.unlink(_f_y)
        nt.unlink(_f_test)

@skip("silverlight", "multiple_execute")
def test_package_back_patching():
    """when importing a package item the package should be updated with the child"""
    try:
        mod_backup = dict(sys.modules)
        _f_dir      = path_combine(testpath.public_testdir, 'the_dir')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_pkg_abc  = path_combine(_f_dir, 'abc1.py')
        _f_pkg_xyz  = path_combine(_f_dir, 'xyz1.py')
                
        # write the files
        ensure_directory_present(_f_dir)

        write_to_file(_f_init,    'import abc1')
        write_to_file(_f_pkg_abc, 'import xyz1')
        write_to_file(_f_pkg_xyz, 'import sys\nsys.foo = "xyz"')
                
        import the_dir
        x, y = the_dir.abc1, the_dir.xyz1
        from the_dir import abc1
        from the_dir import xyz1
        
        AreEqual(x, abc1)
        AreEqual(y, xyz1)
        AreEqual(sys.foo, 'xyz')
                
        del sys.foo
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_init)
        nt.unlink(_f_pkg_abc)
        nt.unlink(_f_pkg_xyz)
        
    
#This cannot be placed in a test_* function as it uses 'from mod import *'
if not is_silverlight: #cp3194
    try:
        mod_backup = dict(sys.modules)
        _f_module = path_combine(testpath.public_testdir, 'the_test.py')
        
        # write the files
        write_to_file(_f_module, '''def foo(some_obj): return 3.14''')
        
        from the_test import *
        AreEqual(foo(None), 3.14)
        
        class Bar:
            foo = foo
        AreEqual(foo(None), Bar().foo())
        
    finally:
        sys.modules = mod_backup
        import nt
        nt.unlink(_f_module)
        
        
@skip("silverlight", "multiple_execute")
def test_pack_module_relative_collision():
    """when importing a package item the package should be updated with the child"""
    try:
        mod_backup = dict(sys.modules)
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_foo_dir  = path_combine(_f_dir, 'foo')
        _f_foo_py   = path_combine(_f_foo_dir, 'foo.py')
        _f_foo_init = path_combine(_f_foo_dir, '__init__.py')
                
        # write the files
        ensure_directory_present(_f_dir)
        ensure_directory_present(_f_foo_dir)

        write_to_file(_f_init,    'from foo import bar')
        write_to_file(_f_foo_py, 'bar = "BAR"')
        write_to_file(_f_foo_init, 'from foo import bar')
                
        import test_dir
        AreEqual(test_dir.bar, 'BAR')
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_foo_init)
        nt.unlink(_f_init)

@skip("silverlight", "multiple_execute")
def test_from_import_publishes_in_package():
    try:
        mod_backup = dict(sys.modules)
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir2')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_foo_py   = path_combine(_f_dir, 'foo.py')
                
        # write the files
        ensure_directory_present(_f_dir)

        write_to_file(_f_init,    'from foo import bar')
        write_to_file(_f_foo_py, 'bar = "BAR"')
                
        import test_dir2
        AreEqual(type(test_dir2.foo), type(sys))
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_init)

@skip("silverlight", "multiple_execute")
def test_from_import_publishes_in_package_relative():
    try:
        mod_backup = dict(sys.modules)
        print testpath.public_testdir
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir3')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_foo_py   = path_combine(_f_dir, 'foof.py')
                
        # write the files
        ensure_directory_present(_f_dir)

        write_to_file(_f_init,    'from .foof import bar')
        write_to_file(_f_foo_py, 'bar = "BxAR"')
                
        import test_dir3
        print test_dir3, dir(test_dir3)
        #print test_dir2.foof, dir(test_dir2.foof), test_dir2.foof.bar
        AreEqual(test_dir3.bar, 'BxAR')
        AreEqual(type(test_dir3.foof), type(sys))
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_init)

@skip("silverlight", "multiple_execute")
def test_from_import_publishes_in_package_relative():
    try:
        mod_backup = dict(sys.modules)
        print testpath.public_testdir
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir3')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_foo_py   = path_combine(_f_dir, 'foof.py')
                
        # write the files
        ensure_directory_present(_f_dir)

        write_to_file(_f_init,    'from .foof import bar')
        write_to_file(_f_foo_py, 'bar = "BxAR"')
                
        import test_dir3

        AreEqual(test_dir3.bar, 'BxAR')
        AreEqual(type(test_dir3.foof), type(sys))
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_init)

@skip("silverlight", "multiple_execute")
def test_from_import_publishes_in_package_relative_self():
    try:
        mod_backup = dict(sys.modules)
        print testpath.public_testdir
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir4')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_foo_py   = path_combine(_f_dir, 'foof.py')
                
        # write the files
        ensure_directory_present(_f_dir)

        write_to_file(_f_init,    'from . import foof')
        write_to_file(_f_foo_py, 'bar = "BxAR"')

        import test_dir4
        print test_dir4, dir(test_dir4)
        #AreEqual(test_dir4.bar, 'BxAR')
        AreEqual(type(test_dir4.foof), type(sys))
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_init)

@skip("silverlight")
def test_multiple_relative_imports_and_package():
    try:
        mod_backup = dict(sys.modules)
        print testpath.public_testdir
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir5')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_foo_py   = path_combine(_f_dir, 'foo5.py')
        _f_bar_py   = path_combine(_f_dir, 'bar5.py')
                
        # write the files
        ensure_directory_present(_f_dir)

        write_to_file(_f_init,    'from .foo5 import x\nfrom .bar5 import y')
        write_to_file(_f_foo_py, 'x = 42')
        write_to_file(_f_bar_py, 'y = 42')

        import test_dir5
        AreEqual(test_dir5.x, 42)
        AreEqual(test_dir5.y, 42)
    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_bar_py)
        nt.unlink(_f_init)

@skip("silverlight")
def test_cp34551():
    try:
        mod_backup = dict(sys.modules)
        print testpath.public_testdir
        _f_dir      = path_combine(testpath.public_testdir, 'test_dir6')
        _f_init     = path_combine(_f_dir, '__init__.py')
        _f_subdir   = path_combine(_f_dir, 'sub')
        _f_subinit  = path_combine(_f_subdir, '__init__.py')
        _f_foo_py   = path_combine(_f_subdir, 'foo.py')
                
        # write the files
        ensure_directory_present(_f_dir)
        ensure_directory_present(_f_subdir)

        write_to_file(_f_init,  """
from .sub.foo import bar
def baz():
    pass
""")
        write_to_file(_f_subinit, '')
        write_to_file(_f_foo_py, """
def bar():
    pass
""")

        from test_dir6 import baz
        import test_dir6
        Assert(getattr(test_dir6, 'sub', None) != None)

    finally:
        sys.modules = mod_backup
        nt.unlink(_f_foo_py)
        nt.unlink(_f_subinit)
        nt.unlink(_f_init)


#--MAIN------------------------------------------------------------------------
run_test(__name__)

# remove all test files
if not is_silverlight:
    delete_all_f(__name__)
