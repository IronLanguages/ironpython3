# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, is_mono, is_osx, is_posix, run_test, skipUnlessIronPython
from shutil import copyfile

@skipUnlessIronPython()
class ClrLoadTest(IronPythonTestCase):

    def setUp(self):
        super(ClrLoadTest, self).setUp()
        self.load_iron_python_test()

    def test_loadtest(self):
        import IronPythonTest.LoadTest as lt
        self.assertEqual(lt.Name1.Value, lt.Values.GlobalName1)
        self.assertEqual(lt.Name2.Value, lt.Values.GlobalName2)
        self.assertEqual(lt.Nested.Name1.Value, lt.Values.NestedName1)
        self.assertEqual(lt.Nested.Name2.Value, lt.Values.NestedName2)

    def test_negative_assembly_names(self):
        import clr
        self.assertRaises(IOError, clr.AddReferenceToFileAndPath, os.path.join(self.test_dir, 'this_file_does_not_exist.dll'))
        self.assertRaises(IOError, clr.AddReferenceToFileAndPath, os.path.join(self.test_dir, 'this_file_does_not_exist.dll'))
        self.assertRaises(IOError, clr.AddReferenceToFileAndPath, os.path.join(self.test_dir, 'this_file_does_not_exist.dll'))
        self.assertRaises(IOError, clr.AddReferenceByName, 'bad assembly name', 'WellFormed.But.Nonexistent, Version=9.9.9.9, Culture=neutral, PublicKeyToken=deadbeefdeadbeef, processorArchitecture=6502')
        self.assertRaises(SystemError, clr.AddReference, 'this_assembly_does_not_exist_neither_by_file_name_nor_by_strong_name')

        self.assertRaises(TypeError, clr.AddReference, 35)

        for method in [
            clr.AddReference,
            clr.AddReferenceToFile,
            clr.AddReferenceToFileAndPath,
            clr.AddReferenceByName,
            clr.AddReferenceByPartialName,
            clr.LoadAssemblyFromFileWithPath,
            clr.LoadAssemblyFromFile,
            clr.LoadAssemblyByName,
            clr.LoadAssemblyByPartialName,
            ]:
            self.assertRaises(TypeError, method, None)

        for method in [
            clr.AddReference,
            clr.AddReferenceToFile,
            clr.AddReferenceToFileAndPath,
            clr.AddReferenceByName,
            clr.AddReferenceByPartialName,
            ]:
            self.assertRaises(TypeError, method, None, None)

        import System
        self.assertRaises(ValueError, clr.LoadAssemblyFromFile, System.IO.Path.DirectorySeparatorChar)
        self.assertRaises(ValueError, clr.LoadAssemblyFromFile, '')

    def test_get_type(self):
        import clr
        self.assertEqual(clr.GetClrType(None), None)
        self.assertRaises(TypeError, clr.GetPythonType, None)

    #TODO:@skip("multiple_execute")
    def test_ironpythontest_from_alias(self):
        IPTestAlias = self.load_iron_python_test(True)
        self.assertEqual(dir(IPTestAlias).count('IronPythonTest'), 1)

    def test_references(self):
        import clr
        refs = clr.References
        atuple = refs + (clr.GetClrType(int).Assembly, ) # should be able to append to references_tuple
        #self.assertRaises(TypeError, refs.__add__, "I am not a tuple")

        s = str(refs)
        temp = ',' + os.linesep
        self.assertEqual(s, '(' + temp.join(map((lambda x:'<'+x.ToString()+'>'), refs)) + ')' + os.linesep)

    @unittest.skipIf(is_netcoreapp, "no GAC")
    def test_gac(self):
        import clr
        import System
        def get_gac():
                process = System.Diagnostics.Process()
                if is_osx:
                    process.StartInfo.FileName = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/gacutil"
                elif is_posix:
                    process.StartInfo.FileName = "/usr/bin/gacutil"
                else:
                    process.StartInfo.FileName = System.IO.Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "gacutil.exe")
                process.StartInfo.Arguments = "/nologo /l"
                process.StartInfo.CreateNoWindow = True
                process.StartInfo.UseShellExecute = False
                process.StartInfo.RedirectStandardInput = True
                process.StartInfo.RedirectStandardOutput = True
                process.StartInfo.RedirectStandardError = True
                try:
                    process.Start()
                except WindowsError:
                    return []
                result = process.StandardOutput.ReadToEnd()
                process.StandardError.ReadToEnd()
                process.WaitForExit()
                if process.ExitCode == 0:
                    try:
                        divByNewline = result.split(newline + '  ')[1:]
                        divByNewline[-1] = divByNewline[-1].split(newline + newline)[0]
                        return divByNewline
                    except Exception:
                        return []
                return []

        gaclist = get_gac()
        if (len(gaclist) > 0):
            clr.AddReferenceByName(gaclist[-1])


    def test_nonamespaceloadtest(self):
        import NoNamespaceLoadTest
        a = NoNamespaceLoadTest()
        self.assertEqual(a.HelloWorld(), 'Hello World')

    #TODO:@skip("multiple_execute")
    def test_addreferencetofileandpath_conflict(self):
        """verify AddReferenceToFileAndPath picks up the path specified, not some arbitrary assembly somewhere in your path already"""
        code1 = """
    using System;

    public class CollisionTest {
        public static string Result(){
            return "Test1";
        }
    }
    """

        code2 = """
    using System;

    public class CollisionTest {
        public static string Result(){
            return "Test2";
        }
    }
    """
        import clr
        tmp = self.temporary_dir

        test1_cs, test1_dll = os.path.join(tmp, 'test1.cs'), os.path.join(tmp, 'CollisionTest.dll')
        test2_cs, test2_dll = os.path.join(tmp, 'test2.cs'), os.path.join(sys.prefix, 'CollisionTest.dll')

        self.write_to_file(test1_cs, code1)
        self.write_to_file(test2_cs, code2)

        self.assertEqual(self.run_csc("/nologo /target:library /out:" + test2_dll + ' ' + test2_cs), 0)
        self.assertEqual(self.run_csc("/nologo /target:library /out:" + test1_dll + ' ' + test1_cs), 0)

        clr.AddReferenceToFileAndPath(test1_dll)
        import CollisionTest
        self.assertEqual(CollisionTest.Result(), "Test1")

    #TODO:@skip("multiple_execute")
    def test_addreferencetofile_verification(self):
        import clr
        tmp = self.temporary_dir
        sys.path.append(tmp)

        code1 = """
    using System;

    public class test1{
        public static string Test1(){
            test2 t2 = new test2();
            return t2.DoSomething();
        }

        public static string Test2(){
            return "test1.test2";
        }
    }
    """

        code2 = """
    using System;

    public class test2{
        public string DoSomething(){
            return "hello world";
        }
    }
    """

        test1_dll_along_with_ipy = os.path.join(sys.prefix, 'test1.dll') # this dll is need for peverify

        # delete the old test1.dll if exists
        self.delete_files(test1_dll_along_with_ipy)

        test1_cs, test1_dll = os.path.join(tmp, 'test1.cs'), os.path.join(tmp, 'test1.dll')
        test2_cs, test2_dll = os.path.join(tmp, 'test2.cs'), os.path.join(tmp, 'test2.dll')

        self.write_to_file(test1_cs, code1)
        self.write_to_file(test2_cs, code2)

        self.assertEqual(self.run_csc("/nologo /target:library /out:"+ test2_dll + ' ' + test2_cs), 0)
        self.assertEqual(self.run_csc("/nologo /target:library /r:" + test2_dll + " /out:" + test1_dll + ' ' + test1_cs), 0)

        clr.AddReferenceToFile('test1')

        self.assertEqual(len([x for x in clr.References if x.FullName.startswith("test1")]), 1)

        # test 2 shouldn't be loaded yet...
        self.assertEqual(len([x for x in clr.References if x.FullName.startswith("test2")]), 0)

        import test1
        # should create test1 (even though we're a top-level namespace)
        a = test1()
        self.assertEqual(a.Test2(), 'test1.test2')
        # should load test2 from path
        self.assertEqual(a.Test1(), 'hello world')
        self.assertEqual(len([x for x in clr.References if x.FullName.startswith("test2")]), 0)

        # this is to make peverify happy, apparently snippetx.dll referenced to test1
        copyfile(test1_dll, test1_dll_along_with_ipy)

    #TODO: @skip("multiple_execute")
    @unittest.skipIf(is_mono, "mono may have a bug here...need to investigate https://github.com/IronLanguages/main/issues/1595")
    @unittest.skipIf(is_netcoreapp, "TODO: figure out")
    def test_assembly_resolve_isolation(self):
        import clr, os
        clr.AddReference("IronPython")
        clr.AddReference("Microsoft.Scripting")
        from IronPython.Hosting import Python
        from Microsoft.Scripting import SourceCodeKind
        tmp = self.temporary_dir
        tmp1 = os.path.join(tmp, 'resolve1')
        tmp2 = os.path.join(tmp, 'resolve2')

        if not os.path.exists(tmp1):
            os.mkdir(tmp1)
        if not os.path.exists(tmp2):
            os.mkdir(tmp2)

        code1a = """
using System;

public class ResolveTestA {
    public static string Test() {
        ResolveTestB test = new ResolveTestB();
        return test.DoSomething();
    }
}
    """

        code1b = """
using System;

public class ResolveTestB {
    public string DoSomething() {
        return "resolve test 1";
    }
}
    """

        code2a = """
using System;

public class ResolveTestA {
    public static string Test() {
        ResolveTestB test = new ResolveTestB();
        return test.DoSomething();
    }
}
    """

        code2b = """
using System;

public class ResolveTestB {
    public string DoSomething() {
        return "resolve test 2";
    }
}
    """

        script_code = """import clr
clr.AddReferenceToFile("ResolveTestA")
from ResolveTestA import Test
result = Test()
    """

        test1a_cs, test1a_dll, test1b_cs, test1b_dll = map(
            lambda x: os.path.join(tmp1, x),
            ['ResolveTestA.cs', 'ResolveTestA.dll', 'ResolveTestB.cs', 'ResolveTestB.dll']
        )

        test2a_cs, test2a_dll, test2b_cs, test2b_dll = map(
            lambda x: os.path.join(tmp2, x),
            ['ResolveTestA.cs', 'ResolveTestA.dll', 'ResolveTestB.cs', 'ResolveTestB.dll']
        )

        self.write_to_file(test1a_cs, code1a)
        self.write_to_file(test1b_cs, code1b)
        self.write_to_file(test2a_cs, code2a)
        self.write_to_file(test2b_cs, code2b)

        self.assertEqual(self.run_csc("/nologo /target:library /out:" + test1b_dll + ' ' + test1b_cs), 0)
        self.assertEqual(self.run_csc("/nologo /target:library /r:" + test1b_dll + " /out:" + test1a_dll + ' ' + test1a_cs), 0)
        self.assertEqual(self.run_csc("/nologo /target:library /out:" + test2b_dll + ' ' + test2b_cs), 0)
        self.assertEqual(self.run_csc("/nologo /target:library /r:" + test2b_dll + " /out:" + test2a_dll + ' ' + test2a_cs), 0)

        engine1 = Python.CreateEngine()
        paths1 = engine1.GetSearchPaths()
        paths1.Add(tmp1)
        engine1.SetSearchPaths(paths1)
        scope1 = engine1.CreateScope()
        script1 = engine1.CreateScriptSourceFromString(script_code, SourceCodeKind.Statements)
        script1.Execute(scope1)
        result1 = scope1.GetVariable("result")
        self.assertEqual(result1, "resolve test 1")

        engine2 = Python.CreateEngine()
        paths2 = engine2.GetSearchPaths()
        paths2.Add(tmp2)
        engine2.SetSearchPaths(paths2)
        scope2 = engine2.CreateScope()
        script2 = engine2.CreateScriptSourceFromString(script_code, SourceCodeKind.Statements)
        script2.Execute(scope2)
        result2 = scope2.GetVariable("result")
        self.assertEqual(result2, "resolve test 2")

    def test_addreference_sanity(self):
        import clr
        # add reference directly to assembly
        clr.AddReference(''.GetType().Assembly)
        # add reference via partial name
        clr.AddReference('System.Xml')
        # add a reference via a fully qualified name
        clr.AddReference(''.GetType().Assembly.FullName)

    def get_local_filename(self, base):
        if __file__.count(os.sep):
            return os.path.join(__file__.rsplit(os.sep, 1)[0], base)
        else:
            return base

    def compileAndLoad(self, name, filename, *args):
        import clr
        sys.path.append(sys.exec_prefix)
        self.assertEqual(self.run_csc("/nologo /t:library " + ' '.join(args) + " /out:\"" + os.path.join(sys.exec_prefix, name +".dll") + "\" \"" + filename + "\""), 0)
        return clr.LoadAssemblyFromFile(name)

    #TODO: @skip("multiple_execute")
    def test_classname_same_as_ns(self):
        import clr
        sys.path.append(sys.exec_prefix)
        self.assertEqual(self.run_csc("/nologo /t:library /out:\"" + os.path.join(sys.exec_prefix, "c4.dll") + "\" \"" + self.get_local_filename('c4.cs') + "\""), 0)
        clr.AddReference("c4")
        import c4
        self.assertTrue(not c4 is c4.c4)
        self.assertTrue(c4!=c4.c4)

    #TODO: @skip("multiple_execute")
    def test_local_dll(self):
        x = self.compileAndLoad('c3', self.get_local_filename('c3.cs') )

        self.assertEqual(repr(x), "<Assembly c3, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>")
        self.assertEqual(repr(x.Foo), "<class 'Foo'>")
        self.assertEqual(repr(x.BarNamespace), "<module 'BarNamespace' (CLS module from c3, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null)>")
        self.assertEqual(repr(x.BarNamespace.NestedNamespace), "<module 'NestedNamespace' (CLS module from c3, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null)>")
        self.assertEqual(repr(x.BarNamespace.Bar.NestedBar), "<class 'NestedBar'>")
        self.assertEqual(x.__dict__["BarNamespace"], x.BarNamespace)
        self.assertEqual(x.BarNamespace.__dict__["Bar"], x.BarNamespace.Bar)
        self.assertEqual(x.BarNamespace.__dict__["NestedNamespace"], x.BarNamespace.NestedNamespace)
        self.assertEqual(x.BarNamespace.NestedNamespace.__name__, "NestedNamespace")
        self.assertEqual(x.BarNamespace.NestedNamespace.__file__, "c3, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
        self.assertRaises(AttributeError, lambda: x.BarNamespace.NestedNamespace.not_exist)
        self.assertRaises(AttributeError, lambda: x.Foo2)  # assembly c3 has no type Foo2
        self.assertTrue(set(['NestedNamespace', 'Bar']) <= set(dir(x.BarNamespace)))

        def f(): x.BarNamespace.Bar = x.Foo
        self.assertRaises(AttributeError, f)

        def f(): del x.BarNamespace.NotExist
        self.assertRaises(AttributeError, f)

        def f(): del x.BarNamespace
        self.assertRaises(AttributeError, f)

    #TODO:@skip("multiple_execute")
    @unittest.skipIf(is_netcoreapp, "TODO: figure out")
    def test_namespaceimport(self):
        import clr
        tmp = self.temporary_dir
        if tmp not in sys.path:
            sys.path.append(tmp)

        code1 = "namespace TestNamespace { public class Test1 {} }"
        code2 = "namespace TestNamespace { public class Test2 {} }"

        test1_cs, test1_dll = os.path.join(tmp, 'testns1.cs'), os.path.join(tmp, 'testns1.dll')
        test2_cs, test2_dll = os.path.join(tmp, 'testns2.cs'), os.path.join(tmp, 'testns2.dll')

        self.write_to_file(test1_cs, code1)
        self.write_to_file(test2_cs, code2)

        self.assertEqual(self.run_csc("/nologo /target:library /out:"+ test1_dll + ' ' + test1_cs), 0)
        self.assertEqual(self.run_csc("/nologo /target:library /out:"+ test2_dll + ' ' + test2_cs), 0)

        clr.AddReference('testns1')
        import TestNamespace
        self.assertEqual(dir(TestNamespace), ['Test1'])
        clr.AddReference('testns2')
        # verify that you don't need to import TestNamespace again to see Test2
        self.assertEqual(dir(TestNamespace), ['Test1', 'Test2'])

    def test_no_names_provided(self):
        import clr
        self.assertRaises(TypeError, clr.AddReference, None)
        self.assertRaises(TypeError, clr.AddReferenceToFile, None)
        self.assertRaises(TypeError, clr.AddReferenceByName, None)
        self.assertRaises(TypeError, clr.AddReferenceByPartialName, None)
        self.assertRaises(ValueError, clr.AddReference)
        self.assertRaises(ValueError, clr.AddReferenceToFile)
        self.assertRaises(ValueError, clr.AddReferenceByName)
        self.assertRaises(ValueError, clr.AddReferenceByPartialName)

    #TODO: @skip("multiple_execute")
    def test_load_count(self):
        # verify loading an assembly updates the assembly-loaded count in the repr
        # if a new assembly gets loaded before this that contains System both numbers
        # need to be updated
        import clr, System
        before = repr(System)
        if is_netcoreapp:
            clr.AddReference('System.Drawing.Primitives')
        else:
            clr.AddReference('System.Drawing')
        after = repr(System)

        # Strip common substring from start and end
        start = 0; end = 1
        while before[start] == after[start]: start += 1
        while before[-end] == after[-end]: end += 1
        end -= 1;

        # what remains is an int - number of assemblies loaded.
        # The integer must have increased value by 1
        self.assertEqual(int(before[start:-end]) + 1, int(after[start:-end]))

run_test(__name__)
