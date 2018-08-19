# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
This module consists of regression tests for CodePlex and Dev10 IronPython bugs on
.NET 4.0's dynamic feature added primarily by IP developers that need to be 
folded into other test modules and packages.

Any test case added to this file should be of the form:
    def test_cp1234(): ...
where 'cp' refers to the fact that the test case is for a regression on CodePlex
(use 'dev10' for Dev10 bugs).  '1234' should refer to the CodePlex or Dev10
Work Item number.
"""

import sys
import unittest

from iptest import IronPythonTestCase, is_netcoreapp, is_mono, run_test, skipUnlessIronPython

#--CodePlex 24118--#
def GetMethodTest():
    return Method01()

class Method01(object):
    #Function 
    def Normal01(self, a, b):
        return a + b

    #Function 
    def Optional01(self, a, b=1):
        return a + b

cp24113_vb_snippet = '''
Imports System.Dynamic

Public Module CodePlex24113

    Public Function cp24113(ByVal test As Object)
        test("hello") = "hi"
        Return test("abc")
    End Function

End Module
'''

cp20519_vb_snippet = '''
Imports System
Imports IronPython.Hosting

Module CodePlex20519

    Sub Main()
        Dim py_run = Python.CreateRuntime()
        Dim py_eng = py_run.GetEngine("py")
        Dim scope1 = py_run.CreateScope()
        Dim py_code = "class C(object):" & vbNewLine & _
                        "    def __init__(self):" & vbNewLine & _
                        "        self.x = 42" & vbNewLine & _
                        "o = C()"

        py_eng.Execute(py_code, scope1)
        Dim o As Object = scope1.GetVariable("o")
        Dim o_one = o.x
        Dim o_two = o.x()
        If o_one <> o_two Then
            System.Console.WriteLine(o_one.ToString() & " does not equal " & o_two.ToString())
            System.Environment.Exit(1)
        ElseIf o_one <> 42 Then
            System.Console.WriteLine(o_one.ToString() & " does not equal 42")
            System.Environment.Exit(1)
        End If
    End Sub
End Module
'''

@unittest.skipIf(is_mono, 'Test currently has issues on mono')
@skipUnlessIronPython()
class DynamicRegressionTest(IronPythonTestCase):
    def setUp(self):
        super(DynamicRegressionTest, self).setUp()
        self.load_iron_python_test()

    def test_cp24117(self):
        import IronPythonTest.DynamicRegressions as DR
        self.assertEqual(DR.cp24117(xrange),    "IronPython.Runtime.Types.PythonType")
        self.assertEqual(DR.cp24117(xrange(3)), "IronPython.Runtime.XRange")

    def test_cp24118(self):
        import IronPythonTest.DynamicRegressions as DR
        #TODO: once 26089 gets fixed the following needs actual verification.  That is,
        #right now DR.cp24118 just calls GetMethodTest without doing much validation.
        DR.cp24118(sys.modules[__name__])

    def test_cp24115(self):
        import IronPythonTest.DynamicRegressions as DR
        class TestObj(object): pass
        DR.cp24115(TestObj())

    def test_cp24111(self):
        import IronPythonTest.DynamicRegressions as DR
        class TestObj(object):
            def __init__(self, nz):
                self.nz = nz
            def __nonzero__(self):
                return self.nz

        for x in [0, 1]:
            self.assertEqual(DR.cp24111(TestObj(x)), not TestObj(x))

    def test_cp24088(self):
        import IronPythonTest.DynamicRegressions as DR
        from IronPythonTest import DelegateTest
        self.assertRaisesMessage(Exception, "Operator '+=' cannot be applied to operands of type 'IronPython.Runtime.Types.ReflectedEvent.BoundEvent' and 'int'",
                            DR.cp24088, DelegateTest.Event)

    def test_cp24113(self):
        import clr
        import os
        
        cp24113_vb_filename = os.path.join(self.temporary_dir, "cp24113_vb_module.vb")
        f = open(cp24113_vb_filename, "w")
        f.writelines(cp24113_vb_snippet)
        f.close()

        cp24113_vb_dllname  = os.path.join(self.temporary_dir, "cp24113_vb_dll")
        self.run_vbc("/target:library /out:%s %s" % (cp24113_vb_dllname, cp24113_vb_filename))
        clr.AddReferenceToFileAndPath(cp24113_vb_dllname)
        import CodePlex24113

        class TestObj(object):
            Prop = None
            def __getitem__(self, key): 
                return key
            def __setitem__(self, key, item): 
                self.Prop = item

        to = TestObj()
        self.assertEqual(to.Prop, None)
        self.assertEqual(CodePlex24113.cp24113(to), "abc")
        self.assertEqual(to.Prop, "hi")

    @unittest.skipIf(is_netcoreapp, "can't compile snippet due to missing references")
    def test_cp20519(self):
        import os
        import System

        cp20519_vb_filename = os.path.join(self.temporary_dir, "cp20519_vb_module.vb")
        f = open(cp20519_vb_filename, "w")
        f.writelines(cp20519_vb_snippet)
        f.close()

        cp20519_vb_exename  = os.path.join(self.temporary_dir, "cp20519_vb.exe")
        compile_cmd = "/target:exe /out:%s %s /reference:%s /reference:%s /reference:%s" % (cp20519_vb_exename,
                                                                            cp20519_vb_filename,
                                                                            os.path.join(sys.exec_prefix, "IronPython.dll"),
                                                                            os.path.join(sys.exec_prefix, "Microsoft.Scripting.dll"),
                                                                            os.path.join(sys.exec_prefix, "Microsoft.Dynamic.dll"))
        self.assertEqual(self.run_vbc(compile_cmd), 0)
        for x in ["IronPython.dll", "Microsoft.Scripting.dll", "Microsoft.Dynamic.dll"]:
            System.IO.File.Copy(os.path.join(sys.exec_prefix, x), os.path.join(self.temporary_dir, x), True)
        
        self.assertEqual(os.system(cp20519_vb_exename), 0)
        os.remove(cp20519_vb_exename)
        os.remove(cp20519_vb_filename)

run_test(__name__)