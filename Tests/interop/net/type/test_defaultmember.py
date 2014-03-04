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
Covers VB default indexer.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("defaultmembersvb", "defaultmemberscs", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.TypeSample import *
from Merlin.Testing.DefaultMemberSample import *

def test_vb_scenarios():
    '''vb supported scenarios '''
    x = ClassWithOverloadDefaultIndexer()
   
    for i in range(3):
        #x[i] = 2 * i
        #AreEqual(x[i], 2 * i)
    
        x.MyProperty[i] = 3 * i
        AreEqual(x.MyProperty[i], 3 * i)
    
    for i in range(2, 4):
        for j in range(6, 9):
            a = i + j

            #x[i, j] = a
            #AreEqual(a, x[i, j])
            
            #x.MyProperty[i, j] = a * 2
            #AreEqual(x.MyProperty[i, j], a * 2)
    
    # negative scenarios: incorrect argument count, type
    #x[()]
    #x[1, 2, 3]
    #x[x]

    # value type
    x = StructWithDefaultIndexer()
    x.Init()
    
    #x[1] = 1
    #print x.MyProperty[0]
    #x.MyProperty[1] = 1
    
    x = ClassWithNotExistingMember()
    # x[1] 
    # x[2] = 2
    AreEqual(x.MyProperty[1] , 0)
    x.MyProperty[1] = 10
    AreEqual(x.MyProperty[1] , 10)
    
    # interface declared with default indexer
    for t in [StructImplementsIDefaultIndexer, ClassImplementsIDefaultIndexer]:
        x = t()
        #AreEqual(x[5], 5)
        #x[5] = 6
        #Flag.Check(11)
        
        #IDefaultIndexer.MyProperty.GetValue(x, 5) 

    # try to leverage default member from the derived class: no
    x = DerivedClass()
    # x[2]
    # x[2] = 3
    x.MyProperty[2] = 4
    AreEqual(x.MyProperty[2], 4)
    
def test_cs_scenarios():
    x = ClassWithItem()
    AssertError(TypeError, lambda: x[1])
    x.Item = 2
    AreEqual(x.Item, 2)
    
    x = ClassWithset_Item()
    def f(): x[10] = 20
    AssertError(TypeError, f)
    x.set_Item(3)
    Flag.Check(3)
    
    x = ClassWithget_Item()
    def f(): return x[10]
    AssertError(TypeError, f)
    AreEqual(x[10, 20], 30)
    AssertError(TypeError, x.get_Item, 10)
    AreEqual(x.get_Item(10, 20), 30)
    
    # try other types
    x = ClassWithDefaultMemberCtor(1)

def test_cp_19510():
    """Test indexing on .NET classes with default members"""
    import clr
    clr.AddReference("System.Xml")
    import System.Xml
    
    doc = System.Xml.XmlDocument()
    doc.LoadXml('<tag attr="value">Data</tag>')
    root = doc.SelectSingleNode("tag")
    
    AreEqual(root.Attributes["attr"].Name, "attr")

run_test(__name__)

