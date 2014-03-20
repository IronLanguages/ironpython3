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
'''
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *

import clr
from System.Reflection import Assembly
from System.Reflection.Emit import AssemblyBuilder
    
def test_assembly_instance():
    mscorlib = clr.LoadAssemblyByName("mscorlib")
        
    #GetMemberNames
    Assert(len(dir(mscorlib)), 65)
    for x in ["System", "Microsoft"]:
        Assert( x in dir(mscorlib), "dir(mscorlib) does not have %s" % x)
    
    #GetBoundMember
    AreEqual(mscorlib.System.Int32(42), 42)    
    AssertError(AttributeError, lambda: mscorlib.NonExistentNamespace)

def test_assemblybuilder_instance():    
    name = System.Reflection.AssemblyName()
    name.Name = 'Test'
    assemblyBuilder = System.AppDomain.CurrentDomain.DefineDynamicAssembly(name, System.Reflection.Emit.AssemblyBuilderAccess.Run)    
    
    asm_builder_dir = dir(assemblyBuilder)
    if not is_net40:
        AreEqual(len(asm_builder_dir), 78)
        Assert("GetCustomAttributesData" not in asm_builder_dir)
    else:
        AreEqual(len(asm_builder_dir), 84)
        Assert("GetCustomAttributesData" in asm_builder_dir)
        
    Assert("AddResourceFile" in asm_builder_dir)
    Assert("CreateInstance" in asm_builder_dir)
    
def test_type():
    mscorlib = Assembly.Load("mscorlib")
    Assert("Assembly" in repr(mscorlib))  
    if not is_net40:
        AreEqual(len(dir(Assembly)), 65)
        AreEqual(len(dir(AssemblyBuilder)), 78)
    else:
        AreEqual(len(dir(Assembly)), 71)
        AreEqual(len(dir(AssemblyBuilder)), 84)
        
#####################################################################################
run_test(__name__)
#####################################################################################