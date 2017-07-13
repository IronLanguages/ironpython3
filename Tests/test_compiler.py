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
import unittest
from iptest import IronPythonTestCase, is_cli, is_netstandard, run_test

@unittest.skipIf(not is_cli or is_netstandard, 'IronPython specific case, compiler not supported on netstandard')
class CompilerTest(IronPythonTestCase):
    
    def compileCode(self, name, *codeArr):
        import clr
        inputFiles = []
        counter = 0
        for code in codeArr:
            inputFile = os.path.join(self.temporary_dir, name + ("" if counter == 0 else str(counter)) + ".py")
            self.write_to_file(inputFile, code)
            inputFiles.append(inputFile)
            counter+=1
        dllFile = os.path.join(self.temporary_dir, name + ".dll")
        clr.CompileModules(dllFile, mainModule=inputFiles[0], *inputFiles)
        self.delete_files(*inputFiles)
        clr.AddReferenceToFileAndPath(dllFile)

    def compilePackage(self, packageName, codeDict):
        import clr
        packagePath = os.path.join(self.temporary_dir, packageName)
        self.ensure_directory_present(packagePath)
        fileList = []
        for fileName, code in codeDict.iteritems():
            filePath = os.path.join(packagePath, fileName)
            self.ensure_directory_present(os.path.dirname(filePath))
            self.write_to_file(filePath, code)
            fileList.append(filePath)
        dllFile = os.path.join(self.temporary_dir, packageName + ".dll")
        clr.CompileModules(dllFile, mainModule=fileList[0], *fileList)
        self.delete_files(*fileList)
        clr.AddReferenceToFileAndPath(dllFile)
        
############################ Tests ###################################################

    def test_simple(self):
        self.compileCode("simpleTest", "def f(): return 42")
                
        import simpleTest
        self.assertEqual(simpleTest.f(), 42)    

    def test_simple_dynsite(self):
        #containing a dynamic site.
        self.compileCode("simpleDynSiteTest", "def f(a , b): return a + b")
        
        import simpleDynSiteTest
        self.assertEqual(simpleDynSiteTest.f(2,3), 5)
    
    def test_syntax_error(self):
        self.assertRaises(SyntaxError, self.compileCode, "syntaxerrTest", "def f() pass")
        
    def test_runtime_error(self):
        self.compileCode("runtimeError", "def f(): print a")
        
        from runtimeError import f
        self.assertRaises(NameError, f)
    
    def test_multiple_files(self):
        self.compileCode("multiFiles", "def f(): return 42", "def g(): return 33")
        
        import multiFiles, multiFiles1
        self.assertEqual(multiFiles.f(), 42)
        self.assertEqual(multiFiles1.g(), 33)

    def test_multifile_import(self):
        self.compileCode("multiFileImport", "import multiFileImport1\ndef f(): return multiFileImport1.f()", "def f(): return 42")
        
        import multiFileImport
        self.assertEqual(multiFileImport.f(), 42)

    def test_multifile_import_external(self):
        self.compileCode("multiFileImportExternal", "import external\ndef f(): return external.f()")    
        self.write_to_file(os.path.join(self.temporary_dir, "external.py"), "def f(): return 'hello'")
        
        import multiFileImportExternal
        self.assertEqual(multiFileImportExternal.f(), 'hello')
    
    def test_load_order_builtins(self):
        self.compileCode("sys", "def f(): return 'hello'")
        import sys
        self.assertRaises(AttributeError, lambda: sys.f)

    def test_load_order_modfile(self):
        import clr
        fileName = os.path.join(self.temporary_dir,"loadOrderMod.py")
        dllName = os.path.join(self.temporary_dir,"loadOrderMod.dll")
        self.write_to_file(fileName, "def f(): return 'hello'")
        clr.CompileModules(dllName, fileName)
        self.write_to_file(fileName, "def f(): return 'bonjour'")
        clr.AddReferenceToFileAndPath(dllName)
        import loadOrderMod
        self.assertEqual(loadOrderMod.f(), 'hello')
    
    def test_exceptions(self):
        self.compileCode("exceptionsTest", "def f(): raise SystemError")

        import exceptionsTest
        self.assertRaises(SystemError, exceptionsTest.f)

    def test_package_init(self):
        self.compilePackage("initPackage", { "__init__.py" : "def f(): return 42" });
        
        import initPackage
        self.assertEqual(initPackage.f(), 42)

    def test_package_simple(self):
        self.compilePackage("simplePackage", { "__init__.py" : "import a\nimport b\ndef f(): return a.f() + b.f()",
                                        "a.py" : "def f() : return 10",
                                        "b.py" : "def f() : return 20"})
                                        
        import simplePackage
        self.assertEqual(simplePackage.f(), 30)       
        self.assertEqual(simplePackage.a.f(), 10)
        self.assertEqual(simplePackage.b.f(), 20)
    
    def test_package_subpackage(self):
        self.compilePackage("subPackage", { "__init__.py" : "import a\nimport b.c\ndef f(): return a.f() + b.c.f()",
                                    "a.py" : "def f(): return 10",
                                    "b/__init__.py" : "def f(): return 'kthxbye'",
                                    "b/c.py" : "def f(): return 20"})

        import subPackage
        self.assertEqual(subPackage.f(), 30)
        self.assertEqual(subPackage.b.f(), 'kthxbye')    
        self.assertEqual(subPackage.b.c.f(), 20)

    def test_package_subpackage_relative_imports(self):
        self.compilePackage("subPackage_relative", { "__init__.py" : "from foo import bar",
                                    "foo/__init__.py" : "from foo import bar",
                                    "foo/foo.py" : "bar = 'BAR'"})

        import subPackage_relative
        self.assertEqual(subPackage_relative.bar, 'BAR')

    #TODO add some more tests for main after this bug is fixed.
    def test_main(self):
        self.compileCode("mainTest", "def f(): return __name__")
        #this probably won't work. Need to verify once bug is fixed.
        import mainTest
        self.assertEqual(mainTest.f(), "mainTest")
    
    def test_empty_file(self):
        self.compileCode("emptyFile", "")
        import emptyFile
    
    def test_negative(self):
        import clr
        self.assertRaises(TypeError, clr.CompileModules, None, None)
        self.assertRaises(IOError, clr.CompileModules, "foo.dll", "ffoo.py")

    def test_overwrite(self):
        import clr
        self.write_to_file(os.path.join(self.temporary_dir, "overwrite.py"), "def foo(): return 'bar'")
        dllFile = os.path.join(self.temporary_dir, "overwrite.dll")
        clr.CompileModules(dllFile, os.path.join(self.temporary_dir, "overwrite.py"))
        self.write_to_file(os.path.join(self.temporary_dir, "overwrite1.py"), "def foo(): return 'boo'")
        clr.CompileModules(dllFile, os.path.join(self.temporary_dir, "overwrite1.py"))
        clr.AddReferenceToFileAndPath(dllFile)
        
        import overwrite1
        self.assertEqual(overwrite1.foo(), 'boo')


    def test_cyclic_modules(self):
        self.compileCode("cyclic_modules", "import cyclic_modules1\nA = 0", "import cyclic_modules\nA=1")
        
        import cyclic_modules
        self.assertEqual(cyclic_modules.A, 0)
        self.assertEqual(cyclic_modules.cyclic_modules1.A, 1)
        
        import cyclic_modules1
        self.assertEqual(cyclic_modules1.A, 1)
        self.assertEqual(cyclic_modules1.cyclic_modules.A, 0)

    def test_cyclic_pkg(self):
        self.compilePackage("cyclic_package", { "__init__.py" : "import cyclic_submodules0\nimport cyclic_submodules1",
                                        "cyclic_submodules0.py" : "import cyclic_package.cyclic_submodules1\nA = 2",
                                        "cyclic_submodules1.py" : "import cyclic_package.cyclic_submodules0\nA = 3"})
                                        
        import cyclic_package
        self.assertEqual(cyclic_package.cyclic_submodules0.A, 2)
        self.assertEqual(cyclic_package.cyclic_submodules0.cyclic_package.cyclic_submodules1.A, 3)
        
        self.assertEqual(cyclic_package.cyclic_submodules1.A, 3)
        self.assertEqual(cyclic_package.cyclic_submodules1.cyclic_package.cyclic_submodules0.A, 2)

    def test_system_core_cp20623(self):
        self.compileCode("cp20623", "import System\nA=System.DateTime(350000000).Second\nprint A")
        import cp20623
        self.assertEqual(cp20623.A, 35)
        #TODO: need to also generate a standalone exe from cp20623 and try running it

    def test_cp30178(self):
        self.compileCode("cp30178", 'mydict = { "a": ("Fail", "tuple") }')
        import cp30178
        self.assertEqual(cp30178.mydict, {'a' : ('Fail', 'tuple')})

run_test(__name__)

