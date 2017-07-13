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

import unittest

from iptest import IronPythonTestCase, is_mono, is_netstandard, run_test, skipUnlessIronPython

@unittest.skipIf(is_netstandard, 'TODO')
@skipUnlessIronPython()
class AssemlbyTest(IronPythonTestCase):
    
    def test_assembly_instance(self):
        import clr
        mscorlib = clr.LoadAssemblyByName("mscorlib")

        #GetMemberNames
        self.assertEqual(len(dir(mscorlib)), 78)
        for x in ["System", "Microsoft"]:
            self.assertTrue( x in dir(mscorlib), "dir(mscorlib) does not have %s" % x)
        
        #GetBoundMember
        self.assertEqual(mscorlib.System.Int32(42), 42)
        self.assertRaises(AttributeError, lambda: mscorlib.NonExistentNamespace)

    def test_assemblybuilder_instance(self):
        import System
        name = System.Reflection.AssemblyName()
        name.Name = 'Test'
        assemblyBuilder = System.AppDomain.CurrentDomain.DefineDynamicAssembly(name, System.Reflection.Emit.AssemblyBuilderAccess.Run)
        
        asm_builder_dir = dir(assemblyBuilder)
        if is_mono: # Mono has another member
            self.assertEqual(len(asm_builder_dir), 90)
        else:
            self.assertEqual(len(asm_builder_dir), 89)
        self.assertTrue("GetCustomAttributesData" in asm_builder_dir)
            
        self.assertTrue("AddResourceFile" in asm_builder_dir)
        self.assertTrue("CreateInstance" in asm_builder_dir)
    
    def test_type(self):
        from System.Reflection import Assembly
        from System.Reflection.Emit import AssemblyBuilder
        mscorlib = Assembly.Load("mscorlib")
        self.assertTrue("Assembly" in repr(mscorlib))
        if is_mono: # Mono has another member
            self.assertEqual(len(dir(Assembly)), 76)
        else:
            self.assertEqual(len(dir(Assembly)), 75)
        if is_mono: # Mono has another member
            self.assertEqual(len(dir(AssemblyBuilder)), 90)
        else:
            self.assertEqual(len(dir(AssemblyBuilder)), 89)
        
run_test(__name__)