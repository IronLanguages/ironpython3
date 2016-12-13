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

#------------------------------------------------------------------------------
#--Imports
from iptest.assert_util import *
skiptest("win32")
skiptest("silverlight")
if not is_net40:
    print "This test module should only be run from .NET 4.0!"
    sys.exit(0)

import sys
import clr
clr.AddReference("IronPythonTest")
import IronPythonTest.DynamicRegressions as DR

#------------------------------------------------------------------------------
#--Globals

#------------------------------------------------------------------------------
#--Test cases
def test_cp24117():
    AreEqual(DR.cp24117(xrange),    "IronPython.Runtime.Types.PythonType")
    AreEqual(DR.cp24117(xrange(3)), "IronPython.Runtime.XRange")

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

def test_cp24118():
    #TODO: once 26089 gets fixed the following needs actual verification.  That is,
    #right now DR.cp24118 just calls GetMethodTest without doing much validation.
    DR.cp24118(sys.modules[__name__])

def test_cp24115():
    class TestObj(object): pass
    DR.cp24115(TestObj())

def test_cp24111():
    class TestObj(object):
        def __init__(self, nz):
            self.nz = nz
        def __bool__(self):
            return self.nz

    for x in [0, 1]:
        AreEqual(DR.cp24111(TestObj(x)), not TestObj(x))

def test_cp24088():
    from IronPythonTest import DelegateTest
    AssertErrorWithMessage(Exception, "Operator '+=' cannot be applied to operands of type 'IronPython.Runtime.Types.ReflectedEvent.BoundEvent' and 'int'",
                           DR.cp24088, DelegateTest.Event)

cp24113_vb_snippet = '''
Imports System.Dynamic

Public Module CodePlex24113

    Public Function cp24113(ByVal test As Object)
        test("hello") = "hi"
        Return test("abc")
    End Function

End Module
'''

def test_cp24113():
    import os
    from iptest.process_util import run_vbc
    from iptest.assert_util import  testpath

    cp24113_vb_filename = testpath.temporary_dir + r"\cp24113_vb_module.vb"
    f = open(cp24113_vb_filename, "w")
    f.writelines(cp24113_vb_snippet)
    f.close()

    cp24113_vb_dllname  = testpath.temporary_dir + r"\cp24113_vb_dll"
    run_vbc("/target:library /out:%s %s" % (cp24113_vb_dllname, cp24113_vb_filename))
    clr.AddReferenceToFileAndPath(cp24113_vb_dllname)
    import CodePlex24113

    class TestObj(object):
        Prop = None
        def __getitem__(self, key): 
            return key
        def __setitem__(self, key, item): 
            self.Prop = item

    to = TestObj()
    AreEqual(to.Prop, None)
    AreEqual(CodePlex24113.cp24113(to), "abc")
    AreEqual(to.Prop, "hi")




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

def test_cp20519():
    import os
    import System
    from iptest.process_util import run_vbc
    from iptest.assert_util import  testpath

    cp20519_vb_filename = testpath.temporary_dir + r"\cp20519_vb_module.vb"
    f = open(cp20519_vb_filename, "w")
    f.writelines(cp20519_vb_snippet)
    f.close()

    cp20519_vb_exename  = testpath.temporary_dir + r"\cp20519_vb.exe"
    compile_cmd = "/target:exe /out:%s %s /reference:%s /reference:%s" % (cp20519_vb_exename, 
                                                                          cp20519_vb_filename,
                                                                          sys.exec_prefix + r"\IronPython.dll",
                                                                          sys.exec_prefix + r"\Microsoft.Scripting.dll")
    AreEqual(run_vbc(compile_cmd), 0)
    for x in [r"\IronPython.dll", r"\Microsoft.Scripting.dll", r"\Microsoft.Dynamic.dll"]:
        System.IO.File.Copy(sys.exec_prefix + x, testpath.temporary_dir + x, True)
    
    AreEqual(os.system(cp20519_vb_exename), 0)
    os.remove(cp20519_vb_exename)
    os.remove(cp20519_vb_filename)

#------------------------------------------------------------------------------
#--Main
run_test(__name__)