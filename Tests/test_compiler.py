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
skiptest("silverlight")
skiptest("win32")
skiptest("multiple_execute")

import clr
from System import Array
from System.IO import Path

###########################   Helpers ###############################################
def compileCode(name, *codeArr):
    inputFiles = []
    counter = 0
    for code in codeArr:
        inputFile = path_combine(testpath.temporary_dir, name + ("" if counter == 0 else str(counter)) + ".py")
        write_to_file(inputFile, code)
        inputFiles.append(inputFile)
        counter+=1
    dllFile = path_combine(testpath.temporary_dir, name + ".dll")
    clr.CompileModules(dllFile, mainModule=inputFiles[0], *inputFiles)
    delete_files(*inputFiles)
    clr.AddReferenceToFileAndPath(dllFile)

def compilePackage(packageName, codeDict):
    packagePath = path_combine(testpath.temporary_dir, packageName)
    ensure_directory_present(packagePath)
    fileList = []
    for fileName, code in codeDict.items():
        filePath = path_combine(packagePath, fileName)        
        ensure_directory_present(Path.GetDirectoryName(filePath))
        write_to_file(filePath, code)
        fileList.append(filePath)
    dllFile = path_combine(testpath.temporary_dir, packageName + ".dll")
    clr.CompileModules(dllFile, mainModule=fileList[0], *fileList)
    delete_files(*fileList)
    clr.AddReferenceToFileAndPath(dllFile)
        
############################ Tests ###################################################

def test_simple():    
    compileCode("simpleTest", "def f(): return 42")
            
    import simpleTest
    AreEqual(simpleTest.f(), 42)    

def test_simple_dynsite():   
    #containing a dynamic site.
    compileCode("simpleDynSiteTest", "def f(a , b): return a + b")
    
    import simpleDynSiteTest
    AreEqual(simpleDynSiteTest.f(2,3), 5)
    
def test_synatx_error():
    AssertError(SyntaxError, compileCode, "syntaxerrTest", "def f() pass")
        
def test_runtime_error():
    compileCode("runtimeError", "def f(): print a")
    
    from runtimeError import f
    AssertError(NameError, f)
    
def test_multiple_files():
    compileCode("multiFiles", "def f(): return 42", "def g(): return 33")
    
    import multiFiles, multiFiles1
    AreEqual(multiFiles.f(), 42)
    AreEqual(multiFiles1.g(), 33)

def test_multifile_import():
    compileCode("multiFileImport", "import multiFileImport1\ndef f(): return multiFileImport1.f()", "def f(): return 42")
    
    import multiFileImport
    AreEqual(multiFileImport.f(), 42)

def test_multifile_import_external():
    compileCode("multiFileImportExternal", "import external\ndef f(): return external.f()")    
    write_to_file(path_combine(testpath.temporary_dir, "external.py"), "def f(): return 'hello'")
    
    import multiFileImportExternal
    AreEqual(multiFileImportExternal.f(), 'hello')
    
def test_load_order_builtins():
    compileCode("sys", "def f(): return 'hello'")
    import sys
    AssertError(AttributeError, lambda: sys.f)

def test_load_order_modfile():
    fileName = path_combine(testpath.temporary_dir,"loadOrderMod.py")
    dllName = path_combine(testpath.temporary_dir,"loadOrderMod.dll")
    write_to_file(fileName, "def f(): return 'hello'")
    clr.CompileModules(dllName, fileName)
    write_to_file(fileName, "def f(): return 'bonjour'")
    clr.AddReferenceToFileAndPath(dllName)
    import loadOrderMod
    AreEqual(loadOrderMod.f(), 'hello')
    
def test_exceptions():
    compileCode("exceptionsTest", "def f(): raise SystemError")
    
    import exceptionsTest
    AssertError(SystemError, exceptionsTest.f)

def test_package_init():
    compilePackage("initPackage", { "__init__.py" : "def f(): return 42" });
    
    import initPackage
    AreEqual(initPackage.f(), 42)

def test_package_simple():
    compilePackage("simplePackage", { "__init__.py" : "import a\nimport b\ndef f(): return a.f() + b.f()",
                                      "a.py" : "def f() : return 10",
                                      "b.py" : "def f() : return 20"})
                                      
    import simplePackage
    AreEqual(simplePackage.f(), 30)       
    AreEqual(simplePackage.a.f(), 10)
    AreEqual(simplePackage.b.f(), 20)
    
def test_package_subpackage():
    compilePackage("subPackage", { "__init__.py" : "import a\nimport b.c\ndef f(): return a.f() + b.c.f()",
                                   "a.py" : "def f(): return 10",
                                   "b\\__init__.py" : "def f(): return 'kthxbye'",
                                   "b\\c.py" : "def f(): return 20"})

    import subPackage
    AreEqual(subPackage.f(), 30)
    AreEqual(subPackage.b.f(), 'kthxbye')    
    AreEqual(subPackage.b.c.f(), 20)

def test_package_subpackage_relative_imports():
    compilePackage("subPackage_relative", { "__init__.py" : "from foo import bar",
                                   "foo\\__init__.py" : "from foo import bar",
                                   "foo\\foo.py" : "bar = 'BAR'"})

    import subPackage_relative
    AreEqual(subPackage_relative.bar, 'BAR')

#TODO add some more tests for main after this bug is fixed.
def test_main():
    compileCode("mainTest", "def f(): return __name__")
    #this probably won't work. Need to verify once bug is fixed.
    import mainTest
    AreEqual(mainTest.f(), "mainTest")
    
#-------------------- P2 scenarios --------------------------------------------------------------------------

def test_empty_file():
    compileCode("emptyFile", "")
    import emptyFile
    
def test_negative():
    AssertError(TypeError, clr.CompileModules, None, None)
    AssertError(IOError, clr.CompileModules, "foo.dll", "ffoo.py")

def test_overwrite():    
    write_to_file(path_combine(testpath.temporary_dir, "overwrite.py"), "def foo(): return 'bar'")
    dllFile = path_combine(testpath.temporary_dir, "overwrite.dll")
    clr.CompileModules(dllFile, path_combine(testpath.temporary_dir, "overwrite.py"))
    write_to_file(path_combine(testpath.temporary_dir, "overwrite1.py"), "def foo(): return 'boo'")
    clr.CompileModules(dllFile, path_combine(testpath.temporary_dir, "overwrite1.py"))
    clr.AddReferenceToFileAndPath(dllFile)
    
    import overwrite1
    AreEqual(overwrite1.foo(), 'boo')


def test_cyclic_modules():
    compileCode("cyclic_modules", "import cyclic_modules1\nA = 0", "import cyclic_modules\nA=1")
    
    import cyclic_modules
    AreEqual(cyclic_modules.A, 0)
    AreEqual(cyclic_modules.cyclic_modules1.A, 1)
    
    import cyclic_modules1
    AreEqual(cyclic_modules1.A, 1)
    AreEqual(cyclic_modules1.cyclic_modules.A, 0)

def test_cyclic_pkg():
    compilePackage("cyclic_package", { "__init__.py" : "import cyclic_submodules0\nimport cyclic_submodules1",
                                      "cyclic_submodules0.py" : "import cyclic_package.cyclic_submodules1\nA = 2",
                                      "cyclic_submodules1.py" : "import cyclic_package.cyclic_submodules0\nA = 3"})
                                      
    import cyclic_package
    AreEqual(cyclic_package.cyclic_submodules0.A, 2)
    AreEqual(cyclic_package.cyclic_submodules0.cyclic_package.cyclic_submodules1.A, 3)
    
    AreEqual(cyclic_package.cyclic_submodules1.A, 3)
    AreEqual(cyclic_package.cyclic_submodules1.cyclic_package.cyclic_submodules0.A, 2)

def test_system_core_cp20623():
    compileCode("cp20623", "import System\nA=System.DateTime(350000000).Second\nprint A")
    import cp20623
    AreEqual(cp20623.A, 35)
    #TODO: need to also generate a standalone exe from cp20623 and try running it

def test_cp30178():
    compileCode("cp30178", 'mydict = { "a": ("Fail", "tuple") }')
    import cp30178
    AreEqual(cp30178.mydict, {'a' : ('Fail', 'tuple')})

#------------------------------------------------------------------------------        
run_test(__name__)
